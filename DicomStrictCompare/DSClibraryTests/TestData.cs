using System;
using System.IO;
using System.Linq;

namespace DSClibraryTests
{
    /// <summary>Paths to the files the tests measure against.</summary>
    internal static class TestData
    {
        /// <summary>
        /// The committed PTW water tank scan: 11 scans in one file, 6 MV photons, 300 x 300 mm,
        /// SSD 1000 mm. Linked into the output by DSClibraryTests.csproj.
        /// </summary>
        public static string PTWScan => Path.Combine(AppContext.BaseDirectory, "TestData", "ptw-scan.mcc");

        /// <summary>
        /// The repository's resources folder, found by walking up from the test assembly.
        /// <para>
        /// Not linked into the output like the mcc is: the DICOM resources are about 650 MB and
        /// copying them per build would be absurd. Walking up is the usual answer for data-heavy
        /// tests, and <see cref="Found"/> lets a test say plainly that it could not locate them rather
        /// than failing in a way that looks like a parsing bug.
        /// </para>
        /// </summary>
        public static string? ResourcesDirectory { get; } = FindResources();

        public static bool Found => ResourcesDirectory is not null;

        /// <summary>The 26 RT Dose files, sorted, or empty when the folder was not found.</summary>
        public static string[] DoseFiles => Found
            ? Directory.GetFiles(ResourcesDirectory!, "RD*.dcm").OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        /// <summary>The 9 RT Plan files, sorted, or empty when the folder was not found.</summary>
        public static string[] PlanFiles => Found
            ? Directory.GetFiles(ResourcesDirectory!, "RP*.dcm").OrderBy(f => f, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        private static string? FindResources()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, "resources");
                if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "RD*.dcm").Length > 0)
                    return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
