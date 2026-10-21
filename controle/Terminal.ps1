# ================================================================
#  Plataforma Bot_RPA - Terminal de Controle
#  Arquivo: controle/Terminal.ps1
#
#  Terminal interativo estilo CMD com todas as funcoes de
#  administracao do projeto em um so lugar.
#
#  Uso interativo (duplo clique):  controle\Terminal.bat
#  Uso direto (um comando):        powershell -File controle\Terminal.ps1 -Comando "build teste"
# ================================================================
param(
    [string]$Comando = ""
)

$ErrorActionPreference = "Stop"

$ControleDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ControleDir)) {
    $ControleDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$Root = Split-Path -Parent $ControleDir

$BuildPs1       = Join-Path $ControleDir "Build.ps1"
$InstBat        = Join-Path $Root "scripts\build_installer.bat"
$DepsBat        = Join-Path $Root "scripts\install_dependencies.bat"
$AdbExe         = Join-Path $Root "tools\platform-tools\adb.exe"
$VersionFile    = Join-Path $Root "config\version.txt"

# ----------------------------------------------------------------
function Find-Adb {
    if (Test-Path -LiteralPath $AdbExe) { return $AdbExe }
    $cmd = Get-Command "adb.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Invoke-BatchFile {
    param([string]$BatPath)
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
    return $code
}

function Read-SimNao {
    param([string]$Pergunta)
    $r = (Read-Host "$Pergunta [S/N]").Trim().ToUpper()
    return ($r -eq "S" -or $r -eq "SIM")
}

function Show-Ajuda {
    Write-Host ""
    Write-Host "  Comandos disponiveis:" -ForegroundColor Cyan
    Write-Host "    ajuda                    Mostra esta lista"
    Write-Host "    build teste              Compila versao TESTE em data\output (icone azul)"
    Write-Host "    build final | release    Compila versao FINAL/RELEASE em BotRPA_Release (icone roxo)"
    Write-Host "    build instalavel         Gera o instalador/Atualizador .exe (Inno Setup) em controle"
    Write-Host "    versao                   Mostra a versao atual (config\version.txt)"
    Write-Host "    dispositivos             Lista aparelhos Android via ADB (adb devices -l)"
    Write-Host "    deps                     Instala dependencias (GCC + .NET 8 SDK)"
    Write-Host "    limpar                   Limpa a tela"
    Write-Host "    sair                     Fecha o terminal"
    Write-Host ""
}

function Invoke-TerminalBuild {
    param([string]$Modo)
    if ($Modo -ne "Teste" -and $Modo -ne "Final") {
        Write-Host "Uso: build teste | build final | build instalavel" -ForegroundColor Yellow
        return 1
    }
    if ($Modo -eq "Final") {
        Write-Host "[!] O build FINAL recompila tudo e RECRIA a pasta BotRPA_Release." -ForegroundColor Yellow
        if (-not (Read-SimNao -Pergunta "Continuar?")) {
            Write-Host "[*] Cancelado." -ForegroundColor Yellow
            return 1
        }
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $BuildPs1 -Modo $Modo
    return $LASTEXITCODE
}

function Invoke-TerminalInstalador {
    if (-not (Test-Path -LiteralPath $InstBat)) {
        Write-Host "[ERRO] Gerador nao encontrado: $InstBat" -ForegroundColor Red
        return 1
    }
    Write-Host "[*] Gerando o instalador .exe (Inno Setup)..." -ForegroundColor Cyan
    return (Invoke-BatchFile -BatPath $InstBat)
}

function Invoke-Versao {
    if (Test-Path -LiteralPath $VersionFile) {
        $v = (Get-Content -LiteralPath $VersionFile -TotalCount 1).Trim()
        Write-Host "Versao atual: $v" -ForegroundColor Green
    }
    else {
        Write-Host "Arquivo de versao nao encontrado: $VersionFile" -ForegroundColor Yellow
    }
    return 0
}

function Invoke-Dispositivos {
    $adb = Find-Adb
    if (-not $adb) {
        Write-Host "[ERRO] adb.exe nao encontrado em tools\platform-tools nem no PATH." -ForegroundColor Red
        Write-Host "       Rode o comando 'deps' ou abra o app (PreflightChecker baixa sozinho)." -ForegroundColor Yellow
        return 1
    }
    $prefSalvo = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $saida = & $adb devices -l 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $prefSalvo
    }
    foreach ($linha in $saida) { Write-Host $linha }
    return $code
}

function Invoke-Deps {
    Write-Host "[!] Vai verificar/instalar GCC (winget) e .NET 8 SDK. Pode demorar." -ForegroundColor Yellow
    if (-not (Read-SimNao -Pergunta "Continuar?")) {
        Write-Host "[*] Cancelado." -ForegroundColor Yellow
        return 1
    }
    return Invoke-BatchFile -BatPath $DepsBat
}

# Retorna: 0 = ok, 1 = erro, 9 = sair
function Invoke-Linha {
    param([string]$Linha)
    $t = $Linha.Trim()
    if ([string]::IsNullOrWhiteSpace($t)) { return 0 }
    $partes = $t -split '\s+'
    $cmd = $partes[0].ToLower()
    $arg = ""
    if ($partes.Count -gt 1) { $arg = $partes[1].ToLower() }

    switch ($cmd) {
        "ajuda" { Show-Ajuda; return 0 }
        "help" { Show-Ajuda; return 0 }
        "build" {
            if ($arg -eq "teste") { return Invoke-TerminalBuild -Modo "Teste" }
            if ($arg -eq "final" -or $arg -eq "release") { return Invoke-TerminalBuild -Modo "Final" }
            if ($arg -eq "instalavel" -or $arg -eq "instalador") { return Invoke-TerminalInstalador }
            Write-Host "Uso: build teste | build final | build instalavel" -ForegroundColor Yellow
            return 1
        }
        "versao" { return Invoke-Versao }
        "dispositivos" { return Invoke-Dispositivos }
        "deps" { return Invoke-Deps }
        "limpar" { Clear-Host; return 0 }
        "cls" { Clear-Host; return 0 }
        "sair" { return 9 }
        "exit" { return 9 }
        default {
            Write-Host "Comando desconhecido: '$t'. Digite 'ajuda'." -ForegroundColor Yellow
            return 1
        }
    }
    return 0
}

# ----------------------------------------------------------------
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Plataforma Bot_RPA - Terminal de Controle" -ForegroundColor Cyan
Write-Host "  Digite 'ajuda' para ver todas as funcoes." -ForegroundColor DarkGray
Write-Host "========================================================" -ForegroundColor Cyan

$exitCode = 0
if (-not [string]::IsNullOrWhiteSpace($Comando)) {
    $exitCode = Invoke-Linha -Linha $Comando
    if ($exitCode -eq 9) { $exitCode = 0 }
    exit $exitCode
}

while ($true) {
    Write-Host ""
    Write-Host -NoNewline "BotRPA> " -ForegroundColor Green
    $linha = Read-Host
    if ($null -eq $linha) { break }
    $rc = Invoke-Linha -Linha $linha
    if ($rc -eq 9) { break }
    if ($rc -ne 0) { $exitCode = $rc } else { $exitCode = 0 }
}

exit $exitCode
