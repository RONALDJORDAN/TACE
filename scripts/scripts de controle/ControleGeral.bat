@echo off
setlocal enabledelayedexpansion
title Bot_RPA - Controle Geral
color 0B

:: ==========================================================================
::  Plataforma Bot_RPA - CONTROLE GERAL (arquivo unico de operacoes)
::
::  Este script substitui e corrige (em um unico arquivo):
::    PainelBuild.bat / Build.ps1 / Terminal.bat / Terminal.ps1 / unified.bat
::    build.bat / build_release.bat / build_installer.bat
::    install_dependencies.bat / setup_adb.bat / monitor_tela.bat
::
::  Auditorias: docs\registros\YYYY-MM-DD\HH-MM_Plataforma_BotRPA_DESKTOP_BUILD
::
::  Uso:
::    Duplo clique ............ menu interativo
::    ControleGeral.bat 1..9 ... executa modo direto (sem pausas)
::    ControleGeral.bat build ^| release ^| installer ^| deps ^| adb ^| monitor ^|
::                            tools ^| registros ^| ajuda
::    ControleGeral.bat 0 ...... sai
::
::  Sub-lanchonetes dev (ferramentas auxiliares .ps1/py) ficam em scripts\scripts de teste
:: ==========================================================================

for %%I in ("%~dp0..\..") do set "ROOT_DIR=%%~fI"
if not exist "%ROOT_DIR%\config" mkdir "%ROOT_DIR%\config"

if "%~1"=="" ( set "INTERATIVO=1" & goto main_menu )
set "INTERATIVO=0"
call :dispatch %*
set "RC=%errorlevel%"
if "%RC%"=="9" exit /b 0
exit /b %RC%

:: --------------------------------------------------------------------------
:dispatch
if "%~1"=="1" goto build_teste
if "%~1"=="2" goto build_release
if "%~1"=="3" goto build_installer
if "%~1"=="4" goto deps
if "%~1"=="5" goto adb_setup
if "%~1"=="6" goto monitor_mode
if "%~1"=="7" goto tools_menu
if /i "%~1"=="8" goto help
if "%~1"=="9" goto enviar_registros_menu
if /i "%~1"=="help" goto help
if /i "%~1"=="ajuda" goto help
if /i "%~1"=="build" goto build_teste
if /i "%~1"=="release" goto build_release
if /i "%~1"=="installer" goto build_installer
if /i "%~1"=="deps" goto deps
if /i "%~1"=="adb" goto adb_setup
if /i "%~1"=="monitor" goto monitor_mode
if /i "%~1"=="tools" goto tools_menu
if /i "%~1"=="registros" goto enviar_registros_menu
if /i "%~1"=="enviar" goto enviar_registros_menu
if /i "%~1"=="logs" goto enviar_registros_menu
if "%~1"=="0" goto goodbye
echo.
echo [ERRO] Opcao invalida: '%~1'
echo        Use 0-9 (ou 0 para sair). Ex: ControleGeral.bat 1
exit /b 1

:: --------------------------------------------------------------------------
:main_menu
cls
echo.
echo  ==============================================================
echo    BOT_RPA  ::  PLATAFORMA DE AUTOMACAO
echo  ==============================================================
echo    Raiz     : %ROOT_DIR%
call :read_version
echo    Versao   : %VER_TXT%
call :read_tool_status
echo    Ambiente : GCC %GCC_ST%   ^|   .NET %DOTNET_ST%
echo  --------------------------------------------------------------
echo    BUILD
echo      [1] Teste .................. motor C + UI em data\output
echo      [2] Release ................ pacote portatil BotRPA_Release
echo      [3] Instalador ............. setup.exe via Inno Setup
echo      [4] Dependencias ........... GCC + .NET 8 SDK
echo  --------------------------------------------------------------
echo    OPERACAO
echo      [5] ADB .................... baixar platform-tools
echo      [6] Monitor de tela ........ captura continua via ADB
echo      [7] Ferramentas dev ........ fluxo, XML e dispositivos
echo      [8] Ajuda .................. detalhes e auditoria
echo      [9] Enviar registros ....... zipar docs\registros + logs
echo  --------------------------------------------------------------
echo      [0] Sair
echo  ==============================================================
echo.
set "ESCOLHA="
set /p ESCOLHA="  Opcao: "
if "%ESCOLHA%"=="" ( echo    ^> Opcao vazia. Tente novamente. & timeout /t 1 >nul & goto main_menu )
if "%ESCOLHA%"=="0" goto goodbye
call :dispatch "%ESCOLHA%"
if errorlevel 1 (
    call :pause_if_interativo
)
goto main_menu

