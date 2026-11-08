using System;
using System.Globalization;

namespace DSClibrary.Results.DICOM
{
    /// <summary>A DICOM data element tag: a group number and an element number.</summary>
    public readonly record struct DicomTag(ushort Group, ushort Element) : IComparable<DicomTag>
    {
        public bool IsItem => Group == 0xFFFE && Element == 0xE000;
        public bool IsItemDelimiter => Group == 0xFFFE && Element == 0xE00D;
        public bool IsSequenceDelimiter => Group == 0xFFFE && Element == 0xE0DD;
        public bool IsDelimiter => IsItemDelimiter || IsSequenceDelimiter;

        /// <summary>Group 0002, the file meta information, which is always explicit VR little endian.</summary>
        public bool IsFileMeta => Group == 0x0002;

        public int CompareTo(DicomTag other) =>
            Group != other.Group ? Group.CompareTo(other.Group) : Element.CompareTo(other.Element);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:X4},{1:X4})", Group, Element);
    }

    /// <summary>
    /// The tags this library reads, named as the DICOM standard names them.
    /// <para>
    /// Deliberately a short list rather than a full data dictionary. Nothing here needs to know the
    /// value representation of every tag in the standard: with implicit VR the length field tells the
    /// parser where an element ends, and the caller's accessor decides how to interpret the bytes.
    /// </para>
    /// </summary>
    public static class DicomTags
    {
        // File meta (group 0002, always explicit VR LE)
        public static readonly DicomTag TransferSyntaxUID = new(0x0002, 0x0010);
        public static readonly DicomTag MediaStorageSOPClassUID = new(0x0002, 0x0002);

        // SOP common
        public static readonly DicomTag SOPClassUID = new(0x0008, 0x0016);
        public static readonly DicomTag SOPInstanceUID = new(0x0008, 0x0018);
        public static readonly DicomTag Modality = new(0x0008, 0x0060);
        public static readonly DicomTag ReferencedSOPInstanceUID = new(0x0008, 0x1155);

        // Frame of reference
        public static readonly DicomTag FrameOfReferenceUID = new(0x0020, 0x0052);

        // Image plane / pixel
        public static readonly DicomTag ImagePositionPatient = new(0x0020, 0x0032);
        public static readonly DicomTag ImageOrientationPatient = new(0x0020, 0x0037);
        public static readonly DicomTag SamplesPerPixel = new(0x0028, 0x0002);
        public static readonly DicomTag NumberOfFrames = new(0x0028, 0x0008);
        public static readonly DicomTag Rows = new(0x0028, 0x0010);
        public static readonly DicomTag Columns = new(0x0028, 0x0011);
        public static readonly DicomTag PixelSpacing = new(0x0028, 0x0030);
        public static readonly DicomTag BitsAllocated = new(0x0028, 0x0100);
        public static readonly DicomTag BitsStored = new(0x0028, 0x0101);
        public static readonly DicomTag HighBit = new(0x0028, 0x0102);
        public static readonly DicomTag PixelRepresentation = new(0x0028, 0x0103);
        public static readonly DicomTag PixelData = new(0x7FE0, 0x0010);

        // RT Dose
        public static readonly DicomTag DoseUnits = new(0x3004, 0x0002);
        public static readonly DicomTag DoseType = new(0x3004, 0x0004);
        public static readonly DicomTag DoseSummationType = new(0x3004, 0x000A);
        public static readonly DicomTag GridFrameOffsetVector = new(0x3004, 0x000C);
        public static readonly DicomTag DoseGridScaling = new(0x3004, 0x000E);

        // RT Dose -> RT Plan reference
        public static readonly DicomTag ReferencedRTPlanSequence = new(0x300C, 0x0002);
        public static readonly DicomTag ReferencedFractionGroupSequence = new(0x300C, 0x0020);
        public static readonly DicomTag ReferencedBeamSequence = new(0x300C, 0x0004);
        public static readonly DicomTag ReferencedBeamNumber = new(0x300C, 0x0006);
        public static readonly DicomTag ReferencedFractionGroupNumber = new(0x300C, 0x0022);

        // RT Plan
        public static readonly DicomTag RTPlanLabel = new(0x300A, 0x0002);
        public static readonly DicomTag RTPlanName = new(0x300A, 0x0003);
        public static readonly DicomTag FractionGroupSequence = new(0x300A, 0x0070);
        public static readonly DicomTag FractionGroupNumber = new(0x300A, 0x0071);
        public static readonly DicomTag NumberOfFractionsPlanned = new(0x300A, 0x0078);
        public static readonly DicomTag ReferencedBeamSequencePlan = new(0x300C, 0x0004);
        public static readonly DicomTag BeamMeterset = new(0x300A, 0x0086);
        public static readonly DicomTag BeamSequence = new(0x300A, 0x00B0);
        public static readonly DicomTag TreatmentMachineName = new(0x300A, 0x00B2);
        public static readonly DicomTag SourceAxisDistance = new(0x300A, 0x00B4);
        public static readonly DicomTag BeamLimitingDeviceSequence = new(0x300A, 0x00B6);
        public static readonly DicomTag RTBeamLimitingDeviceType = new(0x300A, 0x00B8);
        public static readonly DicomTag NumberOfLeafJawPairs = new(0x300A, 0x00BC);
        public static readonly DicomTag BeamNumber = new(0x300A, 0x00C0);
        public static readonly DicomTag BeamName = new(0x300A, 0x00C2);
        public static readonly DicomTag BeamType = new(0x300A, 0x00C4);
        public static readonly DicomTag RadiationType = new(0x300A, 0x00C6);
        public static readonly DicomTag NumberOfControlPoints = new(0x300A, 0x0110);
        public static readonly DicomTag ControlPointSequence = new(0x300A, 0x0111);
        public static readonly DicomTag NominalBeamEnergy = new(0x300A, 0x0114);
        public static readonly DicomTag BeamLimitingDevicePositionSequence = new(0x300A, 0x011A);
        public static readonly DicomTag LeafJawPositions = new(0x300A, 0x011C);
        public static readonly DicomTag GantryAngle = new(0x300A, 0x011E);
        public static readonly DicomTag BeamLimitingDeviceAngle = new(0x300A, 0x0120);
        public static readonly DicomTag PatientSupportAngle = new(0x300A, 0x0122);
        public static readonly DicomTag IsocenterPosition = new(0x300A, 0x012C);
        public static readonly DicomTag SurfaceEntryPoint = new(0x300A, 0x012E);
        public static readonly DicomTag SourceToSurfaceDistance = new(0x300A, 0x0130);

        // Fluence / FFF detection
        public static readonly DicomTag PrimaryFluenceModeSequence = new(0x3002, 0x0050);
        public static readonly DicomTag FluenceMode = new(0x3002, 0x0051);
        public static readonly DicomTag FluenceModeID = new(0x3002, 0x0052);

        // SOP class UIDs worth recognising
        public const string RTDoseStorage = "1.2.840.10008.5.1.4.1.1.481.2";
        public const string RTPlanStorage = "1.2.840.10008.5.1.4.1.1.481.5";

        // Transfer syntaxes
        public const string ImplicitVRLittleEndian = "1.2.840.10008.1.2";
        public const string ExplicitVRLittleEndian = "1.2.840.10008.1.2.1";
        public const string DeflatedExplicitVRLittleEndian = "1.2.840.10008.1.2.1.99";
        public const string ExplicitVRBigEndian = "1.2.840.10008.1.2.2";
    }
}
