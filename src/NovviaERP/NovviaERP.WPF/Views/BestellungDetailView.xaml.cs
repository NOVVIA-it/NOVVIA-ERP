using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungDetailView : UserControl
    {
        private readonly CoreService _core;
        private CoreService.BestellungDetail? _bestellung;
        private int _kundeId;

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

                // Kundenkommentar / Anmerkung
                txtKundenkommentar.Text = ""; // TODO: Kundenkommentar aus Bestellung
                txtAnmerkung.Text = _bestellung.CAnmerkung ?? "";

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
            if (_kundeId > 0)
            {
                var kundeDetail = new KundeDetailView(_kundeId);
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
        }

        private void TabTexte_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabDetails, false);
            SetTabActive(tabTexte, true, "#0078D4");
            SetTabActive(tabEigeneFelder, false);
            MessageBox.Show("Texte-Tab wird noch implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TabEigeneFelder_Click(object sender, MouseButtonEventArgs e)
        {
            SetTabActive(tabDetails, false);
            SetTabActive(tabTexte, false);
            SetTabActive(tabEigeneFelder, true, "#0078D4");
            MessageBox.Show("Eigene Felder-Tab wird noch implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void RechnungsadresseAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rechnungsadresse auswaehlen...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RechnungsadresseBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Rechnungsadresse bearbeiten...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LieferadresseAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lieferadresse auswaehlen...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LieferadresseBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lieferadresse bearbeiten...\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Positionen-Toolbar

        private void ArtikelSuchen_Click(object sender, RoutedEventArgs e)
        {
            var suchbegriff = txtArtikelSuche.Text?.Trim();
            if (string.IsNullOrEmpty(suchbegriff))
            {
                MessageBox.Show("Bitte einen Suchbegriff eingeben.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MessageBox.Show($"Artikel suchen: {suchbegriff}\n\n(Funktion folgt)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
                _bestellung.CAnmerkung = txtAnmerkung.Text;

                await _core.UpdateBestellungAsync(_bestellung);

                txtStatus.Text = "Gespeichert";
                MessageBox.Show("Auftrag gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Lieferschein fuer Auftrag {_bestellung?.CBestellNr} erstellen...\n\n(Funktion folgt)",
                "Lieferschein", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Rechnung fuer Auftrag {_bestellung?.CBestellNr} erstellen...\n\n(Funktion folgt)",
                "Rechnung", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
