using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace DSClibrary.Results.DICOM
{
    /// <summary>
    /// An RT Dose file, as the file has it: DICOM's own attribute names, DICOM's own units, and the
    /// voxels as stored integers with the scaling factor beside them rather than multiplied in.
    /// <para>
    /// Keeping them unmultiplied is the layer's job. Cody, 2026-11-08: a dose volume holds absolute
    /// dose in centigray or relative dose in percent, "either would be the voxel value * dose grid
    /// scaling" - so the multiplication is a mapper's decision, taken once, where the unit conversion
    /// to centigray also happens. He also explained why the format is built this way: RT Dose predates
    /// cheap doubles, and the integer-plus-factor pair was how precision was preserved.
    /// </para>
    /// <para>
    /// EvilDICOM applies the factor on read and hands back gray. This type deliberately does not, so
    /// that what the file said and what we make of it stay separately checkable.
    /// </para>
    /// </summary>
    public sealed class RTDoseResult
    {
        private readonly DicomDataset _dataset;
        private uint? _maximumRawVoxel;

        public string SourcePath { get; }

        internal RTDoseResult(DicomFile file)
        {
            if (!file.IsRTDose)
                throw new DicomFormatException(
                    $"Not an RT Dose: SOPClassUID is {file.SOPClassUID ?? "absent"}, expected {DicomTags.RTDoseStorage}.");
            _dataset = file.Dataset;
            SourcePath = file.SourcePath;
        }

        public static RTDoseResult ReadFile(string path) => new(DicomFileReader.ReadFile(path));

        /// <summary>Every element of the dataset, for anything not promoted to a property here.</summary>
        public DicomDataset Dataset => _dataset;

        public string? SOPInstanceUID => _dataset.GetString(DicomTags.SOPInstanceUID);
        public string? FrameOfReferenceUID => _dataset.GetString(DicomTags.FrameOfReferenceUID);

        /// <summary>PatientID (0010,0020). "UnitTest" in the reference files.</summary>
        public string? PatientID => _dataset.GetString(DicomTags.PatientID);

        public string? SeriesDescription => _dataset.GetString(DicomTags.SeriesDescription);
        public string? StudyInstanceUID => _dataset.GetString(DicomTags.StudyInstanceUID);

        // ---- geometry, in DICOM's millimetres and DICOM's patient coordinate system ----

        /// <summary>
        /// ImagePositionPatient (0020,0032): the centre of the first transmitted voxel, in the patient
        /// coordinate system. Measured across all 26 reference files: negative in every one, between
        /// -97.5 and -101.9 mm, because the frame of reference for an RT plan sits at or near the
        /// isocentre and a grid spanning it starts negative.
        /// </summary>
        public IReadOnlyList<double> ImagePositionPatient => _dataset.GetDecimals(DicomTags.ImagePositionPatient);

        /// <summary>
        /// ImageOrientationPatient (0020,0037): row and column direction cosines. Measured as
        /// 1\0\0\0\1\0 in all 26 reference files - axial and unrotated.
        /// </summary>
        public IReadOnlyList<double> ImageOrientationPatient => _dataset.GetDecimals(DicomTags.ImageOrientationPatient);

        /// <summary>PixelSpacing (0028,0030): row spacing then column spacing, in millimetres.</summary>
        public IReadOnlyList<double> PixelSpacing => _dataset.GetDecimals(DicomTags.PixelSpacing);

        public ushort? Rows => _dataset.GetUInt16(DicomTags.Rows);
        public ushort? Columns => _dataset.GetUInt16(DicomTags.Columns);
        public int? NumberOfFrames => _dataset.GetInteger(DicomTags.NumberOfFrames);

        /// <summary>
        /// GridFrameOffsetVector (3004,000C): the frame offsets along the normal. Equidistant in the
        /// reference files, but the standard permits unequal spacing, which is the case
        /// <c>decisions/2026-10-02-comparison-rules.md</c> item 3 calls an axial dataset with
        /// independent z.
        /// </summary>
        public IReadOnlyList<double> GridFrameOffsetVector => _dataset.GetDecimals(DicomTags.GridFrameOffsetVector);

        /// <summary>True when every frame offset step is the same, within a tenth of a millimetre.</summary>
        public bool HasUniformFrameSpacing
        {
            get
            {
                var offsets = GridFrameOffsetVector;
                if (offsets.Count < 3) return true;
                double first = offsets[1] - offsets[0];
                for (int i = 2; i < offsets.Count; i++)
                    if (Math.Abs((offsets[i] - offsets[i - 1]) - first) > 0.1)
                        return false;
                return true;
            }
        }

        // ---- dose ----

        /// <summary>DoseUnits (3004,0002). <c>GY</c> in all 26 reference files; may also be <c>RELATIVE</c>.</summary>
        public string? DoseUnits => _dataset.GetString(DicomTags.DoseUnits);

        public string? DoseType => _dataset.GetString(DicomTags.DoseType);

        /// <summary>DoseSummationType (3004,000A). <c>BEAM</c> in the reference files.</summary>
        public string? DoseSummationType => _dataset.GetString(DicomTags.DoseSummationType);

        /// <summary>
        /// DoseGridScaling (3004,000E): the factor that turns a stored voxel into a dose in
        /// <see cref="DoseUnits"/>. <b>Not applied here.</b>
        /// </summary>
        public double? DoseGridScaling => _dataset.GetDecimal(DicomTags.DoseGridScaling);

        // ---- the plan this dose came from ----

        /// <summary>
        /// The SOPInstanceUID of the RT Plan, from ReferencedRTPlanSequence (300C,0002). Measured: all
        /// 26 reference doses carry it and all 26 resolve to one of the 9 plans beside them, so pairing
        /// is by this and not by filename.
        /// </summary>
        public string? ReferencedRTPlanSOPInstanceUID =>
            _dataset.GetFirstItem(DicomTags.ReferencedRTPlanSequence)
                   ?.GetString(DicomTags.ReferencedSOPInstanceUID);

        /// <summary>
        /// The beam this dose is for, from ReferencedRTPlanSequence -> ReferencedFractionGroupSequence
        /// -> ReferencedBeamSequence. Nested inside the plan reference, not at the top level.
        /// </summary>
        public int? ReferencedBeamNumber =>
            _dataset.GetFirstItem(DicomTags.ReferencedRTPlanSequence)
                   ?.GetFirstItem(DicomTags.ReferencedFractionGroupSequence)
                   ?.GetFirstItem(DicomTags.ReferencedBeamSequence)
                   ?.GetInteger(DicomTags.ReferencedBeamNumber);

        public int? ReferencedFractionGroupNumber =>
            _dataset.GetFirstItem(DicomTags.ReferencedRTPlanSequence)
                   ?.GetFirstItem(DicomTags.ReferencedFractionGroupSequence)
                   ?.GetInteger(DicomTags.ReferencedFractionGroupNumber);

        // ---- voxels ----

        public ushort? BitsAllocated => _dataset.GetUInt16(DicomTags.BitsAllocated);

        /// <summary>PixelRepresentation (0028,0103): 0 unsigned, 1 two's complement.</summary>
        public ushort? PixelRepresentation => _dataset.GetUInt16(DicomTags.PixelRepresentation);

        /// <summary>
        /// The voxels as stored, undecoded. Held as bytes rather than materialised into an array
        /// because a reference file's grid is 201 x 200 x 201 - about 8.1 million voxels and 32 MB at
        /// 32 bits - and copying that per file is worth avoiding when a caller may only want the
        /// maximum.
        /// </summary>
        public ReadOnlyMemory<byte> PixelData => _dataset.Element(DicomTags.PixelData)?.Value ?? default;

        public int VoxelCount =>
            BitsAllocated is { } bits and > 0 ? PixelData.Length / (bits / 8) : 0;

        /// <summary>The expected voxel count from the dimensions, for checking against <see cref="VoxelCount"/>.</summary>
        public int? DeclaredVoxelCount =>
            Rows is { } r && Columns is { } c && NumberOfFrames is { } f ? r * c * f : null;

        /// <summary>One stored voxel, undecoded and unscaled. Index runs x fastest, then y, then z.</summary>
        public uint RawVoxel(int index)
        {
            int bits = BitsAllocated ?? throw new DicomFormatException("BitsAllocated is absent.");
            var span = PixelData.Span;
            return bits switch
            {
                32 => BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(index * 4, 4)),
                16 => BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(index * 2, 2)),
                _ => throw new DicomFormatException($"BitsAllocated of {bits} is not supported; expected 16 or 32.")
            };
        }

        /// <summary>The largest stored voxel. Computed once and remembered.</summary>
        public uint MaximumRawVoxel
        {
            get
            {
                if (_maximumRawVoxel.HasValue) return _maximumRawVoxel.Value;

                int bits = BitsAllocated ?? throw new DicomFormatException("BitsAllocated is absent.");
                var span = PixelData.Span;
                uint max = 0;
                if (bits == 32)
                    for (int i = 0; i + 4 <= span.Length; i += 4)
                    {
                        uint v = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i, 4));
                        if (v > max) max = v;
                    }
                else if (bits == 16)
                    for (int i = 0; i + 2 <= span.Length; i += 2)
                    {
                        uint v = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i, 2));
                        if (v > max) max = v;
                    }
                else
                    throw new DicomFormatException($"BitsAllocated of {bits} is not supported; expected 16 or 32.");

                _maximumRawVoxel = max;
                return max;
            }
        }

        /// <summary>
        /// Decodes every voxel, multiplies by <paramref name="factor"/>, and writes the result into
        /// <paramref name="destination"/> - one traversal, in the reader where
        /// <see cref="BitsAllocated"/> is known.
        /// <para>
        /// This exists so a dose volume can be produced in its final units without the caller reading
        /// voxels one at a time through <see cref="RawVoxel"/>, each call paying a bounds-checked
        /// slice. The comparison engine reads each voxel up to eight times, so the scaling belongs
        /// here, once, and not on the read path.
        /// </para>
        /// </summary>
        public void CopyScaledTo(double[] destination, double factor)
        {
            if (destination is null) throw new ArgumentNullException(nameof(destination));

            int bits = BitsAllocated ?? throw new DicomFormatException("BitsAllocated is absent.");
            var span = PixelData.Span;
            int count = VoxelCount;

            if (destination.Length < count)
                throw new ArgumentException(
                    $"Destination holds {destination.Length} values but the file has {count} voxels.",
                    nameof(destination));

            if (bits == 32)
                for (int i = 0; i < count; i++)
                    destination[i] = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i * 4, 4)) * factor;
            else if (bits == 16)
                for (int i = 0; i < count; i++)
                    destination[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i * 2, 2)) * factor;
            else
                throw new DicomFormatException($"BitsAllocated of {bits} is not supported; expected 16 or 32.");
        }

        public override string ToString() =>
            $"{System.IO.Path.GetFileName(SourcePath)}: {Columns}x{Rows}x{NumberOfFrames}, "
            + $"{DoseUnits}, scaling {DoseGridScaling}, beam {ReferencedBeamNumber}";
    }
}
