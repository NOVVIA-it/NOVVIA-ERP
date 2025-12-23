using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelDetailView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly EinkaufService _einkaufService;
        private readonly MSV3Service _msv3Service;
        private readonly int? _artikelId;
        private CoreService.ArtikelDetail? _artikel;
        private List<CoreService.HerstellerRef> _hersteller = new();
        private List<CoreService.SteuerklasseRef> _steuerklassen = new();
        private List<ArtikelMSV3Lieferant> _msv3Lieferanten = new();

        public ArtikelDetailView(int? artikelId)
        {
            InitializeComponent();
            _artikelId = artikelId;
            _coreService = App.Services.GetRequiredService<CoreService>();
            _einkaufService = App.Services.GetRequiredService<EinkaufService>();
            _msv3Service = App.Services.GetRequiredService<MSV3Service>();
            txtTitel.Text = artikelId.HasValue ? "Artikel bearbeiten" : "Neuer Artikel";
            Loaded += async (s, e) => await LadeArtikelAsync();
        }

        private async System.Threading.Tasks.Task LadeArtikelAsync()
        {
            try
            {
                txtStatus.Text = "Lade Stammdaten...";

                // Stammdaten laden
                _hersteller = (await _coreService.GetHerstellerAsync()).ToList();
                cmbHersteller.ItemsSource = _hersteller;

                _steuerklassen = (await _coreService.GetSteuerklassenAsync()).ToList();
                cmbSteuerklasse.ItemsSource = _steuerklassen;

                if (_artikelId.HasValue)
                {
                    txtStatus.Text = "Lade Artikeldaten...";
                    _artikel = await _coreService.GetArtikelByIdAsync(_artikelId.Value);

                    if (_artikel == null)
                    {
                        txtStatus.Text = "Artikel nicht gefunden";
                        return;
                    }

                    // Header
                    txtTitel.Text = _artikel.Name ?? "Artikel";
                    txtSubtitel.Text = _artikel.Hersteller ?? "";
                    txtArtNr.Text = $"Art-Nr: {_artikel.CArtNr}";

                    // Identifikation
                    txtArtNrEdit.Text = _artikel.CArtNr;
                    txtBarcode.Text = _artikel.CBarcode;
                    txtHAN.Text = _artikel.CHAN;
                    txtSuchbegriffe.Text = _artikel.CSuchbegriffe;

                    // Beschreibung
                    txtName.Text = _artikel.Name;
                    txtBeschreibung.Text = _artikel.Beschreibung;

                    // Klassifikation
                    cmbHersteller.SelectedValue = _artikel.KHersteller;
                    cmbSteuerklasse.SelectedValue = _artikel.KSteuerklasse;

                    // Status
                    chkAktiv.IsChecked = _artikel.CAktiv == "Y";
                    chkTopArtikel.IsChecked = _artikel.CTopArtikel == "Y";
                    chkMHD.IsChecked = _artikel.NMHD == 1;

                    // Preise
                    txtVKNetto.Text = _artikel.FVKNetto.ToString("N2");
                    txtUVP.Text = _artikel.FUVP.ToString("N2");
                    txtEKNetto.Text = _artikel.FEKNetto.ToString("N2");

                    // Lager
                    txtBestand.Text = $"{_artikel.NLagerbestand:N0} Stueck";
                    txtMindestbestand.Text = _artikel.NMidestbestand.ToString("N0");
                    chkLagerartikel.IsChecked = _artikel.CLagerArtikel == "Y";

                    // MSV3-Lieferanten laden
                    await LadeMSV3LieferantenAsync();

                    // Eigene Felder laden
                    await LadeEigeneFelderAsync();

                    txtStatus.Text = "Artikel geladen";
                }
                else
                {
                    // Neuer Artikel
                    _artikel = new CoreService.ArtikelDetail();
                    txtArtNr.Text = "(wird automatisch vergeben)";
                    chkAktiv.IsChecked = true;
                    chkLagerartikel.IsChecked = true;
                    txtMindestbestand.Text = "0";
                    txtStatus.Text = "Neuen Artikel anlegen";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task LadeMSV3LieferantenAsync()
        {
            if (!_artikelId.HasValue) return;

            try
            {
                // MSV3-Lieferanten fuer diesen Artikel laden
                var lieferantenMap = await _einkaufService.GetArtikelLieferantenMapAsync(new[] { _artikelId.Value });
                if (lieferantenMap.TryGetValue(_artikelId.Value, out var lieferanten))
                {
                    _msv3Lieferanten = lieferanten.Where(l => l.HatMSV3).ToList();
                }

                if (_msv3Lieferanten.Any())
                {
                    pnlMSV3.Visibility = Visibility.Visible;
                    await LadeMSV3BestaendeAsync();
                }
                else
                {
                    pnlMSV3.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                txtMSV3Status.Text = $"Fehler: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task LadeMSV3BestaendeAsync()
        {
            if (!_artikelId.HasValue || !_msv3Lieferanten.Any()) return;

            // PZN = Artikelnummer (cArtNr)
            var artNr = _artikel?.CArtNr?.Trim();

            // Nur Ziffern extrahieren fuer PZN
            string? pzn = null;
            if (!string.IsNullOrEmpty(artNr))
            {
                var digits = new string(artNr.Where(char.IsDigit).ToArray());
                if (digits.Length >= 7 && digits.Length <= 8)
                {
                    pzn = digits;
                }
                else if (digits.Length > 0)
                {
                    // Auch kuerzere Nummern akzeptieren, mit fuehrenden Nullen auffuellen
                    pzn = digits.PadLeft(7, '0');
                    if (pzn.Length > 8) pzn = digits; // Original behalten wenn zu lang
                }
            }

            if (string.IsNullOrEmpty(pzn))
            {
                txtMSV3Status.Text = $"Keine PZN gefunden (Art-Nr: {artNr ?? "-"})";
                lstMSV3Bestaende.ItemsSource = null;
                return;
            }

            txtMSV3Status.Text = "Lade Verfuegbarkeit...";
            btnMSV3Refresh.IsEnabled = false;

            var bestandsInfos = new List<MSV3BestandInfo>();

            foreach (var lieferant in _msv3Lieferanten)
            {
                var info = new MSV3BestandInfo { LieferantName = lieferant.LieferantName ?? "Unbekannt" };

                try
                {
                    // MSV3-Config laden
                    var msv3Configs = await _msv3Service.GetMSV3LieferantenAsync();
                    var config = msv3Configs.FirstOrDefault(c => c.KLieferant == lieferant.KLieferant);

                    if (config == null)
                    {
                        info.StatusText = "Keine MSV3-Konfiguration";
                        info.BestandText = "-";
                        info.BestandFarbe = new SolidColorBrush(Colors.Gray);
                    }
                    else
                    {
                        // GEHE/Alliance Erkennung - braucht CheckVerfuegbarkeitViaBestellenAsync
                        bool isGehe = config.CMSV3Url?.Contains("gehe", StringComparison.OrdinalIgnoreCase) == true ||
                                      config.CMSV3Url?.Contains("alliance", StringComparison.OrdinalIgnoreCase) == true;

                        if (isGehe)
                        {
                            // GEHE/Alliance: Bestandsabfrage via Bestellen-Operation
                            var bestellPos = new List<NovviaERP.Core.Services.MSV3BestellPosition>
                            {
                                new NovviaERP.Core.Services.MSV3BestellPosition { PZN = pzn, Menge = 1 }
                            };

                            var geheResult = await _msv3Service.CheckVerfuegbarkeitViaBestellenAsync(config, bestellPos);

                            if (geheResult.Success && geheResult.Positionen != null && geheResult.Positionen.Any())
                            {
                                var pos = geheResult.Positionen.First();
                                info.Bestand = pos.VerfuegbareMenge;
                                info.BestandText = $"{pos.VerfuegbareMenge:N0}";
                                info.StatusText = pos.StatusCode ?? "OK";

                                if (pos.VerfuegbareMenge > 0)
                                {
                                    info.BestandFarbe = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Gruen
                                }
                                else
                                {
                                    info.BestandFarbe = new SolidColorBrush(Color.FromRgb(211, 47, 47)); // Rot
                                }

                                if (pos.NaechsterLieferzeitpunkt.HasValue)
                                {
                                    info.StatusText += $" | Lieferung: {pos.NaechsterLieferzeitpunkt:dd.MM.yyyy}";
                                }
                            }
                            else
                            {
                                info.StatusText = geheResult.Fehler ?? "Keine Antwort";
                                info.BestandText = "-";
                                info.BestandFarbe = new SolidColorBrush(Colors.Gray);
                            }
                        }
                        else
                        {
                            // Standard MSV3: VerfuegbarkeitAnfragen
                            var result = await _msv3Service.CheckVerfuegbarkeitAsync(config, new[] { pzn });

                            if (result.Success && result.Positionen != null && result.Positionen.Any())
                            {
                                var pos = result.Positionen.First();
                                info.Bestand = (int)pos.MengeVerfuegbar;
                                info.BestandText = $"{pos.MengeVerfuegbar:N0}";
                                info.PreisText = pos.PreisEK > 0 ? $"EK: {pos.PreisEK:N2} EUR" : "";
                                info.StatusText = pos.Status ?? "OK";

                                if (pos.Verfuegbar)
                                {
                                    info.BestandFarbe = new SolidColorBrush(Color.FromRgb(46, 125, 50)); // Gruen
                                }
                                else if (pos.MengeVerfuegbar > 0)
                                {
                                    info.BestandFarbe = new SolidColorBrush(Color.FromRgb(245, 124, 0)); // Orange
                                }
                                else
                                {
                                    info.BestandFarbe = new SolidColorBrush(Color.FromRgb(211, 47, 47)); // Rot
                                }

                                if (pos.MHD.HasValue)
                                {
                                    info.StatusText += $" | MHD: {pos.MHD:dd.MM.yyyy}";
                                }
                            }
                            else
                            {
                                info.StatusText = result.Fehler ?? "Keine Positionen";
                                info.BestandText = "-";
                                info.BestandFarbe = new SolidColorBrush(Colors.Gray);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    info.StatusText = $"Fehler: {ex.Message}";
                    info.BestandText = "?";
                    info.BestandFarbe = new SolidColorBrush(Colors.Red);
                }

                bestandsInfos.Add(info);
            }

            lstMSV3Bestaende.ItemsSource = bestandsInfos;
            txtMSV3Status.Text = $"Aktualisiert: {DateTime.Now:HH:mm:ss}";
            btnMSV3Refresh.IsEnabled = true;
        }

        private async void MSV3Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LadeMSV3BestaendeAsync();
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Speichere...";

                // Validierung
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Bitte Artikelname angeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Daten aus Formular
                _artikel!.CArtNr = txtArtNrEdit.Text.Trim();
                _artikel.CBarcode = txtBarcode.Text.Trim();
                _artikel.CHAN = txtHAN.Text.Trim();
                _artikel.CSuchbegriffe = txtSuchbegriffe.Text.Trim();

                _artikel.Name = txtName.Text.Trim();
                _artikel.Beschreibung = txtBeschreibung.Text.Trim();

                _artikel.KHersteller = cmbHersteller.SelectedValue as int?;
                _artikel.KSteuerklasse = cmbSteuerklasse.SelectedValue as int?;

                _artikel.CAktiv = chkAktiv.IsChecked == true ? "Y" : "N";
                _artikel.CTopArtikel = chkTopArtikel.IsChecked == true ? "Y" : "N";
                _artikel.NMHD = (byte)(chkMHD.IsChecked == true ? 1 : 0);

                _artikel.FVKNetto = decimal.TryParse(txtVKNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var vk) ? vk : 0;
                _artikel.FUVP = decimal.TryParse(txtUVP.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var uvp) ? uvp : 0;
                _artikel.FEKNetto = decimal.TryParse(txtEKNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var ek) ? ek : 0;

                _artikel.NMidestbestand = decimal.TryParse(txtMindestbestand.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var min) ? min : 0;
                _artikel.CLagerArtikel = chkLagerartikel.IsChecked == true ? "Y" : "N";

                if (_artikelId.HasValue)
                {
                    // Update
                    await _coreService.UpdateArtikelAsync(_artikel);
                    txtStatus.Text = "Artikel gespeichert";
                    MessageBox.Show("Artikel wurde gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Neu anlegen
                    var newId = await _coreService.CreateArtikelAsync(_artikel);
                    _artikel.KArtikel = newId;
                    txtArtNr.Text = $"Art-Nr: {_artikel.CArtNr}";
                    txtStatus.Text = $"Artikel {_artikel.CArtNr} angelegt";
                    MessageBox.Show($"Artikel {_artikel.CArtNr} wurde angelegt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(App.Services.GetRequiredService<ArtikelView>());
            }
        }

        #region Eigene Felder (Validierung)

        private async System.Threading.Tasks.Task LadeEigeneFelderAsync()
        {
            if (!_artikelId.HasValue) return;

            try
            {
                var felder = await _coreService.GetArtikelEigeneFelderAsync(_artikelId.Value);

                // Validierung Checkboxen
                chkValAmbient.IsChecked = GetBoolWert(felder, "Ambient");
                chkValCool.IsChecked = GetBoolWert(felder, "Cool");
                chkValMedcan.IsChecked = GetBoolWert(felder, "Medcan");
                chkValTierarznei.IsChecked = GetBoolWert(felder, "Tierarznei");
                chkValSecurPharm.IsChecked = GetBoolWert(felder, "SecurPharm");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden eigener Felder: {ex.Message}");
            }
        }

        private bool GetBoolWert(Dictionary<string, string?> felder, string key)
        {
            if (felder.TryGetValue(key, out var wert))
            {
                return wert == "1" || wert?.ToLower() == "true" || wert?.ToLower() == "ja";
            }
            return false;
        }

        private async void EigeneFelderSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_artikelId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Artikel speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                txtStatus.Text = "Speichere eigene Felder...";

                var felder = new Dictionary<string, string?>
                {
                    ["Ambient"] = chkValAmbient.IsChecked == true ? "1" : "0",
                    ["Cool"] = chkValCool.IsChecked == true ? "1" : "0",
                    ["Medcan"] = chkValMedcan.IsChecked == true ? "1" : "0",
                    ["Tierarznei"] = chkValTierarznei.IsChecked == true ? "1" : "0",
                    ["SecurPharm"] = chkValSecurPharm.IsChecked == true ? "1" : "0"
                };

                await _coreService.SetArtikelEigeneFelderAsync(_artikelId.Value, felder);

                txtStatus.Text = "Eigene Felder gespeichert";
                MessageBox.Show("Eigene Felder wurden gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion
    }

    // Hilfsklasse fuer MSV3-Bestandsanzeige
    public class MSV3BestandInfo
    {
        public string LieferantName { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string BestandText { get; set; } = "-";
        public string PreisText { get; set; } = "";
        public int Bestand { get; set; }
        public SolidColorBrush BestandFarbe { get; set; } = new SolidColorBrush(Colors.Gray);
    }
}
