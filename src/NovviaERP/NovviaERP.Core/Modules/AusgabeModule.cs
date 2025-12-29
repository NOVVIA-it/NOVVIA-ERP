using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Serilog;

namespace NovviaERP.Core.Modules
{
    /// <summary>
    /// Zentrales Ausgabe-Modul für alle Dokumenttypen
    /// Modulare Struktur: Formulare, Renderer, Ausgabe-Aktionen
    /// </summary>
    public class AusgabeModule
    {
        private readonly string _connectionString;
        private readonly IFormularRenderer _renderer;
        private static readonly ILogger _log = Log.ForContext<AusgabeModule>();

        public AusgabeModule(string connectionString, IFormularRenderer? renderer = null)
        {
            _connectionString = connectionString;
            _renderer = renderer ?? new QuestPdfRenderer();
        }

        #region Formulare laden

        /// <summary>
        /// Lädt verfügbare Formulare für einen Dokumenttyp
        /// </summary>
        public async Task<IEnumerable<FormularInfo>> GetFormulareAsync(AusgabeDokumentTyp typ)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var formularTyp = GetJtlFormularTyp(typ);

            var sql = @"
                SELECT fv.kFormularVorlage AS Id,
                       ISNULL(fv.cName, f.cName) AS Name,
                       CASE WHEN fv.nTyp = 2 THEN 1 ELSE 0 END AS IstStandard,
                       fv.kFormular AS FormularId
                FROM dbo.tFormularVorlage fv
                INNER JOIN dbo.tFormular f ON fv.kFormular = f.kFormular
                WHERE f.nTyp = @formularTyp
                ORDER BY CASE WHEN fv.nTyp = 2 THEN 0 ELSE 1 END, fv.cName";

            var result = await conn.QueryAsync<FormularInfo>(sql, new { formularTyp });

            // Fallback wenn keine JTL-Formulare
            if (!result.Any())
            {
                return new List<FormularInfo>
                {
                    new() { Id = 0, Name = "Standard", IstStandard = true }
                };
            }

            return result;
        }

        private int GetJtlFormularTyp(AusgabeDokumentTyp typ) => typ switch
        {
            AusgabeDokumentTyp.Angebot => 0,
            AusgabeDokumentTyp.Auftrag => 1,
            AusgabeDokumentTyp.Rechnung => 2,
            AusgabeDokumentTyp.Lieferschein => 3,
            AusgabeDokumentTyp.Gutschrift => 4,
            AusgabeDokumentTyp.Mahnung => 31,
            AusgabeDokumentTyp.Versandetikett => 13,
            AusgabeDokumentTyp.Packliste => 26,
            _ => 2
        };

        #endregion

        #region PDF generieren

