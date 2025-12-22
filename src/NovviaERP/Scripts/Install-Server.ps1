#Requires -RunAsAdministrator
# ============================================
# NOVVIA ERP - Server Installation
# Für Windows Server 2019/2022
# ============================================

param(
    [string]$InstallPfad = "",
    [string]$SqlServer = "",
    [string]$SqlUser = "",
    [string]$SqlPass = "",
    [switch]$MitWorkerDienst = $true,
    [switch]$Silent = $false
)

Clear-Host
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  NOVVIA ERP - Server Installation" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# KONFIGURATION ABFRAGEN
# ============================================

if (-not $Silent) {
    Write-Host "📋 KONFIGURATION" -ForegroundColor Yellow
    Write-Host "──────────────────────────────────────────────" -ForegroundColor Gray
    Write-Host ""
    
    # Installationspfad
    if ([string]::IsNullOrEmpty($InstallPfad)) {
        $default = "C:\NovviaERP"
        $input = Read-Host "Installationspfad [$default]"
        $InstallPfad = if ([string]::IsNullOrEmpty($input)) { $default } else { $input }
    }
    Write-Host "  ✓ Installationspfad: $InstallPfad" -ForegroundColor Green
    
    # SQL Server
    if ([string]::IsNullOrEmpty($SqlServer)) {
        Write-Host ""
        Write-Host "  SQL Server Beispiele:" -ForegroundColor Gray
        Write-Host "    - localhost\JTLWAWI" -ForegroundColor Gray
        Write-Host "    - 192.168.0.220\SQLEXPRESS" -ForegroundColor Gray
        Write-Host "    - 24.134.81.65,2107\NOVVIAS05" -ForegroundColor Gray
        Write-Host ""
        $SqlServer = Read-Host "SQL Server (Name\Instanz oder IP,Port\Instanz)"
        
        if ([string]::IsNullOrEmpty($SqlServer)) {
            Write-Host "  ❌ SQL Server ist erforderlich!" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "  ✓ SQL Server: $SqlServer" -ForegroundColor Green
    
    # SQL Benutzer
    if ([string]::IsNullOrEmpty($SqlUser)) {
        $default = "sa"
        $input = Read-Host "SQL Benutzer [$default]"
        $SqlUser = if ([string]::IsNullOrEmpty($input)) { $default } else { $input }
    }
    Write-Host "  ✓ SQL Benutzer: $SqlUser" -ForegroundColor Green
    
    # SQL Passwort
    if ([string]::IsNullOrEmpty($SqlPass)) {
        $securePass = Read-Host "SQL Passwort" -AsSecureString
        $SqlPass = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePass))
        
        if ([string]::IsNullOrEmpty($SqlPass)) {
            Write-Host "  ❌ SQL Passwort ist erforderlich!" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "  ✓ SQL Passwort: ********" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "──────────────────────────────────────────────" -ForegroundColor Gray
}

# ============================================
# 1. SQL-VERBINDUNG TESTEN
# ============================================
Write-Host ""
Write-Host "[1/7] Teste SQL-Verbindung..." -ForegroundColor Yellow

$connStr = "Server=$SqlServer;Database=master;User Id=$SqlUser;Password=$SqlPass;TrustServerCertificate=True;Connection Timeout=10"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    
    # Mandanten suchen
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT name FROM sys.databases WHERE name LIKE 'Mandant_%' OR name = 'eazybusiness' ORDER BY name"
    $reader = $cmd.ExecuteReader()
    
    $mandanten = @()
    while ($reader.Read()) {
        $mandanten += $reader.GetString(0)
    }
    $reader.Close()
    $conn.Close()
    
    Write-Host "  ✅ Verbindung erfolgreich!" -ForegroundColor Green
    Write-Host "  📦 Gefundene Mandanten:" -ForegroundColor Gray
    foreach ($m in $mandanten) {
        Write-Host "      - $m" -ForegroundColor Gray
    }
    
} catch {
    Write-Host "  ❌ Verbindung fehlgeschlagen!" -ForegroundColor Red
    Write-Host "     $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Mögliche Ursachen:" -ForegroundColor Yellow
    Write-Host "    - SQL Server nicht erreichbar" -ForegroundColor Gray
    Write-Host "    - Falscher Servername/Port" -ForegroundColor Gray
    Write-Host "    - Falscher Benutzer/Passwort" -ForegroundColor Gray
    Write-Host "    - Firewall blockiert Port" -ForegroundColor Gray
    exit 1
}

# ============================================
# 2. .NET PRÜFEN
# ============================================
Write-Host ""
Write-Host "[2/7] Prüfe .NET 8..." -ForegroundColor Yellow

$dotnetVersion = dotnet --list-runtimes 2>$null | Select-String "Microsoft.WindowsDesktop.App 8"
if (-not $dotnetVersion) {
    Write-Host "  ⚠️  .NET 8 Desktop Runtime nicht gefunden!" -ForegroundColor Yellow
    Write-Host "  📥 Bitte installieren von:" -ForegroundColor Yellow
    Write-Host "     https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    Write-Host ""
    
    $install = Read-Host "Jetzt automatisch installieren? (j/n)"
    if ($install -eq "j") {
        try {
            Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile "$env:TEMP\dotnet-install.ps1"
            & "$env:TEMP\dotnet-install.ps1" -Channel 8.0 -Runtime windowsdesktop
            Write-Host "  ✅ .NET 8 installiert" -ForegroundColor Green
        } catch {
            Write-Host "  ❌ Automatische Installation fehlgeschlagen" -ForegroundColor Red
            Write-Host "     Bitte manuell installieren und Script erneut starten" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "  Bitte .NET 8 installieren und Script erneut starten" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "  ✅ .NET 8 gefunden" -ForegroundColor Green
}

# ============================================
# 3. ORDNER ERSTELLEN
# ============================================
Write-Host ""
Write-Host "[3/7] Erstelle Ordner..." -ForegroundColor Yellow

$folders = @(
    $InstallPfad,
    "$InstallPfad\Logs",
    "$InstallPfad\Dokumente",
    "$InstallPfad\Worker",
    "$InstallPfad\Scripts"
)

foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }
}
Write-Host "  ✅ Ordner erstellt" -ForegroundColor Green

# ============================================
# 4. DATEIEN KOPIEREN
# ============================================
Write-Host ""
Write-Host "[4/7] Kopiere Dateien..." -ForegroundColor Yellow

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Split-Path -Parent $scriptPath

# Kompilieren falls nötig
if (Test-Path "$sourcePath\NovviaERP.WPF\NovviaERP.WPF.csproj") {
    if (-not (Test-Path "$sourcePath\NovviaERP.WPF\bin\Release\net8.0-windows\NovviaERP.WPF.exe")) {
        Write-Host "  🔨 Kompiliere Anwendung..." -ForegroundColor Gray
        Push-Location $sourcePath
        dotnet build -c Release --verbosity quiet
        Pop-Location
    }
    
    if (Test-Path "$sourcePath\NovviaERP.WPF\bin\Release\net8.0-windows") {
        Copy-Item "$sourcePath\NovviaERP.WPF\bin\Release\net8.0-windows\*" "$InstallPfad\" -Recurse -Force
    }
    
    if (Test-Path "$sourcePath\NovviaERP.Worker\bin\Release\net8.0") {
        Copy-Item "$sourcePath\NovviaERP.Worker\bin\Release\net8.0\*" "$InstallPfad\Worker\" -Recurse -Force
    }
}

Copy-Item "$sourcePath\Scripts\*.sql" "$InstallPfad\Scripts\" -Force -ErrorAction SilentlyContinue

Write-Host "  ✅ Dateien kopiert" -ForegroundColor Green

# ============================================
# 5. KONFIGURATION SPEICHERN
# ============================================
Write-Host ""
Write-Host "[5/7] Speichere Konfiguration..." -ForegroundColor Yellow

# appsettings.json
$appSettings = @{
    ConnectionStrings = @{
        DefaultConnection = "Server=$SqlServer;Database=Mandant_1;User Id=$SqlUser;Password=$SqlPass;TrustServerCertificate=True;MultipleActiveResultSets=True"
    }
    SqlServer = @{
        Server = $SqlServer
        User = $SqlUser
    }
    Logging = @{
        LogLevel = @{
            Default = "Information"
        }
    }
}

$appSettings | ConvertTo-Json -Depth 5 | Set-Content "$InstallPfad\appsettings.json" -Encoding UTF8
Write-Host "  ✅ appsettings.json erstellt" -ForegroundColor Green

# Benutzer-Profil (für Login-Dialog)
$profilePath = "$env:APPDATA\NovviaERP"
if (-not (Test-Path $profilePath)) {
    New-Item -ItemType Directory -Path $profilePath -Force | Out-Null
}

# Mandanten-Info für Profil
$mandantenInfo = @()
foreach ($m in $mandanten) {
    $name = switch ($m) {
        "eazybusiness" { "eB-Standard" }
        "Mandant_1" { "NOVVIA" }
        "Mandant_2" { "NOVVIA_PHARM" }
        "Mandant_3" { "PA" }
        "Mandant_5" { "NOVVIA_TEST" }
        default { $m }
    }
    $mandantenInfo += @{
        Name = $name
        Datenbank = $m
        Aktiv = $true
    }
}

$profile = @(
    @{
        Name = "Standard"
        Beschreibung = "Automatisch erstellt bei Installation"
        Server = $SqlServer
        SqlBenutzer = $SqlUser
        SqlPasswort = $SqlPass
        Mandanten = $mandantenInfo
    }
)

$profile | ConvertTo-Json -Depth 5 | Set-Content "$profilePath\profile.json" -Encoding UTF8
Write-Host "  ✅ Serverprofil erstellt" -ForegroundColor Green

# ============================================
# 6. NOVVIA-TABELLEN ERSTELLEN
# ============================================
Write-Host ""
Write-Host "[6/7] Erstelle NOVVIA-Tabellen..." -ForegroundColor Yellow

$sqlScript = Get-Content "$InstallPfad\Scripts\Setup-NovviaTables.sql" -Raw -ErrorAction SilentlyContinue

if ($sqlScript) {
    foreach ($mandant in $mandanten) {
        Write-Host "  📦 $mandant..." -ForegroundColor Gray -NoNewline
        
        $connStrMandant = "Server=$SqlServer;Database=$mandant;User Id=$SqlUser;Password=$SqlPass;TrustServerCertificate=True"
        
        try {
            $conn = New-Object System.Data.SqlClient.SqlConnection($connStrMandant)
            $conn.Open()
            
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $sqlScript
            $cmd.CommandTimeout = 120
            $cmd.ExecuteNonQuery() | Out-Null
            
            $conn.Close()
            Write-Host " ✅" -ForegroundColor Green
        } catch {
            Write-Host " ⚠️ $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  ⚠️ SQL-Script nicht gefunden, Tabellen manuell erstellen" -ForegroundColor Yellow
}

# ============================================
# 7. WORKER-DIENST
# ============================================
Write-Host ""
Write-Host "[7/7] Worker-Dienst..." -ForegroundColor Yellow

if ($MitWorkerDienst -and (Test-Path "$InstallPfad\Worker\NovviaERP.Worker.exe")) {
    $serviceName = "NovviaERP-Worker"
    
    $existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existing) {
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $serviceName | Out-Null
        Start-Sleep -Seconds 2
    }
    
    New-Service -Name $serviceName `
                -BinaryPathName "$InstallPfad\Worker\NovviaERP.Worker.exe" `
                -DisplayName "NOVVIA ERP Worker" `
                -Description "Hintergrund-Prozesse (Sync, Zahlungen, etc.)" `
                -StartupType Automatic | Out-Null
    
    Start-Service -Name $serviceName -ErrorAction SilentlyContinue
    Write-Host "  ✅ Worker-Dienst installiert" -ForegroundColor Green
} else {
    Write-Host "  ⏭️ Worker-Dienst übersprungen" -ForegroundColor Gray
}

# ============================================
# DESKTOP-VERKNÜPFUNG
# ============================================
if (Test-Path "$InstallPfad\NovviaERP.WPF.exe") {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut("$env:PUBLIC\Desktop\NOVVIA ERP.lnk")
    $shortcut.TargetPath = "$InstallPfad\NovviaERP.WPF.exe"
    $shortcut.WorkingDirectory = $InstallPfad
    $shortcut.Save()
    Write-Host "  ✅ Desktop-Verknüpfung erstellt" -ForegroundColor Green
}

# ============================================
# FERTIG
# ============================================
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  ✅ Installation abgeschlossen!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Konfiguration:" -ForegroundColor White
Write-Host "    SQL Server:  $SqlServer" -ForegroundColor Gray
Write-Host "    SQL User:    $SqlUser" -ForegroundColor Gray
Write-Host "    Mandanten:   $($mandanten.Count) gefunden" -ForegroundColor Gray
Write-Host "    Pfad:        $InstallPfad" -ForegroundColor Gray
Write-Host ""
Write-Host "  Starten: Desktop-Verknüpfung 'NOVVIA ERP'" -ForegroundColor Cyan
Write-Host ""
