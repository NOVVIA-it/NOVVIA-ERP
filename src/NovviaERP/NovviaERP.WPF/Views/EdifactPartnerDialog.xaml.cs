using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EdifactPartnerDialog : Window
    {
        private readonly EdifactService _edifact;
        private readonly int? _partnerId;

        public EdifactPartnerDialog(EdifactPartner? partner = null)
        {
            InitializeComponent();
            _edifact = App.Services.GetRequiredService<EdifactService>();
            _partnerId = partner?.KPartner;

            if (partner != null)
            {
                Title = "EDIFACT Partner bearbeiten";
                LadePartner(partner);
            }
            else
            {
                Title = "Neuer EDIFACT Partner";
            }
        }

        private void LadePartner(EdifactPartner p)
        {
            txtName.Text = p.CName;
            txtPartnerGLN.Text = p.CPartnerGLN;
            txtEigeneGLN.Text = p.CEigeneGLN;
            txtEigeneFirma.Text = p.CEigeneFirma;
            txtEigeneStrasse.Text = p.CEigeneStrasse;
            txtEigenePLZ.Text = p.CEigenePLZ;
            txtEigeneOrt.Text = p.CEigeneOrt;

            // Protokoll
            foreach (System.Windows.Controls.ComboBoxItem item in cmbProtokoll.Items)
            {
                if (item.Content.ToString() == p.CProtokoll)
                {
                    cmbProtokoll.SelectedItem = item;
                    break;
                }
            }

            txtHost.Text = p.CHost;
            txtPort.Text = p.NPort.ToString();
            txtBenutzer.Text = p.CBenutzer;
            // Passwort nicht anzeigen
            txtVerzeichnisIn.Text = p.CVerzeichnisIn;
            txtVerzeichnisOut.Text = p.CVerzeichnisOut;
            chkAktiv.IsChecked = p.NAktiv;
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Bitte geben Sie einen Namen ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPartnerGLN.Text))
            {
                MessageBox.Show("Bitte geben Sie die Partner-GLN ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPartnerGLN.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtEigeneGLN.Text))
            {
                MessageBox.Show("Bitte geben Sie die eigene GLN ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtEigeneGLN.Focus();
                return;
            }

            try
            {
                var partner = new EdifactPartner
                {
                    KPartner = _partnerId ?? 0,
                    CName = txtName.Text.Trim(),
                    CPartnerGLN = txtPartnerGLN.Text.Trim(),
                    CEigeneGLN = txtEigeneGLN.Text.Trim(),
                    CEigeneFirma = txtEigeneFirma.Text.Trim(),
                    CEigeneStrasse = txtEigeneStrasse.Text.Trim(),
                    CEigenePLZ = txtEigenePLZ.Text.Trim(),
                    CEigeneOrt = txtEigeneOrt.Text.Trim(),
                    CProtokoll = (cmbProtokoll.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "SFTP",
                    CHost = txtHost.Text.Trim(),
                    NPort = int.TryParse(txtPort.Text, out var port) ? port : 22,
                    CBenutzer = txtBenutzer.Text.Trim(),
                    CPasswort = txtPasswort.Password,
                    CVerzeichnisIn = txtVerzeichnisIn.Text.Trim(),
                    CVerzeichnisOut = txtVerzeichnisOut.Text.Trim(),
                    NAktiv = chkAktiv.IsChecked ?? true
                };

                await _edifact.SavePartnerAsync(partner);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
