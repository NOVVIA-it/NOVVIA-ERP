-- =====================================================
-- NOVVIA Eigene Felder für Lieferanten
-- =====================================================
-- JTL hat keine nativen eigenen Felder für Lieferanten,
-- daher legen wir diese in NOVVIA-Schema an.
-- =====================================================

USE [Mandant_1]
GO

-- Schema sicherstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- Tabelle: NOVVIA.LieferantAttribut
-- Definition der verfügbaren Attribute für Lieferanten
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'LieferantAttribut')
BEGIN
    CREATE TABLE NOVVIA.LieferantAttribut (
        kLieferantAttribut INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(255) NOT NULL,
        cBeschreibung NVARCHAR(500) NULL,
        nFeldTyp INT NOT NULL DEFAULT 1,  -- 1=Text, 2=Int, 3=Decimal, 4=DateTime
        nSortierung INT NOT NULL DEFAULT 0,
        nAktiv BIT NOT NULL DEFAULT 1,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
        dGeaendert DATETIME NULL
    );

    CREATE UNIQUE INDEX IX_LieferantAttribut_Name ON NOVVIA.LieferantAttribut(cName);

    PRINT 'Tabelle NOVVIA.LieferantAttribut erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.LieferantAttribut existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.LieferantEigenesFeld
-- Werte der eigenen Felder pro Lieferant
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'LieferantEigenesFeld')
BEGIN
    CREATE TABLE NOVVIA.LieferantEigenesFeld (
        kLieferantEigenesFeld INT IDENTITY(1,1) PRIMARY KEY,
        kLieferant INT NOT NULL,
        kLieferantAttribut INT NOT NULL,
        cWertVarchar NVARCHAR(MAX) NULL,
        nWertInt INT NULL,
        fWertDecimal DECIMAL(25,10) NULL,
        dWertDateTime DATETIME NULL,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
        dGeaendert DATETIME NULL,

        CONSTRAINT FK_LieferantEigenesFeld_Attribut
            FOREIGN KEY (kLieferantAttribut) REFERENCES NOVVIA.LieferantAttribut(kLieferantAttribut),
        CONSTRAINT UQ_LieferantEigenesFeld_LieferantAttribut
            UNIQUE (kLieferant, kLieferantAttribut)
    );

    CREATE INDEX IX_LieferantEigenesFeld_Lieferant ON NOVVIA.LieferantEigenesFeld(kLieferant);

    PRINT 'Tabelle NOVVIA.LieferantEigenesFeld erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.LieferantEigenesFeld existiert bereits.';
GO

-- =====================================================
-- SP: NOVVIA.spLieferantEigenesFeldSpeichern
-- Erstellt oder aktualisiert ein eigenes Feld
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLieferantEigenesFeldSpeichern')
    DROP PROCEDURE NOVVIA.spLieferantEigenesFeldSpeichern;
GO

CREATE PROCEDURE NOVVIA.spLieferantEigenesFeldSpeichern
    @kLieferant INT,
    @kLieferantAttribut INT,
    @cWertVarchar NVARCHAR(MAX) = NULL,
    @nWertInt INT = NULL,
    @fWertDecimal DECIMAL(25,10) = NULL,
    @dWertDateTime DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Prüfen ob Attribut existiert
    IF NOT EXISTS (SELECT 1 FROM NOVVIA.LieferantAttribut WHERE kLieferantAttribut = @kLieferantAttribut)
    BEGIN
        RAISERROR('Attribut nicht gefunden.', 16, 1);
        RETURN;
    END

    -- Merge: Insert oder Update
    MERGE NOVVIA.LieferantEigenesFeld AS target
    USING (SELECT @kLieferant AS kLieferant, @kLieferantAttribut AS kLieferantAttribut) AS source
    ON target.kLieferant = source.kLieferant AND target.kLieferantAttribut = source.kLieferantAttribut
    WHEN MATCHED THEN
        UPDATE SET
            cWertVarchar = @cWertVarchar,
            nWertInt = @nWertInt,
            fWertDecimal = @fWertDecimal,
            dWertDateTime = @dWertDateTime,
            dGeaendert = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (kLieferant, kLieferantAttribut, cWertVarchar, nWertInt, fWertDecimal, dWertDateTime)
        VALUES (@kLieferant, @kLieferantAttribut, @cWertVarchar, @nWertInt, @fWertDecimal, @dWertDateTime);

    SELECT SCOPE_IDENTITY() AS kLieferantEigenesFeld;
END
GO

PRINT 'SP NOVVIA.spLieferantEigenesFeldSpeichern erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spLieferantAttributSpeichern
-- Erstellt oder aktualisiert ein Attribut
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLieferantAttributSpeichern')
    DROP PROCEDURE NOVVIA.spLieferantAttributSpeichern;
GO

CREATE PROCEDURE NOVVIA.spLieferantAttributSpeichern
    @kLieferantAttribut INT = NULL,  -- NULL = Neu
    @cName NVARCHAR(255),
    @cBeschreibung NVARCHAR(500) = NULL,
    @nFeldTyp INT = 1,
    @nSortierung INT = 0,
    @nAktiv BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @kLieferantAttribut IS NULL OR @kLieferantAttribut = 0
    BEGIN
        -- Neu anlegen
        INSERT INTO NOVVIA.LieferantAttribut (cName, cBeschreibung, nFeldTyp, nSortierung, nAktiv)
        VALUES (@cName, @cBeschreibung, @nFeldTyp, @nSortierung, @nAktiv);

        SELECT SCOPE_IDENTITY() AS kLieferantAttribut;
    END
    ELSE
    BEGIN
        -- Aktualisieren
        UPDATE NOVVIA.LieferantAttribut
        SET cName = @cName,
            cBeschreibung = @cBeschreibung,
            nFeldTyp = @nFeldTyp,
            nSortierung = @nSortierung,
            nAktiv = @nAktiv,
            dGeaendert = GETDATE()
        WHERE kLieferantAttribut = @kLieferantAttribut;

        SELECT @kLieferantAttribut AS kLieferantAttribut;
    END
END
GO

PRINT 'SP NOVVIA.spLieferantAttributSpeichern erstellt.';
GO

-- =====================================================
-- Beispiel-Attribute für Lieferanten anlegen
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM NOVVIA.LieferantAttribut WHERE cName = 'Kundennummer')
BEGIN
    INSERT INTO NOVVIA.LieferantAttribut (cName, cBeschreibung, nFeldTyp, nSortierung)
    VALUES
        ('Kundennummer', 'Unsere Kundennummer beim Lieferanten', 1, 1),
        ('Zahlungsziel', 'Zahlungsziel in Tagen', 2, 2),
        ('Skonto', 'Skonto in Prozent', 3, 3),
        ('Vertragsbeginn', 'Datum des Vertragsbeginns', 4, 4),
        ('Notizen', 'Interne Notizen', 1, 5);

    PRINT 'Beispiel-Attribute für Lieferanten angelegt.';
END
GO

PRINT 'Setup NOVVIA.LieferantEigenesFeld abgeschlossen.';
GO
