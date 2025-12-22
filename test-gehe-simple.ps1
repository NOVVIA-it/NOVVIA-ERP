Write-Host "Starte MSV3 Test..." -ForegroundColor Yellow

$url = "https://www.gehe-auftragsservice.de/msv3/v2.0/VerbindungTesten"

$soap = '<?xml version="1.0" encoding="UTF-8"?><soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:msv="urn:msv3:v2"><soap:Body><msv:VerbindungTesten><msv:Clientsystem>NovviaERP</msv:Clientsystem><msv:Benutzerkennung>152776</msv:Benutzerkennung><msv:Kennwort>ajrjwo30</msv:Kennwort></msv:VerbindungTesten></soap:Body></soap:Envelope>'

$cred = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("152776:ajrjwo30"))

try {
    Write-Host "Sende Request an: $url"

    $r = Invoke-RestMethod -Uri $url -Method POST -Body $soap -ContentType "application/soap+xml" -Headers @{Authorization="Basic $cred"} -TimeoutSec 15

    Write-Host "ERFOLG!" -ForegroundColor Green
    Write-Host $r.OuterXml
}
catch {
    Write-Host "Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    Write-Host "Fehler: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Fertig. Enter druecken..."
$null = Read-Host
