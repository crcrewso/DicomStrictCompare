using System;
using System.Collections.Generic;
using System.Linq;
using DSClibrary.Results.DICOM;

namespace DSClibrary.Matching
{
    /// <summary>
    /// Which dose algorithm a dose came from, read from its names, so matching can keep AAA with AAA (crcrewso,
    /// 2026-11-16). Eclipse's exports don't record the algorithm: no private tag in the dose, nothing in the plan's
    /// Varian XML, and SoftwareVersions is the exporting application. A name is checked for each algorithm's tokens,
    /// case-insensitively, as substrings.
    /// </summary>
    public sealed class AlgorithmTokens
    {
        /// <summary>AAA; AXB or Acuros; eMC.</summary>
        public static AlgorithmTokens Default { get; } = Parse("AAA = AAA; AXB = AXB, Acuros; eMC = eMC");

        public IReadOnlyList<(string Algorithm, IReadOnlyList<string> Tokens)> Entries { get; }

        private AlgorithmTokens(IReadOnlyList<(string, IReadOnlyList<string>)> entries) => Entries = entries;

        /// <summary>Parses <c>Algorithm = token, token; Algorithm = token</c>; a missing "= ..." uses the name as its token.</summary>
        public static AlgorithmTokens Parse(string? text)
        {
            var entries = new List<(string, IReadOnlyList<string>)>();
            foreach (string part in (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = part.IndexOf('=');
                string name = (eq < 0 ? part : part[..eq]).Trim();
                if (name.Length == 0) throw new FormatException($"'{part}' needs an algorithm name before '='.");
                var tokens = (eq < 0 ? name : part[(eq + 1)..])
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length == 0) throw new FormatException($"'{part}' needs at least one token after '='.");
                entries.Add((name, tokens));
            }
            return new AlgorithmTokens(entries);
        }

        public override string ToString() =>
            string.Join("; ", Entries.Select(e => $"{e.Algorithm} = {string.Join(", ", e.Tokens)}"));

        /// <summary>The algorithms whose tokens appear in the text.</summary>
        public IReadOnlyList<string> Named(string? text) => string.IsNullOrEmpty(text)
            ? Array.Empty<string>()
            : Entries.Where(e => e.Tokens.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase)))
                     .Select(e => e.Algorithm).ToArray();

        /// <summary>
        /// A dose's algorithm: the one its plan label and beam name name between them; when they name none, the one the
        /// fallback (a folder's or dose set's algorithm) names. Null when unknown, or when more than one is named.
        /// </summary>
        public string? Of(RTDoseWithPlan dose, string? fallback = null)
        {
            if (dose is null) throw new ArgumentNullException(nameof(dose));
            var named = Named(dose.Plan?.RTPlanLabel).Concat(Named(dose.Beam?.BeamName)).Distinct().ToArray();
            if (named.Length == 1) return named[0];
            if (named.Length > 1) return null;
            var fromFallback = Named(fallback);
            return fromFallback.Count == 1 ? fromFallback[0] : null;
        }
    }
}
