using System.Windows;
using System.Windows.Controls;
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

                // Header
                txtTitel.Text = $"Auftrag";
                txtBestellNr.Text = _bestellung.CBestellNr;
                txtSubtitel.Text = $"vom {_bestellung.DErstellt:dd.MM.yyyy} - {_bestellung.KundeName}";
                txtStatusBadge.Text = _bestellung.CStatus ?? "Offen";
                SetStatusBadgeColor(_bestellung.CStatus);

                // Uebersicht
                txtBestellNrDetail.Text = _bestellung.CBestellNr;
                txtExterneNr.Text = _bestellung.CInetBestellNr;
                txtDatum.Text = _bestellung.DErstellt.ToString("dd.MM.yyyy HH:mm");
                txtShop.Text = _bestellung.ShopName ?? "-";

                // Status ComboBox
                foreach (ComboBoxItem item in cmbStatus.Items)
                {
                    if (item.Tag?.ToString() == _bestellung.CStatus)
                    {
                        cmbStatus.SelectedItem = item;
                        break;
                    }
                }

                // Kunde
                _kundeId = _bestellung.TKunde_KKunde;
                txtKundeFirma.Text = _bestellung.KundeFirma ?? "";
                txtKundeName.Text = _bestellung.KundeName ?? "";
                txtKundeNr.Text = $"Kd-Nr.: {_bestellung.CKundenNr}";
                txtKundeTel.Text = _bestellung.KundeTel ?? "-";
                txtKundeMail.Text = _bestellung.KundeMail ?? "-";

                // Adressen
                var ra = _bestellung.Rechnungsadresse;
                var la = _bestellung.Lieferadresse;
                txtRechnungsadresse.Text = FormatAdresse(ra?.CFirma, $"{ra?.CVorname} {ra?.CName}".Trim(),
                    ra?.CStrasse, ra?.CPLZ, ra?.COrt);
                txtLieferadresse.Text = FormatAdresse(la?.CFirma, $"{la?.CVorname} {la?.CName}".Trim(),
                    la?.CStrasse, la?.CPLZ, la?.COrt);

                // Positionen
                dgPositionen.ItemsSource = _bestellung.Positionen;

                // Summen
                var summeNetto = _bestellung.GesamtNetto;
                var summeBrutto = _bestellung.GesamtBrutto;
                var mwst = summeBrutto - summeNetto;

                txtSummeNetto.Text = $"{summeNetto:N2} EUR";
                txtMwSt.Text = $"{mwst:N2} EUR";
                txtGesamtBrutto.Text = $"{summeBrutto:N2} EUR";

                // Versand
                txtSendungsnummer.Text = _bestellung.CIdentCode ?? "";
                dpVersanddatum.SelectedDate = _bestellung.DVersandt;

                // Zahlung
                txtZahlungsziel.Text = _bestellung.NZahlungsZiel > 0 ? $"{_bestellung.NZahlungsZiel} Tage" : "-";
                dpBezahltAm.SelectedDate = _bestellung.DBezahlt;

                // Anmerkungen
                txtAnmerkungen.Text = _bestellung.CAnmerkung ?? "";

                txtStatus.Text = $"Bestellung {_bestellung.CBestellNr} geladen";
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

        private string FormatAdresse(string? firma, string? name, string? strasse, string? plz, string? ort)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(firma)) lines.Add(firma);
            if (!string.IsNullOrWhiteSpace(name)) lines.Add(name);
            if (!string.IsNullOrWhiteSpace(strasse)) lines.Add(strasse);
            if (!string.IsNullOrWhiteSpace(plz) || !string.IsNullOrWhiteSpace(ort))
                lines.Add($"{plz} {ort}".Trim());
            return lines.Count > 0 ? string.Join("\n", lines) : "-";
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                var liste = App.Services.GetRequiredService<BestellungenView>();
                main.ShowContent(liste);
            }
        }

        private void KundeOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (_kundeId > 0)
            {
                var kundeDetail = new KundeDetailView(_kundeId);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(kundeDetail);
            }
        }

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

                _bestellung.CIdentCode = txtSendungsnummer.Text;
                _bestellung.DVersandt = dpVersanddatum.SelectedDate;
                _bestellung.DBezahlt = dpBezahltAm.SelectedDate;
                _bestellung.CAnmerkung = txtAnmerkungen.Text;

                await _core.UpdateBestellungAsync(_bestellung);

                txtStatus.Text = "Gespeichert";
                MessageBox.Show("Bestellung gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AlsVersendetMarkieren_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            dpVersanddatum.SelectedDate ??= DateTime.Today;

            await _core.UpdateBestellStatusAsync(_bestellung.KBestellung, "Versendet");
            _bestellung.CStatus = "Versendet";
            txtStatusBadge.Text = "Versendet";
            SetStatusBadgeColor("Versendet");

            foreach (ComboBoxItem item in cmbStatus.Items)
            {
                if (item.Tag?.ToString() == "Versendet")
                {
                    cmbStatus.SelectedItem = item;
                    break;
                }
            }

            txtStatus.Text = "Als versendet markiert";
        }

        private async void AlsBezahltMarkieren_Click(object sender, RoutedEventArgs e)
        {
            if (_bestellung == null) return;

            dpBezahltAm.SelectedDate ??= DateTime.Today;

            await _core.UpdateBestellStatusAsync(_bestellung.KBestellung, "Bezahlt");
            _bestellung.CStatus = "Bezahlt";
            txtStatusBadge.Text = "Bezahlt";
            SetStatusBadgeColor("Bezahlt");

            foreach (ComboBoxItem item in cmbStatus.Items)
            {
                if (item.Tag?.ToString() == "Bezahlt")
                {
                    cmbStatus.SelectedItem = item;
                    break;
                }
            }

            txtStatus.Text = "Als bezahlt markiert";
        }

        private void Lieferschein_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Lieferschein fuer Bestellung {_bestellung?.CBestellNr} erstellen...\n\n(Funktion folgt)",
                "Lieferschein", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Rechnung_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Rechnung fuer Bestellung {_bestellung?.CBestellNr} erstellen...\n\n(Funktion folgt)",
                "Rechnung", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
