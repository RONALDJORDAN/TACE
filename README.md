# Bot_RPA

![Bot_RPA Banner](docs/banner.svg)

![Build](https://img.shields.io/badge/build-passing-brightgreen) ![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![C](https://img.shields.io/badge/C-GCC-orange) ![C%23](https://img.shields.io/badge/C%23-WPF-239120) ![ADB](https://img.shields.io/badge/ADB-Automation-red) ![License](https://img.shields.io/badge/license-Proprietary-lightgrey) ![Status](https://img.shields.io/badge/status-Active-success) ![Architecture](https://img.shields.io/badge/architecture-Hybrid%20Premium-9cf) ![UI](https://img.shields.io/badge/UI-Glassmorphism%20Cyber--Industrial-ff69b4)

> Sistema de automação robótica de alta performance com arquitetura **Híbrida Premium**: motor nativo em C + interface visual em C# WPF.

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

O **Bot_RPA** foi criado para **automatizar a configuração de dispositivos específicos** utilizados em **aplicações de avaliações municipais**, reduzindo drastically a ocorrência de erros de corrompimento de dados — de **10 para 1** — e o tempo de produção de **100 por dia para 100 por hora**, dependendo da quantidade de conexões ADB utilizadas.

A ferramenta escala horizontalmente: é possível usar **1 porta USB ou 10 portas USB** simultaneamente, fazendo com que o tempo de produção diminua **exponencialmente** conforme mais dispositivos são conectados via cabo USB.

Além disso, o Bot_RPA **registra todos os aparelhos conectados via ADB**, capturando:
- Número de série
- Endereço MAC da rede Wi-Fi (evitando MACs invisíveis/ocultos)

Também gera **registro de auditoria dos equipamentos**, extraindo dados em logs e convertendo-os em informações legíveis, fornecendo **relatórios sobre os erros ocorridos nas aplicações**, permitindo correção e melhoria contínua para as próximas execuções.

---

## Visão Geral

O **Bot_RPA** combina a velocidade bruta do **C puro** com a excelência visual do **C# WPF**, seguindo a política de **Zero-Leak** e isolamento de camadas.

- **Motor (BotEngine.dll):** C puro, gerencia ADB, dispositivos, configurações e banco offline em memória.
- **Interface (BotRPAui.exe):** WPF .NET 8 com design *Glassmorphism Cyber-Industrial*, consumindo o motor via `DllImport`.

---

## Módulos e Submódulos

O Bot_RPA conta com diversos módulos e submódulos organizados por responsabilidade:

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

```
┌──────────────────────┐        ┌──────────────────────┐
│  BotRPAui.exe (WPF)  │  P/Invoke  │  BotEngine.dll (C)    │
│  UI / UX             │ ────────► │  Motor / ADB / Data   │
└──────────────────────┘        └──────────────────────┘
```

Camadas isoladas garantem escalabilidade e robustez operacional.

---

## Estrutura do Projeto

```
Bot_RPA/
├── config/              # Configurações dinâmicas (settings.json, version.txt)
├── data/
│   ├── input/           # Entradas (municipios.csv)
│   └── output/          # Binários compilados
├── docs/                # Documentação e auditorias de build
├── logs/                # Logs de execução
├── scripts/
│   ├── scripts de controle/   # ControleGeral.bat (build, release, installer)
│   └── scripts de teste/      # Ferramentas auxiliares
├── src/
│   ├── core/            # Código C puro (ADB, dispositivos, funções)
│   └── ui/              # Projeto WPF C# (.NET 8)
└── tools/               # ADB platform-tools e dependências
```

---

## Pré-requisitos

| Ferramenta | Versão | Observação |
|---|---|---|
| GCC (MinGW) | Recente | Via Chocolatey: `choco install mingw` |
| .NET SDK | 8.0+ | Via Chocolatey: `choco install dotnet-8.0-sdk` |
| Inno Setup | 6.x | Apenas para gerar instalador (opção 3) |

---

## Como Compilar

Execute o script unificado:

```cmd
scripts\scripts de controle\ControleGeral.bat 1
```

Isso gera:
- `data\output\BotEngine.dll` (motor C)
- `data\output\BotRPAui.exe` (interface WPF self-contained)

### Opções adicionais

| Comando | Descrição |
|---|---|
| `ControleGeral.bat 2` | Build Release (pacote portátil) |
| `ControleGeral.bat 3` | Build Instalador (Inno Setup) |
| `ControleGeral.bat 4` | Instalar dependências |

---

## Como Executar

Após o build, execute:

```
data\output\BotRPAui.exe
```

O motor `BotEngine.dll` é carregado automaticamente pela interface.

---

## Auditoria e Logs

Cada build gera um registro em:

```
docs\registros\YYYY-MM-DD\HH-MM_Plataforma_BotRPA_DESKTOP_BUILD\build.log
```

Logs de execução ficam em `logs/`.

---

## Contribuição

1. Faça um fork do repositório
2. Crie uma branch: `git checkout -b feature/minha-feature`
3. Commit suas mudanças: `git commit -m "feat: descrição"`
4. Push: `git push origin feature/minha-feature`
5. Abra um Pull Request

---

## Licença

Projeto proprietário. Todos os direitos reservados.

---

*Bot_RPA — Automação de alta performance com arquitetura híbrida.*