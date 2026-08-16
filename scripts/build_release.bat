@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo [Inno RPA] Gerador de Release (Versao Portatil PnP)
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
set RELEASE_DIR=InnoRPA_Release
echo [*] Criando pasta de Release: %RELEASE_DIR%
if exist "%RELEASE_DIR%" rmdir /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%"

REM 3. Copiando Executaveis e Motor DLL
echo [*] Copiando arquivos binarios...
copy /y "data\output\InnoRPAui.exe" "%RELEASE_DIR%\" >nul
copy /y "data\output\InnoEngine.dll" "%RELEASE_DIR%\" >nul

REM 4. Copiando Dependencias Estruturais
echo [*] Copiando pasta config e tools...
xcopy /s /i /y "config" "%RELEASE_DIR%\config" >nul
xcopy /s /i /y "tools" "%RELEASE_DIR%\tools" >nul

REM 5. Criando pastas essenciais e copiando dados existentes do desenvolvedor
echo [*] Copiando base de dados e aprendizado da IA (inno_database.db / inno_learn.json)...
mkdir "%RELEASE_DIR%\data" 2>nul
if exist "data\output\data\inno_database.db" (
    copy /y "data\output\data\inno_database.db" "%RELEASE_DIR%\data\" >nul
    echo [+][OK] Banco de dados inno_database.db copiado.
) else (
    echo [!] Aviso: Banco de dados inno_database.db nao encontrado em data\output\data\.
)

if exist "data\output\data\inno_learn.json" (
    copy /y "data\output\data\inno_learn.json" "%RELEASE_DIR%\data\" >nul
    echo [+][OK] Aprendizado InnoLearn copiado.
) else if exist "data\inno_learn.json" (
    copy /y "data\inno_learn.json" "%RELEASE_DIR%\data\" >nul
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
