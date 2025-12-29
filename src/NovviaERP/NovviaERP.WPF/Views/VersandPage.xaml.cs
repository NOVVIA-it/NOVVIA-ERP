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
    public partial class VersandPage : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.VersandItem> _versandListe = new();
        private ShippingConfig? _shippingConfig;

        public VersandPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) =>
            {
                try
                {
                    // Shipping-Config aus DB laden
                    _shippingConfig = await _core.GetShippingConfigAsync();
                    await LadeVersandAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden der Versand-Seite: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
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

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (IsLoaded) _ = LadeVersandAsync();
        }

        private void DgVersand_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgVersand.SelectedItem is CoreService.VersandItem item)
            {
                // Navigation zur Bestellungsdetail (öffnet als neues Fenster)
                var frame = new System.Windows.Controls.Frame();
                frame.Navigate(new BestellungDetailPage(item.KBestellung));

                var detailWindow = new Window
                {
                    Title = $"Bestellung {item.BestellNr}",
                    Content = frame,
                    Width = 1200,
                    Height = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                detailWindow.ShowDialog();
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

            if (MessageBox.Show($"{ausgewaehlt.Count} Auftrag/Auftraege mit {carrier} versenden?\n\n" +
                "Es wird automatisch:\n" +
                "• Lieferschein erstellt (falls noch nicht vorhanden)\n" +
                "• Versand-Label generiert\n" +
                "• Tracking-Nr. gespeichert",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var erfolg = 0;
            var fehler = 0;
            var fehlerDetails = new List<string>();

            // Config aus DB verwenden (bereits beim Laden geholt)
            var cfg = _shippingConfig ?? await _core.GetShippingConfigAsync();
            using var svc = new ShippingService(cfg);

            foreach (var item in ausgewaehlt)
            {
                try
                {
                    txtStatus.Text = $"Versende {item.BestellNr}...";

                    // Lieferadresse aus Auftrag laden
                    var lieferadresse = await _core.GetAuftragLieferadresseAsync(item.KBestellung);
                    if (lieferadresse == null)
                    {
                        fehlerDetails.Add($"{item.BestellNr}: Keine Lieferadresse");
                        fehler++;
                        continue;
                    }

                    // Shipment-Request erstellen
                    var req = new ShipmentRequest
                    {
                        Referenz = item.BestellNr ?? "",
                        EmpfaengerName = !string.IsNullOrEmpty(lieferadresse.CFirma)
                            ? lieferadresse.CFirma
                            : $"{lieferadresse.CVorname} {lieferadresse.CName}".Trim(),
                        EmpfaengerStrasse = lieferadresse.CStrasse ?? "",
                        EmpfaengerPLZ = lieferadresse.CPLZ ?? "",
                        EmpfaengerOrt = lieferadresse.COrt ?? "",
                        EmpfaengerLand = lieferadresse.CISO ?? "DE",
                        EmpfaengerEmail = lieferadresse.CMail,
                        GewichtKg = item.Gewicht > 0 ? item.Gewicht : 1
                    };

                    // Label bei Carrier generieren
                    var shipResult = await svc.CreateShipmentAsync(req, carrier);
                    if (!shipResult.Success)
                    {
                        fehlerDetails.Add($"{item.BestellNr}: {shipResult.Error}");
                        fehler++;
                        continue;
                    }

                    // Kompletten Versand in DB buchen (Lieferschein + tVersand + Tracking)
                    var versandResult = await _core.VersandBuchenAsync(
                        item.KBestellung,
                        carrier,
                        shipResult.TrackingNumber,
                        shipResult.LabelPdf,
                        item.Gewicht > 0 ? item.Gewicht : 1);

                    if (versandResult.Success)
                    {
                        // Label auch lokal speichern
                        if (shipResult.LabelPdf != null)
                        {
                            var labelDir = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                "NovviaERP", "Labels");
                            System.IO.Directory.CreateDirectory(labelDir);
                            var filename = System.IO.Path.Combine(labelDir, $"Label_{item.BestellNr}_{carrier}.pdf");
                            System.IO.File.WriteAllBytes(filename, shipResult.LabelPdf);
                        }
                        erfolg++;
                    }
                    else
                    {
                        fehlerDetails.Add($"{item.BestellNr}: {versandResult.Error}");
                        fehler++;
                    }
                }
                catch (Exception ex)
                {
                    fehlerDetails.Add($"{item.BestellNr}: {ex.Message}");
                    fehler++;
                }
            }

            await LadeVersandAsync();

            var msg = $"Versand abgeschlossen:\n{erfolg} erfolgreich\n{fehler} fehlgeschlagen";
            if (fehlerDetails.Any())
            {
                msg += "\n\nFehler:\n" + string.Join("\n", fehlerDetails.Take(5));
                if (fehlerDetails.Count > 5)
                    msg += $"\n... und {fehlerDetails.Count - 5} weitere";
            }
            MessageBox.Show(msg, "Ergebnis", MessageBoxButton.OK,
                erfolg > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private async void LieferscheinErstellen_Click(object sender, RoutedEventArgs e)
        {
            var ausgewaehlt = dgVersand.SelectedItems.Cast<CoreService.VersandItem>()
                .Where(x => string.IsNullOrEmpty(x.TrackingNr))
                .ToList();

            if (!ausgewaehlt.Any())
            {
                MessageBox.Show("Bitte mindestens einen Auftrag auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Fuer {ausgewaehlt.Count} Auftrag/Auftraege Lieferscheine erstellen?",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var erstellt = 0;
            foreach (var item in ausgewaehlt)
            {
                try
                {
                    txtStatus.Text = $"Erstelle Lieferschein fuer {item.BestellNr}...";
                    await _core.GetOrCreateLieferscheinAsync(item.KBestellung);
                    erstellt++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler bei {item.BestellNr}: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            await LadeVersandAsync();
            MessageBox.Show($"{erstellt} Lieferschein(e) erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void LabelDrucken_Click(object sender, RoutedEventArgs e)
        {
            if (dgVersand.SelectedItem is not CoreService.VersandItem item)
            {
                MessageBox.Show("Bitte einen Auftrag auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(item.TrackingNr))
            {
                MessageBox.Show("Dieser Auftrag wurde noch nicht versendet.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Zuerst im lokalen Labels-Ordner suchen
            var labelDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NovviaERP", "Labels");
            var filename = System.IO.Path.Combine(labelDir, $"Label_{item.BestellNr}_{item.VersandDienstleister}.pdf");

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
                // Versuche Label aus DB zu laden
                try
                {
                    var labelData = await _core.GetVersandLabelAsync(item.KBestellung);
                    if (labelData != null && labelData.Length > 0)
                    {
                        System.IO.Directory.CreateDirectory(labelDir);
                        System.IO.File.WriteAllBytes(filename, labelData);
                        Process.Start(new ProcessStartInfo(filename) { UseShellExecute = true });
                    }
                    else
                    {
                        MessageBox.Show($"Kein Label fuer diesen Auftrag vorhanden.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden des Labels: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Einstellungen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VersandEinstellungenDialog(_shippingConfig ?? new ShippingConfig());
            if (dialog.ShowDialog() == true && dialog.Config != null)
            {
                _shippingConfig = dialog.Config;
                _ = _core.SaveShippingConfigAsync(dialog.Config);
                MessageBox.Show("Versand-Einstellungen wurden gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
