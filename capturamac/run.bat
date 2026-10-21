@echo off
cd /d "%~dp0.."
set PATH=%CD%\tools\mingw64\bin;%PATH%
if not exist capturamac\capturamac.exe (
    echo Compilando capturamac.c ...
    gcc -O2 -Wall -o capturamac\capturamac.exe capturamac\capturamac.c
    if errorlevel 1 (
        echo Falha na compilacao.
        pause
        exit /b 1
    )
)
capturamac\capturamac.exe
pause
