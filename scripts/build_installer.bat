@echo off
setlocal enabledelayedexpansion
title Bot_RPA - Gerador do Instalador

REM ==========================================================================
REM  Gera o instalador (setup.exe) a partir do pacote BotRPA_Release\.
REM  Uso:  scripts\build_installer.bat
REM  Pre-requisito: o pacote portatil deve estar pronto em BotRPA_Release\
REM  (gerado por scripts\build_release.bat ou controle\Build.ps1 -Modo Final).
REM ==========================================================================

cd /d "%~dp0.."

echo ========================================================
echo [Bot_RPA] Gerador do Instalador (Inno Setup)
echo ========================================================
echo.

REM --- 1. Valida o pacote portatil ---
if not exist "BotRPA_Release\BotRPAui.exe" (
    echo [ERRO] Pacote nao encontrado: BotRPA_Release\BotRPAui.exe
    echo         Rode antes: controle\Build.ps1 -Modo Final
    pause
    exit /b 1
)

REM --- 2. Le a versao do build ---
set "VER="
if exist "config\version.txt" set /p VER=<config\version.txt
if "!VER!"=="" (
    echo [ERRO] config\version.txt vazio ou inexistente.
    pause
    exit /b 1
)
echo [*] Versao do build: !VER!

REM --- 3. Localiza o compilador do Inno Setup ---
set "ISCC="
for %%P in (
    "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    "%ProgramFiles%\Inno Setup 6\ISCC.exe"
    "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
) do (
    if not "!ISCC!"=="" goto :jaAchou
    if exist %%P set "ISCC=%%~P"
)
:jaAchou
if "!ISCC!"=="" (
    echo [ERRO] Inno Setup 6 nao encontrado.
    echo         Instale com: winget install --id JRSoftware.InnoSetup -e
    pause
    exit /b 1
)
echo [*] Compilador: !ISCC!

REM --- 4. Gera o script temporario com a versao atual ---
set "ISS_TMP=%TEMP%\BotRPA_build.iss"
if exist "!ISS_TMP!" del "!ISS_TMP!"

set "VER_LINHA=AppVersion={#AppVersion}"
set "VER_NOME=AppVerName={#AppName} {#AppVersion}"
set "ARQ_VER=OutputBaseFilename=InstaladorBot_RPA_!VER!"

powershell -NoProfile -Command ^
  "$src = Get-Content -LiteralPath 'controle\BotRPA.iss' -Raw; " ^
  "$src = $src -replace '(?m)^#define AppVersion .*$', '%%VER_LINHA%%'; " ^
  "$src = $src -replace '(?m)^#define AppVerName .*$', '%%VER_NOME%%'; " ^
  "$src = $src -replace '(?m)^OutputBaseFilename=.*$', '%%ARQ_VER%%'; " ^
  "Set-Content -LiteralPath $env:ISS_TMP -Value $src -Encoding UTF8" 2>nul
if not exist "!ISS_TMP!" (
    echo [ERRO] Falha ao gerar o script temporario do Inno Setup.
    pause
    exit /b 1
)

REM --- 5. Compila o instalador ---
echo.
echo [*] Compilando o instalador...
pushd "%TEMP%"
"!ISCC!" "BotRPA_build.iss" > "%TEMP%\BotRPA_iscc.log" 2>&1
set "ISCC_ERR=%errorlevel%"
popd

if not "%ISCC_ERR%"=="0" (
    echo.
    echo [ERRO] Falha ao compilar o instalador. Log:
    type "%TEMP%\BotRPA_iscc.log"
    pause
    exit /b 1
)

REM --- 6. Confere o resultado ---
set "SETUP=controle\InstaladorBot_RPA_!VER!.exe"
if not exist "!SETUP!" (
    echo [ERRO] Instalador nao gerado: !SETUP!
    pause
    exit /b 1
)

echo.
echo [OK] Instalador gerado: !SETUP!
for %%F in ("!SETUP!") do echo      %%~zF bytes
echo.
echo O instalador:
echo   - Instala em C:\Program Files\Bot_RPA
echo   - Grava os DADOS em %%ProgramData%%\Bot_RPA
echo   - Atualizacao preserva os dados do usuario
echo   - Desinstalacao preserva os dados (com opcao de apagar)
echo.
pause
exit /b 0
