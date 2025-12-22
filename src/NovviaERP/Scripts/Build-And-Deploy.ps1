#Requires -RunAsAdministrator
# ============================================
# NOVVIA ERP - Build & Deploy
# Kompiliert und installiert alles
# ============================================

param(
    [string]$InstallPfad = "C:\NovviaERP",
    [string]$SqlServer = "",
    [string]$SqlUser = "",
    [string]$SqlPass = "",
    [switch]$NurBuild = $false
)

$ErrorActionPreference = "Stop"

Clear-Host
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  NOVVIA ERP - Build & Deploy" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Wo liegt das Script?
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Get-Location }
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "Projektverzeichnis: $projectRoot" -ForegroundColor Gray
Write-Host ""

# ============================================
# 1. .NET SDK PRÜFEN
# ============================================
Write-Host "[1/6] Prüfe .NET SDK..." -ForegroundColor Yellow

$dotnetSdk = dotnet --list-sdks 2>$null | Select-String "8\."
if (-not $dotnetSdk) {
    Write-Host "  ❌ .NET 8 SDK nicht gefunden!" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Bitte installieren von:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    Write-Host "  → .NET 8.0 SDK (nicht Runtime!)" -ForegroundColor Gray
    Write-Host ""
    exit 1
}
Write-Host "  ✅ .NET SDK gefunden: $($dotnetSdk -split ' ' | Select-Object -First 1)" -ForegroundColor Green

# ============================================
# 2. RESTORE
# ============================================
Write-Host ""
Write-Host "[2/6] Restore NuGet Pakete..." -ForegroundColor Yellow

Push-Location $projectRoot
dotnet restore --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ❌ Restore fehlgeschlagen!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "  ✅ Pakete wiederhergestellt" -ForegroundColor Green

# ============================================
# 3. BUILD WPF CLIENT (Self-Contained)
# ============================================
Write-Host ""
Write-Host "[3/6] Build WPF Client (Self-Contained)..." -ForegroundColor Yellow

dotnet publish NovviaERP.WPF/NovviaERP.WPF.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$projectRoot\publish\Client" `
    --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ❌ Build fehlgeschlagen!" -ForegroundColor Red
    Pop-Location
    exit 1
}

# EXE umbenennen
if (Test-Path "$projectRoot\publish\Client\NovviaERP.WPF.exe") {
    Move-Item "$projectRoot\publish\Client\NovviaERP.WPF.exe" "$projectRoot\publish\Client\NovviaERP.exe" -Force
}

Write-Host "  ✅ Client kompiliert" -ForegroundColor Green

# ============================================
# 4. BUILD WORKER (Self-Contained)
# ============================================
Write-Host ""
Write-Host "[4/6] Build Worker Service (Self-Contained)..." -ForegroundColor Yellow

dotnet publish NovviaERP.Worker/NovviaERP.Worker.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$projectRoot\publish\Worker" `
    --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "  ⚠️ Worker Build fehlgeschlagen (optional)" -ForegroundColor Yellow
} else {
    Write-Host "  ✅ Worker kompiliert" -ForegroundColor Green
}

Pop-Location

# ============================================
# 5. SQL SCRIPTS KOPIEREN
# ============================================
Write-Host ""
Write-Host "[5/6] Kopiere SQL Scripts..." -ForegroundColor Yellow

New-Item -ItemType Directory -Path "$projectRoot\publish\Scripts" -Force | Out-Null
Copy-Item "$projectRoot\Scripts\*.sql" "$projectRoot\publish\Scripts\" -Force
Copy-Item "$projectRoot\INSTALLATION*.md" "$projectRoot\publish\" -Force
Copy-Item "$projectRoot\README.md" "$projectRoot\publish\" -Force

Write-Host "  ✅ Scripts kopiert" -ForegroundColor Green

# ============================================
# 6. ZIP ERSTELLEN
# ============================================
Write-Host ""
Write-Host "[6/6] Erstelle ZIP-Pakete..." -ForegroundColor Yellow

$publishDir = "$projectRoot\publish"
$timestamp = Get-Date -Format "yyyyMMdd"

# Client ZIP (für Arbeitsplätze - nur EXE)
$clientZip = "$projectRoot\NovviaERP-Client-$timestamp.zip"
Compress-Archive -Path "$publishDir\Client\*" -DestinationPath $clientZip -Force
$clientSize = [math]::Round((Get-Item $clientZip).Length / 1MB, 1)
Write-Host "  ✅ Client: NovviaERP-Client-$timestamp.zip ($clientSize MB)" -ForegroundColor Green

# Server ZIP (komplett mit Worker)
$serverZip = "$projectRoot\NovviaERP-Server-$timestamp.zip"
Compress-Archive -Path "$publishDir\*" -DestinationPath $serverZip -Force
$serverSize = [math]::Round((Get-Item $serverZip).Length / 1MB, 1)
Write-Host "  ✅ Server: NovviaERP-Server-$timestamp.zip ($serverSize MB)" -ForegroundColor Green

