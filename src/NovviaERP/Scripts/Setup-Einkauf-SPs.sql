-- ============================================
-- NOVVIA ERP - Einkauf Stored Procedures
-- Nutzt JTL SPs wo vorhanden, erweitert um MSV3/ABdata
-- ============================================

USE Mandant_3;
GO

-- ============================================
-- WRAPPER: Lieferantenbestellung erstellen (nutzt JTL SP)
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_LieferantenBestellungErstellen' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_LieferantenBestellungErstellen;
GO

CREATE PROCEDURE spNOVVIA_LieferantenBestellungErstellen
    @kLieferant INT,
    @kBenutzer INT,
    @kFirma INT = 1,
    @kLager INT = 1,
    @cEigeneBestellnummer NVARCHAR(255) = NULL,
    @cInternerKommentar NVARCHAR(MAX) = NULL,
    @dLieferdatum DATETIME = NULL,
    @nViaMSV3 BIT = 0,                    -- NOVVIA: MSV3 Bestellung?
    @kLieferantenBestellung INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- JTL SP aufrufen
    EXEC [Lieferantenbestellung].[spLieferantenBestellungErstellen]
        @kLieferant = @kLieferant,
        @kSprache = 1,
        @kLieferantenBestellungRA = 0,
        @kLieferantenBestellungLA = 0,
        @cWaehrungISO = N'EUR',
        @cInternerKommentar = @cInternerKommentar,
        @cDruckAnmerkung = NULL,
        @nStatus = 1,  -- Offen
        @kFirma = @kFirma,
        @kLager = @kLager,
        @kKunde = 0,
        @dLieferdatum = @dLieferdatum,
        @cEigeneBestellnummer = @cEigeneBestellnummer,
        @cBezugsAuftragsnummer = NULL,
        @nDropShipping = 0,
        @kLieferantenBestellungLieferant = 0,
        @kBenutzer = @kBenutzer,
        @fFaktor = 1.0,
        @cFremdbelegnummer = NULL,
        @kLieferschein = 0,
        @nBestaetigt = 0,
        @istGedruckt = 0,
        @istGemailt = 0,
        @istGefaxt = 0,
        @nAngelegtDurchWMS = 0,
        @xLieferantenbestellungPos = NULL,
        @kLieferantenbestellung = @kLieferantenBestellung OUTPUT;

    -- MSV3 Tracking anlegen wenn gewünscht
    IF @nViaMSV3 = 1
    BEGIN
        DECLARE @kMSV3Lieferant INT;
        SELECT @kMSV3Lieferant = kMSV3Lieferant FROM NOVVIA.MSV3Lieferant WHERE kLieferant = @kLieferant AND nAktiv = 1;

        IF @kMSV3Lieferant IS NOT NULL
        BEGIN
            INSERT INTO NOVVIA.MSV3Bestellung (kLieferantenBestellung, kMSV3Lieferant, cMSV3Status, nAnzahlPositionen)
            VALUES (@kLieferantenBestellung, @kMSV3Lieferant, 'VORBEREITET', 0);
        END
    END
END
GO

PRINT 'SP spNOVVIA_LieferantenBestellungErstellen erstellt (nutzt JTL SP)';
GO

-- ============================================
-- WRAPPER: Position hinzufügen (nutzt JTL SP)
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_LieferantenBestellungPosErstellen' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_LieferantenBestellungPosErstellen;
GO

