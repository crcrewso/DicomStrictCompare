namespace DSC
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.lblIntro = new System.Windows.Forms.Label();
            this.tbxSource = new System.Windows.Forms.TextBox();
            this.tbxTarget = new System.Windows.Forms.TextBox();
            this.lblTightTol = new System.Windows.Forms.Label();
            this.lblMainTol = new System.Windows.Forms.Label();
            this.tbxTightTol = new System.Windows.Forms.TextBox();
            this.tbxMainTol = new System.Windows.Forms.TextBox();
            this.btnExecute = new System.Windows.Forms.Button();
            this.btnSourceDir = new System.Windows.Forms.Button();
            this.btnTargetDir = new System.Windows.Forms.Button();
            this.lblSourceFilesFoundText = new System.Windows.Forms.Label();
            this.lblTargetFilesFoundText = new System.Windows.Forms.Label();
            this.lblSourceFilesFound = new System.Windows.Forms.Label();
            this.lblTargetFilesFound = new System.Windows.Forms.Label();
            this.tbxThreshholdTol = new System.Windows.Forms.TextBox();
            this.lblthreshhold = new System.Windows.Forms.Label();
            this.btnSaveDir = new System.Windows.Forms.Button();
            this.tbxSaveDir = new System.Windows.Forms.TextBox();
            this.tbxSaveName = new System.Windows.Forms.TextBox();
            this.lblSaveName = new System.Windows.Forms.Label();
            this.chkPDDCompare = new System.Windows.Forms.CheckBox();
            this.chkDoseCompare = new System.Windows.Forms.CheckBox();
            this.doseProgressBar = new System.Windows.Forms.ProgressBar();
            this.testDirectories = new System.Windows.Forms.Button();
            this.tbxTargetLabel = new System.Windows.Forms.TextBox();
            this.tbxSourceLabel = new System.Windows.Forms.TextBox();
            this.lblTargetRefName = new System.Windows.Forms.Label();
            this.lblSourceRefName = new System.Windows.Forms.Label();
            this.lblRunStatus = new System.Windows.Forms.Label();
            this.chkBoxUseGPU = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // lblIntro
            // 
            this.lblIntro.AutoSize = true;
            this.lblIntro.Location = new System.Drawing.Point(12, 12);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(389, 195);
            this.lblIntro.TabIndex = 0;
            this.lblIntro.Text = resources.GetString("lblIntro.Text");
            // 
            // tbxSource
            // 
            this.tbxSource.Location = new System.Drawing.Point(93, 289);
            this.tbxSource.MinimumSize = new System.Drawing.Size(779, 20);
            this.tbxSource.Name = "tbxSource";
            this.tbxSource.Size = new System.Drawing.Size(779, 20);
            this.tbxSource.TabIndex = 4;
            this.tbxSource.TextChanged += new System.EventHandler(this.TbxSource_TextChanged);
            // 
            // tbxTarget
            // 
            this.tbxTarget.Location = new System.Drawing.Point(93, 342);
            this.tbxTarget.Name = "tbxTarget";
            this.tbxTarget.Size = new System.Drawing.Size(779, 20);
            this.tbxTarget.TabIndex = 6;
            this.tbxTarget.TextChanged += new System.EventHandler(this.TbxTarget_TextChanged);
            // 
            // lblTightTol
            // 
            this.lblTightTol.AutoSize = true;
            this.lblTightTol.Location = new System.Drawing.Point(449, 17);
            this.lblTightTol.Name = "lblTightTol";
            this.lblTightTol.Size = new System.Drawing.Size(99, 13);
            this.lblTightTol.TabIndex = 5;
            this.lblTightTol.Text = "Tight Tolerance (%)";
            // 
            // lblMainTol
            // 
            this.lblMainTol.AutoSize = true;
            this.lblMainTol.Location = new System.Drawing.Point(450, 46);
            this.lblMainTol.Name = "lblMainTol";
            this.lblMainTol.Size = new System.Drawing.Size(98, 13);
            this.lblMainTol.TabIndex = 6;
            this.lblMainTol.Text = "Main Tolerance (%)";
            // 
            // tbxTightTol
            // 
            this.tbxTightTol.Location = new System.Drawing.Point(554, 14);
            this.tbxTightTol.Name = "tbxTightTol";
            this.tbxTightTol.Size = new System.Drawing.Size(53, 20);
            this.tbxTightTol.TabIndex = 0;
            this.tbxTightTol.TextChanged += new System.EventHandler(this.TbxTightTol_TextChanged);
            // 
            // tbxMainTol
            // 
            this.tbxMainTol.Location = new System.Drawing.Point(554, 43);
            this.tbxMainTol.Name = "tbxMainTol";
            this.tbxMainTol.Size = new System.Drawing.Size(53, 20);
            this.tbxMainTol.TabIndex = 1;
            this.tbxMainTol.TextChanged += new System.EventHandler(this.TbxMainTol_TextChanged);
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(797, 41);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(75, 23);
            this.btnExecute.TabIndex = 10;
            this.btnExecute.Text = "Run";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.BtnExecute_Click);
            // 
            // btnSourceDir
            // 
            this.btnSourceDir.Location = new System.Drawing.Point(12, 287);
            this.btnSourceDir.Name = "btnSourceDir";
            this.btnSourceDir.Size = new System.Drawing.Size(75, 23);
            this.btnSourceDir.TabIndex = 10;
            this.btnSourceDir.Text = "Source";
            this.btnSourceDir.UseVisualStyleBackColor = true;
            this.btnSourceDir.Click += new System.EventHandler(this.BtnSourceDir_Click);
            // 
            // btnTargetDir
            // 
            this.btnTargetDir.Location = new System.Drawing.Point(12, 340);
            this.btnTargetDir.Name = "btnTargetDir";
            this.btnTargetDir.Size = new System.Drawing.Size(75, 23);
            this.btnTargetDir.TabIndex = 11;
            this.btnTargetDir.Text = "Target";
            this.btnTargetDir.UseVisualStyleBackColor = true;
            this.btnTargetDir.Click += new System.EventHandler(this.BtnTargetDir_Click);
            // 
            // lblSourceFilesFoundText
            // 
            this.lblSourceFilesFoundText.AutoSize = true;
            this.lblSourceFilesFoundText.Location = new System.Drawing.Point(634, 17);
            this.lblSourceFilesFoundText.Name = "lblSourceFilesFoundText";
            this.lblSourceFilesFoundText.Size = new System.Drawing.Size(126, 13);
            this.lblSourceFilesFoundText.TabIndex = 12;
            this.lblSourceFilesFoundText.Text = "Source Dose Files Found";
            // 
            // lblTargetFilesFoundText
            // 
            this.lblTargetFilesFoundText.AutoSize = true;
            this.lblTargetFilesFoundText.Location = new System.Drawing.Point(634, 46);
            this.lblTargetFilesFoundText.Name = "lblTargetFilesFoundText";
            this.lblTargetFilesFoundText.Size = new System.Drawing.Size(123, 13);
            this.lblTargetFilesFoundText.TabIndex = 13;
            this.lblTargetFilesFoundText.Text = "Target Dose Files Found";
            // 
            // lblSourceFilesFound
            // 
            this.lblSourceFilesFound.AutoSize = true;
            this.lblSourceFilesFound.Location = new System.Drawing.Point(766, 17);
            this.lblSourceFilesFound.Name = "lblSourceFilesFound";
            this.lblSourceFilesFound.Size = new System.Drawing.Size(14, 13);
            this.lblSourceFilesFound.TabIndex = 14;
            this.lblSourceFilesFound.Text = "#";
            // 
            // lblTargetFilesFound
            // 
            this.lblTargetFilesFound.AutoSize = true;
            this.lblTargetFilesFound.Location = new System.Drawing.Point(766, 46);
            this.lblTargetFilesFound.Name = "lblTargetFilesFound";
            this.lblTargetFilesFound.Size = new System.Drawing.Size(14, 13);
            this.lblTargetFilesFound.TabIndex = 15;
            this.lblTargetFilesFound.Text = "#";
            // 
            // tbxThreshholdTol
            // 
            this.tbxThreshholdTol.Location = new System.Drawing.Point(554, 72);
            this.tbxThreshholdTol.Name = "tbxThreshholdTol";
            this.tbxThreshholdTol.Size = new System.Drawing.Size(53, 20);
            this.tbxThreshholdTol.TabIndex = 2;
            this.tbxThreshholdTol.TextChanged += new System.EventHandler(this.ThreshBox_TextChanged);
            // 
            // lblthreshhold
            // 
            this.lblthreshhold.AutoSize = true;
            this.lblthreshhold.Location = new System.Drawing.Point(471, 75);
            this.lblthreshhold.Name = "lblthreshhold";
            this.lblthreshhold.Size = new System.Drawing.Size(77, 13);
            this.lblthreshhold.TabIndex = 16;
            this.lblthreshhold.Text = "Threshhold (%)";
            // 
            // btnSaveDir
            // 
            this.btnSaveDir.Location = new System.Drawing.Point(10, 392);
            this.btnSaveDir.Name = "btnSaveDir";
            this.btnSaveDir.Size = new System.Drawing.Size(75, 23);
            this.btnSaveDir.TabIndex = 18;
            this.btnSaveDir.Text = "Save Location";
            this.btnSaveDir.UseVisualStyleBackColor = true;
            this.btnSaveDir.Click += new System.EventHandler(this.BtnSaveDir_Click);
            // 
            // tbxSaveDir
            // 
            this.tbxSaveDir.Location = new System.Drawing.Point(93, 394);
            this.tbxSaveDir.Name = "tbxSaveDir";
            this.tbxSaveDir.Size = new System.Drawing.Size(779, 20);
            this.tbxSaveDir.TabIndex = 8;
            this.tbxSaveDir.TextChanged += new System.EventHandler(this.TbxSaveDir_TextChanged);
            // 
            // tbxSaveName
            // 
            this.tbxSaveName.Location = new System.Drawing.Point(93, 368);
            this.tbxSaveName.Name = "tbxSaveName";
            this.tbxSaveName.Size = new System.Drawing.Size(455, 20);
            this.tbxSaveName.TabIndex = 7;
            this.tbxSaveName.TextChanged += new System.EventHandler(this.TbxSaveName_TextChanged);
            // 
            // lblSaveName
            // 
            this.lblSaveName.AutoSize = true;
            this.lblSaveName.Location = new System.Drawing.Point(22, 371);
            this.lblSaveName.Name = "lblSaveName";
            this.lblSaveName.Size = new System.Drawing.Size(63, 13);
            this.lblSaveName.TabIndex = 21;
            this.lblSaveName.Text = "Save Name";
            // 
            // chkPDDCompare
            // 
            this.chkPDDCompare.AutoSize = true;
            this.chkPDDCompare.Location = new System.Drawing.Point(697, 72);
            this.chkPDDCompare.Name = "chkPDDCompare";
            this.chkPDDCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkPDDCompare.Size = new System.Drawing.Size(175, 17);
            this.chkPDDCompare.TabIndex = 23;
            this.chkPDDCompare.Text = "Produce PDD comparison Plots";
            this.chkPDDCompare.UseVisualStyleBackColor = true;
            // 
            // chkDoseCompare
            // 
            this.chkDoseCompare.AutoSize = true;
            this.chkDoseCompare.Location = new System.Drawing.Point(694, 95);
            this.chkDoseCompare.Name = "chkDoseCompare";
            this.chkDoseCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkDoseCompare.Size = new System.Drawing.Size(178, 17);
            this.chkDoseCompare.TabIndex = 24;
            this.chkDoseCompare.Text = "Produce Dose Comparison table";
            this.chkDoseCompare.UseVisualStyleBackColor = true;
            // 
            // doseProgressBar
            // 
            this.doseProgressBar.Location = new System.Drawing.Point(10, 426);
            this.doseProgressBar.Name = "doseProgressBar";
            this.doseProgressBar.Size = new System.Drawing.Size(862, 23);
            this.doseProgressBar.TabIndex = 25;
            // 
            // testDirectories
            // 
            this.testDirectories.Location = new System.Drawing.Point(797, 12);
            this.testDirectories.Name = "testDirectories";
            this.testDirectories.Size = new System.Drawing.Size(75, 23);
            this.testDirectories.TabIndex = 9;
            this.testDirectories.Text = "Test Directories";
            this.testDirectories.UseVisualStyleBackColor = true;
            this.testDirectories.Click += new System.EventHandler(this.TestDirectories_Click);
            // 
            // tbxTargetLabel
            // 
            this.tbxTargetLabel.Location = new System.Drawing.Point(93, 315);
            this.tbxTargetLabel.Name = "tbxTargetLabel";
            this.tbxTargetLabel.Size = new System.Drawing.Size(455, 20);
            this.tbxTargetLabel.TabIndex = 5;
            this.tbxTargetLabel.TextChanged += new System.EventHandler(this.TbxTargetLabel_TextChanged);
            // 
            // tbxSourceLabel
            // 
            this.tbxSourceLabel.Location = new System.Drawing.Point(93, 263);
            this.tbxSourceLabel.Name = "tbxSourceLabel";
            this.tbxSourceLabel.Size = new System.Drawing.Size(455, 20);
            this.tbxSourceLabel.TabIndex = 3;
            this.tbxSourceLabel.TextChanged += new System.EventHandler(this.TbxSourceLabel_TextChanged);
            // 
            // lblTargetRefName
            // 
            this.lblTargetRefName.AutoSize = true;
            this.lblTargetRefName.Location = new System.Drawing.Point(20, 318);
            this.lblTargetRefName.Name = "lblTargetRefName";
            this.lblTargetRefName.Size = new System.Drawing.Size(67, 13);
            this.lblTargetRefName.TabIndex = 31;
            this.lblTargetRefName.Text = "Target Label";
            // 
            // lblSourceRefName
            // 
            this.lblSourceRefName.AutoSize = true;
            this.lblSourceRefName.Location = new System.Drawing.Point(17, 266);
            this.lblSourceRefName.Name = "lblSourceRefName";
            this.lblSourceRefName.Size = new System.Drawing.Size(70, 13);
            this.lblSourceRefName.TabIndex = 32;
            this.lblSourceRefName.Text = "Source Label";
            // 
            // lblRunStatus
            // 
            this.lblRunStatus.AutoSize = true;
            this.lblRunStatus.Location = new System.Drawing.Point(453, 95);
            this.lblRunStatus.Name = "lblRunStatus";
            this.lblRunStatus.Size = new System.Drawing.Size(37, 13);
            this.lblRunStatus.TabIndex = 33;
            this.lblRunStatus.Text = "Status";
            // 
            // chkBoxUseGPU
            // 
            this.chkBoxUseGPU.AutoSize = true;
            this.chkBoxUseGPU.Location = new System.Drawing.Point(794, 118);
            this.chkBoxUseGPU.Name = "chkBoxUseGPU";
            this.chkBoxUseGPU.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkBoxUseGPU.Size = new System.Drawing.Size(78, 17);
            this.chkBoxUseGPU.TabIndex = 34;
            this.chkBoxUseGPU.Text = "Use CUDA";
            this.chkBoxUseGPU.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(884, 461);
            this.Controls.Add(this.chkBoxUseGPU);
            this.Controls.Add(this.lblRunStatus);
            this.Controls.Add(this.lblSourceRefName);
            this.Controls.Add(this.lblTargetRefName);
            this.Controls.Add(this.tbxSourceLabel);
            this.Controls.Add(this.tbxTargetLabel);
            this.Controls.Add(this.testDirectories);
            this.Controls.Add(this.doseProgressBar);
            this.Controls.Add(this.chkDoseCompare);
            this.Controls.Add(this.chkPDDCompare);
            this.Controls.Add(this.lblSaveName);
            this.Controls.Add(this.tbxSaveName);
            this.Controls.Add(this.tbxSaveDir);
            this.Controls.Add(this.btnSaveDir);
            this.Controls.Add(this.tbxThreshholdTol);
            this.Controls.Add(this.lblthreshhold);
            this.Controls.Add(this.lblTargetFilesFound);
            this.Controls.Add(this.lblSourceFilesFound);
            this.Controls.Add(this.lblTargetFilesFoundText);
            this.Controls.Add(this.lblSourceFilesFoundText);
            this.Controls.Add(this.btnTargetDir);
            this.Controls.Add(this.btnSourceDir);
            this.Controls.Add(this.btnExecute);
            this.Controls.Add(this.tbxMainTol);
            this.Controls.Add(this.tbxTightTol);
            this.Controls.Add(this.lblMainTol);
            this.Controls.Add(this.lblTightTol);
            this.Controls.Add(this.tbxTarget);
            this.Controls.Add(this.tbxSource);
            this.Controls.Add(this.lblIntro);
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "Form1";
            this.Text = "Dicom Dose Comparison";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.TextBox tbxSource;
        private System.Windows.Forms.TextBox tbxTarget;
        private System.Windows.Forms.Label lblTightTol;
        private System.Windows.Forms.Label lblMainTol;
        private System.Windows.Forms.TextBox tbxTightTol;
        private System.Windows.Forms.TextBox tbxMainTol;
        private System.Windows.Forms.Button btnExecute;
        private System.Windows.Forms.Button btnSourceDir;
        private System.Windows.Forms.Button btnTargetDir;
        private System.Windows.Forms.Label lblSourceFilesFoundText;
        private System.Windows.Forms.Label lblTargetFilesFoundText;
        private System.Windows.Forms.Label lblSourceFilesFound;
        private System.Windows.Forms.Label lblTargetFilesFound;
        private System.Windows.Forms.TextBox tbxThreshholdTol;
        private System.Windows.Forms.Label lblthreshhold;
        private System.Windows.Forms.Button btnSaveDir;
        private System.Windows.Forms.TextBox tbxSaveDir;
        private System.Windows.Forms.TextBox tbxSaveName;
        private System.Windows.Forms.Label lblSaveName;
        private System.Windows.Forms.CheckBox chkPDDCompare;
        private System.Windows.Forms.CheckBox chkDoseCompare;
        private System.Windows.Forms.ProgressBar doseProgressBar;
        private System.Windows.Forms.Button testDirectories;
        private System.Windows.Forms.TextBox tbxTargetLabel;
        private System.Windows.Forms.TextBox tbxSourceLabel;
        private System.Windows.Forms.Label lblTargetRefName;
        private System.Windows.Forms.Label lblSourceRefName;
        private System.Windows.Forms.Label lblRunStatus;
        private System.Windows.Forms.CheckBox chkBoxUseGPU;
    }
}

