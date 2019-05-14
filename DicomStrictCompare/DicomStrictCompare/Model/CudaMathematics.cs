using Alea;
using Alea.Parallel;
using System.Linq;
using System;

namespace DicomStrictCompare
{
    public class CudaMathematics : IMathematics
    {

        public CudaMathematics()
        {
            if (Alea.DeviceArch.Default.Major < 2)
                throw new SystemException("I do not have a gpu");
            
        }

        public override System.Tuple<int, int> CompareAbsolute(double[] source, double[] target, double tolerance, double epsilon)
        {
            return CompareAbsoluteOld(source, target, tolerance, epsilon);
        }


        [GpuManaged]
        public System.Tuple<int, int> CompareAbsoluteOld(double[] source, double[] target, double tolerance, double epsilon)
        {
            System.Diagnostics.Debug.WriteLine("starting an absolute comparison on GPU");
            if (source.Length != target.Length)
                throw new ArgumentException("The source and target lengths need to match");

            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * epsilon;
            int[] isCountedArray = new int[source.Length];
            double[] differenceDoubles = new double[source.Length];
            double[] absDifferenceDoubles = new double[source.Length];
            int[] isGTtol = new int[source.Length];
            int failed = 0;
            int isCounted = 0;
            Gpu gpu = Gpu.Default;

            // filter doses below threshold
            // TODO: should failure be -1?
            gpu.For(0, source.Length, i => source[i] = (source[i] > epsilon) ? source[i] : 0);
            gpu.For(0, target.Length, i => target[i] = (target[i] > epsilon) ? target[i] : 0);
            gpu.For(0, source.Length, i => source[i] = (target[i] > epsilon) ? source[i] : 0);
            gpu.For(0, target.Length, i => target[i] = (source[i] > epsilon) ? target[i] : 0);
            gpu.For(0, source.Length, i => isCountedArray[i] = (source[i] > 0) ? 1 : 0);

            // find relative difference 
            gpu.For(0, differenceDoubles.Length, i => differenceDoubles[i] = ((source[i] - target[i])/source[i]) );

            // absolute value of previous table 
            gpu.For(0, absDifferenceDoubles.Length,
                i => absDifferenceDoubles[i] = (differenceDoubles[i] < 0) ? -1*differenceDoubles[i] : differenceDoubles[i]);

            //determine if relative difference is greater than minDoseEvaluated 
            // stores 1 as GT minDoseEvaluated is true
           gpu.For(0, isGTtol.Length,
                i => isGTtol[i] = (absDifferenceDoubles[i] > tolerance) ? 1 : 0);

            isCounted = gpu.Sum(isCountedArray);


            failed = gpu.Sum(isGTtol);

            System.Diagnostics.Debug.WriteLine("finished an absolute comparison on GPU");

            return new System.Tuple<int, int>(failed, isCounted);
        }

        [GpuManaged]
        public override System.Tuple<int, int> CompareRelative(double[] source, double[] target, double tolerance, double epsilon)
        {
            System.Diagnostics.Debug.WriteLine("starting a relative comparison on GPU");
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * epsilon;
            double sourceVariance = MaxSource * tolerance;
            int[] isCountedArray = new int[source.Length];
            double[] sourceLow = new double[source.Length];
            double[] sourceHigh = new double[source.Length];
            double[] differenceDoubles = new double[source.Length];
            double[] absDifferenceDoubles = new double[source.Length];
            int[] isGTtol = new int[source.Length];

            // filter doses below threshold
            // TODO: should failure be -1?
            Gpu.Default.For(0, source.Length, i => source[i] = (source[i] > epsilon) ? source[i] : 0);
            Gpu.Default.For(0, source.Length, i => target[i] = (source[i] > epsilon) ? target[i] : 0);
            Gpu.Default.For(0, target.Length, i => target[i] = (target[i] > epsilon) ? target[i] : 0);
            Gpu.Default.For(0, target.Length, i => source[i] = (target[i] > epsilon) ? source[i] : 0);
            Gpu.Default.For(0, source.Length, i => isCountedArray[i] = (source[i] > 0) ? 1 : 0);
            //determine if relative difference is greater than minDoseEvaluated 
            // stores 1 as GT minDoseEvaluated is true
            Gpu.Default.For(0, isGTtol.Length,
                i => isGTtol[i] = (((source[i] - sourceVariance) < target[i]) && ((source[i] + sourceVariance) > target[i])) ? 0 : 1);
            int failed = 0;
            failed = Gpu.Default.Sum(isGTtol);
            int isCounted = Gpu.Default.Sum(isCountedArray);
            System.Diagnostics.Debug.WriteLine("finished a relative comparison on GPU");

            return new System.Tuple<int, int>(failed, isCounted);
        }

