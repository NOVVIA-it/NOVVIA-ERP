using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelDetailPage : Page
    {
        private readonly CoreService _coreService;
        private int? _artikelId;
        private CoreService.ArtikelDetail? _artikel;
        private List<CoreService.HerstellerRef> _hersteller = new();
        private List<CoreService.SteuerklasseRef> _steuerklassen = new();
        private List<CoreService.WarengruppeRef> _warengruppen = new();

        public ArtikelDetailPage(int? artikelId)
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            _artikelId = artikelId;
            Loaded += async (s, e) => await LadeArtikelAsync();
        }

        private async System.Threading.Tasks.Task LadeArtikelAsync()
        {
            try
            {
                txtStatus.Text = "Lade Stammdaten...";

                // Stammdaten laden
                _hersteller = (await _coreService.GetHerstellerAsync()).ToList();
                cmbHersteller.ItemsSource = _hersteller;

                _steuerklassen = (await _coreService.GetSteuerklassenAsync()).ToList();
                cmbSteuerklasse.ItemsSource = _steuerklassen;

                _warengruppen = (await _coreService.GetWarengruppenAsync()).ToList();
                cmbWarengruppe.ItemsSource = _warengruppen;

                if (_artikelId.HasValue)
                {
                    txtStatus.Text = "Lade Artikeldaten...";
                    _artikel = await _coreService.GetArtikelByIdAsync(_artikelId.Value);

                    if (_artikel == null)
                    {
                        MessageBox.Show("Artikel nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        NavigationService?.GoBack();
                        return;
                    }

                    // Header
                    txtTitel.Text = _artikel.Name ?? "Artikel";
                    txtSubtitel.Text = _artikel.Hersteller ?? "";
                    txtArtNr.Text = $"Art-Nr: {_artikel.CArtNr}";

                    // Identifikation
                    txtArtNrEdit.Text = _artikel.CArtNr;
                    txtBarcode.Text = _artikel.CBarcode;
                    txtHAN.Text = _artikel.CHAN;
                    txtISBN.Text = _artikel.CISBN;
                    txtUPC.Text = _artikel.CUPC;
                    txtASIN.Text = _artikel.CASIN;
                    txtSuchbegriffe.Text = _artikel.CSuchbegriffe;

                    // Beschreibung
                    txtName.Text = _artikel.Name;
                    txtKurzBeschreibung.Text = _artikel.KurzBeschreibung;
                    txtBeschreibung.Text = _artikel.Beschreibung;
                    txtSeo.Text = _artikel.CSeo;

                    // Klassifikation
                    cmbHersteller.SelectedValue = _artikel.KHersteller;
                    cmbWarengruppe.SelectedValue = _artikel.KWarengruppe;
                    cmbSteuerklasse.SelectedValue = _artikel.KSteuerklasse;

                    // Status
                    chkAktiv.IsChecked = _artikel.CAktiv == "Y";
                    chkTopArtikel.IsChecked = _artikel.CTopArtikel == "Y";
                    chkNeu.IsChecked = _artikel.CNeu == "Y";
                    chkTeilbar.IsChecked = _artikel.CTeilbar == "Y";

                    // Tracking
                    chkMHD.IsChecked = _artikel.NMHD == 1;
                    chkCharge.IsChecked = _artikel.NCharge == 1;
                    chkSeriennummer.IsChecked = _artikel.NSeriennummernVerfolgung == 1;

                    // Preise
                    txtVKNetto.Text = _artikel.FVKNetto.ToString("N2");
                    txtUVP.Text = _artikel.FUVP.ToString("N2");
                    txtEKNetto.Text = _artikel.FEKNetto.ToString("N2");
                    txtLetzterEK.Text = _artikel.FLetzterEK > 0 ? $"{_artikel.FLetzterEK:N2} EUR" : "-";

                    // Lager
                    txtBestand.Text = $"{_artikel.NLagerbestand:N0} Stueck";
                    txtMindestbestand.Text = _artikel.NMidestbestand.ToString("N0");
                    txtPackeinheit.Text = _artikel.FPackeinheit.ToString("N0");
                    chkLagerartikel.IsChecked = _artikel.CLagerArtikel == "Y";
                    chkLagerAktiv.IsChecked = _artikel.CLagerAktiv == "Y";
                    chkUnterNull.IsChecked = _artikel.CLagerKleinerNull == "Y";

                    // Lagerbestaende
                    if (_artikel.Lagerbestaende.Count > 0)
                    {
                        dgLagerbestaende.ItemsSource = _artikel.Lagerbestaende;
                    }

                    // Masse/Gewicht
                    txtGewicht.Text = _artikel.FGewicht?.ToString("N3") ?? "";
                    txtArtGewicht.Text = _artikel.FArtGewicht?.ToString("N3") ?? "";
                    txtBreite.Text = _artikel.FBreite?.ToString("N1") ?? "";
                    txtHoehe.Text = _artikel.FHoehe?.ToString("N1") ?? "";
                    txtLaenge.Text = _artikel.FLaenge?.ToString("N1") ?? "";

                    // Zoll
                    txtTaric.Text = _artikel.CTaric;
                    txtHerkunftsland.Text = _artikel.CHerkunftsland;

                    // Lieferanten
                    if (_artikel.Lieferanten.Count > 0)
                    {
                        dgLieferanten.ItemsSource = _artikel.Lieferanten;
                        txtKeineLieferanten.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        dgLieferanten.ItemsSource = null;
                        txtKeineLieferanten.Visibility = Visibility.Visible;
                    }

                    // Statistik
                    txtErstelldatum.Text = _artikel.DErstelldatum?.ToString("dd.MM.yyyy HH:mm") ?? "-";
                    txtAenderungsdatum.Text = _artikel.DMod?.ToString("dd.MM.yyyy HH:mm") ?? "-";

                    txtStatus.Text = "Artikel geladen";
                }
                else
                {
                    // Neuer Artikel
                    _artikel = new CoreService.ArtikelDetail();

                    txtTitel.Text = "Neuer Artikel";
                    txtSubtitel.Text = "";
                    txtArtNr.Text = "(wird automatisch vergeben)";

                    // Defaults
                    chkAktiv.IsChecked = true;
                    chkLagerartikel.IsChecked = true;
                    chkLagerAktiv.IsChecked = true;
                    txtPackeinheit.Text = "1";
                    txtMindestbestand.Text = "0";

                    txtKeineLieferanten.Visibility = Visibility.Visible;

                    txtStatus.Text = "Neuen Artikel anlegen";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Speichere...";

                // Validierung
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Bitte Artikelname angeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Daten aus Formular
                _artikel!.CArtNr = txtArtNrEdit.Text.Trim();
                _artikel.CBarcode = txtBarcode.Text.Trim();
                _artikel.CHAN = txtHAN.Text.Trim();
                _artikel.CISBN = txtISBN.Text.Trim();
                _artikel.CUPC = txtUPC.Text.Trim();
                _artikel.CASIN = txtASIN.Text.Trim();
                _artikel.CSuchbegriffe = txtSuchbegriffe.Text.Trim();

                _artikel.Name = txtName.Text.Trim();
                _artikel.KurzBeschreibung = txtKurzBeschreibung.Text.Trim();
                _artikel.Beschreibung = txtBeschreibung.Text.Trim();
                _artikel.CSeo = txtSeo.Text.Trim();

                _artikel.KHersteller = cmbHersteller.SelectedValue as int?;
                _artikel.KWarengruppe = cmbWarengruppe.SelectedValue as int?;
                _artikel.KSteuerklasse = cmbSteuerklasse.SelectedValue as int?;

                _artikel.CAktiv = chkAktiv.IsChecked == true ? "Y" : "N";
                _artikel.CTopArtikel = chkTopArtikel.IsChecked == true ? "Y" : "N";
                _artikel.CNeu = chkNeu.IsChecked == true ? "Y" : "N";
                _artikel.CTeilbar = chkTeilbar.IsChecked == true ? "Y" : "N";

                _artikel.NMHD = (byte)(chkMHD.IsChecked == true ? 1 : 0);
                _artikel.NCharge = (byte)(chkCharge.IsChecked == true ? 1 : 0);
                _artikel.NSeriennummernVerfolgung = (byte)(chkSeriennummer.IsChecked == true ? 1 : 0);

                _artikel.FVKNetto = decimal.TryParse(txtVKNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var vk) ? vk : 0;
                _artikel.FUVP = decimal.TryParse(txtUVP.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var uvp) ? uvp : 0;
                _artikel.FEKNetto = decimal.TryParse(txtEKNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ek) ? ek : 0;

                _artikel.NMidestbestand = decimal.TryParse(txtMindestbestand.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var min) ? min : 0;
                _artikel.FPackeinheit = decimal.TryParse(txtPackeinheit.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var pack) ? pack : 1;
                _artikel.CLagerArtikel = chkLagerartikel.IsChecked == true ? "Y" : "N";
                _artikel.CLagerAktiv = chkLagerAktiv.IsChecked == true ? "Y" : "N";
                _artikel.CLagerKleinerNull = chkUnterNull.IsChecked == true ? "Y" : "N";

                _artikel.FGewicht = decimal.TryParse(txtGewicht.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var gew) ? gew : null;
                _artikel.FArtGewicht = decimal.TryParse(txtArtGewicht.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var artGew) ? artGew : null;
                _artikel.FBreite = decimal.TryParse(txtBreite.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var breite) ? breite : null;
                _artikel.FHoehe = decimal.TryParse(txtHoehe.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var hoehe) ? hoehe : null;
                _artikel.FLaenge = decimal.TryParse(txtLaenge.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var laenge) ? laenge : null;

                _artikel.CTaric = txtTaric.Text.Trim();
                _artikel.CHerkunftsland = txtHerkunftsland.Text.Trim();

                if (_artikelId.HasValue)
                {
                    // Update
                    await _coreService.UpdateArtikelAsync(_artikel);
                    txtStatus.Text = "Artikel gespeichert";
                    MessageBox.Show("Artikel wurde gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Neu anlegen
                    _artikelId = await _coreService.CreateArtikelAsync(_artikel);
                    _artikel.KArtikel = _artikelId.Value;
                    txtArtNr.Text = $"Art-Nr: {_artikel.CArtNr}";
                    txtStatus.Text = $"Artikel {_artikel.CArtNr} angelegt";
                    MessageBox.Show($"Artikel {_artikel.CArtNr} wurde angelegt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}
