using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KategorieAuswahlDialog : Window
    {
        private ObservableCollection<KategorieTreeItem> _kategorien = new();
        private List<KategorieTreeItem> _allItems = new();

        public HashSet<int> AusgewaehlteKategorien { get; private set; } = new();

        public KategorieAuswahlDialog(IEnumerable<CoreService.KategorieInfo> kategorien, HashSet<int> ausgewaehlt)
        {
            InitializeComponent();

            // Flache Liste in Baum umwandeln
            var kategorieDict = kategorien.ToDictionary(k => k.KKategorie);
            var roots = new List<KategorieTreeItem>();

            foreach (var kat in kategorien.OrderBy(k => k.NSort))
            {
                var item = new KategorieTreeItem
                {
                    KKategorie = kat.KKategorie,
                    Name = kat.CName ?? "",
                    IsSelected = ausgewaehlt.Contains(kat.KKategorie)
                };
                _allItems.Add(item);

                if (kat.KOberKategorie == 0 || kat.KOberKategorie == null)
                {
                    roots.Add(item);
                }
                else
                {
                    var parent = _allItems.FirstOrDefault(i => i.KKategorie == kat.KOberKategorie);
                    if (parent != null)
                    {
                        parent.Children.Add(item);
                    }
                    else
                    {
                        roots.Add(item);
                    }
                }
            }

            _kategorien = new ObservableCollection<KategorieTreeItem>(roots);
            tvKategorien.ItemsSource = _kategorien;
        }

        private void TxtSuche_TextChanged(object sender, TextChangedEventArgs e)
        {
            var suchtext = txtSuche.Text.ToLower();
            if (string.IsNullOrWhiteSpace(suchtext))
            {
                tvKategorien.ItemsSource = _kategorien;
                return;
            }

            // Gefilterte Ansicht
            var gefiltert = _allItems.Where(k => k.Name.ToLower().Contains(suchtext)).ToList();
            tvKategorien.ItemsSource = gefiltert;
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Nichts zu tun - Binding aktualisiert automatisch
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            AusgewaehlteKategorien = _allItems.Where(k => k.IsSelected).Select(k => k.KKategorie).ToHashSet();
            DialogResult = true;
            Close();
        }

        private void BtnAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class KategorieTreeItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int KKategorie { get; set; }
        public string Name { get; set; } = "";
        public ObservableCollection<KategorieTreeItem> Children { get; set; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
