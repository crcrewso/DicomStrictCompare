using System;
using System.IO;

namespace DSClibraryTests.Properties
{
    /// <summary>
    /// The test resources, read from the repository's <c>resources/</c> folder on first use.
    /// <para>
    /// This replaces the generated <c>Resources.Designer.cs</c> and its <c>Resources.resx</c> of
    /// <c>ResXFileRef</c> entries, with the same member names so no test changed. Two things were
    /// wrong with the resx. On Windows it embedded every referenced file into the test assembly:
    /// 662 MB in <c>DSCLibrary.Tests.dll</c>, rebuilt and copied on every build. On Linux (measured on
    /// Ubuntu 24.04, SDK 10.0.403, 2026-11-09) the resource generator could not resolve the
    /// backslash file references, produced a 12-byte resource stream without an error, and every
    /// test touching <c>Properties.Resources</c> failed with an <c>EndOfStreamException</c>.
    /// </para>
    /// <para>
    /// Members are read on each access, as the generated class did; a test that wants to measure a
    /// comparison without the read should keep the array it gets.
    /// </para>
    /// </summary>
    internal static class Resources
    {
        private static string Path(string fileName)
        {
            string? dir = TestData.ResourcesDirectory;
            if (dir is null)
                throw new FileNotFoundException(
                    "The repository's resources/ folder was not found above " + AppContext.BaseDirectory
                    + "; the DICOM test data is read from there rather than embedded.", fileName);
            return System.IO.Path.Combine(dir, fileName);
        }

        private static byte[] Bytes(string fileName) => File.ReadAllBytes(Path(fileName));

        private static string Text(string fileName) => File.ReadAllText(Path(fileName));

        public static byte[] RD_UnitTest_P1Ref_e_100A_10_0_1_13 => Bytes("RD.UnitTest.P1Ref.e-100A-10.0-1-13.dcm");
        public static byte[] RD_UnitTest_P1Ref_F_100A_10_0_1 => Bytes("RD.UnitTest.P1Ref.F-100A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P1Ref_X_100A_10_0_1 => Bytes("RD.UnitTest.P1Ref.X-100A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P1_2_mm_e_100A_10_0_02_3 => Bytes("RD.UnitTest.P1 2 mm.e-100A-10.0-02-3.dcm");
        public static byte[] RD_UnitTest_P1_2_mm_F_100A_10_0_0_2 => Bytes("RD.UnitTest.P1 2 mm.F-100A-10.0-0.2.dcm");
        public static byte[] RD_UnitTest_P1_2_mm_X_100A_10_0_0_2 => Bytes("RD.UnitTest.P1 2 mm.X-100A-10.0-0.2.dcm");
        public static byte[] RD_UnitTest_P1_5_mm_e_100A_10_0_5_13 => Bytes("RD.UnitTest.P1 5 mm.e-100A-10.0-5-13.dcm");
        public static byte[] RD_UnitTest_P1_5_mm_F_100A_10_0_5 => Bytes("RD.UnitTest.P1 5 mm.F-100A-10.0-5.dcm");
        public static byte[] RD_UnitTest_P1_5_mm_X_100A_10_0_5 => Bytes("RD.UnitTest.P1 5 mm.X-100A-10.0-5.dcm");
        public static byte[] RD_UnitTest_P1_Acuros_F_100X_10_0_1 => Bytes("RD.UnitTest.P1 Acuros.F-100X-10.0-1.dcm");
        public static byte[] RD_UnitTest_P1_Acuros_X_100X_10_0_1 => Bytes("RD.UnitTest.P1 Acuros.X-100X-10.0-1.dcm");
        public static byte[] RD_UnitTest_P2_10_1_F_100X_10_1_1 => Bytes("RD.UnitTest.P2 10.1.F-100X-10.1-1.dcm");
        public static byte[] RD_UnitTest_P2_10_1_X_100X_10_1_1 => Bytes("RD.UnitTest.P2 10.1.X-100X-10.1-1.dcm");
        public static byte[] RD_UnitTest_P3_11_0_F_100X_10_1_1 => Bytes("RD.UnitTest.P3 11.0.F-100X-10.1-1.dcm");
        public static byte[] RD_UnitTest_P3_11_0_X_100X_10_1_1 => Bytes("RD.UnitTest.P3 11.0.X-100X-10.1-1.dcm");
        public static byte[] RD_UnitTest_P4_E_2D_e_100A_10_0_1_12 => Bytes("RD.UnitTest.P4 E 2D.e-100A-10.0-1-12.dcm");
        public static byte[] RD_UnitTest_P4_E_None_e_100A_10_0_1_N => Bytes("RD.UnitTest.P4 E None.e-100A-10.0-1-N.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_e_095A_10_0_1_13 => Bytes("RD.UnitTest.P5 Varied MU.e-095A-10.0-1-13.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_e_099A_10_0_1_13 => Bytes("RD.UnitTest.P5 Varied MU.e-099A-10.0-1-13.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_e_101A_10_0_1_13 => Bytes("RD.UnitTest.P5 Varied MU.e-101A-10.0-1-13.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_F_095A_10_0_1 => Bytes("RD.UnitTest.P5 Varied MU.F-095A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_F_099A_10_0_1 => Bytes("RD.UnitTest.P5 Varied MU.F-099A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_F_101A_10_0_1 => Bytes("RD.UnitTest.P5 Varied MU.F-101A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_X_095A_10_0_1 => Bytes("RD.UnitTest.P5 Varied MU.X-095A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_X_099A_10_0_1 => Bytes("RD.UnitTest.P5 Varied MU.X-099A-10.0-1.dcm");
        public static byte[] RD_UnitTest_P5_Varied_MU_X_101A_10_0_1 => Bytes("RD.UnitTest.P5 Varied MU.X-101A-10.0-1.dcm");
        public static byte[] RP_UnitTest_P1Ref => Bytes("RP.UnitTest.P1Ref.dcm");
        public static byte[] RP_UnitTest_P1_2_mm => Bytes("RP.UnitTest.P1 2 mm.dcm");
        public static byte[] RP_UnitTest_P1_5_mm => Bytes("RP.UnitTest.P1 5 mm.dcm");
        public static byte[] RP_UnitTest_P1_Acuros => Bytes("RP.UnitTest.P1 Acuros.dcm");
        public static byte[] RP_UnitTest_P2_10_1 => Bytes("RP.UnitTest.P2 10.1.dcm");
        public static byte[] RP_UnitTest_P3_11_0 => Bytes("RP.UnitTest.P3 11.0.dcm");
        public static byte[] RP_UnitTest_P4_E_2D => Bytes("RP.UnitTest.P4 E 2D.dcm");
        public static byte[] RP_UnitTest_P4_E_None => Bytes("RP.UnitTest.P4 E None.dcm");
        public static byte[] RP_UnitTest_P5_Varied_MU => Bytes("RP.UnitTest.P5 Varied MU.dcm");
        public static string SNC_Patient_Comparisons => Text("SNC Patient Comparisons.csv");
        public static string TextFile1 => Text("TextFile1.txt");
    }
}
