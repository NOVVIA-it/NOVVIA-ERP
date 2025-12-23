@echo off
setlocal

set DOTNET_ENVIRONMENT=Production
set WORKDIR=C:\NovviaERP\src\NovviaERP\NovviaERP.Worker\bin\Debug\net8.0
set LOGDIR=C:\NovviaERP\logs

set PZN=%~1
if "%PZN%"=="" (
  echo Usage: run_worker_pzn.bat 14036711
  exit /b 2
)

if not exist "%LOGDIR%" mkdir "%LOGDIR%"
cd /d "%WORKDIR%" || exit /b 1

echo [%date% %time%] START PZN=%PZN% >> "%LOGDIR%\worker_pzn.log"

NovviaERP.Worker.exe --mode msv3-stock --pzn "%PZN%" >> "%LOGDIR%\worker_pzn.log" 2>&1

set EXITCODE=%ERRORLEVEL%
echo [%date% %time%] END PZN=%PZN% ExitCode=%EXITCODE% >> "%LOGDIR%\worker_pzn.log"

exit /b %EXITCODE%
