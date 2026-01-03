using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovviaERP.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog;
using ZXing;
using ZXing.Common;

namespace NovviaERP.Core.Services
{
    public class ReportService
    {
        private readonly Firma _firma;
        private static readonly ILogger _log = Log.ForContext<ReportService>();

        // Briefpapier-Bilder (werden einmal geladen und gecached)
        private byte[]? _briefpapierRechnung;
        private byte[]? _briefpapierLieferschein;
        private byte[]? _briefpapierMahnung;
        private byte[]? _briefpapierAngebot;
        private byte[]? _briefpapierAuftrag;
        private byte[]? _briefpapierGutschrift;
        private bool _briefpapierAktiv = false;

        public ReportService(Firma firma) { _firma = firma; QuestPDF.Settings.License = LicenseType.Community; }

        /// <summary>
        /// Setzt die Briefpapier-Bilder fuer alle Belegtypen
        /// </summary>
        public void SetBriefpapier(
            byte[]? rechnung = null,
            byte[]? lieferschein = null,
            byte[]? mahnung = null,
            byte[]? angebot = null,
            byte[]? auftrag = null,
            byte[]? gutschrift = null,
            bool aktiv = true)
        {
            _briefpapierRechnung = rechnung;
            _briefpapierLieferschein = lieferschein;
            _briefpapierMahnung = mahnung;
            _briefpapierAngebot = angebot;
            _briefpapierAuftrag = auftrag;
            _briefpapierGutschrift = gutschrift;
            _briefpapierAktiv = aktiv;
            _log.Information("Briefpapier gesetzt: Aktiv={Aktiv}, Rechnung={R}, LS={L}, Mahnung={M}",
                aktiv, rechnung?.Length > 0, lieferschein?.Length > 0, mahnung?.Length > 0);
        }

        /// <summary>
        /// Fuegt Briefpapier als Hintergrund auf der Seite ein
        /// </summary>
        private void ApplyBriefpapier(PageDescriptor page, byte[]? briefpapier)
        {
            if (_briefpapierAktiv && briefpapier != null && briefpapier.Length > 0)
            {
                page.Background().Image(briefpapier, ImageScaling.Resize);
            }
        }

