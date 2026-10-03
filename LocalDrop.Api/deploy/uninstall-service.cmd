@echo off
REM Rolls back install-service.cmd: removes the service and restores the IIS site.
REM Must be run from an elevated (Administrator) command prompt.
setlocal

set SERVICE_NAME=LocalDrop
set APPCMD=%windir%\System32\inetsrv\appcmd.exe

echo [1/2] Removing the Windows service...
sc.exe stop %SERVICE_NAME%
sc.exe delete %SERVICE_NAME%

echo [2/2] Restoring the IIS site...
"%APPCMD%" set site "LocalDrop" /serverAutoStart:true
"%APPCMD%" start site "LocalDrop"

echo.
echo Rollback complete.
endlocal
