using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class RechnungenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.RechnungUebersicht> _alleRechnungen = new();
        private string _selectedStatus = "";
        private bool _statusFromMainWindow = false;

        public RechnungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) =>
            {
                await LadeEinstellungenAsync();
                await LadeRechnungenAsync();
                dgRechnungen.DateFilterChanged += DgRechnungen_DateFilterChanged;
            };

            Unloaded += async (s, e) =>
            {
                await SpeichereSplitterEinstellungenAsync();
            };
        }

        /// <summary>
        /// Setzt den Status-Filter von außen (z.B. vom MainWindow-Menü)
        /// </summary>
        public void SetStatusFilter(string status)
        {
            _selectedStatus = status;
            _statusFromMainWindow = true;
        }

        private async Task LadeEinstellungenAsync()
        {
            try
            {
                // Splitter-Positionen laden
                var rechnungenHoehe = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "RechnungenView.RechnungenHoehe");
                if (!string.IsNullOrEmpty(rechnungenHoehe) && double.TryParse(rechnungenHoehe, out double rhHeight))
                {
                    rechnungenRow.Height = new GridLength(rhHeight, GridUnitType.Star);
                }

                var positionenHoehe = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "RechnungenView.PositionenHoehe");
                if (!string.IsNullOrEmpty(positionenHoehe) && double.TryParse(positionenHoehe, out double phHeight))
                {
                    positionenRow.Height = new GridLength(phHeight, GridUnitType.Star);
                }
            }
            catch { /* Ignorieren - Standardwerte verwenden */ }
        }

        private async Task SpeichereSplitterEinstellungenAsync()
        {
            try
            {
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "RechnungenView.RechnungenHoehe", rechnungenRow.Height.Value.ToString());
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "RechnungenView.PositionenHoehe", positionenRow.Height.Value.ToString());
            }
            catch { /* Ignorieren */ }
        }

        private async Task LadeRechnungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Rechnungen...";

                var suche = txtSuche.Text.Trim();

                // Status in int? umwandeln für die API (laut SP-Dokumentation)
                // 0=Offen, 1=Bezahlt, 2=Storniert, 3=Teilbezahlt, 4=Angemahnt
                int? statusInt = null;
                if (!string.IsNullOrEmpty(_selectedStatus))
                {
                    statusInt = _selectedStatus switch
                    {
                        "Offen" => 0,
                        "Bezahlt" => 1,
                        "Storniert" => 2,
                        "Teilbezahlt" => 3,
                        "Ueberfaellig" => 0,  // Offen + wird client-seitig gefiltert
                        _ => null
                    };
                }

                var rechnungen = await _core.GetAllRechnungenAsync(
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    status: statusInt,
                    limit: 100000);

                _alleRechnungen = rechnungen.ToList();
                dgRechnungen.ItemsSource = _alleRechnungen;
                txtAnzahl.Text = $"({_alleRechnungen.Count} Rechnungen)";
                txtStatus.Text = $"{_alleRechnungen.Count} Rechnungen geladen";

                UpdateSummen();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgRechnungen_DateFilterChanged(object? sender, EventArgs e)
        {
            UpdateSummen();
        }

        private void UpdateSummen()
        {
            var gefiltert = dgRechnungen.FilteredItemsSource?.Cast<CoreService.RechnungUebersicht>().ToList()
                ?? new List<CoreService.RechnungUebersicht>();

            var summeNetto = gefiltert.Where(r => !r.NStorno).Sum(r => r.FNetto);
            var summeBrutto = gefiltert.Where(r => !r.NStorno).Sum(r => r.FBrutto);
            var summeOffen = gefiltert.Where(r => !r.NStorno).Sum(r => r.Offen);

            txtSummeAnzahl.Text = $"{gefiltert.Count} Rechnungen";
            txtSummeNetto.Text = $"{summeNetto:N2}";
            txtSummeBrutto.Text = $"{summeBrutto:N2} EUR";
            txtSummeOffen.Text = $"{summeOffen:N2} EUR";
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeRechnungenAsync();

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeRechnungenAsync();
        }

        private async void DG_SelectionChanged(object? sender, object? e)
        {
            var selected = dgRechnungen.Grid.SelectedItem as CoreService.RechnungUebersicht;
            var hasSelection = selected != null;

            btnOeffnen.IsEnabled = hasSelection;
            btnAuftrag.IsEnabled = hasSelection && selected?.KAuftrag != null;
            btnKunde.IsEnabled = hasSelection;
            btnZahlung.IsEnabled = hasSelection && !selected!.NStorno && selected.Offen > 0;
            btnMahnung.IsEnabled = hasSelection && !selected!.NStorno && selected.Offen > 0;
            btnDrucken.IsEnabled = hasSelection;
            btnStorno.IsEnabled = hasSelection && !selected!.NStorno;

            // Positionen laden
            if (selected != null)
            {
                await LadePositionenAsync(selected.KRechnung);
            }
            else
            {
                dgPositionen.ItemsSource = null;
            }
        }

        private async Task LadePositionenAsync(int kRechnung)
        {
            try
            {
                var rechnung = await _core.GetRechnungMitPositionenAsync(kRechnung);
                dgPositionen.ItemsSource = rechnung?.Positionen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Positionen: {ex.Message}");
                dgPositionen.ItemsSource = null;
            }
        }

        private void DG_DoubleClick(object? sender, object? e)
        {
            if (e is CoreService.RechnungUebersicht rechnung)
                NavigateToRechnung(rechnung.KRechnung);
        }

        private void Oeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.Grid.SelectedItem is CoreService.RechnungUebersicht rechnung)
                NavigateToRechnung(rechnung.KRechnung);
        }

        private void Auftrag_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.Grid.SelectedItem is CoreService.RechnungUebersicht rechnung && rechnung.KAuftrag.HasValue)
                NavigateToAuftrag(rechnung.KAuftrag.Value);
        }

        private async void Zahlung_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.Grid.SelectedItem is not CoreService.RechnungUebersicht rechnung) return;
            if (rechnung.NStorno || rechnung.Offen <= 0) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Zahlungsbetrag fuer Rechnung {rechnung.CRechnungsNr}:\n\nOffener Betrag: {rechnung.Offen:N2} EUR",
                "Zahlung erfassen",
                rechnung.Offen.ToString("N2"));

            if (string.IsNullOrEmpty(input)) return;

            if (!decimal.TryParse(input.Replace(".", ","), out var betrag) || betrag <= 0)
            {
                MessageBox.Show("Ungueltiger Betrag!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _core.ErfasseZahlungAsync(rechnung.KRechnung, betrag, App.BenutzerId);
                MessageBox.Show($"Zahlung von {betrag:N2} EUR erfasst!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeRechnungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Storno_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.Grid.SelectedItem is not CoreService.RechnungUebersicht rechnung) return;
            if (rechnung.NStorno) return;

            var result = MessageBox.Show(
                $"Rechnung {rechnung.CRechnungsNr} wirklich stornieren?\n\nDer zugehoerige Auftrag wird wieder bearbeitbar.",
                "Stornieren",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.StornoRechnungAsync(rechnung.KRechnung, App.BenutzerId);
                MessageBox.Show("Rechnung wurde storniert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeRechnungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToRechnung(int rechnungId)
        {
            try
            {
                var detailView = new RechnungDetailView(rechnungId);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(detailView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen der Rechnung:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToAuftrag(int auftragId)
        {
            try
            {
                var detailView = App.Services.GetRequiredService<BestellungDetailView>();
                detailView.LadeBestellung(auftragId);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(detailView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen des Auftrags:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
