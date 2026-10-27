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
  paths WinGet com hash frágeis. (O `scripts/scripts de controle/Build.ps1` já mitiga por fora com
  checagem de artefatos.)
  Proposta: fail-fast interno + unificar os 3 scripts no `Build.ps1` como orquestrador
  canônico.   (Correção `wmic` → `Get-Date` feita em 24/09/2026.)

- [x] **🟢 Terminal de controle — implementado 25/09/2026**
  `scripts/scripts de controle/Terminal.ps1` + `Terminal.bat`: REPL `BotRPA>` com `ajuda`, `build teste`,
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
  Problema: só o BotLearn tem rotação (3 dias); `logs/` e `docs/registros/` crescem sem limite.
  Proposta: retenção de 30 dias no startup.

- [ ] **🟢 Binários fora do Git — esforço P**
  Problema: pack de 16,8MB (`adb.exe` 8MB etc.); `PreflightChecker` já baixa
  platform-tools sozinho.
  Proposta: remover do versionamento (ou Git LFS).

## FASE 6 — Decisão pendente (comportamento do produto)

- [x] **🟢 BotLearn "morto-vivo"**
  Stubs no-op desde 16/07, mas a UI promete IA que aprende. Decidir: reativar com
  guardrails ou ajustar o discurso para "seletores determinísticos versionados".
  Decisão em 26/10/2026: discurso ajustado para "Motor de Estratégias Adaptativas",
  com ranking heurístico e feedback real; limitações do motor (persistência parcial,
  seeds mortos e feedback variável por modelo) documentadas na memória. Reativação
  plena com guardrails permanece fora do escopo desta entrega.

## Apêndice — NÃO mexer (acertos estruturais)

Isolamento por modelo como princípio · política zero-warnings · auditoria em
`docs/registros/` · SHA-256 no OTA · Fast UI parser em RAM · SQLite local como fonte da
verdade operacional.

## Backlog operacional herdado

- Testes em bancada com hub USB e lotes de tablets.
- Validação SQLite com múltiplos dispositivos simultâneos.

---

# PLANO DE IMPLEMENTAÇÃO — 26/10/2026 (concluído)

**Escopo consolidado desta sessão:** (a) melhorar a página de Registros & Auditoria;
(b) armazenar registros por data/hora e por modalidade; (c) refazer a exportação PDF
como relatório executivo vertical; (d) reescrever a página Sobre com dados reais.
Itens ficam `[ ]` até a validação e passam a `[x]` após build/teste.

## Decisões transversais
- Empresa no cabeçalho do PDF: **InnovEdu** (grafia solicitada), setor **Operacional**.
  Manter as duas strings centralizadas para troca fácil.
- PDF A4 retrato gerado com `PdfSharpCore` (já referenciado no projeto), sem depender do
  Microsoft Print to PDF.
- Preservar leitura dos registros antigos (`docs/registros/AAAA-MM-DD/*.log` e builds).
- Novos logs de execução: `docs/registros/AAAA-MM-DD/EXECUCAO/HH-mm_<serial>_execucao.log`.
  Builds continuam no caminho canônico do AGENTS e são classificados como modalidade `Build`.
- Auditoria ganha coluna `Modalidade` com migração idempotente e backfill dos eventos antigos.
- Nenhuma remoção destrutiva: migrações e organização apenas aditivas.

## Fase 1 — Armazenamento por data/hora/modalidade
- [x] Criar `src/ui/Core/RegistroStorage.cs`: modalidades (`Build`, `Execução`, `OTA`,
  `Sistema`), pastas por dia/modalidade, sanitização e escrita thread-safe.
- [x] Atualizar `MonitorView.LogMessage` para gravar via `RegistroStorage` com cabeçalho de
  sessão e linhas `[AAAA-MM-DD HH:mm:ss]`, mantendo o log visual atual.
- [x] Ajustar `ControleGeral.bat` para registrar `Modalidade: BUILD` no `build.log`,
  mantendo a pasta canônica `docs/registros/AAAA-MM-DD/HH-MM_..._BUILD`.
- [x] `AuditLogManager`: adicionar `Modalidade` ao modelo e à tabela (ALTER TABLE + índice),
  backfill dos eventos existentes e parâmetro de modalidade na gravação.
- [x] Criar `LerFiltrado(periodo, modalidade, busca, limite)` e contagens por modalidade,
  mantendo `LerTodos` compatível.

## Fase 2 — Página Registros & Auditoria
- [x] Cabeçalho novo com 4 KPIs: total de registros, registros de hoje, builds e execuções.
- [x] Abas internas: **Arquivos & Builds** e **Auditoria da Base**.
- [x] Registros: busca, filtro de modalidade, filtro de período e contador; lista com
  Data, Hora, Modalidade, Registro e Tamanho; visualizador com seletor de arquivos,
  metadados, copiar, abrir no Bloco de Notas e abrir pasta/arquivo.
