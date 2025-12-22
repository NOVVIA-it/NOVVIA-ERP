using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class PlattformPage : Page
    {
        private readonly PlattformService _service;
        private readonly JtlDbContext _db;
        public ObservableCollection<PlattformViewModel> Plattformen { get; } = new();
        private PlattformViewModel? _selected;

        public PlattformPage(PlattformService service, JtlDbContext db)
        {
            _service = service;
            _db = db;
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var plattformen = await _service.GetPlattformenAsync(false);
            Plattformen.Clear();
            foreach (var p in plattformen)
            {
                Plattformen.Add(new PlattformViewModel(p));
            }
            lstPlattformen.ItemsSource = Plattformen;
        }

        private void LstPlattformen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = lstPlattformen.SelectedItem as PlattformViewModel;
            if (_selected == null) return;

            txtName.Text = _selected.Name;
            txtUrl.Text = _selected.Url;
            txtApiKey.Text = _selected.ApiKey;
            chkAktiv.IsChecked = _selected.Aktiv;
            chkAutoSync.IsChecked = _selected.AutoSync;
            txtIntervall.Text = _selected.SyncIntervallMin.ToString();
            txtLetzterSync.Text = _selected.LetzterSync?.ToString("dd.MM.yyyy HH:mm:ss") ?? "Noch nie synchronisiert";

            foreach (ComboBoxItem item in cbTyp.Items)
            {
                if (item.Tag?.ToString() == ((int)_selected.Typ).ToString())
                {
                    cbTyp.SelectedItem = item;
                    break;
                }
            }
        }

        private void BtnNeu_Click(object sender, RoutedEventArgs e)
        {
            var neu = new PlattformViewModel(new Plattform { Name = "Neue Plattform", Aktiv = true });
            Plattformen.Add(neu);
            lstPlattformen.SelectedItem = neu;
        }

        private async void BtnLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            if (MessageBox.Show($"Plattform '{_selected.Name}' wirklich löschen?", "Löschen", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Plattformen.Remove(_selected);
                // TODO: Aus DB löschen
            }
        }

        private async void BtnTestVerbindung_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            
            txtTestErgebnis.Text = "Teste...";
            txtTestErgebnis.Foreground = Brushes.Gray;

            var (erfolg, meldung) = await _service.TestVerbindungAsync(_selected.Id);
            
            txtTestErgebnis.Text = meldung;
            txtTestErgebnis.Foreground = erfolg ? Brushes.Green : Brushes.Red;
        }

        private async void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            MessageBox.Show("Synchronisation gestartet...", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            // TODO: Sync ausführen
        }

        private void BtnSyncLog_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Sync-Log Dialog öffnen
        }
    }

    public class PlattformViewModel
    {
        private readonly Plattform _plattform;
        public PlattformViewModel(Plattform p) => _plattform = p;

        public int Id => _plattform.Id;
        public string Name { get => _plattform.Name; set => _plattform.Name = value; }
        public PlattformTyp Typ => _plattform.Typ;
        public string TypName => Typ.ToString();
        public string Url { get => _plattform.Url; set => _plattform.Url = value; }
        public string? ApiKey { get => _plattform.ApiKey; set => _plattform.ApiKey = value; }
        public bool Aktiv { get => _plattform.Aktiv; set => _plattform.Aktiv = value; }
        public bool AutoSync { get => _plattform.AutoSync; set => _plattform.AutoSync = value; }
        public int SyncIntervallMin { get => _plattform.SyncIntervallMin; set => _plattform.SyncIntervallMin = value; }
        public DateTime? LetzterSync => _plattform.LetzterSync;
        
        public Brush StatusFarbe => Aktiv 
            ? (_plattform.LetzterSyncErfolg ? Brushes.LimeGreen : Brushes.Orange)
            : Brushes.Gray;
        
        public string LetzterSyncText => LetzterSync?.ToString("dd.MM. HH:mm") ?? "-";
    }
}
