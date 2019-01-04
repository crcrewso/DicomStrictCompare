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

namespace DSC
{
    public partial class Form1 : Form
    {
        public float TightTol { get; private set; } = 1;
        public float MainTol { get; private set; } = 2;
        public string SourceDirectory { get; private set; }
        public string TargetDirectory { get; private set; }



        public Form1()
        {
            InitializeComponent();
            tbxTightTol.Text = TightTol.ToString();
            tbxMainTol.Text = MainTol.ToString();
        }

        /// <summary>
        /// Allows user to enter a tolerance value and converts it to a class parameter 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbxTightTol_TextChanged(object sender, EventArgs e)
        {
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
                MainTol = Math.Abs(TightTol);
                if (TightTol > MainTol)
                {
                    TightTol = MainTol;
                    tbxTightTol.Text = MainTol.ToString();
                }
                tbxMainTol.Text = TightTol.ToString();
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
            }
        }
    }
}
