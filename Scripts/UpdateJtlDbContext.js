const fs = require('fs');
const path = 'C:/NovviaERP/src/NovviaERP/NovviaERP.Core/Data/JtlDbContext.cs';

let content = fs.readFileSync(path, 'utf8');

// Find and replace the Bestellung region
const regionStart = content.indexOf('#region Bestellung');
const regionEnd = content.indexOf('#region Rechnung');

if (regionStart === -1 || regionEnd === -1) {
    console.log('Region markers not found!');
    process.exit(1);
}

const beforeRegion = content.substring(0, regionStart);
const afterRegion = content.substring(regionEnd);

const newBestellungRegion = `#region Bestellung (JTL-Schema: Verkauf.tAuftrag)

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

            // Kunde laden für Adressdaten
            var kunde = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT k.kKunde, k.kSprache, k.kKundenGruppe,
                       a.cFirma, a.cAnrede, a.cTitel, a.cVorname, a.cName, a.cStrasse,
                       a.cAdressZusatz, a.cPLZ, a.cOrt, a.cLand, a.cISO, a.cBundesland,
                       a.cTel, a.cMobil, a.cMail
                FROM dbo.tKunde k
                LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                WHERE k.kKunde = @KundeId", new { KundeId = b.KundeId });

            // Auftrag anlegen
            var auftragId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO Verkauf.tAuftrag (
                    kBenutzer, kBenutzerErstellt, kKunde, cAuftragsnr, nType, dErstellt,
                    nBeschreibung, cWaehrung, fFaktor, kFirmaHistory, kSprache,
                    nSteuereinstellung, nHatUpload, fZusatzGewicht,
                    cVersandlandISO, cVersandlandWaehrung, fVersandlandWaehrungFaktor,
                    nStorno, nKomplettAusgeliefert, nLieferPrioritaet, nPremiumVersand,
                    nIstExterneRechnung, nIstReadOnly, nArchiv, nReserviert,
                    nAuftragStatus, fFinanzierungskosten, nPending, nSteuersonderbehandlung,
                    cInternerKommentar
                ) VALUES (
                    1, 1, @KundeId, @AuftragNr, 1, GETDATE(),
                    0, @Waehrung, 1, 1, ISNULL(@Sprache, 1),
                    0, 0, 0,
                    ISNULL(@LandISO, 'DE'), 'EUR', 1,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    @Status, 0, 0, 0,
                    @Kommentar
                );
                SELECT SCOPE_IDENTITY();",
                new {
                    b.KundeId,
                    AuftragNr = b.BestellNr,
                    Waehrung = b.Waehrung ?? "EUR",
                    Sprache = (int?)kunde?.kSprache,
                    LandISO = (string?)kunde?.cISO ?? "DE",
                    Status = b.Status > 0 ? b.Status : 1,
                    Kommentar = b.InternerKommentar
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
                // Artikeldaten laden wenn nötig
                var artikel = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT a.kArtikel, a.cArtNr, ab.cName, a.fVKNetto, a.fMwSt
                    FROM dbo.tArtikel a
                    LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                    WHERE a.kArtikel = @ArtikelId", new { pos.ArtikelId });

                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragPosition (
                        kArtikel, kAuftrag, cArtNr, nReserviert, cName, cHinweis,
                        fAnzahl, fEkNetto, fVkNetto, fRabatt, fMwSt, nSort,
                        cNameStandard, nType, cEinheit, nHatUpload, fFaktor
                    ) VALUES (
                        @ArtikelId, @AuftragId, @ArtNr, 0, @Name, '',
                        @Menge, 0, @VKNetto, @Rabatt, @MwSt, @Sort,
                        @Name, 0, 'Stk', 0, 1
                    )",
                    new {
                        pos.ArtikelId,
                        AuftragId = auftragId,
                        ArtNr = pos.ArtNr ?? (string?)artikel?.cArtNr ?? "",
                        Name = pos.Name ?? (string?)artikel?.cName ?? "",
                        pos.Menge,
                        VKNetto = pos.VKNetto > 0 ? pos.VKNetto : (decimal?)artikel?.fVKNetto ?? 0,
                        Rabatt = pos.Rabatt,
                        MwSt = pos.MwSt > 0 ? pos.MwSt : (decimal?)artikel?.fMwSt ?? 19,
                        Sort = posSort++
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

        `;

content = beforeRegion + newBestellungRegion + afterRegion;

fs.writeFileSync(path, content);
console.log('JtlDbContext.cs updated successfully!');
