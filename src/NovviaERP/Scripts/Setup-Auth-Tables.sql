-- NOVVIA ERP V2.0 - Berechtigungssystem Tabellen
-- Für JTL-Wawi Datenbank

-- Rollen
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tRolle' AND xtype='U')
CREATE TABLE tRolle (
    kRolle INT IDENTITY(1,1) PRIMARY KEY,
    cName NVARCHAR(100) NOT NULL,
    cBeschreibung NVARCHAR(500),
    nIstAdmin BIT DEFAULT 0
);

-- Berechtigungen
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tBerechtigung' AND xtype='U')
CREATE TABLE tBerechtigung (
    kBerechtigung INT IDENTITY(1,1) PRIMARY KEY,
    cModul NVARCHAR(50) NOT NULL,
    cAktion NVARCHAR(50) NOT NULL,
    cBeschreibung NVARCHAR(200)
);

-- Rolle-Berechtigung Zuordnung
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tRolleBerechtigung' AND xtype='U')
CREATE TABLE tRolleBerechtigung (
    kRolleBerechtigung INT IDENTITY(1,1) PRIMARY KEY,
    kRolle INT NOT NULL FOREIGN KEY REFERENCES tRolle(kRolle),
    kBerechtigung INT NOT NULL FOREIGN KEY REFERENCES tBerechtigung(kBerechtigung)
);

-- Benutzer (erweitert bestehende tBenutzer oder neu)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tBenutzer') AND name = 'cSalt')
BEGIN
    ALTER TABLE tBenutzer ADD cSalt NVARCHAR(100);
    ALTER TABLE tBenutzer ADD kRolle INT;
    ALTER TABLE tBenutzer ADD nFehlversuche INT DEFAULT 0;
    ALTER TABLE tBenutzer ADD dGesperrtBis DATETIME;
END

-- Benutzer-Log
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tBenutzerLog' AND xtype='U')
CREATE TABLE tBenutzerLog (
    kBenutzerLog INT IDENTITY(1,1) PRIMARY KEY,
    kBenutzer INT NOT NULL,
    cAktion NVARCHAR(50) NOT NULL,
    cModul NVARCHAR(50),
    cDetails NVARCHAR(500),
    cIP NVARCHAR(50),
    dZeitpunkt DATETIME DEFAULT GETDATE()
);

-- Standard-Rollen einfügen
IF NOT EXISTS (SELECT * FROM tRolle WHERE cName = 'Administrator')
INSERT INTO tRolle (cName, cBeschreibung, nIstAdmin) VALUES 
    ('Administrator', 'Vollzugriff auf alle Funktionen', 1),
    ('Verkauf', 'Bestellungen, Kunden, Rechnungen', 0),
    ('Lager', 'Lager, Versand, Wareneingang', 0),
    ('Einkauf', 'Einkauf, Lieferanten', 0),
    ('Buchhaltung', 'Rechnungen, Mahnungen, DATEV', 0),
    ('Nur Lesen', 'Nur Lesezugriff', 0);

-- Standard-Berechtigungen einfügen
IF NOT EXISTS (SELECT * FROM tBerechtigung WHERE cModul = 'Artikel')
INSERT INTO tBerechtigung (cModul, cAktion, cBeschreibung) VALUES
    ('Artikel', 'Lesen', 'Artikel anzeigen'),
    ('Artikel', 'Schreiben', 'Artikel erstellen/bearbeiten'),
    ('Artikel', 'Loeschen', 'Artikel löschen'),
    ('Artikel', 'Preis', 'Preise ändern'),
    ('Kunden', 'Lesen', 'Kunden anzeigen'),
    ('Kunden', 'Schreiben', 'Kunden erstellen/bearbeiten'),
    ('Kunden', 'Loeschen', 'Kunden löschen'),
    ('Bestellungen', 'Lesen', 'Bestellungen anzeigen'),
    ('Bestellungen', 'Schreiben', 'Bestellungen erstellen/bearbeiten'),
    ('Bestellungen', 'Stornieren', 'Bestellungen stornieren'),
    ('Rechnungen', 'Lesen', 'Rechnungen anzeigen'),
    ('Rechnungen', 'Erstellen', 'Rechnungen erstellen'),
    ('Rechnungen', 'Zahlung', 'Zahlungen buchen'),
    ('Lager', 'Lesen', 'Lagerbestände anzeigen'),
    ('Lager', 'Buchen', 'Lagerbewegungen buchen'),
    ('Lager', 'Inventur', 'Inventur durchführen'),
    ('Versand', 'Lesen', 'Versand anzeigen'),
    ('Versand', 'Label', 'Versandlabels erstellen'),
    ('Einkauf', 'Lesen', 'Einkauf anzeigen'),
    ('Einkauf', 'Bestellen', 'Einkaufsbestellungen erstellen'),
    ('Mahnungen', 'Lesen', 'Mahnungen anzeigen'),
    ('Mahnungen', 'Erstellen', 'Mahnungen erstellen'),
    ('DATEV', 'Export', 'DATEV-Export durchführen'),
    ('WooCommerce', 'Sync', 'WooCommerce synchronisieren'),
    ('Berichte', 'Lesen', 'Berichte anzeigen'),
    ('Einstellungen', 'Lesen', 'Einstellungen anzeigen'),
    ('Einstellungen', 'Schreiben', 'Einstellungen ändern'),
    ('Benutzer', 'Verwalten', 'Benutzer verwalten');

-- Berechtigungen für Verkauf-Rolle
INSERT INTO tRolleBerechtigung (kRolle, kBerechtigung)
SELECT (SELECT kRolle FROM tRolle WHERE cName = 'Verkauf'), kBerechtigung
FROM tBerechtigung WHERE cModul IN ('Artikel', 'Kunden', 'Bestellungen', 'Rechnungen') AND cAktion IN ('Lesen', 'Schreiben');

-- Admin-Benutzer erstellen (Passwort: admin)
IF NOT EXISTS (SELECT * FROM tBenutzer WHERE cLogin = 'admin')
INSERT INTO tBenutzer (cLogin, cPasswortHash, cSalt, cNachname, kRolle, nAktiv, dErstellt)
VALUES ('admin', 'jZae727K08KaOmKSgOaGzww/XVqGr/PKEgIMkjrcbJI=', 'DefaultSalt123', 'Administrator', 
        (SELECT kRolle FROM tRolle WHERE cName = 'Administrator'), 1, GETDATE());

PRINT 'Berechtigungssystem eingerichtet!';