:: --------------------------------------------------------------------------
:build_teste
cd /d "%ROOT_DIR%"
echo.
echo ========================================================
echo    [1] BUILD TESTE - motor C + UI em data\output
echo ========================================================

call :gen_version

:: Encerra qualquer instancia da UI para liberar o executavel
taskkill /F /IM BotRPAui.exe /T 2>nul

:: PATH do GCC instalado pelo WinGet (garante que gcc sempre e encontrado)
set "PATH=%PATH%;%USERPROFILE%\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin"

:: Compilar o Engine em C (DLL) - Motores Separados Vision7 e TabMulti
mkdir "data\output" 2>nul
set SRCS_CORE=src\api.c src\core\drivers\adb.c src\core\data\municipios.c src\core\data\cofre.c src\core\data\paths.c src\core\data\dispositivos.c src\core\config\config.c src\core\learn\learn_db.c src\core\learn\learn_engine.c
set SRCS_V7=src\core\functions\vision7\f1_vision_tab7.c src\core\functions\vision7\f2_vision_tab7.c src\core\functions\vision7\f3_vision_tab7.c src\core\functions\vision7\f4_vision_tab7.c src\core\functions\vision7\f5_vision_tab7.c src\core\functions\vision7\f6_vision_tab7.c src\core\functions\vision7\f8_vision_tab7.c
set SRCS_M7=src\core\functions\tabmulti\f1_tabmulti_m7.c src\core\functions\tabmulti\f2_tabmulti_m7.c src\core\functions\tabmulti\f3_tabmulti_m7.c src\core\functions\tabmulti\f4_tabmulti_m7.c src\core\functions\tabmulti\f5_tabmulti_m7.c src\core\functions\tabmulti\m7_helpers.c
set SRCS_T20=src\core\functions\nokia_t20\f1_nokia_t20.c src\core\functions\nokia_t20\f2_nokia_t20.c src\core\functions\nokia_t20\f3_nokia_t20.c

echo [*] Compilando BotEngine.dll (Motor V7 + M7 + T20)...
>> "%AUDIT_DIR%\build.log" echo --- gcc BotEngine.dll ---
gcc -shared %SRCS_CORE% %SRCS_V7% %SRCS_M7% %SRCS_T20% -o data\output\BotEngine.dll -static -lcomctl32 -lgdi32 -luser32 -lcrypt32 -mwindows -D_WIN32_WINNT=0x0A00 -DWINVER=0x0A00 >> "%AUDIT_DIR%\build.log" 2>&1
set GCC_ERR=%errorlevel%
if %GCC_ERR% neq 0 (
    echo [!] ERRO na compilacao do motor C. Log: %AUDIT_DIR%\build.log
    type "%AUDIT_DIR%\build.log"
    call :pause_if_interativo
    exit /b %GCC_ERR%
)

