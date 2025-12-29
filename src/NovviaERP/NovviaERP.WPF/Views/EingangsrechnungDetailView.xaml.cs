using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EingangsrechnungDetailView : Window
    {
        private readonly CoreService _core;
        private readonly int _eingangsrechnungId;
        private List<EingangsrechnungPosVM> _positionen = new();

        public EingangsrechnungDetailView(int eingangsrechnungId)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _eingangsrechnungId = eingangsrechnungId;

            Loaded += async (s, e) => await LadeDatenAsync();
        }

        private async System.Threading.Tasks.Task LadeDatenAsync()
        {
            try
            {
                // Eingangsrechnung laden
                var rechnung = await _core.GetEingangsrechnungDetailAsync(_eingangsrechnungId);
                if (rechnung == null)
                {
                    MessageBox.Show("Eingangsrechnung nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                // Header
                Title = $"Eingangsrechnung - {rechnung.Fremdbelegnummer}";
                txtLieferant.Text = rechnung.LieferantName;
                txtStrasse.Text = rechnung.Strasse;
                txtPLZ.Text = rechnung.PLZ;
                txtOrt.Text = rechnung.Ort;
                txtLand.Text = rechnung.Land;
                txtTelefon.Text = rechnung.Telefon;
                txtEmail.Text = rechnung.Email;

                txtFremdbelegnummer.Text = rechnung.Fremdbelegnummer;
                txtEigeneNummer.Text = rechnung.EigeneNummer;
                dpBelegdatum.SelectedDate = rechnung.Belegdatum;
                dpZahlungsziel.SelectedDate = rechnung.Zahlungsziel;
                txtHinweise.Text = rechnung.Hinweise;

                chkZurZahlungFreigeben.IsChecked = rechnung.ZahlungFreigegeben;
                dpZahlbarBis.SelectedDate = rechnung.Zahlungsziel;

                // Status setzen
                foreach (ComboBoxItem item in cboStatus.Items)
                {
                    if (int.TryParse(item.Tag?.ToString(), out int status) && status == rechnung.Status)
                    {
                        cboStatus.SelectedItem = item;
                        break;
                    }
                }

                // Enthaltene Bestellungen
                if (rechnung.Bestellungen != null && rechnung.Bestellungen.Any())
                {
                    lstBestellungen.ItemsSource = rechnung.Bestellungen;
                }

                // Positionen laden
                var posData = await _core.GetEingangsrechnungPositionenAsync(_eingangsrechnungId);
                _positionen = posData.Select(p => new EingangsrechnungPosVM
                {
                    KPosition = p.KPosition,
                    KArtikel = p.KArtikel,
                    CArtNr = p.CArtNr,
                    CLieferantenArtNr = p.CLieferantenArtNr,
                    CName = p.CName,
                    CEinheit = p.CEinheit,
                    CHinweis = p.CHinweis,
                    FMenge = p.FMenge,
                    FEKNetto = p.FEKNetto,
                    FMwSt = p.FMwSt
                }).ToList();
                dgPositionen.ItemsSource = _positionen;

                // Summen berechnen
                txtAnzahlPositionen.Text = _positionen.Count.ToString();
                var summeNetto = _positionen.Sum(p => p.NettoGesamt);
                var summeBrutto = _positionen.Sum(p => p.BruttoGesamt);
                txtSummeNetto.Text = $"{summeNetto:N2} EUR";
                txtSummeBrutto.Text = $"{summeBrutto:N2} EUR";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int status = 0;
                if (cboStatus.SelectedItem is ComboBoxItem statusItem && int.TryParse(statusItem.Tag?.ToString(), out int s))
                    status = s;

                await _core.UpdateEingangsrechnungJtlAsync(_eingangsrechnungId, new CoreService.EingangsrechnungUpdateDto
                {
                    Status = status,
                    ZahlungFreigegeben = chkZurZahlungFreigeben.IsChecked == true,
                    Zahlungsziel = dpZahlungsziel.SelectedDate,
                    Hinweise = txtHinweise.Text
                });

                MessageBox.Show("Eingangsrechnung gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Schliessen_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class EingangsrechnungPosVM
    {
        public int KPosition { get; set; }
        public int KArtikel { get; set; }
        public string CArtNr { get; set; } = "";
        public string CLieferantenArtNr { get; set; } = "";
        public string CName { get; set; } = "";
        public string CEinheit { get; set; } = "";
        public string CHinweis { get; set; } = "";
        public decimal FMenge { get; set; }
        public decimal FEKNetto { get; set; }
        public decimal FMwSt { get; set; }

        public decimal NettoGesamt => FMenge * FEKNetto;
        public decimal BruttoEK => FEKNetto * (1 + FMwSt / 100);
        public decimal BruttoGesamt => FMenge * BruttoEK;
    }
}
