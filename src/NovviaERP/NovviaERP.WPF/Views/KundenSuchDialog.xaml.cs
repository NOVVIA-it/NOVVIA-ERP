using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KundenSuchDialog : Window
    {
        private readonly CoreService _core;

        public CoreService.KundeUebersicht? SelectedKunde { get; private set; }

        public KundenSuchDialog(string? initialerSuchbegriff = null)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();

            if (!string.IsNullOrWhiteSpace(initialerSuchbegriff))
            {
                txtSuche.Text = initialerSuchbegriff;
                Loaded += async (s, e) => await SucheKundenAsync(initialerSuchbegriff);
            }
            else
            {
                Loaded += async (s, e) => await LadeKundenAsync();
            }

            txtSuche.Focus();
        }

        private async Task LadeKundenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Kunden...";
                dgKunden.ItemsSource = null;

                var kunden = await _core.GetKundenAsync(null, limit: 200);
                var liste = kunden.ToList();

                dgKunden.ItemsSource = liste;
                txtStatus.Text = $"{liste.Count} Kunden";

                if (liste.Count > 0)
                    dgKunden.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Suchen_Click(sender, new RoutedEventArgs());
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            var suchbegriff = txtSuche.Text?.Trim();
            if (string.IsNullOrEmpty(suchbegriff))
            {
                await LadeKundenAsync();
                return;
            }

            await SucheKundenAsync(suchbegriff);
        }

        private async Task SucheKundenAsync(string suchbegriff)
        {
            try
            {
                txtStatus.Text = "Suche...";
                dgKunden.ItemsSource = null;

                var kunden = await _core.SearchKundenAsync(suchbegriff, limit: 100);
                var liste = kunden.ToList();

                dgKunden.ItemsSource = liste;
                txtStatus.Text = $"{liste.Count} Kunden gefunden";

                if (liste.Count > 0)
                    dgKunden.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void DgKunden_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgKunden.SelectedItem != null)
                Auswaehlen_Click(sender, new RoutedEventArgs());
        }

        private void Auswaehlen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKunden.SelectedItem is not CoreService.KundeUebersicht kunde)
            {
                txtStatus.Text = "Bitte einen Kunden auswählen";
                return;
            }

            SelectedKunde = kunde;
            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
