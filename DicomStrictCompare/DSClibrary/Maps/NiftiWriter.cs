using System;
using System.IO;
using System.Text;

namespace DSClibrary.Maps
{
    /// <summary>
    /// Writes a float volume as a single-file NIfTI-1 (.nii), the format NiiVue and most medical imaging tools read.
    /// The voxel order is the dose grid's (x fastest, then y, then frames), so values are written as they are. The
    /// orientation (sform and qform, both set) maps a voxel into a <see cref="BeamFrame"/>, in mm, rather than into the
    /// patient's RAS: a viewer's axial view is then the plane across the beam, its coronal the X jaws against depth.
    /// </summary>
    public static class NiftiWriter
    {
        private const int HeaderSize = 348;

        /// <param name="values">One float per voxel; NaN where nothing was evaluated.</param>
        /// <param name="grid">The grid the values lie on, for its shape, origin and spacing.</param>
        /// <param name="frame">The frame to show it in.</param>
        /// <param name="description">Up to 79 characters, kept in the header.</param>
        public static void Write(string path, float[] values, DoseMatrixOptimal grid, BeamFrame frame, string description = "")
        {
            using var stream = File.Create(path);
            Write(stream, values, grid, frame, description);
        }

        public static void Write(Stream stream, float[] values, DoseMatrixOptimal grid, BeamFrame frame, string description = "")
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            if (grid is null) throw new ArgumentNullException(nameof(grid));
            if (frame is null) throw new ArgumentNullException(nameof(frame));
            int nx = grid.DimensionX, ny = grid.DimensionY, nz = grid.DimensionZ;
            if (values.Length != (long)nx * ny * nz) throw new ArgumentException($"{values.Length} values for a {nx}x{ny}x{nz} grid");
            if (nx > short.MaxValue || ny > short.MaxValue || nz > short.MaxValue) throw new ArgumentException("NIfTI-1 axes stop at 32767 voxels");

            var (affine, quaternion, offset) = Orientation(grid, frame);
            var h = new byte[HeaderSize + 4];   // the header, then the 4-byte extension flag (none)
            var b = new BinaryWriter(new MemoryStream(h), Encoding.ASCII);
            Put(b, 0, HeaderSize);
            b.Seek(40, SeekOrigin.Begin);                       // dim
            foreach (short d in new short[] { 3, (short)nx, (short)ny, (short)nz, 1, 1, 1, 1 }) b.Write(d);
            b.Seek(70, SeekOrigin.Begin); b.Write((short)16);   // datatype: float32
            b.Write((short)32);                                 // bitpix
            b.Seek(76, SeekOrigin.Begin);                       // pixdim: qfac, then spacing
            foreach (float p in new[] { 1f, (float)grid.XRes, (float)grid.YRes, (float)grid.ZRes, 0, 0, 0, 0 }) b.Write(p);
            b.Write((float)(HeaderSize + 4));                   // vox_offset
            b.Write(1f); b.Write(0f);                           // scl_slope, scl_inter: values as stored
            b.Seek(123, SeekOrigin.Begin); b.Write((byte)2);    // xyzt_units: mm
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (float v in values) { if (float.IsNaN(v)) continue; if (v < min) min = v; if (v > max) max = v; }
            b.Seek(124, SeekOrigin.Begin);
            b.Write(float.IsFinite(max) ? max : 0f); b.Write(float.IsFinite(min) ? min : 0f);   // cal_max, cal_min
            var text = Encoding.ASCII.GetBytes(description.Length > 79 ? description[..79] : description);
            Array.Copy(text, 0, h, 148, text.Length);           // descrip
            b.Seek(252, SeekOrigin.Begin); b.Write((short)1); b.Write((short)1);   // qform_code, sform_code: scanner-like frame
            foreach (float q in new[] { (float)quaternion.B, (float)quaternion.C, (float)quaternion.D, (float)offset.X, (float)offset.Y, (float)offset.Z }) b.Write(q);
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++) b.Write((float)affine[r, c]);   // srow_x, srow_y, srow_z
            var intent = Encoding.ASCII.GetBytes("DSC beam frame");
            Array.Copy(intent, 0, h, 328, intent.Length);       // intent_name
            Array.Copy(Encoding.ASCII.GetBytes("n+1\0"), 0, h, 344, 4);   // magic
            stream.Write(h);

