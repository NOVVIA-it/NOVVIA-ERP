using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class AuftragsstapelimportView : Window
    {
        private readonly CoreService _coreService;
        private ObservableCollection<ImportPosition> _positionen = new();

        public AuftragsstapelimportView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            dgPositionen.ItemsSource = _positionen;
        }

        private async void ExcelImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Excel-Datei fuer Import auswaehlen",
                Filter = "Excel-Dateien (*.xlsx;*.xls)|*.xlsx;*.xls|CSV-Dateien (*.csv)|*.csv|Alle Dateien (*.*)|*.*",
                DefaultExt = ".xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    txtImportDatei.Text = dialog.FileName;
                    _positionen.Clear();

                    // CSV oder Excel laden
                    var extension = Path.GetExtension(dialog.FileName).ToLower();
                    List<ImportPosition> importedItems;

                    if (extension == ".csv")
                    {
                        importedItems = await LoadCsvAsync(dialog.FileName);
                    }
                    else
                    {
                        // Fuer Excel brauchen wir eine Library wie EPPlus oder ClosedXML
                        // Vorerst nur CSV unterstuetzen
                        MessageBox.Show("Excel-Import wird noch implementiert.\nBitte CSV-Datei verwenden.\n\nFormat: AdressNr;ArtNr;Menge;Preis",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    foreach (var item in importedItems)
                    {
                        // Kunde und Artikel nachschlagen
                        await EnrichImportPosition(item);
                        _positionen.Add(item);
                    }

                    txtPositionenCount.Text = _positionen.Count.ToString();
                    btnSpeichern.IsEnabled = _positionen.Any();
                    btnSpeichernBuchen.IsEnabled = _positionen.Any();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Import: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async System.Threading.Tasks.Task<List<ImportPosition>> LoadCsvAsync(string filePath)
        {
            var result = new List<ImportPosition>();
            var lines = await File.ReadAllLinesAsync(filePath);

            // Erste Zeile ist Header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(';', ',');
                if (parts.Length >= 3)
                {
                    var pos = new ImportPosition
                    {
                        AdressNr = parts[0].Trim(),
                        ArtNr = parts[1].Trim(),
                        Menge = decimal.TryParse(parts[2].Trim().Replace(',', '.'),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 1,
                        Preis = parts.Length > 3 && decimal.TryParse(parts[3].Trim().Replace(',', '.'),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0
                    };
                    result.Add(pos);
                }
            }

            return result;
        }

        private async System.Threading.Tasks.Task EnrichImportPosition(ImportPosition pos)
        {
            try
            {
                // Kunde suchen (nach AdressNr oder Kundennummer)
                var adressTyp = (cboAdressNr.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Adress-Nr.";

                // Artikel suchen
                var artikel = await _coreService.GetArtikelByArtNrAsync(pos.ArtNr);
                if (artikel != null)
                {
                    pos.ArtikelName = artikel.CName;
                    if (pos.Preis == 0)
                        pos.Preis = artikel.FVKNetto;
                }
                else
                {
                    pos.ArtikelName = "(Artikel nicht gefunden)";
                }

                // Kunde Name laden (vereinfacht)
                pos.KundeName = $"Kunde {pos.AdressNr}";
            }
            catch
            {
                // Fehler ignorieren, Position trotzdem hinzufuegen
            }
        }

        private async void SpeichernBuchen_Click(object sender, RoutedEventArgs e)
        {
            await SpeichernAsync(buchen: true);
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            await SpeichernAsync(buchen: false);
        }

        private async System.Threading.Tasks.Task SpeichernAsync(bool buchen)
        {
            if (!_positionen.Any())
            {
                MessageBox.Show("Keine Positionen zum Speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnSpeichern.IsEnabled = false;
                btnSpeichernBuchen.IsEnabled = false;
                pbProgress.Visibility = Visibility.Visible;
                pbProgress.Maximum = _positionen.Count;
                pbProgress.Value = 0;

                // Mindest-MHD berechnen
                var mindestMHD = GetMindestMHD();

                // Positionen nach Kunde gruppieren (ein Auftrag pro Kunde)
                var gruppiertNachKunde = _positionen.GroupBy(p => p.AdressNr).ToList();
                int erstellteAuftraege = 0;

                foreach (var kundenGruppe in gruppiertNachKunde)
                {
                    try
                    {
                        // Konvertiere zu CoreService DTO
                        var corePositionen = kundenGruppe.Select(p => new CoreService.AuftragImportPosition
                        {
                            AdressNr = p.AdressNr,
                            ArtNr = p.ArtNr,
                            Menge = p.Menge,
                            Preis = p.Preis,
                            MindestMHD = mindestMHD
                        }).ToList();

                        // Auftrag erstellen (mit Mindest-MHD)
                        var auftragId = await _coreService.CreateAuftragFromImportAsync(
                            kundenGruppe.Key,
                            corePositionen,
                            txtZusatztext.Text,
                            rbUeberPositionen.IsChecked == true,
                            mindestMHD);

                        if (auftragId > 0)
                        {
                            erstellteAuftraege++;

                            if (buchen)
                            {
                                // Auftrag buchen/freigeben
                                await _coreService.AuftragBuchenAsync(auftragId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler bei Kunde {kundenGruppe.Key}: {ex.Message}",
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    pbProgress.Value += kundenGruppe.Count();
                }

                pbProgress.Visibility = Visibility.Collapsed;

                var modus = buchen ? "erstellt und gebucht" : "erstellt";
                MessageBox.Show($"{erstellteAuftraege} Auftraege wurden {modus}!",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                if (erstellteAuftraege > 0)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSpeichern.IsEnabled = _positionen.Any();
                btnSpeichernBuchen.IsEnabled = _positionen.Any();
                pbProgress.Visibility = Visibility.Collapsed;
            }
        }

        private void Schliessen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Berechnet das Mindest-MHD aus den UI-Eingaben
        /// </summary>
        private DateTime? GetMindestMHD()
        {
            // Direktes Datum hat Vorrang
            if (dpMindestMHD.SelectedDate.HasValue)
                return dpMindestMHD.SelectedDate.Value;

            // Offset aus Text berechnen
            if (!string.IsNullOrWhiteSpace(txtMHDOffset.Text) &&
                int.TryParse(txtMHDOffset.Text.Trim(), out int offset) && offset > 0)
            {
                var einheit = (cboMHDEinheit.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Monate";

                if (einheit == "Monate")
                    return DateTime.Today.AddMonths(offset);
                else // Tage
                    return DateTime.Today.AddDays(offset);
            }

            return null;
        }
    }

    public class ImportPosition
    {
        public string AdressNr { get; set; } = "";
        public string KundeName { get; set; } = "";
        public string ArtNr { get; set; } = "";
        public string ArtikelName { get; set; } = "";
        public decimal Menge { get; set; }
        public decimal Preis { get; set; }
        public DateTime? MindestMHD { get; set; }
    }
}
