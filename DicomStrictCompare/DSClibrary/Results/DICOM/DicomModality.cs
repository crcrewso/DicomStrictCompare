using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace DSClibrary.Results.DICOM
{
    public enum DicomModality
    {
        Unknown = 0,
        RTDose,
        RTPlan,
        Other
    }

    /// <summary>
    /// Classifies a DICOM file without reading all of it.
    /// <para>
    /// Worth having because classification happens on every file in a folder and the answer is one
    /// attribute. The reference dose files are about 32 MB each, so reading twenty-six of them whole
    /// just to sort doses from plans moves about 800 MB for a few hundred bytes of information - which
    /// is what the existing <c>DCSCore.DicomFile</c> does, since it calls <c>DICOMObject.Read</c> and
    /// then looks at one tag.
    /// </para>
    /// <para>
    /// SOPClassUID (0008,0016) and Modality (0008,0060) both sit near the start of the dataset, before
    /// the pixel data, so a prefix of the file is enough. If the prefix turns out not to contain them
    /// the result is <see cref="DicomModality.Unknown"/> and the caller can fall back to a full read -
    /// a guess is never substituted.
    /// </para>
    /// </summary>
    public static class DicomClassifier
    {
        /// <summary>How much of the file to read. Generous: the reference headers are under 3 KB.</summary>
        public const int PrefixBytes = 64 * 1024;

        public static DicomModality Classify(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("DICOM file not found.", path);

            byte[] prefix = ReadPrefix(path, PrefixBytes);
            return ClassifyPrefix(prefix);
        }

        internal static DicomModality ClassifyPrefix(ReadOnlySpan<byte> prefix)
        {
            if (prefix.Length < 140) return DicomModality.Unknown;
            if (Encoding.ASCII.GetString(prefix.Slice(128, 4)) != "DICM") return DicomModality.Unknown;

            string? sopClass = FindShortString(prefix, 0x0008, 0x0016)
                               ?? FindShortString(prefix, 0x0002, 0x0002);
            if (sopClass == DicomTags.RTDoseStorage) return DicomModality.RTDose;
            if (sopClass == DicomTags.RTPlanStorage) return DicomModality.RTPlan;

            string? modality = FindShortString(prefix, 0x0008, 0x0060);
            if (string.Equals(modality, "RTDOSE", StringComparison.OrdinalIgnoreCase)) return DicomModality.RTDose;
            if (string.Equals(modality, "RTPLAN", StringComparison.OrdinalIgnoreCase)) return DicomModality.RTPlan;

            return sopClass is not null || modality is not null ? DicomModality.Other : DicomModality.Unknown;
        }

        /// <summary>
        /// Finds a short text element by scanning for its tag bytes.
        /// <para>
        /// A scan rather than a parse, because the point is to avoid parsing. It is safe for the two
        /// tags used here: both carry a UID or a short code, both appear before the pixel data, and a
        /// false positive would need the same four bytes followed by a plausible length - and a wrong
        /// answer only sends the caller down the full-read path, because the value is checked against
        /// known UIDs rather than trusted.
        /// </para>
        /// </summary>
        private static string? FindShortString(ReadOnlySpan<byte> buffer, ushort group, ushort element)
        {
            Span<byte> pattern = stackalloc byte[4];
            BinaryPrimitives.WriteUInt16LittleEndian(pattern, group);
            BinaryPrimitives.WriteUInt16LittleEndian(pattern[2..], element);

            for (int i = 132; i + 12 < buffer.Length; i++)
            {
                if (!buffer.Slice(i, 4).SequenceEqual(pattern)) continue;

                // Explicit VR: tag, two VR letters, then a 16-bit length.
                bool explicitVr = IsVrLetter(buffer[i + 4]) && IsVrLetter(buffer[i + 5]);
                int lengthAt = explicitVr ? i + 6 : i + 4;
                int valueAt = explicitVr ? i + 8 : i + 8;
                int length = explicitVr
                    ? BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(lengthAt, 2))
                    : (int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(lengthAt, 4));

                if (length is <= 0 or > 128 || valueAt + length > buffer.Length) continue;

                return Encoding.ASCII.GetString(buffer.Slice(valueAt, length)).TrimEnd('\0', ' ');
            }

            return null;
        }

        private static bool IsVrLetter(byte b) => b is >= (byte)'A' and <= (byte)'Z';

        private static byte[] ReadPrefix(string path, int count)
        {
            using var stream = File.OpenRead(path);
            byte[] buffer = new byte[(int)Math.Min(count, stream.Length)];
            int read = 0;
            while (read < buffer.Length)
            {
                int got = stream.Read(buffer, read, buffer.Length - read);
                if (got <= 0) break;
                read += got;
            }
            return read == buffer.Length ? buffer : buffer[..read];
        }
    }
}
