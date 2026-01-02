using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class LieferantenView : UserControl
    {
        private readonly EinkaufService _einkaufService;
        private readonly MSV3Service _msv3Service;
        private readonly ABdataService _abdataService;
        private readonly CoreService _coreService;

        private List<LieferantUebersicht> _lieferanten = new();
        private List<LieferantenBestellungUebersicht> _bestellungen = new();
        private LieferantUebersicht? _selectedLieferant;
        private LieferantStammdaten? _selectedLiefStammdaten;
        private MSV3Lieferant? _selectedLiefMSV3;
        private List<LieferantEigenesFeldViewModel> _lieferantEigeneFelder = new();
        private bool _isEditMode = false;

        public LieferantenView()
        {
            InitializeComponent();

            _einkaufService = App.Services.GetRequiredService<EinkaufService>();
            _msv3Service = App.Services.GetRequiredService<MSV3Service>();
            _abdataService = App.Services.GetRequiredService<ABdataService>();
            _coreService = App.Services.GetRequiredService<CoreService>();

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Spalten-Konfiguration für alle DataGrids
                DataGridColumnConfig.EnableColumnChooser(dgLieferanten, "LieferantenView");
                DataGridColumnConfig.EnableColumnChooser(dgBestellungen, "LieferantenView.Bestellungen");
                DataGridColumnConfig.EnableColumnChooser(dgBestellPositionen, "LieferantenView.Positionen");
                DataGridColumnConfig.EnableColumnChooser(dgABdataArtikel, "LieferantenView.ABdata");
                DataGridColumnConfig.EnableColumnChooser(dgMSV3Log, "LieferantenView.MSV3Log");
                DataGridColumnConfig.EnableColumnChooser(dgLieferantEigeneFelder, "LieferantenView.EigeneFelder");

                await LadeLieferantenAsync();
                await LadeBestellungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Lieferanten

        private async Task LadeLieferantenAsync()
        {
            _lieferanten = (await _einkaufService.GetLieferantenUebersichtAsync()).ToList();

            if (chkNurMSV3.IsChecked == true)
                dgLieferanten.ItemsSource = _lieferanten.Where(l => l.NHatMSV3).ToList();
            else
                dgLieferanten.ItemsSource = _lieferanten;
        }

        private async void LieferantSuchen_Click(object sender, RoutedEventArgs e)
        {
            var suche = txtLieferantSuche.Text.ToLower();

            var gefiltert = _lieferanten.Where(l =>
                string.IsNullOrEmpty(suche) ||
                (l.CFirma?.ToLower().Contains(suche) ?? false) ||
                (l.CLiefNr?.ToLower().Contains(suche) ?? false) ||
                (l.COrt?.ToLower().Contains(suche) ?? false)
            );

            if (chkNurMSV3.IsChecked == true)
                gefiltert = gefiltert.Where(l => l.NHatMSV3);

            dgLieferanten.ItemsSource = gefiltert.ToList();
        }

        private void ChkNurMSV3_Changed(object sender, RoutedEventArgs e)
        {
            // Automatisch filtern wenn Checkbox geändert wird
            LieferantSuchen_Click(sender, e);
        }

        private void NeuerLieferant_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lieferanten werden in JTL-Wawi verwaltet.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Lieferant_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgLieferanten.SelectedItem is LieferantUebersicht lieferant)
            {
                _selectedLieferant = lieferant;
                pnlLieferantDetail.Visibility = Visibility.Visible;

                txtLiefFirma.Text = lieferant.CFirma;
                txtLiefNr.Text = $"Lieferanten-Nr: {lieferant.CLiefNr}";

                // Vollständige Stammdaten laden
                await LadeLieferantStammdatenAsync(lieferant.KLieferant);
                await LadeLieferantMSV3ConfigAsync(lieferant.KLieferant);
                await LadeLieferantEigeneFelderAsync(lieferant.KLieferant);
                await pnlTextmeldungen.LoadAsync("Lieferant", lieferant.KLieferant, "Einkauf");
                await pnlTextmeldungen.ShowPopupAsync("Lieferant", lieferant.KLieferant, "Einkauf", lieferant.CFirma ?? "");
            }
            else
            {
                _selectedLieferant = null;
                _selectedLiefStammdaten = null;
                _lieferantEigeneFelder.Clear();
                dgLieferantEigeneFelder.ItemsSource = null;
                pnlLieferantDetail.Visibility = Visibility.Collapsed;
                pnlTextmeldungen.Clear();
            }
        }

        private async Task LadeLieferantStammdatenAsync(int kLieferant)
        {
            try
            {
                _selectedLiefStammdaten = await _einkaufService.GetLieferantStammdatenAsync(kLieferant);

                if (_selectedLiefStammdaten != null)
                {
                    // Identifikation
                    txtLiefLiefNr.Text = _selectedLiefStammdaten.LiefNr ?? "-";
                    txtLiefEigeneKdNr.Text = _selectedLiefStammdaten.EigeneKundennr ?? "-";
                    txtLiefStatus.Text = _selectedLiefStammdaten.AktivText;

                    // Adresse & Kontakt
                    txtLiefStrasse.Text = _selectedLiefStammdaten.Strasse ?? "";
                    txtLiefPLZ.Text = _selectedLiefStammdaten.PLZ ?? "";
                    txtLiefOrt.Text = _selectedLiefStammdaten.Ort ?? "";
                    txtLiefLand.Text = _selectedLiefStammdaten.Land ?? "";
                    txtLiefAnsprechpartner.Text = _selectedLiefStammdaten.Ansprechpartner ?? "-";
                    txtLiefTel.Text = _selectedLiefStammdaten.Tel ?? "-";
                    txtLiefTelDurchwahl.Text = _selectedLiefStammdaten.TelDurchwahl ?? "-";
                    txtLiefMobil.Text = _selectedLiefStammdaten.Mobil ?? "-";
                    txtLiefFax.Text = _selectedLiefStammdaten.Fax ?? "-";
                    txtLiefEmail.Text = _selectedLiefStammdaten.EMail ?? "-";
                    txtLiefHomepage.Text = _selectedLiefStammdaten.Homepage ?? "-";

                    // Finanzdaten
                    txtLiefUstId.Text = _selectedLiefStammdaten.UstId ?? "-";
                    txtLiefUstBefreit.Text = _selectedLiefStammdaten.UstBefreit ? "✅ Ja" : "❌ Nein";
                    txtLiefGLN.Text = _selectedLiefStammdaten.GLN ?? "-";
                    txtLiefKreditorNr.Text = _selectedLiefStammdaten.KreditorNr?.ToString() ?? "-";
                    txtLiefWaehrung.Text = _selectedLiefStammdaten.Bestellwaehrung ?? "EUR";
                    txtLiefIBAN.Text = FormatIBAN(_selectedLiefStammdaten.IBAN) ?? "-";
                    txtLiefBIC.Text = _selectedLiefStammdaten.BIC ?? "-";
                    txtLiefBank.Text = _selectedLiefStammdaten.Bankname ?? "-";

                    // Konditionen
                    txtLiefZahlungsziel.Text = _selectedLiefStammdaten.ZahlungszielText;
                    txtLiefSkonto.Text = _selectedLiefStammdaten.SkontoText;
                    txtLiefMindestbestellwert.Text = _selectedLiefStammdaten.MindestbestellwertText;
                    txtLiefMindermenge.Text = _selectedLiefStammdaten.MindermengenzuschlagText;
                    txtLiefFrachtkosten.Text = _selectedLiefStammdaten.FrachtkostenText;
                    txtLiefFreiHausAb.Text = _selectedLiefStammdaten.FreiHausAbText;
                    txtLiefLieferzeit.Text = _selectedLiefStammdaten.LieferzeitText;
                    txtLiefRabatt.Text = _selectedLiefStammdaten.RabattText;

                    // Dropshipping
                    txtLiefDropshipping.Text = _selectedLiefStammdaten.DropshippingText;
                    txtLiefDropshippingNN.Text = _selectedLiefStammdaten.DropshippingNachnahme ? "✅ Ja" : "❌ Nein";
                    txtLiefDropshippingFP.Text = _selectedLiefStammdaten.DropshippingFreipos ? "✅ Ja" : "❌ Nein";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Stammdaten: {ex.Message}");
            }
        }

        private static string? FormatIBAN(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return null;
            // IBAN in 4er-Gruppen formatieren: DE89 3704 0044 0532 0130 00
            var clean = iban.Replace(" ", "");
            var formatted = string.Join(" ", Enumerable.Range(0, (clean.Length + 3) / 4)
                .Select(i => clean.Substring(i * 4, Math.Min(4, clean.Length - i * 4))));
            return formatted;
        }

        private void LiefEmail_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_selectedLiefStammdaten?.EMail != null && _selectedLiefStammdaten.EMail != "-")
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = $"mailto:{_selectedLiefStammdaten.EMail}",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void LiefHomepage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_selectedLiefStammdaten?.Homepage != null && _selectedLiefStammdaten.Homepage != "-")
            {
                try
                {
                    var url = _selectedLiefStammdaten.Homepage;
                    if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        url = "https://" + url;

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        #region Lieferant Bearbeiten

        private void LiefBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode(true);
        }

        private async void LiefSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLieferant == null) return;

            try
            {
                btnLiefSpeichern.IsEnabled = false;
                btnLiefSpeichern.Content = "Speichere...";

                // Daten aus Feldern sammeln (korrekte JTL-Spaltennamen)
                var updateData = new Dictionary<string, object?>
                {
                    ["cStrasse"] = txtLiefStrasse.Text.Trim(),
                    ["cPLZ"] = txtLiefPLZ.Text.Trim(),
                    ["cOrt"] = txtLiefOrt.Text.Trim(),
                    ["cLand"] = txtLiefLand.Text.Trim(),
                    ["cKontakt"] = txtLiefAnsprechpartner.Text.Trim(),
                    ["cTelZentralle"] = txtLiefTel.Text.Trim(),
                    ["cTelDurchwahl"] = txtLiefTelDurchwahl.Text.Trim(),
                    ["cFax"] = txtLiefFax.Text.Trim(),
                    ["cEMail"] = txtLiefEmail.Text.Trim(),
                    ["cWWW"] = txtLiefHomepage.Text.Trim()
                };

                await _einkaufService.UpdateLieferantAsync(_selectedLieferant.KLieferant, updateData);

                MessageBox.Show("Lieferant erfolgreich gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                SetEditMode(false);

                // Daten neu laden
                await LadeLieferantStammdatenAsync(_selectedLieferant.KLieferant);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLiefSpeichern.IsEnabled = true;
                btnLiefSpeichern.Content = "💾 Speichern";
            }
        }

        private async void LiefAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode(false);

            // Daten zurücksetzen
            if (_selectedLieferant != null)
            {
                await LadeLieferantStammdatenAsync(_selectedLieferant.KLieferant);
            }
        }

        private void SetEditMode(bool editMode)
        {
            _isEditMode = editMode;

            // Buttons umschalten
            btnLiefBearbeiten.Visibility = editMode ? Visibility.Collapsed : Visibility.Visible;
            btnLiefSpeichern.Visibility = editMode ? Visibility.Visible : Visibility.Collapsed;
            btnLiefAbbrechen.Visibility = editMode ? Visibility.Visible : Visibility.Collapsed;

            // TextBoxen editierbar machen
            var editableStyle = editMode;
            txtLiefStrasse.IsReadOnly = !editMode;
            txtLiefStrasse.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefStrasse.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefPLZ.IsReadOnly = !editMode;
            txtLiefPLZ.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefPLZ.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefOrt.IsReadOnly = !editMode;
            txtLiefOrt.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefOrt.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefLand.IsReadOnly = !editMode;
            txtLiefLand.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefLand.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefAnsprechpartner.IsReadOnly = !editMode;
            txtLiefAnsprechpartner.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefAnsprechpartner.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefTel.IsReadOnly = !editMode;
            txtLiefTel.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefTel.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefTelDurchwahl.IsReadOnly = !editMode;
            txtLiefTelDurchwahl.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefTelDurchwahl.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefMobil.IsReadOnly = !editMode;
            txtLiefMobil.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefMobil.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefFax.IsReadOnly = !editMode;
            txtLiefFax.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefFax.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefEmail.IsReadOnly = !editMode;
            txtLiefEmail.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefEmail.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;

            txtLiefHomepage.IsReadOnly = !editMode;
            txtLiefHomepage.BorderThickness = editMode ? new Thickness(1) : new Thickness(0);
            txtLiefHomepage.Background = editMode ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;
        }

        #endregion

        private async Task LadeLieferantMSV3ConfigAsync(int kLieferant)
        {
            _selectedLiefMSV3 = await _msv3Service.GetMSV3LieferantAsync(kLieferant);

            if (_selectedLiefMSV3 != null)
            {
                txtLiefMSV3Url.Text = _selectedLiefMSV3.CMSV3Url;
                txtLiefMSV3Benutzer.Text = _selectedLiefMSV3.CBenutzer;
                txtLiefMSV3Passwort.Password = _selectedLiefMSV3.CPasswort;
                txtLiefMSV3Kundennr.Text = _selectedLiefMSV3.CKundennummer;
                txtLiefMSV3Filiale.Text = _selectedLiefMSV3.CFiliale;
                cboLiefMSV3Version.SelectedIndex = _selectedLiefMSV3.NMSV3Version == 2 ? 1 : 0;
                chkLiefMSV3Aktiv.IsChecked = _selectedLiefMSV3.NAktiv;
            }
            else
            {
                _selectedLiefMSV3 = new MSV3Lieferant { KLieferant = kLieferant };
                txtLiefMSV3Url.Text = "";
                txtLiefMSV3Benutzer.Text = "";
                txtLiefMSV3Passwort.Password = "";
                txtLiefMSV3Kundennr.Text = "";
                txtLiefMSV3Filiale.Text = "001";
                cboLiefMSV3Version.SelectedIndex = 0;
                chkLiefMSV3Aktiv.IsChecked = false;
            }
        }

        private async void LieferantMSV3Test_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLiefMSV3 == null || _selectedLieferant == null) return;

            UpdateLiefMSV3Config();

            try
            {
                // VerbindungTesten - der sichere Test-Endpoint (keine Bestellung!)
                var result = await _msv3Service.VerbindungTestenAsync(_selectedLiefMSV3);

                if (result.Success)
                {
                    MessageBox.Show($"MSV3-Verbindung erfolgreich!\n\n{result.Meldung}",
                        "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Zeige auch die Response für Debugging
                    var responseInfo = !string.IsNullOrEmpty(result.ResponseXml)
                        ? result.ResponseXml.Length > 500 ? result.ResponseXml.Substring(0, 500) + "..." : result.ResponseXml
                        : "(keine Response)";
                    MessageBox.Show($"MSV3-Fehler:\n{result.Fehler}\n\n--- Response ---\n{responseInfo}", "Test fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verbindungsfehler: {ex.Message}", "Test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LieferantMSV3Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLiefMSV3 == null || _selectedLieferant == null) return;

            UpdateLiefMSV3Config();

            try
            {
                await _msv3Service.SaveMSV3LieferantAsync(_selectedLiefMSV3);
                MessageBox.Show("MSV3-Konfiguration gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeLieferantenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateLiefMSV3Config()
        {
            if (_selectedLiefMSV3 == null) return;

            _selectedLiefMSV3.CMSV3Url = txtLiefMSV3Url.Text;
            _selectedLiefMSV3.CBenutzer = txtLiefMSV3Benutzer.Text;
            _selectedLiefMSV3.CPasswort = txtLiefMSV3Passwort.Password;
            _selectedLiefMSV3.CKundennummer = txtLiefMSV3Kundennr.Text;
            _selectedLiefMSV3.CFiliale = txtLiefMSV3Filiale.Text;
            _selectedLiefMSV3.NAktiv = chkLiefMSV3Aktiv.IsChecked ?? false;
            _selectedLiefMSV3.NMSV3Version = cboLiefMSV3Version.SelectedIndex == 1 ? 2 : 1;
        }

        private void Lieferant_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

        private async void PZNAbfragen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLiefMSV3 == null || string.IsNullOrWhiteSpace(txtLiefMSV3Url.Text))
            {
                MessageBox.Show("Bitte MSV3-Konfiguration eintragen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pzn = txtPZNAbfrage.Text.Trim();
            if (string.IsNullOrEmpty(pzn))
            {
                MessageBox.Show("Bitte PZN eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateLiefMSV3Config();

            try
            {
                // GEHE-Erkennung: Keine separate Bestandsabfrage möglich!
                bool isGehe = _selectedLiefMSV3.CMSV3Url?.Contains("gehe", StringComparison.OrdinalIgnoreCase) == true;

                if (isGehe)
                {
                    // GEHE: Warnung anzeigen - dies löst eine ECHTE Bestellung aus!
                    var answer = MessageBox.Show(
                        $"ACHTUNG: GEHE unterstützt keine separate Bestandsabfrage!\n\n" +
                        $"Dies wird eine ECHTE BESTELLUNG für PZN {pzn} (Menge: 1) auslösen.\n\n" +
                        $"Die Verfügbarkeitsinformationen werden aus der Bestellantwort ausgelesen.\n\n" +
                        $"Fortfahren?",
                        "GEHE Bestellung",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (answer != MessageBoxResult.Yes)
                        return;

                    // GEHE: Verwende bestellen-Operation
                    var positionen = new List<MSV3BestellPosition>
                    {
                        new MSV3BestellPosition { PZN = pzn, Menge = 1 }
                    };

                    var geheResult = await _msv3Service.CheckVerfuegbarkeitViaBestellenAsync(_selectedLiefMSV3, positionen);

                    if (!geheResult.Success)
                    {
                        // Zeige auch die Response für Debugging
                        var responseInfo = !string.IsNullOrEmpty(geheResult.ResponseXml)
                            ? geheResult.ResponseXml.Length > 1000 ? geheResult.ResponseXml.Substring(0, 1000) + "..." : geheResult.ResponseXml
                            : "(keine Response)";
                        MessageBox.Show($"GEHE-Fehler:\n{geheResult.Fehler}\n\n--- Response ---\n{responseInfo}", "MSV3-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // GEHE Response anzeigen
                    var gehePos = geheResult.Positionen.FirstOrDefault();
                    pnlPZNErgebnis.Visibility = Visibility.Visible;
                    txtPZNArtikel.Text = $"PZN {pzn} (GEHE Bestellung)";

                    if (gehePos != null)
                    {
                        // Verfügbare Menge aus Anteilen berechnen
                        int verfuegbareMenge = gehePos.VerfuegbareMenge;
                        txtPZNBestand.Text = verfuegbareMenge.ToString("N0");
                        txtPZNBestand.Foreground = new System.Windows.Media.SolidColorBrush(
                            verfuegbareMenge > 0 ? System.Windows.Media.Colors.Green : System.Windows.Media.Colors.Red);

                        // Status aus Anteilen
                        txtPZNStatus.Text = gehePos.StatusCode;
                        txtPZNMHD.Text = "-";
                        txtPZNCharge.Text = "-";
                        txtPZNPreis.Text = "-";

                        // Details anzeigen
                        var details = new System.Text.StringBuilder();
                        details.AppendLine($"BestellSupportId: {geheResult.BestellSupportId}");
                        details.AppendLine($"Nachtbetrieb: {geheResult.NachtBetrieb}");
                        details.AppendLine();
                        foreach (var anteil in gehePos.Anteile)
                        {
                            details.AppendLine($"Anteil: Menge={anteil.Menge}, Typ={anteil.Typ}");
                            if (!string.IsNullOrEmpty(anteil.Grund))
                                details.AppendLine($"  Grund: {anteil.Grund}");
                            if (anteil.Lieferzeitpunkt.HasValue)
                                details.AppendLine($"  Lieferzeitpunkt: {anteil.Lieferzeitpunkt:dd.MM.yyyy HH:mm}");
                        }

                        MessageBox.Show(
                            $"GEHE Bestellung erfolgreich!\n\n" +
                            $"PZN: {pzn}\n" +
                            $"Verfügbare Menge: {verfuegbareMenge}\n" +
                            $"Status: {gehePos.StatusCode}\n" +
                            $"Haupt-Typ: {gehePos.HauptTyp}\n" +
                            $"Haupt-Grund: {gehePos.HauptGrund}\n\n" +
                            $"--- Details ---\n{details}",
                            "GEHE Bestellung OK",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        txtPZNBestand.Text = "-";
                        txtPZNStatus.Text = "Keine Position in Antwort";
                        txtPZNMHD.Text = "-";
                        txtPZNCharge.Text = "-";
                        txtPZNPreis.Text = "-";
                    }

                    return;
                }

                // Standard MSV3: VerfuegbarkeitAbfragen verwenden
                var result = await _msv3Service.CheckVerfuegbarkeitAsync(_selectedLiefMSV3, new List<string> { pzn });

                if (!result.Success)
                {
                    MessageBox.Show($"Fehler: {result.Fehler}", "MSV3-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var pos = result.Positionen.FirstOrDefault();
                if (pos == null)
                {
                    pnlPZNErgebnis.Visibility = Visibility.Visible;
                    txtPZNArtikel.Text = $"PZN {pzn} - Keine Daten";
                    txtPZNBestand.Text = "-";
                    txtPZNMHD.Text = "-";
                    txtPZNCharge.Text = "-";
                    txtPZNPreis.Text = "-";
                    txtPZNStatus.Text = "Keine Antwort";
                    return;
                }

                pnlPZNErgebnis.Visibility = Visibility.Visible;
                txtPZNArtikel.Text = $"PZN {pzn}";
                txtPZNBestand.Text = pos.MengeVerfuegbar.ToString("N0");
                txtPZNBestand.Foreground = new System.Windows.Media.SolidColorBrush(
                    pos.Verfuegbar ? System.Windows.Media.Colors.Green : System.Windows.Media.Colors.Red);
                txtPZNMHD.Text = pos.MHD?.ToString("dd.MM.yyyy") ?? "-";
                txtPZNCharge.Text = pos.ChargenNr ?? "-";
                txtPZNPreis.Text = pos.PreisEK > 0 ? pos.PreisEK.ToString("C") : (pos.PreisAEP > 0 ? pos.PreisAEP.ToString("C") + " (AEP)" : "-");
                txtPZNStatus.Text = pos.Status;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "MSV3-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Bestellungen

        private async Task LadeBestellungenAsync(int? status = null)
        {
            _bestellungen = (await _einkaufService.GetBestellungenAsync(status)).ToList();
            dgBestellungen.ItemsSource = _bestellungen;
        }

        private async void BestellStatus_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            int? status = null;
            if (cboBestellStatus.SelectedItem is ComboBoxItem item && item.Tag != null)
                status = int.Parse(item.Tag.ToString()!);

            await LadeBestellungenAsync(status);
        }

        private async void BestellungenLaden_Click(object sender, RoutedEventArgs e)
        {
            int? status = null;
            if (cboBestellStatus.SelectedItem is ComboBoxItem item && item.Tag != null)
                status = int.Parse(item.Tag.ToString()!);

            await LadeBestellungenAsync(status);
        }

        private List<BestellPositionLieferantZeile> _currentZeilen = new();

        private async void Bestellung_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is LieferantenBestellungUebersicht bestellung)
            {
                // Prüfen ob mindestens ein MSV3-Lieferant verfügbar ist (wird nach Laden der Positionen aktualisiert)
                btnMSV3Bestand.IsEnabled = false;
                btnMSV3Senden.IsEnabled = false;
                btnBestandAlleAbfragen.IsEnabled = false;
                btnBestellungUebermitteln.IsEnabled = false;

                // Positionen MIT Lieferantenauswahl laden
                var vollstaendigeBestellung = await _einkaufService.GetBestellungAsync(bestellung.KLieferantenBestellung, mitLieferantenAuswahl: true);
                var positionen = vollstaendigeBestellung?.Positionen ?? new List<LieferantenBestellungPos>();

                // Flache Darstellung erstellen: Eine Zeile pro Artikel-Lieferant-Kombination
                _currentZeilen = FlattenPositionen(positionen);
                dgBestellPositionen.ItemsSource = _currentZeilen;

                // Prüfen ob MSV3-Lieferanten verfügbar sind
                bool hatMSV3Positionen = _currentZeilen.Any(z => z.HatMSV3);
                btnMSV3Bestand.IsEnabled = hatMSV3Positionen;
                btnMSV3Senden.IsEnabled = hatMSV3Positionen && bestellung.NStatus == 1;
                btnBestandAlleAbfragen.IsEnabled = hatMSV3Positionen;
                btnBestellungUebermitteln.IsEnabled = hatMSV3Positionen;
            }
            else
            {
                btnMSV3Bestand.IsEnabled = false;
                btnMSV3Senden.IsEnabled = false;
                btnBestandAlleAbfragen.IsEnabled = false;
                btnBestellungUebermitteln.IsEnabled = false;
                _currentZeilen.Clear();
                dgBestellPositionen.ItemsSource = null;
            }
        }

        /// <summary>
        /// Erstellt flache Darstellung: Eine Zeile pro Artikel-Lieferant-Kombination.
        /// Automatisch Häkchen beim günstigsten Lieferant pro Artikel.
        /// </summary>
        private List<BestellPositionLieferantZeile> FlattenPositionen(List<LieferantenBestellungPos> positionen)
        {
            var zeilen = new List<BestellPositionLieferantZeile>();

            foreach (var pos in positionen)
            {
                if (pos.VerfuegbareLieferanten.Any())
                {
                    // Eine Zeile pro Lieferant
                    foreach (var lief in pos.VerfuegbareLieferanten)
                    {
                        // EK Netto: Bestellposition > 0 ? Bestellposition : Stammdaten
                        var ekNetto = pos.FEKNetto > 0 ? pos.FEKNetto : lief.EKNetto;

                        zeilen.Add(new BestellPositionLieferantZeile
                        {
                            KLieferantenBestellungPos = pos.KLieferantenBestellungPos,
                            KArtikel = pos.KArtikel,
                            CArtNr = pos.CArtNr,
                            ArtikelName = pos.ArtikelName,
                            CPZN = pos.CPZN,
                            FMenge = pos.FMenge,
                            CHinweis = pos.CHinweis,
                            KLieferant = lief.KLieferant,
                            KMSV3Lieferant = lief.KMSV3Lieferant,
                            LieferantName = lief.LieferantName,
                            LieferantenArtNr = lief.LieferantenArtNr,
                            EKNetto = ekNetto, // Bestellposition oder Stammdaten-Fallback
                            HatMSV3 = lief.HatMSV3,
                            IstAusgewaehlt = false, // Wird unten gesetzt
                            WunschMHD = ParseMinMHDFromHinweis(pos.CHinweis), // Parsen aus Hinweis
                            CPositionsText = pos.CHinweis // Positionstext aus Hinweis
                        });
                    }
                }
                else
                {
                    // Kein Lieferant - trotzdem eine Zeile anzeigen
                    zeilen.Add(new BestellPositionLieferantZeile
                    {
                        KLieferantenBestellungPos = pos.KLieferantenBestellungPos,
                        KArtikel = pos.KArtikel,
                        CArtNr = pos.CArtNr,
                        ArtikelName = pos.ArtikelName,
                        CPZN = pos.CPZN,
                        FMenge = pos.FMenge,
                        CHinweis = pos.CHinweis,
                        KLieferant = 0,
                        LieferantName = "(kein Lieferant)",
                        EKNetto = pos.FEKNetto,
                        HatMSV3 = false,
                        IstAusgewaehlt = false,
                        WunschMHD = ParseMinMHDFromHinweis(pos.CHinweis),
                        CPositionsText = pos.CHinweis
                    });
                }
            }

            // Automatisch günstigsten MSV3-Lieferant pro Artikel auswählen
            var artikelGruppen = zeilen.GroupBy(z => z.ArtikelKey);
            foreach (var gruppe in artikelGruppen)
            {
                var msv3Zeilen = gruppe.Where(z => z.HatMSV3).ToList();
                if (msv3Zeilen.Any())
                {
                    // Günstigsten MSV3-Lieferant auswählen
                    var guenstigster = msv3Zeilen.OrderBy(z => z.EKNetto).First();
                    guenstigster.IstAusgewaehlt = true;
                }
                else
                {
                    // Kein MSV3 - günstigsten normalen Lieferant auswählen
                    var guenstigster = gruppe.OrderBy(z => z.EKNetto).FirstOrDefault();
                    if (guenstigster != null)
                        guenstigster.IstAusgewaehlt = true;
                }
            }

            return zeilen.OrderBy(z => z.ArtikelName).ThenBy(z => z.EKNetto).ToList();
        }

        private async void BestandAlleAbfragen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung) return;
            if (!_currentZeilen.Any()) return;

            try
            {
                btnBestandAlleAbfragen.IsEnabled = false;
                btnBestandAlleAbfragen.Content = "Abfrage läuft...";

                // Nur ausgewählte Zeilen mit MSV3-Lieferant
                var zeilenMitMSV3 = _currentZeilen
                    .Where(z => z.IstAusgewaehlt && z.HatMSV3 && !string.IsNullOrEmpty(z.CPZN) && z.KMSV3Lieferant.HasValue)
                    .ToList();

                if (!zeilenMitMSV3.Any())
                {
                    MessageBox.Show("Keine Positionen mit MSV3-Lieferant ausgewählt.\n\n" +
                        "Bitte setzen Sie ein Häkchen bei den gewünschten Lieferanten.",
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var gruppen = zeilenMitMSV3
                    .GroupBy(z => z.KMSV3Lieferant!.Value)
                    .ToList();

                var zusammenfassung = new System.Text.StringBuilder();
                zusammenfassung.AppendLine($"Bestandsabfrage für {gruppen.Count} Lieferant(en):\n");

                int gesamtVerfuegbar = 0, gesamtTeilweise = 0, gesamtNichtVerfuegbar = 0;

                foreach (var gruppe in gruppen)
                {
                    var kMSV3Lieferant = gruppe.Key;
                    var msv3Config = await _msv3Service.GetMSV3LieferantByIdAsync(kMSV3Lieferant);

                    if (msv3Config == null)
                    {
                        zusammenfassung.AppendLine($"⚠ Lieferant #{kMSV3Lieferant}: Keine MSV3-Konfiguration");
                        continue;
                    }

                    var pzns = gruppe.Select(z => z.CPZN!).Distinct().ToList();
                    var lieferantName = gruppe.First().LieferantName ?? $"#{kMSV3Lieferant}";

                    // GEHE/Alliance: Bestandsabfrage nur über bestellen möglich
                    bool isGehe = msv3Config.CMSV3Url?.Contains("gehe", StringComparison.OrdinalIgnoreCase) == true ||
                                  msv3Config.CMSV3Url?.Contains("alliance", StringComparison.OrdinalIgnoreCase) == true;

                    if (isGehe)
                    {
                        // GEHE/Alliance: Bestandsabfrage = Bestellung (gleiche Operation)
                        var bestellpositionen = gruppe.Select(z => new MSV3BestellPosition
                        {
                            PZN = z.CPZN!,
                            Menge = (int)z.FMenge,
                            LieferantenArtNr = z.LieferantenArtNr,
                            MinMHD = z.WunschMHD,
                            Freitext = z.CPositionsText
                        }).ToList();

                        var geheResult = await _msv3Service.CheckVerfuegbarkeitViaBestellenAsync(msv3Config, bestellpositionen);

                        // Logging
                        await _msv3Service.LogMSV3RequestAsync(
                            kMSV3Lieferant: kMSV3Lieferant,
                            aktion: "Bestellen",
                            requestXml: $"PZNs: {string.Join(", ", pzns)}",
                            responseXml: geheResult.ResponseXml,
                            httpStatus: geheResult.Success ? 200 : 500,
                            erfolg: geheResult.Success,
                            fehler: geheResult.Fehler,
                            bestellSupportId: geheResult.BestellSupportId,
                            kLieferantenBestellung: bestellung.KLieferantenBestellung);

                        if (!geheResult.Success)
                        {
                            zusammenfassung.AppendLine($"✗ {lieferantName}: {geheResult.Fehler}");
                            continue;
                        }

                        // Ergebnisse in Zeilen übernehmen
                        foreach (var zeile in gruppe)
                        {
                            var v = geheResult.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                            if (v != null)
                            {
                                zeile.MSV3Bestand = v.VerfuegbareMenge;
                                zeile.MSV3Verfuegbar = v.VerfuegbareMenge > 0;
                                zeile.MSV3StatusText = v.StatusCode;
                                zeile.MSV3Lieferzeit = v.NaechsterLieferzeitpunkt;
                            }
                        }

                        int geliefert = geheResult.Positionen.Count(p => p.VerfuegbareMenge > 0);
                        gesamtVerfuegbar += geliefert;
                        gesamtNichtVerfuegbar += geheResult.Positionen.Count(p => p.VerfuegbareMenge == 0);

                        zusammenfassung.AppendLine($"✓ {lieferantName}: {pzns.Count} PZNs (ID: {geheResult.BestellSupportId}, {geliefert} lieferbar)");

                        // GEHE Response Popup anzeigen
                        var gehePositionen = gruppe.Select(zeile => {
                            var v = geheResult.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                            return new MSV3ResponsePosition
                            {
                                PZN = zeile.CPZN,
                                ArtikelName = zeile.ArtikelName,
                                Menge = (int)zeile.FMenge,
                                VerfuegbareMenge = v?.VerfuegbareMenge ?? 0,
                                StatusCode = v?.StatusCode ?? "UNBEKANNT",
                                MHDText = v?.NaechsterLieferzeitpunkt?.ToString("dd.MM.yyyy") ?? "",
                                LieferantName = lieferantName
                            };
                        }).ToList();

                        var geheDlg = new MSV3ResponseDialog();
                        geheDlg.Owner = Window.GetWindow(this);
                        geheDlg.SetErgebnis(
                            $"GEHE Bestellung: {lieferantName}",
                            $"ID: {geheResult.BestellSupportId} - {geliefert} von {pzns.Count} lieferbar",
                            gehePositionen,
                            geheResult.ResponseXml
                        );
                        geheDlg.ShowDialog();

                        continue;
                    }

                    // Standard MSV3: VerfuegbarkeitAnfragen
                    var result = await _msv3Service.CheckVerfuegbarkeitAsync(msv3Config, pzns);

                    // Logging: Request + Response speichern
                    await _msv3Service.LogMSV3RequestAsync(
                        kMSV3Lieferant: kMSV3Lieferant,
                        aktion: "VerfuegbarkeitAnfragen",
                        requestXml: $"PZNs: {string.Join(", ", pzns)}",
                        responseXml: result.ResponseXml,
                        httpStatus: result.Success ? 200 : 500,
                        erfolg: result.Success,
                        fehler: result.Fehler,
                        kLieferantenBestellung: bestellung.KLieferantenBestellung);

                    if (!result.Success)
                    {
                        zusammenfassung.AppendLine($"✗ {lieferantName}: {result.Fehler}");
                        continue;
                    }

                    // Ergebnisse in Zeilen übernehmen
                    foreach (var zeile in gruppe)
                    {
                        var v = result.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                        if (v != null)
                        {
                            zeile.MSV3Bestand = v.Bestand;
                            zeile.MSV3Verfuegbar = v.Verfuegbar;
                            zeile.MSV3StatusText = v.Status;
                            zeile.MSV3MHD = v.MHD;
                            zeile.MSV3ChargenNr = v.ChargenNr;
                        }
                    }

                    gesamtVerfuegbar += result.AnzahlVerfuegbar;
                    gesamtTeilweise += result.AnzahlTeilweise;
                    gesamtNichtVerfuegbar += result.AnzahlNichtVerfuegbar;

                    zusammenfassung.AppendLine($"✓ {lieferantName}: {pzns.Count} PZNs ({result.AnzahlVerfuegbar} verfügbar)");
                    // Kein Popup - Ergebnisse werden im Grid angezeigt
                }

                dgBestellPositionen.ItemsSource = null;
                dgBestellPositionen.ItemsSource = _currentZeilen;
                // Kein Summary-Popup - Ergebnisse sind im Grid sichtbar
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "MSV3-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnBestandAlleAbfragen.IsEnabled = true;
                btnBestandAlleAbfragen.Content = "Bestand abfragen";
            }
        }

        private async void BestellungUebermitteln_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung) return;
            if (!_currentZeilen.Any()) return;

            try
            {
                btnBestellungUebermitteln.IsEnabled = false;
                btnBestellungUebermitteln.Content = "Pruefe...";

                // Nur ausgewählte Zeilen mit MSV3-Lieferant
                var zeilenMitMSV3 = _currentZeilen
                    .Where(z => z.IstAusgewaehlt && z.HatMSV3 && !string.IsNullOrEmpty(z.CPZN) && z.KMSV3Lieferant.HasValue)
                    .ToList();

                if (!zeilenMitMSV3.Any())
                {
                    MessageBox.Show("Keine Positionen mit MSV3-Lieferant ausgewaehlt.\n\n" +
                        "Bitte setzen Sie ein Haekchen bei den gewuenschten Lieferanten.",
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Prüfen ob bereits übermittelt
                var bereitsUebermittelt = await _msv3Service.GetLetzteMSV3BestellungAsync(bestellung.KLieferantenBestellung);
                if (bereitsUebermittelt != null)
                {
                    var antwort = MessageBox.Show(
                        $"Diese Bestellung wurde bereits uebermittelt!\n\n" +
                        $"Letzte Uebermittlung: {bereitsUebermittelt.DZeitpunkt:dd.MM.yyyy HH:mm}\n" +
                        $"BestellSupportId: {bereitsUebermittelt.CBestellSupportId ?? "-"}\n" +
                        $"MSV3-Auftragsnr: {bereitsUebermittelt.CMSV3AuftragsId ?? "-"}\n" +
                        $"Status: {(bereitsUebermittelt.NErfolg ? "Erfolgreich" : "Fehlgeschlagen")}\n\n" +
                        $"Trotzdem ERNEUT uebermitteln?",
                        "Bestellung bereits uebermittelt",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (antwort != MessageBoxResult.Yes)
                        return;
                }

                // Weiter mit der Bestellung (ruft die eigentliche Logik auf)
                btnBestellungUebermitteln.Content = "Sende...";
                await SendeMSV3BestellungAsync(bestellung, zeilenMitMSV3);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnBestellungUebermitteln.IsEnabled = true;
                btnBestellungUebermitteln.Content = "Bestellung uebermitteln";
            }
        }

        private async Task SendeMSV3BestellungAsync(LieferantenBestellungUebersicht bestellung, List<BestellPositionLieferantZeile> zeilenMitMSV3)
        {
            var gruppen = zeilenMitMSV3
                .GroupBy(z => z.KMSV3Lieferant!.Value)
                .ToList();

            // Detaillierte Bestätigung anfordern mit Positionsliste
            var confirmMsg = new System.Text.StringBuilder();
            confirmMsg.AppendLine($"MSV3-Bestellung an {gruppen.Count} Lieferant(en):\n");
            confirmMsg.AppendLine("═══════════════════════════════════════════════════════════\n");

            decimal gesamtSumme = 0;
            foreach (var gruppe in gruppen)
            {
                var lieferantName = gruppe.First().LieferantName ?? $"#{gruppe.Key}";
                confirmMsg.AppendLine($"► {lieferantName} ({gruppe.Count()} Positionen):");
                confirmMsg.AppendLine("───────────────────────────────────────────────────────────");

                decimal lieferantSumme = 0;
                foreach (var zeile in gruppe)
                {
                    var posWert = zeile.FMenge * zeile.EKNetto;
                    lieferantSumme += posWert;
                    var wunschMHD = zeile.WunschMHD?.ToString("dd.MM.yy") ?? "";
                    confirmMsg.AppendLine($"  {zeile.CPZN,-10} {zeile.ArtikelName?.Truncate(25),-25} {zeile.FMenge,5:N0} x {zeile.EKNetto,8:N2} € = {posWert,10:N2} €{(wunschMHD != "" ? $"  MHD: {wunschMHD}" : "")}");
                }
                confirmMsg.AppendLine($"                                              Summe: {lieferantSumme,10:N2} €");
                confirmMsg.AppendLine();
                gesamtSumme += lieferantSumme;
            }

            confirmMsg.AppendLine("═══════════════════════════════════════════════════════════");
            confirmMsg.AppendLine($"                                       GESAMTSUMME: {gesamtSumme,10:N2} €");
            confirmMsg.AppendLine("\nBestellung jetzt an die Lieferanten uebermitteln?");

            if (MessageBox.Show(confirmMsg.ToString(), "MSV3 Bestellung bestaetigen",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            var zusammenfassung = new System.Text.StringBuilder();
            zusammenfassung.AppendLine($"MSV3-Bestellung an {gruppen.Count} Lieferant(en):\n");

            int erfolgreich = 0, fehlgeschlagen = 0;

            foreach (var gruppe in gruppen)
            {
                var kMSV3Lieferant = gruppe.Key;
                var msv3Config = await _msv3Service.GetMSV3LieferantByIdAsync(kMSV3Lieferant);

                if (msv3Config == null)
                {
                    zusammenfassung.AppendLine($"⚠ Lieferant #{kMSV3Lieferant}: Keine MSV3-Konfiguration");
                    fehlgeschlagen++;
                    continue;
                }

                var lieferantName = gruppe.First().LieferantName ?? msv3Config.LieferantName ?? $"#{kMSV3Lieferant}";
                var pzns = gruppe.Select(z => z.CPZN!).Distinct().ToList();

                // Bestellpositionen für diesen Lieferanten
                var bestellpositionen = gruppe.Select(z => new MSV3BestellPosition
                {
                    PZN = z.CPZN!,
                    Menge = (int)z.FMenge,
                    LieferantenArtNr = z.LieferantenArtNr,
                    MinMHD = z.WunschMHD,
                    Freitext = z.CPositionsText
                }).ToList();

                // GEHE/Alliance Erkennung
                bool isGehe = msv3Config.CMSV3Url?.Contains("gehe", StringComparison.OrdinalIgnoreCase) == true ||
                              msv3Config.CMSV3Url?.Contains("alliance", StringComparison.OrdinalIgnoreCase) == true;

                string? requestInfo = $"Positionen: {string.Join(", ", bestellpositionen.Select(p => $"{p.PZN} x{p.Menge}"))}";

                if (isGehe)
                {
                    // GEHE: bestellen mit Verfügbarkeitsanzeige
                    var geheResult = await _msv3Service.CheckVerfuegbarkeitViaBestellenAsync(msv3Config, bestellpositionen);

                    // Logging
                    await _msv3Service.LogMSV3RequestAsync(
                        kMSV3Lieferant: kMSV3Lieferant,
                        aktion: "Bestellen",
                        requestXml: requestInfo,
                        responseXml: geheResult.ResponseXml,
                        httpStatus: geheResult.Success ? 200 : 500,
                        erfolg: geheResult.Success,
                        fehler: geheResult.Fehler,
                        bestellSupportId: geheResult.BestellSupportId,
                        kLieferantenBestellung: bestellung.KLieferantenBestellung);

                    if (!geheResult.Success)
                    {
                        zusammenfassung.AppendLine($"✗ {lieferantName}: {geheResult.Fehler}");
                        fehlgeschlagen++;
                        continue;
                    }

                    // Ergebnisse in Zeilen übernehmen
                    foreach (var zeile in gruppe)
                    {
                        var v = geheResult.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                        if (v != null)
                        {
                            zeile.MSV3Bestand = v.VerfuegbareMenge;
                            zeile.MSV3Verfuegbar = v.VerfuegbareMenge > 0;
                            zeile.MSV3StatusText = v.StatusCode;
                        }
                    }

                    int geliefert = geheResult.Positionen.Count(p => p.VerfuegbareMenge > 0);
                    zusammenfassung.AppendLine($"✓ {lieferantName}: {bestellpositionen.Count} Pos. (ID: {geheResult.BestellSupportId}, {geliefert} lieferbar)");
                    erfolgreich++;

                    // Popup pro Lieferant anzeigen
                    var lieferantPositionen = gruppe.Select(zeile => {
                        var v = geheResult.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                        return new MSV3ResponsePosition
                        {
                            PZN = zeile.CPZN,
                            ArtikelName = zeile.ArtikelName,
                            Menge = (int)zeile.FMenge,
                            VerfuegbareMenge = v?.VerfuegbareMenge ?? 0,
                            StatusCode = v?.StatusCode ?? "UNBEKANNT",
                            MHDText = v?.NaechsterLieferzeitpunkt?.ToString("dd.MM.yyyy") ?? "",
                            LieferantName = lieferantName
                        };
                    }).ToList();

                    var dlg = new MSV3ResponseDialog();
                    dlg.Owner = Window.GetWindow(this);
                    dlg.SetErgebnis(
                        $"MSV3 Bestellung: {lieferantName}",
                        $"ID: {geheResult.BestellSupportId} - {geliefert} von {bestellpositionen.Count} lieferbar",
                        lieferantPositionen,
                        geheResult.ResponseXml
                    );
                    dlg.ShowDialog();
                }
                else
                {
                    // Andere Großhändler: Standard SendBestellungAsync
                    var result = await _msv3Service.SendBestellungAsync(msv3Config, bestellung.CEigeneBestellnummer ?? "", bestellpositionen);

                    // Logging
                    await _msv3Service.LogMSV3RequestAsync(
                        kMSV3Lieferant: kMSV3Lieferant,
                        aktion: "Bestellen",
                        requestXml: requestInfo,
                        responseXml: result.ResponseXml,
                        httpStatus: result.Success ? 200 : 500,
                        erfolg: result.Success,
                        fehler: result.Fehlermeldung,
                        msv3AuftragsId: result.MSV3Bestellnummer,
                        kLieferantenBestellung: bestellung.KLieferantenBestellung);

                    // Response-Positionen für Popup sammeln
                    var lieferantPosList = gruppe.Select(zeile => new MSV3ResponsePosition
                    {
                        PZN = zeile.CPZN,
                        ArtikelName = zeile.ArtikelName,
                        Menge = (int)zeile.FMenge,
                        VerfuegbareMenge = result.Success ? (int)zeile.FMenge : 0,
                        StatusCode = result.Success ? "BESTELLT" : "FEHLER",
                        MHDText = zeile.WunschMHD?.ToString("dd.MM.yyyy") ?? "",
                        LieferantName = lieferantName
                    }).ToList();

                    if (result.Success)
                    {
                        zusammenfassung.AppendLine($"✓ {lieferantName}: {bestellpositionen.Count} Pos. (Nr: {result.MSV3Bestellnummer})");
                        erfolgreich++;
                    }
                    else
                    {
                        zusammenfassung.AppendLine($"✗ {lieferantName}: {result.Fehlermeldung}");
                        fehlgeschlagen++;
                    }

                    // Popup pro Lieferant anzeigen
                    var dlg2 = new MSV3ResponseDialog();
                    dlg2.Owner = Window.GetWindow(this);
                    dlg2.SetErgebnis(
                        $"MSV3 Bestellung: {lieferantName}",
                        result.Success
                            ? $"Bestellnummer: {result.MSV3Bestellnummer} - {bestellpositionen.Count} Positionen bestellt"
                            : $"FEHLER: {result.Fehlermeldung}",
                        lieferantPosList,
                        result.ResponseXml
                    );
                    dlg2.ShowDialog();
                }
            }

            dgBestellPositionen.ItemsSource = null;
            dgBestellPositionen.ItemsSource = _currentZeilen;

            // Zusammenfassung am Ende
            MessageBox.Show(zusammenfassung.ToString() + $"\n\nErfolgreich: {erfolgreich}\nFehlgeschlagen: {fehlgeschlagen}",
                "MSV3 Bestellung abgeschlossen", MessageBoxButton.OK,
                fehlgeschlagen > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await LadeBestellungenAsync();
        }

        private async void MSV3BestandAbfragen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung) return;
            if (!_currentZeilen.Any()) return;

            try
            {
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(bestellung.KLieferant);
                if (msv3Config == null)
                {
                    MessageBox.Show("Keine MSV3-Konfiguration.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var pzns = _currentZeilen.Where(z => !string.IsNullOrEmpty(z.CPZN)).Select(z => z.CPZN!).Distinct().ToList();
                if (!pzns.Any()) return;

                // GEHE: Hinweis auf "Bestand alle abfragen"
                bool isGehe = msv3Config.CMSV3Url?.Contains("gehe", StringComparison.OrdinalIgnoreCase) == true ||
                              msv3Config.CMSV3Url?.Contains("alliance", StringComparison.OrdinalIgnoreCase) == true;

                if (isGehe)
                {
                    MessageBox.Show("GEHE/Alliance: Bitte 'Bestand alle abfragen' verwenden.\n\n" +
                        "Einzelabfragen werden nicht unterstützt.",
                        "GEHE", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Andere Großhändler: Standard verfuegbarkeitAnfragen
                var result = await _msv3Service.CheckVerfuegbarkeitAsync(msv3Config, pzns);

                foreach (var zeile in _currentZeilen)
                {
                    if (string.IsNullOrEmpty(zeile.CPZN)) continue;
                    var v = result.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                    if (v != null)
                    {
                        zeile.MSV3Bestand = v.Bestand;
                        zeile.MSV3Verfuegbar = v.Verfuegbar;
                        zeile.MSV3StatusText = v.Status;
                        zeile.MSV3MHD = v.MHD;
                    }
                }

                dgBestellPositionen.ItemsSource = null;
                dgBestellPositionen.ItemsSource = _currentZeilen;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MSV3-Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void NeueBestellung_Click(object sender, RoutedEventArgs e)
        {
            if (dgLieferanten.SelectedItem is not LieferantUebersicht lieferant)
            {
                MessageBox.Show("Bitte Lieferanten auswählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var kBestellung = await _einkaufService.CreateBestellungAsync(lieferant.KLieferant, App.BenutzerId);
                await LadeBestellungenAsync();
                MessageBox.Show($"Bestellung {kBestellung} erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MSV3Senden_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung) return;
            if (!_currentZeilen.Any()) return;

            try
            {
                btnMSV3Senden.IsEnabled = false;
                btnMSV3Senden.Content = "Sende...";

                // Nur ausgewählte Zeilen mit MSV3-Lieferant
                var zeilenMitMSV3 = _currentZeilen
                    .Where(z => z.IstAusgewaehlt && z.HatMSV3 && !string.IsNullOrEmpty(z.CPZN) && z.KMSV3Lieferant.HasValue)
                    .ToList();

                if (!zeilenMitMSV3.Any())
                {
                    MessageBox.Show("Keine Positionen mit MSV3-Lieferant ausgewählt.\n\n" +
                        "Bitte setzen Sie ein Häkchen bei den gewünschten Lieferanten.",
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var gruppen = zeilenMitMSV3
                    .GroupBy(z => z.KMSV3Lieferant!.Value)
                    .ToList();

                // Detaillierte Bestätigung anfordern mit Positionsliste
                var confirmMsg = new System.Text.StringBuilder();
                confirmMsg.AppendLine($"MSV3-Bestellung an {gruppen.Count} Lieferant(en):\n");
                confirmMsg.AppendLine("═══════════════════════════════════════════════════════════\n");

                decimal gesamtSumme = 0;
                foreach (var gruppe in gruppen)
                {
                    var lieferantName = gruppe.First().LieferantName ?? $"#{gruppe.Key}";
                    confirmMsg.AppendLine($"► {lieferantName} ({gruppe.Count()} Positionen):");
                    confirmMsg.AppendLine("───────────────────────────────────────────────────────────");

                    decimal lieferantSumme = 0;
                    foreach (var zeile in gruppe)
                    {
                        var posWert = zeile.FMenge * zeile.EKNetto;
                        lieferantSumme += posWert;
                        var wunschMHD = zeile.WunschMHD?.ToString("dd.MM.yy") ?? "";
                        confirmMsg.AppendLine($"  {zeile.CPZN,-10} {zeile.ArtikelName?.Truncate(25),-25} {zeile.FMenge,5:N0} x {zeile.EKNetto,8:N2} € = {posWert,10:N2} €{(wunschMHD != "" ? $"  MHD: {wunschMHD}" : "")}");
                    }
                    confirmMsg.AppendLine($"                                              Summe: {lieferantSumme,10:N2} €");
                    confirmMsg.AppendLine();
                    gesamtSumme += lieferantSumme;
                }

                confirmMsg.AppendLine("═══════════════════════════════════════════════════════════");
                confirmMsg.AppendLine($"                                       GESAMTSUMME: {gesamtSumme,10:N2} €");
                confirmMsg.AppendLine("\nBestellung jetzt an die Lieferanten übermitteln?");

                if (MessageBox.Show(confirmMsg.ToString(), "MSV3 Bestellung bestätigen",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                    return;

                var zusammenfassung = new System.Text.StringBuilder();
                zusammenfassung.AppendLine($"MSV3-Bestellung an {gruppen.Count} Lieferant(en):\n");

                int erfolgreich = 0, fehlgeschlagen = 0;

                foreach (var gruppe in gruppen)
                {
                    var kMSV3Lieferant = gruppe.Key;
                    var msv3Config = await _msv3Service.GetMSV3LieferantByIdAsync(kMSV3Lieferant);

                    if (msv3Config == null)
                    {
                        zusammenfassung.AppendLine($"⚠ Lieferant #{kMSV3Lieferant}: Keine MSV3-Konfiguration");
                        fehlgeschlagen++;
                        continue;
                    }

                    var lieferantName = gruppe.First().LieferantName ?? msv3Config.LieferantName ?? $"#{kMSV3Lieferant}";

                    // Bestellpositionen für diesen Lieferanten
                    var bestellpositionen = gruppe.Select(z => new MSV3BestellPosition
                    {
                        PZN = z.CPZN!,
                        Menge = (int)z.FMenge,
                        LieferantenArtNr = z.LieferantenArtNr,
                        MinMHD = z.WunschMHD,
                        Freitext = z.CPositionsText
                    }).ToList();

                    // GEHE/Alliance Erkennung
                    bool isGehe = msv3Config.CMSV3Url?.Contains("gehe", StringComparison.OrdinalIgnoreCase) == true ||
                                  msv3Config.CMSV3Url?.Contains("alliance", StringComparison.OrdinalIgnoreCase) == true;

                    string? requestInfo = $"Positionen: {string.Join(", ", bestellpositionen.Select(p => $"{p.PZN} x{p.Menge}"))}";

                    if (isGehe)
                    {
                        // GEHE: bestellen mit Verfügbarkeitsanzeige
                        var geheResult = await _msv3Service.CheckVerfuegbarkeitViaBestellenAsync(msv3Config, bestellpositionen);

                        // Logging
                        await _msv3Service.LogMSV3RequestAsync(
                            kMSV3Lieferant: kMSV3Lieferant,
                            aktion: "Bestellen",
                            requestXml: requestInfo,
                            responseXml: geheResult.ResponseXml,
                            httpStatus: geheResult.Success ? 200 : 500,
                            erfolg: geheResult.Success,
                            fehler: geheResult.Fehler,
                            bestellSupportId: geheResult.BestellSupportId,
                            kLieferantenBestellung: bestellung.KLieferantenBestellung);

                        if (!geheResult.Success)
                        {
                            zusammenfassung.AppendLine($"✗ {lieferantName}: {geheResult.Fehler}");
                            fehlgeschlagen++;
                            continue;
                        }

                        // Ergebnisse in Zeilen übernehmen
                        foreach (var zeile in gruppe)
                        {
                            var v = geheResult.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                            if (v != null)
                            {
                                zeile.MSV3Bestand = v.VerfuegbareMenge;
                                zeile.MSV3Verfuegbar = v.VerfuegbareMenge > 0;
                                zeile.MSV3StatusText = v.StatusCode;
                            }
                        }

                        int geliefert = geheResult.Positionen.Count(p => p.VerfuegbareMenge > 0);
                        zusammenfassung.AppendLine($"✓ {lieferantName}: {bestellpositionen.Count} Pos. (ID: {geheResult.BestellSupportId}, {geliefert} lieferbar)");
                        erfolgreich++;

                        // Popup pro Lieferant anzeigen
                        var lieferantPositionen = gruppe.Select(zeile => {
                            var v = geheResult.Positionen.FirstOrDefault(a => a.PZN == zeile.CPZN);
                            return new MSV3ResponsePosition
                            {
                                PZN = zeile.CPZN,
                                ArtikelName = zeile.ArtikelName,
                                Menge = (int)zeile.FMenge,
                                VerfuegbareMenge = v?.VerfuegbareMenge ?? 0,
                                StatusCode = v?.StatusCode ?? "UNBEKANNT",
                                MHDText = v?.NaechsterLieferzeitpunkt?.ToString("dd.MM.yyyy") ?? "",
                                LieferantName = lieferantName
                            };
                        }).ToList();

                        var dlg = new MSV3ResponseDialog();
                        dlg.Owner = Window.GetWindow(this);
                        dlg.SetErgebnis(
                            $"MSV3 Bestellung: {lieferantName}",
                            $"ID: {geheResult.BestellSupportId} - {geliefert} von {bestellpositionen.Count} lieferbar",
                            lieferantPositionen,
                            geheResult.ResponseXml
                        );
                        dlg.ShowDialog();
                    }
                    else
                    {
                        // Andere Großhändler: Standard SendBestellungAsync
                        var result = await _msv3Service.SendBestellungAsync(msv3Config, bestellung.CEigeneBestellnummer ?? "", bestellpositionen);

                        // Logging
                        await _msv3Service.LogMSV3RequestAsync(
                            kMSV3Lieferant: kMSV3Lieferant,
                            aktion: "Bestellen",
                            requestXml: requestInfo,
                            responseXml: result.ResponseXml,
                            httpStatus: result.Success ? 200 : 500,
                            erfolg: result.Success,
                            fehler: result.Fehlermeldung,
                            msv3AuftragsId: result.MSV3Bestellnummer,
                            kLieferantenBestellung: bestellung.KLieferantenBestellung);

                        // Response-Positionen fuer Popup sammeln
                        var lieferantPosList = gruppe.Select(zeile => new MSV3ResponsePosition
                        {
                            PZN = zeile.CPZN,
                            ArtikelName = zeile.ArtikelName,
                            Menge = (int)zeile.FMenge,
                            VerfuegbareMenge = result.Success ? (int)zeile.FMenge : 0,
                            StatusCode = result.Success ? "BESTELLT" : "FEHLER",
                            MHDText = zeile.WunschMHD?.ToString("dd.MM.yyyy") ?? "",
                            LieferantName = lieferantName
                        }).ToList();

                        if (result.Success)
                        {
                            zusammenfassung.AppendLine($"✓ {lieferantName}: {bestellpositionen.Count} Pos. (Nr: {result.MSV3Bestellnummer})");
                            erfolgreich++;
                        }
                        else
                        {
                            zusammenfassung.AppendLine($"✗ {lieferantName}: {result.Fehlermeldung}");
                            fehlgeschlagen++;
                        }

                        // Popup pro Lieferant anzeigen
                        var dlg2 = new MSV3ResponseDialog();
                        dlg2.Owner = Window.GetWindow(this);
                        dlg2.SetErgebnis(
                            $"MSV3 Bestellung: {lieferantName}",
                            result.Success
                                ? $"Bestellnummer: {result.MSV3Bestellnummer} - {bestellpositionen.Count} Positionen bestellt"
                                : $"FEHLER: {result.Fehlermeldung}",
                            lieferantPosList,
                            result.ResponseXml
                        );
                        dlg2.ShowDialog();
                    }
                }

                dgBestellPositionen.ItemsSource = null;
                dgBestellPositionen.ItemsSource = _currentZeilen;

                // Zusammenfassung am Ende
                MessageBox.Show(zusammenfassung.ToString() + $"\n\nErfolgreich: {erfolgreich}\nFehlgeschlagen: {fehlgeschlagen}",
                    "MSV3 Bestellung abgeschlossen", MessageBoxButton.OK,
                    fehlgeschlagen > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                await LadeBestellungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnMSV3Senden.IsEnabled = true;
                btnMSV3Senden.Content = "MSV3 Bestellen";
            }
        }

        #endregion

        #region ABdata

        private void ABdataDateiWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV/TXT|*.csv;*.txt|Alle|*.*",
                Title = "ABdata-Datei"
            };
            if (dialog.ShowDialog() == true)
                txtABdataDatei.Text = dialog.FileName;
        }

        private async void ABdataImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtABdataDatei.Text)) return;

            try
            {
                var result = await _abdataService.ImportArtikelstammAsync(txtABdataDatei.Text);
                MessageBox.Show($"Import: {result.AnzahlGesamt} Gesamt, {result.AnzahlNeu} Neu, {result.AnzahlAktualisiert} Aktualisiert",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ABdataSuchen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtABdataSuche.Text)) return;

            try
            {
                var ergebnis = await _abdataService.SucheArtikelAsync(txtABdataSuche.Text);
                dgABdataArtikel.ItemsSource = ergebnis;
                btnABdataKopieren.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ABdataArtikel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnABdataKopieren.IsEnabled = dgABdataArtikel.SelectedItem != null;
        }

        private void ABdataKopieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgABdataArtikel.SelectedItem is not ABdataArtikel artikel) return;
            MessageBox.Show($"Artikel {artikel.PZN} - {artikel.Name}\n\nKopieren nach JTL wird noch implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region MSV3 Log

        private async void LogAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnLogAktualisieren.IsEnabled = false;
                btnLogAktualisieren.Content = "Laden...";

                var logs = await _msv3Service.GetMSV3LogAsync(maxEintraege: 500);
                dgMSV3Log.ItemsSource = logs;

                var anzahl = logs.Count();
                txtLogAnzahl.Text = $"{anzahl} Einträge";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden des Logs: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLogAktualisieren.IsEnabled = true;
                btnLogAktualisieren.Content = "Aktualisieren";
            }
        }

        private async void LogLeeren_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var anzahl = await _msv3Service.GetMSV3LogCountAsync();

                if (anzahl == 0)
                {
                    MessageBox.Show("Das Log ist bereits leer.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var antwort = MessageBox.Show(
                    $"Möchten Sie wirklich {anzahl} Log-Einträge löschen?\n\nDieser Vorgang kann nicht rückgängig gemacht werden.",
                    "Log leeren",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (antwort != MessageBoxResult.Yes)
                    return;

                btnLogLeeren.IsEnabled = false;
                btnLogLeeren.Content = "Lösche...";

                var geloescht = await _msv3Service.ClearMSV3LogAsync();

                dgMSV3Log.ItemsSource = null;
                txtLogAnzahl.Text = "0 Einträge";

                MessageBox.Show($"{geloescht} Log-Einträge wurden gelöscht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Leeren des Logs: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnLogLeeren.IsEnabled = true;
                btnLogLeeren.Content = "Log leeren";
            }
        }

        private void MSV3Log_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMSV3Log.SelectedItem is MSV3LogEintrag log)
            {
                txtLogRequest.Text = FormatXml(log.CRequestXml) ?? "(kein Request)";
                txtLogResponse.Text = FormatXml(log.CResponseXml) ?? "(keine Response)";
                txtLogDetailHeader.Text = $"📋 {log.CAktion} - {log.ZeitpunktText} - {log.LieferantName}";
            }
            else
            {
                txtLogRequest.Text = "";
                txtLogResponse.Text = "";
                txtLogDetailHeader.Text = "Request / Response XML";
            }
        }

        /// <summary>
        /// Formatiert XML für bessere Lesbarkeit (einfache Einrückung)
        /// </summary>
        private static string? FormatXml(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;

            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                return doc.ToString();
            }
            catch
            {
                // Falls kein valides XML, Original zurückgeben
                return xml;
            }
        }

        #endregion

        #region Eigene Felder (NOVVIA)

        private async Task LadeLieferantEigeneFelderAsync(int kLieferant)
        {
            try
            {
                // Alle verfügbaren Attribute laden
                var attribute = (await _coreService.GetLieferantAttributeAsync()).ToList();

                if (!attribute.Any())
                {
                    txtLiefEFHinweis.Text = "Keine Attribute definiert (Einstellungen → Eigene Felder → Lieferant)";
                    _lieferantEigeneFelder.Clear();
                    dgLieferantEigeneFelder.ItemsSource = null;
                    return;
                }

                txtLiefEFHinweis.Text = "";

                // Bestehende Werte für diesen Lieferanten laden
                var werte = (await _coreService.GetLieferantEigeneFelderAsync(kLieferant)).ToList();

                // ViewModel erstellen: Für jedes Attribut ein Eintrag
                _lieferantEigeneFelder = attribute.Select(attr =>
                {
                    var wert = werte.FirstOrDefault(w => w.KAttribut == attr.KAttribut);
                    return new LieferantEigenesFeldViewModel
                    {
                        KAttribut = attr.KAttribut,
                        KLieferant = kLieferant,
                        CAttributName = attr.CName,
                        NFeldTyp = attr.NFeldTyp,
                        CWertVarchar = wert?.CWertVarchar,
                        NWertInt = wert?.NWertInt,
                        FWertDecimal = wert?.FWertDecimal,
                        DWertDateTime = wert?.DWertDateTime
                    };
                }).ToList();

                dgLieferantEigeneFelder.ItemsSource = _lieferantEigeneFelder;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Eigenen Felder: {ex.Message}");
                txtLiefEFHinweis.Text = "Fehler beim Laden - bitte Setup-Script ausführen";
            }
        }

        private async void LieferantEigeneFelderSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLieferant == null || !_lieferantEigeneFelder.Any())
            {
                MessageBox.Show("Kein Lieferant ausgewählt.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                foreach (var feld in _lieferantEigeneFelder)
                {
                    // Wert aus WertBearbeitung parsen basierend auf Feldtyp
                    object? wert = feld.NFeldTyp switch
                    {
                        1 => int.TryParse(feld.WertBearbeitung, out var i) ? i : (int?)null,
                        2 => decimal.TryParse(feld.WertBearbeitung, out var d) ? d : (decimal?)null,
                        3 => feld.WertBearbeitung,
                        4 => DateTime.TryParse(feld.WertBearbeitung, out var dt) ? dt : (DateTime?)null,
                        _ => feld.WertBearbeitung
                    };

                    await _coreService.SaveLieferantEigenesFeldAsync(feld.KLieferant, feld.KAttribut, wert);
                }

                MessageBox.Show("Eigene Felder gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                // Neu laden
                await LadeLieferantEigeneFelderAsync(_selectedLieferant.KLieferant);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Parst MinMHD aus dem Hinweis-Feld.
        /// Unterstützt Formate: "MHD: 2025-06", "MHD 06/2025", "MHD: 2025-06-30", "mind. 06/25"
        /// </summary>
        private static DateTime? ParseMinMHDFromHinweis(string? hinweis)
        {
            if (string.IsNullOrWhiteSpace(hinweis)) return null;

            // Muster: MHD: 2025-06 oder MHD: 2025-06-30
            var match = Regex.Match(hinweis, @"MHD[:\s]*(\d{4})-(\d{2})(?:-(\d{2}))?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int year = int.Parse(match.Groups[1].Value);
                int month = int.Parse(match.Groups[2].Value);
                int day = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 1;
                return new DateTime(year, month, day);
            }

            // Muster: MHD 06/2025 oder MHD 06/25 oder mind. 06/25
            match = Regex.Match(hinweis, @"(?:MHD|mind\.?)[:\s]*(\d{1,2})/(\d{2,4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int month = int.Parse(match.Groups[1].Value);
                int year = int.Parse(match.Groups[2].Value);
                if (year < 100) year += 2000;
                return new DateTime(year, month, 1);
            }

            return null;
        }

        #endregion
    }

    internal static class StringExtensions
    {
        public static string Truncate(this string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 2) + "..";
        }
    }

    /// <summary>
    /// ViewModel für Lieferant Eigene Felder in der Detailansicht
    /// </summary>
    public class LieferantEigenesFeldViewModel
    {
        public int KAttribut { get; set; }
        public int KLieferant { get; set; }
        public string CAttributName { get; set; } = "";
        public int NFeldTyp { get; set; }
        public string? CWertVarchar { get; set; }
        public int? NWertInt { get; set; }
        public decimal? FWertDecimal { get; set; }
        public DateTime? DWertDateTime { get; set; }

        public string FeldTypName => NFeldTyp switch
        {
            1 => "Ganzzahl",
            2 => "Dezimal",
            3 => "Text",
            4 => "Datum",
            _ => "Text"
        };

        public string WertAnzeige => NFeldTyp switch
        {
            1 => NWertInt?.ToString() ?? "",
            2 => FWertDecimal?.ToString("N2") ?? "",
            3 => CWertVarchar ?? "",
            4 => DWertDateTime?.ToString("dd.MM.yyyy") ?? "",
            _ => CWertVarchar ?? ""
        };

        public string WertBearbeitung
        {
            get => WertAnzeige;
            set
            {
                // Wert wird beim Speichern geparst
                switch (NFeldTyp)
                {
                    case 1:
                        if (int.TryParse(value, out var i)) NWertInt = i;
                        break;
                    case 2:
                        if (decimal.TryParse(value, out var d)) FWertDecimal = d;
                        break;
                    case 3:
                        CWertVarchar = value;
                        break;
                    case 4:
                        if (DateTime.TryParse(value, out var dt)) DWertDateTime = dt;
                        break;
                    default:
                        CWertVarchar = value;
                        break;
                }
            }
        }
    }
}
