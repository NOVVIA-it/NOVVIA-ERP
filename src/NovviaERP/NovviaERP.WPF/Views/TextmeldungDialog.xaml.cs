using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Dialogs;

namespace NovviaERP.WPF.Views
{
    public partial class TextmeldungDialog : Window
    {
        private readonly CoreService _core;
        public CoreService.Textmeldung? Meldung { get; private set; }
        public bool IstNeu { get; private set; }
        public ObservableCollection<EntityZuweisung> Entities { get; private set; } = new();

        public class EntityZuweisung
        {
            public int KEntityTextmeldung { get; set; }
            public string CEntityTyp { get; set; } = "";
            public int KEntity { get; set; }
            public string EntityNr { get; set; } = "";
            public string EntityName { get; set; } = "";
        }

        public TextmeldungDialog(CoreService.Textmeldung? meldung = null)
        {
            InitializeComponent();
            _core = new CoreService(App.ConnectionString);
            IstNeu = meldung == null;
            Title = IstNeu ? "Neue Textmeldung" : "Textmeldung bearbeiten";

            lstEntities.ItemsSource = Entities;
            Entities.CollectionChanged += (s, e) => UpdateKeineZuweisungen();

            if (meldung != null)
            {
                txtTitel.Text = meldung.CTitel;
                txtText.Text = meldung.CText;
                cmbTyp.SelectedIndex = meldung.NTyp;
                chkEinkauf.IsChecked = meldung.NBereichEinkauf;
                chkVerkauf.IsChecked = meldung.NBereichVerkauf;
                chkStammdaten.IsChecked = meldung.NBereichStammdaten;
                chkDokumente.IsChecked = meldung.NBereichDokumente;
                chkOnline.IsChecked = meldung.NBereichOnline;
                chkAktiv.IsChecked = meldung.NAktiv;
                chkPopup.IsChecked = meldung.NPopupAnzeigen;
                dpVon.SelectedDate = meldung.DGueltigVon;
                dpBis.SelectedDate = meldung.DGueltigBis;
                Meldung = meldung;
            }
            else
            {
                cmbTyp.SelectedIndex = 0;
                chkStammdaten.IsChecked = true;
            }

            Loaded += (s, e) => txtTitel.Focus();
            UpdateKeineZuweisungen();
        }

        private void UpdateKeineZuweisungen()
        {
            txtKeineZuweisungen.Visibility = Entities.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public async void SetEntities(List<CoreService.EntityTextmeldung> entities)
        {
            Entities.Clear();
            foreach (var e in entities)
            {
                var zuweisung = new EntityZuweisung
                {
                    KEntityTextmeldung = e.KEntityTextmeldung,
                    CEntityTyp = e.CEntityTyp,
                    KEntity = e.KEntity
                };
                await LadeEntityDetails(zuweisung);
                Entities.Add(zuweisung);
            }
            UpdateKeineZuweisungen();
        }

        private async System.Threading.Tasks.Task LadeEntityDetails(EntityZuweisung zuweisung)
        {
            try
            {
                switch (zuweisung.CEntityTyp)
                {
                    case "Kunde":
                        var kunde = await _core.GetKundeByIdAsync(zuweisung.KEntity);
                        if (kunde != null)
                        {
                            zuweisung.EntityNr = kunde.CKundenNr ?? "";
                            zuweisung.EntityName = kunde.StandardAdresse?.CFirma ?? "";
                        }
                        break;
                    case "Artikel":
                        var artikel = await _core.GetArtikelByIdAsync(zuweisung.KEntity);
                        if (artikel != null)
                        {
                            zuweisung.EntityNr = artikel.CArtNr ?? "";
                            zuweisung.EntityName = artikel.Name ?? "";
                        }
                        break;
                    case "Lieferant":
                        var lieferanten = await _core.GetLieferantenAsync();
                        var lieferant = lieferanten.FirstOrDefault(l => l.KLieferant == zuweisung.KEntity);
                        if (lieferant != null)
                        {
                            zuweisung.EntityNr = $"L-{lieferant.KLieferant}";
                            zuweisung.EntityName = lieferant.CFirma ?? "";
                        }
                        break;
                }
            }
            catch { }
        }

        private void SucheKunde_Click(object sender, RoutedEventArgs e)
        {
            var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Kunde, this);
            if (result.HasValue)
            {
                AddEntity("Kunde", result.Value.Id!.Value, result.Value.Nr, result.Value.Name);
            }
        }

        private void SucheArtikel_Click(object sender, RoutedEventArgs e)
        {
            var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Artikel, this);
            if (result.HasValue)
            {
                AddEntity("Artikel", result.Value.Id!.Value, result.Value.Nr, result.Value.Name);
            }
        }

        private void SucheLieferant_Click(object sender, RoutedEventArgs e)
        {
            var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Lieferant, this);
            if (result.HasValue)
            {
                AddEntity("Lieferant", result.Value.Id!.Value, result.Value.Nr, result.Value.Name);
            }
        }

        private void AddEntity(string entityTyp, int kEntity, string nr, string name)
        {
            // Pruefen ob bereits vorhanden
            if (Entities.Any(x => x.CEntityTyp == entityTyp && x.KEntity == kEntity))
            {
                MessageBox.Show($"{entityTyp} ist bereits zugewiesen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Entities.Add(new EntityZuweisung
            {
                CEntityTyp = entityTyp,
                KEntity = kEntity,
                EntityNr = nr,
                EntityName = name
            });
        }

        private void EntityEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EntityZuweisung entity)
            {
                Entities.Remove(entity);
            }
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTitel.Text))
            {
                MessageBox.Show("Bitte einen Titel eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTitel.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtText.Text))
            {
                MessageBox.Show("Bitte einen Text eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtText.Focus();
                return;
            }

            if (chkEinkauf.IsChecked != true && chkVerkauf.IsChecked != true &&
                chkStammdaten.IsChecked != true && chkDokumente.IsChecked != true &&
                chkOnline.IsChecked != true)
            {
                MessageBox.Show("Bitte mindestens einen Bereich auswaehlen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Meldung = Meldung ?? new CoreService.Textmeldung();
            Meldung.CTitel = txtTitel.Text.Trim();
            Meldung.CText = txtText.Text.Trim();
            Meldung.NTyp = cmbTyp.SelectedIndex;
            Meldung.NBereichEinkauf = chkEinkauf.IsChecked == true;
            Meldung.NBereichVerkauf = chkVerkauf.IsChecked == true;
            Meldung.NBereichStammdaten = chkStammdaten.IsChecked == true;
            Meldung.NBereichDokumente = chkDokumente.IsChecked == true;
            Meldung.NBereichOnline = chkOnline.IsChecked == true;
            Meldung.NAktiv = chkAktiv.IsChecked == true;
            Meldung.NPopupAnzeigen = chkPopup.IsChecked == true;
            Meldung.DGueltigVon = dpVon.SelectedDate;
            Meldung.DGueltigBis = dpBis.SelectedDate;

            DialogResult = true;
            Close();
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
