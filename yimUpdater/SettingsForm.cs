using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace yimUpdater
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void locateDllButton_Click(object sender, EventArgs e)
        {

        }

        private void testButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 101; i++)
            {
                testProgressBar.Value = i;
                for (int j = 0; j < 11;  j++)
                {
                    if (i%10  == j)
                    {
                        MessageBox.Show(j.ToString());
                    }
                }
            }
        }
    }
}
