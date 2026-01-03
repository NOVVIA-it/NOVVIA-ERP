using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly AppDataService _appData;
        private List<ArtikelListItem> _artikel = new();
        private List<KategorieNode> _kategorien = new();
        private int? _selectedKategorieId;
        private int? _selectedArtikelId;
        private bool _initialized = false;
        private CancellationTokenSource? _searchCts;
        private List<CoreService.CustomQueryInfo> _customQueries = new();
        private int? _activeCustomQueryId;

        public ArtikelView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            _appData = App.Services.GetRequiredService<AppDataService>();
            Loaded += async (s, e) => await InitializeAsync();
        }

        #region Initialisierung

        private async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            // NovviaGrid formatiert sich selbst (ViewName="ArtikelView")

            // Splitter-Position laden
            await LadeSplitterPositionAsync();

            try
            {
                txtStatus.Text = "Lade Kategorien...";
                await LadeKategorienAsync();
                txtStatus.Text = "Lade Custom Queries...";
                await LadeCustomQueriesAsync();
                txtStatus.Text = "Lade Artikel...";
                await LadeArtikelAsync();
                txtStatus.Text = "Bereit";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async Task LadeCustomQueriesAsync()
        {
            try
            {
                // Custom Queries für Artikel laden (kKontext = 2)
                _customQueries = (await _coreService.GetCustomQueriesAsync(2)).ToList();

                if (spEigeneUebersichten != null)
                {
                    // Alle dynamischen Buttons entfernen (ausser + Button)
                    var buttonsToRemove = spEigeneUebersichten.Children.OfType<Button>()
                        .Where(b => b.Tag is int).ToList();
                    foreach (var btn in buttonsToRemove)
                        spEigeneUebersichten.Children.Remove(btn);

                    // Neue Buttons für Custom Queries hinzufügen (vor dem + Button)
                    int insertIndex = 0;
                    bool isFirst = true;
                    foreach (var query in _customQueries)
                    {
                        var btn = new Button
                        {
                            Content = query.CName,
                            Padding = new Thickness(10, 5, 10, 5),
                            FontSize = 11,
                            Background = isFirst
                                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224))
                                : System.Windows.Media.Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            Cursor = System.Windows.Input.Cursors.Hand,
                            Tag = query.KCustomerQuery
                        };
                        btn.Click += CustomQuerySubTab_Click;
                        spEigeneUebersichten.Children.Insert(insertIndex++, btn);

                        if (isFirst)
                        {
                            _activeCustomQueryId = query.KCustomerQuery;
                            isFirst = false;
                        }
                    }
                }
            }
            catch { }
        }

        private async void CustomQuerySubTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int queryId)
                return;

            // Alle Sub-Tab Buttons zuruecksetzen
            ResetEigeneUebersichtenButtons();
            btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224));

            _activeCustomQueryId = queryId;

            // Query ausfuehren wenn ein Artikel ausgewaehlt ist
            if (_selectedArtikelId.HasValue)
            {
                await ExecuteCustomQueryAsync(queryId, _selectedArtikelId.Value);
            }
        }

        private void ResetEigeneUebersichtenButtons()
        {
            // Alle dynamischen Buttons zuruecksetzen
            var transparent = System.Windows.Media.Brushes.Transparent;
            if (spEigeneUebersichten != null)
            {
                foreach (var btn in spEigeneUebersichten.Children.OfType<Button>().Where(b => b.Tag is int))
                {
                    btn.Background = transparent;
                }
            }
        }

        private void AddCustomQuery_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Dialog zum Erstellen einer neuen Custom Query öffnen
            MessageBox.Show("Neue Übersicht erstellen...\n\nDiese Funktion wird in einer zukünftigen Version verfügbar sein.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ExecuteCustomQueryAsync(int queryId, int kArtikel)
        {
            try
            {
                var results = await _coreService.ExecuteCustomQueryAsync(queryId, kArtikel);
                dgHistory.ItemsSource = results.ToList();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Query-Fehler: {ex.Message}";
            }
        }

        #endregion

        #region Kategorien

        public class KategorieNode
        {
            public int KKategorie { get; set; }
            public int? KOberKategorie { get; set; }
            public string Name { get; set; } = "";
            public ObservableCollection<KategorieNode> Children { get; set; } = new();
        }

        private async Task LadeKategorienAsync()
        {
            var kategorien = (await _coreService.GetKategorienAsync()).ToList();

            // Hierarchie aufbauen
            var nodeDict = kategorien.ToDictionary(
                k => k.KKategorie,
                k => new KategorieNode
                {
                    KKategorie = k.KKategorie,
                    KOberKategorie = k.KOberKategorie,
                    Name = k.CName ?? $"Kategorie {k.KKategorie}"
                });

            var rootNodes = new List<KategorieNode>();

            foreach (var node in nodeDict.Values)
            {
                if (node.KOberKategorie.HasValue && nodeDict.TryGetValue(node.KOberKategorie.Value, out var parent))
                {
                    parent.Children.Add(node);
                }
                else
                {
                    rootNodes.Add(node);
                }
            }

            _kategorien = rootNodes;
            tvKategorien.ItemsSource = _kategorien;
            txtKategorieAnzahl.Text = $"{kategorien.Count} Kategorie(n)";
        }

        private async void TvKategorien_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is KategorieNode node)
            {
                _selectedKategorieId = node.KKategorie;
                await LadeArtikelAsync();
            }
        }

        #endregion

        #region Artikel laden

        public class ArtikelListItem
        {
            public int KArtikel { get; set; }
            public string? CArtNr { get; set; }
            public string? Name { get; set; }
            public decimal FVKNetto { get; set; }
            public decimal FVKBrutto { get; set; }
            public decimal FEKNetto { get; set; }
            public decimal FUVP { get; set; }
            public decimal NLagerbestand { get; set; }
            public decimal NMidestbestand { get; set; }
            public bool UnterMindestbestand => NLagerbestand < NMidestbestand && NMidestbestand > 0;
            public decimal Verfuegbar { get; set; }
            public decimal AufEinkaufsliste { get; set; }
            public decimal InAuftraegen { get; set; }
            public decimal ImZulauf { get; set; }
            public decimal Gewinn => FVKNetto - FEKNetto;
            public decimal GewinnProzent => FEKNetto > 0 ? Math.Round(((FVKNetto - FEKNetto) / FEKNetto) * 100, 2) : 0;
            public bool Aktiv { get; set; }
            public string? Hersteller { get; set; }
            public string? Warengruppe { get; set; }

            // Erweiterte Felder
            public string? CBarcode { get; set; }
            public string? CHAN { get; set; }
            public string? CASIN { get; set; }
            public string? CISBN { get; set; }
            public string? CUPC { get; set; }
            public string? CTaric { get; set; }
            public int NSort { get; set; }
            public DateTime? DErstellt { get; set; }
            public DateTime? DLetzteAenderung { get; set; }
            public decimal FGewicht { get; set; }
            public string? CMasseinheit { get; set; }
            public bool TopArtikel { get; set; }
            public bool Neu { get; set; }
            public bool OnlineShop { get; set; }
        }

        private async Task LadeArtikelAsync()
        {
            try
            {
                var nurAktive = cmbArtikelFilter.SelectedIndex == 0;
                var nurInaktive = cmbArtikelFilter.SelectedIndex == 2;

                var artikelRaw = await _coreService.GetArtikelAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text,
                    kategorieId: _selectedKategorieId,
                    nurAktive: nurAktive,
                    limit: 500
                );

                _artikel = artikelRaw.Select(a => new ArtikelListItem
                {
                    KArtikel = a.KArtikel,
                    CArtNr = a.CArtNr,
                    Name = a.Name,
                    FVKNetto = a.FVKNetto,
                    FVKBrutto = a.FVKBrutto,
                    FEKNetto = a.FEKNetto,
                    FUVP = a.FUVP,
                    NLagerbestand = a.NLagerbestand,
                    NMidestbestand = a.NMidestbestand,
                    Verfuegbar = a.FVerfuegbar,
                    AufEinkaufsliste = a.FAufEinkaufsliste,
                    InAuftraegen = a.FInAuftraegen,
                    ImZulauf = a.FImZulauf,
                    Aktiv = a.Aktiv,
                    Hersteller = a.Hersteller,
                    Warengruppe = a.Warengruppe,
                    CBarcode = a.CBarcode,
                    CHAN = a.CHAN,
                    CASIN = a.CASIN,
                    CISBN = a.CISBN,
                    CUPC = a.CUPC,
                    CTaric = a.CTaric,
                    NSort = a.NSort,
                    DErstellt = a.DErstellt,
                    DLetzteAenderung = a.DLetzteAenderung,
                    FGewicht = a.FGewicht,
                    CMasseinheit = a.CMasseinheit,
                    TopArtikel = a.NTopArtikel == 1,
                    Neu = a.NNeu == 1,
                    OnlineShop = a.NOnlineShop == 1
                }).ToList();

                if (nurInaktive)
                    _artikel = _artikel.Where(a => !a.Aktiv).ToList();

                dgArtikel.ItemsSource = _artikel;
                txtArtikelAnzahl.Text = $"{_artikel.Count} Artikel";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion

        #region Suche

        private async void TxtSuche_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Debounce
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;
                await LadeArtikelAsync();
            }
            catch (TaskCanceledException) { }
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _searchCts?.Cancel();
                await LadeArtikelAsync();
            }
        }

        private async void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSuche.Text = "";
            await LadeArtikelAsync();
        }

        private async void CmbArtikelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nur reagieren wenn initialisiert
            if (!_initialized) return;
            await LadeArtikelAsync();
        }

        #endregion

        #region Artikel Details (bei Auswahl)

        private async void DG_SelectionChanged(object? sender, object? e)
        {
            if (e is ArtikelListItem artikel)
            {
                _selectedArtikelId = artikel.KArtikel;
                await LadeArtikelDetailsAsync(artikel.KArtikel);

                // Custom Query ausfuehren wenn eine aktiv ist
                if (_activeCustomQueryId.HasValue)
                {
                    await ExecuteCustomQueryAsync(_activeCustomQueryId.Value, artikel.KArtikel);
                }
            }
            else
            {
                _selectedArtikelId = null;
                ClearArtikelDetails();
            }
        }

        private void ClearArtikelDetails()
        {
            dgBestaende.ItemsSource = null;
            dgLieferanten.ItemsSource = null;
            dgPreise.ItemsSource = null;
            dgSonderpreise.ItemsSource = null;
            dgVerknuepfte.ItemsSource = null;
            dgHistory.ItemsSource = null;
        }

        private async Task LadeArtikelDetailsAsync(int kArtikel)
        {
            try
            {
                // Bestände laden
                var bestaende = await _coreService.GetArtikelBestaendeAsync(kArtikel);
                dgBestaende.ItemsSource = bestaende.Select(b => new
                {
                    Lager = b.CLagerName ?? "",
                    Menge = b.FAufLager,
                    Verfuegbar = b.FAufLager // TODO: Reservierungen abziehen
                }).ToList();

                // Lieferanten laden
                var lieferanten = await _coreService.GetArtikelLieferantenAsync(kArtikel);
                dgLieferanten.ItemsSource = lieferanten.Select(l => new
                {
                    Kontakt = l.Kontakt,
                    Firmenname = l.Firmenname
                }).ToList();

                // Preise laden (Kundengruppen-Preise)
                var preise = await _coreService.GetArtikelKundengruppenPreiseAsync(kArtikel);
                dgPreise.ItemsSource = preise.Select(p => new
                {
                    Kundengruppe = p.CKundengruppe ?? "",
                    VKNetto = p.FNettoPreis,
                    VKBrutto = p.FNettoPreis * 1.19m // TODO: Steuersatz aus Artikel
                }).ToList();

                // Sonderpreise laden
                var sonderpreise = await _coreService.GetArtikelSonderpreiseAsync(kArtikel);
                dgSonderpreise.ItemsSource = sonderpreise.Select(s => new
                {
                    Typ = s.CKundengruppe ?? "Sonderpreis",
                    Preis = s.FSonderpreisNetto,
                    GueltigBis = s.DEnde
                }).ToList();

                // History laden
                var history = await _coreService.GetArtikelHistoryAsync(kArtikel, 50);
                dgHistory.ItemsSource = history.Select(h => new
                {
                    Datum = h.Datum,
                    Anzahl = h.Anzahl,
                    Art = h.Art,
                    Auftrag = h.AuftragNr,
                    Kommentar = h.Kommentar,
                    MHD = h.MHD,
                    Charge = h.Charge,
                    Kunde = h.KundenNr,
                    Firma = h.Firma
                }).ToList();
            }
            catch { }
        }

        #endregion

        #region Navigation

        private void NavigateTo(UserControl view)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(view);
            }
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(new ArtikelDetailView(null));
        }

        private void DG_DoubleClick(object? sender, object? e)
        {
            if (e is ArtikelListItem artikel)
            {
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
            }
        }

        #endregion

        #region Kontextmenue

        private void ContextMenu_Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ArtikelListItem artikel)
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
        }

        private void ContextMenu_Duplizieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ArtikelListItem artikel)
                NavigateTo(new ArtikelDetailView(artikel.KArtikel, duplizieren: true));
        }

        private void ContextMenu_Lagerbestand_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ArtikelListItem artikel)
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
        }

        private void ContextMenu_Preise_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ArtikelListItem artikel)
                NavigateTo(new ArtikelDetailView(artikel.KArtikel));
        }

        private async void ContextMenu_Deaktivieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not ArtikelListItem artikel) return;

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
            if (dgArtikel.SelectedItem is not ArtikelListItem artikel) return;

            var result = MessageBox.Show(
                $"Artikel '{artikel.Name}' wirklich LOESCHEN?\n\nDiese Aktion kann nicht rueckgaengig gemacht werden!",
                "Artikel loeschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _coreService.DeleteArtikelAsync(artikel.KArtikel);
                txtStatus.Text = $"Artikel {artikel.CArtNr} geloescht";
                await LadeArtikelAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async void ContextMenu_Aktivieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not ArtikelListItem artikel) return;

            try
            {
                await _coreService.SetArtikelAktivAsync(artikel.KArtikel, true);
                txtStatus.Text = $"Artikel {artikel.CArtNr} aktiviert";
                await LadeArtikelAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void ContextMenu_KategorieVerschieben_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Dialog zum Verschieben in eine andere Kategorie
            MessageBox.Show("Funktion noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ContextMenu_KategorieEntfernen_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Artikel aus aktueller Kategorie entfernen
            MessageBox.Show("Funktion noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnArtikel_Click(object sender, RoutedEventArgs e)
        {
            // Öffne das ContextMenu des Buttons
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        #endregion

        #region Splitter-Speicherung

        private async Task LadeSplitterPositionAsync()
        {
            try
            {
                // Kategorien-Breite (oben links)
                var breite = await _coreService.GetBenutzerEinstellungAsync(App.BenutzerId, "ArtikelView.KategorienBreite");
                if (!string.IsNullOrEmpty(breite) && double.TryParse(breite, out var width) && width >= 180)
                {
                    colKategorien.Width = new GridLength(width);
                }

                // Detail-Hoehe (unten)
                var hoehe = await _coreService.GetBenutzerEinstellungAsync(App.BenutzerId, "ArtikelView.DetailHoehe");
                if (!string.IsNullOrEmpty(hoehe) && double.TryParse(hoehe, out var height) && height >= 120)
                {
                    rowUnten.Height = new GridLength(height);
                }

                // Detail-Links Breite (unten links)
                var detailBreite = await _coreService.GetBenutzerEinstellungAsync(App.BenutzerId, "ArtikelView.DetailLinksBreite");
                if (!string.IsNullOrEmpty(detailBreite) && double.TryParse(detailBreite, out var dw) && dw >= 200)
                {
                    colDetailLinks.Width = new GridLength(dw);
                }
            }
            catch { }
        }

        private async void SplitterKategorien_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                var width = colKategorien.ActualWidth;
                await _coreService.SaveBenutzerEinstellungAsync(App.BenutzerId, "ArtikelView.KategorienBreite", width.ToString("F0"));
            }
            catch { }
        }

        private async void SplitterHorizontal_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                var height = rowUnten.ActualHeight;
                await _coreService.SaveBenutzerEinstellungAsync(App.BenutzerId, "ArtikelView.DetailHoehe", height.ToString("F0"));
            }
            catch { }
        }

        private async void SplitterDetailUnten_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            try
            {
                var width = colDetailLinks.ActualWidth;
                await _coreService.SaveBenutzerEinstellungAsync(App.BenutzerId, "ArtikelView.DetailLinksBreite", width.ToString("F0"));
            }
            catch { }
        }

        #endregion
    }
}
