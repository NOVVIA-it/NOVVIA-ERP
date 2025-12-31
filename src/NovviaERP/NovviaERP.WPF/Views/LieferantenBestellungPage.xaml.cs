using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class LieferantenBestellungPage : UserControl
    {
        private readonly CoreService _coreService;
        private List<CoreService.LieferantRef> _lieferanten = new();

        public LieferantenBestellungPage()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) =>
            {
                // Spalten-Konfiguration aktivieren (Rechtsklick auf Header)
                DataGridColumnConfig.EnableColumnChooser(dgBestellungen, "LieferantenBestellungPage");
                await LadeDataAsync();
            };
        }

        private async System.Threading.Tasks.Task LadeDataAsync()
        {
            try
            {
                txtStatus.Text = "Lade Daten...";

                // Lieferanten fuer Filter laden
                _lieferanten = (await _coreService.GetLieferantenAsync()).ToList();
                var alleLieferanten = new List<CoreService.LieferantRef> { new CoreService.LieferantRef { KLieferant = 0, CFirma = "(Alle)" } };
                alleLieferanten.AddRange(_lieferanten);
                cboFilterLieferant.ItemsSource = alleLieferanten;
                cboFilterLieferant.SelectedIndex = 0;

                await LadeBestellungenAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LadeBestellungenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Bestellungen...";

                int? kLieferant = null;
                if (cboFilterLieferant.SelectedValue is int liefId && liefId > 0)
                    kLieferant = liefId;

                int? status = null;
                if (cboFilterStatus.SelectedItem is ComboBoxItem statusItem &&
                    !string.IsNullOrEmpty(statusItem.Tag?.ToString()) &&
                    int.TryParse(statusItem.Tag.ToString(), out int s))
                {
                    status = s;
                }

                var bestellungen = await _coreService.GetLieferantenBestellungenAsync(kLieferant, status);
                var liste = bestellungen.ToList();
                dgBestellungen.ItemsSource = liste;

                var count = liste.Count;
                txtAnzahl.Text = $"{count} Bestellung{(count != 1 ? "en" : "")}";
                txtAnzahlHeader.Text = $"({count} Bestellungen)";
                txtStatus.Text = "Bereit";

                // Summen berechnen
                var summePositionen = liste.Sum(b => b.AnzahlPositionen);
                var summeNettoEK = liste.Sum(b => b.NettoGesamt);
                txtSummeAnzahl.Text = $"{count} Bestellungen";
                txtSummePositionen.Text = summePositionen.ToString();
                txtSummeNettoEK.Text = summeNettoEK.ToString("N2");
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                await LadeBestellungenAsync();
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeBestellungenAsync();
        }

        private async void Neu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LieferantenBestellungDetailView();
            if (dialog.ShowDialog() == true)
            {
                await LadeBestellungenAsync();
            }
        }

        private async void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.LieferantenBestellungUebersicht bestellung)
            {
                var dialog = new LieferantenBestellungDetailView(bestellung.KLieferantenBestellung);
                if (dialog.ShowDialog() == true)
                {
                    await LadeBestellungenAsync();
                }
            }
            else
            {
                MessageBox.Show("Bitte eine Bestellung auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Bestellungen_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.LieferantenBestellungUebersicht bestellung)
            {
                var dialog = new LieferantenBestellungDetailView(bestellung.KLieferantenBestellung);
                if (dialog.ShowDialog() == true)
                {
                    await LadeBestellungenAsync();
                }
            }
        }

        private async void Duplizieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.LieferantenBestellungUebersicht bestellung)
            {
                try
                {
                    var result = MessageBox.Show(
                        $"Bestellung {bestellung.KLieferantenBestellung} duplizieren?",
                        "Duplizieren",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        txtStatus.Text = "Dupliziere...";
                        var newId = await _coreService.DuplicateLieferantenBestellungAsync(bestellung.KLieferantenBestellung);
                        MessageBox.Show($"Neue Bestellung {newId} wurde erstellt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LadeBestellungenAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Duplizieren: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Bitte eine Bestellung auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBestellungen.SelectedItem is CoreService.LieferantenBestellungUebersicht bestellung)
            {
                var result = MessageBox.Show(
                    $"Bestellung {bestellung.KLieferantenBestellung} wirklich loeschen?\n\nLieferant: {bestellung.LieferantName}\nNetto-EK: {bestellung.NettoGesamt:N2} EUR",
                    "Bestellung loeschen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _coreService.DeleteLieferantenBestellungAsync(bestellung.KLieferantenBestellung);
                        MessageBox.Show("Bestellung wurde geloescht!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LadeBestellungenAsync();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler beim Loeschen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Bitte eine Bestellung auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