- [x] Varredura recursiva compatível com o layout antigo e o novo, sem duplicar builds;
  classificação de modalidade e ordenação por data/hora decrescente.
- [x] Auditoria: grade com Data/Hora, Modalidade, Ação, Tabela, Registro, Usuário e Detalhes;
  filtros de busca, modalidade e período; painel de detalhes da linha; contador.
- [x] Atualizar navegação e recarga sem pop-ups desnecessários; estados vazios claros.

## Fase 3 — PDF executivo vertical
- [x] Substituir `PrintDialog`/`FlowDocument` por geração direta com `PdfSharpCore` e
  `SaveFileDialog`.
- [x] Resolver de fontes do Windows (Segoe UI/Arial/Consolas) e metadados do PDF.
- [x] Estrutura: **capa**; **contracapa/ficha técnica**; **sumário**; primeira página de
  conteúdo com KPIs e **gráficos**; resumo por fabricante/modelo; dados por dispositivo;
  **anexo final** com marca, modelo, número de série e MAC em ordem.
- [x] Cabeçalho em todas as páginas de conteúdo com InnovEdu, Setor Operacional, título e
  data; rodapé com numeração e versão.
- [x] Gráficos vetoriais: rosca por modelo, barras por fabricante e cobertura do cadastro,
  com legendas e percentuais.
- [x] Sumário com páginas reais (duas passagens) e quebras de seção controladas.
- [x] Validar com seleção personalizada e base completa; abrir o PDF gerado; tratar erros.

## Fase 4 — Página Sobre
- [x] Revisar `AboutView.xaml`/`.cs` e remover placeholders e dados obsoletos.
- [x] Exibir dados reais: versão/build, ambiente, empresa/setor, arquitetura, módulos,
  tecnologias e versões, funcionalidades, dispositivos suportados, contagens do banco,
  caminhos de dados, créditos, documentação e avisos legais.
- [x] Layout consistente com o restante (cards, seções, tipografia e scroll responsivo).
- [x] Dados dinâmicos sempre que possível (versão, contagens, status) e estáticos precisos
  quando não houver fonte em runtime.

## Fase 5 — Menu superior (identidade visual)
- [x] Substituir o bloco "BR"/logo textual do menu superior padrão (MainWindow) pelo
  ícone da ferramenta (`Assets/Icons/BotRPAui.ico` ou imagem equivalente), ao lado do
  texto "Bot_RPA", preservando tamanho, alinhamento e comportamento em todas as páginas.
- [x] Validar em janela normal, maximizada e fullscreen.

## Fase 6 — Validação
- [x] `dotnet build` sem novos warnings; build TESTE via `ControleGeral.bat 1` conforme AGENTS.
- [x] Testar Registros (legado + novo), Auditoria (filtros), PDF (capa/contracapa/sumário/
  gráficos/anexo), Sobre e menu superior.
- [x] Registrar o detalhamento e o build em `memoria_bot_rpa.md`; marcar este plano.

**Concluído em 26/10/2026:** build `26.10.2026.2120`, auditoria em
`docs/registros/2026-10-26/21-20_Plataforma_BotRPA_DESKTOP_BUILD`.
Registros: 317 total, 24 hoje, 72 builds e 245 execuções. Sobre: 463 dispositivos,
400 patrimônios, 406 MACs, 1.136 eventos e 86,4% de cobertura. PDF real: 30 páginas
(compactado para 18 páginas em 27/10/2026; ver plano seguinte).

## Critérios de aceite
- Registros antigos continuam visíveis; novos logs ficam em pasta de modalidade com data/hora.
- Auditoria filtra por período, modalidade e busca, com detalhes legíveis.
- PDF A4 retrato com capa, contracapa, sumário, gráficos na primeira página de conteúdo,
  dados por dispositivo e anexo final com marca/modelo/serial/MAC.
- Página Sobre sem informações desatualizadas, com versão e contagens corretas.
- Zero erros de compilação e nenhuma regressão nas telas existentes.

## Arquivos previstos
- `src/ui/Core/RegistroStorage.cs` (novo)
- `src/ui/Core/AuditLogManager.cs`
- `src/ui/Views/MonitorView.xaml.cs`
- `src/ui/Views/RelatoriosView.xaml`
- `src/ui/Views/RelatoriosView.xaml.cs`
- `src/ui/Views/AboutView.xaml`
- `src/ui/Views/AboutView.xaml.cs`
- `scripts/scripts de controle/ControleGeral.bat`
- `docs/plano_melhorias.md`, `memoria de desenvolvimento/memoria_bot_rpa.md`

## Riscos e mitigação
- Fonte ausente no PDF: fallback para Arial/DejaVu instalado no Windows.
- Migração SQLite: `ALTER TABLE` idempotente e protegido por try/catch; nunca apagar dados.
- Logs legados: leitura recursiva e classificação por nome; nada é movido automaticamente.
- Build canônico: preservar o caminho exigido pelo AGENTS e apenas enriquecer metadados.

