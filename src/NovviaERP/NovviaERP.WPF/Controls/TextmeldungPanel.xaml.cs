using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Controls
{
    /// <summary>
    /// Konvertiert NTyp zu Hintergrund-/Rahmenfarbe
    /// </summary>
    public class TextmeldungTypToColorConverter : IValueConverter
    {
        public bool IsBackground { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var typ = value as int? ?? 0;
            if (IsBackground)
            {
                return typ switch
                {
                    2 => new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE)), // Wichtig - hellrot
                    1 => new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1)), // Warnung - hellgelb
                    _ => new SolidColorBrush(Color.FromRgb(0xE3, 0xF2, 0xFD))  // Info - hellblau
                };
            }
            else
            {
                return typ switch
                {
                    2 => new SolidColorBrush(Color.FromRgb(0xEF, 0x9A, 0x9A)), // Wichtig - rot
                    1 => new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x82)), // Warnung - gelb
                    _ => new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9))  // Info - blau
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Wiederverwendbares Panel zur Anzeige von Textmeldungen fuer Entities
    /// </summary>
    public partial class TextmeldungPanel : UserControl
    {
        private CoreService? _core;
        private string _entityTyp = "";
        private int _kEntity;
        private string? _bereich;
        private List<CoreService.EntityTextmeldung>? _meldungen;

        public event EventHandler? MeldungenChanged;

        public TextmeldungPanel()
        {
            InitializeComponent();
        }

        private CoreService Core => _core ??= App.Services.GetRequiredService<CoreService>();

        /// <summary>
        /// Laedt Textmeldungen fuer eine Entity
        /// </summary>
        public async System.Threading.Tasks.Task LoadAsync(string entityTyp, int kEntity, string? bereich = null)
        {
            _entityTyp = entityTyp;
            _kEntity = kEntity;
            _bereich = bereich;

            try
            {
                _meldungen = await Core.GetEntityTextmeldungenAsync(entityTyp, kEntity, bereich);

                if (_meldungen.Count > 0)
                {
                    lstMeldungen.ItemsSource = _meldungen;
                    txtMeldungHeader.Text = _meldungen.Count == 1 ? "1 Hinweis" : $"{_meldungen.Count} Hinweise";
                    pnlMeldungen.Visibility = Visibility.Visible;
                    txtKeineMeldungen.Visibility = Visibility.Collapsed;

                    // Farbe basierend auf hoechstem Typ
                    var maxTyp = _meldungen.Max(m => m.NTyp);
                    switch (maxTyp)
                    {
                        case 2: // Wichtig
                            pnlMeldungen.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0xFF, 0xEB, 0xEE));
                            pnlMeldungen.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0xEF, 0x9A, 0x9A));
                            break;
                        case 1: // Warnung
                            pnlMeldungen.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0xFF, 0xF8, 0xE1));
                            pnlMeldungen.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0xFF, 0xE0, 0x82));
                            break;
                        default: // Info
                            pnlMeldungen.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0xE3, 0xF2, 0xFD));
                            pnlMeldungen.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0x90, 0xCA, 0xF9));
                            break;
                    }
                }
                else
                {
                    pnlMeldungen.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextmeldungPanel.LoadAsync Fehler: {ex.Message}");
            }
        }

        /// <summary>
        /// Loescht die angezeigten Meldungen
        /// </summary>
        public void Clear()
        {
            _entityTyp = "";
            _kEntity = 0;
            _bereich = null;
            _meldungen = null;
            lstMeldungen.ItemsSource = null;
            pnlMeldungen.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Zeigt Popup-Meldungen an (fuer Entity-Auswahl)
        /// </summary>
        public async System.Threading.Tasks.Task ShowPopupAsync(string entityTyp, int kEntity, string bereich, string entityName)
        {
            try
            {
                var meldungen = await Core.GetPopupTextmeldungenAsync(entityTyp, kEntity, bereich);
                if (meldungen.Count == 0) return;

                var message = string.Join("\n\n", meldungen.Select(m =>
                    $"[{(m.NTyp == 2 ? "WICHTIG" : m.NTyp == 1 ? "Warnung" : "Info")}] {m.CTitel}\n{m.CText}"));

                var icon = meldungen.Any(m => m.NTyp == 2) ? MessageBoxImage.Warning :
                           meldungen.Any(m => m.NTyp == 1) ? MessageBoxImage.Exclamation :
                           MessageBoxImage.Information;

                MessageBox.Show(message, $"Hinweise: {entityName}", MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowPopupAsync Fehler: {ex.Message}");
            }
        }

        private async void MeldungVerwalten_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TextmeldungZuweisungDialog(Core, _entityTyp, _kEntity);
            if (dialog.ShowDialog() == true)
            {
                await LoadAsync(_entityTyp, _kEntity, _bereich);
                MeldungenChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async void MeldungEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int kEntityTextmeldung)
            {
                var result = MessageBox.Show("Textmeldung von diesem Datensatz entfernen?",
                    "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await Core.RemoveEntityTextmeldungAsync(kEntityTextmeldung);
                        await LoadAsync(_entityTyp, _kEntity, _bereich);
                        MeldungenChanged?.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dialog zum Zuweisen von Textmeldungen
    /// </summary>
    public class TextmeldungZuweisungDialog : Window
    {
        private readonly CoreService _core;
        private readonly string _entityTyp;
        private readonly int _kEntity;
        private readonly ListBox _lstVerfuegbar;
        private List<CoreService.Textmeldung>? _textmeldungen;

        public TextmeldungZuweisungDialog(CoreService core, string entityTyp, int kEntity)
        {
            _core = core;
            _entityTyp = entityTyp;
            _kEntity = kEntity;

            Title = $"Textmeldungen zuweisen - {entityTyp} #{kEntity}";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = "Waehlen Sie Textmeldungen aus, die diesem Datensatz zugewiesen werden sollen:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            _lstVerfuegbar = new ListBox
            {
                SelectionMode = SelectionMode.Multiple,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _lstVerfuegbar.ItemTemplate = CreateItemTemplate();
            Grid.SetRow(_lstVerfuegbar, 1);
            grid.Children.Add(_lstVerfuegbar);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnOk = new Button { Content = "Zuweisen", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 10, 0) };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new Button { Content = "Abbrechen", Padding = new Thickness(20, 8, 20, 8) };
            btnCancel.Click += (s, e) => DialogResult = false;
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            Content = grid;
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private DataTemplate CreateItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.MarginProperty, new Thickness(5, 3, 5, 3));

            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CTitel"));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

            var bereiche = new FrameworkElementFactory(typeof(TextBlock));
            bereiche.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("BereicheText"));
            bereiche.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray);
            bereiche.SetValue(TextBlock.FontSizeProperty, 11.0);

            factory.AppendChild(title);
            factory.AppendChild(bereiche);
            template.VisualTree = factory;
            return template;
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            _textmeldungen = await _core.GetTextmeldungenAsync();
            var zugewiesen = await _core.GetEntityTextmeldungenAsync(_entityTyp, _kEntity);
            var zugewiesenIds = zugewiesen.Select(z => z.KTextmeldung).ToHashSet();

            // Nur aktive und nicht bereits zugewiesene anzeigen
            var verfuegbar = _textmeldungen.Where(t => t.NAktiv && !zugewiesenIds.Contains(t.KTextmeldung)).ToList();
            _lstVerfuegbar.ItemsSource = verfuegbar;
        }

        private async void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (CoreService.Textmeldung meldung in _lstVerfuegbar.SelectedItems)
                {
                    await _core.AddEntityTextmeldungAsync(meldung.KTextmeldung, _entityTyp, _kEntity, App.BenutzerId);
                }
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
