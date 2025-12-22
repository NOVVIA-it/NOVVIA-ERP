using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungenView : UserControl
    {
        private readonly CoreService _core;

        public BestellungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeBestellungenAsync();
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
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeBestellungenAsync();
        private async void Aktualisieren_Click(object sender, RoutedEventArgs e) => await LadeBestellungenAsync();
        private async void Status_Changed(object sender, SelectionChangedEventArgs e) => await LadeBestellungenAsync();

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
            MessageBox.Show("Neue Bestellung wird in JTL-Wawi erstellt.\n\nIn NovviaERP werden Bestellungen nur angezeigt und bearbeitet.",
                "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
                MessageBox.Show($"Rechnung fuer Bestellung {best.CBestellNr} erstellen...\n\n(Funktion folgt)",
                    "Rechnung", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.BestellungUebersicht best)
                MessageBox.Show($"Lieferschein fuer Bestellung {best.CBestellNr} erstellen...\n\n(Funktion folgt)",
                    "Lieferschein", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
