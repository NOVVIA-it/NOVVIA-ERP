-- =============================================
-- NOVVIA.spOrderCreateUpdateDelete
-- Erstellt, aktualisiert oder loescht Auftraege in JTL Verkauf-Tabellen
--
-- Verwendet:
--   Verkauf.tAuftrag
--   Verkauf.tAuftragAdresse
--   Verkauf.tAuftragPosition
--   Verkauf.spAuftragEckdatenBerechnen
--   dbo.tLaufendeNummern (kLaufendeNummer = 3 fuer Auftraege)
--
-- Erstellt: 2024-12-29
-- =============================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spOrderCreateUpdateDelete' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spOrderCreateUpdateDelete
GO

CREATE PROCEDURE NOVVIA.spOrderCreateUpdateDelete
    @Action NVARCHAR(10),           -- 'CREATE', 'UPDATE', 'DELETE'
    @kAuftrag INT = NULL OUTPUT,    -- Auftrag-ID (OUTPUT bei CREATE)
    @kKunde INT = NULL,             -- Kunde-ID
    @kBenutzer INT = 1,             -- Benutzer-ID (Default: 1)
    @kVersandArt INT = NULL,        -- Versandart
    @kZahlungsart INT = NULL,       -- Zahlungsart
    @cWaehrung NVARCHAR(3) = 'EUR', -- Waehrung

    -- Lieferadresse (nTyp = 1)
    @LAFirma NVARCHAR(255) = NULL,
    @LAAnrede NVARCHAR(50) = NULL,
    @LAVorname NVARCHAR(100) = NULL,
    @LAName NVARCHAR(100) = NULL,
    @LAStrasse NVARCHAR(255) = NULL,
    @LAPLZ NVARCHAR(20) = NULL,
    @LAOrt NVARCHAR(100) = NULL,
    @LALand NVARCHAR(100) = 'Deutschland',
    @LAISO NVARCHAR(2) = 'DE',
    @LATel NVARCHAR(50) = NULL,
    @LAMail NVARCHAR(255) = NULL,

    -- Rechnungsadresse (nTyp = 0) - optional, wenn NULL dann = Lieferadresse
    @RAFirma NVARCHAR(255) = NULL,
    @RAAnrede NVARCHAR(50) = NULL,
    @RAVorname NVARCHAR(100) = NULL,
    @RAName NVARCHAR(100) = NULL,
    @RAStrasse NVARCHAR(255) = NULL,
    @RAPLZ NVARCHAR(20) = NULL,
    @RAOrt NVARCHAR(100) = NULL,
    @RALand NVARCHAR(100) = NULL,
    @RAISO NVARCHAR(2) = NULL,
    @RATel NVARCHAR(50) = NULL,
    @RAMail NVARCHAR(255) = NULL,

    -- Storno-Grund (nur bei DELETE)
    @StornoGrund NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cAuftragsNr NVARCHAR(50);
    DECLARE @kSprache INT = 1;
    DECLARE @kFirma INT = 1;
    DECLARE @ErrorMessage NVARCHAR(4000);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- =============================================
        -- CREATE: Neuen Auftrag anlegen
        -- =============================================
        IF @Action = 'CREATE'
        BEGIN
            -- Validierung
            IF @kKunde IS NULL
            BEGIN
                RAISERROR('kKunde ist erforderlich fuer CREATE', 16, 1);
                RETURN;
            END

            -- 1. Naechste Auftragsnummer aus tLaufendeNummern holen
            DECLARE @nNummer INT;
            DECLARE @cPrefix NVARCHAR(50);
            DECLARE @cSuffix NVARCHAR(50);

            SELECT @nNummer = nNummer, @cPrefix = cPrefix, @cSuffix = cSuffix
            FROM dbo.tLaufendeNummern WITH (UPDLOCK)
            WHERE kLaufendeNummer = 3; -- Auftrag

            SET @nNummer = ISNULL(@nNummer, 10000) + 1;

            -- Nummer hochzaehlen
            UPDATE dbo.tLaufendeNummern
            SET nNummer = @nNummer
            WHERE kLaufendeNummer = 3;

            -- Auftragsnummer zusammenbauen
            SET @cAuftragsNr = ISNULL(@cPrefix, '') + CAST(@nNummer AS NVARCHAR(20)) + ISNULL(@cSuffix, '');

            -- 2. Kundendaten laden falls Adresse nicht angegeben
            IF @LAName IS NULL AND @LAFirma IS NULL
            BEGIN
                SELECT
                    @LAFirma = a.cFirma,
                    @LAAnrede = a.cAnrede,
                    @LAVorname = a.cVorname,
                    @LAName = a.cName,
                    @LAStrasse = a.cStrasse,
                    @LAPLZ = a.cPLZ,
                    @LAOrt = a.cOrt,
                    @LALand = ISNULL(a.cLand, 'Deutschland'),
                    @LAISO = ISNULL(a.cISO, 'DE'),
                    @LATel = a.cTel,
                    @LAMail = a.cMail
                FROM dbo.tAdresse a
                WHERE a.kKunde = @kKunde AND a.nStandard = 1;
            END

            -- 3. Auftrag anlegen
            INSERT INTO Verkauf.tAuftrag (
                kBenutzer, kBenutzerErstellt, kKunde, cAuftragsNr, nType, dErstellt,
                nBeschreibung, cWaehrung, fFaktor, kFirmaHistory, kSprache,
                nSteuereinstellung, nHatUpload, fZusatzGewicht,
                cVersandlandISO, cVersandlandWaehrung, fVersandlandWaehrungFaktor,
                nStorno, nKomplettAusgeliefert, nLieferPrioritaet, nPremiumVersand,
                nIstExterneRechnung, nIstReadOnly, nArchiv, nReserviert,
                nAuftragStatus, fFinanzierungskosten, nPending,
                kVersandArt, kZahlungsart
            ) VALUES (
                @kBenutzer, @kBenutzer, @kKunde, @cAuftragsNr, 1, GETDATE(),
                0, @cWaehrung, 1, @kFirma, @kSprache,
                0, 0, 0,
                @LAISO, @cWaehrung, 1,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0,
                @kVersandArt, @kZahlungsart
            );

            SET @kAuftrag = SCOPE_IDENTITY();

            -- 4. Rechnungsadresse anlegen (nTyp = 0)
            INSERT INTO Verkauf.tAuftragAdresse (
                kAuftrag, kKunde, nTyp,
                cFirma, cAnrede, cVorname, cName, cStrasse, cPLZ, cOrt, cLand, cISO, cTel, cMail
            ) VALUES (
                @kAuftrag, @kKunde, 0,
                ISNULL(@RAFirma, @LAFirma),
                ISNULL(@RAAnrede, @LAAnrede),
                ISNULL(@RAVorname, @LAVorname),
                ISNULL(@RAName, @LAName),
                ISNULL(@RAStrasse, @LAStrasse),
                ISNULL(@RAPLZ, @LAPLZ),
                ISNULL(@RAOrt, @LAOrt),
                ISNULL(@RALand, @LALand),
                ISNULL(@RAISO, @LAISO),
                ISNULL(@RATel, @LATel),
                ISNULL(@RAMail, @LAMail)
            );

            -- 5. Lieferadresse anlegen (nTyp = 1)
            INSERT INTO Verkauf.tAuftragAdresse (
                kAuftrag, kKunde, nTyp,
                cFirma, cAnrede, cVorname, cName, cStrasse, cPLZ, cOrt, cLand, cISO, cTel, cMail
            ) VALUES (
                @kAuftrag, @kKunde, 1,
                @LAFirma, @LAAnrede, @LAVorname, @LAName, @LAStrasse, @LAPLZ, @LAOrt, @LALand, @LAISO, @LATel, @LAMail
            );

            -- 6. Eckdaten berechnen (ueber JTL SP)
            DECLARE @AuftragTable Verkauf.TYPE_spAuftragEckdatenBerechnen;
            INSERT INTO @AuftragTable (kAuftrag) VALUES (@kAuftrag);
            EXEC Verkauf.spAuftragEckdatenBerechnen @Auftrag = @AuftragTable;

            PRINT 'Auftrag ' + @cAuftragsNr + ' (kAuftrag=' + CAST(@kAuftrag AS VARCHAR) + ') erstellt.';
        END

        -- =============================================
        -- UPDATE: Auftrag aktualisieren
        -- =============================================
        ELSE IF @Action = 'UPDATE'
        BEGIN
            IF @kAuftrag IS NULL
            BEGIN
                RAISERROR('kAuftrag ist erforderlich fuer UPDATE', 16, 1);
                RETURN;
            END

            -- Auftrag-Header aktualisieren
            UPDATE Verkauf.tAuftrag
            SET
                kVersandArt = ISNULL(@kVersandArt, kVersandArt),
                kZahlungsart = ISNULL(@kZahlungsart, kZahlungsart),
                kBenutzer = @kBenutzer
            WHERE kAuftrag = @kAuftrag;

            -- Lieferadresse aktualisieren (wenn angegeben)
            IF @LAName IS NOT NULL OR @LAFirma IS NOT NULL
            BEGIN
                UPDATE Verkauf.tAuftragAdresse
                SET
                    cFirma = ISNULL(@LAFirma, cFirma),
                    cAnrede = ISNULL(@LAAnrede, cAnrede),
                    cVorname = ISNULL(@LAVorname, cVorname),
                    cName = ISNULL(@LAName, cName),
                    cStrasse = ISNULL(@LAStrasse, cStrasse),
                    cPLZ = ISNULL(@LAPLZ, cPLZ),
                    cOrt = ISNULL(@LAOrt, cOrt),
                    cLand = ISNULL(@LALand, cLand),
                    cISO = ISNULL(@LAISO, cISO),
                    cTel = ISNULL(@LATel, cTel),
                    cMail = ISNULL(@LAMail, cMail)
                WHERE kAuftrag = @kAuftrag AND nTyp = 1;
            END

            -- Rechnungsadresse aktualisieren (wenn angegeben)
            IF @RAName IS NOT NULL OR @RAFirma IS NOT NULL
            BEGIN
                UPDATE Verkauf.tAuftragAdresse
                SET
                    cFirma = ISNULL(@RAFirma, cFirma),
                    cAnrede = ISNULL(@RAAnrede, cAnrede),
                    cVorname = ISNULL(@RAVorname, cVorname),
                    cName = ISNULL(@RAName, cName),
                    cStrasse = ISNULL(@RAStrasse, cStrasse),
                    cPLZ = ISNULL(@RAPLZ, cPLZ),
                    cOrt = ISNULL(@RAOrt, cOrt),
                    cLand = ISNULL(@RALand, cLand),
                    cISO = ISNULL(@RAISO, cISO),
                    cTel = ISNULL(@RATel, cTel),
                    cMail = ISNULL(@RAMail, cMail)
                WHERE kAuftrag = @kAuftrag AND nTyp = 0;
            END

            -- Eckdaten neu berechnen
            DECLARE @AuftragTableUpd Verkauf.TYPE_spAuftragEckdatenBerechnen;
            INSERT INTO @AuftragTableUpd (kAuftrag) VALUES (@kAuftrag);
            EXEC Verkauf.spAuftragEckdatenBerechnen @Auftrag = @AuftragTableUpd;

            PRINT 'Auftrag kAuftrag=' + CAST(@kAuftrag AS VARCHAR) + ' aktualisiert.';
        END

        -- =============================================
        -- DELETE: Auftrag stornieren (Soft Delete)
        -- =============================================
        ELSE IF @Action = 'DELETE'
        BEGIN
            IF @kAuftrag IS NULL
            BEGIN
                RAISERROR('kAuftrag ist erforderlich fuer DELETE', 16, 1);
                RETURN;
            END

            -- Pruefen ob bereits Lieferscheine/Rechnungen existieren
            IF EXISTS (SELECT 1 FROM dbo.tLieferschein WHERE kBestellung = @kAuftrag)
            BEGIN
                RAISERROR('Auftrag hat bereits Lieferscheine und kann nicht storniert werden.', 16, 1);
                RETURN;
            END

            -- Storno-Eintrag erstellen
            INSERT INTO Verkauf.tAuftragStorno (kAuftrag, kBenutzer, dStorniert, cKommentar)
            VALUES (@kAuftrag, @kBenutzer, GETDATE(), ISNULL(@StornoGrund, 'Storniert via NOVVIA'));

            -- Auftrag als storniert markieren
            UPDATE Verkauf.tAuftrag
            SET nStorno = 1,
                kBenutzer = @kBenutzer
            WHERE kAuftrag = @kAuftrag;

            -- Reservierungen aufheben
            UPDATE Verkauf.tAuftragPosition
            SET nReserviert = 0
            WHERE kAuftrag = @kAuftrag;

            PRINT 'Auftrag kAuftrag=' + CAST(@kAuftrag AS VARCHAR) + ' storniert.';
        END

        ELSE
        BEGIN
            RAISERROR('Ungueltige Action. Erlaubt: CREATE, UPDATE, DELETE', 16, 1);
            RETURN;
        END

        COMMIT TRANSACTION;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @ErrorMessage = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO

