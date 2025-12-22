using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class AngebotDetailDialog : Window
    {
        private readonly AngebotService _service;
        private readonly int? _angebotId;
        private Angebot _angebot = new();
        public ObservableCollection<AngebotPosition> Positionen { get; } = new();

        public AngebotDetailDialog(AngebotService service, int? angebotId)
        {
            _service = service;
            _angebotId = angebotId;
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (_angebotId.HasValue)
            {
                _angebot = await _service.GetAngebotAsync(_angebotId.Value) ?? new Angebot();
                txtAngebotNr.Text = _angebot.AngebotNr;
                dpDatum.SelectedDate = _angebot.AngebotsDatum;
                dpGueltigBis.SelectedDate = _angebot.GueltigBis;
                txtBemerkung.Text = _angebot.Bemerkung;

                foreach (ComboBoxItem item in cbStatus.Items)
                {
                    if (item.Tag?.ToString() == ((int)_angebot.Status).ToString())
                    {
                        cbStatus.SelectedItem = item;
                        break;
                    }
                }

                Positionen.Clear();
                foreach (var p in _angebot.Positionen)
                    Positionen.Add(p);
            }
            else
            {
                txtAngebotNr.Text = "(wird automatisch vergeben)";
                dpDatum.SelectedDate = DateTime.Today;
                dpGueltigBis.SelectedDate = DateTime.Today.AddDays(30);
                cbStatus.SelectedIndex = 0;
            }

            dgPositionen.ItemsSource = Positionen;
            BerecheSummen();
        }

        private void BtnKundeWaehlen_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Kunden-Auswahl Dialog
        }

        private void BtnPositionHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            var pos = new AngebotPosition
            {
                PosNr = Positionen.Count + 1,
                Menge = 1,
                Einheit = "Stk",
                MwStSatz = 19
            };
            Positionen.Add(pos);
            dgPositionen.SelectedItem = pos;
        }

        private void BtnPositionLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgPositionen.SelectedItem is AngebotPosition pos)
            {
                Positionen.Remove(pos);
                // Pos-Nummern neu vergeben
                var nr = 1;
                foreach (var p in Positionen)
                    p.PosNr = nr++;
                dgPositionen.Items.Refresh();
                BerecheSummen();
            }
        }

        private void BerecheSummen()
        {
            var netto = Positionen.Sum(p => p.Gesamt);
            var mwst = Positionen.Sum(p => p.Gesamt * p.MwStSatz / 100);
            var brutto = netto + mwst;

            txtNetto.Text = $"{netto:N2} €";
            txtMwSt.Text = $"{mwst:N2} €";
            txtBrutto.Text = $"{brutto:N2} €";
        }

        private async void BtnSpeichern_Click(object sender, RoutedEventArgs e)
        {
            _angebot.AngebotsDatum = dpDatum.SelectedDate ?? DateTime.Today;
            _angebot.GueltigBis = dpGueltigBis.SelectedDate ?? DateTime.Today.AddDays(30);
            _angebot.Bemerkung = txtBemerkung.Text;
            _angebot.Positionen = Positionen.ToList();

            if (cbStatus.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                _angebot.Status = (AngebotStatus)int.Parse(item.Tag.ToString()!);
            }

            try
            {
                if (_angebotId.HasValue)
                {
                    await _service.UpdateAngebotAsync(_angebot);
                }
                else
                {
                    await _service.CreateAngebotAsync(_angebot);
                }
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
