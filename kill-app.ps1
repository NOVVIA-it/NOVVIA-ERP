Get-Process -Name "NovviaERP*" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*NovviaERP*" } | Stop-Process -Force
