@echo off
echo [*] Iniciando a Ponte de Internet (PC -^> Tablet Android)
echo.

if not exist gnirehtet.exe (
    echo [X] Erro: gnirehtet.exe nao encontrado!
    echo Execute o script '1_instalar_ferramenta.ps1' primeiro para fazer o download.
    pause
    exit /b
)

echo [*] Instalando a ponte no Tablet (isso injetara o APK necessario no aparelho)
gnirehtet install

echo [*] Ativando a internet via cabo USB...
echo [*] (Verifique a tela do Tablet e de permissao de conexao VPN se solicitado)
gnirehtet run

pause
