# Memória do Projeto — Bot_RPA

**Projeto:** Bot_RPA (Plataforma Bot_RPA / InnovTech)
**Tipo:** Automação RPA para tablets Android via ADB + Kiosk Mode
**Arquitetura:** Motor nativo em C (`BotEngine.dll`) + Interface WPF em C# (.NET 8, `BotRPAui.exe`)
**Versão de referência:** `21.10.2026.0909`
**Status:** Produção
**Última atualização desta memória:** 21/10/2026

---

## 1. Visão Geral

O Bot_RPA automatiza o provisionamento, atualização, configuração, sincronização e
desligamento de tablets Android em bancada, executando pipelines determinísticos
(F1 a F8) por modelo de equipamento, com comunicação 100% via ADB/USB.

- Motor C nativo com política Zero-Leak, Zero Warnings e Zero Errors.
- Interface C# WPF consumindo a DLL via P/Invoke (`[DllImport]`).
- IA visual determinística (BotAI / BotLearn) com seletores versionados e matching fuzzy.
- Operação multi-dispositivo em paralelo (dumps XML isolados por serial).
- Operação 100% offline/local; tablets são sempre operados por USB.
- Gestão de dispositivos em SQLite (`bot_database.db`) sem sincronização em nuvem.

## 2. Equipamentos Suportados (isolamento estrito por modelo)

| Modelo | Pasta do motor | Funções |
|---|---|---|
| Positivo Tab Vision 7 (Android 14) | `src/core/functions/vision7/` | F1, F2, F3, F4, F5, F6, F8 |
| Multilaser TabMulti M7 | `src/core/functions/tabmulti/` | F1/F7, F2, F3, F4, F5 |
| Nokia T20 (Android 11) | `src/core/functions/nokia_t20/` | F1, F2, F3 |

**Regra de Ouro #1:** alterações em um modelo ficam restritas ao arquivo daquele modelo.
Nunca há efeito colateral em outro equipamento.

### Pipelines principais (Positivo Tab Vision 7)

- **F1 — Conf. Geral:** provisionamento completo (wakeup, dark mode, limpeza de `/sdcard`,
  debloat, APK, Gnirehtet, login, importação do ZIP, wallpaper) e desligamento.
- **F2 — Reinstalação:** mesma esteira, com encerramento adaptado.
- **F3 — Atualização de Pacote:** envia APK/ZIP atualizados e importa sem trocar login.
- **F4 — Gestão do Aparelho:** câmera, configurações, modo restrito, blindagem,
  wallpaper, internet USB e barra de pesquisa (12 ações + perfis Blindar/Restaurar).
- **F5 — Desligamento Seguro:** encerra apps, limpa recentes e desliga.
- **F6 — Sincronização Direta:** conecta, sincroniza na nuvem e desliga.
- **F8 — Conexão Ativa:** mantém o túnel USB (Gnirehtet) ligado.

## 3. Estrutura de Pastas

```
Bot_RPA/
├── AGENTS.md                        # Regras de ouro do desenvolvimento
├── memoria_bot_rpa.md               # Histórico cronológico completo (fonte oficial)
├── DOCUMENTACAO_COMPLETA.md         # Documentação técnica detalhada
├── memmoria de desenvolvimento/     # Memória por mês/ano (esta pasta)
│   ├── demmoria do projeto.md       # Este arquivo (visão consolidada)
│   ├── 2026-06.md ... 2026-10.md    # Histórico mensal
├── FERRAMENTA_AQUI/                 # Pacotes prontos para uso
│   ├── bot rpa instalavel/          # Release PnP desktop
│   └── bot rpa movel/               # Release PnP (versão móvel/portátil)
├── src/
│   ├── core/                        # Motor C
│   │   ├── config/ data/ drivers/ functions/ learn/
│   ├── api.c                        # Exportações da DLL
│   └── ui/                          # Interface WPF (Views, Core, Assets)
├── config/                          # settings.json, first_run.json, municipios.csv, version.txt
├── tools/                           # platform-tools (ADB), reverse_tethering (Gnirehtet), APKs
├── scripts/                         # build.bat, build_release.bat
├── controle/                        # Build.ps1, Terminal.ps1 (somente dev)
├── data/                            # output/ (dev), bancos, entrada de municípios
├── docs/                            # Relatórios técnicos e plano de melhorias
├── registros/                       # Auditoria de builds (YYYY-MM-DD/HH-MM_...)
├── logs/                            # Logs de execução
├── BotRPA_Release/                  # Release portátil oficial (gerada pelo build)
├── memoria_bot_rpa.md               # (histórico principal)
└── FERRAMENTA_AQUI/                 # (pacotes de entrega)
```

## 4. Build e Release

- **Build de teste/dev:** `scripts/build.bat` → `data/output/` (ícone azul escuro).
- **Build final/PnP:** `controle/Build.ps1 -Modo Final` → `BotRPA_Release/` (ícone roxo).
- **Política:** não compilar sem ordem explícita do usuário.
- **Auditoria:** cada build grava registro em `registros/YYYY-MM-DD/HH-MM_Plataforma_BotRPA_DESKTOP_BUILD`.
- **Release atual:** `21.10.2026.0932` (sem Firebase/OTA/InnovStock)
  - `BotRPAui.exe`: 194.006.309 bytes — SHA-256 `948ABEE49D13FDE74FDA687893CA226C85C3C8CEE4FFFB645D3D0E1D773FA166`
  - `BotEngine.dll`: 418.471 bytes — SHA-256 `A2D67AC7307B0824CCD72CC5010782AADDE35B7E2A7BE24DEC9F0DF554432F58`

## 5. Pacotes de Entrega (FERRAMENTA_AQUI)

As pastas de entrega devem ser cópias exatas da release `BotRPA_Release`. Estado atual: ainda na versão `21.10.2026.0909` (sincronização com a `0929` pendente):

- **`bot rpa instalavel/`** — versão instalável desktop (executar `BotRPAui.exe`).
- **`bot rpa movel/`** — versão móvel/portátil, mesma build, para uso em campo.

Conteúdo comum: `BotRPAui.exe`, `BotEngine.dll`, `municipios.csv`, `config/`,
`tools/` (ADB + Gnirehtet + APK),
`data/bot_database.db`, `Arquivos/`, `logs/`, `registros/`.

## 6. Segurança e Dados

- Credenciais de municípios protegidas por DPAPI (cofre local por usuário/máquina).
- `config/municipios.csv` (seed versionado) não contém senhas.
- `config/settings.json` é local e não deve ser versionado com credenciais.
- Banco de dispositivos com IDs contíguos 1..N e coluna `Patrimonio`.
- Sem integrações externas: Firebase, OTA e sync InnovStock removidos.

## 7. Regras de Ouro (AGENTS.md)

1. **Isolamento estrito por equipamento/modelo** — zero efeito colateral entre modelos.
2. **Rastreabilidade obrigatória** — toda alteração registrada em `memoria_bot_rpa.md`.
3. **Organização limpa de pastas** — sem binários/CSVs soltos na raiz.
4. **Zero Warning & Build obrigatório** — validar compilação quando houver edição em C/C#.

## 8. Histórico Mensal

- [2026-06.md](2026-06.md) — Junho/2026
- [2026-07.md](2026-07.md) — Julho/2026
- [2026-08.md](2026-08.md) — Agosto/2026
- [2026-09.md](2026-09.md) — Setembro/2026
- [2026-10.md](2026-10.md) — Outubro/2026
