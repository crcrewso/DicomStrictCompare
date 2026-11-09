using System;
using System.Globalization;
using System.Linq;

namespace DSClibrary.Results.DICOM
{
    /// <summary>
    /// Builds a new RT Dose dataset from an existing one with its grid replaced - the step between
    /// <see cref="Resampling.GridResampler"/> and <see cref="DicomFileWriter"/>.
    /// <para>
    /// Everything the source file said that is not about the grid is kept: patient, study, series,
    /// frame of reference, plan and beam references, dose units and type, scaling. What changes is
    /// exactly what a new grid changes - <c>Rows</c>, <c>Columns</c>, <c>NumberOfFrames</c>,
    /// <c>PixelSpacing</c>, <c>GridFrameOffsetVector</c>, <c>PixelData</c> - plus a new
    /// <c>SOPInstanceUID</c>, because a different grid is a different object and two files with the
    /// same instance UID and different voxels is the one thing DICOM forbids. <c>DerivationDescription</c>
    /// (0008,2111) records how the file came about.
    /// </para>
    /// </summary>
    public static class RTDoseDerivation
    {
        public static readonly DicomTag DerivationDescription = new(0x0008, 0x2111);

        /// <summary>
        /// The source dose's voxels as a grid in the file's own stored units - the raw integers as
        /// doubles, with no scaling applied - so a resampled grid can be stored back under the same
        /// <c>DoseGridScaling</c> with coincident voxels bit-identical to the source.
        /// </summary>
        public static DoseMatrixOptimal RawGrid(RTDoseResult dose)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            var values = new double[dose.VoxelCount];
            dose.CopyScaledTo(values, 1.0);
            return dose.ToGrid(values);
        }

        /// <summary>
        /// The source dataset with <paramref name="rawGrid"/> as its voxels. The grid's values are
        /// taken as stored integers (see <see cref="RawGrid"/>): rounded to the nearest unsigned
        /// 32-bit value and written under the source's own <c>DoseGridScaling</c>. Values below zero
        /// or above the 32-bit range are refused rather than wrapped.
        /// </summary>
        public static DicomDataset WithGrid(RTDoseResult dose, DoseMatrixOptimal rawGrid, string description)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            if (rawGrid is null) throw new ArgumentNullException(nameof(rawGrid));

            if (dose.BitsAllocated != 32 || dose.PixelRepresentation != 0)
                throw new DicomFormatException(
                    $"Only 32-bit unsigned voxels are written; this file has BitsAllocated {dose.BitsAllocated} and "
                    + $"PixelRepresentation {dose.PixelRepresentation}. Every surveyed file is 32-bit unsigned.");
            if (rawGrid.DimensionX > ushort.MaxValue || rawGrid.DimensionY > ushort.MaxValue)
                throw new DicomFormatException("Rows and Columns are unsigned shorts; the grid is too large.");

            var voxels = new uint[rawGrid.DoseValues.Length];
            for (int i = 0; i < voxels.Length; i++)
            {
                double v = Math.Round(rawGrid.DoseValues[i]);
                if (v < 0 || v > uint.MaxValue)
                    throw new DicomFormatException($"Voxel {i} of {v} does not fit an unsigned 32-bit value.");
                voxels[i] = (uint)v;
            }

            var offsets = Enumerable.Range(0, rawGrid.DimensionZ).Select(k => k * rawGrid.ZRes).ToArray();

            return dose.Dataset
                .WithElement(DicomTags.SOPInstanceUID, DicomValues.UniqueIdentifier(DicomValues.NewUid()))
                .WithElement(DicomTags.Columns, DicomValues.UnsignedShort((ushort)rawGrid.DimensionX))
                .WithElement(DicomTags.Rows, DicomValues.UnsignedShort((ushort)rawGrid.DimensionY))
                .WithElement(DicomTags.NumberOfFrames, DicomValues.IntegerString(rawGrid.DimensionZ))
                // PixelSpacing is row spacing then column spacing: y then x.
                .WithElement(DicomTags.PixelSpacing, DicomValues.DecimalString(rawGrid.YRes, rawGrid.XRes))
                .WithElement(DicomTags.ImagePositionPatient, DicomValues.DecimalString(rawGrid.X0, rawGrid.Y0, rawGrid.Z0))
                .WithElement(DicomTags.GridFrameOffsetVector, DicomValues.DecimalString(offsets))
                .WithElement(DicomTags.PixelData, DicomValues.UnsignedInt32Words(voxels))
                .WithElement(DerivationDescription, DicomValues.Text(Truncate(description, 64)));
        }

        /// <summary>A description for a resample, e.g. <c>DSClibrary trilinear resample to 5x5x5 mm</c>.</summary>
        public static string ResampleDescription(DoseMatrixOptimal grid) => string.Format(
            CultureInfo.InvariantCulture, "DSClibrary trilinear resample to {0}x{1}x{2} mm",
            grid.XRes, grid.YRes, grid.ZRes);

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);
    }
}
