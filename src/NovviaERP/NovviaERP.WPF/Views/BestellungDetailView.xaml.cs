using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Infrastructure.Jtl;
using NovviaERP.Core.Services;
using System.Threading.Tasks;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungDetailView : UserControl
    {
        private readonly CoreService _core;
        private CoreService.BestellungDetail? _bestellung;
        private int _kundeId;
        private Dictionary<string, string> _origEigeneFelder = new();

        public BestellungDetailView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
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

                // Positionen
                dgPositionen.ItemsSource = _bestellung.Positionen;

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

                txtStatus.Text = $"Auftrag {_bestellung.CBestellNr} geladen";
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

        #region Navigation

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                var liste = App.Services.GetRequiredService<BestellungenView>();
                main.ShowContent(liste);
            }
        }

        private void KundeOeffnen_Click(object sender, MouseButtonEventArgs e)
        {
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
                    // TODO: Adresse in Bestellung speichern
                    txtStatus.Text = "Lieferadresse geaendert";
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

        #region Positionen-Toolbar

        private async void ArtikelSuchen_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            var suchbegriff = txtArtikelSuche.Text?.Trim();

            var dialog = new ArtikelSuchDialog(suchbegriff);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.IstAusgewaehlt && dialog.AusgewaehlterArtikel != null)
            {
                await ArtikelHinzufuegenAsync(dialog.AusgewaehlterArtikel, dialog.Menge);
            }
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

        private void FreipositionHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Freiposition hinzufuegen...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PositionLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgPositionen.SelectedItem == null)
            {
                MessageBox.Show("Bitte eine Position auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MessageBox.Show("Position loeschen...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RabattSetzen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rabatt setzen...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PreiseNeuErmitteln_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Preise neu ermitteln...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Speichern und Aktionen

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

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
