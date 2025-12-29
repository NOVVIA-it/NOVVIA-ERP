using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class AuftragsstapelimportView : Window
    {
        private readonly CoreService _coreService;
        private ObservableCollection<ImportPosition> _positionen = new();
        private int? _selectedKundeId;
        private string _selectedKundeNr = "";

        public AuftragsstapelimportView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            dgPositionen.ItemsSource = _positionen;
        }

        private async void KundeAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new KundeSucheDialog();
            if (dialog.ShowDialog() == true && dialog.SelectedKunde != null)
            {
                _selectedKundeId = dialog.SelectedKunde.KKunde;
                _selectedKundeNr = dialog.SelectedKunde.CKundenNr ?? _selectedKundeId.ToString()!;

                txtSelectedKundeName.Text = $"{_selectedKundeNr} - {dialog.SelectedKunde.Anzeigename}";
                txtSelectedKundeDetails.Text = $"{dialog.SelectedKunde.CPLZ} {dialog.SelectedKunde.COrt} {dialog.SelectedKunde.CISO}";
                btnKundeEntfernen.Visibility = Visibility.Visible;

                // Positionen ohne AdressNr aktualisieren
                foreach (var pos in _positionen.Where(p => string.IsNullOrEmpty(p.AdressNr)))
                {
                    pos.AdressNr = _selectedKundeNr;
                    pos.KundeName = txtSelectedKundeName.Text;
                }
                dgPositionen.Items.Refresh();
            }
        }

        private void KundeEntfernen_Click(object sender, RoutedEventArgs e)
        {
            _selectedKundeId = null;
            _selectedKundeNr = "";
            txtSelectedKundeName.Text = "(kein Kunde ausgewaehlt)";
            txtSelectedKundeDetails.Text = "";
            btnKundeEntfernen.Visibility = Visibility.Collapsed;

            // Positionen ohne eigene AdressNr zuruecksetzen
            foreach (var pos in _positionen.Where(p => p.OriginalAdressNr == ""))
            {
                pos.AdressNr = "";
                pos.KundeName = "";
            }
            dgPositionen.Items.Refresh();
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
                        // Excel mit ClosedXML laden
                        importedItems = await LoadExcelAsync(dialog.FileName);
                    }

                    foreach (var item in importedItems)
                    {
                        // Kunde und Artikel nachschlagen
                        await EnrichImportPosition(item);
                        _positionen.Add(item);
                    }

                    txtPositionenCount.Text = _positionen.Count.ToString();
                    btnSpeichern.IsEnabled = _positionen.Any();
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

        private async System.Threading.Tasks.Task<List<ImportPosition>> LoadExcelAsync(string filePath)
        {
            var result = new List<ImportPosition>();

            await System.Threading.Tasks.Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1); // Erstes Blatt

                // Spalten-Mapping ermitteln (flexible Header-Erkennung)
                var headerRow = worksheet.Row(1);
                var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 10;
                for (int col = 1; col <= lastCol; col++)
                {
                    var header = headerRow.Cell(col).GetString().Trim();
                    if (!string.IsNullOrEmpty(header))
                        columnMap[header] = col;
                }

                // Spalten-Indizes ermitteln (mit Fallbacks)
                int colAdressNr = FindColumn(columnMap, "AdressNr", "Adress-Nr", "Adresse", "KundenNr", "Kundennummer", "Kunde");
                int colArtNr = FindColumn(columnMap, "ArtNr", "Art-Nr", "Artikelnummer", "Artikel", "PZN", "EAN");
                int colMenge = FindColumn(columnMap, "Menge", "Anzahl", "Qty", "Quantity", "Stueck");
                int colPreis = FindColumn(columnMap, "Preis", "VK", "VKNetto", "Einzelpreis", "Price");

                // AdressNr ist jetzt optional (kann oben manuell ausgewaehlt werden)
                if (colArtNr == 0 || colMenge == 0)
                {
                    throw new Exception(
                        "Excel-Datei muss mindestens folgende Spalten enthalten:\n" +
                        "- ArtNr (oder: Artikelnummer, PZN, EAN)\n" +
                        "- Menge (oder: Anzahl, Qty)\n\n" +
                        "Optional: AdressNr, Preis");
                }

                // Daten ab Zeile 2 lesen
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                for (int row = 2; row <= lastRow; row++)
                {
                    var wsRow = worksheet.Row(row);

                    var adressNr = colAdressNr > 0 ? wsRow.Cell(colAdressNr).GetString().Trim() : "";
                    var artNr = wsRow.Cell(colArtNr).GetString().Trim();

                    // Leere Zeilen ueberspringen (ArtNr muss vorhanden sein)
                    if (string.IsNullOrWhiteSpace(artNr))
                        continue;

                    var pos = new ImportPosition
                    {
                        AdressNr = adressNr,
                        OriginalAdressNr = adressNr,  // Merken ob aus Excel
                        ArtNr = artNr,
                        Menge = GetDecimalValue(wsRow.Cell(colMenge), 1),
                        Preis = colPreis > 0 ? GetDecimalValue(wsRow.Cell(colPreis), 0) : 0
                    };

                    result.Add(pos);
                }
            });

            return result;
        }

        private int FindColumn(Dictionary<string, int> columnMap, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (columnMap.TryGetValue(name, out int col))
                    return col;
            }
            return 0;
        }

        private decimal GetDecimalValue(IXLCell cell, decimal defaultValue)
        {
            try
            {
                if (cell.IsEmpty()) return defaultValue;

                // Versuche direkt als Zahl zu lesen
                if (cell.DataType == XLDataType.Number)
                    return (decimal)cell.GetDouble();

                // Sonst als Text parsen
                var text = cell.GetString().Trim().Replace(',', '.');
                if (decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                    return val;

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private async System.Threading.Tasks.Task EnrichImportPosition(ImportPosition pos)
        {
            try
            {
                // Falls keine AdressNr aus Excel, den manuell ausgewaehlten Kunden verwenden
                if (string.IsNullOrEmpty(pos.AdressNr) && !string.IsNullOrEmpty(_selectedKundeNr))
                {
                    pos.AdressNr = _selectedKundeNr;
                    pos.KundeName = txtSelectedKundeName.Text;
                }

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

                // Kunde Name laden wenn noch nicht gesetzt
                if (string.IsNullOrEmpty(pos.KundeName) && !string.IsNullOrEmpty(pos.AdressNr))
                {
                    pos.KundeName = $"Kunde {pos.AdressNr}";
                }
            }
            catch
            {
                // Fehler ignorieren, Position trotzdem hinzufuegen
            }
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            await SpeichernAsync();
        }

        private async System.Threading.Tasks.Task SpeichernAsync()
        {
            if (!_positionen.Any())
            {
                MessageBox.Show("Keine Positionen zum Speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnSpeichern.IsEnabled = false;
                pbProgress.Visibility = Visibility.Visible;
                pbProgress.Maximum = _positionen.Count;
                pbProgress.Value = 0;

                // Mindest-MHD berechnen
                var mindestMHD = GetMindestMHD();

                // Positionen nach Kunde gruppieren (ein Auftrag pro Kunde)
                var gruppiertNachKunde = _positionen.GroupBy(p => p.AdressNr).ToList();
                int erstellteAuftraege = 0;

                int gesamtPositionen = 0;
                var alleNichtGefunden = new List<string>();

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
                        var result = await _coreService.CreateAuftragFromImportAsync(
                            kundenGruppe.Key,
                            corePositionen,
                            txtZusatztext.Text,
                            rbUeberPositionen.IsChecked == true,
                            mindestMHD);

                        if (result.AuftragId > 0)
                        {
                            erstellteAuftraege++;
                            gesamtPositionen += result.PositionenAngelegt;
                        }

                        if (result.NichtGefundeneArtikel.Any())
                        {
                            alleNichtGefunden.AddRange(result.NichtGefundeneArtikel);
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

                // Erfolgsmeldung mit Details
                var msg = $"{erstellteAuftraege} Auftraege mit {gesamtPositionen} Positionen erstellt!";
                if (alleNichtGefunden.Any())
                {
                    msg += $"\n\nNicht gefundene Artikel ({alleNichtGefunden.Count}):\n" +
                           string.Join(", ", alleNichtGefunden.Take(20));
                    if (alleNichtGefunden.Count > 20)
                        msg += $"\n... und {alleNichtGefunden.Count - 20} weitere";
                }
                MessageBox.Show(msg, "Ergebnis", MessageBoxButton.OK,
                    alleNichtGefunden.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);

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
        public string OriginalAdressNr { get; set; } = "";  // Speichert ob aus Excel oder manuell
        public string KundeName { get; set; } = "";
        public string ArtNr { get; set; } = "";
        public string ArtikelName { get; set; } = "";
        public decimal Menge { get; set; }
        public decimal Preis { get; set; }
        public DateTime? MindestMHD { get; set; }
    }

    /// <summary>
    /// Dialog zur Kundensuche und -auswahl (mit Suchfeld)
    /// </summary>
    public class KundeSucheDialog : Window
    {
        public CoreService.KundeUebersicht? SelectedKunde { get; private set; }
        private readonly CoreService _coreService;
        private TextBox _txtSuche;
        private ListBox _list;
        private TextBlock _txtStatus;

        public KundeSucheDialog()
        {
            _coreService = App.Services.GetRequiredService<CoreService>();

            Title = "Kunde suchen";
            Width = 550;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Suchzeile
            var searchPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _txtSuche = new TextBox { Width = 350, Padding = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center };
            _txtSuche.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) Suchen(); };
            var btnSuchen = new Button { Content = "Suchen", Padding = new Thickness(15, 5, 15, 5), Margin = new Thickness(10, 0, 0, 0) };
            btnSuchen.Click += (s, e) => Suchen();
            searchPanel.Children.Add(_txtSuche);
            searchPanel.Children.Add(btnSuchen);
            Grid.SetRow(searchPanel, 0);
            mainGrid.Children.Add(searchPanel);

            // Ergebnisliste
            _list = new ListBox();
            _list.MouseDoubleClick += (s, e) => Select();
            _list.ItemTemplate = CreateKundeTemplate();
            Grid.SetRow(_list, 1);
            mainGrid.Children.Add(_list);

            // Status
            _txtStatus = new TextBlock { Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(_txtStatus, 2);
            mainGrid.Children.Add(_txtStatus);

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnCancel = new Button { Content = "Abbrechen", Padding = new Thickness(15, 5, 15, 5), Margin = new Thickness(0, 0, 10, 0) };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            var btnOk = new Button { Content = "Auswaehlen", Padding = new Thickness(15, 5, 15, 5), Background = System.Windows.Media.Brushes.Green, Foreground = System.Windows.Media.Brushes.White };
            btnOk.Click += (s, e) => Select();
            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 3);
            mainGrid.Children.Add(btnPanel);

            Content = mainGrid;
            Loaded += (s, e) => _txtSuche.Focus();
        }

        private DataTemplate CreateKundeTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.MarginProperty, new Thickness(5));

            // Zeile 1: Kundennummer - Name
            var line1 = new FrameworkElementFactory(typeof(TextBlock));
            line1.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding { StringFormat = "{0}" });
            line1.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            // MultiBinding fuer Kundennummer + Name
            var mb = new System.Windows.Data.MultiBinding { StringFormat = "{0} - {1}" };
            mb.Bindings.Add(new System.Windows.Data.Binding("CKundenNr"));
            mb.Bindings.Add(new System.Windows.Data.Binding("Anzeigename"));
            line1.SetBinding(TextBlock.TextProperty, mb);

            // Zeile 2: PLZ Ort Land
            var line2 = new FrameworkElementFactory(typeof(TextBlock));
            line2.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("AdressZeile"));
            line2.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
            line2.SetValue(TextBlock.FontSizeProperty, 11.0);

            factory.AppendChild(line1);
            factory.AppendChild(line2);
            template.VisualTree = factory;
            return template;
        }

        private async void Suchen()
        {
            var suchbegriff = _txtSuche.Text.Trim();
            if (string.IsNullOrEmpty(suchbegriff))
            {
                _txtStatus.Text = "Bitte Suchbegriff eingeben (Name, Nr, PLZ, Ort)";
                return;
            }

            try
            {
                _txtStatus.Text = "Suche...";
                var kunden = await _coreService.SearchKundenAsync(suchbegriff, 50);
                _list.ItemsSource = kunden;
                _txtStatus.Text = $"{kunden.Count} Kunde(n) gefunden";

                if (kunden.Count == 1)
                {
                    _list.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Select()
        {
            SelectedKunde = _list.SelectedItem as CoreService.KundeUebersicht;
            if (SelectedKunde != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
