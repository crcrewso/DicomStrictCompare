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
            this.label1 = new System.Windows.Forms.Label();
            this.chkPDDCompare = new System.Windows.Forms.CheckBox();
            this.chkDoseCompare = new System.Windows.Forms.CheckBox();
            this.doseProgressBar = new System.Windows.Forms.ProgressBar();
            this.pddProgressBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // lblIntro
            // 
            this.lblIntro.AutoSize = true;
            this.lblIntro.Location = new System.Drawing.Point(12, 9);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(389, 156);
            this.lblIntro.TabIndex = 0;
            this.lblIntro.Text = resources.GetString("lblIntro.Text");
            // 
            // tbxSource
            // 
            this.tbxSource.Location = new System.Drawing.Point(93, 171);
            this.tbxSource.Name = "tbxSource";
            this.tbxSource.Size = new System.Drawing.Size(341, 20);
            this.tbxSource.TabIndex = 0;
            this.tbxSource.TextChanged += new System.EventHandler(this.tbxSource_TextChanged);
            // 
            // tbxTarget
            // 
            this.tbxTarget.Location = new System.Drawing.Point(93, 202);
            this.tbxTarget.Name = "tbxTarget";
            this.tbxTarget.Size = new System.Drawing.Size(341, 20);
            this.tbxTarget.TabIndex = 1;
            this.tbxTarget.TextChanged += new System.EventHandler(this.tbxTarget_TextChanged);
            // 
            // lblTightTol
            // 
            this.lblTightTol.AutoSize = true;
            this.lblTightTol.Location = new System.Drawing.Point(7, 231);
            this.lblTightTol.Name = "lblTightTol";
            this.lblTightTol.Size = new System.Drawing.Size(99, 13);
            this.lblTightTol.TabIndex = 5;
            this.lblTightTol.Text = "Tight Tolerance (%)";
            // 
            // lblMainTol
            // 
            this.lblMainTol.AutoSize = true;
            this.lblMainTol.Location = new System.Drawing.Point(171, 231);
            this.lblMainTol.Name = "lblMainTol";
            this.lblMainTol.Size = new System.Drawing.Size(98, 13);
            this.lblMainTol.TabIndex = 6;
            this.lblMainTol.Text = "Main Tolerance (%)";
            // 
            // tbxTightTol
            // 
            this.tbxTightTol.Location = new System.Drawing.Point(112, 228);
            this.tbxTightTol.Name = "tbxTightTol";
            this.tbxTightTol.Size = new System.Drawing.Size(53, 20);
            this.tbxTightTol.TabIndex = 2;
            this.tbxTightTol.TextChanged += new System.EventHandler(this.tbxTightTol_TextChanged);
            // 
            // tbxMainTol
            // 
            this.tbxMainTol.Location = new System.Drawing.Point(275, 228);
            this.tbxMainTol.Name = "tbxMainTol";
            this.tbxMainTol.Size = new System.Drawing.Size(53, 20);
            this.tbxMainTol.TabIndex = 3;
            this.tbxMainTol.TextChanged += new System.EventHandler(this.tbxMainTol_TextChanged);
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(359, 320);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(75, 23);
            this.btnExecute.TabIndex = 7;
            this.btnExecute.Text = "Run";
            this.btnExecute.UseVisualStyleBackColor = true;
            this.btnExecute.Click += new System.EventHandler(this.btnExecute_Click);
            // 
            // btnSourceDir
            // 
            this.btnSourceDir.Location = new System.Drawing.Point(12, 169);
            this.btnSourceDir.Name = "btnSourceDir";
            this.btnSourceDir.Size = new System.Drawing.Size(75, 23);
            this.btnSourceDir.TabIndex = 10;
            this.btnSourceDir.Text = "Source";
            this.btnSourceDir.UseVisualStyleBackColor = true;
            this.btnSourceDir.Click += new System.EventHandler(this.btnSourceDir_Click);
            // 
            // btnTargetDir
            // 
            this.btnTargetDir.Location = new System.Drawing.Point(12, 199);
            this.btnTargetDir.Name = "btnTargetDir";
            this.btnTargetDir.Size = new System.Drawing.Size(75, 23);
            this.btnTargetDir.TabIndex = 11;
            this.btnTargetDir.Text = "Target";
            this.btnTargetDir.UseVisualStyleBackColor = true;
            this.btnTargetDir.Click += new System.EventHandler(this.btnTargetDir_Click);
            // 
            // lblSourceFilesFoundText
            // 
            this.lblSourceFilesFoundText.AutoSize = true;
            this.lblSourceFilesFoundText.Location = new System.Drawing.Point(454, 174);
            this.lblSourceFilesFoundText.Name = "lblSourceFilesFoundText";
            this.lblSourceFilesFoundText.Size = new System.Drawing.Size(89, 13);
            this.lblSourceFilesFoundText.TabIndex = 12;
            this.lblSourceFilesFoundText.Text = "Dose Files Found";
            // 
            // lblTargetFilesFoundText
            // 
            this.lblTargetFilesFoundText.AutoSize = true;
            this.lblTargetFilesFoundText.Location = new System.Drawing.Point(454, 204);
            this.lblTargetFilesFoundText.Name = "lblTargetFilesFoundText";
            this.lblTargetFilesFoundText.Size = new System.Drawing.Size(86, 13);
            this.lblTargetFilesFoundText.TabIndex = 13;
            this.lblTargetFilesFoundText.Text = "Dose Files found";
            // 
            // lblSourceFilesFound
            // 
            this.lblSourceFilesFound.AutoSize = true;
            this.lblSourceFilesFound.Location = new System.Drawing.Point(546, 174);
            this.lblSourceFilesFound.Name = "lblSourceFilesFound";
            this.lblSourceFilesFound.Size = new System.Drawing.Size(14, 13);
            this.lblSourceFilesFound.TabIndex = 14;
            this.lblSourceFilesFound.Text = "#";
            // 
            // lblTargetFilesFound
            // 
            this.lblTargetFilesFound.AutoSize = true;
            this.lblTargetFilesFound.Location = new System.Drawing.Point(546, 204);
            this.lblTargetFilesFound.Name = "lblTargetFilesFound";
            this.lblTargetFilesFound.Size = new System.Drawing.Size(14, 13);
            this.lblTargetFilesFound.TabIndex = 15;
            this.lblTargetFilesFound.Text = "#";
            // 
            // tbxThreshholdTol
            // 
            this.tbxThreshholdTol.Location = new System.Drawing.Point(417, 228);
            this.tbxThreshholdTol.Name = "tbxThreshholdTol";
            this.tbxThreshholdTol.Size = new System.Drawing.Size(53, 20);
            this.tbxThreshholdTol.TabIndex = 4;
            this.tbxThreshholdTol.TextChanged += new System.EventHandler(this.threshBox_TextChanged);
            // 
            // lblthreshhold
            // 
            this.lblthreshhold.AutoSize = true;
            this.lblthreshhold.Location = new System.Drawing.Point(334, 231);
            this.lblthreshhold.Name = "lblthreshhold";
            this.lblthreshhold.Size = new System.Drawing.Size(77, 13);
            this.lblthreshhold.TabIndex = 16;
            this.lblthreshhold.Text = "Threshhold (%)";
            // 
            // btnSaveDir
            // 
            this.btnSaveDir.Location = new System.Drawing.Point(12, 282);
            this.btnSaveDir.Name = "btnSaveDir";
            this.btnSaveDir.Size = new System.Drawing.Size(75, 23);
            this.btnSaveDir.TabIndex = 18;
            this.btnSaveDir.Text = "Save Location";
            this.btnSaveDir.UseVisualStyleBackColor = true;
            this.btnSaveDir.Click += new System.EventHandler(this.BtnSaveDir_Click);
            // 
            // tbxSaveDir
            // 
            this.tbxSaveDir.Location = new System.Drawing.Point(93, 284);
            this.tbxSaveDir.Name = "tbxSaveDir";
            this.tbxSaveDir.Size = new System.Drawing.Size(341, 20);
            this.tbxSaveDir.TabIndex = 6;
            this.tbxSaveDir.TextChanged += new System.EventHandler(this.TbxSaveDir_TextChanged);
            // 
            // tbxSaveName
            // 
            this.tbxSaveName.Location = new System.Drawing.Point(93, 258);
            this.tbxSaveName.Name = "tbxSaveName";
            this.tbxSaveName.Size = new System.Drawing.Size(341, 20);
            this.tbxSaveName.TabIndex = 5;
            this.tbxSaveName.TextChanged += new System.EventHandler(this.TbxSaveName_TextChanged);
            // 
            // lblSaveName
            // 
            this.lblSaveName.AutoSize = true;
            this.lblSaveName.Location = new System.Drawing.Point(454, 261);
            this.lblSaveName.Name = "lblSaveName";
            this.lblSaveName.Size = new System.Drawing.Size(169, 13);
            this.lblSaveName.TabIndex = 21;
            this.lblSaveName.Text = "Save Name of Excel Summary File";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(454, 287);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(139, 13);
            this.label1.TabIndex = 22;
            this.label1.Text = "Directory to Save Results to";
            // 
            // chkPDDCompare
            // 
            this.chkPDDCompare.AutoSize = true;
            this.chkPDDCompare.Location = new System.Drawing.Point(97, 324);
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
            this.chkDoseCompare.Location = new System.Drawing.Point(94, 347);
            this.chkDoseCompare.Name = "chkDoseCompare";
            this.chkDoseCompare.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.chkDoseCompare.Size = new System.Drawing.Size(178, 17);
            this.chkDoseCompare.TabIndex = 24;
            this.chkDoseCompare.Text = "Produce Dose Comparison table";
            this.chkDoseCompare.UseVisualStyleBackColor = true;
            // 
            // doseProgressBar
            // 
            this.doseProgressBar.Location = new System.Drawing.Point(10, 384);
            this.doseProgressBar.Name = "doseProgressBar";
            this.doseProgressBar.Size = new System.Drawing.Size(613, 23);
            this.doseProgressBar.TabIndex = 25;
            // 
            // pddProgressBar
            // 
            this.pddProgressBar.Location = new System.Drawing.Point(10, 413);
            this.pddProgressBar.Name = "pddProgressBar";
            this.pddProgressBar.Size = new System.Drawing.Size(613, 23);
            this.pddProgressBar.TabIndex = 26;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(642, 514);
            this.Controls.Add(this.pddProgressBar);
            this.Controls.Add(this.doseProgressBar);
            this.Controls.Add(this.chkDoseCompare);
            this.Controls.Add(this.chkPDDCompare);
            this.Controls.Add(this.label1);
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
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox chkPDDCompare;
        private System.Windows.Forms.CheckBox chkDoseCompare;
        private System.Windows.Forms.ProgressBar doseProgressBar;
        private System.Windows.Forms.ProgressBar pddProgressBar;
    }
}

