@echo off
title Capturador de MAC - Bot RPA
cd /d "%~dp0"
if not exist "capturamac.exe" (
    echo Executavel capturamac.exe nao encontrado nesta pasta.
    pause
    exit /b 1
)
if "%~1"=="" (
    capturamac.exe
    echo.
    pause
) else (
    capturamac.exe %*
)
