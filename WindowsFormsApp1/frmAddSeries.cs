using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace GodexIndustrial
{
    public partial class frmAddSeries : Form
    {
        public string Prefix { get; private set; }
        public int StartValue { get; private set; }
        public int EndValue { get; private set; }
        public int StepValue { get; private set; }

        public frmAddSeries()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Prefix = tbPrefix.Text;
            StartValue = (int)numStart.Value;
            EndValue = (int)numEnd.Value;
            StepValue = (int)numStep.Value;

            if (StepValue <= 0)
            {
                MessageBox.Show("Step must be greater than zero.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (StartValue > EndValue)
            {
                MessageBox.Show("Start value cannot be greater than end value.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        public List<string> GenerateSeries()
        {
            List<string> series = new List<string>();
            for (int i = StartValue; i <= EndValue; i += StepValue)
            {
                series.Add($"{Prefix}{i}");
            }
            return series;
        }
    }
}
