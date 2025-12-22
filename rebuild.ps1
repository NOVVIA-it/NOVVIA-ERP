taskkill /IM NovviaERP.WPF.exe /F 2>$null
Start-Sleep -Seconds 2
Set-Location "C:\NovviaERP\src\NovviaERP\NovviaERP.WPF"
dotnet build 2>&1 | Select-Object -Last 8
