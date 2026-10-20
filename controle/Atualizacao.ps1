# Script PowerShell de Compilação Automatizada OTA - Plataforma Bot_RPA
Set-Location -Path "$PSScriptRoot\.."

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Plataforma Bot_RPA - Gerador de Atualizacao OTA" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# Encerra a UI se estiver aberta
Get-Process -Name "BotRPAui" -ErrorAction SilentlyContinue | Stop-Process -Force

# Gerar versão única por Timestamp (DD.MM.YYYY.HHMM)
$now = Get-Date
$buildVer = $now.ToString("dd.MM.yyyy.HHmm")
Write-Host "[*] Versao gerada para esta Release OTA: $buildVer" -ForegroundColor Green

New-Item -ItemType Directory -Path "config" -Force | Out-Null
Set-Content -Path "config\version.txt" -Value $buildVer

# Compilar Motor C (DLL)
Write-Host "[*] Compilando motor BotEngine.dll..." -ForegroundColor Yellow
$env:PATH += ";$env:USERPROFILE\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin"
New-Item -ItemType Directory -Path "data\output" -Force | Out-Null

$srcsCore = "src/api.c", "src/core/drivers/adb.c", "src/core/data/municipios.c", "src/core/data/cofre.c", "src/core/data/dispositivos.c", "src/core/config/config.c", "src/core/learn/learn_db.c", "src/core/learn/learn_engine.c"
$srcsV7 = "src/core/functions/vision7/f1_vision_tab7.c", "src/core/functions/vision7/f2_vision_tab7.c", "src/core/functions/vision7/f3_vision_tab7.c", "src/core/functions/vision7/f4_vision_tab7.c", "src/core/functions/vision7/f5_vision_tab7.c", "src/core/functions/vision7/f6_vision_tab7.c", "src/core/functions/vision7/f8_vision_tab7.c"
$srcsM7 = "src/core/functions/tabmulti/f1_tabmulti_m7.c", "src/core/functions/tabmulti/f2_tabmulti_m7.c", "src/core/functions/tabmulti/f3_tabmulti_m7.c", "src/core/functions/tabmulti/f4_tabmulti_m7.c", "src/core/functions/tabmulti/f5_tabmulti_m7.c", "src/core/functions/tabmulti/m7_helpers.c"
$srcsT20 = "src/core/functions/nokia_t20/f1_nokia_t20.c", "src/core/functions/nokia_t20/f2_nokia_t20.c", "src/core/functions/nokia_t20/f3_nokia_t20.c"

gcc -shared $srcsCore $srcsV7 $srcsM7 $srcsT20 -o data/output/BotEngine.dll -static -lcomctl32 -lgdi32 -luser32 -lcrypt32 -mwindows -D_WIN32_WINNT=0x0A00 -DWINVER=0x0A00

# Compilar UI C# WPF
Write-Host "[*] Compilando BotRPAui.exe (C# WPF)..." -ForegroundColor Yellow
dotnet publish src/ui/BotRpaUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o data/output/

# Compactar pacote OTA .ZIP (estrutura com pastas: config/version.txt na raiz do pacote)
$otaTemp = "data/output/ota_temp"
if (Test-Path $otaTemp) { Remove-Item -Path $otaTemp -Recurse -Force }
New-Item -ItemType Directory -Path "$otaTemp/config" -Force | Out-Null
New-Item -ItemType Directory -Path "$otaTemp/scripts" -Force | Out-Null

Copy-Item "data/output/BotRPAui.exe" -Destination $otaTemp -Force
Copy-Item "data/output/BotEngine.dll" -Destination $otaTemp -Force
Copy-Item "config/version.txt" -Destination "$otaTemp/config/version.txt" -Force
if (Test-Path "scripts/apply_ota_update.bat") { Copy-Item "scripts/apply_ota_update.bat" -Destination "$otaTemp/scripts/apply_ota_update.bat" -Force }
# municipios.csv NAO entra no OTA: o cofre DPAPI e por maquina (nao descriptografa
# em outro PC) e o pacote sobrescreveria o cofre local do usuario. Seed vai na Release.

$otaPkgDir = "controle"
New-Item -ItemType Directory -Path $otaPkgDir -Force | Out-Null
$zipName = "BotRPA_v$buildVer.zip"
$zipPath = "$otaPkgDir/$zipName"

if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force }

Write-Host "[*] Gerando pacote compactado .ZIP ($zipName)..." -ForegroundColor Cyan
Compress-Archive -Path "$otaTemp/*" -DestinationPath $zipPath -Force
Remove-Item -Path $otaTemp -Recurse -Force

# Calcula SHA-256 do pacote (obrigatório no manifesto ota/manifest)
$sha256 = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLower()
$manifestModel = @{
    version = $buildVer
    download_url = "<URL_DIRETA_FIREBASE_STORAGE/$zipName>"
    sha256 = $sha256
    changelog = "Atualizacao Bot_RPA $buildVer"
    mandatory = $true
    release_date = (Get-Date).ToString("o")
    published_at = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
} | ConvertTo-Json -Depth 4
$manifestPath = "$otaPkgDir/ota_manifest_$buildVer.json"
Set-Content -Path $manifestPath -Value $manifestModel -Encoding UTF8

# Atualizar release local
Copy-Item "data/output/BotRPAui.exe" -Destination "BotRPA_Release/BotRPAui.exe" -Force
Copy-Item "data/output/BotEngine.dll" -Destination "BotRPA_Release/BotEngine.dll" -Force
Copy-Item "config/version.txt" -Destination "BotRPA_Release/config/version.txt" -Force

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "[SUCESSO] RELEASE OTA GERADA COM PERFEICAO!" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host "- Numero da Versao : $buildVer" -ForegroundColor White
Write-Host "- Pacote OTA ZIP    : $zipPath" -ForegroundColor White
Write-Host "- SHA-256           : $sha256" -ForegroundColor White
Write-Host "- Manifesto modelo  : $manifestPath" -ForegroundColor White
Write-Host ""
Write-Host "Proximo passo (painel canônico):" -ForegroundColor Yellow
Write-Host "1) Suba o ZIP no Firebase Storage e copie a URL direta https." -ForegroundColor Yellow
Write-Host "2) Abra o InnovStock > Bot_RPA > Atualizacoes & OTA > Publicar Nova Versao OTA." -ForegroundColor Yellow
Write-Host "3) Cole versao ($buildVer), URL direta e SHA-256 acima. O Bot_RPA detecta no proximo check." -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Green
