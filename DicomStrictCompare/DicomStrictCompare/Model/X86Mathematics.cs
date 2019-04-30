using Alea.Parallel;
using System.Linq;
using System.Threading.Tasks;

namespace DicomStrictCompare
{
    public class X86Mathematics : IMathematics
    {

        public X86Mathematics()
        {

        }

        public int CompareAbsolute(double[] source, double[] target, double tolerance, double epsilon)
        {
            return LinearCompareAbslute(source,  target, tolerance, epsilon);
        }

        public int CompareRelative(double[] source, double[] target, double tolerance, double epsilon)
        {
            return LinearCompareRelative(source, target, tolerance, epsilon);
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

        public int LinearCompareAbslute(double[] source, double[] target, double tolerance, double epsilon)
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



        public int LinearCompareRelative(double[] source, double[] target, double tolerance, double epsilon)
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
                    var sourceLow = sourcei - (tolerance * MaxSource);
                    var sourceHigh = sourcei + (tolerance * MaxSource);
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failed++;
                }

            }
            return failed;
        }

        /// <summary>
        /// returns false if they're different, true if they're within minDoseEvaluated of each other
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

}
