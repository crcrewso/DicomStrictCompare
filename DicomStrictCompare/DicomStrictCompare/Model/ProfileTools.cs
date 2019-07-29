using System.Collections.Generic;
using EvilDICOM.RT;
using System;

namespace DicomStrictCompare
{
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
            foreach (DoseValue dose in reference) { maxDose = (dose.Dose > maxDose) ? dose.Dose : maxDose; }

            tolerance = maxDose * percent / 100;
            threshold = 5 * tolerance;

            for (int i = 0; i < profile.Count; i++)
            {
                if (profile[i].Dose < threshold) { continue; }
                double difference = System.Math.Abs(profile[i].Dose - reference[i].Dose);
                if (difference > tolerance) { failedPercent.Add(profile[i]); }
            }

            foreach (DoseValue item in failedPercent)
            {
                List<double> listOfDosesWithinDtaTolerance = new List<double>();
                foreach (DoseValue refItem in reference)
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
            foreach (DoseValue dose in reference) { maxDose = (dose.Dose > maxDose) ? dose.Dose : maxDose; }

            tolerance = maxDose * percent / 100;
            threshold = 5 * tolerance;

            for (int i = 0; i < profile.Count; i++)
            {
                if (profile[i].Dose < threshold) { continue; }
                double difference = Math.Abs(profile[i].Dose - reference[i].Dose);
                if (difference > tolerance) { failedPercent.Add(profile[i]); }
            }

            foreach (DoseValue item in failedPercent)
            {
                List<double> listOfDosesWithinDtaTolerance = new List<double>();
                foreach (DoseValue refItem in reference)
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

        /// <summary>
        /// Calculates the depth from surface that a PDD will drop below the percent provided. percent=50 would yield the depth at which the profile falls to below 0.5*Peak 
        /// </summary>
        /// <param name="doseValues">PDD Profile data</param>
        /// <param name="percent">Integer representing percent of depth of interest </param>
        /// <returns>The index of the furthest voxel from the surface that has a dose value above the percent of interest</returns>
        ///<exception cref="ArgumentOutOfRangeException">Thrown when the profile does not drop below the sought percent</exception>
        public static int DepthToPercentOfPeak(List<DoseValue> doseValues, int percent)
        {
            // finding value and location of maximum
            double max = 0;
            int indexMax = 0;
            for(int i = 0; i < doseValues.Count; i++)
            {
                if (doseValues[i].Dose > max)
                {
                    max = doseValues[i].Dose;
                    indexMax = i;
                }
            }
            // I have the location and value of max
            double threshold = max * (double)percent / 100.0;
            for(int i = indexMax; i < doseValues.Count; i++)
            {
                if (doseValues[i].Dose < threshold)
                {
                    return i - 1;
                }
            }
            throw new ArgumentOutOfRangeException("The profile does not drop below " + percent + " of the peak");
        }


    }

}
