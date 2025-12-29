using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EingangsrechnungDialog : Window
    {
        private readonly CoreService _core;
        private readonly int? _id;

        public EingangsrechnungDialog(int? id)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _id = id;

            dpRechnungsDatum.SelectedDate = DateTime.Today;
            dpFaelligAm.SelectedDate = DateTime.Today.AddDays(30);

            Loaded += async (s, e) => await LadeDatenAsync();
        }

        private async System.Threading.Tasks.Task LadeDatenAsync()
        {
            try
            {
                // Lieferanten laden
                var lieferanten = await _core.GetLieferantenAsync();
                cboLieferant.ItemsSource = lieferanten;

                // Offene Bestellungen laden
                var bestellungen = await _core.GetOffeneLieferantenBestellungenAsync();
                cboBestellung.ItemsSource = bestellungen.Select(b => new
                {
                    b.KLieferantenBestellung,
                    DisplayText = $"#{b.KLieferantenBestellung} - {b.CLieferantName} ({b.DErstellt:dd.MM.yyyy})"
                }).ToList();

                if (_id.HasValue)
                {
                    txtHeader.Text = $"Eingangsrechnung #{_id}";
                    btnLoeschen.Visibility = Visibility.Visible;

                    var item = await _core.GetEingangsrechnungByIdAsync(_id.Value);
                    if (item != null)
                    {
                        cboLieferant.SelectedValue = item.LieferantId;
                        txtRechnungsNr.Text = item.RechnungsNr;
                        dpRechnungsDatum.SelectedDate = item.RechnungsDatum;
                        dpFaelligAm.SelectedDate = item.FaelligAm;
                        txtNetto.Text = item.Netto.ToString("N2");
                        txtMwSt.Text = item.MwSt.ToString("N2");
                        txtBrutto.Text = item.Brutto.ToString("N2");
                        txtBemerkung.Text = item.Bemerkung;

                        if (item.LieferantenBestellungId.HasValue)
                            cboBestellung.SelectedValue = item.LieferantenBestellungId;

                        foreach (ComboBoxItem statusItem in cboStatus.Items)
                        {
                            if (int.TryParse(statusItem.Tag?.ToString(), out int status) && status == item.Status)
                            {
                                cboStatus.SelectedItem = statusItem;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Betrag_Changed(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(txtNetto.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var netto) &&
                decimal.TryParse(txtMwSt.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var mwst))
            {
                txtBrutto.Text = (netto + mwst).ToString("N2");
            }
        }

        private void MwSt19_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtNetto.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var netto))
            {
                txtMwSt.Text = (netto * 0.19m).ToString("N2");
            }
        }

        private void MwSt7_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtNetto.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var netto))
            {
                txtMwSt.Text = (netto * 0.07m).ToString("N2");
            }
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (cboLieferant.SelectedValue == null)
            {
                MessageBox.Show("Bitte Lieferant auswaehlen.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtRechnungsNr.Text))
            {
                MessageBox.Show("Bitte Rechnungsnummer eingeben.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtNetto.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var netto))
            {
                MessageBox.Show("Bitte gueltigen Netto-Betrag eingeben.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal.TryParse(txtMwSt.Text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var mwst);

            int status = 0;
            if (cboStatus.SelectedItem is ComboBoxItem statusItem && int.TryParse(statusItem.Tag?.ToString(), out int s))
                status = s;

            try
            {
                var dto = new CoreService.EingangsrechnungDto
                {
                    Id = _id ?? 0,
                    LieferantId = (int)cboLieferant.SelectedValue,
                    LieferantenBestellungId = cboBestellung.SelectedValue as int?,
                    RechnungsNr = txtRechnungsNr.Text.Trim(),
                    RechnungsDatum = dpRechnungsDatum.SelectedDate ?? DateTime.Today,
                    FaelligAm = dpFaelligAm.SelectedDate,
                    Netto = netto,
                    MwSt = mwst,
                    Brutto = netto + mwst,
                    Status = status,
                    BezahltAm = status == 2 ? DateTime.Today : null,
                    Bemerkung = txtBemerkung.Text?.Trim()
                };

                if (_id.HasValue)
                {
                    await _core.UpdateEingangsrechnungAsync(dto);
                    MessageBox.Show("Eingangsrechnung aktualisiert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _core.CreateEingangsrechnungAsync(dto);
                    MessageBox.Show("Eingangsrechnung erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (!_id.HasValue) return;

            if (MessageBox.Show("Eingangsrechnung wirklich loeschen?", "Loeschen bestaetigen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _core.DeleteEingangsrechnungAsync(_id.Value);
                DialogResult = true;
                Close();
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
