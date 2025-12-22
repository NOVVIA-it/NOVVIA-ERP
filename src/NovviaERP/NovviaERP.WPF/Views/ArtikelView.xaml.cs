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
    public partial class ArtikelView : UserControl
    {
        private readonly CoreService _coreService;
        private List<CoreService.ArtikelUebersicht> _artikel = new();
        private List<CoreService.HerstellerRef> _hersteller = new();

        public ArtikelView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            Loaded += ArtikelView_Loaded;
        }

        private void ArtikelView_Loaded(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Klicke 'Suchen' um Artikel zu laden";
        }

        private void NavigateTo(UserControl view)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(view);
            }
        }

        private async System.Threading.Tasks.Task LadeArtikelAsync()
        {
            try
            {
                txtStatus.Text = "Lade Artikel...";

                _artikel = (await _coreService.GetArtikelAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text,
                    nurAktive: true,
                    limit: 100
                )).ToList();

                dgArtikel.ItemsSource = _artikel;
                txtAnzahl.Text = $"({_artikel.Count} Artikel)";
                txtStatus.Text = $"{_artikel.Count} Artikel geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler");
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

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            // Deaktiviert
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeArtikelAsync();
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(new ArtikelDetailView(null));
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
            {
                MessageBox.Show($"DEBUG: Navigiere zu Artikel {artikel.KArtikel}");
                var view = new ArtikelDetailView(artikel.KArtikel);
                MessageBox.Show("DEBUG: View erstellt");
                NavigateTo(view);
                MessageBox.Show("DEBUG: Navigation fertig");
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
