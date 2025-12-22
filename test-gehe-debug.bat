@echo off
echo ========================================
echo MSV3 GEHE Debug Test
echo ========================================
echo.
echo 1. Teste Basis-URL...
curl -s -w "HTTP: %%{http_code}\n" -o nul "https://www.gehe-auftragsservice.de/msv3" --max-time 10
echo.
echo 2. Teste v2.0 URL...
curl -s -w "HTTP: %%{http_code}\n" -o nul "https://www.gehe-auftragsservice.de/msv3/v2.0" --max-time 10
echo.
echo 3. Teste VerbindungTesten mit Auth...
curl -s -w "\nHTTP: %%{http_code}" -X POST "https://www.gehe-auftragsservice.de/msv3/v2.0/VerbindungTesten" -H "Content-Type: text/xml" -u "152776:ajrjwo30" -d "<?xml version=\"1.0\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:msv=\"urn:msv3:v2\"><soap:Body><msv:VerbindungTesten><msv:Clientsystem>NovviaERP</msv:Clientsystem><msv:Benutzerkennung>152776</msv:Benutzerkennung><msv:Kennwort>ajrjwo30</msv:Kennwort></msv:VerbindungTesten></soap:Body></soap:Envelope>" --max-time 15
echo.
echo.
echo ========================================
pause
