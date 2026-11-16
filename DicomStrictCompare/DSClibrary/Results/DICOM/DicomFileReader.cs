using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DSClibrary.Results.DICOM
{
    /// <summary>Thrown when a file is not DICOM, or is in a form this reader does not handle.</summary>
    public sealed class DicomFormatException : Exception
    {
        public DicomFormatException(string message) : base(message) { }
    }

    /// <summary>A parsed DICOM file: its meta information and its dataset, as the file has them.</summary>
    public sealed class DicomFile
    {
        public string SourcePath { get; }
        public string TransferSyntaxUID { get; }
        public DicomDataset FileMeta { get; }
        public DicomDataset Dataset { get; }

        internal DicomFile(string sourcePath, string transferSyntaxUid, DicomDataset fileMeta, DicomDataset dataset, bool headerOnly = false)
        {
            SourcePath = sourcePath;
            TransferSyntaxUID = transferSyntaxUid;
            FileMeta = fileMeta;
            Dataset = dataset;
            HeaderOnly = headerOnly;
        }

        /// <summary>True when the file was read up to its pixel data and no further (<see cref="DicomFileReader.ReadHeaderFile"/>).</summary>
        public bool HeaderOnly { get; }

        public string? SOPClassUID => Dataset.GetString(DicomTags.SOPClassUID)
                                      ?? FileMeta.GetString(DicomTags.MediaStorageSOPClassUID);

        public string? SOPInstanceUID => Dataset.GetString(DicomTags.SOPInstanceUID);

        public bool IsRTDose => SOPClassUID == DicomTags.RTDoseStorage;

        public bool IsRTPlan => SOPClassUID == DicomTags.RTPlanStorage;

        public override string ToString() =>
            $"{Path.GetFileName(SourcePath)}: {Dataset.Count} element(s), {TransferSyntaxUID}";
    }

    /// <summary>
    /// Reads a DICOM file into a <see cref="DicomFile"/>, interpreting nothing.
    /// <para>
    /// Handles implicit and explicit VR little endian, which is what RT Dose and RT Plan are written
    /// as in practice - measured: all 26 reference doses and 9 plans in <c>resources/</c> are implicit
    /// VR little endian. Big endian, deflated and compressed transfer syntaxes are refused by name
    /// rather than mis-parsed, because a reader that silently produces wrong numbers is worse than one
    /// that stops.
    /// </para>
    /// <para>
    /// <b>Attribution.</b> Written for this project, with the element and sequence structure taken
    /// from the DICOM standard (PS3.5 for the encoding, PS3.3 for the modules). EvilDICOM (MIT,
    /// Rex Cardan) is the reference implementation this is verified against in the tests, and
    /// pymedphys (Apache-2.0) is the reference for the RT geometry conventions used by the mappers.
    /// </para>
    /// </summary>
    public static class DicomFileReader
    {
        private const uint UndefinedLength = 0xFFFFFFFF;

        private static readonly HashSet<string> LongFormVrs =
            new(StringComparer.Ordinal) { "OB", "OW", "OF", "OD", "OL", "SQ", "UT", "UN", "UC", "UR" };

        public static DicomFile ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("DICOM file not found.", path);
            return Read(File.ReadAllBytes(path), path);
        }

        /// <summary>
        /// Reads everything before the top-level PixelData (7FE0,0010) and keeps none of the voxels.
        /// <para>
        /// For pairing thousands of doses (design/comparison-studies.md, F12): matching needs only the
        /// headers, and a full read keeps every dose's voxels alive - measured 2026-11-14, three
        /// 23 GB QAtpsPTW folders were killed by the kernel at 24 GB resident. The header is copied out
        /// of the file's buffer, because the parsed elements are slices of it and would otherwise keep
        /// all of it in memory.
        /// </para>
        /// <para>
        /// Only a growing prefix of the file is read (<see cref="ReadHeader"/>), not the whole file: a 1 mm dose is about
        /// 500 MB and its header a few kB. Found by crane, 2026-11-15: pairing three E##V## sets read 31 GB.
        /// </para>
        /// </summary>
        public static DicomFile ReadHeaderFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A path is required.", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("DICOM file not found.", path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
            return ReadHeader(stream, path, out _);   // no pixel data: the whole file is read, as it is all header
        }

        /// <summary>
        /// Reads a file's header from the start of <paramref name="stream"/>, growing the prefix until the
        /// top-level PixelData is reached, so the voxels are never read. <paramref name="pixelDataAt"/> is
        /// that element's offset in the file, or -1 when the file has none (the whole file is then parsed).
        /// </summary>
        internal static DicomFile ReadHeader(Stream stream, string sourcePath, out long pixelDataAt)
        {
            long length = stream.Length;
            int size = 64 * 1024;
            while (true)
            {
                int n = (int)Math.Min(size, length);
                var prefix = new byte[n];
                stream.Position = 0;
                stream.ReadExactly(prefix);
                Read(prefix, sourcePath, headerOnly: true, stopAtPixelData: true, out int at);
                // An element cut off by the prefix end leaves the pixel data unreached, so a hit is complete.
                // Elements slice the buffer they were parsed from, so parse again from a copy of the header
                // alone: otherwise every kept header holds its whole prefix, 64 kB for a header of a few kB.
                if (at >= 0)
                {
                    pixelDataAt = at;
                    return Read(prefix.AsSpan(0, at).ToArray(), sourcePath, headerOnly: true, stopAtPixelData: true, out _);
                }
                if (n == length) { pixelDataAt = -1; return Read(prefix, sourcePath); }
                size = (int)Math.Min((long)size * 4, int.MaxValue);
            }
        }

        public static DicomFile Read(ReadOnlyMemory<byte> bytes, string sourcePath = "") =>
            Read(bytes, sourcePath, headerOnly: false, stopAtPixelData: false, out _);

        private static DicomFile Read(ReadOnlyMemory<byte> bytes, string sourcePath, bool headerOnly, bool stopAtPixelData, out int pixelDataAt)
        {
            pixelDataAt = -1;
            int pos = 0;
            bool hasPreamble = bytes.Length >= 132
                               && Encoding.ASCII.GetString(bytes.Slice(128, 4).Span) == "DICM";
            if (hasPreamble) pos = 132;

            // The file meta group is always explicit VR little endian, whatever the dataset uses.
            var fileMeta = hasPreamble
                ? ParseElements(bytes, ref pos, bytes.Length, implicitVr: false, stopAfterGroup: 0x0002)
                : new DicomDataset(new Dictionary<DicomTag, DicomElement>());

            string transferSyntax = fileMeta.GetString(DicomTags.TransferSyntaxUID)
                                    ?? DicomTags.ImplicitVRLittleEndian;

            bool implicitVr = transferSyntax switch
            {
                DicomTags.ImplicitVRLittleEndian => true,
                DicomTags.ExplicitVRLittleEndian => false,
                DicomTags.ExplicitVRBigEndian =>
                    throw new DicomFormatException(
                        "Explicit VR big endian is not supported. Convert the file, or add byte swapping here."),
                DicomTags.DeflatedExplicitVRLittleEndian =>
                    throw new DicomFormatException(
                        "Deflated explicit VR little endian is not supported. Inflate the file first."),
                _ => throw new DicomFormatException(
                    $"Transfer syntax {transferSyntax} is not supported. Only implicit and explicit VR little endian are.")
            };

            var dataset = ParseElements(bytes, ref pos, bytes.Length, implicitVr, stopAtPixelData: stopAtPixelData);
            if (stopAtPixelData && pos < bytes.Length) pixelDataAt = pos;
            dataset.SetImplicitVr(implicitVr);
            return new DicomFile(sourcePath, transferSyntax, fileMeta, dataset, headerOnly);
        }

        /// <summary>Parses the items of a sequence held as raw bytes.</summary>
        internal static IReadOnlyList<DicomDataset> ParseSequenceItems(ReadOnlyMemory<byte> bytes, bool implicitVr)
        {
            var items = new List<DicomDataset>();
            int pos = 0;
            while (pos + 8 <= bytes.Length)
            {
                DicomTag tag = ReadTag(bytes, ref pos);
                uint length = ReadUInt32(bytes, ref pos);

                if (tag.IsSequenceDelimiter) break;
                if (!tag.IsItem) break;   // not an item: stop rather than guess

                if (length == UndefinedLength)
                {
                    int itemPos = pos;
                    items.Add(ParseElements(bytes, ref itemPos, bytes.Length, implicitVr, stopAtItemDelimiter: true));
                    pos = itemPos;
                }
                else
                {
                    int end = Math.Min(pos + (int)length, bytes.Length);
                    int itemPos = pos;
                    items.Add(ParseElements(bytes, ref itemPos, end, implicitVr));
                    pos = end;
                }
            }

            foreach (var item in items) item.SetImplicitVr(implicitVr);
            return items;
        }

        private static DicomDataset ParseElements(
            ReadOnlyMemory<byte> bytes,
            ref int pos,
            int end,
            bool implicitVr,
            ushort? stopAfterGroup = null,
            bool stopAtItemDelimiter = false,
            bool stopAtPixelData = false)
        {
            var elements = new Dictionary<DicomTag, DicomElement>();

            while (pos + 8 <= end)
            {
                int elementStart = pos;
                DicomTag tag = ReadTag(bytes, ref pos);

                if (stopAtPixelData && tag == DicomTags.PixelData)
                {
                    pos = elementStart;   // the caller keeps everything before this element
                    break;
                }

                if (stopAfterGroup.HasValue && tag.Group != stopAfterGroup.Value)
                {
                    pos = elementStart;   // this element belongs to the next parse, not this one
                    break;
                }

                if (tag.IsDelimiter)
                {
                    pos += 4;             // delimiters carry a zero length
                    if (stopAtItemDelimiter && tag.IsItemDelimiter) break;
                    continue;
                }

                uint length;
                bool isSequenceVr = false;

                if (implicitVr)
                {
                    length = ReadUInt32(bytes, ref pos);
                }
                else
                {
                    if (pos + 2 > end) break;
                    string vr = Encoding.ASCII.GetString(bytes.Slice(pos, 2).Span);
                    pos += 2;
                    isSequenceVr = vr == "SQ";
                    if (LongFormVrs.Contains(vr))
                    {
                        pos += 2;         // reserved
                        if (pos + 4 > end) break;
                        length = ReadUInt32(bytes, ref pos);
                    }
                    else
                    {
                        if (pos + 2 > end) break;
                        length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(pos, 2).Span);
                        pos += 2;
                    }
                }

                if (length == UndefinedLength)
                {
                    // A sequence, or encapsulated pixel data. Only the former can occur in the
                    // uncompressed transfer syntaxes this reader accepts.
                    int itemsStart = pos;
                    var items = ParseItemsInPlace(bytes, ref pos, end, implicitVr);
                    elements[tag] = new DicomElement(tag, bytes[itemsStart..pos], items);
                    continue;
                }

                int valueEnd = (int)Math.Min((long)pos + length, end);
                var value = bytes[pos..valueEnd];
                pos = valueEnd;

                if (isSequenceVr)
                    elements[tag] = new DicomElement(tag, value, ParseSequenceItems(value, implicitVr));
                else
                    elements[tag] = new DicomElement(tag, value);
            }

            return new DicomDataset(elements);
        }

        /// <summary>Reads items of an undefined-length sequence, advancing past its delimiter.</summary>
        private static List<DicomDataset> ParseItemsInPlace(
            ReadOnlyMemory<byte> bytes, ref int pos, int end, bool implicitVr)
        {
            var items = new List<DicomDataset>();
            while (pos + 8 <= end)
            {
                DicomTag tag = ReadTag(bytes, ref pos);
                uint length = ReadUInt32(bytes, ref pos);

                if (tag.IsSequenceDelimiter) break;

                if (!tag.IsItem)
                {
                    pos -= 8;   // not ours; leave it for the caller
                    break;
                }

                if (length == UndefinedLength)
                {
                    int itemPos = pos;
                    items.Add(ParseElements(bytes, ref itemPos, end, implicitVr, stopAtItemDelimiter: true));
                    pos = itemPos;
                }
                else
                {
                    int itemEnd = (int)Math.Min((long)pos + length, end);
                    int itemPos = pos;
                    items.Add(ParseElements(bytes, ref itemPos, itemEnd, implicitVr));
                    pos = itemEnd;
                }
            }
            return items;
        }

        private static DicomTag ReadTag(ReadOnlyMemory<byte> bytes, ref int pos)
        {
            ushort group = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(pos, 2).Span);
            ushort element = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(pos + 2, 2).Span);
            pos += 4;
            return new DicomTag(group, element);
        }

        private static uint ReadUInt32(ReadOnlyMemory<byte> bytes, ref int pos)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(pos, 4).Span);
            pos += 4;
            return value;
        }
    }
}
