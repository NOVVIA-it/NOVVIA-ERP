using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Controls
{
    public partial class EntitySearchControl : UserControl
    {
        private CoreService? _core;
        private bool _filterLoaded = false;

        public enum SearchEntityType { Kunde, Artikel, Lieferant }

        // Dependency Properties
        public static readonly DependencyProperty EntityTypeProperty =
            DependencyProperty.Register("EntityType", typeof(SearchEntityType), typeof(EntitySearchControl),
                new PropertyMetadata(SearchEntityType.Kunde, OnEntityTypeChanged));

        public static readonly DependencyProperty ShowFiltersProperty =
            DependencyProperty.Register("ShowFilters", typeof(bool), typeof(EntitySearchControl),
                new PropertyMetadata(false, OnShowFiltersChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(EntitySearchControl),
                new PropertyMetadata(""));

        public static readonly DependencyProperty SelectedIdProperty =
            DependencyProperty.Register("SelectedId", typeof(int), typeof(EntitySearchControl),
                new PropertyMetadata(0));

        public static readonly DependencyProperty SelectedTextProperty =
            DependencyProperty.Register("SelectedText", typeof(string), typeof(EntitySearchControl),
                new PropertyMetadata(""));

        public SearchEntityType EntityType
        {
            get => (SearchEntityType)GetValue(EntityTypeProperty);
            set => SetValue(EntityTypeProperty, value);
        }

        public bool ShowFilters
        {
            get => (bool)GetValue(ShowFiltersProperty);
            set => SetValue(ShowFiltersProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public int SelectedId
        {
            get => (int)GetValue(SelectedIdProperty);
            set => SetValue(SelectedIdProperty, value);
        }

        public string SelectedText
        {
            get => (string)GetValue(SelectedTextProperty);
            set => SetValue(SelectedTextProperty, value);
        }

        // Event fuer Auswahl
        public event EventHandler<EntitySelectedEventArgs>? EntitySelected;

        public class EntitySelectedEventArgs : EventArgs
        {
            public int Id { get; set; }
            public string Nr { get; set; } = "";
            public string Name { get; set; } = "";
            public SearchEntityType EntityType { get; set; }
        }

        public class SuchErgebnis
        {
            public int Id { get; set; }
            public string Nr { get; set; } = "";
            public string Name { get; set; } = "";
            public string Extra { get; set; } = "";
        }

        public EntitySearchControl()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                _core = new CoreService(App.ConnectionString);
                UpdateFilterVisibility();
                if (ShowFilters && !_filterLoaded)
                {
                    await LoadFiltersAsync();
                }
            };
        }

        private static void OnEntityTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EntitySearchControl ctrl)
            {
                ctrl.UpdateFilterVisibility();
            }
        }

        private static async void OnShowFiltersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EntitySearchControl ctrl)
            {
                ctrl.UpdateFilterVisibility();
                if ((bool)e.NewValue && ctrl._core != null && !ctrl._filterLoaded)
                {
                    await ctrl.LoadFiltersAsync();
                }
            }
        }

        private void UpdateFilterVisibility()
        {
            if (!ShowFilters)
            {
                gridFilter.Visibility = Visibility.Collapsed;
                return;
            }

            gridFilter.Visibility = Visibility.Visible;

            // Kunde Filter anzeigen
            bool isKunde = EntityType == SearchEntityType.Kunde || EntityType == SearchEntityType.Lieferant;
            lblKategorie.Visibility = isKunde ? Visibility.Visible : Visibility.Collapsed;
            cmbKategorie.Visibility = isKunde ? Visibility.Visible : Visibility.Collapsed;
            lblGruppe.Visibility = isKunde ? Visibility.Visible : Visibility.Collapsed;
            cmbGruppe.Visibility = isKunde ? Visibility.Visible : Visibility.Collapsed;

            // Artikel Filter anzeigen
            bool isArtikel = EntityType == SearchEntityType.Artikel;
            lblWarengruppe.Visibility = isArtikel ? Visibility.Visible : Visibility.Collapsed;
            cmbWarengruppe.Visibility = isArtikel ? Visibility.Visible : Visibility.Collapsed;

            // Bei Lieferant nur ein leeres Filter-Grid (keine speziellen Filter)
            if (EntityType == SearchEntityType.Lieferant)
            {
                gridFilter.Visibility = Visibility.Collapsed;
            }
        }

        private async System.Threading.Tasks.Task LoadFiltersAsync()
        {
            if (_core == null) return;

            try
            {
                // Kundenkategorien und Kundengruppen laden
                var kategorien = (await _core.GetKundenkategorienAsync()).ToList();
                kategorien.Insert(0, new CoreService.KundenkategorieRef { KKundenKategorie = 0, CName = "(Alle)" });
                cmbKategorie.ItemsSource = kategorien;
                cmbKategorie.SelectedIndex = 0;

                var gruppen = (await _core.GetKundengruppenAsync()).ToList();
                gruppen.Insert(0, new CoreService.KundengruppeRef { KKundenGruppe = 0, CName = "(Alle)" });
                cmbGruppe.ItemsSource = gruppen;
                cmbGruppe.SelectedIndex = 0;

                // Warengruppen laden
                var warengruppen = (await _core.GetWarengruppenAsync()).ToList();
                warengruppen.Insert(0, new CoreService.WarengruppeRef { KWarengruppe = 0, CName = "(Alle)" });
                cmbWarengruppe.ItemsSource = warengruppen;
                cmbWarengruppe.SelectedIndex = 0;

                _filterLoaded = true;
            }
            catch { }
        }

        private void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Suchen_Click(sender, e);
            }
            else if (e.Key == Key.Down && popupErgebnisse.IsOpen && lstErgebnisse.Items.Count > 0)
            {
                lstErgebnisse.Focus();
                lstErgebnisse.SelectedIndex = 0;
            }
            else if (e.Key == Key.Escape)
            {
                popupErgebnisse.IsOpen = false;
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null) return;

            var suchbegriff = txtSuche.Text.Trim();

            try
            {
                var ergebnisse = new List<SuchErgebnis>();

                switch (EntityType)
                {
                    case SearchEntityType.Kunde:
                        ergebnisse = await SucheKundenAsync(suchbegriff);
                        break;

                    case SearchEntityType.Artikel:
                        ergebnisse = await SucheArtikelAsync(suchbegriff);
                        break;

                    case SearchEntityType.Lieferant:
                        ergebnisse = await SucheLieferantenAsync(suchbegriff);
                        break;
                }

                lstErgebnisse.ItemsSource = ergebnisse;
                popupErgebnisse.IsOpen = ergebnisse.Count > 0;

                if (ergebnisse.Count == 0 && !string.IsNullOrEmpty(suchbegriff))
                {
                    MessageBox.Show($"Keine {EntityType} gefunden.", "Suche", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei Suche:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task<List<SuchErgebnis>> SucheKundenAsync(string suchbegriff)
        {
            // Filter-Werte holen
            int? kategorieId = null;
            int? gruppeId = null;

            if (ShowFilters)
            {
                if (cmbKategorie.SelectedValue is int kat && kat > 0) kategorieId = kat;
                if (cmbGruppe.SelectedValue is int grp && grp > 0) gruppeId = grp;
            }

            // Erweiterte Suche mit Filtern
            var kunden = await _core!.SearchKundenErweitertAsync(suchbegriff, kategorieId, gruppeId, 30);
            return kunden.Select(k => new SuchErgebnis
            {
                Id = k.KKunde,
                Nr = k.CKundenNr ?? "",
                Name = k.Anzeigename ?? k.CFirma ?? "",
                Extra = k.Kundengruppe ?? ""
            }).ToList();
        }

        private async System.Threading.Tasks.Task<List<SuchErgebnis>> SucheArtikelAsync(string suchbegriff)
        {
            // Filter-Werte holen
            int? warengruppeId = null;

            if (ShowFilters)
            {
                if (cmbWarengruppe.SelectedValue is int wg && wg > 0) warengruppeId = wg;
            }

            // Erweiterte Suche mit Filtern
            var artikel = await _core!.SearchArtikelErweitertAsync(suchbegriff, warengruppeId, 30);
            return artikel.Select(a => new SuchErgebnis
            {
                Id = a.KArtikel,
                Nr = a.CArtNr ?? "",
                Name = a.Name ?? "",
                Extra = a.Warengruppe ?? ""
            }).ToList();
        }

        private async System.Threading.Tasks.Task<List<SuchErgebnis>> SucheLieferantenAsync(string suchbegriff)
        {
            // Lieferanten-Suche (wie Kunden: Name, Firma, etc.)
            var lieferanten = await _core!.SearchLieferantenErweitertAsync(suchbegriff, 30);
            return lieferanten.Select(l => new SuchErgebnis
            {
                Id = l.KLieferant,
                Nr = $"L-{l.KLieferant}",
                Name = l.CFirma ?? "",
                Extra = l.COrt ?? ""
            }).ToList();
        }

        private void LstErgebnisse_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectCurrentItem();
        }

        private void LstErgebnisse_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SelectCurrentItem();
            }
            else if (e.Key == Key.Escape)
            {
                popupErgebnisse.IsOpen = false;
                txtSuche.Focus();
            }
        }

        private void SelectCurrentItem()
        {
            if (lstErgebnisse.SelectedItem is SuchErgebnis ergebnis)
            {
                SelectedId = ergebnis.Id;
                SelectedText = $"{ergebnis.Nr} - {ergebnis.Name}";
                txtSuche.Text = SelectedText;
                popupErgebnisse.IsOpen = false;

                EntitySelected?.Invoke(this, new EntitySelectedEventArgs
                {
                    Id = ergebnis.Id,
                    Nr = ergebnis.Nr,
                    Name = ergebnis.Name,
                    EntityType = EntityType
                });
            }
        }

        // Oeffentliche Methoden
        public void Clear()
        {
            txtSuche.Text = "";
            SelectedId = 0;
            SelectedText = "";
            popupErgebnisse.IsOpen = false;
        }

        public void SetValue(int id, string displayText)
        {
            SelectedId = id;
            SelectedText = displayText;
            txtSuche.Text = displayText;
        }
    }
}
