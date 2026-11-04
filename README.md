# TACE — Tablet Auditor & Configurator for Education

![TACE Banner](docs/banner.svg)

[![Build](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/tace-platform/tace)
[![Platform](https://img.shields.io/badge/platform-Windows-blue)](https://img.shields.io/badge/platform-Windows-blue)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://img.shields.io/badge/.NET-8.0-purple)
[![C](https://img.shields.io/badge/C-GCC-orange)](https://img.shields.io/badge/C-GCC-orange)
[![C%23](https://img.shields.io/badge/C%23-WPF-239120)](https://img.shields.io/badge/C%23-WPF-239120)
[![ADB](https://img.shields.io/badge/ADB-Automation-red)](https://img.shields.io/badge/ADB-Automation-red)
[![License](https://img.shields.io/badge/license-Proprietary-lightgrey)](https://img.shields.io/badge/license-Proprietary-lightgrey)
![Status](https://img.shields.io/badge/status-Active-success)
![Architecture](https://img.shields.io/badge/architecture-Hybrid%20Premium-9cf)
![UI](https://img.shields.io/badge/UI-Glassmorphism%20Cyber--Industrial-ff69b4)

---

## 🎯 Visão Geral

**TACE** — Tablet Auditor & Configurator for Education. Sistema de automação robótica de alta performance para configuração e auditoria de tablets em avaliações municipais, com arquitetura **Híbrida Premium**: motor nativo em C + interface visual em C# WPF.

> **Impacto Comprovado:**
> - Reduz erros de corrupção de dados de **10 para 1**
> - Aumenta produção de **100/dia para 100/hora** (dependendo de conexões ADB)
> - Escala de 1 a 10+ portas USB simultâneas

---

## 🏗️ Arquitetura Híbrida Premium

| Camada | Tecnologia | Responsabilidade |
|--------|-----------|------------------|
| **Apresentação** | TACEui.exe · .NET 8 · Glassmorphism Cyber-Industrial | Interface visual, UX, interação usuário |
| **Interoperabilidade** | src/api.c · P/Invoke seguro · Zero-Leak | Pontuação entre C# e C nativo |
| **Motor C** | BotEngine.dll · GCC · Alta performance | ADB, dispositivos, configurações, banco offline |
| **Dispositivos** | ADB · Tablets · MAC/SN · Logs · Auditoria | Comunicação física com tablets |

### Princípios Arquiteturais

- **Zero-Leak:** camadas isoladas, sem vazamento de responsabilidades
- **Híbrida Premium:** C puro para performance + WPF para UX
- **Escalabilidade Horizontal:** 1 a N conexões ADB simultâneas
- **Auditoria Total:** logs estruturados e relatórios de erro
- **Configuração Dinâmica:** sem hardcoding de variáveis globais
- **IA Adaptativa (LeanBot):** aprendizado por equipamento (em fase de testes)

### Padrões de Design

- **Facade:** `src/api.c` como fachada única do motor para a UI
- **Strategy:** módulos Vision7 / TabMulti / Nokia T20 como estratégias por dispositivo
- **Repository:** `core/data/` encapsula persistência (municipios, cofre, dispositivos)
- **Observer (logs):** auditoria contínua com geração de relatórios
- **Factory (build):** `ControleGeral.bat` orquestra compilação multi-target

---

## 📁 Estrutura do Projeto

```
TACE/
├── .github/                     # Workflows e templates do GitHub
├── config/                      # Configurações dinâmicas
│   ├── settings.json            # Configurações globais (sem hardcoding)
│   ├── version.txt              # Versão de build
│   └── municipios.csv           # Seed público de municípios
├── data/                        # Dados de execução
│   ├── input/                   # Entradas (municipios.csv, apps)
│   │   ├── municipios/
│   │   └── apps/
│   ├── output/                  # Binários compilados (dev)
│   │   ├── BotEngine.dll        # Motor C compilado
│   │   └── TACEui.exe           # Interface WPF
│   │   ├── arquivos/
│   │   │   ├── apks/
│   │   │   └── pacotes/
│   │   └── data/                # Dados de runtime (bot_learn.json)
│   └── bot_database.db          # Banco de dados de dispositivos
├── docs/                        # Documentação técnica
│   ├── pipeline_architecture.md # Arquitetura em pipeline
│   ├── banner.svg               # Imagem de capa do projeto
│   └── registros/               # Auditorias de build (YYYY-MM-DD/HH-MM_...)
├── logs/                        # Logs de execução e auditoria
├── scripts/                     # Operação e automação
│   ├── scripts de controle/     # ControleGeral.bat (menus 1-9)
│   │   ├── ControleGeralLauncher.c
│   │   ├── TACE.iss             # Script do instalador TACE Setup
│   │   └── InstallerAssets/     # Assets do instalador
│   └── scripts de teste/        # Monitoramento e testes
│       ├── gravar_fluxo_manual.ps1
│       ├── analise_xml.ps1
│       ├── show_zip_node.ps1
│       └── monitor_tela.bat
├── src/                         # Fonte principal
│   ├── api.c                    # Interface P/Invoke
│   ├── core/                    # Motor C
│   │   ├── config/              # Configuração dinâmica
│   │   ├── data/                # municipios, cofre, paths, dispositivos
│   │   ├── drivers/             # ADB
│   │   ├── functions/           # vision7, tabmulti, nokia_t20
│   │   └── learn/               # LeanBot (IA adaptativa)
│   └── ui/                      # Projeto WPF (.NET 8)
│       ├── Assets/Icons/        # Ícones e recursos visuais
│       ├── Core/
│       ├── Properties/
│       ├── Resources/           # gnirehtet.zip
│       └── Views/               # Telas da aplicação
└── tools/                       # Ferramentas externas
    ├── platform-tools/          # ADB (Google)
    ├── reverse_tethering/       # Gnirehtet (reverse tethering)
    └── mac_address_finder.apk
```

---

## 🚀 Como Compilar

### Build Teste (recomendado para desenvolvimento)

```cmd
cd P:\Projetos\TACE
scripts\scripts de controle\ControleGeral.bat 1
```

**O que acontece:**
1. Gera número de versão único (`DD.MM.YYYY.HHMM`) em `config/version.txt`
2. Cria pasta de auditoria em `docs/registros/YYYY-MM-DD/HH-MM_..._BUILD`
3. Compila o motor C em `data/output/BotEngine.dll` (GCC, estático)
4. Publica a UI WPF em `data/output/TACEui.exe` (self-contained, single-file)
5. Copia `municipios.csv` e cria pastas auxiliares

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

**Requer TACE Setup 6.** Gera `TACE_Instalador_<versao>.exe`.

### Instalar Dependências Automaticamente

```cmd
scripts\scripts de controle\ControleGeral.bat 4
```

Verifica e instala GCC e .NET 8 SDK via Winget.

### Troubleshooting

| Problema | Solução |
|----------|---------|
| `gcc` não reconhecido | Instale MinGW (`choco install mingw`) e reinicie o terminal |
| `.NET 8 SDK` não encontrado | `choco install dotnet-8.0-sdk` |
| Recurso `gnirehtet.zip` ausente | Crie `src/ui/Resources/gnirehtet.zip` (zip vazio) |
| UI não compila por SDK 10 | Use SDK 8 instalado; script aponta para `dotnet.exe` 8 |
| Binário em uso | Feche `TACEui.exe` antes do build (script faz taskkill) |

---

## ▶️ Como Executar

Após o build, execute:

```cmd
data\output\TACEui.exe
```

O motor `BotEngine.dll` é carregado automaticamente pela interface.

---

## 📝 Auditoria e Logs

Cada build gera um registro em:

```
docs\registros\YYYY-MM-DD\HH-MM_Plataforma_TACE_DESKTOP_BUILD\build.log
```

Logs de execução ficam em `logs/`.

---

## 📄 Licença

Este projeto é distribuído sob **Licença Proprietária**. Todos os direitos reservados.

### Termos Resumidos

- **Uso de binários:** permitido para uso pessoal ou comercial, mantendo avisos de copyright
- **Código-fonte:** propriedade exclusiva do autor; uso/modificação/redistribuição requer autorização prévia e por escrito
- **Engenharia reversa:** proibida
- **Garantia:** software fornecido "como está", sem garantias

Consulte o arquivo [`LICENSE`](LICENSE) para os termos completos.

Para solicitar autorização de uso do código-fonte, entre em contato com o autor.

---

## 📦 Release Notes — Versão Atual

- **Versão:** `21.10.2026.0932`
- **`TACEui.exe`:** 194.006.309 bytes — SHA-256 `948ABEE49D13FDE74FDA687893CA226C85C3C8CEE4FFFB645D3D0E1D773FA166`
- **`BotEngine.dll`:** 418.471 bytes — SHA-256 `A2D67AC7307B0824CCD72CC5010782AADDE35B7E2A7BE24DEC9F0DF554432F58`
- **Arquitetura:** Híbrida Premium (C + WPF .NET 8)
- **Status:** Ativa — Produção

---

*TACE — Automação de alta performance com arquitetura híbrida.*

---
Próximos passos: conferir scripts de build, validar ícone do projeto e documentação completa.
<tool_call>
<function=shell>
<parameter=workdir>
P:\Projetos