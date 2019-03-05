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
        public float TightTol { get; private set; } = 1;
        public float MainTol { get; private set; } = 2;
        public float Threshold { get; private set; } = 10;
        public string SourceDirectory { get; private set; }
        public string TargetDirectory { get; private set; }


        private DscDataHandler _dataHandler;


        /// <inheritdoc />
        public Form1()
        {
            InitializeComponent();
            tbxTightTol.Text = TightTol.ToString();
            tbxMainTol.Text = MainTol.ToString();
            _dataHandler = new DscDataHandler();
            
        }

        /// <summary>
        /// Allows user to enter a tolerance value and converts it to a class parameter 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxTightTol_TextChanged(object sender, EventArgs e)
        {
            System.Threading.Thread.Sleep(250);
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

        private void tbxMainTol_TextChanged(object sender, EventArgs e)
        {
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

        private void tbxSource_TextChanged(object sender, EventArgs e)
        {
            tbxSource.Text = SourceDirectory;
        }

        private void tbxTarget_TextChanged(object sender, EventArgs e)
        {
            tbxTarget.Text = TargetDirectory;
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
                }
                lblSourceFilesFound.Text = _dataHandler.CreateSourceList(SourceDirectory).ToString();
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
                    tbxTarget.Text = SourceDirectory;
                }
                lblTargetFilesFound.Text = _dataHandler.CreateTargetList(TargetDirectory).ToString();
            }
        }

        /// <summary>
        /// TODO add error checking here!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExecute_Click(object sender, EventArgs e)
        {
            _dataHandler.EpsilonTol = Threshold / 100.0;
            _dataHandler.MainTol = MainTol/100.0;
            _dataHandler.TightTol = TightTol/100.0;
            _dataHandler.Run();
            System.Windows.Forms.MessageBox.Show("Ive finished\n" + MatchedDosePair.ResultHeader + "\n" + _dataHandler.ResultMessage);
        }

        private void threshBox_TextChanged(object sender, EventArgs e)
        {
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
    }
}
