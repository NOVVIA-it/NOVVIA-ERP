using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EinstellungenView : UserControl
    {
        private readonly CoreService _core;
        private readonly PaymentService? _payment;
        private int? _selectedKundengruppeId;
        private int? _selectedKundenkategorieId;
        private int? _selectedZahlungsartId;
        private int? _selectedVersandartId;
        private int? _selectedLieferantAttributId;

        private static readonly string PaymentConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NovviaERP", "payment-config.json");

        public EinstellungenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _payment = App.Services.GetService<PaymentService>();
            Loaded += async (s, e) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                await LadeFirmendatenAsync();
                await LadeKundengruppenAsync();
                await LadeKundenkategorienAsync();
                await LadeZahlungsartenAsync();
                await LadeVersandartenAsync();
                await LadeSteuernAsync();
                await LadeKontenAsync();
                await LadeEigeneFelderAsync();
                LadeZahlungsanbieterAsync();
                await LadeZahlungsabgleichEinstellungenAsync();
                await LadeWooShopsAsync();
                await LadeLogsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Firmendaten

        private async System.Threading.Tasks.Task LadeFirmendatenAsync()
        {
            var firma = await _core.GetFirmendatenAsync();
            if (firma == null) return;

            txtFirmaName.Text = firma.CFirma ?? "";
            txtFirmaZusatz.Text = firma.CZusatz ?? "";
            txtFirmaStrasse.Text = firma.CStrasse ?? "";
            txtFirmaHausnr.Text = firma.CHausNr ?? "";
            txtFirmaPLZ.Text = firma.CPLZ ?? "";
            txtFirmaOrt.Text = firma.COrt ?? "";
            txtFirmaLand.Text = firma.CLand ?? "";
            txtFirmaTelefon.Text = firma.CTel ?? "";
            txtFirmaFax.Text = firma.CFax ?? "";
            txtFirmaEmail.Text = firma.CMail ?? "";
            txtFirmaWebsite.Text = firma.CWWW ?? "";
            txtFirmaUstId.Text = firma.CUSTID ?? "";
            txtFirmaSteuerNr.Text = firma.CSteuerNr ?? "";
            txtFirmaHReg.Text = firma.CHReg ?? "";
            txtFirmaGF.Text = firma.CGF ?? "";
            txtFirmaBank.Text = firma.CBank ?? "";
            txtFirmaBIC.Text = firma.CBIC ?? "";
            txtFirmaIBAN.Text = firma.CIBAN ?? "";
        }

        private async void FirmaSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var firma = new CoreService.FirmendatenDetail
                {
                    CFirma = txtFirmaName.Text.Trim(),
                    CZusatz = txtFirmaZusatz.Text.Trim(),
                    CStrasse = txtFirmaStrasse.Text.Trim(),
                    CHausNr = txtFirmaHausnr.Text.Trim(),
                    CPLZ = txtFirmaPLZ.Text.Trim(),
                    COrt = txtFirmaOrt.Text.Trim(),
                    CLand = txtFirmaLand.Text.Trim(),
                    CTel = txtFirmaTelefon.Text.Trim(),
                    CFax = txtFirmaFax.Text.Trim(),
                    CMail = txtFirmaEmail.Text.Trim(),
                    CWWW = txtFirmaWebsite.Text.Trim(),
                    CUSTID = txtFirmaUstId.Text.Trim(),
                    CSteuerNr = txtFirmaSteuerNr.Text.Trim(),
                    CHReg = txtFirmaHReg.Text.Trim(),
                    CGF = txtFirmaGF.Text.Trim(),
                    CBank = txtFirmaBank.Text.Trim(),
                    CBIC = txtFirmaBIC.Text.Trim(),
                    CIBAN = txtFirmaIBAN.Text.Trim()
                };

                await _core.UpdateFirmendatenAsync(firma);
                MessageBox.Show("Firmendaten gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Kundengruppen

        private async System.Threading.Tasks.Task LadeKundengruppenAsync()
        {
            var gruppen = await _core.GetKundengruppenDetailAsync();
            dgKundengruppen.ItemsSource = gruppen.ToList();
        }

        private void KundengruppeNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedKundengruppeId = null;
            txtKGName.Text = "";
            txtKGRabatt.Text = "0";
            txtKGName.Focus();
        }

        private void KundengruppeBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgKundengruppen.SelectedItem is CoreService.KundengruppeDetail kg)
            {
                _selectedKundengruppeId = kg.KKundenGruppe;
                txtKGName.Text = kg.CName ?? "";
                txtKGRabatt.Text = kg.FRabatt.ToString("N2");
            }
        }

        private async void KundengruppeLoschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKundengruppen.SelectedItem is not CoreService.KundengruppeDetail kg) return;

            if (MessageBox.Show($"Kundengruppe '{kg.CName}' wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteKundengruppeAsync(kg.KKundenGruppe);
                await LadeKundengruppenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void KundengruppeSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtKGName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                decimal.TryParse(txtKGRabatt.Text, out var rabatt);

                if (_selectedKundengruppeId.HasValue)
                {
                    await _core.UpdateKundengruppeAsync(_selectedKundengruppeId.Value, txtKGName.Text.Trim(), rabatt);
                }
                else
                {
                    await _core.CreateKundengruppeAsync(txtKGName.Text.Trim(), rabatt);
                }

                await LadeKundengruppenAsync();
                _selectedKundengruppeId = null;
                txtKGName.Text = "";
                txtKGRabatt.Text = "0";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Kundenkategorien

        private async System.Threading.Tasks.Task LadeKundenkategorienAsync()
        {
            var kategorien = await _core.GetKundenkategorienAsync();
            dgKundenkategorien.ItemsSource = kategorien.ToList();
        }

        private void KundenkategorieNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedKundenkategorieId = null;
            txtKKName.Text = "";
            txtKKName.Focus();
        }

        private void KundenkategorieBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgKundenkategorien.SelectedItem is CoreService.KundenkategorieRef kk)
            {
                _selectedKundenkategorieId = kk.KKundenKategorie;
                txtKKName.Text = kk.CName ?? "";
            }
        }

        private async void KundenkategorieLoschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKundenkategorien.SelectedItem is not CoreService.KundenkategorieRef kk) return;

            if (MessageBox.Show($"Kundenkategorie '{kk.CName}' wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteKundenkategorieAsync(kk.KKundenKategorie);
                await LadeKundenkategorienAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void KundenkategorieSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtKKName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_selectedKundenkategorieId.HasValue)
                {
                    await _core.UpdateKundenkategorieAsync(_selectedKundenkategorieId.Value, txtKKName.Text.Trim());
                }
                else
                {
                    await _core.CreateKundenkategorieAsync(txtKKName.Text.Trim());
                }

                await LadeKundenkategorienAsync();
                _selectedKundenkategorieId = null;
                txtKKName.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Zahlungsarten

        private async System.Threading.Tasks.Task LadeZahlungsartenAsync()
        {
            var zahlungsarten = await _core.GetZahlungsartenDetailAsync();
            dgZahlungsarten.ItemsSource = zahlungsarten.ToList();
        }

        private void ZahlungsartNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedZahlungsartId = null;
            txtZAName.Text = "";
            txtZAModul.Text = "";
            txtZAName.Focus();
        }

        private void ZahlungsartBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgZahlungsarten.SelectedItem is CoreService.ZahlungsartDetail za)
            {
                _selectedZahlungsartId = za.KZahlungsart;
                txtZAName.Text = za.CName ?? "";
                txtZAModul.Text = za.CModulId ?? "";
            }
        }

        private async void ZahlungsartLoschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgZahlungsarten.SelectedItem is not CoreService.ZahlungsartDetail za) return;

            if (MessageBox.Show($"Zahlungsart '{za.CName}' wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteZahlungsartAsync(za.KZahlungsart);
                await LadeZahlungsartenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ZahlungsartSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtZAName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_selectedZahlungsartId.HasValue)
                {
                    await _core.UpdateZahlungsartAsync(_selectedZahlungsartId.Value, txtZAName.Text.Trim(), txtZAModul.Text.Trim());
                }
                else
                {
                    await _core.CreateZahlungsartAsync(txtZAName.Text.Trim(), txtZAModul.Text.Trim());
                }

                await LadeZahlungsartenAsync();
                _selectedZahlungsartId = null;
                txtZAName.Text = "";
                txtZAModul.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Versandarten

        private async System.Threading.Tasks.Task LadeVersandartenAsync()
        {
            var versandarten = await _core.GetVersandartenDetailAsync();
            dgVersandarten.ItemsSource = versandarten.ToList();
        }

        private void VersandartNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedVersandartId = null;
            txtVAName.Text = "";
            txtVAKosten.Text = "0";
            txtVAName.Focus();
        }

        private void VersandartBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgVersandarten.SelectedItem is CoreService.VersandartDetail va)
            {
                _selectedVersandartId = va.KVersandart;
                txtVAName.Text = va.CName ?? "";
                txtVAKosten.Text = va.FKosten.ToString("N2");
            }
        }

        private async void VersandartLoschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgVersandarten.SelectedItem is not CoreService.VersandartDetail va) return;

            if (MessageBox.Show($"Versandart '{va.CName}' wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteVersandartAsync(va.KVersandart);
                await LadeVersandartenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void VersandartSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtVAName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                decimal.TryParse(txtVAKosten.Text, out var kosten);

                if (_selectedVersandartId.HasValue)
                {
                    await _core.UpdateVersandartAsync(_selectedVersandartId.Value, txtVAName.Text.Trim(), kosten);
                }
                else
                {
                    await _core.CreateVersandartAsync(txtVAName.Text.Trim(), kosten);
                }

                await LadeVersandartenAsync();
                _selectedVersandartId = null;
                txtVAName.Text = "";
                txtVAKosten.Text = "0";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Steuern & Konten (nur Ansicht)

        private async System.Threading.Tasks.Task LadeSteuernAsync()
        {
            var steuern = await _core.GetSteuernAsync();
            dgSteuern.ItemsSource = steuern.ToList();
        }

        private async System.Threading.Tasks.Task LadeKontenAsync()
        {
            var konten = await _core.GetKontenAsync();
            dgKonten.ItemsSource = konten.ToList();
        }

        private async System.Threading.Tasks.Task LadeEigeneFelderAsync()
        {
            // Lieferant Attribute (NOVVIA - editierbar)
            try
            {
                var lieferantAttr = await _core.GetLieferantAttributeAsync();
                var liste = lieferantAttr.ToList();
                dgLieferantAttribute.ItemsSource = liste;
                txtLieferantHinweis.Text = liste.Count == 0 ? "Keine Attribute. Bitte 'Neu' klicken um Felder anzulegen." : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lieferant Attribute: {ex.Message}");
                txtLieferantHinweis.Text = "Tabelle fehlt - bitte Setup-EigeneFelderLieferant.sql ausführen";
            }

            // Kunde Attribute (JTL - nur Ansicht)
            try
            {
                var kundeAttr = await _core.GetKundeAttributeAsync();
                dgKundeAttribute.ItemsSource = kundeAttr.ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Kunde Attribute: {ex.Message}"); }

            // Artikel Attribute (JTL - nur Ansicht)
            try
            {
                var artikelAttr = await _core.GetArtikelAttributeAsync();
                dgArtikelAttribute.ItemsSource = artikelAttr.ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Artikel Attribute: {ex.Message}"); }

            // Auftrag Attribute (JTL - nur Ansicht)
            try
            {
                var auftragAttr = await _core.GetAuftragAttributeAsync();
                dgAuftragAttribute.ItemsSource = auftragAttr.ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Auftrag Attribute: {ex.Message}"); }

            // Firma Attribute (Definitionen)
            try
            {
                var firmaAttr = await _core.GetFirmaAttributeAsync();
                dgFirmaAttribute.ItemsSource = firmaAttr.ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Firma Attribute: {ex.Message}"); }

            // Firma Eigene Felder (Werte) - explizit mit kFirma=1 aufrufen für EigenesFeldWert
            try
            {
                var firmaFelder = await _core.GetFirmaEigeneFelderAsync(1);
                dgFirmaEigeneFelder.ItemsSource = firmaFelder.ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Firma Felder: {ex.Message}"); }
        }

        #endregion

        #region Lieferant Attribute (NOVVIA)

        private void LieferantAttributNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedLieferantAttributId = null;
            txtLiefAttrName.Text = "";
            txtLiefAttrBeschr.Text = "";
            cmbLiefAttrTyp.SelectedIndex = 0;
            txtLiefAttrSort.Text = "0";
            txtLieferantHinweis.Text = "Neues Attribut - Name eingeben und Speichern klicken";
            txtLieferantHinweis.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            txtLiefAttrName.Focus();
        }

        private void LieferantAttributBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgLieferantAttribute.SelectedItem is CoreService.EigenesFeldDefinition attr)
            {
                _selectedLieferantAttributId = attr.KAttribut;
                txtLiefAttrName.Text = attr.CName ?? "";
                txtLiefAttrBeschr.Text = attr.CBeschreibung ?? "";
                // Typ auswählen (1=Ganzzahl, 2=Dezimal, 3=Text, 4=Datum)
                cmbLiefAttrTyp.SelectedIndex = attr.NFeldTyp switch
                {
                    3 => 0, // Text
                    1 => 1, // Ganzzahl
                    2 => 2, // Dezimal
                    4 => 3, // Datum
                    _ => 0
                };
                txtLiefAttrSort.Text = attr.NSortierung.ToString();
                txtLieferantHinweis.Text = $"Bearbeite: {attr.CName}";
                txtLieferantHinweis.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
                txtLiefAttrName.Focus();
            }
            else
            {
                MessageBox.Show("Bitte zuerst ein Attribut in der Liste auswählen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void LieferantAttributLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgLieferantAttribute.SelectedItem is not CoreService.EigenesFeldDefinition attr) return;

            if (MessageBox.Show($"Attribut '{attr.CName}' wirklich loeschen?\nAlle zugeordneten Werte werden ebenfalls geloescht!",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteLieferantAttributAsync(attr.KAttribut);
                await LadeEigeneFelderAsync();
                LieferantAttributNeu_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LieferantAttributSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLiefAttrName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int.TryParse(txtLiefAttrSort.Text, out var sort);
                var feldTyp = int.Parse((cmbLiefAttrTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "3");

                var attr = new CoreService.EigenesFeldDefinition
                {
                    KAttribut = _selectedLieferantAttributId ?? 0,
                    CName = txtLiefAttrName.Text.Trim(),
                    CBeschreibung = txtLiefAttrBeschr.Text.Trim(),
                    NFeldTyp = feldTyp,
                    NSortierung = sort,
                    NAktiv = true
                };

                await _core.SaveLieferantAttributAsync(attr);
                await LadeEigeneFelderAsync();

                // Formular leeren
                _selectedLieferantAttributId = null;
                txtLiefAttrName.Text = "";
                txtLiefAttrBeschr.Text = "";
                cmbLiefAttrTyp.SelectedIndex = 0;
                txtLiefAttrSort.Text = "0";
                txtLieferantHinweis.Text = "";
                txtLieferantHinweis.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);

                MessageBox.Show("Attribut gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Zahlungsanbieter

        private void LadeZahlungsanbieterAsync()
        {
            try
            {
                if (File.Exists(PaymentConfigPath))
                {
                    var json = File.ReadAllText(PaymentConfigPath);
                    var config = JsonSerializer.Deserialize<PaymentConfig>(json);
                    if (config != null)
                    {
                        txtPayPalClientId.Text = config.PayPalClientId;
                        txtPayPalSecret.Password = config.PayPalSecret;
                        txtPayPalReturnUrl.Text = config.PayPalReturnUrl ?? "";
                        txtPayPalCancelUrl.Text = config.PayPalCancelUrl ?? "";
                        chkPayPalSandbox.IsChecked = config.PayPalSandbox;

                        txtMollieApiKey.Password = config.MollieApiKey;
                        txtMollieRedirectUrl.Text = config.MollieRedirectUrl ?? "";
                        txtMollieWebhookUrl.Text = config.MollieWebhookUrl ?? "";

                        txtPaymentFirmaName.Text = config.FirmaName ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Payment Config laden: {ex.Message}");
            }
        }

        private void ZahlungsanbieterSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = new PaymentConfig
                {
                    PayPalClientId = txtPayPalClientId.Text.Trim(),
                    PayPalSecret = txtPayPalSecret.Password,
                    PayPalReturnUrl = txtPayPalReturnUrl.Text.Trim(),
                    PayPalCancelUrl = txtPayPalCancelUrl.Text.Trim(),
                    PayPalSandbox = chkPayPalSandbox.IsChecked == true,

                    MollieApiKey = txtMollieApiKey.Password,
                    MollieRedirectUrl = txtMollieRedirectUrl.Text.Trim(),
                    MollieWebhookUrl = txtMollieWebhookUrl.Text.Trim(),

                    FirmaName = txtPaymentFirmaName.Text.Trim()
                };

                // Verzeichnis sicherstellen
                var dir = Path.GetDirectoryName(PaymentConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PaymentConfigPath, json);

                txtPaymentStatus.Text = "Zahlungsanbieter-Einstellungen gespeichert!";
                txtPaymentStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);

                MessageBox.Show("Zahlungsanbieter-Einstellungen gespeichert!\n\nHinweis: Die App muss neu gestartet werden, damit die Aenderungen wirksam werden.",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtPaymentStatus.Text = $"Fehler: {ex.Message}";
                txtPaymentStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ZahlungsanbieterTesten_Click(object sender, RoutedEventArgs e)
        {
            var results = new System.Text.StringBuilder();
            results.AppendLine("Verbindungstest:\n");

            // PayPal testen
            if (!string.IsNullOrEmpty(txtPayPalClientId.Text) && !string.IsNullOrEmpty(txtPayPalSecret.Password))
            {
                try
                {
                    txtPaymentStatus.Text = "Teste PayPal...";
                    var baseUrl = chkPayPalSandbox.IsChecked == true
                        ? "https://api-m.sandbox.paypal.com"
                        : "https://api-m.paypal.com";

                    using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, $"{baseUrl}/v1/oauth2/token");
                    var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{txtPayPalClientId.Text}:{txtPayPalSecret.Password}"));
                    request.Headers.Add("Authorization", $"Basic {credentials}");
                    request.Content = new System.Net.Http.StringContent("grant_type=client_credentials", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                    var response = await http.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                        results.AppendLine("PayPal: OK (Token erfolgreich abgerufen)");
                    else
                        results.AppendLine($"PayPal: FEHLER ({response.StatusCode})");
                }
                catch (Exception ex)
                {
                    results.AppendLine($"PayPal: FEHLER ({ex.Message})");
                }
            }
            else
            {
                results.AppendLine("PayPal: Nicht konfiguriert");
            }

            // Mollie testen
            if (!string.IsNullOrEmpty(txtMollieApiKey.Password))
            {
                try
                {
                    txtPaymentStatus.Text = "Teste Mollie...";
                    using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {txtMollieApiKey.Password}");

                    var response = await http.GetAsync("https://api.mollie.com/v2/methods");
                    if (response.IsSuccessStatusCode)
                        results.AppendLine("Mollie: OK (API erreichbar)");
                    else
                        results.AppendLine($"Mollie: FEHLER ({response.StatusCode})");
                }
                catch (Exception ex)
                {
                    results.AppendLine($"Mollie: FEHLER ({ex.Message})");
                }
            }
            else
            {
                results.AppendLine("Mollie: Nicht konfiguriert");
            }

            txtPaymentStatus.Text = "Test abgeschlossen";
            MessageBox.Show(results.ToString(), "Verbindungstest", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async System.Threading.Tasks.Task LadeZahlungsabgleichEinstellungenAsync()
        {
            try
            {
                var zahlungsabgleichService = App.Services.GetService<ZahlungsabgleichService>();
                if (zahlungsabgleichService != null)
                {
                    var schwelle = await zahlungsabgleichService.GetAutoMatchSchwelleAsync();
                    txtAutoMatchSchwelle.Text = schwelle.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Zahlungsabgleich-Einstellungen laden: {ex.Message}");
            }
        }

        private async void ZahlungsabgleichEinstellungenSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtAutoMatchSchwelle.Text, out var schwelle) || schwelle < 0 || schwelle > 100)
                {
                    MessageBox.Show("Bitte geben Sie eine gueltige Schwelle zwischen 0 und 100 ein.", "Hinweis",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var zahlungsabgleichService = App.Services.GetService<ZahlungsabgleichService>();
                if (zahlungsabgleichService != null)
                {
                    await zahlungsabgleichService.SetAutoMatchSchwelleAsync(schwelle);
                    MessageBox.Show($"Auto-Match Schwelle wurde auf {schwelle}% gesetzt.", "Erfolg",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region WooCommerce Shops

        private List<WooCommerceShop> _wooShops = new();
        private WooCommerceShop? _selectedWooShop;

        private async System.Threading.Tasks.Task LadeWooShopsAsync()
        {
            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                _wooShops = await db.GetWooCommerceShopsAsync();
                dgWooShops.ItemsSource = _wooShops;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WooCommerce Shops laden: {ex.Message}");
            }
        }

        private void DgWooShops_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedWooShop = dgWooShops.SelectedItem as WooCommerceShop;
            if (_selectedWooShop != null)
            {
                txtWooSecret.Password = _selectedWooShop.ConsumerSecret ?? "";
                txtWooWebhookSecret.Text = _selectedWooShop.WebhookSecret ?? "(noch nicht generiert)";
                txtWooCallbackUrl.Text = _selectedWooShop.WebhookCallbackUrl ?? "";
            }
        }

        private void WooShopNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedWooShop = new WooCommerceShop
            {
                Name = "Neuer Shop",
                Url = "https://",
                Aktiv = true,
                SyncIntervallMinuten = 15,
                WebhookSecret = Guid.NewGuid().ToString("N")
            };
            _wooShops.Add(_selectedWooShop);
            dgWooShops.ItemsSource = null;
            dgWooShops.ItemsSource = _wooShops;
            dgWooShops.SelectedItem = _selectedWooShop;

            txtWooWebhookSecret.Text = _selectedWooShop.WebhookSecret;
        }

        private async void WooShopSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null) return;

            try
            {
                _selectedWooShop.ConsumerSecret = txtWooSecret.Password;
                _selectedWooShop.WebhookCallbackUrl = txtWooCallbackUrl.Text.Trim();

                var db = App.Services.GetRequiredService<JtlDbContext>();
                if (_selectedWooShop.Id == 0)
                {
                    _selectedWooShop.Id = await db.CreateWooCommerceShopAsync(_selectedWooShop);
                }
                else
                {
                    await db.UpdateWooCommerceShopAsync(_selectedWooShop);
                }

                await LadeWooShopsAsync();
                MessageBox.Show("Shop gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WooShopLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null || _selectedWooShop.Id == 0) return;

            if (MessageBox.Show($"Shop '{_selectedWooShop.Name}' wirklich loeschen?", "Bestaetigung",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                await db.DeleteWooCommerceShopAsync(_selectedWooShop.Id);
                await LadeWooShopsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WooShopTest_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null)
            {
                MessageBox.Show("Bitte zuerst einen Shop auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var testModus = chkWooTestModus.IsChecked == true;
                var db = App.Services.GetRequiredService<JtlDbContext>();
                using var woo = new WooCommerceService(db, testModus);

                var ok = await woo.TestConnectionAsync(_selectedWooShop);

                if (testModus)
                {
                    ZeigeApiLog(woo.GetApiLog());
                }

                if (ok)
                {
                    MessageBox.Show($"Verbindung zu '{_selectedWooShop.Name}' erfolgreich!", "Test OK",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Verbindung zu '{_selectedWooShop.Name}' fehlgeschlagen!\n\nBitte URL und Zugangsdaten pruefen.",
                        "Test FEHLGESCHLAGEN", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WooShopWebhooks_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null)
            {
                MessageBox.Show("Bitte zuerst einen Shop auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtWooCallbackUrl.Text))
            {
                MessageBox.Show("Bitte zuerst eine Webhook Callback URL eingeben!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                using var woo = new WooCommerceService(db, true);

                var success = await woo.SetupWebhooksAsync(_selectedWooShop, txtWooCallbackUrl.Text.Trim());
                ZeigeApiLog(woo.GetApiLog());

                if (success)
                {
                    _selectedWooShop.WebhooksAktiv = true;
                    _selectedWooShop.WebhookCallbackUrl = txtWooCallbackUrl.Text.Trim();
                    await db.UpdateWooCommerceShopAsync(_selectedWooShop);
                    await LadeWooShopsAsync();

                    MessageBox.Show("Webhooks erfolgreich im Shop registriert!\n\n" +
                        "Folgende Events werden jetzt gesendet:\n" +
                        "- order.created\n- order.updated\n- product.updated\n- product.deleted",
                        "Webhooks eingerichtet", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Webhooks konnten nicht eingerichtet werden.\nBitte pruefen Sie die Verbindung zum Shop.",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WooSyncKategorien_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null) return;

            try
            {
                var testModus = chkWooTestModus.IsChecked == true;
                var db = App.Services.GetRequiredService<JtlDbContext>();
                using var woo = new WooCommerceService(db, testModus);

                var kategorien = await db.GetKategorienAsync();
                var result = await woo.SyncAllCategoriesAsync(_selectedWooShop, kategorien);

                if (testModus) ZeigeApiLog(woo.GetApiLog());

                MessageBox.Show($"Kategorien Sync abgeschlossen:\n\n" +
                    $"Erstellt: {result.Erstellt}\n" +
                    $"Uebersprungen: {result.Uebersprungen}\n" +
                    $"Fehler: {result.Fehler}",
                    "Sync-Ergebnis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WooSyncArtikel_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null) return;

            try
            {
                var testModus = chkWooTestModus.IsChecked == true;
                var db = App.Services.GetRequiredService<JtlDbContext>();
                using var woo = new WooCommerceService(db, testModus);

                var artikel = (await db.GetArtikelAsync(null, aktiv: true, limit: 10000)).ToList();
                var result = await woo.SyncAllProductsAsync(_selectedWooShop, artikel);

                await db.UpdateWooCommerceSyncTimeAsync(_selectedWooShop.Id);
                await LadeWooShopsAsync();

                if (testModus) ZeigeApiLog(woo.GetApiLog());

                MessageBox.Show($"Artikel Sync abgeschlossen:\n\n" +
                    $"Erstellt: {result.Erstellt}\n" +
                    $"Aktualisiert: {result.Aktualisiert}\n" +
                    $"Fehler: {result.Fehler}",
                    "Sync-Ergebnis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WooImportBestellungen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWooShop == null) return;

            try
            {
                var testModus = chkWooTestModus.IsChecked == true;
                var db = App.Services.GetRequiredService<JtlDbContext>();
                using var woo = new WooCommerceService(db, testModus);

                var result = await woo.ImportAllOrdersAsync(_selectedWooShop);

                if (testModus) ZeigeApiLog(woo.GetApiLog());

                MessageBox.Show($"Bestellungen Import abgeschlossen:\n\n" +
                    $"Importiert: {result.Erstellt}\n" +
                    $"Uebersprungen: {result.Uebersprungen}\n" +
                    $"Fehler: {result.Fehler}",
                    "Import-Ergebnis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ZeigeApiLog(List<WooCommerceService.ApiLogEntry> log)
        {
            var sb = new StringBuilder();
            foreach (var entry in log)
            {
                sb.AppendLine($"[{entry.Zeitpunkt:HH:mm:ss}] {entry.Methode} {entry.Url}");
                if (!string.IsNullOrEmpty(entry.RequestBody))
                    sb.AppendLine($"  Request: {TruncateString(entry.RequestBody, 200)}");
                sb.AppendLine($"  Response: {entry.StatusCode} ({entry.DauerMs}ms)");
                if (!string.IsNullOrEmpty(entry.ResponseBody))
                    sb.AppendLine($"  Body: {TruncateString(entry.ResponseBody, 200)}");
                if (!string.IsNullOrEmpty(entry.Fehler))
                    sb.AppendLine($"  FEHLER: {entry.Fehler}");
                sb.AppendLine();
            }
            txtWooApiLog.Text = sb.ToString();
        }

        private static string TruncateString(string s, int maxLength)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length > maxLength ? s[..maxLength] + "..." : s;
        }

        #endregion

        #region Protokoll / Log

        private LogService? _logService;
        private List<LogService.LogEintrag> _logs = new();

        private async System.Threading.Tasks.Task LadeLogsAsync()
        {
            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                _logService ??= new LogService(db);

                // Statistik laden
                var stats = await _logService.GetStatsAsync(7);
                txtLogGesamt.Text = stats.Gesamt.ToString();
                txtLogShop.Text = stats.Shop.ToString();
                txtLogZahlung.Text = stats.Zahlungsabgleich.ToString();
                txtLogStamm.Text = stats.Stammdaten.ToString();
                txtLogBewegung.Text = stats.Bewegungsdaten.ToString();
                txtLogFehler.Text = stats.Fehler.ToString();

                // Logs laden mit Filter
                var filter = new LogService.LogFilter
                {
                    Kategorie = GetSelectedComboText(cmbLogKategorie),
                    Modul = GetSelectedComboText(cmbLogModul),
                    Von = dpLogVon.SelectedDate,
                    Bis = dpLogBis.SelectedDate?.AddDays(1),
                    Suche = string.IsNullOrWhiteSpace(txtLogSuche.Text) ? null : txtLogSuche.Text.Trim()
                };

                _logs = await _logService.GetLogsAsync(filter, 500);
                dgLogs.ItemsSource = _logs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logs laden: {ex.Message}");
            }
        }

        private string? GetSelectedComboText(ComboBox cmb)
        {
            if (cmb.SelectedItem is ComboBoxItem item)
            {
                var text = item.Content?.ToString();
                if (text == "(Alle)" || string.IsNullOrEmpty(text))
                    return null;
                return text;
            }
            return null;
        }

        private void LogFilter_Changed(object sender, object e)
        {
            // Automatisch neu laden wenn Filter geaendert wird
            // Nur wenn bereits geladen
            if (_logService != null && IsLoaded)
            {
                _ = LadeLogsAsync();
            }
        }

        private async void LogSuchen_Click(object sender, RoutedEventArgs e)
        {
            await LadeLogsAsync();
        }

        private async void LogAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeLogsAsync();
        }

        private void DgLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgLogs.SelectedItem is LogService.LogEintrag log)
            {
                txtLogFeld.Text = log.CFeldname ?? "-";
                txtLogAlterWert.Text = log.CAlterWert ?? "-";
                txtLogNeuerWert.Text = log.CNeuerWert ?? "-";
                txtLogDetails.Text = log.CDetails ?? "-";
                txtLogRechner.Text = log.CRechnername ?? "-";
                txtLogIP.Text = log.CIP ?? "-";
            }
            else
            {
                txtLogFeld.Text = "-";
                txtLogAlterWert.Text = "-";
                txtLogNeuerWert.Text = "-";
                txtLogDetails.Text = "-";
                txtLogRechner.Text = "-";
                txtLogIP.Text = "-";
            }
        }

        #endregion
    }
}
