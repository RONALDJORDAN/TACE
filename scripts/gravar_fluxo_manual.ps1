# =============================================================================
# Inno RPA - Gravador de Fluxo Manual via ADB
# Desenvolvedor: Jordan Araujo | InnovTech
# Descricao: Captura o estado da tela do tablet (XML + foco da janela) a cada
#            passo executado manualmente, gerando um log para mapeamento da
#            automacao real.
# =============================================================================

param(
    [string]$Serial = "",          # Serial do dispositivo (deixe vazio para auto-detectar)
    [string]$NomeFluxo = "fluxo",  # Nome base para os arquivos de captura
    [int]$IntervaloMs = 0          # 0 = captura manual (pressionar ENTER), >0 = captura automatica em ms
)

# --- Configuracao ---
# Caminho do ADB local do projeto (instalado via setup_adb.bat)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ADB = Join-Path $ProjectRoot "tools\platform-tools\adb.exe"

# Fallback: tenta o ADB do PATH do sistema caso o local nao exista
if (-not (Test-Path $ADB)) {
    $ADB = "adb"
    Write-Host "[!] ADB local nao encontrado. Tentando ADB do PATH do sistema..." -ForegroundColor DarkYellow
    Write-Host "    (Para instalar o ADB local, execute: scripts\setup_adb.bat)" -ForegroundColor DarkGray
} else {
    Write-Host "[+] ADB local detectado: $ADB" -ForegroundColor DarkGreen
}

$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$OutputDir = "logs\gravacao_$NomeFluxo`_$Timestamp"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  GRAVADOR DE FLUXO MANUAL - Inno RPA" -ForegroundColor Cyan
Write-Host "  InnovTech | Desenvolvido por Jordan Araujo" -ForegroundColor DarkCyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# --- Auto-detecta o dispositivo se o serial nao foi fornecido ---
if ($Serial -eq "") {
    Write-Host "[*] Detectando dispositivos ADB conectados..." -ForegroundColor Yellow
    $devices = & $ADB devices | Select-String "device$"
    if ($devices.Count -eq 0) {
        Write-Host "[ERRO] Nenhum dispositivo ADB detectado. Conecte o tablet e tente novamente." -ForegroundColor Red
        exit 1
    }
    $Serial = ($devices[0].ToString() -split "\s+")[0]
    Write-Host "[+] Dispositivo detectado: $Serial" -ForegroundColor Green
} else {
    Write-Host "[+] Usando dispositivo: $Serial" -ForegroundColor Green
}

Write-Host ""
Write-Host "[*] Capturas salvas em: $OutputDir" -ForegroundColor Yellow
Write-Host "[*] Cada ENTER = 1 captura de tela (XML + foco + screenshot)" -ForegroundColor Yellow
Write-Host "[*] Digite 'sair' para encerrar e gerar o relatorio final." -ForegroundColor Yellow
Write-Host ""

$Step = 1
$Log = @()

