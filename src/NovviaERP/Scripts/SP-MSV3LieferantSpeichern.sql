USE Mandant_2;
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_MSV3LieferantSpeichern')
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
            nAktiv = 1,
            dGeaendert = GETDATE()
        WHERE kLieferant = @kLieferant;

        SELECT @kMSV3Lieferant = kMSV3Lieferant FROM NOVVIA.MSV3Lieferant WHERE kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.MSV3Lieferant (kLieferant, cMSV3Url, cMSV3Benutzer, cMSV3Passwort, cMSV3Kundennummer, cMSV3Filiale, nMSV3Version, nPrioritaet, nAktiv)
        VALUES (@kLieferant, @cMSV3Url, @cMSV3Benutzer, @cMSV3Passwort, @cMSV3Kundennummer, @cMSV3Filiale, @nMSV3Version, @nPrioritaet, 1);

        SET @kMSV3Lieferant = SCOPE_IDENTITY();
    END
END
GO

PRINT 'SP spNOVVIA_MSV3LieferantSpeichern erstellt';
GO
