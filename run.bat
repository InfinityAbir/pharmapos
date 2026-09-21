@echo off
setlocal enabledelayedexpansion
title PharmaPOS - Docker Launcher

rem Always run from the folder this script lives in (repo root).
cd /d "%~dp0"

rem --- 1. Docker must be installed and running -------------------------------
where docker >NUL 2>&1
if %errorlevel% neq 0 (
  echo [ERROR] Docker was not found.
  echo Install Docker Desktop from https://www.docker.com/products/docker-desktop
  echo and make sure it is running, then run this script again.
  pause
  exit /b 1
)

rem --- 2. Environment file ----------------------------------------------------
if not exist .env (
  echo [.env] not found - creating it from .env.example ...
  copy /y .env.example .env >NUL
  echo.
  echo Edit .env and set POSTGRES_PASSWORD / KEYCLOAK_ADMIN_PASSWORD,
  echo then run this script again.
  pause
  exit /b 1
)

rem --- 2b. Backfill keys added to .env.example after .env was created ------------
rem (e.g. KEYCLOAK_CLIENT_SECRET). Missing keys are appended with example values.
powershell -NoProfile -Command "$have=@{}; Get-Content .\.env | ForEach-Object { if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=') { $have[$Matches[1]]=$true } }; Get-Content .\.env.example | ForEach-Object { if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$' -and -not $have.ContainsKey($Matches[1])) { Add-Content .\.env $_; Write-Output ('  backfilled missing key from .env.example: ' + $Matches[1]) } }"

rem Read ports from .env (defaults match .env.example).
set FRONTEND_PORT=4200
set API_PORT=8080
set KEYCLOAK_PORT=8090
for /f "usebackq tokens=1* delims==" %%a in (".env") do (
  if "%%a"=="FRONTEND_PORT" set FRONTEND_PORT=%%b
  if "%%a"=="API_PORT" set API_PORT=%%b
  if "%%a"=="KEYCLOAK_PORT" set KEYCLOAK_PORT=%%b
)
set FRONTEND_PORT=%FRONTEND_PORT: =%
set API_PORT=%API_PORT: =%
set KEYCLOAK_PORT=%KEYCLOAK_PORT: =%

rem --- 2b. Realm upgrade notice -------------------------------------------------
rem Keycloak imports the realm only on first boot. If data volumes already exist from
rem an older checkout, the running realm lacks the login/registration support
rem (confidential client, direct grants, staff roles). Detect and warn (non-fatal).
docker volume inspect pharmapos_postgres-data >NUL 2>&1
if %errorlevel%==0 (
  echo.
  echo [NOTE] Existing data volumes found. If login fails with "login service"
  echo errors, your Keycloak realm predates the app-owned login. Reset dev data with:
  echo   stop.bat clean
  echo (deletes ALL local dev data, then run.bat reimports everything fresh.)
  echo.
)

rem --- 3. Build + start backend and frontend ----------------------------------
echo.
echo Starting PharmaPOS stack: postgres + keycloak + api + web ...
echo (First run builds the images and imports Keycloak - this takes a few minutes.)
echo.
docker compose up --build -d
if %errorlevel% neq 0 (
  echo.
  echo [ERROR] "docker compose up" failed. See the output above.
  echo Tip: docker compose logs --tail=50
  pause
  exit /b 1
)

rem --- 4. Wait until the web UI answers ---------------------------------------
echo.
echo Waiting for the web UI at http://localhost:%FRONTEND_PORT%/ ...
set /a TRIES=0
:waitloop
curl -fsS -o NUL http://localhost:%FRONTEND_PORT%/ >NUL 2>&1
if %errorlevel%==0 goto ready
set /a TRIES+=1
if %TRIES% GEQ 60 goto slowready
timeout /t 5 /nobreak >NUL
echo   ... still starting ^(%TRIES%/60^)
goto waitloop

:slowready
echo.
echo [WARN] Still not responding after ~5 minutes. Opening the browser anyway -
echo the first Keycloak import can be slow. Check progress with:
echo   docker compose ps
echo   docker compose logs -f web api keycloak

:ready
rem --- 5. API + Keycloak must also answer: login/registration need both ------------
echo.
echo Web UI is up. Waiting for the API and Keycloak (needed for login) ...
set /a TRIES=0
:apiloop
curl -fsS -o NUL http://localhost:%API_PORT%/health/ready >NUL 2>&1
if %errorlevel%==0 goto keycloakwait
set /a TRIES+=1
if %TRIES% GEQ 24 goto apiskip
timeout /t 5 /nobreak >NUL
goto apiloop

:apiskip
echo [WARN] API not ready yet (docker compose logs -f api). Continuing anyway.

:keycloakwait
set /a TRIES=0
:kcloop
curl -fsS -o NUL http://localhost:%KEYCLOAK_PORT%/realms/pharmapos/.well-known/openid-configuration >NUL 2>&1
if %errorlevel%==0 goto openbrowser
set /a TRIES+=1
if %TRIES% GEQ 24 goto kcskip
timeout /t 5 /nobreak >NUL
goto kcloop

:kcskip
echo [WARN] Keycloak realm not reachable yet (docker compose logs -f keycloak). Continuing anyway.

:openbrowser
echo.
echo Opening http://localhost:%FRONTEND_PORT%/ in your browser ...
start "" "http://localhost:%FRONTEND_PORT%/"
echo.
echo   Web UI         : http://localhost:%FRONTEND_PORT%/
echo   API            : http://localhost:%API_PORT%  (Swagger: /swagger)
echo   Keycloak admin : http://localhost:%KEYCLOAK_PORT%
echo.
echo   Sign in with a demo account : admin@pharmapos.local / Platform#2026!
echo                                 owner@demo-pharmacy.local / Pharmacy#2026!
echo   Or register a new pharmacy at : http://localhost:%FRONTEND_PORT%/register
echo.
echo   Stop everything with:  stop.bat  (stop.bat clean wipes all local data)
echo.
pause
