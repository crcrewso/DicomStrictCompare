using Alea;
using Alea.Parallel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DicomStrictCompare
{
    interface IMathematics
    {
        int Compare(double[] source, double[] target, double tolerance, double epsilon);

    }

    public class X86Mathematics : IMathematics
    {

        public X86Mathematics()
        {

        }

        public int Compare(double[] source, double[] target, double tolerance, double epsilon)
        {
            return LinearCompare(source,  target, tolerance, epsilon);
        }

        public int ParallelCompare(double[] source, double[] target, double tolerance, double epsilon)
        {
            int[] failedList = new int[source.Count()];
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * epsilon;
            Parallel.For(0, failedList.Count(), i =>
            {
                double sourcei = source[i];
                double targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    var sourceLow = (1.0 - tolerance) * sourcei;
                    var sourceHigh = (1.0 + tolerance) * sourcei;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failedList[i] = 1;
                }
                else
                {
                    failedList[i] = 0;
                }
            });
            return failedList.AsParallel().Sum();

        }

        public int LinearCompare(double[] source, double[] target, double tolerance, double epsilon)
        {
            int failed = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * tolerance;
            for (int i = 0; i < target.Length; i++)
            {
                var sourcei = source[i];
                var targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    var sourceLow = (1.0 - tolerance) * sourcei;
                    var sourceHigh = (1.0 + tolerance) * sourcei;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failed++;
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
        public int Compare(double[] source, double[] target, double tolerance, double epsilon)
        {
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * tolerance;
            var differenceDoubles = new double[source.Length];
            var absDifferenceDoubles = new double[source.Length];
            var isGTtol = new int[source.Length];

            // filter doses below threshold

            Gpu.Default.For(0, source.Length, i => source[i] = (source[i] > epsilon) ? source[i] : 0);
            Gpu.Default.For(0, target.Length, i => target[i] = (target[i] > epsilon) ? target[i] : 0);


            // find relative difference 
            Gpu.Default.For(0, differenceDoubles.Length, i => differenceDoubles[i] = ((source[i] - target[i])/source[i]) );

            // absolute value of previous table 
            Gpu.Default.For(0, absDifferenceDoubles.Length,
                i => absDifferenceDoubles[i] = (differenceDoubles[i] < 0) ? -1*differenceDoubles[i] : differenceDoubles[i]);

            //determine if relative difference is greater than tolerance 
            // stores 1 as GT tolerance is true
            Gpu.Default.For(0, isGTtol.Length,
                i => isGTtol[i] = (absDifferenceDoubles[i] > tolerance) ? 1 : 0);

            

            int failed = 0;
            failed = Gpu.Default.Sum(isGTtol);

            /*foreach (var value in isGTtol)
            {
                if (value > 0)
                {
                    failed++;
                }
            }*/

            return failed;
        }
    }

}
