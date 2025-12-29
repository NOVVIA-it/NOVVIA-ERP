using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace NovviaERP.WPF.Views
{
    public partial class RechnungenPage : Page
    {
        private readonly CoreService _core;

        public RechnungenPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeRechnungenAsync();
        }

        private async System.Threading.Tasks.Task LadeRechnungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Rechnungen...";
                var rechnungen = await App.Db.GetOffeneRechnungenAsync();
                dgRechnungen.ItemsSource = rechnungen;
                txtStatus.Text = $"{rechnungen.Count()} Rechnungen";
            }
            catch (System.Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            var suche = txtSuche.Text?.Trim() ?? "";
            var rechnungen = await App.Db.GetOffeneRechnungenAsync();

            if (!string.IsNullOrEmpty(suche))
            {
                rechnungen = rechnungen.Where(r =>
                    (r.RechnungsNr?.Contains(suche, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.Kunde?.Firma?.Contains(suche, System.StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (status != "Alle" && !string.IsNullOrEmpty(status))
            {
                int statusCode = status switch
                {
                    "Offen" => 0,
                    "Bezahlt" => 3,
                    "Storniert" => 5,
                    _ => -1
                };
                if (statusCode >= 0)
                    rechnungen = rechnungen.Where(r => r.Status == statusCode);
            }

            dgRechnungen.ItemsSource = rechnungen.ToList();
            txtStatus.Text = $"{rechnungen.Count()} Rechnungen gefunden";
        }

        private void DgRechnungen_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgRechnungen.SelectedItem is Rechnung r)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    main.ShowContent(new RechnungDetailView(r.Id));
                }
            }
        }

        private async void PDF_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is not Rechnung r)
            {
                MessageBox.Show("Bitte eine Rechnung auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var kunde = await App.Db.GetKundeByIdAsync(r.KundeId, false);
                var firma = new Firma { Name = "NOVVIA GmbH" };
                var svc = new ReportService(firma);
                var pdf = svc.GenerateRechnungPdf(r, kunde!, r.Positionen ?? new System.Collections.Generic.List<RechnungsPosition>());
                var filename = $"Rechnung_{r.RechnungsNr}.pdf";
                System.IO.File.WriteAllBytes(filename, pdf);
                MessageBox.Show($"PDF erstellt: {filename}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen des PDFs:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Drucken_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is not Rechnung r) return;
            Helpers.AusgabeHelper.AusgabeRechnung(r.Id, r.RechnungsNr, Window.GetWindow(this));
        }

        private async void Stornieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is not Rechnung r)
            {
                MessageBox.Show("Bitte eine Rechnung auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (r.Status == 5 || r.IstStorniert)
            {
                MessageBox.Show("Diese Rechnung ist bereits storniert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Rechnung {r.RechnungsNr} wirklich stornieren?\n\n" +
                "Der Auftrag wird wieder bearbeitbar (Preis, Anschrift).\n" +
                "Lagerbewegungen bleiben bestehen - nur über Retoure änderbar.\n\n" +
                "Für eine Gutschrift nutzen Sie 'Rechnungskorrektur'.",
                "Rechnung stornieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                txtStatus.Text = "Storniere Rechnung...";
                await _core.StornoRechnungAsync(r.Id);
                txtStatus.Text = "";

                MessageBox.Show(
                    $"Rechnung {r.RechnungsNr} wurde storniert.\n\n" +
                    "Der Auftrag ist jetzt wieder bearbeitbar.",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LadeRechnungenAsync();
            }
            catch (System.Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show($"Fehler beim Stornieren:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Rechnungskorrektur_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is not Rechnung r)
            {
                MessageBox.Show("Bitte eine Rechnung auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Betrag für Gutschrift abfragen
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Gutschrift-Betrag eingeben (Brutto):\n\nRechnungsbetrag: {r.Brutto:N2} EUR",
                "Gutschrift erstellen",
                r.Brutto.ToString("N2"));

            if (string.IsNullOrWhiteSpace(input)) return;

            if (!decimal.TryParse(input.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var betrag) || betrag <= 0)
            {
                MessageBox.Show("Bitte einen gültigen Betrag eingeben.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var grund = Microsoft.VisualBasic.Interaction.InputBox(
                "Grund für die Gutschrift:",
                "Gutschrift - Grund",
                "Kulanzgutschrift");

            if (string.IsNullOrWhiteSpace(grund)) return;

            try
            {
                txtStatus.Text = "Erstelle Gutschrift...";
                var kGutschrift = await _core.CreateRechnungskorrekturAsync(r.Id, betrag, grund);
                txtStatus.Text = "";

                MessageBox.Show(
                    $"Gutschrift wurde erstellt.\n\nGutschrift-Nr: GS-{kGutschrift}",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LadeRechnungenAsync();
            }
            catch (System.Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ZahlungErfassen_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is not Rechnung r)
            {
                MessageBox.Show("Bitte eine Rechnung auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (r.Offen <= 0)
            {
                MessageBox.Show("Diese Rechnung ist bereits vollständig bezahlt.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Zahlungsbetrag eingeben:\n\nOffener Betrag: {r.Offen:N2} EUR",
                "Zahlung erfassen",
                r.Offen.ToString("N2"));

            if (string.IsNullOrWhiteSpace(input)) return;

            if (!decimal.TryParse(input.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var betrag) || betrag <= 0)
            {
                MessageBox.Show("Bitte einen gültigen Betrag eingeben.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtStatus.Text = "Erfasse Zahlung...";
                await _core.ErfasseZahlungAsync(r.Id, betrag);
                txtStatus.Text = "";

                MessageBox.Show(
                    $"Zahlung von {betrag:N2} EUR erfasst.",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LadeRechnungenAsync();
            }
            catch (System.Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