CREATE PROCEDURE spNOVVIA_LieferantenBestellungPosErstellen
    @kLieferantenBestellung INT,
    @kArtikel INT,
    @fMenge DECIMAL(18,4),
    @fEKNetto DECIMAL(18,4) = NULL,
    @cHinweis NVARCHAR(2000) = NULL,
    @kLieferantenBestellungPos INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Artikeldaten holen
    DECLARE @cArtNr NVARCHAR(255), @cName NVARCHAR(255), @fUST DECIMAL(18,4);
    DECLARE @cLieferantenArtNr NVARCHAR(255), @kLieferant INT;

    SELECT @kLieferant = kLieferant FROM tLieferantenBestellung WHERE kLieferantenBestellung = @kLieferantenBestellung;

    SELECT @cArtNr = a.cArtNr, @cName = ab.cName, @fUST = 19.0
    FROM tArtikel a
    LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
    WHERE a.kArtikel = @kArtikel;

    -- Lieferanten-Artikelnummer holen
    SELECT TOP 1 @fEKNetto = COALESCE(@fEKNetto, fEKNetto)
    FROM tLiefArtikel WHERE tArtikel_kArtikel = @kArtikel AND tLieferant_kLieferant = @kLieferant;

    -- Nächste Sortierung
    DECLARE @nSort INT;
    SELECT @nSort = ISNULL(MAX(nSort), 0) + 1 FROM tLieferantenBestellungPos WHERE kLieferantenBestellung = @kLieferantenBestellung;

    -- JTL SP aufrufen
    EXEC [Lieferantenbestellung].[spLieferantenBestellungPosErstellen]
        @kLieferantenbestellung = @kLieferantenBestellung,
        @xLieferantenbestellungPos = NULL,
        @nStatus = NULL,
        @kArtikel = @kArtikel,
        @cArtNr = @cArtNr,
        @cLieferantenArtNr = @cLieferantenArtNr,
        @cName = @cName,
        @cLieferantenBezeichnung = NULL,
        @fUST = @fUST,
        @fMenge = @fMenge,
        @cHinweis = @cHinweis,
        @fEKNetto = @fEKNetto,
        @nPosTyp = 0,
        @cNameLieferant = NULL,
        @nLiefertage = NULL,
        @dLieferdatum = NULL,
        @nSort = @nSort,
        @kLieferscheinPos = NULL,
        @cVPEEinheit = NULL,
        @nVPEMenge = NULL,
        @kLieferantenbestellungPos = @kLieferantenBestellungPos OUTPUT;

    -- MSV3 Position anlegen wenn MSV3-Bestellung
    IF EXISTS (SELECT 1 FROM NOVVIA.MSV3Bestellung WHERE kLieferantenBestellung = @kLieferantenBestellung)
    BEGIN
        DECLARE @kMSV3Bestellung INT, @cPZN NVARCHAR(20);

        SELECT @kMSV3Bestellung = kMSV3Bestellung FROM NOVVIA.MSV3Bestellung WHERE kLieferantenBestellung = @kLieferantenBestellung;

        -- PZN ermitteln
        SELECT @cPZN = COALESCE(am.cPZN, a.cBarcode, a.cHAN)
        FROM tArtikel a
        LEFT JOIN NOVVIA.ABdataArtikelMapping am ON a.kArtikel = am.kArtikel
        WHERE a.kArtikel = @kArtikel;

        INSERT INTO NOVVIA.MSV3BestellungPos (kMSV3Bestellung, kLieferantenBestellungPos, cPZN, fMengeBestellt, cStatus)
        VALUES (@kMSV3Bestellung, @kLieferantenBestellungPos, @cPZN, @fMenge, 'VORBEREITET');

        -- Anzahl aktualisieren
        UPDATE NOVVIA.MSV3Bestellung SET nAnzahlPositionen = nAnzahlPositionen + 1 WHERE kMSV3Bestellung = @kMSV3Bestellung;
    END
END
GO

PRINT 'SP spNOVVIA_LieferantenBestellungPosErstellen erstellt (nutzt JTL SP)';
GO

-- ============================================
-- WRAPPER: Status ändern (nutzt JTL SP)
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_LieferantenBestellungStatusAendern' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_LieferantenBestellungStatusAendern;
GO

CREATE PROCEDURE spNOVVIA_LieferantenBestellungStatusAendern
    @kLieferantenBestellung INT,
    @nStatus INT  -- 1=Offen, 2=InBearbeitung, 3=Geliefert, 4=Abgeschlossen
AS
BEGIN
    SET NOCOUNT ON;

    -- JTL SP aufrufen
    EXEC [Lieferantenbestellung].[spLieferantenBestellungStatusAendern]
        @kLieferantenBestellung = @kLieferantenBestellung,
        @nStatus = @nStatus;
