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
            this.btnExecute = new System.Windows.Forms.Button();
            this.btnSourceDir = new System.Windows.Forms.Button();
            this.btnTargetDir = new System.Windows.Forms.Button();
            this.lblSourceFilesFoundText = new System.Windows.Forms.Label();
            this.lblTargetFilesFoundText = new System.Windows.Forms.Label();
            this.lblSourceFilesFound = new System.Windows.Forms.Label();
            this.lblTargetFilesFound = new System.Windows.Forms.Label();
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.dtaListPairs = new System.Windows.Forms.ListBox();
            this.addDTAs = new System.Windows.Forms.GroupBox();
            this.txtBxTrim = new System.Windows.Forms.TextBox();
            this.lblTrim = new System.Windows.Forms.Label();
            this.units = new System.Windows.Forms.ListBox();
            this.btnDAadd = new System.Windows.Forms.Button();
            this.chkBoxDArel = new System.Windows.Forms.CheckBox();
            this.txtBoxDAdta = new System.Windows.Forms.TextBox();
            this.lblDAdta = new System.Windows.Forms.Label();
            this.txtBoxDAthres = new System.Windows.Forms.TextBox();
            this.lblDAthresh = new System.Windows.Forms.Label();
            this.txtBoxDAtol = new System.Windows.Forms.TextBox();
            this.lblDAtol = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.addDTAs.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblIntro
            // 
            this.lblIntro.AutoSize = true;
            this.lblIntro.Location = new System.Drawing.Point(6, 9);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(389, 221);
            this.lblIntro.TabIndex = 0;
            this.lblIntro.Text = resources.GetString("lblIntro.Text");
            // 
            // tbxSource
            // 
            this.tbxSource.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbxSource.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbxSource.Location = new System.Drawing.Point(87, 32);
            this.tbxSource.MinimumSize = new System.Drawing.Size(314, 20);
            this.tbxSource.Name = "tbxSource";
            this.tbxSource.Size = new System.Drawing.Size(742, 20);
            this.tbxSource.TabIndex = 4;
            this.tbxSource.TextChanged += new System.EventHandler(this.TbxSource_TextChanged);
            // 
            // tbxTarget
            // 
            this.tbxTarget.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbxTarget.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbxTarget.Location = new System.Drawing.Point(87, 85);
            this.tbxTarget.MinimumSize = new System.Drawing.Size(314, 20);
            this.tbxTarget.Name = "tbxTarget";
            this.tbxTarget.Size = new System.Drawing.Size(742, 20);
            this.tbxTarget.TabIndex = 6;
            this.tbxTarget.TextChanged += new System.EventHandler(this.TbxTarget_TextChanged);
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(173, 36);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(75, 23);
            this.btnExecute.TabIndex = 10;
            this.btnExecute.Text = "Run";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.BtnExecute_Click);
            // 
            // btnSourceDir
            // 
            this.btnSourceDir.Location = new System.Drawing.Point(9, 30);
            this.btnSourceDir.Name = "btnSourceDir";
            this.btnSourceDir.Size = new System.Drawing.Size(75, 23);
            this.btnSourceDir.TabIndex = 10;
            this.btnSourceDir.Text = "Source";
            this.btnSourceDir.UseVisualStyleBackColor = true;
            this.btnSourceDir.Click += new System.EventHandler(this.BtnSourceDir_Click);
            // 
            // btnTargetDir
            // 
            this.btnTargetDir.Location = new System.Drawing.Point(6, 83);
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
            this.lblSourceFilesFoundText.Location = new System.Drawing.Point(10, 12);
            this.lblSourceFilesFoundText.Name = "lblSourceFilesFoundText";
            this.lblSourceFilesFoundText.Size = new System.Drawing.Size(126, 13);
            this.lblSourceFilesFoundText.TabIndex = 12;
            this.lblSourceFilesFoundText.Text = "Source Dose Files Found";
            // 
            // lblTargetFilesFoundText
            // 
            this.lblTargetFilesFoundText.AutoSize = true;
            this.lblTargetFilesFoundText.Location = new System.Drawing.Point(10, 41);
            this.lblTargetFilesFoundText.Name = "lblTargetFilesFoundText";
            this.lblTargetFilesFoundText.Size = new System.Drawing.Size(123, 13);
            this.lblTargetFilesFoundText.TabIndex = 13;
            this.lblTargetFilesFoundText.Text = "Target Dose Files Found";
            // 
            // lblSourceFilesFound
            // 
            this.lblSourceFilesFound.AutoSize = true;
            this.lblSourceFilesFound.Location = new System.Drawing.Point(142, 12);
            this.lblSourceFilesFound.Name = "lblSourceFilesFound";
            this.lblSourceFilesFound.Size = new System.Drawing.Size(14, 13);
            this.lblSourceFilesFound.TabIndex = 14;
            this.lblSourceFilesFound.Text = "#";
            // 
            // lblTargetFilesFound
            // 
            this.lblTargetFilesFound.AutoSize = true;
            this.lblTargetFilesFound.Location = new System.Drawing.Point(142, 41);
            this.lblTargetFilesFound.Name = "lblTargetFilesFound";
            this.lblTargetFilesFound.Size = new System.Drawing.Size(14, 13);
            this.lblTargetFilesFound.TabIndex = 15;
            this.lblTargetFilesFound.Text = "#";
            // 
            // btnSaveDir
            // 
            this.btnSaveDir.Location = new System.Drawing.Point(4, 135);
            this.btnSaveDir.Name = "btnSaveDir";
            this.btnSaveDir.Size = new System.Drawing.Size(75, 23);
            this.btnSaveDir.TabIndex = 18;
            this.btnSaveDir.Text = "Save Location";
            this.btnSaveDir.UseVisualStyleBackColor = true;
            this.btnSaveDir.Click += new System.EventHandler(this.BtnSaveDir_Click);
            // 
            // tbxSaveDir
            // 
            this.tbxSaveDir.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbxSaveDir.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbxSaveDir.Location = new System.Drawing.Point(87, 137);
            this.tbxSaveDir.MinimumSize = new System.Drawing.Size(314, 20);
            this.tbxSaveDir.Name = "tbxSaveDir";
            this.tbxSaveDir.Size = new System.Drawing.Size(742, 20);
            this.tbxSaveDir.TabIndex = 8;
            this.tbxSaveDir.TextChanged += new System.EventHandler(this.TbxSaveDir_TextChanged);
            // 
            // tbxSaveName
            // 
            this.tbxSaveName.Location = new System.Drawing.Point(87, 111);
            this.tbxSaveName.MinimumSize = new System.Drawing.Size(314, 20);
            this.tbxSaveName.Name = "tbxSaveName";
            this.tbxSaveName.Size = new System.Drawing.Size(742, 20);
            this.tbxSaveName.TabIndex = 7;
            this.tbxSaveName.TextChanged += new System.EventHandler(this.TbxSaveName_TextChanged);
            // 
            // lblSaveName
            // 
            this.lblSaveName.AutoSize = true;
            this.lblSaveName.Location = new System.Drawing.Point(16, 114);
            this.lblSaveName.Name = "lblSaveName";
            this.lblSaveName.Size = new System.Drawing.Size(63, 13);
            this.lblSaveName.TabIndex = 21;
            this.lblSaveName.Text = "Save Name";
            // 
            // chkPDDCompare
            // 
            this.chkPDDCompare.AutoSize = true;
            this.chkPDDCompare.Location = new System.Drawing.Point(73, 67);
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
            this.chkDoseCompare.Location = new System.Drawing.Point(70, 90);
            this.chkDoseCompare.Name = "chkDoseCompare";
            this.chkDoseCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkDoseCompare.Size = new System.Drawing.Size(178, 17);
            this.chkDoseCompare.TabIndex = 24;
            this.chkDoseCompare.Text = "Produce Dose Comparison table";
            this.chkDoseCompare.UseVisualStyleBackColor = true;
            // 
            // doseProgressBar
            // 
            this.doseProgressBar.Location = new System.Drawing.Point(3, 169);
            this.doseProgressBar.MinimumSize = new System.Drawing.Size(400, 25);
            this.doseProgressBar.Name = "doseProgressBar";
            this.doseProgressBar.Size = new System.Drawing.Size(828, 25);
            this.doseProgressBar.TabIndex = 25;
            // 
            // testDirectories
            // 
            this.testDirectories.Location = new System.Drawing.Point(173, 7);
            this.testDirectories.Name = "testDirectories";
            this.testDirectories.Size = new System.Drawing.Size(75, 23);
            this.testDirectories.TabIndex = 9;
            this.testDirectories.Text = "Test Directories";
            this.testDirectories.UseVisualStyleBackColor = true;
            this.testDirectories.Click += new System.EventHandler(this.TestDirectories_Click);
            // 
            // tbxTargetLabel
            // 
            this.tbxTargetLabel.Location = new System.Drawing.Point(87, 58);
            this.tbxTargetLabel.MinimumSize = new System.Drawing.Size(314, 20);
            this.tbxTargetLabel.Name = "tbxTargetLabel";
            this.tbxTargetLabel.Size = new System.Drawing.Size(742, 20);
            this.tbxTargetLabel.TabIndex = 5;
            this.tbxTargetLabel.TextChanged += new System.EventHandler(this.TbxTargetLabel_TextChanged);
            // 
            // tbxSourceLabel
            // 
            this.tbxSourceLabel.Location = new System.Drawing.Point(87, 6);
            this.tbxSourceLabel.MinimumSize = new System.Drawing.Size(314, 20);
            this.tbxSourceLabel.Name = "tbxSourceLabel";
            this.tbxSourceLabel.Size = new System.Drawing.Size(742, 20);
            this.tbxSourceLabel.TabIndex = 3;
            this.tbxSourceLabel.TextChanged += new System.EventHandler(this.TbxSourceLabel_TextChanged);
            // 
            // lblTargetRefName
            // 
            this.lblTargetRefName.AutoSize = true;
            this.lblTargetRefName.Location = new System.Drawing.Point(14, 61);
            this.lblTargetRefName.Name = "lblTargetRefName";
            this.lblTargetRefName.Size = new System.Drawing.Size(67, 13);
            this.lblTargetRefName.TabIndex = 31;
            this.lblTargetRefName.Text = "Target Label";
            // 
            // lblSourceRefName
            // 
            this.lblSourceRefName.AutoSize = true;
            this.lblSourceRefName.Location = new System.Drawing.Point(11, 9);
            this.lblSourceRefName.Name = "lblSourceRefName";
            this.lblSourceRefName.Size = new System.Drawing.Size(70, 13);
            this.lblSourceRefName.TabIndex = 32;
            this.lblSourceRefName.Text = "Source Label";
            // 
            // lblRunStatus
            // 
            this.lblRunStatus.AutoSize = true;
            this.lblRunStatus.Location = new System.Drawing.Point(597, 169);
            this.lblRunStatus.Name = "lblRunStatus";
            this.lblRunStatus.Size = new System.Drawing.Size(37, 13);
            this.lblRunStatus.TabIndex = 33;
            this.lblRunStatus.Text = "Status";
            // 
            // groupBox1
            // 
            this.groupBox1.AutoSize = true;
            this.groupBox1.Controls.Add(this.testDirectories);
            this.groupBox1.Controls.Add(this.chkDoseCompare);
            this.groupBox1.Controls.Add(this.chkPDDCompare);
            this.groupBox1.Controls.Add(this.lblTargetFilesFound);
            this.groupBox1.Controls.Add(this.lblSourceFilesFound);
            this.groupBox1.Controls.Add(this.lblTargetFilesFoundText);
            this.groupBox1.Controls.Add(this.lblSourceFilesFoundText);
            this.groupBox1.Controls.Add(this.btnExecute);
            this.groupBox1.Location = new System.Drawing.Point(587, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(254, 163);
            this.groupBox1.TabIndex = 35;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "groupBox1";
            // 
            // panel1
            // 
            this.panel1.AutoSize = true;
            this.panel1.Controls.Add(this.lblIntro);
            this.panel1.Location = new System.Drawing.Point(6, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(401, 230);
            this.panel1.TabIndex = 37;
            // 
            // panel2
            // 
            this.panel2.AutoSize = true;
            this.panel2.Controls.Add(this.lblSourceRefName);
            this.panel2.Controls.Add(this.lblTargetRefName);
            this.panel2.Controls.Add(this.tbxSourceLabel);
            this.panel2.Controls.Add(this.tbxTargetLabel);
            this.panel2.Controls.Add(this.doseProgressBar);
            this.panel2.Controls.Add(this.lblSaveName);
            this.panel2.Controls.Add(this.tbxSaveName);
            this.panel2.Controls.Add(this.tbxSaveDir);
            this.panel2.Controls.Add(this.btnSaveDir);
            this.panel2.Controls.Add(this.btnTargetDir);
            this.panel2.Controls.Add(this.btnSourceDir);
            this.panel2.Controls.Add(this.tbxTarget);
            this.panel2.Controls.Add(this.tbxSource);
            this.panel2.Location = new System.Drawing.Point(6, 235);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(835, 197);
            this.panel2.TabIndex = 38;
            // 
            // dtaListPairs
            // 
            this.dtaListPairs.FormattingEnabled = true;
            this.dtaListPairs.Location = new System.Drawing.Point(9, 454);
            this.dtaListPairs.Name = "dtaListPairs";
            this.dtaListPairs.Size = new System.Drawing.Size(673, 160);
            this.dtaListPairs.TabIndex = 39;
            this.dtaListPairs.SelectedIndexChanged += new System.EventHandler(this.DtaListPairs_SelectedIndexChanged);
            // 
            // addDTAs
            // 
            this.addDTAs.Controls.Add(this.txtBxTrim);
            this.addDTAs.Controls.Add(this.lblTrim);
            this.addDTAs.Controls.Add(this.units);
            this.addDTAs.Controls.Add(this.btnDAadd);
            this.addDTAs.Controls.Add(this.chkBoxDArel);
            this.addDTAs.Controls.Add(this.txtBoxDAdta);
            this.addDTAs.Controls.Add(this.lblDAdta);
            this.addDTAs.Controls.Add(this.txtBoxDAthres);
            this.addDTAs.Controls.Add(this.lblDAthresh);
            this.addDTAs.Controls.Add(this.txtBoxDAtol);
            this.addDTAs.Controls.Add(this.lblDAtol);
            this.addDTAs.Location = new System.Drawing.Point(413, 3);
            this.addDTAs.Name = "addDTAs";
            this.addDTAs.Size = new System.Drawing.Size(153, 226);
            this.addDTAs.TabIndex = 40;
            this.addDTAs.TabStop = false;
            this.addDTAs.Text = "Add DTA\'s";
            // 
            // txtBxTrim
            // 
            this.txtBxTrim.Location = new System.Drawing.Point(89, 103);
            this.txtBxTrim.Name = "txtBxTrim";
            this.txtBxTrim.Size = new System.Drawing.Size(55, 20);
            this.txtBxTrim.TabIndex = 26;
            // 
            // lblTrim
            // 
            this.lblTrim.AutoSize = true;
            this.lblTrim.Location = new System.Drawing.Point(7, 106);
            this.lblTrim.Name = "lblTrim";
            this.lblTrim.Size = new System.Drawing.Size(60, 13);
            this.lblTrim.TabIndex = 27;
            this.lblTrim.Text = "Trim voxels";
            // 
            // units
            // 
            this.units.FormattingEnabled = true;
            this.units.Items.AddRange(new object[] {
            "mm",
            "Voxels"});
            this.units.Location = new System.Drawing.Point(89, 39);
            this.units.Name = "units";
            this.units.Size = new System.Drawing.Size(55, 30);
            this.units.TabIndex = 25;
            // 
            // btnDAadd
            // 
            this.btnDAadd.Location = new System.Drawing.Point(40, 169);
            this.btnDAadd.Name = "btnDAadd";
            this.btnDAadd.Size = new System.Drawing.Size(75, 23);
            this.btnDAadd.TabIndex = 24;
            this.btnDAadd.Text = "Add";
            this.btnDAadd.UseVisualStyleBackColor = true;
            this.btnDAadd.Click += new System.EventHandler(this.BtnDAadd_Click);
            // 
            // chkBoxDArel
            // 
            this.chkBoxDArel.AutoSize = true;
            this.chkBoxDArel.Location = new System.Drawing.Point(9, 146);
            this.chkBoxDArel.Name = "chkBoxDArel";
            this.chkBoxDArel.Size = new System.Drawing.Size(134, 17);
            this.chkBoxDArel.TabIndex = 23;
            this.chkBoxDArel.Text = "Relative to Max Dose?";
            this.chkBoxDArel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkBoxDArel.UseVisualStyleBackColor = true;
            // 
            // txtBoxDAdta
            // 
            this.txtBoxDAdta.Location = new System.Drawing.Point(41, 45);
            this.txtBoxDAdta.Name = "txtBoxDAdta";
            this.txtBoxDAdta.Size = new System.Drawing.Size(42, 20);
            this.txtBoxDAdta.TabIndex = 20;
            // 
            // lblDAdta
            // 
            this.lblDAdta.AutoSize = true;
            this.lblDAdta.Location = new System.Drawing.Point(6, 48);
            this.lblDAdta.Name = "lblDAdta";
            this.lblDAdta.Size = new System.Drawing.Size(29, 13);
            this.lblDAdta.TabIndex = 21;
            this.lblDAdta.Text = "DTA";
            // 
            // txtBoxDAthres
            // 
            this.txtBoxDAthres.Location = new System.Drawing.Point(89, 75);
            this.txtBoxDAthres.Name = "txtBoxDAthres";
            this.txtBoxDAthres.Size = new System.Drawing.Size(55, 20);
            this.txtBoxDAthres.TabIndex = 17;
            // 
            // lblDAthresh
            // 
            this.lblDAthresh.AutoSize = true;
            this.lblDAthresh.Location = new System.Drawing.Point(6, 78);
            this.lblDAthresh.Name = "lblDAthresh";
            this.lblDAthresh.Size = new System.Drawing.Size(77, 13);
            this.lblDAthresh.TabIndex = 18;
            this.lblDAthresh.Text = "Threshhold (%)";
            // 
            // txtBoxDAtol
            // 
            this.txtBoxDAtol.Location = new System.Drawing.Point(89, 13);
            this.txtBoxDAtol.Name = "txtBoxDAtol";
            this.txtBoxDAtol.Size = new System.Drawing.Size(55, 20);
            this.txtBoxDAtol.TabIndex = 6;
            // 
            // lblDAtol
            // 
            this.lblDAtol.AutoSize = true;
            this.lblDAtol.Location = new System.Drawing.Point(6, 16);
            this.lblDAtol.Name = "lblDAtol";
            this.lblDAtol.Size = new System.Drawing.Size(72, 13);
            this.lblDAtol.TabIndex = 7;
            this.lblDAtol.Text = "Tolerance (%)";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(846, 638);
            this.Controls.Add(this.addDTAs);
            this.Controls.Add(this.dtaListPairs);
            this.Controls.Add(this.lblRunStatus);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.groupBox1);
            this.MinimumSize = new System.Drawing.Size(862, 476);
            this.Name = "Form1";
            this.Text = "Dicom Dose Comparison";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.addDTAs.ResumeLayout(false);
            this.addDTAs.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblIntro;
        private System.Windows.Forms.TextBox tbxSource;
        private System.Windows.Forms.TextBox tbxTarget;
        private System.Windows.Forms.Button btnExecute;
        private System.Windows.Forms.Button btnSourceDir;
        private System.Windows.Forms.Button btnTargetDir;
        private System.Windows.Forms.Label lblSourceFilesFoundText;
        private System.Windows.Forms.Label lblTargetFilesFoundText;
        private System.Windows.Forms.Label lblSourceFilesFound;
        private System.Windows.Forms.Label lblTargetFilesFound;
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
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.ListBox dtaListPairs;
        private System.Windows.Forms.GroupBox addDTAs;
        private System.Windows.Forms.TextBox txtBoxDAthres;
        private System.Windows.Forms.Label lblDAthresh;
        private System.Windows.Forms.TextBox txtBoxDAtol;
        private System.Windows.Forms.Label lblDAtol;
        private System.Windows.Forms.TextBox txtBoxDAdta;
        private System.Windows.Forms.Label lblDAdta;
        private System.Windows.Forms.Button btnDAadd;
        private System.Windows.Forms.CheckBox chkBoxDArel;
        private System.Windows.Forms.ListBox units;
        private System.Windows.Forms.TextBox txtBxTrim;
        private System.Windows.Forms.Label lblTrim;
    }
}

