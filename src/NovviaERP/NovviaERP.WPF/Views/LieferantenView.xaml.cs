using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class LieferantenView : UserControl
    {
        private readonly EinkaufService _einkaufService;
        private readonly MSV3Service _msv3Service;
        private readonly ABdataService _abdataService;

        private List<LieferantUebersicht> _lieferanten = new();
        private List<LieferantenBestellungUebersicht> _bestellungen = new();
        private LieferantUebersicht? _selectedLieferant;
        private MSV3Lieferant? _selectedLiefMSV3;

        public LieferantenView()
        {
            InitializeComponent();

            _einkaufService = App.Services.GetRequiredService<EinkaufService>();
            _msv3Service = App.Services.GetRequiredService<MSV3Service>();
            _abdataService = App.Services.GetRequiredService<ABdataService>();

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
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
                txtLiefAdresse.Text = $"{lieferant.CStrasse}\n{lieferant.CPLZ} {lieferant.COrt}";
                txtLiefTel.Text = $"Tel: {lieferant.CTel}";
                txtLiefEmail.Text = $"E-Mail: {lieferant.CEmail}";

                await LadeLieferantMSV3ConfigAsync(lieferant.KLieferant);
            }
            else
            {
                _selectedLieferant = null;
                pnlLieferantDetail.Visibility = Visibility.Collapsed;
            }
        }

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
                // Test mit echter PZN (Aspirin 500mg - bekannte PZN)
                var result = await _msv3Service.CheckVerfuegbarkeitAsync(_selectedLiefMSV3, new List<string> { "04114918" });

                if (result.Success)
                {
                    var pos = result.Positionen.FirstOrDefault();
                    var details = pos != null
                        ? $"PZN: {pos.PZN}\nVerfügbar: {pos.Verfuegbar}\nBestand: {pos.MengeVerfuegbar}\nStatus: {pos.Status}"
                        : "Keine Artikeldaten in Antwort";

                    MessageBox.Show($"MSV3-Verbindung erfolgreich!\n\n{details}\n\nAnzahl Positionen: {result.Positionen.Count}",
                        "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"MSV3-Fehler:\n{result.Fehler}", "Test fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private List<LieferantenBestellungPos> _currentPositionen = new();

        private async void Bestellung_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is LieferantenBestellungUebersicht bestellung)
            {
                var lieferant = _lieferanten.FirstOrDefault(l => l.KLieferant == bestellung.KLieferant);
                var hatMSV3 = lieferant?.NHatMSV3 ?? false;

                btnMSV3Bestand.IsEnabled = hatMSV3;
                btnMSV3Senden.IsEnabled = hatMSV3 && bestellung.NStatus == 1;
                btnBestandAlleAbfragen.IsEnabled = hatMSV3;

                _currentPositionen = (await _einkaufService.GetBestellungPositionenAsync(bestellung.KLieferantenBestellung)).ToList();
                dgBestellPositionen.ItemsSource = _currentPositionen;
            }
            else
            {
                btnMSV3Bestand.IsEnabled = false;
                btnMSV3Senden.IsEnabled = false;
                btnBestandAlleAbfragen.IsEnabled = false;
                _currentPositionen.Clear();
                dgBestellPositionen.ItemsSource = null;
            }
        }

        private async void BestandAlleAbfragen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung) return;
            if (!_currentPositionen.Any()) return;

            try
            {
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(bestellung.KLieferant);
                if (msv3Config == null)
                {
                    MessageBox.Show("Keine MSV3-Konfiguration.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var pzns = _currentPositionen.Where(p => !string.IsNullOrEmpty(p.CPZN)).Select(p => p.CPZN!).Distinct().ToList();
                if (!pzns.Any())
                {
                    MessageBox.Show("Keine PZNs in den Positionen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                btnBestandAlleAbfragen.IsEnabled = false;
                btnBestandAlleAbfragen.Content = "Abfrage läuft...";

                var result = await _msv3Service.CheckVerfuegbarkeitAsync(msv3Config, pzns);

                if (!result.Success)
                {
                    MessageBox.Show($"MSV3-Fehler: {result.Fehler}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var pos in _currentPositionen)
                {
                    if (string.IsNullOrEmpty(pos.CPZN)) continue;
                    var v = result.Positionen.FirstOrDefault(a => a.PZN == pos.CPZN);
                    if (v != null)
                    {
                        pos.MSV3Bestand = v.Bestand;
                        pos.MSV3Verfuegbar = v.Verfuegbar;
                        pos.MSV3StatusText = v.Status;
                        pos.MSV3MHD = v.MHD;
                        pos.MSV3ChargenNr = v.ChargenNr;
                    }
                }

                dgBestellPositionen.ItemsSource = null;
                dgBestellPositionen.ItemsSource = _currentPositionen;

                MessageBox.Show($"Bestand für {pzns.Count} PZNs abgefragt.\n\n" +
                    $"Verfügbar: {result.AnzahlVerfuegbar}\n" +
                    $"Teilweise: {result.AnzahlTeilweise}\n" +
                    $"Nicht verfügbar: {result.AnzahlNichtVerfuegbar}",
                    "Bestandsabfrage", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "MSV3-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnBestandAlleAbfragen.IsEnabled = true;
                btnBestandAlleAbfragen.Content = "Bestand alle abfragen";
            }
        }

        private async void MSV3BestandAbfragen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung) return;
            if (!_currentPositionen.Any()) return;

            try
            {
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(bestellung.KLieferant);
                if (msv3Config == null)
                {
                    MessageBox.Show("Keine MSV3-Konfiguration.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var pzns = _currentPositionen.Where(p => !string.IsNullOrEmpty(p.CPZN)).Select(p => p.CPZN!).Distinct().ToList();
                if (!pzns.Any()) return;

                var result = await _msv3Service.CheckVerfuegbarkeitAsync(msv3Config, pzns);

                foreach (var pos in _currentPositionen)
                {
                    if (string.IsNullOrEmpty(pos.CPZN)) continue;
                    var v = result.Positionen.FirstOrDefault(a => a.PZN == pos.CPZN);
                    if (v != null)
                    {
                        pos.MSV3Bestand = v.Bestand;
                        pos.MSV3Verfuegbar = v.Verfuegbar;
                        pos.MSV3StatusText = v.Status;
                        pos.MSV3MHD = v.MHD;
                    }
                }

                dgBestellPositionen.ItemsSource = null;
                dgBestellPositionen.ItemsSource = _currentPositionen;
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

            try
            {
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(bestellung.KLieferant);
                if (msv3Config == null) return;

                var bestellpositionen = _currentPositionen.Select(p => new MSV3BestellPosition
                {
                    PZN = p.CPZN ?? "",
                    Menge = (int)p.FMenge,
                    LieferantenArtNr = p.CLieferantenArtNr
                }).ToList();

                var result = await _msv3Service.SendBestellungAsync(msv3Config, bestellung.CEigeneBestellnummer ?? "", bestellpositionen);

                if (result.Success)
                    MessageBox.Show($"MSV3-Bestellung gesendet!\nNr: {result.MSV3Bestellnummer}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show($"Fehler: {result.Fehlermeldung}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

                await LadeBestellungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
