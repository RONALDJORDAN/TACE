# TACE — Tablet Auditor & Configurator for Education

![TACE Banner](docs/banner.svg)

![Build](https://img.shields.io/badge/build-passing-brightgreen) ![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![C](https://img.shields.io/badge/C-GCC-orange) ![C%23](https://img.shields.io/badge/C%23-WPF-239120) ![ADB](https://img.shields.io/badge/ADB-Automation-red) ![License](https://img.shields.io/badge/license-Proprietary-lightgrey) ![Status](https://img.shields.io/badge/status-Active-success) ![Architecture](https://img.shields.io/badge/architecture-Hybrid%20Premium-9cf) ![UI](https://img.shields.io/badge/UI-Glassmorphism%20Cyber--Industrial-ff69b4)

> **TACE** — Tablet Auditor & Configurator for Education. Sistema de automação robótica de alta performance para configuração e auditoria de tablets em avaliações municipais, com arquitetura **Híbrida Premium**: motor nativo em C + interface visual em C# WPF.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Estrutura do Projeto](#estrutura-do-projeto)
- [Pré-requisitos](#pré-requisitos)
- [Como Compilar](#como-compilar)
- [Como Executar](#como-executar)
- [Auditoria e Logs](#auditoria-e-logs)
- [Contribuição](#contribuição)
- [Licença](#licença)

---

## Objetivo e Impacto

O **TACE** foi criado para **automatizar a configuração de dispositivos específicos** utilizados em **aplicações de avaliações municipais**, reduzindo drasticamente a ocorrência de erros de corrompimento de dados — de **10 para 1** — e o tempo de produção de **100 por dia para 100 por hora**, dependendo da quantidade de conexões ADB utilizadas.

A ferramenta escala horizontalmente: é possível usar **1 porta USB ou 10 portas USB** simultaneamente, fazendo com que o tempo de produção diminua **exponencialmente** conforme mais dispositivos são conectados via cabo USB.

Além disso, o TACE **registra todos os aparelhos conectados via ADB**, capturando:
- Número de série
- Endereço MAC da rede Wi-Fi (evitando MACs invisíveis/ocultos)

Também gera **registro de auditoria dos equipamentos**, extraindo dados em logs e convertendo-os em informações legíveis, fornecendo **relatórios sobre os erros ocorridos nas aplicações**, permitindo correção e melhoria contínua para as próximas execuções.

---

## Visão Geral

O **TACE** combina a velocidade bruta do **C puro** com a excelência visual do **C# WPF**, seguindo a política de **Zero-Leak** e isolamento de camadas.

- **Motor (BotEngine.dll):** C puro, gerencia ADB, dispositivos, configurações e banco offline em memória.
- **Interface (TACEui.exe):** WPF .NET 8 com design *Glassmorphism Cyber-Industrial*, consumindo o motor via `DllImport`.

---

## Módulos e Submódulos

O TACE conta com diversos módulos e submódulos organizados por responsabilidade:

### Motor C (BotEngine.dll)

- **Core / Drivers** — `src/core/drivers/adb.c`: comunicação ADB com dispositivos.
- **Core / Data** — `src/core/data/`: municipios, cofre (credenciais DPAPI), paths, dispositivos.
- **Core / Config** — `src/core/config/config.c`: configurações dinâmicas.
- **Core / Learn (LeanBot)** — `src/core/learn/`: IA adaptativa que registra clicagens no tablet, mapeia caminhos de menu e se adapta ao equipamento. *Fase de testes/implementação.*
- **Funções / Vision7** — `src/core/functions/vision7/`: fluxos para dispositivos Vision (f1–f8).
- **Funções / TabMulti** — `src/core/functions/tabmulti/`: fluxos multi-tab (f1–f5 + helpers).
- **Funções / Nokia T20** — `src/core/functions/nokia_t20/`: fluxos para Nokia T20 (f1–f3).
- **API** — `src/api.c`: interface P/Invoke consumida pela UI.

### Interface WPF (.NET 8)

- **Views** — `src/ui/Views/`: telas da aplicação.
- **Core** — `src/ui/Core/`: lógica de apoio da UI.
- **Assets / Icons** — `src/ui/Assets/Icons/`: ícones e recursos visuais.
- **Resources** — `src/ui/Resources/`: recursos embarcados (ex.: gnirehtet.zip).

### Ferramentas e Operação

- **Scripts de Controle** — `scripts/scripts de controle/ControleGeral.bat`: build, release, installer, deps, ADB, monitor.
- **Scripts de Teste** — `scripts/scripts de teste/`: gravador de fluxo, análise de XML, monitor de tela.
- **Auditoria** — `docs/registros/`: logs de build e execução.

> ⚠️ O **LeanBot** ainda está em **fase de testes e implementação**.

---

## Arquitetura

### Visão em Camadas (Híbrida Premium)

```
┌─────────────────────────────────────────────────────────────────────┐
│                     CAMADA DE APRESENTAÇÃO (WPF)                    │
│  TACEui.exe · .NET 8 · Glassmorphism Cyber-Industrial             │
│  Views · Assets · Resources · Core (lógica de apoio)                 │
└───────────────────────────────┬─────────────────────────────────────┘
                                │  P/Invoke (DllImport)
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     CAMADA DE INTEROPERAÇÃO (API)                    │
│  src/api.c · Contrato estável · Marshaling seguro · Zero-Leak       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     CAMADA DE MOTOR (C PURO)                        │
│  BotEngine.dll · GCC · Alta performance · Memória controlada        │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   │
│  │ Drivers/ADB │ │ Data/Cofre  │ │ Config      │ │ LearnBot    │   │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘   │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐                   │
│  │ Vision7     │ │ TabMulti    │ │ Nokia T20   │                   │
│  └─────────────┘ └─────────────┘ └─────────────┘                   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     CAMADA DE DISPOSITIVOS / DADOS                  │
│  ADB (USB) · Tablets · MAC/SN · Logs · Auditoria · bot_database.db  │
└─────────────────────────────────────────────────────────────────────┘
```

### Princípios Arquiteturais

- **Zero-Leak:** camadas isoladas, sem vazamento de responsabilidades.
- **Híbrida Premium:** C puro para performance + WPF para UX.
- **Escalabilidade Horizontal:** 1 a N conexões ADB simultâneas.
- **Auditoria Total:** logs estruturados e relatórios de erro.
- **Configuração Dinâmica:** sem hardcoding de variáveis globais (`config/settings.json`).
- **IA Adaptativa (LeanBot):** aprendizado por equipamento (em fase de testes).

### Padrões de Design

- **Facade:** `src/api.c` como fachada única do motor para a UI.
- **Strategy:** módulos Vision7 / TabMulti / Nokia T20 como estratégias por dispositivo.
- **Repository:** `core/data/` encapsula persistência (municipios, cofre, dispositivos).
- **Observer (logs):** auditoria contínua com geração de relatórios.
- **Factory (build):** `ControleGeral.bat` orquestra compilação multi-target.

### Segurança e Zero-Leak

- Credenciais protegidas via **DPAPI** (`cofre.c`).
- Sem hardcoding de variáveis globais.
- Separação física entre motor (DLL) e UI (EXE).
- Assinatura de código opcional (SignTool) para release/installer.

### Observabilidade

- Logs de build e execução em `docs/registros/` e `logs/`.
- Relatórios de erro legíveis a partir dos logs brutos.
- Auditoria por dispositivo (SN, MAC Wi-Fi, incluindo MACs ocultos).

### Resiliência

- Recuperação de falhas ADB com re-tentativa.
- Encerramento forçado de instâncias da UI antes do build.
- Limpeza de intermediários (obj/bin) pós-publish.

### Escalabilidade

- Paralelismo via múltiplas portas USB (1 a 10+).
- Tempo de produção reduzido de 100/dia para 100/hora.
- Erros de corrompimento reduzidos de 10 para 1.

### Pipeline de Build e Deploy

```
GCC (Motor C) ──► BotEngine.dll
       │
       ▼
dotnet publish (WPF) ──► TACEui.exe (self-contained)
       │
       ▼
Release (portátil) ──► Installer (Inno Setup)
       │
       ▼
Auditoria (docs/registros/) + Logs
```

### Fluxo de Execução

1. UI dispara comando via P/Invoke → API C.
2. Motor executa operação ADB no(s) dispositivo(s).
3. Dados (SN, MAC) são registrados e persistidos.
4. Logs/auditoria são gerados e expostos à UI.
5. LeanBot (quando ativo) aprende o caminho de menu por equipamento.

---

## Estrutura do Projeto

```
TACE/
├── .github/             # Workflows e templates do GitHub
├── config/              # Configurações dinâmicas
│   ├── settings.json    # Configurações globais (sem hardcoding)
│   ├── version.txt      # Versão de build
│   └── municipios.csv   # Seed público de municípios
├── data/
│   ├── input/           # Entradas (municipios.csv, apps)
│   │   ├── municipios/
│   │   └── apps/
│   ├── output/          # Binários compilados (BotEngine.dll, TACEui.exe)
│   │   ├── arquivos/
│   │   │   ├── apks/
│   │   │   └── pacotes/
│   │   └── data/        # Dados de runtime (bot_learn.json)
│   └── bot_database.db  # Banco de dados de dispositivos
├── docs/
│   ├── pipeline_architecture.md
│   ├── banner.svg       # Imagem de capa
│   └── registros/       # Auditorias de build (YYYY-MM-DD/HH-MM_...)
├── logs/                # Logs de execução e auditoria
├── scripts/
│   ├── scripts de controle/
│   │   ├── ControleGeral.bat   # Script unificado de operações
│   │   ├── ControleGeralLauncher.c
│   │   ├── TACE.iss          # Script do Inno Setup
│   │   └── InstallerAssets/    # Assets do instalador
│   └── scripts de teste/
│       ├── gravar_fluxo_manual.ps1
│       ├── analise_xml.ps1
│       ├── show_zip_node.ps1
│       └── monitor_tela.bat
├── src/
│   ├── api.c            # Interface P/Invoke
│   ├── core/
│   │   ├── config/      # Configuração dinâmica
│   │   ├── data/        # municipios, cofre, paths, dispositivos
│   │   ├── drivers/     # ADB
│   │   ├── functions/   # vision7, tabmulti, nokia_t20
│   │   └── learn/       # LeanBot (IA adaptativa)
│   └── ui/              # Projeto WPF (.NET 8)
│       ├── Assets/Icons/
│       ├── Core/
│       ├── Properties/
│       ├── Resources/    # gnirehtet.zip
│       ├── Views/
│       └── TACEUI.csproj
└── tools/
    ├── platform-tools/  # ADB (Google)
    ├── reverse_tethering/
    └── mac_address_finder.apk
```

---

## Pré-requisitos

### Ambiente de Desenvolvimento

| Ferramenta | Versão Mínima | Instalação (Windows) | Observação |
|---|---|---|---|
| Windows | 10/11 (x64) | — | SO alvo da aplicação |
| GCC (MinGW-w64) | 11+ | `choco install mingw` ou `winget install BrechtSanders.WinLibs.POSIX.UCRT` | Compila o motor C |
| .NET SDK | 8.0+ | `choco install dotnet-8.0-sdk` ou `winget install Microsoft.DotNet.SDK.8` | Compila a UI WPF |
| Inno Setup | 6.x | `winget install JRSoftware.InnoSetup` | Apenas para instalador (opção 3) |
| Git | 2.x | `winget install Git.Git` | Controle de versão |

### Dependências de Runtime (dispositivos)

| Item | Descrição |
|---|---|
| ADB (platform-tools) | Baixado via `ControleGeral.bat 5` ou manualmente |
| Tablets Android | Conectados via USB (1 a 10+ portas) |
| Driver USB | Instalado conforme fabricante do dispositivo |

### Verificação Rápida

```cmd
gcc --version
dotnet --list-sdks
```

Se ambos retornarem versão, o ambiente está pronto para build (`ControleGeral.bat 1`).

---

## Como Compilar

### Build Teste (recomendado para desenvolvimento)

```cmd
cd P:\Projetos\TACE
scripts\scripts de controle\ControleGeral.bat 1
```

**O que acontece:**
1. Gera número de versão único (`DD.MM.YYYY.HHMM`) em `config/version.txt`.
2. Cria pasta de auditoria em `docs/registros/YYYY-MM-DD/HH-MM_..._BUILD`.
3. Compila o motor C em `data/output/BotEngine.dll` (GCC, estático).
4. Publica a UI WPF em `data/output/TACEui.exe` (self-contained, single-file).
5. Copia `municipios.csv` e cria pastas auxiliares.

**Saída esperada:**
```
[+] Compilacao concluida com sucesso
[+] Motor     : data\output\BotEngine.dll
[+] Interface : data\output\TACEui.exe
```

### Build Release (pacote portátil)

```cmd
scripts\scripts de controle\ControleGeral.bat 2
```

Gera a pasta `TACE_Release/` com binários, config, tools (ADB), banco de dados e dados de aprendizado.

### Build Instalador

```cmd
scripts\scripts de controle\ControleGeral.bat 3
```

Requer Inno Setup 6. Gera `TACE_Instalador_<versao>.exe`.

### Instalar Dependências Automaticamente

```cmd
scripts\scripts de controle\ControleGeral.bat 4
```

Verifica e instala GCC e .NET 8 SDK via Winget.

### Troubleshooting

| Problema | Solução |
|---|---|
| `gcc` não reconhecido | Instale MinGW (`choco install mingw`) e reinicie o terminal |
| `.NET 8 SDK` não encontrado | `choco install dotnet-8.0-sdk` |
| Recurso `gnirehtet.zip` ausente | Crie `src/ui/Resources/gnirehtet.zip` (zip vazio) |
| UI não compila por SDK 10 | Use SDK 8 instalado; script aponta para `dotnet.exe` 8 |
| Binário em uso | Feche `TACEui.exe` antes do build (script faz taskkill) |

---

## Como Executar

Após o build, execute:

```
data\output\TACEui.exe
```

O motor `BotEngine.dll` é carregado automaticamente pela interface.

---

## Auditoria e Logs

Cada build gera um registro em:

```
docs\registros\YYYY-MM-DD\HH-MM_Plataforma_TACE_DESKTOP_BUILD\build.log
```

Logs de execução ficam em `logs/`.

---

## Licença

Este projeto é distribuído sob **Licença Proprietária**. Todos os direitos reservados.

### Termos Resumidos

- **Uso de binários:** permitido para uso pessoal ou comercial, mantendo avisos de copyright.
- **Código-fonte:** propriedade exclusiva do autor; uso/modificação/redistribuição requer autorização prévia e por escrito.
- **Engenharia reversa:** proibida.
- **Garantia:** software fornecido "como está", sem garantias.

Consulte o arquivo [`LICENSE`](LICENSE) para os termos completos.

Para solicitar autorização de uso do código-fonte, entre em contato com o autor.

---

*TACE — Automação de alta performance com arquitetura híbrida.*