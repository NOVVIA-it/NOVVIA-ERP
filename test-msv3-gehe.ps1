# MSV3 GEHE Test-Script
# Fuehre dieses Script auf dem PC aus wo auch Vario 8 laeuft

$url = "https://www.gehe-auftragsservice.de/msv3/v2.0/VerbindungTesten"
$user = "152776"
$pass = "ajrjwo30"

$soapBody = @"
<?xml version="1.0" encoding="UTF-8"?>
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:msv="urn:msv3:v2">
   <soap:Body>
      <msv:VerbindungTesten>
         <msv:Clientsystem>NovviaERP</msv:Clientsystem>
         <msv:Benutzerkennung>$user</msv:Benutzerkennung>
         <msv:Kennwort>$pass</msv:Kennwort>
      </msv:VerbindungTesten>
   </soap:Body>
</soap:Envelope>
"@

$bytes = [System.Text.Encoding]::UTF8.GetBytes("${user}:${pass}")
$base64 = [Convert]::ToBase64String($bytes)

Write-Host "=== MSV3 VerbindungTesten ===" -ForegroundColor Cyan
Write-Host "URL: $url"

try {
    $response = Invoke-WebRequest -Uri $url -Method POST -Body $soapBody `
        -ContentType "application/soap+xml; charset=utf-8" `
        -Headers @{ "Authorization" = "Basic $base64" } `
        -UseBasicParsing -TimeoutSec 30

    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== ANTWORT ===" -ForegroundColor Cyan

    # XML formatieren
    $xml = [xml]$response.Content
    $xml.Save([Console]::Out)

} catch {
    Write-Host "FEHLER: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "Response: $errorBody" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Druecke Enter zum Beenden..."
Read-Host
