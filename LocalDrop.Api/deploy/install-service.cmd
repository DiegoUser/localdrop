@echo off
REM Installs LocalDrop as a Windows service running on Kestrel.
REM Must be run from an elevated (Administrator) command prompt.
setlocal

set SERVICE_NAME=LocalDrop
set APP_EXE=C:\Proyectos\LocalDropServer\LocalDrop.Api.exe
set APPCMD=%windir%\System32\inetsrv\appcmd.exe

echo [1/3] Releasing port 5001 from IIS...
"%APPCMD%" stop site "LocalDrop"
"%APPCMD%" set site "LocalDrop" /serverAutoStart:false

echo [2/3] Registering the Windows service...
sc.exe create %SERVICE_NAME% binPath= "\"%APP_EXE%\"" start= auto DisplayName= "LocalDrop"
sc.exe description %SERVICE_NAME% "LocalDrop local file transfer server (Kestrel)."
sc.exe failure %SERVICE_NAME% reset= 86400 actions= restart/5000/restart/5000/restart/60000

echo [3/3] Starting the service...
sc.exe start %SERVICE_NAME%

echo.
echo Done. Check http://localhost:5001/health
endlocal
