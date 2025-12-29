using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class LieferantenBestellungDetailView : Window
    {
        private readonly CoreService _coreService;
        private readonly int? _bestellungId;
        private ObservableCollection<PositionVM> _positionen = new();
        private List<CoreService.LieferantRef> _lieferanten = new();
        private List<CoreService.WarenlagerRef> _lager = new();
        private List<CoreService.FirmaRef> _firmen = new();

        public bool WurdeSpeichert { get; private set; }

        public LieferantenBestellungDetailView(int? bestellungId = null)
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            _bestellungId = bestellungId;

            dgPositionen.ItemsSource = _positionen;
            dpDatum.SelectedDate = DateTime.Today;
            dpLieferdatum.SelectedDate = DateTime.Today.AddDays(3);

            Loaded += async (s, e) => await LadeStammdatenAsync();
        }

        private async System.Threading.Tasks.Task LadeStammdatenAsync()
        {
            try
            {
                // Lieferanten laden
                _lieferanten = (await _coreService.GetLieferantenAsync()).ToList();
                cboLieferant.ItemsSource = _lieferanten;

                // Lager laden
                _lager = (await _coreService.GetWarenlagerAsync()).ToList();
                cboLager.ItemsSource = _lager;
                if (_lager.Any())
                    cboLager.SelectedIndex = 0;

                // Firmen laden
                _firmen = (await _coreService.GetFirmenAsync()).ToList();
                cboFirma.ItemsSource = _firmen;
                if (_firmen.Any())
                    cboFirma.SelectedIndex = 0;

                // Wenn Bearbeitung: Daten laden
                if (_bestellungId.HasValue)
                {
                    await LadeBestellungAsync(_bestellungId.Value);
                    Title = $"Beschaffung - Bestellung {_bestellungId}";
                }
                else
                {
                    Title = "Beschaffung - Neue Bestellung";
                    cboStatus.SelectedIndex = 0;
                }

                UpdateSummen();

                // Initialen Zustand der Controls setzen
                LieferungAn_Changed(null, null!);
                LieferadresseGleich_Changed(null, null!);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LadeBestellungAsync(int bestellungId)
        {
            var bestellung = await _coreService.GetLieferantenBestellungAsync(bestellungId);
            if (bestellung == null)
            {
                MessageBox.Show("Bestellung nicht gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            cboLieferant.SelectedValue = bestellung.KLieferant;
            dpDatum.SelectedDate = bestellung.DErstellt;
            txtEigeneBestellnummer.Text = bestellung.CEigeneBestellnummer;
            txtFremdbelegnummer.Text = bestellung.CFremdbelegnummer;
            txtBezugsAuftragsNummer.Text = bestellung.CBezugsAuftragsNummer;
            txtAnmerkung.Text = bestellung.CDruckAnmerkung;
            txtInternerKommentar.Text = bestellung.CInternerKommentar;
            dpLieferdatum.SelectedDate = bestellung.DLieferdatum;
            cboLager.SelectedValue = bestellung.KLager;
            cboFirma.SelectedValue = bestellung.KFirma;

            foreach (ComboBoxItem item in cboStatus.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out int status) && status == bestellung.NStatus)
                {
                    cboStatus.SelectedItem = item;
                    break;
                }
            }

            if (bestellung.NDropShipping == 1)
                rbDropshipping.IsChecked = true;

            // Lieferadresse laden
            chkLieferadresseGleich.IsChecked = bestellung.LieferadresseGleichRechnungsadresse;
            if (!bestellung.LieferadresseGleichRechnungsadresse && bestellung.Lieferadresse != null)
            {
                txtLAAnrede.Text = bestellung.Lieferadresse.CAnrede;
                txtLATitel.Text = bestellung.Lieferadresse.CTitel;
                txtLAVorname.Text = bestellung.Lieferadresse.CVorname;
                txtLANachname.Text = bestellung.Lieferadresse.CNachname;
                txtLAFirma.Text = bestellung.Lieferadresse.CFirma;
                txtLAFirmenzusatz.Text = bestellung.Lieferadresse.CFirmenZusatz;
                txtLAStrasse.Text = bestellung.Lieferadresse.CStrasse;
                txtLAPLZ.Text = bestellung.Lieferadresse.CPLZ;
                txtLAOrt.Text = bestellung.Lieferadresse.COrt;
                txtLABundesland.Text = bestellung.Lieferadresse.CBundesland;
                txtLAEmail.Text = bestellung.Lieferadresse.CMail;
                txtLAFax.Text = bestellung.Lieferadresse.CFax;
                txtLATelefon.Text = bestellung.Lieferadresse.CTel;
                txtLAMobil.Text = bestellung.Lieferadresse.CMobil;
            }

            var positionen = await _coreService.GetLieferantenBestellungPositionenAsync(bestellungId);
            _positionen.Clear();
            foreach (var pos in positionen)
            {
                _positionen.Add(PositionVM.FromDto(pos));
            }
        }

        private void Lieferant_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboLieferant.SelectedItem is CoreService.LieferantRef lieferant)
            {
                txtLiefFirma.Text = lieferant.CFirma;
                txtLiefStrasse.Text = lieferant.CStrasse;
                txtLiefPLZ.Text = lieferant.CPLZ;
                txtLiefOrt.Text = lieferant.COrt;
                txtLiefLand.Text = lieferant.CLand;
                txtLiefTelefon.Text = lieferant.CTelefon;
                txtLiefFax.Text = lieferant.CFax;
                txtLiefEmail.Text = lieferant.CEmail;
            }
        }

        private void LieferungAn_Changed(object sender, RoutedEventArgs e)
        {
            if (rbDropshipping == null || txtDropshipping == null || cboLieferungAn == null || cboLager == null)
                return;
            bool isDropshipping = rbDropshipping.IsChecked == true;
            txtDropshipping.IsEnabled = isDropshipping;
            cboLieferungAn.IsEnabled = isDropshipping;
            cboLager.IsEnabled = !isDropshipping;
        }

        private void LieferadresseGleich_Changed(object sender, RoutedEventArgs e)
        {
            if (chkLieferadresseGleich == null || grpLieferadresse == null)
                return;

            bool isGleich = chkLieferadresseGleich.IsChecked == true;
            grpLieferadresse.IsEnabled = !isGleich;

            if (isGleich && cboFirma.SelectedItem is CoreService.FirmaRef firma)
            {
                txtLAFirma.Text = firma.CFirma;
                txtLAStrasse.Text = firma.CStrasse;
                txtLAPLZ.Text = firma.CPLZ;
                txtLAOrt.Text = firma.COrt;
            }
        }

        private async void Suchen_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtSuchen.Text))
            {
                await FuegeArtikelHinzuAsync(txtSuchen.Text.Trim());
                txtSuchen.Clear();
            }
        }

        private async void ArtikelSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(txtArtikelSuche.Text))
            {
                await FuegeArtikelHinzuAsync(txtArtikelSuche.Text.Trim());
                txtArtikelSuche.Clear();
            }
        }

        private async System.Threading.Tasks.Task FuegeArtikelHinzuAsync(string artNrOderLiefArtNr)
        {
            if (cboLieferant.SelectedValue == null)
            {
                MessageBox.Show("Bitte zuerst einen Lieferanten auswaehlen!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int kLieferant = (int)cboLieferant.SelectedValue;

            try
            {
                var artikel = await _coreService.FindeArtikelFuerLieferantAsync(artNrOderLiefArtNr, kLieferant);

                if (artikel == null)
                {
                    MessageBox.Show($"Artikel '{artNrOderLiefArtNr}' nicht gefunden!", "Nicht gefunden", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existing = _positionen.FirstOrDefault(p => p.KArtikel == artikel.KArtikel);
                if (existing != null)
                {
                    existing.FMenge += 1;
                    dgPositionen.Items.Refresh();
                }
                else
                {
                    var pos = new PositionVM
                    {
                        KArtikel = artikel.KArtikel,
                        CArtNr = artikel.CArtNr,
                        CLieferantenArtNr = artikel.CLieferantenArtNr,
                        CName = artikel.CName,
                        FUST = artikel.FUST,
                        FMenge = 1,
                        FEKNetto = artikel.FEKNetto,
                        DLieferdatum = dpLieferdatum.SelectedDate,
                        NSort = _positionen.Count + 1
                    };
                    _positionen.Add(pos);
                }

                UpdateSummen();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ArtikelAuswahl_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Artikelauswahl-Dialog noch nicht implementiert", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Freiposition_Click(object sender, RoutedEventArgs e)
        {
            var pos = new PositionVM
            {
                KArtikel = 0,
                CArtNr = "",
                CName = "Freiposition",
                FMenge = 1,
                FEKNetto = 0,
                NPosTyp = 2,
                NSort = _positionen.Count + 1
            };
            _positionen.Add(pos);
            dgPositionen.SelectedItem = pos;
            UpdateSummen();
        }

        private void PositionLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgPositionen.SelectedItem is PositionVM pos)
            {
                _positionen.Remove(pos);
                UpdateSummen();
            }
        }

        private void PositionHoch_Click(object sender, RoutedEventArgs e)
        {
            if (dgPositionen.SelectedItem is PositionVM pos)
            {
                int index = _positionen.IndexOf(pos);
                if (index > 0)
                {
                    _positionen.Move(index, index - 1);
                    UpdateSortierung();
                }
            }
        }

        private void PositionRunter_Click(object sender, RoutedEventArgs e)
        {
            if (dgPositionen.SelectedItem is PositionVM pos)
            {
                int index = _positionen.IndexOf(pos);
                if (index < _positionen.Count - 1)
                {
                    _positionen.Move(index, index + 1);
                    UpdateSortierung();
                }
            }
        }

        private void UpdateSortierung()
        {
            for (int i = 0; i < _positionen.Count; i++)
                _positionen[i].NSort = i + 1;
        }

        private void UpdateSummen()
        {
            decimal nettoGesamt = _positionen.Sum(p => p.NettoGesamt);
            txtNettoEKGesamt.Text = nettoGesamt.ToString("N2");
            txtGewicht.Text = "0,0000";
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cboLieferant.SelectedValue == null)
                {
                    MessageBox.Show("Bitte Lieferant auswaehlen!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_positionen.Any())
                {
                    MessageBox.Show("Bitte mindestens eine Position hinzufuegen!", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int status = 5;
                if (cboStatus.SelectedItem is ComboBoxItem statusItem && int.TryParse(statusItem.Tag?.ToString(), out int s))
                    status = s;

                var bestellung = new CoreService.LieferantenBestellungDto
                {
                    KLieferantenBestellung = _bestellungId ?? 0,
                    KLieferant = (int)cboLieferant.SelectedValue,
                    KSprache = 1,
                    CWaehrungISO = "EUR",
                    CInternerKommentar = txtInternerKommentar.Text,
                    CDruckAnmerkung = txtAnmerkung.Text,
                    NStatus = status,
                    DErstellt = dpDatum.SelectedDate ?? DateTime.Now,
                    KFirma = cboFirma.SelectedValue as int? ?? 1,
                    KLager = cboLager.SelectedValue as int? ?? 0,
                    DLieferdatum = dpLieferdatum.SelectedDate,
                    CEigeneBestellnummer = txtEigeneBestellnummer.Text,
                    CBezugsAuftragsNummer = txtBezugsAuftragsNummer.Text,
                    NDropShipping = rbDropshipping.IsChecked == true ? 1 : 0,
                    CFremdbelegnummer = txtFremdbelegnummer.Text,
                    Positionen = _positionen.Select(p => p.ToDto()).ToList(),
                    LieferadresseGleichRechnungsadresse = chkLieferadresseGleich.IsChecked == true
                };

                // Lieferadresse hinzufuegen wenn abweichend
                if (chkLieferadresseGleich.IsChecked != true)
                {
                    bestellung.Lieferadresse = new CoreService.LieferantenBestellungAdresse
                    {
                        CAnrede = txtLAAnrede.Text,
                        CTitel = txtLATitel.Text,
                        CVorname = txtLAVorname.Text,
                        CNachname = txtLANachname.Text,
                        CFirma = txtLAFirma.Text,
                        CFirmenZusatz = txtLAFirmenzusatz.Text,
                        CStrasse = txtLAStrasse.Text,
                        CPLZ = txtLAPLZ.Text,
                        COrt = txtLAOrt.Text,
                        CBundesland = txtLABundesland.Text,
                        CLandISO = "DE", // TODO: Land aus ComboBox
                        CMail = txtLAEmail.Text,
                        CFax = txtLAFax.Text,
                        CTel = txtLATelefon.Text,
                        CMobil = txtLAMobil.Text
                    };
                }

                int bestellungId;
                if (_bestellungId.HasValue)
                {
                    await _coreService.UpdateLieferantenBestellungAsync(bestellung);
                    bestellungId = _bestellungId.Value;
                    MessageBox.Show("Bestellung wurde aktualisiert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    bestellungId = await _coreService.CreateLieferantenBestellungAsync(bestellung);
                    MessageBox.Show($"Bestellung {bestellungId} wurde angelegt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                WurdeSpeichert = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Wareneingang_Click(object sender, RoutedEventArgs e)
        {
            if (!_positionen.Any())
            {
                MessageBox.Show("Keine Positionen vorhanden.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_bestellungId.HasValue)
            {
                MessageBox.Show("Bitte zuerst die Bestellung speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pruefen ob es offene Positionen gibt
            var offenePositionen = _positionen.Where(p => p.FMenge > p.FMengeGeliefert).ToList();
            if (!offenePositionen.Any())
            {
                MessageBox.Show("Alle Positionen sind bereits vollstaendig geliefert.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lieferant = cboLieferant.SelectedItem is CoreService.LieferantRef l ? l.CFirma : "";
            var dialog = new WareneingangDialog(_bestellungId.Value, lieferant, offenePositionen);

            if (dialog.ShowDialog() == true && dialog.WurdeGebucht)
            {
                // Positionen neu laden
                _ = LadeBestellungAsync(_bestellungId.Value);
            }
        }

        private void EingangsrechnungDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private async void EingangsrechnungAlle_Click(object sender, RoutedEventArgs e)
        {
            await ErstelleEingangsrechnungAsync(nurGelieferte: false);
        }

        private async void EingangsrechnungGeliefert_Click(object sender, RoutedEventArgs e)
        {
            await ErstelleEingangsrechnungAsync(nurGelieferte: true);
        }

        private async System.Threading.Tasks.Task ErstelleEingangsrechnungAsync(bool nurGelieferte)
        {
            if (!_bestellungId.HasValue)
            {
                MessageBox.Show("Bitte zuerst die Bestellung speichern.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_positionen.Any())
            {
                MessageBox.Show("Keine Positionen vorhanden.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var relevantePositionen = nurGelieferte
                ? _positionen.Where(p => p.FMengeGeliefert > 0).ToList()
                : _positionen.ToList();

            if (!relevantePositionen.Any())
            {
                MessageBox.Show("Keine Positionen fuer Eingangsrechnung vorhanden.\n(Noch keine Lieferungen gebucht)", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var msg = nurGelieferte
                ? $"Eingangsrechnung fuer {relevantePositionen.Count} gelieferte Positionen erstellen?"
                : $"Eingangsrechnung fuer alle {relevantePositionen.Count} Positionen erstellen?";

            if (MessageBox.Show(msg, "Eingangsrechnung erstellen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                int kLieferant = (int)cboLieferant.SelectedValue!;
                var positionenFuerRechnung = relevantePositionen.Select(p => new CoreService.EingangsrechnungPosInput
                {
                    KArtikel = p.KArtikel,
                    CArtNr = p.CArtNr,
                    CLieferantenArtNr = p.CLieferantenArtNr,
                    CName = p.CName,
                    FMenge = nurGelieferte ? p.FMengeGeliefert : p.FMenge,
                    FEKNetto = p.FEKNetto,
                    FMwSt = p.FUST
                }).ToList();

                int rechnungId = await _coreService.CreateEingangsrechnungFromBestellungAsync(
                    _bestellungId.Value, kLieferant, positionenFuerRechnung);

                MessageBox.Show($"Eingangsrechnung {rechnungId} wurde erstellt.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                // Optional: Detailansicht oeffnen
                var detailView = new EingangsrechnungDetailView(rechnungId);
                detailView.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen der Eingangsrechnung:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ViewModel fuer Positionen mit PropertyChanged
    public class PositionVM : INotifyPropertyChanged
    {
        private decimal _fMenge;
        private decimal _fEKNetto;

        public int KLieferantenBestellungPos { get; set; }
        public int KLieferantenBestellung { get; set; }
        public int KArtikel { get; set; }
        public string CArtNr { get; set; } = "";
        public string CLieferantenArtNr { get; set; } = "";
        public string CName { get; set; } = "";
        public string CLieferantenBezeichnung { get; set; } = "";
        public decimal FUST { get; set; }

        public decimal FMenge
        {
            get => _fMenge;
            set { _fMenge = value; OnPropertyChanged(nameof(FMenge)); OnPropertyChanged(nameof(NettoGesamt)); }
        }

        public string CHinweis { get; set; } = "";

        public decimal FEKNetto
        {
            get => _fEKNetto;
            set { _fEKNetto = value; OnPropertyChanged(nameof(FEKNetto)); OnPropertyChanged(nameof(NettoGesamt)); }
        }

        public int NPosTyp { get; set; } = 1;
        public string CNameLieferant { get; set; } = "";
        public int NLiefertage { get; set; }
        public DateTime? DLieferdatum { get; set; }
        public int NSort { get; set; }
        public decimal FMengeGeliefert { get; set; }
        public string CVPEEinheit { get; set; } = "";
        public decimal NVPEMenge { get; set; }

        public decimal NettoGesamt => FMenge * FEKNetto;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static PositionVM FromDto(CoreService.LieferantenBestellungPosition dto)
        {
            return new PositionVM
            {
                KLieferantenBestellungPos = dto.KLieferantenBestellungPos,
                KLieferantenBestellung = dto.KLieferantenBestellung,
                KArtikel = dto.KArtikel,
                CArtNr = dto.CArtNr,
                CLieferantenArtNr = dto.CLieferantenArtNr,
                CName = dto.CName,
                CLieferantenBezeichnung = dto.CLieferantenBezeichnung,
                FUST = dto.FUST,
                FMenge = dto.FMenge,
                CHinweis = dto.CHinweis,
                FEKNetto = dto.FEKNetto,
                NPosTyp = dto.NPosTyp,
                CNameLieferant = dto.CNameLieferant,
                NLiefertage = dto.NLiefertage,
                DLieferdatum = dto.DLieferdatum,
                NSort = dto.NSort,
                FMengeGeliefert = dto.FMengeGeliefert,
                CVPEEinheit = dto.CVPEEinheit,
                NVPEMenge = dto.NVPEMenge
            };
        }

        public CoreService.LieferantenBestellungPosition ToDto()
        {
            return new CoreService.LieferantenBestellungPosition
            {
                KLieferantenBestellungPos = KLieferantenBestellungPos,
                KLieferantenBestellung = KLieferantenBestellung,
                KArtikel = KArtikel,
                CArtNr = CArtNr,
                CLieferantenArtNr = CLieferantenArtNr,
                CName = CName,
                CLieferantenBezeichnung = CLieferantenBezeichnung,
                FUST = FUST,
                FMenge = FMenge,
                CHinweis = CHinweis,
                FEKNetto = FEKNetto,
                NPosTyp = NPosTyp,
                CNameLieferant = CNameLieferant,
                NLiefertage = NLiefertage,
                DLieferdatum = DLieferdatum,
                NSort = NSort,
                FMengeGeliefert = FMengeGeliefert,
                CVPEEinheit = CVPEEinheit,
                NVPEMenge = NVPEMenge
            };
        }
    }
}
