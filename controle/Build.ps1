# ================================================================
#  Plataforma Bot_RPA - Central de Build
#  Arquivo: controle/Build.ps1
#
#  Painel interativo com 2 opcoes:
#    [1] Build TESTE - compila motor + UI em data/output (icone azul)
#    [2] Build FINAL - gera pacote portatil PnP em BotRPA_Release (icone roxo)
#
#  Uso interativo (duplo clique):  controle\Build.bat
#  Uso direto (sem menu):
#      powershell -File controle\Build.ps1 -Modo Teste
#      powershell -File controle\Build.ps1 -Modo Final [-Sim]
# ================================================================
param(
    [ValidateSet("", "Teste", "Final")]
    [string]$Modo = "",
    [switch]$Sim
)

$ErrorActionPreference = "Stop"

# --- Raiz do projeto (este script mora em controle/) ---
$ControleDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ControleDir)) {
    $ControleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$Root = Split-Path -Parent $ControleDir

$BuildBat        = Join-Path $Root "scripts\build.bat"
$BuildReleaseBat = Join-Path $Root "scripts\build_release.bat"
$VersionFile     = Join-Path $Root "config\version.txt"
$TesteDll        = Join-Path $Root "data\output\BotEngine.dll"
$TesteExe        = Join-Path $Root "data\output\BotRPAui.exe"
$ReleaseExe      = Join-Path $Root "BotRPA_Release\BotRPAui.exe"
$ReleaseDll      = Join-Path $Root "BotRPA_Release\BotEngine.dll"

$GccWinGetPaths = @(
    "$env:USERPROFILE\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin",
    "$env:USERPROFILE\AppData\Local\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.MSVCRT_Microsoft.Winget.Source_8wekyb3d8bbwe\mingw64\bin"
)
$DotnetExePath = "C:\Program Files\dotnet\dotnet.exe"

# ----------------------------------------------------------------
function Get-VersaoAtual {
    if (Test-Path -LiteralPath $VersionFile) {
        $v = (Get-Content -LiteralPath $VersionFile -TotalCount 1).Trim()
        if ([string]::IsNullOrWhiteSpace($v)) { return "N/A" }
        return $v
    }
    return "N/A"
}

function Find-Gcc {
    $cmd = Get-Command "gcc.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($p in $GccWinGetPaths) {
        $exe = Join-Path $p "gcc.exe"
        if (Test-Path -LiteralPath $exe) {
            $env:PATH += ";$p"
            return $exe
        }
    }
    return $null
}

function Find-Dotnet {
    $cmd = Get-Command "dotnet.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    if (Test-Path -LiteralPath $DotnetExePath) { return $DotnetExePath }
    return $null
}

function Test-Prerequisitos {
    param([bool]$Final = $false)
    $ok = $true

    $gcc = Find-Gcc
    if ($gcc) { Write-Host "[OK] GCC: $gcc" -ForegroundColor Green }
    else {
        Write-Host "[ERRO] gcc.exe nao encontrado (PATH nem WinGet MinGW)." -ForegroundColor Red
        Write-Host "       Rode scripts\install_dependencies.bat e tente de novo." -ForegroundColor Yellow
        $ok = $false
    }

    $dn = Find-Dotnet
    if ($dn) { Write-Host "[OK] dotnet: $dn" -ForegroundColor Green }
    else {
        Write-Host "[ERRO] dotnet nao encontrado. Instale o .NET 8 SDK." -ForegroundColor Red
        $ok = $false
    }

    foreach ($f in @("$Root\src\api.c", "$Root\src\ui\BotRpaUI.csproj", $BuildBat)) {
        if (-not (Test-Path -LiteralPath $f)) {
            Write-Host "[ERRO] Arquivo obrigatorio ausente: $f" -ForegroundColor Red
            $ok = $false
        }
    }
    if ($Final -and -not (Test-Path -LiteralPath $BuildReleaseBat)) {
        Write-Host "[ERRO] Arquivo obrigatorio ausente: $BuildReleaseBat" -ForegroundColor Red
        $ok = $false
    }
    return $ok
}

