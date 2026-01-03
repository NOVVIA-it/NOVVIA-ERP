using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class MahnungslaufPage : UserControl
    {
        private readonly CoreService _core;
        private List<MahnKandidat> _kandidaten = new();

        public MahnungslaufPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
        }

        private async void BtnLaden_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int faelligTage = int.TryParse(txtFaelligTage.Text, out int t) ? t : 14;
                decimal mindestbetrag = decimal.TryParse(txtMindestbetrag.Text.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal m) ? m : 10m;

                _kandidaten = (await _core.GetMahnKandidatenAsync(faelligTage, mindestbetrag)).ToList();
                dgMahnungen.ItemsSource = _kandidaten;

                AktualisiereZusammenfassung();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAlleAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var k in _kandidaten) k.IsSelected = true;
            dgMahnungen.Items.Refresh();
            AktualisiereZusammenfassung();
        }

        private void BtnAuswahlAufheben_Click(object sender, RoutedEventArgs e)
        {
            foreach (var k in _kandidaten) k.IsSelected = false;
            dgMahnungen.Items.Refresh();
            AktualisiereZusammenfassung();
        }

        private void AktualisiereZusammenfassung()
        {
            var ausgewaehlt = _kandidaten.Where(k => k.IsSelected).ToList();
            var summe = ausgewaehlt.Sum(k => k.OffenerBetrag);
            txtZusammenfassung.Text = $"{ausgewaehlt.Count} Rechnungen ausgewaehlt | Summe: {summe:N2} EUR";
        }

        private async void BtnMahnungenErstellen_Click(object sender, RoutedEventArgs e)
        {
            var ausgewaehlt = _kandidaten.Where(k => k.IsSelected).ToList();
            if (!ausgewaehlt.Any())
            {
                MessageBox.Show("Bitte waehlen Sie mindestens eine Rechnung aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Moechten Sie {ausgewaehlt.Count} Mahnungen erstellen?", "Mahnungen erstellen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                int erstellt = 0;
                foreach (var k in ausgewaehlt)
                {
                    await _core.ErstelleMahnungAsync(k.RechnungId, k.AktuelleMahnstufe + 1);
                    erstellt++;
                }

                MessageBox.Show($"{erstellt} Mahnungen wurden erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                BtnLaden_Click(sender, e); // Neu laden
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMahnungenDrucken_Click(object sender, RoutedEventArgs e)
        {
            var ausgewaehlt = _kandidaten.Where(k => k.IsSelected).ToList();
            if (!ausgewaehlt.Any())
            {
                MessageBox.Show("Bitte waehlen Sie mindestens eine Rechnung aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show($"Druckfunktion fuer {ausgewaehlt.Count} Mahnungen wird implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
