@echo off
title Bot_RPA - Central de Build
:: Atalho de duplo clique para o painel de build (controle\Build.ps1).
:: Uso: duplo clique (menu) ou Build.bat Teste | Build.bat Final | Build.bat Instalavel [-Sim]
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %*
if errorlevel 1 pause
