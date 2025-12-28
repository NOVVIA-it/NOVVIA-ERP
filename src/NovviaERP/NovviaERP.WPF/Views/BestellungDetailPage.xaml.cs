using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class BestellungDetailPage : Page
    {
        private readonly CoreService _coreService;
        private int? _bestellungId;
        private CoreService.BestellungDetail? _bestellung;
        private List<CoreService.VersandartRef> _versandarten = new();
        private List<CoreService.ZahlungsartRef> _zahlungsarten = new();

        public BestellungDetailPage(int? bestellungId)
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            _bestellungId = bestellungId;
            Loaded += async (s, e) => await LadeBestellungAsync();
        }

        private async System.Threading.Tasks.Task LadeBestellungAsync()
        {
            try
            {
                // Stammdaten laden
                _versandarten = (await _coreService.GetVersandartenAsync()).ToList();
                cmbVersandart.ItemsSource = _versandarten;

                _zahlungsarten = (await _coreService.GetZahlungsartenAsync()).ToList();
                cmbZahlungsart.ItemsSource = _zahlungsarten;

                if (_bestellungId.HasValue)
                {
                    txtStatus.Text = "Lade Auftrag...";
                    _bestellung = await _coreService.GetBestellungByIdAsync(_bestellungId.Value);

                    if (_bestellung == null)
                    {
                        MessageBox.Show("Auftrag nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        NavigationService?.GoBack();
                        return;
                    }

                    // Header
                    txtTitel.Text = $"Auftrag {_bestellung.CBestellNr}";
                    txtBestellNr.Text = _bestellung.CBestellNr;
                    txtSubtitel.Text = _bestellung.DErstellt.ToString("dd.MM.yyyy HH:mm");
                    UpdateStatusBadge(_bestellung.CStatus);

                    // Uebersicht
                    txtBestellNrDetail.Text = _bestellung.CBestellNr;
                    txtExterneNr.Text = _bestellung.CInetBestellNr ?? "-";
                    txtDatum.Text = _bestellung.DErstellt.ToString("dd.MM.yyyy HH:mm");
                    SetComboBoxByTag(cmbStatus, _bestellung.CStatus ?? "Offen");
                    txtShop.Text = _bestellung.ShopName ?? "-";

                    // Kunde
                    txtKundeFirma.Text = _bestellung.KundeFirma ?? "";
                    txtKundeName.Text = _bestellung.KundeName ?? "";
                    txtKundeNr.Text = $"Kd-Nr: {_bestellung.CKundenNr}";
                    txtKundeTel.Text = _bestellung.KundeTel ?? "-";
                    txtKundeMail.Text = _bestellung.KundeMail ?? "-";

                    // Adressen
                    txtRechnungsadresse.Text = FormatAdresse(_bestellung.Rechnungsadresse);
                    txtLieferadresse.Text = FormatAdresse(_bestellung.Lieferadresse);

                    // Positionen
                    dgPositionen.ItemsSource = _bestellung.Positionen;

                    // Summen
                    txtSummeNetto.Text = $"{_bestellung.GesamtNetto:N2} EUR";
                    txtVersandkosten.Text = $"{_bestellung.FVersandBruttoPreis:N2} EUR";
                    var mwst = _bestellung.GesamtBrutto - _bestellung.GesamtNetto;
                    txtMwSt.Text = $"{mwst:N2} EUR";
                    txtGesamtBrutto.Text = $"{_bestellung.GesamtBrutto:N2} EUR";

                    // Versand
                    cmbVersandart.SelectedValue = _bestellung.TVersandArt_KVersandArt;
                    txtSendungsnummer.Text = _bestellung.CIdentCode ?? "";
                    dpVersanddatum.SelectedDate = _bestellung.DVersandt;
                    btnAlsVersendetMarkieren.IsEnabled = !_bestellung.DVersandt.HasValue;

                    // Zahlung
                    cmbZahlungsart.SelectedValue = _bestellung.KZahlungsart;
                    txtZahlungsziel.Text = _bestellung.NZahlungsZiel.ToString();
                    dpBezahltAm.SelectedDate = _bestellung.DBezahlt;
                    btnAlsBezahltMarkieren.IsEnabled = !_bestellung.DBezahlt.HasValue;

                    // Anmerkungen
                    txtAnmerkungen.Text = _bestellung.CAnmerkung ?? "";

                    txtStatus.Text = "Auftrag geladen";
                }
                else
                {
                    // Neuer Auftrag
                    txtTitel.Text = "Neuer Auftrag";
                    txtBestellNr.Text = "(wird automatisch vergeben)";
                    txtStatus.Text = "Neuen Auftrag anlegen";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void UpdateStatusBadge(string? status)
        {
            txtStatusBadge.Text = status ?? "Offen";
            brdStatus.Background = status switch
            {
                "Storniert" => new SolidColorBrush(Color.FromRgb(220, 53, 69)),    // Rot
                "Versendet" => new SolidColorBrush(Color.FromRgb(40, 167, 69)),    // Gruen
                "Bezahlt" => new SolidColorBrush(Color.FromRgb(0, 123, 255)),      // Blau
                "In Bearbeitung" => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Gelb
                _ => new SolidColorBrush(Color.FromRgb(108, 117, 125))              // Grau
            };
        }

        private string FormatAdresse(CoreService.AdresseDetail? adr)
        {
            if (adr == null) return "-";
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(adr.CFirma)) lines.Add(adr.CFirma);
            var name = $"{adr.CVorname} {adr.CName}".Trim();
            if (!string.IsNullOrEmpty(name)) lines.Add(name);
            if (!string.IsNullOrEmpty(adr.CStrasse)) lines.Add(adr.CStrasse);
            if (!string.IsNullOrEmpty(adr.CPLZ) || !string.IsNullOrEmpty(adr.COrt))
                lines.Add($"{adr.CPLZ} {adr.COrt}".Trim());
            if (!string.IsNullOrEmpty(adr.CLand) && adr.CLand != "Deutschland")
                lines.Add(adr.CLand);
            return string.Join("\n", lines);
        }

        private void SetComboBoxByTag(ComboBox cmb, string tag)
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
            cmb.SelectedIndex = 0;
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_bestellung == null || !_bestellungId.HasValue)
                {
                    MessageBox.Show("Neuen Auftrag anlegen wird noch implementiert.",
                        "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                txtStatus.Text = "Speichere...";

                // Status
                if (cmbStatus.SelectedItem is ComboBoxItem statusItem)
                    _bestellung.CStatus = statusItem.Tag?.ToString();

                // Versand
                _bestellung.TVersandArt_KVersandArt = cmbVersandart.SelectedValue as int?;
                _bestellung.CIdentCode = txtSendungsnummer.Text;
                _bestellung.DVersandt = dpVersanddatum.SelectedDate;

                // Zahlung
                _bestellung.KZahlungsart = cmbZahlungsart.SelectedValue as int?;
                _bestellung.NZahlungsZiel = int.TryParse(txtZahlungsziel.Text, out var zz) ? zz : 14;
                _bestellung.DBezahlt = dpBezahltAm.SelectedDate;

                // Anmerkungen
                _bestellung.CAnmerkung = txtAnmerkungen.Text;

                await _coreService.UpdateBestellungAsync(_bestellung);

                UpdateStatusBadge(_bestellung.CStatus);
                btnAlsVersendetMarkieren.IsEnabled = !_bestellung.DVersandt.HasValue;
                btnAlsBezahltMarkieren.IsEnabled = !_bestellung.DBezahlt.HasValue;

                txtStatus.Text = "Auftrag gespeichert";
                MessageBox.Show("Auftrag wurde gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void AlsVersendetMarkieren_Click(object sender, RoutedEventArgs e)
        {
            dpVersanddatum.SelectedDate = DateTime.Today;
            SetComboBoxByTag(cmbStatus, "Versendet");
        }

        private void AlsBezahltMarkieren_Click(object sender, RoutedEventArgs e)
        {
            dpBezahltAm.SelectedDate = DateTime.Today;
            SetComboBoxByTag(cmbStatus, "Bezahlt");
        }

        private void KundeOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung != null)
            {
                NavigationService?.Navigate(new KundeDetailPage(_bestellung.TKunde_KKunde));
            }
        }

        private void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung != null)
            {
                MessageBox.Show($"Lieferschein fuer Auftrag {_bestellung.CBestellNr} erstellen\n(wird implementiert)",
                    "Lieferschein", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var kRechnung = await _coreService.CreateRechnungAsync(_bestellung.KBestellung);
                var rechnungen = await _coreService.GetRechnungenAsync(_bestellung.KBestellung);
                var neueRechnung = rechnungen.FirstOrDefault(r => r.KRechnung == kRechnung);

                MessageBox.Show(
                    $"Rechnung {neueRechnung?.CRechnungsnr ?? kRechnung.ToString()} wurde erstellt!",
                    "Erfolg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fehler beim Erstellen der Rechnung:\n\n{ex.Message}",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}
