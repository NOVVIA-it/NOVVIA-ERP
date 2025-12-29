using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    public class StammdatenService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<StammdatenService>();

        public StammdatenService(JtlDbContext db) => _db = db;

        #region Firma
        public async Task<Firma?> GetFirmaAsync(int id = 1)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Firma>("SELECT * FROM tFirma WHERE kFirma = @Id", new { Id = id });
        }

        public async Task UpdateFirmaAsync(Firma firma)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tFirma SET cName=@Name, cStrasse=@Strasse, cPLZ=@PLZ, cOrt=@Ort, cLand=@Land,
                cTel=@Telefon, cFax=@Fax, cMail=@Email, cWWW=@Website, cUStID=@UStID, cSteuerNr=@SteuerNr,
                cIBAN=@IBAN, cBIC=@BIC, cBank=@Bank, cGeschaeftsfuehrer=@Geschaeftsfuehrer WHERE kFirma=@Id", firma);
        }
        #endregion

        #region Warenlager
        public async Task<IEnumerable<Warenlager>> GetWarenlagerAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tWarenLager" + (nurAktive ? " WHERE nAktiv = 1" : "") + " ORDER BY nStandard DESC, cName";
            return await conn.QueryAsync<Warenlager>(sql);
        }

        public async Task<int> CreateWarenlagerAsync(Warenlager lager)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tWarenLager (cName, cKuerzel, cBeschreibung, cStrasse, cPLZ, cOrt, cLand, nStandard, nAktiv)
                VALUES (@Name, @Kuerzel, @Beschreibung, @Strasse, @PLZ, @Ort, @Land, @IstStandard, @Aktiv); SELECT SCOPE_IDENTITY();", lager);
        }

        public async Task<IEnumerable<Lagerplatz>> GetLagerplaetzeAsync(int lagerId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Lagerplatz>("SELECT * FROM tWarenLagerPlatz WHERE kWarenLager = @Id ORDER BY nSortierung, cName", new { Id = lagerId });
        }

        public async Task<int> CreateLagerplatzAsync(Lagerplatz platz)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tWarenLagerPlatz (kWarenLager, cName, cRegal, cFach, cEbene, cBarcode, nAktiv, nSortierung)
                VALUES (@WarenlagerId, @Name, @Regal, @Fach, @Ebene, @Barcode, @Aktiv, @Sortierung); SELECT SCOPE_IDENTITY();", platz);
        }

        public async Task UpdateWarenlagerAsync(Warenlager lager)
        {
            var conn = await _db.GetConnectionAsync();
            // Falls dieses Lager als Standard gesetzt wird, andere Standard-Flags zuruecksetzen
            if (lager.IstStandard)
                await conn.ExecuteAsync("UPDATE tWarenLager SET nStandard = 0 WHERE kWarenLager != @Id", new { lager.Id });

            await conn.ExecuteAsync(@"UPDATE tWarenLager SET cName=@Name, cKuerzel=@Kuerzel, cBeschreibung=@Beschreibung,
                cStrasse=@Strasse, cPLZ=@PLZ, cOrt=@Ort, cLand=@Land, nStandard=@IstStandard, nAktiv=@Aktiv
                WHERE kWarenLager=@Id", lager);
        }

        public async Task DeleteWarenlagerAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            // Pruefe ob Bestaende existieren
            var bestaende = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tLagerbestand WHERE kWarenlager = @Id AND fBestand > 0", new { Id = id });
            if (bestaende > 0)
                throw new InvalidOperationException($"Lager kann nicht geloescht werden - es existieren noch {bestaende} Bestaende.");

            // Lagerplaetze loeschen
            await conn.ExecuteAsync("DELETE FROM tWarenLagerPlatz WHERE kWarenLager = @Id", new { Id = id });
            // Lager loeschen
            await conn.ExecuteAsync("DELETE FROM tWarenLager WHERE kWarenLager = @Id", new { Id = id });
        }

        public async Task UpdateLagerplatzAsync(Lagerplatz platz)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tWarenLagerPlatz SET cName=@Name, cRegal=@Regal, cFach=@Fach,
                cEbene=@Ebene, cBarcode=@Barcode, nAktiv=@Aktiv, nSortierung=@Sortierung
                WHERE kWarenLagerPlatz=@Id", platz);
        }

        public async Task DeleteLagerplatzAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            // Pruefe ob Bestaende auf diesem Platz existieren (falls PlatzID in Lagerbestand)
            await conn.ExecuteAsync("DELETE FROM tWarenLagerPlatz WHERE kWarenLagerPlatz = @Id", new { Id = id });
        }
        #endregion

        #region Kategorien
        public async Task<IEnumerable<Kategorie>> GetKategorienAsync(int? oberKategorieId = null)
        {
            var conn = await _db.GetConnectionAsync();
            var kategorien = oberKategorieId.HasValue
                ? await conn.QueryAsync<Kategorie>("SELECT * FROM tKategorie WHERE kOberKategorie = @Id ORDER BY nSort", new { Id = oberKategorieId })
                : await conn.QueryAsync<Kategorie>("SELECT * FROM tKategorie WHERE kOberKategorie IS NULL OR kOberKategorie = 0 ORDER BY nSort");

            foreach (var k in kategorien)
            {
                k.Beschreibung = await conn.QuerySingleOrDefaultAsync<KategorieBeschreibung>(
                    "SELECT * FROM tKategorieSprache WHERE kKategorie = @Id AND kSprache = 1", new { Id = k.Id });
                k.Unterkategorien = (await GetKategorienAsync(k.Id)).AsList();
            }
            return kategorien;
        }

        public async Task<int> CreateKategorieAsync(Kategorie kategorie, string name, string? beschreibung = null)
        {
            var conn = await _db.GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                var id = await conn.QuerySingleAsync<int>(@"INSERT INTO tKategorie (kOberKategorie, nSort, nEbene, nAktiv)
                    VALUES (@OberKategorieId, @Sortierung, @Ebene, @Aktiv); SELECT SCOPE_IDENTITY();", kategorie, tx);
                await conn.ExecuteAsync(@"INSERT INTO tKategorieSprache (kKategorie, kSprache, cName, cBeschreibung)
                    VALUES (@KatId, 1, @Name, @Beschreibung)", new { KatId = id, Name = name, Beschreibung = beschreibung }, tx);
                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }
        #endregion

        #region Kundengruppen
        public async Task<IEnumerable<Kundengruppe>> GetKundengruppenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Kundengruppe>("SELECT * FROM tKundenGruppe ORDER BY cStandard DESC, cName");
        }

        public async Task<int> CreateKundengruppeAsync(Kundengruppe gruppe)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tKundenGruppe (cName, fRabatt, cStandard, nNettoPreise)
                VALUES (@Name, @Rabatt, @IstStandard, @NettoPreise); SELECT SCOPE_IDENTITY();", gruppe);
        }

        public async Task<IEnumerable<Kundenkategorie>> GetKundenkategorienAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Kundenkategorie>("SELECT * FROM tKundenKategorie ORDER BY cName");
        }
        #endregion

        #region Versandarten
        public async Task<IEnumerable<Versandart>> GetVersandartenAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tVersandArt" + (nurAktive ? " WHERE nAktiv = 1" : "") + " ORDER BY nSort, cName";
            return await conn.QueryAsync<Versandart>(sql);
        }

        public async Task<int> CreateVersandartAsync(Versandart versandart)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tVersandArt (cName, cAnbieter, cLieferzeit, fPreis, fVersandkostenfreiAb, nSort, nAktiv, cTrackingURL)
                VALUES (@Name, @Anbieter, @Lieferzeit, @Preis, @VersandkostenfreiAb, @Sortierung, @Aktiv, @TrackingUrl); SELECT SCOPE_IDENTITY();", versandart);
        }

        public async Task<IEnumerable<Versanddienstleister>> GetVersanddienstleisterAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Versanddienstleister>("SELECT * FROM tVersandDienstleister WHERE nAktiv = 1 ORDER BY cName");
        }
        #endregion

        #region Zahlungsarten
        public async Task<IEnumerable<Zahlungsart>> GetZahlungsartenAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tZahlungsArt" + (nurAktive ? " WHERE nAktiv = 1" : "") + " ORDER BY nSort, cName";
            return await conn.QueryAsync<Zahlungsart>(sql);
        }

        public async Task<int> CreateZahlungsartAsync(Zahlungsart zahlungsart)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tZahlungsArt (cName, cModulId, cKundenName, nSort, nAktiv, fAufpreis, fProzent, nNachnahme, nZahlungszielTage, nSkontoTage, fSkontoProzent)
                VALUES (@Name, @ModulId, @KundenName, @Sortierung, @Aktiv, @Aufpreis, @AufpreisProzent, @IstNachnahme, @ZahlungszielTage, @SkontoTage, @SkontoProzent); SELECT SCOPE_IDENTITY();", zahlungsart);
        }
        #endregion

        #region Hersteller
        public async Task<IEnumerable<Hersteller>> GetHerstellerAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Hersteller>("SELECT * FROM tHersteller ORDER BY nSort, cName");
        }

        public async Task<int> CreateHerstellerAsync(Hersteller hersteller)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tHersteller (cName, cSeo, cBildPfad, cHomepage, nSort, cBeschreibung)
                VALUES (@Name, @SeoUrl, @LogoPfad, @Homepage, @Sortierung, @Beschreibung); SELECT SCOPE_IDENTITY();", hersteller);
        }
        #endregion

        #region Lieferanten
        public async Task<IEnumerable<Lieferant>> GetLieferantenAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tLieferant" + (nurAktive ? " WHERE nAktiv = 1" : "") + " ORDER BY nStandard DESC, cFirma";
            return await conn.QueryAsync<Lieferant>(sql);
        }

        public async Task<Lieferant?> GetLieferantByIdAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Lieferant>("SELECT * FROM tLieferant WHERE kLieferant = @Id", new { Id = id });
        }

        public async Task<int> CreateLieferantAsync(Lieferant lieferant)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tLieferant (cFirma, cAnsprechpartner, cStrasse, cPLZ, cOrt, cLand, cTel, cFax, cMail, cWWW, cUStID, cIBAN, cBIC, cBank, cKundennummer, nStandard, nAktiv, fRabatt, nLieferzeitTage, fMindestbestellwert, nZahlungszielTage)
                VALUES (@Firma, @Ansprechpartner, @Strasse, @PLZ, @Ort, @Land, @Telefon, @Fax, @Email, @Website, @UStID, @IBAN, @BIC, @Bank, @Kundennummer, @IstStandard, @Aktiv, @Rabatt, @LieferzeitTage, @Mindestbestellwert, @ZahlungszielTage); SELECT SCOPE_IDENTITY();", lieferant);
        }

        public async Task UpdateLieferantAsync(Lieferant lieferant)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tLieferant SET cFirma=@Firma, cAnsprechpartner=@Ansprechpartner, cStrasse=@Strasse, cPLZ=@PLZ, cOrt=@Ort, cLand=@Land,
                cTel=@Telefon, cFax=@Fax, cMail=@Email, cWWW=@Website, cUStID=@UStID, cIBAN=@IBAN, cBIC=@BIC, cBank=@Bank,
                cKundennummer=@Kundennummer, nStandard=@IstStandard, nAktiv=@Aktiv, fRabatt=@Rabatt, nLieferzeitTage=@LieferzeitTage,
                fMindestbestellwert=@Mindestbestellwert, nZahlungszielTage=@ZahlungszielTage WHERE kLieferant=@Id", lieferant);
        }
        #endregion

        #region Merkmale
        public async Task<IEnumerable<Merkmal>> GetMerkmaleAsync()
        {
            var conn = await _db.GetConnectionAsync();
            var merkmale = await conn.QueryAsync<Merkmal>("SELECT * FROM tMerkmal ORDER BY nSort, cName");
            foreach (var m in merkmale)
                m.Werte = (await conn.QueryAsync<MerkmalWert>("SELECT * FROM tMerkmalWert WHERE kMerkmal = @Id ORDER BY nSort", new { Id = m.Id })).AsList();
            return merkmale;
        }

        public async Task<int> CreateMerkmalAsync(Merkmal merkmal)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tMerkmal (cName, cTyp, nSort, nGlobal, nMehrfachauswahl)
                VALUES (@Name, @Typ, @Sortierung, @IstGlobal, @Mehrfachauswahl); SELECT SCOPE_IDENTITY();", merkmal);
        }

        public async Task<int> CreateMerkmalWertAsync(MerkmalWert wert)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tMerkmalWert (kMerkmal, cWert, nSort, cSeo, cBildPfad)
                VALUES (@MerkmalId, @Wert, @Sortierung, @SeoUrl, @BildPfad); SELECT SCOPE_IDENTITY();", wert);
        }
        #endregion

        #region Eigenschaften (Variationen)
        public async Task<IEnumerable<Eigenschaft>> GetEigenschaftenAsync(int artikelId)
        {
            var conn = await _db.GetConnectionAsync();
            var eigenschaften = await conn.QueryAsync<Eigenschaft>("SELECT * FROM tEigenschaft WHERE kArtikel = @Id ORDER BY nSort", new { Id = artikelId });
            foreach (var e in eigenschaften)
                e.Werte = (await conn.QueryAsync<EigenschaftWert>("SELECT * FROM tEigenschaftWert WHERE kEigenschaft = @Id ORDER BY nSort", new { Id = e.Id })).AsList();
            return eigenschaften;
        }

        public async Task<int> CreateEigenschaftAsync(Eigenschaft eigenschaft)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"INSERT INTO tEigenschaft (kArtikel, cName, cTyp, nSort)
                VALUES (@ArtikelId, @Name, @Typ, @Sortierung); SELECT SCOPE_IDENTITY();", eigenschaft);
        }
        #endregion

        #region Mahnstufen
        public async Task<IEnumerable<Mahnstufe>> GetMahngruppenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Mahnstufe>("SELECT * FROM tMahnstufe WHERE nAktiv = 1 ORDER BY nStufe");
        }

        public async Task UpdateMahnstufeAsync(Mahnstufe stufe)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tMahnstufe SET cName=@Name, nTageNachFaelligkeit=@TageNachFaelligkeit, 
                fGebuehr=@Gebuehr, fZinsProzent=@ZinsProzent, cText=@Text, nAktiv=@Aktiv WHERE kMahnstufe=@Id", stufe);
        }
        #endregion

        #region Steuern
        public async Task<IEnumerable<Steuerklasse>> GetSteuerklassenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Steuerklasse>("SELECT * FROM tSteuerKlasse ORDER BY nStandard DESC, cName");
        }

        public async Task<IEnumerable<Steuersatz>> GetSteuersaetzeAsync(int steuerklasseId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Steuersatz>("SELECT * FROM tSteuerSatz WHERE kSteuerKlasse = @Id", new { Id = steuerklasseId });
        }
        #endregion

        #region Sprachen / Währungen / Länder
        public async Task<IEnumerable<Sprache>> GetSprachenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Sprache>("SELECT * FROM tSprache ORDER BY nStandard DESC, cName");
        }

        public async Task<IEnumerable<Waehrung>> GetWaehrungenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Waehrung>("SELECT * FROM tWaehrung ORDER BY nStandard DESC, cName");
        }

        public async Task<IEnumerable<Land>> GetLaenderAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tLand" + (nurAktive ? " WHERE nAktiv = 1" : "") + " ORDER BY cName";
            return await conn.QueryAsync<Land>(sql);
        }
        #endregion

        #region Nummernkreise
        public async Task<IEnumerable<Nummernkreis>> GetNummernkreiseAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Nummernkreis>("SELECT * FROM tNummernKreis ORDER BY cName");
        }

        public async Task UpdateNummernkreisAsync(Nummernkreis nk)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tNummernKreis SET cPrefix=@Prefix, cSuffix=@Suffix, nNummer=@AktuelleNummer, 
                nStellen=@Stellen, nJahresabhaengig=@Jahresabhaengig WHERE kNummernKreis=@Id", nk);
        }
        #endregion

        #region Eigene Felder
        public async Task<IEnumerable<EigenesFeld>> GetEigeneFelderAsync(string bereich)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<EigenesFeld>("SELECT * FROM tEigenesFeld WHERE cBereich = @Bereich ORDER BY nSort", new { Bereich = bereich });
        }

        public async Task<string?> GetEigenesFeldWertAsync(int feldId, int keyId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<string>("SELECT cWert FROM tEigenesFeldWert WHERE kEigenesFeld = @FeldId AND kKey = @KeyId",
                new { FeldId = feldId, KeyId = keyId });
        }

        public async Task SetEigenesFeldWertAsync(int feldId, int keyId, string wert)
        {
            var conn = await _db.GetConnectionAsync();
            var exists = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tEigenesFeldWert WHERE kEigenesFeld = @FeldId AND kKey = @KeyId",
                new { FeldId = feldId, KeyId = keyId });
            if (exists > 0)
                await conn.ExecuteAsync("UPDATE tEigenesFeldWert SET cWert = @Wert WHERE kEigenesFeld = @FeldId AND kKey = @KeyId",
                    new { Wert = wert, FeldId = feldId, KeyId = keyId });
            else
                await conn.ExecuteAsync("INSERT INTO tEigenesFeldWert (kEigenesFeld, kKey, cWert) VALUES (@FeldId, @KeyId, @Wert)",
                    new { FeldId = feldId, KeyId = keyId, Wert = wert });
        }
        #endregion
    }
}
