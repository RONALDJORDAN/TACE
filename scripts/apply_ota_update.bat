@echo off
title Bot_RPA OTA Updater
echo ========================================================
echo   Plataforma Bot_RPA - Aplicando Atualizacao OTA...
echo ========================================================
echo.
echo Aguardando o encerramento do BotRPAui.exe...
timeout /t 3 /nobreak >nul
taskkill /F /IM BotRPAui.exe /T 2>nul
timeout /t 2 /nobreak >nul

set "ZIP_PATH=%~1"
set "TARGET_DIR=%~2"
if "%TARGET_DIR%"=="" set "TARGET_DIR=%~dp0.."
set "LOG_FILE=%TARGET_DIR%\logs\ota_update_%date:~6,4%-%date:~3,2%-%date:~0,2%.log"
if not exist "%TARGET_DIR%\logs" mkdir "%TARGET_DIR%\logs" 2>nul

if not exist "%ZIP_PATH%" (
    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%>> "%LOG_FILE%" 2>&1
    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%
    pause
    exit /b 1
)

echo Extraindo pacote de atualizacao...>> "%LOG_FILE%" 2>&1
echo Extraindo pacote de atualizacao...
powershell -NoProfile -Command "Expand-Archive -LiteralPath $env:ZIP_PATH -DestinationPath $env:TARGET_DIR -Force">> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERRO] Falha ao extrair o pacote. Veja o log: %LOG_FILE%
    pause
    exit /b 1
)

echo Atualizacao concluida com sucesso!>> "%LOG_FILE%" 2>&1
echo Atualizacao concluida com sucesso!
echo Reiniciando o Bot_RPA...
timeout /t 2 /nobreak >nul

if exist "%TARGET_DIR%\BotRPAui.exe" (
    start "" "%TARGET_DIR%\BotRPAui.exe"
) else if exist "%TARGET_DIR%\data\output\BotRPAui.exe" (
    start "" "%TARGET_DIR%\data\output\BotRPAui.exe"
)
exit /b 0