---

# ÚLTIMO PLANO DE IMPLEMENTAÇÃO — 27/10/2026 (concluído)

**Escopo consolidado desta sessão:** (a) compactar o relatório PDF; (b) modernizar a
tela Gerenciar Municípios; (c) revisar Configurações e remover opções sem efeito;
(d) tornar o backup confiável com banco em uso e bloqueio de Zip Slip.

## Fase 1 — Relatório PDF compacto
- [x] Remover a seção duplicada de dispositivos; manter um único anexo de 4 colunas:
  Marca, Modelo, Nº de Série e MAC, sem coluna `#`.
- [x] Atualizar sumário, contracapa e cabeçalhos para refletir o anexo único.
- [x] Validar PDF real com 463 dispositivos: 18 páginas (antes ~30), A4 retrato.

## Fase 2 — Gerenciar Municípios
- [x] Reescrever layout e fluxo de cadastro/edição, com lista, busca, contador e status
  de arquivos anexados.
- [x] Nome do município somente leitura na edição; renomear exige novo cadastro.
- [x] Validar nome vazio, duplicidade exata e duplicidade pela chave sanitizada ASCII.
- [x] Anexar/copiar APK, pacote ZIP e wallpaper; preservar arquivo de origem na purga.
- [x] Excluir pastas de APK, pacote e wallpapers do município (incluindo `clean`/`cleanAscii`).
- [x] Corrigir binding da lista (propriedades `NomeExibicao`/`LoginExibicao` em vez de campos).
- [x] Restaurar cor do status após carregar, limpar, preencher, salvar, anexar e excluir.
- [x] Ajustar janela para 940x700, com mínimo 840x620, sem cortar o formulário.

## Fase 3 — Configurações
- [x] Substituir toggles de Motor C e Reverse Tethering por selos informativos
  (`SEMPRE ATIVO` e `INTEGRADO AOS FLUXOS`), pois não havia fluxo consumindo os toggles.
- [x] Adicionar card de licença com a versão do sistema.
- [x] Corrigir textos/acentos e impedir desativar todos os modelos de tablet.
- [x] Manter as propriedades legadas em `SettingsManager` sem exposição na UI.

## Fase 4 — Backup e restauração
- [x] Incluir `config`, `municipios.csv`, banco SQLite, registros, arquivos operacionais
  e `bot_learn.json` (quando existente).
- [x] Criar snapshot consistente do SQLite via `BackupDatabase`, sem erro de arquivo em uso.
- [x] Adicionar arquivos ao ZIP com `FileShare.ReadWrite | FileShare.Delete`.
- [x] Bloquear Zip Slip na importação com caminho absoluto validado e contagem de ignorados.
- [x] Testar exportação real: 289 entradas, banco de 471.040 B com 463 dispositivos.

## Fase 5 — Validação
- [x] `dotnet build` com 0 erros e 0 avisos.
- [x] `ControleGeral.bat 1` concluído; build `27.10.2026.0049`.
- [x] UIAutomation: lista de municípios com “Boca da Mata”/“aplicador”, busca e estado vazio;
  Configurações com selos, versão e card de licença.
- [x] Exportação de backup validada em runtime; ZIP aberto e banco conferido.
- [x] Registrar detalhamento e build em `memoria_bot_rpa.md`.

**Concluído em 27/10/2026:** build `27.10.2026.0049`, auditoria em
`docs/registros/2026-10-27/00-49_Plataforma_BotRPA_DESKTOP_BUILD`.
Relatório compacto: 463 dispositivos em 18 páginas. Backup: 289 entradas e snapshot
SQLite íntegro. Municípios/Configurações validados por UIAutomation.

## Arquivos principais
- `src/ui/Core/RelatorioPdfGenerator.cs`
- `src/ui/Views/MunicipiosWindow.xaml`, `src/ui/Views/MunicipiosWindow.xaml.cs`
- `src/ui/Views/MonitorView.xaml.cs` (propriedades de exibição do município)
- `src/ui/Core/PathHelper.cs`
- `src/ui/Views/ConfigView.xaml`, `src/ui/Views/ConfigView.xaml.cs`
- `src/ui/Core/SQLiteDatabaseManager.cs`
- `src/ui/Core/SettingsManager.cs`
- `config/version.txt`
- `docs/plano_melhorias.md`, `memoria de desenvolvimento/memoria_bot_rpa.md`

## Riscos e mitigação
- Banco em uso: snapshot SQLite consistente em vez de copiar o arquivo aberto.
- Backup grande: gravação em ZIP com compactação ótima e progresso na UI.
- Zip Slip: entradas fora da raiz são ignoradas e contabilizadas.
- Sem tablet físico: fluxos ADB/F1-F8 permanecem pendentes de bancada.
- Nome do município é chave de pastas: renomeação só por novo cadastro.
