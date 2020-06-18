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
            this.testAndRunBox = new System.Windows.Forms.GroupBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.addDTAs = new System.Windows.Forms.GroupBox();
            this.chkBoxGamma = new System.Windows.Forms.CheckBox();
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
            this.dtaListView = new System.Windows.Forms.ListView();
            this.tol = new System.Windows.Forms.ColumnHeader();
            this.dtaVal = new System.Windows.Forms.ColumnHeader();
            this.thresh = new System.Windows.Forms.ColumnHeader();
            this.trim = new System.Windows.Forms.ColumnHeader();
            this.rel = new System.Windows.Forms.ColumnHeader();
            this.gamma = new System.Windows.Forms.ColumnHeader();
            this.testAndRunBox.SuspendLayout();
            this.panel2.SuspendLayout();
            this.addDTAs.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblIntro
            // 
            this.lblIntro.AutoSize = true;
            this.lblIntro.Location = new System.Drawing.Point(11, 800);
            this.lblIntro.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(708, 475);
            this.lblIntro.TabIndex = 0;
            this.lblIntro.Text = resources.GetString("lblIntro.Text");
            this.lblIntro.Click += new System.EventHandler(this.lblIntro_Click);
            // 
            // tbxSource
            // 
            this.tbxSource.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbxSource.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbxSource.Location = new System.Drawing.Point(130, 48);
            this.tbxSource.Margin = new System.Windows.Forms.Padding(4);
            this.tbxSource.MinimumSize = new System.Drawing.Size(469, 20);
            this.tbxSource.Name = "tbxSource";
            this.tbxSource.Size = new System.Drawing.Size(672, 31);
            this.tbxSource.TabIndex = 4;
            this.tbxSource.TextChanged += new System.EventHandler(this.TbxSource_TextChanged);
            // 
            // tbxTarget
            // 
            this.tbxTarget.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbxTarget.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbxTarget.Location = new System.Drawing.Point(130, 128);
            this.tbxTarget.Margin = new System.Windows.Forms.Padding(4);
            this.tbxTarget.MinimumSize = new System.Drawing.Size(469, 20);
            this.tbxTarget.Name = "tbxTarget";
            this.tbxTarget.Size = new System.Drawing.Size(672, 31);
            this.tbxTarget.TabIndex = 6;
            this.tbxTarget.TextChanged += new System.EventHandler(this.TbxTarget_TextChanged);
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(244, 75);
            this.btnExecute.Margin = new System.Windows.Forms.Padding(4);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(112, 34);
            this.btnExecute.TabIndex = 10;
            this.btnExecute.Text = "Run";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.BtnExecute_Click);
            // 
            // btnSourceDir
            // 
            this.btnSourceDir.Location = new System.Drawing.Point(14, 45);
            this.btnSourceDir.Margin = new System.Windows.Forms.Padding(4);
            this.btnSourceDir.Name = "btnSourceDir";
            this.btnSourceDir.Size = new System.Drawing.Size(112, 34);
            this.btnSourceDir.TabIndex = 10;
            this.btnSourceDir.Text = "Source";
            this.btnSourceDir.UseVisualStyleBackColor = true;
            this.btnSourceDir.Click += new System.EventHandler(this.BtnSourceDir_Click);
            // 
            // btnTargetDir
            // 
            this.btnTargetDir.Location = new System.Drawing.Point(9, 124);
            this.btnTargetDir.Margin = new System.Windows.Forms.Padding(4);
            this.btnTargetDir.Name = "btnTargetDir";
            this.btnTargetDir.Size = new System.Drawing.Size(112, 34);
            this.btnTargetDir.TabIndex = 11;
            this.btnTargetDir.Text = "Target";
            this.btnTargetDir.UseVisualStyleBackColor = true;
            this.btnTargetDir.Click += new System.EventHandler(this.BtnTargetDir_Click);
            // 
            // lblSourceFilesFoundText
            // 
            this.lblSourceFilesFoundText.AutoSize = true;
            this.lblSourceFilesFoundText.Location = new System.Drawing.Point(15, 36);
            this.lblSourceFilesFoundText.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSourceFilesFoundText.Name = "lblSourceFilesFoundText";
            this.lblSourceFilesFoundText.Size = new System.Drawing.Size(207, 25);
            this.lblSourceFilesFoundText.TabIndex = 12;
            this.lblSourceFilesFoundText.Text = "Source Dose Files Found";
            // 
            // lblTargetFilesFoundText
            // 
            this.lblTargetFilesFoundText.AutoSize = true;
            this.lblTargetFilesFoundText.Location = new System.Drawing.Point(15, 80);
            this.lblTargetFilesFoundText.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTargetFilesFoundText.Name = "lblTargetFilesFoundText";
            this.lblTargetFilesFoundText.Size = new System.Drawing.Size(201, 25);
            this.lblTargetFilesFoundText.TabIndex = 13;
            this.lblTargetFilesFoundText.Text = "Target Dose Files Found";
            // 
            // lblSourceFilesFound
            // 
            this.lblSourceFilesFound.AutoSize = true;
            this.lblSourceFilesFound.Location = new System.Drawing.Point(213, 36);
            this.lblSourceFilesFound.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSourceFilesFound.Name = "lblSourceFilesFound";
            this.lblSourceFilesFound.Size = new System.Drawing.Size(23, 25);
            this.lblSourceFilesFound.TabIndex = 14;
            this.lblSourceFilesFound.Text = "#";
            // 
            // lblTargetFilesFound
            // 
            this.lblTargetFilesFound.AutoSize = true;
            this.lblTargetFilesFound.Location = new System.Drawing.Point(213, 80);
            this.lblTargetFilesFound.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTargetFilesFound.Name = "lblTargetFilesFound";
            this.lblTargetFilesFound.Size = new System.Drawing.Size(23, 25);
            this.lblTargetFilesFound.TabIndex = 15;
            this.lblTargetFilesFound.Text = "#";
            // 
            // btnSaveDir
            // 
            this.btnSaveDir.Location = new System.Drawing.Point(6, 202);
            this.btnSaveDir.Margin = new System.Windows.Forms.Padding(4);
            this.btnSaveDir.Name = "btnSaveDir";
            this.btnSaveDir.Size = new System.Drawing.Size(112, 34);
            this.btnSaveDir.TabIndex = 18;
            this.btnSaveDir.Text = "Save Location";
            this.btnSaveDir.UseVisualStyleBackColor = true;
            this.btnSaveDir.Click += new System.EventHandler(this.BtnSaveDir_Click);
            // 
            // tbxSaveDir
            // 
            this.tbxSaveDir.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.tbxSaveDir.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystemDirectories;
            this.tbxSaveDir.Location = new System.Drawing.Point(130, 206);
            this.tbxSaveDir.Margin = new System.Windows.Forms.Padding(4);
            this.tbxSaveDir.MinimumSize = new System.Drawing.Size(469, 20);
            this.tbxSaveDir.Name = "tbxSaveDir";
            this.tbxSaveDir.Size = new System.Drawing.Size(672, 31);
            this.tbxSaveDir.TabIndex = 8;
            this.tbxSaveDir.TextChanged += new System.EventHandler(this.TbxSaveDir_TextChanged);
            // 
            // tbxSaveName
            // 
            this.tbxSaveName.Location = new System.Drawing.Point(130, 166);
            this.tbxSaveName.Margin = new System.Windows.Forms.Padding(4);
            this.tbxSaveName.MinimumSize = new System.Drawing.Size(469, 20);
            this.tbxSaveName.Name = "tbxSaveName";
            this.tbxSaveName.Size = new System.Drawing.Size(672, 31);
            this.tbxSaveName.TabIndex = 7;
            this.tbxSaveName.TextChanged += new System.EventHandler(this.TbxSaveName_TextChanged);
            // 
            // lblSaveName
            // 
            this.lblSaveName.AutoSize = true;
            this.lblSaveName.Location = new System.Drawing.Point(24, 171);
            this.lblSaveName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSaveName.Name = "lblSaveName";
            this.lblSaveName.Size = new System.Drawing.Size(101, 25);
            this.lblSaveName.TabIndex = 21;
            this.lblSaveName.Text = "Save Name";
            // 
            // chkPDDCompare
            // 
            this.chkPDDCompare.AutoSize = true;
            this.chkPDDCompare.Location = new System.Drawing.Point(69, 118);
            this.chkPDDCompare.Margin = new System.Windows.Forms.Padding(4);
            this.chkPDDCompare.Name = "chkPDDCompare";
            this.chkPDDCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkPDDCompare.Size = new System.Drawing.Size(287, 29);
            this.chkPDDCompare.TabIndex = 23;
            this.chkPDDCompare.Text = "Produce PDD comparison Plots";
            this.chkPDDCompare.UseVisualStyleBackColor = true;
            // 
            // chkDoseCompare
            // 
            this.chkDoseCompare.AutoSize = true;
            this.chkDoseCompare.Location = new System.Drawing.Point(61, 155);
            this.chkDoseCompare.Margin = new System.Windows.Forms.Padding(4);
            this.chkDoseCompare.Name = "chkDoseCompare";
            this.chkDoseCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkDoseCompare.Size = new System.Drawing.Size(295, 29);
            this.chkDoseCompare.TabIndex = 24;
            this.chkDoseCompare.Text = "Produce Dose Comparison table";
            this.chkDoseCompare.UseVisualStyleBackColor = true;
            // 
            // doseProgressBar
            // 
            this.doseProgressBar.Location = new System.Drawing.Point(4, 254);
            this.doseProgressBar.Margin = new System.Windows.Forms.Padding(4);
            this.doseProgressBar.MinimumSize = new System.Drawing.Size(600, 38);
            this.doseProgressBar.Name = "doseProgressBar";
            this.doseProgressBar.Size = new System.Drawing.Size(867, 38);
            this.doseProgressBar.TabIndex = 25;
            // 
            // testDirectories
            // 
            this.testDirectories.Location = new System.Drawing.Point(244, 31);
            this.testDirectories.Margin = new System.Windows.Forms.Padding(4);
            this.testDirectories.Name = "testDirectories";
            this.testDirectories.Size = new System.Drawing.Size(112, 34);
            this.testDirectories.TabIndex = 9;
            this.testDirectories.Text = "Test Directories";
            this.testDirectories.UseVisualStyleBackColor = true;
            this.testDirectories.Click += new System.EventHandler(this.TestDirectories_Click);
            // 
            // tbxTargetLabel
            // 
            this.tbxTargetLabel.Location = new System.Drawing.Point(130, 87);
            this.tbxTargetLabel.Margin = new System.Windows.Forms.Padding(4);
            this.tbxTargetLabel.MinimumSize = new System.Drawing.Size(469, 20);
            this.tbxTargetLabel.Name = "tbxTargetLabel";
            this.tbxTargetLabel.Size = new System.Drawing.Size(672, 31);
            this.tbxTargetLabel.TabIndex = 5;
            this.tbxTargetLabel.TextChanged += new System.EventHandler(this.TbxTargetLabel_TextChanged);
            // 
            // tbxSourceLabel
            // 
            this.tbxSourceLabel.Location = new System.Drawing.Point(130, 9);
            this.tbxSourceLabel.Margin = new System.Windows.Forms.Padding(4);
            this.tbxSourceLabel.MinimumSize = new System.Drawing.Size(469, 20);
            this.tbxSourceLabel.Name = "tbxSourceLabel";
            this.tbxSourceLabel.Size = new System.Drawing.Size(672, 31);
            this.tbxSourceLabel.TabIndex = 3;
            this.tbxSourceLabel.TextChanged += new System.EventHandler(this.TbxSourceLabel_TextChanged);
            // 
            // lblTargetRefName
            // 
            this.lblTargetRefName.AutoSize = true;
            this.lblTargetRefName.Location = new System.Drawing.Point(21, 92);
            this.lblTargetRefName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTargetRefName.Name = "lblTargetRefName";
            this.lblTargetRefName.Size = new System.Drawing.Size(106, 25);
            this.lblTargetRefName.TabIndex = 31;
            this.lblTargetRefName.Text = "Target Label";
            // 
            // lblSourceRefName
            // 
            this.lblSourceRefName.AutoSize = true;
            this.lblSourceRefName.Location = new System.Drawing.Point(16, 14);
            this.lblSourceRefName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblSourceRefName.Name = "lblSourceRefName";
            this.lblSourceRefName.Size = new System.Drawing.Size(112, 25);
            this.lblSourceRefName.TabIndex = 32;
            this.lblSourceRefName.Text = "Source Label";
            // 
            // lblRunStatus
            // 
            this.lblRunStatus.AutoSize = true;
            this.lblRunStatus.Location = new System.Drawing.Point(905, 230);
            this.lblRunStatus.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblRunStatus.Name = "lblRunStatus";
            this.lblRunStatus.Size = new System.Drawing.Size(60, 25);
            this.lblRunStatus.TabIndex = 33;
            this.lblRunStatus.Text = "Status";
            // 
            // testAndRunBox
            // 
            this.testAndRunBox.AutoSize = true;
            this.testAndRunBox.Controls.Add(this.testDirectories);
            this.testAndRunBox.Controls.Add(this.chkDoseCompare);
            this.testAndRunBox.Controls.Add(this.chkPDDCompare);
            this.testAndRunBox.Controls.Add(this.lblTargetFilesFound);
            this.testAndRunBox.Controls.Add(this.lblSourceFilesFound);
            this.testAndRunBox.Controls.Add(this.lblTargetFilesFoundText);
            this.testAndRunBox.Controls.Add(this.lblSourceFilesFoundText);
            this.testAndRunBox.Controls.Add(this.btnExecute);
            this.testAndRunBox.Location = new System.Drawing.Point(890, 13);
            this.testAndRunBox.Margin = new System.Windows.Forms.Padding(4);
            this.testAndRunBox.Name = "testAndRunBox";
            this.testAndRunBox.Padding = new System.Windows.Forms.Padding(4);
            this.testAndRunBox.Size = new System.Drawing.Size(366, 216);
            this.testAndRunBox.TabIndex = 35;
            this.testAndRunBox.TabStop = false;
            this.testAndRunBox.Text = "Control";
            // 
            // panel2
            // 
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
            this.panel2.Location = new System.Drawing.Point(5, 18);
            this.panel2.Margin = new System.Windows.Forms.Padding(4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(877, 296);
            this.panel2.TabIndex = 38;
            // 
            // addDTAs
            // 
            this.addDTAs.Controls.Add(this.chkBoxGamma);
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
            this.addDTAs.Location = new System.Drawing.Point(1016, 309);
            this.addDTAs.Margin = new System.Windows.Forms.Padding(4);
            this.addDTAs.Name = "addDTAs";
            this.addDTAs.Padding = new System.Windows.Forms.Padding(4);
            this.addDTAs.Size = new System.Drawing.Size(230, 329);
            this.addDTAs.TabIndex = 40;
            this.addDTAs.TabStop = false;
            this.addDTAs.Text = "Add DTA\'s";
            // 
            // chkBoxGamma
            // 
            this.chkBoxGamma.AutoSize = true;
            this.chkBoxGamma.Location = new System.Drawing.Point(8, 239);
            this.chkBoxGamma.Margin = new System.Windows.Forms.Padding(4);
            this.chkBoxGamma.Name = "chkBoxGamma";
            this.chkBoxGamma.Size = new System.Drawing.Size(108, 29);
            this.chkBoxGamma.TabIndex = 23;
            this.chkBoxGamma.Text = "Gamma?";
            this.chkBoxGamma.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkBoxGamma.UseVisualStyleBackColor = true;
            // 
            // txtBxTrim
            // 
            this.txtBxTrim.Location = new System.Drawing.Point(133, 163);
            this.txtBxTrim.Margin = new System.Windows.Forms.Padding(4);
            this.txtBxTrim.Name = "txtBxTrim";
            this.txtBxTrim.Size = new System.Drawing.Size(80, 31);
            this.txtBxTrim.TabIndex = 26;
            // 
            // lblTrim
            // 
            this.lblTrim.AutoSize = true;
            this.lblTrim.Location = new System.Drawing.Point(9, 168);
            this.lblTrim.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblTrim.Name = "lblTrim";
            this.lblTrim.Size = new System.Drawing.Size(99, 25);
            this.lblTrim.TabIndex = 27;
            this.lblTrim.Text = "Trim voxels";
            // 
            // units
            // 
            this.units.FormattingEnabled = true;
            this.units.ItemHeight = 25;
            this.units.Items.AddRange(new object[] {
            "mm",
            "Voxels"});
            this.units.Location = new System.Drawing.Point(134, 58);
            this.units.Margin = new System.Windows.Forms.Padding(4);
            this.units.Name = "units";
            this.units.Size = new System.Drawing.Size(80, 54);
            this.units.TabIndex = 25;
            // 
            // btnDAadd
            // 
            this.btnDAadd.Location = new System.Drawing.Point(62, 283);
            this.btnDAadd.Margin = new System.Windows.Forms.Padding(4);
            this.btnDAadd.Name = "btnDAadd";
            this.btnDAadd.Size = new System.Drawing.Size(112, 34);
            this.btnDAadd.TabIndex = 24;
            this.btnDAadd.Text = "Add";
            this.btnDAadd.UseVisualStyleBackColor = true;
            this.btnDAadd.Click += new System.EventHandler(this.BtnDAadd_Click);
            // 
            // chkBoxDArel
            // 
            this.chkBoxDArel.AutoSize = true;
            this.chkBoxDArel.Location = new System.Drawing.Point(9, 202);
            this.chkBoxDArel.Margin = new System.Windows.Forms.Padding(4);
            this.chkBoxDArel.Name = "chkBoxDArel";
            this.chkBoxDArel.Size = new System.Drawing.Size(212, 29);
            this.chkBoxDArel.TabIndex = 23;
            this.chkBoxDArel.Text = "Relative to Max Dose?";
            this.chkBoxDArel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.chkBoxDArel.UseVisualStyleBackColor = true;
            // 
            // txtBoxDAdta
            // 
            this.txtBoxDAdta.Location = new System.Drawing.Point(62, 68);
            this.txtBoxDAdta.Margin = new System.Windows.Forms.Padding(4);
            this.txtBoxDAdta.Name = "txtBoxDAdta";
            this.txtBoxDAdta.Size = new System.Drawing.Size(61, 31);
            this.txtBoxDAdta.TabIndex = 20;
            // 
            // lblDAdta
            // 
            this.lblDAdta.AutoSize = true;
            this.lblDAdta.Location = new System.Drawing.Point(9, 72);
            this.lblDAdta.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDAdta.Name = "lblDAdta";
            this.lblDAdta.Size = new System.Drawing.Size(44, 25);
            this.lblDAdta.TabIndex = 21;
            this.lblDAdta.Text = "DTA";
            // 
            // txtBoxDAthres
            // 
            this.txtBoxDAthres.Location = new System.Drawing.Point(133, 121);
            this.txtBoxDAthres.Margin = new System.Windows.Forms.Padding(4);
            this.txtBoxDAthres.Name = "txtBoxDAthres";
            this.txtBoxDAthres.Size = new System.Drawing.Size(80, 31);
            this.txtBoxDAthres.TabIndex = 17;
            // 
            // lblDAthresh
            // 
            this.lblDAthresh.AutoSize = true;
            this.lblDAthresh.Location = new System.Drawing.Point(8, 126);
            this.lblDAthresh.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDAthresh.Name = "lblDAthresh";
            this.lblDAthresh.Size = new System.Drawing.Size(130, 25);
            this.lblDAthresh.TabIndex = 18;
            this.lblDAthresh.Text = "Threshhold (%)";
            // 
            // txtBoxDAtol
            // 
            this.txtBoxDAtol.Location = new System.Drawing.Point(134, 20);
            this.txtBoxDAtol.Margin = new System.Windows.Forms.Padding(4);
            this.txtBoxDAtol.Name = "txtBoxDAtol";
            this.txtBoxDAtol.Size = new System.Drawing.Size(80, 31);
            this.txtBoxDAtol.TabIndex = 6;
            // 
            // lblDAtol
            // 
            this.lblDAtol.AutoSize = true;
            this.lblDAtol.Location = new System.Drawing.Point(9, 24);
            this.lblDAtol.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDAtol.Name = "lblDAtol";
            this.lblDAtol.Size = new System.Drawing.Size(115, 25);
            this.lblDAtol.TabIndex = 7;
            this.lblDAtol.Text = "Tolerance (%)";
            // 
            // dtaListView
            // 
            this.dtaListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.tol,
            this.dtaVal,
            this.thresh,
            this.trim,
            this.rel,
            this.gamma});
            this.dtaListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.dtaListView.HideSelection = false;
            this.dtaListView.Location = new System.Drawing.Point(384, 321);
            this.dtaListView.Name = "dtaListView";
            this.dtaListView.Size = new System.Drawing.Size(625, 476);
            this.dtaListView.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.dtaListView.TabIndex = 41;
            this.dtaListView.UseCompatibleStateImageBehavior = false;
            this.dtaListView.View = System.Windows.Forms.View.Details;
            // 
            // tol
            // 
            this.tol.Name = "tol";
            this.tol.Text = "Tolerance (%)";
            this.tol.Width = 120;
            // 
            // dtaVal
            // 
            this.dtaVal.Name = "dtaVal";
            this.dtaVal.Text = "DTA";
            this.dtaVal.Width = 70;
            // 
            // thresh
            // 
            this.thresh.Name = "thresh";
            this.thresh.Text = "Threshhold (%)";
            this.thresh.Width = 130;
            // 
            // trim
            // 
            this.trim.Name = "trim";
            this.trim.Text = "Trim (voxels)";
            this.trim.Width = 110;
            // 
            // rel
            // 
            this.rel.Name = "rel";
            this.rel.Text = "Relative";
            this.rel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.rel.Width = 80;
            // 
            // gamma
            // 
            this.gamma.Name = "gamma";
            this.gamma.Text = "Gamma";
            this.gamma.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.gamma.Width = 80;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(144F, 144F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(1264, 1284);
            this.Controls.Add(this.dtaListView);
            this.Controls.Add(this.lblIntro);
            this.Controls.Add(this.addDTAs);
            this.Controls.Add(this.lblRunStatus);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.testAndRunBox);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(940, 700);
            this.Name = "Form1";
            this.Text = "Dicom Dose Comparison";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.testAndRunBox.ResumeLayout(false);
            this.testAndRunBox.PerformLayout();
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
        private System.Windows.Forms.GroupBox testAndRunBox;
        private System.Windows.Forms.Panel panel2;
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
        private System.Windows.Forms.ListView dtaListView;
        private System.Windows.Forms.ColumnHeader tol;
        private System.Windows.Forms.ColumnHeader dtaVal;
        private System.Windows.Forms.ColumnHeader thresh;
        private System.Windows.Forms.ColumnHeader trim;
        private System.Windows.Forms.ColumnHeader rel;
        private System.Windows.Forms.ColumnHeader gamma;
    }
}

