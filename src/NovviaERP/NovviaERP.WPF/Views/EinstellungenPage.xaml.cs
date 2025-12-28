using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EinstellungenPage : Page
    {
        private readonly CoreService _core;
        private int? _selectedKundengruppeId;
        private int? _selectedKundenkategorieId;
        private int? _selectedZahlungsartId;
        private int? _selectedVersandartId;

        public EinstellungenPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
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

        private async void KundengruppeLöschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKundengruppen.SelectedItem is not CoreService.KundengruppeDetail kg) return;

            if (MessageBox.Show($"Kundengruppe '{kg.CName}' wirklich löschen?", "Bestätigung",
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

        private async void KundenkategorieLöschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKundenkategorien.SelectedItem is not CoreService.KundenkategorieRef kk) return;

            if (MessageBox.Show($"Kundenkategorie '{kk.CName}' wirklich löschen?", "Bestätigung",
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

        private async void ZahlungsartLöschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgZahlungsarten.SelectedItem is not CoreService.ZahlungsartDetail za) return;

            if (MessageBox.Show($"Zahlungsart '{za.CName}' wirklich löschen?", "Bestätigung",
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

        private async void VersandartLöschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgVersandarten.SelectedItem is not CoreService.VersandartDetail va) return;

            if (MessageBox.Show($"Versandart '{va.CName}' wirklich löschen?", "Bestätigung",
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

        #endregion
    }
}
