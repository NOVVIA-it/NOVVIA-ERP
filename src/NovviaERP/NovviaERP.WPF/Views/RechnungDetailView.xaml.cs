using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class RechnungDetailView : UserControl
    {
        private readonly CoreService _core;
        private Rechnung? _rechnung;
        private readonly int _rechnungId;

        public RechnungDetailView(int rechnungId)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _rechnungId = rechnungId;
            Loaded += async (s, e) => await LadeRechnungAsync();
        }

        private async System.Threading.Tasks.Task LadeRechnungAsync()
        {
            try
            {
                _rechnung = await _core.GetRechnungMitPositionenAsync(_rechnungId);
                if (_rechnung == null)
                {
                    MessageBox.Show("Rechnung nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Header
                txtHeader.Text = $"Rechnung {_rechnung.RechnungsNr}";
                SetStatus(_rechnung);

                // Rechnungsdaten
                txtRechnungsNr.Text = _rechnung.RechnungsNr;
                txtDatum.Text = _rechnung.Erstellt.ToString("dd.MM.yyyy");
                txtFaellig.Text = _rechnung.Faellig?.ToString("dd.MM.yyyy") ?? "-";
                txtBestellNr.Text = _rechnung.BestellungId?.ToString() ?? "-";

                // Kunde
                if (_rechnung.Kunde != null)
                {
                    txtKundeFirma.Text = _rechnung.Kunde.Firma ?? $"{_rechnung.Kunde.Vorname} {_rechnung.Kunde.Nachname}".Trim();
                    txtKundeAdresse.Text = _rechnung.Kunde.Strasse ?? "";
                    txtKundePLZOrt.Text = $"{_rechnung.Kunde.PLZ} {_rechnung.Kunde.Ort}".Trim();
                    txtKundenNr.Text = _rechnung.Kunde.KundenNr ?? _rechnung.KundeId.ToString();
                }

                // Betraege
                txtNetto.Text = $"{_rechnung.Netto:N2} EUR";
                txtMwSt.Text = $"{_rechnung.MwSt:N2} EUR";
                txtBrutto.Text = $"{_rechnung.Brutto:N2} EUR";
                txtOffen.Text = $"{_rechnung.Offen:N2} EUR";

                // Positionen
                var positionen = (_rechnung.Positionen ?? new List<RechnungsPosition>())
                    .Select((p, idx) => new RechnungsPositionViewModel
                    {
                        Pos = idx + 1,
                        ArtNr = p.ArtNr,
                        Name = p.Name,
                        Menge = p.Menge,
                        PreisNetto = p.PreisNetto,
                        Rabatt = p.Rabatt,
                        MwStSatz = p.MwStSatz,
                        Gesamt = p.Menge * p.PreisNetto * (1 - p.Rabatt / 100)
                    }).ToList();
                dgPositionen.ItemsSource = positionen;

                // Zahlungen
                dgZahlungen.ItemsSource = _rechnung.Zahlungen ?? new List<Zahlungseingang>();

                // Button deaktivieren wenn bezahlt
                btnZahlung.IsEnabled = _rechnung.Offen > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetStatus(Rechnung r)
        {
            string statusText;
            Color bgColor;

            if (r.IstStorniert || r.Status == 5)
            {
                statusText = "Storniert";
                bgColor = Colors.Gray;
            }
            else if (r.Bezahlt.HasValue || r.Status == 3)
            {
                statusText = "Bezahlt";
                bgColor = Colors.Green;
            }
            else if (r.Faellig.HasValue && r.Faellig.Value < DateTime.Today)
            {
                statusText = "Ueberfaellig";
                bgColor = Colors.Red;
            }
            else
            {
                statusText = "Offen";
                bgColor = Colors.Orange;
            }

            txtStatus.Text = statusText;
            brdStatus.Background = new SolidColorBrush(bgColor);
            txtStatus.Foreground = Brushes.White;
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(App.Services.GetRequiredService<RechnungenView>());
            }
        }

        private async void PDF_Click(object sender, RoutedEventArgs e)
        {
            if (_rechnung == null) return;

            try
            {
                var firma = new Firma { Name = "NOVVIA GmbH" };
                var svc = new ReportService(firma);
                var pdf = svc.GenerateRechnungPdf(_rechnung, _rechnung.Kunde!, _rechnung.Positionen ?? new List<RechnungsPosition>());
                var filename = $"Rechnung_{_rechnung.RechnungsNr}.pdf";
                System.IO.File.WriteAllBytes(filename, pdf);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filename) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Drucken_Click(object sender, RoutedEventArgs e)
        {
            if (_rechnung == null) return;
            Helpers.AusgabeHelper.AusgabeRechnung(_rechnung.Id, _rechnung.RechnungsNr, Window.GetWindow(this));
        }

        private void Email_Click(object sender, RoutedEventArgs e)
        {
            if (_rechnung == null) return;
            Helpers.AusgabeHelper.AusgabeRechnung(_rechnung.Id, _rechnung.RechnungsNr, Window.GetWindow(this));
        }

        private async void ZahlungErfassen_Click(object sender, RoutedEventArgs e)
        {
            if (_rechnung == null || _rechnung.Offen <= 0) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Zahlungsbetrag eingeben:\n\nOffener Betrag: {_rechnung.Offen:N2} EUR",
                "Zahlung erfassen",
                _rechnung.Offen.ToString("N2"));

            if (string.IsNullOrWhiteSpace(input)) return;

            if (!decimal.TryParse(input.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var betrag) || betrag <= 0)
            {
                MessageBox.Show("Bitte einen gueltigen Betrag eingeben.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _core.ErfasseZahlungAsync(_rechnung.Id, betrag);
                MessageBox.Show($"Zahlung von {betrag:N2} EUR erfasst.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeRechnungAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class RechnungsPositionViewModel
    {
        public int Pos { get; set; }
        public string? ArtNr { get; set; }
        public string? Name { get; set; }
        public decimal Menge { get; set; }
        public decimal PreisNetto { get; set; }
        public decimal Rabatt { get; set; }
        public decimal MwStSatz { get; set; }
        public decimal Gesamt { get; set; }
    }
}
