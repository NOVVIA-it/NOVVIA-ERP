# Kill all processes
Get-Process -Name "NovviaERP*" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "VBCSCompiler" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3

# Delete bin/obj
Remove-Item "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF\obj" -Recurse -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Build
Set-Location "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF"
dotnet build 2>&1 | Select-Object -Last 10
