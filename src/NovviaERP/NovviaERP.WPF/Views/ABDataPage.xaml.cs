using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ABDataPage : UserControl
    {
        private readonly ABdataService _abdata;
        private readonly CoreService _core;
        private List<ABdataArtikel> _liste = new();

        public ABDataPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _abdata = new ABdataService(_core.ConnectionString);

            Loaded += async (s, e) =>
            {
                await LadeArtikelCountAsync();
                await LadeLetztesImportDatumAsync();
            };
        }

        private async System.Threading.Tasks.Task LadeArtikelCountAsync()
        {
            try
            {
                var alle = await _abdata.SucheArtikelAsync("", 1);
                // Zeige Gesamtanzahl - bei leerem Ergebnis steht 0
                txtArtikelCount.Text = "Datenbank bereit";
            }
            catch
            {
                txtArtikelCount.Text = "Keine Daten";
            }
        }

        private async System.Threading.Tasks.Task LadeLetztesImportDatumAsync()
        {
            try
            {
                var historie = await _abdata.GetImportHistorieAsync(1);
                var letzter = historie.FirstOrDefault();
                if (letzter != null)
                {
                    txtLastImport.Text = $"Letzter Import: {letzter.ImportStart:dd.MM.yyyy HH:mm} ({letzter.AnzahlGesamt} Artikel)";
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task SucheAsync()
        {
            try
            {
                txtStatus.Text = "Suche...";
                var suche = txtSuche.Text?.Trim() ?? "";

                if (string.IsNullOrEmpty(suche))
                {
                    txtStatus.Text = "Bitte Suchbegriff eingeben (PZN, Name, Hersteller, Wirkstoff)";
                    return;
                }

                _liste = (await _abdata.SucheArtikelAsync(suche, 500)).ToList();

                // Filter anwenden
                if (chkRezeptpflicht.IsChecked == true)
                    _liste = _liste.Where(x => x.Rezeptpflicht).ToList();
                if (chkBTM.IsChecked == true)
                    _liste = _liste.Where(x => x.BTM).ToList();
                if (chkKuehlpflichtig.IsChecked == true)
                    _liste = _liste.Where(x => x.Kuehlpflichtig).ToList();

                dgArtikel.ItemsSource = _liste;
                txtStatus.Text = $"{_liste.Count} Artikel gefunden";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Suche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = SucheAsync();
        }

        private void Suchen_Click(object sender, RoutedEventArgs e) => _ = SucheAsync();

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "ABData-Datei importieren",
                Filter = "CSV-Dateien (*.csv)|*.csv|Text-Dateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                btnImport.IsEnabled = false;
                txtStatus.Text = "Importiere...";

                var result = await _abdata.ImportArtikelstammAsync(dlg.FileName);

                if (result.Success)
                {
                    MessageBox.Show(
                        $"Import abgeschlossen!\n\n" +
                        $"Gesamt: {result.AnzahlGesamt}\n" +
                        $"Neu: {result.AnzahlNeu}\n" +
                        $"Aktualisiert: {result.AnzahlAktualisiert}",
                        "Import erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Import mit Fehlern!\n\n" +
                        $"Gesamt: {result.AnzahlGesamt}\n" +
                        $"Fehler: {result.AnzahlFehler}\n\n" +
                        string.Join("\n", result.Fehler.Take(10)),
                        "Import-Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await LadeLetztesImportDatumAsync();
                await LadeArtikelCountAsync();
                txtStatus.Text = "Import abgeschlossen";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnImport.IsEnabled = true;
            }
        }

        private async void AutoMapping_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "Auto-Mapping verknuepft ABData-Artikel automatisch mit JTL-Artikeln anhand der PZN.\n\n" +
                "Dies kann einige Minuten dauern. Fortfahren?",
                "Auto-Mapping", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                btnAutoMapping.IsEnabled = false;
                txtStatus.Text = "Mapping laeuft...";

                var anzahl = await _abdata.AutoMappingAsync();

                MessageBox.Show($"{anzahl} Artikel wurden automatisch verknuepft.",
                    "Auto-Mapping", MessageBoxButton.OK, MessageBoxImage.Information);
                txtStatus.Text = $"Mapping abgeschlossen: {anzahl} verknuepft";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mapping fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnAutoMapping.IsEnabled = true;
            }
        }

        private void DataGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgArtikel.SelectedItem is ABdataArtikel artikel)
            {
                ShowArtikelDetails(artikel);
            }
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ABdataArtikel artikel)
            {
                ShowArtikelDetails(artikel);
            }
        }

        private void ShowArtikelDetails(ABdataArtikel artikel)
        {
            var msg = $"PZN: {artikel.PZN}\n" +
                      $"Name: {artikel.Name}\n" +
                      $"Hersteller: {artikel.Hersteller}\n" +
                      $"Darreichungsform: {artikel.Darreichungsform}\n" +
                      $"Packungsgroesse: {artikel.Packungsgroesse}\n" +
                      $"Wirkstoff: {artikel.Wirkstoff}\n" +
                      $"ATC-Code: {artikel.ATC}\n\n" +
                      $"AEP (Einkauf): {artikel.AEP:N2} EUR\n" +
                      $"AVP (Verkauf): {artikel.AVP:N2} EUR\n" +
                      $"AEK (EK netto): {artikel.AEK:N2} EUR\n\n" +
                      $"Rezeptpflicht: {(artikel.Rezeptpflicht ? "Ja" : "Nein")}\n" +
                      $"BTM: {(artikel.BTM ? "Ja" : "Nein")}\n" +
                      $"Kuehlpflichtig: {(artikel.Kuehlpflichtig ? "Ja" : "Nein")}";

            MessageBox.Show(msg, $"ABData Artikel - {artikel.PZN}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ImportToJTL_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not ABdataArtikel artikel)
            {
                MessageBox.Show("Bitte einen Artikel auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                $"Artikel '{artikel.Name}' (PZN: {artikel.PZN}) als neuen JTL-Artikel anlegen?\n\n" +
                $"ArtNr: {artikel.PZN}\n" +
                $"EK-Preis: {artikel.AEK:N2} EUR\n" +
                $"VK-Preis: {artikel.AVP:N2} EUR",
                "Artikel importieren", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                // Neuen Artikel in JTL anlegen
                var kArtikel = await _core.CreateArtikelFromABDataAsync(artikel);

                // Mapping erstellen
                await _abdata.MapArtikelAsync(kArtikel, artikel.PZN);

                MessageBox.Show($"Artikel erfolgreich angelegt!\nJTL-ArtikelNr: {artikel.PZN}",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Anlegen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LinkToJTL_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not ABdataArtikel artikel)
            {
                MessageBox.Show("Bitte einen Artikel auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Einfacher Dialog fuer JTL-ArtikelID
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"JTL-Artikel-ID eingeben zum Verknuepfen mit PZN {artikel.PZN}:",
                "Mit JTL-Artikel verknuepfen", "");

            if (string.IsNullOrWhiteSpace(input)) return;

            if (!int.TryParse(input, out var kArtikel))
            {
                MessageBox.Show("Bitte eine gueltige Artikel-ID eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _abdata.MapArtikelAsync(kArtikel, artikel.PZN);
                MessageBox.Show($"PZN {artikel.PZN} wurde mit Artikel {kArtikel} verknuepft.",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyPZN_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ABdataArtikel artikel)
            {
                Clipboard.SetText(artikel.PZN);
                txtStatus.Text = $"PZN {artikel.PZN} in Zwischenablage kopiert";
            }
        }

        private async void Historie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var historie = await _abdata.GetImportHistorieAsync(20);

                if (!historie.Any())
                {
                    MessageBox.Show("Noch keine Importe durchgefuehrt.", "Import-Historie", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var msg = string.Join("\n", historie.Select(h =>
                    $"{h.ImportStart:dd.MM.yyyy HH:mm} - {h.Dateiname}: {h.AnzahlGesamt} Artikel ({h.AnzahlNeu} neu, {h.AnzahlAktualisiert} aktualisiert, {h.AnzahlFehler} Fehler) - {h.Status}"));

                MessageBox.Show(msg, "Import-Historie", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
