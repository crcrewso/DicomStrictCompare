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

namespace DSC
{
    public partial class Form1 : Form
    {
        public bool tested;
        public float TightTol { get; private set; } = 1;
        public float MainTol { get; private set; } = 2;
        public float Threshold { get; private set; } = 10;
        public string SourceDirectory { get; private set; }
        public string TargetDirectory { get; private set; }
        public string SaveDirectory { get; private set; }
        public string SaveNamePrefix { get; private set; }
        public string SourceAliasName { get; private set; } = "Reference";
        public string TargetAliasName { get; private set; } = "New Model";

        private DscDataHandler _dataHandler;

        const int delayTime = 1000;

        /// <inheritdoc />
        public Form1()
        {
            InitializeComponent();
            tbxTightTol.Text = TightTol.ToString();
            tbxMainTol.Text = MainTol.ToString();
            tbxThreshholdTol.Text = Threshold.ToString();
            tbxSourceLabel.Text = SourceAliasName.ToString();
            tbxTargetLabel.Text = TargetAliasName.ToString();
            _dataHandler = new DscDataHandler();
            tested = false;

        }


        private void Form1_Load(object sender, EventArgs e)
        {
            tested = false;
        }



        #region textBoxesChanged

        /// <summary>
        /// Allows user to enter a tolerance value and converts it to a class parameter 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxTightTol_TextChanged(object sender, EventArgs e)
        {
            tested = false;
        }

        private void tbxMainTol_TextChanged(object sender, EventArgs e)
        {
            tested = false;
        }

        private void threshBox_TextChanged(object sender, EventArgs e)
        {
            tested = false;
        }

        private void tbxSource_TextChanged(object sender, EventArgs e)
        {
            tested = false;
        }

        private void tbxTarget_TextChanged(object sender, EventArgs e)
        {
            tested = false;
        }


        private void TbxSaveDir_TextChanged(object sender, EventArgs e)
        {
            tested = false;
        }

        private void TbxSourceLabel_TextChanged(object sender, EventArgs e)
        {
            tested = false;
            var temp = tbxSourceLabel.Text;
            SourceAliasName = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxSourceLabel.Text = SourceAliasName;
        }

        private void TbxTargetLabel_TextChanged(object sender, EventArgs e)
        {
            var temp = tbxTargetLabel.Text;
            TargetAliasName = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxTargetLabel.Text = TargetAliasName;
            tested = false;
        }

        private void TbxSaveName_TextChanged(object sender, EventArgs e)
        {
            tested = false;
            var temp = tbxSaveName.Text;
            SaveNamePrefix = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxSaveName.Text = SaveNamePrefix;
        }
        #endregion


        #region ButtonClicks

        private void btnSourceDir_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = @"Select Source Dose Folder Location";
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    SourceDirectory = fbd.SelectedPath;
                    tbxSource.Text = SourceDirectory;
                    lblSourceFilesFound.Text = _dataHandler.CreateSourceList(SourceDirectory).ToString();
                }

            }

        }

        private void btnTargetDir_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = @"Select Source Dose Folder Location";
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    TargetDirectory = fbd.SelectedPath;
                    tbxTarget.Text = TargetDirectory;
                    lblTargetFilesFound.Text = _dataHandler.CreateTargetList(TargetDirectory).ToString();
                }
            }
        }

        private void BtnSaveDir_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = @"Select Source Dose Folder Location";
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    SaveDirectory = fbd.SelectedPath;
                    tbxSaveDir.Text = SaveDirectory;
                }

            }
        }


        #endregion










        /// <summary>
        /// TODO add error checking here!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExecute_Click(object sender, EventArgs e)
        {


            _dataHandler.ThresholdTol = Threshold / 100.0;
            _dataHandler.MainTol = MainTol / 100.0;
            _dataHandler.TightTol = TightTol / 100.0;
            _dataHandler.SourceAliasName = SourceAliasName;
            _dataHandler.TargetAliasName = TargetAliasName;
            if (chkBoxUseGPU.Checked == true)
            {
                _dataHandler.UseGPU = true;
            }

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += workerRunWorkerCompleted;
            if (tested == false)
            {
                TestDirectories_Click(sender, e);
            }
            if (tested == false)
            {
                lblRunStatus.Text = "Tested text Fields, Please Rerun";
                return;
            }
            if (!chkPDDCompare.Checked && !chkDoseCompare.Checked)
            {
                lblRunStatus.Text = "Nothing to do";
                return;
            }



            worker.RunWorkerAsync();
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _dataHandler.Run(chkDoseCompare.Checked, chkPDDCompare.Checked, SaveDirectory, sender);

            if (chkDoseCompare.Checked == true)
            {
                SaveFile saveFile = new SaveFile(SaveNamePrefix, SaveDirectory);
                saveFile.Save(_dataHandler.ResultMessage);
            }
        }

        void worker_ProgressChanged (object sender, ProgressChangedEventArgs e)
        {
            doseProgressBar.Value = e.ProgressPercentage;
            lblRunStatus.Text = e.UserState.ToString();
        }

        void workerRunWorkerCompleted (object sender, RunWorkerCompletedEventArgs e)
        {
            lblRunStatus.Text = "Finished";
            doseProgressBar.Value = doseProgressBar.Maximum;

            if(e.Error != null)
            {
                System.Windows.Forms.MessageBox.Show(e.Error.ToString());
            }

        }










        private void TestDirectories_Click(object sender, EventArgs e)
        {
            try
            {
                SaveDirectory = tbxSaveDir.Text;
                if (!Directory.Exists(SaveDirectory))
                    SaveDirectory = null;
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
            catch (ArgumentNullException )
            {
                System.Windows.Forms.MessageBox.Show("One of the directory fields is either empty or invalid");
                return;
            }

            try
            {
                TightTol = float.Parse(tbxTightTol.Text);
                TightTol = Math.Abs(TightTol);
                tbxTightTol.Text = TightTol.ToString();

                MainTol = float.Parse(tbxMainTol.Text);
                MainTol = Math.Abs(MainTol);
                tbxMainTol.Text = MainTol.ToString();

                if (TightTol > MainTol)
                {
                    System.Windows.Forms.MessageBox.Show("Your tight tolerance is greater than your main tolerance. This does not make sense");
                    return;
                }

                Threshold = float.Parse(tbxThreshholdTol.Text);
                Threshold = Math.Abs(Threshold);
                tbxThreshholdTol.Text = Threshold.ToString();
            }
            catch (FormatException)
            {
                System.Windows.Forms.MessageBox.Show("Please enter a floating point number above zero");
                return;
            }
            catch (ArgumentNullException)
            {
                System.Windows.Forms.MessageBox.Show("One of the tolerance fields is either empty or invalid");
                return;
            }
            tested = true;
        }
    }
}