:: Compilar a Interface Grafica em C# WPF (.NET 8)
echo [*] Compilando BotRpaUI (C# WPF) Self-Contained Offline...
:: Reforca o encerramento da UI (pode ter sido reaberta durante o build)
taskkill /F /IM BotRPAui.exe /T 2>nul
cd /d "%ROOT_DIR%\src\ui"
>> "..\..\%AUDIT_DIR%\build.log" echo --- dotnet publish ---
"C:\Program Files\dotnet\dotnet.exe" publish BotRpaUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ..\..\data\output\ >> "..\..\%AUDIT_DIR%\build.log" 2>&1
set DOTNET_ERR=%errorlevel%
:: Limpar intermediarios para economizar espaco em disco
rd /s /q obj 2>nul
rd /s /q bin 2>nul
del /f /q *.wpftmp.csproj 2>nul
cd /d "%ROOT_DIR%"

if %DOTNET_ERR% neq 0 (
    echo [!] ERRO na compilacao da UI. Log: %AUDIT_DIR%\build.log
    type "%AUDIT_DIR%\build.log"
    call :pause_if_interativo
    exit /b %DOTNET_ERR%
)

if not exist "data\output\municipios.csv" (
    if exist "config\municipios.csv" copy /y "config\municipios.csv" "data\output\municipios.csv" >nul
)
mkdir "data\output\arquivos" 2>nul
mkdir "data\output\arquivos\apks" 2>nul
mkdir "data\output\arquivos\pacotes" 2>nul
>> "%AUDIT_DIR%\build.log" echo Resultado: SUCESSO
echo.
echo [+] Compilacao concluida com sucesso!
echo [+] Motor     : data\output\BotEngine.dll
echo [+] Interface : data\output\BotRPAui.exe
echo [+] Auditoria : %AUDIT_DIR%

:: Pergunta se deseja enviar os registros atuais (somente build TESTE avulso)
if /i not "%BUILD_MODE%"=="RELEASE" call :perguntar_enviar_registros

call :pause_if_interativo
exit /b 0

:: --------------------------------------------------------------------------
:build_release
cd /d "%ROOT_DIR%"
echo.
echo ========================================================
echo    [2] BUILD RELEASE - pacote portatil PnP (BotRPA_Release)
echo ========================================================

:: 1. Compila no modo RELEASE (reutiliza a rotina de Build TESTE)
set "BUILD_MODE=RELEASE"
call :build_teste
set "BUILD_MODE="
if errorlevel 1 (
    echo.
    echo [ERRO] Build TESTE falhou. Build RELEASE cancelado.
    call :pause_if_interativo
    exit /b 1
)

:: 2. Assina os binarios antes de empacotar (o release e o instalador herdam)
call :sign_file "data\output\BotRPAui.exe"
call :sign_file "data\output\BotEngine.dll"

:: 3. Prepara a pasta de Release
set "RELEASE_DIR=BotRPA_Release"
echo [*] Criando pasta de Release: %RELEASE_DIR%
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%"

:: 4. Copia Executavel e Motor DLL
echo [*] Copiando binarios...
copy /y "data\output\BotRPAui.exe"  "%RELEASE_DIR%\" >nul
copy /y "data\output\BotEngine.dll" "%RELEASE_DIR%\" >nul

:: 5. Copia Dependencias Estruturais (SEM tools\mingw64)
echo [*] Copiando pasta config e tools...
xcopy /s /i /y "config" "%RELEASE_DIR%\config" >nul
mkdir "%RELEASE_DIR%\tools" 2>nul
if exist "tools\platform-tools"   xcopy /s /e /i /y "tools\platform-tools"   "%RELEASE_DIR%\tools\platform-tools"   >nul
if exist "tools\reverse_tethering" xcopy /s /e /i /y "tools\reverse_tethering" "%RELEASE_DIR%\tools\reverse_tethering" >nul
if exist "tools\mac_address_finder.apk" copy /y "tools\mac_address_finder.apk" "%RELEASE_DIR%\tools\" >nul
echo [+][OK] Tools copiadas (platform-tools + reverse_tethering + mac_address_finder.apk, sem mingw64).

:: 6. Banco de dados do sistema COM os registros dos aparelhos (ordem explicita
:: do usuario em 28/09/2026)
echo [*] Embarcando banco de dados com os registros dos aparelhos...
mkdir "%RELEASE_DIR%\data" 2>nul
if exist "data\bot_database.db" (
    copy /y "data\bot_database.db" "%RELEASE_DIR%\data\bot_database.db" >nul
    echo [+][OK] bot_database.db embarcado com os registros.
) else (
    echo [!] AVISO: data\bot_database.db nao encontrado - release saira sem registros.
)
:: Aprendizado da IA (bot_learn.json)
if exist "data\output\data\bot_learn.json" (
    copy /y "data\output\data\bot_learn.json" "%RELEASE_DIR%\data\" >nul
    echo [+][OK] Aprendizado BotLearn copiado.
) else if exist "data\bot_learn.json" (
    copy /y "data\bot_learn.json" "%RELEASE_DIR%\data\" >nul
)

:: 7. Pastas de sistema (logs de runtime + registros do app)
echo [*] Criando pastas de sistema...
mkdir "%RELEASE_DIR%\logs" 2>nul
mkdir "%RELEASE_DIR%\docs\registros" 2>nul

:: 8. Pacotes de municipios, APKs e Arquivos organizados
echo [*] Copiando municipios, APKs e Arquivos...
mkdir "%RELEASE_DIR%\data\input\municipios" 2>nul
mkdir "%RELEASE_DIR%\data\input\apps" 2>nul
if exist "data\input\municipios" (
    xcopy /s /e /i /y "data\input\municipios" "%RELEASE_DIR%\data\input\municipios" >nul
    echo [+][OK] Diretorios data\input\municipios copiados.
)
mkdir "%RELEASE_DIR%\Arquivos" 2>nul
if exist "Arquivos" (
    xcopy /s /e /i /y "Arquivos" "%RELEASE_DIR%\Arquivos" >nul
    echo [+][OK] Diretorio Arquivos copiado.
)

:: 9. Seed publico de municipios (SEM credenciais: cofre DPAPI e por maquina)
if not exist "%RELEASE_DIR%\municipios.csv" (
    if exist "config\municipios.csv" copy /y "config\municipios.csv" "%RELEASE_DIR%\municipios.csv" >nul
)

echo.
echo ========================================================
echo [SUCESSO] Pacote Release criado!
echo O software esta pronto na pasta: %RELEASE_DIR%
echo Basta zipar esta pasta e enviar para o usuario final.
echo ========================================================
call :pause_if_interativo
goto :eof

:: --------------------------------------------------------------------------
:build_installer
cd /d "%ROOT_DIR%"
echo.
echo ========================================================
echo    [3] BUILD INSTALADOR - Inno Setup
echo ========================================================

:: 1. Valida o pacote portatil (gera automaticamente se necessario/desatualizado)
set "RELEASE_VER="
if exist "BotRPA_Release\config\version.txt" set /p RELEASE_VER=<"BotRPA_Release\config\version.txt"
set "CURRENT_VER="
if exist "config\version.txt" set /p CURRENT_VER=<"config\version.txt"

if not exist "BotRPA_Release\BotRPAui.exe" (
    echo [*] Pacote Release ausente. Executando Build RELEASE primeiro...
    call :build_release
    if errorlevel 1 (
        echo [ERRO] Build RELEASE falhou. Instalador cancelado.
        call :pause_if_interativo
        exit /b 1
    )
) else if /i not "!RELEASE_VER!"=="!CURRENT_VER!" (
    echo [i] Pacote Release desatualizado: !RELEASE_VER! / atual !CURRENT_VER!. Recompilando...
    call :build_release
    if errorlevel 1 (
        echo [ERRO] Build RELEASE falhou. Instalador cancelado.
        call :pause_if_interativo
        exit /b 1
    )
)

if not exist "BotRPA_Release\BotRPAui.exe" (
    echo [ERRO] Pacote nao encontrado: BotRPA_Release\BotRPAui.exe
    echo        Rode a opcao [2] Build RELEASE manualmente.
    call :pause_if_interativo
    exit /b 1
)

:: 2. Le a versao do build (trim total)
set "VER="
if exist "config\version.txt" for /f "delims=" %%V in ('powershell -NoProfile -Command "(Get-Content config\version.txt -TotalCount 1).Trim()"') do set "VER=%%V"
if "!VER!"=="" (
    echo [ERRO] config\version.txt vazio ou inexistente.
    call :pause_if_interativo
    exit /b 1
)
echo [*] Versao do build: !VER!

:: 3. Localiza o compilador do Inno Setup
set "ISCC="
for %%P in (
    "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    "%ProgramFiles%\Inno Setup 6\ISCC.exe"
    "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
) do if exist %%P if not defined ISCC set "ISCC=%%~P"
if "!ISCC!"=="" (
    echo [ERRO] Inno Setup 6 nao encontrado.
    echo        Instale com: winget install --id JRSoftware.InnoSetup -e
    call :pause_if_interativo
    exit /b 1
)
echo [*] Compilador: !ISCC!

:: 4. Sincroniza a versao no script do instalador
set "BOTRPA_VER=!VER!"
set "ISS_PATH=%~dp0BotRPA.iss"
powershell -NoProfile -Command "$f=$env:ISS_PATH; $v=$env:BOTRPA_VER; $c=[char]34; $s=Get-Content -LiteralPath $f -Raw; $s=$s -replace '(?m)^#define AppVersion .*$', ('#define AppVersion ' + $c + $v + $c); Set-Content -LiteralPath $f -Value $s -Encoding UTF8"
if errorlevel 1 (
    echo [ERRO] Falha ao sincronizar a versao em scripts de controle\BotRPA.iss.
    call :pause_if_interativo
    exit /b 1
)
echo [*] Versao gravada no script do instalador: !VER!

:: 5. Compila o instalador
echo [*] Compilando o instalador...
"!ISCC!" "%~dp0BotRPA.iss" > "%TEMP%\BotRPA_iscc.log" 2>&1
set "ISCC_ERR=%errorlevel%"
if not "%ISCC_ERR%"=="0" (
    echo [ERRO] Falha ao compilar o instalador. Log:
    type "%TEMP%\BotRPA_iscc.log"
    call :pause_if_interativo
    exit /b 1
)

:: 6. Confere o resultado
set "SETUP=%~dp0BotRPA_Instalador_!VER!.exe"
if not exist "!SETUP!" (
    echo [ERRO] Instalador nao gerado: !SETUP!
    call :pause_if_interativo
    exit /b 1
)

:: Assinar instalador (se certificado configurado)
call :sign_file "!SETUP!"

echo.
echo [OK] Instalador gerado: !SETUP!
for %%F in ("!SETUP!") do echo      %%~zF bytes
echo O instalador:
echo   - Instala em C:\Program Files\Bot_RPA
echo   - Grava os DADOS em %%ProgramData%%\Bot_RPA
echo   - Atualizacao preserva os dados do usuario
echo   - Desinstalacao preserva os dados (com opcao de apagar)
echo ========================================================
call :pause_if_interativo
goto :eof

:: --------------------------------------------------------------------------
:deps
cd /d "%ROOT_DIR%"
echo.
echo ========================================================
echo    [4] INSTALAR DEPENDENCIAS (GCC + .NET 8 SDK)
echo ========================================================
echo [*] Verificando estrutura de diretorios...

:: Criar diretorios obrigatorios se nao existirem
if not exist "data\output"      mkdir "data\output"
if not exist "data\input"       mkdir "data\input"
if not exist "logs"             mkdir "logs"
if not exist "docs\registros"   mkdir "docs\registros"
if not exist "config"           mkdir "config"

:: Verificar GCC (WinGet)
gcc --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] GCC nao encontrado no PATH padrao.
    echo [*] Tentando instalar GCC via Winget...
    winget install --id BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source -e --accept-package-agreements --accept-source-agreements
) else (
    echo [+] GCC Detectado.
)

