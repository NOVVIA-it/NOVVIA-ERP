using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Controls
{
    /// <summary>
    /// Helfer-Klasse für DataGrid Spalten-Konfiguration
    /// Ermöglicht Benutzern, Spalten ein-/auszublenden und speichert die Einstellungen
    /// </summary>
    public static class DataGridColumnConfig
    {
        private static UserViewSettings? _settings;
        private static int? _currentUserId;
        private static AppDataService? _appData;

        private static string SettingsKey => $"view_settings_{App.BenutzerId}";

        private static AppDataService AppData
        {
            get
            {
                _appData ??= App.Services.GetRequiredService<AppDataService>();
                return _appData;
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
                }
                _settings ??= AppData.Load<UserViewSettings>(SettingsKey) ?? new UserViewSettings();
                return _settings;
            }
        }

        /// <summary>
        /// Wendet gespeicherte Spalten-Einstellungen auf ein DataGrid an
        /// </summary>
        public static void ApplySettings(DataGrid grid, string viewName)
        {
            foreach (var column in grid.Columns)
            {
                var header = column.Header?.ToString();
                if (string.IsNullOrEmpty(header)) continue;

                var isVisible = Settings.GetColumnVisibility(viewName, header, true);
                column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Fügt Rechtsklick-Menü zum Ein-/Ausblenden von Spalten hinzu
        /// </summary>
        public static void EnableColumnChooser(DataGrid grid, string viewName)
        {
            // Wende gespeicherte Einstellungen an
            ApplySettings(grid, viewName);

            // Kontext-Menü für Spalten-Header
            grid.ColumnHeaderStyle = CreateHeaderStyle(grid, viewName);
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
            AppData.Save(SettingsKey, Settings);
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