        /// <summary>
        /// Generiert PDF für ein Dokument
        /// </summary>
        public async Task<byte[]?> GenerierePdfAsync(AusgabeDokumentTyp typ, int dokumentId, int? formularId = null)
        {
            try
            {
                _log.Debug("Generiere PDF: {Typ} ID={Id}", typ, dokumentId);

                // Dokument-Daten laden
                var daten = await LadeDokumentDatenAsync(typ, dokumentId);
                if (daten == null)
                {
                    _log.Warning("Keine Daten gefunden für {Typ} ID={Id}", typ, dokumentId);
                    return null;
                }

                // PDF rendern
                return await _renderer.RenderAsync(typ, daten, formularId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Generieren von {Typ} ID={Id}", typ, dokumentId);
                throw;
            }
        }

        /// <summary>
        /// Lädt Dokument-Daten aus der Datenbank
        /// </summary>
        private async Task<DokumentDaten?> LadeDokumentDatenAsync(AusgabeDokumentTyp typ, int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            return typ switch
            {
                AusgabeDokumentTyp.Rechnung => await LadeRechnungDatenAsync(conn, id),
                AusgabeDokumentTyp.Lieferschein => await LadeLieferscheinDatenAsync(conn, id),
                AusgabeDokumentTyp.Auftrag => await LadeAuftragDatenAsync(conn, id),
                AusgabeDokumentTyp.Angebot => await LadeAngebotDatenAsync(conn, id),
                AusgabeDokumentTyp.Mahnung => await LadeMahnungDatenAsync(conn, id),
                _ => null
            };
        }

        private async Task<DokumentDaten?> LadeRechnungDatenAsync(SqlConnection conn, int id)
        {
            var rechnung = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT r.kRechnung, r.cRechnungsNr, r.dErstellt, r.fGesamtNetto, r.fGesamtBrutto,
                       b.cBestellNr, b.kKunde,
                       k.cKundenNr, k.cFirma, k.cVorname, k.cName, k.cStrasse, k.cPLZ, k.cOrt,
                       ra.cFirma AS ReFirma, ra.cVorname AS ReVorname, ra.cName AS ReName,
                       ra.cStrasse AS ReStrasse, ra.cPLZ AS RePLZ, ra.cOrt AS ReOrt
                FROM dbo.tRechnung r
                LEFT JOIN dbo.tBestellung b ON r.kBestellung = b.kBestellung
                LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                LEFT JOIN Verkauf.tAuftragAdresse ra ON b.kBestellung = ra.kAuftrag AND ra.nTyp = 0
                WHERE r.kRechnung = @id", new { id });

            if (rechnung == null) return null;

            var positionen = await conn.QueryAsync<DokumentPosition>(@"
                SELECT rp.nPosNr AS PosNr, rp.cArtNr AS ArtNr, rp.cName AS Bezeichnung,
                       rp.fAnzahl AS Menge, rp.cEinheit AS Einheit,
                       rp.fVKNetto AS Einzelpreis, rp.fRabatt AS Rabatt,
                       rp.fVKNetto * rp.fAnzahl AS Netto
                FROM dbo.tRechnungsPos rp
                WHERE rp.kRechnung = @id
                ORDER BY rp.nPosNr", new { id });

            return new DokumentDaten
            {
                Typ = AusgabeDokumentTyp.Rechnung,
                Nummer = rechnung.cRechnungsNr ?? id.ToString(),
                Datum = rechnung.dErstellt,
                Netto = rechnung.fGesamtNetto ?? 0,
                Brutto = rechnung.fGesamtBrutto ?? 0,
                Kunde = new KundenDaten
                {
                    KundenNr = rechnung.cKundenNr,
                    Firma = rechnung.ReFirma ?? rechnung.cFirma,
                    Name = $"{rechnung.ReVorname ?? rechnung.cVorname} {rechnung.ReName ?? rechnung.cName}".Trim(),
                    Strasse = rechnung.ReStrasse ?? rechnung.cStrasse,
                    PLZ = rechnung.RePLZ ?? rechnung.cPLZ,
                    Ort = rechnung.ReOrt ?? rechnung.cOrt
                },
                Positionen = positionen.ToList()
            };
        }

        private async Task<DokumentDaten?> LadeLieferscheinDatenAsync(SqlConnection conn, int id)
        {
            var ls = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT l.kLieferschein, l.cLieferscheinNr, l.dErstellt,
                       b.cBestellNr, b.kKunde,
                       la.cFirma, la.cVorname, la.cName, la.cStrasse, la.cPLZ, la.cOrt
                FROM dbo.tLieferschein l
                LEFT JOIN dbo.tBestellung b ON l.kBestellung = b.kBestellung
                LEFT JOIN Verkauf.tAuftragAdresse la ON b.kBestellung = la.kAuftrag AND la.nTyp = 1
                WHERE l.kLieferschein = @id", new { id });

            if (ls == null) return null;

            var positionen = await conn.QueryAsync<DokumentPosition>(@"
                SELECT lp.nPosNr AS PosNr, lp.cArtNr AS ArtNr, lp.cName AS Bezeichnung,
                       lp.fAnzahl AS Menge, lp.cEinheit AS Einheit,
                       0 AS Einzelpreis, 0 AS Rabatt, 0 AS Netto
                FROM dbo.tLieferscheinPos lp
                WHERE lp.kLieferschein = @id
                ORDER BY lp.nPosNr", new { id });

            return new DokumentDaten
            {
                Typ = AusgabeDokumentTyp.Lieferschein,
                Nummer = ls.cLieferscheinNr ?? id.ToString(),
                Datum = ls.dErstellt,
                Kunde = new KundenDaten
                {
                    Firma = ls.cFirma,
                    Name = $"{ls.cVorname} {ls.cName}".Trim(),
                    Strasse = ls.cStrasse,
                    PLZ = ls.cPLZ,
                    Ort = ls.cOrt
                },
                Positionen = positionen.ToList()
            };
        }

        private async Task<DokumentDaten?> LadeAuftragDatenAsync(SqlConnection conn, int id)
        {
            var auftrag = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT b.kBestellung, b.cBestellNr, b.dErstellt, b.fGesamtsummeNetto, b.fGesamtsummeBrutto,
                       k.cKundenNr, k.cFirma, k.cVorname, k.cName, k.cStrasse, k.cPLZ, k.cOrt
                FROM dbo.tBestellung b
                LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                WHERE b.kBestellung = @id", new { id });

            if (auftrag == null) return null;

            var positionen = await conn.QueryAsync<DokumentPosition>(@"
                SELECT bp.nPosNr AS PosNr, bp.cArtNr AS ArtNr, bp.cName AS Bezeichnung,
                       bp.fAnzahl AS Menge, bp.cEinheit AS Einheit,
                       bp.fVKNetto AS Einzelpreis, 0 AS Rabatt,
                       bp.fVKNetto * bp.fAnzahl AS Netto
                FROM dbo.tBestellPos bp
                WHERE bp.kBestellung = @id
                ORDER BY bp.nPosNr", new { id });

            return new DokumentDaten
            {
                Typ = AusgabeDokumentTyp.Auftrag,
                Nummer = auftrag.cBestellNr ?? id.ToString(),
                Datum = auftrag.dErstellt,
                Netto = auftrag.fGesamtsummeNetto ?? 0,
                Brutto = auftrag.fGesamtsummeBrutto ?? 0,
                Kunde = new KundenDaten
                {
                    KundenNr = auftrag.cKundenNr,
                    Firma = auftrag.cFirma,
                    Name = $"{auftrag.cVorname} {auftrag.cName}".Trim(),
                    Strasse = auftrag.cStrasse,
                    PLZ = auftrag.cPLZ,
                    Ort = auftrag.cOrt
                },
                Positionen = positionen.ToList()
            };
        }

        private async Task<DokumentDaten?> LadeAngebotDatenAsync(SqlConnection conn, int id)
        {
            // Vereinfachte Version
            return new DokumentDaten
            {
                Typ = AusgabeDokumentTyp.Angebot,
                Nummer = id.ToString(),
                Datum = DateTime.Now
            };
        }

        private async Task<DokumentDaten?> LadeMahnungDatenAsync(SqlConnection conn, int id)
        {
            // Vereinfachte Version
            return new DokumentDaten
            {
                Typ = AusgabeDokumentTyp.Mahnung,
                Nummer = id.ToString(),
                Datum = DateTime.Now
            };
        }

        #endregion

        #region Ausgabe-Aktionen

        /// <summary>
        /// Führt Ausgabe-Aktionen aus
        /// </summary>
        public async Task<AusgabeResultat> AusfuehrenAsync(AusgabeAuftrag auftrag)
        {
            var resultat = new AusgabeResultat();

            try
            {
                // PDF generieren (falls nicht vorhanden)
                var pdf = auftrag.PdfDaten ?? await GenerierePdfAsync(auftrag.DokumentTyp, auftrag.DokumentId, auftrag.FormularId);
                if (pdf == null)
                {
                    resultat.Fehler.Add("PDF konnte nicht generiert werden");
                    return resultat;
                }

                resultat.PdfDaten = pdf;

                // Vorschau
                if (auftrag.MitVorschau)
                {
                    var tempPfad = Path.Combine(Path.GetTempPath(), $"NOVVIA_{auftrag.DokumentTyp}_{Guid.NewGuid()}.pdf");
                    await File.WriteAllBytesAsync(tempPfad, pdf);
                    resultat.VorschauPfad = tempPfad;
                    resultat.VorschauAngezeigt = true;
                }

                // Speichern
                if (auftrag.MitSpeichern && !string.IsNullOrEmpty(auftrag.SpeicherPfad))
                {
                    var dir = Path.GetDirectoryName(auftrag.SpeicherPfad);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(auftrag.SpeicherPfad, pdf);
                    resultat.GespeicherterPfad = auftrag.SpeicherPfad;
                }

                // Drucken (TODO: Implementieren)
                if (auftrag.MitDrucken)
                {
                    // Drucken über Windows-API
                    resultat.Gedruckt = true;
                }

                // E-Mail (TODO: Implementieren)
                if (auftrag.MitEmail)
                {
                    resultat.EmailGesendet = false;
                    resultat.Fehler.Add("E-Mail-Versand noch nicht implementiert");
                }
            }
            catch (Exception ex)
            {
                resultat.Fehler.Add(ex.Message);
                _log.Error(ex, "Fehler bei Ausgabe {Typ} ID={Id}", auftrag.DokumentTyp, auftrag.DokumentId);
            }

            return resultat;
        }

        #endregion

        #region E-Mail

        /// <summary>
        /// Lädt E-Mail-Adresse für ein Dokument
        /// </summary>
        public async Task<string?> GetDokumentEmailAsync(AusgabeDokumentTyp typ, int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            return typ switch
            {
                AusgabeDokumentTyp.Rechnung => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT COALESCE(b.cEmail, k.cMail)
                      FROM dbo.tRechnung r
                      LEFT JOIN dbo.tBestellung b ON r.kBestellung = b.kBestellung
                      LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                      WHERE r.kRechnung = @id", new { id }),

                AusgabeDokumentTyp.Auftrag => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT COALESCE(b.cEmail, k.cMail)
                      FROM dbo.tBestellung b
                      LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                      WHERE b.kBestellung = @id", new { id }),

                _ => null
            };
        }

        /// <summary>
        /// Lädt E-Mail-Vorlagen
        /// </summary>
        public async Task<IEnumerable<EmailVorlageInfo>> GetEmailVorlagenAsync(AusgabeDokumentTyp? typ = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT kEmailVorlage AS Id, cName AS Name, cBetreff AS Betreff FROM tEmailVorlage WHERE nAktiv = 1";
            if (typ.HasValue)
                sql += " AND cTyp = @Typ";
            sql += " ORDER BY cName";

            return await conn.QueryAsync<EmailVorlageInfo>(sql, new { Typ = typ?.ToString() });
        }

        #endregion
    }

    #region Interfaces

    /// <summary>
    /// Interface für Formular-Renderer
    /// </summary>
    public interface IFormularRenderer
    {
        Task<byte[]?> RenderAsync(AusgabeDokumentTyp typ, DokumentDaten daten, int? formularId = null);
    }

    #endregion

    #region DTOs

    public enum AusgabeDokumentTyp
    {
        Rechnung, Lieferschein, Auftrag, Angebot, Mahnung, Gutschrift, Packliste, Versandetikett
    }

    public class FormularInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IstStandard { get; set; }
        public int FormularId { get; set; }
    }

    public class EmailVorlageInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Betreff { get; set; } = "";
    }

    public class DokumentDaten
    {
        public AusgabeDokumentTyp Typ { get; set; }
        public string Nummer { get; set; } = "";
        public DateTime Datum { get; set; }
        public decimal Netto { get; set; }
        public decimal Brutto { get; set; }
        public decimal MwSt => Brutto - Netto;
        public KundenDaten? Kunde { get; set; }
        public List<DokumentPosition> Positionen { get; set; } = new();
    }

    public class KundenDaten
    {
        public string? KundenNr { get; set; }
        public string? Firma { get; set; }
        public string? Name { get; set; }
        public string? Strasse { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string AdressBlock => string.Join("\n",
            new[] { Firma, Name, Strasse, $"{PLZ} {Ort}" }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public class DokumentPosition
    {
        public int PosNr { get; set; }
        public string? ArtNr { get; set; }
        public string? Bezeichnung { get; set; }
        public decimal Menge { get; set; }
        public string? Einheit { get; set; }
        public decimal Einzelpreis { get; set; }
        public decimal Rabatt { get; set; }
        public decimal Netto { get; set; }
    }

    public class AusgabeAuftrag
    {
        public AusgabeDokumentTyp DokumentTyp { get; set; }
        public int DokumentId { get; set; }
        public int? FormularId { get; set; }
        public byte[]? PdfDaten { get; set; }

        public bool MitVorschau { get; set; }
        public bool MitDrucken { get; set; }
        public bool MitSpeichern { get; set; }
        public bool MitEmail { get; set; }

        public string? DruckerName { get; set; }
        public int Kopien { get; set; } = 1;
        public string? SpeicherPfad { get; set; }
        public string? EmailEmpfaenger { get; set; }
        public int? EmailVorlageId { get; set; }
    }

    public class AusgabeResultat
    {
        public byte[]? PdfDaten { get; set; }
        public string? VorschauPfad { get; set; }
        public bool VorschauAngezeigt { get; set; }
        public bool Gedruckt { get; set; }
        public string? GespeicherterPfad { get; set; }
        public bool EmailGesendet { get; set; }
        public List<string> Fehler { get; set; } = new();
        public bool IstErfolgreich => !Fehler.Any();
    }

    #endregion
}
