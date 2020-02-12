using Microsoft.VisualStudio.TestTools.UnitTesting;
using DicomStrictCompare;

namespace DicomStrictCompare.Tests
{
    [TestClass()]
    public class DoseComparisonsTests: X86Mathematics
    {
        int threadMin, threadMax;
        X86Mathematics mathematics;
        enum DtaTypes { t0d0p0mm, t0d0p0vox, t10d0p0vox, t10d10p10vox, t10d10p10voxRel, t0d0p0mmTrim10, t0d0p0voxTrim10 };
        System.Collections.Generic.Dictionary<string, DoseFile> refDoseFiles;

        System.Collections.Generic.Dictionary<DtaTypes, Model.Dta> dtas;

        [ClassInitialize()]
        public void Initialize()
        {
            threadMin = 1;
            threadMax = System.Environment.ProcessorCount;
            mathematics = new X86Mathematics();
            refDoseFiles = new System.Collections.Generic.Dictionary<string, DoseFile>();
            dtas[DtaTypes.t0d0p0mm] = new Model.Dta(true, 0, 0, 0, false, 0);
            dtas[DtaTypes.t0d0p0vox] = new Model.Dta(false, 0, 0, 0, false, 0);
            dtas[DtaTypes.t10d0p0vox] = new Model.Dta(false, 0.10, 0, 0, false, 0);
            dtas[DtaTypes.t10d10p10vox] = new Model.Dta(false, 0.10, 0.10, 0, false, 0);
            dtas[DtaTypes.t10d10p10voxRel] = new Model.Dta(false, 0.10, 0.10, 0, true, 0);
            dtas[DtaTypes.t0d0p0mmTrim10] = new Model.Dta(true, 0, 0, 0, false, 10);
            dtas[DtaTypes.t0d0p0voxTrim10] = new Model.Dta(false, 0, 0, 0, false, 10);
            refDoseFiles["X"] = new DoseFile();
            refDoseFiles["F"] = new DoseFile();
            refDoseFiles["e"] = new DoseFile();
        }



        [TestMethod()]
        public void CompareParallelSampleTest()
        {
            Model.DoseMatrixOptimal refDose, targetDose;
            refDose = new Model.DoseMatrixOptimal( refDoseFiles["X"].DoseMatrix());
            targetDose = new Model.DoseMatrixOptimal(); 
            System.Tuple<int, int> result = mathematics.CompareParallel(refDose, targetDose, dtas[DtaTypes.t0d0p0mm], threadMin, Type.relative);
            int expectedFail = 0;
            Assert.AreEqual(targetDose.Count, result.Item2); // confirm all voxels are compared 
            Assert.AreEqual(expectedFail, result.Item1); //confirms the number of failed voxels is zero
        }
    }
}