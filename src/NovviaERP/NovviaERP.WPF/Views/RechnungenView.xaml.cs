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
        private List<CoreService.RechnungUebersicht> _rechnungen = new();

        public RechnungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeRechnungenAsync();
        }

        private async Task LadeRechnungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Rechnungen...";

                int? status = null;
                if (cmbStatus.SelectedItem is ComboBoxItem item && !string.IsNullOrEmpty(item.Tag?.ToString()))
                    status = int.Parse(item.Tag.ToString()!);

                var suche = txtSuche.Text.Trim();

                _rechnungen = (await _core.GetAllRechnungenAsync(
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    status: status,
                    vonDatum: dpVon.SelectedDate,
                    bisDatum: dpBis.SelectedDate
                )).ToList();

                dgRechnungen.ItemsSource = _rechnungen;

                // Summen berechnen
                var summeOffen = _rechnungen.Where(r => !r.NStorno && !r.DBezahlt.HasValue).Sum(r => r.Offen);
                txtAnzahl.Text = $"({_rechnungen.Count} Rechnungen, {summeOffen:N2} EUR offen)";
                txtStatus.Text = $"{_rechnungen.Count} Rechnungen geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Event Handlers

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeRechnungenAsync();
        private async void Aktualisieren_Click(object sender, RoutedEventArgs e) => await LadeRechnungenAsync();

        private async void Status_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeRechnungenAsync();
        }

        private async void Datum_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeRechnungenAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeRechnungenAsync();
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgRechnungen.SelectedItem is CoreService.RechnungUebersicht r;
            btnOeffnen.IsEnabled = selected;
            btnAuftrag.IsEnabled = selected && (dgRechnungen.SelectedItem as CoreService.RechnungUebersicht)?.KAuftrag != null;
            btnZahlung.IsEnabled = selected && !(dgRechnungen.SelectedItem as CoreService.RechnungUebersicht)?.NStorno == true
                                            && (dgRechnungen.SelectedItem as CoreService.RechnungUebersicht)?.Offen > 0;
            btnStorno.IsEnabled = selected && !(dgRechnungen.SelectedItem as CoreService.RechnungUebersicht)?.NStorno == true;
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgRechnungen.SelectedItem is CoreService.RechnungUebersicht rechnung)
                NavigateToRechnung(rechnung.KRechnung);
        }

        private void Oeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is CoreService.RechnungUebersicht rechnung)
                NavigateToRechnung(rechnung.KRechnung);
        }

        private void Auftrag_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is CoreService.RechnungUebersicht rechnung && rechnung.KAuftrag.HasValue)
                NavigateToAuftrag(rechnung.KAuftrag.Value);
        }

        private async void Zahlung_Click(object sender, RoutedEventArgs e)
        {
            if (dgRechnungen.SelectedItem is not CoreService.RechnungUebersicht rechnung) return;
            if (rechnung.NStorno || rechnung.Offen <= 0) return;

            // Einfacher Zahlungsdialog
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                $"Zahlungsbetrag fuer Rechnung {rechnung.CRechnungsNr}:\n\nOffener Betrag: {rechnung.Offen:N2} EUR",
                "Zahlung erfassen",
                rechnung.Offen.ToString("N2"));

            if (string.IsNullOrEmpty(input)) return;

            if (!decimal.TryParse(input.Replace(".", ","), out var betrag) || betrag <= 0)
            {
                MessageBox.Show("Ungültiger Betrag!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            if (dgRechnungen.SelectedItem is not CoreService.RechnungUebersicht rechnung) return;
            if (rechnung.NStorno) return;

            var result = MessageBox.Show(
                $"Rechnung {rechnung.CRechnungsNr} wirklich stornieren?\n\nDer zugehörige Auftrag wird wieder bearbeitbar.",
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

        #endregion

        #region Navigation

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

        #endregion
    }
}
