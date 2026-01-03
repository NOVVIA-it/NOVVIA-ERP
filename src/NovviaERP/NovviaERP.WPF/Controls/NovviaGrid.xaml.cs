using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Controls
{
    /// <summary>
    /// Modus fuer den Datumsfilter
    /// </summary>
    public enum DateFilterMode
    {
        /// <summary>Kein Datumsfilter</summary>
        None,
        /// <summary>Von/Bis DatePicker</summary>
        DatePicker,
        /// <summary>JTL Monats-Navigator (&lt; 07.2022 &gt;)</summary>
        MonthNavigator
    }

    /// <summary>
    /// Universelles NOVVIA Grid - basierend auf dem funktionierenden RechnungenPage-Grid.
    /// - Design-Einstellungen aus GridStyleHelper (Zeilenhoehe, Schriftgroesse, Farben)
    /// - Spalteneinstellungen pro Benutzer in DB gespeichert
    /// - Rechtsklick-Menue fuer Spaltenauswahl
    /// - JTL-Datumslogik oben rechts (optional, 2 Modi: DatePicker oder MonthNavigator)
    /// </summary>
    [ContentProperty("Columns")]
    public partial class NovviaGrid : UserControl
    {
        private CoreService? _core;
        private DispatcherTimer? _saveTimer;
        private bool _initialized;
        private IEnumerable? _originalItemsSource;

        public NovviaGrid()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        #region Properties

        public static readonly DependencyProperty ViewNameProperty =
            DependencyProperty.Register(nameof(ViewName), typeof(string), typeof(NovviaGrid), new PropertyMetadata(""));

        /// <summary>Eindeutiger Name fuer Speicherung (z.B. "RechnungenPage")</summary>
        public string ViewName
        {
            get => (string)GetValue(ViewNameProperty);
            set => SetValue(ViewNameProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(NovviaGrid),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NovviaGrid grid)
            {
                grid._originalItemsSource = e.NewValue as IEnumerable;
                grid.ApplyDateFilter();
            }
        }

        public static readonly DependencyProperty ShowDateFilterProperty =
            DependencyProperty.Register(nameof(ShowDateFilter), typeof(bool), typeof(NovviaGrid),
                new PropertyMetadata(false, OnShowDateFilterChanged));

        /// <summary>Zeigt den JTL-Datumsfilter oben rechts an</summary>
        public bool ShowDateFilter
        {
            get => (bool)GetValue(ShowDateFilterProperty);
            set => SetValue(ShowDateFilterProperty, value);
        }

        private static void OnShowDateFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NovviaGrid grid)
            {
                grid.UpdateDateFilterVisibility();
            }
        }

        public static readonly DependencyProperty DateFilterModeProperty =
            DependencyProperty.Register(nameof(DateFilterMode), typeof(DateFilterMode), typeof(NovviaGrid),
                new PropertyMetadata(DateFilterMode.MonthNavigator, OnDateFilterModeChanged));

        /// <summary>Modus des Datumsfilters (DatePicker oder MonthNavigator)</summary>
        public DateFilterMode DateFilterMode
        {
            get => (DateFilterMode)GetValue(DateFilterModeProperty);
            set => SetValue(DateFilterModeProperty, value);
        }

        private static void OnDateFilterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NovviaGrid grid)
            {
                grid.UpdateDateFilterVisibility();
            }
        }

        public static readonly DependencyProperty AnzahlTextProperty =
            DependencyProperty.Register(nameof(AnzahlText), typeof(string), typeof(NovviaGrid),
                new PropertyMetadata("Eintraege"));

        /// <summary>Text fuer Anzahl-Anzeige (z.B. "Auftraege", "Rechnungen")</summary>
        public string AnzahlText
        {
            get => (string)GetValue(AnzahlTextProperty);
            set => SetValue(AnzahlTextProperty, value);
        }

        public static readonly DependencyProperty DatePropertyNameProperty =
            DependencyProperty.Register(nameof(DatePropertyName), typeof(string), typeof(NovviaGrid),
                new PropertyMetadata("DErstellt"));

        /// <summary>Name der Datums-Property fuer Filterung (Standard: DErstellt)</summary>
        public string DatePropertyName
        {
            get => (string)GetValue(DatePropertyNameProperty);
            set => SetValue(DatePropertyNameProperty, value);
        }

        public static readonly DependencyProperty DefaultDaysBackProperty =
            DependencyProperty.Register(nameof(DefaultDaysBack), typeof(int), typeof(NovviaGrid),
                new PropertyMetadata(30));

        /// <summary>Standard: Wie viele Tage zurueck fuer Von-Datum (0 = kein Standard)</summary>
        public int DefaultDaysBack
        {
            get => (int)GetValue(DefaultDaysBackProperty);
            set => SetValue(DefaultDaysBackProperty, value);
        }

        /// <summary>Zugriff auf die Spalten fuer XAML-Definition</summary>
        public System.Collections.ObjectModel.ObservableCollection<DataGridColumn> Columns => InnerGrid.Columns;

        /// <summary>Ausgewaehltes Element</summary>
        public object? SelectedItem
        {
            get => InnerGrid.SelectedItem;
            set => InnerGrid.SelectedItem = value;
        }

        /// <summary>Zugriff auf das innere DataGrid</summary>
        public DataGrid Grid => InnerGrid;

        /// <summary>Die gefilterten Daten (nach Datumsfilter)</summary>
        public IEnumerable? FilteredItemsSource => InnerGrid.ItemsSource;

        /// <summary>Die Original-Daten (vor Datumsfilter)</summary>
        public IEnumerable? OriginalItemsSource => _originalItemsSource;

        /// <summary>Von-Datum des Filters</summary>
        public DateTime? FilterVon
        {
            get => dpVon.SelectedDate;
            set => dpVon.SelectedDate = value;
        }

        /// <summary>Bis-Datum des Filters</summary>
        public DateTime? FilterBis
        {
            get => dpBis.SelectedDate;
            set => dpBis.SelectedDate = value;
        }

        #endregion

        #region Events

        public event EventHandler<object?>? ItemDoubleClick;
        public event EventHandler<object?>? ItemSelected;
        public event EventHandler? DateFilterChanged;

        private void InnerGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ItemDoubleClick?.Invoke(this, InnerGrid.SelectedItem);
        }

        private void InnerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ItemSelected?.Invoke(this, InnerGrid.SelectedItem);
        }

        private void DateFilter_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            ApplyDateFilter();
            DateFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ResetDateFilter_Click(object sender, RoutedEventArgs e)
        {
            dpVon.SelectedDate = null;
            dpBis.SelectedDate = null;
            ApplyDateFilter();
            DateFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MonthNavigator_Changed(object? sender, EventArgs e)
        {
            if (!_initialized) return;
            ApplyDateFilter();
            DateFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MonthNavigator_AllClicked(object? sender, EventArgs e)
        {
            if (!_initialized) return;
            ApplyDateFilter();
            DateFilterChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Initialization

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized || string.IsNullOrEmpty(ViewName)) return;
            _initialized = true;

            _core = App.Services.GetRequiredService<CoreService>();

            // 1. Design-Einstellungen laden und anwenden
            await GridStyleHelper.Instance.LoadSettingsAsync(_core, App.BenutzerId);
            GridStyleHelper.Instance.ApplyStyle(InnerGrid);

            // 2. Spalteneinstellungen aus DB laden
            await LoadColumnSettingsAsync();

            // 3. Rechtsklick-Menue fuer Spaltenauswahl
            SetupColumnChooser();

            // 4. Aenderungen automatisch speichern
            SetupAutoSave();

            // 5. JTL-Datumsfilter initialisieren
            UpdateDateFilterVisibility();
            InitializeDateFilter();
        }

        /// <summary>
        /// Aktualisiert die Sichtbarkeit der Datumsfilter-Elemente basierend auf ShowDateFilter und DateFilterMode
        /// </summary>
        private void UpdateDateFilterVisibility()
        {
            if (!ShowDateFilter)
            {
                DateFilterPanel.Visibility = Visibility.Collapsed;
                return;
            }

            DateFilterPanel.Visibility = Visibility.Visible;

            switch (DateFilterMode)
            {
                case DateFilterMode.MonthNavigator:
                    monthNavigator.Visibility = Visibility.Visible;
                    datePickerPanel.Visibility = Visibility.Collapsed;
                    break;
                case DateFilterMode.DatePicker:
                    monthNavigator.Visibility = Visibility.Collapsed;
                    datePickerPanel.Visibility = Visibility.Visible;
                    break;
                default:
                    monthNavigator.Visibility = Visibility.Collapsed;
                    datePickerPanel.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// Initialisiert den Datumsfilter mit Standardwerten
        /// </summary>
        private void InitializeDateFilter()
        {
            if (!ShowDateFilter) return;

            switch (DateFilterMode)
            {
                case DateFilterMode.MonthNavigator:
                    // MonthNavigator ist bereits auf aktuellen Monat gesetzt
                    monthNavigator.SetToCurrentMonth();
                    break;
                case DateFilterMode.DatePicker:
                    if (DefaultDaysBack > 0)
                    {
                        dpVon.SelectedDate = DateTime.Today.AddDays(-DefaultDaysBack);
                        dpBis.SelectedDate = DateTime.Today;
                    }
                    break;
            }
        }

        #endregion

        #region Date Filter

        private void ApplyDateFilter()
        {
            if (_originalItemsSource == null)
            {
                InnerGrid.ItemsSource = null;
                UpdateAnzahlText(0);
                return;
            }

            // Ermittle Von/Bis basierend auf dem aktiven Modus
            DateTime? von = null;
            DateTime? bis = null;

            if (ShowDateFilter)
            {
                switch (DateFilterMode)
                {
                    case DateFilterMode.MonthNavigator:
                        von = monthNavigator.GetVonDatum();
                        bis = monthNavigator.GetBisDatum()?.AddDays(1); // End of day
                        break;
                    case DateFilterMode.DatePicker:
                        von = dpVon.SelectedDate?.Date;
                        bis = dpBis.SelectedDate?.Date.AddDays(1); // End of day
                        break;
                }
            }

            // Kein Filter aktiv?
            if (!ShowDateFilter || (von == null && bis == null))
            {
                InnerGrid.ItemsSource = _originalItemsSource;
                UpdateAnzahlText(CountItems(_originalItemsSource));
                return;
            }

            try
            {
                var filtered = new List<object>();
                var propName = DatePropertyName;

                foreach (var item in _originalItemsSource)
                {
                    if (item == null) continue;

                    var prop = item.GetType().GetProperty(propName);
                    if (prop == null)
                    {
                        // Wenn Property nicht existiert, alle einschliessen
                        filtered.Add(item);
                        continue;
                    }

                    var value = prop.GetValue(item);
                    DateTime? itemDate = value switch
                    {
                        DateTime dt => dt,
                        DateTimeOffset dto => dto.DateTime,
                        _ => null
                    };

                    if (!itemDate.HasValue)
                    {
                        // Items ohne Datum einschliessen
                        filtered.Add(item);
                        continue;
                    }

                    var date = itemDate.Value.Date;

                    bool inRange = true;
                    if (von.HasValue && date < von.Value) inRange = false;
                    if (bis.HasValue && date >= bis.Value) inRange = false;

                    if (inRange) filtered.Add(item);
                }

                InnerGrid.ItemsSource = filtered;
                UpdateAnzahlText(filtered.Count);
            }
            catch
            {
                // Bei Fehler: Original verwenden
                InnerGrid.ItemsSource = _originalItemsSource;
                UpdateAnzahlText(CountItems(_originalItemsSource));
            }
        }

        /// <summary>
        /// Aktualisiert die Anzahl-Anzeige
        /// </summary>
        private void UpdateAnzahlText(int count)
        {
            txtAnzahl.Text = $"{count} {AnzahlText}";
        }

        /// <summary>
        /// Zaehlt die Elemente in einer IEnumerable
        /// </summary>
        private int CountItems(IEnumerable? items)
        {
            if (items == null) return 0;
            int count = 0;
            foreach (var _ in items) count++;
            return count;
        }

        #endregion

        #region Column Settings (DB)

        private async System.Threading.Tasks.Task LoadColumnSettingsAsync()
        {
            try
            {
                var json = await _core!.GetBenutzerEinstellungAsync(App.BenutzerId, $"NovviaGrid.{ViewName}");
                if (string.IsNullOrEmpty(json)) return;

                var settings = JsonSerializer.Deserialize<Dictionary<string, ColumnData>>(json);
                if (settings == null) return;

                foreach (var col in InnerGrid.Columns)
                {
                    var header = col.Header?.ToString();
                    if (string.IsNullOrEmpty(header) || !settings.TryGetValue(header, out var data)) continue;

                    col.Visibility = data.Visible ? Visibility.Visible : Visibility.Collapsed;
                    if (data.Width > 0) col.Width = new DataGridLength(data.Width);
                    if (data.Index >= 0 && data.Index < InnerGrid.Columns.Count)
                    {
                        try { col.DisplayIndex = data.Index; } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NovviaGrid.LoadSettings: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task SaveColumnSettingsAsync()
        {
            try
            {
                var settings = new Dictionary<string, ColumnData>();
                foreach (var col in InnerGrid.Columns)
                {
                    var header = col.Header?.ToString();
                    if (string.IsNullOrEmpty(header)) continue;

                    settings[header] = new ColumnData
                    {
                        Visible = col.Visibility == Visibility.Visible,
                        Width = col.ActualWidth,
                        Index = col.DisplayIndex
                    };
                }

                var json = JsonSerializer.Serialize(settings);
                await _core!.SaveBenutzerEinstellungAsync(App.BenutzerId, $"NovviaGrid.{ViewName}", json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NovviaGrid.SaveSettings: {ex.Message}");
            }
        }

        private class ColumnData
        {
            public bool Visible { get; set; } = true;
            public double Width { get; set; }
            public int Index { get; set; } = -1;
        }

        #endregion

        #region Column Chooser (Rechtsklick-Menue)

        private void SetupColumnChooser()
        {
            // Rechtsklick-Menue auf das gesamte Grid setzen
            var menu = CreateColumnMenu();
            InnerGrid.ContextMenu = menu;

            // Zusaetzlich auf Header setzen wenn verfuegbar
            InnerGrid.Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var header in FindChildren<DataGridColumnHeader>(InnerGrid))
                    {
                        if (header.ContextMenu == null)
                            header.ContextMenu = menu;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        private ContextMenu CreateColumnMenu()
        {
            var menu = new ContextMenu();
            menu.Opened += (s, e) =>
            {
                menu.Items.Clear();
                menu.Items.Add(new MenuItem { Header = "Spalten:", IsEnabled = false, FontWeight = FontWeights.Bold });
                menu.Items.Add(new Separator());

                foreach (var col in InnerGrid.Columns)
                {
                    var header = col.Header?.ToString();
                    if (string.IsNullOrEmpty(header)) continue;

                    var item = new MenuItem
                    {
                        Header = header,
                        IsCheckable = true,
                        IsChecked = col.Visibility == Visibility.Visible
                    };

                    var column = col;
                    item.Click += (ms, me) =>
                    {
                        column.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                        QueueSave();
                    };
                    menu.Items.Add(item);
                }

                menu.Items.Add(new Separator());

                var showAll = new MenuItem { Header = "Alle anzeigen" };
                showAll.Click += (ms, me) =>
                {
                    foreach (var c in InnerGrid.Columns)
                        c.Visibility = Visibility.Visible;
                    QueueSave();
                };
                menu.Items.Add(showAll);

                var reset = new MenuItem { Header = "Zuruecksetzen" };
                reset.Click += async (ms, me) =>
                {
                    await _core!.SaveBenutzerEinstellungAsync(App.BenutzerId, $"NovviaGrid.{ViewName}", "");
                    foreach (var c in InnerGrid.Columns)
                        c.Visibility = Visibility.Visible;
                };
                menu.Items.Add(reset);
            };
            return menu;
        }

        #endregion

        #region Auto-Save

        private void SetupAutoSave()
        {
            InnerGrid.ColumnReordered += (s, e) => QueueSave();

            foreach (DataGridColumn col in InnerGrid.Columns)
            {
                var dpd = DependencyPropertyDescriptor.FromProperty(
                    DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
                dpd?.AddValueChanged(col, (s, e) => QueueSave());
            }
        }

        private void QueueSave()
        {
            _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveTimer.Tick -= OnSaveTick;
            _saveTimer.Tick += OnSaveTick;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private async void OnSaveTick(object? sender, EventArgs e)
        {
            _saveTimer?.Stop();
            await SaveColumnSettingsAsync();
        }

        #endregion

        #region Helpers

        private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindChildren<T>(child)) yield return sub;
            }
        }

        /// <summary>
        /// Aktualisiert die Daten (ruft DateFilter erneut an)
        /// </summary>
        public void Refresh()
        {
            ApplyDateFilter();
        }

        /// <summary>
        /// Setzt die Daten neu (mit Filter-Anwendung)
        /// </summary>
        public void SetItemsSource(IEnumerable items)
        {
            _originalItemsSource = items;
            ApplyDateFilter();
        }

        #endregion
    }
}
