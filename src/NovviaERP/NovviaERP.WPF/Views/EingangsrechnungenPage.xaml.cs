using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EingangsrechnungenPage : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.EingangsrechnungItem> _liste = new();
        private DateTime _selectedMonth = DateTime.Today;
        private string _statusFilterFromMenu = "";

        public EingangsrechnungenPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            UpdateMonatAnzeige();
            Loaded += async (s, e) =>
            {
                // Wenn Status vom Menü vorgegeben, ComboBox setzen
                if (!string.IsNullOrEmpty(_statusFilterFromMenu))
                {
                    foreach (ComboBoxItem item in cmbStatus.Items)
                    {
                        if (item.Tag?.ToString() == _statusFilterFromMenu)
                        {
                            cmbStatus.SelectedItem = item;
                            break;
                        }
                    }
                }
                await LadeListeAsync();
            };
        }

        /// <summary>
        /// Setzt den Status-Filter von außen (z.B. vom MainWindow-Menü)
        /// </summary>
        public void SetStatusFilter(string status)
        {
            _statusFilterFromMenu = status;
        }

        private void UpdateMonatAnzeige()
        {
            txtMonatAnzeige.Text = _selectedMonth.ToString("MM.yyyy");
        }

        private void MonatZurueck_Click(object sender, RoutedEventArgs e)
        {
            _selectedMonth = _selectedMonth.AddMonths(-1);
            UpdateMonatAnzeige();
            _ = LadeListeAsync();
        }

        private void MonatVor_Click(object sender, RoutedEventArgs e)
        {
            _selectedMonth = _selectedMonth.AddMonths(1);
            UpdateMonatAnzeige();
            _ = LadeListeAsync();
        }

        private (DateTime? von, DateTime? bis) GetZeitraumFilter()
        {
            var tag = (cmbZeitraum.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "monat";
            var heute = DateTime.Today;

            return tag switch
            {
                "30" => (heute.AddDays(-30), null),
                "90" => (heute.AddDays(-90), null),
                "360" => (heute.AddDays(-360), null),
                "jahr" => (new DateTime(_selectedMonth.Year, 1, 1), new DateTime(_selectedMonth.Year + 1, 1, 1)),
                "monat" => (new DateTime(_selectedMonth.Year, _selectedMonth.Month, 1),
                           new DateTime(_selectedMonth.Year, _selectedMonth.Month, 1).AddMonths(1)),
                "heute" => (heute, heute.AddDays(1)),
                _ => (null, null)
            };
        }

        private async System.Threading.Tasks.Task LadeListeAsync()
        {
            try
            {
                txtStatus.Text = "Lade...";

                var suche = txtSuche.Text?.Trim();
                var statusFilter = (cmbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                int? status = string.IsNullOrEmpty(statusFilter) ? null : int.Parse(statusFilter);
                var (von, bis) = GetZeitraumFilter();

                _liste = (await _core.GetEingangsrechnungenAsync(suche, status, von, bis)).ToList();
                dgEingangsrechnungen.ItemsSource = _liste;

                var summe = _liste.Sum(x => x.Brutto);
                var offen = _liste.Where(x => x.Status == 0).Sum(x => x.Brutto);
                txtStatus.Text = $"{_liste.Count} Eingangsrechnungen";
                txtSumme.Text = $"Summe: {summe:N2} EUR (davon offen: {offen:N2} EUR)";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Suche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) _ = LadeListeAsync();
        }

        private void Suchen_Click(object sender, RoutedEventArgs e) => _ = LadeListeAsync();
        private void Status_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) _ = LadeListeAsync(); }
        private void Zeitraum_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) _ = LadeListeAsync(); }

        private void DataGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgEingangsrechnungen.SelectedItem is CoreService.EingangsrechnungItem item)
            {
                var detailView = new EingangsrechnungDetailView(item.Id);
                detailView.ShowDialog();
                _ = LadeListeAsync();
            }
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new EingangsrechnungDialog(null);
            if (dialog.ShowDialog() == true)
                _ = LadeListeAsync();
        }

        private async void AlsGeprueft_Click(object sender, RoutedEventArgs e)
        {
            if (dgEingangsrechnungen.SelectedItem is not CoreService.EingangsrechnungItem item)
            {
                MessageBox.Show("Bitte eine Rechnung auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.Status >= 1)
            {
                MessageBox.Show("Diese Rechnung ist bereits geprueft.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _core.UpdateEingangsrechnungStatusAsync(item.Id, 1, null);
            await LadeListeAsync();
        }

        private async void AlsBezahlt_Click(object sender, RoutedEventArgs e)
        {
            if (dgEingangsrechnungen.SelectedItem is not CoreService.EingangsrechnungItem item)
            {
                MessageBox.Show("Bitte eine Rechnung auswaehlen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (item.Status == 2)
            {
                MessageBox.Show("Diese Rechnung ist bereits bezahlt.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Rechnung {item.RechnungsNr} als bezahlt markieren?",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _core.UpdateEingangsrechnungStatusAsync(item.Id, 2, DateTime.Today);
                await LadeListeAsync();
            }
        }
    }
}
