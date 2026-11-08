using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DSClibrary.Results.DICOM
{
    /// <summary>One parsed data element: its tag, its raw value bytes, and its items if it was a sequence.</summary>
    public sealed class DicomElement
    {
        public DicomTag Tag { get; }

        /// <summary>The value exactly as it appeared, undecoded.</summary>
        public ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Items, when this element was parsed as a sequence of undefined length. Null otherwise - a
        /// sequence of defined length keeps its bytes and is parsed by
        /// <see cref="DicomDataset.GetSequence"/> on demand.
        /// </summary>
        public IReadOnlyList<DicomDataset>? Items { get; }

        internal DicomElement(DicomTag tag, ReadOnlyMemory<byte> value, IReadOnlyList<DicomDataset>? items = null)
        {
            Tag = tag;
            Value = value;
            Items = items;
        }

        public override string ToString() =>
            Items is null
                ? $"{Tag} {Value.Length} byte(s)"
                : $"{Tag} sequence of {Items.Count} item(s)";
    }

    /// <summary>
    /// A parsed DICOM dataset: the elements of a file, or of one item of a sequence.
    /// <para>
    /// Values are kept as bytes and interpreted by the accessor the caller chooses. That is what lets
    /// this work without a value-representation dictionary: under implicit VR the length field says
    /// where an element ends, so a parser needs no opinion about its type, and the caller reading
    /// <c>DoseGridScaling</c> already knows it wants a decimal string.
    /// </para>
    /// <para>
    /// Every accessor returns null, or an empty list, for a tag that is absent or unparseable. An
    /// absent value is never a zero: a dose grid whose origin is genuinely at zero and one whose
    /// origin was not recorded must not look the same.
    /// </para>
    /// </summary>
    public sealed class DicomDataset
    {
        private readonly Dictionary<DicomTag, DicomElement> _elements;

        internal DicomDataset(Dictionary<DicomTag, DicomElement> elements) => _elements = elements;

        public IEnumerable<DicomTag> Tags => _elements.Keys.OrderBy(t => t);

        public int Count => _elements.Count;

        public bool Contains(DicomTag tag) => _elements.ContainsKey(tag);

        public DicomElement? Element(DicomTag tag) =>
            _elements.TryGetValue(tag, out var e) ? e : null;

        /// <summary>
        /// A text value, trimmed of the trailing null or space DICOM pads with. Decoded as Latin1,
        /// which cannot throw and maps every byte - the alternative, UTF-8, substitutes on a stray
        /// high byte and would silently corrupt a machine name or a comment.
        /// </summary>
        public string? GetString(DicomTag tag)
        {
            var e = Element(tag);
            if (e is null) return null;
            return Encoding.Latin1.GetString(e.Value.Span).TrimEnd('\0', ' ');
        }

        /// <summary>A multi-valued text element, split on the DICOM backslash separator.</summary>
        public IReadOnlyList<string> GetStrings(DicomTag tag) =>
            GetString(tag) is { } s && s.Length > 0
                ? s.Split('\\').Select(v => v.Trim()).ToArray()
                : Array.Empty<string>();

        /// <summary>
        /// A decimal string (VR DS) or integer string (VR IS) as a number. Always invariant culture:
        /// DICOM writes these with a period regardless of the machine's locale, so a bare
        /// double.Parse would throw or misread where the decimal separator is a comma.
        /// </summary>
        public double? GetDecimal(DicomTag tag)
        {
            var values = GetDecimals(tag);
            return values.Count > 0 ? values[0] : null;
        }

        public IReadOnlyList<double> GetDecimals(DicomTag tag)
        {
            var parsed = new List<double>();
            foreach (string part in GetStrings(tag))
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    parsed.Add(d);
            return parsed;
        }

        public int? GetInteger(DicomTag tag)
        {
            var values = GetDecimals(tag);
            return values.Count > 0 ? (int)Math.Round(values[0]) : null;
        }

        /// <summary>A binary unsigned short (VR US), little endian.</summary>
        public ushort? GetUInt16(DicomTag tag)
        {
            var e = Element(tag);
            return e is not null && e.Value.Length >= 2
                ? BitConverter.ToUInt16(e.Value.Span[..2])
                : null;
        }

        /// <summary>
        /// A sequence's items. Returns the items parsed eagerly when the sequence had undefined
        /// length, otherwise parses the stored bytes now.
        /// </summary>
        public IReadOnlyList<DicomDataset> GetSequence(DicomTag tag)
        {
            var e = Element(tag);
            if (e is null) return Array.Empty<DicomDataset>();
            if (e.Items is not null) return e.Items;
            return DicomFileReader.ParseSequenceItems(e.Value, _implicitVr);
        }

        /// <summary>The first item of a sequence, or null when the sequence is absent or empty.</summary>
        public DicomDataset? GetFirstItem(DicomTag tag) => GetSequence(tag).FirstOrDefault();

        private bool _implicitVr = true;

        internal void SetImplicitVr(bool implicitVr)
        {
            _implicitVr = implicitVr;
            foreach (var e in _elements.Values)
                if (e.Items is not null)
                    foreach (var item in e.Items)
                        item.SetImplicitVr(implicitVr);
        }

        public override string ToString() => $"{_elements.Count} element(s)";
    }
}
