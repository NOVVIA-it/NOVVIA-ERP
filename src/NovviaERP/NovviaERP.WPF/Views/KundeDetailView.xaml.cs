using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;
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
                    // Spalten-Konfiguration für alle DataGrids
                    DataGridColumnConfig.EnableColumnChooser(dgAdressen, "KundeDetailView.Adressen");
                    DataGridColumnConfig.EnableColumnChooser(dgAnsprechpartner, "KundeDetailView.Ansprechpartner");
                    DataGridColumnConfig.EnableColumnChooser(dgBankverbindungen, "KundeDetailView.Bankverbindungen");
                    DataGridColumnConfig.EnableColumnChooser(dgOnlineshop, "KundeDetailView.Onlineshop");
                    DataGridColumnConfig.EnableColumnChooser(dgEigeneFelder, "KundeDetailView.EigeneFelder");
                    DataGridColumnConfig.EnableColumnChooser(dgAuftraege, "KundeDetailView.Auftraege");
                    DataGridColumnConfig.EnableColumnChooser(dgRechnungen, "KundeDetailView.Rechnungen");
                    DataGridColumnConfig.EnableColumnChooser(dgGutschriften, "KundeDetailView.Gutschriften");

                    await LadeReferenzdatenAsync();
                    await LadeKundeAsync();
                    await LadeValidierungAsync();
                    if (_kundeId.HasValue)
                    {
                        await pnlTextmeldungen.LoadAsync("Kunde", _kundeId.Value, "Stammdaten");
                        await pnlTextmeldungen.ShowPopupAsync("Kunde", _kundeId.Value, "Stammdaten", txtFirma.Text);
                    }
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
                txtStatus.Text = "Lade Belege...";
                await LadeKundenBelegeAsync();

                txtStatus.Text = $"Kunde {_kunde.CKundenNr} geladen - {dgAuftraege.Items.Count} Aufträge, {dgRechnungen.Items.Count} Rechnungen";
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
                    SELECT a.kAuftrag AS KAuftrag, a.cAuftragsNr AS CAuftragsNr,
                           a.dErstellt AS DErstellt, a.nAuftragStatus AS NAuftragStatus,
                           ISNULL(ae.fWertNetto, 0) AS FGesamtNetto,
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
                    SELECT r.kRechnung AS KRechnung, r.cRechnungsnr AS CRechnungsNr,
                           r.dErstellt AS DErstellt,
                           ISNULL(re.fVkBruttoGesamt, 0) AS FGesamtBrutto,
                           ISNULL(re.nZahlungStatus, 0) AS NZahlungStatus,
                           CASE ISNULL(re.nZahlungStatus, 0)
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

                // Gutschriften laden (aus dbo.tgutschrift)
                var gutschriften = await conn.QueryAsync<KundeGutschriftItem>(@"
                    SELECT g.kGutschrift AS KGutschrift,
                           g.cGutschriftNr AS CGutschriftNr,
                           g.dErstellt AS DErstellt,
                           ISNULL(g.fPreis, 0) AS FGesamtBrutto
                    FROM dbo.tgutschrift g
                    WHERE g.kKunde = @KundeId AND g.nStorno = 0
                    ORDER BY g.dErstellt DESC",
                    new { KundeId = _kundeId.Value });
                var gutschriftenListe = gutschriften.ToList();
                dgGutschriften.ItemsSource = gutschriftenListe;
                txtGutschriftenAnzahl.Text = $"{gutschriftenListe.Count} Gutschriften";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Kundenbelege: {ex.Message}");
                txtStatus.Text = $"Fehler Belege: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden der Belege:\n\n{ex.Message}\n\nStackTrace:\n{ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}",
                    "Fehler Belege", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AuftragNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is KundeAuftragItem auftrag)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    var view = new BestellungDetailView();
                    view.LadeBestellung(auftrag.KAuftrag);
                    main.ShowContent(view);
                }
            }
        }

        private void OeffneAuftrag()
        {
            var selected = dgAuftraege.SelectedItem;
            if (selected == null)
            {
                MessageBox.Show("Bitte einen Auftrag auswählen", "Info");
                return;
            }

            if (selected is KundeAuftragItem auftrag)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    var view = new BestellungDetailView();
                    view.LadeBestellung(auftrag.KAuftrag);
                    main.ShowContent(view);
                }
            }
            else
            {
                MessageBox.Show($"Unbekannter Typ: {selected.GetType().Name}", "Debug");
            }
        }

        private void RechnungNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is KundeRechnungItem rechnung)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    var view = new RechnungDetailView(rechnung.KRechnung);
                    main.ShowContent(view);
                }
            }
        }

        private void OeffneRechnung()
        {
            var selected = dgRechnungen.SelectedItem;
            if (selected == null)
            {
                MessageBox.Show("Bitte eine Rechnung auswählen", "Info");
                return;
            }

            if (selected is KundeRechnungItem rechnung)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    var view = new RechnungDetailView(rechnung.KRechnung);
                    main.ShowContent(view);
                }
            }
            else
            {
                MessageBox.Show($"Unbekannter Typ: {selected.GetType().Name}", "Debug");
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
            public int KGutschrift { get; set; }
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

        private async void AdresseHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_kundeId.HasValue)
            {
                MessageBox.Show("Bitte erst den Kunden speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dto = new AdresseDto { KKunde = _kundeId.Value, NTyp = 1 };
            var dialog = new AdresseBearbeitenDialog(dto, "Neue Adresse");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                try
                {
                    var adresse = new CoreService.AdresseDetail
                    {
                        KKunde = _kundeId.Value,
                        NTyp = (byte)dto.NTyp,
                        NStandard = (byte)dto.NStandard,
                        CFirma = dto.Firma,
                        CAnrede = dto.Anrede,
                        CTitel = dto.Titel,
                        CVorname = dto.Vorname,
                        CName = dto.Nachname,
                        CStrasse = dto.Strasse,
                        CAdressZusatz = dto.Adresszusatz,
                        CPLZ = dto.PLZ,
                        COrt = dto.Ort,
                        CLand = dto.Land,
                        CTel = dto.Telefon,
                        CMobil = dto.Mobil,
                        CFax = dto.Fax,
                        CMail = dto.Email
                    };
                    await _coreService.AddKundeAdresseAsync(_kundeId.Value, adresse);
                    await LadeKundeAsync(); // Neu laden
                    MessageBox.Show("Adresse wurde hinzugefuegt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AdresseBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgAdressen.SelectedItem is not CoreService.AdresseDetail adr)
            {
                MessageBox.Show("Bitte eine Adresse auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dto = new AdresseDto
            {
                KAdresse = adr.KAdresse,
                KKunde = adr.KKunde,
                NTyp = adr.NTyp,
                NStandard = adr.NStandard,
                Firma = adr.CFirma,
                Anrede = adr.CAnrede,
                Titel = adr.CTitel,
                Vorname = adr.CVorname,
                Nachname = adr.CName,
                Strasse = adr.CStrasse,
                Adresszusatz = adr.CAdressZusatz,
                PLZ = adr.CPLZ,
                Ort = adr.COrt,
                Land = adr.CLand,
                Telefon = adr.CTel,
                Mobil = adr.CMobil,
                Fax = adr.CFax,
                Email = adr.CMail
            };

            var dialog = new AdresseBearbeitenDialog(dto, "Adresse bearbeiten");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                try
                {
                    adr.NTyp = (byte)dto.NTyp;
                    adr.NStandard = (byte)dto.NStandard;
                    adr.CFirma = dto.Firma;
                    adr.CAnrede = dto.Anrede;
                    adr.CTitel = dto.Titel;
                    adr.CVorname = dto.Vorname;
                    adr.CName = dto.Nachname;
                    adr.CStrasse = dto.Strasse;
                    adr.CAdressZusatz = dto.Adresszusatz;
                    adr.CPLZ = dto.PLZ;
                    adr.COrt = dto.Ort;
                    adr.CLand = dto.Land;
                    adr.CTel = dto.Telefon;
                    adr.CMobil = dto.Mobil;
                    adr.CFax = dto.Fax;
                    adr.CMail = dto.Email;

                    await _coreService.UpdateKundeAdresseAsync(adr);
                    await LadeKundeAsync();
                    MessageBox.Show("Adresse wurde aktualisiert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AdresseLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgAdressen.SelectedItem is not CoreService.AdresseDetail adr)
            {
                MessageBox.Show("Bitte eine Adresse auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Adresse wirklich loeschen?\n\n{adr.CStrasse}\n{adr.CPLZ} {adr.COrt}",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.DeleteKundeAdresseAsync(adr.KAdresse);
                    await LadeKundeAsync();
                    MessageBox.Show("Adresse wurde geloescht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Ansprechpartner Buttons

        private async void AnsprechpartnerHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_kundeId.HasValue)
            {
                MessageBox.Show("Bitte erst den Kunden speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dto = new AnsprechpartnerDto { KKunde = _kundeId.Value };
            var dialog = new AnsprechpartnerDialog(dto, "Neuer Ansprechpartner");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                try
                {
                    var ap = new CoreService.AnsprechpartnerDetail
                    {
                        KKunde = _kundeId.Value,
                        CAnrede = dto.Anrede,
                        CVorname = dto.Vorname,
                        CName = dto.Nachname,
                        CAbteilung = dto.Abteilung,
                        CTel = dto.Telefon,
                        CMobil = dto.Mobil,
                        CFax = dto.Fax,
                        CMail = dto.Email
                    };
                    await _coreService.AddKundeAnsprechpartnerAsync(_kundeId.Value, ap);
                    await LadeKundeAsync();
                    MessageBox.Show("Ansprechpartner wurde hinzugefuegt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AnsprechpartnerBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgAnsprechpartner.SelectedItem is not CoreService.AnsprechpartnerDetail ap)
            {
                MessageBox.Show("Bitte einen Ansprechpartner auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dto = new AnsprechpartnerDto
            {
                KAnsprechpartner = ap.KAnsprechpartner,
                KKunde = ap.KKunde,
                Anrede = ap.CAnrede,
                Vorname = ap.CVorname,
                Nachname = ap.CName,
                Abteilung = ap.CAbteilung,
                Telefon = ap.CTel,
                Mobil = ap.CMobil,
                Fax = ap.CFax,
                Email = ap.CMail
            };

            var dialog = new AnsprechpartnerDialog(dto, "Ansprechpartner bearbeiten");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                try
                {
                    ap.CAnrede = dto.Anrede;
                    ap.CVorname = dto.Vorname;
                    ap.CName = dto.Nachname;
                    ap.CAbteilung = dto.Abteilung;
                    ap.CTel = dto.Telefon;
                    ap.CMobil = dto.Mobil;
                    ap.CFax = dto.Fax;
                    ap.CMail = dto.Email;

                    await _coreService.UpdateKundeAnsprechpartnerAsync(ap);
                    await LadeKundeAsync();
                    MessageBox.Show("Ansprechpartner wurde aktualisiert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void AnsprechpartnerLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgAnsprechpartner.SelectedItem is not CoreService.AnsprechpartnerDetail ap)
            {
                MessageBox.Show("Bitte einen Ansprechpartner auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Ansprechpartner wirklich loeschen?\n\n{ap.CVorname} {ap.CName}",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.DeleteKundeAnsprechpartnerAsync(ap.KAnsprechpartner);
                    await LadeKundeAsync();
                    MessageBox.Show("Ansprechpartner wurde geloescht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Bankverbindung Buttons

        private async void BankverbindungHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_kundeId.HasValue)
            {
                MessageBox.Show("Bitte erst den Kunden speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dto = new BankverbindungDto { KKunde = _kundeId.Value };
            var dialog = new BankverbindungDialog(dto, "Neue Bankverbindung");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                try
                {
                    var bv = new CoreService.BankverbindungDetail
                    {
                        KKunde = _kundeId.Value,
                        NStandard = (byte)dto.NStandard,
                        CBankName = dto.BankName,
                        CInhaber = dto.Inhaber,
                        CIBAN = dto.IBAN,
                        CBIC = dto.BIC,
                        CBLZ = dto.BLZ,
                        CKontoNr = dto.KontoNr
                    };
                    await _coreService.AddKundeBankverbindungAsync(_kundeId.Value, bv);
                    await LadeKundeAsync();
                    MessageBox.Show("Bankverbindung wurde hinzugefuegt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BankverbindungBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgBankverbindungen.SelectedItem is not CoreService.BankverbindungDetail bv)
            {
                MessageBox.Show("Bitte eine Bankverbindung auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dto = new BankverbindungDto
            {
                KKontoDaten = bv.KKontoDaten,
                KKunde = bv.KKunde,
                NStandard = bv.NStandard,
                BankName = bv.CBankName,
                Inhaber = bv.CInhaber,
                IBAN = bv.CIBAN,
                BIC = bv.CBIC,
                BLZ = bv.CBLZ,
                KontoNr = bv.CKontoNr
            };

            var dialog = new BankverbindungDialog(dto, "Bankverbindung bearbeiten");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                try
                {
                    bv.NStandard = (byte)dto.NStandard;
                    bv.CBankName = dto.BankName;
                    bv.CInhaber = dto.Inhaber;
                    bv.CIBAN = dto.IBAN;
                    bv.CBIC = dto.BIC;
                    bv.CBLZ = dto.BLZ;
                    bv.CKontoNr = dto.KontoNr;

                    await _coreService.UpdateKundeBankverbindungAsync(bv);
                    await LadeKundeAsync();
                    MessageBox.Show("Bankverbindung wurde aktualisiert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BankverbindungLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBankverbindungen.SelectedItem is not CoreService.BankverbindungDetail bv)
            {
                MessageBox.Show("Bitte eine Bankverbindung auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Bankverbindung wirklich loeschen?\n\n{bv.CBankName}\n{bv.CIBAN}",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.DeleteKundeBankverbindungAsync(bv.KKontoDaten);
                    await LadeKundeAsync();
                    MessageBox.Show("Bankverbindung wurde geloescht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                // Versuche Zurueck-Navigation ueber Stack
                if (!main.NavigateBack())
                {
                    // Fallback zur Kundenliste
                    main.ShowContent(App.Services.GetRequiredService<KundenView>(), pushToStack: false);
                }
            }
        }
    }
}
