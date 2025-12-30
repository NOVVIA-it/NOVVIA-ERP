using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelSuchDialog : Window
    {
        private readonly CoreService _core;

        public CoreService.ArtikelUebersicht? AusgewaehlterArtikel { get; private set; }
        public decimal Menge { get; private set; } = 1;
        public bool IstAusgewaehlt { get; private set; }

        public ArtikelSuchDialog(string? initialerSuchbegriff = null)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();

            if (!string.IsNullOrWhiteSpace(initialerSuchbegriff))
            {
                txtSuche.Text = initialerSuchbegriff;
                Loaded += async (s, e) => await SucheArtikelAsync(initialerSuchbegriff);
            }
            else
            {
                // 200 Artikel ohne Suche anzeigen
                Loaded += async (s, e) => await LadeArtikelAsync();
            }

            txtSuche.Focus();
        }

        private async Task LadeArtikelAsync()
        {
            try
            {
                txtStatus.Text = "Lade Artikel...";
                dgArtikel.ItemsSource = null;

                var artikel = await _core.GetArtikelAsync(null, limit: 200);
                var liste = artikel.ToList();

                dgArtikel.ItemsSource = liste;
                txtStatus.Text = $"{liste.Count} Artikel";

                if (liste.Count > 0)
                    dgArtikel.SelectedIndex = 0;
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
                // Bei leerem Suchbegriff alle Artikel anzeigen
                await LadeArtikelAsync();
                return;
            }

            await SucheArtikelAsync(suchbegriff);
        }

        private async Task SucheArtikelAsync(string suchbegriff)
        {
            try
            {
                txtStatus.Text = "Suche...";
                dgArtikel.ItemsSource = null;

                var artikel = await _core.GetArtikelAsync(suchbegriff, limit: 100);
                var liste = artikel.ToList();

                dgArtikel.ItemsSource = liste;
                txtStatus.Text = $"{liste.Count} Artikel gefunden";

                if (liste.Count > 0)
                    dgArtikel.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void DgArtikel_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgArtikel.SelectedItem != null)
                Hinzufuegen_Click(sender, new RoutedEventArgs());
        }

        private void Hinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not CoreService.ArtikelUebersicht artikel)
            {
                txtStatus.Text = "Bitte einen Artikel auswählen";
                return;
            }

            if (!decimal.TryParse(txtMenge.Text, out var menge) || menge <= 0)
            {
                txtStatus.Text = "Bitte gültige Menge eingeben";
                txtMenge.Focus();
                return;
            }

            AusgewaehlterArtikel = artikel;
            Menge = menge;
            IstAusgewaehlt = true;
            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            IstAusgewaehlt = false;
            DialogResult = false;
            Close();
        }
    }
}
