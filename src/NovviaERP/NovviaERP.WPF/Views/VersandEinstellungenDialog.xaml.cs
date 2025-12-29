using System.Windows;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class VersandEinstellungenDialog : Window
    {
        public ShippingConfig? Config { get; private set; }

        public VersandEinstellungenDialog(ShippingConfig config)
        {
            InitializeComponent();

            // Werte laden
            txtDHLUser.Text = config.DHLUser;
            txtDHLPassword.Password = config.DHLPassword;
            txtDHLProfile.Text = config.DHLProfile;
            txtDHLBillingNumber.Text = config.DHLBillingNumber;

            txtDPDUser.Text = config.DPDUser;
            txtDPDPassword.Password = config.DPDPassword;
            txtDPDDepot.Text = config.DPDDepot;

            txtGLSUser.Text = config.GLSUser;
            txtGLSPassword.Password = config.GLSPassword;
            txtGLSShipperId.Text = config.GLSShipperId;

            txtUPSToken.Password = config.UPSToken;
            txtUPSAccountNumber.Text = config.UPSAccountNumber;

            Config = config;
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            Config = new ShippingConfig
            {
                DHLUser = txtDHLUser.Text,
                DHLPassword = txtDHLPassword.Password,
                DHLProfile = txtDHLProfile.Text,
                DHLBillingNumber = txtDHLBillingNumber.Text,

                DPDUser = txtDPDUser.Text,
                DPDPassword = txtDPDPassword.Password,
                DPDDepot = txtDPDDepot.Text,

                GLSUser = txtGLSUser.Text,
                GLSPassword = txtGLSPassword.Password,
                GLSShipperId = txtGLSShipperId.Text,

                UPSToken = txtUPSToken.Password,
                UPSAccountNumber = txtUPSAccountNumber.Text
            };

            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
