using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.Core.Services.Base;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class RechnungenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.RechnungUebersicht> _rechnungen = new();
        private ComboBox? _cmbStatus;

        public RechnungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();

            Loaded += async (s, e) =>
            {
                // Status-Filter hinzufuegen
                _cmbStatus = filterBar.AddFilter("Status:", new[]
                {
                    new { Text = "Alle", Value = (int?)null },
                    new { Text = "Offen", Value = (int?)0 },
                    new { Text = "Bezahlt", Value = (int?)1 },
                    new { Text = "Ueberfaellig", Value = (int?)2 },
                    new { Text = "Storniert", Value = (int?)3 }
                }, "Text");

                // Spalten-Konfiguration aktivieren (Rechtsklick auf Header)
                DataGridColumnConfig.EnableColumnChooser(dgRechnungen, "RechnungenView");

                // Daten sofort laden (kein Aktualisieren-Button)
                await LadeRechnungenAsync();
            };
        }

        private async Task LadeRechnungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Rechnungen...";
                filterBar.SetLoading(true);

                // Status aus Filter lesen
                int? status = null;
                if (_cmbStatus?.SelectedItem != null)
                {
                    var selected = _cmbStatus.SelectedItem;
                    var valueProp = selected.GetType().GetProperty("Value");
                    if (valueProp != null)
                        status = valueProp.GetValue(selected) as int?;
                }

                // JTL-Zeitraum in Datum umwandeln
                var zeitraum = filterBar.Zeitraum;
                DateTime? vonDatum = null;
                DateTime? bisDatum = null;

                if (!string.IsNullOrEmpty(zeitraum) && zeitraum != "Alle")
                {
                    var (von, bis) = BaseDatabaseService.JtlZeitraumZuDatum(zeitraum);
                    vonDatum = von;
                    bisDatum = bis;
                }

                var suche = filterBar.Suchbegriff;

                _rechnungen = (await _core.GetAllRechnungenAsync(
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    status: status,
                    vonDatum: vonDatum,
                    bisDatum: bisDatum
                )).ToList();

                dgRechnungen.ItemsSource = _rechnungen;

                // Summen berechnen
                var anzahl = _rechnungen.Count;
                var summeNetto = _rechnungen.Where(r => !r.NStorno).Sum(r => r.FNetto);
                var summeBrutto = _rechnungen.Where(r => !r.NStorno).Sum(r => r.FBrutto);
                var summeOffen = _rechnungen.Where(r => !r.NStorno).Sum(r => r.Offen);

                txtSummeAnzahl.Text = $"{anzahl} Rechnungen";
                txtSummeNetto.Text = $"{summeNetto:N2}";
                txtSummeBrutto.Text = $"{summeBrutto:N2}";
                txtSummeOffen.Text = $"{summeOffen:N2}";

                filterBar.Anzahl = anzahl;
                txtAnzahl.Text = $"({anzahl} Rechnungen, {summeOffen:N2} EUR offen)";
                txtStatus.Text = $"{anzahl} Rechnungen geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                filterBar.SetLoading(false);
            }
        }

        #region FilterBar Events

        private async void FilterBar_SucheGestartet(object? sender, EventArgs e)
        {
            await LadeRechnungenAsync();
        }

        #endregion

        #region DataGrid Events

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

        #endregion

        #region Button Click Events

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
            if (dgRechnungen.SelectedItem is not CoreService.RechnungUebersicht rechnung) return;
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
