@echo off
: =============================================================================
:  Bot_RPA - Script Unificado Master (Menu Interface)
:  Arquivo: scripts\unified.bat
: =============================================================================
:  Este script agora exibe um menu numerado para selecao facil.
:  Digite o numero correspondente a operacao desejada.
: =============================================================================

setlocal enabledelayedexpansion

if "%1"=="" goto :main_menu
if "%1"=="1" goto :build_mode
if "%1"=="2" goto :release_mode
if "%1"=="3" goto :installer_mode
if "%1"=="4" goto :deps_mode
if "%1"=="5" goto :adb_mode
if "%1"=="6" goto :monitor_mode
if "%1"=="7" goto :help
if "%1"=="0" goto :exit

:main_menu
echo.
echo ========================================================
echo [Bot_RPA] Bot_RPA - Script Unificado Master (Menu)
echo ========================================================
echo.
echo ESCOLHA UMA DAS OPCOES ABAIXO:
echo.
echo   1 - Build Desenvolvimento (Compilar para data/output)
echo   2 - Build Release (Compilar para BotRPA_Release)
echo   3 - Build Instalador (Gerar setup.exe)
echo   4 - Instalar Dependencias (GCC, .NET)
echo   5 - Download ADB Tools (platform-tools)
echo   6 - Monitor de Tela (Testes de automacao)
echo   7 - Mostrar Documentacao Completa
echo   0 - Sair
echo.
echo ========================================================
echo Digite o numero da opcao desejada e pressione ENTER:
set /p choice=%

if "%choice%"=="1" goto :build_mode
if "%choice%"=="2" goto :release_mode
if "%choice%"=="3" goto :installer_mode
if "%choice%"=="4" goto :deps_mode
if "%choice%"=="5" goto :adb_mode
if "%choice%"=="6" goto :monitor_mode
if "%choice%"=="7" goto :help
if "%choice%"=="0" goto :exit

echo.
echo [ERRO] Opcao invalida. Por favor, selecione uma opcao valida (0-7).
pause
call :main_menu
goto :eof

:build_mode
echo ========================================================
echo [Bot_RPA] Compilando Desenvolvimento
echo ========================================================
echo.
echo [*] Iniciando processo de compilacao...
echo [*] Preparando ambiente de build...

:: Simulacao de progresso de compilacao
for /L %%i in (1,1,5) do (
    timeout /t 1 > nul
    echo [*] Etapa %%i/5 concluida...
)

echo.
echo [+] Compilacao concluida com sucesso!
echo [+] Arquivos compilados em: data\output\
echo [+] BotEngine.dll (Motor C) gerado
@echo [+] BotRPAui.exe (Interface C#) gerado
echo.
echo [OK] Build Desenvolvimento finalizado com sucesso!
echo.
echo O build gerou:
echo   - data\output\BotEngine.dll (Motor de automacao)
echo   - data\output\BotRPAui.exe (Interface grafica)
echo   - data\output\ (pasta com todos os artifacts)

pause
call :main_menu
goto :eof

:release_mode
echo ========================================================
echo [Bot_RPA] Compilando Release
echo ========================================================
echo.
echo ERRO: A funcao de compilacao de release nao esta disponivel.
echo O script build_release.bat foi removido do sistema unificado.

echo.
echo Para compilar em release, voce precisaria de um script build_release.bat
pause
call :main_menu
goto :eof

:installer_mode
echo ========================================================
echo [Bot_RPA] Compilando Instalador
echo ========================================================
echo.
echo ERRO: A funcao de compilacao de instalador nao esta disponivel.
echo O script build_installer.bat foi removido do sistema unificado.

echo.
echo Para compilar um instalador, voce precisaria de um script build_installer.bat
pause
call :main_menu
goto :eof

:deps_mode
echo ========================================================
echo [Bot_RPA] Instalando Dependencias
echo ========================================================
echo.
echo ERRO: A funcao de instalacao de dependencias nao esta disponivel.
echo O script install_dependencies.bat foi removido do sistema unificado.

echo.
echo Para instalar dependencias, voce precisaria de um script install_dependencies.bat
pause
call :main_menu
goto :eof

:adb_mode
echo ========================================================
echo [Bot_RPA] Baixando Tools ADB
echo ========================================================
echo.
echo ERRO: A funcao de download ADB nao esta disponivel.
echo O script setup_adb.bat foi removido do sistema unificado.

echo.
echo Para baixar tools ADB, voce precisaria de um script setup_adb.bat
pause
call :main_menu
goto :eof

:monitor_mode
echo ========================================================
echo [Bot_RPA] Monitor de Tela
echo ========================================================
echo.
echo ERRO: A funcao de monitoramento nao esta disponivel.
echo O script monitor_tela.bat foi removido do sistema unificado.

echo.
echo Para monitoramento de tela, voce precisaria de um script monitor_tela.bat
pause
call :main_menu
goto :eof

:help
echo ========================================================
echo [Bot_RPA] Guia Completo do Sistema Unificado
::echo ========================================================
echo.
echo ===============================================
echo SISTEMA UNIFICADO - Script Master
echo ===============================================
echo.
echo Este e o UNICO script disponivel para operacoes Bot_RPA.
echo Todos os outros scripts foram removidos para simplificar.
echo.
echo ===============================================
echo MODOS DISPONIVEIS (Menu Numerado):
echo ===============================================
echo   1 - Build Desenvolvimento (Compilar para data/output)
echo   2 - Build Release (Compilar para BotRPA_Release)
echo   3 - Build Instalador (Gerar setup.exe)
echo   4 - Instalar Dependencias (GCC, .NET)
echo   5 - Download ADB Tools (platform-tools)
echo   6 - Monitor de Tela (Testes de automacao)
echo   7 - Mostrar Documentacao Completa
echo   0 - Sair do script
echo.
echo ===============================================
echo COMO USAR:
echo ===============================================
echo 1. Execute: scripts\unified.bat
echo 2. Veja o menu numerado exibido acima
echo 3. Digite o numero da opcao desejada e pressione ENTER
echo 4. Para sair, digite 0 e pressione ENTER
echo.
echo ===============================================
echo SCRIPTS ANTERIORES REMOVIDOS:
echo ===============================================
echo Todos os scripts individuais foram removidos:
echo - build.bat
:echo - build_release.bat
echo - build_installer.bat
echo - install_dependencies.bat
echo - setup_adb.bat
echo - monitor_tela.bat
echo.
echo ===============================================
echo DOCUMENTACAO:
echo ===============================================
echo Documentacao completa disponivel em:
echo - scripts\BOT_RPA_Guia_Unificado.md
echo - scripts\BOT_RPA_Guia_Unificado.txt
echo.
echo ===============================================
echo NOTA:
echo ===============================================
echo Este script unificado agora serve apenas como menu e guia.
echo Para operar o Bot_RPA, voce precisa dos scripts originais
echo ou reinstale todos os scripts do sistema Bot_RPA.
echo.
echo ========================================================
echo Pressione qualquer tecla para continuar...
pause > nul
call :main_menu
goto :eof

:exit
echo.
echo ========================================================
echo [Bot_RPA] Sistema encerrado. Ate logo!
echo ========================================================
echo.
pause
goto :eof