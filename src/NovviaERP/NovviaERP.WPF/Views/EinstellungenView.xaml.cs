using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

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
                // Spalten-Konfiguration für alle DataGrids
                DataGridColumnConfig.EnableColumnChooser(dgFirmaEinstellungen, "EinstellungenView.Firma");
                DataGridColumnConfig.EnableColumnChooser(dgKundengruppen, "EinstellungenView.Kundengruppen");
                DataGridColumnConfig.EnableColumnChooser(dgKundenkategorien, "EinstellungenView.Kategorien");
                DataGridColumnConfig.EnableColumnChooser(dgZahlungsarten, "EinstellungenView.Zahlungsarten");
                DataGridColumnConfig.EnableColumnChooser(dgVersandarten, "EinstellungenView.Versandarten");
                DataGridColumnConfig.EnableColumnChooser(dgWooShops, "EinstellungenView.WooShops");
                DataGridColumnConfig.EnableColumnChooser(dgSteuern, "EinstellungenView.Steuern");
                DataGridColumnConfig.EnableColumnChooser(dgKonten, "EinstellungenView.Konten");
                DataGridColumnConfig.EnableColumnChooser(dgLieferantAttribute, "EinstellungenView.LieferantAttr");
                DataGridColumnConfig.EnableColumnChooser(dgKundeAttribute, "EinstellungenView.KundeAttr");
                DataGridColumnConfig.EnableColumnChooser(dgArtikelAttribute, "EinstellungenView.ArtikelAttr");
                DataGridColumnConfig.EnableColumnChooser(dgAuftragAttribute, "EinstellungenView.AuftragAttr");
                DataGridColumnConfig.EnableColumnChooser(dgFirmaAttribute, "EinstellungenView.FirmaAttr");
                DataGridColumnConfig.EnableColumnChooser(dgFirmaEigeneFelder, "EinstellungenView.FirmaFelder");
                DataGridColumnConfig.EnableColumnChooser(dgLogs, "EinstellungenView.Logs");
                DataGridColumnConfig.EnableColumnChooser(dgJtlBilder, "EinstellungenView.JtlBilder");

                await LadeFirmendatenAsync();
                await LadeFirmaEinstellungenAsync();
                await LadeKundengruppenAsync();
                await LadeKundenkategorienAsync();
                await LadeZahlungsartenAsync();
                await LadeVersandartenAsync();
                await LadeLagerAsync();
                await LadeSteuernAsync();
                await LadeKontenAsync();
                await LadeEigeneFelderAsync();
                LadeZahlungsanbieterAsync();
                await LadeZahlungsabgleichEinstellungenAsync();
                await LadeWooShopsAsync();
                await LadeLogsAsync();
                await LadeLogAufbewahrungAsync();
                await LadeNovviaEinstellungenAsync();
                await LadeBenutzerAsync();
                await LadeRollenAsync();
                await LadeVorlagenAsync();
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

        private List<JtlDbContext.NovviaEinstellung> _firmaEinstellungen = new();

        private async System.Threading.Tasks.Task LadeFirmaEinstellungenAsync()
        {
            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                _firmaEinstellungen = (await db.GetAlleEinstellungenAsync()).ToList();
                dgFirmaEinstellungen.ItemsSource = _firmaEinstellungen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firma Einstellungen laden: {ex.Message}");
            }
        }

        private void FirmaEinstellungNeu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Neues Feld", "Schluessel (z.B. Firma.MeinFeld):");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Ergebnis))
            {
                var neueEinstellung = new JtlDbContext.NovviaEinstellung
                {
                    CSchluessel = dialog.Ergebnis,
                    CWert = "",
                    CBeschreibung = "",
                    DGeaendert = DateTime.Now
                };
                _firmaEinstellungen.Add(neueEinstellung);
                dgFirmaEinstellungen.ItemsSource = null;
                dgFirmaEinstellungen.ItemsSource = _firmaEinstellungen;
            }
        }

        private async void FirmaEinstellungenSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                foreach (var einstellung in _firmaEinstellungen)
                {
                    await db.SetEinstellungAsync(einstellung.CSchluessel, einstellung.CWert ?? "",
                        einstellung.CBeschreibung);
                }
                MessageBox.Show($"{_firmaEinstellungen.Count} Einstellungen gespeichert.", "Erfolg",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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

        #region Lager (JTL-Style)

        private List<CoreService.LagerUebersicht>? _lager;
        private int? _selectedLagerId;

        private bool _isLoadingLager = false;

        private async System.Threading.Tasks.Task LadeLagerAsync()
        {
            if (_isLoadingLager) return;
            _isLoadingLager = true;

            try
            {
                // Auswahl merken vor dem Neuladen
                var selectedId = _selectedLagerId;

                _lager = await _core.GetLagerUebersichtAsync();
                lstLager.ItemsSource = _lager;

                // Auswahl wiederherstellen
                if (selectedId.HasValue)
                {
                    lstLager.SelectedValue = selectedId.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lager laden: {ex.Message}");
            }
            finally
            {
                _isLoadingLager = false;
            }
        }

        private void LstLager_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Waehrend des Ladens keine Aenderungen verarbeiten
            if (_isLoadingLager) return;

            if (lstLager.SelectedItem is CoreService.LagerUebersicht lager)
            {
                _selectedLagerId = lager.KWarenLager;
                btnLagerLoeschen.IsEnabled = true;

                // Allgemein
                txtLagerName.Text = lager.CName ?? "";
                txtLagerKuerzel.Text = lager.CKuerzel ?? "";

                // Lagertyp
                cmbLagerTyp.SelectedIndex = 0; // Default
                foreach (ComboBoxItem item in cmbLagerTyp.Items)
                {
                    if (item.Tag?.ToString() == lager.CLagerTyp)
                    {
                        cmbLagerTyp.SelectedItem = item;
                        break;
                    }
                }

                // Optionen
                chkLagerAktiv.IsChecked = lager.NAktiv;

                // Adresse (aus erweiterten Daten laden, falls vorhanden)
                txtLagerStrasse.Text = lager.CStrasse ?? "";
                txtLagerPLZ.Text = lager.CPLZ ?? "";
                txtLagerOrt.Text = lager.COrt ?? "";
                txtLagerLand.Text = lager.CLand ?? "Deutschland";
                txtLagerTelefon.Text = lager.CTelefon ?? "";
                txtLagerEmail.Text = lager.CEmail ?? "";
                txtLagerAnsprechpartner.Text = lager.CAnsprechpartner ?? "";

                txtLagerStatus.Text = $"Lager ID: {lager.KWarenLager}";
                txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
            // WICHTIG: _selectedLagerId wird NICHT geloescht, wenn SelectedItem null ist
            // Das kann passieren wenn der Fokus wechselt, aber das Lager bleibt ausgewaehlt
        }

        private void ClearLagerForm()
        {
            txtLagerName.Text = "";
            txtLagerKuerzel.Text = "";
            cmbLagerTyp.SelectedIndex = 0;
            chkLagerAktiv.IsChecked = true;
            txtLagerStrasse.Text = "";
            txtLagerPLZ.Text = "";
            txtLagerOrt.Text = "";
            txtLagerLand.Text = "Deutschland";
            txtLagerTelefon.Text = "";
            txtLagerEmail.Text = "";
            txtLagerAnsprechpartner.Text = "";
            txtLagerStatus.Text = "";
            txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        private void LagerNeu_Click(object sender, RoutedEventArgs e)
        {
            // Formular leeren fuer neues Lager
            _selectedLagerId = null;
            btnLagerLoeschen.IsEnabled = false;

            // Auswahl in der ListBox entfernen (ohne SelectionChanged zu triggern)
            _isLoadingLager = true;
            lstLager.SelectedItem = null;
            _isLoadingLager = false;

            ClearLagerForm();
            txtLagerName.Focus();
            txtLagerStatus.Text = "Neues Lager - Namen eingeben und Speichern klicken.";
            txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
        }

        private async void LagerLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLagerId == null) return;

            var lager = lstLager.SelectedItem as CoreService.LagerUebersicht;
            if (lager == null) return;

            var result = MessageBox.Show(
                $"Lager '{lager.CName}' wirklich loeschen?\n\n" +
                "Achtung: Dies ist nur moeglich, wenn das Lager leer ist.",
                "Lager loeschen", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteWarenlagerAsync(_selectedLagerId.Value);
                await LadeLagerAsync();
                ClearLagerForm();
                txtLagerStatus.Text = "Lager geloescht.";
                txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LagerplaetzeVerwalten_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLagerId == null)
            {
                MessageBox.Show("Bitte zuerst ein Lager auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lager = lstLager.SelectedItem as CoreService.LagerUebersicht;
            MessageBox.Show(
                $"Lagerplaetze fuer '{lager?.CName}':\n\n" +
                $"Anzahl Plaetze: {lager?.AnzahlPlaetze ?? 0}\n\n" +
                "Funktion wird noch implementiert.\n" +
                "Neue Plaetze koennen ueber SQL hinzugefuegt werden.",
                "Lagerplaetze", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void LagerSpeichern_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            if (string.IsNullOrWhiteSpace(txtLagerName.Text))
            {
                MessageBox.Show("Bitte einen Lagernamen eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtLagerName.Focus();
                return;
            }

            try
            {
                var lagerTyp = (cmbLagerTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Standard";
                var kuerzel = string.IsNullOrWhiteSpace(txtLagerKuerzel.Text)
                    ? txtLagerName.Text.Substring(0, Math.Min(3, txtLagerName.Text.Length)).ToUpper()
                    : txtLagerKuerzel.Text.Trim();

                // Speichern-Button waehrend Speicherung deaktivieren
                txtLagerStatus.Text = $"Speichern... (ID: {_selectedLagerId?.ToString() ?? "NEU"})";
                txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);

                if (_selectedLagerId == null)
                {
                    // Neues Lager anlegen
                    var newId = await _core.CreateWarenlagerAsync(txtLagerName.Text.Trim(), kuerzel);
                    _selectedLagerId = newId; // Neue ID merken
                    txtLagerStatus.Text = $"Neues Lager '{txtLagerName.Text}' angelegt (ID: {newId})!";
                    btnLagerLoeschen.IsEnabled = true;
                }
                else
                {
                    // Bestehendes Lager aktualisieren
                    var rowsAffected = await _core.UpdateWarenlagerAsync(
                        kWarenLager: _selectedLagerId.Value,
                        name: txtLagerName.Text.Trim(),
                        kuerzel: kuerzel,
                        aktiv: chkLagerAktiv.IsChecked == true,
                        lagerTyp: lagerTyp,
                        strasse: txtLagerStrasse.Text.Trim(),
                        plz: txtLagerPLZ.Text.Trim(),
                        ort: txtLagerOrt.Text.Trim(),
                        land: txtLagerLand.Text.Trim(),
                        ansprechpartnerName: txtLagerAnsprechpartner.Text.Trim(),
                        telefon: txtLagerTelefon.Text.Trim(),
                        email: txtLagerEmail.Text.Trim());

                    txtLagerStatus.Text = $"Lager '{txtLagerName.Text}' gespeichert!";
                }

                txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);

                // Lagerliste neu laden und Auswahl wiederherstellen
                await LadeLagerAsync();
            }
            catch (Exception ex)
            {
                txtLagerStatus.Text = $"Fehler: {ex.Message}";
                txtLagerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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

        #region NOVVIA Einstellungen

        private async System.Threading.Tasks.Task LadeNovviaEinstellungenAsync()
        {
            try
            {
                // Lager für Dropdown laden
                var lager = await _core.GetLagerUebersichtAsync();
                cmbQuarantaeneLager.ItemsSource = lager;

                // Einstellungen aus NOVVIA.FirmaEinstellung laden
                var einstellungen = await _core.GetNovviaEinstellungenAsync();

                chkPharmaModus.IsChecked = einstellungen.PharmaModus;
                cmbQuarantaeneLager.SelectedValue = einstellungen.QuarantaeneLagerId;
                txtAutoMatchSchwelle.Text = einstellungen.AutoMatchSchwelle.ToString();
                txtNovviaFirmenname.Text = einstellungen.Firmenname ?? "";

                // Logo laden
                txtLogoPfad.Text = einstellungen.LogoPfad ?? "";
                LadeLogoVorschau(einstellungen.LogoPfad);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NOVVIA Einstellungen laden: {ex.Message}");
            }
        }

        private void LadeLogoVorschau(string? pfad)
        {
            try
            {
                if (!string.IsNullOrEmpty(pfad) && System.IO.File.Exists(pfad))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(pfad, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgLogoPreview.Source = bitmap;
                    btnLogoEntfernen.Visibility = Visibility.Visible;
                }
                else
                {
                    imgLogoPreview.Source = null;
                    btnLogoEntfernen.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                imgLogoPreview.Source = null;
                btnLogoEntfernen.Visibility = Visibility.Collapsed;
            }
        }

        private void LogoAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Firmenlogo auswaehlen",
                Filter = "Bilddateien (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                txtLogoPfad.Text = dialog.FileName;
                LadeLogoVorschau(dialog.FileName);
            }
        }

        private void LogoEntfernen_Click(object sender, RoutedEventArgs e)
        {
            txtLogoPfad.Text = "";
            imgLogoPreview.Source = null;
            btnLogoEntfernen.Visibility = Visibility.Collapsed;
        }

        private async void NovviaEinstellungenSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var einstellungen = new CoreService.NovviaEinstellungen
                {
                    PharmaModus = chkPharmaModus.IsChecked == true,
                    QuarantaeneLagerId = cmbQuarantaeneLager.SelectedValue as int? ?? 3,
                    AutoMatchSchwelle = int.TryParse(txtAutoMatchSchwelle.Text, out var schwelle) ? schwelle : 90,
                    Firmenname = txtNovviaFirmenname.Text.Trim(),
                    LogoPfad = txtLogoPfad.Text.Trim()
                };

                await _core.SaveNovviaEinstellungenAsync(einstellungen);

                txtNovviaStatus.Text = "Einstellungen gespeichert!";
                txtNovviaStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            catch (Exception ex)
            {
                txtNovviaStatus.Text = $"Fehler: {ex.Message}";
                txtNovviaStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
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

                        // Bank / FinTS
                        txtBankName.Text = config.BankName ?? "";
                        txtBankBLZ.Text = config.BankBLZ ?? "";
                        txtBankKontonummer.Text = config.BankKontonummer ?? "";
                        txtBankIBAN.Text = config.BankIBAN ?? "";
                        txtBankBIC.Text = config.BankBIC ?? "";
                        txtBankFinTSUrl.Text = config.BankFinTSUrl ?? "https://banking-sn3.s-fints-pt-sn.de/fints30";

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

                    // Bank / FinTS
                    BankName = txtBankName.Text.Trim(),
                    BankBLZ = txtBankBLZ.Text.Trim(),
                    BankKontonummer = txtBankKontonummer.Text.Trim(),
                    BankIBAN = txtBankIBAN.Text.Trim(),
                    BankBIC = txtBankBIC.Text.Trim(),
                    BankFinTSUrl = txtBankFinTSUrl.Text.Trim(),

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

                // Zeitraum berechnen (JTL-Logik)
                var (von, bis) = BerechneLogZeitraum();

                // Logs laden mit Filter
                var filter = new LogService.LogFilter
                {
                    Kategorie = GetSelectedComboText(cmbLogKategorie),
                    Modul = GetSelectedComboText(cmbLogModul),
                    Von = von,
                    Bis = bis,
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

        /// <summary>
        /// Berechnet Von/Bis basierend auf JTL-Zeitraum-Logik
        /// </summary>
        private (DateTime? Von, DateTime? Bis) BerechneLogZeitraum()
        {
            var tag = (cmbLogZeitraum.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "7";
            var heute = DateTime.Today;

            return tag switch
            {
                "0" => (heute, heute.AddDays(1)),                                    // Heute
                "1" => (heute.AddDays(-1), heute),                                   // Gestern
                "7" => (heute.AddDays(-7), null),                                    // Letzte 7 Tage
                "30" => (heute.AddDays(-30), null),                                  // Letzte 30 Tage
                "M" => (new DateTime(heute.Year, heute.Month, 1), null),             // Dieser Monat
                "LM" => (new DateTime(heute.Year, heute.Month, 1).AddMonths(-1),     // Letzter Monat
                         new DateTime(heute.Year, heute.Month, 1)),
                "Y" => (new DateTime(heute.Year, 1, 1), null),                       // Dieses Jahr
                "ALL" => (null, null),                                               // Alle
                _ => (heute.AddDays(-7), null)
            };
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

        private async System.Threading.Tasks.Task LadeLogAufbewahrungAsync()
        {
            try
            {
                var logService = App.Services.GetService<LogService>();
                if (logService != null)
                {
                    var tage = await logService.GetLogAufbewahrungTageAsync();
                    txtLogAufbewahrungTage.Text = tage.ToString();
                }
            }
            catch { /* Einstellung nicht vorhanden */ }
        }

        private async void LogAufbewahrungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtLogAufbewahrungTage.Text, out var tage) || tage < 0)
            {
                MessageBox.Show("Bitte geben Sie eine gueltige Anzahl Tage ein (0 = keine automatische Loeschung).",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var logService = App.Services.GetService<LogService>();
                if (logService != null)
                {
                    await logService.SetLogAufbewahrungTageAsync(tage);
                    MessageBox.Show($"Log-Aufbewahrung auf {tage} Tage gesetzt.", "Erfolg",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogBereinigen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logService = App.Services.GetService<LogService>();
                if (logService == null) return;

                var anzahl = await logService.GetAnzahlZuLoeschendeLogsAsync();
                if (anzahl == 0)
                {
                    MessageBox.Show("Keine alten Logs zum Bereinigen vorhanden.", "Hinweis",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show($"{anzahl} Log-Eintraege werden geloescht.\n\nFortfahren?",
                    "Logs bereinigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                var geloescht = await logService.BereinigeAlteLogs();
                MessageBox.Show($"{geloescht} Log-Eintraege wurden geloescht.", "Erfolg",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LadeLogsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Bereinigen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Benutzer & Rollen

        private List<CoreService.NovviaBenutzer>? _benutzer;
        private CoreService.NovviaBenutzer? _selectedBenutzer;
        private List<CoreService.RolleSelection>? _benutzerRollen;

        private List<CoreService.NovviaRolle>? _rollen;
        private CoreService.NovviaRolle? _selectedRolle;

        private async System.Threading.Tasks.Task LadeBenutzerAsync()
        {
            try
            {
                _benutzer = await _core.GetNovviaBenutzerAsync();
                lstBenutzer.ItemsSource = _benutzer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Benutzer laden: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LadeRollenAsync()
        {
            try
            {
                _rollen = await _core.GetNovviaRollenAsync();
                lstRollen.ItemsSource = _rollen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rollen laden: {ex.Message}");
            }
        }

        private async void LstBenutzer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstBenutzer.SelectedItem is CoreService.NovviaBenutzer benutzer)
            {
                _selectedBenutzer = benutzer;
                btnBenutzerLoeschen.IsEnabled = benutzer.KBenutzer != 1; // Admin nicht loeschbar

                txtBenutzerName.Text = benutzer.CBenutzername;
                txtBenutzerVorname.Text = benutzer.CVorname ?? "";
                txtBenutzerNachname.Text = benutzer.CNachname ?? "";
                txtBenutzerEmail.Text = benutzer.CEmail ?? "";
                txtBenutzerTelefon.Text = benutzer.CTelefon ?? "";
                txtBenutzerPasswort.Password = "";
                chkBenutzerAktiv.IsChecked = benutzer.NAktiv;
                chkBenutzerGesperrt.IsChecked = benutzer.NGesperrt;

                // Rollen laden
                _benutzerRollen = await _core.GetBenutzerRollenSelectionAsync(benutzer.KBenutzer);
                lstBenutzerRollen.ItemsSource = _benutzerRollen;

                txtBenutzerStatus.Text = $"Benutzer ID: {benutzer.KBenutzer}";
                txtBenutzerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        private async void BenutzerNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedBenutzer = null;
            btnBenutzerLoeschen.IsEnabled = false;

            txtBenutzerName.Text = "";
            txtBenutzerVorname.Text = "";
            txtBenutzerNachname.Text = "";
            txtBenutzerEmail.Text = "";
            txtBenutzerTelefon.Text = "";
            txtBenutzerPasswort.Password = "";
            chkBenutzerAktiv.IsChecked = true;
            chkBenutzerGesperrt.IsChecked = false;

            // Alle Rollen laden (keine ausgewaehlt)
            _benutzerRollen = await _core.GetBenutzerRollenSelectionAsync(0);
            lstBenutzerRollen.ItemsSource = _benutzerRollen;

            txtBenutzerName.Focus();
            txtBenutzerStatus.Text = "Neuer Benutzer - Benutzername und Passwort eingeben.";
            txtBenutzerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
        }

        private async void BenutzerLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBenutzer == null) return;
            if (_selectedBenutzer.KBenutzer == 1)
            {
                MessageBox.Show("Der Admin-Benutzer kann nicht geloescht werden.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Benutzer '{_selectedBenutzer.CBenutzername}' wirklich loeschen?",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteNovviaBenutzerAsync(_selectedBenutzer.KBenutzer);
                await LadeBenutzerAsync();
                BenutzerNeu_Click(sender, e);
                txtBenutzerStatus.Text = "Benutzer geloescht.";
                txtBenutzerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BenutzerSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBenutzerName.Text))
            {
                MessageBox.Show("Bitte einen Benutzernamen eingeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_selectedBenutzer == null)
                {
                    // Neuer Benutzer
                    if (string.IsNullOrWhiteSpace(txtBenutzerPasswort.Password))
                    {
                        MessageBox.Show("Bitte ein Passwort fuer den neuen Benutzer eingeben.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var neuerBenutzer = new CoreService.NovviaBenutzer
                    {
                        CBenutzername = txtBenutzerName.Text.Trim(),
                        CVorname = txtBenutzerVorname.Text.Trim(),
                        CNachname = txtBenutzerNachname.Text.Trim(),
                        CEmail = txtBenutzerEmail.Text.Trim(),
                        CTelefon = txtBenutzerTelefon.Text.Trim(),
                        NAktiv = chkBenutzerAktiv.IsChecked == true,
                        NGesperrt = chkBenutzerGesperrt.IsChecked == true
                    };

                    var newId = await _core.CreateNovviaBenutzerAsync(neuerBenutzer, txtBenutzerPasswort.Password);
                    txtBenutzerStatus.Text = $"Benutzer '{txtBenutzerName.Text}' angelegt (ID: {newId})!";
                }
                else
                {
                    // Bestehender Benutzer
                    _selectedBenutzer.CBenutzername = txtBenutzerName.Text.Trim();
                    _selectedBenutzer.CVorname = txtBenutzerVorname.Text.Trim();
                    _selectedBenutzer.CNachname = txtBenutzerNachname.Text.Trim();
                    _selectedBenutzer.CEmail = txtBenutzerEmail.Text.Trim();
                    _selectedBenutzer.CTelefon = txtBenutzerTelefon.Text.Trim();
                    _selectedBenutzer.NAktiv = chkBenutzerAktiv.IsChecked == true;
                    _selectedBenutzer.NGesperrt = chkBenutzerGesperrt.IsChecked == true;

                    await _core.UpdateNovviaBenutzerAsync(_selectedBenutzer);

                    // Passwort aktualisieren falls eingegeben
                    if (!string.IsNullOrWhiteSpace(txtBenutzerPasswort.Password))
                    {
                        await _core.SetBenutzerPasswortAsync(_selectedBenutzer.KBenutzer, txtBenutzerPasswort.Password);
                    }

                    // Rollen speichern
                    if (_benutzerRollen != null)
                    {
                        var selectedRollen = _benutzerRollen.Where(r => r.IsSelected).Select(r => r.KRolle).ToList();
                        await _core.SetBenutzerRollenAsync(_selectedBenutzer.KBenutzer, selectedRollen);
                    }

                    txtBenutzerStatus.Text = $"Benutzer '{txtBenutzerName.Text}' gespeichert!";
                }

                txtBenutzerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                txtBenutzerPasswort.Password = "";
                await LadeBenutzerAsync();
            }
            catch (Exception ex)
            {
                txtBenutzerStatus.Text = $"Fehler: {ex.Message}";
                txtBenutzerStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private async void BenutzerPasswortReset_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedBenutzer == null)
            {
                MessageBox.Show("Bitte zuerst einen Benutzer auswaehlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new InputDialog("Passwort zuruecksetzen", "Neues Passwort:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Ergebnis))
            {
                try
                {
                    await _core.SetBenutzerPasswortAsync(_selectedBenutzer.KBenutzer, dialog.Ergebnis);
                    MessageBox.Show($"Passwort fuer '{_selectedBenutzer.CBenutzername}' wurde zurueckgesetzt.",
                        "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BenutzerRolle_Click(object sender, RoutedEventArgs e)
        {
            // Checkbox-Click aktualisiert automatisch das IsSelected Property
        }

        // === Rollen ===

        private async void LstRollen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstRollen.SelectedItem is CoreService.NovviaRolle rolle)
            {
                try
                {
                    _selectedRolle = rolle;
                    btnRolleLoeschen.IsEnabled = !rolle.NIstSystem;

                    txtRolleName.Text = rolle.CName;
                    txtRolleBezeichnung.Text = rolle.CBezeichnung;
                    txtRolleBeschreibung.Text = rolle.CBeschreibung ?? "";
                    chkRolleAktiv.IsChecked = rolle.NAktiv;
                    chkRolleAdmin.IsChecked = rolle.NIstAdmin;
                    chkRolleSystem.IsChecked = rolle.NIstSystem;

                    // Benutzer mit dieser Rolle laden
                    var benutzerMitRolle = await _core.GetBenutzerMitRolleAsync(rolle.KRolle);
                    lstRolleBenutzer.ItemsSource = benutzerMitRolle;

                    txtRolleStatus.Text = $"Rolle ID: {rolle.KRolle}";
                    txtRolleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                }
                catch (Exception ex)
                {
                    txtRolleStatus.Text = $"Fehler: {ex.Message}";
                    txtRolleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
            }
        }

        private void RolleNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedRolle = null;
            btnRolleLoeschen.IsEnabled = false;

            txtRolleName.Text = "";
            txtRolleBezeichnung.Text = "";
            txtRolleBeschreibung.Text = "";
            chkRolleAktiv.IsChecked = true;
            chkRolleAdmin.IsChecked = false;
            chkRolleSystem.IsChecked = false;

            lstRolleBenutzer.ItemsSource = null;
            txtRolleName.Focus();
            txtRolleStatus.Text = "Neue Rolle - Name und Bezeichnung eingeben.";
            txtRolleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
        }

        private async void RolleLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRolle == null) return;
            if (_selectedRolle.NIstSystem)
            {
                MessageBox.Show("System-Rollen koennen nicht geloescht werden.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Rolle '{_selectedRolle.CBezeichnung}' wirklich loeschen?\n\n" +
                "Alle Benutzer-Zuordnungen werden ebenfalls entfernt.",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteNovviaRolleAsync(_selectedRolle.KRolle);
                await LadeRollenAsync();
                RolleNeu_Click(sender, e);
                txtRolleStatus.Text = "Rolle geloescht.";
                txtRolleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RolleSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRolleName.Text) || string.IsNullOrWhiteSpace(txtRolleBezeichnung.Text))
            {
                MessageBox.Show("Bitte Name und Bezeichnung eingeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_selectedRolle == null)
                {
                    // Neue Rolle
                    var neueRolle = new CoreService.NovviaRolle
                    {
                        CName = txtRolleName.Text.Trim(),
                        CBezeichnung = txtRolleBezeichnung.Text.Trim(),
                        CBeschreibung = txtRolleBeschreibung.Text.Trim(),
                        NAktiv = chkRolleAktiv.IsChecked == true,
                        NIstAdmin = chkRolleAdmin.IsChecked == true
                    };

                    var newId = await _core.CreateNovviaRolleAsync(neueRolle);
                    txtRolleStatus.Text = $"Rolle '{txtRolleBezeichnung.Text}' angelegt (ID: {newId})!";
                }
                else
                {
                    // Bestehende Rolle
                    _selectedRolle.CName = txtRolleName.Text.Trim();
                    _selectedRolle.CBezeichnung = txtRolleBezeichnung.Text.Trim();
                    _selectedRolle.CBeschreibung = txtRolleBeschreibung.Text.Trim();
                    _selectedRolle.NAktiv = chkRolleAktiv.IsChecked == true;
                    _selectedRolle.NIstAdmin = chkRolleAdmin.IsChecked == true;

                    await _core.UpdateNovviaRolleAsync(_selectedRolle);
                    txtRolleStatus.Text = $"Rolle '{txtRolleBezeichnung.Text}' gespeichert!";
                }

                txtRolleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                await LadeRollenAsync();
            }
            catch (Exception ex)
            {
                txtRolleStatus.Text = $"Fehler: {ex.Message}";
                txtRolleStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        #endregion

        #region Vorlagen-Import

        private List<CoreService.JtlVorlageDto> _alleJtlVorlagen = new();
        private List<CoreService.JtlVorlageDto> _alleJtlBilder = new();

        private async System.Threading.Tasks.Task LadeVorlagenAsync()
        {
            try
            {
                var vorlagen = await _core.GetJtlVorlagenAsync();
                var bilder = await _core.GetJtlBilderAsync();

                _alleJtlVorlagen = vorlagen.ToList();
                _alleJtlBilder = bilder.ToList();

                dgJtlVorlagen.ItemsSource = _alleJtlVorlagen;
                dgJtlBilder.ItemsSource = _alleJtlBilder;

                txtVorlagenAnzahl.Text = _alleJtlVorlagen.Count.ToString();
                txtBilderAnzahl.Text = _alleJtlBilder.Count.ToString();

                // E-Mail Vorlagen zaehlen
                var emailCount = await _core.GetJtlEmailVorlagenCountAsync();
                txtEmailVorlagenAnzahl.Text = emailCount.ToString();
            }
            catch (Exception ex)
            {
                txtVorlagenStatus.Text = $"Fehler: {ex.Message}";
                txtVorlagenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private void VorlagenTypFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            var filtered = _alleJtlVorlagen.AsEnumerable();

            if (cmbVorlagenTypFilter.SelectedItem is ComboBoxItem item &&
                !string.IsNullOrEmpty(item.Tag?.ToString()))
            {
                var typ = int.Parse(item.Tag.ToString()!);
                filtered = filtered.Where(v => v.VorlagenTyp == typ);
            }

            dgJtlVorlagen.ItemsSource = filtered.ToList();
        }

        private async void VorlagenAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeVorlagenAsync();
            txtVorlagenStatus.Text = "Vorlagen aktualisiert.";
            txtVorlagenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
        }

        private async void VorlagenBildExport_Click(object sender, RoutedEventArgs e)
        {
            if (dgJtlBilder.SelectedItem is not CoreService.JtlVorlageDto bild) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = bild.Name,
                Filter = "PNG Bild|*.png|JPEG Bild|*.jpg|Alle Dateien|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = await _core.GetJtlVorlageDatenAsync(bild.Id);
                    File.WriteAllBytes(dialog.FileName, data);
                    txtVorlagenStatus.Text = $"Bild exportiert: {dialog.FileName}";
                    txtVorlagenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Export:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void VorlagenAlleBilderExport_Click(object sender, RoutedEventArgs e)
        {
            // Zielordner abfragen
            var targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JTL-Bilder");
            var result = MessageBox.Show(
                $"Alle {_alleJtlBilder.Count} Bilder werden nach folgendem Ordner exportiert:\n\n{targetPath}\n\nFortfahren?",
                "Bilder exportieren",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Directory.CreateDirectory(targetPath);
                    int count = 0;
                    foreach (var bild in _alleJtlBilder)
                    {
                        var data = await _core.GetJtlVorlageDatenAsync(bild.Id);
                        var filename = Path.Combine(targetPath, $"{bild.Name}.png");
                        File.WriteAllBytes(filename, data);
                        count++;
                    }
                    txtVorlagenStatus.Text = $"{count} Bilder exportiert nach: {targetPath}";
                    txtVorlagenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);

                    // Ordner oeffnen
                    System.Diagnostics.Process.Start("explorer.exe", targetPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Export:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void VorlagenBildImport_Click(object sender, RoutedEventArgs e)
        {
            if (dgJtlBilder.SelectedItem is not CoreService.JtlVorlageDto bild)
            {
                MessageBox.Show("Bitte waehlen Sie zuerst ein Bild aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var data = await _core.GetJtlVorlageDatenAsync(bild.Id);

                // In NOVVIA.FormularBild speichern
                await _core.SaveFormularBildAsync(bild.Name, data);

                txtVorlagenStatus.Text = $"Bild '{bild.Name}' in NOVVIA importiert.";
                txtVorlagenStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Import:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Grid-Formatierung

        private async void GridEinstellungenSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Grid-Einstellungen zusammenstellen
                var settings = new Dictionary<string, string>
                {
                    // Allgemein
                    { "Grid.Zeilenhoehe", txtGridZeilenhoehe.Text },
                    { "Grid.Schriftgroesse", txtGridSchriftgroesse.Text },
                    { "Grid.Schriftart", (cmbGridSchriftart.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI" },
                    { "Grid.GridLines", (cmbGridLinien.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All" },

                    // Header
                    { "Grid.HeaderHoehe", txtGridHeaderHoehe.Text },
                    { "Grid.HeaderBackground", txtGridHeaderBg.Text },
                    { "Grid.HeaderForeground", txtGridHeaderFg.Text },
                    { "Grid.HeaderFontWeight", (cmbGridHeaderFontWeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SemiBold" },
                    { "Grid.HeaderBorderColor", txtGridHeaderBorderColor.Text },
                    { "Grid.HeaderBorderWidth", txtGridHeaderBorderWidth.Text },

                    // Zeile 1
                    { "Grid.RowBackground", txtGridRowBg.Text },
                    { "Grid.RowForeground", txtGridRowFg.Text },
                    { "Grid.RowFontWeight", (cmbGridRowFontWeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal" },
                    { "Grid.RowBorderColor", txtGridRowBorderColor.Text },
                    { "Grid.RowBorderWidth", txtGridRowBorderWidth.Text },

                    // Zeile 2 (Alternierend)
                    { "Grid.RowAltBackground", txtGridRowAltBg.Text },
                    { "Grid.RowAltForeground", txtGridRowAltFg.Text },
                    { "Grid.RowAltFontWeight", (cmbGridRowAltFontWeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal" },
                    { "Grid.ZebraStreifen", (chkGridZebraStreifen.IsChecked == true).ToString() },

                    // Selektion
                    { "Grid.SelectionBackground", txtGridSelectionBg.Text },
                    { "Grid.SelectionForeground", txtGridSelectionFg.Text },

                    // Datumsformatierung
                    { "Grid.DatumFormat", (cmbDatumFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "dd.MM.yyyy" },
                    { "Grid.DatumZeitFormat", (cmbDatumZeitFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "dd.MM.yyyy HH:mm" },
                    { "Grid.ZeitFormat", (cmbZeitFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "HH:mm" },

                    // Zahlenformatierung
                    { "Grid.Tausendertrennzeichen", (cmbTausenderTrenner.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "." },
                    { "Grid.Dezimaltrennzeichen", (cmbDezimalTrenner.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "," },
                    { "Grid.Dezimalstellen", txtDezimalstellen.Text },
                    { "Grid.WaehrungSymbol", (cmbWaehrungSymbol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EUR" },
                    { "Grid.WaehrungAnzeigen", (chkWaehrungAnzeigen.IsChecked == true).ToString() }
                };

                // Speichern in NOVVIA.BenutzerEinstellung
                foreach (var kv in settings)
                {
                    await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, kv.Key, kv.Value);
                }

                // GridStyleHelper aktualisieren
                await Controls.Base.GridStyleHelper.Instance.LoadSettingsAsync(_core, App.BenutzerId);

                txtGridStatus.Text = "Einstellungen gespeichert und angewendet.";
                txtGridStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);

                // Vorschau aktualisieren
                ApplyGridFormatierungToVorschau();
                UpdateFormatVorschau();
            }
            catch (Exception ex)
            {
                txtGridStatus.Text = $"Fehler: {ex.Message}";
                txtGridStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }

        private void UpdateFormatVorschau()
        {
            try
            {
                var datumFormat = (cmbDatumFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "dd.MM.yyyy";
                var tausender = (cmbTausenderTrenner.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (tausender == "(kein)") tausender = "";
                var dezimal = (cmbDezimalTrenner.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? ",";
                var waehrung = (cmbWaehrungSymbol.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "EUR";

                txtFormatVorschauDatum.Text = DateTime.Now.ToString(datumFormat);

                var culture = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
                culture.NumberFormat.NumberGroupSeparator = tausender ?? ".";
                culture.NumberFormat.NumberDecimalSeparator = dezimal;

                var betrag = 1234.56m.ToString("N2", culture);
                txtFormatVorschauBetrag.Text = chkWaehrungAnzeigen.IsChecked == true ? $"{betrag} {waehrung}" : betrag;
                txtFormatVorschauMenge.Text = 42m.ToString("N0", culture);
            }
            catch { }
        }

        private void GridVorschauAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            ApplyGridFormatierungToVorschau();
            txtGridStatus.Text = "Vorschau aktualisiert.";
            txtGridStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
        }

        private void ApplyGridFormatierungToVorschau()
        {
            try
            {
                // Zeilenhoehe
                if (double.TryParse(txtGridZeilenhoehe.Text, out var rowHeight))
                    dgGridVorschau.RowHeight = rowHeight;

                // Header-Hoehe
                if (double.TryParse(txtGridHeaderHoehe.Text, out var headerHeight))
                    dgGridVorschau.ColumnHeaderHeight = headerHeight;

                // Schriftgroesse
                if (double.TryParse(txtGridSchriftgroesse.Text, out var fontSize))
                    dgGridVorschau.FontSize = fontSize;

                // Schriftart
                var fontFamily = (cmbGridSchriftart.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
                dgGridVorschau.FontFamily = new System.Windows.Media.FontFamily(fontFamily);

                // Gitternetzlinien
                var gridLines = (cmbGridLinien.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
                dgGridVorschau.GridLinesVisibility = gridLines switch
                {
                    "Horizontal" => DataGridGridLinesVisibility.Horizontal,
                    "Vertical" => DataGridGridLinesVisibility.Vertical,
                    "None" => DataGridGridLinesVisibility.None,
                    _ => DataGridGridLinesVisibility.All
                };

                // Gitternetzlinien-Farbe (verwende Row Border Color)
                try
                {
                    var lineColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridRowBorderColor.Text);
                    dgGridVorschau.HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(lineColor);
                    dgGridVorschau.VerticalGridLinesBrush = new System.Windows.Media.SolidColorBrush(lineColor);
                }
                catch { }

                // Zeilen-Hintergrund
                try
                {
                    var rowBg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridRowBg.Text);
                    dgGridVorschau.RowBackground = new System.Windows.Media.SolidColorBrush(rowBg);
                }
                catch { }

                // Zebrastreifen
                if (chkGridZebraStreifen.IsChecked == true)
                {
                    try
                    {
                        var altColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridRowAltBg.Text);
                        dgGridVorschau.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(altColor);
                    }
                    catch { }
                }
                else
                {
                    dgGridVorschau.AlternatingRowBackground = null;
                }

                // Row Style mit Textfarbe und Schriftstaerke
                var rowStyle = new Style(typeof(DataGridRow));
                try
                {
                    var rowFg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridRowFg.Text);
                    rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new System.Windows.Media.SolidColorBrush(rowFg)));
                }
                catch { }
                var rowFontWeight = (cmbGridRowFontWeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal";
                rowStyle.Setters.Add(new Setter(DataGridRow.FontWeightProperty, rowFontWeight == "Bold" ? FontWeights.Bold : rowFontWeight == "SemiBold" ? FontWeights.SemiBold : FontWeights.Normal));

                // Selektion Trigger
                var selectionTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
                try
                {
                    var selBg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridSelectionBg.Text);
                    var selFg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridSelectionFg.Text);
                    selectionTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new System.Windows.Media.SolidColorBrush(selBg)));
                    selectionTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, new System.Windows.Media.SolidColorBrush(selFg)));
                }
                catch { }
                rowStyle.Triggers.Add(selectionTrigger);
                dgGridVorschau.RowStyle = rowStyle;

                // Header Style
                var headerStyle = new Style(typeof(DataGridColumnHeader));
                try
                {
                    var headerBg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridHeaderBg.Text);
                    var headerFg = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridHeaderFg.Text);
                    var headerBorder = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(txtGridHeaderBorderColor.Text);
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new System.Windows.Media.SolidColorBrush(headerBg)));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new System.Windows.Media.SolidColorBrush(headerFg)));
                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(headerBorder)));

                    if (double.TryParse(txtGridHeaderBorderWidth.Text, out var borderWidth))
                        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, borderWidth, borderWidth)));

                    headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));
                }
                catch { }
                var headerFontWeight = (cmbGridHeaderFontWeight.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SemiBold";
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, headerFontWeight == "Bold" ? FontWeights.Bold : headerFontWeight == "SemiBold" ? FontWeights.SemiBold : FontWeights.Normal));
                dgGridVorschau.ColumnHeaderStyle = headerStyle;

                // Beispieldaten fuer Vorschau
                dgGridVorschau.ItemsSource = new[]
                {
                    new { Artikelnr = "ART-001", Bezeichnung = "Beispielartikel 1", Lagerbestand = 42, VKNetto = 19.99m },
                    new { Artikelnr = "ART-002", Bezeichnung = "Beispielartikel 2", Lagerbestand = 15, VKNetto = 29.50m },
                    new { Artikelnr = "ART-003", Bezeichnung = "Beispielartikel 3", Lagerbestand = 0, VKNetto = 9.95m },
                    new { Artikelnr = "ART-004", Bezeichnung = "Beispielartikel 4", Lagerbestand = 128, VKNetto = 149.00m }
                };
            }
            catch (Exception ex)
            {
                txtGridStatus.Text = $"Vorschau-Fehler: {ex.Message}";
                txtGridStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            }
        }

        private async void GridEinstellungenReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Alle Einstellungen auf Standard zuruecksetzen?",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Allgemeine Standardwerte
                txtGridZeilenhoehe.Text = "18";
                txtGridSchriftgroesse.Text = "12";
                cmbGridSchriftart.SelectedIndex = 0; // Segoe UI
                cmbGridLinien.SelectedIndex = 0; // All

                // Header Standardwerte
                txtGridHeaderHoehe.Text = "32";
                txtGridHeaderBg.Text = "#f0f0f0";
                txtGridHeaderFg.Text = "#333333";
                cmbGridHeaderFontWeight.SelectedIndex = 1; // SemiBold
                txtGridHeaderBorderColor.Text = "#cccccc";
                txtGridHeaderBorderWidth.Text = "1";

                // Zeile 1 Standardwerte
                txtGridRowBg.Text = "#ffffff";
                txtGridRowFg.Text = "#333333";
                cmbGridRowFontWeight.SelectedIndex = 0; // Normal
                txtGridRowBorderColor.Text = "#e0e0e0";
                txtGridRowBorderWidth.Text = "1";

                // Zeile 2 Standardwerte
                txtGridRowAltBg.Text = "#f9f9f9";
                txtGridRowAltFg.Text = "#333333";
                cmbGridRowAltFontWeight.SelectedIndex = 0; // Normal
                chkGridZebraStreifen.IsChecked = true;

                // Selektion Standardwerte
                txtGridSelectionBg.Text = "#0078D4";
                txtGridSelectionFg.Text = "#ffffff";

                // Datum Standardwerte
                cmbDatumFormat.SelectedIndex = 0; // dd.MM.yyyy
                cmbDatumZeitFormat.SelectedIndex = 0; // dd.MM.yyyy HH:mm
                cmbZeitFormat.SelectedIndex = 0; // HH:mm

                // Zahlen Standardwerte
                cmbTausenderTrenner.SelectedIndex = 0; // .
                cmbDezimalTrenner.SelectedIndex = 0; // ,
                txtDezimalstellen.Text = "2";
                cmbWaehrungSymbol.SelectedIndex = 0; // EUR
                chkWaehrungAnzeigen.IsChecked = true;

                // Speichern
                GridEinstellungenSpeichern_Click(sender, e);

                txtGridStatus.Text = "Alle Einstellungen auf Standard zurueckgesetzt.";
                txtGridStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
        }

        private async System.Threading.Tasks.Task LadeGridEinstellungenAsync()
        {
            try
            {
                var settings = await _core.GetBenutzerEinstellungenAsync(App.BenutzerId, "Grid.");

                foreach (var setting in settings)
                {
                    switch (setting.Key)
                    {
                        // Allgemein
                        case "Grid.Zeilenhoehe":
                            txtGridZeilenhoehe.Text = setting.Value;
                            break;
                        case "Grid.Schriftgroesse":
                            txtGridSchriftgroesse.Text = setting.Value;
                            break;
                        case "Grid.Schriftart":
                            SelectComboByContent(cmbGridSchriftart, setting.Value);
                            break;
                        case "Grid.GridLines":
                            SelectComboByTag(cmbGridLinien, setting.Value);
                            break;

                        // Header
                        case "Grid.HeaderHoehe":
                            txtGridHeaderHoehe.Text = setting.Value;
                            break;
                        case "Grid.HeaderBackground":
                            txtGridHeaderBg.Text = setting.Value;
                            break;
                        case "Grid.HeaderForeground":
                            txtGridHeaderFg.Text = setting.Value;
                            break;
                        case "Grid.HeaderFontWeight":
                            SelectComboByContent(cmbGridHeaderFontWeight, setting.Value);
                            break;
                        case "Grid.HeaderBorderColor":
                            txtGridHeaderBorderColor.Text = setting.Value;
                            break;
                        case "Grid.HeaderBorderWidth":
                            txtGridHeaderBorderWidth.Text = setting.Value;
                            break;

                        // Zeile 1
                        case "Grid.RowBackground":
                            txtGridRowBg.Text = setting.Value;
                            break;
                        case "Grid.RowForeground":
                            txtGridRowFg.Text = setting.Value;
                            break;
                        case "Grid.RowFontWeight":
                            SelectComboByContent(cmbGridRowFontWeight, setting.Value);
                            break;
                        case "Grid.RowBorderColor":
                            txtGridRowBorderColor.Text = setting.Value;
                            break;
                        case "Grid.RowBorderWidth":
                            txtGridRowBorderWidth.Text = setting.Value;
                            break;

                        // Zeile 2
                        case "Grid.RowAltBackground":
                            txtGridRowAltBg.Text = setting.Value;
                            break;
                        case "Grid.RowAltForeground":
                            txtGridRowAltFg.Text = setting.Value;
                            break;
                        case "Grid.RowAltFontWeight":
                            SelectComboByContent(cmbGridRowAltFontWeight, setting.Value);
                            break;
                        case "Grid.ZebraStreifen":
                            chkGridZebraStreifen.IsChecked = setting.Value == "True";
                            break;

                        // Selektion
                        case "Grid.SelectionBackground":
                            txtGridSelectionBg.Text = setting.Value;
                            break;
                        case "Grid.SelectionForeground":
                            txtGridSelectionFg.Text = setting.Value;
                            break;

                        // Datumsformatierung
                        case "Grid.DatumFormat":
                            SelectComboByContent(cmbDatumFormat, setting.Value);
                            break;
                        case "Grid.DatumZeitFormat":
                            SelectComboByContent(cmbDatumZeitFormat, setting.Value);
                            break;
                        case "Grid.ZeitFormat":
                            SelectComboByContent(cmbZeitFormat, setting.Value);
                            break;

                        // Zahlenformatierung
                        case "Grid.Tausendertrennzeichen":
                            SelectComboByContent(cmbTausenderTrenner, setting.Value);
                            break;
                        case "Grid.Dezimaltrennzeichen":
                            SelectComboByContent(cmbDezimalTrenner, setting.Value);
                            break;
                        case "Grid.Dezimalstellen":
                            txtDezimalstellen.Text = setting.Value;
                            break;
                        case "Grid.WaehrungSymbol":
                            SelectComboByContent(cmbWaehrungSymbol, setting.Value);
                            break;
                        case "Grid.WaehrungAnzeigen":
                            chkWaehrungAnzeigen.IsChecked = setting.Value == "True";
                            break;
                    }
                }

                // GridStyleHelper aktualisieren
                await Controls.Base.GridStyleHelper.Instance.LoadSettingsAsync(_core, App.BenutzerId);

                // Vorschau aktualisieren
                ApplyGridFormatierungToVorschau();
                UpdateFormatVorschau();
            }
            catch
            {
                // Bei Fehler: Standardwerte beibehalten
            }
        }

        private void SelectComboByContent(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if ((combo.Items[i] as ComboBoxItem)?.Content?.ToString() == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectComboByTag(ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if ((combo.Items[i] as ComboBoxItem)?.Tag?.ToString() == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        #endregion

        #region Nummernkreise

        private List<CoreService.Nummernkreis> _nummernkreise = new();
        private bool _nummernkreiseGeaendert = false;

        private async void BtnNummernkreiseAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeNummernkreiseAsync();
        }

        private async Task LadeNummernkreiseAsync()
        {
            try
            {
                _nummernkreise = (await _core.GetNummernkreiseAsync()).ToList();
                dgNummernkreise.ItemsSource = _nummernkreise;
                _nummernkreiseGeaendert = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Nummernkreise:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgNummernkreise_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            _nummernkreiseGeaendert = true;
        }

        private async void BtnNummernkreiseSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_nummernkreiseGeaendert)
            {
                MessageBox.Show("Keine Aenderungen zum Speichern.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                foreach (var nk in _nummernkreise)
                {
                    await _core.UpdateNummernkreisAsync(nk);
                }
                MessageBox.Show("Nummernkreise wurden gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                _nummernkreiseGeaendert = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Vorgangsfarben

        private List<CoreService.Vorgangsfarbe> _vorgangsfarben = new();

        private async void BtnVorgangsfarbenAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeVorgangsfarbenAsync();
        }

        private async Task LadeVorgangsfarbenAsync()
        {
            try
            {
                _vorgangsfarben = (await _core.GetVorgangsfarbenAsync()).ToList();
                dgVorgangsfarben.ItemsSource = _vorgangsfarben;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Vorgangsfarben:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgVorgangsfarben_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnVorgangsfarbeLoeschen.IsEnabled = dgVorgangsfarben.SelectedItem != null;
        }

        private async void BtnVorgangsfarbeNeu_Click(object sender, RoutedEventArgs e)
        {
            var neueFarbe = new CoreService.Vorgangsfarbe
            {
                CBedeutung = "Neue Farbe",
                NRotwert = 128,
                NGruenwert = 128,
                NBlauwert = 128,
                NAlphawert = 255,
                NAktiv = true
            };

            try
            {
                var id = await _core.SaveVorgangsfarbeAsync(neueFarbe);
                neueFarbe.KVorgangsfarbe = id;
                _vorgangsfarben.Add(neueFarbe);
                dgVorgangsfarben.ItemsSource = null;
                dgVorgangsfarben.ItemsSource = _vorgangsfarben;
                dgVorgangsfarben.SelectedItem = neueFarbe;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnVorgangsfarbeLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgVorgangsfarben.SelectedItem is not CoreService.Vorgangsfarbe farbe) return;

            var result = MessageBox.Show($"Vorgangsfarbe '{farbe.CBedeutung}' wirklich loeschen?",
                "Loeschen bestaetigen", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteVorgangsfarbeAsync(farbe.KVorgangsfarbe);
                _vorgangsfarben.Remove(farbe);
                dgVorgangsfarben.ItemsSource = null;
                dgVorgangsfarben.ItemsSource = _vorgangsfarben;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnVorgangsfarbenSpeichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var farbe in _vorgangsfarben)
                {
                    await _core.SaveVorgangsfarbeAsync(farbe);
                }
                MessageBox.Show("Vorgangsfarben wurden gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
