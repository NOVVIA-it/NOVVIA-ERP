using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KundeDetailPage : Page
    {
        private readonly CoreService _coreService;
        private readonly EigeneFelderService? _eigeneFelderService;
        private int? _kundeId;
        private CoreService.KundeDetail? _kunde;
        private CoreService.AdresseDetail? _adresse;
        private List<CoreService.KundengruppeRef> _kundengruppen = new();
        private List<CoreService.ZahlungsartRef> _zahlungsarten = new();
        private List<EigenesFeldDefinition> _eigeneFelder = new();
        private Dictionary<int, FrameworkElement> _eigeneFelderControls = new();

        public KundeDetailPage(int? kundeId)
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            _eigeneFelderService = App.Services.GetService<EigeneFelderService>();
            _kundeId = kundeId;
            Loaded += async (s, e) => await LadeKundeAsync();
        }

        private async System.Threading.Tasks.Task LadeKundeAsync()
        {
            try
            {
                // Stammdaten laden
                _kundengruppen = (await _coreService.GetKundengruppenAsync()).ToList();
                cmbKundengruppe.ItemsSource = _kundengruppen;

                _zahlungsarten = (await _coreService.GetZahlungsartenAsync()).ToList();
                cmbZahlungsart.ItemsSource = _zahlungsarten;

                if (_kundeId.HasValue)
                {
                    txtStatus.Text = "Lade Kundendaten...";
                    _kunde = await _coreService.GetKundeByIdAsync(_kundeId.Value);

                    if (_kunde == null)
                    {
                        MessageBox.Show("Kunde nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        NavigationService?.GoBack();
                        return;
                    }

                    _adresse = _kunde.StandardAdresse ?? new CoreService.AdresseDetail();

                    // Header
                    string name = !string.IsNullOrEmpty(_adresse.CFirma) ? _adresse.CFirma : $"{_adresse.CVorname} {_adresse.CName}".Trim();
                    txtTitel.Text = name;
                    txtSubtitel.Text = $"{_adresse.CPLZ} {_adresse.COrt}";
                    txtKundenNr.Text = $"Kd-Nr: {_kunde.CKundenNr}";

                    // Adresse
                    SetComboBoxByContent(cmbAnrede, _adresse.CAnrede);
                    txtTitelAdresse.Text = _adresse.CTitel;
                    txtVorname.Text = _adresse.CVorname;
                    txtNachname.Text = _adresse.CName;
                    txtFirma.Text = _adresse.CFirma;
                    txtZusatz.Text = _adresse.CAdressZusatz;
                    txtStrasse.Text = _adresse.CStrasse;
                    txtPLZ.Text = _adresse.CPLZ;
                    txtOrt.Text = _adresse.COrt;
                    SetComboBoxByTag(cmbLand, _adresse.CISO ?? "DE");
                    txtBundesland.Text = _adresse.CBundesland;

                    // Kontakt
                    txtTelefon.Text = _adresse.CTel;
                    txtMobil.Text = _adresse.CMobil;
                    txtFax.Text = _adresse.CFax;
                    txtEmail.Text = _adresse.CMail;
                    txtWebsite.Text = _kunde.CWWW;
                    txtUStID.Text = _adresse.CUSTID;

                    // Weitere Adressen
                    dgAdressen.ItemsSource = _kunde.Adressen.Where(a => a.KAdresse != _adresse.KAdresse).ToList();

                    // Konditionen
                    cmbKundengruppe.SelectedValue = _kunde.KKundenGruppe;
                    txtRabatt.Text = _kunde.FRabatt.ToString("N2");
                    txtDebitorennr.Text = _kunde.NDebitorennr.ToString();
                    cmbZahlungsart.SelectedValue = _kunde.KZahlungsart;
                    txtZahlungsziel.Text = _kunde.NZahlungsziel?.ToString() ?? "14";
                    txtSkonto.Text = _kunde.FSkonto.ToString("N2");
                    txtSkontoTage.Text = _kunde.NSkontoInTagen.ToString();
                    txtKreditlimit.Text = _kunde.NKreditlimit.ToString();
                    chkMahnstopp.IsChecked = _kunde.NMahnstopp == 1;
                    txtMahnrhythmus.Text = _kunde.NMahnrhythmus.ToString();
                    chkGesperrt.IsChecked = _kunde.CSperre == "Y";
                    chkNewsletter.IsChecked = _kunde.CNewsletter == "Y";
                    txtSteuernr.Text = _kunde.CSteuerNr;

                    // Statistik
                    txtKundeSeit.Text = _kunde.DErstellt?.ToString("dd.MM.yyyy") ?? "-";
                    txtAnzahlBestellungen.Text = _kunde.AnzahlBestellungen.ToString();
                    txtGesamtumsatz.Text = $"{_kunde.GesamtUmsatz:N2} EUR";

                    // TODO: Letzte Bestellungen laden
                    // dgBestellungen.ItemsSource = ...

                    // Eigene Felder laden
                    await LadeEigeneFelderAsync();

                    // Validierungsfelder laden
                    await LadeValidierungAsync();

                    txtStatus.Text = "Kunde geladen";
                }
                else
                {
                    // Neuer Kunde
                    _kunde = new CoreService.KundeDetail();
                    _adresse = new CoreService.AdresseDetail { NStandard = 1 };

                    txtTitel.Text = "Neuer Kunde";
                    txtSubtitel.Text = "";
                    txtKundenNr.Text = "(wird automatisch vergeben)";

                    // Defaults
                    cmbLand.SelectedIndex = 0;
                    cmbKundengruppe.SelectedIndex = 0;
                    txtZahlungsziel.Text = "14";

                    // Eigene Felder laden (ohne Werte)
                    await LadeEigeneFelderAsync();

                    txtStatus.Text = "Neuen Kunden anlegen";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void SetComboBoxByContent(ComboBox cmb, string? value)
        {
            if (string.IsNullOrEmpty(value)) { cmb.SelectedIndex = 0; return; }
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Content?.ToString() == value) { cmb.SelectedItem = item; return; }
            }
            cmb.SelectedIndex = 0;
        }

        private void SetComboBoxByTag(ComboBox cmb, string? tag)
        {
            if (string.IsNullOrEmpty(tag)) { cmb.SelectedIndex = 0; return; }
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Tag?.ToString() == tag) { cmb.SelectedItem = item; return; }
            }
            cmb.SelectedIndex = 0;
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Speichere...";

                // Validierung
                if (string.IsNullOrWhiteSpace(txtNachname.Text) && string.IsNullOrWhiteSpace(txtFirma.Text))
                {
                    MessageBox.Show("Bitte Nachname oder Firma angeben!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Adresse aus Formular
                _adresse!.CAnrede = (cmbAnrede.SelectedItem as ComboBoxItem)?.Content?.ToString();
                _adresse.CTitel = txtTitelAdresse.Text;
                _adresse.CVorname = txtVorname.Text;
                _adresse.CName = txtNachname.Text;
                _adresse.CFirma = txtFirma.Text;
                _adresse.CAdressZusatz = txtZusatz.Text;
                _adresse.CStrasse = txtStrasse.Text;
                _adresse.CPLZ = txtPLZ.Text;
                _adresse.COrt = txtOrt.Text;
                _adresse.CISO = (cmbLand.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "DE";
                _adresse.CLand = (cmbLand.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Deutschland";
                _adresse.CBundesland = txtBundesland.Text;
                _adresse.CTel = txtTelefon.Text;
                _adresse.CMobil = txtMobil.Text;
                _adresse.CFax = txtFax.Text;
                _adresse.CMail = txtEmail.Text;
                _adresse.CUSTID = txtUStID.Text;

                // Kundendaten
                _kunde!.KKundenGruppe = cmbKundengruppe.SelectedValue as int?;
                _kunde.FRabatt = decimal.TryParse(txtRabatt.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var rabatt) ? rabatt : 0;
                _kunde.NDebitorennr = int.TryParse(txtDebitorennr.Text, out var debNr) ? debNr : 0;
                _kunde.KZahlungsart = cmbZahlungsart.SelectedValue as int?;
                _kunde.NZahlungsziel = int.TryParse(txtZahlungsziel.Text, out var zz) ? zz : 14;
                _kunde.FSkonto = decimal.TryParse(txtSkonto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var skonto) ? skonto : 0;
                _kunde.NSkontoInTagen = int.TryParse(txtSkontoTage.Text, out var skontoTage) ? skontoTage : 0;
                _kunde.NKreditlimit = int.TryParse(txtKreditlimit.Text, out var kredit) ? kredit : 0;
                _kunde.NMahnstopp = (byte)(chkMahnstopp.IsChecked == true ? 1 : 0);
                _kunde.NMahnrhythmus = int.TryParse(txtMahnrhythmus.Text, out var mahnr) ? mahnr : 14;
                _kunde.CSperre = chkGesperrt.IsChecked == true ? "Y" : "N";
                _kunde.CNewsletter = chkNewsletter.IsChecked == true ? "Y" : "N";
                _kunde.CSteuerNr = txtSteuernr.Text;
                _kunde.CWWW = txtWebsite.Text;

                if (_kundeId.HasValue)
                {
                    // Update
                    await _coreService.UpdateKundeAsync(_kunde);
                    await _coreService.UpdateAdresseAsync(_adresse);
                    await SpeichereEigeneFelderAsync();
                    txtStatus.Text = "Kunde gespeichert";
                    MessageBox.Show("Kunde wurde gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Neu anlegen
                    _kundeId = await _coreService.CreateKundeAsync(_kunde, _adresse);
                    _kunde.KKunde = _kundeId.Value;
                    await SpeichereEigeneFelderAsync();
                    txtKundenNr.Text = $"Kd-Nr: {_kunde.CKundenNr}";
                    txtStatus.Text = $"Kunde {_kunde.CKundenNr} angelegt";
                    MessageBox.Show($"Kunde {_kunde.CKundenNr} wurde angelegt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void DgAdressen_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TODO: Adresse bearbeiten Dialog öffnen
            if (dgAdressen.SelectedItem is CoreService.AdresseDetail adr)
            {
                MessageBox.Show($"Adresse bearbeiten: {adr.CName}, {adr.COrt}\n(Dialog wird implementiert)",
                    "Adresse", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NeueAdresse_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Neue Adresse Dialog öffnen
            MessageBox.Show("Neue Adresse hinzufuegen\n(Dialog wird implementiert)",
                "Neue Adresse", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #region Eigene Felder

        private async System.Threading.Tasks.Task LadeEigeneFelderAsync()
        {
            if (_eigeneFelderService == null) return;

            try
            {
                _eigeneFelder = (await _eigeneFelderService.GetFelderAsync("Kunde")).ToList();
                pnlEigeneFelder.Children.Clear();
                _eigeneFelderControls.Clear();

                if (_eigeneFelder.Count == 0)
                {
                    txtKeineEigenenFelder.Visibility = Visibility.Visible;
                    return;
                }

                txtKeineEigenenFelder.Visibility = Visibility.Collapsed;

                // Werte laden wenn Kunde existiert
                Dictionary<string, string?>? werte = null;
                if (_kundeId.HasValue)
                {
                    werte = await _eigeneFelderService.GetWerteAsync("Kunde", _kundeId.Value);
                }

                foreach (var feld in _eigeneFelder)
                {
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Label
                    var label = new TextBlock
                    {
                        Text = feld.Name + (feld.IstPflichtfeld ? " *" : ""),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(label, 0);
                    row.Children.Add(label);

                    // Control basierend auf Typ
                    var wert = werte?.TryGetValue(feld.InternerName ?? feld.Name, out var v) == true ? v : feld.Standardwert;
                    var control = BuildEigenesFeldControl(feld, wert);
                    Grid.SetColumn(control, 1);
                    control.Margin = new Thickness(10, 0, 0, 0);
                    row.Children.Add(control);
                    _eigeneFelderControls[feld.Id] = control;

                    // Hinweis
                    if (!string.IsNullOrEmpty(feld.Hinweis))
                    {
                        var hinweis = new TextBlock
                        {
                            Text = feld.Hinweis,
                            Foreground = System.Windows.Media.Brushes.Gray,
                            FontSize = 11,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 0, 0, 0)
                        };
                        Grid.SetColumn(hinweis, 2);
                        row.Children.Add(hinweis);
                    }

                    pnlEigeneFelder.Children.Add(row);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden eigener Felder: {ex.Message}");
            }
        }

        private FrameworkElement BuildEigenesFeldControl(EigenesFeldDefinition feld, string? wert)
        {
            switch (feld.Typ)
            {
                case EigenesFeldTyp.Text:
                    return new TextBox { Text = wert ?? "", Height = 28, MinWidth = 200 };

                case EigenesFeldTyp.Textarea:
                    return new TextBox
                    {
                        Text = wert ?? "",
                        Height = 80,
                        MinWidth = 300,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };

                case EigenesFeldTyp.Int:
                case EigenesFeldTyp.Decimal:
                    return new TextBox { Text = wert ?? "", Height = 28, Width = 120, HorizontalAlignment = HorizontalAlignment.Left };

                case EigenesFeldTyp.Date:
                    var dp = new DatePicker { Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
                    if (DateTime.TryParse(wert, out var date))
                        dp.SelectedDate = date;
                    return dp;

                case EigenesFeldTyp.DateTime:
                    // Vereinfacht: Nur Datum, Zeit separat wäre aufwendiger
                    var dpDt = new DatePicker { Width = 150, HorizontalAlignment = HorizontalAlignment.Left };
                    if (DateTime.TryParse(wert, out var dateTime))
                        dpDt.SelectedDate = dateTime;
                    return dpDt;

                case EigenesFeldTyp.Bool:
                    return new CheckBox { IsChecked = wert == "1" || wert?.ToLower() == "true", VerticalAlignment = VerticalAlignment.Center };

                case EigenesFeldTyp.Select:
                    var cmb = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Height = 28 };
                    if (!string.IsNullOrEmpty(feld.AuswahlWerte))
                    {
                        foreach (var opt in feld.AuswahlWerte.Split('|'))
                            cmb.Items.Add(opt);
                    }
                    cmb.SelectedItem = wert;
                    return cmb;

                case EigenesFeldTyp.MultiSelect:
                    var lb = new ListBox
                    {
                        Width = 200,
                        Height = 100,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        SelectionMode = SelectionMode.Multiple
                    };
                    var selectedValues = wert?.Split('|') ?? Array.Empty<string>();
                    if (!string.IsNullOrEmpty(feld.AuswahlWerte))
                    {
                        foreach (var opt in feld.AuswahlWerte.Split('|'))
                        {
                            var item = new ListBoxItem { Content = opt };
                            if (selectedValues.Contains(opt))
                                item.IsSelected = true;
                            lb.Items.Add(item);
                        }
                    }
                    return lb;

                default:
                    return new TextBox { Text = wert ?? "", Height = 28, MinWidth = 200 };
            }
        }

        private string? GetEigenesFeldWert(FrameworkElement control, EigenesFeldTyp typ)
        {
            switch (typ)
            {
                case EigenesFeldTyp.Text:
                case EigenesFeldTyp.Textarea:
                case EigenesFeldTyp.Int:
                case EigenesFeldTyp.Decimal:
                    return (control as TextBox)?.Text;

                case EigenesFeldTyp.Date:
                case EigenesFeldTyp.DateTime:
                    var dp = control as DatePicker;
                    return dp?.SelectedDate?.ToString("yyyy-MM-dd");

                case EigenesFeldTyp.Bool:
                    return (control as CheckBox)?.IsChecked == true ? "1" : "0";

                case EigenesFeldTyp.Select:
                    return (control as ComboBox)?.SelectedItem?.ToString();

                case EigenesFeldTyp.MultiSelect:
                    var lb = control as ListBox;
                    if (lb == null) return null;
                    var selected = lb.Items.Cast<ListBoxItem>()
                        .Where(i => i.IsSelected)
                        .Select(i => i.Content?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s));
                    return string.Join("|", selected);

                default:
                    return (control as TextBox)?.Text;
            }
        }

        private async System.Threading.Tasks.Task SpeichereEigeneFelderAsync()
        {
            if (_eigeneFelderService == null || !_kundeId.HasValue) return;

            foreach (var feld in _eigeneFelder)
            {
                if (_eigeneFelderControls.TryGetValue(feld.Id, out var control))
                {
                    var wert = GetEigenesFeldWert(control, feld.Typ);
                    await _eigeneFelderService.SetWertAsync(feld.Id, _kundeId.Value, wert);
                }
            }
        }

        private void NeuesEigenesFeld_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Neue eigene Felder werden unter Einstellungen -> Eigene Felder verwaltet.",
                "Eigene Felder", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Validierungsfelder (JTL Eigene Felder)

        private async System.Threading.Tasks.Task LadeValidierungAsync()
        {
            if (!_kundeId.HasValue) return;

            try
            {
                var felder = await _coreService.GetKundeEigeneFelderAsync(_kundeId.Value);

                // Validierung Checkboxen
                chkKundeAmbient.IsChecked = GetBoolWert(felder, "Ambient");
                chkKundeCool.IsChecked = GetBoolWert(felder, "Cool");
                chkKundeMedcan.IsChecked = GetBoolWert(felder, "Medcan");
                chkKundeTierarznei.IsChecked = GetBoolWert(felder, "Tierarznei");

                // Qualifikation
                if (felder.TryGetValue("QualifiziertAm", out var qualDatum) && DateTime.TryParse(qualDatum, out var dt))
                {
                    dpKundeQualifiziertAm.SelectedDate = dt;
                }

                txtKundeQualifiziertVon.Text = felder.TryGetValue("QualifiziertVon", out var qualVon) ? qualVon ?? "" : "";
                txtKundeGDP.Text = felder.TryGetValue("GDP", out var gdp) ? gdp ?? "" : "";
                txtKundeGMP.Text = felder.TryGetValue("GMP", out var gmp) ? gmp ?? "" : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Validierungsfelder: {ex.Message}");
            }
        }

        private bool GetBoolWert(Dictionary<string, string?> felder, string key)
        {
            if (felder.TryGetValue(key, out var wert))
            {
                return wert == "1" || wert?.ToLower() == "true" || wert?.ToLower() == "ja";
            }
            return false;
        }

        private async void ValidierungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_kundeId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Kunden speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                txtStatus.Text = "Speichere Validierungsfelder...";

                var felder = new Dictionary<string, string?>
                {
                    ["Ambient"] = chkKundeAmbient.IsChecked == true ? "1" : "0",
                    ["Cool"] = chkKundeCool.IsChecked == true ? "1" : "0",
                    ["Medcan"] = chkKundeMedcan.IsChecked == true ? "1" : "0",
                    ["Tierarznei"] = chkKundeTierarznei.IsChecked == true ? "1" : "0",
                    ["QualifiziertAm"] = dpKundeQualifiziertAm.SelectedDate?.ToString("yyyy-MM-dd"),
                    ["QualifiziertVon"] = txtKundeQualifiziertVon.Text.Trim(),
                    ["GDP"] = txtKundeGDP.Text.Trim(),
                    ["GMP"] = txtKundeGMP.Text.Trim()
                };

                await _coreService.SetKundeEigeneFelderAsync(_kundeId.Value, felder);

                txtStatus.Text = "Validierungsfelder gespeichert";
                MessageBox.Show("Validierungsfelder wurden gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        #endregion
    }
}
