using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class LieferantenPage : Page
    {
        private readonly EinkaufService _einkaufService;
        private readonly MSV3Service _msv3Service;
        private readonly ABdataService _abdataService;
        private readonly EigeneFelderService? _eigeneFelderService;

        private List<LieferantUebersicht> _lieferanten = new();
        private List<LieferantenBestellungUebersicht> _bestellungen = new();
        private MSV3Lieferant? _selectedMSV3Lieferant;
        private List<EigenesFeldDefinition> _eigeneFelder = new();
        private Dictionary<int, FrameworkElement> _eigeneFelderControls = new();

        public LieferantenPage()
        {
            InitializeComponent();

            _einkaufService = App.Services.GetRequiredService<EinkaufService>();
            _msv3Service = App.Services.GetRequiredService<MSV3Service>();
            _abdataService = App.Services.GetRequiredService<ABdataService>();
            _eigeneFelderService = App.Services.GetService<EigeneFelderService>();

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await LadeLieferantenAsync();
                await LadeBestellungenAsync();
                await LadeEinkaufslisteAsync();
                await LadeEingangsrechnungenAsync();
                await LadeABdataImportLogAsync();

                // Lieferanten für Combo-Boxen
                var lieferanten = await _einkaufService.GetLieferantenAsync();
                cboEinkaufLieferant.ItemsSource = lieferanten;
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

        private void NeuerLieferant_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lieferanten werden in JTL-Wawi verwaltet.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private LieferantUebersicht? _selectedLieferant;
        private MSV3Lieferant? _selectedLiefMSV3;

        private async void Lieferant_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgLieferanten.SelectedItem is LieferantUebersicht lieferant)
            {
                _selectedLieferant = lieferant;
                pnlLieferantDetail.Visibility = Visibility.Visible;

                // Stammdaten anzeigen
                txtLiefFirma.Text = lieferant.CFirma;
                txtLiefNr.Text = $"Lieferanten-Nr: {lieferant.CLiefNr}";
                txtLiefAdresse.Text = $"{lieferant.CStrasse}\n{lieferant.CPLZ} {lieferant.COrt}";
                txtLiefTel.Text = $"Tel: {lieferant.CTel}";
                txtLiefEmail.Text = $"E-Mail: {lieferant.CEmail}";

                // MSV3 Konfiguration laden
                await LadeLieferantMSV3ConfigAsync(lieferant.KLieferant);

                // Eigene Felder laden
                await LadeEigeneFelderAsync(lieferant.KLieferant);
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
                chkLiefMSV3Aktiv.IsChecked = _selectedLiefMSV3.NAktiv;
                // Version setzen (0 oder 1 => 1, sonst den Wert)
                var version = _selectedLiefMSV3.NMSV3Version <= 0 ? 2 : _selectedLiefMSV3.NMSV3Version;
                cboLiefMSV3Version.SelectedIndex = version == 1 ? 0 : 1;
            }
            else
            {
                // Neue MSV3-Konfiguration
                _selectedLiefMSV3 = new MSV3Lieferant { KLieferant = kLieferant };
                txtLiefMSV3Url.Text = "";
                txtLiefMSV3Benutzer.Text = "";
                txtLiefMSV3Passwort.Password = "";
                txtLiefMSV3Kundennr.Text = "";
                txtLiefMSV3Filiale.Text = "001";
                chkLiefMSV3Aktiv.IsChecked = false;
                cboLiefMSV3Version.SelectedIndex = 1; // Default: Version 2
            }
        }

        private async void LieferantMSV3Test_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLiefMSV3 == null || _selectedLieferant == null) return;

            UpdateLiefMSV3Config();

            try
            {
                // Zeige was wir senden
                MessageBox.Show($"Teste MSV3-Verbindung...\n\nURL: {_selectedLiefMSV3.CMSV3Url}\nBenutzer: {_selectedLiefMSV3.CBenutzer}\nVersion: {_selectedLiefMSV3.NMSV3Version}", "MSV3 Test", MessageBoxButton.OK, MessageBoxImage.Information);

                // Verwende VerbindungTesten - den offiziellen MSV3 Test-Endpoint
                var result = await _msv3Service.VerbindungTestenAsync(_selectedLiefMSV3);

                // Zeige IMMER die volle Response (für Debugging)
                var responseInfo = !string.IsNullOrEmpty(result.ResponseXml)
                    ? result.ResponseXml.Length > 1500
                        ? result.ResponseXml.Substring(0, 1500) + "..."
                        : result.ResponseXml
                    : "(keine Response)";

                if (result.Success)
                {
                    MessageBox.Show($"MSV3-Verbindung ERFOLGREICH!\n\n{result.Meldung}\n\n--- Response ---\n{responseInfo}", "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"MSV3-Verbindung FEHLGESCHLAGEN!\n\nFehler: {result.Fehler}\n\n--- Response ---\n{responseInfo}", "Test Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verbindungsfehler:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}", "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
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
                await LadeLieferantenAsync(); // Refresh um MSV3-Status zu aktualisieren
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

            // Version aus ComboBox lesen (Default: 2 für GEHE)
            if (cboLiefMSV3Version.SelectedItem is ComboBoxItem versionItem && versionItem.Tag != null)
                _selectedLiefMSV3.NMSV3Version = int.Parse(versionItem.Tag.ToString()!);
            else
                _selectedLiefMSV3.NMSV3Version = 2; // Default Version 2
        }

        private void Lieferant_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Doppelklick öffnet Detail-Ansicht (schon durch SelectionChanged sichtbar)
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
                // Prüfen ob Lieferant MSV3 hat
                var lieferant = _lieferanten.FirstOrDefault(l => l.KLieferant == bestellung.KLieferant);
                var hatMSV3 = lieferant?.NHatMSV3 ?? false;

                btnMSV3Bestand.IsEnabled = hatMSV3;
                btnMSV3Senden.IsEnabled = hatMSV3 && bestellung.NStatus == 1;

                // Positionen laden
                _currentPositionen = (await _einkaufService.GetBestellungPositionenAsync(bestellung.KLieferantenBestellung)).ToList();
                dgBestellPositionen.ItemsSource = _currentPositionen;

                txtMSV3Status.Text = hatMSV3 ? "" : "(Lieferant hat kein MSV3)";
            }
            else
            {
                btnMSV3Bestand.IsEnabled = false;
                btnMSV3Senden.IsEnabled = false;
                _currentPositionen.Clear();
                dgBestellPositionen.ItemsSource = null;
                txtMSV3Status.Text = "";
            }
        }

        private async void MSV3BestandAbfragen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung)
                return;

            if (!_currentPositionen.Any())
            {
                MessageBox.Show("Keine Positionen in der Bestellung.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // MSV3-Konfiguration laden
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(bestellung.KLieferant);
                if (msv3Config == null)
                {
                    MessageBox.Show("Für diesen Lieferanten ist keine MSV3-Konfiguration hinterlegt.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtMSV3Status.Text = "MSV3-Abfrage läuft...";
                btnMSV3Bestand.IsEnabled = false;

                // PZNs sammeln (nur Positionen mit PZN)
                var pzns = _currentPositionen
                    .Where(p => !string.IsNullOrEmpty(p.CPZN))
                    .Select(p => p.CPZN!)
                    .Distinct()
                    .ToList();

                if (!pzns.Any())
                {
                    MessageBox.Show("Keine Positionen mit PZN vorhanden.\nMSV3 benötigt PZN für die Verfügbarkeitsabfrage.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtMSV3Status.Text = "";
                    btnMSV3Bestand.IsEnabled = true;
                    return;
                }

                // MSV3-Verfügbarkeitsabfrage
                var result = await _msv3Service.CheckVerfuegbarkeitAsync(msv3Config, pzns);

                // Ergebnisse auf Positionen mappen
                int verfuegbar = 0, nichtVerfuegbar = 0;
                foreach (var pos in _currentPositionen)
                {
                    if (string.IsNullOrEmpty(pos.CPZN))
                    {
                        pos.MSV3StatusText = "Keine PZN";
                        continue;
                    }

                    var verfuegbarkeit = result.Positionen.FirstOrDefault(a => a.PZN == pos.CPZN);
                    if (verfuegbarkeit != null)
                    {
                        pos.MSV3Bestand = verfuegbarkeit.Bestand;
                        pos.MSV3Verfuegbar = verfuegbarkeit.Verfuegbar;
                        pos.MSV3StatusText = verfuegbarkeit.Status;
                        pos.MSV3Lieferzeit = verfuegbarkeit.Lieferzeit;
                        pos.MSV3MHD = verfuegbarkeit.MHD;
                        pos.MSV3ChargenNr = verfuegbarkeit.ChargenNr;

                        if (verfuegbarkeit.Verfuegbar)
                            verfuegbar++;
                        else
                            nichtVerfuegbar++;
                    }
                    else
                    {
                        pos.MSV3StatusText = "Nicht gefunden";
                        pos.MSV3Verfuegbar = false;
                        nichtVerfuegbar++;
                    }
                }

                // Grid aktualisieren
                dgBestellPositionen.ItemsSource = null;
                dgBestellPositionen.ItemsSource = _currentPositionen;

                txtMSV3Status.Text = $"MSV3: {verfuegbar} verfügbar, {nichtVerfuegbar} nicht verfügbar";
                btnMSV3Bestand.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MSV3-Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtMSV3Status.Text = "MSV3-Abfrage fehlgeschlagen";
                btnMSV3Bestand.IsEnabled = true;
            }
        }

        private void GlobalMinMHD_Click(object sender, RoutedEventArgs e)
        {
            if (dpGlobalMinMHD.SelectedDate == null)
            {
                MessageBox.Show("Bitte wählen Sie ein Datum aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_currentPositionen.Any())
            {
                MessageBox.Show("Keine Positionen vorhanden.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // MinMHD in alle Positionen übernehmen
            foreach (var pos in _currentPositionen)
            {
                pos.MinMHD = dpGlobalMinMHD.SelectedDate;
            }

            // Grid aktualisieren
            dgBestellPositionen.ItemsSource = null;
            dgBestellPositionen.ItemsSource = _currentPositionen;

            MessageBox.Show($"Min.MHD {dpGlobalMinMHD.SelectedDate:dd.MM.yyyy} für {_currentPositionen.Count} Positionen gesetzt.",
                "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void NeueBestellung_Click(object sender, RoutedEventArgs e)
        {
            if (dgLieferanten.SelectedItem is not LieferantUebersicht lieferant)
            {
                MessageBox.Show("Bitte wählen Sie zuerst einen Lieferanten aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var kBestellung = await _einkaufService.CreateBestellungAsync(lieferant.KLieferant, App.BenutzerId);
                await LadeBestellungenAsync();
                MessageBox.Show($"Bestellung {kBestellung} wurde erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MSV3Senden_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is not LieferantenBestellungUebersicht bestellung)
                return;

            try
            {
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(bestellung.KLieferant);
                if (msv3Config == null)
                {
                    MessageBox.Show("Für diesen Lieferanten ist keine MSV3-Konfiguration hinterlegt.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Positionen mit MinMHD aus _currentPositionen verwenden
                var bestellpositionen = _currentPositionen.Select(p => new MSV3BestellPosition
                {
                    PZN = p.CPZN ?? "",
                    Menge = (int)p.FMenge,
                    LieferantenArtNr = p.CLieferantenArtNr,
                    MinMHD = p.MinMHD  // MinMHD aus UI übernehmen
                }).ToList();

                if (!bestellpositionen.Any(p => !string.IsNullOrEmpty(p.PZN)))
                {
                    MessageBox.Show("Keine Positionen mit PZN vorhanden.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Hinweis wenn MinMHD gesetzt
                var mitMinMHD = bestellpositionen.Count(p => p.MinMHD.HasValue);
                if (mitMinMHD > 0)
                {
                    var confirm = MessageBox.Show(
                        $"Bestellung mit {bestellpositionen.Count} Position(en) senden?\n\n{mitMinMHD} Position(en) haben ein Mindest-MHD.\n\nHinweis: MinMHD wird nicht von allen Großhändlern unterstützt.",
                        "MSV3 Bestellung",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                var result = await _msv3Service.SendBestellungAsync(msv3Config, bestellung.CEigeneBestellnummer ?? "", bestellpositionen);

                if (result.Success)
                {
                    MessageBox.Show($"MSV3-Bestellung gesendet!\nMSV3-Bestellnummer: {result.MSV3Bestellnummer}", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LadeBestellungenAsync();
                }
                else
                {
                    MessageBox.Show($"MSV3-Fehler: {result.Fehlermeldung}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Einkaufsliste

        private async Task LadeEinkaufslisteAsync(int? kLieferant = null)
        {
            var liste = await _einkaufService.GetEinkaufslisteAsync(kLieferant);
            dgEinkaufsliste.ItemsSource = liste;
        }

        private async void EinkaufLieferant_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            int? kLieferant = cboEinkaufLieferant.SelectedValue as int?;
            await LadeEinkaufslisteAsync(kLieferant);
        }

        private async void EinkaufslisteLaden_Click(object sender, RoutedEventArgs e)
        {
            int? kLieferant = cboEinkaufLieferant.SelectedValue as int?;
            await LadeEinkaufslisteAsync(kLieferant);
        }

        private void AlleAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            if (dgEinkaufsliste.ItemsSource is IEnumerable<EinkaufslisteItem> items)
            {
                foreach (var item in items)
                    item.IsSelected = true;
                dgEinkaufsliste.Items.Refresh();
            }
        }

        private async void MSV3Verfuegbarkeit_Click(object sender, RoutedEventArgs e)
        {
            if (dgEinkaufsliste.ItemsSource is not IEnumerable<EinkaufslisteItem> items)
                return;

            var selectedItems = items.Where(i => i.IsSelected && !string.IsNullOrEmpty(i.CPZN)).ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Bitte wählen Sie Artikel mit PZN aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Gruppieren nach Lieferant
            var perLieferant = selectedItems.GroupBy(i => i.KLieferant);

            foreach (var gruppe in perLieferant)
            {
                var msv3Config = await _msv3Service.GetMSV3LieferantAsync(gruppe.Key);
                if (msv3Config == null) continue;

                var pzns = gruppe.Select(i => i.CPZN!).Distinct().ToList();
                var result = await _msv3Service.CheckVerfuegbarkeitAsync(msv3Config, pzns);

                // Status aktualisieren (vereinfacht)
                MessageBox.Show($"MSV3 Verfügbarkeit geprüft für {gruppe.Count()} Artikel", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await LadeEinkaufslisteAsync();
        }

        private async void AusListeBestellen_Click(object sender, RoutedEventArgs e)
        {
            if (dgEinkaufsliste.ItemsSource is not IEnumerable<EinkaufslisteItem> items)
                return;

            var selectedItems = items.Where(i => i.IsSelected).ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Bitte wählen Sie Artikel aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Gruppieren nach Lieferant
            var perLieferant = selectedItems.GroupBy(i => i.KLieferant);
            int anzahlBestellungen = 0;

            foreach (var gruppe in perLieferant)
            {
                if (gruppe.Key == 0) continue;

                var lieferant = _lieferanten.FirstOrDefault(l => l.KLieferant == gruppe.Key);
                var kBestellung = await _einkaufService.CreateBestellungAsync(gruppe.Key, App.BenutzerId);

                foreach (var item in gruppe)
                {
                    await _einkaufService.AddPositionAsync(kBestellung, item.KArtikel, item.FAnzahl, item.FEKNettoLieferant ?? 0, item.CLieferantenArtNr);
                }

                anzahlBestellungen++;
            }

            MessageBox.Show($"{anzahlBestellungen} Bestellung(en) erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            await LadeBestellungenAsync();
        }

        #endregion

        #region Eingangsrechnungen

        private async Task LadeEingangsrechnungenAsync(bool? geprueft = null, bool? freigegeben = null)
        {
            var rechnungen = await _einkaufService.GetEingangsrechnungenAsync(geprueft, freigegeben);
            dgEingangsrechnungen.ItemsSource = rechnungen;
        }

        private async void ERStatus_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            bool? geprueft = null, freigegeben = null;

            switch (cboERStatus.SelectedIndex)
            {
                case 1: geprueft = false; break;    // Offen
                case 2: geprueft = true; break;     // Geprüft
                case 3: freigegeben = true; break;  // Freigegeben
            }

            await LadeEingangsrechnungenAsync(geprueft, freigegeben);
        }

        private async void EingangsrechnungenLaden_Click(object sender, RoutedEventArgs e)
        {
            await LadeEingangsrechnungenAsync();
        }

        private void ER_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgEingangsrechnungen.SelectedItem is EingangsrechnungUebersicht er)
            {
                btnERPruefen.IsEnabled = !er.NGeprueft;
                btnERFreigeben.IsEnabled = er.NGeprueft && !er.NFreigegeben;
            }
            else
            {
                btnERPruefen.IsEnabled = false;
                btnERFreigeben.IsEnabled = false;
            }
        }

        private async void ERPruefen_Click(object sender, RoutedEventArgs e)
        {
            if (dgEingangsrechnungen.SelectedItem is not EingangsrechnungUebersicht er)
                return;

            try
            {
                await _einkaufService.PruefenUndFreigebenAsync(er.KEingangsrechnung, pruefen: true, freigeben: false, App.BenutzerId);
                MessageBox.Show("Rechnung wurde geprüft.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeEingangsrechnungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ERFreigeben_Click(object sender, RoutedEventArgs e)
        {
            if (dgEingangsrechnungen.SelectedItem is not EingangsrechnungUebersicht er)
                return;

            try
            {
                await _einkaufService.PruefenUndFreigebenAsync(er.KEingangsrechnung, pruefen: true, freigeben: true, App.BenutzerId);
                MessageBox.Show("Rechnung wurde freigegeben.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeEingangsrechnungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region ABdata

        private async Task LadeABdataImportLogAsync()
        {
            var logs = await _abdataService.GetImportHistorieAsync();
            dgABdataImportLog.ItemsSource = logs;
        }

        private void ABdataDateiWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV/TXT Dateien|*.csv;*.txt|Alle Dateien|*.*",
                Title = "ABdata-Datei auswählen"
            };

            if (dialog.ShowDialog() == true)
            {
                txtABdataDatei.Text = dialog.FileName;
            }
        }

        private async void ABdataImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtABdataDatei.Text))
            {
                MessageBox.Show("Bitte wählen Sie eine Datei aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var result = await _abdataService.ImportArtikelstammAsync(txtABdataDatei.Text);

                if (result.Success)
                {
                    MessageBox.Show($"Import abgeschlossen!\n\nGesamt: {result.AnzahlGesamt}\nNeu: {result.AnzahlNeu}\nAktualisiert: {result.AnzahlAktualisiert}",
                        "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Import mit Fehlern!\n\n{string.Join("\n", result.Fehler.Take(10))}",
                        "Warnung", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                await LadeABdataImportLogAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ABdataAutoMapping_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var anzahl = await _abdataService.AutoMappingAsync();
                MessageBox.Show($"{anzahl} Artikel wurden automatisch zugeordnet.", "Auto-Mapping", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ABdataSuchen_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtABdataSuche.Text))
                return;

            try
            {
                var ergebnis = await _abdataService.SucheArtikelAsync(txtABdataSuche.Text);
                dgABdataArtikel.ItemsSource = ergebnis;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Eigene Felder

        private async Task LadeEigeneFelderAsync(int lieferantId)
        {
            if (_eigeneFelderService == null) return;

            try
            {
                _eigeneFelder = (await _eigeneFelderService.GetFelderAsync("Lieferant")).ToList();
                pnlEigeneFelderControls.Children.Clear();
                _eigeneFelderControls.Clear();

                if (_eigeneFelder.Count == 0)
                {
                    txtKeineEigenenFelder.Visibility = Visibility.Visible;
                    return;
                }

                txtKeineEigenenFelder.Visibility = Visibility.Collapsed;

                var werte = await _eigeneFelderService.GetWerteAsync("Lieferant", lieferantId);

                foreach (var feld in _eigeneFelder)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };

                    var label = new TextBlock
                    {
                        Text = feld.Name + ":",
                        Width = 80,
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    row.Children.Add(label);

                    var wert = werte?.TryGetValue(feld.InternerName ?? feld.Name, out var v) == true ? v : feld.Standardwert;
                    var control = BuildEigenesFeldControl(feld, wert);
                    row.Children.Add(control);
                    _eigeneFelderControls[feld.Id] = control;

                    pnlEigeneFelderControls.Children.Add(row);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler Eigene Felder: {ex.Message}");
            }
        }

        private FrameworkElement BuildEigenesFeldControl(EigenesFeldDefinition feld, string? wert)
        {
            switch (feld.Typ)
            {
                case EigenesFeldTyp.Bool:
                    return new CheckBox { IsChecked = wert == "1" || wert?.ToLower() == "true", VerticalAlignment = VerticalAlignment.Center };
                case EigenesFeldTyp.Date:
                    var dp = new DatePicker { Width = 100 };
                    if (DateTime.TryParse(wert, out var d)) dp.SelectedDate = d;
                    return dp;
                case EigenesFeldTyp.Select:
                    var cmb = new ComboBox { Width = 120, Height = 24 };
                    if (!string.IsNullOrEmpty(feld.AuswahlWerte))
                        foreach (var opt in feld.AuswahlWerte.Split('|'))
                            cmb.Items.Add(opt);
                    cmb.SelectedItem = wert;
                    return cmb;
                default:
                    return new TextBox { Text = wert ?? "", Width = 150, Height = 22, FontSize = 11 };
            }
        }

        private string? GetEigenesFeldWert(FrameworkElement control, EigenesFeldTyp typ)
        {
            return typ switch
            {
                EigenesFeldTyp.Bool => (control as CheckBox)?.IsChecked == true ? "1" : "0",
                EigenesFeldTyp.Date => (control as DatePicker)?.SelectedDate?.ToString("yyyy-MM-dd"),
                EigenesFeldTyp.Select => (control as ComboBox)?.SelectedItem?.ToString(),
                _ => (control as TextBox)?.Text
            };
        }

        private async void EigeneFelderSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_eigeneFelderService == null || _selectedLieferant == null) return;

            try
            {
                foreach (var feld in _eigeneFelder)
                {
                    if (_eigeneFelderControls.TryGetValue(feld.Id, out var control))
                    {
                        var wert = GetEigenesFeldWert(control, feld.Typ);
                        await _eigeneFelderService.SetWertAsync(feld.Id, _selectedLieferant.KLieferant, wert);
                    }
                }
                MessageBox.Show("Eigene Felder gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
