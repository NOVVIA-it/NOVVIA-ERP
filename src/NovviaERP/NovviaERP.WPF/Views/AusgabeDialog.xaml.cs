using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;
using FormularVorlageItem = NovviaERP.Core.Services.CoreService.FormularVorlageItem;

namespace NovviaERP.WPF.Views
{
    public partial class AusgabeDialog : Window
    {
        private readonly CoreService _core;
        private readonly AusgabeService _ausgabeService;
        private readonly EmailVorlageService _emailService;
        private readonly DokumentTyp _dokumentTyp;
        private readonly int _dokumentId;
        private readonly string? _dokumentNr;

        private List<FormularVorlageItem> _formulare = new();
        private byte[]? _currentPdf;
        private List<BitmapImage>? _previewPages;
        private int _currentPage = 0;
        private double _zoomFactor = 1.0;

        public AusgabeDialog(DokumentTyp typ, int id, string? dokumentNr = null)
        {
            _core = App.Services.GetRequiredService<CoreService>();
            _ausgabeService = App.Services.GetRequiredService<AusgabeService>();
            _emailService = App.Services.GetRequiredService<EmailVorlageService>();
            _dokumentTyp = typ;
            _dokumentId = id;
            _dokumentNr = dokumentNr;

            InitializeComponent();
            SetupCheckboxEvents();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        // Alternative Konstruktor fuer Kompatibilitaet
        public AusgabeDialog(AusgabeService ausgabe, EmailVorlageService email, DokumentTyp typ, int id)
        {
            _core = App.Services.GetRequiredService<CoreService>();
            _ausgabeService = ausgabe;
            _emailService = email;
            _dokumentTyp = typ;
            _dokumentId = id;

            InitializeComponent();
            SetupCheckboxEvents();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private void SetupCheckboxEvents()
        {
            chkDrucken.Checked += (s, e) => pnlDruckOptionen.Visibility = Visibility.Visible;
            chkDrucken.Unchecked += (s, e) => pnlDruckOptionen.Visibility = Visibility.Collapsed;
            chkSpeichern.Checked += (s, e) => pnlSpeichernOptionen.Visibility = Visibility.Visible;
            chkSpeichern.Unchecked += (s, e) => pnlSpeichernOptionen.Visibility = Visibility.Collapsed;
            chkEmail.Checked += (s, e) => pnlEmailOptionen.Visibility = Visibility.Visible;
            chkEmail.Unchecked += (s, e) => pnlEmailOptionen.Visibility = Visibility.Collapsed;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                txtTitel.Text = $"{_dokumentTyp} ausgeben";
                txtStatus.Text = "Lade...";
                System.Diagnostics.Debug.WriteLine($"AusgabeDialog: Lade {_dokumentTyp} ID={_dokumentId}");
                txtDokumentInfo.Text = !string.IsNullOrEmpty(_dokumentNr)
                    ? $"{_dokumentTyp} {_dokumentNr}"
                    : $"Dokument-ID: {_dokumentId}";

                // Formulare laden
                await LoadFormulareAsync();

                // Drucker laden
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    cbDrucker.Items.Add(printer);
                }
                if (cbDrucker.Items.Count > 0)
                    cbDrucker.SelectedIndex = 0;

                // E-Mail-Vorlagen laden
                var vorlagen = await _emailService.GetVorlagenAsync(_dokumentTyp.ToString());
                cbEmailVorlage.ItemsSource = vorlagen;
                if (vorlagen.Any())
                    cbEmailVorlage.SelectedIndex = 0;

                // E-Mail-Empfaenger vorausfuellen
                var email = await GetDokumentEmailAsync();
                if (!string.IsNullOrEmpty(email))
                    txtEmpfaenger.Text = email;

                // Standard-Speicherpfad
                var nr = _dokumentNr ?? _dokumentId.ToString();
                txtSpeicherPfad.Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NOVVIA", _dokumentTyp.ToString(), $"{_dokumentTyp}_{nr}.pdf");

                // Vorschau erstellen
                if (chkVorschau.IsChecked == true)
                {
                    await CreatePreviewAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadFormulareAsync()
        {
            var formulare = await _core.GetFormulareAsync(_dokumentTyp);
            _formulare = formulare.ToList();

            cbFormular.ItemsSource = _formulare;
            if (_formulare.Any())
            {
                // Standard-Formular auswaehlen
                var standard = _formulare.FirstOrDefault(f => f.IsStandard) ?? _formulare.First();
                cbFormular.SelectedItem = standard;
            }
        }

        private async Task<string?> GetDokumentEmailAsync()
        {
            return await _core.GetDokumentEmailAsync(_dokumentTyp, _dokumentId);
        }

        #region Vorschau

        private async Task CreatePreviewAsync()
        {
            try
            {
                pnlLaden.Visibility = Visibility.Visible;
                pnlKeineVorschau.Visibility = Visibility.Collapsed;
                imgVorschau.Source = null;

                var formular = cbFormular.SelectedItem as FormularVorlageItem;

                // PDF generieren
                _currentPdf = await _ausgabeService.GeneratePdfAsync(_dokumentTyp, _dokumentId, formular?.KFormularVorlage);

                if (_currentPdf != null && _currentPdf.Length > 0)
                {
                    // PDF zu Bildern konvertieren fuer Vorschau
                    _previewPages = await ConvertPdfToImagesAsync(_currentPdf);
                    _currentPage = 0;

                    if (_previewPages != null && _previewPages.Any())
                    {
                        UpdatePreviewImage();
                        txtSeite.Text = $"Seite {_currentPage + 1} von {_previewPages.Count}";
                    }
                    else
                    {
                        pnlKeineVorschau.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    pnlKeineVorschau.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Vorschau-Fehler: {ex.Message}";
                pnlKeineVorschau.Visibility = Visibility.Visible;
            }
            finally
            {
                pnlLaden.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<List<BitmapImage>?> ConvertPdfToImagesAsync(byte[] pdfData)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var images = new List<BitmapImage>();

                    // PDFium oder andere Lib verwenden
                    // Fuer jetzt: Temporaer als PDF speichern und ersten Frame als Platzhalter
                    var tempPath = Path.Combine(Path.GetTempPath(), $"preview_{Guid.NewGuid()}.pdf");
                    File.WriteAllBytes(tempPath, pdfData);

                    // PDFium.Net verwenden wenn verfuegbar, sonst Platzhalter-Bild
                    // TODO: PDFium oder Windows PDF-Renderer integrieren

                    // Platzhalter: Zeige Info dass PDF erstellt wurde
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var bitmap = new BitmapImage();
                        // Einfaches Platzhalter-Bild erstellen
                        var drawingVisual = new System.Windows.Media.DrawingVisual();
                        using (var dc = drawingVisual.RenderOpen())
                        {
                            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, 595, 842));
                            dc.DrawText(
                                new System.Windows.Media.FormattedText(
                                    $"PDF-Vorschau\n\n{_dokumentTyp}\nID: {_dokumentId}\n\nSeiten: Unbekannt\nGroesse: {pdfData.Length / 1024} KB",
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new System.Windows.Media.Typeface("Segoe UI"),
                                    14,
                                    System.Windows.Media.Brushes.Black,
                                    96),
                                new System.Windows.Point(50, 50));
                        }

                        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(595, 842, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        rtb.Render(drawingVisual);

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rtb));

                        using var ms = new MemoryStream();
                        encoder.Save(ms);
                        ms.Position = 0;

                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        images.Add(bitmap);
                    });

                    // Temp-Datei aufraumen
                    try { File.Delete(tempPath); } catch { }

                    return images;
                }
                catch
                {
                    return null;
                }
            });
        }

        private void UpdatePreviewImage()
        {
            if (_previewPages == null || !_previewPages.Any()) return;

            var page = _previewPages[_currentPage];
            var transform = new System.Windows.Media.ScaleTransform(_zoomFactor, _zoomFactor);
            imgVorschau.LayoutTransform = transform;
            imgVorschau.Source = page;
        }

        private void ChkVorschau_Changed(object sender, RoutedEventArgs e)
        {
            if (chkVorschau.IsChecked == true && _currentPdf == null && IsLoaded)
            {
                _ = CreatePreviewAsync();
            }
        }

        private void CbFormular_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && cbFormular.SelectedItem != null)
            {
                _ = CreatePreviewAsync();
            }
        }

        private void BtnAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            _ = CreatePreviewAsync();
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomFactor < 3.0)
            {
                _zoomFactor += 0.25;
                txtZoom.Text = $"{(int)(_zoomFactor * 100)}%";
                UpdatePreviewImage();
            }
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_zoomFactor > 0.25)
            {
                _zoomFactor -= 0.25;
                txtZoom.Text = $"{(int)(_zoomFactor * 100)}%";
                UpdatePreviewImage();
            }
        }

        private void BtnSeiteVor_Click(object sender, RoutedEventArgs e)
        {
            if (_previewPages != null && _currentPage < _previewPages.Count - 1)
            {
                _currentPage++;
                txtSeite.Text = $"Seite {_currentPage + 1} von {_previewPages.Count}";
                UpdatePreviewImage();
            }
        }

        private void BtnSeiteZurueck_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                txtSeite.Text = $"Seite {_currentPage + 1} von {_previewPages?.Count ?? 1}";
                UpdatePreviewImage();
            }
        }

        #endregion

        #region Ausgabe

        private void BtnPfadWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF-Dateien|*.pdf",
                FileName = Path.GetFileName(txtSpeicherPfad.Text),
                InitialDirectory = Path.GetDirectoryName(txtSpeicherPfad.Text)
            };

            if (dialog.ShowDialog() == true)
            {
                txtSpeicherPfad.Text = dialog.FileName;
            }
        }

        private async void BtnAusfuehren_Click(object sender, RoutedEventArgs e)
        {
            var aktionen = new List<AusgabeAktion>();

            if (chkVorschau.IsChecked == true) aktionen.Add(AusgabeAktion.Vorschau);
            if (chkDrucken.IsChecked == true) aktionen.Add(AusgabeAktion.Drucken);
            if (chkSpeichern.IsChecked == true) aktionen.Add(AusgabeAktion.Speichern);
            if (chkEmail.IsChecked == true) aktionen.Add(AusgabeAktion.EMail);
            if (chkArchivieren.IsChecked == true) aktionen.Add(AusgabeAktion.Archivieren);

            if (!aktionen.Any())
            {
                MessageBox.Show("Bitte mindestens eine Aktion auswaehlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            txtStatus.Text = "Verarbeite...";
            btnAusfuehren.IsEnabled = false;

            try
            {
                var formular = cbFormular.SelectedItem as FormularVorlageItem;

                var anfrage = new AusgabeAnfrage
                {
                    DokumentTyp = _dokumentTyp,
                    DokumentId = _dokumentId,
                    Aktionen = aktionen,
                    DruckerName = cbDrucker.SelectedItem?.ToString(),
                    Kopien = int.TryParse(txtKopien.Text, out var k) ? k : 1,
                    SpeicherPfad = txtSpeicherPfad.Text,
                    Archivieren = chkArchivieren.IsChecked == true,
                    EmpfaengerEmail = txtEmpfaenger.Text,
                    EmailCC = txtCC.Text,
                    EmailVorlageId = (cbEmailVorlage.SelectedItem as EmailVorlageErweitert)?.Id,
                    EmailVorschau = chkEmailVorschau.IsChecked == true,
                    FormularVorlageId = formular?.KFormularVorlage
                };

                // Wenn bereits PDF vorhanden, direkt verwenden
                if (_currentPdf != null)
                {
                    anfrage.VorhandenesPdf = _currentPdf;
                }

                var ergebnis = await _ausgabeService.AusgabeAsync(anfrage);

                // Ergebnis anzeigen
                var erfolge = new List<string>();
                if (ergebnis.VorschauAngezeigt) erfolge.Add("Vorschau angezeigt");
                if (ergebnis.Gedruckt) erfolge.Add($"Gedruckt ({anfrage.Kopien}x)");
                if (ergebnis.Gespeichert) erfolge.Add($"Gespeichert: {anfrage.SpeicherPfad}");
                if (ergebnis.EmailGesendet) erfolge.Add("E-Mail gesendet");
                if (ergebnis.Archiviert) erfolge.Add("Archiviert");

                var msg = erfolge.Any()
                    ? "Ausgabe abgeschlossen:\n" + string.Join("\n", erfolge.Select(x => $"  {x}"))
                    : "Keine Aktionen ausgefuehrt.";

                if (!string.IsNullOrEmpty(ergebnis.Fehler))
                {
                    msg += $"\n\nFehler: {ergebnis.Fehler}";
                    MessageBox.Show(msg, "Ausgabe mit Fehlern", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(msg, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                txtStatus.Text = "";
                btnAusfuehren.IsEnabled = true;
            }
        }

        #endregion
    }
}
