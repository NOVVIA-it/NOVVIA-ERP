using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Data
{
    public class JtlDbContext : IDisposable
    {
        private readonly string _connectionString;
        private SqlConnection? _connection;
        private static readonly ILogger _log = Log.ForContext<JtlDbContext>();

        public JtlDbContext(string connectionString) => _connectionString = connectionString;
        public JtlDbContext() : this("Server=192.168.0.220;Database=Mandant_1;User Id=sa;Password=YourPassword;TrustServerCertificate=True;") { }

        public async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                _connection?.Dispose();
                _connection = new SqlConnection(_connectionString);
                await _connection.OpenAsync();
            }
            return _connection;
        }

        public void Dispose() { _connection?.Dispose(); _connection = null; }

        #region Nummernkreise
        public async Task<string> GetNaechsteNummerAsync(string typ)
        {
            var conn = await GetConnectionAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                var nk = await conn.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT * FROM tNummernkreis WITH (UPDLOCK) WHERE cName = @Name", new { Name = typ }, transaction);
                if (nk == null)
                {
                    await conn.ExecuteAsync("INSERT INTO tNummernkreis (cName, cPrefix, nAktuelleNr, nStellen) VALUES (@Name, @Prefix, 1, 6)",
                        new { Name = typ, Prefix = typ.Length >= 2 ? typ.Substring(0, 2).ToUpper() : typ.ToUpper() }, transaction);
                    transaction.Commit();
                    return $"{(typ.Length >= 2 ? typ.Substring(0, 2).ToUpper() : typ.ToUpper())}000001";
                }
                int neueNr = (int)nk.nAktuelleNr + 1;
                await conn.ExecuteAsync("UPDATE tNummernkreis SET nAktuelleNr = @Nr WHERE kNummernkreis = @Id",
                    new { Nr = neueNr, Id = (int)nk.kNummernkreis }, transaction);
                transaction.Commit();
                return $"{nk.cPrefix ?? ""}{neueNr.ToString().PadLeft((int)(nk.nStellen ?? 6), '0')}{nk.cSuffix ?? ""}";
            }
            catch { transaction.Rollback(); throw; }
        }
        #endregion

        #region Artikel
        public async Task<IEnumerable<Artikel>> GetArtikelAsync(string? such = null, int? katId = null, bool aktiv = true, int limit = 100, int offset = 0)
        {
            var conn = await GetConnectionAsync();
            var sql = @"SELECT a.*, ab.cName AS Name, ab.cBeschreibung FROM tArtikel a 
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 WHERE 1=1";
            if (aktiv) sql += " AND a.cAktiv = 'Y'";
            if (!string.IsNullOrEmpty(such)) sql += " AND (a.cArtNr LIKE @Such OR ab.cName LIKE @Such OR a.cBarcode = @SuchExakt)";
            if (katId.HasValue) sql += " AND EXISTS (SELECT 1 FROM tArtikelKategorie ak WHERE ak.kArtikel = a.kArtikel AND ak.kKategorie = @KatId)";
            sql += " ORDER BY ab.cName OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";
            return await conn.QueryAsync<Artikel>(sql, new { Such = $"%{such}%", SuchExakt = such, KatId = katId, Limit = limit, Offset = offset });
        }

        public async Task<Artikel?> GetArtikelByIdAsync(int id, bool details = true)
        {
            var conn = await GetConnectionAsync();
            var artikel = await conn.QuerySingleOrDefaultAsync<Artikel>("SELECT * FROM tArtikel WHERE kArtikel = @Id", new { Id = id });
            if (artikel == null) return null;
            if (details)
            {
                artikel.Beschreibungen = (await conn.QueryAsync<ArtikelBeschreibung>("SELECT * FROM tArtikelBeschreibung WHERE kArtikel = @Id", new { Id = id })).ToList();
                artikel.Beschreibung = artikel.Beschreibungen.FirstOrDefault(b => b.SpracheId == 1);
                artikel.Merkmale = (await conn.QueryAsync<ArtikelMerkmal>(@"SELECT am.*, m.cName AS MerkmalName, mw.cWert AS WertName FROM tArtikelMerkmal am 
                    INNER JOIN tMerkmal m ON am.kMerkmal = m.kMerkmal INNER JOIN tMerkmalWert mw ON am.kMerkmalWert = mw.kMerkmalWert WHERE am.kArtikel = @Id", new { Id = id })).ToList();
                artikel.Attribute = (await conn.QueryAsync<ArtikelAttribut>("SELECT * FROM tArtikelAttribut WHERE kArtikel = @Id", new { Id = id })).ToList();
                artikel.Preise = (await conn.QueryAsync<ArtikelPreis>("SELECT * FROM tArtikelPreis WHERE kArtikel = @Id", new { Id = id })).ToList();
                artikel.Staffelpreise = (await conn.QueryAsync<ArtikelStaffelpreis>("SELECT * FROM tArtikelStaffelpreis WHERE kArtikel = @Id ORDER BY fAbMenge", new { Id = id })).ToList();
                artikel.Bilder = (await conn.QueryAsync<ArtikelBild>("SELECT * FROM tArtikelBild WHERE kArtikel = @Id ORDER BY nNr", new { Id = id })).ToList();
                artikel.Kategorien = (await conn.QueryAsync<ArtikelKategorie>(@"SELECT ak.*, k.cName AS KategorieName FROM tArtikelKategorie ak 
                    INNER JOIN tKategorie k ON ak.kKategorie = k.kKategorie WHERE ak.kArtikel = @Id", new { Id = id })).ToList();
                if (artikel.IstStueckliste)
                    artikel.Stuecklistenkomponenten = (await conn.QueryAsync<Stueckliste>(@"SELECT s.*, a.cArtNr AS KomponenteArtNr, ab.cName AS KomponenteName FROM tStueckliste s 
                        INNER JOIN tArtikel a ON s.kArtikelKomponente = a.kArtikel LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 
                        WHERE s.kArtikel = @Id ORDER BY s.nSort", new { Id = id })).ToList();
                artikel.Lieferanten = (await conn.QueryAsync<ArtikelLieferant>(@"SELECT al.*, l.cFirma AS LieferantName FROM tArtikelLieferant al 
                    INNER JOIN tLieferant l ON al.kLieferant = l.kLieferant WHERE al.kArtikel = @Id ORDER BY al.nPrioritaet", new { Id = id })).ToList();
                artikel.Lagerbestaende = (await conn.QueryAsync<Lagerbestand>(@"SELECT lb.*, w.cName AS LagerName FROM tLagerbestand lb 
                    INNER JOIN tWarenlager w ON lb.kWarenlager = w.kWarenlager WHERE lb.kArtikel = @Id", new { Id = id })).ToList();
                artikel.WooCommerceLinks = (await conn.QueryAsync<ArtikelWooCommerce>("SELECT * FROM tArtikelWooCommerce WHERE kArtikel = @Id", new { Id = id })).ToList();
            }
            return artikel;
        }

        public async Task<Artikel?> GetArtikelByBarcodeAsync(string barcode)
        {
            var conn = await GetConnectionAsync();
            var id = await conn.QuerySingleOrDefaultAsync<int?>("SELECT kArtikel FROM tArtikel WHERE cBarcode = @B OR cArtNr = @B", new { B = barcode });
            return id.HasValue ? await GetArtikelByIdAsync(id.Value) : null;
        }

        public async Task<int> CreateArtikelAsync(Artikel a)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                if (string.IsNullOrEmpty(a.ArtNr)) a.ArtNr = await GetNaechsteNummerAsync("Artikel");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tArtikel (kHersteller, kSteuerklasse, cArtNr, cBarcode, cHAN, fLagerbestand, fMindestbestand, fVKNetto, fVKBrutto, fEKNetto, fGewicht, nIstVater, kVaterArtikel, nIstStueckliste, cLagerArtikel, cAktiv, nTopArtikel, dErstellt)
                    VALUES (@HerstellerId, @SteuerklasseId, @ArtNr, @Barcode, @HAN, @Lagerbestand, @Mindestbestand, @VKNetto, @VKBrutto, @EKNetto, @Gewicht, @IstVater, @VaterArtikelId, @IstStueckliste, @LagerArtikel, @Aktiv, @TopArtikel, GETDATE()); SELECT SCOPE_IDENTITY();", a, tx);
                if (a.Beschreibung != null)
                {
                    a.Beschreibung.ArtikelId = id;
                    await conn.ExecuteAsync(@"INSERT INTO tArtikelBeschreibung (kArtikel, kSprache, cName, cBeschreibung, cKurzBeschreibung, cSeo) 
                        VALUES (@ArtikelId, @SpracheId, @Name, @Beschreibung, @KurzBeschreibung, @SeoUrl)", a.Beschreibung, tx);
                }
                foreach (var s in a.Stuecklistenkomponenten)
                {
                    s.ArtikelId = id;
                    await conn.ExecuteAsync("INSERT INTO tStueckliste (kArtikel, kArtikelKomponente, fMenge) VALUES (@ArtikelId, @KomponenteArtikelId, @Menge)", s, tx);
                }
                foreach (var k in a.Kategorien)
                    await conn.ExecuteAsync("INSERT INTO tArtikelKategorie (kArtikel, kKategorie) VALUES (@ArtId, @KatId)", new { ArtId = id, KatId = k.KategorieId }, tx);
                tx.Commit();
                _log.Information("Artikel {ArtNr} erstellt (ID: {Id})", a.ArtNr, id);
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task UpdateArtikelAsync(Artikel a)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tArtikel SET kHersteller=@HerstellerId, cArtNr=@ArtNr, cBarcode=@Barcode, cHAN=@HAN, fMindestbestand=@Mindestbestand, 
                fVKNetto=@VKNetto, fVKBrutto=@VKBrutto, fEKNetto=@EKNetto, fGewicht=@Gewicht, nIstStueckliste=@IstStueckliste, cLagerArtikel=@LagerArtikel, 
                cAktiv=@Aktiv, nTopArtikel=@TopArtikel, dGeaendert=GETDATE() WHERE kArtikel=@Id", a);
            if (a.Beschreibung != null)
            {
                var exists = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tArtikelBeschreibung WHERE kArtikel=@Id AND kSprache=@S", new { Id = a.Id, S = a.Beschreibung.SpracheId });
                if (exists > 0)
                    await conn.ExecuteAsync("UPDATE tArtikelBeschreibung SET cName=@Name, cBeschreibung=@Beschreibung, cKurzBeschreibung=@KurzBeschreibung, cSeo=@SeoUrl WHERE kArtikel=@ArtikelId AND kSprache=@SpracheId", a.Beschreibung);
                else
                {
                    a.Beschreibung.ArtikelId = a.Id;
                    await conn.ExecuteAsync("INSERT INTO tArtikelBeschreibung (kArtikel, kSprache, cName, cBeschreibung, cKurzBeschreibung, cSeo) VALUES (@ArtikelId, @SpracheId, @Name, @Beschreibung, @KurzBeschreibung, @SeoUrl)", a.Beschreibung);
                }
            }
        }

        public async Task UpdateLagerbestandAsync(int artikelId, decimal menge, int? lagerId = null, string? grund = null)
        {
            var conn = await GetConnectionAsync();
            var alter = await conn.QuerySingleOrDefaultAsync<decimal>("SELECT fLagerbestand FROM tArtikel WHERE kArtikel=@Id", new { Id = artikelId });
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("UPDATE tArtikel SET fLagerbestand=@M, dGeaendert=GETDATE() WHERE kArtikel=@Id", new { M = menge, Id = artikelId }, tx);
                await conn.ExecuteAsync(@"INSERT INTO tWarenbewegung (kArtikel, kWarenlagerNach, nTyp, fMenge, dDatum, cGrund) 
                    VALUES (@ArtikelId, @LagerId, @Typ, @Diff, GETDATE(), @Grund)",
                    new { ArtikelId = artikelId, LagerId = lagerId ?? 1, Typ = menge > alter ? 1 : 2, Diff = Math.Abs(menge - alter), Grund = grund ?? "Korrektur" }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
        #endregion

        #region Kunde
        public async Task<IEnumerable<Kunde>> GetKundenAsync(string? such = null, bool aktiv = true, int limit = 100, int offset = 0)
        {
            var conn = await GetConnectionAsync();
            var sql = "SELECT * FROM tKunde WHERE 1=1";
            if (aktiv) sql += " AND cAktiv='Y'";
            if (!string.IsNullOrEmpty(such)) sql += " AND (cKundenNr LIKE @S OR cFirma LIKE @S OR cNachname LIKE @S OR cMail LIKE @S)";
            sql += " ORDER BY cNachname, cFirma OFFSET @O ROWS FETCH NEXT @L ROWS ONLY";
            return await conn.QueryAsync<Kunde>(sql, new { S = $"%{such}%", O = offset, L = limit });
        }

        public async Task<Kunde?> GetKundeByIdAsync(int id, bool details = true)
        {
            var conn = await GetConnectionAsync();
            var kunde = await conn.QuerySingleOrDefaultAsync<Kunde>("SELECT * FROM tKunde WHERE kKunde=@Id", new { Id = id });
            if (kunde != null && details)
            {
                kunde.Adressen = (await conn.QueryAsync<KundeAdresse>("SELECT * FROM tKundeAdresse WHERE kKunde=@Id", new { Id = id })).ToList();
                kunde.Bestellungen = (await conn.QueryAsync<Bestellung>("SELECT TOP 50 * FROM tBestellung WHERE kKunde=@Id ORDER BY dErstellt DESC", new { Id = id })).ToList();
            }
            return kunde;
        }

        public async Task<int> CreateKundeAsync(Kunde k)
        {
            var conn = await GetConnectionAsync();
            if (string.IsNullOrEmpty(k.KundenNr)) k.KundenNr = await GetNaechsteNummerAsync("Kunde");
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tKunde (kKundengruppe, cKundenNr, cAnrede, cVorname, cNachname, cFirma, cStrasse, cPLZ, cOrt, cLand, cTel, cMobil, cMail, cUSTID, cIBAN, cBIC, fKreditlimit, fRabatt, nZahlungsziel, fSkonto, cAktiv, dErstellt, cAnmerkung)
                VALUES (@KundengruppeId, @KundenNr, @Anrede, @Vorname, @Nachname, @Firma, @Strasse, @PLZ, @Ort, @Land, @Telefon, @Mobil, @Email, @UStID, @IBAN, @BIC, @Kreditlimit, @Rabatt, @Zahlungsziel, @Skonto, @Aktiv, GETDATE(), @Anmerkung); SELECT SCOPE_IDENTITY();", k);
        }

        public async Task UpdateKundeAsync(Kunde k)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tKunde SET kKundengruppe=@KundengruppeId, cAnrede=@Anrede, cVorname=@Vorname, cNachname=@Nachname, cFirma=@Firma, cStrasse=@Strasse, cPLZ=@PLZ, cOrt=@Ort, cLand=@Land, cTel=@Telefon, cMobil=@Mobil, cMail=@Email, cUSTID=@UStID, cIBAN=@IBAN, cBIC=@BIC, fKreditlimit=@Kreditlimit, fRabatt=@Rabatt, nZahlungsziel=@Zahlungsziel, fSkonto=@Skonto, cAktiv=@Aktiv, cAnmerkung=@Anmerkung WHERE kKunde=@Id", k);
        }

        public async Task<bool> KundenZusammenfuehrenAsync(int behalten, int loeschen)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("UPDATE tBestellung SET kKunde=@B WHERE kKunde=@L", new { B = behalten, L = loeschen }, tx);
                await conn.ExecuteAsync("UPDATE tRechnung SET kKunde=@B WHERE kKunde=@L", new { B = behalten, L = loeschen }, tx);
                await conn.ExecuteAsync("UPDATE tRMA SET kKunde=@B WHERE kKunde=@L", new { B = behalten, L = loeschen }, tx);
                await conn.ExecuteAsync("DELETE FROM tKunde WHERE kKunde=@L", new { L = loeschen }, tx);
                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); return false; }
        }
        #endregion

        #region Bestellung
        public async Task<IEnumerable<Bestellung>> GetBestellungenAsync(BestellStatus? status = null, DateTime? von = null, DateTime? bis = null, int? kundeId = null, int limit = 100, int offset = 0)
        {
            var conn = await GetConnectionAsync();
            var sql = "SELECT b.*, k.cFirma, k.cNachname, k.cKundenNr FROM tBestellung b LEFT JOIN tKunde k ON b.kKunde=k.kKunde WHERE 1=1";
            if (status.HasValue) sql += " AND b.nStatus=@Status";
            if (von.HasValue) sql += " AND b.dErstellt>=@Von";
            if (bis.HasValue) sql += " AND b.dErstellt<=@Bis";
            if (kundeId.HasValue) sql += " AND b.kKunde=@KundeId";
            sql += " ORDER BY b.dErstellt DESC OFFSET @O ROWS FETCH NEXT @L ROWS ONLY";
            return await conn.QueryAsync<Bestellung>(sql, new { Status = (int?)status, Von = von, Bis = bis, KundeId = kundeId, O = offset, L = limit });
        }

        public async Task<Bestellung?> GetBestellungByIdAsync(int id, bool details = true)
        {
            var conn = await GetConnectionAsync();
            var b = await conn.QuerySingleOrDefaultAsync<Bestellung>("SELECT * FROM tBestellung WHERE kBestellung=@Id", new { Id = id });
            if (b != null && details)
            {
                b.Kunde = await conn.QuerySingleOrDefaultAsync<Kunde>("SELECT * FROM tKunde WHERE kKunde=@Id", new { Id = b.KundeId });
                b.Positionen = (await conn.QueryAsync<BestellPosition>("SELECT * FROM tBestellPos WHERE kBestellung=@Id ORDER BY kBestellPos", new { Id = id })).ToList();
                b.Lieferadresse = await conn.QuerySingleOrDefaultAsync<BestellAdresse>("SELECT * FROM tBestellAdresse WHERE kBestellung=@Id AND nTyp=2", new { Id = id });
                b.Rechnungsadresse = await conn.QuerySingleOrDefaultAsync<BestellAdresse>("SELECT * FROM tBestellAdresse WHERE kBestellung=@Id AND nTyp=1", new { Id = id });
                b.Rechnungen = (await conn.QueryAsync<Rechnung>("SELECT * FROM tRechnung WHERE kBestellung=@Id", new { Id = id })).ToList();
                b.Lieferscheine = (await conn.QueryAsync<Lieferschein>("SELECT * FROM tLieferschein WHERE kBestellung=@Id", new { Id = id })).ToList();
            }
            return b;
        }

        public async Task<int> CreateBestellungAsync(Bestellung b)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                if (string.IsNullOrEmpty(b.BestellNr)) b.BestellNr = await GetNaechsteNummerAsync("Bestellung");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tBestellung (kKunde, kWarenlager, kZahlungsart, kVersandart, cBestellNr, cExterneAuftragsnummer, cPlatform, dErstellt, nStatus, fGesamtNetto, fGesamtBrutto, fVersandkosten, fRabatt, cWaehrung, cKommentar, cInternerKommentar)
                    VALUES (@KundeId, @WarenlagerId, @ZahlungsartId, @VersandartId, @BestellNr, @ExterneAuftragsnummer, @Platform, GETDATE(), @Status, @GesamtNetto, @GesamtBrutto, @Versandkosten, @Rabatt, @Waehrung, @Kommentar, @InternerKommentar); SELECT SCOPE_IDENTITY();", b, tx);
                foreach (var p in b.Positionen)
                {
                    p.BestellungId = id;
                    await conn.ExecuteAsync(@"INSERT INTO tBestellPos (kBestellung, kArtikel, nPosTyp, cArtNr, cName, fMenge, fGeliefert, fVKNetto, fVKBrutto, fMwSt, fRabatt)
                        VALUES (@BestellungId, @ArtikelId, @PosTyp, @ArtNr, @Name, @Menge, @Geliefert, @VKNetto, @VKBrutto, @MwSt, @Rabatt)", p, tx);
                }
                if (b.Lieferadresse != null) { b.Lieferadresse.BestellungId = id; b.Lieferadresse.Typ = (int)AdressTyp.Lieferadresse; await CreateBestellAdresseAsync(conn, tx, b.Lieferadresse); }
                if (b.Rechnungsadresse != null) { b.Rechnungsadresse.BestellungId = id; b.Rechnungsadresse.Typ = (int)AdressTyp.Rechnungsadresse; await CreateBestellAdresseAsync(conn, tx, b.Rechnungsadresse); }
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        private async Task CreateBestellAdresseAsync(SqlConnection conn, SqlTransaction tx, BestellAdresse a)
        {
            await conn.ExecuteAsync(@"INSERT INTO tBestellAdresse (kBestellung, nTyp, cVorname, cNachname, cFirma, cStrasse, cPLZ, cOrt, cLand, cTel, cMail)
                VALUES (@BestellungId, @Typ, @Vorname, @Nachname, @Firma, @Strasse, @PLZ, @Ort, @Land, @Telefon, @Email)", a, tx);
        }

        public async Task UpdateBestellStatusAsync(int id, BestellStatus status)
        {
            var conn = await GetConnectionAsync();
            var zusatz = status == BestellStatus.Bezahlt ? ", dBezahlt=GETDATE()" : status == BestellStatus.Versendet ? ", dVersandt=GETDATE()" : "";
            await conn.ExecuteAsync($"UPDATE tBestellung SET nStatus=@S{zusatz} WHERE kBestellung=@Id", new { S = (int)status, Id = id });
        }
        #endregion

        #region Rechnung
        public async Task<int> CreateRechnungAsync(int bestellungId)
        {
            var conn = await GetConnectionAsync();
            var b = await GetBestellungByIdAsync(bestellungId);
            if (b == null) throw new Exception($"Bestellung {bestellungId} nicht gefunden");
            using var tx = conn.BeginTransaction();
            try
            {
                var nr = await GetNaechsteNummerAsync("Rechnung");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tRechnung (kBestellung, kKunde, cRechnungsNr, dErstellt, dFaellig, nTyp, nStatus, fNetto, fBrutto, fOffen, cWaehrung)
                    VALUES (@BestellungId, @KundeId, @Nr, GETDATE(), DATEADD(DAY, @Ziel, GETDATE()), 1, 1, @Netto, @Brutto, @Brutto, @Waehrung); SELECT SCOPE_IDENTITY();",
                    new { BestellungId = bestellungId, KundeId = b.KundeId, Nr = nr, Ziel = b.Kunde?.Zahlungsziel ?? 14, Netto = b.GesamtNetto, Brutto = b.GesamtBrutto, Waehrung = b.Waehrung }, tx);
                foreach (var p in b.Positionen)
                    await conn.ExecuteAsync(@"INSERT INTO tRechnungsPos (kRechnung, kArtikel, cArtNr, cName, fMenge, fVKNetto, fVKBrutto, fMwSt) VALUES (@RId, @ArtikelId, @ArtNr, @Name, @Menge, @VKNetto, @VKBrutto, @MwSt)",
                        new { RId = id, p.ArtikelId, p.ArtNr, p.Name, p.Menge, p.VKNetto, p.VKBrutto, p.MwSt }, tx);
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<Rechnung?> GetRechnungByIdAsync(int id)
        {
            var conn = await GetConnectionAsync();
            var r = await conn.QuerySingleOrDefaultAsync<Rechnung>("SELECT * FROM tRechnung WHERE kRechnung=@Id", new { Id = id });
            if (r != null)
            {
                r.Positionen = (await conn.QueryAsync<RechnungsPosition>("SELECT * FROM tRechnungsPos WHERE kRechnung=@Id", new { Id = id })).ToList();
                r.Zahlungen = (await conn.QueryAsync<Zahlungseingang>("SELECT * FROM tZahlungseingang WHERE kRechnung=@Id ORDER BY dDatum", new { Id = id })).ToList();
            }
            return r;
        }

        public async Task BucheZahlungseingangAsync(int rechnungId, decimal betrag, string? referenz = null)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(@"INSERT INTO tZahlungseingang (kRechnung, kKunde, fBetrag, dDatum, cZahlungsreferenz) 
                    SELECT @RId, kKunde, @Betrag, GETDATE(), @Ref FROM tRechnung WHERE kRechnung=@RId", new { RId = rechnungId, Betrag = betrag, Ref = referenz }, tx);
                var r = await conn.QuerySingleAsync<dynamic>("SELECT fBrutto, fBezahlt FROM tRechnung WHERE kRechnung=@Id", new { Id = rechnungId }, tx);
                decimal neuerBezahlt = (decimal)r.fBezahlt + betrag;
                decimal neuerOffen = (decimal)r.fBrutto - neuerBezahlt;
                int neuerStatus = neuerOffen <= 0 ? 3 : 2;
                await conn.ExecuteAsync(@"UPDATE tRechnung SET fBezahlt=@B, fOffen=@O, nStatus=@S, dBezahlt=CASE WHEN @S=3 THEN GETDATE() ELSE dBezahlt END WHERE kRechnung=@Id",
                    new { B = neuerBezahlt, O = Math.Max(0, neuerOffen), S = neuerStatus, Id = rechnungId }, tx);
                if (neuerStatus == 3)
                    await conn.ExecuteAsync("UPDATE tBestellung SET nStatus=3, dBezahlt=GETDATE() WHERE kBestellung=(SELECT kBestellung FROM tRechnung WHERE kRechnung=@Id) AND nStatus<3", new { Id = rechnungId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<IEnumerable<Rechnung>> GetOffeneRechnungenAsync() =>
            await (await GetConnectionAsync()).QueryAsync<Rechnung>("SELECT * FROM tRechnung WHERE nStatus IN (1,2) AND fOffen > 0 ORDER BY dFaellig");

        public async Task<IEnumerable<Rechnung>> GetFaelligeRechnungenAsync(int? mahnstufe = null)
        {
            var conn = await GetConnectionAsync();
            var sql = "SELECT r.*, k.cFirma, k.cNachname FROM tRechnung r INNER JOIN tKunde k ON r.kKunde=k.kKunde WHERE r.nStatus IN (1,2,5) AND r.fOffen>0 AND r.dFaellig<GETDATE()";
            if (mahnstufe.HasValue) sql += " AND r.nMahnstufe=@S";
            return await conn.QueryAsync<Rechnung>(sql + " ORDER BY r.dFaellig", new { S = mahnstufe });
        }
        #endregion

        #region Lieferschein & Versand
        public async Task<int> CreateLieferscheinAsync(int bestellungId, List<int>? posIds = null)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                var nr = await GetNaechsteNummerAsync("Lieferschein");
                var id = await conn.QuerySingleAsync<int>("INSERT INTO tLieferschein (kBestellung, cLieferscheinNr, dErstellt) VALUES (@BId, @Nr, GETDATE()); SELECT SCOPE_IDENTITY();", new { BId = bestellungId, Nr = nr }, tx);
                var sql = "SELECT * FROM tBestellPos WHERE kBestellung=@BId AND fMenge > fGeliefert";
                if (posIds?.Any() == true) sql += " AND kBestellPos IN @Ids";
                var positionen = await conn.QueryAsync<BestellPosition>(sql, new { BId = bestellungId, Ids = posIds }, tx);
                foreach (var p in positionen)
                {
                    var menge = p.Menge - p.Geliefert;
                    await conn.ExecuteAsync("INSERT INTO tLieferscheinPos (kLieferschein, kArtikel, cArtNr, cName, fMenge) VALUES (@LsId, @ArtikelId, @ArtNr, @Name, @Menge)", new { LsId = id, p.ArtikelId, p.ArtNr, p.Name, Menge = menge }, tx);
                    await conn.ExecuteAsync("UPDATE tBestellPos SET fGeliefert=fGeliefert+@M WHERE kBestellPos=@Id", new { M = menge, Id = p.Id }, tx);
                    if (p.ArtikelId.HasValue)
                        await conn.ExecuteAsync("UPDATE tArtikel SET fLagerbestand=fLagerbestand-@M WHERE kArtikel=@Id", new { M = menge, Id = p.ArtikelId }, tx);
                }
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task SetTrackingAsync(int lieferscheinId, string tracking, string dienstleister)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE tLieferschein SET cTrackingID=@T, cVersandDienstleister=@D, dVersandt=GETDATE() WHERE kLieferschein=@Id", new { T = tracking, D = dienstleister, Id = lieferscheinId });
            await conn.ExecuteAsync("UPDATE tBestellung SET cTrackingID=@T, cVersandDienstleister=@D, dVersandt=GETDATE(), nStatus=5 WHERE kBestellung=(SELECT kBestellung FROM tLieferschein WHERE kLieferschein=@Id)", new { T = tracking, D = dienstleister, Id = lieferscheinId });
        }
        #endregion

        #region Lager
        public async Task<IEnumerable<Warenlager>> GetWarenlagerAsync() =>
            await (await GetConnectionAsync()).QueryAsync<Warenlager>("SELECT * FROM tWarenlager WHERE cAktiv='Y' ORDER BY nStandard DESC, cName");

        public async Task<IEnumerable<Lagerbestand>> GetLagerbestaendeAsync(int? artikelId = null, int? lagerId = null)
        {
            var conn = await GetConnectionAsync();
            var sql = @"SELECT lb.*, a.cArtNr, ab.cName AS ArtikelName, w.cName AS LagerName FROM tLagerbestand lb 
                INNER JOIN tArtikel a ON lb.kArtikel=a.kArtikel LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel=ab.kArtikel AND ab.kSprache=1 
                INNER JOIN tWarenlager w ON lb.kWarenlager=w.kWarenlager WHERE 1=1";
            if (artikelId.HasValue) sql += " AND lb.kArtikel=@AId";
            if (lagerId.HasValue) sql += " AND lb.kWarenlager=@LId";
            return await conn.QueryAsync<Lagerbestand>(sql + " ORDER BY ab.cName", new { AId = artikelId, LId = lagerId });
        }

        public async Task UmlagernAsync(int artikelId, int vonLagerId, int nachLagerId, decimal menge, string? grund = null)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("UPDATE tLagerbestand SET fBestand=fBestand-@M WHERE kArtikel=@A AND kWarenlager=@L", new { M = menge, A = artikelId, L = vonLagerId }, tx);
                var exists = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tLagerbestand WHERE kArtikel=@A AND kWarenlager=@L", new { A = artikelId, L = nachLagerId }, tx);
                if (exists > 0)
                    await conn.ExecuteAsync("UPDATE tLagerbestand SET fBestand=fBestand+@M WHERE kArtikel=@A AND kWarenlager=@L", new { M = menge, A = artikelId, L = nachLagerId }, tx);
                else
                    await conn.ExecuteAsync("INSERT INTO tLagerbestand (kArtikel, kWarenlager, fBestand, fReserviert) VALUES (@A, @L, @M, 0)", new { A = artikelId, L = nachLagerId, M = menge }, tx);
                await conn.ExecuteAsync("INSERT INTO tWarenbewegung (kArtikel, kWarenlagerVon, kWarenlagerNach, nTyp, fMenge, dDatum, cGrund) VALUES (@A, @V, @N, 3, @M, GETDATE(), @G)",
                    new { A = artikelId, V = vonLagerId, N = nachLagerId, M = menge, G = grund ?? "Umlagerung" }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task InventurBuchenAsync(int artikelId, int lagerId, decimal gezaehlt, string? benutzer = null)
        {
            var conn = await GetConnectionAsync();
            var aktuell = await conn.QuerySingleOrDefaultAsync<decimal>("SELECT ISNULL(fBestand,0) FROM tLagerbestand WHERE kArtikel=@A AND kWarenlager=@L", new { A = artikelId, L = lagerId });
            using var tx = conn.BeginTransaction();
            try
            {
                var exists = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tLagerbestand WHERE kArtikel=@A AND kWarenlager=@L", new { A = artikelId, L = lagerId }, tx);
                if (exists > 0)
                    await conn.ExecuteAsync("UPDATE tLagerbestand SET fBestand=@M WHERE kArtikel=@A AND kWarenlager=@L", new { M = gezaehlt, A = artikelId, L = lagerId }, tx);
                else
                    await conn.ExecuteAsync("INSERT INTO tLagerbestand (kArtikel, kWarenlager, fBestand, fReserviert) VALUES (@A, @L, @M, 0)", new { A = artikelId, L = lagerId, M = gezaehlt }, tx);
                await conn.ExecuteAsync("UPDATE tArtikel SET fLagerbestand=(SELECT ISNULL(SUM(fBestand),0) FROM tLagerbestand WHERE kArtikel=@A) WHERE kArtikel=@A", new { A = artikelId }, tx);
                await conn.ExecuteAsync("INSERT INTO tWarenbewegung (kArtikel, kWarenlagerNach, nTyp, fMenge, dDatum, cGrund, cBenutzer) VALUES (@A, @L, 4, @D, GETDATE(), 'Inventur', @B)",
                    new { A = artikelId, L = lagerId, D = Math.Abs(gezaehlt - aktuell), B = benutzer }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
        #endregion

        #region Einkauf
        public async Task<IEnumerable<Lieferant>> GetLieferantenAsync(string? such = null, bool aktiv = true)
        {
            var conn = await GetConnectionAsync();
            var sql = "SELECT * FROM tLieferant WHERE 1=1";
            if (aktiv) sql += " AND cAktiv='Y'";
            if (!string.IsNullOrEmpty(such)) sql += " AND (cFirma LIKE @S OR cLieferantenNr LIKE @S)";
            return await conn.QueryAsync<Lieferant>(sql + " ORDER BY cFirma", new { S = $"%{such}%" });
        }

        public async Task<int> CreateEinkaufsBestellungAsync(EinkaufsBestellung b)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                if (string.IsNullOrEmpty(b.BestellNr)) b.BestellNr = await GetNaechsteNummerAsync("Einkauf");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tEinkaufsbestellung (kLieferant, kWarenlager, cBestellNr, dErstellt, nStatus, fSummeNetto, fSummeBrutto, cWaehrung, cAnmerkung)
                    VALUES (@LieferantId, @WarenlagerId, @BestellNr, GETDATE(), @Status, @SummeNetto, @SummeBrutto, @Waehrung, @Anmerkung); SELECT SCOPE_IDENTITY();", b, tx);
                foreach (var p in b.Positionen)
                {
                    p.EinkaufsbestellungId = id;
                    await conn.ExecuteAsync("INSERT INTO tEinkaufsbestellungPos (kEinkaufsbestellung, kArtikel, cArtNr, cArtNrLieferant, cName, fMenge, fGeliefert, fEKNetto, fMwSt) VALUES (@EinkaufsbestellungId, @ArtikelId, @ArtNr, @ArtNrLieferant, @Name, @Menge, @Geliefert, @EKNetto, @MwSt)", p, tx);
                }
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<int> CreateWareneingangAsync(Wareneingang w)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                if (string.IsNullOrEmpty(w.WareneingangNr)) w.WareneingangNr = await GetNaechsteNummerAsync("Wareneingang");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tWareneingang (kEinkaufsbestellung, kLieferant, kWarenlager, cWareneingangNr, cLieferscheinNr, dDatum, cAnmerkung, cBenutzer)
                    VALUES (@EinkaufsbestellungId, @LieferantId, @WarenlagerId, @WareneingangNr, @LieferscheinNrLieferant, GETDATE(), @Anmerkung, @Benutzer); SELECT SCOPE_IDENTITY();", w, tx);
                foreach (var p in w.Positionen)
                {
                    p.WareneingangId = id;
                    await conn.ExecuteAsync("INSERT INTO tWareneingangPos (kWareneingang, kArtikel, fMenge, cChargenNr, dMHD, cSerial) VALUES (@WareneingangId, @ArtikelId, @Menge, @ChargenNr, @MHD, @Seriennummer)", p, tx);
                    await conn.ExecuteAsync("UPDATE tArtikel SET fLagerbestand=fLagerbestand+@M, dLetzterZugang=GETDATE() WHERE kArtikel=@A", new { M = p.Menge, A = p.ArtikelId }, tx);
                    await conn.ExecuteAsync("INSERT INTO tWarenbewegung (kArtikel, kWarenlagerNach, nTyp, fMenge, dDatum, cGrund, cBenutzer) VALUES (@A, @L, 1, @M, GETDATE(), 'Wareneingang', @B)",
                        new { A = p.ArtikelId, L = w.WarenlagerId, M = p.Menge, B = w.Benutzer }, tx);
                }
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }
        #endregion

        #region RMA
        public async Task<int> CreateRMAAsync(RMA r)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                if (string.IsNullOrEmpty(r.RMANr)) r.RMANr = await GetNaechsteNummerAsync("RMA");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tRMA (kBestellung, kKunde, cRMANr, dErstellt, nStatus, nTyp, cGrund, cAnmerkungKunde, cAnmerkungIntern)
                    VALUES (@BestellungId, @KundeId, @RMANr, GETDATE(), @Status, @Typ, @Grund, @AnmerkungKunde, @AnmerkungIntern); SELECT SCOPE_IDENTITY();", r, tx);
                foreach (var p in r.Positionen)
                {
                    p.RMAId = id;
                    await conn.ExecuteAsync("INSERT INTO tRMAPos (kRMA, kArtikel, cArtNr, cName, fMenge, nZustand) VALUES (@RMAId, @ArtikelId, @ArtNr, @Name, @Menge, @Zustand)", p, tx);
                }
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task RMAWareneingangAsync(int rmaId, List<(int PosId, decimal Menge, ArtikelZustand Zustand)> eingaenge)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var (posId, menge, zustand) in eingaenge)
                {
                    await conn.ExecuteAsync("UPDATE tRMAPos SET fMengeEingegangen=fMengeEingegangen+@M, nZustand=@Z WHERE kRMAPos=@Id", new { M = menge, Z = (int)zustand, Id = posId }, tx);
                    if (zustand == ArtikelZustand.Neuwertig)
                    {
                        var artId = await conn.QuerySingleOrDefaultAsync<int?>("SELECT kArtikel FROM tRMAPos WHERE kRMAPos=@Id", new { Id = posId }, tx);
                        if (artId.HasValue)
                            await conn.ExecuteAsync("UPDATE tArtikel SET fLagerbestand=fLagerbestand+@M WHERE kArtikel=@Id", new { M = menge, Id = artId }, tx);
                    }
                }
                await conn.ExecuteAsync("UPDATE tRMA SET nStatus=2, dEingegangen=GETDATE() WHERE kRMA=@Id", new { Id = rmaId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
        #endregion

        #region Mahnwesen
        public async Task<int> CreateMahnungAsync(int kundeId, List<int> rechnungIds)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                var maxStufe = await conn.QuerySingleAsync<int>("SELECT ISNULL(MAX(nMahnstufe),0)+1 FROM tRechnung WHERE kRechnung IN @Ids", new { Ids = rechnungIds }, tx);
                var stufe = await conn.QuerySingleOrDefaultAsync<Mahnstufe>("SELECT * FROM tMahnstufe WHERE nStufe=@S", new { S = maxStufe }, tx) ?? new Mahnstufe { Stufe = maxStufe, Gebuehr = 5 };
                var summe = await conn.QuerySingleAsync<decimal>("SELECT SUM(fOffen) FROM tRechnung WHERE kRechnung IN @Ids", new { Ids = rechnungIds }, tx);
                var nr = await GetNaechsteNummerAsync("Mahnung");
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tMahnung (kKunde, cMahnungNr, nMahnstufe, dErstellt, dFaellig, fGebuehr, fZinsen, fSummeOffen, nStatus)
                    VALUES (@K, @Nr, @S, GETDATE(), DATEADD(DAY, 14, GETDATE()), @G, @Z, @O, 1); SELECT SCOPE_IDENTITY();",
                    new { K = kundeId, Nr = nr, S = maxStufe, G = stufe.Gebuehr, Z = summe * stufe.Zinssatz / 100, O = summe }, tx);
                foreach (var rid in rechnungIds)
                    await conn.ExecuteAsync("UPDATE tRechnung SET nMahnstufe=@S, nStatus=5 WHERE kRechnung=@Id", new { S = maxStufe, Id = rid }, tx);
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }
        #endregion

        #region Dashboard
        public async Task<DashboardStats> GetDashboardStatsAsync()
        {
            var conn = await GetConnectionAsync();
            var stats = new DashboardStats
            {
                BestellungenHeute = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tBestellung WHERE CAST(dErstellt AS DATE)=CAST(GETDATE() AS DATE)"),
                UmsatzHeute = await conn.QuerySingleOrDefaultAsync<decimal>("SELECT ISNULL(SUM(fGesamtBrutto),0) FROM tBestellung WHERE CAST(dErstellt AS DATE)=CAST(GETDATE() AS DATE)"),
                OffeneBestellungen = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tBestellung WHERE nStatus IN (1,2,3)"),
                ZuVersenden = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tBestellung WHERE nStatus=3 AND dVersandt IS NULL"),
                OffeneRechnungen = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tRechnung WHERE nStatus IN (1,2)"),
                OffenerBetrag = await conn.QuerySingleOrDefaultAsync<decimal>("SELECT ISNULL(SUM(fOffen),0) FROM tRechnung WHERE nStatus IN (1,2)"),
                ArtikelUnterMindestbestand = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tArtikel WHERE cAktiv='Y' AND fLagerbestand<fMindestbestand"),
                OffeneRMAs = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tRMA WHERE nStatus IN (1,2,3)")
            };
            return stats;
        }
        #endregion
    }
}
