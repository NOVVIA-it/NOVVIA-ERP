using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EinstellungenView : UserControl
    {
        private readonly CoreService _core;
        private int? _selectedKundengruppeId;
        private int? _selectedKundenkategorieId;
        private int? _selectedZahlungsartId;
        private int? _selectedVersandartId;
        private int? _selectedLieferantAttributId;

        public EinstellungenView()
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
                await LadeEigeneFelderAsync();
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
                System.Diagnostics.Debug.WriteLine($"Lieferant Attribute geladen: {liste.Count} Einträge");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lieferant Attribute FEHLER: {ex.Message}");
                // NOVVIA.LieferantAttribut Tabelle fehlt - bitte Setup-EigeneFelderLieferant.sql ausführen
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
            txtLiefAttrName.Focus();
        }

        private void LieferantAttributBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgLieferantAttribute.SelectedItem is CoreService.EigenesFeldDefinition attr)
            {
                _selectedLieferantAttributId = attr.KAttribut;
                txtLiefAttrName.Text = attr.CName ?? "";
                txtLiefAttrBeschr.Text = attr.CBeschreibung ?? "";
                // Typ auswählen (1=Text, 2=Ganzzahl, 3=Dezimal, 4=Datum)
                cmbLiefAttrTyp.SelectedIndex = attr.NFeldTyp switch
                {
                    1 => 0, // Text
                    2 => 1, // Ganzzahl
                    3 => 2, // Dezimal
                    4 => 3, // Datum
                    _ => 0
                };
                txtLiefAttrSort.Text = attr.NSortierung.ToString();
                txtLiefAttrName.Focus();
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
                var feldTyp = int.Parse((cmbLiefAttrTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1");

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

                MessageBox.Show("Attribut gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
