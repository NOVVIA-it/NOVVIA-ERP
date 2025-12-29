using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class VersandPage : Page
    {
        private readonly CoreService _core;
        private List<CoreService.VersandItem> _versandListe = new();

        public VersandPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeVersandAsync();
        }

        private async System.Threading.Tasks.Task LadeVersandAsync()
        {
            try
            {
                txtStatus.Text = "Lade Auftraege...";

                var suche = txtSuche.Text?.Trim() ?? "";
                var statusFilter = (cmbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var von = dpVon.SelectedDate;
                var bis = dpBis.SelectedDate?.AddDays(1);

                var items = await _core.GetVersandListeAsync(suche, statusFilter, von, bis);
                _versandListe = items.ToList();
                dgVersand.ItemsSource = _versandListe;

                var zuVersenden = _versandListe.Count(x => string.IsNullOrEmpty(x.TrackingNr));
                var versendet = _versandListe.Count(x => !string.IsNullOrEmpty(x.TrackingNr));
                txtStatus.Text = $"{_versandListe.Count} Auftraege ({zuVersenden} zu versenden, {versendet} versendet)";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = LadeVersandAsync();
        }

        private void Suchen_Click(object sender, RoutedEventArgs e) => _ = LadeVersandAsync();
        private void Aktualisieren_Click(object sender, RoutedEventArgs e) => _ = LadeVersandAsync();

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) _ = LadeVersandAsync();
        }

        private void DgVersand_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgVersand.SelectedItem is CoreService.VersandItem item)
            {
                // Navigation zur Bestellungsdetail
                NavigationService?.Navigate(new BestellungDetailPage(item.KBestellung));
            }
        }

        private async void VersendeDHL_Click(object sender, RoutedEventArgs e) => await VersendeAusgewaehlteAsync("DHL");
        private async void VersendeDPD_Click(object sender, RoutedEventArgs e) => await VersendeAusgewaehlteAsync("DPD");
        private async void VersendeGLS_Click(object sender, RoutedEventArgs e) => await VersendeAusgewaehlteAsync("GLS");
        private async void VersendeUPS_Click(object sender, RoutedEventArgs e) => await VersendeAusgewaehlteAsync("UPS");

        private async System.Threading.Tasks.Task VersendeAusgewaehlteAsync(string carrier)
        {
            var ausgewaehlt = dgVersand.SelectedItems.Cast<CoreService.VersandItem>()
                .Where(x => string.IsNullOrEmpty(x.TrackingNr))
                .ToList();

            if (!ausgewaehlt.Any())
            {
                MessageBox.Show("Bitte mindestens einen noch nicht versendeten Auftrag auswaehlen.",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"{ausgewaehlt.Count} Auftrag/Auftraege mit {carrier} versenden?",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var erfolg = 0;
            var fehler = 0;
            var cfg = new ShippingConfig { AbsenderName = "NOVVIA GmbH" };
            var svc = new ShippingService(cfg);

            foreach (var item in ausgewaehlt)
            {
                try
                {
                    txtStatus.Text = $"Versende {item.BestellNr}...";

                    var best = await App.Db.GetBestellungByIdAsync(item.KBestellung);
                    if (best?.Lieferadresse == null) continue;

                    var req = new ShipmentRequest
                    {
                        Referenz = best.BestellNr ?? "",
                        EmpfaengerName = $"{best.Lieferadresse.Vorname} {best.Lieferadresse.Nachname}".Trim(),
                        EmpfaengerStrasse = best.Lieferadresse.Strasse ?? "",
                        EmpfaengerPLZ = best.Lieferadresse.PLZ ?? "",
                        EmpfaengerOrt = best.Lieferadresse.Ort ?? "",
                        EmpfaengerLand = best.Lieferadresse.Land ?? "DE",
                        GewichtKg = item.Gewicht > 0 ? item.Gewicht : 1
                    };

                    var result = await svc.CreateShipmentAsync(req, carrier);
                    if (result.Success)
                    {
                        await App.Db.SetTrackingAsync(item.KBestellung, result.TrackingNumber, carrier);
                        if (result.LabelPdf != null)
                        {
                            var filename = $"Label_{best.BestellNr}_{carrier}.pdf";
                            System.IO.File.WriteAllBytes(filename, result.LabelPdf);
                        }
                        erfolg++;
                    }
                    else
                    {
                        fehler++;
                    }
                }
                catch
                {
                    fehler++;
                }
            }

            await LadeVersandAsync();
            MessageBox.Show($"Versand abgeschlossen:\n{erfolg} erfolgreich\n{fehler} fehlgeschlagen",
                "Ergebnis", MessageBoxButton.OK, erfolg > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void TrackingOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (dgVersand.SelectedItem is not CoreService.VersandItem item || string.IsNullOrEmpty(item.TrackingNr))
            {
                MessageBox.Show("Bitte einen Auftrag mit Tracking-Nr auswaehlen.",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var url = GetTrackingUrl(item.VersandDienstleister ?? "", item.TrackingNr);
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Oeffnen: {ex.Message}", "Fehler");
                }
            }
        }

        private string GetTrackingUrl(string carrier, string tracking)
        {
            return carrier.ToUpper() switch
            {
                "DHL" => $"https://www.dhl.de/de/privatkunden/pakete-empfangen/verfolgen.html?piececode={tracking}",
                "DPD" => $"https://tracking.dpd.de/status/de_DE/parcel/{tracking}",
                "GLS" => $"https://gls-group.eu/DE/de/paketverfolgung?match={tracking}",
                "UPS" => $"https://www.ups.com/track?tracknum={tracking}",
                "HERMES" => $"https://www.myhermes.de/empfangen/sendungsverfolgung/?trackingId={tracking}",
                _ => ""
            };
        }

        private void LabelDrucken_Click(object sender, RoutedEventArgs e)
        {
            if (dgVersand.SelectedItem is not CoreService.VersandItem item)
            {
                MessageBox.Show("Bitte einen Auftrag auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var filename = $"Label_{item.BestellNr}_{item.VersandDienstleister}.pdf";
            if (System.IO.File.Exists(filename))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Oeffnen: {ex.Message}", "Fehler");
                }
            }
            else
            {
                MessageBox.Show($"Label-Datei nicht gefunden: {filename}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
