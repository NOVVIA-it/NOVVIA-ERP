#Requires -RunAsAdministrator
# NOVVIA ERP V2.0 - Windows Server Installation Script
# Für Windows Server 2019/2022

param(
    [string]$InstallPath = "C:\NovviaERP",
    [string]$SqlServer = "192.168.0.220",
    [string]$SqlDatabase = "Mandant_1",
    [string]$SqlUser = "sa",
    [string]$SqlPassword = "YourPassword",
    [switch]$InstallDotNet,
    [switch]$InstallSqlServer,
    [switch]$CreateFirewallRules
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   NOVVIA ERP V2.0 Installation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. .NET 8 Runtime installieren
if ($InstallDotNet) {
    Write-Host "`n[1/6] Installiere .NET 8 Runtime..." -ForegroundColor Yellow
    $dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/dotnet-runtime-8.0-win-x64.exe"
    $dotnetInstaller = "$env:TEMP\dotnet-runtime-8.exe"
    Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller
    Start-Process -FilePath $dotnetInstaller -Args "/install /quiet /norestart" -Wait
    Write-Host "   .NET 8 Runtime installiert" -ForegroundColor Green
}

# 2. Installationsverzeichnis erstellen
Write-Host "`n[2/6] Erstelle Verzeichnisse..." -ForegroundColor Yellow
$paths = @($InstallPath, "$InstallPath\logs", "$InstallPath\config", "$InstallPath\labels", "$InstallPath\exports")
foreach ($path in $paths) {
    if (!(Test-Path $path)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
}
Write-Host "   Verzeichnisse erstellt: $InstallPath" -ForegroundColor Green

# 3. Konfigurationsdatei erstellen
Write-Host "`n[3/6] Erstelle Konfiguration..." -ForegroundColor Yellow
$config = @{
    ConnectionStrings = @{
        JTL = "Server=$SqlServer;Database=$SqlDatabase;User Id=$SqlUser;Password=$SqlPassword;TrustServerCertificate=True;"
    }
    Shipping = @{
        DHL = @{ User = ""; Password = ""; BillingNumber = "" }
        DPD = @{ User = ""; Password = ""; Depot = "" }
        GLS = @{ User = ""; Password = ""; ShipperId = "" }
        UPS = @{ Token = ""; AccountNumber = "" }
    }
    Payment = @{
        PayPalClientId = ""
        PayPalSecret = ""
        MollieApiKey = ""
    }
    WooCommerce = @{
        Shops = @(
            @{ Name = "novvia.de"; Url = "https://novvia.de"; ConsumerKey = ""; ConsumerSecret = "" }
            @{ Name = "novvia-cosmetic.de"; Url = "https://novvia-cosmetic.de"; ConsumerKey = ""; ConsumerSecret = "" }
        )
    }
    Email = @{
        SmtpHost = "smtp.office365.com"
        SmtpPort = 587
        UseSsl = $true
        Username = ""
        Password = ""
        FromAddress = "info@novvia.de"
    }
    Firma = @{
        Name = "NOVVIA GmbH"
        Strasse = ""
        PLZ = ""
        Ort = ""
        Telefon = ""
        Email = "info@novvia.de"
        Website = "https://novvia.de"
        UStID = ""
        IBAN = ""
        BIC = ""
        Bank = ""
    }
} | ConvertTo-Json -Depth 5
$config | Out-File "$InstallPath\config\appsettings.json" -Encoding UTF8
Write-Host "   Konfiguration erstellt" -ForegroundColor Green

# 4. Firewall-Regeln
if ($CreateFirewallRules) {
    Write-Host "`n[4/6] Erstelle Firewall-Regeln..." -ForegroundColor Yellow
    New-NetFirewallRule -DisplayName "NovviaERP API" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName "NovviaERP API HTTPS" -Direction Inbound -Port 5001 -Protocol TCP -Action Allow -ErrorAction SilentlyContinue
    Write-Host "   Firewall-Regeln erstellt (Port 5000, 5001)" -ForegroundColor Green
}

# 5. Windows-Dienst erstellen
Write-Host "`n[5/6] Erstelle Windows-Dienst..." -ForegroundColor Yellow
$serviceName = "NovviaERP.API"
$serviceExists = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($serviceExists) {
    Stop-Service -Name $serviceName -Force
    sc.exe delete $serviceName
}
# Dienst wird nach dem Build erstellt
Write-Host "   Dienst wird nach dem Build registriert" -ForegroundColor Green

# 6. Desktop-Verknüpfung
Write-Host "`n[6/6] Erstelle Desktop-Verknüpfung..." -ForegroundColor Yellow
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:PUBLIC\Desktop\NOVVIA ERP.lnk")
$Shortcut.TargetPath = "$InstallPath\NovviaERP.WPF.exe"
$Shortcut.WorkingDirectory = $InstallPath
$Shortcut.IconLocation = "$InstallPath\novvia.ico"
$Shortcut.Save()
Write-Host "   Desktop-Verknüpfung erstellt" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "   Installation abgeschlossen!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nNächste Schritte:"
Write-Host "1. Kopieren Sie die kompilierten Dateien nach: $InstallPath"
Write-Host "2. Bearbeiten Sie: $InstallPath\config\appsettings.json"
Write-Host "3. Starten Sie NovviaERP.WPF.exe"
Write-Host "`nAPI-Dienst starten:"
Write-Host "   sc.exe create NovviaERP.API binPath= '$InstallPath\NovviaERP.API.exe'"
Write-Host "   sc.exe start NovviaERP.API"
