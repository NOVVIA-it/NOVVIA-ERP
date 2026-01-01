using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NovviaERP.WPF.Controls.Base
{
    public partial class EntityListControl : UserControl
    {
        // Events
        public event Func<Task>? DatenLaden;
        public event EventHandler? NeuGeklickt;
        public event EventHandler<object?>? BearbeitenGeklickt;
        public event EventHandler<object?>? LoeschenGeklickt;
        public event EventHandler<object?>? DoppelklickAufItem;
        public event EventHandler<object?>? AuswahlGeaendert;

        // Dependency Properties
        public static readonly DependencyProperty TitelProperty =
            DependencyProperty.Register("Titel", typeof(string), typeof(EntityListControl),
                new PropertyMetadata("Daten"));

        public static readonly DependencyProperty ZeigeZeitraumProperty =
            DependencyProperty.Register("ZeigeZeitraum", typeof(bool), typeof(EntityListControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ZeigeLoeschenProperty =
            DependencyProperty.Register("ZeigeLoeschen", typeof(bool), typeof(EntityListControl),
                new PropertyMetadata(false, OnZeigeLoeschenChanged));

        public static readonly DependencyProperty NeuButtonTextProperty =
            DependencyProperty.Register("NeuButtonText", typeof(string), typeof(EntityListControl),
                new PropertyMetadata("+ Neu"));

        public static readonly DependencyProperty AnzahlTextProperty =
            DependencyProperty.Register("AnzahlText", typeof(string), typeof(EntityListControl),
                new PropertyMetadata("Eintraege"));

        public string Titel
        {
            get => (string)GetValue(TitelProperty);
            set => SetValue(TitelProperty, value);
        }

        public bool ZeigeZeitraum
        {
            get => (bool)GetValue(ZeigeZeitraumProperty);
            set => SetValue(ZeigeZeitraumProperty, value);
        }

        public bool ZeigeLoeschen
        {
            get => (bool)GetValue(ZeigeLoeschenProperty);
            set => SetValue(ZeigeLoeschenProperty, value);
        }

        public string NeuButtonText
        {
            get => (string)GetValue(NeuButtonTextProperty);
            set => SetValue(NeuButtonTextProperty, value);
        }

        public string AnzahlText
        {
            get => (string)GetValue(AnzahlTextProperty);
            set => SetValue(AnzahlTextProperty, value);
        }

        // Properties
        public DataGrid DataGrid => dgItems;
        public FilterBarControl FilterBar => filterBar;
        public object? SelectedItem => dgItems.SelectedItem;
        public string Suchbegriff => filterBar.Suchbegriff;
        public string Zeitraum => filterBar.Zeitraum;

        public EntityListControl()
        {
            InitializeComponent();

            Loaded += async (s, e) =>
            {
                // Initiale Konfiguration
                filterBar.ZeigeZeitraum = ZeigeZeitraum;
                filterBar.NeuButtonText = NeuButtonText;
                filterBar.AnzahlText = AnzahlText;

                // Daten sofort laden
                await LadeDatenAsync();
            };
        }

        private static void OnZeigeLoeschenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EntityListControl ctrl)
            {
                ctrl.btnLoeschen.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Setzt die Spalten des DataGrids
        /// </summary>
        public void SetColumns(IEnumerable<DataGridColumn> columns)
        {
            dgItems.Columns.Clear();
            foreach (var col in columns)
            {
                dgItems.Columns.Add(col);
            }
        }

        /// <summary>
        /// Fuegt eine Textspalte hinzu
        /// </summary>
        public DataGridTextColumn AddTextColumn(string header, string binding, double width = 100, bool rightAlign = false)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding),
                Width = width == 0 ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(width)
            };

            if (rightAlign)
            {
                col.ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) }
                };
            }

            dgItems.Columns.Add(col);
            return col;
        }

        /// <summary>
        /// Fuegt eine Waehrungsspalte hinzu
        /// </summary>
        public DataGridTextColumn AddCurrencyColumn(string header, string binding, double width = 100)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding) { StringFormat = "N2" },
                Width = new DataGridLength(width)
            };

            col.ElementStyle = new Style(typeof(TextBlock))
            {
                Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) }
            };

            dgItems.Columns.Add(col);
            return col;
        }

        /// <summary>
        /// Fuegt eine Datumsspalte hinzu
        /// </summary>
        public DataGridTextColumn AddDateColumn(string header, string binding, double width = 100)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding) { StringFormat = "dd.MM.yyyy" },
                Width = new DataGridLength(width)
            };

            dgItems.Columns.Add(col);
            return col;
        }

        /// <summary>
        /// Setzt die Datenquelle
        /// </summary>
        public void SetItemsSource(IEnumerable items, int count)
        {
            dgItems.ItemsSource = items;
            filterBar.Anzahl = count;
            txtStatus.Text = $"{count} {AnzahlText} geladen";
        }

        /// <summary>
        /// Laedt die Daten (ruft DatenLaden-Event auf)
        /// </summary>
        public async Task LadeDatenAsync()
        {
            if (DatenLaden == null) return;

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                filterBar.SetLoading(true);
                txtStatus.Text = "Laden...";

                await DatenLaden.Invoke();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                filterBar.SetLoading(false);
            }
        }

        /// <summary>
        /// Zeigt einen Status-Text an
        /// </summary>
        public void SetStatus(string text)
        {
            txtStatus.Text = text;
        }

        private async void FilterBar_SucheGestartet(object? sender, EventArgs e)
        {
            await LadeDatenAsync();
        }

        private void FilterBar_NeuGeklickt(object? sender, EventArgs e)
        {
            NeuGeklickt?.Invoke(this, EventArgs.Empty);
        }

        private void DgItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = dgItems.SelectedItem != null;
            btnBearbeiten.IsEnabled = hasSelection;
            btnLoeschen.IsEnabled = hasSelection;

            AuswahlGeaendert?.Invoke(this, dgItems.SelectedItem);
        }

        private void DgItems_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgItems.SelectedItem != null)
            {
                DoppelklickAufItem?.Invoke(this, dgItems.SelectedItem);
            }
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgItems.SelectedItem != null)
            {
                BearbeitenGeklickt?.Invoke(this, dgItems.SelectedItem);
            }
        }

        private void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgItems.SelectedItem != null)
            {
                LoeschenGeklickt?.Invoke(this, dgItems.SelectedItem);
            }
        }
    }
}
