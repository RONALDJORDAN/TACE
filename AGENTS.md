# Diretrizes Globais e Regras de Desenvolvimento - Bot_RPA

Este documento estabelece as regras obrigatórias que qualquer desenvolvedor ou agente de Inteligência Artificial DEVE seguir rigorosamente ao trabalhar no código-fonte e na estrutura de pastas deste repositório.

---

## 📌 1. Checklist Inicial Obrigatório
Antes de realizar qualquer modificação no repositório:
1. **Leitura de Memória:** Ler o arquivo [memoria_bot_rpa.md](file:///c:/PROJETOS/Bot_RPA/memoria_bot_rpa.md) localizado na raiz para compreender o histórico, arquitetura e estado atual do sistema.
2. **Confirmação de Contexto:** Confirmar o nome do projeto (**Bot_RPA**) e a data/hora exata do início do trabalho.

---

## 🎯 2. Regra de Ouro #1 - Isolamento Estrito por Equipamento / Modelo
- **Modularidade Total:** O motor em C é dividido estritamente por fabricante e modelo de dispositivo dentro de `src/core/functions/`:
  - 📱 **Positivo Tab Vision 7:** `src/core/functions/vision7/` (`f1_vision_tab7.c`, `f2_vision_tab7.c`, `f3_vision_tab7.c`, etc.)
  - 📱 **Multilaser TabMulti M7:** `src/core/functions/tabmulti/` (`f1_tabmulti_m7.c`, `f2_tabmulti_m7.c`, `f3_tabmulti_m7.c`, etc.)
  - 📱 **Nokia T20 (Android 11):** `src/core/functions/nokia_t20/` (`f1_nokia_t20.c`, `f2_nokia_t20.c`, `f3_nokia_t20.c`, etc.)
- **Efeito Colateral ZERO:** Ao realizar configurações, ajustes ou otimizações em uma função de um equipamento específico (exemplo: alterar a função F1 do Positivo Tab Vision 7 em `f1_vision_tab7.c`), **É EXPRESSAMENTE PROIBIDO** modificar ou afetar o código de outros equipamentos. A alteração deve ser feita **estritamente e apenas** no arquivo correspondente àquele modelo.

---

## 📝 3. Regra de Ouro #2 - Rastreabilidade e Histórico Obrigatório de Ajustes
- **Registro de Memória:** Toda e qualquer alteração de código, refatoração, ajuste técnico ou nova funcionalidade DEVE ser registrada e detalhada em ordem cronológica no arquivo [memoria_bot_rpa.md](file:///c:/PROJETOS/Bot_RPA/memoria_bot_rpa.md).
- **Logs de Auditoria de Build:** Todas as compilações realizadas devem registrar um histórico no padrão `registros/YYYY-MM-DD/HH-MM_Plataforma_BotRPA_DESKTOP_BUILD`.
- **Versionamento Git:** commits devem seguir o padrão Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `style:`).

---

## 📂 4. Regra de Ouro #3 - Organização Limpa de Pastas
- **Ferramentas (`tools/`):** Concentra 100% dos executáveis e utilitários externos (`tools/platform-tools/`, `tools/reverse_tethering/`, `tools/mac_address_finder.apk`).
- **Configurações (`config/`):** Contém estritamente arquivos de configuração JSON (`settings.json`, `first_run.json`).
- **Zero Poluição:** Não criar arquivos soltos na raiz ou pastas físicas de backup local. O Git e o `memoria_bot_rpa.md` cuidam da versão histórica.
- **Mapa oficial de arquivos:**
  - Raiz: apenas `.gitignore`, `README.md`, `AGENTS.md`, `memoria_bot_rpa.md`, pastas do projeto e dot-configs locais (`compile_commands.json`, `.clangd` — ignorados pelo Git). **Nunca** binários (`*.dll`, `*.exe`) nem CSVs/TXTs soltos na raiz.
  - Binários de dev/teste: `data/output/` (`BotEngine.dll`, `BotRPAui.exe`).
  - Release portátil PnP: `BotRPA_Release/`.
  - Ferramentas de administração (somente dev, fora da Release): `controle/` (`Build.ps1` + `Build.bat` = painel Teste/Final, `Terminal.ps1` + `Terminal.bat` = REPL admin, `Atualizacao.*` = pacote OTA).
  - Documentação viva: `docs/plano_melhorias.md` (fases e checkboxes) + `memoria_bot_rpa.md` (histórico cronológico).
  - `municipios.csv`: seed versionado em `config/municipios.csv`; em runtime o motor C lê o CSV ao lado do executável (`exe_dir\municipios.csv`, ex: `data/output/municipios.csv`).
  - Versão oficial: `config/version.txt` (lida pelo OTA via `OtaUpdateManager`).

---

## ⚡ 5. Regra de Ouro #4 - Política de Zero Warning & Build Obrigatório
- Ao término de edições nos arquivos em C ou C#, é **obrigatório** rodar o script de compilação `scripts/build.bat` para validar a ausência de erros e warnings.