# --- Funcao de captura ---
function Capture-Step {
    param([int]$StepNum, [string]$Descricao = "")

    $StepLabel = "passo_{0:D3}" -f $StepNum
    $StepDir = "$OutputDir\$StepLabel"
    New-Item -ItemType Directory -Force -Path $StepDir | Out-Null

    $ts = Get-Date -Format "HH:mm:ss.fff"
    Write-Host ""
    Write-Host "  [$ts] Capturando $StepLabel..." -ForegroundColor Magenta

    # 1. Foco da janela atual
    $focus = & $ADB -s $Serial shell "dumpsys window | grep mCurrentFocus" 2>&1
    $focus | Out-File "$StepDir\focus.txt" -Encoding UTF8
    Write-Host "  [>] Foco: $focus" -ForegroundColor DarkYellow

    # 2. Package em primeiro plano
    $topActivity = & $ADB -s $Serial shell "dumpsys activity activities | grep ResumedActivity" 2>&1
    $topActivity | Out-File "$StepDir\top_activity.txt" -Encoding UTF8

    # 3. XML dump da interface
    Write-Host "  [>] Capturando XML da interface..." -ForegroundColor DarkYellow
    & $ADB -s $Serial shell "uiautomator dump /sdcard/inno_step.xml" | Out-Null
    & $ADB -s $Serial pull "/sdcard/inno_step.xml" "$StepDir\ui_dump.xml" | Out-Null

    # 4. Screenshot da tela
    Write-Host "  [>] Capturando screenshot..." -ForegroundColor DarkYellow
    & $ADB -s $Serial shell "screencap -p /sdcard/inno_step.png" | Out-Null
    & $ADB -s $Serial pull "/sdcard/inno_step.png" "$StepDir\screenshot.png" | Out-Null

    # 5. Elementos clicaveis do XML (resumo rapido)
    $xmlContent = ""
    if (Test-Path "$StepDir\ui_dump.xml") {
        $xmlContent = Get-Content "$StepDir\ui_dump.xml" -Raw -Encoding UTF8
        
        # Extrai todos os nos clicaveis com text e bounds
        $matches = [regex]::Matches($xmlContent, 'text="([^"]*)"[^>]*clickable="true"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"')
        $clickables = @()
        foreach ($m in $matches) {
            $txt   = $m.Groups[1].Value
            $x1    = [int]$m.Groups[2].Value
            $y1    = [int]$m.Groups[3].Value
            $x2    = [int]$m.Groups[4].Value
            $y2    = [int]$m.Groups[5].Value
            $cx    = [int](($x1 + $x2) / 2)
            $cy    = [int](($y1 + $y2) / 2)
            if ($txt -ne "") {
                $clickables += "  CLICAVEL | Centroide ($cx,$cy) | bounds=[$x1,$y1][$x2,$y2] | text=`"$txt`""
            }
        }
        
        # Elementos com content-desc
        $matchesDesc = [regex]::Matches($xmlContent, 'content-desc="([^"]*)"[^>]*clickable="true"[^>]*bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"')
        foreach ($m in $matchesDesc) {
            $desc  = $m.Groups[1].Value
            $x1    = [int]$m.Groups[2].Value
            $y1    = [int]$m.Groups[3].Value
            $x2    = [int]$m.Groups[4].Value
            $y2    = [int]$m.Groups[5].Value
            $cx    = [int](($x1 + $x2) / 2)
            $cy    = [int](($y1 + $y2) / 2)
            if ($desc -ne "") {
                $clickables += "  CLICAVEL | Centroide ($cx,$cy) | bounds=[$x1,$y1][$x2,$y2] | desc=`"$desc`""
            }
        }

        $summaryPath = "$StepDir\clickables_summary.txt"
        if ($clickables.Count -gt 0) {
            $clickables | Out-File $summaryPath -Encoding UTF8
            Write-Host "  [+] $($clickables.Count) elementos clicaveis encontrados -> clickables_summary.txt" -ForegroundColor Green
            $clickables | Select-Object -First 8 | ForEach-Object { Write-Host $_ -ForegroundColor DarkGray }
            if ($clickables.Count -gt 8) {
                Write-Host "  ... (+$($clickables.Count - 8) elementos adicionais no arquivo)" -ForegroundColor DarkGray
            }
        } else {
            Write-Host "  [!] Nenhum elemento clicavel com texto encontrado nesta tela." -ForegroundColor DarkRed
        }
    }

    # 6. Registra no log geral
    $entry = [PSCustomObject]@{
        Passo      = $StepLabel
        Timestamp  = $ts
        Descricao  = $Descricao
        Foco       = ($focus -join " ").Trim()
        Clicaveis  = $clickables.Count
        Diretorio  = $StepDir
    }
    return $entry
}

# --- Loop principal de captura manual ---
while ($true) {
    Write-Host ""
    Write-Host "--- PASSO $Step ---" -ForegroundColor Cyan
    $input = Read-Host "  Descricao do passo (ou 'sair')"
    
    if ($input.ToLower() -eq "sair") {
        break
    }

    $entry = Capture-Step -StepNum $Step -Descricao $input
    $Log += $entry

    Write-Host "  [OK] Passo $Step capturado com sucesso!" -ForegroundColor Green
    $Step++
}

# --- Gera relatorio final em Markdown ---
Write-Host ""
Write-Host "[*] Gerando relatorio final do fluxo..." -ForegroundColor Yellow

$reportPath = "$OutputDir\RELATORIO_FLUXO.md"
$reportLines = @()
$reportLines += "# Relatorio de Fluxo Manual - $NomeFluxo"
$reportLines += ""
$reportLines += "**Data/Hora de Gravacao:** $Timestamp"
$reportLines += "**Dispositivo:** $Serial"
$reportLines += "**Total de Passos Capturados:** $($Log.Count)"
$reportLines += ""
$reportLines += "---"
$reportLines += ""
$reportLines += "## Passos Gravados"
$reportLines += ""

foreach ($entry in $Log) {
    $reportLines += "### $($entry.Passo)"
    $reportLines += "- **Hora:** $($entry.Timestamp)"
    $reportLines += "- **Descricao:** $($entry.Descricao)"
    $reportLines += "- **Foco da Janela:** ``$($entry.Foco)``"
    $reportLines += "- **Elementos Clicaveis Detectados:** $($entry.Clicaveis)"
    $reportLines += "- **Arquivos:** [ui_dump.xml]($($entry.Diretorio)\ui_dump.xml) | [screenshot.png]($($entry.Diretorio)\screenshot.png) | [clickables_summary.txt]($($entry.Diretorio)\clickables_summary.txt)"
    $reportLines += ""
}

$reportLines += "---"
$reportLines += ""
$reportLines += "> Relatorio gerado automaticamente pelo Gravador de Fluxo Manual do Inno RPA - InnovTech"

$reportLines | Out-File $reportPath -Encoding UTF8
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  [+] Gravacao encerrada! $($Log.Count) passos capturados." -ForegroundColor Green
Write-Host "  [+] Relatorio: $reportPath" -ForegroundColor Green
Write-Host "  [+] Diretorio: $OutputDir" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

# Abre o relatorio automaticamente
Start-Process $reportPath
