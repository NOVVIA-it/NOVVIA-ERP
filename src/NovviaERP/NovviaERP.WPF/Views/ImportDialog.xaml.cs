using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ImportDialog : Window
    {
        private readonly AuftragsImportService _import;
        private string? _dateiPfad;
        private string[]? _csvHeader;
        private Dictionary<string, string> _feldzuordnung = new();
        
        public ObservableCollection<string> CsvSpalten { get; } = new();
        public ObservableCollection<ImportVorlage> Vorlagen { get; } = new();

        public ImportDialog(AuftragsImportService import)
        {
            _import = import;
            InitializeComponent();
            
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            // Vorlagen laden
            var vorlagen = await _import.GetVorlagenAsync();
            Vorlagen.Clear();
            Vorlagen.Add(new ImportVorlage { Name = "(Keine Vorlage)" });
            foreach (var v in vorlagen) Vorlagen.Add(v);
            cbVorlage.ItemsSource = Vorlagen;
            cbVorlage.DisplayMemberPath = "Name";
            cbVorlage.SelectedIndex = 0;

            // Zielfelder aufbauen
            BuildZielfelder();
        }

        private void BuildZielfelder()
        {
            var gruppen = AuftragsImportService.VerfuegbareFelder
                .GroupBy(f => f.Gruppe)
                .Select(g => new FeldGruppe
                {
                    Name = g.Key,
                    FontWeight = FontWeights.Bold,
                    Felder = g.Select(f => new FeldItem
                    {
                        Name = f.Anzeigename + (f.IstPflicht ? " *" : ""),
                        FeldName = f.FeldName,
                        IstPflicht = f.IstPflicht
                    }).ToList()
                }).ToList();

            tvZielfelder.ItemsSource = gruppen;
        }

        private async void BtnDateiWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV-Dateien|*.csv|Excel-Dateien|*.xlsx;*.xls|Alle Dateien|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _dateiPfad = dialog.FileName;
                txtDatei.Text = Path.GetFileName(_dateiPfad);

                try
                {
                    _csvHeader = await _import.GetCsvHeaderAsync(_dateiPfad);
                    CsvSpalten.Clear();
                    foreach (var h in _csvHeader) CsvSpalten.Add(h);
                    lstCsvSpalten.ItemsSource = CsvSpalten;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Lesen der Datei: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CbVorlage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vorlage = cbVorlage.SelectedItem as ImportVorlage;
            if (vorlage?.Id > 0 && !string.IsNullOrEmpty(vorlage.FeldzuordnungJson))
            {
                _feldzuordnung = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                    vorlage.FeldzuordnungJson) ?? new();
                UpdateZuordnungsAnzeige();
            }
        }

        private void BtnZuordnen_Click(object sender, RoutedEventArgs e)
        {
            var csvSpalte = lstCsvSpalten.SelectedItem as string;
            var zielFeld = GetSelectedZielfeld();
            
            if (csvSpalte != null && zielFeld != null)
            {
                _feldzuordnung[zielFeld.FeldName] = csvSpalte;
                zielFeld.Zuordnung = $"← {csvSpalte}";
                tvZielfelder.Items.Refresh();
            }
        }

        private void BtnEntfernen_Click(object sender, RoutedEventArgs e)
        {
            var zielFeld = GetSelectedZielfeld();
            if (zielFeld != null && _feldzuordnung.ContainsKey(zielFeld.FeldName))
            {
                _feldzuordnung.Remove(zielFeld.FeldName);
                zielFeld.Zuordnung = "";
                tvZielfelder.Items.Refresh();
            }
        }

        private FeldItem? GetSelectedZielfeld()
        {
            return tvZielfelder.SelectedItem as FeldItem;
        }

        private void UpdateZuordnungsAnzeige()
        {
            foreach (FeldGruppe gruppe in tvZielfelder.ItemsSource)
            {
                foreach (var feld in gruppe.Felder)
                {
                    if (_feldzuordnung.TryGetValue(feld.FeldName, out var csv))
                        feld.Zuordnung = $"← {csv}";
                    else
                        feld.Zuordnung = "";
                }
            }
            tvZielfelder.Items.Refresh();
        }

        private async void BtnVorschau_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_dateiPfad))
            {
                MessageBox.Show("Bitte zuerst eine Datei auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var vorlage = new ImportVorlage
                {
                    Trennzeichen = ";",
                    HatKopfzeile = true,
                    FeldzuordnungJson = System.Text.Json.JsonSerializer.Serialize(_feldzuordnung)
                };

                var vorschau = await _import.GetVorschauAsync(_dateiPfad, vorlage, 10);
                
                // DataTable für Grid erstellen
                var dt = new DataTable();
                foreach (var key in _feldzuordnung.Keys)
                    dt.Columns.Add(key);

                foreach (var zeile in vorschau.Zeilen)
                {
                    var row = dt.NewRow();
                    foreach (var kvp in zeile)
                        if (dt.Columns.Contains(kvp.Key))
                            row[kvp.Key] = kvp.Value;
                    dt.Rows.Add(row);
                }

                dgVorschau.ItemsSource = dt.DefaultView;
                txtVorschauStatus.Text = $"{vorschau.Zeilen.Count} Zeilen geladen" + 
                    (vorschau.Warnungen.Any() ? $", {vorschau.Warnungen.Count} Warnungen" : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei Vorschau: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnVorlageSpeichern_Click(object sender, RoutedEventArgs e)
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox("Name der Vorlage:", "Vorlage speichern", "Meine Vorlage");
            if (string.IsNullOrEmpty(name)) return;

            var vorlage = new ImportVorlage
            {
                Name = name,
                Typ = "Auftrag",
                Trennzeichen = ";",
                HatKopfzeile = true,
                FeldzuordnungJson = System.Text.Json.JsonSerializer.Serialize(_feldzuordnung)
            };

            await _import.SaveVorlageAsync(vorlage);
            await LoadDataAsync();
            MessageBox.Show("Vorlage gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnVorlageNeu_Click(object sender, RoutedEventArgs e)
        {
            _feldzuordnung.Clear();
            UpdateZuordnungsAnzeige();
            cbVorlage.SelectedIndex = 0;
        }

        private async void BtnImportieren_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_dateiPfad))
            {
                MessageBox.Show("Bitte zuerst eine Datei auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_feldzuordnung.Count == 0)
            {
                MessageBox.Show("Bitte mindestens ein Feld zuordnen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var vorlage = new ImportVorlage
                {
                    Trennzeichen = ";",
                    HatKopfzeile = true,
                    FeldzuordnungJson = System.Text.Json.JsonSerializer.Serialize(_feldzuordnung)
                };

                var optionen = new ImportOptionen
                {
                    NeueKundenErstellen = chkNeueKunden.IsChecked == true,
                    UnbekannteArtikelErstellen = chkNeueArtikel.IsChecked == true,
                    FehlerIgnorieren = chkFehlerIgnorieren.IsChecked == true
                };

                var ergebnis = await _import.ImportierenAsync(_dateiPfad, vorlage, optionen);

                var msg = $"Import abgeschlossen!\n\n" +
                          $"Zeilen gesamt: {ergebnis.GesamtZeilen}\n" +
                          $"Erfolgreich: {ergebnis.ErfolgreicheZeilen}\n" +
                          $"Aufträge erstellt: {ergebnis.ErstellteAuftraege}\n" +
                          $"Positionen: {ergebnis.ImportiertePositionen}";

                if (ergebnis.FehlerhafteZeilen > 0)
                    msg += $"\n\nFehler: {ergebnis.FehlerhafteZeilen}";

                MessageBox.Show(msg, "Import-Ergebnis", MessageBoxButton.OK, MessageBoxImage.Information);
                
                if (ergebnis.FehlerhafteZeilen == 0)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class FeldGruppe
    {
        public string Name { get; set; } = "";
        public FontWeight FontWeight { get; set; } = FontWeights.Bold;
        public List<FeldItem> Felder { get; set; } = new();
    }

    public class FeldItem
    {
        public string Name { get; set; } = "";
        public string FeldName { get; set; } = "";
        public bool IstPflicht { get; set; }
        public string Zuordnung { get; set; } = "";
        public FontWeight FontWeight => FontWeights.Normal;
    }
}
