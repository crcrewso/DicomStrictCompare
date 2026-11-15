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

        public static DoseMatrixOptimal Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("DICOM file not found.", path);

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan);
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
            int bytesPerVoxel = bits / 8;
            if (valueLength < (long)count * bytesPerVoxel)
                throw new DicomFormatException(
                    $"{name}: PixelData holds {valueLength} bytes but {count} voxels of {bits} bits need {(long)count * bytesPerVoxel}.");

            var values = new double[count];
            var buffer = new byte[ChunkBytes];
            stream.Position = valueAt;
            int done = 0, maxIndex = 0;
            double max = double.NegativeInfinity;
            while (done < count)
            {
                int voxels = Math.Min(ChunkBytes / bytesPerVoxel, count - done);
                var chunk = buffer.AsSpan(0, voxels * bytesPerVoxel);
                stream.ReadExactly(chunk);
                for (int i = 0; i < voxels; i++)
                {
                    double raw = bits == 32
                        ? (signed ? BinaryPrimitives.ReadInt32LittleEndian(chunk.Slice(i * 4)) : BinaryPrimitives.ReadUInt32LittleEndian(chunk.Slice(i * 4)))
                        : (signed ? BinaryPrimitives.ReadInt16LittleEndian(chunk.Slice(i * 2)) : BinaryPrimitives.ReadUInt16LittleEndian(chunk.Slice(i * 2)));
                    double v = raw * scaling;
                    values[done + i] = v;
                    if (v > max) { max = v; maxIndex = done + i; }
                }
                done += voxels;
            }

            // Geometry as RTDoseResult.ToGrid has it: PixelSpacing is y then x, z steps from GridFrameOffsetVector.
            var origin = dose.ImagePositionPatient;
            var spacing = dose.PixelSpacing;
            var offsets = dose.GridFrameOffsetVector;
            if (origin.Count < 3) throw new DicomFormatException($"{name}: ImagePositionPatient needs three values.");
            if (spacing.Count < 2) throw new DicomFormatException($"{name}: PixelSpacing needs two values.");
            if (!dose.HasUniformFrameSpacing)
                throw new DicomFormatException($"{name}: GridFrameOffsetVector is not uniformly spaced, and DoseMatrixOptimal holds a single ZRes.");

            int dimX = dose.Columns!.Value, dimY = dose.Rows!.Value, dimZ = dose.NumberOfFrames!.Value;
            double x0 = origin[0], y0 = origin[1];
            double z0 = origin[2] + (offsets.Count > 0 ? offsets[0] : 0.0);
            double xRes = spacing[1], yRes = spacing[0];
            double zRes = offsets.Count > 1 ? offsets[1] - offsets[0] : spacing[0];

            int mx = maxIndex % dimX, my = maxIndex / dimX % dimY, mz = maxIndex / (dimX * dimY);
            var maxPoint = new DoseValue(x0 + mx * xRes, y0 + my * yRes, z0 + mz * zRes, max);

            return new DoseMatrixOptimal(dimX, dimY, dimZ, x0, y0, z0, xRes, yRes, zRes, scaling, values, maxPoint);
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
