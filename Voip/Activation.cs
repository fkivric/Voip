using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;

namespace Voip
{
    public partial class Activation : DevExpress.XtraEditors.XtraForm
    {

        public string m_licUserId;
        public string m_licKey;

        public Activation()
        {
            InitializeComponent();
        }

        private void Activation_Load(object sender, EventArgs e)
        {

        }
    }
}