:: Verificar .NET 8 SDK
dotnet --list-sdks | findstr /R /C:"^8\." >nul
if %errorlevel% neq 0 (
    echo [!] .NET 8 SDK nao encontrado.
    echo [*] Tentando instalar .NET 8 SDK via Winget...
    winget install Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements
) else (
    echo [+] .NET 8 SDK Detectado.
)

echo [+] Setup Concluido! Tudo pronto para o Build.
call :pause_if_interativo
goto :eof

:: --------------------------------------------------------------------------
:adb_setup
cd /d "%ROOT_DIR%"
echo.
echo ========================================================
echo    [5] BAIXAR ADB (Google platform-tools)
echo ========================================================
set "TOOLS_DIR=%ROOT_DIR%\tools"
if not exist "%TOOLS_DIR%" mkdir "%TOOLS_DIR%"
cd /d "%TOOLS_DIR%"

echo [*] Baixando Google Platform Tools (ADB)...
powershell -NoProfile -Command "Invoke-WebRequest -Uri 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile 'platform-tools.zip'"
echo [*] Parando servicos ADB antigos...
taskkill /IM adb.exe /F 2>nul
echo [*] Extraindo arquivos...
powershell -NoProfile -Command "Expand-Archive -Path 'platform-tools.zip' -DestinationPath '.' -Force"
del platform-tools.zip 2>nul
echo [+] Ferramentas ADB instaladas com sucesso em tools/platform-tools/
echo ========================================================
call :pause_if_interativo
goto :eof

