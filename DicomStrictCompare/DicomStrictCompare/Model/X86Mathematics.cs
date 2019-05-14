using Alea.Parallel;
using System.Linq;
using System.Threading.Tasks;
using EvilDICOM.RT;


namespace DicomStrictCompare
{
    public class X86Mathematics : IMathematics
    {

        public X86Mathematics()
        {

        }

        public override System.Tuple<int, int> CompareAbsolute(double[] source, double[] target, double tolerance, double epsilon)
        {
            return LinearCompareAbslute(source,  target, tolerance, epsilon);
        }

        public override System.Tuple<int, int> CompareRelative(double[] source, double[] target, double tolerance, double epsilon)
        {
            return LinearCompareRelative(source, target, tolerance, epsilon);
        }

        public System.Tuple<int, int> ParallelCompare(double[] source, double[] target, double tolerance, double epsilon)
        {
            int[] failedList = new int[source.Count()];
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * epsilon;
            _ = Parallel.For(0, failedList.Count(), i =>
              {
                  double sourcei = source[i];
                  double targeti = target[i];
                  if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                  {
                      double sourceLow = (1.0 - tolerance) * sourcei;
                      double sourceHigh = (1.0 + tolerance) * sourcei;
                      if (targeti < sourceLow || targeti > sourceHigh)
                          failedList[i] = 1;
                  }
                  else
                  {
                      failedList[i] = 0;
                  }
              });

            return new System.Tuple<int, int>(failedList.AsParallel().Sum(), 0);
        }

        public System.Tuple<int, int> LinearCompareAbslute(double[] source, double[] target, double tolerance, double epsilon)
        {
            int failed = 0;
            int TotalCompared = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * epsilon;
            for (int i = 0; i < target.Length; i++)
            {
                double sourcei = source[i];
                double targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    TotalCompared++;
                    double sourceLow = (1.0 - tolerance) * sourcei;
                    double sourceHigh = (1.0 + tolerance) * sourcei;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failed++;
                }

            }
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            System.Tuple<int, int> ret = new System.Tuple<int, int>(failed, TotalCompared);
            return ret;
        }



        public System.Tuple<int, int> LinearCompareRelative(double[] source, double[] target, double tolerance, double epsilon)
        {
            int failed = 0;
            int TotalCompared = 0;
            double MaxSource = source.Max();
            double MaxTarget = target.Max();
            double MinDoseEvaluated = MaxSource * epsilon;
            double sourceVariance = MaxSource * tolerance;
            for (int i = 0; i < target.Length; i++)
            {
                double sourcei = source[i];
                double targeti = target[i];
                if (sourcei > MinDoseEvaluated && targeti > MinDoseEvaluated)
                {
                    TotalCompared++;
                    double sourceLow = sourcei - sourceVariance;
                    double sourceHigh = sourcei + sourceVariance;
                    if (targeti < sourceLow || targeti > sourceHigh)
                        failed++;
                }

            }
            System.Diagnostics.Debug.WriteLine("Failed: " + failed + " of " + TotalCompared);
            System.Tuple<int, int> ret = new System.Tuple<int, int>(failed, TotalCompared);
            return ret;
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
            double temp = sourcei - targeti;
            temp = (temp > 0) ? temp : -1 * temp;
            temp = temp / sourcei;
            if (temp > tolerance)
                return false ;
            return true;
        }


    }

}
