using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace DSClibrary.Results.DICOM
{
    /// <summary>
    /// Encodes values the way DICOM stores them, for elements this library writes itself. Only the
    /// value representations the RT Dose module needs are here; a full dictionary is not the aim.
    /// </summary>
    public static class DicomValues
    {
        /// <summary>DS: decimal strings separated by backslashes, invariant culture, padded to even length with a space.</summary>
        public static byte[] DecimalString(params double[] values) =>
            PadWithSpace(string.Join("\\", values.Select(v => v.ToString("0.##########", CultureInfo.InvariantCulture))));

        /// <summary>IS: an integer string, padded to even length with a space.</summary>
        public static byte[] IntegerString(int value) => PadWithSpace(value.ToString(CultureInfo.InvariantCulture));

        /// <summary>US: one unsigned short, little endian.</summary>
        public static byte[] UnsignedShort(ushort value)
        {
            var bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            return bytes;
        }

        /// <summary>UI: a UID, padded to even length with a null.</summary>
        public static byte[] UniqueIdentifier(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException("A UID is required.", nameof(uid));
            if (uid.Length > 64) throw new ArgumentException("A UID has at most 64 characters.", nameof(uid));
            var bytes = Encoding.ASCII.GetBytes(uid);
            return bytes.Length % 2 == 0 ? bytes : bytes.Concat(new byte[] { 0 }).ToArray();
        }

        /// <summary>LO / SH / CS and the other text VRs: Latin1, padded to even length with a space.</summary>
        public static byte[] Text(string value) => PadWithSpace(value ?? string.Empty);

        /// <summary>OW holding 32-bit unsigned voxels, little endian - the form every surveyed RT Dose used.</summary>
        public static byte[] UnsignedInt32Words(ReadOnlySpan<uint> voxels)
        {
            var bytes = new byte[voxels.Length * 4];
            for (int i = 0; i < voxels.Length; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), voxels[i]);
            return bytes;
        }

        /// <summary>
        /// A new UID under the 2.25 root: the decimal form of a 128-bit GUID, which the standard permits
        /// without a registered organisation root (PS3.5 B.2). At most 44 characters.
        /// </summary>
        public static string NewUid()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
            return "2.25." + value.ToString(CultureInfo.InvariantCulture);
        }

        private static byte[] PadWithSpace(string s)
        {
            var bytes = Encoding.Latin1.GetBytes(s);
            return bytes.Length % 2 == 0 ? bytes : bytes.Concat(new byte[] { 0x20 }).ToArray();
        }
    }

    /// <summary>
    /// Writes a <see cref="DicomDataset"/> as a Part 10 file in implicit VR little endian.
    /// <para>
    /// The counterpart of <see cref="DicomFileReader"/>, and as deliberately narrow: it exists so a
    /// dataset read from one of these files can be changed and written back. Element values are
    /// re-emitted as the bytes the reader kept, so everything that was not touched comes out as it
    /// went in. The file meta group is written fresh, because its values (SOP instance, transfer
    /// syntax, implementation) describe the file being written and not the one that was read.
    /// </para>
    /// <para>
    /// Only datasets whose values were parsed under implicit VR can be written, because a sequence's
    /// item bytes are kept verbatim and under explicit VR they would carry VR fields the implicit
    /// encoding has no place for. Measured 2026-11-08: all 26 reference doses and all 58 files sampled
    /// across the 2,791 in <c>EquinoxCommissioning/DICOM</c> are implicit VR little endian, so this
    /// covers every file the project has seen. Explicit VR output would need a VR per element, which
    /// the reader does not keep.
    /// </para>
    /// </summary>
    public static class DicomFileWriter
    {
        /// <summary>
        /// This library's implementation class UID, written into every file it produces so a file can
        /// say where it came from. A 2.25 UID fixed once, 2026-11-08.
        /// </summary>
        public const string ImplementationClassUID = "2.25.198337463817402861253019338567384419451";

        public const string ImplementationVersionName = "DSClibrary";

        private const uint UndefinedLength = 0xFFFFFFFF;

        public static void WriteFile(string path, DicomDataset dataset)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
            Write(stream, dataset);
        }

        public static byte[] ToBytes(DicomDataset dataset)
        {
            using var stream = new MemoryStream();
            Write(stream, dataset);
            return stream.ToArray();
        }

        public static void Write(Stream stream, DicomDataset dataset)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (dataset is null) throw new ArgumentNullException(nameof(dataset));
            if (!dataset.IsImplicitVr)
                throw new DicomFormatException(
                    "Only datasets parsed under implicit VR little endian can be written: sequence items are kept as "
                    + "their original bytes, and explicit VR items do not re-encode as implicit ones.");

            string sopClass = dataset.GetString(DicomTags.SOPClassUID)
                ?? throw new DicomFormatException("SOPClassUID is absent; a Part 10 file needs it in the meta group.");
            string sopInstance = dataset.GetString(DicomTags.SOPInstanceUID)
                ?? throw new DicomFormatException("SOPInstanceUID is absent; a Part 10 file needs it in the meta group.");

            using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);

            writer.Write(new byte[128]);
            writer.Write(Encoding.ASCII.GetBytes("DICM"));
            WriteFileMeta(writer, sopClass, sopInstance);

            foreach (DicomTag tag in dataset.Tags)
            {
                if (tag.IsFileMeta) continue;   // the meta group was just written from the dataset's own identity
                DicomElement element = dataset.Element(tag)!;
                WriteTag(writer, tag);
                if (element.Items is not null)
                {
                    // An undefined-length sequence: the kept bytes are the items and the sequence delimiter.
                    writer.Write(UndefinedLength);
                    writer.Write(element.Value.Span);
                }
                else
                {
                    ReadOnlySpan<byte> value = element.Value.Span;
                    bool odd = value.Length % 2 != 0;
                    writer.Write((uint)(value.Length + (odd ? 1 : 0)));
                    writer.Write(value);
                    if (odd) writer.Write((byte)0);
                }
            }
            writer.Flush();
        }

        /// <summary>
        /// Group 0002 in explicit VR little endian, as PS3.10 requires whatever the dataset uses. Group
        /// length first, computed over the elements that follow it.
        /// </summary>
        private static void WriteFileMeta(BinaryWriter writer, string sopClass, string sopInstance)
        {
            var elements = new List<(DicomTag Tag, string Vr, byte[] Value)>
            {
                (new DicomTag(0x0002, 0x0001), "OB", new byte[] { 0, 1 }),
                (new DicomTag(0x0002, 0x0002), "UI", DicomValues.UniqueIdentifier(sopClass)),
                (new DicomTag(0x0002, 0x0003), "UI", DicomValues.UniqueIdentifier(sopInstance)),
                (DicomTags.TransferSyntaxUID, "UI", DicomValues.UniqueIdentifier(DicomTags.ImplicitVRLittleEndian)),
                (new DicomTag(0x0002, 0x0012), "UI", DicomValues.UniqueIdentifier(ImplementationClassUID)),
                (new DicomTag(0x0002, 0x0013), "SH", DicomValues.Text(ImplementationVersionName)),
            };

            uint groupLength = 0;
            foreach (var (_, vr, value) in elements)
                groupLength += (uint)(vr == "OB" ? 12 : 8) + (uint)value.Length;

            WriteTag(writer, new DicomTag(0x0002, 0x0000));
            writer.Write(Encoding.ASCII.GetBytes("UL"));
            writer.Write((ushort)4);
            writer.Write(groupLength);

            foreach (var (tag, vr, value) in elements)
            {
                WriteTag(writer, tag);
                writer.Write(Encoding.ASCII.GetBytes(vr));
                if (vr == "OB")
                {
                    writer.Write((ushort)0);
                    writer.Write((uint)value.Length);
                }
                else
                {
                    writer.Write((ushort)value.Length);
                }
                writer.Write(value);
            }
        }

        private static void WriteTag(BinaryWriter writer, DicomTag tag)
        {
            writer.Write(tag.Group);
            writer.Write(tag.Element);
        }
    }
}
