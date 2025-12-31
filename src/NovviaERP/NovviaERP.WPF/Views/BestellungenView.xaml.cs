using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungenView : UserControl
    {
        private readonly CoreService _core;

        public BestellungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) =>
            {
                // Spalten-Konfiguration aktivieren (Rechtsklick auf Header)
                DataGridColumnConfig.EnableColumnChooser(dgBestellungen, "BestellungenView");
                await LadeBestellungenAsync();
            };
        }

        private async Task LadeBestellungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Bestellungen...";

                var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var suche = txtSuche.Text.Trim();

                var bestellungen = await _core.GetBestellungenAsync(
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    status: string.IsNullOrEmpty(status) ? null : status);

                var liste = bestellungen.ToList();
                dgBestellungen.ItemsSource = liste;
                txtAnzahl.Text = $"({liste.Count} Bestellungen)";
                txtStatus.Text = $"{liste.Count} Bestellungen geladen";

                // Summen berechnen
                var summeNetto = liste.Sum(b => b.GesamtNetto);
                var summeBrutto = liste.Sum(b => b.GesamtBrutto);
                txtSummeAnzahl.Text = $"{liste.Count} Bestellungen";
                txtSummeNetto.Text = summeNetto.ToString("N2");
                txtSummeBrutto.Text = summeBrutto.ToString("N2");
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeBestellungenAsync();
        private async void Aktualisieren_Click(object sender, RoutedEventArgs e) => await LadeBestellungenAsync();
        private async void Status_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Nicht laden während InitializeComponent
            if (!IsLoaded) return;
            await LadeBestellungenAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeBestellungenAsync();
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgBestellungen.SelectedItem != null;
            btnBearbeiten.IsEnabled = selected;
            btnRechnung.IsEnabled = selected;
            btnLieferschein.IsEnabled = selected;
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
                NavigateToDetail(best.KBestellung);
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
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
            // Neue Auftragsansicht öffnen (wie Auftragsdarstellung)
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
            if (dgBestellungen.SelectedItem is not CoreService.BestellungUebersicht best) return;

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
            if (dgBestellungen.SelectedItem is not CoreService.BestellungUebersicht best) return;

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
