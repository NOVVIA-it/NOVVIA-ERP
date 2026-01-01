using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using static NovviaERP.Core.Services.ZahlungsabgleichService;

namespace NovviaERP.WPF.Views
{
    public partial class ZahlungZuordnenDialog : Window
    {
        private readonly ZahlungsabgleichService _service;
        private readonly ZahlungsabgleichEintrag _zahlung;
        private List<OffeneRechnung> _rechnungen = new();
        private OffeneRechnung? _selectedRechnung;

        public bool WurdeZugeordnet { get; private set; }

        public ZahlungZuordnenDialog(ZahlungsabgleichEintrag zahlung)
        {
            InitializeComponent();

            _zahlung = zahlung;
            _service = App.Services.GetRequiredService<ZahlungsabgleichService>();

            // Zahlungsdaten anzeigen
            txtDatum.Text = zahlung.Buchungsdatum.ToString("dd.MM.yyyy");
            txtName.Text = zahlung.Name ?? "-";
            txtIBAN.Text = zahlung.Konto ?? "-";
            txtVzweck.Text = zahlung.Verwendungszweck ?? "-";
            txtBetrag.Text = zahlung.Betrag.ToString("N2");
            txtZuordnungsbetrag.Text = zahlung.Betrag.ToString("N2");

            // Initial laden
            Loaded += async (s, e) =>
            {
                await LadeRechnungenAsync();
            };

            // Selection handler
            dgRechnungen.SelectionChanged += (s, e) =>
            {
                _selectedRechnung = dgRechnungen.SelectedItem as OffeneRechnung;
                btnZuordnen.IsEnabled = _selectedRechnung != null;

                if (_selectedRechnung != null)
                {
                    // Vorschlag: Minimum aus Zahlungsbetrag und offenem Betrag
                    var vorschlag = Math.Min(_zahlung.Betrag, _selectedRechnung.Offen);
                    txtZuordnungsbetrag.Text = vorschlag.ToString("N2");
                }
            };
        }

        private async System.Threading.Tasks.Task LadeRechnungenAsync()
        {
            try
            {
                var suchbegriff = txtSuche.Text.Trim();
                _rechnungen = (await _service.SucheOffeneRechnungenAsync(
                    string.IsNullOrEmpty(suchbegriff) ? null : suchbegriff
                )).ToList();

                dgRechnungen.ItemsSource = _rechnungen;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            await LadeRechnungenAsync();
        }

        private async void Suche_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await LadeRechnungenAsync();
            }
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedRechnung != null)
            {
                Zuordnen_Click(sender, e);
            }
        }

        private async void Zuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRechnung == null) return;

            // Betrag parsen
            if (!decimal.TryParse(txtZuordnungsbetrag.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var betrag))
            {
                MessageBox.Show("Bitte geben Sie einen gueltigen Betrag ein.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (betrag <= 0)
            {
                MessageBox.Show("Der Betrag muss groesser als 0 sein.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (betrag > _zahlung.Betrag)
            {
                MessageBox.Show($"Der Betrag kann nicht groesser als der Zahlungsbetrag ({_zahlung.Betrag:N2} EUR) sein.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Bestaetigung
            var result = MessageBox.Show(
                $"Zahlung zuordnen?\n\n" +
                $"Rechnung: {_selectedRechnung.CRechnungsnummer}\n" +
                $"Kunde: {_selectedRechnung.KundeDisplay}\n" +
                $"Betrag: {betrag:N2} EUR",
                "Zuordnung bestaetigen",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _service.ZuordnenAsync(_zahlung.Id, _selectedRechnung.KRechnung, betrag);

                WurdeZugeordnet = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei der Zuordnung:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
