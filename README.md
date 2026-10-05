# Projeto Bot_RPA

O **Bot_RPA** é um sistema de automação robótica de alta performance construído com a arquitetura **Híbrida Premium**, combinando a velocidade bruta do **C** com a excelência visual do **C# WPF**.

## Arquitetura Híbrida (Google-Style)
O projeto está dividido em duas camadas isoladas para garantir escalabilidade e a política de **Zero-Leak**:
1. **Motor (BotEngine.dll):** Desenvolvido em C puro. Lê configurações, banco de registros offline e gerencia comandos ADB diretamente na memória.
2. **Interface Visual (BotRPAui.exe):** Desenvolvido em C# WPF. Interface *Glassmorphism Cyber-Industrial* consumindo o motor em C em tempo real via `[DllImport]`.

## Estrutura de Diretórios
- `/docs` - Documentação da arquitetura e fluxos (Veja: [Arquitetura do Pipeline](docs/pipeline_architecture.md)).
- `/archive` - Versionamento obrigatório e auditoria de cada tarefa/funcionalidade criada.
- `/config` - Configurações dinâmicas em JSON (`settings.json`). Não há Hardcoding de variáveis globais.
- `/data/input` - Arquivos de entrada (ex: `municipios.csv`).
- `/data/output` - Destino dos binários compilados (`BotEngine.dll` e `BotRpaUI.exe`).
- `/logs` - Registros de execução, auditoria e falhas (Política de Robusteza).
- `/registros` - Repositório histórico para registrar builds de compilação.
- `/scripts` - Scripts de automação (ex: `build.bat` para compilar o C e o C# numa só etapa).
- `/src/core` - Código C puro de alta velocidade (ADB, Dispositivos).
- `/src/ui` - Projeto WPF C# (.NET 8) para a Interface Gráfica.

## Como Compilar
Execute o script `scripts\build.bat` via Prompt de Comando. Ele irá gerar a DLL do motor usando GCC e compilará a interface visual usando o `.NET 8 SDK`, alocando todos os binários na pasta `data/output`.

---
*Projeto alinhado aos padrões arquiteturais de alta performance e interface Cyber-Industrial.*
