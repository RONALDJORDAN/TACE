# Script para baixar o Gnirehtet oficial (Reverse Tethering)
$ErrorActionPreference = "Stop"
$gnirehtetUrl = "https://github.com/Genymobile/gnirehtet/releases/download/v2.5/gnirehtet-rust-win64-v2.5.zip"
$zipFile = "gnirehtet.zip"

Write-Host "[*] Baixando a ferramenta de Reverse Tethering (Gnirehtet v2.5)..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $gnirehtetUrl -OutFile $zipFile

Write-Host "[*] Extraindo os arquivos..." -ForegroundColor Cyan
Expand-Archive -Path $zipFile -DestinationPath ".\" -Force

Write-Host "[*] Movendo os executaveis para a raiz da pasta..." -ForegroundColor Cyan
Move-Item -Path ".\gnirehtet-rust-win64\*" -Destination ".\" -Force

Write-Host "[*] Limpando arquivos temporarios..." -ForegroundColor Cyan
Remove-Item -Path $zipFile -Force
Remove-Item -Path ".\gnirehtet-rust-win64" -Recurse -Force

Write-Host "[+] Ferramenta instalada com sucesso!" -ForegroundColor Green
Write-Host "    Para usar, execute o '2_iniciar_internet.bat' com um tablet conectado." -ForegroundColor Yellow
Pause
