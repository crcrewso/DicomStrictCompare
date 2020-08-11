using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DicomStrictCompare;
using DicomStrictCompare.Model;
using DSCcore.Properties;
using DSCcore.View;

namespace DSC
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// Bool to keep track of if the settings have been changed since last check 
        /// </summary>
        public bool Tested { get; private set; } = false;
        /// <summary>
        /// Directory containing 'source' or reference dicom dose files and plan files 
        /// </summary>
        public string SourceDirectory { get; private set; }
        /// <summary>
        /// Directory containing Dicom files being evaluated
        /// </summary>
        public string TargetDirectory { get; private set; }
        /// <summary>
        /// Destination directory for all results
        /// </summary>
        public string SaveDirectory { get; private set; }
        /// <summary>
        /// Shortname of files (without extension) to be saved. Program will pick extension as applicable
        /// </summary>
        public string SaveNamePrefix { get; private set; }
        /// <summary>
        /// Name user would like to call the source or reference dataset in results data and plots 
        /// </summary>
        public string SourceAliasName { get; private set; } = "Reference";
        /// <summary>
        /// Name user would like to call the target dataset in the results data and plots
        /// </summary>
        public string TargetAliasName { get; private set; } = "New Model";
        /// <summary>
        /// List of DTA parameters of interest
        /// </summary>
        public List<DicomStrictCompare.Model.Dta> Dtas { get; private set; }

        private readonly DscDataHandler _dataHandler;

        private readonly BackgroundWorker worker;
        private bool _isRunning;
        /// <inheritdoc />
        public Form1()
        {
            InitializeComponent();
            Dtas = new List<DicomStrictCompare.Model.Dta>();
            tbxSourceLabel.Text = SourceAliasName.ToString();
            tbxTargetLabel.Text = TargetAliasName.ToString();
            _dataHandler = new DscDataHandler();
            Tested = false;
            worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += WorkerRunWorkerCompleted;
            _isRunning = false;

        }


        private void Form1_Load(object sender, EventArgs e)
        {
            Tested = false;
        }




        #region textBoxesChanged

        /// <summary>
        /// Allows user to enter a tolerance value and converts it to a class parameter 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbxTightTol_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
        }

        private void TbxMainTol_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
        }

        private void ThreshBox_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
        }

        private void TbxSource_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
        }

        private void TbxTarget_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
        }


        private void TbxSaveDir_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
        }

        private void TbxSourceLabel_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
            string temp = tbxSourceLabel.Text;
            SourceAliasName = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxSourceLabel.Text = SourceAliasName;
        }

        private void TbxTargetLabel_TextChanged(object sender, EventArgs e)
        {
            string temp = tbxTargetLabel.Text;
            TargetAliasName = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxTargetLabel.Text = TargetAliasName;
            Tested = false;
        }

        private void TbxSaveName_TextChanged(object sender, EventArgs e)
        {
            Tested = false;
            string temp = tbxSaveName.Text;
            SaveNamePrefix = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxSaveName.Text = SaveNamePrefix;
        }


        #endregion


        #region ButtonClicks

        private void BtnSourceDir_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = @"Select Source Dose Folder Location";
            DialogResult result = fbd.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                SourceDirectory = fbd.SelectedPath;
                tbxSource.Text = SourceDirectory;
                lblSourceFilesFound.Text = _dataHandler.CreateSourceList(SourceDirectory).ToString();
            }

        }

        private void BtnTargetDir_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = @"Select Source Dose Folder Location";
            DialogResult result = fbd.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                TargetDirectory = fbd.SelectedPath;
                tbxTarget.Text = TargetDirectory;
                lblTargetFilesFound.Text = _dataHandler.CreateTargetList(TargetDirectory).ToString();
            }
        }

        private void BtnSaveDir_Click(object sender, EventArgs e)
        {
            using FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = @"Select Source Dose Folder Location";
            DialogResult result = fbd.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                SaveDirectory = fbd.SelectedPath;
                tbxSaveDir.Text = SaveDirectory;
            }
        }

        //TODO add error checking here!
        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnExecute_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem dtaItem in dtaListView.SelectedItems)
            {
                Dtas.Add(viewSupport.listViewToDta(dtaItem));
            }

            //TODO: impliment proper threadding request
            DicomStrictCompare.Controller.DSCUserSettings settings = new DicomStrictCompare.Controller.DSCUserSettings(Dtas.ToArray(), chkDoseCompare.Checked, chkPDDCompare.Checked, false, Environment.ProcessorCount);
            //TODO: make Fuzzy res width gui configurable  
            //TODO: Impliment gamma


            if (!_isRunning)
            {
                _dataHandler.Settings = settings;
                _dataHandler.SourceAliasName = SourceAliasName;
                _dataHandler.TargetAliasName = TargetAliasName;



                if (Tested == false)
                {
                    TestDirectories_Click(sender, e);
                }
                if (Tested == false)
                {
                    lblRunStatus.Text = Resources.paramTestFailed;
                    return;
                }
                if (!chkPDDCompare.Checked && !chkDoseCompare.Checked)
                {
                    lblRunStatus.Text = Resources.zeroTasks;
                    return;
                }



                worker.RunWorkerAsync();
                return;
            }
            else
                worker.CancelAsync();
        }

        private void TestDirectories_Click(object sender, EventArgs e)
        {
            try
            {
                SaveDirectory = tbxSaveDir.Text;
                //                if (!Directory.Exists(SaveDirectory))
                //                    SaveDirectory = null;
                tbxSaveDir.Text = SaveDirectory;

                TargetDirectory = tbxTarget.Text;
                if (!Directory.Exists(TargetDirectory))
                    TargetDirectory = null;
                tbxTarget.Text = TargetDirectory;
                lblTargetFilesFound.Text = _dataHandler.CreateTargetList(TargetDirectory).ToString();

                SourceDirectory = tbxSource.Text;
                if (!Directory.Exists(SourceDirectory))
                    SourceDirectory = null;
                tbxSource.Text = SourceDirectory;
                lblSourceFilesFound.Text = _dataHandler.CreateSourceList(SourceDirectory).ToString();
            }
            catch (ArgumentNullException)
            {
                _ = System.Windows.Forms.MessageBox.Show(Resources.paramTestFailed);
                return;
            }
            foreach (ListViewItem dtaItem in dtaListView.SelectedItems)
            {
                Dtas.Add(viewSupport.listViewToDta(dtaItem));
            }
            if (Dtas.Count != dtaListView.SelectedItems.Count)
                throw new Exception();
            Tested = true;
        }



        private void BtnDAadd_Click(object sender, EventArgs e)
        {
            double distance = 0;
            if (txtBoxDAdta.TextLength > 0)
            {
                distance = Math.Abs(Convert.ToDouble(txtBoxDAdta.Text));
            }

            bool isMM;

            if (units.SelectedItem == null)
            {
                isMM = false;
            }
            else
            {
                isMM = units.SelectedItem.ToString() == "mm" ? true : false;
            }

            if (String.IsNullOrEmpty(txtBxTrim.Text))
            {
                txtBxTrim.Text = DSCcore.Properties.Resources.minDTAtrim;
            }

            dtaListView.Items.Add(new ListViewItem(new string[] {
                txtBoxDAtol.Text
                , txtBoxDAdta.Text + ' '+ ((units.SelectedItem == null) ? "voxels": "mm")
                , txtBoxDAthres.Text
                , txtBxTrim.Text
                , chkBoxDArel.Checked ? "y": "n"
                , chkBoxGamma.Checked ? "y": "n"
                }));


            txtBoxDAdta.Clear();
            txtBoxDAthres.Clear();
            txtBoxDAtol.Clear();
            txtBxTrim.Clear();
            units.ClearSelected();
            chkBoxDArel.Checked = false;
            chkBoxGamma.Checked = false;

        }

        private void lblIntro_Click(object sender, EventArgs e)
        {
            string boxMessage = "DTA Definitions"
                + "\nTolerance\t- Percent Deviation of Target dose at each point to pass agreement"
                + "\nDTA\t- Distance to agreement in units of mm or voxels, can be 0"
                + "\nThreshhold - Percent of Max source below which comparisons will not be made"
                + "\nTrim - Allows the user to remove a number of skin voxels from the phantom"
                + "\n\t\tUseful if there is a known discrepancy in skin dose between two algorithims"
                + "\n\t\tthat the user would like to remove from analysis"
                + "\nGamma\t- if Checked a gamma calculation will be used for comparison,"
                + "\n\t\tIf not software defaults to dta ";

            System.Windows.Forms.MessageBox.Show(boxMessage);

        }

        #endregion


        #region Worker 








        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _isRunning = true;
            _dataHandler.Run(chkDoseCompare.Checked, chkPDDCompare.Checked, SaveDirectory, sender);

            if (chkDoseCompare.Checked == true)
            {
                SaveFile saveFile = new SaveFile(SaveNamePrefix, SaveDirectory);
                saveFile.Save(_dataHandler.ResultMessage);
            }
        }

        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            doseProgressBar.Value = e.ProgressPercentage;
            lblRunStatus.Text = e.UserState.ToString();
        }

        void WorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _isRunning = false;
            lblRunStatus.Text = Resources.finished;
            doseProgressBar.Value = doseProgressBar.Maximum;

            if (e.Error != null)
            {
                _ = System.Windows.Forms.MessageBox.Show(e.Error.ToString());
            }

            BackgroundWorker worker = sender as BackgroundWorker;
            worker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(WorkerRunWorkerCompleted);
            worker.DoWork -= new DoWorkEventHandler(Worker_DoWork);
            worker.Dispose();

        }
        #endregion




    }
}
