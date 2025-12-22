using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EmailVorlagenPage : Page
    {
        private readonly EmailVorlageService _service;
        public ObservableCollection<EmailVorlageViewModel> Vorlagen { get; } = new();
        private EmailVorlageViewModel? _selected;

        public EmailVorlagenPage(EmailVorlageService service)
        {
            _service = service;
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var vorlagen = await _service.GetVorlagenAsync();
            Vorlagen.Clear();
            foreach (var v in vorlagen)
            {
                Vorlagen.Add(new EmailVorlageViewModel(v));
            }
            lstVorlagen.ItemsSource = Vorlagen;
            
            // Platzhalter laden
            LoadPlatzhalter("Rechnung");
        }

        private void LoadPlatzhalter(string typ)
        {
            var platzhalter = _service.GetPlatzhalter(typ);
            tvPlatzhalter.ItemsSource = platzhalter.ToList();
        }

        private void CbTypFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var typ = (cbTypFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (typ == "Alle Typen")
            {
                lstVorlagen.ItemsSource = Vorlagen;
            }
            else
            {
                lstVorlagen.ItemsSource = Vorlagen.Where(v => v.Typ == typ).ToList();
            }
        }

        private void LstVorlagen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = lstVorlagen.SelectedItem as EmailVorlageViewModel;
            if (_selected == null) return;

            txtName.Text = _selected.Name;
            txtBetreff.Text = _selected.Betreff;
            txtText.Text = _selected.IsHtml ? _selected.HtmlText : _selected.Text;
            chkStandard.IsChecked = _selected.IstStandard;
            chkHtml.IsChecked = _selected.IsHtml;
            chkAktiv.IsChecked = _selected.Aktiv;

            foreach (ComboBoxItem item in cbTyp.Items)
            {
                if (item.Content?.ToString() == _selected.Typ)
                {
                    cbTyp.SelectedItem = item;
                    break;
                }
            }

            icAnhaenge.ItemsSource = _selected.Anhaenge;
            LoadPlatzhalter(_selected.Typ);
        }

        private void TvPlatzhalter_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (tvPlatzhalter.SelectedItem is string platzhalter)
            {
                var caretIndex = txtText.CaretIndex;
                txtText.Text = txtText.Text.Insert(caretIndex, platzhalter);
                txtText.CaretIndex = caretIndex + platzhalter.Length;
                txtText.Focus();
            }
        }

        private void BtnNeu_Click(object sender, RoutedEventArgs e)
        {
            var neu = new EmailVorlageViewModel(new EmailVorlageErweitert
            {
                Name = "Neue Vorlage",
                Typ = "Rechnung",
                Betreff = "Betreff",
                Text = "Sehr geehrte Damen und Herren,\n\n\n\nMit freundlichen Grüßen",
                Aktiv = true
            });
            Vorlagen.Add(neu);
            lstVorlagen.SelectedItem = neu;
        }

        private async void BtnDuplizieren_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            
            var name = Microsoft.VisualBasic.Interaction.InputBox("Name der Kopie:", "Duplizieren", $"{_selected.Name} (Kopie)");
            if (string.IsNullOrEmpty(name)) return;

            var neueId = await _service.DupliziereVorlageAsync(_selected.Id, name);
            await LoadDataAsync();
        }

        private async void BtnLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            if (MessageBox.Show($"Vorlage '{_selected.Name}' wirklich löschen?", "Löschen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Vorlagen.Remove(_selected);
                // TODO: Aus DB löschen
            }
        }

        private void BtnVorschau_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Vorschau mit Beispieldaten
            MessageBox.Show(txtText.Text, "Vorschau", MessageBoxButton.OK);
        }

        private async void BtnAnhangHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;

            var dialog = new OpenFileDialog
            {
                Filter = "Alle Dateien|*.*|PDF|*.pdf|Bilder|*.jpg;*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                var anhang = new EmailVorlageAnhang
                {
                    VorlageId = _selected.Id,
                    Name = System.IO.Path.GetFileName(dialog.FileName),
                    Pfad = dialog.FileName,
                    Groesse = (int)new System.IO.FileInfo(dialog.FileName).Length
                };

                await _service.AddAnhangAsync(anhang);
                _selected.Anhaenge.Add(anhang);
                icAnhaenge.ItemsSource = null;
                icAnhaenge.ItemsSource = _selected.Anhaenge;
            }
        }

        private async void BtnAnhangEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                await _service.RemoveAnhangAsync(id);
                _selected?.Anhaenge.RemoveAll(a => a.Id == id);
                icAnhaenge.ItemsSource = null;
                icAnhaenge.ItemsSource = _selected?.Anhaenge;
            }
        }

        private async void BtnSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;

            _selected.Name = txtName.Text;
            _selected.Typ = (cbTyp.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Rechnung";
            _selected.Betreff = txtBetreff.Text;
            _selected.Text = txtText.Text;
            _selected.IstStandard = chkStandard.IsChecked == true;
            _selected.IsHtml = chkHtml.IsChecked == true;
            _selected.Aktiv = chkAktiv.IsChecked == true;

            if (_selected.IsHtml)
                _selected.HtmlText = txtText.Text;

            await _service.SaveVorlageAsync(_selected.ToModel());
            MessageBox.Show("Vorlage gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class EmailVorlageViewModel
    {
        private readonly EmailVorlageErweitert _model;
        public EmailVorlageViewModel(EmailVorlageErweitert model) => _model = model;

        public int Id => _model.Id;
        public string Name { get => _model.Name; set => _model.Name = value; }
        public string Typ { get => _model.Typ; set => _model.Typ = value; }
        public string Betreff { get => _model.Betreff; set => _model.Betreff = value; }
        public string Text { get => _model.Text; set => _model.Text = value; }
        public string? HtmlText { get => _model.HtmlText; set => _model.HtmlText = value; }
        public bool IsHtml { get => _model.IsHtml; set => _model.IsHtml = value; }
        public bool IstStandard { get => _model.IstStandard; set => _model.IstStandard = value; }
        public bool Aktiv { get => _model.Aktiv; set => _model.Aktiv = value; }
        public List<EmailVorlageAnhang> Anhaenge => _model.Anhaenge;

        public Visibility IstStandardVisibility => IstStandard ? Visibility.Visible : Visibility.Collapsed;

        public EmailVorlageErweitert ToModel() => _model;
    }
}
