using System.Windows;
using System.Windows.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class AnsprechpartnerDialog : Window
    {
        public AnsprechpartnerDto Ansprechpartner { get; private set; }
        public bool IstGespeichert { get; private set; }

        public AnsprechpartnerDialog(AnsprechpartnerDto? ansprechpartner = null, string titel = "Ansprechpartner bearbeiten")
        {
            InitializeComponent();
            txtHeader.Text = titel;
            Ansprechpartner = ansprechpartner ?? new AnsprechpartnerDto();
            LadeDaten();
        }

        private void LadeDaten()
        {
            // Anrede
            foreach (ComboBoxItem item in cmbAnrede.Items)
            {
                if (item.Content?.ToString() == Ansprechpartner.Anrede)
                {
                    cmbAnrede.SelectedItem = item;
                    break;
                }
            }

            txtVorname.Text = Ansprechpartner.Vorname ?? "";
            txtNachname.Text = Ansprechpartner.Nachname ?? "";
            txtAbteilung.Text = Ansprechpartner.Abteilung ?? "";
            txtTelefon.Text = Ansprechpartner.Telefon ?? "";
            txtMobil.Text = Ansprechpartner.Mobil ?? "";
            txtFax.Text = Ansprechpartner.Fax ?? "";
            txtEmail.Text = Ansprechpartner.Email ?? "";
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            if (string.IsNullOrWhiteSpace(txtNachname.Text))
            {
                MessageBox.Show("Bitte einen Nachnamen eingeben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Daten uebernehmen
            Ansprechpartner.Anrede = (cmbAnrede.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            Ansprechpartner.Vorname = txtVorname.Text.Trim();
            Ansprechpartner.Nachname = txtNachname.Text.Trim();
            Ansprechpartner.Abteilung = txtAbteilung.Text.Trim();
            Ansprechpartner.Telefon = txtTelefon.Text.Trim();
            Ansprechpartner.Mobil = txtMobil.Text.Trim();
            Ansprechpartner.Fax = txtFax.Text.Trim();
            Ansprechpartner.Email = txtEmail.Text.Trim();

            IstGespeichert = true;
            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            IstGespeichert = false;
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// DTO fuer Ansprechpartner
    /// </summary>
    public class AnsprechpartnerDto
    {
        public int? KAnsprechpartner { get; set; }
        public int? KKunde { get; set; }
        public int? KLieferant { get; set; }
        public string? Anrede { get; set; }
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Abteilung { get; set; }
        public string? Telefon { get; set; }
        public string? Mobil { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }

        public string VollstaendigerName => $"{Vorname} {Nachname}".Trim();
    }
}
