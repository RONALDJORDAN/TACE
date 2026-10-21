@echo off
rem Capturador de MAC (Bot RPA) - roda pela pasta do script, sem depender do cwd
setlocal
cd /d "%~dp0"
set MINGW=%~dp0..\mingw64\bin
if not exist "%MINGW%\gcc.exe" set "PATH=%PATH%"
if exist "%MINGW%\gcc.exe" set "PATH=%MINGW%;%PATH%"
if not exist "capturamac.exe" (
    echo Compilando capturamac.c ...
    "%MINGW%\gcc.exe" -O2 -Wall -o "capturamac.exe" "capturamac.c"
    if errorlevel 1 (
        echo Falha na compilacao.
        pause
        exit /b 1
    )
)
capturamac.exe %*
pause
