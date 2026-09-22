namespace GodexIndustrial
{
    partial class frmAddSeries
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblPrefix = new System.Windows.Forms.Label();
            this.tbPrefix = new System.Windows.Forms.TextBox();
            this.lblStart = new System.Windows.Forms.Label();
            this.numStart = new System.Windows.Forms.NumericUpDown();
            this.lblEnd = new System.Windows.Forms.Label();
            this.numEnd = new System.Windows.Forms.NumericUpDown();
            this.lblStep = new System.Windows.Forms.Label();
            this.numStep = new System.Windows.Forms.NumericUpDown();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numStart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEnd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStep)).BeginInit();
            this.panelHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblPrefix
            // 
            this.lblPrefix.AutoSize = true;
            this.lblPrefix.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblPrefix.ForeColor = System.Drawing.Color.Gainsboro;
            this.lblPrefix.Location = new System.Drawing.Point(20, 70);
            this.lblPrefix.Name = "lblPrefix";
            this.lblPrefix.Size = new System.Drawing.Size(48, 20);
            this.lblPrefix.Text = "Prefix:";
            // 
            // tbPrefix
            // 
            this.tbPrefix.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(36)))), ((int)(((byte)(81)))));
            this.tbPrefix.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbPrefix.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.tbPrefix.ForeColor = System.Drawing.Color.Gainsboro;
            this.tbPrefix.Location = new System.Drawing.Point(80, 68);
            this.tbPrefix.Name = "tbPrefix";
            this.tbPrefix.Size = new System.Drawing.Size(180, 27);
            // 
            // lblStart
            // 
            this.lblStart.AutoSize = true;
            this.lblStart.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblStart.ForeColor = System.Drawing.Color.Gainsboro;
            this.lblStart.Location = new System.Drawing.Point(20, 110);
            this.lblStart.Name = "lblStart";
            this.lblStart.Size = new System.Drawing.Size(43, 20);
            this.lblStart.Text = "Start:";
            // 
            // numStart
            // 
            this.numStart.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(36)))), ((int)(((byte)(81)))));
            this.numStart.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.numStart.ForeColor = System.Drawing.Color.Gainsboro;
            this.numStart.Location = new System.Drawing.Point(80, 108);
            this.numStart.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numStart.Name = "numStart";
            this.numStart.Size = new System.Drawing.Size(180, 27);
            // 
            // lblEnd
            // 
            this.lblEnd.AutoSize = true;
            this.lblEnd.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblEnd.ForeColor = System.Drawing.Color.Gainsboro;
            this.lblEnd.Location = new System.Drawing.Point(20, 150);
            this.lblEnd.Name = "lblEnd";
            this.lblEnd.Size = new System.Drawing.Size(37, 20);
            this.lblEnd.Text = "End:";
            // 
            // numEnd
            // 
            this.numEnd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(36)))), ((int)(((byte)(81)))));
            this.numEnd.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.numEnd.ForeColor = System.Drawing.Color.Gainsboro;
            this.numEnd.Location = new System.Drawing.Point(80, 148);
            this.numEnd.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            this.numEnd.Name = "numEnd";
            this.numEnd.Size = new System.Drawing.Size(180, 27);
            this.numEnd.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // lblStep
            // 
            this.lblStep.AutoSize = true;
            this.lblStep.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.lblStep.ForeColor = System.Drawing.Color.Gainsboro;
            this.lblStep.Location = new System.Drawing.Point(20, 190);
            this.lblStep.Name = "lblStep";
            this.lblStep.Size = new System.Drawing.Size(42, 20);
            this.lblStep.Text = "Step:";
            // 
            // numStep
            // 
            this.numStep.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(36)))), ((int)(((byte)(81)))));
            this.numStep.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.numStep.ForeColor = System.Drawing.Color.Gainsboro;
            this.numStep.Location = new System.Drawing.Point(80, 188);
            this.numStep.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numStep.Name = "numStep";
            this.numStep.Size = new System.Drawing.Size(180, 27);
            this.numStep.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // btnOK
            // 
            this.btnOK.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.btnOK.FlatAppearance.BorderSize = 0;
            this.btnOK.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOK.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.Location = new System.Drawing.Point(24, 240);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(110, 35);
            this.btnOK.Text = "Generate";
            this.btnOK.UseVisualStyleBackColor = false;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(30)))), ((int)(((byte)(68)))));
            this.btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.DimGray;
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.btnCancel.ForeColor = System.Drawing.Color.Gainsboro;
            this.btnCancel.Location = new System.Drawing.Point(150, 240);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(110, 35);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(26)))), ((int)(((byte)(25)))), ((int)(((byte)(62)))));
            this.panelHeader.Controls.Add(this.lblTitle);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(284, 45);
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.Gainsboro;
            this.lblTitle.Location = new System.Drawing.Point(12, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(155, 25);
            this.lblTitle.Text = "Generate Series";
            // 
            // frmAddSeries
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(33)))), ((int)(((byte)(74)))));
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 301);
            this.Controls.Add(this.panelHeader);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.numStep);
            this.Controls.Add(this.lblStep);
            this.Controls.Add(this.numEnd);
            this.Controls.Add(this.lblEnd);
            this.Controls.Add(this.numStart);
            this.Controls.Add(this.lblStart);
            this.Controls.Add(this.tbPrefix);
            this.Controls.Add(this.lblPrefix);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmAddSeries";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Generate Series";
            ((System.ComponentModel.ISupportInitialize)(this.numStart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEnd)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStep)).EndInit();
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblPrefix;
        private System.Windows.Forms.TextBox tbPrefix;
        private System.Windows.Forms.Label lblStart;
        private System.Windows.Forms.NumericUpDown numStart;
        private System.Windows.Forms.Label lblEnd;
        private System.Windows.Forms.NumericUpDown numEnd;
        private System.Windows.Forms.Label lblStep;
        private System.Windows.Forms.NumericUpDown numStep;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label lblTitle;
    }
}
