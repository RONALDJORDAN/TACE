@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo [Bot_RPA] Gerador de Release (Versao Portatil PnP)
echo ========================================================
echo.

REM Vai para a raiz do projeto
cd /d "%~dp0\.."

REM 1. Compila o projeto mais recente no modo RELEASE
echo [*] Compilando os codigos C e C# (Modo Release - Icone Roxo)...
set "BUILD_MODE=RELEASE"
call scripts\build.bat
set "BUILD_MODE="


REM 2. Prepara a pasta de Release
set RELEASE_DIR=BotRPA_Release
echo [*] Criando pasta de Release: %RELEASE_DIR%
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%"

REM 3. Copiando Executaveis e Motor DLL
echo [*] Copiando arquivos binarios...
copy /y "data\output\BotRPAui.exe" "%RELEASE_DIR%\" >nul
copy /y "data\output\BotEngine.dll" "%RELEASE_DIR%\" >nul

REM 4. Copiando Dependencias Estruturais
echo [*] Copiando pasta config e tools...
xcopy /s /i /y "config" "%RELEASE_DIR%\config" >nul
REM Copia seletiva das tools (SEM tools\mingw64 ~= 968MB de compilador — nao vai para o usuario)
mkdir "%RELEASE_DIR%\tools" 2>nul
if exist "tools\platform-tools" xcopy /s /e /i /y "tools\platform-tools" "%RELEASE_DIR%\tools\platform-tools" >nul
if exist "tools\reverse_tethering" xcopy /s /e /i /y "tools\reverse_tethering" "%RELEASE_DIR%\tools\reverse_tethering" >nul
if exist "tools\mac_address_finder.apk" copy /y "tools\mac_address_finder.apk" "%RELEASE_DIR%\tools\" >nul
echo [+][OK] Tools copiadas (platform-tools + reverse_tethering, sem mingw64).

REM 4b. Script de aplicacao OTA (usado pelo OtaUpdateManager.ApplyUpdate)
mkdir "%RELEASE_DIR%\scripts" 2>nul
if exist "scripts\apply_ota_update.bat" copy /y "scripts\apply_ota_update.bat" "%RELEASE_DIR%\scripts\" >nul

REM 5. Banco de dados do sistema COM os registros dos aparelhos (ordem explicita
REM do usuario em 28/09/2026): embarca data\bot_database.db (410 aparelhos) para que
REM Relatorios > Banco de Dados abra com os registros. InitializeDatabase() usa
REM CREATE TABLE IF NOT EXISTS, entao o banco embarcado e preservado no PC do usuario.
echo [*] Embarcando banco de dados com os registros dos aparelhos...
mkdir "%RELEASE_DIR%\data" 2>nul
if exist "data\bot_database.db" (
    copy /y "data\bot_database.db" "%RELEASE_DIR%\data\bot_database.db" >nul
    echo [+][OK] bot_database.db embarcado com os registros.
) else (
    echo [!] AVISO: data\bot_database.db nao encontrado — release saira sem registros.
)
echo [*] Criando pastas de aprendizado da IA (bot_learn.json)...

if exist "data\output\data\bot_learn.json" (
    copy /y "data\output\data\bot_learn.json" "%RELEASE_DIR%\data\" >nul
    echo [+][OK] Aprendizado BotLearn copiado.
) else if exist "data\bot_learn.json" (
    copy /y "data\bot_learn.json" "%RELEASE_DIR%\data\" >nul
)

echo [*] Criando pastas de sistema de logs e registros...
mkdir "%RELEASE_DIR%\logs" 2>nul
mkdir "%RELEASE_DIR%\registros" 2>nul

echo [*] Copiando pacotes de municipios e APKs existentes...
mkdir "%RELEASE_DIR%\data\input\municipios" 2>nul
mkdir "%RELEASE_DIR%\data\input\apps" 2>nul
if exist "data\input\municipios" (
    xcopy /s /e /i /y "data\input\municipios" "%RELEASE_DIR%\data\input\municipios" >nul
    echo [+][OK] Diretorios data\input\municipios copiados.
)

echo [*] Copiando arquivos organizados da pasta Arquivos...
mkdir "%RELEASE_DIR%\Arquivos" 2>nul
if exist "Arquivos" (
    xcopy /s /e /i /y "Arquivos" "%RELEASE_DIR%\Arquivos" >nul
    echo [+][OK] Diretorio Arquivos copiado.
)

REM 6. Copiando base de dados de municipios (municipios.csv) se necessario
if not exist "%RELEASE_DIR%\municipios.csv" (
    if exist "data\output\municipios.csv" (
        copy /y "data\output\municipios.csv" "%RELEASE_DIR%\municipios.csv" >nul
    ) else if exist "registros\municipios\municipios.csv" (
        copy /y "registros\municipios\municipios.csv" "%RELEASE_DIR%\municipios.csv" >nul
    )
)

echo.
echo ========================================================
echo [SUCESSO] Pacote Release criado com perfeicao!
echo O software esta pronto na pasta: %RELEASE_DIR%
echo Basta zipar esta pasta e enviar para o usuario final.
echo ========================================================
pause
