using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class KundenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.KundeUebersicht> _kunden = new();
        private CoreService.KundeUebersicht? _selectedKunde;

        public KundenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await InitAsync();
        }

        private async Task InitAsync()
        {
            try
            {
                // Spalten-Konfiguration aktivieren (Rechtsklick auf Header)
                DataGridColumnConfig.EnableColumnChooser(dgKunden, "KundenView");

                // Kundengruppen laden
                var gruppen = (await _core.GetKundengruppenAsync()).ToList();
                cboKundengruppe.Items.Clear();
                cboKundengruppe.Items.Add(new ComboBoxItem { Content = "Alle Gruppen", IsSelected = true });
                foreach (var g in gruppen)
                    cboKundengruppe.Items.Add(new ComboBoxItem { Content = g.CName, Tag = g.KKundenGruppe });

                // Kunden laden
                await LadeKundenAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
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
                txtStatus.Text = "Lade Kunden...";

                var suche = string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text.Trim();
                int? gruppeId = null;
                if (cboKundengruppe.SelectedItem is ComboBoxItem item && item.Tag is int id)
                    gruppeId = id;

                _kunden = (await _core.GetKundenAsync(suche: suche, kundengruppeId: gruppeId)).ToList();
                dgKunden.ItemsSource = _kunden;
                txtAnzahl.Text = $"({_kunden.Count} Kunden)";
                txtStatus.Text = $"{_kunden.Count} Kunden geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
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
            cboKundengruppe.SelectedIndex = 0;
            await LadeKundenAsync();
        }

        private void Neu_Click(object sender, RoutedEventArgs e) => NavigateTo(new KundeDetailView(null));

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKunde != null)
                NavigateTo(new KundeDetailView(_selectedKunde.KKunde));
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedKunde != null)
                NavigateTo(new KundeDetailView(_selectedKunde.KKunde));
        }

        private async void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedKunde = dgKunden.SelectedItem as CoreService.KundeUebersicht;
            btnBearbeiten.IsEnabled = _selectedKunde != null;
            btnNachricht.IsEnabled = _selectedKunde != null;

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

                // Header
                txtKundeName.Text = kunde.Anzeigename;
                txtKundeNr.Text = $"Kd-Nr: {kunde.CKundenNr}";

                // Kontakt
                txtKundeMail.Text = kunde.CMail ?? "-";
                txtKundeTel.Text = kunde.CTel ?? "-";
                txtKundeAdresse.Text = kunde.COrt ?? "-";

                // Statistiken laden
                var stats = await _core.GetKundeStatistikAsync(kunde.KKunde);
                txtStatUmsatz.Text = $"{stats.UmsatzGesamt:N2} EUR";
                txtStatAuftraege.Text = stats.AnzahlAuftraege.ToString();
                txtStatDurchschnitt.Text = $"{stats.DurchschnittWarenkorb:N2} EUR";
                txtStatOffen.Text = $"{stats.OffenePosten:N2} EUR";
                txtStatRetouren.Text = stats.AnzahlRetouren.ToString();
                txtStatSeit.Text = stats.ErstBestellung?.ToString("dd.MM.yyyy") ?? "-";

                // Tabs laden (parallel)
                var auftraegeTask = _core.GetKundeAuftraegeAsync(kunde.KKunde);
                var rechnungenTask = _core.GetKundeRechnungenAsync(kunde.KKunde);
                var adressenTask = _core.GetKundeAdressenKurzAsync(kunde.KKunde);
                var historieTask = _core.GetKundeHistorieAsync(kunde.KKunde);

                await Task.WhenAll(auftraegeTask, rechnungenTask, adressenTask, historieTask);

                dgKundeAuftraege.ItemsSource = auftraegeTask.Result.ToList();
                dgKundeRechnungen.ItemsSource = rechnungenTask.Result.ToList();
                dgKundeAdressen.ItemsSource = adressenTask.Result.ToList();
                lstHistorie.ItemsSource = historieTask.Result.ToList();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler 360°: {ex.Message}";
            }
        }

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
}
