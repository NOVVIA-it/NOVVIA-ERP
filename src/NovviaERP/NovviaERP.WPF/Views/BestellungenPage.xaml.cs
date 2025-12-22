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

        private void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
            {
                MessageBox.Show($"Rechnung fuer Auftrag {best.CBestellNr} erstellen\n(wird implementiert)",
                    "Rechnung", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
            {
                MessageBox.Show($"Lieferschein fuer Auftrag {best.CBestellNr} erstellen\n(wird implementiert)",
                    "Lieferschein", MessageBoxButton.OK, MessageBoxImage.Information);
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
