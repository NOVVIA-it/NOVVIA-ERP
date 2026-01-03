using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Dialogs;

namespace NovviaERP.WPF.Views
{
    public partial class KategorieDetailView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly int? _kategorieId;
        private CoreService.KategorieDetail? _kategorie;

        public KategorieDetailView(int? kategorieId)
        {
            InitializeComponent();
            _kategorieId = kategorieId;
            _coreService = App.Services.GetRequiredService<CoreService>();

            txtTitel.Text = kategorieId.HasValue ? "Kategorie bearbeiten" : "Neue Kategorie";

            Loaded += async (s, e) =>
            {
                await LadeOberkategorienAsync();
                if (_kategorieId.HasValue)
                {
                    await LadeKategorieAsync();
                }
                else
                {
                    chkAktiv.IsChecked = true;
                    chkInternet.IsChecked = true;
                    txtSort.Text = "0";
                }
            };
        }

        private async System.Threading.Tasks.Task LadeOberkategorienAsync()
        {
            try
            {
                var kategorien = await _coreService.GetKategorienAsync();

                cmbOberkategorie.Items.Clear();
                cmbOberkategorie.Items.Add(new ComboBoxItem { Content = "(Hauptkategorie)", Tag = (int?)null });

                foreach (var kat in kategorien.Where(k => k.KKategorie != _kategorieId))
                {
                    // Einrueckung basierend auf Ebene (vereinfacht - pruefen ob Oberkategorie)
                    var prefix = kat.KOberKategorie.HasValue && kat.KOberKategorie > 0 ? "  " : "";
                    cmbOberkategorie.Items.Add(new ComboBoxItem
                    {
                        Content = prefix + kat.CName,
                        Tag = kat.KKategorie
                    });
                }

                cmbOberkategorie.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task LadeKategorieAsync()
        {
            if (!_kategorieId.HasValue) return;

            try
            {
                txtStatus.Text = "Lade Kategorie...";
                _kategorie = await _coreService.GetKategorieByIdAsync(_kategorieId.Value);

                if (_kategorie == null)
                {
                    MessageBox.Show("Kategorie nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    Zurueck_Click(this, new RoutedEventArgs());
                    return;
                }

                // Felder fuellen
                txtName.Text = _kategorie.CName ?? "";
                txtBeschreibung.Text = _kategorie.CBeschreibung ?? "";
                txtSort.Text = _kategorie.NSort.ToString();
                chkAktiv.IsChecked = _kategorie.CAktiv;
                chkInternet.IsChecked = _kategorie.CInet;

                txtUrlPfad.Text = _kategorie.CUrlPfad ?? "";
                txtTitleTag.Text = _kategorie.CTitleTag ?? "";
                txtMetaDescription.Text = _kategorie.CMetaDescription ?? "";
                txtMetaKeywords.Text = _kategorie.CMetaKeywords ?? "";

                // Oberkategorie auswaehlen
                foreach (ComboBoxItem item in cmbOberkategorie.Items)
                {
                    if ((item.Tag as int?) == _kategorie.KOberKategorie)
                    {
                        cmbOberkategorie.SelectedItem = item;
                        break;
                    }
                }

                // Artikel
                dgArtikel.ItemsSource = _kategorie.Artikel;
                txtArtikelAnzahl.Text = $"{_kategorie.Artikel.Count} Artikel";

                // Unterkategorien
                dgUnterkategorien.ItemsSource = _kategorie.Unterkategorien;
                txtSubAnzahl.Text = $"{_kategorie.Unterkategorien.Count} Unterkategorien";

                txtStatus.Text = $"Kategorie '{_kategorie.CName}' geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Bitte einen Namen eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            try
            {
                txtStatus.Text = "Speichere...";

                var oberkategorie = (cmbOberkategorie.SelectedItem as ComboBoxItem)?.Tag as int?;

                if (_kategorieId.HasValue && _kategorie != null)
                {
                    // Update
                    _kategorie.CName = txtName.Text.Trim();
                    _kategorie.CBeschreibung = txtBeschreibung.Text.Trim();
                    _kategorie.KOberKategorie = oberkategorie;
                    _kategorie.NSort = int.TryParse(txtSort.Text, out var sort) ? sort : 0;
                    _kategorie.CAktiv = chkAktiv.IsChecked == true;
                    _kategorie.CInet = chkInternet.IsChecked == true;
                    _kategorie.CUrlPfad = txtUrlPfad.Text.Trim();
                    _kategorie.CTitleTag = txtTitleTag.Text.Trim();
                    _kategorie.CMetaDescription = txtMetaDescription.Text.Trim();
                    _kategorie.CMetaKeywords = txtMetaKeywords.Text.Trim();

                    await _coreService.UpdateKategorieAsync(_kategorie);
                    txtStatus.Text = "Gespeichert";
                    MessageBox.Show("Kategorie wurde gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Neu
                    var neu = new CoreService.KategorieDetail
                    {
                        CName = txtName.Text.Trim(),
                        CBeschreibung = txtBeschreibung.Text.Trim(),
                        KOberKategorie = oberkategorie,
                        NSort = int.TryParse(txtSort.Text, out var sort) ? sort : 0,
                        CAktiv = chkAktiv.IsChecked == true,
                        CInet = chkInternet.IsChecked == true,
                        CUrlPfad = txtUrlPfad.Text.Trim(),
                        CTitleTag = txtTitleTag.Text.Trim(),
                        CMetaDescription = txtMetaDescription.Text.Trim(),
                        CMetaKeywords = txtMetaKeywords.Text.Trim()
                    };

                    var neueId = await _coreService.CreateKategorieAsync(neu);
                    txtStatus.Text = $"Kategorie {neueId} erstellt";
                    MessageBox.Show($"Kategorie wurde erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Zur neuen Kategorie navigieren
                    if (Window.GetWindow(this) is MainWindow main)
                    {
                        main.ShowContent(new KategorieDetailView(neueId), pushToStack: false);
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ArtikelHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (!_kategorieId.HasValue)
            {
                MessageBox.Show("Bitte erst die Kategorie speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Artikel, Window.GetWindow(this));
            if (result.HasValue && result.Value.Id.HasValue)
            {
                AddArtikelAsync(result.Value.Id.Value);
            }
        }

        private async void AddArtikelAsync(int kArtikel)
        {
            if (!_kategorieId.HasValue) return;

            try
            {
                await _coreService.AddArtikelToKategorieAsync(_kategorieId.Value, kArtikel);
                await LadeKategorieAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ArtikelEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (dgArtikel.SelectedItem is not CoreService.KategorieArtikelItem artikel || !_kategorieId.HasValue) return;

            var result = MessageBox.Show($"Artikel '{artikel.CName}' aus dieser Kategorie entfernen?",
                "Bestaetigen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _coreService.RemoveArtikelFromKategorieAsync(_kategorieId.Value, artikel.KArtikel);
                    await LadeKategorieAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void NeueUnterkategorie_Click(object sender, RoutedEventArgs e)
        {
            if (!_kategorieId.HasValue)
            {
                MessageBox.Show("Bitte erst die Kategorie speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Neue Kategorie mit dieser als Oberkategorie erstellen
            if (Window.GetWindow(this) is MainWindow main)
            {
                var view = new KategorieDetailView(null);
                view.Loaded += (s, ev) =>
                {
                    // Oberkategorie vorauswaehlen
                    foreach (ComboBoxItem item in view.cmbOberkategorie.Items)
                    {
                        if ((item.Tag as int?) == _kategorieId)
                        {
                            view.cmbOberkategorie.SelectedItem = item;
                            break;
                        }
                    }
                };
                main.ShowContent(view);
            }
        }

        private void DgUnterkategorien_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgUnterkategorien.SelectedItem is CoreService.KategorieUebersicht kat)
            {
                if (Window.GetWindow(this) is MainWindow main)
                {
                    main.ShowContent(new KategorieDetailView(kat.KKategorie));
                }
            }
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                if (!main.NavigateBack())
                {
                    main.ShowContent(App.Services.GetRequiredService<KategorieView>(), pushToStack: false);
                }
            }
        }
    }
}
