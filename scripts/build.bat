@echo off
echo [*] Iniciando compilacao do Bot_RPA (Interface Completa Win32)...

:: Muda para a pasta raiz do projeto
cd /d "%~dp0\.."

:: Gera o número de versão único de compilação baseado na data + hora (DD.MM.YYYY.HHMM)
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMddHHmm"') do set datetime=%%I
set YYYY=%datetime:~0,4%
set MM=%datetime:~4,2%
set DD=%datetime:~6,2%
set HH=%datetime:~8,2%
set MIN=%datetime:~10,2%

set BUILD_VER=%DD%.%MM%.%YYYY%.%HH%%MIN%
set AUDIT_DIR=registros\%YYYY%-%MM%-%DD%\%HH%-%MIN%_Plataforma_BotRPA_DESKTOP_BUILD
mkdir config 2>nul
echo %BUILD_VER%> config\version.txt
echo [*] Versao de Compilacao Gerada: %BUILD_VER%
mkdir "%AUDIT_DIR%" 2>nul
echo [*] Registro de build criado em: %AUDIT_DIR%
> "%AUDIT_DIR%\build.log" echo Versao: %BUILD_VER%
>> "%AUDIT_DIR%\build.log" echo Modo: %BUILD_MODE%
>> "%AUDIT_DIR%\build.log" echo PastaAuditoria: %AUDIT_DIR%

:: Encerra qualquer instancia aberta da UI para liberar o executavel para a compilacao
taskkill /F /IM BotRPAui.exe /T 2>nul

:: PATH do GCC instalado pelo Winget (garante que gcc sempre e encontrado)
set "PATH=%PATH%;%USERPROFILE%\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin"

:: Compilar o Engine em C (DLL) - Motores Separados Vision7 e TabMulti
mkdir "data\output" 2>nul
set SRCS_CORE=src\api.c src\core\drivers\adb.c src\core\data\municipios.c src\core\data\cofre.c src\core\data\paths.c src\core\data\dispositivos.c src\core\config\config.c src\core\learn\learn_db.c src\core\learn\learn_engine.c

set SRCS_V7=src\core\functions\vision7\f1_vision_tab7.c src\core\functions\vision7\f2_vision_tab7.c src\core\functions\vision7\f3_vision_tab7.c src\core\functions\vision7\f4_vision_tab7.c src\core\functions\vision7\f5_vision_tab7.c src\core\functions\vision7\f6_vision_tab7.c src\core\functions\vision7\f8_vision_tab7.c
set SRCS_M7=src\core\functions\tabmulti\f1_tabmulti_m7.c src\core\functions\tabmulti\f2_tabmulti_m7.c src\core\functions\tabmulti\f3_tabmulti_m7.c src\core\functions\tabmulti\f4_tabmulti_m7.c src\core\functions\tabmulti\f5_tabmulti_m7.c src\core\functions\tabmulti\m7_helpers.c

set SRCS_T20=src\core\functions\nokia_t20\f1_nokia_t20.c src\core\functions\nokia_t20\f2_nokia_t20.c src\core\functions\nokia_t20\f3_nokia_t20.c

echo [*] Compilando BotEngine.dll (Motor V7 + Motor M7 + Motor T20)...
>> "%AUDIT_DIR%\build.log" echo --- gcc BotEngine.dll ---
gcc -shared %SRCS_CORE% %SRCS_V7% %SRCS_M7% %SRCS_T20% -o data\output\BotEngine.dll -static -lcomctl32 -lgdi32 -luser32 -lcrypt32 -mwindows -D_WIN32_WINNT=0x0A00 -DWINVER=0x0A00 >> "%AUDIT_DIR%\build.log" 2>&1
set GCC_ERR=%errorlevel%
if %GCC_ERR% neq 0 (
    echo [!] ERRO na compilacao do motor C. Log: %AUDIT_DIR%\build.log
    type "%AUDIT_DIR%\build.log"
    pause
    exit /b %GCC_ERR%
)

  :: Compilar a Interface Grafica em C# WPF
  echo [*] Compilando BotRpaUI (C# WPF) no modo Self-Contained Offline...
  copy /Y "src\\ui\\Assets\\Icons\\BotRPAui.ico" "src\\ui\\Assets\\Icons\\BotRPAui.ico" 1>nul
  copy /Y "src\\ui\\Assets\\Icons\\BotRPAui.png" "src\\ui\\Assets\\Icons\\BotRPAui.png" 1>nul

cd src\ui
>> "..\..\%AUDIT_DIR%\build.log" echo --- dotnet publish ---
"C:\Program Files\dotnet\dotnet.exe" publish BotRpaUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ..\..\data\output\ >> "..\..\%AUDIT_DIR%\build.log" 2>&1
set DOTNET_ERR=%errorlevel%
:: Limpar intermediarios para economizar espaço em disco/memória
rd /s /q obj 2>nul
rd /s /q bin 2>nul
del /f /q *.wpftmp.csproj 2>nul
cd ..\..

:: Restaurar Icone Roxo no repositorio de codigo fonte
copy /Y "src\ui\Assets\Icons\BotRPAui_Release_Purple.ico" "src\ui\Assets\Icons\BotRPAui.ico" 1>nul
copy /Y "src\ui\Assets\Icons\BotRPAui_Release_Purple.png" "src\ui\Assets\Icons\BotRPAui.png" 1>nul



if %DOTNET_ERR% neq 0 (
    echo [!] ERRO na compilacao. Log: %AUDIT_DIR%\build.log
    type "%AUDIT_DIR%\build.log"
    pause
    exit /b %DOTNET_ERR%
)

echo [+] Compilacao concluida com sucesso!
echo [+] Motor: data\output\BotEngine.dll
echo [+] Interface: data\output\BotRPAui.exe
if not exist "data\output\municipios.csv" (
    copy /y "config\municipios.csv" "data\output\municipios.csv" >nul
)

mkdir "data\output\arquivos" 2>nul
mkdir "data\output\arquivos\apks" 2>nul
mkdir "data\output\arquivos\pacotes" 2>nul
>> "%AUDIT_DIR%\build.log" echo Resultado: SUCESSO

echo.
pause
