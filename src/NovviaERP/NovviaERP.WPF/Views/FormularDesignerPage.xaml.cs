using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Dapper;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class FormularDesignerPage : Page, INotifyPropertyChanged
    {
        private readonly JtlDbContext _db;
        private Point _dragStart;
        private UIElement? _draggedElement;
        private bool _isDragging;

        public FormularDesignerPage(JtlDbContext db)
        {
            InitializeComponent();
            DataContext = this;
            _db = db;
            LoadFormulare();
            LoadDatenfelder();
        }

        #region Properties
        public ObservableCollection<FormularDefinition> Formulare { get; } = new();
        public FormularDefinition? SelectedFormular { get; set; }
        public FormularElement? SelectedElement { get; set; }
        public ObservableCollection<string> VerfuegbareFelder { get; } = new();
        
        public string StatusText { get; set; } = "Bereit";
        public string MousePosition { get; set; } = "0, 0";
        public double CanvasWidth { get; set; } = 595; // A4 bei 72 DPI
        public double CanvasHeight { get; set; } = 842;
        
        public ObservableCollection<string> Schriftarten { get; } = new()
        {
            "Arial", "Calibri", "Times New Roman", "Courier New", "Verdana", "Tahoma"
        };
        #endregion

        #region Load Data
        private async void LoadFormulare()
        {
            var conn = await _db.GetConnectionAsync();
            var formulare = await conn.QueryAsync<FormularDefinition>(
                "SELECT * FROM tFormular WHERE nAktiv = 1 ORDER BY cTyp, cName");
            
            Formulare.Clear();
            foreach (var f in formulare) Formulare.Add(f);

            // Standard-Formulare hinzufügen falls leer
            if (Formulare.Count == 0)
            {
                Formulare.Add(new FormularDefinition { Name = "Rechnung", Typ = "Rechnung", Icon = "📄" });
                Formulare.Add(new FormularDefinition { Name = "Lieferschein", Typ = "Lieferschein", Icon = "📦" });
                Formulare.Add(new FormularDefinition { Name = "Mahnung", Typ = "Mahnung", Icon = "⚠️" });
                Formulare.Add(new FormularDefinition { Name = "Versandetikett", Typ = "Versandetikett", Icon = "🏷️" });
                Formulare.Add(new FormularDefinition { Name = "Artikeletikett", Typ = "Artikeletikett", Icon = "🔖" });
                Formulare.Add(new FormularDefinition { Name = "Pickliste", Typ = "Pickliste", Icon = "📋" });
            }
        }

        private void LoadDatenfelder()
        {
            // Rechnung / Lieferschein Felder
            VerfuegbareFelder.Clear();
            var felder = new[]
            {
                // Firma
                "{Firma.Name}", "{Firma.Strasse}", "{Firma.PLZ}", "{Firma.Ort}", "{Firma.Land}",
                "{Firma.Telefon}", "{Firma.Email}", "{Firma.Website}", "{Firma.UStID}", "{Firma.Logo}",
                "{Firma.Bankname}", "{Firma.IBAN}", "{Firma.BIC}",
                
                // Kunde
                "{Kunde.Name}", "{Kunde.Firma}", "{Kunde.Strasse}", "{Kunde.PLZ}", "{Kunde.Ort}",
                "{Kunde.Land}", "{Kunde.KundenNr}", "{Kunde.Email}", "{Kunde.Telefon}", "{Kunde.UStID}",
                
                // Dokument
                "{Dokument.Nummer}", "{Dokument.Datum}", "{Dokument.Faellig}", "{Dokument.Betreff}",
                "{Dokument.Netto}", "{Dokument.MwSt}", "{Dokument.Brutto}", "{Dokument.Waehrung}",
                "{Dokument.Zahlungsbedingung}", "{Dokument.Lieferbedingung}",
                
                // Positionen (für Tabelle)
                "{Pos.Nr}", "{Pos.ArtNr}", "{Pos.Name}", "{Pos.Menge}", "{Pos.Einheit}",
                "{Pos.Einzelpreis}", "{Pos.Rabatt}", "{Pos.Gesamt}", "{Pos.MwStSatz}",
                
                // Versand
                "{Versand.Carrier}", "{Versand.TrackingNr}", "{Versand.Gewicht}", "{Versand.Pakete}",
                
                // Sonstiges
                "{Seite}", "{Seiten}", "{Datum}", "{Zeit}", "{Bearbeiter}"
            };
            foreach (var f in felder) VerfuegbareFelder.Add(f);
        }
        #endregion

        #region Drag & Drop
        private void ElementeListe_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is ListBoxItem item)
            {
                var elementTyp = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(elementTyp))
                {
                    DragDrop.DoDragDrop(listBox, elementTyp, DragDropEffects.Copy);
                }
            }
        }

        private void DatenfelderListe_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is string feld)
            {
                DragDrop.DoDragDrop(listBox, $"DataField:{feld}", DragDropEffects.Copy);
            }
        }

        private void DesignerCanvas_Drop(object sender, DragEventArgs e)
        {
            var pos = e.GetPosition(DesignerCanvas);
            var data = e.Data.GetData(DataFormats.StringFormat) as string;

            if (string.IsNullOrEmpty(data)) return;

            FormularElement element;
            if (data.StartsWith("DataField:"))
            {
                var feldName = data.Substring(10);
                element = CreateElement("DataField", pos.X, pos.Y);
                element.DataBinding = feldName;
                element.Text = feldName;
            }
            else
            {
                element = CreateElement(data, pos.X, pos.Y);
            }

            AddElementToCanvas(element);
        }

        private FormularElement CreateElement(string typ, double x, double y)
        {
            return new FormularElement
            {
                ElementTyp = typ,
                X = x,
                Y = y,
                Width = typ switch { "Line" => 200, "Rectangle" => 150, "Table" => 400, "Image" => 100, _ => 120 },
                Height = typ switch { "Line" => 2, "Rectangle" => 100, "Table" => 150, "Image" => 50, _ => 20 },
                Text = typ switch { "TextBlock" => "Text hier...", "DataField" => "{Feld}", _ => "" },
                FontFamily = "Arial",
                FontSize = 10,
                Foreground = "#000000",
                Background = "Transparent"
            };
        }

        private void AddElementToCanvas(FormularElement element)
        {
            UIElement visual = element.ElementTyp switch
            {
                "TextBlock" or "DataField" => new TextBlock
                {
                    Text = element.Text,
                    FontFamily = new FontFamily(element.FontFamily ?? "Arial"),
                    FontSize = element.FontSize,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(element.Foreground ?? "#000000"))
                },
                "TextBox" => new TextBox { Text = element.Text, Width = element.Width },
                "Image" => new Border { Width = element.Width, Height = element.Height, Background = Brushes.LightGray, 
                    Child = new TextBlock { Text = "🖼️ Bild", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } },
                "Barcode" => new Border { Width = element.Width, Height = element.Height, Background = Brushes.White, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1),
                    Child = new TextBlock { Text = "|||||||||||", FontFamily = new FontFamily("Courier New"), FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center } },
                "Line" => new Line { X1 = 0, Y1 = 0, X2 = element.Width, Y2 = 0, Stroke = Brushes.Black, StrokeThickness = 1 },
                "Rectangle" => new Rectangle { Width = element.Width, Height = element.Height, Stroke = Brushes.Black, StrokeThickness = 1, Fill = Brushes.Transparent },
                "Table" => CreateTablePlaceholder(element),
                _ => new TextBlock { Text = element.Text }
            };

            Canvas.SetLeft(visual, element.X);
            Canvas.SetTop(visual, element.Y);
            if (visual is FrameworkElement fe) fe.Tag = element;

            // Selection Border
            var container = new Border
            {
                Child = visual is Border ? null : visual,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent,
                Tag = element
            };
            if (visual is Border b) container = b;

            container.MouseLeftButtonDown += Element_MouseDown;
            container.MouseEnter += (s, e) => { if (container.BorderBrush != Brushes.Blue) container.BorderBrush = Brushes.LightBlue; };
            container.MouseLeave += (s, e) => { if (container.BorderBrush != Brushes.Blue) container.BorderBrush = Brushes.Transparent; };

            DesignerCanvas.Children.Add(container);
            SelectElement(container, element);
        }

        private UIElement CreateTablePlaceholder(FormularElement element)
        {
            var grid = new Grid { Width = element.Width, Height = element.Height, Background = Brushes.White };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(25) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            var header = new Border { Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            header.Child = new TextBlock { Text = "Pos | ArtNr | Bezeichnung | Menge | Preis | Gesamt", Margin = new Thickness(5), FontWeight = FontWeights.Bold, FontSize = 9 };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var body = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1, 0, 1, 1) };
            body.Child = new TextBlock { Text = "{Positionen}", Margin = new Thickness(5), Foreground = Brushes.Gray };
            Grid.SetRow(body, 1);
            grid.Children.Add(body);

            return grid;
        }
        #endregion

        #region Element Selection & Movement
        private void Element_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is FormularElement element)
            {
                SelectElement(el, element);
                _draggedElement = el;
                _dragStart = e.GetPosition(DesignerCanvas);
                _isDragging = true;
                el.CaptureMouse();
                e.Handled = true;
            }
        }

        private void SelectElement(FrameworkElement visual, FormularElement element)
        {
            // Alte Selektion aufheben
            foreach (UIElement child in DesignerCanvas.Children)
            {
                if (child is Border b) b.BorderBrush = Brushes.Transparent;
            }

            // Neue Selektion
            if (visual is Border border) border.BorderBrush = Brushes.Blue;
            SelectedElement = element;
            OnPropertyChanged(nameof(SelectedElement));
        }

        private void DesignerCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == DesignerCanvas)
            {
                // Klick auf leeren Canvas - Selektion aufheben
                foreach (UIElement child in DesignerCanvas.Children)
                {
                    if (child is Border b) b.BorderBrush = Brushes.Transparent;
                }
                SelectedElement = null;
                OnPropertyChanged(nameof(SelectedElement));
            }
        }

        private void DesignerCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(DesignerCanvas);
            MousePosition = $"{pos.X:F0}, {pos.Y:F0}";
            OnPropertyChanged(nameof(MousePosition));

            if (_isDragging && _draggedElement != null)
            {
                var delta = pos - _dragStart;
                var newX = Canvas.GetLeft(_draggedElement) + delta.X;
                var newY = Canvas.GetTop(_draggedElement) + delta.Y;
                
                Canvas.SetLeft(_draggedElement, Math.Max(0, newX));
                Canvas.SetTop(_draggedElement, Math.Max(0, newY));
                
                _dragStart = pos;

                if (_draggedElement is FrameworkElement fe && fe.Tag is FormularElement el)
                {
                    el.X = newX;
                    el.Y = newY;
                }
            }
        }

        private void DesignerCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && _draggedElement != null)
            {
                _draggedElement.ReleaseMouseCapture();
                _isDragging = false;
                _draggedElement = null;
            }
        }
        #endregion

        #region Commands
        public ICommand NeuesFormularCommand => new RelayCommand(_ => NeuesFormular());
        public ICommand SpeichernCommand => new RelayCommand(async _ => await Speichern());
        public ICommand LadenCommand => new RelayCommand(async _ => await Laden());
        public ICommand VorschauCommand => new RelayCommand(_ => Vorschau());
        public ICommand DruckenCommand => new RelayCommand(_ => Drucken());
        public ICommand UndoCommand => new RelayCommand(_ => { });
        public ICommand RedoCommand => new RelayCommand(_ => { });

        private void NeuesFormular()
        {
            DesignerCanvas.Children.Clear();
            SelectedFormular = new FormularDefinition { Name = "Neues Formular" };
            StatusText = "Neues Formular erstellt";
            OnPropertyChanged(nameof(StatusText));
        }

        private async System.Threading.Tasks.Task Speichern()
        {
            if (SelectedFormular == null) return;

            var elemente = new List<FormularElement>();
            foreach (UIElement child in DesignerCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is FormularElement el)
                    elemente.Add(el);
            }

            SelectedFormular.ElementeJson = JsonSerializer.Serialize(elemente);

            var conn = await _db.GetConnectionAsync();
            if (SelectedFormular.Id == 0)
            {
                SelectedFormular.Id = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tFormular (cName, cTyp, cPapierFormat, cElementeJson, nAktiv)
                    VALUES (@Name, @Typ, @PapierFormat, @ElementeJson, 1); SELECT SCOPE_IDENTITY();",
                    SelectedFormular);
            }
            else
            {
                await conn.ExecuteAsync(@"UPDATE tFormular SET cElementeJson = @ElementeJson WHERE kFormular = @Id",
                    SelectedFormular);
            }

            StatusText = "Formular gespeichert";
            OnPropertyChanged(nameof(StatusText));
        }

        private async System.Threading.Tasks.Task Laden()
        {
            if (SelectedFormular == null) return;

            var conn = await _db.GetConnectionAsync();
            var formular = await conn.QuerySingleOrDefaultAsync<FormularDefinition>(
                "SELECT * FROM tFormular WHERE kFormular = @Id", new { Id = SelectedFormular.Id });

            if (formular != null && !string.IsNullOrEmpty(formular.ElementeJson))
            {
                var elemente = JsonSerializer.Deserialize<List<FormularElement>>(formular.ElementeJson);
                DesignerCanvas.Children.Clear();
                if (elemente != null)
                {
                    foreach (var el in elemente) AddElementToCanvas(el);
                }
            }
        }

        private void Vorschau()
        {
            MessageBox.Show("Vorschau wird generiert...", "Vorschau");
            // TODO: PDF generieren und anzeigen
        }

        private void Drucken()
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
            {
                pd.PrintVisual(DesignerCanvas, "Formular-Test");
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }

    #region DTOs
    public class FormularDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "";
        public string Icon { get; set; } = "📄";
        public string? PapierFormat { get; set; } = "A4";
        public string? ElementeJson { get; set; }
        public bool Aktiv { get; set; } = true;
    }

    public class FormularElement : INotifyPropertyChanged
    {
        public string ElementTyp { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 100;
        public double Height { get; set; } = 20;
        public string? Text { get; set; }
        public string? DataBinding { get; set; }
        public string? FontFamily { get; set; } = "Arial";
        public double FontSize { get; set; } = 10;
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
        public string? Foreground { get; set; } = "#000000";
        public string? Background { get; set; } = "Transparent";
        public double BorderThickness { get; set; }
        public string? BorderColor { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
    }
    #endregion
}
