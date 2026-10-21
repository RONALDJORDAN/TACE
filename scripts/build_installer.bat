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

REM --- 4. Sincroniza a versao no script do instalador (controle\BotRPA.iss) ---
set "BOTRPA_VER=!VER!"
powershell -NoProfile -Command "$f='controle\BotRPA.iss'; $v=$env:BOTRPA_VER; $c=[char]34; $s=Get-Content -LiteralPath $f -Raw; $s=$s -replace '(?m)^#define AppVersion .*$', ('#define AppVersion ' + $c + $v + $c); Set-Content -LiteralPath $f -Value $s -Encoding UTF8"
if errorlevel 1 (
    echo [ERRO] Falha ao sincronizar a versao em controle\BotRPA.iss.
    pause
    exit /b 1
)
echo [*] Versao gravada no script do instalador: !VER!

REM --- 5. Compila o instalador ---
echo.
echo [*] Compilando o instalador...
"!ISCC!" "controle\BotRPA.iss" > "%TEMP%\BotRPA_iscc.log" 2>&1
set "ISCC_ERR=%errorlevel%"

if not "%ISCC_ERR%"=="0" (
    echo.
    echo [ERRO] Falha ao compilar o instalador. Log:
    type "%TEMP%\BotRPA_iscc.log"
    pause
    exit /b 1
)

REM --- 6. Confere o resultado ---
set "SETUP=controle\BotRPA_Instalador_!VER!.exe"
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
