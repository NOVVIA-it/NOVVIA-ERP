using System.Windows;
using System.Windows.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class AdresseBearbeitenDialog : Window
    {
        public AdresseDto Adresse { get; private set; }
        public bool IstGespeichert { get; private set; }

        public AdresseBearbeitenDialog(AdresseDto? adresse = null, string titel = "Adresse bearbeiten")
        {
            InitializeComponent();
            txtHeader.Text = titel;
            Adresse = adresse ?? new AdresseDto();
            LadeAdresse();
        }

        private void LadeAdresse()
        {
            // Adresstyp
            foreach (ComboBoxItem item in cmbTyp.Items)
            {
                if (item.Tag?.ToString() == Adresse.NTyp.ToString())
                {
                    cmbTyp.SelectedItem = item;
                    break;
                }
            }
            if (cmbTyp.SelectedItem == null) cmbTyp.SelectedIndex = 0;

            chkStandard.IsChecked = Adresse.NStandard == 1;

            txtFirma.Text = Adresse.Firma ?? "";
            txtTitel.Text = Adresse.Titel ?? "";
            txtVorname.Text = Adresse.Vorname ?? "";
            txtNachname.Text = Adresse.Nachname ?? "";
            txtStrasse.Text = Adresse.Strasse ?? "";
            txtAdresszusatz.Text = Adresse.Adresszusatz ?? "";
            txtPLZ.Text = Adresse.PLZ ?? "";
            txtOrt.Text = Adresse.Ort ?? "";
            txtTelefon.Text = Adresse.Telefon ?? "";
            txtMobil.Text = Adresse.Mobil ?? "";
            txtFax.Text = Adresse.Fax ?? "";
            txtEmail.Text = Adresse.Email ?? "";

            // Anrede
            foreach (ComboBoxItem item in cmbAnrede.Items)
            {
                if (item.Content?.ToString() == Adresse.Anrede)
                {
                    cmbAnrede.SelectedItem = item;
                    break;
                }
            }

            // Land
            cmbLand.Text = string.IsNullOrEmpty(Adresse.Land) ? "Deutschland" : Adresse.Land;
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            if (string.IsNullOrWhiteSpace(txtNachname.Text) && string.IsNullOrWhiteSpace(txtFirma.Text))
            {
                MessageBox.Show("Bitte mindestens Firma oder Nachname eingeben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Daten uebernehmen
            Adresse.NTyp = int.TryParse((cmbTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var typ) ? typ : 1;
            Adresse.NStandard = chkStandard.IsChecked == true ? 1 : 0;
            Adresse.Firma = txtFirma.Text.Trim();
            Adresse.Titel = txtTitel.Text.Trim();
            Adresse.Anrede = (cmbAnrede.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            Adresse.Vorname = txtVorname.Text.Trim();
            Adresse.Nachname = txtNachname.Text.Trim();
            Adresse.Strasse = txtStrasse.Text.Trim();
            Adresse.Adresszusatz = txtAdresszusatz.Text.Trim();
            Adresse.PLZ = txtPLZ.Text.Trim();
            Adresse.Ort = txtOrt.Text.Trim();
            Adresse.Land = cmbLand.Text.Trim();
            Adresse.Telefon = txtTelefon.Text.Trim();
            Adresse.Mobil = txtMobil.Text.Trim();
            Adresse.Fax = txtFax.Text.Trim();
            Adresse.Email = txtEmail.Text.Trim();

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
    /// DTO fuer Adressdaten
    /// </summary>
    public class AdresseDto
    {
        public int? KAdresse { get; set; }
        public int? KKunde { get; set; }
        public int NTyp { get; set; } = 1; // 1=Rechnung, 2=Lieferung
        public int NStandard { get; set; } = 0;
        public string? Firma { get; set; }
        public string? Anrede { get; set; }
        public string? Titel { get; set; }
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Strasse { get; set; }
        public string? Adresszusatz { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string? Land { get; set; }
        public string? Telefon { get; set; }
        public string? Mobil { get; set; }
        public string? Fax { get; set; }
        public string? Email { get; set; }

        public string TypText => NTyp == 2 ? "Lieferadresse" : "Rechnungsadresse";

        public string Formatiert => FormatAdresse();

        private string FormatAdresse()
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(Firma)) lines.Add(Firma);
            var name = $"{Vorname} {Nachname}".Trim();
            if (!string.IsNullOrWhiteSpace(name) && name != Firma) lines.Add(name);
            if (!string.IsNullOrWhiteSpace(Strasse)) lines.Add(Strasse);
            if (!string.IsNullOrWhiteSpace(PLZ) || !string.IsNullOrWhiteSpace(Ort))
                lines.Add($"{PLZ} {Ort}".Trim());
            if (!string.IsNullOrWhiteSpace(Land) && Land != "Deutschland" && Land != "DE")
                lines.Add(Land);
            return lines.Count > 0 ? string.Join("\n", lines) : "-";
        }
    }
}