:: --------------------------------------------------------------------------
:monitor_mode
cd /d "%ROOT_DIR%"
echo ========================================================
echo    [6] MONITOR CONTINUO DE TELA - Bot_RPA
echo ========================================================
echo.
echo    Uso direto: ControleGeral.bat monitor "SERIAL"
echo --------------------------------------------------------

set "ADB=tools\platform-tools\adb.exe"
set "SERIAL=QT20TA1394272600549"
if exist "%ADB%" goto ter_serial_interativo
set "ADB=adb"
echo [ERRO] ADB local nao encontrado em tools\platform-tools.
echo        Rode antes a opcao [5] Baixar ADB.
call :pause_if_interativo
exit /b 1

:ter_serial_interativo
echo    Serial: %SERIAL%
echo    Para usar outro aparelho digite o serial OR Enter.
set "SERIAL_INPUT="
set /p SERIAL_INPUT="    Serial do tablet (Enter = padrao): "
if not "%SERIAL_INPUT%"=="" set "SERIAL=%SERIAL_INPUT%"

set SESSION=%date:~6,4%-%date:~3,2%-%date:~0,2%_%time:~0,2%-%time:~3,2%-%time:~6,2%
set SESSION=%SESSION: =0%
set "OUTDIR=logs\monitor_%SESSION%"

mkdir "%OUTDIR%" 2>nul
echo.
echo ============================================================
echo   Dispositivo: %SERIAL%
echo   Capturas em : %OUTDIR%
echo ============================================================
echo   [*] Iniciando captura automatica a cada 2 segundos...
echo   [*] FACA O FLUXO NO TABLET AGORA
echo   [*] Pressione CTRL+C para encerrar quando terminar.
echo ============================================================
echo.

set /a COUNT=0

