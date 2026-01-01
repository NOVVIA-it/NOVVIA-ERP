using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

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
            }
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
            foreach (var column in grid.Columns)
            {
                var header = column.Header?.ToString();
                if (string.IsNullOrEmpty(header)) continue;

                if (!viewSettings.Columns.ContainsKey(header))
                    viewSettings.Columns[header] = new ColumnSetting();

                viewSettings.Columns[header].Width = column.ActualWidth;
                viewSettings.Columns[header].IsVisible = column.Visibility == Visibility.Visible;
                viewSettings.Columns[header].DisplayIndex = column.DisplayIndex;
            }
            SaveSettings();
        }

        private static Style CreateHeaderStyle(DataGrid grid, string viewName)
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, System.Windows.Media.Brushes.White));
            style.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(10, 8, 10, 8)));
            style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD))));
            style.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));

            var contextMenu = CreateColumnContextMenu(grid, viewName);
            style.Setters.Add(new Setter(DataGridColumnHeader.ContextMenuProperty, contextMenu));

            return style;
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
