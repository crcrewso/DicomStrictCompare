using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using DSClibrary.Parsers;

namespace DSClibrary.Tests
{
    [TestClass()]
    public class PTWScanTests
    {
        string? basicScan;
        string[]? basicScanSplit;
        string? testFileName;
        string? testFilePath;

        [ClassInitialize()]
        public static void Setup(TestContext context)
        {
            string setupPath = Path.GetTempPath();
            setupPath = Path.Join(setupPath, "PTWScanTest");
            string setupTestFile = "scanTestFile.mcc";
            System.IO.Directory.CreateDirectory(setupPath);
            System.IO.File.WriteAllBytes(Path.Join(setupPath, setupTestFile), Properties.Resources.X06_OPEN_30X30_PDDCRIN_TBA_180406_16_02_52);
            
        }

        [TestInitialize()]
        public void testSetup()
        {
            Debug.WriteLine("Test initializing");
            basicScan = "\tBEGIN_SCAN  1\r\n\t\tTASK_NAME=tba PDD Profiles\r\n\t\tPROGRAM=tbaScan\r\n\t\tMEAS_DATE=06-Apr-2018 15:36:30\r\n\t\tLINAC=MIRAGE\r\n\t\tMODALITY=X\r\n\t\tISOCENTER=1000.00\r\n\t\tINPLANE_AXIS=Inplane\r\n\t\tCROSSPLANE_AXIS=Crossplane\r\n\t\tDEPTH_AXIS=Depth\r\n\t\tINPLANE_AXIS_DIR=GUN_TARGET\r\n\t\tCROSSPLANE_AXIS_DIR=LEFT_RIGHT\r\n\t\tDEPTH_AXIS_DIR=UP_DOWN\r\n\t\tENERGY=6.00\r\n\t\tNOMINAL_DMAX=16.00\r\n\t\tSSD=1000.00\r\n\t\tSCD=450.00\r\n\t\tBLOCK=0\r\n\t\tWEDGE_ANGLE=0.00\r\n\t\tFIELD_INPLANE=300.00\r\n\t\tFIELD_CROSSPLANE=300.00\r\n\t\tFIELD_TYPE=RECTANGULAR\r\n\t\tGANTRY=0.00\r\n\t\tGANTRY_UPRIGHT_POSITION=0\r\n\t\tGANTRY_ROTATION=CW\r\n\t\tCOLL_ANGLE=0.00\r\n\t\tCOLL_OFFSET_INPLANE=0.00\r\n\t\tCOLL_OFFSET_CROSSPLANE=0.00\r\n\t\tSCAN_DEVICE=MP3\r\n\t\tSCAN_DEVICE_SETUP=BARA_LEFT_RIGHT\r\n\t\tELECTROMETER=TANDEM\r\n\t\tRANGE_FIELD=AUTO\r\n\t\tRANGE_REFERENCE=AUTO\r\n\t\tDETECTOR=THIMBLE_CHAMBER\r\n\t\tDETECTOR_SUBCODE=SEMIFLEX\r\n\t\tDETECTOR_RADIUS=2.75\r\n\t\tDETECTOR_NAME=PTW 31010 Semiflex\r\n\t\tDETECTOR_SN=4351\r\n\t\tDETECTOR_CALIBRATION=300000000.00\r\n\t\tDETECTOR_IS_CALIBRATED=0\r\n\t\tDETECTOR_REFERENCE=THIMBLE_CHAMBER\r\n\t\tDETECTOR_REFERENCE_SUBCODE=SEMIFLEX\r\n\t\tDETECTOR_REFERENCE_RADIUS=2.75\r\n\t\tDETECTOR_REFERENCE_NAME=PTW 31010 Semiflex\r\n\t\tDETECTOR_REFERENCE_SN=4311\r\n\t\tDETECTOR_REFERENCE_IS_CALIBRATED=0\r\n\t\tDETECTOR_REFERENCE_CALIBRATION=300000000.00\r\n\t\tDETECTOR_HV=-400.0\r\n\t\tDETECTOR_REFERENCE_HV=-400.0\r\n\t\tFILTER=FF\r\n\t\tREF_FIELD_DEPTH=0.00\r\n\t\tREF_FIELD_DEFINED=WATER_SURFACE\r\n\t\tREF_FIELD_INPLANE=100.00\r\n\t\tREF_FIELD_CROSSPLANE=100.00\r\n\t\tREF_SCAN_POSITIONS=-5.00;-4.00;-3.00;-2.00;-1.00;0.00;1.50;3.00;4.50;6.00;7.50;9.00;10.50;12.00;13.50;15.00;16.50;18.00;19.50;20.00;25.00;30.00;35.00;40.00;45.00;50.00;60.00;70.00;80.00;90.00;100.00;110.00;120.00;130.00;140.00;150.00;160.00;170.00;180.00;190.00;200.00;210.00;220.00;230.00;240.00;250.00;260.00;270.00;280.00;290.00;300.00;\r\n\t\tSCAN_CURVETYPE=PDD\r\n\t\tSCAN_OFFAXIS_INPLANE=0.00\r\n\t\tSCAN_OFFAXIS_CROSSPLANE=0.00\r\n\t\tSCAN_ANGLE=0.00\r\n\t\tSCAN_DIAGONAL=NOT_DIAGONAL\r\n\t\tSCAN_DIRECTION=NEGATIVE\r\n\t\tMEAS_MEDIUM=WATER\r\n\t\tMEAS_PRESET=REFERENCE_DOSEMETER\r\n\t\tMEAS_TIME=0.700\r\n\t\tMEAS_UNIT=A.U.\r\n\t\tSCAN_SPEEDS=50.00; 10.00;150.00; 20.00;999.00; 50.00;\r\n\t\tDELAY_TIMES=50.00; 0.500;150.00; 0.200;999.90; 0.000;\r\n\t\tPRESSURE=1013.25\r\n\t\tTEMPERATURE=20.00\r\n\t\tNORM_TEMPERATURE=20.00\r\n\t\tCORRECTION_FACTOR=1.0000\r\n\t\tEXPECTED_MAX_DOSE_RATE=3.00\r\n\t\tBEGIN_DATA\r\n\t\t\t-5.00\t\t1.0570E+00\t\t3.6648E+00\r\n\t\t\t-4.00\t\t1.0634E+00\t\t3.6648E+00\r\n\t\t\t-3.00\t\t1.0742E+00\t\t3.6630E+00\r\n\t\t\t-2.00\t\t1.0899E+00\t\t3.6648E+00\r\n\t\t\t-1.00\t\t1.1144E+00\t\t3.6666E+00\r\n\t\t\t0.00\t\t1.1514E+00\t\t3.6630E+00\r\n\t\t\t1.50\t\t1.2342E+00\t\t3.6666E+00\r\n\t\t\t3.00\t\t1.3817E+00\t\t3.6594E+00\r\n\t\t\t4.50\t\t1.5420E+00\t\t3.6630E+00\r\n\t\t\t6.00\t\t1.6354E+00\t\t3.6630E+00\r\n\t\t\t7.50\t\t1.6889E+00\t\t3.6630E+00\r\n\t\t\t9.00\t\t1.7214E+00\t\t3.6630E+00\r\n\t\t\t10.50\t\t1.7391E+00\t\t3.6630E+00\r\n\t\t\t12.00\t\t1.7456E+00\t\t3.6648E+00\r\n\t\t\t13.50\t\t1.7474E+00\t\t3.6630E+00\r\n\t\t\t15.00\t\t1.7450E+00\t\t3.6630E+00\r\n\t\t\t16.50\t\t1.7409E+00\t\t3.6612E+00\r\n\t\t\t18.00\t\t1.7304E+00\t\t3.6648E+00\r\n\t\t\t19.50\t\t1.7258E+00\t\t3.6630E+00\r\n\t\t\t20.00\t\t1.7219E+00\t\t3.6630E+00\r\n\t\t\t25.00\t\t1.6914E+00\t\t3.6630E+00\r\n\t\t\t30.00\t\t1.6614E+00\t\t3.6630E+00\r\n\t\t\t35.00\t\t1.6300E+00\t\t3.6630E+00\r\n\t\t\t40.00\t\t1.5978E+00\t\t3.6612E+00\r\n\t\t\t45.00\t\t1.5646E+00\t\t3.6666E+00\r\n\t\t\t50.00\t\t1.5354E+00\t\t3.6648E+00\r\n\t\t\t60.00\t\t1.4725E+00\t\t3.6648E+00\r\n\t\t\t70.00\t\t1.4128E+00\t\t3.6630E+00\r\n\t\t\t80.00\t\t1.3531E+00\t\t3.6648E+00\r\n\t\t\t90.00\t\t1.2953E+00\t\t3.6630E+00\r\n\t\t\t100.00\t\t1.2376E+00\t\t3.6666E+00\r\n\t\t\t110.00\t\t1.1853E+00\t\t3.6612E+00\r\n\t\t\t120.00\t\t1.1306E+00\t\t3.6666E+00\r\n\t\t\t130.00\t\t1.0795E+00\t\t3.6666E+00\r\n\t\t\t140.00\t\t1.0314E+00\t\t3.6648E+00\r\n\t\t\t150.00\t\t985.75E-03\t\t3.6630E+00\r\n\t\t\t160.00\t\t938.14E-03\t\t3.6666E+00\r\n\t\t\t170.00\t\t896.26E-03\t\t3.6612E+00\r\n\t\t\t180.00\t\t854.05E-03\t\t3.6630E+00\r\n\t\t\t190.00\t\t813.76E-03\t\t3.6630E+00\r\n\t\t\t200.00\t\t775.05E-03\t\t3.6648E+00\r\n\t\t\t210.00\t\t738.70E-03\t\t3.6648E+00\r\n\t\t\t220.00\t\t703.69E-03\t\t3.6630E+00\r\n\t\t\t230.00\t\t671.58E-03\t\t3.6612E+00\r\n\t\t\t240.00\t\t638.82E-03\t\t3.6630E+00\r\n\t\t\t250.00\t\t609.14E-03\t\t3.6612E+00\r\n\t\t\t260.00\t\t579.65E-03\t\t3.6612E+00\r\n\t\t\t270.00\t\t550.59E-03\t\t3.6648E+00\r\n\t\t\t280.00\t\t524.07E-03\t\t3.6648E+00\r\n\t\t\t290.00\t\t500.49E-03\t\t3.6612E+00\r\n\t\t\t300.00\t\t476.50E-03\t\t3.6612E+00\r\n\t\tEND_DATA\r\n\tEND_SCAN  1";
            basicScanSplit = basicScan.Split("\r\n");
            testFilePath = Path.GetTempPath();
            testFilePath = Path.Join(testFilePath, "PTWScanTest");
            testFileName = "scanTestFile.mcc";

        }

        [TestCleanup()]
        public void testTearDown()
        {

        }

        [ClassCleanup()]
        public static void Teardown()
        {
            string setupPath = Path.GetTempPath();
            setupPath = Path.Join(setupPath, "PTWScanTest");
            System.IO.Directory.Delete(setupPath, true);
        }

        [TestMethod()]
        public void PTWScanTest()
        {
            var scan = new PTWScan(basicScanSplit);
            Assert.AreEqual("tba PDD Profiles", scan.Task_Name);
        }

        [TestMethod()]
        public void PTWFileReadTest()
        {
            var temp = new ParsePTW(Path.Join(testFilePath, testFileName));
            Assert.IsTrue(temp.PTWScans.Count > 0);
        }

        [TestMethod()]
        public void PTWFileCorrectCount()
        {
            var temp = new ParsePTW(Path.Join(testFilePath, testFileName));
            Assert.AreEqual(11, temp.PTWScans.Count);
        }


        [TestMethod()]
        public void PTWFileFormatCorrect()
        {
            var temp = new ParsePTW(Path.Join(testFilePath, testFileName));
            Assert.AreEqual("CC-Export V1.9", temp.FileFormat);
        }

    }
}