using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class WareneingangDialog : Window
    {
        private readonly CoreService _core;
        private readonly int _bestellungId;
        private readonly ObservableCollection<WareneingangPositionVM> _positionen = new();

        public bool WurdeGebucht { get; private set; }

        public WareneingangDialog(int bestellungId, string lieferant, IEnumerable<PositionVM> positionen)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _bestellungId = bestellungId;

            txtBestellNr.Text = $"#{bestellungId}";
            txtLieferant.Text = lieferant;
            dpEingangsdatum.SelectedDate = DateTime.Today;

            foreach (var pos in positionen)
            {
                _positionen.Add(new WareneingangPositionVM
                {
                    KLieferantenBestellungPos = pos.KLieferantenBestellungPos,
                    KArtikel = pos.KArtikel,
                    CArtNr = pos.CArtNr,
                    CName = pos.CName,
                    FMenge = pos.FMenge,
                    FMengeGeliefert = pos.FMengeGeliefert,
                    JetztGeliefert = pos.FMenge - pos.FMengeGeliefert // Default: Rest vollstaendig
                });
            }

            dgPositionen.ItemsSource = _positionen;
        }

        private void AlleVollstaendig_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pos in _positionen)
            {
                pos.JetztGeliefert = pos.Offen;
            }
            dgPositionen.Items.Refresh();
        }

        private void AlleNull_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pos in _positionen)
            {
                pos.JetztGeliefert = 0;
            }
            dgPositionen.Items.Refresh();
        }

        private async void Buchen_Click(object sender, RoutedEventArgs e)
        {
            var zuBuchen = _positionen.Where(p => p.JetztGeliefert > 0).ToList();

            if (!zuBuchen.Any())
            {
                MessageBox.Show("Bitte mindestens eine Menge eintragen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Validierung: Nicht mehr liefern als offen
            var ueberliefert = zuBuchen.Where(p => p.JetztGeliefert > p.Offen + 0.001m).ToList();
            if (ueberliefert.Any())
            {
                var msg = string.Join("\n", ueberliefert.Select(p => $"- {p.CArtNr}: {p.JetztGeliefert:N2} > {p.Offen:N2}"));
                MessageBox.Show($"Folgende Positionen haben mehr Menge als offen:\n\n{msg}\n\nBitte korrigieren.",
                    "Ueberlieferung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Wareneingang fuer {zuBuchen.Count} Position(en) buchen?\n\n" +
                $"Eingangsdatum: {dpEingangsdatum.SelectedDate:dd.MM.yyyy}\n" +
                $"Lieferschein-Nr: {txtLieferscheinNr.Text}",
                "Wareneingang bestaetigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var buchungen = zuBuchen.Select(p => new CoreService.WareneingangPosition
                {
                    KLieferantenBestellungPos = p.KLieferantenBestellungPos,
                    KArtikel = p.KArtikel,
                    FMenge = p.JetztGeliefert,
                    CChargenNr = p.ChargenNr,
                    DMHD = p.MHD
                }).ToList();

                await _core.WareneingangBuchenAsync(
                    _bestellungId,
                    dpEingangsdatum.SelectedDate ?? DateTime.Today,
                    txtLieferscheinNr.Text?.Trim(),
                    buchungen);

                WurdeGebucht = true;
                MessageBox.Show($"Wareneingang erfolgreich gebucht!\n\n{zuBuchen.Count} Position(en), Lagerbestand aktualisiert.",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Buchen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class WareneingangPositionVM : INotifyPropertyChanged
    {
        private decimal _jetztGeliefert;

        public int KLieferantenBestellungPos { get; set; }
        public int KArtikel { get; set; }
        public string CArtNr { get; set; } = "";
        public string CName { get; set; } = "";
        public decimal FMenge { get; set; }
        public decimal FMengeGeliefert { get; set; }
        public string? ChargenNr { get; set; }
        public DateTime? MHD { get; set; }

        public decimal Offen => FMenge - FMengeGeliefert;

        public decimal JetztGeliefert
        {
            get => _jetztGeliefert;
            set { _jetztGeliefert = value; OnPropertyChanged(nameof(JetztGeliefert)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
