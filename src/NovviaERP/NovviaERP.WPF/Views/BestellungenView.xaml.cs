using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.BestellungUebersicht> _alleBestellungen = new();
        private string _selectedStatus = "";
        private bool _isInitializing = true;
        private bool _statusFromMainWindow = false; // Status wurde vom MainWindow gesetzt

        public BestellungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) =>
            {
                await LadeEinstellungenAsync();
                await LadeBestellungenAsync();
                dgBestellungen.DateFilterChanged += DgBestellungen_DateFilterChanged;
                _isInitializing = false;
            };

            // Einstellungen speichern wenn View entladen wird
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
                // Gespeicherten Status laden (nur wenn nicht vom MainWindow vorgegeben)
                if (!_statusFromMainWindow)
                {
                    var savedStatus = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "BestellungenView.Status");
                    if (!string.IsNullOrEmpty(savedStatus))
                    {
                        _selectedStatus = savedStatus;
                    }
                }

                // Splitter-Positionen laden
                var auftraegeHoehe = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "BestellungenView.AuftraegeHoehe");
                if (!string.IsNullOrEmpty(auftraegeHoehe) && double.TryParse(auftraegeHoehe, out double ahHeight))
                {
                    auftraegeRow.Height = new GridLength(ahHeight, GridUnitType.Star);
                }

                var positionenHoehe = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "BestellungenView.PositionenHoehe");
                if (!string.IsNullOrEmpty(positionenHoehe) && double.TryParse(positionenHoehe, out double phHeight))
                {
                    positionenRow.Height = new GridLength(phHeight, GridUnitType.Star);
                }
            }
            catch { /* Ignorieren - Standardwerte verwenden */ }
        }

        private async Task SpeichereEinstellungenAsync()
        {
            try
            {
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "BestellungenView.Status", _selectedStatus);
            }
            catch { /* Ignorieren */ }
        }

        private async Task SpeichereSplitterEinstellungenAsync()
        {
            try
            {
                // Aufträge/Positionen Höhen speichern (als Star-Werte)
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "BestellungenView.AuftraegeHoehe", auftraegeRow.Height.Value.ToString());
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "BestellungenView.PositionenHoehe", positionenRow.Height.Value.ToString());
            }
            catch { /* Ignorieren */ }
        }

        private async Task LadeBestellungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Bestellungen...";

                var suche = txtSuche.Text.Trim();
                var status = string.IsNullOrEmpty(_selectedStatus) ? null : _selectedStatus;

                // WICHTIG: Bei "Alle anzeigen" kein Limit verwenden!
                var limit = 100000; // Praktisch unbegrenzt

                var bestellungen = await _core.GetBestellungenAsync(
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    status: status,
                    limit: limit);

                _alleBestellungen = bestellungen.ToList();
                dgBestellungen.ItemsSource = _alleBestellungen;
                txtAnzahl.Text = $"({_alleBestellungen.Count} Auftraege)";
                txtStatus.Text = $"{_alleBestellungen.Count} Bestellungen geladen";

                // Summen basierend auf gefilterten Daten aktualisieren
                UpdateSummen();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgBestellungen_DateFilterChanged(object? sender, EventArgs e)
        {
            // Summen neu berechnen basierend auf den gefilterten Daten
            UpdateSummen();
        }

        private void UpdateSummen()
        {
            // Gefilterte Daten aus dem Grid holen
            var gefiltert = dgBestellungen.FilteredItemsSource?.Cast<CoreService.BestellungUebersicht>().ToList()
                ?? new List<CoreService.BestellungUebersicht>();

            var summeNetto = gefiltert.Sum(b => b.GesamtNetto);
            var summeBrutto = gefiltert.Sum(b => b.GesamtBrutto);

            txtSummeAnzahl.Text = $"{gefiltert.Count} Auftraege";
            txtSummeBrutto.Text = $"{summeBrutto:N2} EUR";
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeBestellungenAsync();

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeBestellungenAsync();
        }

        private async void DG_SelectionChanged(object? sender, object? e)
        {
            var selected = dgBestellungen.Grid.SelectedItem as CoreService.BestellungUebersicht;
            var hasSelection = selected != null;

            // Buttons aktivieren/deaktivieren
            btnAuftrag.IsEnabled = hasSelection;
            btnAusgabe.IsEnabled = hasSelection;
            btnKunde.IsEnabled = hasSelection;
            btnRechnungErstellen.IsEnabled = hasSelection;
            btnZusammenfassen.IsEnabled = hasSelection;
            btnZahlung.IsEnabled = hasSelection;
            btnAusliefern.IsEnabled = hasSelection;
            btnVersandinfo.IsEnabled = hasSelection;

            // Positionen laden
            if (selected != null)
            {
                await LadePositionenAsync(selected.KBestellung);
            }
            else
            {
                dgPositionen.ItemsSource = null;
            }
        }

        private async Task LadePositionenAsync(int kBestellung)
        {
            try
            {
                var bestellung = await _core.GetBestellungByIdAsync(kBestellung);
                dgPositionen.ItemsSource = bestellung?.Positionen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Positionen: {ex.Message}");
                dgPositionen.ItemsSource = null;
            }
        }

        private void DG_DoubleClick(object? sender, object? e)
        {
            if (e is CoreService.BestellungUebersicht best)
                NavigateToDetail(best.KBestellung);
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.Grid.SelectedItem is CoreService.BestellungUebersicht best)
                NavigateToDetail(best.KBestellung);
        }

        private void NavigateToDetail(int bestellungId)
        {
            try
            {
                var detailView = App.Services.GetRequiredService<BestellungDetailView>();
                detailView.LadeBestellung(bestellungId);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(detailView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen der Bestellung:\n\n{ex.Message}\n\nDetails:\n{ex.StackTrace}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                var detailView = new BestellungDetailView();
                detailView.LadeNeuerAuftrag();
                main.ShowContent(detailView);
            }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AuftragsstapelimportView();
            if (dialog.ShowDialog() == true)
            {
                await LadeBestellungenAsync();
            }
        }

        private async void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.Grid.SelectedItem is not CoreService.BestellungUebersicht best) return;

            var result = MessageBox.Show(
                $"Rechnung für Auftrag {best.CBestellNr} erstellen?\n\nHinweis: Ein Lieferschein muss bereits existieren.",
                "Rechnung erstellen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var kRechnung = await _core.CreateRechnungAsync(best.KBestellung);
                var rechnungen = await _core.GetRechnungenAsync(best.KBestellung);
                var neueRechnung = rechnungen.FirstOrDefault(r => r.KRechnung == kRechnung);

                MessageBox.Show(
                    $"Rechnung {neueRechnung?.CRechnungsnr ?? kRechnung.ToString()} wurde erstellt!",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fehler beim Erstellen der Rechnung:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.Grid.SelectedItem is not CoreService.BestellungUebersicht best) return;

            var result = MessageBox.Show(
                $"Lieferschein für Auftrag {best.CBestellNr} erstellen?\n\nAlle offenen Positionen werden übernommen.",
                "Lieferschein erstellen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var kLieferschein = await _core.CreateLieferscheinAsync(best.KBestellung);
                var lieferscheine = await _core.GetLieferscheineAsync(best.KBestellung);
                var neuerLieferschein = lieferscheine.FirstOrDefault(l => l.KLieferschein == kLieferschein);

                MessageBox.Show(
                    $"Lieferschein {neuerLieferschein?.CLieferscheinNr ?? kLieferschein.ToString()} wurde erstellt!",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fehler beim Erstellen des Lieferscheins:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