END
GO

PRINT 'SP spNOVVIA_LieferantenBestellungStatusAendern erstellt (nutzt JTL SP)';
GO

-- ============================================
-- MSV3: Lieferant Konfiguration speichern
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_MSV3LieferantSpeichern' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_MSV3LieferantSpeichern;
GO

CREATE PROCEDURE spNOVVIA_MSV3LieferantSpeichern
    @kLieferant INT,
    @cMSV3Url NVARCHAR(500),
    @cMSV3Benutzer NVARCHAR(100),
    @cMSV3Passwort NVARCHAR(255),
    @cMSV3Kundennummer NVARCHAR(50) = NULL,
    @cMSV3Filiale NVARCHAR(20) = NULL,
    @nMSV3Version INT = 1,
    @nPrioritaet INT = 1,
    @kMSV3Lieferant INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.MSV3Lieferant WHERE kLieferant = @kLieferant)
    BEGIN
        UPDATE NOVVIA.MSV3Lieferant SET
            cMSV3Url = @cMSV3Url,
            cMSV3Benutzer = @cMSV3Benutzer,
            cMSV3Passwort = @cMSV3Passwort,
            cMSV3Kundennummer = @cMSV3Kundennummer,
            cMSV3Filiale = @cMSV3Filiale,
            nMSV3Version = @nMSV3Version,
            nPrioritaet = @nPrioritaet,
            dGeaendert = GETDATE()
        WHERE kLieferant = @kLieferant;

        SELECT @kMSV3Lieferant = kMSV3Lieferant FROM NOVVIA.MSV3Lieferant WHERE kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.MSV3Lieferant (kLieferant, cMSV3Url, cMSV3Benutzer, cMSV3Passwort, cMSV3Kundennummer, cMSV3Filiale, nMSV3Version, nPrioritaet)
        VALUES (@kLieferant, @cMSV3Url, @cMSV3Benutzer, @cMSV3Passwort, @cMSV3Kundennummer, @cMSV3Filiale, @nMSV3Version, @nPrioritaet);

        SET @kMSV3Lieferant = SCOPE_IDENTITY();
    END
END
GO

PRINT 'SP spNOVVIA_MSV3LieferantSpeichern erstellt';
GO

-- ============================================
-- MSV3: Bestellung Status Update (nach API-Call)
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_MSV3BestellungStatusUpdate' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_MSV3BestellungStatusUpdate;
GO

CREATE PROCEDURE spNOVVIA_MSV3BestellungStatusUpdate
    @kMSV3Bestellung INT,
    @cMSV3Status NVARCHAR(50),
    @cMSV3AuftragsId NVARCHAR(100) = NULL,
    @cResponseXML NVARCHAR(MAX) = NULL,
    @cFehler NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE NOVVIA.MSV3Bestellung SET
        cMSV3Status = @cMSV3Status,
        cMSV3AuftragsId = COALESCE(@cMSV3AuftragsId, cMSV3AuftragsId),
        cResponseXML = @cResponseXML,
        cFehler = @cFehler,
        dGesendet = CASE WHEN @cMSV3Status = 'GESENDET' THEN GETDATE() ELSE dGesendet END,
        dBestaetigt = CASE WHEN @cMSV3Status = 'BESTAETIGT' THEN GETDATE() ELSE dBestaetigt END,
        dLieferung = CASE WHEN @cMSV3Status = 'GELIEFERT' THEN GETDATE() ELSE dLieferung END
    WHERE kMSV3Bestellung = @kMSV3Bestellung;

    -- Bei Bestätigung JTL-Status auf InBearbeitung setzen
    IF @cMSV3Status = 'BESTAETIGT'
    BEGIN
        DECLARE @kLieferantenBestellung INT;
        SELECT @kLieferantenBestellung = kLieferantenBestellung FROM NOVVIA.MSV3Bestellung WHERE kMSV3Bestellung = @kMSV3Bestellung;

        EXEC [Lieferantenbestellung].[spLieferantenBestellungStatusAendern] @kLieferantenBestellung, 2;
    END
