@echo off
echo [*] Instalador de Dependencias Inno RPA
echo [*] Verificando estrutura de diretorios...

cd /d "%~dp0\.."

:: Criar diretórios obrigatórios se não existirem
if not exist "data\output" mkdir "data\output"
if not exist "data\input" mkdir "data\input"
if not exist "logs" mkdir "logs"
if not exist "registros" mkdir "registros"
if not exist "config" mkdir "config"

:: Verificar GCC (Winget)
gcc --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] GCC nao encontrado no PATH padrão.
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
pause
