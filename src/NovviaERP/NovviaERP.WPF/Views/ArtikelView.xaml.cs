using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly AppDataService _appData;
        private List<ArtikelListItem> _artikel = new();
        private List<KategorieNode> _kategorien = new();
        private int? _selectedKategorieId;
        private bool _initialized = false;
        private CancellationTokenSource? _searchCts;

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

            // Spalten-Konfiguration aktivieren (Rechtsklick auf Header)
            DataGridColumnConfig.EnableColumnChooser(dgArtikel, "ArtikelViewJTL");

            try
            {
                txtStatus.Text = "Lade Kategorien...";
                await LadeKategorienAsync();
                txtStatus.Text = "Lade Artikel...";
                await LadeArtikelAsync();
                txtStatus.Text = "Bereit";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
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
            public bool Aktiv { get; set; }
            public string? Hersteller { get; set; }
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
                    FEKNetto = a.FEKNetto,
                    FUVP = a.FUVP,
                    NLagerbestand = a.NLagerbestand,
                    NMidestbestand = a.NMidestbestand,
                    Verfuegbar = a.NLagerbestand, // TODO: Reservierungen abziehen
                    AufEinkaufsliste = 0, // TODO: Aus tEinkaufsliste
                    InAuftraegen = 0, // TODO: Aus offenen Auftraegen
                    ImZulauf = 0, // TODO: Aus Lieferantenbestellungen
                    Aktiv = a.Aktiv,
                    Hersteller = a.Hersteller
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

        #endregion

        #region Artikel Details (bei Auswahl)

        private async void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgArtikel.SelectedItem is ArtikelListItem artikel)
            {
                await LadeArtikelDetailsAsync(artikel.KArtikel);
            }
            else
            {
                dgLieferanten.ItemsSource = null;
                dgPreise.ItemsSource = null;
                dgSonderpreise.ItemsSource = null;
                dgHistory.ItemsSource = null;
            }
        }

        private async Task LadeArtikelDetailsAsync(int kArtikel)
        {
            try
            {
                // Lieferanten laden
                var lieferanten = await _coreService.GetArtikelLieferantenAsync(kArtikel);
                dgLieferanten.ItemsSource = lieferanten.Select(l => new
                {
                    Kontakt = l.Kontakt,
                    Firmenname = l.Firmenname
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

        #region Sub-Tabs

        private void SubTab_Click(object sender, RoutedEventArgs e)
        {
            // Reset all buttons
            btnHistory.Background = System.Windows.Media.Brushes.Transparent;
            btnVkJeKunde.Background = System.Windows.Media.Brushes.Transparent;
            btnVkMonat.Background = System.Windows.Media.Brushes.Transparent;
            btnVkUebersicht.Background = System.Windows.Media.Brushes.Transparent;
            btnChargen.Background = System.Windows.Media.Brushes.Transparent;

            if (sender is Button btn)
            {
                btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224));
                // TODO: Switch content based on tag
            }
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

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgArtikel.SelectedItem is ArtikelListItem artikel)
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

        #endregion
    }
}
