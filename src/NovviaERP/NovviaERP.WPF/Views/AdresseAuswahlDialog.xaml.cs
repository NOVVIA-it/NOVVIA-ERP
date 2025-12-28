using System.Windows;
using System.Windows.Input;

namespace NovviaERP.WPF.Views
{
    public partial class AdresseAuswahlDialog : Window
    {
        public AdresseDto? AusgewaehlteAdresse { get; private set; }
        public bool IstAusgewaehlt { get; private set; }
        private readonly List<AdresseListItem> _adressen = new();

        public AdresseAuswahlDialog(IEnumerable<AdresseDto> adressen, string titel = "Adresse auswaehlen")
        {
            InitializeComponent();
            txtHeader.Text = titel;

            // Adressen in ListItems umwandeln
            foreach (var adr in adressen)
            {
                _adressen.Add(new AdresseListItem
                {
                    Adresse = adr,
                    Initiale = GetInitiale(adr),
                    Titel = string.IsNullOrWhiteSpace(adr.Firma)
                        ? $"{adr.Vorname} {adr.Nachname}".Trim()
                        : adr.Firma,
                    Zeile1 = adr.Strasse ?? "",
                    Zeile2 = $"{adr.PLZ} {adr.Ort}".Trim(),
                    Typ = adr.KAdresse.HasValue ? $"ID: {adr.KAdresse}" : "Neu"
                });
            }

            lstAdressen.ItemsSource = _adressen;

            if (_adressen.Count > 0)
                lstAdressen.SelectedIndex = 0;
        }

        private static string GetInitiale(AdresseDto adr)
        {
            if (!string.IsNullOrWhiteSpace(adr.Firma))
                return adr.Firma[..1].ToUpper();
            if (!string.IsNullOrWhiteSpace(adr.Nachname))
                return adr.Nachname[..1].ToUpper();
            if (!string.IsNullOrWhiteSpace(adr.Vorname))
                return adr.Vorname[..1].ToUpper();
            return "?";
        }

        private void Uebernehmen_Click(object sender, RoutedEventArgs e)
        {
            if (lstAdressen.SelectedItem is AdresseListItem item)
            {
                AusgewaehlteAdresse = item.Adresse;
                IstAusgewaehlt = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Bitte eine Adresse auswaehlen.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LstAdressen_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstAdressen.SelectedItem != null)
                Uebernehmen_Click(sender, new RoutedEventArgs());
        }

        private void NeueAdresse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AdresseBearbeitenDialog(null, "Neue Adresse erfassen");
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                AusgewaehlteAdresse = dialog.Adresse;
                IstAusgewaehlt = true;
                DialogResult = true;
                Close();
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            IstAusgewaehlt = false;
            DialogResult = false;
            Close();
        }
    }

    internal class AdresseListItem
    {
        public AdresseDto? Adresse { get; set; }
        public string Initiale { get; set; } = "";
        public string Titel { get; set; } = "";
        public string Zeile1 { get; set; } = "";
        public string Zeile2 { get; set; } = "";
        public string Typ { get; set; } = "";
    }
}
