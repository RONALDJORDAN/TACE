@echo off
:: =============================================================================
::  Bot_RPA - Script Unificado Master (Versao Pluggable)
::  Arquivo: scripts\unified.bat
:: =============================================================================
::
::  Gera todos os artifacts necessarios: build development, build installer,
::  setup de dependencias, tools ADB, etc. em um unico script.
::
::  Modos de Operacao:
::    1. build          - Compila desenvolvimento (data/output)
::    2. release        - Compila release (BotRPA_Release)
::    3. installer       - Gera instalador Windows (BotRPA_Instalador_*)
::    4. deps           - Instala dependencias (GCC, .NET)
::    5. adb            - Baixa tools ADB
::    6. monitor        - Inicia monitoramento de tela
::    7. help           - Exibe lista completa de operacoes
::
:: =============================================================================

setlocal enabledelayedexpansion

if "%1"=="" goto :menu
if "%1"=="build" goto :build_mode
if "%1"=="release" goto :release_mode
if "%1"=="installer" goto :installer_mode
if "%1"=="deps" goto :deps_mode
if "%1"=="adb" goto :adb_mode
if "%1"=="monitor" goto :monitor_mode
if "%1"=="help" goto :help

:menu
echo.
echo ========================================================
echo [Bot_RPA] Bot_RPA - Script Unificado Master
::echo ========================================================
echo.
echo Modos de Operacao:
echo.  build          Compilar desenvolvimento (data/output)
echo.  release        Compilar release (BotRPA_Release)
echo.  installer       Gerar instalador Windows
echo.  deps            Instalar dependencias (GCC, .NET)
echo.  adb             Baixar tools ADB
echo.  monitor        Iniciar monitoramento de tela
echo.  help            Exibir esta ajuda
echo.
echo Uso: scripts\unified.bat [modo]

goto :eof

:build_mode
echo ========================================================
echo [Bot_RPA] Compilando Desenvolvimento
echo ========================================================

call scripts\build.bat

goto :eof

:release_mode
echo ========================================================
echo [Bot_RPA] Compilando Release
echo ========================================================

call scripts\build_release.bat

goto :eof

:installer_mode
echo ========================================================
echo [Bot_RPA] Compilando Instalador
echo ========================================================

call scripts\build_installer.bat

goto :eof

:deps_mode
echo ========================================================
echo [Bot_RPA] Instalando Dependencias
echo ========================================================

call scripts\install_dependencies.bat

goto :eof

:adb_mode
echo ========================================================
echo [Bot_RPA] Baixando Tools ADB
echo ========================================================

call scripts\setup_adb.bat

goto :eof

:monitor_mode
echo ========================================================
echo [Bot_RPA] Monitor de Tela
echo ========================================================

call scripts\monitor_tela.bat

goto :eof

:help
echo ========================================================
echo [Bot_RPA] Operacoes Unificadas
::echo ========================================================
echo.
echo Todos os scripts disponiveis podem ser executados via:
echo.  scripts\unified.bat build
::echo.  scripts\unified.bat release
echo.  scripts\unified.bat installer
echo.  scripts\unified.bat deps
echo.  scripts\unified.bat adb
echo.  scripts\unified.bat monitor
echo.
echo Scripts incluidos:
echo.  build.bat              - Compilacao de desenvolvimento
echo.  build_release.bat       - Compilacao de release
echo.  build_installer.bat     - Geracao de instalador
echo.  install_dependencies.bat - Instalacao de dependencias
echo.  setup_adb.bat          - Download de tools ADB
echo.  monitor_tela.bat       - Monitor de tela
echo.
echo ========================================================

goto :eof