-- EDIFACT Partner-Tabelle
-- Speichert Konfiguration fuer EDI-Partner (Grosshaendler, etc.)

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.EdifactPartner') AND type = 'U')
BEGIN
    CREATE TABLE NOVVIA.EdifactPartner (
        kPartner INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(100) NOT NULL,
        cPartnerGLN NVARCHAR(20) NOT NULL,           -- GLN des Partners
        cEigeneGLN NVARCHAR(20) NOT NULL,            -- Eigene GLN
        cEigeneFirma NVARCHAR(100) NULL,
        cEigeneStrasse NVARCHAR(100) NULL,
        cEigenePLZ NVARCHAR(10) NULL,
        cEigeneOrt NVARCHAR(100) NULL,
        cProtokoll NVARCHAR(20) DEFAULT 'SFTP',      -- SFTP, FTP, AS2, Verzeichnis
        cHost NVARCHAR(255) NULL,
        nPort INT DEFAULT 22,
        cBenutzer NVARCHAR(100) NULL,
        cPasswort NVARCHAR(255) NULL,                -- Verschluesselt speichern!
        cVerzeichnisIn NVARCHAR(500) NULL,           -- Eingangsverzeichnis
        cVerzeichnisOut NVARCHAR(500) NULL,          -- Ausgangsverzeichnis
        nAktiv BIT DEFAULT 1,
        dErstellt DATETIME2 DEFAULT GETDATE(),
        dGeaendert DATETIME2 DEFAULT GETDATE()
    )

    PRINT 'Tabelle NOVVIA.EdifactPartner erstellt.'
END
GO

-- EDIFACT Nachrichten-Log
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.EdifactLog') AND type = 'U')
BEGIN
    CREATE TABLE NOVVIA.EdifactLog (
        kLog INT IDENTITY(1,1) PRIMARY KEY,
        kPartner INT NOT NULL,
        cRichtung NVARCHAR(10) NOT NULL,             -- IN, OUT
        cNachrichtentyp NVARCHAR(20) NOT NULL,       -- ORDERS, ORDRSP, DESADV, INVOIC
        cInterchangeRef NVARCHAR(50) NULL,
        cMessageRef NVARCHAR(50) NULL,
        cDokumentNr NVARCHAR(50) NULL,
        cDateiname NVARCHAR(255) NULL,
        cStatus NVARCHAR(20) DEFAULT 'NEU',          -- NEU, VERARBEITET, FEHLER
        cFehler NVARCHAR(MAX) NULL,
        kBestellung INT NULL,
        kRechnung INT NULL,
        kLieferschein INT NULL,
        dErstellt DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_EdifactLog_Partner FOREIGN KEY (kPartner) REFERENCES NOVVIA.EdifactPartner(kPartner)
    )

    CREATE INDEX IX_EdifactLog_Partner ON NOVVIA.EdifactLog(kPartner)
    CREATE INDEX IX_EdifactLog_Status ON NOVVIA.EdifactLog(cStatus)
    CREATE INDEX IX_EdifactLog_Datum ON NOVVIA.EdifactLog(dErstellt)

    PRINT 'Tabelle NOVVIA.EdifactLog erstellt.'
END
GO

-- Beispiel-Partner (Pharma-Grosshandel)
-- INSERT INTO NOVVIA.EdifactPartner (cName, cPartnerGLN, cEigeneGLN, cProtokoll, cHost, cVerzeichnisIn, cVerzeichnisOut)
-- VALUES
--     ('Phoenix', '4300001000001', '4399999000001', 'SFTP', 'edi.phoenix.de', '/in', '/out'),
--     ('Sanacorp', '4300002000001', '4399999000001', 'SFTP', 'edi.sanacorp.de', '/in', '/out'),
--     ('Noweda', '4300003000001', '4399999000001', 'SFTP', 'edi.noweda.de', '/in', '/out')

PRINT 'EDIFACT Setup abgeschlossen.'
GO
