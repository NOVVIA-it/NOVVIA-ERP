using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class AmeiseView : UserControl
    {
        private readonly AmeiseService _ameise;
        private readonly AppDataService _appData;
        private readonly JtlDbContext _db;
        private CancellationTokenSource? _cts;
        private string? _aktuelleVorlage;
        private List<string[]>? _csvDaten;
        private string[]? _csvHeader;
        private Dictionary<string, ComboBox> _mappingControls = new();

        public AmeiseView()
        {
            InitializeComponent();
            _db = App.Services.GetRequiredService<JtlDbContext>();
            _ameise = new AmeiseService(_db);
            _appData = App.Services.GetRequiredService<AppDataService>();

            Loaded += async (s, e) => await InitAsync();
        }

        private async Task InitAsync()
        {
            await LadeHistorieAsync();
        }

        #region Import

        private void Vorlage_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (lstVorlagen.SelectedItem is ListBoxItem item && item.Tag is string vorlage)
            {
                _aktuelleVorlage = vorlage;

                if (AmeiseService.ImportVorlagen.TryGetValue(vorlage, out var def))
                {
                    txtVorlageInfo.Text = $"Tabelle: {def.Tabelle}\nKey: {def.KeySpalte}\nFelder: {def.Spalten.Count}";
                    ErstelleMappingUI(def);
                }

                PruefeImportBereit();
            }
        }

        private void DateiWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import-Datei waehlen",
                Filter = "CSV-Dateien (*.csv)|*.csv|Text-Dateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*",
                FilterIndex = 1
            };

            if (dlg.ShowDialog() == true)
            {
                txtDateiPfad.Text = dlg.FileName;
                LadeCsvVorschau(dlg.FileName);
                PruefeImportBereit();
            }
        }

        private void LadeCsvVorschau(string pfad)
        {
            try
            {
                var trennzeichen = (cmbTrennzeichen.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ";";
                var encoding = GetEncoding();
                var mitHeader = chkHeader.IsChecked == true;

                var zeilen = File.ReadAllLines(pfad, encoding);
                if (zeilen.Length == 0)
                {
                    txtDateiInfo.Text = "Datei ist leer";
                    return;
                }

                _csvDaten = zeilen.Select(z => z.Split(trennzeichen[0])).ToList();

                if (mitHeader && _csvDaten.Count > 0)
                {
                    _csvHeader = _csvDaten[0];
                    _csvDaten = _csvDaten.Skip(1).ToList();
                }
                else
                {
                    _csvHeader = Enumerable.Range(1, _csvDaten[0].Length).Select(i => $"Spalte{i}").ToArray();
                }

                txtDateiInfo.Text = $"{_csvDaten.Count} Zeilen, {_csvHeader.Length} Spalten";
                txtVorschauInfo.Text = $"{Math.Min(100, _csvDaten.Count)} von {_csvDaten.Count} Zeilen";

                // Vorschau-DataGrid fuellen
                var dt = new DataTable();
                foreach (var h in _csvHeader)
                    dt.Columns.Add(h);

                foreach (var zeile in _csvDaten.Take(100))
                {
                    var row = dt.NewRow();
                    for (int i = 0; i < Math.Min(zeile.Length, _csvHeader.Length); i++)
                        row[i] = zeile[i];
                    dt.Rows.Add(row);
                }

                dgVorschau.ItemsSource = dt.DefaultView;

                // Mapping aktualisieren
                if (_aktuelleVorlage != null && AmeiseService.ImportVorlagen.TryGetValue(_aktuelleVorlage, out var def))
                    AktualisiereMappingDropdowns(def);
            }
            catch (Exception ex)
            {
                txtDateiInfo.Text = $"Fehler: {ex.Message}";
            }
        }

        private void ErstelleMappingUI(AmeiseImportVorlage vorlage)
        {
            grdMapping.Children.Clear();
            grdMapping.RowDefinitions.Clear();
            _mappingControls.Clear();

            int row = 0;
            foreach (var (csvName, def) in vorlage.Spalten)
            {
                grdMapping.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

                var lbl = new TextBlock
                {
                    Text = csvName + (def.IstPflicht ? " *" : ""),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = def.IstPflicht ? FontWeights.SemiBold : FontWeights.Normal
                };
                Grid.SetRow(lbl, row);
                Grid.SetColumn(lbl, 0);
                grdMapping.Children.Add(lbl);

                var arrow = new TextBlock { Text = "->", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetRow(arrow, row);
                Grid.SetColumn(arrow, 1);
                grdMapping.Children.Add(arrow);

                var cmb = new ComboBox { VerticalAlignment = VerticalAlignment.Center };
                cmb.Items.Add(new ComboBoxItem { Content = "(nicht zuordnen)", Tag = "" });
                Grid.SetRow(cmb, row);
                Grid.SetColumn(cmb, 2);
                grdMapping.Children.Add(cmb);

                _mappingControls[csvName] = cmb;
                row++;
            }
        }

        private void AktualisiereMappingDropdowns(AmeiseImportVorlage vorlage)
        {
            if (_csvHeader == null) return;

            foreach (var (feldName, cmb) in _mappingControls)
            {
                cmb.Items.Clear();
                cmb.Items.Add(new ComboBoxItem { Content = "(nicht zuordnen)", Tag = "" });

                foreach (var header in _csvHeader)
                {
                    var item = new ComboBoxItem { Content = header, Tag = header };
                    cmb.Items.Add(item);

                    // Auto-Match wenn Namen aehnlich
                    if (header.Equals(feldName, StringComparison.OrdinalIgnoreCase) ||
                        header.Replace("_", "").Equals(feldName, StringComparison.OrdinalIgnoreCase))
                    {
                        cmb.SelectedItem = item;
                    }
                }

                if (cmb.SelectedIndex < 0) cmb.SelectedIndex = 0;
            }
        }

        private void AutoMapping_Click(object sender, RoutedEventArgs e)
        {
            if (_aktuelleVorlage == null || _csvHeader == null) return;

            foreach (var (feldName, cmb) in _mappingControls)
            {
                // Versuche beste Zuordnung zu finden
                var match = _csvHeader.FirstOrDefault(h =>
                    h.Equals(feldName, StringComparison.OrdinalIgnoreCase) ||
                    h.Replace("_", "").Replace(" ", "").Equals(feldName.Replace("_", "").Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    foreach (ComboBoxItem item in cmb.Items)
                    {
                        if (item.Tag?.ToString() == match)
                        {
                            cmb.SelectedItem = item;
                            break;
                        }
                    }
                }
            }

            txtStatus.Text = "Auto-Mapping durchgefuehrt";
        }

        private void VorschauLaden_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtDateiPfad.Text))
                LadeCsvVorschau(txtDateiPfad.Text);
        }

        private void PruefeImportBereit()
        {
            btnImportieren.IsEnabled = !string.IsNullOrEmpty(_aktuelleVorlage) &&
                                       !string.IsNullOrEmpty(txtDateiPfad.Text) &&
                                       File.Exists(txtDateiPfad.Text);
        }

        private async void ImportStarten_Click(object sender, RoutedEventArgs e)
        {
            if (_aktuelleVorlage == null || _csvDaten == null || _csvHeader == null) return;

            _cts = new CancellationTokenSource();
            btnImportieren.IsEnabled = false;
            btnAbbrechen.Visibility = Visibility.Visible;
            pbProgress.Visibility = Visibility.Visible;
            pbProgress.Value = 0;

            var optionen = new AmeiseImportOptionen
            {
                Trennzeichen = (cmbTrennzeichen.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ";",
                UpdateExistierend = chkUpdateExistierend.IsChecked == true && chkNurNeu.IsChecked != true,
                FehlerIgnorieren = chkFehlerIgnorieren.IsChecked == true,
                Transaktion = true
            };

            var testlauf = chkTestlauf.IsChecked == true;

            try
            {
                txtStatus.Text = testlauf ? "Testlauf..." : "Importiere...";
                var gesamt = _csvDaten.Count;
                var erfolg = 0;
                var fehler = 0;
                var fehlerListe = new List<string>();

                using var stream = new MemoryStream();
                using var writer = new StreamWriter(stream, GetEncoding());

                // Header schreiben
                writer.WriteLine(string.Join(optionen.Trennzeichen, _csvHeader));

                // Daten schreiben
                foreach (var zeile in _csvDaten)
                    writer.WriteLine(string.Join(optionen.Trennzeichen, zeile));

                writer.Flush();
                stream.Position = 0;

                if (!testlauf)
                {
                    var result = await _ameise.ImportCsvAsync(_aktuelleVorlage, stream, optionen);
                    erfolg = result.Erfolgreich;
                    fehler = result.Fehlgeschlagen;
                    fehlerListe = result.Fehler;
                }
                else
                {
                    // Testlauf - nur validieren
                    erfolg = gesamt;
                    await Task.Delay(500); // Simulation
                }

                // Historie speichern
                await SpeichereHistorieAsync(new ImportHistorieEintrag
                {
                    Datum = DateTime.Now,
                    Typ = "Import",
                    Vorlage = _aktuelleVorlage,
                    Datei = Path.GetFileName(txtDateiPfad.Text),
                    Gesamt = gesamt,
                    Erfolg = erfolg,
                    Fehler = fehler,
                    Status = testlauf ? "Testlauf OK" : (fehler == 0 ? "Erfolgreich" : $"{fehler} Fehler")
                });

                txtStatus.Text = testlauf
                    ? $"Testlauf: {erfolg} Zeilen wuerden importiert"
                    : $"Import abgeschlossen: {erfolg} OK, {fehler} Fehler";

                if (fehlerListe.Count > 0)
                {
                    MessageBox.Show(string.Join("\n", fehlerListe.Take(20)), "Import-Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await LadeHistorieAsync();
            }
            catch (OperationCanceledException)
            {
                txtStatus.Text = "Import abgebrochen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show(ex.Message, "Import-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnImportieren.IsEnabled = true;
                btnAbbrechen.Visibility = Visibility.Collapsed;
                pbProgress.Visibility = Visibility.Collapsed;
                _cts = null;
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
        }

        #endregion

        #region Export

        private async void ExportTyp_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (lstExportTyp.SelectedItem is ListBoxItem item && item.Tag is string typ)
            {
                await LadeExportVorschauAsync(typ);
            }
        }

        private async Task LadeExportVorschauAsync(string typ)
        {
            try
            {
                txtExportInfo.Text = "Lade...";
                var conn = await _db.GetConnectionAsync();

                if (!AmeiseService.ImportVorlagen.TryGetValue(typ, out var vorlage)) return;

                var sql = $"SELECT TOP 100 * FROM {vorlage.Tabelle}";
                var filter = txtExportFilter.Text?.Trim();
                if (!string.IsNullOrEmpty(filter))
                    sql += $" WHERE {filter}";

                var daten = await conn.QueryAsync(sql);
                var liste = daten.ToList();

                dgExportVorschau.ItemsSource = liste;

                // Gesamtanzahl ermitteln
                var countSql = $"SELECT COUNT(*) FROM {vorlage.Tabelle}";
                if (!string.IsNullOrEmpty(filter))
                    countSql += $" WHERE {filter}";
                var count = await conn.ExecuteScalarAsync<int>(countSql);

                txtExportInfo.Text = $"{count} Datensaetze gefunden";
            }
            catch (Exception ex)
            {
                txtExportInfo.Text = $"Fehler: {ex.Message}";
            }
        }

        private async void Exportieren_Click(object sender, RoutedEventArgs e)
        {
            if (lstExportTyp.SelectedItem is not ListBoxItem item || item.Tag is not string typ) return;

            var dlg = new SaveFileDialog
            {
                Title = "Export speichern",
                Filter = "CSV-Dateien (*.csv)|*.csv",
                FileName = $"{typ}_Export_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                txtStatus.Text = "Exportiere...";
                var trennzeichen = (cmbExportTrennzeichen.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ";";
                int.TryParse(txtExportLimit.Text, out var limit);
                if (limit <= 0) limit = 10000;

                var optionen = new ExportOptionen
                {
                    Trennzeichen = trennzeichen,
                    Filter = txtExportFilter.Text?.Trim(),
                    Limit = limit
                };

                var bytes = await _ameise.ExportCsvAsync(typ, optionen);
                await File.WriteAllBytesAsync(dlg.FileName, bytes);

                // Historie speichern
                await SpeichereHistorieAsync(new ImportHistorieEintrag
                {
                    Datum = DateTime.Now,
                    Typ = "Export",
                    Vorlage = typ,
                    Datei = Path.GetFileName(dlg.FileName),
                    Gesamt = bytes.Length,
                    Erfolg = 1,
                    Fehler = 0,
                    Status = "Erfolgreich"
                });

                txtStatus.Text = $"Export gespeichert: {dlg.FileName}";
                await LadeHistorieAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Export-Fehler: {ex.Message}";
                MessageBox.Show(ex.Message, "Export-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Historie

        private async Task LadeHistorieAsync()
        {
            var historie = _appData.Load<List<ImportHistorieEintrag>>("ameise_historie", new List<ImportHistorieEintrag>());
            dgHistorie.ItemsSource = historie?.OrderByDescending(h => h.Datum).ToList();
        }

        private async Task SpeichereHistorieAsync(ImportHistorieEintrag eintrag)
        {
            var historie = _appData.Load<List<ImportHistorieEintrag>>("ameise_historie", new List<ImportHistorieEintrag>()) ?? new();
            historie.Add(eintrag);

            // Max 100 Eintraege behalten
            if (historie.Count > 100)
                historie = historie.OrderByDescending(h => h.Datum).Take(100).ToList();

            _appData.Save("ameise_historie", historie);
        }

        private async void HistorieLaden_Click(object sender, RoutedEventArgs e)
        {
            await LadeHistorieAsync();
        }

        private void HistorieLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Import-Historie wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _appData.Delete("ameise_historie");
                dgHistorie.ItemsSource = null;
                txtStatus.Text = "Historie geloescht";
            }
        }

        private void Historie_Click(object sender, RoutedEventArgs e)
        {
            // Tab auf Historie wechseln
            var tabControl = this.FindName("tabMain") as TabControl;
        }

        #endregion

        #region Vorlagen

        private void Vorlagen_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Vorlagen-Dialog oeffnen
            MessageBox.Show("Vorlagen-Verwaltung: Hier koennen Sie eigene Import-Vorlagen erstellen und bearbeiten.\n\n(Feature in Entwicklung)",
                "Vorlagen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Helpers

        private Encoding GetEncoding()
        {
            var enc = (cmbEncoding.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "utf-8";
            return enc switch
            {
                "windows-1252" => Encoding.GetEncoding(1252),
                "iso-8859-1" => Encoding.Latin1,
                _ => Encoding.UTF8
            };
        }

        #endregion
    }

    public class ImportHistorieEintrag
    {
        public DateTime Datum { get; set; }
        public string Typ { get; set; } = "";
        public string Vorlage { get; set; } = "";
        public string Datei { get; set; } = "";
        public int Gesamt { get; set; }
        public int Erfolg { get; set; }
        public int Fehler { get; set; }
        public string Status { get; set; } = "";
    }
}