END
GO

PRINT 'SP spNOVVIA_MSV3BestellungStatusUpdate erstellt';
GO

-- ============================================
-- MSV3: Position Verfügbarkeit Update
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_MSV3PositionVerfuegbarkeitUpdate' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_MSV3PositionVerfuegbarkeitUpdate;
GO

CREATE PROCEDURE spNOVVIA_MSV3PositionVerfuegbarkeitUpdate
    @kMSV3BestellungPos INT,
    @fMengeVerfuegbar DECIMAL(18,4),
    @fPreisEK DECIMAL(18,4) = NULL,
    @cStatus NVARCHAR(50),
    @cChargenNr NVARCHAR(50) = NULL,
    @dMHD DATE = NULL,
    @cHinweis NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE NOVVIA.MSV3BestellungPos SET
        fMengeVerfuegbar = @fMengeVerfuegbar,
        fPreisEK = @fPreisEK,
        cStatus = @cStatus,
        cChargenNr = @cChargenNr,
        dMHD = @dMHD,
        cHinweis = @cHinweis
    WHERE kMSV3BestellungPos = @kMSV3BestellungPos;

    -- Statistik aktualisieren
    DECLARE @kMSV3Bestellung INT;
    SELECT @kMSV3Bestellung = kMSV3Bestellung FROM NOVVIA.MSV3BestellungPos WHERE kMSV3BestellungPos = @kMSV3BestellungPos;

    UPDATE NOVVIA.MSV3Bestellung SET
        nAnzahlVerfuegbar = (SELECT COUNT(*) FROM NOVVIA.MSV3BestellungPos WHERE kMSV3Bestellung = @kMSV3Bestellung AND cStatus = 'VERFUEGBAR'),
        nAnzahlNichtVerfuegbar = (SELECT COUNT(*) FROM NOVVIA.MSV3BestellungPos WHERE kMSV3Bestellung = @kMSV3Bestellung AND cStatus = 'NICHT_VERFUEGBAR')
    WHERE kMSV3Bestellung = @kMSV3Bestellung;

    -- EK-Preis in JTL-Position aktualisieren wenn geliefert
    IF @fPreisEK IS NOT NULL
    BEGIN
        DECLARE @kLieferantenBestellungPos INT;
        SELECT @kLieferantenBestellungPos = kLieferantenBestellungPos FROM NOVVIA.MSV3BestellungPos WHERE kMSV3BestellungPos = @kMSV3BestellungPos;

        UPDATE tLieferantenBestellungPos SET fEKNetto = @fPreisEK WHERE kLieferantenBestellungPos = @kLieferantenBestellungPos;
    END
END
GO

PRINT 'SP spNOVVIA_MSV3PositionVerfuegbarkeitUpdate erstellt';
GO

-- ============================================
-- ABdata: Artikel Import/Update
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_ABdataArtikelUpsert' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_ABdataArtikelUpsert;
GO

