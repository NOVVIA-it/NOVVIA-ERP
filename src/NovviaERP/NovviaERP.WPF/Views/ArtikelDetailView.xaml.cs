using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelDetailView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly EinkaufService _einkaufService;
        private readonly MSV3Service _msv3Service;
        private readonly AppDataService _appData;
        private readonly int? _artikelId;
        private readonly bool _duplizieren;
        private readonly ArtikelMappingConfig? _mapping;
        private CoreService.ArtikelDetail? _artikel;
        private List<CoreService.HerstellerRef> _hersteller = new();
        private List<CoreService.SteuerklasseRef> _steuerklassen = new();
        private List<CoreService.VersandklasseRef> _versandklassen = new();
        private List<CoreService.WarengruppeRef> _warengruppen = new();
        private List<CoreService.LieferantRef> _lieferanten = new();

        // Lazy-loading flags
        private bool _beschreibungGeladen = false;
        private bool _bestaendeGeladen = false;
        private bool _lieferantenGeladen = false;
        private bool _bilderGeladen = false;
        private bool _attributeGeladen = false;
        private bool _stuecklisteGeladen = false;
        private bool _sonderpreiseGeladen = false;

        public ArtikelDetailView(int? artikelId, bool duplizieren = false, ArtikelMappingConfig? mapping = null)
        {
            InitializeComponent();
            _artikelId = duplizieren ? null : artikelId;
            _duplizieren = duplizieren;
            _mapping = mapping;
            _coreService = App.Services.GetRequiredService<CoreService>();
            _einkaufService = App.Services.GetRequiredService<EinkaufService>();
            _msv3Service = App.Services.GetRequiredService<MSV3Service>();
            _appData = App.Services.GetRequiredService<AppDataService>();

            if (duplizieren)
                txtTitel.Text = "Artikel duplizieren";
            else if (artikelId.HasValue)
                txtTitel.Text = "Artikel bearbeiten";
            else
                txtTitel.Text = "Neuer Artikel";

            Loaded += async (s, e) => await LadeArtikelAsync();

            if (duplizieren && artikelId.HasValue)
                Loaded += async (s, e) => await LadeVorlageAsync(artikelId.Value);
        }

        private async System.Threading.Tasks.Task LadeVorlageAsync(int vorlageId)
        {
            try
            {
                var vorlage = await _coreService.GetArtikelByIdAsync(vorlageId);
                if (vorlage == null) return;

                txtName.Text = vorlage.Name + " (Kopie)";
                txtGTIN.Text = "";
                txtBeschreibung.Text = vorlage.Beschreibung ?? "";
                txtVKNetto.Text = vorlage.FVKNetto.ToString("N2");
                txtEKNetto.Text = vorlage.FEKNetto.ToString("N2");

                if (vorlage.KHersteller.HasValue)
                    cmbHersteller.SelectedValue = vorlage.KHersteller;
                if (vorlage.KSteuerklasse.HasValue)
                    cmbSteuerklasse.SelectedValue = vorlage.KSteuerklasse;

                txtStatus.Text = $"Vorlage '{vorlage.CArtNr}' geladen - bitte neue Art-Nr. vergeben";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler beim Laden der Vorlage: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task LadeArtikelAsync()
        {
            try
            {
                txtStatus.Text = "Lade Stammdaten...";

                // GridStyleHelper für alle DataGrids initialisieren
                await GridStyleHelper.Instance.LoadSettingsAsync(_coreService, App.BenutzerId);
                InitializeAllGrids();

                // Stammdaten laden
                _hersteller = (await _coreService.GetHerstellerAsync()).ToList();
                cmbHersteller.ItemsSource = _hersteller;

                _steuerklassen = (await _coreService.GetSteuerklassenAsync()).ToList();
                cmbSteuerklasse.ItemsSource = _steuerklassen;

                _versandklassen = (await _coreService.GetVersandklassenAsync()).ToList();
                cmbVersandklasse.ItemsSource = _versandklassen;

                _warengruppen = (await _coreService.GetWarengruppenAsync()).ToList();
                cmbWarengruppe.ItemsSource = _warengruppen;

                _lieferanten = (await _coreService.GetLieferantenAsync()).ToList();
                cmbLieferantenAuswahl.ItemsSource = _lieferanten;

                if (_artikelId.HasValue)
                {
                    txtStatus.Text = "Lade Artikeldaten...";
                    _artikel = await _coreService.GetArtikelByIdAsync(_artikelId.Value);

                    if (_artikel == null)
                    {
                        txtStatus.Text = "Artikel nicht gefunden";
                        return;
                    }

                    // Header
                    txtTitel.Text = $"Artikel - '{_artikel.Name ?? "Artikel"}'";
                    txtSubtitel.Text = "Hier koennen Sie Stammdaten, Preise, Lagerbestand und andere wichtige Daten zu Ihrem Artikel erfassen und pflegen.";

                    // Tab Allgemein - Stammdaten
                    txtArtNrEdit.Text = _artikel.CArtNr;
                    txtGTIN.Text = _artikel.CBarcode;
                    txtName.Text = _artikel.Name;
                    txtHAN.Text = _artikel.CHAN;
                    chkInaktiv.IsChecked = _artikel.CAktiv != "Y";

                    cmbHersteller.SelectedValue = _artikel.KHersteller;
                    cmbSteuerklasse.SelectedValue = _artikel.KSteuerklasse;
                    cmbVersandklasse.SelectedValue = _artikel.KVersandklasse;
                    cmbWarengruppe.SelectedValue = _artikel.KWarengruppe;

                    // Preise
                    txtVKNetto.Text = _artikel.FVKNetto.ToString("N2");
                    txtUVP.Text = _artikel.FUVP.ToString("N2");
                    txtEKNetto.Text = _artikel.FEKNetto.ToString("N2");
                    txtBruttoVK.Text = (_artikel.FVKNetto * 1.19m).ToString("N2");
                    txtLetzterEK.Text = _artikel.FLetzterEK > 0 ? _artikel.FLetzterEK.ToString("N2") : "-";

                    // Lager
                    txtAufLager.Text = _artikel.NLagerbestand.ToString("N0");
                    chkBestandsfuehrung.IsChecked = _artikel.CLagerArtikel == "Y";

                    // Lageroptionen
                    chkSeriennummer.IsChecked = _artikel.NSeriennummernVerfolgung > 0;
                    chkMHD.IsChecked = _artikel.NMHD > 0;
                    chkCharge.IsChecked = _artikel.NCharge > 0;

                    // Gewicht
                    txtGewicht.Text = _artikel.FGewicht?.ToString("N3") ?? "";

                    // Status
                    chkTopArtikel.IsChecked = _artikel.CTopArtikel == "Y";
                    chkNeuImSortiment.IsChecked = _artikel.CNeu == "Y";

                    // Sonstiges-Felder laden
                    txtISBN.Text = _artikel.CISBN ?? "";
                    txtUPC.Text = _artikel.CUPC ?? "";
                    txtTARIC.Text = _artikel.CTaric ?? "";
                    txtHerkunftsland.Text = _artikel.CHerkunftsland ?? "";
                    txtUNNummer.Text = _artikel.CUNNummer ?? "";
                    txtGefahrnummer.Text = _artikel.CGefahrnr ?? "";
                    txtAnmerkung.Text = _artikel.CAnmerkung ?? "";

                    // Kategorien laden
                    await LadeKategorienAsync();

                    // Kundengruppen-Preise laden
                    await LadeKundengruppenPreiseAsync();

                    // Kundeneinzelpreise laden
                    await LadeKundeneinzelpreiseAsync();

                    // Textmeldungen laden
                    await pnlTextmeldungen.LoadAsync("Artikel", _artikelId.Value, "Stammdaten");
                    await pnlTextmeldungen.ShowPopupAsync("Artikel", _artikelId.Value, "Stammdaten", txtName.Text);

                    txtStatus.Text = "Artikel geladen";
                }
                else
                {
                    _artikel = new CoreService.ArtikelDetail();
                    chkInaktiv.IsChecked = false;
                    chkBestandsfuehrung.IsChecked = true;
                    txtStatus.Text = "Neuen Artikel anlegen";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task LadeKategorienAsync()
        {
            if (!_artikelId.HasValue) return;

            try
            {
                var kategorien = await _coreService.GetArtikelKategorienAsync(_artikelId.Value);
                lstKategorien.ItemsSource = kategorien.Select(k => k.CKategoriePfad).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Kategorien: {ex.Message}");
            }
        }

        private async void BtnKategorieZuweisen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Artikel speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Alle verfuegbaren Kategorien laden
                var alleKategorien = await _coreService.GetKategorienAsync();
                var artikelKategorien = await _coreService.GetArtikelKategorienAsync(_artikelId.Value);
                var zugewieseneIds = artikelKategorien.Select(k => k.KKategorie).ToHashSet();

                // Dialog mit Kategorien-Auswahl anzeigen
                var dialog = new KategorieAuswahlDialog(alleKategorien, zugewieseneIds);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true)
                {
                    // Geaenderte Kategorien speichern
                    var neueIds = dialog.AusgewaehlteKategorien.ToList();

                    // Entfernte Kategorien loeschen
                    foreach (var altId in zugewieseneIds.Except(neueIds))
                    {
                        await _coreService.RemoveArtikelKategorieAsync(_artikelId.Value, altId);
                    }

                    // Neue Kategorien hinzufuegen
                    foreach (var neuId in neueIds.Except(zugewieseneIds))
                    {
                        await _coreService.AddArtikelKategorieAsync(_artikelId.Value, neuId);
                    }

                    // Liste neu laden
                    await LadeKategorienAsync();
                    txtStatus.Text = "Kategorien aktualisiert";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zuweisen der Kategorien: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<KundengruppePreisViewModel> _kundengruppenPreise = new();

        private async System.Threading.Tasks.Task LadeKundengruppenPreiseAsync()
        {
            if (!_artikelId.HasValue) return;

            try
            {
                var preise = await _coreService.GetArtikelKundengruppenPreiseAsync(_artikelId.Value);
                decimal mwstSatz = 1.19m; // TODO: Von Steuerklasse holen

                _kundengruppenPreise = preise.Select(p => new KundengruppePreisViewModel
                {
                    KKundengruppe = p.KKundengruppe,
                    CKundengruppe = p.CKundengruppe ?? "",
                    FRabatt = p.FRabatt,
                    FNettoPreis = p.FNettoPreis,
                    FBruttoPreis = p.FNettoPreis * mwstSatz,
                    AnzahlStaffeln = p.KPreis.HasValue ? 1 : 0, // Wird spaeter korrekt geladen
                    KPreis = p.KPreis
                }).ToList();

                // Staffel-Anzahl fuer jede Kundengruppe laden
                foreach (var preis in _kundengruppenPreise.Where(p => p.KPreis.HasValue))
                {
                    var staffeln = await _coreService.GetArtikelStaffelpreiseAsync(_artikelId.Value, preis.KKundengruppe);
                    preis.AnzahlStaffeln = staffeln.Count();
                }

                dgPreiseGlobal.ItemsSource = _kundengruppenPreise;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Kundenpreise: {ex.Message}");
            }
        }

        private async void BtnStaffelpreise_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue) return;

            var selected = dgPreiseGlobal.SelectedItem as KundengruppePreisViewModel;
            if (selected == null)
            {
                MessageBox.Show("Bitte waehlen Sie eine Kundengruppe aus.", "Staffelpreise", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new StaffelpreisDialog(_coreService, _artikelId.Value, selected.KKundengruppe, selected.CKundengruppe);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                // Preise neu laden
                await LadeKundengruppenPreiseAsync();
            }
        }

        private async void BtnPreisLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue) return;

            var selected = dgPreiseGlobal.SelectedItem as KundengruppePreisViewModel;
            if (selected == null || selected.FNettoPreis == 0)
            {
                MessageBox.Show("Bitte waehlen Sie eine Kundengruppe mit Preis aus.", "Preis loeschen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Moechten Sie den Preis fuer '{selected.CKundengruppe}' wirklich loeschen?",
                "Preis loeschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.SaveStaffelpreiseAsync(_artikelId.Value, selected.KKundengruppe, new List<CoreService.StaffelpreisDetail>());
                    await LadeKundengruppenPreiseAsync();
                    txtStatus.Text = "Preis geloescht";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Loeschen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region Kundeneinzelpreise

        private async System.Threading.Tasks.Task LadeKundeneinzelpreiseAsync()
        {
            if (!_artikelId.HasValue) return;

            try
            {
                var preise = await _coreService.GetArtikelKundeneinzelpreiseAsync(_artikelId.Value);
                dgKundenpreise.ItemsSource = preise;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler beim Laden der Kundenpreise: {ex.Message}";
            }
        }

        private async void BtnKundenpreisHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue) return;

            var dialog = new KundeneinzelpreisDialog(_coreService, _artikelId.Value, _artikel?.FVKNetto ?? 0, null);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                await LadeKundeneinzelpreiseAsync();
                txtStatus.Text = "Kundenpreis hinzugefuegt";
            }
        }

        private async void BtnKundenpreisBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue) return;

            var selected = dgKundenpreise.SelectedItem as CoreService.KundeneinzelpreisInfo;
            if (selected == null)
            {
                MessageBox.Show("Bitte waehlen Sie einen Kunden aus.", "Bearbeiten", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new KundeneinzelpreisDialog(_coreService, _artikelId.Value, _artikel?.FVKNetto ?? 0, selected);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                await LadeKundeneinzelpreiseAsync();
                txtStatus.Text = "Kundenpreis aktualisiert";
            }
        }

        private async void BtnKundenpreisEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue) return;

            var selected = dgKundenpreise.SelectedItem as CoreService.KundeneinzelpreisInfo;
            if (selected == null)
            {
                MessageBox.Show("Bitte waehlen Sie einen Kunden aus.", "Entfernen", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Moechten Sie den Preis fuer '{selected.CVorname} {selected.CNachname}' wirklich entfernen?",
                "Kundenpreis entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.DeleteKundeneinzelpreisAsync(_artikelId.Value, selected.KKunde);
                    await LadeKundeneinzelpreiseAsync();
                    txtStatus.Text = "Kundenpreis entfernt";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Entfernen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        public class KundengruppePreisViewModel
        {
            public int KKundengruppe { get; set; }
            public string CKundengruppe { get; set; } = "";
            public decimal FRabatt { get; set; }
            public decimal FNettoPreis { get; set; }
            public decimal FBruttoPreis { get; set; }
            public int AnzahlStaffeln { get; set; }
            public int? KPreis { get; set; }
        }

        private void InitializeAllGrids()
        {
            // Tab Allgemein - Preise
            GridStyleHelper.InitializeGrid(dgPreiseGlobal, "ArtikelDetail.PreiseGlobal");
            GridStyleHelper.InitializeGrid(dgKundenpreise, "ArtikelDetail.Kundenpreise");

            // Tab Bestaende
            GridStyleHelper.InitializeGrid(dgBestaende, "ArtikelDetail.Bestaende");
            GridStyleHelper.InitializeGrid(dgChargen, "ArtikelDetail.Chargen");
            GridStyleHelper.InitializeGrid(dgSeriennummern, "ArtikelDetail.Seriennummern");

            // Tab Lieferanten
            GridStyleHelper.InitializeGrid(dgArtikelLieferanten, "ArtikelDetail.Lieferanten");

            // Tab Bilder
            GridStyleHelper.InitializeGrid(dgBilder, "ArtikelDetail.Bilder");

            // Tab Attribute/Merkmale
            GridStyleHelper.InitializeGrid(dgAttribute, "ArtikelDetail.Attribute");
            GridStyleHelper.InitializeGrid(dgMerkmale, "ArtikelDetail.Merkmale");

            // Tab Stueckliste
            GridStyleHelper.InitializeGrid(dgStueckliste, "ArtikelDetail.Stueckliste");

            // Tab Sonderpreise
            GridStyleHelper.InitializeGrid(dgSonderpreise, "ArtikelDetail.Sonderpreise");
        }

        #region Tab-Wechsel und Lazy Loading

        private async void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != tabControl) return;

            var selectedTab = tabControl.SelectedItem as TabItem;
            if (selectedTab == null || !_artikelId.HasValue) return;

            try
            {
                if (selectedTab == tabBeschreibung && !_beschreibungGeladen)
                {
                    await LadeBeschreibungAsync();
                    _beschreibungGeladen = true;
                }
                else if (selectedTab == tabBestaende && !_bestaendeGeladen)
                {
                    await LadeBestaendeAsync();
                    _bestaendeGeladen = true;
                }
                else if (selectedTab == tabLieferanten && !_lieferantenGeladen)
                {
                    await LadeLieferantenAsync();
                    _lieferantenGeladen = true;
                }
                else if (selectedTab == tabBilder && !_bilderGeladen)
                {
                    await LadeBilderAsync();
                    _bilderGeladen = true;
                }
                else if (selectedTab == tabAttribute && !_attributeGeladen)
                {
                    await LadeAttributeAsync();
                    _attributeGeladen = true;
                }
                else if (selectedTab == tabStueckliste && !_stuecklisteGeladen)
                {
                    await LadeStuecklisteAsync();
                    _stuecklisteGeladen = true;
                }
                else if (selectedTab == tabSonderpreise && !_sonderpreiseGeladen)
                {
                    await LadeSonderpreiseAsync();
                    _sonderpreiseGeladen = true;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion

        #region Tab: Beschreibung

        private async System.Threading.Tasks.Task LadeBeschreibungAsync()
        {
            if (!_artikelId.HasValue) return;

            txtStatus.Text = "Lade Beschreibungen...";
            var beschreibung = await _coreService.GetArtikelBeschreibungAsync(_artikelId.Value, 1, 1); // Deutsch, JTL-Wawi

            if (beschreibung != null)
            {
                txtNameBeschreibung.Text = beschreibung.CName ?? _artikel?.Name ?? "";
                txtKurzbeschreibung.Text = beschreibung.CKurzBeschreibung ?? "";
                txtBeschreibung.Text = beschreibung.CBeschreibung ?? "";
                txtTitleTag.Text = beschreibung.CTitleTag ?? "";
                txtMetaDescription.Text = beschreibung.CMetaDescription ?? "";
                txtMetaKeywords.Text = beschreibung.CMetaKeywords ?? "";
                txtUrlPfad.Text = beschreibung.CUrlPfad ?? "";
            }

            txtStatus.Text = "Beschreibung geladen";
        }

        private void Kurzbeschreibung_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtKurzbeschreibungLabel.Text = $"Kurzbeschreibung ({txtKurzbeschreibung.Text?.Length ?? 0}):";
        }

        private void Beschreibung_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtBeschreibungLabel.Text = $"Beschreibung ({txtBeschreibung.Text?.Length ?? 0}):";
        }

        #endregion

        #region Tab: Bestaende

        private async System.Threading.Tasks.Task LadeBestaendeAsync()
        {
            if (!_artikelId.HasValue || _artikel == null) return;

            txtStatus.Text = "Lade Bestaende...";

            // Lagerbestaende laden
            var bestaende = await _coreService.GetArtikelBestaendeAsync(_artikelId.Value);
            dgBestaende.ItemsSource = bestaende;

            // Summen aus tlagerbestand (globale Summen)
            var lbSumme = await _coreService.GetArtikelLagerbestandSummeAsync(_artikelId.Value);
            txtBestandAufLager.Text = lbSumme.FAufLager.ToString("N0");
            txtBestandReserviert.Text = lbSumme.FReserviert.ToString("N2");
            txtBestandImZulauf.Text = lbSumme.FImZulauf.ToString("N0");
            txtBestandVerfuegbar.Text = lbSumme.FVerfuegbar.ToString("N0");
            txtBestandEinkaufsliste.Text = lbSumme.FAufEinkaufsliste.ToString("N0");
            txtBestandGesperrt.Text = lbSumme.FGesperrt.ToString("N0");

            // Chargen oder Seriennummern basierend auf Artikeltyp
            bool istChargenArtikel = _artikel.NCharge == 1;
            bool istSeriennummerArtikel = _artikel.NSeriennummernVerfolgung == 1;

            if (istSeriennummerArtikel)
            {
                // Seriennummern anzeigen
                grpChargen.Visibility = Visibility.Collapsed;
                grpSeriennummern.Visibility = Visibility.Visible;
                var seriennummern = await _coreService.GetArtikelSeriennummernAsync(_artikelId.Value);
                dgSeriennummern.ItemsSource = seriennummern;
            }
            else if (istChargenArtikel)
            {
                // Chargen anzeigen
                grpChargen.Visibility = Visibility.Visible;
                grpSeriennummern.Visibility = Visibility.Collapsed;
                var chargen = await _coreService.GetArtikelChargenAsync(_artikelId.Value);
                dgChargen.ItemsSource = chargen;
            }
            else
            {
                // Chargen-Box anzeigen (leer), Seriennummern ausblenden
                grpChargen.Visibility = Visibility.Visible;
                grpSeriennummern.Visibility = Visibility.Collapsed;
                dgChargen.ItemsSource = null;
            }

            txtStatus.Text = "Bestaende geladen";
        }

        #endregion

        #region Tab: Lieferanten

        private async System.Threading.Tasks.Task LadeLieferantenAsync()
        {
            if (!_artikelId.HasValue) return;

            txtStatus.Text = "Lade Lieferanten...";
            var lieferanten = await _coreService.GetArtikelLieferantenAsync(_artikelId.Value);
            dgArtikelLieferanten.ItemsSource = lieferanten;
            txtStatus.Text = $"{lieferanten.Count()} Lieferanten geladen";
        }

        private void ArtikelLieferanten_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgArtikelLieferanten.SelectedItem as dynamic;
            if (selected == null) return;

            txtLieferantDetailsHeader.Text = $"{selected.CLieferantName} (Standardlieferant)";
            txtLiefArtikelname.Text = selected.CName ?? "";
            txtLiefArtikelnummer.Text = selected.CLiefArtNr ?? "";
            txtLiefNettoEK.Text = selected.FEKNetto?.ToString("N4") ?? "";
            txtLiefUSt.Text = selected.FMwSt?.ToString("N2") ?? "0,00";
            txtLiefKommentar.Text = selected.CSonstiges ?? "";
            txtVPEEinheit.Text = selected.CVPEEinheit ?? "";
            txtVPEMenge.Text = selected.NVPEMenge?.ToString("N0") ?? "";
            txtLiefMindestabnahme.Text = selected.NMindestAbnahme?.ToString() ?? "";
            txtLiefLieferzeit.Text = selected.NLieferzeit?.ToString() ?? "";
            txtLiefBestand.Text = selected.FLagerbestand?.ToString("N0") ?? "";
        }

        private async void LieferantHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Artikel speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedLieferant = cmbLieferantenAuswahl.SelectedItem as CoreService.LieferantRef;
            if (selectedLieferant == null)
            {
                MessageBox.Show("Bitte einen Lieferanten auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await _coreService.AddArtikelLieferantAsync(_artikelId.Value, selectedLieferant.KLieferant);
                await LadeLieferantenAsync();
                MessageBox.Show("Lieferant hinzugefuegt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StandardLieferantSetzen_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgArtikelLieferanten.SelectedItem as dynamic;
            if (selected == null)
            {
                MessageBox.Show("Bitte einen Lieferanten auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await _coreService.SetArtikelStandardLieferantAsync(_artikelId!.Value, (int)selected.KLieferant);
                await LadeLieferantenAsync();
                MessageBox.Show("Standardlieferant gesetzt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LieferantEntfernen_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgArtikelLieferanten.SelectedItem as dynamic;
            if (selected == null)
            {
                MessageBox.Show("Bitte einen Lieferanten auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Lieferant wirklich entfernen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _coreService.RemoveArtikelLieferantAsync(_artikelId!.Value, (int)selected.KLieferant);
                await LadeLieferantenAsync();
                MessageBox.Show("Lieferant entfernt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Tab: Bilder

        private async System.Threading.Tasks.Task LadeBilderAsync()
        {
            if (!_artikelId.HasValue) return;

            txtStatus.Text = "Lade Bilder...";
            var bilder = await _coreService.GetArtikelBilderAsync(_artikelId.Value);
            dgBilder.ItemsSource = bilder;
            txtStatus.Text = $"{bilder.Count()} Bilder geladen";
        }

        private void Bilder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgBilder.SelectedItem as dynamic;
            if (selected == null) return;

            // Bild-Vorschau laden (wenn vorhanden)
            try
            {
                if (selected.BVorschauBild != null)
                {
                    // Binary zu Image konvertieren
                    // imgVorschau.Source = ...
                }
            }
            catch { }
        }

        private void BildHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Artikel speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Bilder|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Alle Dateien|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                // TODO: Bilder hochladen
                MessageBox.Show($"{dialog.FileNames.Length} Bilder ausgewaehlt.\nUpload-Funktion wird implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BildLoeschen_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgBilder.SelectedItem as dynamic;
            if (selected == null)
            {
                MessageBox.Show("Bitte ein Bild auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Bild wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _coreService.DeleteArtikelBildAsync((int)selected.KBild);
                await LadeBilderAsync();
                MessageBox.Show("Bild geloescht!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Tab: Attribute/Merkmale

        private async System.Threading.Tasks.Task LadeAttributeAsync()
        {
            if (!_artikelId.HasValue) return;

            txtStatus.Text = "Lade Attribute und Merkmale...";

            var attribute = await _coreService.GetArtikelAttributeByArtikelAsync(_artikelId.Value);
            dgAttribute.ItemsSource = attribute;

            var merkmale = await _coreService.GetArtikelMerkmaleAsync(_artikelId.Value);
            dgMerkmale.ItemsSource = merkmale;

            txtStatus.Text = "Attribute und Merkmale geladen";
        }

        private void AttributZuweisen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Attribut-Zuweisung wird implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MerkmalZuweisen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Merkmal-Zuweisung wird implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Tab: Stueckliste

        private async System.Threading.Tasks.Task LadeStuecklisteAsync()
        {
            if (!_artikelId.HasValue) return;

            txtStatus.Text = "Lade Stueckliste...";
            var stueckliste = await _coreService.GetArtikelStuecklisteAsync(_artikelId.Value);
            dgStueckliste.ItemsSource = stueckliste;

            // Summen berechnen
            var nettoSumme = stueckliste.Sum(s => s.FNettoVK * s.FMenge);
            var bruttoSumme = nettoSumme * 1.19m;
            txtStuecklisteNetto.Text = $"Netto-VK (alle Komponenten): {nettoSumme:N2}";
            txtStuecklisteBrutto.Text = $"(Brutto-VK: {bruttoSumme:N2})";

            txtStatus.Text = $"{stueckliste.Count()} Komponenten geladen";
        }

        private async void StuecklisteHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Artikel speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Artikelauswahl-Dialog oeffnen
            var dialog = new ArtikelSuchDialog();
            if (dialog.ShowDialog() == true && dialog.AusgewaehlterArtikel != null)
            {
                try
                {
                    await _coreService.AddArtikelZuStuecklisteAsync(_artikelId.Value, dialog.AusgewaehlterArtikel.KArtikel, 1);
                    await LadeStuecklisteAsync();
                    MessageBox.Show("Komponente hinzugefuegt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void StuecklisteEntfernen_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgStueckliste.SelectedItem as dynamic;
            if (selected == null)
            {
                MessageBox.Show("Bitte eine Komponente auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Komponente wirklich entfernen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _coreService.RemoveArtikelVonStuecklisteAsync((int)selected.KStueckliste);
                await LadeStuecklisteAsync();
                MessageBox.Show("Komponente entfernt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Tab: Sonderpreise

        private async System.Threading.Tasks.Task LadeSonderpreiseAsync()
        {
            if (!_artikelId.HasValue) return;

            try
            {
                txtStatus.Text = "Lade Sonderpreise...";
                var sonderpreise = await _coreService.GetArtikelSonderpreiseAsync(_artikelId.Value);

                // Sonderpreis-Einstellungen (Header-Daten)
                var config = sonderpreise.FirstOrDefault();
                if (config != null)
                {
                    chkSonderpreiseAktiv.IsChecked = config.NAktiv == 1;
                    chkSonderpreiseDatum.IsChecked = config.NIstDatum == 1;
                    dpSonderpreisVon.SelectedDate = config.DStart;
                    dpSonderpreisBis.SelectedDate = config.DEnde;
                    chkSonderpreiseMenge.IsChecked = config.NIstAnzahl == 1;
                    txtSonderpreisMenge.Text = config.NAnzahl?.ToString() ?? "";
                }

                // Kundengruppen laden und mit vorhandenen Sonderpreisen mergen
                var kundengruppen = await _coreService.GetKundengruppenAsync();
                // GroupBy verwenden um doppelte Eintraege zu vermeiden
                var sonderpreisDict = sonderpreise
                    .Where(sp => sp.KKundenGruppe.HasValue)
                    .GroupBy(sp => sp.KKundenGruppe!.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                var sonderpreisListe = kundengruppen.Select(kg =>
                {
                    var hasPreis = sonderpreisDict.TryGetValue(kg.KKundenGruppe, out var existingPreis);
                    return new SonderpreisViewModel
                    {
                        KKundenGruppe = kg.KKundenGruppe,
                        CKundengruppe = kg.CName ?? "",
                        NAktiv = hasPreis && existingPreis!.FSonderpreisNetto > 0,
                        FSonderpreisNetto = hasPreis ? existingPreis!.FSonderpreisNetto : 0m,
                        FSteuersatz = 19m
                    };
                }).ToList();

                dgSonderpreise.ItemsSource = sonderpreisListe;
                txtStatus.Text = "Sonderpreise geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Sonderpreise-Fehler: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Sonderpreise-Fehler: {ex}");
            }
        }

        #endregion

        #region Tab: Eigene Felder

        private async void EigeneFelderSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Artikel speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                txtStatus.Text = "Speichere eigene Felder...";

                var felder = new Dictionary<string, string?>
                {
                    ["Ambient"] = chkValAmbient.IsChecked == true ? "1" : "0",
                    ["Cool"] = chkValCool.IsChecked == true ? "1" : "0",
                    ["Medcan"] = chkValMedcan.IsChecked == true ? "1" : "0",
                    ["Tierarznei"] = chkValTierarznei.IsChecked == true ? "1" : "0",
                    ["SecurPharm"] = chkValSecurPharm.IsChecked == true ? "1" : "0",
                    ["MHDPflichtig"] = chkMHDPflichtig.IsChecked == true ? "1" : "0",
                    ["MHDWarnung"] = txtMHDWarnung.Text
                };

                await _coreService.SetArtikelEigeneFelderAsync(_artikelId.Value, felder);

                txtStatus.Text = "Eigene Felder gespeichert";
                MessageBox.Show("Eigene Felder wurden gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion

        #region Speichern

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Speichere...";

                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Bitte Artikelname angeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Daten aus Formular
                _artikel!.CArtNr = txtArtNrEdit.Text.Trim();
                _artikel.CBarcode = txtGTIN.Text.Trim();
                _artikel.CHAN = txtHAN.Text.Trim();
                _artikel.Name = txtName.Text.Trim();

                _artikel.KHersteller = cmbHersteller.SelectedValue as int?;
                _artikel.KSteuerklasse = cmbSteuerklasse.SelectedValue as int?;
                _artikel.KVersandklasse = cmbVersandklasse.SelectedValue as int?;
                _artikel.KWarengruppe = cmbWarengruppe.SelectedValue as int?;

                _artikel.CAktiv = chkInaktiv.IsChecked == true ? "N" : "Y";
                _artikel.CTopArtikel = chkTopArtikel.IsChecked == true ? "Y" : "N";
                _artikel.CNeu = chkNeuImSortiment.IsChecked == true ? "Y" : "N";

                _artikel.FVKNetto = decimal.TryParse(txtVKNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var vk) ? vk : 0;
                _artikel.FUVP = decimal.TryParse(txtUVP.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var uvp) ? uvp : 0;
                _artikel.FEKNetto = decimal.TryParse(txtEKNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ek) ? ek : 0;

                _artikel.CLagerArtikel = chkBestandsfuehrung.IsChecked == true ? "Y" : "N";

                if (decimal.TryParse(txtGewicht.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var gewicht))
                    _artikel.FGewicht = gewicht;

                if (_artikelId.HasValue)
                {
                    await _coreService.UpdateArtikelAsync(_artikel);
                    txtStatus.Text = "Artikel gespeichert";
                    MessageBox.Show("Artikel wurde gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var newId = await _coreService.CreateArtikelAsync(_artikel);
                    _artikel.KArtikel = newId;
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

        #endregion

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                // Versuche Zurueck-Navigation, sonst zur Liste
                if (!main.NavigateBack())
                {
                    main.ShowContent(App.Services.GetRequiredService<ArtikelView>(), pushToStack: false);
                }
            }
        }
    }

    public class SonderpreisViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _nAktiv;
        private decimal _fSonderpreisNetto;

        public int KKundenGruppe { get; set; }
        public string CKundengruppe { get; set; } = "";
        public decimal FSteuersatz { get; set; } = 19m;

        public bool NAktiv
        {
            get => _nAktiv;
            set { _nAktiv = value; OnPropertyChanged(nameof(NAktiv)); }
        }

        public decimal FSonderpreisNetto
        {
            get => _fSonderpreisNetto;
            set
            {
                _fSonderpreisNetto = value;
                OnPropertyChanged(nameof(FSonderpreisNetto));
                OnPropertyChanged(nameof(FSonderpreisBrutto));
            }
        }

        public decimal FSonderpreisBrutto => FSonderpreisNetto * (1 + FSteuersatz / 100);

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
