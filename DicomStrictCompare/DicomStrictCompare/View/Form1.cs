﻿using System;
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
            tbxSourceLabel.Text = SourceAliasName;
            tbxTargetLabel.Text = TargetAliasName;
            _dataHandler = new DscDataHandler();

        }

        /// <summary>
        /// Allows user to enter a tolerance value and converts it to a class parameter 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void tbxTightTol_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(delayTime);
            try
            {
                TightTol = float.Parse(tbxTightTol.Text);
                TightTol = Math.Abs(TightTol);
                if (TightTol > MainTol)
                {
                    MainTol = TightTol;
                    tbxMainTol.Text = MainTol.ToString();
                }
                tbxTightTol.Text = TightTol.ToString();
            }
            catch (ArgumentNullException)
            {
                return;
            }
            catch (FormatException)
            {
                System.Windows.Forms.MessageBox.Show("Please enter a floating point number above zero");
                return;
            }
            catch (OverflowException)
            {
                return;
            }
        }

        private async void tbxMainTol_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(delayTime);
            try
            {
                MainTol = float.Parse(tbxMainTol.Text);
                MainTol = Math.Abs(MainTol);
                if (TightTol > MainTol)
                {
                    MainTol = TightTol;
                    tbxMainTol.Text = MainTol.ToString();
                }
                tbxMainTol.Text = MainTol.ToString();
            }
            catch (ArgumentNullException)
            {
                return;
            }
            catch (FormatException)
            {
                System.Windows.Forms.MessageBox.Show("Please enter a floating point number above zero");
                return;
            }
            catch (OverflowException)
            {
                return;
            }
        }

        private async void tbxSource_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(10 * delayTime);
            SourceDirectory = tbxSource.Text;
            if (!Directory.Exists(SourceDirectory))
                SourceDirectory = null;
            tbxSource.Text = SourceDirectory;
            lblSourceFilesFound.Text = _dataHandler.CreateSourceList(SourceDirectory).ToString();
        }

        private async void tbxTarget_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(10 * delayTime);
            TargetDirectory = tbxTarget.Text;
            if (!Directory.Exists(TargetDirectory))
                TargetDirectory = null;
            tbxTarget.Text = TargetDirectory;
            lblTargetFilesFound.Text = _dataHandler.CreateTargetList(TargetDirectory).ToString();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

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

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += workerRunWorkerCompleted;
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
        }

        private async void threshBox_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(delayTime);
            try
            {
                Threshold = float.Parse(tbxThreshholdTol.Text);
                Threshold = Math.Abs(Threshold);
                tbxThreshholdTol.Text = Threshold.ToString();
            }
            catch (ArgumentNullException)
            {
                return;
            }
            catch (FormatException)
            {
                System.Windows.Forms.MessageBox.Show("Please enter a floating point number above zero");
                return;
            }
            catch (OverflowException)
            {
                return;
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

        private async void TbxSaveName_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(300);
            var temp = tbxSaveName.Text;
            SaveNamePrefix = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxSaveName.Text = SaveNamePrefix;

        }

        private async void TbxSaveDir_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(2 * delayTime);
            SaveDirectory = tbxSaveDir.Text;
            if (!Directory.Exists(SaveDirectory))
                SaveDirectory = null;
            tbxSaveDir.Text = SaveDirectory;


        }

        private async void TbxSourceLabel_TextChanged(object sender, EventArgs e)
        {
            await Task.Delay(300);
            var temp = tbxSaveName.Text;
            SaveNamePrefix = Path.GetInvalidFileNameChars().Aggregate(temp, (current, c) => current.Replace(c.ToString(), string.Empty));
            tbxSaveName.Text = SaveNamePrefix;
        }
    }
}