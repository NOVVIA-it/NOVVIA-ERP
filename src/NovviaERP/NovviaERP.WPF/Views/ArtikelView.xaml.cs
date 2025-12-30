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
        private readonly AppDataService _appData;
        private List<CoreService.ArtikelUebersicht> _artikel = new();
        private List<CoreService.HerstellerRef> _hersteller = new();
        private List<CoreService.WarengruppeRef> _warengruppen = new();
        private bool _initialized = false;

        public ArtikelView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            _appData = App.Services.GetRequiredService<AppDataService>();
            Loaded += async (s, e) => await InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // Warengruppen laden
                _warengruppen = (await _coreService.GetWarengruppenAsync()).ToList();
                cmbWarengruppe.Items.Clear();
                cmbWarengruppe.Items.Add(new ComboBoxItem { Content = "Alle Warengruppen", IsSelected = true });
                foreach (var wg in _warengruppen)
                {
                    cmbWarengruppe.Items.Add(wg);
                }
                cmbWarengruppe.SelectedIndex = 0;

                // Hersteller laden
                _hersteller = (await _coreService.GetHerstellerAsync()).ToList();
                cmbHersteller.Items.Clear();
                cmbHersteller.Items.Add(new ComboBoxItem { Content = "Alle Hersteller", IsSelected = true });
                foreach (var h in _hersteller)
                {
                    cmbHersteller.Items.Add(h);
                }
                cmbHersteller.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Filter-Fehler: {ex.Message}";
            }

            await LadeArtikelAsync();
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

                // Warengruppe-Filter
                int? warengruppeId = null;
                if (cmbWarengruppe.SelectedItem is CoreService.WarengruppeRef wg)
                    warengruppeId = wg.KWarengruppe;

                // Hersteller-Filter
                int? herstellerId = null;
                if (cmbHersteller.SelectedItem is CoreService.HerstellerRef h)
                    herstellerId = h.KHersteller;

                _artikel = (await _coreService.GetArtikelAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text,
                    herstellerId: herstellerId,
                    warengruppeId: warengruppeId,
                    nurAktive: chkNurAktive.IsChecked == true,
                    nurUnterMindestbestand: chkUnterMindest.IsChecked == true,
                    limit: 500
                )).ToList();

                dgArtikel.ItemsSource = _artikel;
                txtAnzahl.Text = $"({_artikel.Count} Artikel)";
                txtStatus.Text = $"{_artikel.Count} Artikel geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
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
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
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
                // Oeffne Artikel-Detail mit Lager-Tab
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
            }
        }

        private void Preise_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
            {
                // Oeffne Artikel-Detail mit Preise-Tab
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
            }
        }

        #region Kontextmenü

        private void ContextMenu_Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            Bearbeiten_Click(sender, e);
        }

        private void ContextMenu_Duplizieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.ArtikelUebersicht artikel)
            {
                // Neuen Artikel mit Daten des ausgewählten öffnen
                NavigateTo(new ArtikelDetailView(artikel.KArtikel, duplizieren: true));
            }
        }

        private void ContextMenu_NeuErstellen_Click(object sender, RoutedEventArgs e)
        {
            // Neuen Artikel mit Mapping-Defaults erstellen
            var mapping = _appData.GetArtikelMapping();
            NavigateTo(new ArtikelDetailView(null, mapping: mapping));
        }

        private async void ContextMenu_Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not CoreService.ArtikelUebersicht artikel)
            {
                txtStatus.Text = "Bitte einen Artikel auswählen";
                return;
            }

            try
            {
                var mapping = _appData.GetArtikelMapping();
                txtStatus.Text = $"Aktualisiere Artikel {artikel.CArtNr}...";

                // Artikel mit Mapping-Konfiguration aktualisieren
                await _coreService.UpdateArtikelMitMappingAsync(artikel.KArtikel, mapping);

                txtStatus.Text = $"Artikel {artikel.CArtNr} aktualisiert";
                await LadeArtikelAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void ContextMenu_Lagerbestand_Click(object sender, RoutedEventArgs e)
        {
            Lagerbestand_Click(sender, e);
        }

        private void ContextMenu_Preise_Click(object sender, RoutedEventArgs e)
        {
            Preise_Click(sender, e);
        }

        private async void ContextMenu_Deaktivieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not CoreService.ArtikelUebersicht artikel)
            {
                txtStatus.Text = "Bitte einen Artikel auswählen";
                return;
            }

            var result = MessageBox.Show(
                $"Artikel '{artikel.Name}' wirklich deaktivieren?",
                "Artikel deaktivieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _coreService.SetArtikelAktivAsync(artikel.KArtikel, false);
                txtStatus.Text = $"Artikel {artikel.CArtNr} deaktiviert";
                await LadeArtikelAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async void ContextMenu_Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not CoreService.ArtikelUebersicht artikel)
            {
                txtStatus.Text = "Bitte einen Artikel auswählen";
                return;
            }

            var result = MessageBox.Show(
                $"Artikel '{artikel.Name}' wirklich LÖSCHEN?\n\nDiese Aktion kann nicht rückgängig gemacht werden!",
                "Artikel löschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _coreService.DeleteArtikelAsync(artikel.KArtikel);
                txtStatus.Text = $"Artikel {artikel.CArtNr} gelöscht";
                await LadeArtikelAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion
    }
}
