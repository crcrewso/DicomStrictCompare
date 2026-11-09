using System;
using System.Globalization;

namespace DSClibrary.Matching
{
    /// <summary>
    /// Reads a plan's SOPInstanceUID for matching.
    /// <para>
    /// Measured on 2026-11-09 across the 24 PRD-Before and 27 PRD-After plans in Cody's Equinox
    /// export: an Eclipse plan UID is <c>root.installation.serial.YYYYMMDDHHMMSS</c>, shaped like
    /// <c>1.2.246.352.71.5.123456789012.654321.20240727205652</c> (the installation and serial here are
    /// made up), and saving the plan again keeps the serial but writes the new save time, so the same
    /// plan recalculated in August 2026 carries <c>...654321.20260821191739</c>. Matched by the whole
    /// UID, the Before and After exports pair 0 of 726 same-named doses; without the save time they
    /// pair all 726. Cody, 2026-11-09: "what if you ignore the date part of the uid", and "not all tps
    /// will add the timestamp in all situations, so the trim should be detecting the date before
    /// ignoring it".
    /// </para>
    /// <para>
    /// Only a final component that is a plausible date and time (14 digits, month 1-12, day 1-31,
    /// hour under 24, minute and second under 60) is dropped. The anonymised plans in
    /// <c>resources/</c> are <c>1.2.246.352.221.&lt;big integer&gt;</c> and are left as they are, as
    /// is any UID with fewer than three components.
    /// </para>
    /// </summary>
    public static class PlanUids
    {
        /// <summary>The UID with a trailing save-time component removed, or the UID unchanged.</summary>
        public static string WithoutSaveTime(string uid)
        {
            if (uid is null) throw new ArgumentNullException(nameof(uid));
            string trimmed = uid.TrimEnd('\0', ' ');
            int dot = trimmed.LastIndexOf('.');
            if (dot <= 0 || trimmed.IndexOf('.') == dot) return trimmed;
            return IsSaveTime(trimmed.AsSpan(dot + 1)) ? trimmed.Substring(0, dot) : trimmed;
        }

        /// <summary>True when the UID's last component is a plausible YYYYMMDDHHMMSS.</summary>
        public static bool EndsWithSaveTime(string uid)
        {
            if (uid is null) throw new ArgumentNullException(nameof(uid));
            string trimmed = uid.TrimEnd('\0', ' ');
            int dot = trimmed.LastIndexOf('.');
            return dot > 0 && trimmed.IndexOf('.') != dot && IsSaveTime(trimmed.AsSpan(dot + 1));
        }

        private static bool IsSaveTime(ReadOnlySpan<char> s)
        {
            if (s.Length != 14) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            int year = int.Parse(s.Slice(0, 4), NumberStyles.None, CultureInfo.InvariantCulture);
            int month = int.Parse(s.Slice(4, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            int day = int.Parse(s.Slice(6, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            int hour = int.Parse(s.Slice(8, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            int minute = int.Parse(s.Slice(10, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            int second = int.Parse(s.Slice(12, 2), NumberStyles.None, CultureInfo.InvariantCulture);
            return year >= 1990 && year <= 2100
                && month >= 1 && month <= 12
                && day >= 1 && day <= 31
                && hour < 24 && minute < 60 && second < 60;
        }
    }
}
