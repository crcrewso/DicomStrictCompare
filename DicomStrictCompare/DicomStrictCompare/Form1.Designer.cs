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
            this.SuspendLayout();
            // 
            // lblIntro
            // 
            this.lblIntro.AutoSize = true;
            this.lblIntro.Location = new System.Drawing.Point(0, 0);
            this.lblIntro.Name = "lblIntro";
            this.lblIntro.Size = new System.Drawing.Size(389, 156);
            this.lblIntro.TabIndex = 0;
            this.lblIntro.Text = resources.GetString("lblIntro.Text");
            // 
            // tbxSource
            // 
            this.tbxSource.Location = new System.Drawing.Point(84, 180);
            this.tbxSource.Name = "tbxSource";
            this.tbxSource.Size = new System.Drawing.Size(341, 20);
            this.tbxSource.TabIndex = 3;
            this.tbxSource.TextChanged += new System.EventHandler(this.tbxSource_TextChanged);
            // 
            // tbxTarget
            // 
            this.tbxTarget.Location = new System.Drawing.Point(84, 211);
            this.tbxTarget.Name = "tbxTarget";
            this.tbxTarget.Size = new System.Drawing.Size(341, 20);
            this.tbxTarget.TabIndex = 4;
            this.tbxTarget.TextChanged += new System.EventHandler(this.tbxTarget_TextChanged);
            // 
            // lblTightTol
            // 
            this.lblTightTol.AutoSize = true;
            this.lblTightTol.Location = new System.Drawing.Point(0, 317);
            this.lblTightTol.Name = "lblTightTol";
            this.lblTightTol.Size = new System.Drawing.Size(99, 13);
            this.lblTightTol.TabIndex = 5;
            this.lblTightTol.Text = "Tight Tolerance (%)";
            // 
            // lblMainTol
            // 
            this.lblMainTol.AutoSize = true;
            this.lblMainTol.Location = new System.Drawing.Point(232, 317);
            this.lblMainTol.Name = "lblMainTol";
            this.lblMainTol.Size = new System.Drawing.Size(98, 13);
            this.lblMainTol.TabIndex = 6;
            this.lblMainTol.Text = "Main Tolerance (%)";
            // 
            // tbxTightTol
            // 
            this.tbxTightTol.Location = new System.Drawing.Point(105, 314);
            this.tbxTightTol.Name = "tbxTightTol";
            this.tbxTightTol.Size = new System.Drawing.Size(53, 20);
            this.tbxTightTol.TabIndex = 7;
            this.tbxTightTol.TextChanged += new System.EventHandler(this.tbxTightTol_TextChanged);
            // 
            // tbxMainTol
            // 
            this.tbxMainTol.Location = new System.Drawing.Point(336, 314);
            this.tbxMainTol.Name = "tbxMainTol";
            this.tbxMainTol.Size = new System.Drawing.Size(53, 20);
            this.tbxMainTol.TabIndex = 8;
            this.tbxMainTol.TextChanged += new System.EventHandler(this.tbxMainTol_TextChanged);
            // 
            // btnExecute
            // 
            this.btnExecute.Location = new System.Drawing.Point(3, 407);
            this.btnExecute.Name = "btnExecute";
            this.btnExecute.Size = new System.Drawing.Size(75, 23);
            this.btnExecute.TabIndex = 9;
            this.btnExecute.Text = "Run";
            this.btnExecute.UseVisualStyleBackColor = true;
            // 
            // btnSourceDir
            // 
            this.btnSourceDir.Location = new System.Drawing.Point(3, 178);
            this.btnSourceDir.Name = "btnSourceDir";
            this.btnSourceDir.Size = new System.Drawing.Size(75, 23);
            this.btnSourceDir.TabIndex = 10;
            this.btnSourceDir.Text = "Source";
            this.btnSourceDir.UseVisualStyleBackColor = true;
            this.btnSourceDir.Click += new System.EventHandler(this.btnSourceDir_Click);
            // 
            // btnTargetDir
            // 
            this.btnTargetDir.Location = new System.Drawing.Point(3, 208);
            this.btnTargetDir.Name = "btnTargetDir";
            this.btnTargetDir.Size = new System.Drawing.Size(75, 23);
            this.btnTargetDir.TabIndex = 11;
            this.btnTargetDir.Text = "Target";
            this.btnTargetDir.UseVisualStyleBackColor = true;
            this.btnTargetDir.Click += new System.EventHandler(this.btnTargetDir_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
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
            this.Text = "Form1";
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
    }
}

