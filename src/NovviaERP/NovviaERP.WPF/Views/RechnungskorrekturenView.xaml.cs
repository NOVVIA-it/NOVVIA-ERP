using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class RechnungskorrekturenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.RechnungskorrekturUebersicht> _alleKorrekturen = new();
        private string _selectedStatus = "";

        public RechnungskorrekturenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) =>
            {
                await LadeEinstellungenAsync();
                await LadeKorrekturenAsync();
                dgKorrekturen.DateFilterChanged += DgKorrekturen_DateFilterChanged;
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
        }

        private async Task LadeEinstellungenAsync()
        {
            try
            {
                var korrekturenHoehe = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "RechnungskorrekturenView.KorrekturenHoehe");
                if (!string.IsNullOrEmpty(korrekturenHoehe) && double.TryParse(korrekturenHoehe, out double khHeight))
                {
                    korrekturenRow.Height = new GridLength(khHeight, GridUnitType.Star);
                }

                var positionenHoehe = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "RechnungskorrekturenView.PositionenHoehe");
                if (!string.IsNullOrEmpty(positionenHoehe) && double.TryParse(positionenHoehe, out double phHeight))
                {
                    positionenRow.Height = new GridLength(phHeight, GridUnitType.Star);
                }
            }
            catch { /* Ignorieren */ }
        }

        private async Task SpeichereSplitterEinstellungenAsync()
        {
            try
            {
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "RechnungskorrekturenView.KorrekturenHoehe", korrekturenRow.Height.Value.ToString());
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "RechnungskorrekturenView.PositionenHoehe", positionenRow.Height.Value.ToString());
            }
            catch { /* Ignorieren */ }
        }

        private async Task LadeKorrekturenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Rechnungskorrekturen...";

                var suche = txtSuche.Text.Trim();

                // Status-Filter anwenden
                bool? storno = null;
                if (_selectedStatus == "Storniert")
                    storno = true;
                else if (_selectedStatus == "Offen" || _selectedStatus == "Ausgeglichen")
                    storno = false;

                var korrekturen = await _core.GetRechnungskorrekturenAsync(
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    storno: storno,
                    limit: 100000);

                _alleKorrekturen = korrekturen.ToList();
                dgKorrekturen.ItemsSource = _alleKorrekturen;
                txtAnzahl.Text = $"({_alleKorrekturen.Count} Korrekturen)";
                txtStatus.Text = $"{_alleKorrekturen.Count} Rechnungskorrekturen geladen";

                UpdateSummen();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgKorrekturen_DateFilterChanged(object? sender, EventArgs e)
        {
            UpdateSummen();
        }

        private void UpdateSummen()
        {
            var gefiltert = dgKorrekturen.FilteredItemsSource?.Cast<CoreService.RechnungskorrekturUebersicht>().ToList()
                ?? new List<CoreService.RechnungskorrekturUebersicht>();

            var summeNetto = gefiltert.Where(k => !k.NStorno).Sum(k => k.FPreisNetto);
            var summeBrutto = gefiltert.Where(k => !k.NStorno).Sum(k => k.FPreisBrutto);

            txtSummeAnzahl.Text = $"{gefiltert.Count} Korrekturen";
            txtSummeNetto.Text = $"{summeNetto:N2}";
            txtSummeBrutto.Text = $"{summeBrutto:N2} EUR";
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeKorrekturenAsync();

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeKorrekturenAsync();
        }

        private async void DG_SelectionChanged(object? sender, object? e)
        {
            var selected = dgKorrekturen.Grid.SelectedItem as CoreService.RechnungskorrekturUebersicht;
            var hasSelection = selected != null;

            btnOeffnen.IsEnabled = hasSelection;
            btnRechnung.IsEnabled = hasSelection && selected?.KRechnung > 0;
            btnKunde.IsEnabled = hasSelection;
            btnDrucken.IsEnabled = hasSelection;
            btnStorno.IsEnabled = hasSelection && !selected!.NStorno;

            // Positionen laden
            if (selected != null)
            {
                await LadePositionenAsync(selected.KGutschrift);
            }
            else
            {
                dgPositionen.ItemsSource = null;
            }
        }

        private async Task LadePositionenAsync(int kGutschrift)
        {
            try
            {
                var korrektur = await _core.GetRechnungskorrekturMitPositionenAsync(kGutschrift);
                dgPositionen.ItemsSource = korrektur?.Positionen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Positionen: {ex.Message}");
                dgPositionen.ItemsSource = null;
            }
        }

        private void DG_DoubleClick(object? sender, object? e)
        {
            if (e is CoreService.RechnungskorrekturUebersicht korrektur)
                NavigateToKorrektur(korrektur.KGutschrift);
        }

        private void Oeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKorrekturen.Grid.SelectedItem is CoreService.RechnungskorrekturUebersicht korrektur)
                NavigateToKorrektur(korrektur.KGutschrift);
        }

        private void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            if (dgKorrekturen.Grid.SelectedItem is CoreService.RechnungskorrekturUebersicht korrektur && korrektur.KRechnung > 0)
            {
                try
                {
                    var detailView = new RechnungDetailView(korrektur.KRechnung);
                    if (Window.GetWindow(this) is MainWindow main)
                        main.ShowContent(detailView);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Oeffnen der Rechnung:\n\n{ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void NavigateToKorrektur(int kGutschrift)
        {
            try
            {
                var detailView = new RechnungskorrekturDetailView(kGutschrift);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(detailView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen der Rechnungskorrektur:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Kunde_Click(object sender, RoutedEventArgs e)
        {
            if (dgKorrekturen.Grid.SelectedItem is CoreService.RechnungskorrekturUebersicht korrektur)
            {
                // Kunde-ID aus Korrektur holen - wir brauchen eine Abfrage
                MessageBox.Show("Zur Kunden-Ansicht wechseln wird noch implementiert.", "Info");
            }
        }

        private void Drucken_Click(object sender, RoutedEventArgs e)
        {
            if (dgKorrekturen.Grid.SelectedItem is CoreService.RechnungskorrekturUebersicht korrektur)
            {
                MessageBox.Show($"Drucken von {korrektur.CRechnungskorrekturnummer} wird noch implementiert.", "Info");
            }
        }

        private async void Storno_Click(object sender, RoutedEventArgs e)
        {
            if (dgKorrekturen.Grid.SelectedItem is not CoreService.RechnungskorrekturUebersicht korrektur) return;
            if (korrektur.NStorno) return;

            var result = MessageBox.Show(
                $"Soll die Rechnungskorrektur {korrektur.CRechnungskorrekturnummer} wirklich storniert werden?\n\n" +
                $"Betrag: {korrektur.FPreisBrutto:N2} EUR",
                "Stornieren bestaetigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.StorniereRechnungskorrekturAsync(korrektur.KGutschrift, App.BenutzerId);
                MessageBox.Show($"Rechnungskorrektur {korrektur.CRechnungskorrekturnummer} wurde storniert.",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeKorrekturenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Stornieren:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
