@echo off
:: =============================================================================
:: Inno RPA - Monitor Continuo de Tela via ADB
:: Captura XML + Screenshot a cada 2 segundos automaticamente
:: Desenvolvedor: Jordan Araujo | InnovTech
:: =============================================================================

set ADB=tools\platform-tools\adb.exe
set SERIAL=QT20TA1394272600549
set SESSION=%date:~6,4%-%date:~3,2%-%date:~0,2%_%time:~0,2%-%time:~3,2%-%time:~6,2%
set SESSION=%SESSION: =0%
set OUTDIR=logs\monitor_%SESSION%

mkdir "%OUTDIR%" 2>nul

echo.
echo ============================================================
echo   MONITOR CONTINUO DE TELA - Inno RPA
echo   Dispositivo: %SERIAL%
echo   Capturas em: %OUTDIR%
echo ============================================================
echo.
echo [*] Iniciando captura automatica a cada 2 segundos...
echo [*] FACA O FLUXO NO TABLET AGORA
echo [*] Pressione CTRL+C para encerrar quando terminar.
echo.

set /a COUNT=0

:loop
set /a COUNT=COUNT+1
set STEP=step_%COUNT%
if %COUNT% lss 10 set STEP=step_00%COUNT%
if %COUNT% geq 10 set STEP=step_0%COUNT%
if %COUNT% geq 100 set STEP=step_%COUNT%

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

:: Exibe o progresso no console (sem pipe para evitar erro CMD)
echo [%TIME%] Captura %COUNT% salva em: %OUTDIR%\%STEP%
type "%OUTDIR%\%STEP%\focus.txt"

:: Aguarda 2 segundos antes da proxima captura
timeout /t 2 /nobreak > nul

goto loop
