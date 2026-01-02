using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Controls
{
    /// <summary>
    /// Helfer-Klasse für DataGrid Spalten-Konfiguration
    /// Ermöglicht Benutzern, Spalten ein-/auszublenden und speichert die Einstellungen
    /// Speichert in NOVVIA.BenutzerEinstellung Tabelle
    /// </summary>
    public static class DataGridColumnConfig
    {
        private static UserViewSettings? _settings;
        private static int? _currentUserId;
        private static CoreService? _core;
        private static DispatcherTimer? _saveTimer;
        private static readonly Dictionary<string, (DataGrid Grid, string ViewName)> _pendingSaves = new();
        private static bool _settingsLoaded = false;

        private static string SettingsKey => $"Spalten.AllViews";

        private static CoreService Core
        {
            get
            {
                _core ??= App.Services.GetRequiredService<CoreService>();
                return _core;
            }
        }

        private static UserViewSettings Settings
        {
            get
            {
                // Neu laden wenn Benutzer gewechselt hat
                if (_currentUserId != App.BenutzerId)
                {
                    _settings = null;
                    _currentUserId = App.BenutzerId;
                    _settingsLoaded = false;
                }
                if (_settings == null)
                {
                    _settings = new UserViewSettings();
                    // Async laden im Hintergrund
                    _ = LoadSettingsFromDbAsync();
                }
                return _settings;
            }
        }

        private static async Task LoadSettingsFromDbAsync()
        {
            if (_settingsLoaded) return;
            try
            {
                var json = await Core.GetBenutzerEinstellungAsync(App.BenutzerId, SettingsKey);
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonSerializer.Deserialize<UserViewSettings>(json);
                    if (loaded != null)
                    {
                        _settings = loaded;
                    }
                }
                _settingsLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Spalteneinstellungen laden: {ex.Message}");
            }
        }

        /// <summary>
        /// Wendet gespeicherte Spalten-Einstellungen auf ein DataGrid an
        /// </summary>
        public static void ApplySettings(DataGrid grid, string viewName)
        {
            // Sync-Wrapper - versucht sofort anzuwenden, lädt ggf. nach
            DoApplySettings(grid, viewName);
        }

        /// <summary>
        /// Wendet gespeicherte Spalten-Einstellungen auf ein DataGrid an (async)
        /// </summary>
        public static async Task ApplySettingsAsync(DataGrid grid, string viewName)
        {
            await LoadSettingsFromDbAsync();
            DoApplySettings(grid, viewName);
        }

        private static void DoApplySettings(DataGrid grid, string viewName)
        {
            if (_settings == null) return;

            var viewSettings = _settings.GetViewSettings(viewName);

            // Sortierung zurücksetzen
            grid.Items.SortDescriptions.Clear();

            // Sortierte Spalten sammeln
            var sortedColumns = new List<(DataGridColumn Column, string Header, ColumnSetting Settings)>();

            foreach (var column in grid.Columns)
            {
                var header = column.Header?.ToString();
                if (string.IsNullOrEmpty(header)) continue;

                // Sichtbarkeit
                var isVisible = _settings.GetColumnVisibility(viewName, header, column.Visibility == Visibility.Visible);
                column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

                // Spaltenbreite
                if (viewSettings.Columns.TryGetValue(header, out var colSettings) && colSettings.Width > 0)
                {
                    column.Width = new DataGridLength(colSettings.Width);
                }

                // DisplayIndex (Reihenfolge)
                if (colSettings != null && colSettings.DisplayIndex >= 0 && colSettings.DisplayIndex < grid.Columns.Count)
                {
                    try
                    {
                        column.DisplayIndex = colSettings.DisplayIndex;
                    }
                    catch { /* DisplayIndex Konflikte ignorieren */ }
                }

                // Sortierung merken
                if (colSettings != null && !string.IsNullOrEmpty(colSettings.SortDirection) && colSettings.SortOrder >= 0)
                {
                    sortedColumns.Add((column, header, colSettings));
                }
            }

            // Sortierung anwenden (in richtiger Reihenfolge)
            foreach (var (column, header, colSettings) in sortedColumns.OrderBy(x => x.Settings.SortOrder))
            {
                var sortPath = GetSortMemberPath(column);
                if (!string.IsNullOrEmpty(sortPath))
                {
                    var direction = colSettings.SortDirection == "Descending"
                        ? System.ComponentModel.ListSortDirection.Descending
                        : System.ComponentModel.ListSortDirection.Ascending;
                    grid.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription(sortPath, direction));
                    column.SortDirection = direction;
                }
            }
        }

        private static string? GetSortMemberPath(DataGridColumn column)
        {
            if (column is DataGridBoundColumn boundColumn && boundColumn.Binding is System.Windows.Data.Binding binding)
                return binding.Path?.Path;
            if (column is DataGridTextColumn textColumn && textColumn.Binding is System.Windows.Data.Binding textBinding)
                return textBinding.Path?.Path;
            return column.SortMemberPath;
        }

        /// <summary>
        /// Fügt Rechtsklick-Menü zum Ein-/Ausblenden von Spalten hinzu
        /// </summary>
        public static void EnableColumnChooser(DataGrid grid, string viewName)
        {
            // Async Einstellungen laden und anwenden
            _ = InitializeAsync(grid, viewName);
        }

        private static async Task InitializeAsync(DataGrid grid, string viewName)
        {
            // Warte bis Einstellungen geladen sind
            await LoadSettingsFromDbAsync();

            // Auf UI-Thread anwenden
            grid.Dispatcher.Invoke(() =>
            {
                // Wende gespeicherte Einstellungen an
                DoApplySettings(grid, viewName);

                // Kontext-Menü für Spalten-Header
                grid.ColumnHeaderStyle = CreateHeaderStyle(grid, viewName);

                // Spaltenbreiten speichern wenn geändert
                grid.ColumnReordered += (s, e) => SaveColumnWidths(grid, viewName);

                // Sortierung speichern wenn geändert
                grid.Sorting += (s, e) =>
                {
                    // Nach kurzer Verzögerung speichern (nachdem WPF die Sortierung angewendet hat)
                    grid.Dispatcher.BeginInvoke(new Action(() => SaveColumnWidths(grid, viewName)),
                        DispatcherPriority.Background);
                };

                // Breiten speichern nach dem Laden
                foreach (DataGridColumn col in grid.Columns)
                {
                    // Breiten-Änderung überwachen mittels DependencyProperty
                    var dpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                        DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
                    dpd?.AddValueChanged(col, (sender, args) => SaveColumnWidths(grid, viewName));
                }
            });
        }

        private static void SaveColumnWidths(DataGrid grid, string viewName)
        {
            // Debounce: Erst nach 500ms speichern
            _pendingSaves[viewName] = (grid, viewName);

            if (_saveTimer == null)
            {
                _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _saveTimer.Tick += (s, e) =>
                {
                    _saveTimer.Stop();
                    foreach (var (g, vn) in _pendingSaves.Values)
                    {
                        DoSaveColumnWidths(g, vn);
                    }
                    _pendingSaves.Clear();
                };
            }

            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private static void DoSaveColumnWidths(DataGrid grid, string viewName)
        {
            if (_settings == null) return;

            var viewSettings = _settings.GetViewSettings(viewName);

            // SortDescriptions zu Dictionary für schnellen Zugriff
            var sortDict = new Dictionary<string, (System.ComponentModel.ListSortDirection Direction, int Order)>();
            for (int i = 0; i < grid.Items.SortDescriptions.Count; i++)
            {
                var sd = grid.Items.SortDescriptions[i];
                sortDict[sd.PropertyName] = (sd.Direction, i);
            }

            foreach (var column in grid.Columns)
            {
                var header = column.Header?.ToString();
                if (string.IsNullOrEmpty(header)) continue;

                if (!viewSettings.Columns.ContainsKey(header))
                    viewSettings.Columns[header] = new ColumnSetting();

                viewSettings.Columns[header].Width = column.ActualWidth;
                viewSettings.Columns[header].IsVisible = column.Visibility == Visibility.Visible;
                viewSettings.Columns[header].DisplayIndex = column.DisplayIndex;

                // Sortierung speichern
                var sortPath = GetSortMemberPath(column);
                if (!string.IsNullOrEmpty(sortPath) && sortDict.TryGetValue(sortPath, out var sortInfo))
                {
                    viewSettings.Columns[header].SortDirection = sortInfo.Direction.ToString();
                    viewSettings.Columns[header].SortOrder = sortInfo.Order;
                }
                else
                {
                    viewSettings.Columns[header].SortDirection = null;
                    viewSettings.Columns[header].SortOrder = -1;
                }
            }
            SaveSettings();
        }

        private static Style CreateHeaderStyle(DataGrid grid, string viewName)
        {
            // WICHTIG: BasedOn verwenden um Standard-Sortierungs-Template zu erben!
            // TryFindResource statt FindResource um Exception zu vermeiden
            Style? baseStyle = null;
            try
            {
                baseStyle = Application.Current.TryFindResource(typeof(DataGridColumnHeader)) as Style;
            }
            catch { /* Theme nicht verfuegbar */ }

            var style = baseStyle != null
                ? new Style(typeof(DataGridColumnHeader), baseStyle)
                : new Style(typeof(DataGridColumnHeader));

            // Hole Einstellungen von GridStyleHelper
            var settings = GridStyleHelper.Instance.Settings;

            // Hintergrund aus Design-Einstellungen
            style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty,
                ParseBrush(settings.HeaderHintergrund)));

            // Textfarbe aus Design-Einstellungen
            style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty,
                ParseBrush(settings.HeaderTextfarbe)));

            // Schriftstaerke aus Design-Einstellungen
            style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty,
                ParseFontWeight(settings.HeaderSchriftstaerke)));

            // Padding
            style.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));

            // Rahmen aus Design-Einstellungen
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty,
                ParseBrush(settings.HeaderRahmenfarbe)));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty,
                new Thickness(0, 0, settings.HeaderRahmenstaerke, settings.HeaderRahmenstaerke)));

            // Kontextmenue fuer Spaltenauswahl
            var contextMenu = CreateColumnContextMenu(grid, viewName);
            style.Setters.Add(new Setter(DataGridColumnHeader.ContextMenuProperty, contextMenu));

            return style;
        }

        private static System.Windows.Media.SolidColorBrush ParseBrush(string colorString)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
            }
        }

        private static FontWeight ParseFontWeight(string weightString)
        {
            return weightString?.ToLower() switch
            {
                "bold" => FontWeights.Bold,
                "semibold" => FontWeights.SemiBold,
                _ => FontWeights.Normal
            };
        }

        private static ContextMenu CreateColumnContextMenu(DataGrid grid, string viewName)
        {
            var menu = new ContextMenu();

            menu.Opened += (s, e) =>
            {
                menu.Items.Clear();

                // Header
                var headerItem = new MenuItem { Header = "Spalten anzeigen:", IsEnabled = false };
                menu.Items.Add(headerItem);
                menu.Items.Add(new Separator());

                // Alle Spalten als Toggle
                foreach (var column in grid.Columns)
                {
                    var header = column.Header?.ToString();
                    if (string.IsNullOrEmpty(header)) continue;

                    var item = new MenuItem
                    {
                        Header = header,
                        IsCheckable = true,
                        IsChecked = column.Visibility == Visibility.Visible
                    };

                    var col = column; // Closure
                    item.Click += (sender, args) =>
                    {
                        var isVisible = item.IsChecked;
                        col.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                        Settings.SetColumnVisibility(viewName, header, isVisible);
                        SaveSettings();
                    };

                    menu.Items.Add(item);
                }

                menu.Items.Add(new Separator());

                // Alle anzeigen
                var showAllItem = new MenuItem { Header = "Alle anzeigen" };
                showAllItem.Click += (sender, args) =>
                {
                    foreach (var column in grid.Columns)
                    {
                        var h = column.Header?.ToString();
                        if (string.IsNullOrEmpty(h)) continue;
                        column.Visibility = Visibility.Visible;
                        Settings.SetColumnVisibility(viewName, h, true);
                    }
                    SaveSettings();
                };
                menu.Items.Add(showAllItem);

                // Zurücksetzen
                var resetItem = new MenuItem { Header = "Zurücksetzen" };
                resetItem.Click += (sender, args) =>
                {
                    var viewSettings = Settings.GetViewSettings(viewName);
                    viewSettings.Columns.Clear();
                    SaveSettings();
                    foreach (var column in grid.Columns)
                        column.Visibility = Visibility.Visible;
                };
                menu.Items.Add(resetItem);
            };

            return menu;
        }

        private static void SaveSettings()
        {
            // Async in DB speichern
            _ = SaveSettingsToDbAsync();
        }

        private static async Task SaveSettingsToDbAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings);
                await Core.SaveBenutzerEinstellungAsync(App.BenutzerId, SettingsKey, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Spalteneinstellungen speichern: {ex.Message}");
            }
        }

        /// <summary>
        /// Setzt Sichtbarkeit einer Spalte
        /// </summary>
        public static void SetColumnVisible(DataGrid grid, string viewName, string columnHeader, bool visible)
        {
            foreach (var column in grid.Columns)
            {
                if (column.Header?.ToString() == columnHeader)
                {
                    column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    Settings.SetColumnVisibility(viewName, columnHeader, visible);
                    SaveSettings();
                    break;
                }
            }
        }
    }
}