            var buffer = new byte[1 << 16];
            for (int i = 0; i < values.Length;)
            {
                int n = Math.Min(buffer.Length / 4, values.Length - i);
                Buffer.BlockCopy(values, i * 4, buffer, 0, n * 4);
                stream.Write(buffer, 0, n * 4);
                i += n;
            }

            static void Put(BinaryWriter w, int at, int value) { w.Seek(at, SeekOrigin.Begin); w.Write(value); }
        }

        /// <summary>
        /// The voxel-to-frame affine (3x4, mm), and the same rotation as a NIfTI quaternion (b, c, d) with its offset.
        /// Voxel (i, j, k) is at patient (X0 + i XRes, Y0 + j YRes, Z0 + k ZRes), assuming the grid's rows and columns
        /// run along +x and +y.
        /// </summary>
        public static (double[,] Affine, (double B, double C, double D) Quaternion, Position3 Offset) Orientation(DoseMatrixOptimal grid, BeamFrame frame)
        {
            // R's rows are the frame's axes in patient coordinates; column c of R is patient axis c in the frame.
            double[,] r =
            {
                { frame.X.X, frame.X.Y, frame.X.Z },
                { frame.Y.X, frame.Y.Y, frame.Y.Z },
                { frame.Z.X, frame.Z.Y, frame.Z.Z },
            };
            var o = frame.ToFrame(grid.X0, grid.Y0, grid.Z0);
            double[] spacing = { grid.XRes, grid.YRes, grid.ZRes };
            var affine = new double[3, 4];
            for (int row = 0; row < 3; row++)
            {
                for (int c = 0; c < 3; c++) affine[row, c] = r[row, c] * spacing[c];
                affine[row, 3] = row == 0 ? o.X : row == 1 ? o.Y : o.Z;
            }
            return (affine, Quaternion(r), o);
        }

        /// <summary>A proper rotation matrix as NIfTI's (b, c, d), with a = sqrt(1 - b² - c² - d²) ≥ 0.</summary>
        internal static (double B, double C, double D) Quaternion(double[,] r)
        {
            double trace = r[0, 0] + r[1, 1] + r[2, 2];
            double a, b, c, d;
            if (trace > 0)
            {
                double s = Math.Sqrt(trace + 1) * 2;
                a = s / 4; b = (r[2, 1] - r[1, 2]) / s; c = (r[0, 2] - r[2, 0]) / s; d = (r[1, 0] - r[0, 1]) / s;
            }
            else if (r[0, 0] > r[1, 1] && r[0, 0] > r[2, 2])
            {
                double s = Math.Sqrt(1 + r[0, 0] - r[1, 1] - r[2, 2]) * 2;
                a = (r[2, 1] - r[1, 2]) / s; b = s / 4; c = (r[0, 1] + r[1, 0]) / s; d = (r[0, 2] + r[2, 0]) / s;
            }
            else if (r[1, 1] > r[2, 2])
            {
                double s = Math.Sqrt(1 + r[1, 1] - r[0, 0] - r[2, 2]) * 2;
                a = (r[0, 2] - r[2, 0]) / s; b = (r[0, 1] + r[1, 0]) / s; c = s / 4; d = (r[1, 2] + r[2, 1]) / s;
            }
            else
            {
                double s = Math.Sqrt(1 + r[2, 2] - r[0, 0] - r[1, 1]) * 2;
                a = (r[1, 0] - r[0, 1]) / s; b = (r[0, 2] + r[2, 0]) / s; c = (r[1, 2] + r[2, 1]) / s; d = s / 4;
            }
            return a < 0 ? (-b, -c, -d) : (b, c, d);
        }
    }
}
