@echo off
title Bot_RPA - Terminal de Controle
:: Atalho de duplo clique para o terminal (controle\Terminal.ps1).
:: Uso: duplo clique (interativo) ou Terminal.bat "build teste"
cd /d "%~dp0"
if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Terminal.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Terminal.ps1" -Comando "%*"
)
if errorlevel 1 pause
