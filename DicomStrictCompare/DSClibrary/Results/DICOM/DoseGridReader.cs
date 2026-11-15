using System;
using System.Buffers.Binary;
using System.IO;
using EvilDICOM.RT;

namespace DSClibrary.Results.DICOM
{
    /// <summary>
    /// Loads an RT Dose grid for comparison: the header from the file's start, then the voxels streamed
    /// in chunks straight into one <c>double[]</c>, scaled by DoseGridScaling as EvilDICOM's <c>RTDose</c>
    /// scales them. Checked against EvilDICOM in <c>DoseGridReaderOracleTests</c>.
    /// <para>
    /// Why: EvilDICOM's load allocates 4.6 GB to keep a 0.81 GB 1 mm grid, and four pairs at once ran a
    /// 31 GB machine out of memory (measured 2026-11-14, ~Resources/DSC-notes/2026-11-14-memory-findings.md).
    /// This allocates the grid and a 4 MB buffer. EvilDICOM still reads everything else.
    /// </para>
    /// </summary>
    public static class DoseGridReader
    {
        private const int ChunkBytes = 4 * 1024 * 1024;

        /// <summary>What the header says about the voxels and the grid.</summary>
        internal sealed record Layout(int DimX, int DimY, int DimZ, int Bits, bool Signed, double Scaling, long ValueAt,
            double X0, double Y0, double Z0, double XRes, double YRes, double ZRes)
        {
            public int BytesPerVoxel => Bits / 8;
            public int Count => DimX * DimY * DimZ;

            public double Decode(ReadOnlySpan<byte> chunk, int i) => Scaling * (Bits == 32
                ? (Signed ? BinaryPrimitives.ReadInt32LittleEndian(chunk.Slice(i * 4)) : BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(i * 4)))
                : (Signed ? BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(i * 2)) : BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(i * 2))));
        }

        private static Layout ReadLayout(FileStream stream, string path)
        {
            var file = DicomFileReader.ReadHeader(stream, path, out long pixelDataAt);
            var dose = RTDoseResult.From(file);
            string name = Path.GetFileName(path);

            if (pixelDataAt < 0)
                throw new DicomFormatException($"{name} has no PixelData.");
            int bits = dose.BitsAllocated ?? throw new DicomFormatException($"{name}: BitsAllocated is absent.");
            if (bits != 16 && bits != 32)
                throw new DicomFormatException($"{name}: BitsAllocated of {bits} is not supported; expected 16 or 32.");
            bool signed = dose.PixelRepresentation == 1;
            double scaling = dose.DoseGridScaling ?? throw new DicomFormatException($"{name}: DoseGridScaling is absent.");
            int count = dose.DeclaredVoxelCount ?? throw new DicomFormatException($"{name}: Rows, Columns or NumberOfFrames is absent.");

            long valueAt = PixelValueStart(stream, pixelDataAt, file.Dataset.IsImplicitVr, out long valueLength, name);
            if (valueLength < (long)count * (bits / 8))
                throw new DicomFormatException(
                    $"{name}: PixelData holds {valueLength} bytes but {count} voxels of {bits} bits need {(long)count * (bits / 8)}.");

            // Geometry as RTDoseResult.ToGrid has it: PixelSpacing is y then x, z steps from GridFrameOffsetVector.
            var origin = dose.ImagePositionPatient;
            var spacing = dose.PixelSpacing;
            var offsets = dose.GridFrameOffsetVector;
            if (origin.Count < 3) throw new DicomFormatException($"{name}: ImagePositionPatient needs three values.");
            if (spacing.Count < 2) throw new DicomFormatException($"{name}: PixelSpacing needs two values.");
            if (!dose.HasUniformFrameSpacing)
                throw new DicomFormatException($"{name}: GridFrameOffsetVector is not uniformly spaced, and DoseMatrixOptimal holds a single ZRes.");

            return new Layout(dose.Columns!.Value, dose.Rows!.Value, dose.NumberOfFrames!.Value, bits, signed, scaling, valueAt,
                origin[0], origin[1], origin[2] + (offsets.Count > 0 ? offsets[0] : 0.0),
                spacing[1], spacing[0], offsets.Count > 1 ? offsets[1] - offsets[0] : spacing[0]);
        }

        public static DoseMatrixOptimal Read(string path)
        {
            CheckPath(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan);
            var l = ReadLayout(stream, path);
            int dimX = l.DimX, dimY = l.DimY, dimZ = l.DimZ, count = l.Count, bytesPerVoxel = l.BytesPerVoxel;
            var values = new double[count];
            var buffer = new byte[ChunkBytes];
            var axX = AxisMaxima.NegativeInfinity(dimX);
            var axY = AxisMaxima.NegativeInfinity(dimY);
            var axZ = AxisMaxima.NegativeInfinity(dimZ);
            stream.Position = l.ValueAt;
            int done = 0, maxIndex = 0, vx = 0, vy = 0, vz = 0;
            double max = double.NegativeInfinity;
            while (done < count)
            {
                int voxels = Math.Min(ChunkBytes / bytesPerVoxel, count - done);
                var chunk = buffer.AsSpan(0, voxels * bytesPerVoxel);
                stream.ReadExactly(chunk);
                for (int i = 0; i < voxels; i++)
                {
                    double v = l.Decode(chunk, i);
                    values[done + i] = v;
                    if (v > max) { max = v; maxIndex = done + i; }
                    if (v > axX[vx]) axX[vx] = v;
                    if (v > axY[vy]) axY[vy] = v;
                    if (v > axZ[vz]) axZ[vz] = v;
                    if (++vx == dimX) { vx = 0; if (++vy == dimY) { vy = 0; vz++; } }
                }
                done += voxels;
            }

            int mx = maxIndex % dimX, my = maxIndex / dimX % dimY, mz = maxIndex / (dimX * dimY);
            var maxPoint = new DoseValue(l.X0 + mx * l.XRes, l.Y0 + my * l.YRes, l.Z0 + mz * l.ZRes, max);

            return new DoseMatrixOptimal(dimX, dimY, dimZ, l.X0, l.Y0, l.Z0, l.XRes, l.YRes, l.ZRes, l.Scaling, values, maxPoint,
                new AxisMaxima(axX, axY, axZ));
        }

        /// <summary>
        /// Opens a grid for reading only some of its rows (<see cref="PartialDoseGrid"/>): the header now, voxels when
        /// asked. For a target, whose voxels the comparison reads only where the source is above threshold.
        /// </summary>
        public static PartialDoseGrid Open(string path)
        {
            CheckPath(path);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.RandomAccess);
            try { return new PartialDoseGrid(stream, ReadLayout(stream, path)); }
            catch { stream.Dispose(); throw; }
        }

        private static void CheckPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("DICOM file not found.", path);
        }

        /// <summary>
        /// A grid read row by row on request. <see cref="Grid"/> has the file's geometry; voxels not yet read hold
        /// <see cref="Unread"/>, and its maximum is unknown (NaN), so it is only for reading the voxels that were asked for.
        /// </summary>
        public sealed class PartialDoseGrid : IDisposable
        {
            private readonly FileStream _stream;
            private readonly Layout _l;
            private readonly bool[] _rowRead;
            private byte[] _buffer = Array.Empty<byte>();

            /// <summary>
            /// What an unread voxel holds: far above any dose, so a read outside the requested rows makes its point fail
            /// and a comparison against a full read differ, rather than pass quietly as NaN would in the DTA test.
            /// </summary>
            public const double Unread = 1e300;

            public DoseMatrixOptimal Grid { get; }
            public long VoxelsRead { get; private set; }
            public double FractionRead => (double)VoxelsRead / _l.Count;

            internal PartialDoseGrid(FileStream stream, Layout l)
            {
                _stream = stream;
                _l = l;
                _rowRead = new bool[l.DimY * l.DimZ];
                var values = new double[l.Count];
                Array.Fill(values, Unread);
                Grid = new DoseMatrixOptimal(l.DimX, l.DimY, l.DimZ, l.X0, l.Y0, l.Z0, l.XRes, l.YRes, l.ZRes, l.Scaling, values,
                    new DoseValue(double.NaN, double.NaN, double.NaN, double.NaN));
            }

            /// <summary>Rows y0..y1 of planes z0..z1, inclusive and clamped to the grid; rows already read are skipped.</summary>
            public void ReadRows(int z0, int z1, int y0, int y1)
            {
                z0 = Math.Max(0, z0); z1 = Math.Min(_l.DimZ - 1, z1);
                y0 = Math.Max(0, y0); y1 = Math.Min(_l.DimY - 1, y1);
                for (int z = z0; z <= z1; z++)
                    for (int y = y0; y <= y1;)
                    {
                        if (_rowRead[z * _l.DimY + y]) { y++; continue; }
                        int end = y;
                        while (end + 1 <= y1 && !_rowRead[z * _l.DimY + end + 1]) end++;
                        ReadRun(z, y, end);
                        y = end + 1;
                    }
            }

            public void ReadPlane(int z) => ReadRows(z, z, 0, _l.DimY - 1);
            public void ReadAll() => ReadRows(0, _l.DimZ - 1, 0, _l.DimY - 1);

            private void ReadRun(int z, int y0, int y1)
            {
                int rows = y1 - y0 + 1, voxels = rows * _l.DimX, bytes = voxels * _l.BytesPerVoxel;
                if (_buffer.Length < bytes) _buffer = new byte[bytes];
                long first = ((long)z * _l.DimY + y0) * _l.DimX;
                _stream.Position = _l.ValueAt + first * _l.BytesPerVoxel;
                var chunk = _buffer.AsSpan(0, bytes);
                _stream.ReadExactly(chunk);
                var values = Grid.DoseValues;
                for (int i = 0; i < voxels; i++) values[first + i] = _l.Decode(chunk, i);
                for (int y = y0; y <= y1; y++) _rowRead[z * _l.DimY + y] = true;
                VoxelsRead += voxels;
            }

            public void Dispose() => _stream.Dispose();
        }

        /// <summary>Where PixelData's value starts, read from its element header at <paramref name="elementAt"/>.</summary>
        private static long PixelValueStart(Stream stream, long elementAt, bool implicitVr, out long valueLength, string name)
        {
            Span<byte> head = stackalloc byte[12];
            stream.Position = elementAt;
            stream.ReadExactly(head.Slice(0, implicitVr ? 8 : 12));
            uint length = implicitVr
                ? BinaryPrimitives.ReadUInt32LittleEndian(head.Slice(4))
                : BinaryPrimitives.ReadUInt32LittleEndian(head.Slice(8));
            if (length == 0xFFFFFFFF)
                throw new DicomFormatException($"{name}: PixelData is encapsulated (compressed), which is not supported.");
            valueLength = length;
            return elementAt + (implicitVr ? 8 : 12);
        }
    }
}
