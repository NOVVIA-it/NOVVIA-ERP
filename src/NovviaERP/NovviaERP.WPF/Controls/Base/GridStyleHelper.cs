using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Controls.Base
{
    /// <summary>
    /// Zentraler Helper fuer einheitliche DataGrid-Formatierung in der gesamten Anwendung.
    /// Laedt die Einstellungen aus NOVVIA.BenutzerEinstellung und wendet sie auf alle DataGrids an.
    /// </summary>
    public class GridStyleHelper
    {
        private static GridStyleHelper? _instance;
        private static readonly object _lock = new();

        private GridStyleSettings _settings = new();
        private bool _isLoaded = false;

        public static GridStyleHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new GridStyleHelper();
                    }
                }
                return _instance;
            }
        }

        public GridStyleSettings Settings => _settings;

        /// <summary>
        /// Laedt die Grid-Einstellungen aus der Datenbank
        /// </summary>
        public async System.Threading.Tasks.Task LoadSettingsAsync(CoreService core, int benutzerId)
        {
            try
            {
                var dbSettings = await core.GetBenutzerEinstellungenAsync(benutzerId, "Grid.");

                foreach (var kv in dbSettings)
                {
                    switch (kv.Key)
                    {
                        // Allgemein
                        case "Grid.Zeilenhoehe":
                            if (double.TryParse(kv.Value, out var rowHeight))
                                _settings.Zeilenhoehe = rowHeight;
                            break;
                        case "Grid.Schriftgroesse":
                            if (double.TryParse(kv.Value, out var fontSize))
                                _settings.Schriftgroesse = fontSize;
                            break;
                        case "Grid.Schriftart":
                            _settings.Schriftart = kv.Value;
                            break;
                        case "Grid.GridLines":
                            _settings.Gitternetzlinien = kv.Value;
                            break;

                        // Header
                        case "Grid.HeaderHoehe":
                            if (double.TryParse(kv.Value, out var headerHeight))
                                _settings.HeaderHoehe = headerHeight;
                            break;
                        case "Grid.HeaderBackground":
                            _settings.HeaderHintergrund = kv.Value;
                            break;
                        case "Grid.HeaderForeground":
                            _settings.HeaderTextfarbe = kv.Value;
                            break;
                        case "Grid.HeaderFontWeight":
                            _settings.HeaderSchriftstaerke = kv.Value;
                            break;
                        case "Grid.HeaderBorderColor":
                            _settings.HeaderRahmenfarbe = kv.Value;
                            break;
                        case "Grid.HeaderBorderWidth":
                            if (double.TryParse(kv.Value, out var headerBorderWidth))
                                _settings.HeaderRahmenstaerke = headerBorderWidth;
                            break;

                        // Zeile 1
                        case "Grid.RowBackground":
                            _settings.ZeileHintergrund = kv.Value;
                            break;
                        case "Grid.RowForeground":
                            _settings.ZeileTextfarbe = kv.Value;
                            break;
                        case "Grid.RowFontWeight":
                            _settings.ZeileSchriftstaerke = kv.Value;
                            break;
                        case "Grid.RowBorderColor":
                            _settings.ZeileRahmenfarbe = kv.Value;
                            break;
                        case "Grid.RowBorderWidth":
                            if (double.TryParse(kv.Value, out var rowBorderWidth))
                                _settings.ZeileRahmenstaerke = rowBorderWidth;
                            break;

                        // Zeile 2 (Alternierend)
                        case "Grid.RowAltBackground":
                            _settings.ZeileAltHintergrund = kv.Value;
                            break;
                        case "Grid.RowAltForeground":
                            _settings.ZeileAltTextfarbe = kv.Value;
                            break;
                        case "Grid.RowAltFontWeight":
                            _settings.ZeileAltSchriftstaerke = kv.Value;
                            break;
                        case "Grid.ZebraStreifen":
                            _settings.ZebraStreifen = kv.Value == "True";
                            break;

                        // Selektion
                        case "Grid.SelectionBackground":
                            _settings.SelektionHintergrund = kv.Value;
                            break;
                        case "Grid.SelectionForeground":
                            _settings.SelektionTextfarbe = kv.Value;
                            break;
                        case "Grid.SelectionNoFocusBackground":
                            _settings.SelektionOhneFokusHintergrund = kv.Value;
                            break;
                        case "Grid.SelectionNoFocusForeground":
                            _settings.SelektionOhneFokusTextfarbe = kv.Value;
                            break;

                        // Datumsformatierung
                        case "Grid.DatumFormat":
                            _settings.DatumFormat = kv.Value;
                            break;
                        case "Grid.DatumZeitFormat":
                            _settings.DatumZeitFormat = kv.Value;
                            break;
                        case "Grid.ZeitFormat":
                            _settings.ZeitFormat = kv.Value;
                            break;

                        // Zahlenformatierung
                        case "Grid.Tausendertrennzeichen":
                            _settings.Tausendertrennzeichen = kv.Value == "(kein)" ? "" : kv.Value;
                            break;
                        case "Grid.Dezimaltrennzeichen":
                            _settings.Dezimaltrennzeichen = kv.Value;
                            break;
                        case "Grid.Dezimalstellen":
                            if (int.TryParse(kv.Value, out var dez))
                                _settings.Dezimalstellen = dez;
                            break;
                        case "Grid.WaehrungSymbol":
                            _settings.WaehrungSymbol = kv.Value;
                            break;
                        case "Grid.WaehrungAnzeigen":
                            _settings.WaehrungSymbolAnzeigen = kv.Value == "True";
                            break;

                        // ========== SubGrid Einstellungen ==========
                        case "SubGrid.Zeilenhoehe":
                            if (double.TryParse(kv.Value, out var subZh))
                                _settings.SubGridZeilenhoehe = subZh;
                            break;
                        case "SubGrid.Schriftgroesse":
                            if (double.TryParse(kv.Value, out var subSg))
                                _settings.SubGridSchriftgroesse = subSg;
                            break;
                        case "SubGrid.GridLines":
                            _settings.SubGridGitternetzlinien = kv.Value;
                            break;
                        case "SubGrid.HeaderHoehe":
                            if (double.TryParse(kv.Value, out var subHh))
                                _settings.SubGridHeaderHoehe = subHh;
                            break;
                        case "SubGrid.HeaderBackground":
                            _settings.SubGridHeaderHintergrund = kv.Value;
                            break;
                        case "SubGrid.HeaderForeground":
                            _settings.SubGridHeaderTextfarbe = kv.Value;
                            break;
                        case "SubGrid.HeaderFontWeight":
                            _settings.SubGridHeaderSchriftstaerke = kv.Value;
                            break;
                        case "SubGrid.RowBackground":
                            _settings.SubGridZeileHintergrund = kv.Value;
                            break;
                        case "SubGrid.RowForeground":
                            _settings.SubGridZeileTextfarbe = kv.Value;
                            break;
                        case "SubGrid.RowAltBackground":
                            _settings.SubGridZeileAltHintergrund = kv.Value;
                            break;
                        case "SubGrid.ZebraStreifen":
                            _settings.SubGridZebraStreifen = kv.Value == "True";
                            break;
                        case "SubGrid.SelectionBackground":
                            _settings.SubGridSelektionHintergrund = kv.Value;
                            break;
                        case "SubGrid.SelectionForeground":
                            _settings.SubGridSelektionTextfarbe = kv.Value;
                            break;
                        case "SubGrid.SelectionNoFocusBackground":
                            _settings.SubGridSelektionOhneFokusHintergrund = kv.Value;
                            break;
                        case "SubGrid.SelectionNoFocusForeground":
                            _settings.SubGridSelektionOhneFokusTextfarbe = kv.Value;
                            break;
                        case "SubGrid.BorderColor":
                            _settings.SubGridRahmenfarbe = kv.Value;
                            break;
                        case "SubGrid.BorderWidth":
                            if (double.TryParse(kv.Value, out var subBw))
                                _settings.SubGridRahmenstaerke = subBw;
                            break;
                        case "SubGrid.CornerRadius":
                            if (double.TryParse(kv.Value, out var subCr))
                                _settings.SubGridEckenradius = subCr;
                            break;
                    }
                }

                _isLoaded = true;
            }
            catch
            {
                // Bei Fehler: Standardwerte beibehalten
            }
        }

        /// <summary>
        /// Wendet die Grid-Einstellungen auf ein DataGrid an
        /// WICHTIG: Diese Methode setzt NICHT den ColumnHeaderStyle, damit DataGridColumnConfig.EnableColumnChooser
        /// das Kontextmenue hinzufuegen kann. Verwende ApplyStyleWithHeader nur wenn kein Spalten-Chooser benoetigt wird.
        /// </summary>
        public void ApplyStyle(DataGrid dataGrid)
        {
            if (dataGrid == null) return;

            try
            {
                // Allgemein
                dataGrid.RowHeight = _settings.Zeilenhoehe;
                dataGrid.FontSize = _settings.Schriftgroesse;
                dataGrid.FontFamily = new FontFamily(_settings.Schriftart);
                dataGrid.ColumnHeaderHeight = _settings.HeaderHoehe;

                // Gitternetzlinien
                dataGrid.GridLinesVisibility = _settings.Gitternetzlinien switch
                {
                    "Horizontal" => DataGridGridLinesVisibility.Horizontal,
                    "Vertical" => DataGridGridLinesVisibility.Vertical,
                    "None" => DataGridGridLinesVisibility.None,
                    _ => DataGridGridLinesVisibility.All
                };

                // Rahmenfarbe fuer Gitternetzlinien
                var borderBrush = ParseBrush(_settings.ZeileRahmenfarbe);
                dataGrid.HorizontalGridLinesBrush = borderBrush;
                dataGrid.VerticalGridLinesBrush = borderBrush;

                // Zeilen-Hintergrund
                dataGrid.RowBackground = ParseBrush(_settings.ZeileHintergrund);

                // Zebrastreifen
                if (_settings.ZebraStreifen)
                {
                    dataGrid.AlternatingRowBackground = ParseBrush(_settings.ZeileAltHintergrund);
                }
                else
                {
                    dataGrid.AlternatingRowBackground = null;
                }

                // RowStyle fuer Textfarbe und Schriftstaerke
                var rowStyle = CreateRowStyle();
                dataGrid.RowStyle = rowStyle;

                // NICHT ColumnHeaderStyle setzen - das macht DataGridColumnConfig.EnableColumnChooser
                // mit dem Kontextmenue fuer Spaltenauswahl
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GridStyleHelper.ApplyStyle Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Wendet Grid-Einstellungen inklusive Header-Style an (nur verwenden wenn kein Spalten-Chooser benoetigt wird)
        /// </summary>
        public void ApplyStyleWithHeader(DataGrid dataGrid)
        {
            ApplyStyle(dataGrid);
            if (dataGrid != null)
            {
                dataGrid.ColumnHeaderStyle = CreateColumnHeaderStyle();
            }
        }

        /// <summary>
        /// Wendet SubGrid-Einstellungen an (fuer Positionen-Grids in Auftraegen, Rechnungen, etc.)
        /// Verwendet die SubGrid-spezifischen Einstellungen aus Design
        /// </summary>
        public void ApplySubGridStyle(DataGrid dataGrid)
        {
            if (dataGrid == null) return;

            try
            {
                // Schriftgroesse
                dataGrid.FontSize = _settings.SubGridSchriftgroesse;
                dataGrid.FontFamily = new System.Windows.Media.FontFamily(_settings.Schriftart);

                // Zeilenhoehe
                dataGrid.RowHeight = _settings.SubGridZeilenhoehe;

                // Header-Hoehe
                dataGrid.ColumnHeaderHeight = _settings.SubGridHeaderHoehe;

                // Gitternetzlinien
                dataGrid.GridLinesVisibility = _settings.SubGridGitternetzlinien switch
                {
                    "Horizontal" => DataGridGridLinesVisibility.Horizontal,
                    "Vertical" => DataGridGridLinesVisibility.Vertical,
                    "None" => DataGridGridLinesVisibility.None,
                    _ => DataGridGridLinesVisibility.All
                };

                // Rahmenfarbe fuer Gitternetzlinien
                var borderBrush = ParseBrush(_settings.SubGridRahmenfarbe);
                dataGrid.HorizontalGridLinesBrush = borderBrush;
                dataGrid.VerticalGridLinesBrush = borderBrush;

                // Zeilen-Hintergrund
                dataGrid.RowBackground = ParseBrush(_settings.SubGridZeileHintergrund);

                // Zebrastreifen
                if (_settings.SubGridZebraStreifen)
                {
                    dataGrid.AlternatingRowBackground = ParseBrush(_settings.SubGridZeileAltHintergrund);
                }

                // RowStyle mit SubGrid-Farben
                dataGrid.RowStyle = CreateSubGridRowStyle();

                // ColumnHeaderStyle mit SubGrid-Farben
                dataGrid.ColumnHeaderStyle = CreateSubGridColumnHeaderStyle();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GridStyleHelper.ApplySubGridStyle Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Erstellt den RowStyle fuer SubGrids mit SubGrid-spezifischen Farben
        /// </summary>
        private Style CreateSubGridRowStyle()
        {
            var style = new Style(typeof(DataGridRow));

            // Textfarbe
            style.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ParseBrush(_settings.SubGridZeileTextfarbe)));

            // Selektion Trigger (mit Fokus)
            var selectionTrigger = new Trigger
            {
                Property = DataGridRow.IsSelectedProperty,
                Value = true
            };
            selectionTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, ParseBrush(_settings.SubGridSelektionHintergrund)));
            selectionTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ParseBrush(_settings.SubGridSelektionTextfarbe)));
            style.Triggers.Add(selectionTrigger);

            // MultiTrigger: Selektion OHNE Fokus
            var noFocusTrigger = new MultiTrigger();
            noFocusTrigger.Conditions.Add(new Condition(DataGridRow.IsSelectedProperty, true));
            noFocusTrigger.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
            noFocusTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, ParseBrush(_settings.SubGridSelektionOhneFokusHintergrund)));
            noFocusTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ParseBrush(_settings.SubGridSelektionOhneFokusTextfarbe)));
            style.Triggers.Add(noFocusTrigger);

            return style;
        }

        /// <summary>
        /// Erstellt den ColumnHeaderStyle fuer SubGrids
        /// </summary>
        private Style CreateSubGridColumnHeaderStyle()
        {
            var style = new Style(typeof(DataGridColumnHeader));

            style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, ParseBrush(_settings.SubGridHeaderHintergrund)));
            style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, ParseBrush(_settings.SubGridHeaderTextfarbe)));
            style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, ParseFontWeight(_settings.SubGridHeaderSchriftstaerke)));
            style.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, ParseBrush(_settings.SubGridRahmenfarbe)));

            return style;
        }

        /// <summary>
        /// Gibt die SubGrid-Header-Hintergrundfarbe zurueck (fuer Border/Header ausserhalb des Grids)
        /// </summary>
        public System.Windows.Media.Brush GetSubGridHeaderBrush()
        {
            return ParseBrush(_settings.SubGridHeaderHintergrund);
        }

        /// <summary>
        /// Gibt die SubGrid-Header-Textfarbe zurueck
        /// </summary>
        public System.Windows.Media.Brush GetSubGridHeaderTextBrush()
        {
            return ParseBrush(_settings.SubGridHeaderTextfarbe);
        }

        /// <summary>
        /// Gibt die SubGrid-Rahmenfarbe zurueck
        /// </summary>
        public System.Windows.Media.Brush GetSubGridBorderBrush()
        {
            return ParseBrush(_settings.SubGridRahmenfarbe);
        }

        /// <summary>
        /// Gibt den SubGrid-Eckenradius zurueck
        /// </summary>
        public CornerRadius GetSubGridCornerRadius()
        {
            return new CornerRadius(_settings.SubGridEckenradius);
        }

        /// <summary>
        /// Erstellt den RowStyle mit Textfarbe, Schriftstaerke und Selektion
        /// WICHTIG: Enthaelt auch MultiTrigger fuer "Selektion ohne Fokus" damit die Markierung sichtbar bleibt
        /// </summary>
        private Style CreateRowStyle()
        {
            var style = new Style(typeof(DataGridRow));

            // Textfarbe
            style.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ParseBrush(_settings.ZeileTextfarbe)));

            // Schriftstaerke
            style.Setters.Add(new Setter(DataGridRow.FontWeightProperty, ParseFontWeight(_settings.ZeileSchriftstaerke)));

            // Selektion Trigger (mit Fokus)
            var selectionTrigger = new Trigger
            {
                Property = DataGridRow.IsSelectedProperty,
                Value = true
            };
            selectionTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, ParseBrush(_settings.SelektionHintergrund)));
            selectionTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ParseBrush(_settings.SelektionTextfarbe)));
            style.Triggers.Add(selectionTrigger);

            // MultiTrigger: Selektion OHNE Fokus - hellere Variante der Selektionsfarbe
            // Damit bleibt die Markierung sichtbar wenn ein anderes Grid/Control den Fokus hat
            var noFocusTrigger = new MultiTrigger();
            noFocusTrigger.Conditions.Add(new Condition(DataGridRow.IsSelectedProperty, true));
            noFocusTrigger.Conditions.Add(new Condition(Selector.IsSelectionActiveProperty, false));
            // Hellere Variante der Selektionsfarbe fuer "ohne Fokus"
            noFocusTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, ParseBrush(_settings.SelektionOhneFokusHintergrund)));
            noFocusTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, ParseBrush(_settings.SelektionOhneFokusTextfarbe)));
            style.Triggers.Add(noFocusTrigger);

            // Alternating Row Trigger (fuer Textfarbe bei Zebrastreifen)
            if (_settings.ZebraStreifen && _settings.ZeileAltTextfarbe != _settings.ZeileTextfarbe)
            {
                // Leider kein direkter Trigger fuer AlternatingRow - wird ueber AlternatingRowBackground gehandhabt
            }

            return style;
        }

        /// <summary>
        /// Erstellt den ColumnHeaderStyle
        /// </summary>
        private Style CreateColumnHeaderStyle()
        {
            var style = new Style(typeof(DataGridColumnHeader));

            style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, ParseBrush(_settings.HeaderHintergrund)));
            style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, ParseBrush(_settings.HeaderTextfarbe)));
            style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, ParseFontWeight(_settings.HeaderSchriftstaerke)));
            style.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, ParseBrush(_settings.HeaderRahmenfarbe)));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, _settings.HeaderRahmenstaerke, _settings.HeaderRahmenstaerke)));

            return style;
        }

        private SolidColorBrush ParseBrush(string colorString)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorString);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        private FontWeight ParseFontWeight(string weightString)
        {
            return weightString?.ToLower() switch
            {
                "bold" => FontWeights.Bold,
                "semibold" => FontWeights.SemiBold,
                _ => FontWeights.Normal
            };
        }

        /// <summary>
        /// Setzt die Settings zurueck auf Standard
        /// </summary>
        public void ResetToDefaults()
        {
            _settings = new GridStyleSettings();
        }

        /// <summary>
        /// ZENTRALE METHODE: Wendet Grid-Formatierung UND Spalten-Konfiguration in der richtigen Reihenfolge an.
        ///
        /// WICHTIG: Diese Methode IMMER verwenden statt ApplyStyle + EnableColumnChooser einzeln!
        ///
        /// Reihenfolge:
        /// 1. ApplyStyle - Zeilen, Schriftart, Farben (setzt NICHT ColumnHeaderStyle)
        /// 2. EnableColumnChooser - Header-Style mit Kontextmenue fuer Spaltenauswahl
        /// </summary>
        public static void InitializeGrid(DataGrid dataGrid, string configKey)
        {
            if (dataGrid == null) return;

            // 1. Grid-Styling anwenden (Zeilen, Schriftart, Farben - OHNE Header-Style)
            Instance.ApplyStyle(dataGrid);

            // 2. Spalten-Konfiguration mit Kontextmenue aktivieren (setzt Header-Style)
            DataGridColumnConfig.EnableColumnChooser(dataGrid, configKey);
        }

        /// <summary>
        /// ZENTRALE METHODE mit async Laden: Laedt Settings und wendet alles an.
        ///
        /// Verwendung in Page/View:
        /// await GridStyleHelper.InitializeGridAsync(dgKunden, "KundenView", _core, App.BenutzerId);
        /// </summary>
        public static async System.Threading.Tasks.Task InitializeGridAsync(
            DataGrid dataGrid,
            string configKey,
            CoreService core,
            int benutzerId)
        {
            if (dataGrid == null) return;

            // 1. Settings aus DB laden (nur einmal pro Session noetig, aber schadet nicht)
            await Instance.LoadSettingsAsync(core, benutzerId);

            // 2. Grid initialisieren
            InitializeGrid(dataGrid, configKey);
        }
    }

    /// <summary>
    /// Einstellungen fuer Grid-Formatierung
    /// </summary>
    public class GridStyleSettings
    {
        // Allgemein
        public double Zeilenhoehe { get; set; } = 18;  // Standard: sehr kompakt
        public double Schriftgroesse { get; set; } = 11;
        public string Schriftart { get; set; } = "Segoe UI";
        public string Gitternetzlinien { get; set; } = "Horizontal";  // Nur horizontale Linien

        // Header
        public double HeaderHoehe { get; set; } = 22;
        public string HeaderHintergrund { get; set; } = "#f0f0f0";
        public string HeaderTextfarbe { get; set; } = "#333333";
        public string HeaderSchriftstaerke { get; set; } = "SemiBold";
        public string HeaderRahmenfarbe { get; set; } = "#cccccc";
        public double HeaderRahmenstaerke { get; set; } = 1;

        // Zeile 1 (Gerade)
        public string ZeileHintergrund { get; set; } = "#ffffff";
        public string ZeileTextfarbe { get; set; } = "#333333";
        public string ZeileSchriftstaerke { get; set; } = "Normal";
        public string ZeileRahmenfarbe { get; set; } = "#e0e0e0";
        public double ZeileRahmenstaerke { get; set; } = 1;

        // Zeile 2 (Alternierend)
        public string ZeileAltHintergrund { get; set; } = "#f9f9f9";
        public string ZeileAltTextfarbe { get; set; } = "#333333";
        public string ZeileAltSchriftstaerke { get; set; } = "Normal";
        public bool ZebraStreifen { get; set; } = true;

        // Selektion (mit Fokus)
        public string SelektionHintergrund { get; set; } = "#E86B5C";
        public string SelektionTextfarbe { get; set; } = "#ffffff";
        // Selektion (ohne Fokus) - hellere Variante damit Markierung sichtbar bleibt
        public string SelektionOhneFokusHintergrund { get; set; } = "#B3D9FF";
        public string SelektionOhneFokusTextfarbe { get; set; } = "#000000";

        // Status-Farben (fuer farbige Status-Badges, Ampeln, etc.)
        public string StatusErfolg { get; set; } = "#28a745";       // Gruen - OK, Abgeschlossen, Freigegeben
        public string StatusWarnung { get; set; } = "#ffc107";      // Gelb - Warnung, Bald ablaufend, Gesperrt
        public string StatusFehler { get; set; } = "#dc3545";       // Rot - Fehler, Abgelaufen, Kritisch
        public string StatusInfo { get; set; } = "#17a2b8";         // Tuerkis - Info, In Bearbeitung
        public string StatusNeutral { get; set; } = "#6c757d";      // Grau - Inaktiv, Unbekannt

        // Wichtige Zahlen (Bestand, Summen, Betraege)
        public string ZahlenSchriftstaerke { get; set; } = "SemiBold";

        // Datumsformatierung
        public string DatumFormat { get; set; } = "dd.MM.yyyy";
        public string DatumZeitFormat { get; set; } = "dd.MM.yyyy HH:mm";
        public string ZeitFormat { get; set; } = "HH:mm";

        // Zahlenformatierung
        public string WaehrungFormat { get; set; } = "N2";      // 1.234,56
        public string MengeFormat { get; set; } = "N0";          // 1.234
        public string ProzentFormat { get; set; } = "N2";        // 12,34
        public string PreisFormat { get; set; } = "N4";          // 12,3456 (fuer Netto-EK)
        public int Dezimalstellen { get; set; } = 2;
        public string Tausendertrennzeichen { get; set; } = ".";
        public string Dezimaltrennzeichen { get; set; } = ",";

        // ========== SubGrid (Positionen) Einstellungen ==========
        // Fuer eingebettete Grids wie Positionen in Auftraegen, Rechnungen, etc.

        // SubGrid Allgemein
        public double SubGridZeilenhoehe { get; set; } = 24;
        public double SubGridSchriftgroesse { get; set; } = 11;
        public string SubGridGitternetzlinien { get; set; } = "Horizontal";

        // SubGrid Header
        public double SubGridHeaderHoehe { get; set; } = 28;
        public string SubGridHeaderHintergrund { get; set; } = "#d9534f";  // Rot (wie bisher)
        public string SubGridHeaderTextfarbe { get; set; } = "#ffffff";
        public string SubGridHeaderSchriftstaerke { get; set; } = "SemiBold";

        // SubGrid Zeilen
        public string SubGridZeileHintergrund { get; set; } = "#ffffff";
        public string SubGridZeileTextfarbe { get; set; } = "#333333";
        public string SubGridZeileAltHintergrund { get; set; } = "#fafafa";
        public bool SubGridZebraStreifen { get; set; } = true;

        // SubGrid Selektion
        public string SubGridSelektionHintergrund { get; set; } = "#5bc0de";  // Info-Blau
        public string SubGridSelektionTextfarbe { get; set; } = "#ffffff";
        public string SubGridSelektionOhneFokusHintergrund { get; set; } = "#d9edf7";
        public string SubGridSelektionOhneFokusTextfarbe { get; set; } = "#333333";

        // SubGrid Rahmen
        public string SubGridRahmenfarbe { get; set; } = "#ddd";
        public double SubGridRahmenstaerke { get; set; } = 1;
        public double SubGridEckenradius { get; set; } = 4;
        public string WaehrungSymbol { get; set; } = "EUR";
        public bool WaehrungSymbolAnzeigen { get; set; } = true;
    }

    /// <summary>
    /// Statischer Helper fuer Formatierung von Datum und Zahlen
    /// </summary>
    public static class FormatHelper
    {
        private static GridStyleSettings Settings => GridStyleHelper.Instance.Settings;

        /// <summary>
        /// Formatiert ein Datum
        /// </summary>
        public static string FormatDatum(DateTime? datum)
        {
            if (!datum.HasValue) return "";
            return datum.Value.ToString(Settings.DatumFormat);
        }

        /// <summary>
        /// Formatiert Datum mit Zeit
        /// </summary>
        public static string FormatDatumZeit(DateTime? datum)
        {
            if (!datum.HasValue) return "";
            return datum.Value.ToString(Settings.DatumZeitFormat);
        }

        /// <summary>
        /// Formatiert nur Zeit
        /// </summary>
        public static string FormatZeit(DateTime? datum)
        {
            if (!datum.HasValue) return "";
            return datum.Value.ToString(Settings.ZeitFormat);
        }

        /// <summary>
        /// Formatiert einen Waehrungsbetrag
        /// </summary>
        public static string FormatWaehrung(decimal? betrag)
        {
            if (!betrag.HasValue) return "";
            var formatted = betrag.Value.ToString(Settings.WaehrungFormat, GetCulture());
            return Settings.WaehrungSymbolAnzeigen ? $"{formatted} {Settings.WaehrungSymbol}" : formatted;
        }

        /// <summary>
        /// Formatiert eine Menge (Ganzzahl)
        /// </summary>
        public static string FormatMenge(decimal? menge)
        {
            if (!menge.HasValue) return "";
            return menge.Value.ToString(Settings.MengeFormat, GetCulture());
        }

        /// <summary>
        /// Formatiert einen Prozentwert
        /// </summary>
        public static string FormatProzent(decimal? prozent)
        {
            if (!prozent.HasValue) return "";
            return $"{prozent.Value.ToString(Settings.ProzentFormat, GetCulture())} %";
        }

        /// <summary>
        /// Formatiert einen Preis (fuer hohe Praezision wie Netto-EK)
        /// </summary>
        public static string FormatPreis(decimal? preis)
        {
            if (!preis.HasValue) return "";
            return preis.Value.ToString(Settings.PreisFormat, GetCulture());
        }

        /// <summary>
        /// Formatiert eine beliebige Dezimalzahl
        /// </summary>
        public static string FormatDezimal(decimal? wert, int? dezimalstellen = null)
        {
            if (!wert.HasValue) return "";
            var stellen = dezimalstellen ?? Settings.Dezimalstellen;
            return wert.Value.ToString($"N{stellen}", GetCulture());
        }

        /// <summary>
        /// Gibt das konfigurierte CultureInfo zurueck
        /// </summary>
        private static System.Globalization.CultureInfo GetCulture()
        {
            var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
            culture.NumberFormat.NumberGroupSeparator = Settings.Tausendertrennzeichen;
            culture.NumberFormat.NumberDecimalSeparator = Settings.Dezimaltrennzeichen;
            return culture;
        }

        /// <summary>
        /// Erstellt einen StringFormat-String fuer WPF Bindings
        /// </summary>
        public static string GetBindingFormat(string typ)
        {
            return typ.ToLower() switch
            {
                "datum" => $"{{0:{Settings.DatumFormat}}}",
                "datumzeit" => $"{{0:{Settings.DatumZeitFormat}}}",
                "zeit" => $"{{0:{Settings.ZeitFormat}}}",
                "waehrung" => Settings.WaehrungSymbolAnzeigen ? $"{{0:{Settings.WaehrungFormat}}} {Settings.WaehrungSymbol}" : $"{{0:{Settings.WaehrungFormat}}}",
                "menge" => $"{{0:{Settings.MengeFormat}}}",
                "prozent" => $"{{0:{Settings.ProzentFormat}}} %",
                "preis" => $"{{0:{Settings.PreisFormat}}}",
                _ => $"{{0:N{Settings.Dezimalstellen}}}"
            };
        }
    }
}
