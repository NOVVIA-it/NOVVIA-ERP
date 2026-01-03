using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KategorieView : UserControl
    {
        private readonly CoreService _coreService;
        private CoreService.KategorieDetail? _selectedKategorie;

        public KategorieView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();

            // Converter fuer Visibility hinzufuegen
            Resources.Add("BoolToVisibilityInverseConverter", new BoolToVisibilityInverseConverter());

            Loaded += async (s, e) =>
            {
                await LadeKategorienAsync();
            };
        }

        private async System.Threading.Tasks.Task LadeKategorienAsync()
        {
            try
            {
                txtStatusBar.Text = "Lade Kategorien...";
                var baum = await _coreService.GetKategorieBaumAsync();
                tvKategorien.ItemsSource = baum;
                txtStatusBar.Text = $"{CountNodes(baum)} Kategorien geladen";
            }
            catch (Exception ex)
            {
                txtStatusBar.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int CountNodes(System.Collections.Generic.List<CoreService.KategorieTreeNode> nodes)
        {
            return nodes.Sum(n => 1 + CountNodes(n.Kinder));
        }

        private void TxtSuche_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO: Suchfilter implementieren
        }

        private async void TvKategorien_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is CoreService.KategorieTreeNode node)
            {
                await LadeKategorieDetailsAsync(node.KKategorie);
            }
        }

        private async System.Threading.Tasks.Task LadeKategorieDetailsAsync(int kKategorie)
        {
            try
            {
                txtStatusBar.Text = "Lade Kategorie-Details...";
                _selectedKategorie = await _coreService.GetKategorieByIdAsync(kKategorie);

                if (_selectedKategorie != null)
                {
                    txtKategorieName.Text = _selectedKategorie.CName ?? "Unbenannt";
                    txtKategoriePfad.Text = _selectedKategorie.OberKategorieName != null
                        ? $"Oberkategorie: {_selectedKategorie.OberKategorieName}"
                        : "Hauptkategorie";

                    txtArtikelAnzahl.Text = _selectedKategorie.Artikel.Count.ToString();
                    txtSubkategorien.Text = _selectedKategorie.Unterkategorien.Count.ToString();
                    txtStatus.Text = _selectedKategorie.CAktiv ? "Aktiv" : "Inaktiv";

                    dgArtikel.ItemsSource = _selectedKategorie.Artikel;

                    // UI anzeigen
                    pnlStatistik.Visibility = Visibility.Visible;
                    btnBearbeiten.Visibility = Visibility.Visible;
                    btnLoeschen.Visibility = Visibility.Visible;
                    txtKeinAuswahl.Visibility = Visibility.Collapsed;

                    txtStatusBar.Text = $"Kategorie '{_selectedKategorie.CName}' geladen - {_selectedKategorie.Artikel.Count} Artikel";
                }
            }
            catch (Exception ex)
            {
                txtStatusBar.Text = $"Fehler: {ex.Message}";
            }
        }

        private void NeueKategorie_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(new KategorieDetailView(null));
            }
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKategorie != null && Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(new KategorieDetailView(_selectedKategorie.KKategorie));
            }
        }

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKategorie == null) return;

            if (_selectedKategorie.Unterkategorien.Count > 0)
            {
                MessageBox.Show("Diese Kategorie hat Unterkategorien und kann nicht geloescht werden.",
                    "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Kategorie '{_selectedKategorie.CName}' wirklich loeschen?\n\nDie {_selectedKategorie.Artikel.Count} zugeordneten Artikel bleiben erhalten.",
                "Loeschen bestaetigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.DeleteKategorieAsync(_selectedKategorie.KKategorie);
                    await LadeKategorienAsync();
                    _selectedKategorie = null;
                    txtKategorieName.Text = "Kategorie auswaehlen";
                    txtKategoriePfad.Text = "";
                    pnlStatistik.Visibility = Visibility.Collapsed;
                    btnBearbeiten.Visibility = Visibility.Collapsed;
                    btnLoeschen.Visibility = Visibility.Collapsed;
                    txtKeinAuswahl.Visibility = Visibility.Visible;
                    dgArtikel.ItemsSource = null;
                    MessageBox.Show("Kategorie wurde geloescht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DgArtikel_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgArtikel.SelectedItem is CoreService.KategorieArtikelItem artikel)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    main.ShowContent(new ArtikelDetailView(artikel.KArtikel));
                }
            }
        }
    }

    public class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
