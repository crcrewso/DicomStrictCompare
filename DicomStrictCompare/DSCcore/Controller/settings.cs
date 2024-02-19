using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

namespace DCSCore.Controller
{
    /// <summary>
    /// Consolidated settings collection to undo feature creep spaghettification.  
    /// </summary>
    public class Settings(
        Model.Dta[] dtas
            , bool runDoseComparisons
            , bool runPDDComparisons
            , bool runProfileComparisons
            , int coresIn = 4
            )
    {

        /// <summary>
        /// Simple dta algorithm for fractional DTA adjustment
        /// </summary>


        public Model.Dta[] Dtas { get; } = dtas;
        public bool RunDoseComparisons { get; } = runDoseComparisons;
        public bool RunPDDComparisons { get; } = runPDDComparisons;
        public bool RunProfileComparisons { get; } = runProfileComparisons;
        public int CpuParallel { get; private set; } = Math.Min(coresIn, Environment.ProcessorCount);

        public void Save()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = config.AppSettings.Settings;

            //settings["RunDoseComparisons"].Value = RunDoseComparisons.ToString();
            //settings["RunPDDComparisons"].Value = RunPDDComparisons.ToString();
            //settings["RunProfileComparisons"].Value = RunProfileComparisons.ToString();
            settings["CpuParallel"].Value = CpuParallel.ToString() ;

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);

        }



    }



}