        #region Rechnung
        public byte[] GenerateRechnungPdf(Rechnung rechnung, Kunde kunde, List<RechnungsPosition> positionen)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Briefpapier als Hintergrund
                    ApplyBriefpapier(page, _briefpapierRechnung);

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(_firma.Name).Bold().FontSize(16);
                                c.Item().Text($"{_firma.Strasse}");
                                c.Item().Text($"{_firma.PLZ} {_firma.Ort}");
                                c.Item().Text($"Tel: {_firma.Telefon}");
                                c.Item().Text($"E-Mail: {_firma.Email}");
                            });
                            row.RelativeItem().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Rechnung Nr. {rechnung.RechnungsNr}").Bold().FontSize(14);
                                c.Item().Text($"Datum: {rechnung.Erstellt:dd.MM.yyyy}");
                                c.Item().Text($"Fällig: {rechnung.Faellig:dd.MM.yyyy}");
                            });
                        });
                        col.Item().PaddingTop(20).Column(c =>
                        {
                            if (!string.IsNullOrEmpty(kunde.Firma)) c.Item().Text(kunde.Firma).Bold();
                            c.Item().Text($"{kunde.Vorname} {kunde.Nachname}");
                            c.Item().Text(kunde.Strasse);
                            c.Item().Text($"{kunde.PLZ} {kunde.Ort}");
                        });
                    });

                    page.Content().PaddingTop(30).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(4); c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); });
                            table.Header(h =>
                            {
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Pos").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Bezeichnung").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Menge").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Einzelpreis").Bold();
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Gesamt").Bold();
                            });
                            int pos = 1;
                            foreach (var p in positionen)
                            {
                                table.Cell().Padding(5).Text($"{pos++}");
                                table.Cell().Padding(5).Text(p.Name);
                                table.Cell().Padding(5).AlignRight().Text($"{p.Menge:N0}");
                                table.Cell().Padding(5).AlignRight().Text($"{p.VKBrutto:N2} €");
                                table.Cell().Padding(5).AlignRight().Text($"{p.Menge * p.VKBrutto:N2} €");
                            }
                        });
                        col.Item().PaddingTop(20).AlignRight().Width(200).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); });
                            table.Cell().Padding(3).Text("Netto:");
                            table.Cell().Padding(3).AlignRight().Text($"{rechnung.Netto:N2} €");
                            table.Cell().Padding(3).Text("MwSt 19%:");
                            table.Cell().Padding(3).AlignRight().Text($"{rechnung.Brutto - rechnung.Netto:N2} €");
                            table.Cell().Padding(3).Text("Gesamt:").Bold();
                            table.Cell().Padding(3).AlignRight().Text($"{rechnung.Brutto:N2} €").Bold();
                        });
                        col.Item().PaddingTop(30).Text($"Bitte überweisen Sie den Betrag bis zum {rechnung.Faellig:dd.MM.yyyy} auf folgendes Konto:");
                        col.Item().PaddingTop(5).Text($"IBAN: {_firma.IBAN}");
                        col.Item().Text($"BIC: {_firma.BIC} | Bank: {_firma.Bank}");
                        col.Item().Text($"Verwendungszweck: {rechnung.RechnungsNr}");
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span($"{_firma.Name} | USt-IdNr: {_firma.UStID} | ");
                        t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
                    });
                });
            });
            return doc.GeneratePdf();
        }
        #endregion

        #region Lieferschein
        public byte[] GenerateLieferscheinPdf(Lieferschein ls, Bestellung bestellung, List<LieferscheinPosition> positionen)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);

                    // Briefpapier als Hintergrund
                    ApplyBriefpapier(page, _briefpapierLieferschein);

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(_firma.Name).Bold().FontSize(14);
                            row.RelativeItem().AlignRight().Text($"Lieferschein {ls.LieferscheinNr}").Bold().FontSize(12);
                        });
                        col.Item().PaddingTop(10).Text($"Bestellung: {bestellung.BestellNr} | Datum: {ls.Erstellt:dd.MM.yyyy}");
                        if (bestellung.Lieferadresse != null)
                        {
                            col.Item().PaddingTop(15).Column(c =>
                            {
                                if (!string.IsNullOrEmpty(bestellung.Lieferadresse.Firma)) c.Item().Text(bestellung.Lieferadresse.Firma).Bold();
                                c.Item().Text($"{bestellung.Lieferadresse.Vorname} {bestellung.Lieferadresse.Nachname}");
                                c.Item().Text(bestellung.Lieferadresse.Strasse);
                                c.Item().Text($"{bestellung.Lieferadresse.PLZ} {bestellung.Lieferadresse.Ort}");
                            });
                        }
                    });
                    page.Content().PaddingTop(20).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(5); c.RelativeColumn(1); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Pos").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Art.Nr.").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Bezeichnung").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Menge").Bold();
                        });
                        int pos = 1;
                        foreach (var p in positionen)
                        {
                            table.Cell().Padding(5).Text($"{pos++}");
                            table.Cell().Padding(5).Text(p.ArtNr ?? "");
                            table.Cell().Padding(5).Text(p.Name);
                            table.Cell().Padding(5).AlignRight().Text($"{p.Menge:N0}");
                        }
                    });
                    page.Footer().AlignCenter().Text($"Vielen Dank für Ihre Bestellung! | {_firma.Name}");
                });
            });
            return doc.GeneratePdf();
        }
        #endregion

        #region Mahnung
        public byte[] GenerateMahnungPdf(Mahnung mahnung, Kunde kunde, List<Rechnung> rechnungen)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);

                    // Briefpapier als Hintergrund
                    ApplyBriefpapier(page, _briefpapierMahnung);

                    page.Header().Column(col =>
                    {
                        col.Item().Text(_firma.Name).Bold().FontSize(14);
                        col.Item().PaddingTop(20).Text($"{mahnung.Mahnstufe}. Mahnung").Bold().FontSize(16);
                        col.Item().PaddingTop(10).Column(c =>
                        {
                            if (!string.IsNullOrEmpty(kunde.Firma)) c.Item().Text(kunde.Firma);
                            c.Item().Text($"{kunde.Vorname} {kunde.Nachname}");
                            c.Item().Text($"{kunde.Strasse}");
                            c.Item().Text($"{kunde.PLZ} {kunde.Ort}");
                        });
                    });
                    page.Content().PaddingTop(30).Column(col =>
                    {
                        col.Item().Text("Sehr geehrte Damen und Herren,");
                        col.Item().PaddingTop(10).Text($"trotz Fälligkeit sind folgende Rechnungen noch nicht beglichen:");
                        col.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); });
                            table.Header(h =>
                            {
                                h.Cell().Padding(3).Text("Rechnung").Bold();
                                h.Cell().Padding(3).Text("Datum").Bold();
                                h.Cell().Padding(3).Text("Fällig").Bold();
                                h.Cell().Padding(3).AlignRight().Text("Offen").Bold();
                            });
                            foreach (var r in rechnungen)
                            {
                                table.Cell().Padding(3).Text(r.RechnungsNr);
                                table.Cell().Padding(3).Text($"{r.Erstellt:dd.MM.yyyy}");
                                table.Cell().Padding(3).Text($"{r.Faellig:dd.MM.yyyy}");
                                table.Cell().Padding(3).AlignRight().Text($"{r.Offen:N2} €");
                            }
                        });
                        col.Item().PaddingTop(15).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                            table.Cell().Padding(3).Text("Offene Rechnungen:");
                            table.Cell().Padding(3).AlignRight().Text($"{mahnung.SummeOffen:N2} €");
                            if (mahnung.Gebuehr > 0) { table.Cell().Padding(3).Text("Mahngebühr:"); table.Cell().Padding(3).AlignRight().Text($"{mahnung.Gebuehr:N2} €"); }
                            if (mahnung.Zinsen > 0) { table.Cell().Padding(3).Text("Verzugszinsen:"); table.Cell().Padding(3).AlignRight().Text($"{mahnung.Zinsen:N2} €"); }
                            table.Cell().Padding(3).Text("Gesamtbetrag:").Bold();
                            table.Cell().Padding(3).AlignRight().Text($"{mahnung.SummeOffen + mahnung.Gebuehr + mahnung.Zinsen:N2} €").Bold();
                        });
                        col.Item().PaddingTop(20).Text($"Bitte überweisen Sie den Gesamtbetrag bis zum {mahnung.Faellig:dd.MM.yyyy}.");
                        col.Item().PaddingTop(30).Text("Mit freundlichen Grüßen");
                        col.Item().Text(_firma.Name);
                    });
                });
            });
            return doc.GeneratePdf();
        }
        #endregion

        #region Etiketten
        public byte[] GenerateArtikelEtikett(Artikel artikel, int anzahl = 1, string format = "62x29")
        {
            var (width, height) = format switch { "62x29" => (62, 29), "62x100" => (62, 100), "102x51" => (102, 51), _ => (62, 29) };
            var barcode = GenerateBarcode(artikel.Barcode ?? artikel.ArtNr, BarcodeFormat.CODE_128, 200, 50);
            var doc = Document.Create(container =>
            {
                for (int i = 0; i < anzahl; i++)
                {
                    container.Page(page =>
                    {
                        page.Size(width, height, Unit.Millimetre);
                        page.Margin(2, Unit.Millimetre);
                        page.Content().Column(col =>
                        {
                            col.Item().Text(artikel.Beschreibung?.Name ?? artikel.ArtNr).Bold().FontSize(8);
                            col.Item().Text($"Art.Nr: {artikel.ArtNr}").FontSize(6);
                            col.Item().Text($"{artikel.VKBrutto:N2} €").Bold().FontSize(10);
                            col.Item().AlignCenter().Image(barcode).FitWidth();
                            col.Item().AlignCenter().Text(artikel.Barcode ?? artikel.ArtNr).FontSize(6);
                        });
                    });
                }
            });
            return doc.GeneratePdf();
        }

        public byte[] GenerateVersandEtikett(string name, string strasse, string plz, string ort, string land, string? trackingNr = null)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(100, 150, Unit.Millimetre);
                    page.Margin(5, Unit.Millimetre);
                    page.Content().Column(col =>
                    {
                        col.Item().Text("ABSENDER:").FontSize(6);
                        col.Item().Text(_firma.Name).FontSize(8);
                        col.Item().Text($"{_firma.Strasse}, {_firma.PLZ} {_firma.Ort}").FontSize(8);
                        col.Item().PaddingTop(10).Text("EMPFÄNGER:").FontSize(6);
                        col.Item().Text(name).Bold().FontSize(12);
                        col.Item().Text(strasse).FontSize(10);
                        col.Item().Text($"{plz} {ort}").Bold().FontSize(14);
                        col.Item().Text(land).FontSize(10);
                        if (!string.IsNullOrEmpty(trackingNr))
                        {
                            var barcode = GenerateBarcode(trackingNr, BarcodeFormat.CODE_128, 300, 60);
                            col.Item().PaddingTop(10).AlignCenter().Image(barcode).FitWidth();
                            col.Item().AlignCenter().Text(trackingNr).FontSize(8);
                        }
                    });
                });
            });
            return doc.GeneratePdf();
        }

        private byte[] GenerateBarcode(string text, BarcodeFormat format, int width, int height)
        {
            var writer = new BarcodeWriterPixelData { Format = format, Options = new EncodingOptions { Width = width, Height = height, Margin = 0 } };
            var pixelData = writer.Write(text);
            using var ms = new MemoryStream();
            // Simplified - in production use proper image library
            return ms.ToArray();
        }
        #endregion

        #region DATEV
        public string GenerateDATEVExport(List<Rechnung> rechnungen, DateTime von, DateTime bis)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\"Umsatz (ohne Soll/Haben-Kz)\";\"Soll/Haben-Kennzeichen\";\"WKZ Umsatz\";\"Kurs\";\"Basis-Umsatz\";\"WKZ Basis-Umsatz\";\"Konto\";\"Gegenkonto (ohne BU-Schlüssel)\";\"BU-Schlüssel\";\"Belegdatum\";\"Belegfeld 1\";\"Belegfeld 2\";\"Skonto\";\"Buchungstext\"");
            foreach (var r in rechnungen.Where(r => r.Erstellt >= von && r.Erstellt <= bis))
            {
                var betrag = r.Brutto.ToString("F2").Replace(".", ",");
                var datum = r.Erstellt.ToString("ddMM");
                var konto = r.Typ == RechnungTyp.Gutschrift ? "8400" : "4400";
                sb.AppendLine($"\"{betrag}\";\"S\";\"EUR\";\"\";\"\";\"\";\"10000\";\"{konto}\";\"\";\"0101\";\"0101{datum}\";\"\";\"\";\"Rechnung {r.RechnungsNr}\"");
            }
            _log.Information("DATEV Export: {Count} Buchungen", rechnungen.Count);
            return sb.ToString();
        }
        #endregion

        #region Async Stubs (for AusgabeService)
        public System.Threading.Tasks.Task<byte[]?> GenerateRechnungPdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        public System.Threading.Tasks.Task<byte[]?> GenerateLieferscheinPdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        public System.Threading.Tasks.Task<byte[]?> GenerateMahnungPdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        public System.Threading.Tasks.Task<byte[]?> GenerateAngebotPdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        public System.Threading.Tasks.Task<byte[]?> GenerateBestellungPdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        public System.Threading.Tasks.Task<byte[]?> GenerateGutschriftPdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        public System.Threading.Tasks.Task<byte[]?> GeneratePacklistePdfAsync(int id) => System.Threading.Tasks.Task.FromResult<byte[]?>(null);
        #endregion
    }
}