-- =============================================
-- NOVVIA.spOrderPositionAddUpdate
-- Fuegt Positionen zu einem Auftrag hinzu oder aktualisiert sie
-- =============================================

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spOrderPositionAddUpdate' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spOrderPositionAddUpdate
GO

CREATE PROCEDURE NOVVIA.spOrderPositionAddUpdate
    @Action NVARCHAR(10),               -- 'ADD', 'UPDATE', 'DELETE'
    @kAuftrag INT,                      -- Auftrag-ID
    @kAuftragPosition INT = NULL OUTPUT, -- Position-ID (OUTPUT bei ADD)
    @kArtikel INT = NULL,               -- Artikel-ID
    @cArtNr NVARCHAR(50) = NULL,        -- Artikelnummer
    @cName NVARCHAR(255) = NULL,        -- Artikelname (wird aus tArtikel geladen wenn NULL)
    @fAnzahl DECIMAL(18,4) = 1,         -- Menge
    @fVkNetto DECIMAL(18,4) = NULL,     -- VK Netto (wird aus tArtikel geladen wenn NULL)
    @fRabatt DECIMAL(18,4) = 0,         -- Rabatt in %
    @fMwSt DECIMAL(18,4) = NULL,        -- MwSt-Satz (wird aus tSteuersatz geladen wenn NULL)
    @cHinweis NVARCHAR(MAX) = NULL,     -- Positionshinweis
    @cEinheit NVARCHAR(20) = 'Stk'      -- Einheit
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ErrorMessage NVARCHAR(4000);
    DECLARE @nSort INT;

    BEGIN TRY
        -- Validierung
        IF @kAuftrag IS NULL
        BEGIN
            RAISERROR('kAuftrag ist erforderlich', 16, 1);
            RETURN;
        END

        -- Artikeldaten laden wenn nicht angegeben
        IF @kArtikel IS NOT NULL AND (@cName IS NULL OR @fVkNetto IS NULL OR @fMwSt IS NULL)
        BEGIN
            SELECT
                @cArtNr = ISNULL(@cArtNr, a.cArtNr),
                @cName = ISNULL(@cName, ab.cName),
                @fVkNetto = ISNULL(@fVkNetto, a.fVKNetto),
                @fMwSt = ISNULL(@fMwSt, ISNULL(s.fSteuersatz, 19))
            FROM dbo.tArtikel a
            LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
            LEFT JOIN dbo.tSteuersatz s ON a.kSteuerklasse = s.kSteuerklasse AND s.kSteuerzone = 3
            WHERE a.kArtikel = @kArtikel;
        END

        -- Defaults
        SET @fVkNetto = ISNULL(@fVkNetto, 0);
        SET @fMwSt = ISNULL(@fMwSt, 19);
        SET @cName = ISNULL(@cName, '');

        -- =============================================
        -- ADD: Position hinzufuegen
        -- =============================================
        IF @Action = 'ADD'
        BEGIN
            -- Naechste Sortierung ermitteln
            SELECT @nSort = ISNULL(MAX(nSort), 0) + 1
            FROM Verkauf.tAuftragPosition
            WHERE kAuftrag = @kAuftrag;

            INSERT INTO Verkauf.tAuftragPosition (
                kAuftrag, kArtikel, cArtNr, nReserviert, cName, cHinweis,
                fAnzahl, fEkNetto, fVkNetto, fRabatt, fMwSt, nSort,
                cNameStandard, nType, cEinheit, nHatUpload, fFaktor
            ) VALUES (
                @kAuftrag, @kArtikel, @cArtNr, 0, @cName, @cHinweis,
                @fAnzahl, 0, @fVkNetto, @fRabatt, @fMwSt, @nSort,
                @cName, 0, @cEinheit, 0, 1
            );

            SET @kAuftragPosition = SCOPE_IDENTITY();
        END

        -- =============================================
        -- UPDATE: Position aktualisieren
        -- =============================================
        ELSE IF @Action = 'UPDATE'
        BEGIN
            IF @kAuftragPosition IS NULL
            BEGIN
                RAISERROR('kAuftragPosition ist erforderlich fuer UPDATE', 16, 1);
                RETURN;
            END

            UPDATE Verkauf.tAuftragPosition
            SET
                kArtikel = ISNULL(@kArtikel, kArtikel),
                cArtNr = ISNULL(@cArtNr, cArtNr),
                cName = ISNULL(@cName, cName),
                fAnzahl = @fAnzahl,
                fVkNetto = ISNULL(@fVkNetto, fVkNetto),
                fRabatt = @fRabatt,
                fMwSt = ISNULL(@fMwSt, fMwSt),
                cHinweis = @cHinweis,
                cEinheit = @cEinheit
            WHERE kAuftragPosition = @kAuftragPosition;
        END

        -- =============================================
        -- DELETE: Position loeschen
        -- =============================================
        ELSE IF @Action = 'DELETE'
        BEGIN
            IF @kAuftragPosition IS NULL
            BEGIN
                RAISERROR('kAuftragPosition ist erforderlich fuer DELETE', 16, 1);
                RETURN;
            END

            DELETE FROM Verkauf.tAuftragPosition
            WHERE kAuftragPosition = @kAuftragPosition;
        END

        ELSE
        BEGIN
            RAISERROR('Ungueltige Action. Erlaubt: ADD, UPDATE, DELETE', 16, 1);
            RETURN;
        END

        -- Eckdaten neu berechnen
        DECLARE @AuftragTable Verkauf.TYPE_spAuftragEckdatenBerechnen;
        INSERT INTO @AuftragTable (kAuftrag) VALUES (@kAuftrag);
        EXEC Verkauf.spAuftragEckdatenBerechnen @Auftrag = @AuftragTable;

    END TRY
    BEGIN CATCH
        SET @ErrorMessage = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
    END CATCH
