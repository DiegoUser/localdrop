@echo off
REM Publishes the current source to the live deploy folder.
REM The running service locks the binaries, so it must be stopped first.
REM Must be run from an elevated (Administrator) command prompt.
setlocal

set SERVICE_NAME=LocalDrop
set PROJECT_DIR=%~dp0..
set DEPLOY_DIR=C:\Proyectos\LocalDropServer

echo [1/3] Stopping the service...
sc.exe stop %SERVICE_NAME%

echo [2/3] Publishing...
dotnet publish "%PROJECT_DIR%\LocalDrop.Api.csproj" -c Release -o "%DEPLOY_DIR%"
if errorlevel 1 (
  echo Publish failed. Restarting the previous version...
  sc.exe start %SERVICE_NAME%
  exit /b 1
)
del /q "%DEPLOY_DIR%\web.config" 2>nul

echo [3/3] Starting the service...
sc.exe start %SERVICE_NAME%

echo.
echo Done. Check http://localhost:5001/health
endlocal