:monitor_loop
set /a COUNT=COUNT+1
set STEP=step_%COUNT%
if %COUNT% lss 10   set STEP=step_00%COUNT%
if %COUNT% geq 10   set STEP=step_0%COUNT%
if %COUNT% geq 100  set STEP=step_%COUNT%

mkdir "%OUTDIR%\%STEP%" 2>nul

:: Foco da janela
%ADB% -s %SERIAL% shell dumpsys window 2>&1 | findstr "mCurrentFocus" > "%OUTDIR%\%STEP%\focus.txt"

:: Atividade em primeiro plano
%ADB% -s %SERIAL% shell dumpsys activity activities 2>&1 | findstr "ResumedActivity" > "%OUTDIR%\%STEP%\activity.txt"

:: XML Dump da interface
%ADB% -s %SERIAL% shell "uiautomator dump /sdcard/inno_step.xml" > nul 2>&1
%ADB% -s %SERIAL% pull "/sdcard/inno_step.xml" "%OUTDIR%\%STEP%\ui_dump.xml" > nul 2>&1

:: Screenshot
%ADB% -s %SERIAL% shell "screencap -p /sdcard/inno_step.png" > nul 2>&1
%ADB% -s %SERIAL% pull "/sdcard/inno_step.png" "%OUTDIR%\%STEP%\screenshot.png" > nul 2>&1

:: Progresso no console
echo [%TIME%] Captura %COUNT% salva em: %OUTDIR%\%STEP%
type "%OUTDIR%\%STEP%\focus.txt"

:: Aguarda 2 segundos antes da proxima captura
timeout /t 2 /nobreak > nul
goto monitor_loop

:: --------------------------------------------------------------------------
:tools_menu
cls
echo.
echo ========================================================
echo    [7] FERRAMENTAS DEV
echo ========================================================
echo    1 - Gravador de Fluxo Manual (passo a passo ADB)
echo    2 - Analisar XML da tela (logs\live_3s_swipe.xml)
echo    3 - Mostrar nos clicaveis (logs\live_now.xml)
echo    4 - Listar dispositivos ADB
echo    0 - Voltar ao menu principal
echo ========================================================
set "T="
set /p T="    Opcao: "
if "%T%"=="" goto tools_menu
if "%T%"=="1" goto tool_gravar_fluxo
if "%T%"=="2" goto tool_analise_xml
if "%T%"=="3" goto tool_show_zip
if "%T%"=="4" goto tool_dispositivos
if "%T%"=="0" goto :eof
echo.
echo    [ERRO] Opcao invalida: '%T%'
call :pause_if_interativo
goto tools_menu

:tool_gravar_fluxo
cd /d "%ROOT_DIR%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts de teste\gravar_fluxo_manual.ps1" %*
set "PS_ERR=%errorlevel%"
call :pause_if_interativo
if "%PS_ERR%" neq "0" exit /b %PS_ERR%
goto :eof

:tool_analise_xml
cd /d "%ROOT_DIR%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts de teste\analise_xml.ps1"
set "PS_ERR=%errorlevel%"
call :pause_if_interativo
if "%PS_ERR%" neq "0" exit /b %PS_ERR%
goto :eof

:tool_show_zip
cd /d "%ROOT_DIR%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\scripts de teste\show_zip_node.ps1"
set "PS_ERR=%errorlevel%"
call :pause_if_interativo
if "%PS_ERR%" neq "0" exit /b %PS_ERR%
goto :eof

:tool_dispositivos
cd /d "%ROOT_DIR%"
set "ADBEXE=tools\platform-tools\adb.exe"
if not exist "%ADBEXE%" (
    where adb 2>nul | findstr /R "." >nul
    if errorlevel 1 (
        echo.
        echo [ERRO] adb.exe nao encontrado em tools\platform-tools nem no PATH.
        echo        Rode antes a opcao [5] Baixar ADB.
        call :pause_if_interativo
        exit /b 1
    )
    set "ADBEXE=adb"
)
echo ------------------------------------------------------
"%ADBEXE%" devices -l
set "ADB_ERR=%errorlevel%"
call :pause_if_interativo
if "%ADB_ERR%" neq "0" exit /b %ADB_ERR%
goto :eof

:: --------------------------------------------------------------------------
:enviar_registros_menu
cd /d "%ROOT_DIR%"
echo.
echo ========================================================
echo    [9] ENVIAR REGISTROS - docs\registros + logs
echo ========================================================
call :enviar_registros
call :pause_if_interativo
goto :eof