# ============================================
# FERTIG - NUR BUILD
# ============================================
if ($NurBuild) {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "  ✅ Build abgeschlossen!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Erstellte Dateien:" -ForegroundColor White
    Write-Host "    $clientZip" -ForegroundColor Gray
    Write-Host "    $serverZip" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Client-ZIP: Für Arbeitsplätze (nur EXE)" -ForegroundColor Gray
    Write-Host "  Server-ZIP: Komplette Installation" -ForegroundColor Gray
    Write-Host ""
    exit 0
}

# ============================================
# INSTALLATION (falls gewünscht)
# ============================================
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Installation starten?" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$install = Read-Host "Jetzt auf diesem Server installieren? (j/n)"
if ($install -ne "j") {
    Write-Host ""
    Write-Host "Build abgeschlossen. ZIP-Dateien bereit." -ForegroundColor Green
    exit 0
}

# SQL Server abfragen falls nicht übergeben
if ([string]::IsNullOrEmpty($SqlServer)) {
    Write-Host ""
    $SqlServer = Read-Host "SQL Server (z.B. localhost\JTLWAWI oder 24.134.81.65,2107\NOVVIAS05)"
}

if ([string]::IsNullOrEmpty($SqlUser)) {
    $SqlUser = Read-Host "SQL Benutzer [sa]"
    if ([string]::IsNullOrEmpty($SqlUser)) { $SqlUser = "sa" }
}

if ([string]::IsNullOrEmpty($SqlPass)) {
    $securePass = Read-Host "SQL Passwort" -AsSecureString
    $SqlPass = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePass))
}

# Installieren
Write-Host ""
Write-Host "Installiere nach $InstallPfad..." -ForegroundColor Yellow

# Ordner erstellen
New-Item -ItemType Directory -Path $InstallPfad -Force | Out-Null
New-Item -ItemType Directory -Path "$InstallPfad\Logs" -Force | Out-Null
New-Item -ItemType Directory -Path "$InstallPfad\Worker" -Force | Out-Null

# Dateien kopieren
Copy-Item "$publishDir\Client\*" "$InstallPfad\" -Recurse -Force
Copy-Item "$publishDir\Worker\*" "$InstallPfad\Worker\" -Recurse -Force
Copy-Item "$publishDir\Scripts\*" "$InstallPfad\Scripts\" -Recurse -Force

# Profil erstellen
$profilePath = "$env:APPDATA\NovviaERP"
New-Item -ItemType Directory -Path $profilePath -Force | Out-Null

# SQL Verbindung testen und Mandanten holen
Write-Host "Teste SQL-Verbindung..." -ForegroundColor Gray
$connStr = "Server=$SqlServer;Database=master;User Id=$SqlUser;Password=$SqlPass;TrustServerCertificate=True;Connection Timeout=10"

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT name FROM sys.databases WHERE name LIKE 'Mandant_%' OR name = 'eazybusiness' ORDER BY name"
    $reader = $cmd.ExecuteReader()
    
    $mandanten = @()
    while ($reader.Read()) {
        $dbName = $reader.GetString(0)
        $name = switch ($dbName) {
            "eazybusiness" { "eB-Standard" }
            "Mandant_1" { "NOVVIA" }
            "Mandant_2" { "NOVVIA_PHARM" }
            "Mandant_3" { "PA" }
            "Mandant_5" { "NOVVIA_TEST" }
            default { $dbName }
        }
        $mandanten += @{ Name = $name; Datenbank = $dbName; Aktiv = $true }
    }
    $reader.Close()
    $conn.Close()
    
    Write-Host "  ✅ $($mandanten.Count) Mandanten gefunden" -ForegroundColor Green
} catch {
    Write-Host "  ❌ SQL Fehler: $($_.Exception.Message)" -ForegroundColor Red
    $mandanten = @(
        @{ Name = "NOVVIA"; Datenbank = "Mandant_1"; Aktiv = $true }
    )
}

# Profil speichern
$profile = @(@{
    Name = "Standard"
    Server = $SqlServer
    SqlBenutzer = $SqlUser
    SqlPasswort = $SqlPass
    Mandanten = $mandanten
})
$profile | ConvertTo-Json -Depth 5 | Set-Content "$profilePath\profile.json" -Encoding UTF8

# Desktop-Verknüpfung
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$env:PUBLIC\Desktop\NOVVIA ERP.lnk")
$shortcut.TargetPath = "$InstallPfad\NovviaERP.exe"
$shortcut.WorkingDirectory = $InstallPfad
$shortcut.IconLocation = "$InstallPfad\NovviaERP.exe,0"
$shortcut.Save()

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  ✅ Installation abgeschlossen!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Starten: Desktop 'NOVVIA ERP'" -ForegroundColor Cyan
Write-Host ""
