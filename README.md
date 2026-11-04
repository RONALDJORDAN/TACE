# Bot_RPA

![Bot_RPA Banner](docs/banner.svg)

![Build](https://img.shields.io/badge/build-passing-brightgreen) ![Platform](https://img.shields.io/badge/platform-Windows-blue) ![.NET](https://img.shields.io/badge/.NET-8.0-purple) ![C](https://img.shields.io/badge/C-GCC-orange)

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

## Visão Geral

O **Bot_RPA** combina a velocidade bruta do **C puro** com a excelência visual do **C# WPF**, seguindo a política de **Zero-Leak** e isolamento de camadas.

- **Motor (BotEngine.dll):** C puro, gerencia ADB, dispositivos, configurações e banco offline em memória.
- **Interface (BotRPAui.exe):** WPF .NET 8 com design *Glassmorphism Cyber-Industrial*, consumindo o motor via `DllImport`.

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