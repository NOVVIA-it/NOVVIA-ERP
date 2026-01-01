using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ZahlungsabgleichView : UserControl
    {
        private readonly ZahlungsabgleichService _zahlungsabgleich;
        private readonly SepaService _sepa;
        private readonly PaymentService? _payment;
        private List<ZahlungsabgleichEintrag> _zahlungen = new();
        private List<SepaRechnungViewModel> _sepaRechnungen = new();

        public ZahlungsabgleichView()
        {
            InitializeComponent();

            _zahlungsabgleich = App.Services.GetRequiredService<ZahlungsabgleichService>();
            _sepa = App.Services.GetRequiredService<SepaService>();
            _payment = App.Services.GetService<PaymentService>();

            // Standard-Datumsbereich: Letzter Monat
            dpVon.SelectedDate = DateTime.Today.AddMonths(-1);
            dpBis.SelectedDate = DateTime.Today;

            Loaded += async (s, e) => await LadeZahlungenAsync();
        }

        #region Laden

        private async Task LadeZahlungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Zahlungen...";

                var nurUnmatched = chkNurUnmatched.IsChecked == true;

                _zahlungen = (await _zahlungsabgleich.GetAllTransaktionenAsync(
                    von: dpVon.SelectedDate,
                    bis: dpBis.SelectedDate,
                    nurUnmatched: nurUnmatched
                )).ToList();

                dgZahlungen.ItemsSource = _zahlungen;

                // Summen
                var anzahl = _zahlungen.Count;
                var summe = _zahlungen.Where(z => z.Betrag > 0).Sum(z => z.Betrag);
                var unmatched = _zahlungen.Count(z => z.MatchKonfidenz == 0);

                txtSummeAnzahl.Text = $"{anzahl} Zahlungen ({unmatched} nicht zugeordnet)";
                txtSummeBetrag.Text = summe.ToString("N2");
                txtAnzahl.Text = $"({anzahl} Eintraege)";

                txtStatus.Text = $"{anzahl} Zahlungen geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LadeSepaAsync()
        {
            try
            {
                txtStatus.Text = "Lade SEPA-Lastschriften...";

                var rechnungen = await _sepa.GetSepaFaelligAsync();
                _sepaRechnungen = rechnungen.Select(r => new SepaRechnungViewModel(r)).ToList();

                dgSepa.ItemsSource = _sepaRechnungen;

                var anzahl = _sepaRechnungen.Count;
                var summe = _sepaRechnungen.Sum(r => r.Offen);

                txtSepaAnzahl.Text = $"{anzahl} Lastschriften";
                txtSepaSumme.Text = summe.ToString("N2");

                txtStatus.Text = $"{anzahl} SEPA-Lastschriften geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion

        #region Import

        private async void ImportMT940_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "MT940-Datei importieren",
                Filter = "MT940 Dateien (*.sta;*.mt940)|*.sta;*.mt940|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                txtStatus.Text = "Importiere MT940...";

                var result = await _zahlungsabgleich.ImportMT940Async(dialog.FileName);

                if (result.Erfolg)
                {
                    MessageBox.Show($"Import erfolgreich!\n\n" +
                        $"Importiert: {result.ImportiertAnzahl}\n" +
                        $"Uebersprungen (bereits vorhanden): {result.UebersprungAnzahl}",
                        "MT940 Import", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LadeZahlungenAsync();
                }
                else
                {
                    MessageBox.Show($"Import fehlgeschlagen:\n{result.Fehler}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportCAMT_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "CAMT-Datei importieren",
                Filter = "CAMT Dateien (*.xml;*.camt)|*.xml;*.camt|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                txtStatus.Text = "Importiere CAMT...";

                var result = await _zahlungsabgleich.ImportCAMTAsync(dialog.FileName);

                if (result.Erfolg)
                {
                    MessageBox.Show($"Import erfolgreich!\n\n" +
                        $"Importiert: {result.ImportiertAnzahl}\n" +
                        $"Uebersprungen (bereits vorhanden): {result.UebersprungAnzahl}",
                        "CAMT Import", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LadeZahlungenAsync();
                }
                else
                {
                    MessageBox.Show($"Import fehlgeschlagen:\n{result.Fehler}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Auto-Matching

        private async void AutoMatch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Fuehre Auto-Matching durch...";

                var result = await _zahlungsabgleich.MatchZahlungenAsync();

                MessageBox.Show($"Auto-Matching abgeschlossen!\n\n" +
                    $"Automatisch zugeordnet: {result.AutoGematchedAnzahl}\n" +
                    $"Vorschlaege: {result.VorschlaegeAnzahl}\n" +
                    $"Gesamt geprueft: {result.GesamtAnzahl}",
                    "Auto-Matching", MessageBoxButton.OK, MessageBoxImage.Information);

                await LadeZahlungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region SEPA Export

        private void SepaAlleAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _sepaRechnungen)
                r.IsSelected = true;

            dgSepa.Items.Refresh();
            UpdateSepaSumme();
        }

        private async void SepaExport_Click(object sender, RoutedEventArgs e)
        {
            var selected = _sepaRechnungen.Where(r => r.IsSelected).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show("Bitte waehlen Sie mindestens eine Lastschrift aus.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // SEPA-Konfiguration (TODO: aus Einstellungen laden)
            var config = new SepaConfig
            {
                FirmaName = "NOVVIA GmbH",
                IBAN = "DE89370400440532013000", // Beispiel
                BIC = "COBADEFFXXX",
                GlaeubigerId = "DE98ZZZ09999999999"
            };

            // Ausfuehrungsdatum
            var ausfuehrung = DateTime.Today.AddDays(2);
            if (ausfuehrung.DayOfWeek == DayOfWeek.Saturday) ausfuehrung = ausfuehrung.AddDays(2);
            if (ausfuehrung.DayOfWeek == DayOfWeek.Sunday) ausfuehrung = ausfuehrung.AddDays(1);

            try
            {
                txtStatus.Text = "Generiere SEPA-XML...";

                var rechnungIds = selected.Select(r => r.KRechnung).ToList();
                var result = await _sepa.GenerateSepaDirectDebitXmlAsync(rechnungIds, config, ausfuehrung);

                if (!result.Erfolg)
                {
                    var fehler = string.Join("\n", result.Fehler);
                    MessageBox.Show($"Fehler bei der XML-Generierung:\n\n{fehler}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Speichern
                var dialog = new SaveFileDialog
                {
                    Title = "SEPA-XML speichern",
                    Filter = "XML Dateien (*.xml)|*.xml",
                    FileName = $"SEPA_Lastschrift_{DateTime.Now:yyyyMMdd_HHmmss}.xml"
                };

                if (dialog.ShowDialog() == true)
                {
                    await File.WriteAllTextAsync(dialog.FileName, result.XmlContent);

                    MessageBox.Show($"SEPA-XML erfolgreich erstellt!\n\n" +
                        $"Anzahl Lastschriften: {result.GueltigeRechnungen.Count}\n" +
                        $"Gesamtbetrag: {result.GesamtBetrag:N2} EUR\n\n" +
                        $"Datei: {dialog.FileName}",
                        "SEPA Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                txtStatus.Text = "SEPA-XML exportiert";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSepaSumme()
        {
            var selected = _sepaRechnungen.Where(r => r.IsSelected);
            txtSepaAnzahl.Text = $"{selected.Count()} ausgewaehlt";
            txtSepaSumme.Text = selected.Sum(r => r.Offen).ToString("N2");
        }

        #endregion

        #region PayPal / Mollie

        private async void PayPalSync_Click(object sender, RoutedEventArgs e)
        {
            if (_payment == null)
            {
                MessageBox.Show("PaymentService nicht konfiguriert.\n\nBitte konfigurieren Sie PayPal in den Einstellungen.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtProviderStatus.Text = "Synchronisiere PayPal...";
                btnPayPalSync.IsEnabled = false;

                var von = DateTime.Now.AddDays(-30);
                var bis = DateTime.Now;
                var transaktionen = await _payment.GetPayPalTransactionsAsync(von, bis);

                if (transaktionen.Count == 0)
                {
                    txtProviderStatus.Text = "Keine PayPal-Transaktionen gefunden";
                    MessageBox.Show("Keine PayPal-Transaktionen im Zeitraum gefunden.",
                        "PayPal", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Verarbeiten und matchen
                var gebucht = await _payment.ProcessPaymentsAsync();

                txtProviderStatus.Text = $"PayPal: {transaktionen.Count} Transaktionen, {gebucht} gematcht";
                MessageBox.Show($"PayPal-Synchronisation abgeschlossen!\n\n" +
                    $"Transaktionen abgerufen: {transaktionen.Count}\n" +
                    $"Automatisch zugeordnet: {gebucht}",
                    "PayPal", MessageBoxButton.OK, MessageBoxImage.Information);

                await LadeZahlungenAsync();
            }
            catch (Exception ex)
            {
                txtProviderStatus.Text = "PayPal-Fehler";
                MessageBox.Show($"PayPal-Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnPayPalSync.IsEnabled = true;
            }
        }

        private async void MollieSync_Click(object sender, RoutedEventArgs e)
        {
            if (_payment == null)
            {
                MessageBox.Show("PaymentService nicht konfiguriert.\n\nBitte konfigurieren Sie Mollie in den Einstellungen.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtProviderStatus.Text = "Synchronisiere Mollie...";
                btnMollieSync.IsEnabled = false;

                var von = DateTime.Now.AddDays(-30);
                var payments = await _payment.GetMolliePaymentsAsync(von);

                if (payments.Count == 0)
                {
                    txtProviderStatus.Text = "Keine Mollie-Zahlungen gefunden";
                    MessageBox.Show("Keine Mollie-Zahlungen im Zeitraum gefunden.",
                        "Mollie", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Verarbeiten und matchen
                var gebucht = await _payment.ProcessPaymentsAsync();

                var bezahlt = payments.Count(p => p.Status == "paid");
                txtProviderStatus.Text = $"Mollie: {payments.Count} Zahlungen, {gebucht} gematcht";
                MessageBox.Show($"Mollie-Synchronisation abgeschlossen!\n\n" +
                    $"Zahlungen abgerufen: {payments.Count}\n" +
                    $"Davon bezahlt: {bezahlt}\n" +
                    $"Automatisch zugeordnet: {gebucht}",
                    "Mollie", MessageBoxButton.OK, MessageBoxImage.Information);

                await LadeZahlungenAsync();
            }
            catch (Exception ex)
            {
                txtProviderStatus.Text = "Mollie-Fehler";
                MessageBox.Show($"Mollie-Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnMollieSync.IsEnabled = true;
            }
        }

        #endregion

        #region Manuelle Zuordnung

        private async void Zuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (dgZahlungen.SelectedItem is not ZahlungsabgleichEintrag zahlung) return;

            var dialog = new ZahlungZuordnenDialog(zahlung);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.WurdeZugeordnet)
            {
                txtStatus.Text = "Zahlung erfolgreich zugeordnet";
                await LadeZahlungenAsync();
            }
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            if (dgZahlungen.SelectedItem is not ZahlungsabgleichEintrag zahlung) return;

            MessageBox.Show(
                $"Transaktions-ID: {zahlung.TransaktionsId}\n" +
                $"Datum: {zahlung.Buchungsdatum:dd.MM.yyyy}\n" +
                $"Betrag: {zahlung.Betrag:N2} {zahlung.Waehrung}\n" +
                $"Name: {zahlung.Name}\n" +
                $"IBAN: {zahlung.Konto}\n\n" +
                $"Verwendungszweck:\n{zahlung.Verwendungszweck}",
                "Zahlungsdetails", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Event Handlers

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            if (tabZahlungen.SelectedIndex == 1)
                await LadeSepaAsync();
            else
                await LadeZahlungenAsync();
        }

        private async void Tab_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != tabZahlungen || !IsLoaded) return;

            if (tabZahlungen.SelectedIndex == 1)
                await LadeSepaAsync();
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeZahlungenAsync();
        }

        private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeZahlungenAsync();
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = dgZahlungen.SelectedItem != null;
            btnZuordnen.IsEnabled = hasSelection;
            btnDetails.IsEnabled = hasSelection;
        }

        #endregion
    }

    #region ViewModels

    public class SepaRechnungViewModel : SepaRechnung, INotifyPropertyChanged
    {
        private bool _isSelected;

        public SepaRechnungViewModel(SepaRechnung source)
        {
            KRechnung = source.KRechnung;
            CRechnungsnummer = source.CRechnungsnummer;
            Brutto = source.Brutto;
            Offen = source.Offen;
            Faelligkeit = source.Faelligkeit;
            KKunde = source.KKunde;
            CKundenNr = source.CKundenNr;
            KundeName = source.KundeName;
            CIBAN = source.CIBAN;
            CBIC = source.CBIC;
            CKontoinhaber = source.CKontoinhaber;
            CMandatsreferenz = source.CMandatsreferenz;
            DMandatsDatum = source.DMandatsDatum;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    #endregion

    #region Converters

    public class IsNegativeConverter : IValueConverter
    {
        public static readonly IsNegativeConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d) return d < 0;
            if (value is double dd) return dd < 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
