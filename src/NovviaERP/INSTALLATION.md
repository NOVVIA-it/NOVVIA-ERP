# NOVVIA ERP - Installationsanleitung

## 🎯 Systemübersicht

**Server:** `24.134.81.65,2107\NOVVIAS05`  
**SQL-Benutzer:** `NOVVIA_SQL`

**Mandanten:**
| Mandant | Datenbank | Beschreibung |
|---------|-----------|--------------|
| NOVVIA | Mandant_1 | Hauptmandant |
| NOVVIA_PHARM | Mandant_2 | Pharma-Bereich |
| PA | Mandant_3 | - |
| NOVVIA_TEST | Mandant_5 | Testumgebung |

---

## 1. Installation

### 1.1 ZIP entpacken
```powershell
Expand-Archive -Path "NovviaERP.zip" -DestinationPath "C:\Programme\"
cd "C:\Programme\NovviaERP"
```

### 1.2 Kompilieren
```powershell
dotnet restore
dotnet build -c Release
```

### 1.3 NOVVIA-Tabellen erstellen (einmalig)
```sql
-- In SSMS mit NOVVIA_SQL verbinden
-- Für JEDEN Mandanten ausführen:

USE Mandant_1;  -- oder Mandant_2, Mandant_3, etc.
GO

-- Script ausführen:
-- Scripts/Setup-NovviaTables.sql
```

---

## 2. Erster Start

### 2.1 Anwendung starten
```powershell
cd NovviaERP.WPF\bin\Release\net8.0-windows
.\NovviaERP.WPF.exe
```

### 2.2 Profil einrichten (erster Start)
1. Klick auf ⚙️ neben "Serverprofil"
2. Server: `24.134.81.65,2107\NOVVIAS05`
3. SQL-Benutzer: `NOVVIA_SQL`
4. SQL-Passwort eingeben
5. "Verbindung testen" → Mandanten werden geladen
6. "Speichern"

### 2.3 Anmelden
1. Profil auswählen
2. Mandant auswählen (z.B. NOVVIA)
3. JTL-Benutzername eingeben
4. JTL-Passwort eingeben
5. "Anmelden"

---

## 3. Features

### Login-System
- **Profilverwaltung** wie bei JTL
- **Mandanten-Auswahl** beim Login
- **Anmeldedaten merken** optional
- **Benutzer aus JTL** (tBenutzer)

### Multi-Mandant
- Zwischen Mandanten wechseln ohne Neustart
- Jeder Mandant hat eigene Daten
- NOVVIA-Tabellen pro Mandant

---

## 4. Ordnerstruktur

```
%APPDATA%\NovviaERP\
├── profile.json      # Serverprofile
└── login.json        # Letzte Anmeldung
```

---

## 5. Fehlerbehebung

### "Benutzer nicht gefunden"
- JTL-Wawi öffnen → Benutzer prüfen
- Benutzer muss aktiv sein (nAktiv = 1)

### "Falsches Passwort"
- JTL verwendet MD5/SHA1/SHA256 für Passwörter
- In JTL-Wawi Passwort neu setzen

### "Verbindung fehlgeschlagen"
- Server-Adresse prüfen
- SQL-Passwort prüfen
- Firewall Port 2107 freigeben

---

## 6. Technische Details

### Passwort-Hashing
JTL verwendet je nach Version:
- MD5 (ältere Versionen)
- SHA1 
- SHA256 (neuere Versionen)

NOVVIA ERP prüft alle drei Varianten automatisch.

### Datenbank-Zugriff
- Nur LESEND auf JTL-Tabellen
- NOVVIA-Tabellen im Schema `NOVVIA.`
- Kein Konflikt mit JTL-Updates

---

## 📞 Support

IT-Abteilung: it@novvia.de
