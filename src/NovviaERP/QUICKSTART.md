# NOVVIA ERP - Schnellstart

## 🚀 In 3 Schritten zur fertigen Installation

### Schritt 1: .NET 8 SDK installieren

Download: https://dotnet.microsoft.com/download/dotnet/8.0
→ **.NET 8.0 SDK** (Windows x64)

**Wichtig:** SDK, nicht nur Runtime!

---

### Schritt 2: ZIP entpacken & Build starten

```powershell
# Als Administrator!
cd C:\Users\PA\Downloads

# ZIP entpacken
Expand-Archive -Path "NovviaERP.zip" -DestinationPath "C:\Temp\NovviaERP-Build" -Force

# Ins Verzeichnis wechseln
cd C:\Temp\NovviaERP-Build\NovviaERP

# Build & Deploy starten
.\Scripts\Build-And-Deploy.ps1
```

---

### Schritt 3: SQL-Daten eingeben

Das Script fragt:
```
SQL Server: 24.134.81.65,2107\NOVVIAS05
SQL Benutzer: NOVVIA_SQL
SQL Passwort: ********
```

---

## ✅ Fertig!

Nach dem Build hast du:

| Datei | Beschreibung |
|-------|--------------|
| `NovviaERP-Client-DATUM.zip` | Für Arbeitsplätze (nur EXE, ~80 MB) |
| `NovviaERP-Server-DATUM.zip` | Komplette Installation mit Worker |
| Desktop-Verknüpfung | "NOVVIA ERP" |

---

## 📦 Client auf anderen PCs installieren

1. `NovviaERP-Client-DATUM.zip` kopieren
2. Entpacken nach `C:\NovviaERP\`
3. `NovviaERP.exe` starten
4. Beim ersten Start: Profil einrichten (⚙️ Button)

**Kein .NET nötig!** Die EXE ist self-contained.

---

## 🔧 Nur Build (ohne Installation)

```powershell
.\Scripts\Build-And-Deploy.ps1 -NurBuild
```

Erstellt nur die ZIP-Dateien, installiert nichts.
