@echo off
title PharmaPOS - Stop

rem Always run from the folder this script lives in (repo root).
cd /d "%~dp0"

where docker >NUL 2>&1
if %errorlevel% neq 0 (
  echo [ERROR] Docker was not found. Nothing to stop.
  pause
  exit /b 1
)

echo Stopping PharmaPOS stack ...
if /i "%~1"=="clean" goto clean

docker compose down
if %errorlevel% neq 0 (
  echo.
  echo [ERROR] "docker compose down" failed. See the output above.
  pause
  exit /b 1
)

echo.
echo All PharmaPOS containers stopped. Data volumes were kept.
echo To also delete all data (fresh start):  stop.bat clean
echo.
pause
exit /b 0

:clean
echo.
echo WARNING: this deletes ALL local dev data (database, Keycloak realm, images stay).
echo Use this when the Keycloak realm definition changed and must be reimported.
set /p CONFIRM="Type YES to confirm: "
if /i not "%CONFIRM%"=="YES" (
  echo Cancelled. Nothing was deleted.
  pause
  exit /b 0
)
docker compose down -v
if %errorlevel% neq 0 (
  echo.
  echo [ERROR] "docker compose down -v" failed. See the output above.
  pause
  exit /b 1
)
echo.
echo Everything stopped and all local data deleted. Run run.bat for a fresh start.
echo.
pause
