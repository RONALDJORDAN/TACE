@echo off
title InnovTech OTA Release Builder
echo ========================================================
echo   Plataforma InnovTech - Gerador de Atualizacao OTA
echo ========================================================
echo.

:: Muda para a raiz do projeto
cd /d "%~dp0\.."

:: 1. Encerra qualquer instancia aberta da UI para liberar binarios
taskkill /F /IM InnoRPAui.exe /T 2>nul

:: 2. Gerar versao unica por Timestamp (DD.MM.YYYY.HHMM)
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set datetime=%%I
set YYYY=%datetime:~0,4%
set MM=%datetime:~4,2%
set DD=%datetime:~6,2%
set HH=%datetime:~8,2%
set MIN=%datetime:~10,2%

set BUILD_VER=%DD%.%MM%.%YYYY%.%HH%%MIN%

echo [*] Versao gerada para esta Release OTA: %BUILD_VER%
mkdir config 2>nul
echo %BUILD_VER%> config\version.txt

:: 3. Compilar Motor C (DLL)
echo [*] Compilando motor InnoEngine.dll...
set "PATH=%PATH%;%USERPROFILE%\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.MSVCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin;%USERPROFILE%\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin"
mkdir "data\output" 2>nul

set SRCS_CORE=src\api.c src\core\drivers\adb.c src\core\data\municipios.c src\core\data\dispositivos.c src\core\config\config.c src\core\learn\learn_db.c src\core\learn\learn_engine.c
set SRCS_V7=src\core\functions\vision7\f1_vision_tab7.c src\core\functions\vision7\f2_vision_tab7.c src\core\functions\vision7\f3_vision_tab7.c src\core\functions\vision7\f4_vision_tab7.c src\core\functions\vision7\f5_vision_tab7.c src\core\functions\vision7\f6_vision_tab7.c src\core\functions\vision7\f8_vision_tab7.c
set SRCS_M7=src\core\functions\tabmulti\f1_tabmulti_m7.c src\core\functions\tabmulti\f2_tabmulti_m7.c src\core\functions\tabmulti\f3_tabmulti_m7.c src\core\functions\tabmulti\f4_tabmulti_m7.c src\core\functions\tabmulti\f5_tabmulti_m7.c src\core\functions\tabmulti\m7_helpers.c
set SRCS_T20=src\core\functions\nokia_t20\f1_nokia_t20.c src\core\functions\nokia_t20\f2_nokia_t20.c src\core\functions\nokia_t20\f3_nokia_t20.c

gcc -shared %SRCS_CORE% %SRCS_V7% %SRCS_M7% %SRCS_T20% -o data\output\InnoEngine.dll -static -lcomctl32 -lgdi32 -luser32 -mwindows -D_WIN32_WINNT=0x0A00 -DWINVER=0x0A00

:: 4. Compilar UI C# WPF em modo Self-Contained Release
echo [*] Compilando InnoRPAui.exe (C# WPF)...
cd src\ui
"C:\Program Files\dotnet\dotnet.exe" publish InnovPlayUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ..\..\data\output\
cd ..\..

:: 5. Criar pasta temporaria do pacote OTA e compactar em .ZIP
set OTA_DIR=data\output\ota_temp
if exist "%OTA_DIR%" rmdir /s /q "%OTA_DIR%"
mkdir "%OTA_DIR%" 2>nul

copy /y "data\output\InnoRPAui.exe" "%OTA_DIR%\" >nul
copy /y "data\output\InnoEngine.dll" "%OTA_DIR%\" >nul
copy /y "config\version.txt" "%OTA_DIR%\" >nul
if exist "data\output\municipios.csv" copy /y "data\output\municipios.csv" "%OTA_DIR%\" >nul

set OTA_PKG_DIR=controle
mkdir "%OTA_PKG_DIR%" 2>nul
set ZIP_NAME=InnoRPA_v%BUILD_VER%.zip
set ZIP_FULL_PATH=%OTA_PKG_DIR%\%ZIP_NAME%

if exist "%ZIP_FULL_PATH%" del /f /q "%ZIP_FULL_PATH%"

echo [*] Gerando pacote compactado .ZIP diretamente em %ZIP_FULL_PATH%...
powershell -Command "Compress-Archive -Path '%OTA_DIR%\*' -DestinationPath '%ZIP_FULL_PATH%' -Force"
rmdir /s /q "%OTA_DIR%" 2>nul

:: 6. Atualizar arquivo de Release local para paridade
copy /y "data\output\InnoRPAui.exe" "InnoRPA_Release\InnoRPAui.exe" >nul
copy /y "data\output\InnoEngine.dll" "InnoRPA_Release\InnoEngine.dll" >nul
copy /y "config\version.txt" "InnoRPA_Release\config\version.txt" >nul

echo.
echo ========================================================
echo [SUCESSO] RELEASE OTA GERADA COM PERFEICAO!
echo ========================================================
echo.
echo - Numero da Versao : %BUILD_VER%
echo - Pacote OTA ZIP    : %ZIP_FULL_PATH%
echo.
echo Agora abra o Painel Web (web/ota_dashboard.html),
echo selecione o arquivo %ZIP_NAME% e publique na nuvem!
echo ========================================================
pause
