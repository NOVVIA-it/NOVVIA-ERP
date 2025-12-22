using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ImportPage : Page
    {
        private readonly ImportService _importService;
        private List<string> _dateiSpalten = new();
        private DataTable? _vorschauDaten;
        private Dictionary<string, ComboBox> _zuordnungsCombos = new();

        // Zielfelder für Aufträge
        private static readonly Dictionary<string, string> AuftragFelder = new()
        {
            { "ExterneBestellnummer", "Externe Bestellnr. *" },
            { "KundenNr", "Kundennummer" },
            { "KundeMail", "Kunde E-Mail" },
            { "KundeFirma", "Kunde Firma" },
            { "KundeVorname", "Kunde Vorname" },
            { "KundeNachname", "Kunde Nachname" },
            { "KundeStrasse", "Kunde Strasse" },
            { "KundePLZ", "Kunde PLZ" },
            { "KundeOrt", "Kunde Ort" },
            { "KundeLand", "Kunde Land" },
            { "ArtikelNr", "Artikelnummer *" },
            { "ArtikelBarcode", "Artikel Barcode/EAN" },
            { "ArtikelName", "Artikel Name" },
            { "Menge", "Menge *" },
            { "Preis", "Preis" },
            { "Bestelldatum", "Bestelldatum" },
            { "Zahlungsart", "Zahlungsart" },
            { "Versandart", "Versandart" },
            { "Anmerkung", "Anmerkung" }
        };

        // Zielfelder für Lieferantenbestellungen
        private static readonly Dictionary<string, string> BestellungFelder = new()
        {
            { "LieferantenNr", "Lieferantennr. *" },
            { "LieferantName", "Lieferant Name" },
            { "BestellNr", "Bestellnummer" },
            { "Bestelldatum", "Bestelldatum" },
            { "Liefertermin", "Liefertermin" },
            { "PZN", "PZN (Pharma)" },
            { "ArtikelNr", "Artikelnummer *" },
            { "ArtikelBarcode", "Artikel Barcode/EAN" },
            { "LieferantenArtikelNr", "Lieferanten-Artikelnr." },
            { "ArtikelName", "Artikel Name" },
            { "Hersteller", "Hersteller" },
            { "Menge", "Menge *" },
            { "EKPreis", "EK-Preis" },
            { "VKPreis", "VK-Preis" },
            { "MindestMHD", "Mindest-MHD" },
            { "Anmerkung", "Anmerkung" }
        };

        public ImportPage()
        {
            InitializeComponent();
            _importService = new ImportService(App.ConnectionString!);
            BuildSpaltenzuordnung();
        }

        private void DateiAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import-Datei auswaehlen",
                Filter = "Alle unterstuetzten|*.csv;*.xlsx;*.xls|CSV-Dateien|*.csv|Excel-Dateien|*.xlsx;*.xls",
                FilterIndex = 1
            };

            if (dlg.ShowDialog() == true)
            {
                txtDateiPfad.Text = dlg.FileName;
                LadeDateivorschau(dlg.FileName);
            }
        }

        private void LadeDateivorschau(string pfad)
        {
            try
            {
                txtStatus.Text = "Lade Datei...";
                var ext = Path.GetExtension(pfad).ToLower();
                var fileInfo = new FileInfo(pfad);
                txtDateiInfo.Text = $"{fileInfo.Name} ({fileInfo.Length / 1024.0:N0} KB)";

                // Trennzeichen und Encoding
                char trennzeichen = ';';
                if (cmbTrennzeichen.SelectedItem is ComboBoxItem ti)
                    trennzeichen = ti.Tag?.ToString()?[0] ?? ';';

                Encoding encoding = Encoding.UTF8;
                if (cmbEncoding.SelectedItem is ComboBoxItem ei)
                {
                    encoding = ei.Tag?.ToString() switch
                    {
                        "windows-1252" => Encoding.GetEncoding(1252),
                        "iso-8859-1" => Encoding.GetEncoding("ISO-8859-1"),
                        _ => Encoding.UTF8
                    };
                }

                bool hatHeader = chkHeaderZeile.IsChecked == true;

                if (ext == ".csv")
                {
                    _vorschauDaten = LeseCsvVorschau(pfad, trennzeichen, encoding, hatHeader, 100);
                }
                else
                {
                    // Excel - vereinfachte Implementierung
                    txtStatus.Text = "Excel-Import benoetigt ClosedXML (wird geladen...)";
                    _vorschauDaten = LeseExcelVorschau(pfad, hatHeader, 100);
                }

                if (_vorschauDaten != null)
                {
                    _dateiSpalten = _vorschauDaten.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                    dgVorschau.ItemsSource = _vorschauDaten.DefaultView;
                    txtVorschauInfo.Text = $"{_vorschauDaten.Rows.Count} Zeilen, {_dateiSpalten.Count} Spalten";

                    BuildSpaltenzuordnung();
                    AutoZuordnung();
                    btnImportieren.IsEnabled = true;
                    txtStatus.Text = "Datei geladen";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Datei:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private DataTable LeseCsvVorschau(string pfad, char trennzeichen, Encoding encoding, bool hatHeader, int maxZeilen)
        {
            var dt = new DataTable();
            var lines = File.ReadLines(pfad, encoding).Take(maxZeilen + 1).ToList();

            if (lines.Count == 0) return dt;

            // Header
            var headerLine = lines[0].Split(trennzeichen);
            for (int i = 0; i < headerLine.Length; i++)
            {
                var colName = hatHeader ? headerLine[i].Trim().Trim('"') : $"Spalte{i + 1}";
                if (string.IsNullOrEmpty(colName)) colName = $"Spalte{i + 1}";
                // Eindeutige Namen
                int suffix = 1;
                var baseName = colName;
                while (dt.Columns.Contains(colName))
                    colName = $"{baseName}_{suffix++}";
                dt.Columns.Add(colName);
            }

            // Daten
            int startRow = hatHeader ? 1 : 0;
            for (int i = startRow; i < lines.Count && i < maxZeilen + startRow; i++)
            {
                var values = ParseCsvLine(lines[i], trennzeichen);
                var row = dt.NewRow();
                for (int j = 0; j < Math.Min(values.Count, dt.Columns.Count); j++)
                    row[j] = values[j];
                dt.Rows.Add(row);
            }

            return dt;
        }

        private List<string> ParseCsvLine(string line, char trennzeichen)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == trennzeichen && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());
            return result;
        }

        private DataTable? LeseExcelVorschau(string pfad, bool hatHeader, int maxZeilen)
        {
            // Vereinfachte Excel-Implementierung - nutzt ClosedXML wenn verfügbar
            try
            {
                var dt = new DataTable();

                // Prüfen ob ClosedXML verfügbar ist
                var closedXmlType = Type.GetType("ClosedXML.Excel.XLWorkbook, ClosedXML");
                if (closedXmlType == null)
                {
                    MessageBox.Show("Fuer Excel-Import wird ClosedXML benoetigt.\n\nBitte CSV-Format verwenden oder ClosedXML installieren.",
                        "Excel nicht unterstuetzt", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                // Dynamisch laden
                dynamic workbook = Activator.CreateInstance(closedXmlType, pfad)!;
                dynamic worksheet = workbook.Worksheet(1);
                dynamic range = worksheet.RangeUsed();

                if (range == null)
                {
                    workbook.Dispose();
                    return dt;
                }

                int rowCount = Math.Min((int)range.RowCount(), maxZeilen + 1);
                int colCount = (int)range.ColumnCount();

                // Header
                for (int c = 1; c <= colCount; c++)
                {
                    string colName = hatHeader
                        ? worksheet.Cell(1, c).GetString() ?? $"Spalte{c}"
                        : $"Spalte{c}";
                    if (string.IsNullOrEmpty(colName)) colName = $"Spalte{c}";
                    int suffix = 1;
                    var baseName = colName;
                    while (dt.Columns.Contains(colName))
                        colName = $"{baseName}_{suffix++}";
                    dt.Columns.Add(colName);
                }

                // Daten
                int startRow = hatHeader ? 2 : 1;
                for (int r = startRow; r <= rowCount; r++)
                {
                    var row = dt.NewRow();
                    for (int c = 1; c <= colCount; c++)
                        row[c - 1] = worksheet.Cell(r, c).GetString() ?? "";
                    dt.Rows.Add(row);
                }

                workbook.Dispose();
                return dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Lesen der Excel-Datei:\n{ex.Message}",
                    "Excel-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private void BuildSpaltenzuordnung()
        {
            pnlSpaltenzuordnung.Children.Clear();
            _zuordnungsCombos.Clear();

            var felder = rbAuftrag.IsChecked == true ? AuftragFelder : BestellungFelder;

            foreach (var feld in felder)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var label = new TextBlock
                {
                    Text = feld.Value,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                var cmb = new ComboBox
                {
                    Height = 24,
                    FontSize = 11,
                    Tag = feld.Key
                };
                cmb.Items.Add(new ComboBoxItem { Content = "(nicht zuordnen)", Tag = "" });
                foreach (var spalte in _dateiSpalten)
                    cmb.Items.Add(new ComboBoxItem { Content = spalte, Tag = spalte });
                cmb.SelectedIndex = 0;

                Grid.SetColumn(cmb, 1);
                row.Children.Add(cmb);

                pnlSpaltenzuordnung.Children.Add(row);
                _zuordnungsCombos[feld.Key] = cmb;
            }
        }

        private void AutoZuordnung()
        {
            // Automatische Zuordnung basierend auf Spaltennamen
            var mappings = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "ExterneBestellnummer", new[] { "bestellnr", "bestellnummer", "order", "ordernr", "auftragsnr", "externe" } },
                { "KundenNr", new[] { "kundennr", "kdnr", "kundenummer", "customerno" } },
                { "KundeMail", new[] { "email", "e-mail", "mail", "kundemail" } },
                { "KundeFirma", new[] { "firma", "company", "kundefirma" } },
                { "KundeVorname", new[] { "vorname", "firstname", "vname" } },
                { "KundeNachname", new[] { "nachname", "name", "lastname", "nname" } },
                { "KundeStrasse", new[] { "strasse", "street", "adresse", "address" } },
                { "KundePLZ", new[] { "plz", "zip", "postleitzahl" } },
                { "KundeOrt", new[] { "ort", "city", "stadt" } },
                { "KundeLand", new[] { "land", "country" } },
                { "ArtikelNr", new[] { "artikelnr", "artnr", "sku", "artikelnummer", "productno" } },
                { "ArtikelBarcode", new[] { "ean", "barcode", "gtin", "upc" } },
                { "ArtikelName", new[] { "artikelname", "bezeichnung", "name", "description", "produktname" } },
                { "Menge", new[] { "menge", "anzahl", "qty", "quantity", "stueck", "ordermenge", "bestellmenge" } },
                { "Preis", new[] { "preis", "price", "vk", "einzelpreis" } },
                { "EKPreis", new[] { "ekpreis", "ek", "einkaufspreis", "cost", "aek", "aep" } },
                { "Bestelldatum", new[] { "datum", "date", "bestelldatum", "orderdate" } },
                { "LieferantenNr", new[] { "lieferantennr", "liefnr", "supplierno", "vendorno" } },
                { "LieferantName", new[] { "lieferant", "supplier", "vendor" } },
                { "PZN", new[] { "pzn", "pharmazentralnummer" } },
                { "Hersteller", new[] { "hersteller", "manufacturer", "producer" } },
                { "VKPreis", new[] { "vkpreis", "vk", "verkaufspreis", "sellingprice" } },
                { "MindestMHD", new[] { "mhd", "mindestverfall", "verfall", "expiry", "mindestmhd" } }
            };

            foreach (var mapping in mappings)
            {
                if (!_zuordnungsCombos.TryGetValue(mapping.Key, out var cmb)) continue;

                foreach (var keyword in mapping.Value)
                {
                    var match = _dateiSpalten.FirstOrDefault(s =>
                        s.Replace(" ", "").Replace("_", "").Replace("-", "")
                         .Contains(keyword, StringComparison.OrdinalIgnoreCase));

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
                        break;
                    }
                }
            }
        }

        private void AutoErkennung_Click(object sender, RoutedEventArgs e)
        {
            if (_dateiSpalten.Count == 0)
            {
                MessageBox.Show("Bitte zuerst eine Datei auswaehlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AutoZuordnung();
            txtStatus.Text = "Auto-Erkennung durchgefuehrt";
        }

        private void ImportTyp_Changed(object sender, RoutedEventArgs e)
        {
            BuildSpaltenzuordnung();
            if (_dateiSpalten.Count > 0)
                AutoZuordnung();
        }

        private void VorschauAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtDateiPfad.Text) && File.Exists(txtDateiPfad.Text))
                LadeDateivorschau(txtDateiPfad.Text);
        }

        private async void Importieren_Click(object sender, RoutedEventArgs e)
        {
            if (_vorschauDaten == null || _vorschauDaten.Rows.Count == 0)
            {
                MessageBox.Show("Keine Daten zum Importieren vorhanden.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Spaltenzuordnung sammeln
            var zuordnung = new Dictionary<string, string>();
            foreach (var kvp in _zuordnungsCombos)
            {
                if (kvp.Value.SelectedItem is ComboBoxItem item && !string.IsNullOrEmpty(item.Tag?.ToString()))
                    zuordnung[kvp.Key] = item.Tag.ToString()!;
            }

            // Pflichtfelder prüfen
            if (rbAuftrag.IsChecked == true)
            {
                if (!zuordnung.ContainsKey("ArtikelNr") && !zuordnung.ContainsKey("ArtikelBarcode"))
                {
                    MessageBox.Show("Bitte mindestens Artikelnummer oder Barcode zuordnen.", "Validierung",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                if (!zuordnung.ContainsKey("LieferantenNr") && !zuordnung.ContainsKey("LieferantName"))
                {
                    MessageBox.Show("Bitte Lieferantennummer oder -name zuordnen.", "Validierung",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            btnImportieren.IsEnabled = false;
            pbImport.Visibility = Visibility.Visible;
            txtStatus.Text = "Importiere...";

            try
            {
                var config = new ImportService.ImportKonfiguration
                {
                    Typ = rbAuftrag.IsChecked == true
                        ? ImportService.ImportTyp.Auftrag
                        : ImportService.ImportTyp.LieferantenBestellung,
                    DateiPfad = txtDateiPfad.Text,
                    ErsteZeileIstHeader = chkHeaderZeile.IsChecked == true,
                    Spaltenzuordnung = zuordnung
                };

                if (cmbTrennzeichen.SelectedItem is ComboBoxItem ti)
                    config.CsvTrennzeichen = ti.Tag?.ToString()?[0] ?? ';';

                ImportService.ImportErgebnis ergebnis;
                if (config.Typ == ImportService.ImportTyp.Auftrag)
                    ergebnis = await _importService.ImportiereAuftraegeAsync(config);
                else
                    ergebnis = await _importService.ImportiereLieferantenBestellungenAsync(config);

                // Ergebnis anzeigen
                var msg = $"Import abgeschlossen!\n\n" +
                          $"Zeilen gesamt: {ergebnis.AnzahlZeilen}\n" +
                          $"Erfolgreich: {ergebnis.AnzahlErfolgreich}\n" +
                          $"Fehler: {ergebnis.AnzahlFehler}\n" +
                          $"Uebersprungen: {ergebnis.AnzahlUebersprungen}";

                if (ergebnis.Fehler.Count > 0)
                {
                    msg += $"\n\nErste Fehler:\n";
                    foreach (var fehler in ergebnis.Fehler.Take(5))
                        msg += $"- Zeile {fehler.Zeile}: {fehler.Fehlertext}\n";
                }

                MessageBox.Show(msg, "Import-Ergebnis",
                    MessageBoxButton.OK,
                    ergebnis.AnzahlFehler > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                txtStatus.Text = $"Import: {ergebnis.AnzahlErfolgreich} erfolgreich, {ergebnis.AnzahlFehler} Fehler";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Import:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
            finally
            {
                btnImportieren.IsEnabled = true;
                pbImport.Visibility = Visibility.Collapsed;
            }
        }
    }
}
