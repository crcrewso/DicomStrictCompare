using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alea;
using Alea.CSharp;
using Alea.Parallel;

namespace DicomStrictCompare
{
    interface IMathematics
    {
        int Compare(ref List<double> source, ref List<double> target, double tolerance, double epsilon);

    }

    public class X86Mathematics : IMathematics
    {

        public X86Mathematics()
        {

        }

        public  int Compare(ref List<double> source, ref List<double> target, double tolerance, double epsilon)
        {
            int failed = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * tolerance;
            for (int i = 0; i < target.Count; i++)
            {
                var sourcei = source[i];
                var targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    if (!IsWithinTolerance(sourcei, targeti, tolerance))
                    {
                        failed++;
                    }
                }

            }
            return failed;
        }

        /// <summary>
        /// returns false if they're different, true if they're within tolerance of each other
        /// assumes epsilon boundary checking has already been done. This was broken out for easier testing. 
        /// </summary>
        /// <param name="sourcei"></param>
        /// <param name="targeti"></param>
        /// <param name="tolerance"></param>
        /// <returns></returns>
        protected bool IsWithinTolerance(double sourcei, double targeti, double tolerance)
        {
            var temp = sourcei - targeti;
            temp = (temp > 0) ? temp : -1 * temp;
            temp = temp / sourcei;
            if (temp > tolerance)
                return false ;
            return true;
        }


    }



    public class CudaMathematics : IMathematics
    {

        public static void Kernel(double[] result, double[] source, double[] target, double tolerance, double epsilon)
        {
            var start = blockIdx.x * blockDim.x + threadIdx.x;
            var stride = gridDim.x * blockDim.x;
            for (var i = start; i < result.Length; i += stride)
            {
                result[i] = (target[i] - source[i]) / source[i];
                result[i] = (result[i] > 0) ? result[i] : -1 * result[i];
                result[i] = (source[i] > epsilon) ? result[i] : 0;
                result[i] = (target[i] > epsilon) ? result[i] : 0;
                result[i] = (result[i] > tolerance) ? 1 : 0;

            }
        }


        public int Compare(ref List<double> source, ref List<double> target, double tolerance, double epsilon)
        {
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            double MinDoseEvaluated = MaxSource * tolerance;

            var gpu = Gpu.Default;
            var lp = new LaunchParam(16, 256);
            var result = new double[sourceDoubles.Length];
            var filteredSource = new double[sourceDoubles.Length];
            var filteredTarget = new double[targetDoubles.Length];

            gpu.Launch(Kernel, lp, result, sourceDoubles, targetDoubles, tolerance, epsilon);

            double failed = gpu.Sum(result);
            return (int)failed;
        }
    }

}
