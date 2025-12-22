# NOVVIA ERP - Windows Server Installation

## 📋 Voraussetzungen

| Komponente | Anforderung |
|------------|-------------|
| OS | Windows Server 2019/2022 |
| RAM | 4 GB (8 GB empfohlen) |
| Disk | 500 MB frei |
| .NET | 8.0 Runtime |
| Netzwerk | Zugriff auf SQL Server |

---

## 🚀 Schnellinstallation (Automatisch)

### 1. ZIP entpacken
```powershell
Expand-Archive -Path "C:\Downloads\NovviaERP.zip" -DestinationPath "C:\Temp\"
cd C:\Temp\NovviaERP
```

### 2. Installationsskript ausführen (als Administrator!)
```powershell
.\Scripts\Install-Server.ps1 -SqlPass "DEIN_SQL_PASSWORT"
```

### 3. Fertig!
- Desktop-Verknüpfung: "NOVVIA ERP"
- Worker-Dienst läuft automatisch

---

## 🔧 Manuelle Installation

### Schritt 1: .NET 8 installieren

```powershell
# Option A: Über winget
winget install Microsoft.DotNet.DesktopRuntime.8

# Option B: Download
# https://dotnet.microsoft.com/download/dotnet/8.0
# → .NET Desktop Runtime 8.0 (Windows x64)
```

### Schritt 2: Ordner erstellen

```powershell
mkdir C:\NovviaERP
mkdir C:\NovviaERP\Logs
mkdir C:\NovviaERP\Dokumente
mkdir C:\NovviaERP\Worker
```

### Schritt 3: Kompilieren & Kopieren

```powershell
cd C:\Temp\NovviaERP

# Kompilieren
dotnet restore
dotnet build -c Release

# Kopieren
Copy-Item "NovviaERP.WPF\bin\Release\net8.0-windows\*" "C:\NovviaERP\" -Recurse
Copy-Item "NovviaERP.Worker\bin\Release\net8.0\*" "C:\NovviaERP\Worker\" -Recurse
Copy-Item "Scripts\*.sql" "C:\NovviaERP\Scripts\" -Force
```

### Schritt 4: NOVVIA-Tabellen erstellen

```powershell
# SSMS öffnen, verbinden mit:
# Server: 24.134.81.65,2107\NOVVIAS05
# User: NOVVIA_SQL

# Für JEDEN Mandanten ausführen:
USE Mandant_1;  -- bzw. Mandant_2, Mandant_3, Mandant_5
GO
-- Script: C:\NovviaERP\Scripts\Setup-NovviaTables.sql ausführen
```

### Schritt 5: Worker als Dienst installieren

```powershell
# Als Administrator:
sc.exe create "NovviaERP-Worker" `
    binPath="C:\NovviaERP\Worker\NovviaERP.Worker.exe" `
    start=auto `
    DisplayName="NOVVIA ERP Worker"

sc.exe description "NovviaERP-Worker" "Hintergrund-Prozesse für NOVVIA ERP"

# Starten
sc.exe start "NovviaERP-Worker"
```

### Schritt 6: Desktop-Verknüpfung

```powershell
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$env:PUBLIC\Desktop\NOVVIA ERP.lnk")
$shortcut.TargetPath = "C:\NovviaERP\NovviaERP.WPF.exe"
$shortcut.WorkingDirectory = "C:\NovviaERP"
$shortcut.Save()
```

---

## 🖥️ Client-Installation (Arbeitsplätze)

Für weitere Arbeitsplätze nur:

### 1. .NET 8 Desktop Runtime installieren
```
https://dotnet.microsoft.com/download/dotnet/8.0
```

### 2. NovviaERP.WPF.exe kopieren
```powershell
# Vom Server kopieren oder Netzlaufwerk freigeben
\\SERVER\NovviaERP\NovviaERP.WPF.exe
```

### 3. Oder: Netzlaufwerk nutzen
```powershell
# Auf Server: Freigabe erstellen
New-SmbShare -Name "NovviaERP" -Path "C:\NovviaERP" -ReadAccess "Jeder"

# Auf Client: Verknüpfung zu
\\SERVERNAME\NovviaERP\NovviaERP.WPF.exe
```

---

## 🔒 Firewall-Regeln

```powershell
# SQL Server (falls auf anderem Server)
New-NetFirewallRule -DisplayName "SQL Server" `
    -Direction Inbound -Protocol TCP -LocalPort 1433,2107 -Action Allow

# Falls API genutzt wird
New-NetFirewallRule -DisplayName "NOVVIA ERP API" `
    -Direction Inbound -Protocol TCP -LocalPort 5000,5001 -Action Allow
```

---

## 📁 Ordnerstruktur nach Installation

```
C:\NovviaERP\
├── NovviaERP.WPF.exe      # Hauptanwendung
├── appsettings.json        # Konfiguration
├── Logs\                   # Log-Dateien
│   └── novvia-20241215.log
├── Dokumente\              # Generierte PDFs
├── Worker\                 # Hintergrund-Dienst
│   └── NovviaERP.Worker.exe
└── Scripts\                # SQL-Skripte
    └── Setup-NovviaTables.sql

%APPDATA%\NovviaERP\
├── profile.json            # Server-Profile
└── login.json              # Letzte Anmeldung
```

---

## ✅ Installation prüfen

### 1. Worker-Dienst prüfen
```powershell
Get-Service "NovviaERP-Worker"
# Status: Running
```

### 2. Logs prüfen
```powershell
Get-Content "C:\NovviaERP\Logs\novvia-*.log" -Tail 20
```

### 3. SQL-Verbindung testen
```powershell
# In NOVVIA ERP: Profilverwaltung → Verbindung testen
```

---

## 🔄 Update durchführen

```powershell
# 1. Worker stoppen
Stop-Service "NovviaERP-Worker"

# 2. Neue Version kopieren
Copy-Item "NovviaERP-NEU\*" "C:\NovviaERP\" -Recurse -Force

# 3. Worker starten
Start-Service "NovviaERP-Worker"
```

---

## ❌ Deinstallation

```powershell
# Worker-Dienst entfernen
Stop-Service "NovviaERP-Worker"
sc.exe delete "NovviaERP-Worker"

# Dateien löschen
Remove-Item "C:\NovviaERP" -Recurse -Force
Remove-Item "$env:APPDATA\NovviaERP" -Recurse -Force

# Verknüpfungen löschen
Remove-Item "$env:PUBLIC\Desktop\NOVVIA ERP.lnk"
```

---

## 📞 Fehlerbehebung

### "Die Anwendung startet nicht"
→ .NET 8 Desktop Runtime installieren

### "SQL-Verbindung fehlgeschlagen"
→ Firewall Port 2107 prüfen
→ SQL-Passwort in Profil prüfen

### "Worker-Dienst startet nicht"
```powershell
# Log prüfen
Get-EventLog -LogName Application -Source "NovviaERP-Worker" -Newest 10
```

### "Benutzer nicht gefunden"
→ JTL-Wawi öffnen, Benutzer prüfen (nAktiv = 1)

---

## 📞 Support

IT-Abteilung: it@novvia.de
