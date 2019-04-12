using Alea;
using Alea.Parallel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvilDICOM.RT;
using System;

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
            // TODO: should failure be -1?
            Gpu.Default.For(0, source.Length, i => source[i] = (source[i] > epsilon) ? source[i] : 0);
            Gpu.Default.For(0, target.Length, i => target[i] = (target[i] > epsilon) ? target[i] : 0);


            // find relative difference 
            Gpu.Default.For(0, differenceDoubles.Length, i => differenceDoubles[i] = ((source[i] - target[i])/source[i]) );

            // absolute value of previous table 
            Gpu.Default.For(0, absDifferenceDoubles.Length,
                i => absDifferenceDoubles[i] = (differenceDoubles[i] < 0) ? -1*differenceDoubles[i] : differenceDoubles[i]);

            //determine if relative difference is greater than minDoseEvaluated 
            // stores 1 as GT minDoseEvaluated is true
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

    public static class ProfileTools
    {
        /// <summary>
        /// Outputs the distance between two points. Uses the Pythagorean theorum to do it. 
        /// </summary>
        /// <param name="dose1">The dose1.</param>
        /// <param name="dose2">The dose2.</param>
        /// <returns></returns>
        public static double Distance(DoseValue dose1, DoseValue dose2)
        {
            double ret = Math.Pow((dose1.X - dose2.X), 2) + Math.Pow((dose1.Y - dose2.Y), 2) + Math.Pow((dose1.Z - dose2.Z), 2);
            return Math.Sqrt(ret);
        }

        /// <summary>
		/// Returns the 1 dimensional percent agreement as the number of voxels that pass x % and x mm
		/// defaults to 2%/2mm
		/// expand this
		/// </summary>
		/// <param name="profile">the profile being compared</param>
		/// <param name="reference">the reference being compared against</param>
		/// <param name="dta">match distance, mm </param>
		/// <param name="percent">tolerance % of max dose of ref </param>
		/// <returns></returns>
		public static double Comparison(List<DoseValue> reference, List<DoseValue> profile, int dta = 2, double percent = 2)
        {
            List<int> failed = new List<int>();
            double maxDose = 0;
            double ret = 0;
            double tolerance = 0; // the tolerance of dose matching in absolute units of the reference profile
            int pointsCompared = profile.Count;
            int pointsFailedDtAandPercent = 0;
            double threshold = 0;

            List<DoseValue> failedPercent = new List<DoseValue>(); // contains the doses that didn't pass percent agreement


            // finds the maximum dose
            foreach (var dose in reference) { maxDose = (dose.Dose > maxDose) ? dose.Dose : maxDose; }

            tolerance = maxDose * percent / 100;
            threshold = 5 * tolerance;

            for (int i = 0; i < profile.Count; i++)
            {
                if (profile[i].Dose < threshold) { continue; }
                double difference = System.Math.Abs(profile[i].Dose - reference[i].Dose);
                if (difference > tolerance) { failedPercent.Add(profile[i]); }
            }

            foreach (var item in failedPercent)
            {
                List<double> listOfDosesWithinDtaTolerance = new List<double>();
                foreach (var refItem in reference)
                {
                    // generates a list of doses that are within the dta
                    if (Distance(item, refItem) <= dta)
                    {
                        listOfDosesWithinDtaTolerance.Add(refItem.Dose);
                    }
                }
                listOfDosesWithinDtaTolerance.Sort();
                // checks if the dose is within the boundary doses. if yes the pixel's dose agrees with the reference within the dta tolerance
                // should be expanded to use linear interpolation. 
                if (listOfDosesWithinDtaTolerance[0] <= item.Dose && listOfDosesWithinDtaTolerance[listOfDosesWithinDtaTolerance.Count - 1] >= item.Dose)
                {
                    continue;
                }
                // if it fails the point failed
                pointsFailedDtAandPercent++;
            }

            ret = ((double)pointsFailedDtAandPercent) / ((double)pointsCompared) * 100;
            return ret;


        }

        /// <summary>
		/// Comparing the raw data, controls normalization
		/// </summary>
		/// <param name="reference">The reference.</param>
		/// <param name="profile">The profile.</param>
		/// <param name="dta">The dta.</param>
		/// <param name="percent">The percent.</param>
		/// <returns></returns>
		public static int ComparisonRaw(List<DoseValue> reference, List<DoseValue> profile, int dta = 2, double percent = 2)
        {
            double maxDose = 0;
            double tolerance = 0; // the tolerance of dose matching in absolute units of the reference profile
            int pointsCompared = profile.Count;
            int pointsFailedDtAandPercent = 0;
            double threshold = 0;

            List<DoseValue> failedPercent = new List<DoseValue>(); // contains the doses that didn't pass percent agreement


            // finds the maximum dose
            foreach (var dose in reference) { maxDose = (dose.Dose > maxDose) ? dose.Dose : maxDose; }

            tolerance = maxDose * percent / 100;
            threshold = 5 * tolerance;

            for (int i = 0; i < profile.Count; i++)
            {
                if (profile[i].Dose < threshold) { continue; }
                double difference = Math.Abs(profile[i].Dose - reference[i].Dose);
                if (difference > tolerance) { failedPercent.Add(profile[i]); }
            }

            foreach (var item in failedPercent)
            {
                List<double> listOfDosesWithinDtaTolerance = new List<double>();
                foreach (var refItem in reference)
                {
                    // generates a list of doses that are within the dta
                    if (Distance(item, refItem) <= dta)
                    {
                        listOfDosesWithinDtaTolerance.Add(refItem.Dose);
                    }
                }
                listOfDosesWithinDtaTolerance.Sort();
                // checks if the dose is within the boundary doses. if yes the pixel's dose agrees with the reference within the dta tolerance
                // should be expanded to use linear interpolation. 
                if (listOfDosesWithinDtaTolerance[0] <= item.Dose && listOfDosesWithinDtaTolerance[listOfDosesWithinDtaTolerance.Count - 1] >= item.Dose)
                {
                    continue;
                }
                // if it fails the point failed
                pointsFailedDtAandPercent++;
            }

            return pointsFailedDtAandPercent;


        }

    }

}