CREATE PROCEDURE spNOVVIA_ABdataArtikelUpsert
    @cPZN NVARCHAR(20),
    @cName NVARCHAR(500),
    @cHersteller NVARCHAR(255) = NULL,
    @cDarreichungsform NVARCHAR(100) = NULL,
    @cPackungsgroesse NVARCHAR(50) = NULL,
    @fMenge DECIMAL(18,4) = NULL,
    @cEinheit NVARCHAR(50) = NULL,
    @fAEP DECIMAL(18,4) = NULL,
    @fAVP DECIMAL(18,4) = NULL,
    @fAEK DECIMAL(18,4) = NULL,
    @nRezeptpflicht TINYINT = 0,
    @nBTM TINYINT = 0,
    @nKuehlpflichtig TINYINT = 0,
    @cATC NVARCHAR(20) = NULL,
    @cWirkstoff NVARCHAR(500) = NULL,
    @dGueltigAb DATE = NULL,
    @dGueltigBis DATE = NULL,
    @nIsNew BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikel WHERE cPZN = @cPZN)
    BEGIN
        UPDATE NOVVIA.ABdataArtikel SET
            cName = @cName,
            cHersteller = @cHersteller,
            cDarreichungsform = @cDarreichungsform,
            cPackungsgroesse = @cPackungsgroesse,
            fMenge = @fMenge,
            cEinheit = @cEinheit,
            fAEP = @fAEP,
            fAVP = @fAVP,
            fAEK = @fAEK,
            nRezeptpflicht = @nRezeptpflicht,
            nBTM = @nBTM,
            nKuehlpflichtig = @nKuehlpflichtig,
            cATC = @cATC,
            cWirkstoff = @cWirkstoff,
            dGueltigAb = @dGueltigAb,
            dGueltigBis = @dGueltigBis,
            dGeaendert = GETDATE()
        WHERE cPZN = @cPZN;
        SET @nIsNew = 0;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.ABdataArtikel (cPZN, cName, cHersteller, cDarreichungsform, cPackungsgroesse, fMenge, cEinheit, fAEP, fAVP, fAEK, nRezeptpflicht, nBTM, nKuehlpflichtig, cATC, cWirkstoff, dGueltigAb, dGueltigBis)
        VALUES (@cPZN, @cName, @cHersteller, @cDarreichungsform, @cPackungsgroesse, @fMenge, @cEinheit, @fAEP, @fAVP, @fAEK, @nRezeptpflicht, @nBTM, @nKuehlpflichtig, @cATC, @cWirkstoff, @dGueltigAb, @dGueltigBis);
        SET @nIsNew = 1;
    END
END
GO

PRINT 'SP spNOVVIA_ABdataArtikelUpsert erstellt';
GO

-- ============================================
-- ABdata: Auto-Mapping PZN zu JTL-Artikel
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_ABdataAutoMapping' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_ABdataAutoMapping;
GO

CREATE PROCEDURE spNOVVIA_ABdataAutoMapping
    @nAnzahlGemappt INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @nAnzahlGemappt = 0;

    -- Mapping via Barcode = PZN
    INSERT INTO NOVVIA.ABdataArtikelMapping (kArtikel, cPZN, nAutomatisch)
    SELECT DISTINCT a.kArtikel, ab.cPZN, 1
    FROM tArtikel a
    INNER JOIN NOVVIA.ABdataArtikel ab ON a.cBarcode = ab.cPZN
    WHERE NOT EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikelMapping m WHERE m.kArtikel = a.kArtikel);

    SET @nAnzahlGemappt = @nAnzahlGemappt + @@ROWCOUNT;

    -- Mapping via HAN = PZN
    INSERT INTO NOVVIA.ABdataArtikelMapping (kArtikel, cPZN, nAutomatisch)
    SELECT DISTINCT a.kArtikel, ab.cPZN, 1
    FROM tArtikel a
    INNER JOIN NOVVIA.ABdataArtikel ab ON a.cHAN = ab.cPZN
    WHERE NOT EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikelMapping m WHERE m.kArtikel = a.kArtikel);

    SET @nAnzahlGemappt = @nAnzahlGemappt + @@ROWCOUNT;
END
GO

PRINT 'SP spNOVVIA_ABdataAutoMapping erstellt';
GO

-- ============================================
-- Eingangsrechnung: Erweitert speichern
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_EingangsrechnungErweitertSpeichern' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_EingangsrechnungErweitertSpeichern;
GO

