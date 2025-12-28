using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class LieferantenAuswahlDialog : Window
    {
        private readonly List<LieferantItem> _alleLieferanten;
        public List<int> SelectedLieferantIds { get; private set; } = new();

        // Backwards compatibility
        public int? SelectedLieferantId => SelectedLieferantIds.FirstOrDefault();

        public LieferantenAuswahlDialog(IEnumerable<CoreService.LieferantRef> lieferanten)
        {
            InitializeComponent();

            _alleLieferanten = lieferanten.Select(l => new LieferantItem
            {
                KLieferant = l.KLieferant,
                CFirma = l.CFirma ?? "",
                ArtikelAnzahl = l.ArtikelAnzahl,
                DisplayText = l.ArtikelAnzahl > 0
                    ? $"{l.CFirma} ({l.ArtikelAnzahl} Artikel)"
                    : l.CFirma
            }).OrderByDescending(l => l.ArtikelAnzahl).ThenBy(l => l.CFirma).ToList();

            lstLieferanten.ItemsSource = _alleLieferanten;

            if (_alleLieferanten.Any())
                lstLieferanten.SelectedIndex = 0;
        }

        private void Suche_TextChanged(object sender, TextChangedEventArgs e)
        {
            var suche = txtSuche.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(suche))
            {
                lstLieferanten.ItemsSource = _alleLieferanten;
            }
            else
            {
                lstLieferanten.ItemsSource = _alleLieferanten
                    .Where(l => l.CFirma.ToLower().Contains(suche))
                    .ToList();
            }
        }

        private void Lieferant_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            Auswaehlen();
        }

        private void Auswaehlen_Click(object sender, RoutedEventArgs e)
        {
            Auswaehlen();
        }

        private void AlleAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            lstLieferanten.SelectAll();
        }

        private void Auswaehlen()
        {
            var selectedItems = lstLieferanten.SelectedItems.Cast<LieferantItem>().ToList();
            if (selectedItems.Any())
            {
                SelectedLieferantIds = selectedItems.Select(l => l.KLieferant).ToList();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Bitte wählen Sie mindestens einen Lieferanten aus!",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class LieferantItem
    {
        public int KLieferant { get; set; }
        public string CFirma { get; set; } = "";
        public int ArtikelAnzahl { get; set; }
        public string DisplayText { get; set; } = "";
    }
}