END
GO

-- =============================================
-- Beispiel-Aufrufe
-- =============================================
/*
-- Neuen Auftrag erstellen
DECLARE @NewAuftragId INT;
EXEC NOVVIA.spOrderCreateUpdateDelete
    @Action = 'CREATE',
    @kAuftrag = @NewAuftragId OUTPUT,
    @kKunde = 123,
    @kBenutzer = 1,
    @LAFirma = 'Test GmbH',
    @LAName = 'Mueller',
    @LAStrasse = 'Teststr. 1',
    @LAPLZ = '12345',
    @LAOrt = 'Berlin';

SELECT @NewAuftragId AS NeuerAuftrag;

-- Position hinzufuegen
DECLARE @NewPosId INT;
EXEC NOVVIA.spOrderPositionAddUpdate
    @Action = 'ADD',
    @kAuftrag = @NewAuftragId,
    @kAuftragPosition = @NewPosId OUTPUT,
    @kArtikel = 456,
    @fAnzahl = 5;

-- Auftrag stornieren
EXEC NOVVIA.spOrderCreateUpdateDelete
    @Action = 'DELETE',
    @kAuftrag = @NewAuftragId,
    @StornoGrund = 'Kunde hat storniert';
*/

PRINT 'NOVVIA Order Stored Procedures erstellt.';
GO
