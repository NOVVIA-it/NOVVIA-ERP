using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Services
{
    /// <summary>
    /// Service fuer Theme-Konfiguration (Farben, Schriften) - speichert in NOVVIA.Config
    /// </summary>
    public static class ThemeService
    {
        private const string KATEGORIE = "Theme";
        private static ThemeSettings _settings = new();

        public static ThemeSettings Settings => _settings;

        /// <summary>
        /// Laedt die Theme-Einstellungen aus der Datenbank
        /// </summary>
        public static async Task LoadSettingsAsync(string connectionString)
        {
            try
            {
                using var config = new ConfigService(connectionString);
                var werte = await config.GetAllAsync(KATEGORIE);

                if (werte.Count > 0)
                {
                    _settings = new ThemeSettings
                    {
                        PrimaryColor = werte.GetValueOrDefault("PrimaryColor", "#E86B5C"),
                        SecondaryColor = werte.GetValueOrDefault("SecondaryColor", "#6C757D"),
                        BackgroundColor = werte.GetValueOrDefault("BackgroundColor", "#FFFFFF"),
                        HeaderBackgroundColor = werte.GetValueOrDefault("HeaderBackgroundColor", "#F8F9FA"),
                        FilterBackgroundColor = werte.GetValueOrDefault("FilterBackgroundColor", "#F5F5F5"),
                        TextColor = werte.GetValueOrDefault("TextColor", "#212529"),
                        HeaderTextColor = werte.GetValueOrDefault("HeaderTextColor", "#1A1A1A"),
                        MutedTextColor = werte.GetValueOrDefault("MutedTextColor", "#6C757D"),
                        BorderColor = werte.GetValueOrDefault("BorderColor", "#DDDDDD"),
                        SuccessColor = werte.GetValueOrDefault("SuccessColor", "#28A745"),
                        WarningColor = werte.GetValueOrDefault("WarningColor", "#FFC107"),
                        DangerColor = werte.GetValueOrDefault("DangerColor", "#DC3545"),
                        InfoColor = werte.GetValueOrDefault("InfoColor", "#17A2B8"),
                        AlternateRowColor = werte.GetValueOrDefault("AlternateRowColor", "#FAFAFA"),
                        SelectedRowColor = werte.GetValueOrDefault("SelectedRowColor", "#E3F2FD")
                    };
                }
            }
            catch
            {
                _settings = new ThemeSettings();
            }

            ApplyTheme();
        }

        /// <summary>
        /// Speichert die Theme-Einstellungen in die Datenbank
        /// </summary>
        public static async Task SaveSettingsAsync(string connectionString)
        {
            try
            {
                using var config = new ConfigService(connectionString);

                var werte = new Dictionary<string, string>
                {
                    ["PrimaryColor"] = _settings.PrimaryColor,
                    ["SecondaryColor"] = _settings.SecondaryColor,
                    ["BackgroundColor"] = _settings.BackgroundColor,
                    ["HeaderBackgroundColor"] = _settings.HeaderBackgroundColor,
                    ["FilterBackgroundColor"] = _settings.FilterBackgroundColor,
                    ["TextColor"] = _settings.TextColor,
                    ["HeaderTextColor"] = _settings.HeaderTextColor,
                    ["MutedTextColor"] = _settings.MutedTextColor,
                    ["BorderColor"] = _settings.BorderColor,
                    ["SuccessColor"] = _settings.SuccessColor,
                    ["WarningColor"] = _settings.WarningColor,
                    ["DangerColor"] = _settings.DangerColor,
                    ["InfoColor"] = _settings.InfoColor,
                    ["AlternateRowColor"] = _settings.AlternateRowColor,
                    ["SelectedRowColor"] = _settings.SelectedRowColor
                };

                await config.SetAllAsync(KATEGORIE, werte);
            }
            catch { }
        }

        /// <summary>
        /// Wendet das aktuelle Theme auf die App an
        /// </summary>
        public static void ApplyTheme()
        {
            var resources = Application.Current.Resources;

            // Primaerfarbe (Buttons, Links, Akzente)
            resources["PrimaryColor"] = ParseColor(_settings.PrimaryColor);
            resources["PrimaryBrush"] = new SolidColorBrush(ParseColor(_settings.PrimaryColor));

            // Sekundaerfarbe
            resources["SecondaryColor"] = ParseColor(_settings.SecondaryColor);
            resources["SecondaryBrush"] = new SolidColorBrush(ParseColor(_settings.SecondaryColor));

            // Hintergrundfarben
            resources["BackgroundColor"] = ParseColor(_settings.BackgroundColor);
            resources["BackgroundBrush"] = new SolidColorBrush(ParseColor(_settings.BackgroundColor));

            resources["HeaderBackgroundColor"] = ParseColor(_settings.HeaderBackgroundColor);
            resources["HeaderBackgroundBrush"] = new SolidColorBrush(ParseColor(_settings.HeaderBackgroundColor));

            resources["FilterBackgroundColor"] = ParseColor(_settings.FilterBackgroundColor);
            resources["FilterBackgroundBrush"] = new SolidColorBrush(ParseColor(_settings.FilterBackgroundColor));

            // Textfarben
            resources["TextColor"] = ParseColor(_settings.TextColor);
            resources["TextBrush"] = new SolidColorBrush(ParseColor(_settings.TextColor));

            resources["HeaderTextColor"] = ParseColor(_settings.HeaderTextColor);
            resources["HeaderTextBrush"] = new SolidColorBrush(ParseColor(_settings.HeaderTextColor));

            resources["MutedTextColor"] = ParseColor(_settings.MutedTextColor);
            resources["MutedTextBrush"] = new SolidColorBrush(ParseColor(_settings.MutedTextColor));

            // Rahmenfarbe
            resources["BorderColor"] = ParseColor(_settings.BorderColor);
            resources["BorderBrush"] = new SolidColorBrush(ParseColor(_settings.BorderColor));

            // Status-Farben
            resources["SuccessColor"] = ParseColor(_settings.SuccessColor);
            resources["SuccessBrush"] = new SolidColorBrush(ParseColor(_settings.SuccessColor));

            resources["WarningColor"] = ParseColor(_settings.WarningColor);
            resources["WarningBrush"] = new SolidColorBrush(ParseColor(_settings.WarningColor));

            resources["DangerColor"] = ParseColor(_settings.DangerColor);
            resources["DangerBrush"] = new SolidColorBrush(ParseColor(_settings.DangerColor));

            resources["InfoColor"] = ParseColor(_settings.InfoColor);
            resources["InfoBrush"] = new SolidColorBrush(ParseColor(_settings.InfoColor));

            // DataGrid
            resources["AlternateRowColor"] = ParseColor(_settings.AlternateRowColor);
            resources["AlternateRowBrush"] = new SolidColorBrush(ParseColor(_settings.AlternateRowColor));

            resources["SelectedRowColor"] = ParseColor(_settings.SelectedRowColor);
            resources["SelectedRowBrush"] = new SolidColorBrush(ParseColor(_settings.SelectedRowColor));
        }

        private static Color ParseColor(string hex)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return Colors.Black;
            }
        }

        /// <summary>
        /// Setzt das Standard-Theme zurueck
        /// </summary>
        public static async Task ResetToDefaultAsync(string connectionString)
        {
            _settings = new ThemeSettings();
            ApplyTheme();
            await SaveSettingsAsync(connectionString);
        }
    }

    /// <summary>
    /// Theme-Einstellungen
    /// </summary>
    public class ThemeSettings
    {
        // Primaer- und Sekundaerfarben
        public string PrimaryColor { get; set; } = "#E86B5C";      // Microsoft Blue
        public string SecondaryColor { get; set; } = "#6C757D";    // Gray

        // Hintergruende
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string HeaderBackgroundColor { get; set; } = "#F8F9FA";
        public string FilterBackgroundColor { get; set; } = "#F5F5F5";

        // Text
        public string TextColor { get; set; } = "#212529";
        public string HeaderTextColor { get; set; } = "#1A1A1A";
        public string MutedTextColor { get; set; } = "#6C757D";

        // Rahmen
        public string BorderColor { get; set; } = "#DDDDDD";

        // Status-Farben
        public string SuccessColor { get; set; } = "#28A745";
        public string WarningColor { get; set; } = "#FFC107";
        public string DangerColor { get; set; } = "#DC3545";
        public string InfoColor { get; set; } = "#17A2B8";

        // DataGrid
        public string AlternateRowColor { get; set; } = "#FAFAFA";
        public string SelectedRowColor { get; set; } = "#E3F2FD";
    }
}
