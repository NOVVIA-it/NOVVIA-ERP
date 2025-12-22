# NOVVIA ERP V2.0 - Build Script
param([string]$OutputPath = ".\publish")

Write-Host "Building NOVVIA ERP V2.0..." -ForegroundColor Cyan

# Clean
if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }

# Build Core
dotnet publish .\NovviaERP.Core\NovviaERP.Core.csproj -c Release -o "$OutputPath\core"

# Build WPF
dotnet publish .\NovviaERP.WPF\NovviaERP.WPF.csproj -c Release -o "$OutputPath" --self-contained false -r win-x64

# Build API
dotnet publish .\NovviaERP.API\NovviaERP.API.csproj -c Release -o "$OutputPath\api" --self-contained false -r win-x64

Write-Host "Build complete! Output: $OutputPath" -ForegroundColor Green
