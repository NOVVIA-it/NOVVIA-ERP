using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NovviaERP.WPF.Controls
{
    /// <summary>
    /// Wiederverwendbares Control für Positionen (Aufträge, Rechnungen, Bestellungen, etc.)
    /// </summary>
    public partial class PositionenControl : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PositionenControl),
                new PropertyMetadata(false, OnReadOnlyChanged));

        public static readonly DependencyProperty ShowToolbarProperty =
            DependencyProperty.Register(nameof(ShowToolbar), typeof(bool), typeof(PositionenControl),
                new PropertyMetadata(true, OnShowToolbarChanged));

        public static readonly DependencyProperty AllowAddProperty =
            DependencyProperty.Register(nameof(AllowAdd), typeof(bool), typeof(PositionenControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty AllowDeleteProperty =
            DependencyProperty.Register(nameof(AllowDelete), typeof(bool), typeof(PositionenControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty AllowEditProperty =
            DependencyProperty.Register(nameof(AllowEdit), typeof(bool), typeof(PositionenControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(PositionenControl),
                new PropertyMetadata("Positionen"));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public bool ShowToolbar
        {
            get => (bool)GetValue(ShowToolbarProperty);
            set => SetValue(ShowToolbarProperty, value);
        }

        public bool AllowAdd
        {
            get => (bool)GetValue(AllowAddProperty);
            set => SetValue(AllowAddProperty, value);
        }

        public bool AllowDelete
        {
            get => (bool)GetValue(AllowDeleteProperty);
            set => SetValue(AllowDeleteProperty, value);
        }

        public bool AllowEdit
        {
            get => (bool)GetValue(AllowEditProperty);
            set => SetValue(AllowEditProperty, value);
        }

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<PositionEventArgs>? PositionAdded;
        public event EventHandler<PositionEventArgs>? PositionRemoved;
        public event EventHandler<PositionEventArgs>? PositionChanged;
        public event EventHandler? PositionenChanged;
        public event EventHandler<ArtikelSucheEventArgs>? ArtikelSuche;
        public event EventHandler? FreipositionRequested;

        #endregion

        #region Fields

        private ObservableCollection<PositionViewModel> _positionen = new();

        #endregion

        #region Constructor

        public PositionenControl()
        {
            InitializeComponent();
            dgPositionen.ItemsSource = _positionen;
            _positionen.CollectionChanged += (s, e) => UpdateSummen();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Positionen setzen
        /// </summary>
        public void SetPositionen(IEnumerable<PositionViewModel> positionen)
        {
            _positionen.Clear();
            foreach (var pos in positionen)
            {
                pos.PropertyChanged += Position_PropertyChanged;
                _positionen.Add(pos);
            }
            UpdateSummen();
            UpdateAnzahl();
        }

        /// <summary>
        /// Positionen als Liste abrufen
        /// </summary>
        public IReadOnlyList<PositionViewModel> GetPositionen() => _positionen.ToList();

        /// <summary>
        /// Position hinzufügen
        /// </summary>
        public void AddPosition(PositionViewModel position)
        {
            position.PropertyChanged += Position_PropertyChanged;
            _positionen.Add(position);
            PositionAdded?.Invoke(this, new PositionEventArgs(position));
            PositionenChanged?.Invoke(this, EventArgs.Empty);
            UpdateSummen();
            UpdateAnzahl();
        }

        /// <summary>
        /// Position entfernen
        /// </summary>
        public void RemovePosition(PositionViewModel position)
        {
            position.PropertyChanged -= Position_PropertyChanged;
            _positionen.Remove(position);
            PositionRemoved?.Invoke(this, new PositionEventArgs(position));
            PositionenChanged?.Invoke(this, EventArgs.Empty);
            UpdateSummen();
            UpdateAnzahl();
        }

        /// <summary>
        /// Alle Positionen löschen
        /// </summary>
        public void Clear()
        {
            foreach (var pos in _positionen)
                pos.PropertyChanged -= Position_PropertyChanged;
            _positionen.Clear();
            PositionenChanged?.Invoke(this, EventArgs.Empty);
            UpdateSummen();
            UpdateAnzahl();
        }

        /// <summary>
        /// Ausgewählte Position
        /// </summary>
        public PositionViewModel? SelectedPosition => dgPositionen.SelectedItem as PositionViewModel;

        /// <summary>
        /// Summe Netto
        /// </summary>
        public decimal SummeNetto => _positionen.Sum(p => p.GesamtNetto);

        /// <summary>
        /// Summe Brutto
        /// </summary>
        public decimal SummeBrutto => _positionen.Sum(p => p.GesamtBrutto);

        #endregion

        #region Private Methods

        private void UpdateSummen()
        {
            txtSummeNetto.Text = SummeNetto.ToString("N2");
            txtSummeBrutto.Text = SummeBrutto.ToString("N2");
        }

        private void UpdateAnzahl()
        {
            var anzahl = _positionen.Count;
            txtAnzahl.Text = anzahl == 1 ? "1 Position" : $"{anzahl} Positionen";
        }

        private void Position_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is PositionViewModel pos)
            {
                PositionChanged?.Invoke(this, new PositionEventArgs(pos));
                PositionenChanged?.Invoke(this, EventArgs.Empty);
                UpdateSummen();
            }
        }

        private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PositionenControl ctrl)
            {
                ctrl.dgPositionen.IsReadOnly = (bool)e.NewValue;
                ctrl.pnlToolbar.Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static void OnShowToolbarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PositionenControl ctrl)
            {
                ctrl.pnlToolbar.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateButtonStates()
        {
            var hasSelection = dgPositionen.SelectedItem != null;
            btnLoeschen.IsEnabled = hasSelection && AllowDelete && !IsReadOnly;
            btnHoch.IsEnabled = hasSelection && !IsReadOnly;
            btnRunter.IsEnabled = hasSelection && !IsReadOnly;
        }

        #endregion

        #region Event Handlers

        private void DgPositionen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void TxtArtikelSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnSuchen_Click(sender, e);
        }

        private void BtnSuchen_Click(object sender, RoutedEventArgs e)
        {
            var suchtext = txtArtikelSuche.Text.Trim();
            if (string.IsNullOrEmpty(suchtext)) return;

            ArtikelSuche?.Invoke(this, new ArtikelSucheEventArgs(suchtext));
        }

        private void BtnFreiposition_Click(object sender, RoutedEventArgs e)
        {
            if (!AllowAdd || IsReadOnly) return;
            FreipositionRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (!AllowDelete || IsReadOnly) return;
            if (SelectedPosition == null) return;

            if (MessageBox.Show($"Position '{SelectedPosition.Bezeichnung}' wirklich löschen?",
                "Position löschen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RemovePosition(SelectedPosition);
            }
        }

        private void BtnRabatt_Click(object sender, RoutedEventArgs e)
        {
            if (!AllowEdit || IsReadOnly) return;
            if (!_positionen.Any()) return;

            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Rabatt in % für alle Positionen eingeben:",
                "Rabatt setzen", "0");

            if (decimal.TryParse(input.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rabatt))
            {
                foreach (var pos in _positionen)
                    pos.Rabatt = rabatt;

                PositionenChanged?.Invoke(this, EventArgs.Empty);
                UpdateSummen();
            }
        }

        private void BtnPreiseNeu_Click(object sender, RoutedEventArgs e)
        {
            // Event für Parent zum Preise neu laden
            PositionenChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnHoch_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPosition == null || IsReadOnly) return;

            var idx = _positionen.IndexOf(SelectedPosition);
            if (idx > 0)
            {
                _positionen.Move(idx, idx - 1);
                PositionenChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BtnRunter_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPosition == null || IsReadOnly) return;

            var idx = _positionen.IndexOf(SelectedPosition);
            if (idx < _positionen.Count - 1)
            {
                _positionen.Move(idx, idx + 1);
                PositionenChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion
    }

    #region ViewModels & EventArgs

    /// <summary>
    /// ViewModel für eine Position (universell für alle Belegarten)
    /// </summary>
    public class PositionViewModel : INotifyPropertyChanged
    {
        private int _id;
        private int? _artikelId;
        private string _artNr = "";
        private string _bezeichnung = "";
        private decimal _menge = 1;
        private string _einheit = "Stk";
        private decimal _einzelpreisNetto;
        private decimal _mwStSatz = 19;
        private decimal _rabatt;
        private int _posTyp;
        private int _sort;

        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public int? ArtikelId { get => _artikelId; set { _artikelId = value; OnPropertyChanged(); } }
        public string ArtNr { get => _artNr; set { _artNr = value; OnPropertyChanged(); } }
        public string Bezeichnung { get => _bezeichnung; set { _bezeichnung = value; OnPropertyChanged(); } }
        public decimal Menge { get => _menge; set { _menge = value; OnPropertyChanged(); OnPropertyChanged(nameof(GesamtNetto)); OnPropertyChanged(nameof(GesamtBrutto)); } }
        public string Einheit { get => _einheit; set { _einheit = value; OnPropertyChanged(); } }
        public decimal EinzelpreisNetto { get => _einzelpreisNetto; set { _einzelpreisNetto = value; OnPropertyChanged(); OnPropertyChanged(nameof(GesamtNetto)); OnPropertyChanged(nameof(GesamtBrutto)); } }
        public decimal MwStSatz { get => _mwStSatz; set { _mwStSatz = value; OnPropertyChanged(); OnPropertyChanged(nameof(GesamtBrutto)); } }
        public decimal Rabatt { get => _rabatt; set { _rabatt = value; OnPropertyChanged(); OnPropertyChanged(nameof(GesamtNetto)); OnPropertyChanged(nameof(GesamtBrutto)); } }
        public int PosTyp { get => _posTyp; set { _posTyp = value; OnPropertyChanged(); OnPropertyChanged(nameof(PosTypDisplay)); } }
        public int Sort { get => _sort; set { _sort = value; OnPropertyChanged(); } }

        // Berechnete Eigenschaften
        public decimal GesamtNetto => Menge * EinzelpreisNetto * (1 - Rabatt / 100);
        public decimal GesamtBrutto => GesamtNetto * (1 + MwStSatz / 100);
        public string PosTypDisplay => PosTyp switch { 0 => "", 1 => "A", 2 => "F", _ => "" };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PositionEventArgs : EventArgs
    {
        public PositionViewModel Position { get; }
        public PositionEventArgs(PositionViewModel position) => Position = position;
    }

    public class ArtikelSucheEventArgs : EventArgs
    {
        public string Suchtext { get; }
        public ArtikelSucheEventArgs(string suchtext) => Suchtext = suchtext;
    }

    #endregion
}
