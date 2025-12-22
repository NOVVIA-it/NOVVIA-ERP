Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "NovviaERP*" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "VBCSCompiler" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "Prozesse beendet"
