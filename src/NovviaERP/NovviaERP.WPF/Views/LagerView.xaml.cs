using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class LagerView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.LagerbestandUebersicht> _lagerbestaende = new();
        private List<CoreService.LagerbewegungUebersicht> _bewegungen = new();

        public LagerView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Spalten-Konfiguration
                DataGridColumnConfig.EnableColumnChooser(dgLagerbestand, "LagerView");
                DataGridColumnConfig.EnableColumnChooser(dgBewegungen, "LagerView.Bewegungen");

                txtStatus.Text = "Lade Lager...";

                // Warenlager laden
                var lager = (await _core.GetWarenlagerAsync()).ToList();
                lager.Insert(0, new CoreService.WarenlagerRef { KWarenLager = 0, CName = "Alle Lager" });
                cmbLager.ItemsSource = lager;
                cmbLager.SelectedIndex = 0;

                await LadeLagerbestandAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LadeLagerbestandAsync()
        {
            try
            {
                txtStatus.Text = "Lade Lagerbestand...";

                int? kWarenlager = null;
                if (cmbLager.SelectedValue is int selectedLager && selectedLager > 0)
                    kWarenlager = selectedLager;

                var suche = txtSuche.Text.Trim();
                var nurMitBestand = chkNurMitBestand.IsChecked == true;

                _lagerbestaende = (await _core.GetLagerbestaendeAsync(
                    kWarenlager: kWarenlager,
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    nurMitBestand: nurMitBestand
                )).ToList();

                // Filter: Unter Mindestbestand
                if (chkUnterMindest.IsChecked == true)
                    _lagerbestaende = _lagerbestaende.Where(l => l.IstUnterMindestbestand).ToList();

                dgLagerbestand.ItemsSource = _lagerbestaende;
                txtAnzahl.Text = $"({_lagerbestaende.Count} Artikel)";
                txtStatus.Text = $"{_lagerbestaende.Count} Artikel geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LadeBewegungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Bewegungen...";

                int? kWarenlager = null;
                if (cmbLager.SelectedValue is int selectedLager && selectedLager > 0)
                    kWarenlager = selectedLager;

                DateTime? vonDatum = null;
                if (cmbZeitraum.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var tage))
                {
                    if (tage >= 0)
                        vonDatum = DateTime.Today.AddDays(-tage);
                }

                _bewegungen = (await _core.GetLagerbewegungenAsync(
                    kWarenlager: kWarenlager,
                    vonDatum: vonDatum
                )).ToList();

                dgBewegungen.ItemsSource = _bewegungen;
                txtStatus.Text = $"{_bewegungen.Count} Bewegungen geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden der Bewegungen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Event Handlers

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeLagerbestandAsync();

        private async void Lager_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeLagerbestandAsync();
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeLagerbestandAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeLagerbestandAsync();
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnArtikelDetail.IsEnabled = dgLagerbestand.SelectedItem != null;
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgLagerbestand.SelectedItem is CoreService.LagerbestandUebersicht bestand)
                NavigateToArtikel(bestand.KArtikel);
        }

        private void ArtikelDetail_Click(object sender, RoutedEventArgs e)
        {
            if (dgLagerbestand.SelectedItem is CoreService.LagerbestandUebersicht bestand)
                NavigateToArtikel(bestand.KArtikel);
        }

        private async void Bewegungen_Click(object sender, RoutedEventArgs e)
        {
            tabLager.SelectedIndex = 1; // Bewegungen Tab
            await LadeBewegungenAsync();
        }

        private async void Zeitraum_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeBewegungenAsync();
        }

        #endregion

        #region Navigation

        private void NavigateToArtikel(int artikelId)
        {
            try
            {
                var detailView = new ArtikelDetailView(artikelId);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(detailView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen des Artikels:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
