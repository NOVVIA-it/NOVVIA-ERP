using System;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog;

namespace NovviaERP.Core.Modules
{
    /// <summary>
    /// Einfacher PDF-Renderer mit QuestPDF
    /// Ersetzt combit List & Label
    /// </summary>
    public class QuestPdfRenderer : IFormularRenderer
    {
        private static readonly ILogger _log = Log.ForContext<QuestPdfRenderer>();

        static QuestPdfRenderer()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]?> RenderAsync(AusgabeDokumentTyp typ, DokumentDaten daten, int? formularId = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var document = typ switch
                    {
                        AusgabeDokumentTyp.Rechnung => ErstelleRechnung(daten),
                        AusgabeDokumentTyp.Lieferschein => ErstelleLieferschein(daten),
                        AusgabeDokumentTyp.Auftrag => ErstelleAuftrag(daten),
                        AusgabeDokumentTyp.Angebot => ErstelleAngebot(daten),
                        AusgabeDokumentTyp.Mahnung => ErstelleMahnung(daten),
                        _ => ErstelleGenericDokument(daten)
                    };

                    using var stream = new MemoryStream();
                    document.GeneratePdf(stream);
                    return stream.ToArray();
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Fehler beim Rendern von {Typ}", typ);
                    return null;
                }
            });
        }

        #region Dokument-Typen

        private Document ErstelleRechnung(DokumentDaten daten)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => Kopfzeile(c, "RECHNUNG", daten.Nummer, daten.Datum));
                    page.Content().Element(c => RechnungInhalt(c, daten));
                    page.Footer().Element(Fusszeile);
                });
            });
        }

        private Document ErstelleLieferschein(DokumentDaten daten)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => Kopfzeile(c, "LIEFERSCHEIN", daten.Nummer, daten.Datum));
                    page.Content().Element(c => LieferscheinInhalt(c, daten));
                    page.Footer().Element(Fusszeile);
                });
            });
        }

        private Document ErstelleAuftrag(DokumentDaten daten)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => Kopfzeile(c, "AUFTRAGSBESTAETIGUNG", daten.Nummer, daten.Datum));
                    page.Content().Element(c => RechnungInhalt(c, daten)); // Gleiches Layout wie Rechnung
                    page.Footer().Element(Fusszeile);
                });
            });
        }

        private Document ErstelleAngebot(DokumentDaten daten)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => Kopfzeile(c, "ANGEBOT", daten.Nummer, daten.Datum));
                    page.Content().Element(c => RechnungInhalt(c, daten));
                    page.Footer().Element(Fusszeile);
                });
            });
        }

        private Document ErstelleMahnung(DokumentDaten daten)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => Kopfzeile(c, "MAHNUNG", daten.Nummer, daten.Datum));
                    page.Content().Column(col =>
                    {
                        col.Item().PaddingTop(20).Text("Sehr geehrte Damen und Herren,").FontSize(10);
                        col.Item().PaddingTop(10).Text("trotz Faelligkeit konnten wir fuer die nachstehende Rechnung keinen Zahlungseingang feststellen.").FontSize(10);
                    });
                    page.Footer().Element(Fusszeile);
                });
            });
        }

        private Document ErstelleGenericDokument(DokumentDaten daten)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => Kopfzeile(c, daten.Typ.ToString().ToUpper(), daten.Nummer, daten.Datum));
                    page.Content().Element(c => RechnungInhalt(c, daten));
                    page.Footer().Element(Fusszeile);
                });
            });
        }

        #endregion

        #region Komponenten

        private void Kopfzeile(IContainer container, string titel, string nummer, DateTime datum)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("NOVVIA GmbH").Bold().FontSize(14);
                    col.Item().Text("Musterstrasse 1");
                    col.Item().Text("12345 Musterstadt");
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(titel).Bold().FontSize(16);
                    col.Item().Text($"Nr.: {nummer}");
                    col.Item().Text($"Datum: {datum:dd.MM.yyyy}");
                });
            });
        }

        private void RechnungInhalt(IContainer container, DokumentDaten daten)
        {
            container.Column(col =>
            {
                // Kundenadresse
                col.Item().PaddingTop(30).Border(0).Column(addr =>
                {
                    if (daten.Kunde != null)
                    {
                        if (!string.IsNullOrEmpty(daten.Kunde.Firma))
                            addr.Item().Text(daten.Kunde.Firma).Bold();
                        if (!string.IsNullOrEmpty(daten.Kunde.Name))
                            addr.Item().Text(daten.Kunde.Name);
                        if (!string.IsNullOrEmpty(daten.Kunde.Strasse))
                            addr.Item().Text(daten.Kunde.Strasse);
                        addr.Item().Text($"{daten.Kunde.PLZ} {daten.Kunde.Ort}");
                    }
                });

                // Positionen-Tabelle
                col.Item().PaddingTop(30).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);  // Pos
                        columns.ConstantColumn(80);  // ArtNr
                        columns.RelativeColumn();    // Bezeichnung
                        columns.ConstantColumn(50);  // Menge
                        columns.ConstantColumn(70);  // Einzelpreis
                        columns.ConstantColumn(70);  // Gesamt
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Pos").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Art.Nr.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Bezeichnung").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Menge").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Preis").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Gesamt").Bold();
                    });

                    // Zeilen
                    foreach (var pos in daten.Positionen)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.PosNr.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.ArtNr ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.Bezeichnung ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{pos.Menge:N2}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{pos.Einzelpreis:N2}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{pos.Netto:N2}");
                    }
                });

                // Summen
                col.Item().PaddingTop(20).AlignRight().Width(200).Column(sum =>
                {
                    sum.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Netto:");
                        r.ConstantItem(80).AlignRight().Text($"{daten.Netto:N2} EUR");
                    });
                    sum.Item().Row(r =>
                    {
                        r.RelativeItem().Text("MwSt:");
                        r.ConstantItem(80).AlignRight().Text($"{daten.MwSt:N2} EUR");
                    });
                    sum.Item().PaddingTop(5).BorderTop(1).Row(r =>
                    {
                        r.RelativeItem().Text("Brutto:").Bold();
                        r.ConstantItem(80).AlignRight().Text($"{daten.Brutto:N2} EUR").Bold();
                    });
                });
            });
        }

        private void LieferscheinInhalt(IContainer container, DokumentDaten daten)
        {
            container.Column(col =>
            {
                // Kundenadresse (Lieferadresse)
                col.Item().PaddingTop(30).Column(addr =>
                {
                    if (daten.Kunde != null)
                    {
                        if (!string.IsNullOrEmpty(daten.Kunde.Firma))
                            addr.Item().Text(daten.Kunde.Firma).Bold();
                        if (!string.IsNullOrEmpty(daten.Kunde.Name))
                            addr.Item().Text(daten.Kunde.Name);
                        if (!string.IsNullOrEmpty(daten.Kunde.Strasse))
                            addr.Item().Text(daten.Kunde.Strasse);
                        addr.Item().Text($"{daten.Kunde.PLZ} {daten.Kunde.Ort}");
                    }
                });

                // Positionen-Tabelle (ohne Preise)
                col.Item().PaddingTop(30).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(30);  // Pos
                        columns.ConstantColumn(100); // ArtNr
                        columns.RelativeColumn();    // Bezeichnung
                        columns.ConstantColumn(60);  // Menge
                        columns.ConstantColumn(50);  // Einheit
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Pos").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Art.Nr.").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Bezeichnung").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Menge").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Einheit").Bold();
                    });

                    // Zeilen
                    foreach (var pos in daten.Positionen)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.PosNr.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.ArtNr ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.Bezeichnung ?? "");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text($"{pos.Menge:N0}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(pos.Einheit ?? "Stk");
                    }
                });
            });
        }

        private void Fusszeile(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Seite ");
                text.CurrentPageNumber();
                text.Span(" von ");
                text.TotalPages();
            });
        }

        #endregion
    }
}
