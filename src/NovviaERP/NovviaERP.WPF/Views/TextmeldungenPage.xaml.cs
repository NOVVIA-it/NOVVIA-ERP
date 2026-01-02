using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;
using NovviaERP.WPF.Controls.Base;

namespace NovviaERP.WPF.Views
{
    public partial class TextmeldungenPage : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.Textmeldung>? _textmeldungen;

        public TextmeldungenPage()
        {
            InitializeComponent();
            _core = new CoreService(App.ConnectionString);
            Loaded += async (s, e) =>
            {
                try
                {
                    // ZENTRALE GRID-INITIALISIERUNG: Styling + Spalten-Konfiguration in richtiger Reihenfolge
                    await GridStyleHelper.InitializeGridAsync(dgTextmeldungen, "TextmeldungenPage", _core, App.BenutzerId);

                    await LadeTextmeldungenAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden der Textmeldungen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private async System.Threading.Tasks.Task LadeTextmeldungenAsync()
        {
            try
            {
                _textmeldungen = await _core.GetTextmeldungenAsync();

                // Zuweisungen Text pro Meldung laden
                foreach (var meldung in _textmeldungen)
                {
                    var entities = await _core.GetTextmeldungEntitiesAsync(meldung.KTextmeldung);
                    meldung.ZuweisungenText = entities.Count > 0
                        ? $"{entities.Count} Zuweisung(en)"
                        : "Alle";
                }

                dgTextmeldungen.ItemsSource = _textmeldungen;

                // Leer-Hinweis anzeigen
                txtLeerHinweis.Visibility = (_textmeldungen == null || _textmeldungen.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgTextmeldungen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgTextmeldungen.SelectedItem != null;
            btnBearbeiten.IsEnabled = selected;
            btnLoeschen.IsEnabled = selected;
        }

        private void DgTextmeldungen_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgTextmeldungen.SelectedItem is CoreService.Textmeldung)
            {
                TextmeldungBearbeiten_Click(sender, e);
            }
        }

        private async void TextmeldungNeu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TextmeldungDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Meldung != null)
            {
                try
                {
                    var newId = await _core.CreateTextmeldungAsync(dialog.Meldung, App.BenutzerId);

                    // Entities speichern
                    foreach (var entity in dialog.Entities)
                    {
                        await _core.AddEntityTextmeldungAsync(newId, entity.CEntityTyp, entity.KEntity, App.BenutzerId);
                    }

                    await LadeTextmeldungenAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Anlegen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void TextmeldungBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgTextmeldungen.SelectedItem is not CoreService.Textmeldung meldung) return;

            // Entities laden
            var existingEntities = await _core.GetTextmeldungEntitiesAsync(meldung.KTextmeldung);

            var dialog = new TextmeldungDialog(meldung);
            dialog.SetEntities(existingEntities);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Meldung != null)
            {
                try
                {
                    await _core.UpdateTextmeldungAsync(dialog.Meldung, App.BenutzerId);

                    // Entities synchronisieren
                    var newEntities = dialog.Entities.ToList();

                    // Entfernte loeschen
                    foreach (var existing in existingEntities)
                    {
                        if (!newEntities.Any(n => n.CEntityTyp == existing.CEntityTyp && n.KEntity == existing.KEntity))
                        {
                            await _core.RemoveEntityTextmeldungAsync(existing.KEntityTextmeldung);
                        }
                    }

                    // Neue hinzufuegen
                    foreach (var newEntity in newEntities)
                    {
                        if (!existingEntities.Any(e => e.CEntityTyp == newEntity.CEntityTyp && e.KEntity == newEntity.KEntity))
                        {
                            await _core.AddEntityTextmeldungAsync(meldung.KTextmeldung, newEntity.CEntityTyp, newEntity.KEntity, App.BenutzerId);
                        }
                    }

                    await LadeTextmeldungenAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void TextmeldungLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgTextmeldungen.SelectedItem is not CoreService.Textmeldung meldung) return;

            var result = MessageBox.Show($"Textmeldung '{meldung.CTitel}' wirklich loeschen?\n\n" +
                "Alle Zuweisungen zu Kunden/Artikeln/Lieferanten werden ebenfalls entfernt.",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteTextmeldungAsync(meldung.KTextmeldung);
                await LadeTextmeldungenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
