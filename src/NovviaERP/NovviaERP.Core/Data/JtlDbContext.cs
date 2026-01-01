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

        public string ConnectionString => _connectionString;

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
                artikel.Lieferanten = (await conn.QueryAsync<ArtikelLieferant>(@"SELECT la.*, la.tArtikel_kArtikel AS kArtikel, la.tLieferant_kLieferant AS kLieferant, l.cFirma AS LieferantName FROM tLiefArtikel la
                    INNER JOIN tLieferant l ON la.tLieferant_kLieferant = l.kLieferant WHERE la.tArtikel_kArtikel = @Id ORDER BY la.nStandard DESC", new { Id = id })).ToList();
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

            // DataTable für TYPE_spkundeInsert erstellen
            var dt = new DataTable();
            dt.Columns.Add("kInternalId", typeof(int));
            dt.Columns.Add("kInetKunde", typeof(int));
            dt.Columns.Add("kKundenKategorie", typeof(int));
            dt.Columns.Add("cKundenNr", typeof(string));
            dt.Columns.Add("cFirma", typeof(string));
            dt.Columns.Add("cAnrede", typeof(string));
            dt.Columns.Add("cTitel", typeof(string));
            dt.Columns.Add("cVorname", typeof(string));
            dt.Columns.Add("cName", typeof(string));  // = Nachname
            dt.Columns.Add("cStrasse", typeof(string));
            dt.Columns.Add("cPLZ", typeof(string));
            dt.Columns.Add("cOrt", typeof(string));
            dt.Columns.Add("cLand", typeof(string));
            dt.Columns.Add("cTel", typeof(string));
            dt.Columns.Add("cFax", typeof(string));
            dt.Columns.Add("cEMail", typeof(string));
            dt.Columns.Add("dErstellt", typeof(DateTime));
            dt.Columns.Add("cMobil", typeof(string));
            dt.Columns.Add("fRabatt", typeof(decimal));
            dt.Columns.Add("cUSTID", typeof(string));
            dt.Columns.Add("cNewsletter", typeof(string));
            dt.Columns.Add("cZusatz", typeof(string));
            dt.Columns.Add("cEbayName", typeof(string));
            dt.Columns.Add("kBuyer", typeof(int));
            dt.Columns.Add("cAdressZusatz", typeof(string));
            dt.Columns.Add("cGeburtstag", typeof(string));
            dt.Columns.Add("cWWW", typeof(string));
            dt.Columns.Add("cSperre", typeof(string));
            dt.Columns.Add("cPostID", typeof(string));
            dt.Columns.Add("kKundenGruppe", typeof(int));
            dt.Columns.Add("nZahlungsziel", typeof(int));
            dt.Columns.Add("kSprache", typeof(int));
            dt.Columns.Add("cISO", typeof(string));
            dt.Columns.Add("cBundesland", typeof(string));
            dt.Columns.Add("cHerkunft", typeof(string));
            dt.Columns.Add("cKassenKunde", typeof(string));
            dt.Columns.Add("cHRNr", typeof(string));
            dt.Columns.Add("kZahlungsart", typeof(int));
            dt.Columns.Add("nDebitorennr", typeof(int));
            dt.Columns.Add("cSteuerNr", typeof(string));
            dt.Columns.Add("nKreditlimit", typeof(int));
            dt.Columns.Add("kKundenDrucktext", typeof(int));
            dt.Columns.Add("nMahnstopp", typeof(byte));
            dt.Columns.Add("nMahnrhythmus", typeof(int));
            dt.Columns.Add("kFirma", typeof(byte));
            dt.Columns.Add("fProvision", typeof(decimal));
            dt.Columns.Add("nVertreter", typeof(byte));
            dt.Columns.Add("fSkonto", typeof(decimal));
            dt.Columns.Add("nSkontoInTagen", typeof(int));
            dt.Columns.Add("dGeaendert", typeof(DateTime));

            var row = dt.NewRow();
            row["kInternalId"] = 1;
            row["cKundenNr"] = k.KundenNr ?? "";
            row["cFirma"] = k.Firma ?? "";
            row["cAnrede"] = k.Anrede ?? "";
            row["cVorname"] = k.Vorname ?? "";
            row["cName"] = k.Nachname ?? "";
            row["cStrasse"] = k.Strasse ?? "";
            row["cPLZ"] = k.PLZ ?? "";
            row["cOrt"] = k.Ort ?? "";
            row["cLand"] = k.Land ?? "DE";
            row["cTel"] = k.Telefon ?? "";
            row["cMobil"] = k.Mobil ?? "";
            row["cEMail"] = k.Email ?? "";
            row["dErstellt"] = DateTime.Now;
            row["fRabatt"] = k.Rabatt;
            row["cUSTID"] = k.UStId ?? "";
            row["kKundenGruppe"] = k.KundengruppeId > 0 ? k.KundengruppeId : 1;
            row["nZahlungsziel"] = k.Zahlungsziel ?? 14;
            row["kSprache"] = 1;
            row["cISO"] = "DE";
            row["nKreditlimit"] = 0;
            row["fSkonto"] = 0m;
            row["kFirma"] = (byte)1;
            row["cSperre"] = k.Aktiv == "N" ? "Y" : "N";
            row["dGeaendert"] = DateTime.Now;
            dt.Rows.Add(row);

            var p = new DynamicParameters();
            p.Add("@Daten", dt.AsTableValuedParameter("dbo.TYPE_spkundeInsert"));

            // SP aufrufen und neue kKunde ermitteln
            await conn.ExecuteAsync("Kunde.spKundeInsert", p, commandType: CommandType.StoredProcedure);

            // Neu erstellte kKunde abfragen
            var newId = await conn.QuerySingleAsync<int>(
                "SELECT TOP 1 kKunde FROM dbo.tKunde WHERE cKundenNr = @Nr ORDER BY kKunde DESC",
                new { Nr = k.KundenNr });

            return newId;
        }

        public async Task UpdateKundeAsync(Kunde k)
        {
            var conn = await GetConnectionAsync();

            // DataTable für TYPE_spkundeUpdate erstellen
            var dt = new DataTable();
            dt.Columns.Add("kKunde", typeof(int));
            dt.Columns.Add("kKundenKategorie", typeof(int));
            dt.Columns.Add("xFlag_kKundenKategorie", typeof(bool));
            dt.Columns.Add("cKundenNr", typeof(string));
            dt.Columns.Add("xFlag_cKundenNr", typeof(bool));
            dt.Columns.Add("fRabatt", typeof(decimal));
            dt.Columns.Add("xFlag_fRabatt", typeof(bool));
            dt.Columns.Add("kKundenGruppe", typeof(int));
            dt.Columns.Add("xFlag_kKundenGruppe", typeof(bool));
            dt.Columns.Add("nZahlungsziel", typeof(int));
            dt.Columns.Add("xFlag_nZahlungsziel", typeof(bool));
            dt.Columns.Add("nKreditlimit", typeof(int));
            dt.Columns.Add("xFlag_nKreditlimit", typeof(bool));
            dt.Columns.Add("fSkonto", typeof(decimal));
            dt.Columns.Add("xFlag_fSkonto", typeof(bool));
            dt.Columns.Add("cSperre", typeof(string));
            dt.Columns.Add("xFlag_cSperre", typeof(bool));
            dt.Columns.Add("dGeaendert", typeof(DateTime));
            dt.Columns.Add("xFlag_dGeaendert", typeof(bool));

            var row = dt.NewRow();
            row["kKunde"] = k.Id;
            row["kKundenGruppe"] = k.KundengruppeId > 0 ? k.KundengruppeId : 1;
            row["xFlag_kKundenGruppe"] = true;
            row["fRabatt"] = k.Rabatt;
            row["xFlag_fRabatt"] = true;
            row["nZahlungsziel"] = k.Zahlungsziel ?? 14;
            row["xFlag_nZahlungsziel"] = true;
            row["nKreditlimit"] = 0;
            row["xFlag_nKreditlimit"] = false;  // nicht ändern
            row["fSkonto"] = 0m;
            row["xFlag_fSkonto"] = false;  // nicht ändern
            row["cSperre"] = k.Aktiv == "N" ? "Y" : "N";
            row["xFlag_cSperre"] = true;
            row["dGeaendert"] = DateTime.Now;
            row["xFlag_dGeaendert"] = true;
            dt.Rows.Add(row);

            var p = new DynamicParameters();
            p.Add("@Daten", dt.AsTableValuedParameter("dbo.TYPE_spkundeUpdate"));

            await conn.ExecuteAsync("Kunde.spKundeUpdate", p, commandType: CommandType.StoredProcedure);

            // Rechnungsadresse separat aktualisieren (keine SP nötig)
            await conn.ExecuteAsync(
                @"UPDATE dbo.tRechnungsadresse
                  SET cAnrede=@Anrede, cVorname=@Vorname, cName=@Nachname, cFirma=@Firma,
                      cStrasse=@Strasse, cPLZ=@PLZ, cOrt=@Ort, cLand=@Land,
                      cTel=@Telefon, cMobil=@Mobil, cMail=@Email
                  WHERE kKunde=@Id", k);
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

        #region Bestellung (JTL-Schema: Verkauf.tAuftrag)

        /// <summary>
        /// Holt Aufträge aus Verkauf.tAuftrag (JTL-konform)
        /// </summary>
        public async Task<IEnumerable<Bestellung>> GetBestellungenAsync(BestellStatus? status = null, DateTime? von = null, DateTime? bis = null, int? kundeId = null, int limit = 100, int offset = 0)
        {
            var conn = await GetConnectionAsync();
            var sql = @"SELECT
                a.kAuftrag AS Id,
                a.cAuftragsnr AS BestellNr,
                a.kKunde AS KundeId,
                a.dErstellt AS Erstellt,
                a.nAuftragStatus AS Status,
                a.nStorno,
                a.cWaehrung AS Waehrung,
                ISNULL(e.fWertNetto, 0) AS GesamtNetto,
                ISNULL(e.fWertBrutto, 0) AS GesamtBrutto,
                a.cInternerKommentar AS InternerKommentar,
                k.cFirma, k.cNachname, k.cKundenNr
            FROM Verkauf.tAuftrag a
            LEFT JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
            LEFT JOIN dbo.tKunde k ON a.kKunde = k.kKunde
            WHERE a.nStorno = 0";
            if (status.HasValue) sql += " AND a.nAuftragStatus = @Status";
            if (von.HasValue) sql += " AND a.dErstellt >= @Von";
            if (bis.HasValue) sql += " AND a.dErstellt <= @Bis";
            if (kundeId.HasValue) sql += " AND a.kKunde = @KundeId";
            sql += " ORDER BY a.dErstellt DESC OFFSET @O ROWS FETCH NEXT @L ROWS ONLY";
            return await conn.QueryAsync<Bestellung>(sql, new { Status = (int?)status, Von = von, Bis = bis, KundeId = kundeId, O = offset, L = limit });
        }

        /// <summary>
        /// Holt einzelnen Auftrag mit Details (JTL-konform)
        /// </summary>
        public async Task<Bestellung?> GetBestellungByIdAsync(int id, bool details = true)
        {
            var conn = await GetConnectionAsync();
            var b = await conn.QuerySingleOrDefaultAsync<Bestellung>(@"
                SELECT
                    a.kAuftrag AS Id,
                    a.cAuftragsnr AS BestellNr,
                    a.kKunde AS KundeId,
                    a.dErstellt AS Erstellt,
                    a.nAuftragStatus AS Status,
                    a.nStorno,
                    a.cWaehrung AS Waehrung,
                    ISNULL(e.fWertNetto, 0) AS GesamtNetto,
                    ISNULL(e.fWertBrutto, 0) AS GesamtBrutto,
                    a.cInternerKommentar AS InternerKommentar
                FROM Verkauf.tAuftrag a
                LEFT JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
                WHERE a.kAuftrag = @Id", new { Id = id });

            if (b != null && details)
            {
                b.Kunde = await conn.QuerySingleOrDefaultAsync<Kunde>("SELECT * FROM dbo.tKunde WHERE kKunde = @Id", new { Id = b.KundeId });
                b.Positionen = (await conn.QueryAsync<BestellPosition>(@"
                    SELECT
                        ap.kAuftragPosition AS Id,
                        ap.kAuftrag AS BestellungId,
                        ap.kArtikel AS ArtikelId,
                        ap.cArtNr AS ArtNr,
                        ap.cName AS Name,
                        ap.fAnzahl AS Menge,
                        ap.fVKNetto AS VKNetto,
                        ISNULL(pe.fVKBrutto, ap.fVKNetto * (1 + ap.fMwSt/100)) AS VKBrutto,
                        ap.fMwSt AS MwSt,
                        ap.fRabatt AS Rabatt,
                        ap.nSort AS Sort
                    FROM Verkauf.tAuftragPosition ap
                    LEFT JOIN Verkauf.tAuftragPositionEckdaten pe ON ap.kAuftragPosition = pe.kAuftragPosition
                    WHERE ap.kAuftrag = @Id
                    ORDER BY ap.nSort", new { Id = id })).ToList();

                // Adressen (nTyp: 0=Rechnungsadresse, 1=Lieferadresse)
                b.Rechnungsadresse = await conn.QuerySingleOrDefaultAsync<BestellAdresse>(@"
                    SELECT kAuftragAdresse AS Id, kAuftrag AS BestellungId, nTyp AS Typ,
                           cFirma AS Firma, cVorname AS Vorname, cName AS Nachname,
                           cStrasse AS Strasse, cPLZ AS PLZ, cOrt AS Ort, cLand AS Land,
                           cTel AS Telefon, cMail AS Email
                    FROM Verkauf.tAuftragAdresse WHERE kAuftrag = @Id AND nTyp = 0", new { Id = id });
                b.Lieferadresse = await conn.QuerySingleOrDefaultAsync<BestellAdresse>(@"
                    SELECT kAuftragAdresse AS Id, kAuftrag AS BestellungId, nTyp AS Typ,
                           cFirma AS Firma, cVorname AS Vorname, cName AS Nachname,
                           cStrasse AS Strasse, cPLZ AS PLZ, cOrt AS Ort, cLand AS Land,
                           cTel AS Telefon, cMail AS Email
                    FROM Verkauf.tAuftragAdresse WHERE kAuftrag = @Id AND nTyp = 1", new { Id = id });

                // Rechnungen aus Rechnung.tRechnung
                b.Rechnungen = (await conn.QueryAsync<Rechnung>(@"
                    SELECT r.kRechnung AS Id, r.cRechnungsNr AS RechnungsNr, r.kKunde AS KundeId,
                           r.dErstellt AS Erstellt, re.fVKBruttoGesamt AS Brutto, re.fVKNettoGesamt AS Netto
                    FROM Rechnung.tRechnung r
                    LEFT JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    WHERE r.kAuftrag = @Id AND r.nStorno = 0", new { Id = id })).ToList();
            }
            return b;
        }

        /// <summary>
        /// Erstellt neuen Auftrag in Verkauf.tAuftrag (JTL-konform) + ruft spAuftragEckdatenBerechnen auf
        /// </summary>
        public async Task<int> CreateBestellungAsync(Bestellung b)
        {
            var conn = await GetConnectionAsync();

            // Auftragsnummer aus tLaufendeNummern
            var laufendeNr = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT nNummer, cPrefix, cSuffix FROM dbo.tLaufendeNummern WHERE kLaufendeNummer = 3");
            int nextNr = (laufendeNr?.nNummer ?? 10000) + 1;
            await conn.ExecuteAsync("UPDATE dbo.tLaufendeNummern SET nNummer = @Nr WHERE kLaufendeNummer = 3", new { Nr = nextNr });

            var prefix = (string?)laufendeNr?.cPrefix ?? "";
            var suffix = (string?)laufendeNr?.cSuffix ?? "";
            b.BestellNr = prefix + nextNr.ToString() + suffix;

            // Kunde laden für Adressdaten und JTL-Felder
            var kunde = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT k.kKunde, k.kSprache, k.kKundenGruppe, k.cKundenNr, k.nZahlungsziel, k.kZahlungsart,
                       a.cFirma, a.cAnrede, a.cTitel, a.cVorname, a.cName, a.cStrasse,
                       a.cAdressZusatz, a.cPLZ, a.cOrt, a.cLand, a.cISO, a.cBundesland,
                       a.cTel, a.cMobil, a.cMail
                FROM dbo.tKunde k
                LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                WHERE k.kKunde = @KundeId", new { KundeId = b.KundeId });

            // Auftrag anlegen (JTL-kompatibel mit allen Pflichtfeldern)
            var auftragId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO Verkauf.tAuftrag (
                    kBenutzer, kBenutzerErstellt, kKunde, cAuftragsnr, nType, dErstellt,
                    nBeschreibung, cWaehrung, fFaktor, kFirmaHistory, kSprache,
                    nSteuereinstellung, nHatUpload, fZusatzGewicht,
                    cVersandlandISO, cVersandlandWaehrung, fVersandlandWaehrungFaktor,
                    nStorno, nKomplettAusgeliefert, nLieferPrioritaet, nPremiumVersand,
                    nIstExterneRechnung, nIstReadOnly, nArchiv, nReserviert,
                    nAuftragStatus, fFinanzierungskosten, nPending, nSteuersonderbehandlung,
                    cInternerKommentar,
                    kPlattform, kVersandArt, nZahlungszielTage, kZahlungsart, cKundenNr, kKundengruppe
                ) VALUES (
                    1, 1, @KundeId, @AuftragNr, 1, GETDATE(),
                    0, @Waehrung, 1, 1, ISNULL(@Sprache, 1),
                    0, 0, 0,
                    ISNULL(@LandISO, 'DE'), 'EUR', 1,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    @Status, 0, 0, 0,
                    @Kommentar,
                    1, @VersandArt, @Zahlungsziel, @Zahlungsart, @KundenNr, @Kundengruppe
                );
                SELECT SCOPE_IDENTITY();",
                new {
                    b.KundeId,
                    AuftragNr = b.BestellNr,
                    Waehrung = b.Waehrung ?? "EUR",
                    Sprache = (int?)kunde?.kSprache,
                    LandISO = (string?)kunde?.cISO ?? "DE",
                    Status = b.Status > 0 ? b.Status : 1,
                    Kommentar = b.InternerKommentar,
                    VersandArt = (int?)b.VersandartId ?? 10,
                    Zahlungsziel = (int?)kunde?.nZahlungsziel ?? 14,
                    Zahlungsart = (int?)kunde?.kZahlungsart ?? 2,
                    KundenNr = (string?)kunde?.cKundenNr,
                    Kundengruppe = (int?)kunde?.kKundenGruppe
                });

            // Adressen anlegen (nTyp: 0=Rechnungsadresse, 1=Lieferadresse)
            for (int adressTyp = 0; adressTyp <= 1; adressTyp++)
            {
                var adresse = adressTyp == 1 ? b.Lieferadresse : b.Rechnungsadresse;
                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragAdresse (
                        kAuftrag, kKunde, cFirma, cAnrede, cTitel, cVorname, cName,
                        cStrasse, cPLZ, cOrt, cLand, cISO, cBundesland,
                        cTel, cMobil, cMail, cAdressZusatz, nTyp
                    ) VALUES (
                        @AuftragId, @KundeId, @Firma, @Anrede, @Titel, @Vorname, @Name,
                        @Strasse, @PLZ, @Ort, @Land, @ISO, @Bundesland,
                        @Tel, @Mobil, @Mail, @Zusatz, @Typ
                    )",
                    new {
                        AuftragId = auftragId,
                        KundeId = b.KundeId,
                        Firma = adresse?.Firma ?? (string?)kunde?.cFirma ?? "",
                        Anrede = (string?)kunde?.cAnrede ?? "",
                        Titel = (string?)kunde?.cTitel ?? "",
                        Vorname = adresse?.Vorname ?? (string?)kunde?.cVorname ?? "",
                        Name = adresse?.Nachname ?? (string?)kunde?.cName ?? "",
                        Strasse = adresse?.Strasse ?? (string?)kunde?.cStrasse ?? "",
                        PLZ = adresse?.PLZ ?? (string?)kunde?.cPLZ ?? "",
                        Ort = adresse?.Ort ?? (string?)kunde?.cOrt ?? "",
                        Land = adresse?.Land ?? (string?)kunde?.cLand ?? "Deutschland",
                        ISO = (string?)kunde?.cISO ?? "DE",
                        Bundesland = (string?)kunde?.cBundesland ?? "",
                        Tel = adresse?.Telefon ?? (string?)kunde?.cTel ?? "",
                        Mobil = (string?)kunde?.cMobil ?? "",
                        Mail = adresse?.Email ?? (string?)kunde?.cMail ?? "",
                        Zusatz = (string?)kunde?.cAdressZusatz ?? "",
                        Typ = adressTyp
                    });
            }

            // Positionen anlegen
            int posSort = 0;
            foreach (var pos in b.Positionen)
            {
                // Artikeldaten laden inkl. Steuerklasse
                var artikel = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT a.kArtikel, a.cArtNr, ab.cName, a.fVKNetto, a.fMwSt, a.kSteuerklasse
                    FROM dbo.tArtikel a
                    LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                    WHERE a.kArtikel = @ArtikelId", new { pos.ArtikelId });

                // MwSt-Satz bestimmen und Steuerschlüssel ableiten (3=19%, 2=7%, 1=steuerfrei)
                var mwst = pos.MwSt > 0 ? pos.MwSt : (decimal?)artikel?.fMwSt ?? 19;
                int steuerschluessel = mwst >= 19 ? 3 : (mwst >= 7 ? 2 : 1);

                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragPosition (
                        kArtikel, kAuftrag, cArtNr, nReserviert, cName, cHinweis,
                        fAnzahl, fEkNetto, fVkNetto, fRabatt, fMwSt, nSort,
                        cNameStandard, nType, cEinheit, nHatUpload, fFaktor,
                        kSteuerklasse, kSteuerschluessel
                    ) VALUES (
                        @ArtikelId, @AuftragId, @ArtNr, 0, @Name, '',
                        @Menge, 0, @VKNetto, @Rabatt, @MwSt, @Sort,
                        @Name, 1, 'Stk', 0, 1,
                        @Steuerklasse, @Steuerschluessel
                    )",
                    new {
                        pos.ArtikelId,
                        AuftragId = auftragId,
                        ArtNr = pos.ArtNr ?? (string?)artikel?.cArtNr ?? "",
                        Name = pos.Name ?? (string?)artikel?.cName ?? "",
                        pos.Menge,
                        VKNetto = pos.VKNetto > 0 ? pos.VKNetto : (decimal?)artikel?.fVKNetto ?? 0,
                        Rabatt = pos.Rabatt,
                        MwSt = mwst,
                        Sort = posSort++,
                        Steuerklasse = (int?)artikel?.kSteuerklasse ?? 1,
                        Steuerschluessel = steuerschluessel
                    });
            }

            // WICHTIG: Eckdaten berechnen via JTL Stored Procedure
            await BerechneAuftragEckdatenAsync(auftragId);

            return auftragId;
        }

        /// <summary>
        /// Ruft JTL spAuftragEckdatenBerechnen auf - MUSS nach jeder Auftragsänderung aufgerufen werden!
        /// </summary>
        public async Task BerechneAuftragEckdatenAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            var dt = new DataTable();
            dt.Columns.Add("kAuftrag", typeof(int));
            dt.Rows.Add(kAuftrag);

            var p = new DynamicParameters();
            p.Add("@Auftrag", dt.AsTableValuedParameter("Verkauf.TYPE_spAuftragEckdatenBerechnen"));
            await conn.ExecuteAsync("Verkauf.spAuftragEckdatenBerechnen", p, commandType: CommandType.StoredProcedure);
        }

        private async Task CreateBestellAdresseAsync(SqlConnection conn, SqlTransaction tx, BestellAdresse a)
        {
            // Legacy-Methode für Kompatibilität
            await conn.ExecuteAsync(@"
                INSERT INTO Verkauf.tAuftragAdresse (kAuftrag, nTyp, cVorname, cName, cFirma, cStrasse, cPLZ, cOrt, cLand, cTel, cMail)
                VALUES (@BestellungId, @Typ, @Vorname, @Nachname, @Firma, @Strasse, @PLZ, @Ort, @Land, @Telefon, @Email)", a, tx);
        }

        /// <summary>
        /// Aktualisiert Auftragsstatus (JTL-konform)
        /// </summary>
        public async Task UpdateBestellStatusAsync(int id, BestellStatus status)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE Verkauf.tAuftrag
                SET nAuftragStatus = @Status, dGeaendert = GETDATE()
                WHERE kAuftrag = @Id",
                new { Status = (int)status, Id = id });

            // Eckdaten neu berechnen
            await BerechneAuftragEckdatenAsync(id);
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

        #region WooCommerce Shops
        public async Task<List<WooCommerceShop>> GetWooCommerceShopsAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<WooCommerceShop>("SELECT * FROM tWooCommerceShop ORDER BY cName")).ToList();
        }

        public async Task<WooCommerceShop?> GetWooCommerceShopByIdAsync(int id)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<WooCommerceShop>("SELECT * FROM tWooCommerceShop WHERE kWooCommerceShop=@Id", new { Id = id });
        }

        public async Task<int> CreateWooCommerceShopAsync(WooCommerceShop shop)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tWooCommerceShop (cName, cUrl, cConsumerKey, cConsumerSecret, cWebhookSecret, cWebhookCallbackUrl, nAktiv, nWebhooksAktiv, nSyncIntervallMinuten)
                VALUES (@Name, @Url, @ConsumerKey, @ConsumerSecret, @WebhookSecret, @WebhookCallbackUrl, @Aktiv, @WebhooksAktiv, @SyncIntervallMinuten);
                SELECT SCOPE_IDENTITY();", shop);
        }

        public async Task UpdateWooCommerceShopAsync(WooCommerceShop shop)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tWooCommerceShop SET
                    cName=@Name, cUrl=@Url, cConsumerKey=@ConsumerKey, cConsumerSecret=@ConsumerSecret,
                    cWebhookSecret=@WebhookSecret, cWebhookCallbackUrl=@WebhookCallbackUrl,
                    nAktiv=@Aktiv, nWebhooksAktiv=@WebhooksAktiv, nSyncIntervallMinuten=@SyncIntervallMinuten
                WHERE kWooCommerceShop=@Id", shop);
        }

        public async Task UpdateWooCommerceSyncTimeAsync(int shopId)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE tWooCommerceShop SET dLetzterSync=GETDATE() WHERE kWooCommerceShop=@Id", new { Id = shopId });
        }

        public async Task DeleteWooCommerceShopAsync(int id)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM tWooCommerceShop WHERE kWooCommerceShop=@Id", new { Id = id });
        }

        public async Task<List<Artikel>> GetArtikelByIdsAsync(List<int> ids)
        {
            if (ids == null || !ids.Any()) return new List<Artikel>();
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<Artikel>(@"
                SELECT a.*, ab.cName, ab.cBeschreibung, ab.cKurzBeschreibung, ab.cUrlPath
                FROM tArtikel a
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel=ab.kArtikel AND ab.kSprache=1 AND ab.kPlattform=1
                WHERE a.kArtikel IN @Ids", new { Ids = ids })).ToList();
        }

        public async Task<Dictionary<string, int>> GetAllBestaendeAsync()
        {
            var conn = await GetConnectionAsync();
            var result = await conn.QueryAsync<(string ArtNr, decimal Bestand)>(
                "SELECT cArtNr, ISNULL(fLagerbestand, 0) FROM tArtikel WHERE cAktiv='Y'");
            return result.ToDictionary(x => x.ArtNr, x => (int)x.Bestand);
        }

        public async Task<Dictionary<string, decimal>> GetAllPreiseAsync(int kundengruppe = 0)
        {
            var conn = await GetConnectionAsync();
            var result = await conn.QueryAsync<(string ArtNr, decimal Preis)>(@"
                SELECT a.cArtNr, ISNULL(p.fVKNetto, a.fVKNetto)
                FROM tArtikel a
                LEFT JOIN tPreis p ON a.kArtikel=p.kArtikel AND p.kKundengruppe=@Kg
                WHERE a.cAktiv='Y'", new { Kg = kundengruppe });
            return result.ToDictionary(x => x.ArtNr, x => x.Preis);
        }

        public async Task<Bestellung?> GetBestellungByExternerNrAsync(string externeNr)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Bestellung>(
                "SELECT * FROM tBestellung WHERE cExterneAuftragsnummer=@Nr", new { Nr = externeNr });
        }

        public async Task<List<KategorieSync>> GetKategorienAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<KategorieSync>(@"
                SELECT k.kKategorie AS Id, k.kOberKategorie AS ParentId, ks.cName AS Name, ks.cBeschreibung AS Beschreibung, k.nEbene AS Ebene
                FROM tKategorie k
                LEFT JOIN tKategorieSprache ks ON k.kKategorie=ks.kKategorie AND ks.kSprache=1
                WHERE k.nAktiv=1
                ORDER BY k.nEbene, k.nSort")).ToList();
        }

        public class KategorieSync
        {
            public int Id { get; set; }
            public int? ParentId { get; set; }
            public string Name { get; set; } = "";
            public string? Beschreibung { get; set; }
            public int Ebene { get; set; }
        }
        #endregion

        #region JTL Shop Sync (tShop, tArtikelShop)

        /// <summary>
        /// Holt alle konfigurierten JTL Shops
        /// </summary>
        public async Task<List<JtlShop>> GetJtlShopsAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<JtlShop>(@"
                SELECT kShop, cName, cServerWeb AS Url, cBenutzerWeb AS ConsumerKey, cPasswortWeb AS ConsumerSecret,
                       nAktiv AS Aktiv, nTyp AS ShopTyp, kWarenlager AS WarenlagerId
                FROM tShop
                WHERE nAktiv = 1
                ORDER BY cName")).ToList();
        }

        /// <summary>
        /// Holt einen JTL Shop per ID
        /// </summary>
        public async Task<JtlShop?> GetJtlShopByIdAsync(int kShop)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<JtlShop>(@"
                SELECT kShop, cName, cServerWeb AS Url, cBenutzerWeb AS ConsumerKey, cPasswortWeb AS ConsumerSecret,
                       nAktiv AS Aktiv, nTyp AS ShopTyp, kWarenlager AS WarenlagerId
                FROM tShop WHERE kShop = @kShop", new { kShop });
        }

        /// <summary>
        /// Holt Artikel die synchronisiert werden muessen (nAktion > 0)
        /// </summary>
        public async Task<List<ArtikelSyncInfo>> GetArtikelZuSyncenAsync(int kShop, int limit = 100)
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<ArtikelSyncInfo>(@"
                SELECT TOP (@limit)
                    ash.kArtikel, ash.kShop, ash.nAktion, ash.cInet AS ShopProduktId,
                    a.cArtNr, ab.cName AS ArtikelName, a.fVKBrutto AS Preis,
                    ISNULL(a.fLagerbestand, 0) AS Bestand, a.dGeaendert
                FROM tArtikelShop ash
                INNER JOIN tArtikel a ON ash.kArtikel = a.kArtikel
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                WHERE ash.kShop = @kShop
                  AND ash.nAktion > 0
                  AND ISNULL(ash.nInBearbeitung, 0) = 0
                ORDER BY ash.nAktion DESC, a.dGeaendert DESC",
                new { kShop, limit })).ToList();
        }

        /// <summary>
        /// Markiert Artikel als "in Bearbeitung"
        /// </summary>
        public async Task SetArtikelInBearbeitungAsync(int kShop, List<int> artikelIds, bool inBearbeitung)
        {
            if (!artikelIds.Any()) return;
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tArtikelShop
                SET nInBearbeitung = @inBearb
                WHERE kShop = @kShop AND kArtikel IN @artikelIds",
                new { kShop, artikelIds, inBearb = inBearbeitung ? 1 : 0 });
        }

        /// <summary>
        /// Markiert Artikel als synchronisiert (nAktion = 0) und speichert Shop-Produkt-ID
        /// </summary>
        public async Task SetArtikelSyncErfolgreichAsync(int kShop, int kArtikel, string? shopProduktId = null)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tArtikelShop
                SET nAktion = 0, nInBearbeitung = 0, cInet = ISNULL(@shopId, cInet)
                WHERE kShop = @kShop AND kArtikel = @kArtikel",
                new { kShop, kArtikel, shopId = shopProduktId });
        }

        /// <summary>
        /// Markiert Artikel fuer Sync (nAktion = 1 = Update, 2 = Delete)
        /// </summary>
        public async Task SetArtikelSyncNoetigAsync(int kShop, int kArtikel, int aktion = 1)
        {
            var conn = await GetConnectionAsync();
            // Pruefen ob Eintrag existiert
            var exists = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM tArtikelShop WHERE kShop = @kShop AND kArtikel = @kArtikel",
                new { kShop, kArtikel });

            if (exists > 0)
            {
                await conn.ExecuteAsync(@"
                    UPDATE tArtikelShop SET nAktion = @aktion WHERE kShop = @kShop AND kArtikel = @kArtikel",
                    new { kShop, kArtikel, aktion });
            }
            else
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO tArtikelShop (kArtikel, kShop, nAktion, nInBearbeitung)
                    VALUES (@kArtikel, @kShop, @aktion, 0)",
                    new { kShop, kArtikel, aktion });
            }
        }

        /// <summary>
        /// Markiert alle aktiven Artikel eines Shops fuer Sync
        /// </summary>
        public async Task SetAlleArtikelSyncNoetigAsync(int kShop)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                -- Bestehende auf Update setzen
                UPDATE tArtikelShop SET nAktion = 1 WHERE kShop = @kShop AND nAktion = 0;

                -- Neue Artikel hinzufuegen die noch nicht im Shop sind
                INSERT INTO tArtikelShop (kArtikel, kShop, nAktion, nInBearbeitung)
                SELECT a.kArtikel, @kShop, 1, 0
                FROM tArtikel a
                WHERE a.cAktiv = 'Y'
                  AND NOT EXISTS (SELECT 1 FROM tArtikelShop ash WHERE ash.kArtikel = a.kArtikel AND ash.kShop = @kShop)",
                new { kShop });
        }

        /// <summary>
        /// Holt Sync-Statistik fuer einen Shop
        /// </summary>
        public async Task<ShopSyncStats> GetShopSyncStatsAsync(int kShop)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<ShopSyncStats>(@"
                SELECT
                    COUNT(*) AS Gesamt,
                    SUM(CASE WHEN nAktion = 0 THEN 1 ELSE 0 END) AS Synchronisiert,
                    SUM(CASE WHEN nAktion = 1 THEN 1 ELSE 0 END) AS UpdateNoetig,
                    SUM(CASE WHEN nAktion = 2 THEN 1 ELSE 0 END) AS LoeschenNoetig,
                    SUM(CASE WHEN nInBearbeitung = 1 THEN 1 ELSE 0 END) AS InBearbeitung
                FROM tArtikelShop WHERE kShop = @kShop", new { kShop });
        }

        /// <summary>
        /// Holt Zahlungsabgleich-Transaktionen
        /// </summary>
        public async Task<List<ZahlungsabgleichUmsatz>> GetZahlungsabgleichUmsaetzeAsync(int? kModul = null, int limit = 100)
        {
            var conn = await GetConnectionAsync();
            var where = kModul.HasValue ? "WHERE kZahlungsabgleichModul = @kModul" : "";
            return (await conn.QueryAsync<ZahlungsabgleichUmsatz>($@"
                SELECT TOP (@limit) * FROM tZahlungsabgleichUmsatz {where}
                ORDER BY dBuchungsdatum DESC", new { kModul, limit })).ToList();
        }

        public class JtlShop
        {
            public int KShop { get; set; }
            public string Name { get; set; } = "";
            public string? Url { get; set; }
            public string? ConsumerKey { get; set; }
            public string? ConsumerSecret { get; set; }
            public bool Aktiv { get; set; }
            public int ShopTyp { get; set; } // 4 = WooCommerce, etc.
            public int? WarenlagerId { get; set; }
        }

        public class ArtikelSyncInfo
        {
            public int KArtikel { get; set; }
            public int KShop { get; set; }
            public int NAktion { get; set; } // 0=OK, 1=Update, 2=Delete
            public string? ShopProduktId { get; set; }
            public string ArtNr { get; set; } = "";
            public string? ArtikelName { get; set; }
            public decimal Preis { get; set; }
            public decimal Bestand { get; set; }
            public DateTime? DGeaendert { get; set; }
        }

        public class ShopSyncStats
        {
            public int Gesamt { get; set; }
            public int Synchronisiert { get; set; }
            public int UpdateNoetig { get; set; }
            public int LoeschenNoetig { get; set; }
            public int InBearbeitung { get; set; }
        }

        public class ZahlungsabgleichUmsatz
        {
            public int KZahlungsabgleichUmsatz { get; set; }
            public int KZahlungsabgleichModul { get; set; }
            public string? CKontoIdentifikation { get; set; }
            public string? CTransaktionID { get; set; }
            public DateTime? DBuchungsdatum { get; set; }
            public decimal FBetrag { get; set; }
            public string CWaehrungISO { get; set; } = "EUR";
            public string? CName { get; set; }
            public string? CKonto { get; set; }
            public string? CVerwendungszweck { get; set; }
            public int NStatus { get; set; } // 0=offen, 1=zugeordnet, 2=ignoriert
            public DateTime? DAbgleichszeitpunkt { get; set; }
            public int? KBenutzer { get; set; }
        }

        #endregion

        #region JTL Zahlungsabgleich (tZahlungsabgleichUmsatz, tZahlung)

        /// <summary>
        /// Holt offene (nicht zugeordnete) Bank-Transaktionen
        /// </summary>
        public async Task<List<ZahlungsabgleichUmsatz>> GetOffeneUmsaetzeAsync(int? kModul = null, int limit = 500)
        {
            var conn = await GetConnectionAsync();
            var modulFilter = kModul.HasValue ? "AND kZahlungsabgleichModul = @kModul" : "";
            return (await conn.QueryAsync<ZahlungsabgleichUmsatz>($@"
                SELECT TOP (@limit)
                    kZahlungsabgleichUmsatz, kZahlungsabgleichModul, cKontoIdentifikation,
                    cTransaktionID, dBuchungsdatum, fBetrag, cWaehrungISO, cName, cKonto,
                    cVerwendungszweck, nStatus, dAbgleichszeitpunkt, kBenutzer
                FROM tZahlungsabgleichUmsatz
                WHERE nStatus = 0 AND fBetrag > 0 {modulFilter}
                ORDER BY dBuchungsdatum DESC",
                new { kModul, limit })).ToList();
        }

        /// <summary>
        /// Holt bereits zugeordnete Bank-Transaktionen
        /// </summary>
        public async Task<List<ZahlungsabgleichUmsatzMitZuordnung>> GetGematchteUmsaetzeAsync(DateTime? von = null, DateTime? bis = null, int limit = 500)
        {
            var conn = await GetConnectionAsync();
            var dateFilter = "";
            if (von.HasValue) dateFilter += " AND u.dBuchungsdatum >= @von";
            if (bis.HasValue) dateFilter += " AND u.dBuchungsdatum <= @bis";

            return (await conn.QueryAsync<ZahlungsabgleichUmsatzMitZuordnung>($@"
                SELECT TOP (@limit)
                    u.kZahlungsabgleichUmsatz, u.dBuchungsdatum, u.fBetrag, u.cWaehrungISO,
                    u.cName, u.cVerwendungszweck, u.nStatus, u.dAbgleichszeitpunkt,
                    z.kZahlung, z.kBestellung, z.kRechnung, z.fBetrag AS ZahlungsBetrag,
                    z.nZuweisungstyp, z.nZuweisungswertung,
                    r.cRechnungsNr, a.cAuftragsnr AS BestellNr
                FROM tZahlungsabgleichUmsatz u
                INNER JOIN tZahlung z ON u.kZahlungsabgleichUmsatz = z.kZahlungsabgleichUmsatz
                LEFT JOIN Rechnung.tRechnung r ON z.kRechnung = r.kRechnung
                LEFT JOIN Verkauf.tAuftrag a ON z.kBestellung = a.kAuftrag
                WHERE u.nStatus = 1 {dateFilter}
                ORDER BY u.dBuchungsdatum DESC",
                new { von, bis, limit })).ToList();
        }

        /// <summary>
        /// Holt offene Rechnungen fuer Matching-Vorschlaege
        /// </summary>
        public async Task<List<OffeneRechnungInfo>> GetOffeneRechnungenFuerMatchingAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<OffeneRechnungInfo>(@"
                SELECT
                    r.kRechnung, r.cRechnungsNr AS RechnungsNr, r.kKunde,
                    re.fVKBruttoGesamt AS Brutto, re.fBezahlt,
                    (re.fVKBruttoGesamt - ISNULL(re.fBezahlt, 0)) AS OffenerBetrag,
                    r.dErstellt, r.dFaellig,
                    k.cFirma AS KundeFirma, k.cNachname AS KundeNachname, k.cKundenNr,
                    ISNULL(kb.cIBAN, '') AS KundeIBAN
                FROM Rechnung.tRechnung r
                LEFT JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                LEFT JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                LEFT JOIN dbo.tKundeBankverbindung kb ON k.kKunde = kb.kKunde AND kb.nStandard = 1
                WHERE r.nStorno = 0
                  AND (re.fVKBruttoGesamt - ISNULL(re.fBezahlt, 0)) > 0.01
                ORDER BY r.dFaellig")).ToList();
        }

        /// <summary>
        /// Holt offene Auftraege fuer Matching (falls direkt auf Auftrag gebucht wird)
        /// </summary>
        public async Task<List<OffenerAuftragInfo>> GetOffeneAuftraegeFuerMatchingAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<OffenerAuftragInfo>(@"
                SELECT
                    a.kAuftrag, a.cAuftragsnr AS AuftragNr, a.kKunde,
                    e.fWertBrutto AS Brutto, ISNULL(e.fGezahlt, 0) AS Bezahlt,
                    (e.fWertBrutto - ISNULL(e.fGezahlt, 0)) AS OffenerBetrag,
                    a.dErstellt,
                    k.cFirma AS KundeFirma, k.cNachname AS KundeNachname, k.cKundenNr,
                    ISNULL(kb.cIBAN, '') AS KundeIBAN
                FROM Verkauf.tAuftrag a
                LEFT JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
                LEFT JOIN dbo.tKunde k ON a.kKunde = k.kKunde
                LEFT JOIN dbo.tKundeBankverbindung kb ON k.kKunde = kb.kKunde AND kb.nStandard = 1
                WHERE a.nStorno = 0 AND a.nAuftragStatus < 4
                  AND (e.fWertBrutto - ISNULL(e.fGezahlt, 0)) > 0.01
                ORDER BY a.dErstellt DESC")).ToList();
        }

        /// <summary>
        /// Ordnet eine Bank-Transaktion einer Rechnung zu (erstellt tZahlung)
        /// </summary>
        public async Task<int> ZuordnenZuRechnungAsync(int kZahlungsabgleichUmsatz, int kRechnung, decimal betrag, int kBenutzer, int zuweisungswertung = 100)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // Rechnung laden
                var rechnung = await conn.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT kKunde FROM Rechnung.tRechnung WHERE kRechnung = @kRechnung",
                    new { kRechnung }, tx);
                if (rechnung == null) throw new Exception($"Rechnung {kRechnung} nicht gefunden");

                // Zahlungsart ermitteln (Standard: Ueberweisung = 5)
                var zahlungsart = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kZahlungsart FROM tZahlungsart WHERE cName LIKE '%berweis%'", transaction: tx) ?? 5;

                // tZahlung erstellen
                var kZahlung = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tZahlung (
                        cName, dDatum, fBetrag, kBestellung, kRechnung, kBenutzer,
                        kZahlungsart, kZahlungsabgleichUmsatz, nZuweisungstyp, nZuweisungswertung
                    ) VALUES (
                        'Zahlungsabgleich', GETDATE(), @betrag, NULL, @kRechnung, @kBenutzer,
                        @zahlungsart, @kZahlungsabgleichUmsatz, 1, @zuweisungswertung
                    ); SELECT SCOPE_IDENTITY();",
                    new { betrag, kRechnung, kBenutzer, zahlungsart, kZahlungsabgleichUmsatz, zuweisungswertung }, tx);

                // tZahlungsabgleichUmsatz als zugeordnet markieren
                await conn.ExecuteAsync(@"
                    UPDATE tZahlungsabgleichUmsatz
                    SET nStatus = 1, dAbgleichszeitpunkt = GETDATE(), kBenutzer = @kBenutzer
                    WHERE kZahlungsabgleichUmsatz = @kZahlungsabgleichUmsatz",
                    new { kZahlungsabgleichUmsatz, kBenutzer }, tx);

                // Rechnung aktualisieren (bezahlt)
                await conn.ExecuteAsync(@"
                    UPDATE Rechnung.tRechnung SET dGeaendert = GETDATE() WHERE kRechnung = @kRechnung",
                    new { kRechnung }, tx);

                // Eckdaten neu berechnen
                await conn.ExecuteAsync("Rechnung.spRechnungEckdatenBerechnen",
                    new { kRechnung }, tx, commandType: CommandType.StoredProcedure);

                tx.Commit();
                _log.Information("Zahlung {Betrag} EUR zu Rechnung {RechnungId} zugeordnet (Transaktion {UmsatzId})",
                    betrag, kRechnung, kZahlungsabgleichUmsatz);
                return kZahlung;
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Ordnet eine Bank-Transaktion einem Auftrag zu
        /// </summary>
        public async Task<int> ZuordnenZuAuftragAsync(int kZahlungsabgleichUmsatz, int kAuftrag, decimal betrag, int kBenutzer, int zuweisungswertung = 100)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                var zahlungsart = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kZahlungsart FROM tZahlungsart WHERE cName LIKE '%berweis%'", transaction: tx) ?? 5;

                var kZahlung = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tZahlung (
                        cName, dDatum, fBetrag, kBestellung, kRechnung, kBenutzer,
                        kZahlungsart, kZahlungsabgleichUmsatz, nZuweisungstyp, nZuweisungswertung
                    ) VALUES (
                        'Zahlungsabgleich', GETDATE(), @betrag, @kAuftrag, NULL, @kBenutzer,
                        @zahlungsart, @kZahlungsabgleichUmsatz, 1, @zuweisungswertung
                    ); SELECT SCOPE_IDENTITY();",
                    new { betrag, kAuftrag, kBenutzer, zahlungsart, kZahlungsabgleichUmsatz, zuweisungswertung }, tx);

                await conn.ExecuteAsync(@"
                    UPDATE tZahlungsabgleichUmsatz
                    SET nStatus = 1, dAbgleichszeitpunkt = GETDATE(), kBenutzer = @kBenutzer
                    WHERE kZahlungsabgleichUmsatz = @kZahlungsabgleichUmsatz",
                    new { kZahlungsabgleichUmsatz, kBenutzer }, tx);

                tx.Commit();
                return kZahlung;
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Importiert eine Bank-Transaktion (z.B. von PayPal, Mollie, MT940)
        /// </summary>
        public async Task<int> ImportUmsatzAsync(ZahlungsabgleichUmsatz umsatz)
        {
            var conn = await GetConnectionAsync();

            // Pruefen ob bereits importiert (via TransaktionID)
            if (!string.IsNullOrEmpty(umsatz.CTransaktionID))
            {
                var exists = await conn.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM tZahlungsabgleichUmsatz WHERE cTransaktionID = @id",
                    new { id = umsatz.CTransaktionID });
                if (exists > 0) return 0; // Bereits vorhanden
            }

            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tZahlungsabgleichUmsatz (
                    kZahlungsabgleichModul, cKontoIdentifikation, cTransaktionID,
                    dBuchungsdatum, fBetrag, cWaehrungISO, cName, cKonto, cVerwendungszweck,
                    nStatus, nSichtbar, nBuchungstyp
                ) VALUES (
                    @KZahlungsabgleichModul, @CKontoIdentifikation, @CTransaktionID,
                    @DBuchungsdatum, @FBetrag, @CWaehrungISO, @CName, @CKonto, @CVerwendungszweck,
                    0, 1, 1
                ); SELECT SCOPE_IDENTITY();", umsatz);
        }

        /// <summary>
        /// Holt Zahlungsabgleich-Module (Sparkasse, PayPal, etc.)
        /// </summary>
        public async Task<List<ZahlungsabgleichModul>> GetZahlungsabgleichModuleAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<ZahlungsabgleichModul>(
                "SELECT kZahlungsabgleichModul, cModulID, cEinstellungen FROM tZahlungsabgleichModul")).ToList();
        }

        /// <summary>
        /// Erstellt oder aktualisiert ein Zahlungsabgleich-Modul
        /// </summary>
        public async Task<int> SaveZahlungsabgleichModulAsync(string modulId, string? einstellungen = null)
        {
            var conn = await GetConnectionAsync();
            var existing = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT kZahlungsabgleichModul FROM tZahlungsabgleichModul WHERE cModulID = @modulId",
                new { modulId });

            if (existing.HasValue)
            {
                if (einstellungen != null)
                {
                    await conn.ExecuteAsync(
                        "UPDATE tZahlungsabgleichModul SET cEinstellungen = @einstellungen WHERE kZahlungsabgleichModul = @id",
                        new { id = existing.Value, einstellungen });
                }
                return existing.Value;
            }

            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tZahlungsabgleichModul (cModulID, cEinstellungen)
                VALUES (@modulId, @einstellungen);
                SELECT SCOPE_IDENTITY();", new { modulId, einstellungen });
        }

        /// <summary>
        /// Holt Zahlungsarten
        /// </summary>
        public async Task<List<Zahlungsart>> GetZahlungsartenAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<Zahlungsart>(@"
                SELECT kZahlungsart, cName, nAktiv, nLastschrift, cKonto
                FROM tZahlungsart WHERE nAktiv = 1 ORDER BY nPrioritaet")).ToList();
        }

        /// <summary>
        /// Holt Zahlungsabgleich-Statistik
        /// </summary>
        public async Task<ZahlungsabgleichStats> GetZahlungsabgleichStatsAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<ZahlungsabgleichStats>(@"
                SELECT
                    (SELECT COUNT(*) FROM tZahlungsabgleichUmsatz WHERE nStatus = 0 AND fBetrag > 0) AS OffeneTransaktionen,
                    (SELECT ISNULL(SUM(fBetrag), 0) FROM tZahlungsabgleichUmsatz WHERE nStatus = 0 AND fBetrag > 0) AS OffenerBetrag,
                    (SELECT COUNT(*) FROM tZahlungsabgleichUmsatz WHERE nStatus = 1) AS ZugeordneteTransaktionen,
                    (SELECT COUNT(*) FROM Rechnung.tRechnung r
                     LEFT JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                     WHERE r.nStorno = 0 AND (re.fVKBruttoGesamt - ISNULL(re.fBezahlt, 0)) > 0.01) AS OffeneRechnungen,
                    (SELECT ISNULL(SUM(re.fVKBruttoGesamt - ISNULL(re.fBezahlt, 0)), 0)
                     FROM Rechnung.tRechnung r
                     LEFT JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                     WHERE r.nStorno = 0 AND (re.fVKBruttoGesamt - ISNULL(re.fBezahlt, 0)) > 0.01) AS OffenerRechnungsBetrag");
        }

        /// <summary>
        /// Markiert Transaktion als ignoriert
        /// </summary>
        public async Task IgnoriereUmsatzAsync(int kZahlungsabgleichUmsatz, int kBenutzer)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tZahlungsabgleichUmsatz
                SET nStatus = 2, dAbgleichszeitpunkt = GETDATE(), kBenutzer = @kBenutzer
                WHERE kZahlungsabgleichUmsatz = @kZahlungsabgleichUmsatz",
                new { kZahlungsabgleichUmsatz, kBenutzer });
        }

        /// <summary>
        /// Setzt Zuordnung zurueck
        /// </summary>
        public async Task ZuordnungAufhebenAsync(int kZahlungsabgleichUmsatz)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // tZahlung loeschen
                await conn.ExecuteAsync(
                    "DELETE FROM tZahlung WHERE kZahlungsabgleichUmsatz = @id",
                    new { id = kZahlungsabgleichUmsatz }, tx);

                // Status zuruecksetzen
                await conn.ExecuteAsync(@"
                    UPDATE tZahlungsabgleichUmsatz
                    SET nStatus = 0, dAbgleichszeitpunkt = NULL, kBenutzer = NULL
                    WHERE kZahlungsabgleichUmsatz = @id",
                    new { id = kZahlungsabgleichUmsatz }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // DTOs fuer Zahlungsabgleich
        public class ZahlungsabgleichUmsatzMitZuordnung
        {
            public int KZahlungsabgleichUmsatz { get; set; }
            public DateTime? DBuchungsdatum { get; set; }
            public decimal FBetrag { get; set; }
            public string CWaehrungISO { get; set; } = "EUR";
            public string? CName { get; set; }
            public string? CVerwendungszweck { get; set; }
            public int NStatus { get; set; }
            public DateTime? DAbgleichszeitpunkt { get; set; }
            public int KZahlung { get; set; }
            public int? KBestellung { get; set; }
            public int? KRechnung { get; set; }
            public decimal ZahlungsBetrag { get; set; }
            public int NZuweisungstyp { get; set; }
            public int NZuweisungswertung { get; set; }
            public string? RechnungsNr { get; set; }
            public string? BestellNr { get; set; }
        }

        public class OffeneRechnungInfo
        {
            public int KRechnung { get; set; }
            public string RechnungsNr { get; set; } = "";
            public int KKunde { get; set; }
            public decimal Brutto { get; set; }
            public decimal Bezahlt { get; set; }
            public decimal OffenerBetrag { get; set; }
            public DateTime? DErstellt { get; set; }
            public DateTime? DFaellig { get; set; }
            public string? KundeFirma { get; set; }
            public string? KundeNachname { get; set; }
            public string? KundenNr { get; set; }
            public string? KundeIBAN { get; set; }
        }

        public class OffenerAuftragInfo
        {
            public int KAuftrag { get; set; }
            public string AuftragNr { get; set; } = "";
            public int KKunde { get; set; }
            public decimal Brutto { get; set; }
            public decimal Bezahlt { get; set; }
            public decimal OffenerBetrag { get; set; }
            public DateTime? DErstellt { get; set; }
            public string? KundeFirma { get; set; }
            public string? KundeNachname { get; set; }
            public string? KundenNr { get; set; }
            public string? KundeIBAN { get; set; }
        }

        public class ZahlungsabgleichModul
        {
            public int KZahlungsabgleichModul { get; set; }
            public string CModulID { get; set; } = "";
            public string? CEinstellungen { get; set; }
        }

        public class Zahlungsart
        {
            public int KZahlungsart { get; set; }
            public string CName { get; set; } = "";
            public bool NAktiv { get; set; }
            public bool NLastschrift { get; set; }
            public string? CKonto { get; set; }
        }

        public class ZahlungsabgleichStats
        {
            public int OffeneTransaktionen { get; set; }
            public decimal OffenerBetrag { get; set; }
            public int ZugeordneteTransaktionen { get; set; }
            public int OffeneRechnungen { get; set; }
            public decimal OffenerRechnungsBetrag { get; set; }
        }

        #endregion
    }
}
