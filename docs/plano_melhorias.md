# Plano de Melhoria — Bot_RPA (Plataforma InnovTech)

> Criado em 24/09/2026 a partir da auditoria técnica da sessão.
> Como usar: cada item tem `[ ]` de status. Marque `[x]` ao concluir e registre o
> detalhamento cronológico em `memoria_bot_rpa.md` (Regra de Ouro #2).
> Legenda: 🔴 alto impacto · 🟡 médio · 🟢 higiene | Esforço: P/M/G.

## FASE 1 — Segurança e LGPD (fazer primeiro)

- [ ] **🔴 Cofre de senhas (DPAPI) — esforço M**
  Problema: `config/municipios.csv` guarda `login;senha` em texto puro e é copiado
  para `data/output/`, `BotRPA_Release/` e runtime. Quem recebe a release leva as
  credenciais de todos os municípios.
  Proposta: cifrar a coluna com DPAPI do Windows (`CryptProtectData`, sem senha
  mestra). Separar seed público de cofre local.

- [ ] **🔴 Endurecer Firebase (auth + PII mínima) — esforço P/M**
  Problema: RTDB acessado sem autenticação; seriais/IMEI/MAC/hostname sobem em
  `rpa_devices/` e `ota/devices/` (`SQLiteDatabaseManager.cs`, `OtaUpdateManager.cs`).
  Proposta: segredo `?auth=` no `settings.json`, revisar rules do RTDB, enviar hash
  do serial em vez do serial puro onde possível.

## FASE 2 — Arquitetura (custo de cada correção futura)

- [ ] **🔴 Biblioteca de passos comuns entre modelos — esforço G**
  Problema: 3×F1 somam ~5.200 linhas "100% autônomas" (`f1_vision_tab7.c` 2378 +
  `f1_nokia_t20.c` 1478 + `f1_tabmulti_m7.c` 1337) para a mesma sequência canônica.
  Cada fix (fuzzy, batching, polling) foi replicado modelo a modelo em datas distintas.
  Proposta: extrair passos comuns parametrizados por perfil (timings/coordenadas por
  modelo). Isolamento via configuração, não via cópia. Regra #1 mantida.

- [ ] **🟡 Router único de dispatcher F1–F8 — esforço M**
  Problema: chaveamento por modelo duplicado em `MonitorView` e `MonitorProView`.
  Proposta: `DeviceActionRouter` único usado pelas duas telas.

- [ ] **🟡 Sleeps hardcoded → polling por estado — esforço M (fatiável)**
  Problema: 207 `Sleep()` nos pipelines (`f1_vision_tab7.c` 53, `f1_nokia_t20.c` 45,
  `f1_tabmulti_m7.c` 35). Timing fixo em vez de espera por estado.
  Proposta: generalizar `aguardar_condicao_xml(timeout, predicado)` (já existe em pontos
  isolados). Converte por função, começando pelo F1 Vision 7.

## FASE 3 — Nuvem bidirecional (Fase 1 implementada em 24/09/2026)

- [x] **🟡 Download `rpa_devices` (Fase 1) — implementado 24/09/2026**
  `BaixarDoInnovStockAsync()` em `SQLiteDatabaseManager.cs` + botão
  "⬇ Baixar InnovStock" nos Relatórios + toggle `cloud_pull_auto` (default ligado)
  com pull no startup (`App.xaml.cs`). Política segura: só adiciona/preenche vazios,
  nunca apaga nem sobrescreve valor válido; sem re-upload (sem loop).
  Build TESTE `24.09.2026.1901` validado, zero warnings.

- [x] **🟡 Sync automático contínuo — implementado 25/09/2026**
  `CloudAutoSyncService.cs`: timer periódico (`cloud_sync_minutes`, default 15),
  disparo ao reconectar (`NetworkAvailabilityChanged` com debounce), guarda contra
  sobreposição (`Interlocked`) e checagem offline (`GetIsNetworkAvailable`).
  Após o pull, devolve à nuvem (PUT individual) só as linhas tocadas. Toggle único
  em Configurações + ComboBox de intervalo (5/10/15/30/60). `BaixarDoInnovStockAsync`
  passou a retornar também os seriais afetados.
  Build TESTE `25.09.2026.0815` validado, zero warnings.

- [ ] **🟢 Incrementos Fase 2 da nuvem — esforço M**
  Sync incremental por `syncedAt` (`orderBy/startAt` + índice nas rules), tombstones
  (`deleted:true`) para propagar exclusões, filtro por `machineId`, visor da frota
  global nos Relatórios.

## FASE 4 — Performance e distribuição

- [ ] **🟡 EXE de ~185MB — esforço M**
  Problema: single-file self-contained sem trimming (`BotRpaUI.csproj`); cada OTA
  baixa ~185MB.
  Proposta: avaliar `PublishTrimmed` (cuidado com WPF/reflection) ou
  `InvariantGlobalization`.

- [ ] **🟢 Endurecer scripts de build — esforço P**
  Problema: `build.bat` ignora erro do `gcc`; `Atualizacao.ps1` sem error handling;
  paths WinGet com hash frágeis. (O `controle/Build.ps1` já mitiga por fora com
  checagem de artefatos.)
  Proposta: fail-fast interno + unificar os 3 scripts no `Build.ps1` como orquestrador
  canônico.   (Correção `wmic` → `Get-Date` feita em 24/09/2026.)

- [x] **🟢 Terminal de controle — implementado 25/09/2026**
  `controle/Terminal.ps1` + `Terminal.bat`: REPL `BotRPA>` com `ajuda`, `build teste`,
  `build final`, `versao`, `dispositivos` (adb), `ota`, `deps`, `limpar`, `sair`.
  Testado funcionalmente (sintaxe 0 erros, comandos leitura/ajuda validados).

## FASE 5 — Qualidade e operação

- [ ] **🟡 Testes automatizados — esforço M (começo P)**
  Problema: só scripts manuais em `scripts/testes/`.
  Proposta: fumaça pós-build (schema CSV, JSON válido, `Engine_RefreshDevices`
  respondendo) + Unity para funções puras em C (`normalize_char`, fuzzy, parse CSV).

- [ ] **🟢 README fiel à realidade — esforço P**
  Problema: cita `docs/`, `archive/`, `data/input/`, `BotRPAui.exe` — nada existe.
  Proposta: reescrever espelhando o mapa do `AGENTS.md`.

- [ ] **🟢 Rotação de logs/registros — esforço P**
  Problema: só o BotLearn tem rotação (3 dias); `logs/` e `registros/` crescem sem limite.
  Proposta: retenção de 30 dias no startup.

- [ ] **🟢 Binários fora do Git — esforço P**
  Problema: pack de 16,8MB (`adb.exe` 8MB etc.); `PreflightChecker` já baixa
  platform-tools sozinho.
  Proposta: remover do versionamento (ou Git LFS).

## FASE 6 — Decisão pendente (comportamento do produto)

- [ ] **🟢 BotLearn "morto-vivo"**
  Stubs no-op desde 16/07, mas a UI promete IA que aprende. Decidir: reativar com
  guardrails ou ajustar o discurso para "seletores determinísticos versionados".

## Apêndice — NÃO mexer (acertos estruturais)

Isolamento por modelo como princípio · política zero-warnings · auditoria em
`registros/` · SHA-256 no OTA · Fast UI parser em RAM · SQLite local como fonte da
verdade operacional.

## Backlog operacional herdado

- Testes em bancada com hub USB e lotes de tablets.
- Validação SQLite com múltiplos dispositivos simultâneos.
