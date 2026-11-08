using System.IO;

namespace DSClibraryTests
{
    /// <summary>Paths to the files the profile tests measure against.</summary>
    internal static class TestData
    {
        /// <summary>
        /// The committed PTW water tank scan: 11 scans in one file, 6 MV photons, 300 x 300 mm,
        /// SSD 1000 mm. Linked into the output by DSClibraryTests.csproj.
        /// </summary>
        public static string PTWScan => Path.Combine(System.AppContext.BaseDirectory, "TestData", "ptw-scan.mcc");
    }
}
