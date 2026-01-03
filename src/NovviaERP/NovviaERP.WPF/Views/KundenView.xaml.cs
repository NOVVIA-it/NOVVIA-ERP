using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Views
{
    public partial class KundenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.KundeUebersicht> _kunden = new();
        private CoreService.KundeUebersicht? _selectedKunde;
        private int _currentPage = 0;
        private const int PageSize = 1000;

        public KundenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await InitAsync();
            Unloaded += async (s, e) => await SpeichereSplitterEinstellungenAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                // ZENTRALE GRID-INITIALISIERUNG: Styling + Spalten-Konfiguration in richtiger Reihenfolge
                await GridStyleHelper.Instance.LoadSettingsAsync(_core, App.BenutzerId);
                GridStyleHelper.InitializeGrid(dgKunden, "KundenView");
                GridStyleHelper.InitializeGrid(dgKundeAuftraege, "KundenView.Auftraege");
                GridStyleHelper.InitializeGrid(dgKundeRechnungen, "KundenView.Rechnungen");
                GridStyleHelper.InitializeGrid(dgKundeAdressen, "KundenView.Adressen");
                GridStyleHelper.InitializeGrid(dgAnsprechpartner, "KundenView.Ansprechpartner");

                // Kundengruppen laden
                var gruppen = (await _core.GetKundengruppenAsync()).ToList();
                cboKundengruppe.Items.Clear();
                cboKundengruppe.Items.Add(new ComboBoxItem { Content = "Alle Gruppen", IsSelected = true });
                foreach (var g in gruppen)
                    cboKundengruppe.Items.Add(new ComboBoxItem { Content = g.CName, Tag = g.KKundenGruppe });

                // Kundenkategorien laden
                var kategorien = (await _core.GetKundenkategorienAsync()).ToList();
                cboKategorie.Items.Clear();
                cboKategorie.Items.Add(new ComboBoxItem { Content = "Alle Kategorien", IsSelected = true });
                foreach (var k in kategorien)
                    cboKategorie.Items.Add(new ComboBoxItem { Content = k.CName, Tag = k.KKundenKategorie });

                // Kunden laden
                await LadeKundenAsync();

                // Splitter-Positionen laden
                await LadeSplitterEinstellungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LadeSplitterEinstellungenAsync()
        {
            try
            {
                var hauptSplitter = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "KundenView.KundenlisteHoehe");
                if (!string.IsNullOrEmpty(hauptSplitter) && double.TryParse(hauptSplitter, out double h1))
                    kundenlisteRow.Height = new GridLength(h1, GridUnitType.Star);

                var detailSplitter = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "KundenView.DetailsHoehe");
                if (!string.IsNullOrEmpty(detailSplitter) && double.TryParse(detailSplitter, out double h2))
                    detailsRow.Height = new GridLength(h2, GridUnitType.Star);

                var sichtSplitter = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "KundenView.SichtHoehe");
                if (!string.IsNullOrEmpty(sichtSplitter) && double.TryParse(sichtSplitter, out double h3))
                    sichtRow.Height = new GridLength(h3, GridUnitType.Star);

                var tabsSplitter = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "KundenView.TabsHoehe");
                if (!string.IsNullOrEmpty(tabsSplitter) && double.TryParse(tabsSplitter, out double h4))
                    tabsRow.Height = new GridLength(h4, GridUnitType.Star);
            }
            catch { /* Ignorieren */ }
        }

        private async Task SpeichereSplitterEinstellungenAsync()
        {
            try
            {
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "KundenView.KundenlisteHoehe", kundenlisteRow.Height.Value.ToString());
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "KundenView.DetailsHoehe", detailsRow.Height.Value.ToString());
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "KundenView.SichtHoehe", sichtRow.Height.Value.ToString());
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "KundenView.TabsHoehe", tabsRow.Height.Value.ToString());
            }
            catch { /* Ignorieren */ }
        }

        private void NavigateTo(UserControl view)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.ShowContent(view);
        }

        private async Task LadeKundenAsync()
        {
            try
            {
                var suche = string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text.Trim();
                var plz = string.IsNullOrWhiteSpace(txtPLZ.Text) ? null : txtPLZ.Text.Trim();
                int? gruppeId = null;
                int? kategorieId = null;

                if (cboKundengruppe.SelectedItem is ComboBoxItem gruppeItem && gruppeItem.Tag is int gId)
                    gruppeId = gId;
                if (cboKategorie.SelectedItem is ComboBoxItem katItem && katItem.Tag is int kId)
                    kategorieId = kId;

                _kunden = (await _core.GetKundenAsync(suche: suche, plz: plz, kundengruppeId: gruppeId, kategorieId: kategorieId, limit: PageSize)).ToList();
                dgKunden.ItemsSource = _kunden;
                txtAnzahl.Text = $"{_kunden.Count} Kunde(n)";
                _currentPage = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeKundenAsync();
        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await LadeKundenAsync(); }
        private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeKundenAsync();
        }

        private async void FilterReset_Click(object sender, RoutedEventArgs e)
        {
            txtSuche.Text = "";
            txtPLZ.Text = "";
            txtAuftragRechnung.Text = "";
            txtLabel.Text = "";
            cboKundengruppe.SelectedIndex = 0;
            cboKategorie.SelectedIndex = 0;
            await LadeKundenAsync();
        }

        private async void WeitereKundenLaden_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentPage++;
                var suche = string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text.Trim();
                int? gruppeId = null;
                if (cboKundengruppe.SelectedItem is ComboBoxItem item && item.Tag is int id)
                    gruppeId = id;

                var weitereKunden = (await _core.GetKundenAsync(suche: suche, kundengruppeId: gruppeId, limit: PageSize, offset: _currentPage * PageSize)).ToList();
                if (weitereKunden.Any())
                {
                    _kunden.AddRange(weitereKunden);
                    dgKunden.ItemsSource = null;
                    dgKunden.ItemsSource = _kunden;
                    txtAnzahl.Text = $"{_kunden.Count} Kunde(n)";
                }
                else
                {
                    MessageBox.Show("Keine weiteren Kunden vorhanden.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Neu_Click(object sender, RoutedEventArgs e) => NavigateTo(new KundeDetailView(null));

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKunde != null)
                NavigateTo(new KundeDetailView(_selectedKunde.KKunde));
        }

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKunde == null) return;

            var result = MessageBox.Show(
                $"Kunde '{_selectedKunde.Anzeigename}' wirklich loeschen?",
                "Kunde loeschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // TODO: Implement delete in CoreService
                    MessageBox.Show("Loeschen ist noch nicht implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Loeschen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedKunde != null)
                NavigateTo(new KundeDetailView(_selectedKunde.KKunde));
        }

        private async void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedKunde = dgKunden.SelectedItem as CoreService.KundeUebersicht;
            var hasSelection = _selectedKunde != null;

            btnKunde.IsEnabled = hasSelection;
            btnAusgabe.IsEnabled = hasSelection;
            btnNachricht.IsEnabled = hasSelection;
            btnWorkflow.IsEnabled = hasSelection;

            if (_selectedKunde != null)
            {
                await Lade360GradAnsichtAsync(_selectedKunde);
                pnlTabs.Visibility = Visibility.Visible;
            }
            else
            {
                pnlDetails.Visibility = Visibility.Collapsed;
                pnlNoSelection.Visibility = Visibility.Visible;
                pnlTabs.Visibility = Visibility.Collapsed;
            }
        }

        private async Task Lade360GradAnsichtAsync(CoreService.KundeUebersicht kunde)
        {
            try
            {
                pnlNoSelection.Visibility = Visibility.Collapsed;
                pnlDetails.Visibility = Visibility.Visible;

                // Kontaktdaten
                txtKundeAnrede.Text = kunde.CAnrede ?? "";
                txtKundeName.Text = $"{kunde.CVorname} {kunde.CName}".Trim();
                if (string.IsNullOrWhiteSpace(txtKundeName.Text))
                    txtKundeName.Text = kunde.CFirma ?? "-";
                txtKundeFirma.Text = kunde.CFirma ?? "";
                txtKundeNr.Text = kunde.CKundenNr ?? "";

                txtKundeAdresse1.Text = kunde.CStrasse ?? "";
                txtKundeAdresse2.Text = $"{kunde.CPLZ} {kunde.COrt}".Trim();
                txtKundeLand.Text = kunde.CLand ?? kunde.CISO ?? "Deutschland";

                txtKundeTel.Text = kunde.CTel ?? "-";
                txtKundeMobil.Text = kunde.CMobil ?? "-";
                txtKundeFax.Text = kunde.CFax ?? "-";
                txtKundeMail.Text = kunde.CMail ?? "-";

                // Status Icons
                iconGesperrt.Visibility = !string.IsNullOrEmpty(kunde.CSperre) ? Visibility.Visible : Visibility.Collapsed;

                // Statistiken laden
                var stats = await _core.GetKundeStatistikAsync(kunde.KKunde);

                txtStatLetzterAuftrag.Text = stats.LetzteBestellung?.ToString("dd.MM.yyyy") ?? "-";
                txtStatKundeSeit.Text = stats.ErstBestellung?.ToString("dd.MM.yyyy") ?? "-";
                txtStatOffeneAuftraege.Text = $"{stats.OffeneAuftraege} / {stats.OffeneAuftraegeWert:N2} EUR";
                txtStatOffeneRechnungen.Text = $"{stats.OffeneRechnungen} / {stats.OffenePosten:N2} EUR";

                txtStatUmsatz.Text = $"{stats.UmsatzGesamt:N2} EUR";
                txtStatGewinn.Text = $"{stats.GewinnGesamt:N2} EUR";
                txtStatWarenkorb.Text = $"{stats.DurchschnittWarenkorb:N2} EUR";
                txtStatAuftraege.Text = stats.AnzahlAuftraege.ToString();
                txtStatRetouren.Text = $"{stats.AnzahlRetouren} / {stats.AnzahlAuftraege}";
                txtStatStornos.Text = $"{stats.AnzahlStornos} / {stats.AnzahlAuftraege}";

                txtStatCoupon.Text = $"{stats.CouponKaeufe} / {stats.AnzahlAuftraege}";
                txtStatGuthaben.Text = $"{stats.Guthaben:N2} EUR";
                txtStatRabatt.Text = $"{stats.Rabatt:N0} %";

                txtAnmerkung.Text = stats.Anmerkung ?? "";
                chkNewsletter.IsChecked = stats.Newsletter;

                // Tabs laden (parallel)
                var auftraegeTask = _core.GetKundeAuftraegeAsync(kunde.KKunde);
                var rechnungenTask = _core.GetKundeRechnungenAsync(kunde.KKunde);
                var adressenTask = _core.GetKundeAdressenKurzAsync(kunde.KKunde);
                var historieTask = _core.GetKundeHistorieAsync(kunde.KKunde);

                await Task.WhenAll(auftraegeTask, rechnungenTask, adressenTask, historieTask);

                dgKundeAuftraege.ItemsSource = auftraegeTask.Result.ToList();
                dgKundeRechnungen.ItemsSource = rechnungenTask.Result.ToList();
                dgKundeAdressen.ItemsSource = adressenTask.Result.ToList();

                // Historie mit Icons formatieren
                var historie = historieTask.Result.Select(h => new HistorieEintrag
                {
                    Beschreibung = h.Beschreibung,
                    DatumFormatiert = h.Datum.ToString("dd.MM.yyyy - HH:mm"),
                    Icon = GetHistorieIcon(h.Typ),
                    IconBg = GetHistorieIconBg(h.Typ)
                }).ToList();

                lstHistorie.ItemsSource = historie;
                txtKeineHistorie.Visibility = historie.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler 360°: {ex.Message}");
            }
        }

        private string GetHistorieIcon(string typ) => typ?.ToLower() switch
        {
            "auftrag" => "A",
            "rechnung" => "R",
            "lieferschein" => "L",
            "zahlung" => "Z",
            "email" => "E",
            "retoure" => "X",
            _ => "?"
        };

        private Brush GetHistorieIconBg(string typ) => typ?.ToLower() switch
        {
            "auftrag" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),      // Gruen
            "rechnung" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),    // Blau
            "lieferschein" => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Lila
            "zahlung" => new SolidColorBrush(Color.FromRgb(0, 150, 136)),       // Tuerkis
            "email" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),         // Orange
            "retoure" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),       // Rot
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))              // Grau
        };

        private void AuftragNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is CoreService.KundeAuftragKurz auftrag)
            {
                var view = new BestellungDetailView();
                view.LadeBestellung(auftrag.KBestellung);
                NavigateTo(view);
            }
        }

        private void RechnungNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is CoreService.KundeRechnungKurz rechnung)
            {
                var view = new RechnungDetailView(rechnung.KRechnung);
                NavigateTo(view);
            }
        }
    }

    public class HistorieEintrag
    {
        public string Beschreibung { get; set; } = "";
        public string DatumFormatiert { get; set; } = "";
        public string Icon { get; set; } = "?";
        public Brush IconBg { get; set; } = Brushes.Gray;
    }
}