:: --------------------------------------------------------------------------
:help
cls
echo.
echo ========================================================
echo    Bot_RPA - CONTROLE GERAL: AJUDA
echo ========================================================
echo.
echo  MODO INTERATIVO (duplo clique) mostra o menu numerado.
echo.
echo  MODO DIRETO - chamadas equivalentes:
echo    ControleGeral.bat 1 ................ Build TESTE (data\output)
echo    ControleGeral.bat 2 ................ Build RELEASE (BotRPA_Release)
echo    ControleGeral.bat 3 ................ Build INSTALADOR (Inno Setup)
echo    ControleGeral.bat 4 ................ Instalar dependencias
echo    ControleGeral.bat 5 ................ Baixar ADB
echo    ControleGeral.bat 6 ................ Monitor continuo de tela
echo    ControleGeral.bat 7 ................ Ferramentas Dev
echo    ControleGeral.bat 8 ................ Esta ajuda
echo    ControleGeral.bat 9 ................ Enviar registros (ZIP)
echo    ControleGeral.bat 0 ................ Sair
echo.
echo  Nomes textuais equivalentes:
echo    build ^| release ^| installer ^| deps ^| adb ^| monitor ^| tools ^|
echo    registros ^| ajuda
echo.
echo  AUDITORIA DE BUILD:
echo    docs\registros\YYYY-MM-DD\HH-MM_Plataforma_BotRPA_DESKTOP_BUILD
echo    (build.log do gcc e do dotnet)
echo.
echo  FERRAMENTAS DEV (em scripts\scripts de teste):
echo    gravar_fluxo_manual.ps1  (menu 7 - opcao 1)
echo    analise_xml.ps1          (menu 7 - opcao 2)
echo    show_zip_node.ps1        (menu 7 - opcao 3)
echo.
echo  ATALHOS DO INSTALADOR (somente leitura pelo menu 3):
echo    BotRPA.iss + InstallerAssets\ permanecem na pasta
echo    scripts\scripts de controle\ (o Inno Setup exige os arquivos fisicos).
call :pause_if_interativo
goto :eof

:: --------------------------------------------------------------------------
:goodbye
echo.
echo ========================================================
echo    Controle Geral encerrado. Ate logo!
echo ========================================================
if "%INTERATIVO%"=="1" pause >nul
exit /b 0

:: ==========================================================================
::  SUB-ROUTINAS AUXILIARES
:: ==========================================================================

:read_version
set "VER_TXT=N/A"
if exist "%ROOT_DIR%\config\version.txt" (
    for /f "usebackq delims=" %%V in ("%ROOT_DIR%\config\version.txt") do (
        set "VER_TXT=%%V"
        goto versao_ok
    )
)
:versao_ok
goto :eof

:read_tool_status
:: Status de GCC / dotnet para o painel (absorvido do antigo Build.ps1;
:: se achar o GCC do WinGet, ja injeta o PATH no lote da secao)
set "GCC_ST=AUSENTE"
set "DOTNET_ST=AUSENTE"
call gcc --version >nul 2>&1
if not errorlevel 1 set "GCC_ST=OK"
if not "%GCC_ST%"=="OK" (
    set "WG_GCC=%USERPROFILE%\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin"
    if exist "!WG_GCC!\gcc.exe" (
        set "PATH=!PATH!;!WG_GCC!"
        set "GCC_ST=OK (WinGet)"
    )
)
call dotnet --version >nul 2>&1
if not errorlevel 1 set "DOTNET_ST=OK"
goto :eof

:gen_version
:: Gera numero de versao unico de compilacao (DD.MM.YYYY.HHMM)
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMddHHmm"') do set datetime=%%I
set YYYY=%datetime:~0,4%
set MM=%datetime:~4,2%
set DD=%datetime:~6,2%
set HH=%datetime:~8,2%
set MIN=%datetime:~10,2%

set BUILD_VER=%DD%.%MM%.%YYYY%.%HH%%MIN%
if not defined BUILD_MODE set "BUILD_MODE=TESTE"
set AUDIT_DIR=docs\registros\%YYYY%-%MM%-%DD%\%HH%-%MIN%_Plataforma_BotRPA_DESKTOP_BUILD
mkdir "%ROOT_DIR%\%AUDIT_DIR%" 2>nul
echo [*] Versao de Compilacao Gerada: %BUILD_VER%
echo [*] Registro de build criado em: %AUDIT_DIR%
> "%ROOT_DIR%\%AUDIT_DIR%\build.log" echo Versao: %BUILD_VER%
>> "%ROOT_DIR%\%AUDIT_DIR%\build.log" echo Modo: %BUILD_MODE%
>> "%ROOT_DIR%\%AUDIT_DIR%\build.log" echo PastaAuditoria: %AUDIT_DIR%
:: Escreve a versao sem espaco a direita (quebra filename do instalador)
> "%ROOT_DIR%\config\version.txt" echo %BUILD_VER%
goto :eof

