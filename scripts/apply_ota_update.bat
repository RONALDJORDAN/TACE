@echo off
title InnovTech OTA Updater
echo ========================================================
echo   Plataforma InnovTech - Aplicando Atualizacao OTA...
echo ========================================================
echo.
echo Aguardando o encerramento do InnoRPAui.exe...
timeout /t 3 /nobreak >nul

set ZIP_PATH=%~1
set TARGET_DIR=%~2

if not exist "%ZIP_PATH%" (
    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%
    pause
    exit /b 1
)

echo Extraindo pacote de atualizacao...
powershell -Command "Expand-Archive -Path '%ZIP_PATH%' -DestinationPath '%TARGET_DIR%' -Force"

echo Atualizacao concluida com sucesso!
echo Reiniciando o Inno RPA...
timeout /t 2 /nobreak >nul

start "" "%TARGET_DIR%\InnoRPAui.exe"
exit /b 0
