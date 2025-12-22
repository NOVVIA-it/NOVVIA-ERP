@echo off
echo MSV3 GEHE Test startet...
echo.
powershell -ExecutionPolicy Bypass -Command "try { $r = Invoke-WebRequest -Uri 'https://www.gehe-auftragsservice.de/msv3/v2.0/VerbindungTesten' -Method POST -Body '<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:msv=\"urn:msv3:v2\"><soap:Body><msv:VerbindungTesten><msv:Clientsystem>NovviaERP</msv:Clientsystem><msv:Benutzerkennung>152776</msv:Benutzerkennung><msv:Kennwort>ajrjwo30</msv:Kennwort></msv:VerbindungTesten></soap:Body></soap:Envelope>' -ContentType 'application/soap+xml' -Headers @{Authorization='Basic MTUyNzc2OmFqcmp3bzMw'} -UseBasicParsing -TimeoutSec 20; Write-Host 'STATUS:' $r.StatusCode -ForegroundColor Green; Write-Host $r.Content } catch { Write-Host 'FEHLER:' $_.Exception.Message -ForegroundColor Red }"
echo.
echo.
pause
