@echo off
title Bot_RPA OTA Updater
echo ========================================================
echo   Plataforma Bot_RPA - Aplicando Atualizacao OTA...
echo ========================================================
echo.
echo Aguardando o encerramento do BotRPAui.exe...
timeout /t 3 /nobreak >nul
taskkill /F /IM BotRPAui.exe /T 2>nul
timeout /t 2 /nobreak >nul

set "ZIP_PATH=%~1"
set "TARGET_DIR=%~2"
if "%TARGET_DIR%"=="" set "TARGET_DIR=%~dp0.."
set "LOG_FILE=%TARGET_DIR%\logs\ota_update_%date:~6,4%-%date:~3,2%-%date:~0,2%.log"
if not exist "%TARGET_DIR%\logs" mkdir "%TARGET_DIR%\logs" 2>nul

if not exist "%ZIP_PATH%" (
    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%>> "%LOG_FILE%" 2>&1
    echo [ERRO] Arquivo de atualizacao nao encontrado: %ZIP_PATH%
    pause
    exit /b 1
)

REM ==========================================================================
REM  Preserva os DADOS DO USUARIO antes de sobrescrever os binarios.
REM  Em instalacao em "C:\Program Files", os dados ficam em %ProgramData%\Bot_RPA
REM  (variavel BOT_RPA_DATA_DIR) e nao são sobrescritos pela extração.
REM ==========================================================================
set "DATA_DIR=%BOT_RPA_DATA_DIR%"
if "%DATA_DIR%"=="" set "DATA_DIR=%TARGET_DIR%"

echo Preservando dados do usuario em: %DATA_DIR%>> "%LOG_FILE%" 2>&1
set "DATA_BACKUP=%TEMP%\BotRPA_data_backup_%RANDOM%"
if not exist "%DATA_BACKUP%" mkdir "%DATA_BACKUP%" 2>nul
for %%D in ("config\settings.json" "config\first_run.json" "municipios.csv" "data\bot_database.db" "data\dispositivos_registrados.csv" "data\relatorio_serial_mac.csv") do (
    if exist "%DATA_DIR%\%%~D" (
        if not exist "%DATA_BACKUP%\%%~D" mkdir "%DATA_BACKUP%\%%~D" 2>nul
        copy /y "%DATA_DIR%\%%~D" "%DATA_BACKUP%\%%~D" >> "%LOG_FILE%" 2>&1
    )
)
REM Pastas de dados do usuario (conteudo preservado)
for %%D in ("Arquivos" "data\input" "data\backups" "registros") do (
    if exist "%DATA_DIR%\%%~D" (
        xcopy /s /e /i /y "%DATA_DIR%\%%~D" "%DATA_BACKUP%\%%~D" >> "%LOG_FILE%" 2>&1
    )
)

echo Extraindo pacote de atualizacao...>> "%LOG_FILE%" 2>&1
echo Extraindo pacote de atualizacao...
powershell -NoProfile -Command "Expand-Archive -LiteralPath $env:ZIP_PATH -DestinationPath $env:TARGET_DIR -Force">> "%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERRO] Falha ao extrair o pacote. Veja o log: %LOG_FILE%
    pause
    exit /b 1
)

REM ==========================================================================
REM  Restaura os dados do usuario (sobrescritos pela extração do pacote)
REM ==========================================================================
echo Restaurando dados do usuario...>> "%LOG_FILE%" 2>&1
for %%D in ("config\settings.json" "config\first_run.json" "municipios.csv" "data\bot_database.db" "data\dispositivos_registrados.csv" "data\relatorio_serial_mac.csv") do (
    if exist "%DATA_BACKUP%\%%~D" (
        if not exist "%DATA_DIR%\%%~D" mkdir "%DATA_DIR%\%%~D" 2>nul
        copy /y "%DATA_BACKUP%\%%~D" "%DATA_DIR%\%%~D" >> "%LOG_FILE%" 2>&1
    )
)
for %%D in ("Arquivos" "data\input" "data\backups" "registros") do (
    if exist "%DATA_BACKUP%\%%~D" (
        xcopy /s /e /i /y "%DATA_BACKUP%\%%~D" "%DATA_DIR%\%%~D" >> "%LOG_FILE%" 2>&1
    )
)
if exist "%DATA_BACKUP%" rd /s /q "%DATA_BACKUP%" 2>nul

echo Atualizacao concluida com sucesso!>> "%LOG_FILE%" 2>&1
echo Atualizacao concluida com sucesso!
echo Dados do usuario preservados em: %DATA_DIR%
echo Reiniciando o Bot_RPA...
timeout /t 2 /nobreak >nul

if exist "%TARGET_DIR%\BotRPAui.exe" (
    start "" "%TARGET_DIR%\BotRPAui.exe"
) else if exist "%TARGET_DIR%\data\output\BotRPAui.exe" (
    start "" "%TARGET_DIR%\data\output\BotRPAui.exe"
)
exit /b 0
