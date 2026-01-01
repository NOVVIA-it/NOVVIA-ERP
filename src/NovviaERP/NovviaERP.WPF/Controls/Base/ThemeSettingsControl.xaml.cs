using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NovviaERP.WPF.Services;

namespace NovviaERP.WPF.Controls.Base
{
    public partial class ThemeSettingsControl : UserControl
    {
        private bool _isLoading = false;

        public ThemeSettingsControl()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            _isLoading = true;

            var settings = ThemeService.Settings;

            txtPrimaryColor.Text = settings.PrimaryColor;
            txtSecondaryColor.Text = settings.SecondaryColor;
            txtHeaderBg.Text = settings.HeaderBackgroundColor;
            txtFilterBg.Text = settings.FilterBackgroundColor;
            txtBorderColor.Text = settings.BorderColor;
            txtSuccessColor.Text = settings.SuccessColor;
            txtWarningColor.Text = settings.WarningColor;
            txtDangerColor.Text = settings.DangerColor;
            txtInfoColor.Text = settings.InfoColor;
            txtAlternateRow.Text = settings.AlternateRowColor;
            txtSelectedRow.Text = settings.SelectedRowColor;

            UpdateColorPreviews();
            _isLoading = false;
        }

        private void Color_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            UpdateColorPreviews();
        }

        private void UpdateColorPreviews()
        {
            UpdatePreview(txtPrimaryColor, rectPrimary);
            UpdatePreview(txtSecondaryColor, rectSecondary);
            UpdatePreview(txtHeaderBg, rectHeaderBg);
            UpdatePreview(txtFilterBg, rectFilterBg);
            UpdatePreview(txtBorderColor, rectBorder);
            UpdatePreview(txtSuccessColor, rectSuccess);
            UpdatePreview(txtWarningColor, rectWarning);
            UpdatePreview(txtDangerColor, rectDanger);
            UpdatePreview(txtInfoColor, rectInfo);
            UpdatePreview(txtAlternateRow, rectAlternateRow);
            UpdatePreview(txtSelectedRow, rectSelectedRow);

            // Vorschau-Bereich aktualisieren
            UpdateMainPreview();
        }

        private void UpdatePreview(TextBox textBox, Rectangle rect)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(textBox.Text);
                rect.Fill = new SolidColorBrush(color);
                textBox.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
            catch
            {
                rect.Fill = new SolidColorBrush(Colors.Transparent);
                textBox.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        private void UpdateMainPreview()
        {
            try
            {
                var primaryColor = (Color)ColorConverter.ConvertFromString(txtPrimaryColor.Text);
                var headerBg = (Color)ColorConverter.ConvertFromString(txtHeaderBg.Text);
                var borderColor = (Color)ColorConverter.ConvertFromString(txtBorderColor.Text);

                previewButton.Background = new SolidColorBrush(primaryColor);
                previewButton.Foreground = new SolidColorBrush(Colors.White);
                previewBorder.Background = new SolidColorBrush(headerBg);
                previewBorder.BorderBrush = new SolidColorBrush(borderColor);
            }
            catch { }
        }

        private void ApplySettingsFromUI()
        {
            var settings = ThemeService.Settings;

            settings.PrimaryColor = txtPrimaryColor.Text;
            settings.SecondaryColor = txtSecondaryColor.Text;
            settings.HeaderBackgroundColor = txtHeaderBg.Text;
            settings.FilterBackgroundColor = txtFilterBg.Text;
            settings.BorderColor = txtBorderColor.Text;
            settings.SuccessColor = txtSuccessColor.Text;
            settings.WarningColor = txtWarningColor.Text;
            settings.DangerColor = txtDangerColor.Text;
            settings.InfoColor = txtInfoColor.Text;
            settings.AlternateRowColor = txtAlternateRow.Text;
            settings.SelectedRowColor = txtSelectedRow.Text;
        }

        private void Anwenden_Click(object sender, RoutedEventArgs e)
        {
            ApplySettingsFromUI();
            ThemeService.ApplyTheme();
            MessageBox.Show("Farben wurden angewendet.", "Design", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            ApplySettingsFromUI();
            ThemeService.ApplyTheme();
            await ThemeService.SaveSettingsAsync(App.ConnectionString);
            MessageBox.Show("Farben wurden in der Datenbank gespeichert.", "Design", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Zuruecksetzen_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Wirklich auf Standardfarben zuruecksetzen?", "Design",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await ThemeService.ResetToDefaultAsync(App.ConnectionString);
                LoadCurrentSettings();
                MessageBox.Show("Standardfarben wurden wiederhergestellt.", "Design", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
