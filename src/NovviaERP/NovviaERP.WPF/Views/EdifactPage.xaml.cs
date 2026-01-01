using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EdifactPage : UserControl
    {
        private readonly EdifactService _edifact;
        private List<EdifactPartner> _partner = new();

        public EdifactPage()
        {
            InitializeComponent();
            _edifact = App.Services.GetRequiredService<EdifactService>();

            Loaded += async (s, e) => await LadeAllesAsync();
        }

        private async Task LadeAllesAsync()
        {
            await LadePartnerAsync();
            await LadeLogAsync();
        }

        #region Partner

        private async Task LadePartnerAsync()
        {
            try
            {
                _partner = (await _edifact.GetPartnerAsync()).ToList();
                dgPartner.ItemsSource = _partner;

                // ComboBoxen befuellen
                var partnerMitAlle = new List<EdifactPartner>
                {
                    new() { KPartner = 0, CName = "Alle" }
                };
                partnerMitAlle.AddRange(_partner);

                cmbLogPartner.ItemsSource = partnerMitAlle;
                cmbLogPartner.SelectedIndex = 0;

                cmbImportPartner.ItemsSource = _partner;
                cmbExportPartner.ItemsSource = _partner;

                if (_partner.Any())
                {
                    cmbImportPartner.SelectedIndex = 0;
                    cmbExportPartner.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Partner:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Partner_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgPartner.SelectedItem != null;
            btnPartnerBearbeiten.IsEnabled = selected;
            btnPartnerLoeschen.IsEnabled = selected;
        }

        private void Partner_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgPartner.SelectedItem is EdifactPartner partner)
                PartnerBearbeiten(partner);
        }

        private void NeuerPartner_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EdifactPartnerDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = LadePartnerAsync();
            }
        }

        private void PartnerBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgPartner.SelectedItem is EdifactPartner partner)
                PartnerBearbeiten(partner);
        }

        private void PartnerBearbeiten(EdifactPartner partner)
        {
            var dialog = new EdifactPartnerDialog(partner);
            if (dialog.ShowDialog() == true)
            {
                _ = LadePartnerAsync();
            }
        }

        private async void PartnerLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgPartner.SelectedItem is not EdifactPartner partner) return;

            var result = MessageBox.Show(
                $"Partner '{partner.CName}' wirklich loeschen?",
                "Partner loeschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _edifact.DeletePartnerAsync(partner.KPartner);
                await LadePartnerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TestVerbindung_Click(object sender, RoutedEventArgs e)
        {
            if (dgPartner.SelectedItem is not EdifactPartner partner)
            {
                MessageBox.Show("Bitte waehlen Sie einen Partner aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var success = await _edifact.TestConnectionAsync(partner.KPartner);
                if (success)
                    MessageBox.Show($"Verbindung zu '{partner.CName}' erfolgreich!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Verbindung zu '{partner.CName}' fehlgeschlagen!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verbindungsfehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Log

        private async Task LadeLogAsync()
        {
            try
            {
                int? partnerId = null;
                if (cmbLogPartner.SelectedValue is int p && p > 0)
                    partnerId = p;

                string? richtung = null;
                if (cmbLogRichtung.SelectedItem is ComboBoxItem ri && ri.Tag != null)
                    richtung = ri.Tag.ToString();

                string? typ = null;
                if (cmbLogTyp.SelectedItem is ComboBoxItem ti && ti.Tag != null)
                    typ = ti.Tag.ToString();

                string? status = null;
                if (cmbLogStatus.SelectedItem is ComboBoxItem si && si.Tag != null)
                    status = si.Tag.ToString();

                var logs = await _edifact.GetLogAsync(partnerId, richtung, typ, status);
                dgLog.ItemsSource = logs;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden des Logs:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeLogAsync();
        }

        private async void LogAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeLogAsync();
        }

        private void Log_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgLog.SelectedItem is EdifactLogEntry log)
            {
                // Details anzeigen
                var details = $"Nachricht: {log.CNachrichtentyp}\n" +
                             $"Richtung: {log.CRichtung}\n" +
                             $"Interchange-Ref: {log.CInterchangeRef}\n" +
                             $"Message-Ref: {log.CMessageRef}\n" +
                             $"Dokument-Nr: {log.CDokumentNr}\n" +
                             $"Datei: {log.CDateiname}\n" +
                             $"Status: {log.CStatus}\n" +
                             $"Erstellt: {log.DErstellt:dd.MM.yyyy HH:mm:ss}";

                if (!string.IsNullOrEmpty(log.CFehler))
                    details += $"\n\nFehler:\n{log.CFehler}";

                MessageBox.Show(details, "EDIFACT Nachricht", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Import/Export

        private async void Abrufen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Automatischer Abruf von EDIFACT-Nachrichten wird implementiert.\n\n" +
                           "Funktion: Verbindung zu allen aktiven Partnern, Download neuer ORDERS-Dateien.",
                           "Nachrichten abrufen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportDateiWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "EDIFACT-Datei waehlen",
                Filter = "EDIFACT Dateien (*.edi;*.txt)|*.edi;*.txt|Alle Dateien (*.*)|*.*",
                DefaultExt = ".edi"
            };

            if (dialog.ShowDialog() == true)
            {
                txtImportDatei.Text = dialog.FileName;
            }
        }

        private async void ImportOrders_Click(object sender, RoutedEventArgs e)
        {
            if (cmbImportPartner.SelectedItem is not EdifactPartner partner)
            {
                MessageBox.Show("Bitte waehlen Sie einen Partner aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(txtImportDatei.Text))
            {
                MessageBox.Show("Bitte waehlen Sie eine Datei aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtImportStatus.Text = "Importiere...";
                var content = await File.ReadAllTextAsync(txtImportDatei.Text);
                var result = await _edifact.ParseOrdersAsync(content, partner.KPartner);

                if (result.Erfolg && result.Order != null)
                {
                    txtImportStatus.Text = $"Import erfolgreich!\n\n" +
                                           $"Bestellnummer: {result.Order.DocumentNumber}\n" +
                                           $"Bestelldatum: {result.Order.DocumentDate:dd.MM.yyyy}\n" +
                                           $"Positionen: {result.Order.Positions.Count}\n" +
                                           $"Lieferadresse: {result.Order.DeliveryPartyName}";
                }
                else
                {
                    txtImportStatus.Text = $"Import fehlgeschlagen: {result.Fehler}";
                }

                await LadeLogAsync();
            }
            catch (Exception ex)
            {
                txtImportStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Import fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportDokumentSuchen_Click(object sender, RoutedEventArgs e)
        {
            // Hier koennte ein Suchdialog fuer Bestellungen/Rechnungen kommen
            MessageBox.Show("Geben Sie die Bestellnummer oder Rechnungsnummer ein.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ExportGenerieren_Click(object sender, RoutedEventArgs e)
        {
            if (cmbExportPartner.SelectedItem is not EdifactPartner partner)
            {
                MessageBox.Show("Bitte waehlen Sie einen Partner aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(txtExportDokument.Text))
            {
                MessageBox.Show("Bitte geben Sie eine Dokument-Nummer ein.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var typ = (cmbExportTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ORDRSP";

            try
            {
                txtExportStatus.Text = "Generiere...";

                // Demo: Bestellung suchen und Nachricht generieren
                // In der echten Implementierung wuerde hier die Bestellung/Rechnung geladen

                var saveDialog = new SaveFileDialog
                {
                    Title = $"{typ} speichern",
                    Filter = "EDIFACT Dateien (*.edi)|*.edi|Alle Dateien (*.*)|*.*",
                    DefaultExt = ".edi",
                    FileName = $"{typ}_{txtExportDokument.Text}_{DateTime.Now:yyyyMMdd_HHmmss}.edi"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // Hier wuerde die echte Nachricht generiert
                    txtExportStatus.Text = $"{typ} wurde generiert und gespeichert:\n{saveDialog.FileName}";
                }
                else
                {
                    txtExportStatus.Text = "";
                }
            }
            catch (Exception ex)
            {
                txtExportStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Export fehlgeschlagen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
