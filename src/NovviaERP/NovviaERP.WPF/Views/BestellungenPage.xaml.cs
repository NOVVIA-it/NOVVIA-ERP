using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungenPage : Page
    {
        private readonly CoreService _coreService;
        private List<CoreService.BestellungUebersicht> _bestellungen = new();

        public BestellungenPage()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeBestellungenAsync();
        }

        private async System.Threading.Tasks.Task LadeBestellungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Auftraege...";

                string? status = null;
                if (cmbStatus.SelectedItem is ComboBoxItem item && item.Tag != null)
                    status = item.Tag.ToString();

                DateTime? von = dpVon.SelectedDate;
                DateTime? bis = dpBis.SelectedDate?.AddDays(1); // End of day

                bool nurOffene = chkNurOffene.IsChecked == true;

                _bestellungen = (await _coreService.GetBestellungenAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text,
                    status: status,
                    von: von,
                    bis: bis,
                    nurOffene: nurOffene
                )).ToList();

                dgBestellungen.ItemsSource = _bestellungen;
                txtAnzahl.Text = $"({_bestellungen.Count} Auftraege)";

                // Summen berechnen
                var summe = _bestellungen.Sum(b => b.GesamtBrutto);
                txtStatus.Text = $"{_bestellungen.Count} Auftraege geladen - Summe: {summe:N2} EUR";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden der Auftraege:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            await LadeBestellungenAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LadeBestellungenAsync();
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                await LadeBestellungenAsync();
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeBestellungenAsync();
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new BestellungDetailPage(null));
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
                NavigationService?.Navigate(new BestellungDetailPage(best.KBestellung));
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
                NavigationService?.Navigate(new BestellungDetailPage(best.KBestellung));
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = dgBestellungen.SelectedItem != null;
            btnDetails.IsEnabled = hasSelection;
            btnRechnung.IsEnabled = hasSelection;
            btnLieferschein.IsEnabled = hasSelection;
            btnVersenden.IsEnabled = hasSelection;
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
                var kRechnung = await _coreService.CreateRechnungAsync(best.KBestellung);
                var rechnungen = await _coreService.GetRechnungenAsync(best.KBestellung);
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
                var kLieferschein = await _coreService.CreateLieferscheinAsync(best.KBestellung);
                var lieferscheine = await _coreService.GetLieferscheineAsync(best.KBestellung);
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

        private void Versenden_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
            {
                MessageBox.Show($"Auftrag {best.CBestellNr} versenden\n(wird implementiert)",
                    "Versand", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
