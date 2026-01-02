using System.Windows;
using System.Text.RegularExpressions;

namespace NovviaERP.WPF.Views
{
    public partial class BankverbindungDialog : Window
    {
        public BankverbindungDto Bankverbindung { get; private set; }
        public bool IstGespeichert { get; private set; }

        public BankverbindungDialog(BankverbindungDto? bankverbindung = null, string titel = "Bankverbindung bearbeiten")
        {
            InitializeComponent();
            txtHeader.Text = titel;
            Bankverbindung = bankverbindung ?? new BankverbindungDto();
            LadeDaten();
        }

        private void LadeDaten()
        {
            chkStandard.IsChecked = Bankverbindung.NStandard == 1;
            txtBankName.Text = Bankverbindung.BankName ?? "";
            txtInhaber.Text = Bankverbindung.Inhaber ?? "";
            txtIBAN.Text = Bankverbindung.IBAN ?? "";
            txtBIC.Text = Bankverbindung.BIC ?? "";
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            var iban = txtIBAN.Text.Trim().Replace(" ", "").ToUpper();
            if (string.IsNullOrWhiteSpace(iban))
            {
                MessageBox.Show("Bitte eine IBAN eingeben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Einfache IBAN-Validierung (DE: 22 Zeichen)
            if (iban.StartsWith("DE") && iban.Length != 22)
            {
                MessageBox.Show("Deutsche IBAN muss 22 Zeichen haben.", "Validierung",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Daten uebernehmen
            Bankverbindung.NStandard = chkStandard.IsChecked == true ? 1 : 0;
            Bankverbindung.BankName = txtBankName.Text.Trim();
            Bankverbindung.Inhaber = txtInhaber.Text.Trim();
            Bankverbindung.IBAN = iban;
            Bankverbindung.BIC = txtBIC.Text.Trim().ToUpper();

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
    /// DTO fuer Bankverbindung
    /// </summary>
    public class BankverbindungDto
    {
        public int? KKontoDaten { get; set; }
        public int? KKunde { get; set; }
        public int? KLieferant { get; set; }
        public int NStandard { get; set; } = 0;
        public string? BankName { get; set; }
        public string? Inhaber { get; set; }
        public string? IBAN { get; set; }
        public string? BIC { get; set; }
        public string? BLZ { get; set; }
        public string? KontoNr { get; set; }

        public string IBANFormatiert
        {
            get
            {
                if (string.IsNullOrEmpty(IBAN)) return "";
                // IBAN in 4er-Gruppen formatieren
                var clean = IBAN.Replace(" ", "");
                var result = "";
                for (int i = 0; i < clean.Length; i += 4)
                {
                    if (result.Length > 0) result += " ";
                    result += clean.Substring(i, Math.Min(4, clean.Length - i));
                }
                return result;
            }
        }
    }
}
