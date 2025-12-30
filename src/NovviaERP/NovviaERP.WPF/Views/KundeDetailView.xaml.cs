using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;
using Dapper;

namespace NovviaERP.WPF.Views
{
    public partial class KundeDetailView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly int? _kundeId;
        private readonly int? _returnToBestellungId;
        private CoreService.KundeDetail? _kunde;

        public KundeDetailView(int? kundeId, int? returnToBestellungId = null)
        {
            InitializeComponent();
            _kundeId = kundeId;
            _returnToBestellungId = returnToBestellungId;
            _coreService = App.Services.GetRequiredService<CoreService>();
            txtTitel.Text = kundeId.HasValue ? "Kunde bearbeiten" : "Neuer Kunde";
            Loaded += async (s, e) =>
            {
                try
                {
                    await LadeReferenzdatenAsync();
                    await LadeKundeAsync();
                    await LadeValidierungAsync();
                }
                catch (Exception ex)
                {
                    txtStatus.Text = $"Fehler: {ex.Message}";
                    MessageBox.Show($"Fehler beim Laden:\n\n{ex.Message}\n\n{ex.StackTrace}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private async System.Threading.Tasks.Task LadeReferenzdatenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Referenzdaten...";

                // Kundengruppen
                var kundengruppen = await _coreService.GetKundengruppenAsync();
                cmbKundengruppe.Items.Clear();
                cmbKundengruppe.Items.Add(new ComboBoxItem { Content = "", Tag = (int?)null });
                foreach (var kg in kundengruppen)
                {
                    cmbKundengruppe.Items.Add(new ComboBoxItem { Content = kg.CName, Tag = kg.KKundenGruppe });
                }

                // Kundenkategorien
                var kundenkategorien = await _coreService.GetKundenkategorienAsync();
                cmbKundenkategorie.Items.Clear();
                cmbKundenkategorie.Items.Add(new ComboBoxItem { Content = "", Tag = (int?)null });
                foreach (var kk in kundenkategorien)
                {
                    cmbKundenkategorie.Items.Add(new ComboBoxItem { Content = kk.CName, Tag = kk.KKundenKategorie });
                }

                // Zahlungsarten
                var zahlungsarten = await _coreService.GetZahlungsartenAsync();
                cmbZahlungsart.Items.Clear();
                cmbZahlungsart.Items.Add(new ComboBoxItem { Content = "", Tag = (int?)null });
                foreach (var za in zahlungsarten)
                {
                    cmbZahlungsart.Items.Add(new ComboBoxItem { Content = za.CName, Tag = za.KZahlungsart });
                }

                // Laender
                cmbLand.Items.Clear();
                cmbLand.Items.Add("Deutschland");
                cmbLand.Items.Add("Oesterreich");
                cmbLand.Items.Add("Schweiz");
                cmbLand.SelectedIndex = 0;

                txtStatus.Text = "";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler bei Referenzdaten: {ex.Message}";
                MessageBox.Show($"Fehler bei Referenzdaten:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LadeKundeAsync()
        {
            if (!_kundeId.HasValue)
            {
                txtStatus.Text = "Neuer Kunde";
                return;
            }

            try
            {
                txtStatus.Text = $"Lade Kundendaten (ID: {_kundeId.Value})...";
                _kunde = await _coreService.GetKundeByIdAsync(_kundeId.Value);
                if (_kunde == null)
                {
                    txtStatus.Text = $"Kunde {_kundeId.Value} nicht gefunden";
                    MessageBox.Show($"Kunde mit ID {_kundeId.Value} wurde nicht gefunden!",
                        "Nicht gefunden", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtStatus.Text = $"Kunde geladen: {_kunde.CKundenNr}";

                // Header
                txtTitel.Text = $"Kunde - {_kunde.StandardAdresse?.CFirma ?? $"{_kunde.StandardAdresse?.CVorname} {_kunde.StandardAdresse?.CName}".Trim()}";
                txtKundenNrHeader.Text = $"Kd-Nr: {_kunde.CKundenNr}";

                // Allgemeine Informationen
                txtKundenNr.Text = _kunde.CKundenNr ?? "";
                txtDebitorenNr.Text = _kunde.NDebitorennr > 0 ? _kunde.NDebitorennr.ToString() : "";

                // Standard-Adresse in Kundendaten
                var adr = _kunde.StandardAdresse;
                if (adr != null)
                {
                    txtFirma.Text = adr.CFirma ?? "";
                    txtFirmenzusatz.Text = adr.CZusatz ?? "";
                    SetComboBoxByContent(cmbAnrede, adr.CAnrede);
                    txtTitel2.Text = adr.CTitel ?? "";
                    txtVorname.Text = adr.CVorname ?? "";
                    txtNachname.Text = adr.CName ?? "";
                    txtStrasse.Text = adr.CStrasse ?? "";
                    txtAdresszusatz.Text = adr.CAdressZusatz ?? "";
                    txtPLZ.Text = adr.CPLZ ?? "";
                    txtOrt.Text = adr.COrt ?? "";
                    SetComboBoxByContent(cmbLand, adr.CLand ?? "Deutschland");

                    // Kontaktdaten
                    txtEmail.Text = adr.CMail ?? "";
                    txtTelefon.Text = adr.CTel ?? "";
                    txtMobil.Text = adr.CMobil ?? "";
                    txtFax.Text = adr.CFax ?? "";
                    txtUstId.Text = adr.CUSTID ?? "";
                }

                txtWebseite.Text = _kunde.CWWW ?? "";
                txtSteuernr.Text = _kunde.CSteuerNr ?? "";

                // Interne Daten
                chkKassenkunde.IsChecked = _kunde.CKassenKunde == "J";
                chkGesperrt.IsChecked = _kunde.CSperre == "J";
                txtKundeSeit.Text = _kunde.DErstellt?.ToString("dd.MM.yyyy") ?? "";
                SetComboBoxByTag(cmbKundengruppe, _kunde.KKundenGruppe);
                SetComboBoxByTag(cmbKundenkategorie, _kunde.KKundenKategorie);
                txtHerkunft.Text = _kunde.CHerkunft ?? "";

                // Zahlungen
                chkMahnstopp.IsChecked = _kunde.NMahnstopp == 1;
                txtZahlungsziel.Text = _kunde.NZahlungsziel?.ToString() ?? "";
                txtRabatt.Text = _kunde.FRabatt.ToString("N2");
                SetComboBoxByTag(cmbZahlungsart, _kunde.KZahlungsart);
                txtKreditlimit.Text = _kunde.NKreditlimit.ToString();

                // Statistik
                txtAnzahlBestellungen.Text = _kunde.AnzahlBestellungen.ToString();
                txtGesamtUmsatz.Text = $"{_kunde.GesamtUmsatz:N2} EUR";

                // DataGrids
                dgAdressen.ItemsSource = _kunde.Adressen;
                dgAnsprechpartner.ItemsSource = _kunde.Ansprechpartner;
                dgBankverbindungen.ItemsSource = _kunde.Bankverbindungen;
                dgOnlineshop.ItemsSource = _kunde.OnlineshopKunden;

                // Aufträge, Rechnungen, Gutschriften laden
                await LadeKundenBelegeAsync();

                txtStatus.Text = $"Kunde {_kunde.CKundenNr} geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden der Kundendaten:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LadeKundenBelegeAsync()
        {
            if (!_kundeId.HasValue) return;

            try
            {
                var db = App.Services.GetRequiredService<JtlDbContext>();
                var conn = await db.GetConnectionAsync();

                // Aufträge laden
                var auftraege = await conn.QueryAsync<KundeAuftragItem>(@"
                    SELECT a.kAuftrag, a.cAuftragsNr, a.dErstellt, a.nAuftragStatus,
                           ae.fVKNettoGesamt AS FGesamtNetto,
                           CASE a.nAuftragStatus
                               WHEN 1 THEN 'Offen'
                               WHEN 2 THEN 'InBearb.'
                               WHEN 3 THEN 'Versendet'
                               WHEN 4 THEN 'Bezahlt'
                               ELSE 'Sonst.'
                           END AS StatusText
                    FROM Verkauf.tAuftrag a
                    LEFT JOIN Verkauf.tAuftragEckdaten ae ON a.kAuftrag = ae.kAuftrag
                    WHERE a.kKunde = @KundeId AND a.nStorno = 0
                    ORDER BY a.dErstellt DESC",
                    new { KundeId = _kundeId.Value });
                var auftraegeListe = auftraege.ToList();
                dgAuftraege.ItemsSource = auftraegeListe;
                txtAuftraegeAnzahl.Text = $"{auftraegeListe.Count} Auftraege";

                // Rechnungen laden
                var rechnungen = await conn.QueryAsync<KundeRechnungItem>(@"
                    SELECT r.kRechnung, r.cRechnungsnr AS CRechnungsNr, r.dErstellt,
                           re.fVKBruttoGesamt AS FGesamtBrutto, re.nZahlungStatus,
                           CASE re.nZahlungStatus
                               WHEN 1 THEN 'Offen'
                               WHEN 2 THEN 'Bezahlt'
                               ELSE 'Sonst.'
                           END AS StatusText
                    FROM Rechnung.tRechnung r
                    LEFT JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    WHERE r.kKunde = @KundeId AND r.nStorno = 0
                    ORDER BY r.dErstellt DESC",
                    new { KundeId = _kundeId.Value });
                var rechnungenListe = rechnungen.ToList();
                dgRechnungen.ItemsSource = rechnungenListe;
                txtRechnungenAnzahl.Text = $"{rechnungenListe.Count} Rechnungen";

                // Gutschriften laden (Rechnungskorrekturen)
                var gutschriften = await conn.QueryAsync<KundeGutschriftItem>(@"
                    SELECT rk.kRechnungKorrektur, rk.cKorrekturNr AS CGutschriftNr, rk.dErstellt,
                           rk.fBruttoGesamt AS FGesamtBrutto
                    FROM Rechnung.tRechnungKorrektur rk
                    JOIN Rechnung.tRechnung r ON rk.kRechnung = r.kRechnung
                    WHERE r.kKunde = @KundeId
                    ORDER BY rk.dErstellt DESC",
                    new { KundeId = _kundeId.Value });
                var gutschriftenListe = gutschriften.ToList();
                dgGutschriften.ItemsSource = gutschriftenListe;
                txtGutschriftenAnzahl.Text = $"{gutschriftenListe.Count} Gutschriften";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Kundenbelege: {ex.Message}");
            }
        }

        private void DgAuftraege_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgAuftraege.SelectedItem is KundeAuftragItem auftrag)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    var view = new BestellungDetailView();
                    view.LadeBestellung(auftrag.KAuftrag);
                    main.ShowContent(view);
                }
            }
        }

        private void DgRechnungen_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgRechnungen.SelectedItem is KundeRechnungItem rechnung)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    var view = new RechnungDetailView(rechnung.KRechnung);
                    main.ShowContent(view);
                }
            }
        }

        // DTOs für Kundenbelege
        private class KundeAuftragItem
        {
            public int KAuftrag { get; set; }
            public string? CAuftragsNr { get; set; }
            public DateTime? DErstellt { get; set; }
            public int NAuftragStatus { get; set; }
            public decimal FGesamtNetto { get; set; }
            public string? StatusText { get; set; }
        }

        private class KundeRechnungItem
        {
            public int KRechnung { get; set; }
            public string? CRechnungsNr { get; set; }
            public DateTime? DErstellt { get; set; }
            public decimal FGesamtBrutto { get; set; }
            public int NZahlungStatus { get; set; }
            public string? StatusText { get; set; }
        }

        private class KundeGutschriftItem
        {
            public int KRechnungKorrektur { get; set; }
            public string? CGutschriftNr { get; set; }
            public DateTime? DErstellt { get; set; }
            public decimal FGesamtBrutto { get; set; }
        }

        private void SetComboBoxByContent(ComboBox cmb, string? content)
        {
            if (string.IsNullOrEmpty(content)) return;
            foreach (var item in cmb.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Content?.ToString() == content)
                {
                    cmb.SelectedItem = item;
                    return;
                }
                if (item is string s && s == content)
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
        }

        private void SetComboBoxByTag(ComboBox cmb, int? tag)
        {
            if (!tag.HasValue) { cmb.SelectedIndex = 0; return; }
            foreach (var item in cmb.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is int t && t == tag.Value)
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
        }

        #region Validierung (JTL Eigene Felder)

        private async System.Threading.Tasks.Task LadeValidierungAsync()
        {
            if (!_kundeId.HasValue) return;

            try
            {
                var felder = await _coreService.GetKundeEigeneFelderAsync(_kundeId.Value);

                chkValAmbient.IsChecked = GetBoolWert(felder, "Ambient");
                chkValCool.IsChecked = GetBoolWert(felder, "Cool");
                chkValMedcan.IsChecked = GetBoolWert(felder, "Medcan");
                chkValTierarznei.IsChecked = GetBoolWert(felder, "Tierarznei");

                if (felder.TryGetValue("QualifiziertAm", out var qualDatum) && DateTime.TryParse(qualDatum, out var dt))
                {
                    dpQualifiziertAm.SelectedDate = dt;
                }

                txtQualifiziertVon.Text = felder.TryGetValue("QualifiziertVon", out var qualVon) ? qualVon ?? "" : "";
                txtGDP.Text = felder.TryGetValue("GDP", out var gdp) ? gdp ?? "" : "";
                txtGMP.Text = felder.TryGetValue("GMP", out var gmp) ? gmp ?? "" : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Validierungsfelder: {ex.Message}");
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

        private async void ValidierungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_kundeId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Kunden speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var felder = new Dictionary<string, string?>
                {
                    ["Ambient"] = chkValAmbient.IsChecked == true ? "1" : "0",
                    ["Cool"] = chkValCool.IsChecked == true ? "1" : "0",
                    ["Medcan"] = chkValMedcan.IsChecked == true ? "1" : "0",
                    ["Tierarznei"] = chkValTierarznei.IsChecked == true ? "1" : "0",
                    ["QualifiziertAm"] = dpQualifiziertAm.SelectedDate?.ToString("yyyy-MM-dd"),
                    ["QualifiziertVon"] = txtQualifiziertVon.Text.Trim(),
                    ["GDP"] = txtGDP.Text.Trim(),
                    ["GMP"] = txtGMP.Text.Trim()
                };

                await _coreService.SetKundeEigeneFelderAsync(_kundeId.Value, felder);
                MessageBox.Show("Validierungsfelder wurden gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Speichern

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Speichere...";

                if (_kunde == null)
                {
                    // Neuer Kunde
                    _kunde = new CoreService.KundeDetail();
                }

                // Kunde-Felder
                _kunde.CWWW = txtWebseite.Text.Trim();
                _kunde.CSteuerNr = txtSteuernr.Text.Trim();
                _kunde.CKassenKunde = chkKassenkunde.IsChecked == true ? "J" : "N";
                _kunde.CSperre = chkGesperrt.IsChecked == true ? "J" : "N";
                _kunde.KKundenGruppe = (cmbKundengruppe.SelectedItem as ComboBoxItem)?.Tag as int?;
                _kunde.KKundenKategorie = (cmbKundenkategorie.SelectedItem as ComboBoxItem)?.Tag as int?;
                _kunde.CHerkunft = txtHerkunft.Text.Trim();
                _kunde.NMahnstopp = (byte)(chkMahnstopp.IsChecked == true ? 1 : 0);
                if (int.TryParse(txtZahlungsziel.Text, out var zz)) _kunde.NZahlungsziel = zz;
                if (decimal.TryParse(txtRabatt.Text, out var rabatt)) _kunde.FRabatt = rabatt;
                _kunde.KZahlungsart = (cmbZahlungsart.SelectedItem as ComboBoxItem)?.Tag as int?;
                if (int.TryParse(txtKreditlimit.Text, out var kl)) _kunde.NKreditlimit = kl;

                // Standard-Adresse
                var adr = _kunde.StandardAdresse ?? new CoreService.AdresseDetail();
                adr.CFirma = txtFirma.Text.Trim();
                adr.CZusatz = txtFirmenzusatz.Text.Trim();
                adr.CAnrede = (cmbAnrede.SelectedItem as ComboBoxItem)?.Content?.ToString();
                adr.CTitel = txtTitel2.Text.Trim();
                adr.CVorname = txtVorname.Text.Trim();
                adr.CName = txtNachname.Text.Trim();
                adr.CStrasse = txtStrasse.Text.Trim();
                adr.CAdressZusatz = txtAdresszusatz.Text.Trim();
                adr.CPLZ = txtPLZ.Text.Trim();
                adr.COrt = txtOrt.Text.Trim();
                adr.CLand = cmbLand.SelectedItem?.ToString() ?? "Deutschland";
                adr.CMail = txtEmail.Text.Trim();
                adr.CTel = txtTelefon.Text.Trim();
                adr.CMobil = txtMobil.Text.Trim();
                adr.CFax = txtFax.Text.Trim();
                adr.CUSTID = txtUstId.Text.Trim();

                if (_kundeId.HasValue)
                {
                    await _coreService.UpdateKundeAsync(_kunde);
                    // TODO: Adresse separat speichern
                    txtStatus.Text = "Gespeichert";
                }
                else
                {
                    var neueId = await _coreService.CreateKundeAsync(_kunde, adr);
                    txtStatus.Text = $"Kunde {neueId} erstellt";
                }

                MessageBox.Show("Kunde wurde gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Fehler beim Speichern";
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Adressen Buttons

        private void AdresseHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Adresse hinzufuegen - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AdresseBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgAdressen.SelectedItem is CoreService.AdresseDetail adr)
            {
                MessageBox.Show($"Adresse bearbeiten: {adr.CStrasse}, {adr.COrt}\n\nNoch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AdresseLoeschen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Adresse loeschen - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Ansprechpartner Buttons

        private void AnsprechpartnerHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ansprechpartner hinzufuegen - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AnsprechpartnerBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ansprechpartner bearbeiten - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AnsprechpartnerLoeschen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Ansprechpartner loeschen - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Bankverbindung Buttons

        private void BankverbindungHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Bankverbindung hinzufuegen - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BankverbindungBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Bankverbindung bearbeiten - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BankverbindungLoeschen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Bankverbindung loeschen - noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                // Wenn wir von einer Bestellung kommen, zurück zur Bestellung
                if (_returnToBestellungId.HasValue)
                {
                    var bestellungView = new BestellungDetailView();
                    bestellungView.LadeBestellung(_returnToBestellungId.Value);
                    main.ShowContent(bestellungView);
                }
                else
                {
                    main.ShowContent(App.Services.GetRequiredService<KundenView>());
                }
            }
        }
    }
}
