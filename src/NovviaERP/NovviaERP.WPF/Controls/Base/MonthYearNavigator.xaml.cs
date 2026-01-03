using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Controls.Base
{
    /// <summary>
    /// Zeitraum-Modi fuer den Navigator
    /// </summary>
    public enum ZeitraumModus
    {
        Monat,
        Letzte30Tage,
        Letzte90Tage,
        Letzte360Tage,
        Jahr,
        Alle
    }

    /// <summary>
    /// JTL-Style Monats/Jahr Navigator mit Zeitraum-Dropdown.
    /// Speichert die Einstellungen pro Benutzer in der DB.
    /// </summary>
    public partial class MonthYearNavigator : UserControl
    {
        private DateTime _selectedDate;
        private Popup? _calendarPopup;
        private Calendar? _calendar;
        private bool _isInitializing = true;
        private string _settingsKey = "";

        #region Dependency Properties

        public static readonly DependencyProperty SelectedMonthProperty =
            DependencyProperty.Register(nameof(SelectedMonth), typeof(DateTime?), typeof(MonthYearNavigator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedMonthChanged));

        public DateTime? SelectedMonth
        {
            get => (DateTime?)GetValue(SelectedMonthProperty);
            set => SetValue(SelectedMonthProperty, value);
        }

        private static void OnSelectedMonthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonthYearNavigator nav)
                nav.UpdateDisplay();
        }

        public static readonly DependencyProperty ZeitraumProperty =
            DependencyProperty.Register(nameof(Zeitraum), typeof(ZeitraumModus), typeof(MonthYearNavigator),
                new PropertyMetadata(ZeitraumModus.Monat, OnZeitraumChanged));

        public ZeitraumModus Zeitraum
        {
            get => (ZeitraumModus)GetValue(ZeitraumProperty);
            set => SetValue(ZeitraumProperty, value);
        }

        private static void OnZeitraumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonthYearNavigator nav && !nav._isInitializing)
            {
                nav.UpdateDisplay();
                nav.SaveSettingsAsync();
            }
        }

        public static readonly DependencyProperty SettingsKeyProperty =
            DependencyProperty.Register(nameof(SettingsKey), typeof(string), typeof(MonthYearNavigator),
                new PropertyMetadata("", OnSettingsKeyChanged));

        /// <summary>Schluessel fuer DB-Speicherung (z.B. "BestellungenView")</summary>
        public string SettingsKey
        {
            get => (string)GetValue(SettingsKeyProperty);
            set => SetValue(SettingsKeyProperty, value);
        }

        private static void OnSettingsKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonthYearNavigator nav && !string.IsNullOrEmpty(e.NewValue?.ToString()))
            {
                nav._settingsKey = e.NewValue.ToString()!;
                nav.LoadSettingsAsync();
            }
        }

        #endregion

        #region Events

        public event EventHandler? MonthChanged;
        public event EventHandler? AllClicked;

        #endregion

        public MonthYearNavigator()
        {
            InitializeComponent();
            _selectedDate = DateTime.Today;
            SelectedMonth = DateTime.Today;
            CreateCalendarPopup();

            Loaded += (s, e) =>
            {
                _isInitializing = false;
                UpdateDisplay();
            };
        }

        #region Public Methods

        /// <summary>
        /// Gibt das Von-Datum basierend auf dem gewaehlten Zeitraum zurueck
        /// </summary>
        public DateTime? GetVonDatum()
        {
            return Zeitraum switch
            {
                ZeitraumModus.Alle => null,
                ZeitraumModus.Monat => new DateTime(_selectedDate.Year, _selectedDate.Month, 1),
                ZeitraumModus.Jahr => new DateTime(_selectedDate.Year, 1, 1),
                ZeitraumModus.Letzte30Tage => DateTime.Today.AddDays(-30),
                ZeitraumModus.Letzte90Tage => DateTime.Today.AddDays(-90),
                ZeitraumModus.Letzte360Tage => DateTime.Today.AddDays(-360),
                _ => null
            };
        }

        /// <summary>
        /// Gibt das Bis-Datum basierend auf dem gewaehlten Zeitraum zurueck
        /// </summary>
        public DateTime? GetBisDatum()
        {
            return Zeitraum switch
            {
                ZeitraumModus.Alle => null,
                ZeitraumModus.Monat => new DateTime(_selectedDate.Year, _selectedDate.Month, 1).AddMonths(1).AddDays(-1),
                ZeitraumModus.Jahr => new DateTime(_selectedDate.Year, 12, 31),
                ZeitraumModus.Letzte30Tage => DateTime.Today,
                ZeitraumModus.Letzte90Tage => DateTime.Today,
                ZeitraumModus.Letzte360Tage => DateTime.Today,
                _ => null
            };
        }

        public void SetToCurrentMonth()
        {
            _selectedDate = DateTime.Today;
            SelectedMonth = DateTime.Today;
            UpdateDisplay();
            MonthChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearFilter()
        {
            Zeitraum = ZeitraumModus.Alle;
            UpdateDisplay();
            AllClicked?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Event Handlers

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            switch (Zeitraum)
            {
                case ZeitraumModus.Monat:
                    _selectedDate = _selectedDate.AddMonths(-1);
                    break;
                case ZeitraumModus.Jahr:
                    _selectedDate = _selectedDate.AddYears(-1);
                    break;
                default:
                    // Bei Tage-Auswahl: zum vorigen Zeitraum wechseln
                    _selectedDate = _selectedDate.AddMonths(-1);
                    break;
            }
            SelectedMonth = _selectedDate;
            UpdateDisplay();
            MonthChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            switch (Zeitraum)
            {
                case ZeitraumModus.Monat:
                    _selectedDate = _selectedDate.AddMonths(1);
                    break;
                case ZeitraumModus.Jahr:
                    _selectedDate = _selectedDate.AddYears(1);
                    break;
                default:
                    _selectedDate = _selectedDate.AddMonths(1);
                    break;
            }
            SelectedMonth = _selectedDate;
            UpdateDisplay();
            MonthChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnMonthYear_Click(object sender, RoutedEventArgs e)
        {
            ShowCalendarPopup();
        }

        private void CmbZeitraum_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            var tag = (cmbZeitraum.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            Zeitraum = tag switch
            {
                "Monat" => ZeitraumModus.Monat,
                "30" => ZeitraumModus.Letzte30Tage,
                "90" => ZeitraumModus.Letzte90Tage,
                "360" => ZeitraumModus.Letzte360Tage,
                "Jahr" => ZeitraumModus.Jahr,
                "Alle" => ZeitraumModus.Alle,
                _ => ZeitraumModus.Monat
            };

            UpdateDisplay();

            if (Zeitraum == ZeitraumModus.Alle)
                AllClicked?.Invoke(this, EventArgs.Empty);
            else
                MonthChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Calendar Popup

        private void CreateCalendarPopup()
        {
            _calendar = new Calendar
            {
                DisplayMode = CalendarMode.Year,
                SelectionMode = CalendarSelectionMode.SingleDate,
                SelectedDate = _selectedDate
            };
            _calendar.SelectedDatesChanged += Calendar_SelectedDatesChanged;
            _calendar.DisplayModeChanged += Calendar_DisplayModeChanged;

            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = _calendar
            };

            _calendarPopup = new Popup
            {
                Child = border,
                PlacementTarget = btnMonthYear,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };
        }

        private void ShowCalendarPopup()
        {
            if (_calendarPopup != null && _calendar != null)
            {
                _calendar.DisplayDate = _selectedDate;
                _calendar.DisplayMode = Zeitraum == ZeitraumModus.Jahr ? CalendarMode.Decade : CalendarMode.Year;
                _calendarPopup.IsOpen = true;
            }
        }

        private void Calendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_calendar?.SelectedDate != null)
            {
                _selectedDate = _calendar.SelectedDate.Value;
                SelectedMonth = _selectedDate;
                UpdateDisplay();
                _calendarPopup!.IsOpen = false;
                MonthChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Calendar_DisplayModeChanged(object? sender, CalendarModeChangedEventArgs e)
        {
            if (e.NewMode == CalendarMode.Month && _calendar?.DisplayDate != null)
            {
                _selectedDate = new DateTime(_calendar.DisplayDate.Year, _calendar.DisplayDate.Month, 1);
                SelectedMonth = _selectedDate;
                UpdateDisplay();
                _calendarPopup!.IsOpen = false;
                MonthChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (e.NewMode == CalendarMode.Year && Zeitraum == ZeitraumModus.Jahr && _calendar?.DisplayDate != null)
            {
                _selectedDate = new DateTime(_calendar.DisplayDate.Year, 1, 1);
                SelectedMonth = _selectedDate;
                UpdateDisplay();
                _calendarPopup!.IsOpen = false;
                MonthChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Display

        private void UpdateDisplay()
        {
            // Buttons aktivieren/deaktivieren
            bool showNavigation = Zeitraum != ZeitraumModus.Alle &&
                                  Zeitraum != ZeitraumModus.Letzte30Tage &&
                                  Zeitraum != ZeitraumModus.Letzte90Tage &&
                                  Zeitraum != ZeitraumModus.Letzte360Tage;

            btnPrev.Visibility = showNavigation ? Visibility.Visible : Visibility.Collapsed;
            btnNext.Visibility = showNavigation ? Visibility.Visible : Visibility.Collapsed;
            btnMonthYear.Visibility = showNavigation ? Visibility.Visible : Visibility.Collapsed;

            // Text aktualisieren
            txtMonthYear.Text = Zeitraum switch
            {
                ZeitraumModus.Monat => _selectedDate.ToString("MM.yyyy"),
                ZeitraumModus.Jahr => _selectedDate.ToString("yyyy"),
                _ => ""
            };

            txtMonthYear.FontStyle = FontStyles.Normal;
            txtMonthYear.Foreground = System.Windows.Media.Brushes.Black;

            // ComboBox synchronisieren
            if (cmbZeitraum != null && !_isInitializing)
            {
                _isInitializing = true;
                var tagToSelect = Zeitraum switch
                {
                    ZeitraumModus.Monat => "Monat",
                    ZeitraumModus.Letzte30Tage => "30",
                    ZeitraumModus.Letzte90Tage => "90",
                    ZeitraumModus.Letzte360Tage => "360",
                    ZeitraumModus.Jahr => "Jahr",
                    ZeitraumModus.Alle => "Alle",
                    _ => "Monat"
                };

                foreach (ComboBoxItem item in cmbZeitraum.Items)
                {
                    if (item.Tag?.ToString() == tagToSelect)
                    {
                        cmbZeitraum.SelectedItem = item;
                        break;
                    }
                }
                _isInitializing = false;
            }
        }

        #endregion

        #region Settings (DB)

        private async void LoadSettingsAsync()
        {
            if (string.IsNullOrEmpty(_settingsKey)) return;

            try
            {
                var core = App.Services.GetService<CoreService>();
                if (core == null) return;

                var zeitraumStr = await core.GetBenutzerEinstellungAsync(App.BenutzerId, $"MonthNavigator.{_settingsKey}.Zeitraum");
                if (!string.IsNullOrEmpty(zeitraumStr) && Enum.TryParse<ZeitraumModus>(zeitraumStr, out var modus))
                {
                    _isInitializing = true;
                    Zeitraum = modus;
                    UpdateDisplay();
                    _isInitializing = false;
                }
            }
            catch
            {
                // Ignorieren - Standardwerte verwenden
            }
        }

        private async void SaveSettingsAsync()
        {
            if (string.IsNullOrEmpty(_settingsKey)) return;

            try
            {
                var core = App.Services.GetService<CoreService>();
                if (core == null) return;

                await core.SaveBenutzerEinstellungAsync(App.BenutzerId, $"MonthNavigator.{_settingsKey}.Zeitraum", Zeitraum.ToString());
            }
            catch
            {
                // Ignorieren
            }
        }

        #endregion
    }
}
