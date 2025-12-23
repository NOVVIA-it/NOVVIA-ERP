using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class WareneingangPage : Page
    {
        private readonly CoreService _core;
        private CoreService.ArtikelDetail? _selectedArtikel;

        public WareneingangPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                txtInfo.Text = "Lade Stammdaten...";

                // Lager laden
                var lager = await _core.GetWarenlagerAsync();
                cboLager.ItemsSource = lager.ToList();
                if (cboLager.Items.Count > 0)
                    cboLager.SelectedIndex = 0;

                // Historie laden
                await LadeHistorieAsync();

                txtInfo.Text = "";
                txtArtikelSuche.Focus();
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Lager_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            try
            {
                if (cboLager.SelectedItem is CoreService.WarenlagerRef lager)
                {
                    var plaetze = await _core.GetWarenlagerPlaetzeAsync(lager.KWarenLager);
                    cboLagerplatz.ItemsSource = plaetze.ToList();
                    if (cboLagerplatz.Items.Count > 0)
                        cboLagerplatz.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Fehler: {ex.Message}";
            }
        }

        private async Task LadeHistorieAsync()
        {
            try
            {
                var historie = await _core.GetWareneingaengeAsync(50);
                dgHistorie.ItemsSource = historie.ToList();
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Fehler Historie: {ex.Message}";
            }
        }

        private async void TxtArtikelSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SucheArtikelAsync();
            }
        }

        private async void ArtikelSuchen_Click(object sender, RoutedEventArgs e)
        {
            await SucheArtikelAsync();
        }

        private async Task SucheArtikelAsync()
        {
            var suchbegriff = txtArtikelSuche.Text.Trim();
            if (string.IsNullOrEmpty(suchbegriff))
            {
                txtInfo.Text = "Bitte Artikelnummer eingeben";
                return;
            }

            try
            {
                txtInfo.Text = "Suche Artikel...";

                // Artikel suchen (PZN, EAN, Art-Nr)
                var artikel = await _core.GetArtikelByBarcodeAsync(suchbegriff);

                if (artikel != null)
                {
                    _selectedArtikel = artikel;
                    txtArtikelNr.Text = $"Art-Nr: {artikel.CArtNr}";
                    txtArtikelName.Text = $"{artikel.Name}";
                    txtArtikelBestand.Text = $"Bestand: {artikel.NLagerbestand:N0} | EK: {artikel.FEKNetto:N2} EUR";
                    btnBuchen.IsEnabled = true;
                    txtInfo.Text = "Artikel gefunden";

                    // Fokus auf Menge setzen
                    txtMenge.SelectAll();
                    txtMenge.Focus();
                }
                else
                {
                    _selectedArtikel = null;
                    txtArtikelNr.Text = "Art-Nr: -";
                    txtArtikelName.Text = "Artikel nicht gefunden";
                    txtArtikelBestand.Text = "Bestand: -";
                    btnBuchen.IsEnabled = false;
                    txtInfo.Text = "Artikel nicht gefunden";
                }
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Fehler: {ex.Message}";
                _selectedArtikel = null;
                btnBuchen.IsEnabled = false;
            }
        }

        private async void Buchen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedArtikel == null)
            {
                MessageBox.Show("Bitte zuerst einen Artikel suchen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cboLagerplatz.SelectedItem is not CoreService.WarenlagerPlatzRef platz)
            {
                MessageBox.Show("Bitte einen Lagerplatz auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtMenge.Text, out var menge) || menge <= 0)
            {
                MessageBox.Show("Bitte eine gueltige Menge eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtMenge.Focus();
                return;
            }

            try
            {
                btnBuchen.IsEnabled = false;
                txtInfo.Text = "Buche Wareneingang...";

                var chargenNr = string.IsNullOrWhiteSpace(txtChargenNr.Text) ? null : txtChargenNr.Text.Trim();
                var mhd = dpMHD.SelectedDate;
                var lieferscheinNr = string.IsNullOrWhiteSpace(txtLieferscheinNr.Text) ? null : txtLieferscheinNr.Text.Trim();
                var kommentar = string.IsNullOrWhiteSpace(txtKommentar.Text) ? null : txtKommentar.Text.Trim();

                // Benutzer-ID (TODO: aus Session holen)
                var benutzerId = 1;

                await _core.BucheWareneingangAsync(
                    artikelId: _selectedArtikel.KArtikel,
                    lagerPlatzId: platz.KWarenlagerPlatz,
                    menge: menge,
                    benutzerId: benutzerId,
                    kommentar: kommentar,
                    lieferscheinNr: lieferscheinNr,
                    chargenNr: chargenNr,
                    mhd: mhd);

                txtInfo.Text = $"Wareneingang gebucht: {menge:N0}x {_selectedArtikel.CArtNr}";

                // Felder leeren und Historie aktualisieren
                FelderLeeren();
                await LadeHistorieAsync();

                // Fokus zurueck auf Suche
                txtArtikelSuche.Focus();
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Buchen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                btnBuchen.IsEnabled = true;
            }
        }

        private void Leeren_Click(object sender, RoutedEventArgs e)
        {
            FelderLeeren();
            txtArtikelSuche.Focus();
        }

        private void FelderLeeren()
        {
            txtArtikelSuche.Text = "";
            txtArtikelNr.Text = "Art-Nr: -";
            txtArtikelName.Text = "Artikelname: -";
            txtArtikelBestand.Text = "Bestand: -";
            txtMenge.Text = "1";
            txtChargenNr.Text = "";
            dpMHD.SelectedDate = null;
            txtLieferscheinNr.Text = "";
            txtKommentar.Text = "";
            _selectedArtikel = null;
            btnBuchen.IsEnabled = false;
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeHistorieAsync();
        }
    }
}
