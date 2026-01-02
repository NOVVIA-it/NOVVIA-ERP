using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class StaffelpreisDialog : Window
    {
        private readonly CoreService _coreService;
        private readonly int _kArtikel;
        private readonly int _kKundengruppe;
        private ObservableCollection<StaffelpreisViewModel> _staffelpreise = new();
        private const decimal MwstSatz = 1.19m;

        public StaffelpreisDialog(CoreService coreService, int kArtikel, int kKundengruppe, string kundengruppenName)
        {
            InitializeComponent();
            _coreService = coreService;
            _kArtikel = kArtikel;
            _kKundengruppe = kKundengruppe;

            txtKundengruppe.Text = $"Kundengruppe: {kundengruppenName}";
            txtHeader.Text = $"Staffelpreise - {kundengruppenName}";

            Loaded += async (s, e) => await LadeStaffelpreiseAsync();
        }

        private async System.Threading.Tasks.Task LadeStaffelpreiseAsync()
        {
            try
            {
                var staffeln = await _coreService.GetArtikelStaffelpreiseAsync(_kArtikel, _kKundengruppe);
                _staffelpreise = new ObservableCollection<StaffelpreisViewModel>(
                    staffeln.Select(s => new StaffelpreisViewModel
                    {
                        NAnzahlAb = s.NAnzahlAb,
                        FNettoPreis = s.FNettoPreis,
                        FProzent = s.FProzent
                    })
                );

                // Falls keine Staffeln vorhanden, eine leere Zeile hinzufuegen
                if (!_staffelpreise.Any())
                {
                    _staffelpreise.Add(new StaffelpreisViewModel { NAnzahlAb = 0, FNettoPreis = 0, FProzent = 0 });
                }

                dgStaffelpreise.ItemsSource = _staffelpreise;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            // Naechste Staffel-Menge ermitteln
            int naechsteMenge = 1;
            if (_staffelpreise.Any())
            {
                naechsteMenge = _staffelpreise.Max(s => s.NAnzahlAb) + 10;
            }

            _staffelpreise.Add(new StaffelpreisViewModel
            {
                NAnzahlAb = naechsteMenge,
                FNettoPreis = 0,
                FProzent = 0
            });
        }

        private void BtnEntfernen_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgStaffelpreise.SelectedItem as StaffelpreisViewModel;
            if (selected != null && _staffelpreise.Count > 1)
            {
                _staffelpreise.Remove(selected);
            }
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validierung: Keine doppelten Mengen
                var mengen = _staffelpreise.Select(s => s.NAnzahlAb).ToList();
                if (mengen.Count != mengen.Distinct().Count())
                {
                    MessageBox.Show("Es gibt doppelte Mengenangaben. Bitte korrigieren.", "Validierung",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Konvertieren und speichern
                var staffelDetails = _staffelpreise
                    .Where(s => s.FNettoPreis > 0 || s.FProzent != 0)
                    .Select(s => new CoreService.StaffelpreisDetail
                    {
                        NAnzahlAb = s.NAnzahlAb,
                        FNettoPreis = s.FNettoPreis,
                        FProzent = s.FProzent
                    })
                    .ToList();

                await _coreService.SaveStaffelpreiseAsync(_kArtikel, _kKundengruppe, staffelDetails);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class StaffelpreisViewModel : INotifyPropertyChanged
    {
        private int _nAnzahlAb;
        private decimal _fNettoPreis;
        private decimal _fProzent;

        public int NAnzahlAb
        {
            get => _nAnzahlAb;
            set { _nAnzahlAb = value; OnPropertyChanged(nameof(NAnzahlAb)); }
        }

        public decimal FNettoPreis
        {
            get => _fNettoPreis;
            set
            {
                _fNettoPreis = value;
                OnPropertyChanged(nameof(FNettoPreis));
                OnPropertyChanged(nameof(FBruttoPreis));
            }
        }

        public decimal FBruttoPreis => FNettoPreis * 1.19m;

        public decimal FProzent
        {
            get => _fProzent;
            set { _fProzent = value; OnPropertyChanged(nameof(FProzent)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