:pause_if_interativo
if "%INTERATIVO%"=="1" pause
goto :eof

:: ==========================================================================
::  SUB-ROTINA: ASSINATURA DE CODIGO (SignTool)
::  Variaveis de ambiente opcionais:
::    BOTRPA_SIGN_CERT   = caminho para o arquivo .pfx do certificado
::    BOTRPA_SIGN_PASSWORD = senha do certificado .pfx
::  Se nao encontrar o SignTool ou o certificado, pula a assinatura com aviso.
:: ==========================================================================
:sign_file
set "FILE_TO_SIGN=%~1"
if not exist "%FILE_TO_SIGN%" goto :eof

:: Assinatura desativada quando nao ha certificado configurado
if "!BOTRPA_SIGN_CERT!"=="" (
    if not defined SIGN_SKIP_NOTICE (
        echo [i] Assinatura de codigo desativada - defina BOTRPA_SIGN_CERT para assinar.
        set "SIGN_SKIP_NOTICE=1"
    )
    goto :eof
)
if not exist "!BOTRPA_SIGN_CERT!" (
    echo [AVISO] Certificado nao encontrado: !BOTRPA_SIGN_CERT! - pulando assinatura de "%FILE_TO_SIGN%"
    goto :eof
)

:: Localiza o SignTool do Windows SDK
set "SIGNTOOL="
for %%P in (
    "%ProgramFiles(x86)%\Windows Kits\10\bin\x64\signtool.exe"
    "%ProgramFiles(x86)%\Windows Kits\10\bin\x86\signtool.exe"
    "%ProgramFiles%\Windows Kits\10\bin\x64\signtool.exe"
    "%ProgramFiles(x86)%\Windows Kits\8.1\bin\x64\signtool.exe"
    "%ProgramFiles(x86)%\Windows Kits\8.1\bin\x86\signtool.exe"
) do if exist %%P if not defined SIGNTOOL set "SIGNTOOL=%%~P"

if "!SIGNTOOL!"=="" (
    echo [AVISO] SignTool nao encontrado - pulando assinatura de "%FILE_TO_SIGN%"
    goto :eof
)

echo [*] Assinando "%FILE_TO_SIGN%"...
if "!BOTRPA_SIGN_PASSWORD!"=="" (
    "!SIGNTOOL!" sign /f "!BOTRPA_SIGN_CERT!" /t http://timestamp.digicert.com "%FILE_TO_SIGN%" >nul 2>&1
) else (
    "!SIGNTOOL!" sign /f "!BOTRPA_SIGN_CERT!" /p "!BOTRPA_SIGN_PASSWORD!" /t http://timestamp.digicert.com "%FILE_TO_SIGN%" >nul 2>&1
)
if %errorlevel% neq 0 (
    echo [AVISO] Falha ao assinar "%FILE_TO_SIGN%" - continuando sem assinatura
)
goto :eof

:: ==========================================================================
::  SUB-ROTINA: PERGUNTAR ENVIO DE REGISTROS
::  Pergunta se o usuario deseja enviar/zipar os registros atuais
::  (docs\registros + logs) apos o build.
:: ==========================================================================
:perguntar_enviar_registros
if "%INTERATIVO%"=="0" goto :eof
set "ENVIAR="
set /p ENVIAR="    Deseja enviar os registros atuais (docs\registros + logs)? (S/N): "
if /i "!ENVIAR!"=="S" (
    call :enviar_registros
) else if /i "!ENVIAR!"=="N" (
    echo    Registros nao enviados.
) else (
    echo    Opcao invalida. Registros nao enviados.
)
goto :eof

:enviar_registros
cd /d "%ROOT_DIR%"
set "DATA=%date:~6,4%-%date:~3,2%-%date:~0,2%_%time:~0,2%-%time:~3,2%-%time:~6,2%"
set "DATA=%DATA: =0%"
set "ZIP_NAME=RegistrosBotRPA_%DATA%.zip"
set "ZIP_PATH=%TEMP%\%ZIP_NAME%"

echo    Compactando registros em %ZIP_NAME%...
powershell -NoProfile -Command "Compress-Archive -Path 'docs\registros','logs' -DestinationPath '%ZIP_PATH%' -Force" >nul 2>&1

if exist "%ZIP_PATH%" (
    echo    Registros compactados em: %ZIP_PATH%
    echo    Abrindo pasta de downloads para voce enviar o arquivo...
    explorer "%TEMP%"
) else (
    echo    [!] Falha ao compactar registros.
)
goto :eof
