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
        public int Compare(ref List<double> source, ref List<double> target, double tolerance, double epsilon)
        {
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double[] sourceDoubles = source.ToArray();
            double[] targetDoubles = target.ToArray();
            double MinDoseEvaluated = MaxSource * tolerance;
            var differenceDoubles = new double[sourceDoubles.Length];
            var absDifferenceDoubles = new double[sourceDoubles.Length];
            var isGTlerance = new double[sourceDoubles.Length];

            // filter doses below threshold
            Parallel.For(0, sourceDoubles.Length, i => sourceDoubles[i] = (sourceDoubles[i] > epsilon) ? sourceDoubles[i] : 0);
            Parallel.For(0, targetDoubles.Length, i => targetDoubles[i] = (targetDoubles[i] > epsilon) ? targetDoubles[i] : 0);


            // find relative difference 
            Parallel.For(0, differenceDoubles.Length, i => differenceDoubles[i] = ((sourceDoubles[i] - targetDoubles[i])/sourceDoubles[i]) );

            // absolute value of previous table 
            Parallel.For(0, absDifferenceDoubles.Length,
                i => absDifferenceDoubles[i] = (differenceDoubles[i] < 0) ? -1*differenceDoubles[i] : differenceDoubles[i]);

            //determine if relative difference is greater than tolerance 
            // stores 1 as GT tolerance is true
            Parallel.For(0, isGTlerance.Length,
                i => isGTlerance[i] = (absDifferenceDoubles[i] > tolerance) ? 1 : 0);


            int failed = 0;
            foreach (var value in isGTlerance)
            {
                if (value > 0)
                {
                    failed++;
                }
            }

            return failed;
        }
    }

}
