using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Infrastructure.Jtl;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;
using NovviaERP.WPF.Controls.Base;
using System.Threading.Tasks;
using Dapper;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungDetailView : UserControl
    {
        private readonly CoreService _core;
        private readonly JtlDbContext _db;
        private CoreService.BestellungDetail? _bestellung;
        private int _kundeId;
        private Dictionary<string, string> _origEigeneFelder = new();
        private bool _hatRechnung = false;
        private bool _isNeuerAuftrag = false;
        private CoreService.KundeUebersicht? _selectedKunde;
        private List<CoreService.VersandartRef>? _versandarten;
        private List<CoreService.KundeAdresseKurz>? _kundenAdressen;
        private CoreService.KundeAdresseKurz? _selectedLieferadresse;

        public BestellungDetailView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _db = App.Services.GetRequiredService<JtlDbContext>();
            Loaded += async (s, e) =>
            {
                await GridStyleHelper.InitializeGridAsync(dgEigeneFelder, "BestellungDetail.EigeneFelder", _core, App.BenutzerId);
            };
        }

        /// <summary>
        /// Initialisiert die View für einen neuen Auftrag
        /// </summary>
        public async void LadeNeuerAuftrag()
        {
            _isNeuerAuftrag = true;
            _bestellung = null;
            _selectedKunde = null;

            // Header
            txtTitel.Text = "Neuer Auftrag";
            txtBestellNr.Text = "(wird automatisch vergeben)";
            txtKundeName.Text = "Kein Kunde ausgewählt";
            txtStatusBadge.Text = "Neu";
            brdStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17a2b8"));
            txtSubtitel.Text = "Bitte wählen Sie einen Kunden und fügen Sie Positionen hinzu.";

            // Felder leeren
            txtFirma.Text = "";
            txtFirma.IsReadOnly = false;
            txtBestellNrDetail.Text = "(wird automatisch vergeben)";
            txtDatum.Text = DateTime.Now.ToString("dd.MM.yyyy");
            txtKundenNr.Text = "Klicken zum Auswählen...";
            txtKundenNr.Cursor = Cursors.Hand;
            txtKundengruppe.Text = "";
            txtExterneNr.Text = "";
            txtRechnungsNr.Text = "";

            // Adressen leeren
            txtRechnungsadresse.Text = "Bitte Kunde auswählen";
            txtLieferadresse.Text = "Bitte Kunde auswählen";

            // Details leeren
            txtDetailVorgangsstatus.Text = "Auftrag freigegeben";
            txtSteuerart.Text = "Steuerpflichtige Lieferung";
            txtDetailZahlungsart.Text = "";
            txtZahlungsziel.Text = "14";
            txtSkontoProzent.Text = "0";
            txtSkontoTage.Text = "0";
            txtVoraussLieferdatum.Text = "";
            txtDetailVersandart.Text = "";
            txtDetailZusatzgewicht.Text = "0,000";
            txtLieferprioritaet.Text = "0";

            // Stammdaten laden und Dropdowns aktivieren
            await LadeStammdatenFuerNeuerAuftrag();

            // Positionen leeren
            positionenControl.SetPositionen(new List<PositionViewModel>());

            // Zusammenfassung
            txtSummeNetto.Text = "0,00 EUR";
            txtMwSt.Text = "0,00 EUR";
            txtGesamtBrutto.Text = "0,00 EUR";
            txtGewinn.Text = "0,00 EUR";
            txtKosten.Text = "0,00 EUR";

            // Texte-Felder editierbar machen
            txtTextAnmerkung.IsReadOnly = false;
            txtTextAnmerkung.Background = Brushes.White;
            txtDrucktext.IsReadOnly = false;
            txtDrucktext.Background = Brushes.White;
            txtHinweis.IsReadOnly = false;
            txtVorgangsstatus.IsReadOnly = false;
            txtVorgangsstatus.Background = Brushes.White;

            // Voraussichtliches Lieferdatum editierbar
            txtVoraussLieferdatum.IsReadOnly = false;
            txtVoraussLieferdatum.Background = Brushes.White;

            // Vorgangsfarbe aktivieren
            cmbVorgangsfarbe.IsEnabled = true;

            // Buttons anpassen
            btnLieferschein.IsEnabled = false;
            btnRechnung.IsEnabled = false;
            btnRechnungDL.IsEnabled = false;
            btnLieferantenbestellung.IsEnabled = false;
            btnSpeichern.Content = "Auftrag anlegen";

            txtStatus.Text = "Neuer Auftrag - Bitte Kunde auswählen";
        }

        private async Task LadeStammdatenFuerNeuerAuftrag()
        {
            try
            {
                // Versandarten laden
                _versandarten = (await _core.GetVersandartenAsync()).ToList();
                cmbVersandart.ItemsSource = _versandarten;
                if (_versandarten.Any())
                    cmbVersandart.SelectedIndex = 0;

                // Zahlungsarten laden
                var zahlungsarten = (await _core.GetZahlungsartenAsync()).ToList();
                cmbZahlungsart.ItemsSource = zahlungsarten;
                if (zahlungsarten.Any())
                    cmbZahlungsart.SelectedIndex = 0;

                // Dropdowns sichtbar machen, TextBoxen verstecken
                txtDetailVersandart.Visibility = Visibility.Collapsed;
                cmbVersandart.Visibility = Visibility.Visible;
                txtDetailZahlungsart.Visibility = Visibility.Collapsed;
                cmbZahlungsart.Visibility = Visibility.Visible;

                // Zahlungsziel editierbar machen
                txtZahlungsziel.IsReadOnly = false;
                txtZahlungsziel.Background = Brushes.White;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler beim Laden der Stammdaten: {ex.Message}";
            }
        }

        private async void KundenNrAuswaehlen_Click()
        {
            if (!_isNeuerAuftrag) return;

            var dialog = new KundenSuchDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true && dialog.SelectedKunde != null)
            {
                _selectedKunde = dialog.SelectedKunde;
                _kundeId = _selectedKunde.KKunde;

                // UI aktualisieren
                txtKundeName.Text = $"{_selectedKunde.Anzeigename} ({_selectedKunde.CKundenNr})";
                txtFirma.Text = _selectedKunde.CFirma ?? _selectedKunde.Anzeigename ?? "";
                txtKundenNr.Text = _selectedKunde.CKundenNr ?? _selectedKunde.KKunde.ToString();
                txtKundengruppe.Text = _selectedKunde.Kundengruppe ?? "";

                // Adressen laden
                _kundenAdressen = (await _core.GetKundeAdressenKurzAsync(_kundeId)).ToList();
                var hauptAdresse = _kundenAdressen.FirstOrDefault(a => a.NStandard) ?? _kundenAdressen.FirstOrDefault();
                if (hauptAdresse != null)
                {
                    _selectedLieferadresse = hauptAdresse;
                    var adressText = FormatAdresse(_selectedKunde.CFirma,
                        $"{_selectedKunde.CVorname} {_selectedKunde.CName}".Trim(),
                        hauptAdresse.CStrasse, hauptAdresse.CPLZ, hauptAdresse.COrt, hauptAdresse.CISO);
                    txtRechnungsadresse.Text = adressText;
                    txtLieferadresse.Text = adressText;

                    // MwSt basierend auf Lieferland berechnen
                    await BerechneUndAktualisiereMwStAsync(hauptAdresse.CISO);
                }

                txtStatus.Text = $"Kunde {_selectedKunde.CKundenNr} ausgewählt";
            }
        }

        /// <summary>
        /// Berechnet die MwSt basierend auf dem Lieferland und aktualisiert die Positionen
        /// </summary>
        private async Task BerechneUndAktualisiereMwStAsync(string? lieferLandIso)
        {
            try
            {
                // Standard: Deutschland = 19% MwSt
                // EU-Länder ohne USt-ID: 19% MwSt
                // EU-Länder mit USt-ID: 0% MwSt (innergemeinschaftliche Lieferung)
                // Drittländer: 0% MwSt (Ausfuhrlieferung)

                decimal mwstSatz = 19.0m; // Standard Deutschland

                if (!string.IsNullOrEmpty(lieferLandIso) && lieferLandIso != "DE" && lieferLandIso != "Deutschland")
                {
                    // EU-Länder Liste
                    var euLaender = new[] { "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
                        "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE" };

                    if (euLaender.Contains(lieferLandIso.ToUpper()))
                    {
                        // EU-Land - prüfen ob Kunde USt-ID hat
                        if (_selectedKunde != null)
                        {
                            var kundeDetail = await _core.GetKundeByIdAsync(_selectedKunde.KKunde);
                            if (!string.IsNullOrEmpty(kundeDetail?.CUstIdNr))
                            {
                                mwstSatz = 0m; // Innergemeinschaftliche Lieferung
                                txtSteuerart.Text = "Innergemeinschaftliche Lieferung";
                            }
                            else
                            {
                                mwstSatz = 19m; // EU ohne USt-ID
                                txtSteuerart.Text = "Steuerpflichtige Lieferung (EU)";
                            }
                        }
                    }
                    else
                    {
                        // Drittland - Ausfuhrlieferung
                        mwstSatz = 0m;
                        txtSteuerart.Text = "Ausfuhrlieferung (Drittland)";
                    }
                }
                else
                {
                    txtSteuerart.Text = "Steuerpflichtige Lieferung";
                }

                // Positionen aktualisieren mit neuem MwSt-Satz
                var positionen = positionenControl.GetPositionen();
                foreach (var pos in positionen)
                {
                    pos.MwStSatz = mwstSatz;
                }
                positionenControl.SetPositionen(positionen);
                UpdateSummenFromControl();

                txtStatus.Text = $"MwSt-Satz: {mwstSatz}% für Lieferland {lieferLandIso}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler bei MwSt-Berechnung: {ex.Message}";
            }
        }

        public async void LadeBestellung(int bestellungId)
        {
            try
            {
                txtStatus.Text = "Lade Bestellung...";
                _bestellung = await _core.GetBestellungByIdAsync(bestellungId);

                if (_bestellung == null)
                {
                    MessageBox.Show("Bestellung nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Zurueck_Click(this, new RoutedEventArgs());
                    return;
                }

                // Header (JTL-Style)
                txtTitel.Text = "Auftrag";
                txtBestellNr.Text = _bestellung.CBestellNr;
                txtKundeName.Text = $"{_bestellung.KundeFirma ?? _bestellung.KundeName} ({_bestellung.CKundenNr})";
                txtStatusBadge.Text = _bestellung.CStatus ?? "Offen";
                SetStatusBadgeColor(_bestellung.CStatus);

                // Auftragsdaten - Referenzen
                txtFirma.Text = _bestellung.KundeFirma ?? _bestellung.KundeName ?? "";
                txtBestellNrDetail.Text = _bestellung.CBestellNr;
                txtDatum.Text = _bestellung.DErstellt.ToString("dd.MM.yyyy");
                txtKundenNr.Text = _bestellung.CKundenNr;
                txtKundengruppe.Text = ""; // TODO: Kundengruppe laden
                txtExterneNr.Text = _bestellung.CInetBestellNr ?? "";
                txtRechnungsNr.Text = ""; // TODO: Rechnungsnummer laden
                txtEigeneUstId.Text = ""; // TODO: Eigene USt-Id
                txtKundenUstId.Text = ""; // TODO: USt-ID aus Kunde laden

                // Status ComboBox
                foreach (ComboBoxItem item in cmbStatus.Items)
                {
                    if (item.Tag?.ToString() == _bestellung.CStatus)
                    {
                        cmbStatus.SelectedItem = item;
                        break;
                    }
                }

                // Kunde ID merken
                _kundeId = _bestellung.TKunde_KKunde;

                // Kunden-Textmeldungen laden
                if (_kundeId > 0)
                {
                    await pnlTextmeldungen.LoadAsync("Kunde", _kundeId, "Verkauf");
                    await pnlTextmeldungen.ShowPopupAsync("Kunde", _kundeId, "Verkauf", _bestellung.KundeFirma ?? _bestellung.KundeName ?? "");
                }

                // Adressen
                var ra = _bestellung.Rechnungsadresse;
                var la = _bestellung.Lieferadresse;
                txtRechnungsadresse.Text = FormatAdresse(ra?.CFirma, $"{ra?.CVorname} {ra?.CName}".Trim(),
                    ra?.CStrasse, ra?.CPLZ, ra?.COrt, ra?.CLand);
                txtLieferadresse.Text = FormatAdresse(la?.CFirma, $"{la?.CVorname} {la?.CName}".Trim(),
                    la?.CStrasse, la?.CPLZ, la?.COrt, la?.CLand);

                // Details-Tab (JTL-Style) befuellen
                // Auftragsstatus
                txtDetailVorgangsstatus.Text = _bestellung.VorgangsstatusName ?? "Auftrag freigegeben";
                txtRueckhaltegrund.Text = _bestellung.RueckhaltegrundName ?? "";

                // Steuern
                txtSteuerart.Text = _bestellung.SteuerartName ?? "Steuerpflichtige Lieferung";

                // Zahlung
                txtDetailZahlungsart.Text = _bestellung.ZahlungsartName ?? "";
                txtZahlungsziel.Text = _bestellung.NZahlungsZiel.ToString();
                txtSkontoProzent.Text = _bestellung.FSkonto.ToString("N1");
                txtSkontoTage.Text = _bestellung.NSkontoTage.ToString();

                // Versand
                txtVoraussLieferdatum.Text = _bestellung.DVoraussichtlichesLieferdatum?.ToString("dd.MM.yyyy") ?? "";
                txtDetailVersandart.Text = _bestellung.VersandartName ?? "";
                txtDetailZusatzgewicht.Text = _bestellung.FZusatzGewicht.ToString("N3");
                txtLieferprioritaet.Text = _bestellung.NLieferPrioritaet.ToString();

                // Referenzen
                txtDetailSprache.Text = _bestellung.SpracheName ?? "Deutsch";

                // Verkauftexte (aus Verkauf.tAuftragText - fuer Texte-Tab)
                txtTextAnmerkung.Text = _bestellung.CAnmerkung ?? "";
                txtDrucktext.Text = _bestellung.CDrucktext ?? "";
                txtHinweis.Text = _bestellung.CHinweis ?? "";
                txtVorgangsstatus.Text = _bestellung.CVorgangsstatus ?? "";

                // Positionen - Convert to PositionViewModel
                var posViewModels = (_bestellung.Positionen ?? new List<CoreService.BestellPositionDetail>())
                    .Select((p, idx) => new PositionViewModel
                    {
                        Id = p.KBestellPos,
                        ArtikelId = p.TArtikel_KArtikel,
                        ArtNr = p.CArtNr ?? "",
                        Bezeichnung = p.CName ?? "",
                        Hinweis = p.CHinweis ?? "",
                        Menge = p.FAnzahl,
                        Einheit = p.CEinheit ?? "Stk",
                        EinzelpreisNetto = p.FVKNetto,
                        MwStSatz = p.FMwSt,
                        Rabatt = p.FRabatt ?? 0,
                        PosTyp = p.NPosTyp ?? 0,
                        Sort = idx + 1
                    }).ToList();
                positionenControl.SetPositionen(posViewModels);

                // Zusammenfassung - Gewichte
                decimal artikelGewicht = 0;
                if (_bestellung.Positionen != null)
                {
                    // TODO: Gewichte aus Artikeln laden
                }
                txtArtikelgewicht.Text = $"{artikelGewicht:N3} kg";
                txtVersandgewicht.Text = "0,000 kg"; // TODO
                txtGesamtgewicht.Text = $"{artikelGewicht:N3} kg";

                // Zusammenfassung - Auftragswert
                var summeNetto = _bestellung.GesamtNetto;
                var summeBrutto = _bestellung.GesamtBrutto;
                var mwst = summeBrutto - summeNetto;

                txtSummeNetto.Text = $"{summeNetto:N2} EUR";
                txtMwSt.Text = $"{mwst:N2} EUR";
                txtGesamtBrutto.Text = $"{summeBrutto:N2} EUR";
                txtGewinn.Text = "0,00 EUR"; // TODO: Gewinn berechnen
                txtKosten.Text = "0,00 EUR"; // TODO: Kosten

                // Prüfen ob aktive Rechnung vorhanden -> Bearbeitung sperren
                // Status 5 = Storniert -> erlaubt Bearbeitung
                var rechnungen = await _core.GetRechnungenAsync(bestellungId);
                var aktiveRechnungen = rechnungen.Where(r => r.NRechnungStatus != 5).ToList();
                var stornierteRechnungen = rechnungen.Where(r => r.NRechnungStatus == 5).ToList();

                _hatRechnung = aktiveRechnungen.Any();

                if (_hatRechnung)
                {
                    var rechnungsNummern = string.Join(", ", aktiveRechnungen.Select(r => r.CRechnungsnr));
                    txtRechnungsNr.Text = rechnungsNummern;
                    SperreBearbeitung(true);
                    txtStatus.Text = $"Auftrag {_bestellung.CBestellNr} - Rechnung vorhanden (Bearbeitung gesperrt)";
                    txtSubtitel.Text = $"Rechnung(en) vorhanden: {rechnungsNummern} - Änderungen nicht möglich";
                }
                else if (stornierteRechnungen.Any())
                {
                    var stornierteNummern = string.Join(", ", stornierteRechnungen.Select(r => r.CRechnungsnr));
                    txtRechnungsNr.Text = $"(Storniert: {stornierteNummern})";
                    SperreBearbeitung(false);
                    txtStatus.Text = $"Auftrag {_bestellung.CBestellNr} - Rechnung storniert (Bearbeitung möglich)";
                }
                else
                {
                    SperreBearbeitung(false);
                    txtStatus.Text = $"Auftrag {_bestellung.CBestellNr} geladen";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetStatusBadgeColor(string? status)
        {
            brdStatus.Background = status switch
            {
                "Offen" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffc107")),
                "In Bearbeitung" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17a2b8")),
                "Bezahlt" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28a745")),
                "Versendet" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007bff")),
                "Storniert" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc3545")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6c757d"))
            };
        }

        private string FormatAdresse(string? firma, string? name, string? strasse, string? plz, string? ort, string? land = null)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(firma)) lines.Add(firma);
            if (!string.IsNullOrWhiteSpace(name) && name != firma) lines.Add(name);
            if (!string.IsNullOrWhiteSpace(strasse)) lines.Add(strasse);
            if (!string.IsNullOrWhiteSpace(plz) || !string.IsNullOrWhiteSpace(ort))
                lines.Add($"{plz} {ort}".Trim());
            if (!string.IsNullOrWhiteSpace(land) && land != "Deutschland" && land != "DE")
                lines.Add(land);
            return lines.Count > 0 ? string.Join("\n", lines) : "-";
        }

        /// <summary>
        /// Sperrt/Entsperrt die Bearbeitung des Auftrags (wenn Rechnung vorhanden)
        /// </summary>
        private void SperreBearbeitung(bool sperren)
        {
            // Status-ComboBox
            cmbStatus.IsEnabled = !sperren;

            // Externe Auftragsnummer
            txtExterneNr.IsReadOnly = sperren;
            txtExterneNr.Background = sperren
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f0f0f0"))
                : Brushes.White;

            // Texte
            txtTextAnmerkung.IsReadOnly = sperren;
            txtDrucktext.IsReadOnly = sperren;
            txtHinweis.IsReadOnly = sperren;

            // Positionen-Control
            positionenControl.IsReadOnly = sperren;

            // Buttons deaktivieren wenn Rechnung vorhanden
            btnLieferschein.IsEnabled = !sperren;
            btnRechnung.IsEnabled = !sperren;
            btnRechnungDL.IsEnabled = !sperren;
            btnSpeichern.IsEnabled = !sperren;

            // Header-Badge Farbe ändern wenn gesperrt
            if (sperren)
            {
                brdStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6c757d"));
                txtSubtitel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffcccc"));
            }
            else
            {
                txtSubtitel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cce5ff"));
            }
        }

        #region Navigation

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                // Versuche Zurueck-Navigation, sonst zur Liste
                if (!main.NavigateBack())
                {
                    main.ShowContent(App.Services.GetRequiredService<BestellungenView>(), pushToStack: false);
                }
            }
        }

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null)
            {
                MessageBox.Show("Kein Auftrag geladen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (canDelete, reason) = await _core.CanDeleteAuftragAsync(_bestellung.KBestellung);
            if (!canDelete)
            {
                MessageBox.Show("Auftrag kann nicht gelöscht werden:\n\n" + reason, "Löschen nicht möglich", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Möchten Sie den Auftrag " + _bestellung.CBestellNr + " wirklich löschen?\n\nDiese Aktion kann nicht rückgängig gemacht werden!", "Auftrag löschen?", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            var (success, message) = await _core.DeleteAuftragAsync(_bestellung.KBestellung);

            if (success)
            {
                MessageBox.Show(message, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                Zurueck_Click(sender, e);
            }
            else
            {
                MessageBox.Show("Fehler beim Löschen:\n\n" + message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void KundeOeffnen_Click(object sender, MouseButtonEventArgs e)
        {
            // Im Neuer-Auftrag-Modus: Kunde auswählen
            if (_isNeuerAuftrag)
            {
                KundenNrAuswaehlen_Click();
                return;
            }

            // Im Bearbeiten-Modus: Kunde öffnen
            if (_kundeId > 0 && _bestellung != null)
            {
                // Bestellungs-ID übergeben für Zurück-Navigation
                var kundeDetail = new KundeDetailView(_kundeId, _bestellung.KBestellung);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(kundeDetail);
            }
        }

        #endregion

        #region Tab-Handling (Linke Seite)

        private void TabAuftragsdaten_Click(object sender, MouseButtonEventArgs e)
        {
            // TODO: Tab-Inhalt wechseln
            SetTabActive(tabAuftragsdaten, true);
            SetTabActive(tabAnhaenge, false);
            SetTabActive(tabKosten, false);
        }

        private void TabAnhaenge_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabAuftragsdaten, false);
            SetTabActive(tabAnhaenge, true);
            SetTabActive(tabKosten, false);
            MessageBox.Show("Anhaenge-Tab wird noch implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TabKosten_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabAuftragsdaten, false);
            SetTabActive(tabAnhaenge, false);
            SetTabActive(tabKosten, true);
            MessageBox.Show("Kosten-Tab wird noch implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Tab-Handling (Rechte Seite)

        private void TabDetails_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabDetails, true, "#0078D4");
            SetTabActive(tabTexte, false);
            SetTabActive(tabEigeneFelder, false);
            pnlDetails.Visibility = Visibility.Visible;
            pnlTexte.Visibility = Visibility.Collapsed;
            pnlEigeneFelder.Visibility = Visibility.Collapsed;
        }

        private void TabTexte_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabDetails, false);
            SetTabActive(tabTexte, true, "#0078D4");
            SetTabActive(tabEigeneFelder, false);
            pnlDetails.Visibility = Visibility.Collapsed;
            pnlTexte.Visibility = Visibility.Visible;
            pnlEigeneFelder.Visibility = Visibility.Collapsed;
        }

        private void TabEigeneFelder_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabDetails, false);
            SetTabActive(tabTexte, false);
            SetTabActive(tabEigeneFelder, true, "#0078D4");
            pnlDetails.Visibility = Visibility.Collapsed;
            pnlTexte.Visibility = Visibility.Collapsed;
            pnlEigeneFelder.Visibility = Visibility.Visible;
            LadeEigeneFelder();
        }

        private async void LadeEigeneFelder()
        {
            if (_bestellung == null) return;

            try
            {
                // JTL native: Verkauf.tAuftragAttribut / tAuftragAttributSprache
                var felder = await _core.GetAuftragEigeneFelderAsync(_bestellung.KBestellung);

                // Original-Werte merken fuer Aenderungserkennung
                _origEigeneFelder.Clear();
                foreach (var f in felder)
                {
                    _origEigeneFelder[f.FeldName] = f.Wert ?? "";
                }

                var items = felder.Select(f => new EigenesFeldItem
                {
                    FeldName = f.FeldName,
                    Wert = f.Wert ?? ""
                }).ToList();

                dgEigeneFelder.ItemsSource = items;

                if (items.Count == 0)
                {
                    txtStatus.Text = "Keine eigenen Felder gefunden";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler beim Laden der Eigenfelder: {ex.Message}";
            }
        }

        private void SetTabActive(Border tab, bool active, string activeColor = "#0078D4")
        {
            tab.Background = active
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(activeColor))
                : Brushes.Transparent;

            if (tab.Child is TextBlock tb)
            {
                tb.Foreground = active ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666"));
                tb.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        #endregion

        #region Adress-Buttons

        private async void RechnungsadresseAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            if (_kundeId <= 0) return;

            try
            {
                // Kundenadressen laden
                var adressen = await LadeKundenAdressenAsync(_kundeId);

                var dialog = new AdresseAuswahlDialog(adressen, "Rechnungsadresse auswaehlen");
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true && dialog.AusgewaehlteAdresse != null)
                {
                    var adr = dialog.AusgewaehlteAdresse;
                    txtRechnungsadresse.Text = adr.Formatiert;
                    // TODO: Adresse in Bestellung speichern
                    txtStatus.Text = "Rechnungsadresse geaendert";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RechnungsadresseBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            var aktuelleAdresse = AdresseAusText(txtRechnungsadresse.Text);

            var dialog = new AdresseBearbeitenDialog(aktuelleAdresse, "Rechnungsadresse bearbeiten");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                txtRechnungsadresse.Text = dialog.Adresse.Formatiert;
                // TODO: Adresse in Bestellung speichern
                txtStatus.Text = "Rechnungsadresse geaendert";
            }
        }

        private async void LieferadresseAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            if (_kundeId <= 0) return;

            try
            {
                // Kundenadressen laden
                var adressen = await LadeKundenAdressenAsync(_kundeId);

                var dialog = new AdresseAuswahlDialog(adressen, "Lieferadresse auswaehlen");
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true && dialog.AusgewaehlteAdresse != null)
                {
                    var adr = dialog.AusgewaehlteAdresse;
                    txtLieferadresse.Text = adr.Formatiert;

                    // MwSt neu berechnen basierend auf neuem Lieferland
                    var landIso = adr.Land;
                    if (string.IsNullOrEmpty(landIso) && !string.IsNullOrEmpty(adr.PLZ))
                    {
                        // Versuche Land aus PLZ zu ermitteln (DE als Standard)
                        landIso = "DE";
                    }
                    await BerechneUndAktualisiereMwStAsync(landIso);

                    txtStatus.Text = "Lieferadresse geaendert - MwSt neu berechnet";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LieferadresseBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            var aktuelleAdresse = AdresseAusText(txtLieferadresse.Text);

            var dialog = new AdresseBearbeitenDialog(aktuelleAdresse, "Lieferadresse bearbeiten");
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstGespeichert)
            {
                txtLieferadresse.Text = dialog.Adresse.Formatiert;
                // TODO: Adresse in Bestellung speichern
                txtStatus.Text = "Lieferadresse geaendert";
            }
        }

        private async Task<List<AdresseDto>> LadeKundenAdressenAsync(int kundeId)
        {
            // Alle Adressen des Kunden aus tAdresse laden
            // nTyp: 1 = Rechnungsadresse, 2 = Lieferadresse
            var adressen = new List<AdresseDto>();

            try
            {
                var dbAdressen = await _core.GetKundeAdressenKurzAsync(kundeId);

                foreach (var adr in dbAdressen)
                {
                    // Name aufteilen (Format: "Nachname, Vorname")
                    var nameParts = adr.CName?.Split(',') ?? Array.Empty<string>();
                    var nachname = nameParts.Length > 0 ? nameParts[0].Trim() : "";
                    var vorname = nameParts.Length > 1 ? nameParts[1].Trim() : "";

                    adressen.Add(new AdresseDto
                    {
                        KAdresse = adr.KAdresse,
                        Vorname = vorname,
                        Nachname = nachname,
                        Strasse = adr.CStrasse,
                        PLZ = adr.CPLZ,
                        Ort = adr.COrt
                    });
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler beim Laden der Adressen: {ex.Message}";
            }

            // Fallback: Aktuelle Bestelladressen hinzufuegen falls keine aus DB
            if (adressen.Count == 0 && _bestellung?.Rechnungsadresse != null)
            {
                var ra = _bestellung.Rechnungsadresse;
                adressen.Add(new AdresseDto
                {
                    Firma = ra.CFirma,
                    Vorname = ra.CVorname,
                    Nachname = ra.CName,
                    Strasse = ra.CStrasse,
                    PLZ = ra.CPLZ,
                    Ort = ra.COrt,
                    Land = ra.CLand
                });
            }

            return adressen;
        }

        private AdresseDto AdresseAusText(string text)
        {
            var zeilen = text.Split('\n');
            var dto = new AdresseDto();

            if (zeilen.Length >= 1) dto.Firma = zeilen[0].Trim();
            if (zeilen.Length >= 2)
            {
                var nameParts = zeilen[1].Trim().Split(' ');
                if (nameParts.Length >= 2)
                {
                    dto.Vorname = nameParts[0];
                    dto.Nachname = string.Join(" ", nameParts.Skip(1));
                }
                else if (nameParts.Length == 1)
                {
                    dto.Nachname = nameParts[0];
                }
            }
            if (zeilen.Length >= 3) dto.Strasse = zeilen[2].Trim();
            if (zeilen.Length >= 4)
            {
                var plzOrt = zeilen[3].Trim().Split(' ');
                if (plzOrt.Length >= 1) dto.PLZ = plzOrt[0];
                if (plzOrt.Length >= 2) dto.Ort = string.Join(" ", plzOrt.Skip(1));
            }
            if (zeilen.Length >= 5) dto.Land = zeilen[4].Trim();

            return dto;
        }

        #endregion

        #region PositionenControl Event-Handler

        private async void PositionenControl_ArtikelSuche(object sender, ArtikelSucheEventArgs e)
        {
            // Im Neuer-Auftrag-Modus oder bei bestehendem Auftrag ohne Rechnung
            if (!_isNeuerAuftrag && _bestellung == null) return;

            if (_hatRechnung)
            {
                MessageBox.Show("Der Auftrag kann nicht bearbeitet werden, da bereits eine Rechnung erstellt wurde.",
                    "Bearbeitung gesperrt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new ArtikelSuchDialog(e.Suchtext);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstAusgewaehlt && dialog.AusgewaehlterArtikel != null)
            {
                if (_isNeuerAuftrag)
                {
                    // Im Neuer-Auftrag-Modus: Position direkt zur Liste hinzufügen
                    await ArtikelZuListeHinzufuegenAsync(dialog.AusgewaehlterArtikel, dialog.Menge);
                }
                else
                {
                    // Bei bestehendem Auftrag: In Datenbank speichern
                    await ArtikelHinzufuegenAsync(dialog.AusgewaehlterArtikel, dialog.Menge);
                }
            }
        }

        /// <summary>
        /// Fügt einen Artikel zur lokalen Positionsliste hinzu (für Neuer-Auftrag-Modus)
        /// </summary>
        private async Task ArtikelZuListeHinzufuegenAsync(CoreService.ArtikelUebersicht artikel, decimal menge)
        {
            try
            {
                var positionen = positionenControl.GetPositionen().ToList();

                // Neue Position erstellen
                var neuePos = new PositionViewModel
                {
                    Id = positionen.Count + 1,
                    ArtikelId = artikel.KArtikel,
                    ArtNr = artikel.CArtNr ?? "",
                    Bezeichnung = artikel.Name ?? "",
                    Menge = menge,
                    Einheit = "Stk",
                    EinzelpreisNetto = artikel.FVKNetto,
                    MwStSatz = artikel.FMwSt,
                    Rabatt = 0,
                    PosTyp = 1,
                    Sort = positionen.Count + 1
                };

                positionen.Add(neuePos);
                positionenControl.SetPositionen(positionen);
                UpdateSummenFromControl();

                txtStatus.Text = $"{artikel.Name} hinzugefügt";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Hinzufügen:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PositionenControl_FreipositionRequested(object sender, EventArgs e)
        {
            if (_hatRechnung)
            {
                MessageBox.Show("Der Auftrag kann nicht bearbeitet werden, da bereits eine Rechnung erstellt wurde.",
                    "Bearbeitung gesperrt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Freiposition-Dialog öffnen
            var dialog = new FreipositionDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                var positionen = positionenControl.GetPositionen().ToList();

                var neuePos = new PositionViewModel
                {
                    Id = positionen.Count + 1,
                    ArtikelId = 0,
                    ArtNr = "",
                    Bezeichnung = dialog.Bezeichnung,
                    Menge = dialog.Menge,
                    Einheit = dialog.Einheit,
                    EinzelpreisNetto = dialog.PreisNetto,
                    MwStSatz = dialog.MwStSatz,
                    Rabatt = 0,
                    PosTyp = 2, // Freiposition
                    Sort = positionen.Count + 1
                };

                positionen.Add(neuePos);
                positionenControl.SetPositionen(positionen);
                UpdateSummenFromControl();

                txtStatus.Text = $"Freiposition '{dialog.Bezeichnung}' hinzugefügt";
            }
        }

        private void PositionenControl_PositionenChanged(object sender, EventArgs e)
        {
            // Summen aus PositionenControl aktualisieren
            UpdateSummenFromControl();
        }

        private void UpdateSummenFromControl()
        {
            var summeNetto = positionenControl.SummeNetto;
            var summeBrutto = positionenControl.SummeBrutto;
            var mwst = summeBrutto - summeNetto;

            txtSummeNetto.Text = $"{summeNetto:N2} EUR";
            txtMwSt.Text = $"{mwst:N2} EUR";
            txtGesamtBrutto.Text = $"{summeBrutto:N2} EUR";
        }

        private async Task ArtikelHinzufuegenAsync(CoreService.ArtikelUebersicht artikel, decimal menge)
        {
            if (_bestellung == null) return;

            try
            {
                txtStatus.Text = $"Fuege {artikel.Name} hinzu...";

                // Position zum Auftrag hinzufuegen via JtlOrderClient
                var dbContext = App.Services.GetRequiredService<JtlDbContext>();
                var jtlClient = new JtlOrderClient(dbContext.ConnectionString);

                var input = new JtlOrderClient.AuftragPositionInput(
                    KArtikel: artikel.KArtikel,
                    FAnzahl: menge,
                    FVKNetto: artikel.FVKNetto
                );

                var result = await jtlClient.AddAuftragPositionAsync(_bestellung.KBestellung, input);

                if (result.Success)
                {
                    txtStatus.Text = $"{artikel.Name} hinzugefuegt";
                    // Positionen neu laden
                    LadeBestellung(_bestellung.KBestellung);
                }
                else
                {
                    MessageBox.Show($"Fehler: {result.Message}", "Fehler",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Hinzufuegen:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Speichern und Aktionen

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            // Neuer Auftrag speichern
            if (_isNeuerAuftrag)
            {
                await SpeichereNeuenAuftragAsync();
                return;
            }

            if (_bestellung == null) return;

            // Bei Rechnung nur Hinweistext speichern (keine Änderungen an Positionen/Preisen)
            if (_hatRechnung)
            {
                MessageBox.Show("Der Auftrag hat bereits eine Rechnung. Änderungen sind nicht möglich.\n\nNur der interne Hinweis kann bearbeitet werden.",
                    "Bearbeitung eingeschränkt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var neuerStatus = (cmbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (neuerStatus != _bestellung.CStatus)
                {
                    await _core.UpdateBestellStatusAsync(_bestellung.KBestellung, neuerStatus!);
                    _bestellung.CStatus = neuerStatus;
                    txtStatusBadge.Text = neuerStatus ?? "Offen";
                    SetStatusBadgeColor(neuerStatus);
                }

                _bestellung.CInetBestellNr = txtExterneNr.Text;
                _bestellung.CAnmerkung = txtTextAnmerkung.Text;

                await _core.UpdateBestellungAsync(_bestellung);

                // Verkauftexte speichern (Verkauf.tAuftragText)
                await _core.UpdateAuftragTexteAsync(
                    _bestellung.KBestellung,
                    txtTextAnmerkung.Text,
                    txtDrucktext.Text,
                    txtHinweis.Text,
                    txtVorgangsstatus.Text);

                // Eigene Felder speichern (nur geaenderte)
                await SpeichereEigeneFelderAsync();

                txtStatus.Text = "Gespeichert";
                MessageBox.Show("Auftrag gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Speichert einen neuen Auftrag in der Datenbank
        /// </summary>
        private async Task SpeichereNeuenAuftragAsync()
        {
            // Validierung ohne Popup - nur Status-Meldung
            if (_selectedKunde == null)
            {
                txtStatus.Text = "Bitte zuerst Kunde auswählen";
                return;
            }

            var positionen = positionenControl.GetPositionen();
            if (positionen == null || !positionen.Any())
            {
                txtStatus.Text = "Bitte mindestens eine Position hinzufügen";
                return;
            }

            try
            {
                txtStatus.Text = "Erstelle Auftrag...";
                btnSpeichern.IsEnabled = false;

                // Versandart und Zahlungsart auslesen
                var versandart = cmbVersandart.SelectedItem as CoreService.VersandartRef;
                var zahlungsart = cmbZahlungsart.SelectedItem as CoreService.ZahlungsartRef;
                int.TryParse(txtZahlungsziel.Text, out var zahlungsziel);
                if (zahlungsziel <= 0) zahlungsziel = 14;

                // Positionen für CoreService vorbereiten
                var importPositionen = positionen.Select(p => new CoreService.AuftragImportPosition
                {
                    ArtNr = p.ArtNr,
                    Menge = p.Menge,
                    Preis = p.EinzelpreisNetto
                }).ToList();

                // Auftrag über CoreService erstellen
                var result = await _core.CreateAuftragAsync(
                    _selectedKunde.KKunde,
                    importPositionen,
                    versandart?.KVersandArt ?? 10,
                    zahlungsart?.KZahlungsart ?? 2,
                    zahlungsziel,
                    txtTextAnmerkung.Text,
                    txtDrucktext.Text,
                    txtHinweis.Text);

                if (result.Success)
                {
                    txtStatus.Text = $"Auftrag {result.AuftragsNr} erstellt";
                    MessageBox.Show($"Auftrag {result.AuftragsNr} wurde erfolgreich erstellt!",
                        "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Zur Auftragsliste zurückkehren oder den neuen Auftrag laden
                    _isNeuerAuftrag = false;
                    LadeBestellung(result.AuftragId);
                }
                else
                {
                    txtStatus.Text = "Fehler beim Erstellen";
                    var fehlerMsg = result.NichtGefundeneArtikel?.Any() == true
                        ? $"Fehler: Folgende Artikel wurden nicht gefunden:\n{string.Join(", ", result.NichtGefundeneArtikel)}"
                        : "Fehler beim Erstellen des Auftrags.";
                    MessageBox.Show(fehlerMsg, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Fehler";
                MessageBox.Show($"Fehler beim Erstellen des Auftrags:\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSpeichern.IsEnabled = true;
            }
        }

        private async Task SpeichereEigeneFelderAsync()
        {
            if (_bestellung == null) return;

            // Aktuelle Werte aus DataGrid holen
            var items = dgEigeneFelder.ItemsSource as List<EigenesFeldItem>;
            if (items == null || items.Count == 0) return;

            // Geaenderte Felder ermitteln
            var geaendert = new Dictionary<string, string?>();
            foreach (var item in items)
            {
                var origWert = _origEigeneFelder.TryGetValue(item.FeldName, out var v) ? v : "";
                if (item.Wert != origWert)
                {
                    geaendert[item.FeldName] = item.Wert;
                }
            }

            if (geaendert.Count > 0)
            {
                await _core.SetAuftragEigeneFelderAsync(_bestellung.KBestellung, geaendert);

                // Original-Werte aktualisieren
                foreach (var kvp in geaendert)
                {
                    _origEigeneFelder[kvp.Key] = kvp.Value ?? "";
                }
            }
        }

        private async void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            var result = MessageBox.Show(
                $"Lieferschein für Auftrag {_bestellung.CBestellNr} erstellen?\n\nAlle offenen Positionen werden übernommen.",
                "Lieferschein erstellen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                txtStatus.Text = "Erstelle Lieferschein...";
                var kLieferschein = await _core.CreateLieferscheinAsync(_bestellung.KBestellung);

                // Lieferscheinnummer holen
                var lieferscheine = await _core.GetLieferscheineAsync(_bestellung.KBestellung);
                var neuerLieferschein = lieferscheine.FirstOrDefault(l => l.KLieferschein == kLieferschein);

                txtStatus.Text = "";
                MessageBox.Show(
                    $"Lieferschein {neuerLieferschein?.CLieferscheinNr ?? kLieferschein.ToString()} wurde erstellt!",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show(
                    $"Fehler beim Erstellen des Lieferscheins:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            var result = MessageBox.Show(
                $"Rechnung für Auftrag {_bestellung.CBestellNr} erstellen?\n\nHinweis: Ein Lieferschein muss bereits existieren.",
                "Rechnung erstellen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                txtStatus.Text = "Erstelle Rechnung...";
                var kRechnung = await _core.CreateRechnungAsync(_bestellung.KBestellung);

                // Rechnungsnummer holen
                var rechnungen = await _core.GetRechnungenAsync(_bestellung.KBestellung);
                var neueRechnung = rechnungen.FirstOrDefault(r => r.KRechnung == kRechnung);

                txtStatus.Text = "";
                MessageBox.Show(
                    $"Rechnung {neueRechnung?.CRechnungsnr ?? kRechnung.ToString()} wurde erstellt!",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Neu laden um Sperre zu aktivieren
                LadeBestellung(_bestellung.KBestellung);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show(
                    $"Fehler beim Erstellen der Rechnung:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Erstellt eine Rechnung ohne Lieferschein (für Dienstleistungen)
        /// </summary>
        private async void RechnungOhneVersand_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            var result = MessageBox.Show(
                $"Dienstleistungsrechnung für Auftrag {_bestellung.CBestellNr} erstellen?\n\n" +
                "Diese Rechnung wird OHNE Lieferschein erstellt (für Dienstleistungen ohne Warenversand).",
                "Dienstleistungsrechnung erstellen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                txtStatus.Text = "Erstelle Dienstleistungsrechnung...";

                // Rechnung ohne Lieferschein erstellen
                var kRechnung = await _core.CreateRechnungOhneVersandAsync(_bestellung.KBestellung);

                // Rechnungsnummer holen
                var rechnungen = await _core.GetRechnungenAsync(_bestellung.KBestellung);
                var neueRechnung = rechnungen.FirstOrDefault(r => r.KRechnung == kRechnung);

                txtStatus.Text = "";
                MessageBox.Show(
                    $"Dienstleistungsrechnung {neueRechnung?.CRechnungsnr ?? kRechnung.ToString()} wurde erstellt!",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Neu laden um Sperre zu aktivieren
                LadeBestellung(_bestellung.KBestellung);
            }
            catch (Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show(
                    $"Fehler beim Erstellen der Rechnung:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void Lieferantenbestellung_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            try
            {
                // Lieferanten laden, die für Artikel im Auftrag hinterlegt sind (tLieferantenArtikel)
                var lieferanten = (await _core.GetLieferantenForBestellungAsync(_bestellung.KBestellung)).ToList();
                if (!lieferanten.Any())
                {
                    MessageBox.Show("Keine Lieferanten für die Artikel im Auftrag hinterlegt!\n\nBitte prüfen Sie die Lieferanten-Zuordnungen in den Artikelstammdaten.",
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Dialog zur Lieferantenauswahl (Mehrfachauswahl möglich)
                var dialog = new LieferantenAuswahlDialog(lieferanten);
                dialog.Owner = Window.GetWindow(this);

                if (dialog.ShowDialog() == true && dialog.SelectedLieferantIds.Any())
                {
                    var erstellteBestellungen = new List<int>();
                    var fehler = new List<string>();

                    foreach (var kLieferant in dialog.SelectedLieferantIds)
                    {
                        try
                        {
                            txtStatus.Text = $"Erstelle Lieferantenbestellung {erstellteBestellungen.Count + 1}/{dialog.SelectedLieferantIds.Count}...";

                            var liefBestId = await _core.CreateLieferantenbestellungFromAuftragAsync(
                                _bestellung.KBestellung,
                                kLieferant);

                            erstellteBestellungen.Add(liefBestId);
                        }
                        catch (Exception ex)
                        {
                            fehler.Add($"Lieferant {kLieferant}: {ex.Message}");
                        }
                    }

                    txtStatus.Text = "";

                    if (erstellteBestellungen.Any())
                    {
                        var msg = erstellteBestellungen.Count == 1
                            ? $"Lieferantenbestellung {erstellteBestellungen[0]} wurde erstellt!"
                            : $"{erstellteBestellungen.Count} Lieferantenbestellungen wurden erstellt:\n{string.Join(", ", erstellteBestellungen)}";

                        if (fehler.Any())
                            msg += $"\n\nFehler:\n{string.Join("\n", fehler)}";

                        msg += "\n\nSie finden diese unter Beschaffung > Bestellungen.";

                        MessageBox.Show(msg, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (fehler.Any())
                    {
                        MessageBox.Show($"Fehler beim Erstellen:\n\n{string.Join("\n", fehler)}",
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "";
                MessageBox.Show(
                    $"Fehler beim Erstellen der Lieferantenbestellung:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion
    }

    /// <summary>
    /// Hilfsklasse fuer Eigene Felder DataGrid
    /// </summary>
    public class EigenesFeldItem
    {
        public string FeldName { get; set; } = "";
        public string Wert { get; set; } = "";
    }
}