CREATE PROCEDURE spNOVVIA_EingangsrechnungErweitertSpeichern
    @kEingangsrechnung INT,
    @nSkontoTage INT = NULL,
    @fSkontoProzent DECIMAL(5,2) = NULL,
    @cZahlungsreferenz NVARCHAR(100) = NULL,
    @cBankverbindung NVARCHAR(500) = NULL,
    @cDokumentPfad NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @dSkontoFrist DATE, @cSkontoBetrag DECIMAL(18,4);

    -- Skonto berechnen
    IF @nSkontoTage IS NOT NULL AND @fSkontoProzent IS NOT NULL
    BEGIN
        SELECT @dSkontoFrist = DATEADD(DAY, @nSkontoTage, dBelegdatum) FROM tEingangsrechnung WHERE kEingangsrechnung = @kEingangsrechnung;

        SELECT @cSkontoBetrag = SUM(fMenge * fEKNetto) * (@fSkontoProzent / 100.0)
        FROM tEingangsrechnungPos WHERE kEingangsrechnung = @kEingangsrechnung;
    END

    IF EXISTS (SELECT 1 FROM NOVVIA.EingangsrechnungErweitert WHERE kEingangsrechnung = @kEingangsrechnung)
    BEGIN
        UPDATE NOVVIA.EingangsrechnungErweitert SET
            cSkontoBetrag = COALESCE(@cSkontoBetrag, cSkontoBetrag),
            nSkontoTage = COALESCE(@nSkontoTage, nSkontoTage),
            fSkontoProzent = COALESCE(@fSkontoProzent, fSkontoProzent),
            cZahlungsreferenz = COALESCE(@cZahlungsreferenz, cZahlungsreferenz),
            cBankverbindung = COALESCE(@cBankverbindung, cBankverbindung),
            dSkontoFrist = COALESCE(@dSkontoFrist, dSkontoFrist),
            cDokumentPfad = COALESCE(@cDokumentPfad, cDokumentPfad)
        WHERE kEingangsrechnung = @kEingangsrechnung;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.EingangsrechnungErweitert (kEingangsrechnung, cSkontoBetrag, nSkontoTage, fSkontoProzent, cZahlungsreferenz, cBankverbindung, dSkontoFrist, cDokumentPfad)
        VALUES (@kEingangsrechnung, @cSkontoBetrag, @nSkontoTage, @fSkontoProzent, @cZahlungsreferenz, @cBankverbindung, @dSkontoFrist, @cDokumentPfad);
    END
END
GO

PRINT 'SP spNOVVIA_EingangsrechnungErweitertSpeichern erstellt';
GO

-- ============================================
-- Eingangsrechnung: Prüfen und Freigeben
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_EingangsrechnungPruefenUndFreigeben' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_EingangsrechnungPruefenUndFreigeben;
GO

CREATE PROCEDURE spNOVVIA_EingangsrechnungPruefenUndFreigeben
    @kEingangsrechnung INT,
    @kBenutzer INT,
    @nNurPruefen BIT = 0,  -- 0=Prüfen+Freigeben, 1=Nur Prüfen
    @cPruefHinweis NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Erweitert-Datensatz anlegen falls nicht vorhanden
    IF NOT EXISTS (SELECT 1 FROM NOVVIA.EingangsrechnungErweitert WHERE kEingangsrechnung = @kEingangsrechnung)
    BEGIN
        INSERT INTO NOVVIA.EingangsrechnungErweitert (kEingangsrechnung) VALUES (@kEingangsrechnung);
    END

    -- Prüfen
    UPDATE NOVVIA.EingangsrechnungErweitert SET
        nGeprueft = 1,
        kPrueferBenutzer = @kBenutzer,
        dGeprueft = GETDATE(),
        cPruefHinweis = @cPruefHinweis
    WHERE kEingangsrechnung = @kEingangsrechnung;

    -- Freigeben wenn gewünscht
    IF @nNurPruefen = 0
    BEGIN
        UPDATE NOVVIA.EingangsrechnungErweitert SET
            nFreigegeben = 1,
            kFreigabeBenutzer = @kBenutzer,
            dFreigegeben = GETDATE()
        WHERE kEingangsrechnung = @kEingangsrechnung;

        -- JTL Zahlungsfreigabe via SP
        EXEC spEingangsrechnungStatusSetzen @xEingangsrechnungen = NULL;  -- TODO: Korrekte TVP

        -- Alternativ: Direkt Update (wenn SP nicht funktioniert)
        UPDATE tEingangsrechnung SET nZahlungFreigegeben = 1 WHERE kEingangsrechnung = @kEingangsrechnung;
    END
END
GO

PRINT 'SP spNOVVIA_EingangsrechnungPruefenUndFreigeben erstellt';
GO

-- ============================================
-- Übersicht: Lieferanten mit Statistik
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_LieferantenUebersicht' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_LieferantenUebersicht;
GO