        [GpuManaged]
        public System.Tuple<int, int> CompareAbsoluteOpt(double[] source, double[] target, double tolerance, double ThreshholdTol)
        {
            System.Diagnostics.Debug.WriteLine("starting an absolute comparison on GPU");
            if (source.Length != target.Length)
                throw new ArgumentException("The source and target lengths need to match");

            double epsilon = ThreshholdTol;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = (MaxSource * epsilon);
            double zero = 0.0;
            double lowMultiplier = (1 - tolerance);
            double highMultiplier = (1 + tolerance);
            int failed = 0;
            int isCounted = 0;
            Gpu gpu = Gpu.Default;

            // filter doses below threshold
            // TODO: should failure be -1?

            int dimension = source.Length;
            double[] sourceOnGPU = gpu.Allocate(source);
            double[] targetOnGPU = gpu.Allocate(target);
            double[] isCountedArray = gpu.Allocate<double>(dimension);
            double[] sourceOnGPULow = gpu.Allocate<double>(dimension);
            double[] sourceOnGPUHigh = gpu.Allocate<double>(dimension);
            double[] isGTtol = gpu.Allocate<double>(dimension);

            gpu.For(0, dimension, i => sourceOnGPU[i] = (sourceOnGPU[i] > epsilon) ? sourceOnGPU[i] : zero);
            gpu.For(0, dimension, i => targetOnGPU[i] = (targetOnGPU[i] > epsilon) ? targetOnGPU[i] : zero);
            gpu.For(0, dimension, i => sourceOnGPU[i] = (targetOnGPU[i] > epsilon) ? sourceOnGPU[i] : zero);
            gpu.For(0, dimension, i => targetOnGPU[i] = (sourceOnGPU[i] > epsilon) ? targetOnGPU[i] : zero);
            gpu.For(0, dimension, i => isCountedArray[i] = (sourceOnGPU[i] > zero) ? 1.0 : zero);

            gpu.For(0, dimension, i => sourceOnGPULow[i] = lowMultiplier * sourceOnGPU[i]);
            gpu.For(0, dimension, i => sourceOnGPUHigh[i] = highMultiplier * sourceOnGPU[i]);

            //determine if relative difference is greater than minDoseEvaluated 
            // stores 1 as GT minDoseEvaluated is true
            gpu.For(0, isGTtol.Length,
                 i => isGTtol[i] = (targetOnGPU[i] < sourceOnGPULow[i] || targetOnGPU[i] > sourceOnGPUHigh[i]) ? 1 : 0);
            isCounted = (int)gpu.Sum(isCountedArray);
            failed = (int)gpu.Sum(isGTtol);

            Gpu.Free(sourceOnGPU);
            Gpu.Free(targetOnGPU);
            Gpu.Free(sourceOnGPULow);
            Gpu.Free(sourceOnGPUHigh);
            Gpu.Free(isCountedArray);
            Gpu.Free(isGTtol);
            System.Diagnostics.Debug.WriteLine("finished an absolute comparison on GPU");
            //gpu.Dispose();

            return new System.Tuple<int, int>(failed, isCounted);
        }
    }

}
