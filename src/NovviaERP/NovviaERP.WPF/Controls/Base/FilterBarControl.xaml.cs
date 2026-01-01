using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Services.Base;

namespace NovviaERP.WPF.Controls.Base
{
    /// <summary>
    /// Wiederverwendbare Filterleiste mit JTL-Zeitraum
    /// WICHTIG: Aktualisierung nur bei expliziter Suche (Enter oder Button), NICHT bei Filter-Auswahl!
    /// </summary>
    public partial class FilterBarControl : UserControl
    {
        private bool _isLoading = false;

        // Events
        public event EventHandler? SucheGestartet;
        public event EventHandler? NeuGeklickt;

        // Dependency Properties
        public static readonly DependencyProperty TitelProperty =
            DependencyProperty.Register("Titel", typeof(string), typeof(FilterBarControl),
                new PropertyMetadata(""));

        public static readonly DependencyProperty ZeigeZeitraumProperty =
            DependencyProperty.Register("ZeigeZeitraum", typeof(bool), typeof(FilterBarControl),
                new PropertyMetadata(true, OnZeigeZeitraumChanged));

        public static readonly DependencyProperty ZeigeNeuButtonProperty =
            DependencyProperty.Register("ZeigeNeuButton", typeof(bool), typeof(FilterBarControl),
                new PropertyMetadata(true, OnZeigeNeuButtonChanged));

        public static readonly DependencyProperty NeuButtonTextProperty =
            DependencyProperty.Register("NeuButtonText", typeof(string), typeof(FilterBarControl),
                new PropertyMetadata("+ Neu", OnNeuButtonTextChanged));

        public static readonly DependencyProperty AnzahlProperty =
            DependencyProperty.Register("Anzahl", typeof(int), typeof(FilterBarControl),
                new PropertyMetadata(0, OnAnzahlChanged));

        public static readonly DependencyProperty AnzahlTextProperty =
            DependencyProperty.Register("AnzahlText", typeof(string), typeof(FilterBarControl),
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

        public bool ZeigeNeuButton
        {
            get => (bool)GetValue(ZeigeNeuButtonProperty);
            set => SetValue(ZeigeNeuButtonProperty, value);
        }

        public string NeuButtonText
        {
            get => (string)GetValue(NeuButtonTextProperty);
            set => SetValue(NeuButtonTextProperty, value);
        }

        public int Anzahl
        {
            get => (int)GetValue(AnzahlProperty);
            set => SetValue(AnzahlProperty, value);
        }

        public string AnzahlText
        {
            get => (string)GetValue(AnzahlTextProperty);
            set => SetValue(AnzahlTextProperty, value);
        }

        // Aktuelle Filterwerte
        public string Suchbegriff => txtSuche.Text.Trim();
        public string Zeitraum => cmbZeitraum.SelectedItem?.ToString() ?? "Alle";

        public FilterBarControl()
        {
            InitializeComponent();

            // Zeitraum-Optionen laden (KEINE automatische Aktualisierung bei Auswahl!)
            cmbZeitraum.ItemsSource = BaseDatabaseService.JtlZeitraumOptionen;
            cmbZeitraum.SelectedIndex = 0;
        }

        private static void OnZeigeZeitraumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterBarControl ctrl)
            {
                ctrl.cmbZeitraum.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnZeigeNeuButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterBarControl ctrl)
            {
                ctrl.btnNeu.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnNeuButtonTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterBarControl ctrl)
            {
                ctrl.btnNeu.Content = e.NewValue?.ToString() ?? "+ Neu";
            }
        }

        private static void OnAnzahlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterBarControl ctrl)
            {
                ctrl.UpdateAnzahlText();
            }
        }

        private void UpdateAnzahlText()
        {
            txtAnzahl.Text = $"{Anzahl} {AnzahlText}";
        }

        private void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SucheGestartet?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Suchen_Click(object sender, RoutedEventArgs e)
        {
            SucheGestartet?.Invoke(this, EventArgs.Empty);
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            NeuGeklickt?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Fuegt einen zusaetzlichen Filter (ComboBox) hinzu
        /// HINWEIS: Keine automatische Aktualisierung bei Auswahl - nur bei Suche!
        /// </summary>
        public ComboBox AddFilter(string label, IEnumerable<object> items, string displayMemberPath = "")
        {
            var lbl = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 5, 0)
            };
            pnlExtraFilter.Children.Add(lbl);

            var cmb = new ComboBox
            {
                Width = 150,
                Padding = new Thickness(8, 6, 8, 6),
                ItemsSource = items
            };

            if (!string.IsNullOrEmpty(displayMemberPath))
            {
                cmb.DisplayMemberPath = displayMemberPath;
            }

            // KEINE automatische Aktualisierung bei Auswahl!
            cmb.SelectedIndex = 0;

            pnlExtraFilter.Children.Add(cmb);
            return cmb;
        }

        /// <summary>
        /// Fuegt einen CheckBox-Filter hinzu
        /// HINWEIS: Keine automatische Aktualisierung - nur bei Suche!
        /// </summary>
        public CheckBox AddCheckFilter(string label, bool defaultValue = false)
        {
            var chk = new CheckBox
            {
                Content = label,
                IsChecked = defaultValue,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 0, 0)
            };

            // KEINE automatische Aktualisierung!
            pnlExtraFilter.Children.Add(chk);
            return chk;
        }

        /// <summary>
        /// Setzt den Loading-Status
        /// </summary>
        public void SetLoading(bool isLoading)
        {
            _isLoading = isLoading;
            btnSuchen.IsEnabled = !isLoading;
        }

        /// <summary>
        /// Leert das Suchfeld
        /// </summary>
        public void ClearSearch()
        {
            txtSuche.Text = "";
        }

        /// <summary>
        /// Setzt den Fokus auf das Suchfeld
        /// </summary>
        public void FocusSearch()
        {
            txtSuche.Focus();
        }

        /// <summary>
        /// Loest die Suche programmatisch aus
        /// </summary>
        public void TriggerSearch()
        {
            SucheGestartet?.Invoke(this, EventArgs.Empty);
        }
    }
}
