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
            lblIntro = new System.Windows.Forms.Label();
            tbxSource = new System.Windows.Forms.TextBox();
            tbxTarget = new System.Windows.Forms.TextBox();
            btnExecute = new System.Windows.Forms.Button();
            btnSourceDir = new System.Windows.Forms.Button();
            btnTargetDir = new System.Windows.Forms.Button();
            lblSourceFilesFoundText = new System.Windows.Forms.Label();
            lblTargetFilesFoundText = new System.Windows.Forms.Label();
            lblSourceFilesFound = new System.Windows.Forms.Label();
            lblTargetFilesFound = new System.Windows.Forms.Label();
            btnSaveDir = new System.Windows.Forms.Button();
            tbxSaveDir = new System.Windows.Forms.TextBox();
            tbxSaveName = new System.Windows.Forms.TextBox();
            lblSaveName = new System.Windows.Forms.Label();
            chkPDDCompare = new System.Windows.Forms.CheckBox();
            chkDoseCompare = new System.Windows.Forms.CheckBox();
            doseProgressBar = new System.Windows.Forms.ProgressBar();
            testDirectories = new System.Windows.Forms.Button();
            tbxTargetLabel = new System.Windows.Forms.TextBox();
            tbxSourceLabel = new System.Windows.Forms.TextBox();
            lblTargetRefName = new System.Windows.Forms.Label();
            lblSourceRefName = new System.Windows.Forms.Label();
            lblRunStatus = new System.Windows.Forms.Label();
            testAndRunBox = new System.Windows.Forms.GroupBox();
            panel2 = new System.Windows.Forms.Panel();
            dtaListPairs = new System.Windows.Forms.ListBox();
            addDTAs = new System.Windows.Forms.GroupBox();
            chkBoxGamma = new System.Windows.Forms.CheckBox();
            txtBxTrim = new System.Windows.Forms.TextBox();
            lblTrim = new System.Windows.Forms.Label();
            units = new System.Windows.Forms.ListBox();
            btnDAadd = new System.Windows.Forms.Button();
            chkBoxDArel = new System.Windows.Forms.CheckBox();
            txtBoxDAdta = new System.Windows.Forms.TextBox();
            lblDAdta = new System.Windows.Forms.Label();
            txtBoxDAthres = new System.Windows.Forms.TextBox();
            lblDAthresh = new System.Windows.Forms.Label();
            txtBoxDAtol = new System.Windows.Forms.TextBox();
            lblDAtol = new System.Windows.Forms.Label();
            dtaListBoxTitle = new System.Windows.Forms.TextBox();
            btnDRemove = new System.Windows.Forms.Button();
            testAndRunBox.SuspendLayout();
            panel2.SuspendLayout();
            addDTAs.SuspendLayout();
            SuspendLayout();
            // 
            // lblIntro
            // 
            lblIntro.AutoSize = true;
            lblIntro.Location = new System.Drawing.Point(3, 238);
            lblIntro.Name = "lblIntro";
            lblIntro.Size = new System.Drawing.Size(623, 432);
            lblIntro.TabIndex = 0;
            lblIntro.Text = resources.GetString("lblIntro.Text");
            lblIntro.Click += lblIntro_Click;
            // 
            // tbxSource
            // 
            tbxSource.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            tbxSource.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            tbxSource.Location = new System.Drawing.Point(106, 32);
            tbxSource.MinimumSize = new System.Drawing.Size(314, 20);
            tbxSource.Name = "tbxSource";
            tbxSource.Size = new System.Drawing.Size(362, 21);
            tbxSource.TabIndex = 2;
            tbxSource.TextChanged += TbxSource_TextChanged;
            // 
            // tbxTarget
            // 
            tbxTarget.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            tbxTarget.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            tbxTarget.Location = new System.Drawing.Point(106, 85);
            tbxTarget.MinimumSize = new System.Drawing.Size(314, 20);
            tbxTarget.Name = "tbxTarget";
            tbxTarget.Size = new System.Drawing.Size(362, 21);
            tbxTarget.TabIndex = 5;
            tbxTarget.TextChanged += TbxTarget_TextChanged;
            // 
            // btnExecute
            // 
            btnExecute.Location = new System.Drawing.Point(233, 105);
            btnExecute.Name = "btnExecute";
            btnExecute.Size = new System.Drawing.Size(75, 23);
            btnExecute.TabIndex = 7;
            btnExecute.Text = "Run";
            btnExecute.UseVisualStyleBackColor = true;
            btnExecute.Click += BtnExecute_Click;
            // 
            // btnSourceDir
            // 
            btnSourceDir.Location = new System.Drawing.Point(9, 30);
            btnSourceDir.Name = "btnSourceDir";
            btnSourceDir.Size = new System.Drawing.Size(75, 23);
            btnSourceDir.TabIndex = 1;
            btnSourceDir.Text = "Source";
            btnSourceDir.UseVisualStyleBackColor = true;
            btnSourceDir.Click += BtnSourceDir_Click;
            // 
            // btnTargetDir
            // 
            btnTargetDir.Location = new System.Drawing.Point(6, 83);
            btnTargetDir.Name = "btnTargetDir";
            btnTargetDir.Size = new System.Drawing.Size(75, 23);
            btnTargetDir.TabIndex = 4;
            btnTargetDir.Text = "Target";
            btnTargetDir.UseVisualStyleBackColor = true;
            btnTargetDir.Click += BtnTargetDir_Click;
            // 
            // lblSourceFilesFoundText
            // 
            lblSourceFilesFoundText.AutoSize = true;
            lblSourceFilesFoundText.Location = new System.Drawing.Point(6, 24);
            lblSourceFilesFoundText.Name = "lblSourceFilesFoundText";
            lblSourceFilesFoundText.Size = new System.Drawing.Size(168, 16);
            lblSourceFilesFoundText.TabIndex = 12;
            lblSourceFilesFoundText.Text = "Source Dose Files Found";
            // 
            // lblTargetFilesFoundText
            // 
            lblTargetFilesFoundText.AutoSize = true;
            lblTargetFilesFoundText.Location = new System.Drawing.Point(6, 113);
            lblTargetFilesFoundText.Name = "lblTargetFilesFoundText";
            lblTargetFilesFoundText.Size = new System.Drawing.Size(168, 16);
            lblTargetFilesFoundText.TabIndex = 13;
            lblTargetFilesFoundText.Text = "Target Dose Files Found";
            // 
            // lblSourceFilesFound
            // 
            lblSourceFilesFound.AutoSize = true;
            lblSourceFilesFound.Location = new System.Drawing.Point(182, 22);
            lblSourceFilesFound.Name = "lblSourceFilesFound";
            lblSourceFilesFound.Size = new System.Drawing.Size(14, 16);
            lblSourceFilesFound.TabIndex = 14;
            lblSourceFilesFound.Text = "#";
            // 
            // lblTargetFilesFound
            // 
            lblTargetFilesFound.AutoSize = true;
            lblTargetFilesFound.Location = new System.Drawing.Point(182, 114);
            lblTargetFilesFound.Name = "lblTargetFilesFound";
            lblTargetFilesFound.Size = new System.Drawing.Size(14, 16);
            lblTargetFilesFound.TabIndex = 15;
            lblTargetFilesFound.Text = "#";
            // 
            // btnSaveDir
            // 
            btnSaveDir.Location = new System.Drawing.Point(4, 135);
            btnSaveDir.Name = "btnSaveDir";
            btnSaveDir.Size = new System.Drawing.Size(75, 23);
            btnSaveDir.TabIndex = 7;
            btnSaveDir.Text = "Save Location";
            btnSaveDir.UseVisualStyleBackColor = true;
            btnSaveDir.Click += BtnSaveDir_Click;
            // 
            // tbxSaveDir
            // 
            tbxSaveDir.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            tbxSaveDir.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            tbxSaveDir.Location = new System.Drawing.Point(106, 137);
            tbxSaveDir.MinimumSize = new System.Drawing.Size(314, 20);
            tbxSaveDir.Name = "tbxSaveDir";
            tbxSaveDir.Size = new System.Drawing.Size(362, 21);
            tbxSaveDir.TabIndex = 8;
            tbxSaveDir.TextChanged += TbxSaveDir_TextChanged;
            // 
            // tbxSaveName
            // 
            tbxSaveName.Location = new System.Drawing.Point(106, 111);
            tbxSaveName.MinimumSize = new System.Drawing.Size(314, 20);
            tbxSaveName.Name = "tbxSaveName";
            tbxSaveName.Size = new System.Drawing.Size(362, 21);
            tbxSaveName.TabIndex = 6;
            tbxSaveName.TextChanged += TbxSaveName_TextChanged;
            // 
            // lblSaveName
            // 
            lblSaveName.AutoSize = true;
            lblSaveName.Location = new System.Drawing.Point(16, 114);
            lblSaveName.Name = "lblSaveName";
            lblSaveName.Size = new System.Drawing.Size(70, 16);
            lblSaveName.TabIndex = 21;
            lblSaveName.Text = "Save Name";
            // 
            // chkPDDCompare
            // 
            chkPDDCompare.AutoSize = true;
            chkPDDCompare.Location = new System.Drawing.Point(44, 50);
            chkPDDCompare.Name = "chkPDDCompare";
            chkPDDCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            chkPDDCompare.Size = new System.Drawing.Size(222, 20);
            chkPDDCompare.TabIndex = 5;
            chkPDDCompare.Text = "Produce PDD comparison Plots";
            chkPDDCompare.UseVisualStyleBackColor = true;
            // 
            // chkDoseCompare
            // 
            chkDoseCompare.AutoSize = true;
            chkDoseCompare.Location = new System.Drawing.Point(39, 74);
            chkDoseCompare.Name = "chkDoseCompare";
            chkDoseCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            chkDoseCompare.Size = new System.Drawing.Size(229, 20);
            chkDoseCompare.TabIndex = 6;
            chkDoseCompare.Text = "Produce Dose Comparison table";
            chkDoseCompare.UseVisualStyleBackColor = true;
            // 
            // doseProgressBar
            // 
            doseProgressBar.Location = new System.Drawing.Point(3, 169);
            doseProgressBar.MinimumSize = new System.Drawing.Size(400, 25);
            doseProgressBar.Name = "doseProgressBar";
            doseProgressBar.Size = new System.Drawing.Size(465, 25);
            doseProgressBar.TabIndex = 25;
            // 
            // testDirectories
            // 
            testDirectories.Location = new System.Drawing.Point(233, 21);
            testDirectories.Name = "testDirectories";
            testDirectories.Size = new System.Drawing.Size(75, 23);
            testDirectories.TabIndex = 4;
            testDirectories.Text = "Test Directories";
            testDirectories.UseVisualStyleBackColor = true;
            testDirectories.Click += TestDirectories_Click;
            // 
            // tbxTargetLabel
            // 
            tbxTargetLabel.Location = new System.Drawing.Point(106, 58);
            tbxTargetLabel.MinimumSize = new System.Drawing.Size(314, 20);
            tbxTargetLabel.Name = "tbxTargetLabel";
            tbxTargetLabel.Size = new System.Drawing.Size(362, 21);
            tbxTargetLabel.TabIndex = 3;
            tbxTargetLabel.TextChanged += TbxTargetLabel_TextChanged;
            // 
            // tbxSourceLabel
            // 
            tbxSourceLabel.Location = new System.Drawing.Point(106, 6);
            tbxSourceLabel.MinimumSize = new System.Drawing.Size(314, 20);
            tbxSourceLabel.Name = "tbxSourceLabel";
            tbxSourceLabel.Size = new System.Drawing.Size(362, 21);
            tbxSourceLabel.TabIndex = 0;
            tbxSourceLabel.TextChanged += TbxSourceLabel_TextChanged;
            // 
            // lblTargetRefName
            // 
            lblTargetRefName.AutoSize = true;
            lblTargetRefName.Location = new System.Drawing.Point(14, 61);
            lblTargetRefName.Name = "lblTargetRefName";
            lblTargetRefName.Size = new System.Drawing.Size(91, 16);
            lblTargetRefName.TabIndex = 31;
            lblTargetRefName.Text = "Target Label";
            // 
            // lblSourceRefName
            // 
            lblSourceRefName.AutoSize = true;
            lblSourceRefName.Location = new System.Drawing.Point(11, 9);
            lblSourceRefName.Name = "lblSourceRefName";
            lblSourceRefName.Size = new System.Drawing.Size(91, 16);
            lblSourceRefName.TabIndex = 32;
            lblSourceRefName.Text = "Source Label";
            // 
            // lblRunStatus
            // 
            lblRunStatus.AutoSize = true;
            lblRunStatus.Location = new System.Drawing.Point(540, 169);
            lblRunStatus.Name = "lblRunStatus";
            lblRunStatus.Size = new System.Drawing.Size(49, 16);
            lblRunStatus.TabIndex = 33;
            lblRunStatus.Text = "Status";
            // 
            // testAndRunBox
            // 
            testAndRunBox.AutoSize = true;
            testAndRunBox.Controls.Add(testDirectories);
            testAndRunBox.Controls.Add(chkDoseCompare);
            testAndRunBox.Controls.Add(chkPDDCompare);
            testAndRunBox.Controls.Add(lblTargetFilesFound);
            testAndRunBox.Controls.Add(lblSourceFilesFound);
            testAndRunBox.Controls.Add(lblTargetFilesFoundText);
            testAndRunBox.Controls.Add(lblSourceFilesFoundText);
            testAndRunBox.Controls.Add(btnExecute);
            testAndRunBox.Location = new System.Drawing.Point(523, 12);
            testAndRunBox.Name = "testAndRunBox";
            testAndRunBox.Size = new System.Drawing.Size(352, 154);
            testAndRunBox.TabIndex = 35;
            testAndRunBox.TabStop = false;
            testAndRunBox.Text = "Control";
            // 
            // panel2
            // 
            panel2.Controls.Add(doseProgressBar);
            panel2.Controls.Add(lblSourceRefName);
            panel2.Controls.Add(lblTargetRefName);
            panel2.Controls.Add(tbxSourceLabel);
            panel2.Controls.Add(tbxTargetLabel);
            panel2.Controls.Add(lblSaveName);
            panel2.Controls.Add(tbxSaveName);
            panel2.Controls.Add(tbxSaveDir);
            panel2.Controls.Add(btnSaveDir);
            panel2.Controls.Add(btnTargetDir);
            panel2.Controls.Add(btnSourceDir);
            panel2.Controls.Add(tbxTarget);
            panel2.Controls.Add(tbxSource);
            panel2.Location = new System.Drawing.Point(3, 12);
            panel2.Name = "panel2";
            panel2.Size = new System.Drawing.Size(496, 197);
            panel2.TabIndex = 38;
            // 
            // dtaListPairs
            // 
            dtaListPairs.FormattingEnabled = true;
            dtaListPairs.Location = new System.Drawing.Point(658, 268);
            dtaListPairs.Name = "dtaListPairs";
            dtaListPairs.Size = new System.Drawing.Size(217, 164);
            dtaListPairs.TabIndex = 39;
            dtaListPairs.SelectedIndexChanged += DtaListPairs_SelectedIndexChanged;
            // 
            // addDTAs
            // 
            addDTAs.Controls.Add(btnDRemove);
            addDTAs.Controls.Add(chkBoxGamma);
            addDTAs.Controls.Add(txtBxTrim);
            addDTAs.Controls.Add(lblTrim);
            addDTAs.Controls.Add(units);
            addDTAs.Controls.Add(btnDAadd);
            addDTAs.Controls.Add(chkBoxDArel);
            addDTAs.Controls.Add(txtBoxDAdta);
            addDTAs.Controls.Add(lblDAdta);
            addDTAs.Controls.Add(txtBoxDAthres);
            addDTAs.Controls.Add(lblDAthresh);
            addDTAs.Controls.Add(txtBoxDAtol);
            addDTAs.Controls.Add(lblDAtol);
            addDTAs.Location = new System.Drawing.Point(644, 442);
            addDTAs.Name = "addDTAs";
            addDTAs.Size = new System.Drawing.Size(231, 228);
            addDTAs.TabIndex = 40;
            addDTAs.TabStop = false;
            addDTAs.Text = "Add DTA's";
            // 
            // chkBoxGamma
            // 
            chkBoxGamma.AutoSize = true;
            chkBoxGamma.Location = new System.Drawing.Point(6, 168);
            chkBoxGamma.Name = "chkBoxGamma";
            chkBoxGamma.Size = new System.Drawing.Size(68, 20);
            chkBoxGamma.TabIndex = 6;
            chkBoxGamma.Text = "Gamma?";
            chkBoxGamma.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            chkBoxGamma.UseVisualStyleBackColor = true;
            // 
            // txtBxTrim
            // 
            txtBxTrim.Location = new System.Drawing.Point(112, 121);
            txtBxTrim.Name = "txtBxTrim";
            txtBxTrim.Size = new System.Drawing.Size(55, 21);
            txtBxTrim.TabIndex = 4;
            // 
            // lblTrim
            // 
            lblTrim.AutoSize = true;
            lblTrim.Location = new System.Drawing.Point(5, 124);
            lblTrim.Name = "lblTrim";
            lblTrim.Size = new System.Drawing.Size(84, 16);
            lblTrim.TabIndex = 27;
            lblTrim.Text = "Trim voxels";
            // 
            // units
            // 
            units.FormattingEnabled = true;
            units.Items.AddRange(new object[] { "mm", "Voxels" });
            units.Location = new System.Drawing.Point(112, 47);
            units.Name = "units";
            units.Size = new System.Drawing.Size(55, 36);
            units.TabIndex = 2;
            // 
            // btnDAadd
            // 
            btnDAadd.Location = new System.Drawing.Point(6, 199);
            btnDAadd.Name = "btnDAadd";
            btnDAadd.Size = new System.Drawing.Size(75, 23);
            btnDAadd.TabIndex = 7;
            btnDAadd.Text = "Add";
            btnDAadd.UseVisualStyleBackColor = true;
            btnDAadd.Click += BtnDAadd_Click;
            // 
            // chkBoxDArel
            // 
            chkBoxDArel.AutoSize = true;
            chkBoxDArel.Location = new System.Drawing.Point(7, 144);
            chkBoxDArel.Name = "chkBoxDArel";
            chkBoxDArel.Size = new System.Drawing.Size(75, 20);
            chkBoxDArel.TabIndex = 5;
            chkBoxDArel.Text = "Global?";
            chkBoxDArel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            chkBoxDArel.UseVisualStyleBackColor = true;
            // 
            // txtBoxDAdta
            // 
            txtBoxDAdta.Location = new System.Drawing.Point(64, 53);
            txtBoxDAdta.Name = "txtBoxDAdta";
            txtBoxDAdta.Size = new System.Drawing.Size(42, 21);
            txtBoxDAdta.TabIndex = 1;
            // 
            // lblDAdta
            // 
            lblDAdta.AutoSize = true;
            lblDAdta.Location = new System.Drawing.Point(6, 51);
            lblDAdta.Name = "lblDAdta";
            lblDAdta.Size = new System.Drawing.Size(28, 16);
            lblDAdta.TabIndex = 21;
            lblDAdta.Text = "DTA";
            // 
            // txtBoxDAthres
            // 
            txtBoxDAthres.Location = new System.Drawing.Point(112, 93);
            txtBoxDAthres.Name = "txtBoxDAthres";
            txtBoxDAthres.Size = new System.Drawing.Size(55, 21);
            txtBoxDAthres.TabIndex = 3;
            // 
            // lblDAthresh
            // 
            lblDAthresh.AutoSize = true;
            lblDAthresh.Location = new System.Drawing.Point(4, 96);
            lblDAthresh.Name = "lblDAthresh";
            lblDAthresh.Size = new System.Drawing.Size(105, 16);
            lblDAthresh.TabIndex = 18;
            lblDAthresh.Text = "Threshhold (%)";
            // 
            // txtBoxDAtol
            // 
            txtBoxDAtol.Location = new System.Drawing.Point(112, 18);
            txtBoxDAtol.Name = "txtBoxDAtol";
            txtBoxDAtol.Size = new System.Drawing.Size(55, 21);
            txtBoxDAtol.TabIndex = 0;
            // 
            // lblDAtol
            // 
            lblDAtol.AutoSize = true;
            lblDAtol.Location = new System.Drawing.Point(6, 16);
            lblDAtol.Name = "lblDAtol";
            lblDAtol.Size = new System.Drawing.Size(98, 16);
            lblDAtol.TabIndex = 7;
            lblDAtol.Text = "Tolerance (%)";
            // 
            // dtaListBoxTitle
            // 
            dtaListBoxTitle.Location = new System.Drawing.Point(628, 238);
            dtaListBoxTitle.Name = "dtaListBoxTitle";
            dtaListBoxTitle.Size = new System.Drawing.Size(247, 21);
            dtaListBoxTitle.TabIndex = 41;
            dtaListBoxTitle.Text = "Tolerance, Distance, Threshhold, Global?, Unit";
            // 
            // btnDRemove
            // 
            btnDRemove.Location = new System.Drawing.Point(112, 199);
            btnDRemove.Name = "btnDRemove";
            btnDRemove.Size = new System.Drawing.Size(75, 23);
            btnDRemove.TabIndex = 28;
            btnDRemove.Text = "Remove";
            btnDRemove.UseVisualStyleBackColor = true;
            btnDRemove.Click += btnDRemove_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(883, 680);
            Controls.Add(dtaListPairs);
            Controls.Add(dtaListBoxTitle);
            Controls.Add(lblIntro);
            Controls.Add(addDTAs);
            Controls.Add(lblRunStatus);
            Controls.Add(panel2);
            Controls.Add(testAndRunBox);
            Font = new System.Drawing.Font("Cascadia Code", 9F);
            Name = "Form1";
            Text = "Dicom Dose Comparison";
            Load += Form1_Load;
            testAndRunBox.ResumeLayout(false);
            testAndRunBox.PerformLayout();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            addDTAs.ResumeLayout(false);
            addDTAs.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
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
        private System.Windows.Forms.GroupBox testAndRunBox;
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
        private System.Windows.Forms.CheckBox chkBoxGamma;
        private System.Windows.Forms.TextBox dtaListBoxTitle;
        private System.Windows.Forms.Button btnDRemove;
    }
}