CREATE PROCEDURE spNOVVIA_LieferantenUebersicht
    @cSuche NVARCHAR(100) = NULL,
    @nNurAktive BIT = 1,
    @nNurMSV3 BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        l.kLieferant,
        l.cLiefNr,
        l.cFirma,
        l.cOrt,
        l.cTelZentralle AS cTel,
        l.cEMail,
        l.cAktiv,
        l.fMindestbestellwert,
        l.nZahlungsziel,
        l.fSkonto,
        m.kMSV3Lieferant,
        m.cMSV3Url,
        m.nMSV3Version,
        CASE WHEN m.kMSV3Lieferant IS NOT NULL THEN 1 ELSE 0 END AS nHatMSV3,
        (SELECT COUNT(*) FROM tLieferantenBestellung WHERE kLieferant = l.kLieferant AND nStatus < 4) AS nOffeneBestellungen,
        (SELECT COUNT(*) FROM tEingangsrechnung WHERE kLieferant = l.kLieferant AND nStatus = 0) AS nOffeneRechnungen
    FROM tlieferant l
    LEFT JOIN NOVVIA.MSV3Lieferant m ON l.kLieferant = m.kLieferant AND m.nAktiv = 1
    WHERE (@cSuche IS NULL OR l.cFirma LIKE '%' + @cSuche + '%' OR l.cLiefNr LIKE '%' + @cSuche + '%')
      AND (@nNurAktive = 0 OR l.cAktiv = 'Y')
      AND (@nNurMSV3 = 0 OR m.kMSV3Lieferant IS NOT NULL)
    ORDER BY l.cFirma;
END
GO

PRINT 'SP spNOVVIA_LieferantenUebersicht erstellt';
GO

-- ============================================
-- Übersicht: Einkaufsliste mit Pharma-Infos
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_EinkaufslisteMitPharmaInfos' AND schema_id = SCHEMA_ID('dbo'))
    DROP PROCEDURE spNOVVIA_EinkaufslisteMitPharmaInfos;
GO

CREATE PROCEDURE spNOVVIA_EinkaufslisteMitPharmaInfos
    @kBenutzer INT = NULL,
    @kLieferant INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.kArtikelEinkaufsliste,
        e.kArtikel,
        a.cArtNr,
        ab.cName AS ArtikelName,
        e.fAnzahl,
        e.kLieferant,
        l.cFirma AS LieferantName,
        e.fEKNettoLieferant,
        e.cStatus,
        e.dErstellt,
        am.cPZN,
        abd.cHersteller,
        abd.fAEP,
        abd.fAVP,
        abd.nRezeptpflicht,
        abd.nBTM,
        abd.nKuehlpflichtig,
        CASE WHEN msv.kMSV3Lieferant IS NOT NULL THEN 1 ELSE 0 END AS nMSV3Verfuegbar
    FROM tArtikelEinkaufsliste e
    INNER JOIN tArtikel a ON e.kArtikel = a.kArtikel
    LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
    LEFT JOIN tlieferant l ON e.kLieferant = l.kLieferant
    LEFT JOIN NOVVIA.ABdataArtikelMapping am ON a.kArtikel = am.kArtikel
    LEFT JOIN NOVVIA.ABdataArtikel abd ON am.cPZN = abd.cPZN
    LEFT JOIN NOVVIA.MSV3Lieferant msv ON e.kLieferant = msv.kLieferant AND msv.nAktiv = 1
    WHERE (@kBenutzer IS NULL OR e.kBenutzer = @kBenutzer)
      AND (@kLieferant IS NULL OR e.kLieferant = @kLieferant)
    ORDER BY l.cFirma, ab.cName;
END
GO

PRINT 'SP spNOVVIA_EinkaufslisteMitPharmaInfos erstellt';
GO

PRINT '';
PRINT '============================================';
PRINT 'NOVVIA Einkauf-SPs erfolgreich erstellt!';
PRINT 'Nutzt JTL SPs wo vorhanden.';
PRINT '============================================';
GO
