using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Angebots-Verwaltung mit Umwandlung zu Aufträgen
    /// </summary>
    public class AngebotService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<AngebotService>();

        public AngebotService(JtlDbContext db) => _db = db;

        #region Angebote CRUD
        /// <summary>
        /// Angebote suchen
        /// </summary>
        public async Task<IEnumerable<Angebot>> SucheAngeboteAsync(AngebotFilter? filter = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = @"
                SELECT a.*, k.cFirma AS KundeFirma, CONCAT(k.cVorname, ' ', k.cNachname) AS KundeName
                FROM tAngebot a
                LEFT JOIN tKunde k ON a.kKunde = k.kKunde
                WHERE 1=1";

            if (filter != null)
            {
                if (filter.KundeId.HasValue) sql += " AND a.kKunde = @KundeId";
                if (filter.Status.HasValue) sql += " AND a.nStatus = @Status";
                if (filter.VonDatum.HasValue) sql += " AND a.dAngebotsDatum >= @VonDatum";
                if (filter.BisDatum.HasValue) sql += " AND a.dAngebotsDatum <= @BisDatum";
                if (!string.IsNullOrEmpty(filter.Suchtext))
                    sql += " AND (a.cAngebotNr LIKE @Such OR k.cFirma LIKE @Such OR k.cNachname LIKE @Such)";
            }

            sql += " ORDER BY a.dAngebotsDatum DESC";

            return await conn.QueryAsync<Angebot>(sql, new
            {
                filter?.KundeId,
                filter?.Status,
                filter?.VonDatum,
                filter?.BisDatum,
                Such = filter?.Suchtext != null ? $"%{filter.Suchtext}%" : null
            });
        }

        /// <summary>
        /// Angebot mit Positionen laden
        /// </summary>
        public async Task<Angebot?> GetAngebotAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            var angebot = await conn.QuerySingleOrDefaultAsync<Angebot>(
                "SELECT * FROM tAngebot WHERE kAngebot = @Id", new { Id = id });

            if (angebot != null)
            {
                angebot.Positionen = (await conn.QueryAsync<AngebotPosition>(
                    "SELECT * FROM tAngebotPos WHERE kAngebot = @Id ORDER BY nPos", new { Id = id })).ToList();
            }

            return angebot;
        }

        /// <summary>
        /// Angebot erstellen
        /// </summary>
        public async Task<int> CreateAngebotAsync(Angebot angebot)
        {
            var conn = await _db.GetConnectionAsync();
            
            // Nächste Angebotsnummer generieren
            if (string.IsNullOrEmpty(angebot.AngebotNr))
            {
                var letzte = await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT TOP 1 cAngebotNr FROM tAngebot ORDER BY kAngebot DESC");
                var nummer = 1;
                if (!string.IsNullOrEmpty(letzte) && int.TryParse(letzte.Split('-').Last(), out var n))
                    nummer = n + 1;
                angebot.AngebotNr = $"AN-{DateTime.Now:yyyy}-{nummer:D5}";
            }

            angebot.Id = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tAngebot (cAngebotNr, kKunde, dAngebotsDatum, dGueltigBis, nStatus,
                    fNetto, fMwSt, fBrutto, cWaehrung, cBemerkung, cIntNotiz,
                    cLieferStrasse, cLieferPLZ, cLieferOrt, cLieferLand,
                    kBearbeiter, dErstellt)
                VALUES (@AngebotNr, @KundeId, @AngebotsDatum, @GueltigBis, @Status,
                    @Netto, @MwSt, @Brutto, @Waehrung, @Bemerkung, @InterneNotiz,
                    @LieferStrasse, @LieferPLZ, @LieferOrt, @LieferLand,
                    @BearbeiterId, GETDATE());
                SELECT SCOPE_IDENTITY();", angebot);

            // Positionen speichern
            var pos = 1;
            foreach (var p in angebot.Positionen)
            {
                p.AngebotId = angebot.Id;
                p.PosNr = pos++;
                await SavePositionAsync(p);
            }

            // Summen berechnen
            await BerechneAngebotSummenAsync(angebot.Id);

            _log.Information("Angebot {Nr} erstellt für Kunde {KundeId}", angebot.AngebotNr, angebot.KundeId);
            return angebot.Id;
        }

        /// <summary>
        /// Angebot aktualisieren
        /// </summary>
        public async Task UpdateAngebotAsync(Angebot angebot)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tAngebot SET kKunde=@KundeId, dGueltigBis=@GueltigBis, nStatus=@Status,
                    cBemerkung=@Bemerkung, cIntNotiz=@InterneNotiz,
                    cLieferStrasse=@LieferStrasse, cLieferPLZ=@LieferPLZ, cLieferOrt=@LieferOrt, cLieferLand=@LieferLand,
                    dGeaendert=GETDATE()
                WHERE kAngebot=@Id", angebot);

            await BerechneAngebotSummenAsync(angebot.Id);
        }

        private async Task SavePositionAsync(AngebotPosition pos)
        {
            var conn = await _db.GetConnectionAsync();
            if (pos.Id == 0)
            {
                pos.Id = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tAngebotPos (kAngebot, nPos, kArtikel, cArtNr, cName, cBeschreibung, 
                        fMenge, cEinheit, fEinzelpreis, fRabatt, fMwStSatz, fGesamt)
                    VALUES (@AngebotId, @PosNr, @ArtikelId, @ArtNr, @Name, @Beschreibung,
                        @Menge, @Einheit, @Einzelpreis, @Rabatt, @MwStSatz, @Gesamt);
                    SELECT SCOPE_IDENTITY();", pos);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE tAngebotPos SET cArtNr=@ArtNr, cName=@Name, cBeschreibung=@Beschreibung,
                        fMenge=@Menge, fEinzelpreis=@Einzelpreis, fRabatt=@Rabatt, fMwStSatz=@MwStSatz, fGesamt=@Gesamt
                    WHERE kAngebotPos=@Id", pos);
            }
        }

        private async Task BerechneAngebotSummenAsync(int angebotId)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tAngebot SET 
                    fNetto = (SELECT ISNULL(SUM(fGesamt), 0) FROM tAngebotPos WHERE kAngebot = @Id),
                    fMwSt = (SELECT ISNULL(SUM(fGesamt * fMwStSatz / 100), 0) FROM tAngebotPos WHERE kAngebot = @Id),
                    fBrutto = (SELECT ISNULL(SUM(fGesamt * (1 + fMwStSatz / 100)), 0) FROM tAngebotPos WHERE kAngebot = @Id)
                WHERE kAngebot = @Id", new { Id = angebotId });
        }
        #endregion

        #region Angebot zu Auftrag
        /// <summary>
        /// Wandelt ein Angebot in einen Auftrag um
        /// </summary>
        public async Task<int> AngebotZuAuftragAsync(int angebotId, bool angebotAbschliessen = true)
        {
            var conn = await _db.GetConnectionAsync();
            var angebot = await GetAngebotAsync(angebotId);
            
            if (angebot == null)
                throw new Exception("Angebot nicht gefunden");

            if (angebot.Status == AngebotStatus.Abgeschlossen)
                throw new Exception("Angebot wurde bereits in Auftrag umgewandelt");

            // Nächste Bestellnummer
            var letzteNr = await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT TOP 1 cBestellNr FROM tBestellung ORDER BY kBestellung DESC");
            var nummer = 1;
            if (!string.IsNullOrEmpty(letzteNr) && int.TryParse(letzteNr.Split('-').Last(), out var n))
                nummer = n + 1;
            var bestellNr = $"AU-{DateTime.Now:yyyy}-{nummer:D5}";

            // Auftrag erstellen
            var auftragId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tBestellung (cBestellNr, kKunde, dErstellt, nStatus, cBemerkung,
                    cLieferStrasse, cLieferPLZ, cLieferOrt, cLieferLand,
                    fNetto, fMwSt, fBrutto, cWaehrung, kAngebot)
                VALUES (@Nr, @KundeId, GETDATE(), 1, @Bemerkung,
                    @LieferStrasse, @LieferPLZ, @LieferOrt, @LieferLand,
                    @Netto, @MwSt, @Brutto, @Waehrung, @AngebotId);
                SELECT SCOPE_IDENTITY();", new
            {
                Nr = bestellNr,
                angebot.KundeId,
                angebot.Bemerkung,
                angebot.LieferStrasse,
                angebot.LieferPLZ,
                angebot.LieferOrt,
                angebot.LieferLand,
                angebot.Netto,
                angebot.MwSt,
                angebot.Brutto,
                angebot.Waehrung,
                AngebotId = angebotId
            });

            // Positionen kopieren
            var pos = 1;
            foreach (var p in angebot.Positionen)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO tBestellungPos (kBestellung, nPos, kArtikel, cArtNr, cName, cBeschreibung,
                        fMenge, cEinheit, fPreis, fRabatt, fMwStSatz)
                    VALUES (@AuftragId, @Pos, @ArtikelId, @ArtNr, @Name, @Beschreibung,
                        @Menge, @Einheit, @Einzelpreis, @Rabatt, @MwStSatz)",
                    new
                    {
                        AuftragId = auftragId,
                        Pos = pos++,
                        p.ArtikelId,
                        p.ArtNr,
                        p.Name,
                        p.Beschreibung,
                        p.Menge,
                        p.Einheit,
                        p.Einzelpreis,
                        p.Rabatt,
                        p.MwStSatz
                    });
            }

            // Angebot abschließen
            if (angebotAbschliessen)
            {
                await conn.ExecuteAsync(@"
                    UPDATE tAngebot SET nStatus = @Status, kAuftrag = @AuftragId, dGeaendert = GETDATE()
                    WHERE kAngebot = @Id",
                    new { Status = (int)AngebotStatus.Abgeschlossen, AuftragId = auftragId, Id = angebotId });
            }

            _log.Information("Angebot {AngebotId} zu Auftrag {AuftragNr} umgewandelt", angebotId, bestellNr);
            return auftragId;
        }

        /// <summary>
        /// Kopiert ein Angebot
        /// </summary>
        public async Task<int> KopiereAngebotAsync(int angebotId)
        {
            var original = await GetAngebotAsync(angebotId);
            if (original == null) throw new Exception("Angebot nicht gefunden");

            original.Id = 0;
            original.AngebotNr = ""; // Wird neu generiert
            original.AngebotsDatum = DateTime.Now;
            original.GueltigBis = DateTime.Now.AddDays(30);
            original.Status = AngebotStatus.Offen;

            foreach (var p in original.Positionen)
            {
                p.Id = 0;
            }

            return await CreateAngebotAsync(original);
        }
        #endregion

        #region Status-Änderungen
        /// <summary>
        /// Status ändern
        /// </summary>
        public async Task SetStatusAsync(int angebotId, AngebotStatus status)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(
                "UPDATE tAngebot SET nStatus = @Status, dGeaendert = GETDATE() WHERE kAngebot = @Id",
                new { Status = (int)status, Id = angebotId });
        }

        /// <summary>
        /// Prüft und markiert abgelaufene Angebote
        /// </summary>
        public async Task<int> MarkiereAbgelaufeneAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.ExecuteAsync(@"
                UPDATE tAngebot SET nStatus = @Status 
                WHERE nStatus = @OffenStatus AND dGueltigBis < GETDATE()",
                new { Status = (int)AngebotStatus.Abgelaufen, OffenStatus = (int)AngebotStatus.Offen });
        }
        #endregion
    }

    #region DTOs
    public class Angebot
    {
        public int Id { get; set; }
        public string AngebotNr { get; set; } = "";
        public int KundeId { get; set; }
        public string? KundeFirma { get; set; }
        public string? KundeName { get; set; }
        public DateTime AngebotsDatum { get; set; } = DateTime.Now;
        public DateTime GueltigBis { get; set; } = DateTime.Now.AddDays(30);
        public AngebotStatus Status { get; set; } = AngebotStatus.Offen;
        public decimal Netto { get; set; }
        public decimal MwSt { get; set; }
        public decimal Brutto { get; set; }
        public string Waehrung { get; set; } = "EUR";
        public string? Bemerkung { get; set; }
        public string? InterneNotiz { get; set; }
        public string? LieferStrasse { get; set; }
        public string? LieferPLZ { get; set; }
        public string? LieferOrt { get; set; }
        public string? LieferLand { get; set; }
        public int? BearbeiterId { get; set; }
        public int? AuftragId { get; set; }
        public List<AngebotPosition> Positionen { get; set; } = new();
    }

    public class AngebotPosition
    {
        public int Id { get; set; }
        public int AngebotId { get; set; }
        public int PosNr { get; set; }
        public int? ArtikelId { get; set; }
        public string ArtNr { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Beschreibung { get; set; }
        public decimal Menge { get; set; } = 1;
        public string Einheit { get; set; } = "Stk";
        public decimal Einzelpreis { get; set; }
        public decimal Rabatt { get; set; }
        public decimal MwStSatz { get; set; } = 19;
        public decimal Gesamt => Math.Round(Menge * Einzelpreis * (1 - Rabatt / 100), 2);
    }

    public class AngebotFilter
    {
        public int? KundeId { get; set; }
        public AngebotStatus? Status { get; set; }
        public DateTime? VonDatum { get; set; }
        public DateTime? BisDatum { get; set; }
        public string? Suchtext { get; set; }
    }

    public enum AngebotStatus
    {
        Offen = 0,
        Versendet = 1,
        Nachfassen = 2,
        Abgeschlossen = 3, // In Auftrag umgewandelt
        Abgelehnt = 4,
        Abgelaufen = 5
    }
    #endregion
}