function Stop-UiAberta {
    $p = Get-Process -Name "BotRPAui" -ErrorAction SilentlyContinue
    if ($p) {
        Write-Host "[*] Fechando BotRPAui.exe aberto para liberar binarios..." -ForegroundColor Yellow
        $p | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

# Executa um .bat alimentando ENTER no stdin (atravessa o 'pause' final)
# e repassa a saida para o console. Retorna o exit code.
function Invoke-BatchFile {
    param([string]$BatPath)
    Write-Host "--------------------------------------------------------" -ForegroundColor DarkGray
    # stderr de .bat/gcc/dotnet nao pode abortar o wrapper (mantem Stop no resto)
    $prefSalvo = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $saida = "`n" | & cmd.exe /c "`"$BatPath`"" 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prefSalvo
    }
    foreach ($linha in $saida) { Write-Host $linha }
    Write-Host "--------------------------------------------------------" -ForegroundColor DarkGray
    return $code
}

function Test-Artefatos {
    param([string[]]$Arquivos)
    $ok = $true
    foreach ($a in $Arquivos) {
        if ((Test-Path -LiteralPath $a) -and ((Get-Item -LiteralPath $a).Length -gt 0)) {
            $kb = [math]::Round((Get-Item -LiteralPath $a).Length / 1KB)
            Write-Host "[OK] $a (${kb} KB)" -ForegroundColor Green
        }
        else {
            Write-Host "[ERRO] Artefato ausente ou vazio: $a" -ForegroundColor Red
            $ok = $false
        }
    }
    return $ok
}

function Read-SimNao {
    param([string]$Pergunta)
    if ($Sim) { return $true }
    $r = (Read-Host "$Pergunta [S/N]").Trim().ToUpper()
    return ($r -eq "S" -or $r -eq "SIM")
}

# ----------------------------------------------------------------
function Invoke-BuildTeste {
    Write-Host ""
    Write-Host "== BUILD TESTE (data/output, icone azul) ==" -ForegroundColor Cyan
    if (-not (Test-Prerequisitos -Final $false)) { return $false }
    Stop-UiAberta
    $code = Invoke-BatchFile -BatPath $BuildBat
    if ($code -ne 0) {
        Write-Host "[FALHA] scripts\build.bat saiu com codigo $code." -ForegroundColor Red
        return $false
    }
    if (-not (Test-Artefatos -Arquivos @($TesteDll, $TesteExe))) { return $false }
    $pastaTeste = Join-Path $Root "data\output"
    if (-not (Test-Path -LiteralPath $pastaTeste)) {
        Write-Host "[ERRO] Pasta da versao TESTE nao criada: $pastaTeste" -ForegroundColor Red
        return $false
    }
    Write-Host "[OK] Pasta da versao TESTE: $pastaTeste" -ForegroundColor Green
    Write-Host "[SUCESSO] Build TESTE concluido. Versao: $(Get-VersaoAtual)" -ForegroundColor Green
    return $true
}

function Invoke-BuildFinal {
    Write-Host ""
    Write-Host "== BUILD FINAL (BotRPA_Release PnP, icone roxo) ==" -ForegroundColor Magenta
    if (-not (Test-Prerequisitos -Final $true)) { return $false }
    Write-Host "[!] Isso recompila tudo e RECRIA a pasta BotRPA_Release." -ForegroundColor Yellow
    if (-not (Read-SimNao -Pergunta "Continuar com o build FINAL?")) {
        Write-Host "[*] Build FINAL cancelado pelo usuario." -ForegroundColor Yellow
        return $false
    }
    Stop-UiAberta
    $code = Invoke-BatchFile -BatPath $BuildReleaseBat
    if ($code -ne 0) {
        Write-Host "[FALHA] scripts\build_release.bat saiu com codigo $code." -ForegroundColor Red
        return $false
    }
    if (-not (Test-Artefatos -Arquivos @($ReleaseDll, $ReleaseExe))) { return $false }
    $pastaFinal = Join-Path $Root "BotRPA_Release"
    if (-not (Test-Path -LiteralPath $pastaFinal)) {
        Write-Host "[ERRO] Pasta da versao FINAL nao criada: $pastaFinal" -ForegroundColor Red
        return $false
    }
    Write-Host "[OK] Pasta da versao FINAL: $pastaFinal" -ForegroundColor Green
    Write-Host "[SUCESSO] Build FINAL concluido em BotRPA_Release. Versao: $(Get-VersaoAtual)" -ForegroundColor Green
    return $true
}

# ----------------------------------------------------------------
function Show-Painel {
    Clear-Host
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "  Plataforma Bot_RPA - Central de Build" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host "  Raiz ..... : $Root"
    Write-Host "  Versao ... : $(Get-VersaoAtual)"
    $gccSt = Find-Gcc; if (-not $gccSt) { $gccSt = "AUSENTE" }
    $dnSt = Find-Dotnet; if (-not $dnSt) { $dnSt = "AUSENTE" }
    Write-Host "  GCC ...... : $gccSt"
    Write-Host "  dotnet ... : $dnSt"
    Write-Host "--------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  [1] Build TESTE  -> data\output (icone azul, dev)"
    Write-Host "  [2] Build FINAL  -> BotRPA_Release PnP (icone roxo)"
    Write-Host "  [0] Sair"
    Write-Host "========================================================" -ForegroundColor Cyan
}

# ----------------------------------------------------------------
$logDir = Join-Path $Root "logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logFile = Join-Path $logDir ("controle_build_{0}.log" -f (Get-Date -Format "yyyyMMdd_HHmm"))
try { Start-Transcript -Path $logFile -ErrorAction SilentlyContinue | Out-Null } catch { }

$exitCode = 0
try {
    if ($Modo -eq "Teste") {
        if (-not (Invoke-BuildTeste)) { $exitCode = 1 }
    }
    elseif ($Modo -eq "Final") {
        if (-not (Invoke-BuildFinal)) { $exitCode = 1 }
    }
    else {
        $sair = $false
        while (-not $sair) {
            Show-Painel
            $op = (Read-Host "Escolha uma opcao").Trim()
            switch ($op) {
                "1" {
                    if (-not (Invoke-BuildTeste)) { $exitCode = 1 } else { $exitCode = 0 }
                    Read-Host "Pressione ENTER para voltar ao painel" | Out-Null
                }
                "2" {
                    if (-not (Invoke-BuildFinal)) { $exitCode = 1 } else { $exitCode = 0 }
                    Read-Host "Pressione ENTER para voltar ao painel" | Out-Null
                }
                "0" { $sair = $true }
                default {
                    Write-Host "Opcao invalida. Use 1, 2 ou 0." -ForegroundColor Yellow
                    Start-Sleep -Seconds 1
                }
            }
        }
    }
}
finally {
    try { Stop-Transcript -ErrorAction SilentlyContinue | Out-Null } catch { }
}

exit $exitCode
