using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Dialogs
{
    public partial class EntitySucheDialog : Window
    {
        private readonly CoreService _core;
        public EntityTyp Typ { get; private set; }

        public enum EntityTyp { Kunde, Artikel, Lieferant }

        // Ergebnis
        public int? SelectedId { get; private set; }
        public string SelectedNr { get; private set; } = "";
        public string SelectedName { get; private set; } = "";

        public class SuchErgebnis
        {
            public int Id { get; set; }
            public string Nr { get; set; } = "";
            public string Name { get; set; } = "";
            public string Extra { get; set; } = "";
        }

        public EntitySucheDialog(EntityTyp typ)
        {
            InitializeComponent();
            _core = new CoreService(App.ConnectionString);
            Typ = typ;

            Title = typ switch
            {
                EntityTyp.Kunde => "Kunde suchen",
                EntityTyp.Artikel => "Artikel suchen",
                EntityTyp.Lieferant => "Lieferant suchen",
                _ => "Suche"
            };

            // Filter-Bereiche ein/ausblenden
            gridKundeFilter.Visibility = (typ == EntityTyp.Kunde) ? Visibility.Visible : Visibility.Collapsed;
            gridArtikelFilter.Visibility = (typ == EntityTyp.Artikel) ? Visibility.Visible : Visibility.Collapsed;

            // Suchhinweis
            txtHinweis.Text = typ switch
            {
                EntityTyp.Kunde => "Suche in: Kundennummer, Firma, Name, Vorname, Strasse, PLZ, Ort",
                EntityTyp.Artikel => "Suche in: Artikelnummer, Barcode, Name, HAN",
                EntityTyp.Lieferant => "Suche in: Firma, Strasse, PLZ, Ort, Lieferantennummer",
                _ => ""
            };

            Loaded += async (s, e) =>
            {
                await LoadFiltersAsync();
                txtSuche.Focus();
            };

            dgErgebnisse.SelectionChanged += (s, e) =>
            {
                btnUebernehmen.IsEnabled = dgErgebnisse.SelectedItem != null;
            };
        }

        private async System.Threading.Tasks.Task LoadFiltersAsync()
        {
            try
            {
                if (Typ == EntityTyp.Kunde)
                {
                    var kategorien = (await _core.GetKundenkategorienAsync()).ToList();
                    kategorien.Insert(0, new CoreService.KundenkategorieRef { KKundenKategorie = 0, CName = "(Alle)" });
                    cmbKategorie.ItemsSource = kategorien;
                    cmbKategorie.SelectedIndex = 0;

                    var gruppen = (await _core.GetKundengruppenAsync()).ToList();
                    gruppen.Insert(0, new CoreService.KundengruppeRef { KKundenGruppe = 0, CName = "(Alle)" });
                    cmbGruppe.ItemsSource = gruppen;
                    cmbGruppe.SelectedIndex = 0;
                }
                else if (Typ == EntityTyp.Artikel)
                {
                    var warengruppen = (await _core.GetWarengruppenAsync()).ToList();
                    warengruppen.Insert(0, new CoreService.WarengruppeRef { KWarengruppe = 0, CName = "(Alle)" });
                    cmbWarengruppe.ItemsSource = warengruppen;
                    cmbWarengruppe.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Suchen_Click(sender, e);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            var suchbegriff = txtSuche.Text.Trim();
            var ergebnisse = new List<SuchErgebnis>();

            try
            {
                btnSuchen.IsEnabled = false;
                btnSuchen.Content = "Suche...";

                switch (Typ)
                {
                    case EntityTyp.Kunde:
                        int? kategorieId = null;
                        int? gruppeId = null;
                        if (cmbKategorie.SelectedValue is int kat && kat > 0) kategorieId = kat;
                        if (cmbGruppe.SelectedValue is int grp && grp > 0) gruppeId = grp;

                        var kunden = await _core.SearchKundenErweitertAsync(suchbegriff, kategorieId, gruppeId, 100);
                        ergebnisse = kunden.Select(k => new SuchErgebnis
                        {
                            Id = k.KKunde,
                            Nr = k.CKundenNr ?? "",
                            Name = k.Anzeigename ?? k.CFirma ?? "",
                            Extra = k.Kundengruppe ?? ""
                        }).ToList();
                        break;

                    case EntityTyp.Artikel:
                        int? warengruppeId = null;
                        if (cmbWarengruppe.SelectedValue is int wg && wg > 0) warengruppeId = wg;

                        var artikel = await _core.SearchArtikelErweitertAsync(suchbegriff, warengruppeId, 100);
                        ergebnisse = artikel.Select(a => new SuchErgebnis
                        {
                            Id = a.KArtikel,
                            Nr = a.CArtNr ?? "",
                            Name = a.Name ?? "",
                            Extra = a.Warengruppe ?? ""
                        }).ToList();
                        break;

                    case EntityTyp.Lieferant:
                        var lieferanten = await _core.SearchLieferantenErweitertAsync(suchbegriff, 100);
                        ergebnisse = lieferanten.Select(l => new SuchErgebnis
                        {
                            Id = l.KLieferant,
                            Nr = $"L-{l.KLieferant}",
                            Name = l.CFirma ?? "",
                            Extra = l.COrt ?? ""
                        }).ToList();
                        break;
                }

                dgErgebnisse.ItemsSource = ergebnisse;
                txtErgebnisse.Text = $"Ergebnisse: {ergebnisse.Count} gefunden";

                if (ergebnisse.Count == 0 && !string.IsNullOrEmpty(suchbegriff))
                {
                    MessageBox.Show($"Keine {Typ} gefunden.", "Suche", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (ergebnisse.Count == 1)
                {
                    dgErgebnisse.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei Suche:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSuchen.Content = "Suchen";
                btnSuchen.IsEnabled = true;
            }
        }

        private void DgErgebnisse_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgErgebnisse.SelectedItem is SuchErgebnis ergebnis)
            {
                SelectAndClose(ergebnis);
            }
        }

        private void Uebernehmen_Click(object sender, RoutedEventArgs e)
        {
            if (dgErgebnisse.SelectedItem is SuchErgebnis ergebnis)
            {
                SelectAndClose(ergebnis);
            }
        }

        private void SelectAndClose(SuchErgebnis ergebnis)
        {
            SelectedId = ergebnis.Id;
            SelectedNr = ergebnis.Nr;
            SelectedName = ergebnis.Name;
            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Statische Hilfsmethode zum Oeffnen des Dialogs
        public static (int? Id, string Nr, string Name)? Suchen(EntityTyp typ, Window? owner = null)
        {
            var dialog = new EntitySucheDialog(typ);
            if (owner != null) dialog.Owner = owner;

            if (dialog.ShowDialog() == true && dialog.SelectedId.HasValue)
            {
                return (dialog.SelectedId, dialog.SelectedNr, dialog.SelectedName);
            }
            return null;
        }
    }
}
