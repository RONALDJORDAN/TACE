@echo off
echo [*] Baixando Google Platform Tools (ADB)...

set TOOLS_DIR=%~dp0\..\tools
if not exist "%TOOLS_DIR%" mkdir "%TOOLS_DIR%"

cd "%TOOLS_DIR%"

powershell -Command "Invoke-WebRequest -Uri 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile 'platform-tools.zip'"
echo [*] Parando servicos ADB antigos...
taskkill /IM adb.exe /F 2>nul
echo [*] Extraindo arquivos...
powershell -Command "Expand-Archive -Path 'platform-tools.zip' -DestinationPath '.' -Force"

echo [+] Ferramentas ADB instaladas com sucesso em tools/platform-tools/
del platform-tools.zip
pause
