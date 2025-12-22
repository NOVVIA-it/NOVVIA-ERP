@echo off
cd /d C:\NovviaERP\src\NovviaERP\NovviaERP.WPF
echo Baue NovviaERP...
dotnet build -o C:\NovviaERP\build-v38
echo.
echo Starte Anwendung...
start C:\NovviaERP\build-v38\NovviaERP.WPF.exe
pause
