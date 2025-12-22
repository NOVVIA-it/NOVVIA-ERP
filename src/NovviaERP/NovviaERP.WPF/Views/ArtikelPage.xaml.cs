using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelPage : Page
    {
        private readonly CoreService _coreService;
        private List<CoreService.ArtikelUebersicht> _artikel = new();
        private List<CoreService.HerstellerRef> _hersteller = new();

        public ArtikelPage()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeArtikelAsync();
        }

        private async System.Threading.Tasks.Task LadeArtikelAsync()
        {
            try
            {
                txtStatus.Text = "Lade Artikel...";

                // Hersteller laden (einmalig)
                if (_hersteller.Count == 0)
                {
                    _hersteller = (await _coreService.GetHerstellerAsync()).ToList();
                    cmbHersteller.Items.Clear();
                    cmbHersteller.Items.Add(new ComboBoxItem { Content = "Alle Hersteller", IsSelected = true });
                    foreach (var h in _hersteller)
                    {
                        cmbHersteller.Items.Add(new ComboBoxItem { Content = h.CName, Tag = h.KHersteller });
                    }
                    cmbHersteller.SelectedIndex = 0;
                }

                // Artikel laden
                int? herstellerId = null;
                if (cmbHersteller.SelectedItem is ComboBoxItem item && item.Tag is int hId)
                    herstellerId = hId;

                bool nurAktive = chkNurAktive.IsChecked == true;
                bool nurUnterMindest = chkUnterMindest.IsChecked == true;

                _artikel = (await _coreService.GetArtikelAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text,
                    herstellerId: herstellerId,
                    nurAktive: nurAktive,
                    nurUnterMindestbestand: nurUnterMindest
                )).ToList();

                dgArtikel.ItemsSource = _artikel;
                txtAnzahl.Text = $"({_artikel.Count} Artikel)";
                txtStatus.Text = $"{_artikel.Count} Artikel geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden der Artikel:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            await LadeArtikelAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LadeArtikelAsync();
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                await LadeArtikelAsync();
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeArtikelAsync();
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ArtikelDetailPage(null));
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
                NavigationService?.Navigate(new ArtikelDetailPage(artikel.KArtikel));
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
                NavigationService?.Navigate(new ArtikelDetailPage(artikel.KArtikel));
        }

        private void DG_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
            {
                NavigationService?.Navigate(new ArtikelDetailPage(artikel.KArtikel));
                e.Handled = true;
            }
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = dgArtikel.SelectedItem != null;
            btnBearbeiten.IsEnabled = hasSelection;
            btnLager.IsEnabled = hasSelection;
            btnPreise.IsEnabled = hasSelection;
        }

        private void Lagerbestand_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
            {
                MessageBox.Show($"Lagerbestand fuer Artikel {artikel.CArtNr}:\n{artikel.NLagerbestand:N0} Stueck\n\n(Dialog wird implementiert)",
                    "Lagerbestand", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Preise_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
            {
                MessageBox.Show($"Preise fuer Artikel {artikel.CArtNr}:\nVK Netto: {artikel.FVKNetto:N2}\nEK Netto: {artikel.FEKNetto:N2}\n\n(Dialog wird implementiert)",
                    "Preise", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
