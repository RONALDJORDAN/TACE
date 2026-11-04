# DOCUMENTACAO COMPLETA - TACE (Plataforma InnovTech)

> Sistema de automação robótica (RPA) de alta performance para gerenciamento de dispositivos Android via ADB e Kiosk Mode.
> Arquitetura híbrida: Motor Backend em C puro + Interface Gráfica em C# WPF (.NET 8).

---

## 1. VISAO GERAL DO PROJETO

O **TACE** é uma plataforma de automação para tablets Android que executa pipelines configuráveis (F1 a F8) de instalação, atualização, configuração, desligamento e sincronização de dispositivos. Combina a velocidade bruta do C nativo com a renderização fluida do C# WPF.

- **Nome do projeto:** TACE (apelido: inno_bot)
- **Motor C nativo:** `BotEngine.dll`
- **Interface C# WPF:** `TACEui.exe`
- **Comunicação:** P/Invoke `[DllImport]` (C# consome a DLL em C)
- **Política:** Zero-Leak, Zero Warnings, Zero Errors, Isolamento Estrito por Modelo

---

## 2. MAPA OFICIAL DE PASTAS

A seguir, cada pasta do repositório, seu propósito, o que contém e como funciona.

### 2.1 Raiz do Projeto (`/`)
Contém apenas arquivos de configuração e documentação. Nunca binários soltos nem CSVs/TXTs avulsos.

| Arquivo | Propósito |
|---------|-----------|
| `AGENTS.md` | Diretrizes obrigatórias de desenvolvimento (Regras de Ouro) |
| `memoria_TACE.md` | Histórico cronológico de todas as alterações (rastreabilidade) |
| `README.md` | Visão resumida e instruções de build |
| `DOCUMENTACAO_COMPLETA.md` | Este arquivo |
| `.gitignore` | Regras de exclusão do Git (binários, dados, logs, segredos) |
| `compile_commands.json` | Banco de compilação para o LSP `clangd` (IntelliSense C) |
| `.clangd` | Configuração do language server C |
| `.vscode/` | Configurações do VS Code (C/C++ properties, settings) |
| `.agents/` | Prompts de sistema de agentes de assistência (.NET Architect, C# Senior) |

### 2.2 `src/` — Código-fonte principal

Toda a lógica de execução reside aqui, dividida em camada C (motor) e camada C# (interface).

#### 2.2.1 `src/main.c`
Ponto de entrada Win32 da DLL (`WinMain`). Minimalista: retorna 0. A DLL não é um executável autônomo; é consumida pelo C# via P/Invoke.

#### 2.2.2 `src/api.c` — API de exportação da DLL
Arquivo central que expõe todas as funções `DllExport` consumidas pelo C#. Inclui:
- Controle global de pausa (`g_engine_paused`) e cancelamento (`g_engine_canceled`)
- Filtro SEH top-level `InnoUnhandledExceptionFilter` (anti-crash, grava `logs/engine_crash.txt`)
- `engine_sleep_check(int ms)` — Sleep interropível por polling de 100ms (responde a ESC/pausa)
- `Engine_RefreshDevices()` — lista dispositivos ADB e registra no SQLite
- `Engine_GetDeviceCount()`, `Engine_GetDevice()`, `Engine_GetRegisteredTotal()`
- `Engine_ForceStopApp()`, `Engine_UninstallApp()`
- `Engine_RunAdbShell()` — execução livre de shell ADB
- `Engine_GetMunicipios()`, `Engine_SaveMunicipios()` — gestão de municípios
- `Engine_CapturarMAC()` — captura de endereço MAC Wi-Fi via ADB
- **InnoAI (BotLearn):** `Engine_Learn_GetTargetCount`, `Engine_Learn_GetStrategy`, `Engine_Learn_GetStrategyCount`, `Engine_Learn_ResetDB`, `Engine_Learn_SetActiveMode`, `Engine_Learn_GetActiveMode`
- **Exportações por pipeline:** `Engine_ExecuteF1_Positivo`, `Engine_ExecuteF1_NokiaT20`, `Engine_ExecuteF2_Reinstall`, `Engine_ExecuteF2_NokiaT20`, `Engine_ExecuteF3_UpdatePackage`, `Engine_ExecuteF3_NokiaT20`, `Engine_ExecuteF4_Terminal`, `Engine_ExecuteF4_Ferramenta`, `Engine_ExecuteF4_Wallpaper`, `Engine_ExecuteF5_Shutdown`, `Engine_ExecuteF6_Sincronizacao`, `Engine_ExecuteF7_TabMulti`, `Engine_ExecuteF8_ActiveConnection`, e variantes TabMulti (F2/F3/F4/F5).

#### 2.2.3 `src/core/` — Motor C (núcleo)

Organizado por camadas funcionais:

##### `src/core/drivers/` — Comunicação com Android (ADB)
- `adb.c` / `adb.h` — Driver de baixo nível:
  - `adb_exec()`: executa comandos ADB de forma **100% invisível** (`CreateProcessW` + `CREATE_NO_WINDOW`), sem flashes de CMD.
  - Captura de saída via pipe anônimo com timeout de 3s e `TerminateProcess` para evitar processos órfãos.
  - `adb_list_devices()`: descoberta de dispositivos conectados via `adb devices -l`, extraindo serial, modelo, fabricante, versão Android, API, bateria, IMEI e **MAC Wi-Fi** (via `dumpsys wifi` com fallback `/sys/class/net/wlan0/address` e `ip link show wlan0`).
  - `adb_dump_stdout()`: captura da árvore XML UIAutomator diretamente na memória RAM (`exec-out uiautomator dump /dev/stdout`), reduzindo dump de ~1.5s para ~100ms, com fallback para arquivo em disco.
  - `adb_get_mac()`: rotina de 3 camadas para extrair MAC de fábrica (ativa Wi-Fi se necessário, extrai, restaura estado original).
  - `adb_force_stop()`, `adb_uninstall_app()`, `adb_ensure_app_running()` (relança o app InnovAva se encerrado por falta de RAM).
- `adb_manager.c` / `adb_manager.h` — Gerenciador de caminho do ADB (`tools/platform-tools/adb.exe`).

##### `src/core/learn/` — InnoAI (Inteligência Visual)
- `learn_db.c` / `learn_db.h` — Banco de estratégias neurais:
  - Parser/writer JSON minimalista (sem dependência externa) para `data/bot_learn.json`.
  - Estrutura `LearnDB` com targets (alvos de UI) e estratégias concorrentes.
  - `g_learn_active_mode`: 0 = Estático (seed fixo, sem persistência, recomendado produção), 1 = Ativo (aprendizado dinâmico, hit +5 / miss -10, backups de 3 dias).
  - `learn_find_element()`, `learn_record_hit()`, `learn_record_miss()`, `learn_save_fallback_coord()`, `learn_get_fallback_coord()`.
  - `learn_db_load()` / `learn_db_save()` — persistência em disco.
- `learn_engine.c` / `learn_engine.h` — Motor de busca:
  - `inno_normalize_char()` — normalização de caracteres (remove diacríticos, case-insensitive).
  - `inno_stristr_fuzzy()` / `f7_stristr_fuzzy()` — busca fuzzy tolerante a acentos e encoding (Windows-1252, CP850, UTF-8).
  - FastCache de coordenadas de fallback para cliques quando XML falha.
  - Dual-Scan: varredura visual + matching fuzzy para localizar elementos.

##### `src/core/data/` — Persistência de dados locais
- `dispositivos.c` / `dispositivos.h` — Tabela `Dispositivos` no SQLite (`bot_database.db`):
  - Schema: `ID`, `Patrimonio`, `Serial` (UNIQUE), `Fabricante`, `Modelo`, `Versao_Android`, `API_Level`, `Bateria`, `IMEI`, `MAC`.
  - Anti-duplicidade: `Serial TEXT UNIQUE`, `INSERT OR IGNORE`, UPSERT por serial.
  - Migração automática: renumeração contígua de IDs (1..N) após exclusões, remoção de coluna `DataRegistro`, adição de `Patrimonio`.
  - `dispositivos_registrar()`, `dispositivos_carregar()`, `RegistrarOuAtualizarMAC()`.
- `municipios.c` / `municipios.h` — Banco de municípios (`municipios.csv`):
  - Formato: `municipio;login;senha;wallpaper_path` (delimitador `;`).
  - Conversão UTF-8 <-> ANSI (`convert_utf8_to_ansi`, `convert_ansi_to_utf8`) para P/Invoke `CharSet.Ansi` sem caracteres estranhos (Ã§, Ã£).
  - Cofre DPAPI: credenciais (`login`/`senha`) cifradas com `CryptProtectData` (escopo `CURRENT_USER`), prefixo `DPAPIv1:`, revelação na leitura (`cofre_revelar`), cifra na gravação.
  - `municipios_get_csv_path()` — resolve caminho dinamicamente (ao lado do exe em Release, `config/` em dev).
- `cofre.c` / `cofre.h` — Cofre de senhas DPAPI:
  - `CryptProtectData` com entropia fixa da aplicação, Base64 próprio, `SecureZeroMemory` no material sensível.
  - Idempotente, falhas nunca destroem dados, legado em claro segue legível e migra no próximo save.
- `paths.c` / `paths.h` — Resolução de caminhos:
  - `paths_get_data_dir()` — localiza diretório de dados (portável ao lado do exe, ou `%ProgramData%`).
  - Busca recursiva ascendente por marcadores de raiz (`tools` e `config`).

##### `src/core/config/` — Parser de configurações JSON
- `config.c` / `config.h` — Leitura de `config/settings.json`:
  - `get_config_path()` resolve caminho em Release e dev.
  - Campos: `motor_c_enabled`, `reverse_tethering_enabled`, `ai_learn_active`, `enable_device_*`, `usb_monitor_interface`, Firebase, cloud sync, telemetria, meta de lote.

##### `src/core/functions/` — Pipelines por modelo (Isolamento Estrito — Regra de Ouro #1)

Cada modelo tem arquivos `.c` e `.h` dedicados e 100% autônomos. **Zero efeito colateral entre modelos.**

###### `vision7/` — Positivo Tab Vision 7 (Android 14 / API 34)
| Arquivo | Função | Escopo |
|---------|--------|--------|
| `f1_vision_tab7.c/.h` | `V7_ExecutarPipelineCompleto` | F1: Instalação completa (14 sub-etapas): wakeup, trava Portrait, captura MAC, Dark Mode, debloat batched de 25+ apps, purga `/sdcard`, push para `/sdcard/avaliacoes/`, túnel Gnirehtet, injeção de permissões, login InnoAI, importação ZIP, limpeza visual de recentes, wallpaper municipal, shutdown físico `reboot -p`. |
| `f2_vision_tab7.c/.h` | `F2_ExecutarPipelineCompleto` | F2: Reinstalação limpa/offline. Mesma esteira do F1, encerra em Modo Avião, mantém tablet ligado. |
| `f3_vision_tab7.c/.h` | `F3_ExecutarAtualizacaoPacote` | F3: Atualização expressa de pacote ZIP (sem alterar logins), tablet permanece ligado. |
| `f4_vision_tab7.c/.h` | `F4_ExecutarFerramenta` | F4: Terminal Kiosk / Gestão do Aparelho (12 ações: câmera, configurações, modo restrito, wallpaper, Gnirehtet, barra de pesquisa, blindagem, restauração + perfis 1-clique). |
| `f5_vision_tab7.c/.h` | `F5_ExecutarDesligamento` | F5: Desligamento seguro (para VPN, desativa Wi-Fi/BT/GPS, limpeza visual de recentes, `reboot -p`). |
| `f6_vision_tab7.c/.h` | `F6_ExecutarSincronizacao` | F6: Sincronização direta nuvem (Gnirehtet + botão 'SINCRONIZAR AGORA'), desliga ao final. |
| `f8_vision_tab7.c/.h` | `V7_Etapa2_5` | F8: Conexão ativa contínua via Gnirehtet, sem desligamento. |
| `pipeline.h` | — | Header compartilhado do Vision 7. |

###### `tabmulti/` — Multilaser TabMulti M7
| Arquivo | Função | Escopo |
|---------|--------|--------|
| `f1_tabmulti_m7.c/.h` | `M7_ExecutarPipeline` | F1/F7: Pipeline calibrado para hardware lento (2GB RAM), temporizações estendidas, fechamento duplo de teclado virtual (`keyevent 4`), rolagem ampliada de municípios (8 swipes). |
| `f2_tabmulti_m7.c/.h` | `M7_AtualizarPacote` | F2: Atualização de pacote TabMulti. |
| `f3_tabmulti_m7.c/.h` | `M7_ExecutarPipelineF3_AtualizarPacote` | F3: Atualização expressa TabMulti. |
| `f4_tabmulti_m7.c/.h` | `M7_DesligarAparelho` | F4/F5: Desligamento TabMulti (túnel Gnirehtet, Wi-Fi, Bluetooth, GPS, `reboot -p`). |
| `f5_tabmulti_m7.c/.h` | `M7_ExecutarPipelineF5` | F5: Login + túnel 30s + shutdown físico. |
| `m7_helpers.c/.h` | Helpers M7 | Funções auxiliares: `f7_get_centroid_fuzzy`, `f7_get_password_centroid`, `f7_selecionar_municipio_m7`, blindagem nula de ponteiros. |
| `pipeline.h` | — | Header do TabMulti. |

###### `nokia_t20/` — Nokia T20 (Android 11)
| Arquivo | Função | Escopo |
|---------|--------|--------|
| `f1_nokia_t20.c/.h` | `T20_ExecutarPipelineCompleto` | F1: Play Protect bypass, single ADB pipe, smart UI polling, silenciamento de notificações, Gnirehtet VPN bypass, purga `/sdcard` preservando `Android`, `Pictures`, `innovava`. |
| `f2_nokia_t20.c/.h` | `T20_ExecutarPipelineF2` | F2: Reinstalação preservando apps do sistema, omite etapa 1.5 e 7, mantém ligado ao final. |
| `f3_nokia_t20.c/.h` | `T20_ExecutarPipelineF3_AtualizarPacote` | F3: Atualização expressa Nokia T20. |
| *outros* | — | Seleção de ZIP multi-camada (DPAD_DOWN + DPAD_CENTER), pausa de 5s pós-ZIP, encerramento sem desligamento. |

#### 2.2.4 `src/ui/` — Interface C# WPF (.NET 8)

Projeto `TACEUI.csproj`, publicado em modo Self-Contained Offline (win-x64, single file).

##### `src/ui/App.xaml` / `App.xaml.cs`
- Ciclo de vida da aplicação.
- `OnExit`: encerra silenciosamente o servidor ADB (`adb kill-server`).
- `CheckForOtaUpdatesOnStartupAsync()`: verifica OTA obrigatório no boot (`OtaMandatoryUpdateWindow` se `mandatory==true`).
- Timer de telemetria no cabeçalho (`Engine_GetDeviceCount()` a cada 2.5s).

##### `src/ui/Core/` — Camada de negócio C#
| Arquivo | Propósito |
|---------|-----------|
| `SettingsManager.cs` | Carrega/salva `config/settings.json` (`InnoAppSettings`). Detecta tipo de hardware (`DeviceFilterHelper.DetectType`): Vision7, TabMultiM7, NokiaT20, Unknown. Gerencia `UsbMonitorInterface` (Classic/Pro), Firebase credentials, cloud sync, telemetria, meta de lote. |
| `SQLiteDatabaseManager.cs` | Gerencia `bot_database.db`: `RegistrarSeNovo`, `LerTodos`, `BuscarPorSerial`, `AtualizarPatrimonio`, `Excluir`, `GarantirEsquemaDispositivos` (migração automática), sync com InnovStock Firebase (`BaixarDoInnovStockAsync`, `EnviarLocaisFaltantesParaNuvemAsync`), `SincronizarComInnovStockAsync` (PATCH para preservar nuvem). |
| `OtaUpdateManager.cs` | Atualização remota: `CheckForUpdatesAsync` (GET anônimo em `ota/manifest.json`), `SendTelemetryPingAsync`, `ApplyUpdate` (executa `scripts/apply_ota_update.bat`), validação SHA-256, download assíncrono com progresso. |
| `FirebaseAuthService.cs` | Login Identity Toolkit + refresh SecureToken para Firebase (token apenas em memória, retry em 401). |
| `MacCaptureService.cs` | Captura automática de MAC Wi-Fi ao conectar tablet: `GarantirMacRegistrado()` chama `Engine_CapturarMAC()` via DllImport, grava no SQLite (UPSERT), retry espaçado em 5min por serial. |
| `CloudAutoSyncService.cs` | Sync automático periódico (timer `cloud_sync_minutes`, default 15min), gatilho de rede, trava `Interlocked`, gate offline. |
| `AntiCrashGuard.cs` | Triplo handler de exceções (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) + purgador de memória ativo. |
| `BotLearnBackupManager.cs` | Backups automáticos do BotLearn (rotatividade de 3 dias, até 3 arquivos). |
| `PathHelper.cs` | Helpers de caminho: `GetRootDirectory()`, `RemoveAccents`, `SanitizeFolderName`, `GetDynamicApkPath`, `GetDynamicPacotePath`, `DeleteMunicipioFolders`. |
| `PreflightChecker.cs` | Verifica presença de binários ADB no primeiro acesso; faz download automático do ZIP oficial do Google Platform-Tools se ausente. |

##### `src/ui/Views/` — Telas e janelas WPF
| Arquivo | Propósito |
|---------|-----------|
| `MainWindow.xaml/.cs` | Shell principal. Navegação entre módulos, header com telemetria, OTA boot, seleção de interface Monitor (Classic/Pro), atalhos globais (ESC duplo, F1-F8, F11 tela cheia). |
| `MonitorView.xaml/.cs` | Cockpit padrão. Lista de dispositivos conectados (Modelo, Serial, MAC Wi-Fi), pílulas de status ao vivo, console de logs, botões de ação rápida F1-F8, terminal Kiosk, automação ao conectar, botão Parar total, limpar logs seletivo. |
| `MonitorProView.xaml/.cs` | Layout Studio de alta densidade para hubs USB (12-16 portas). KPIs em tempo real (tabs/hora, tempo médio por tablet, ETA), cards de bancada interativos, execução em lote (`⚡ EXECUTAR EM TODOS`), desligar concluídos, meta de lote editável, console com filtros semânticos e exportador. |
| `BotLearnView.xaml/.cs` | Matriz de conhecimento do InnoAI. Abas por perfil de hardware (Todos, V7, M7, T20, Geral), agrupamento por etapas 1-7, scoring, hits/misses, taxa de precisão, botão reset (ativo apenas no modo IA Ativo). |
| `BrainVisualizerWindow.xaml/.cs` | Rede Neural 3D (Holo-Sphere). Constelação esférica com projeção em perspectiva (D=1000), nós por hemisfério de hardware (V7 esmeralda, M7 âmbar, T20 azul, Geral lilás), sinapses dinâmicas, rotação 360° com mouse, zoom, painel holográfico de inspeção lateral, filtros de tags, busca viva, HUD de estatísticas. |
| `ConfigView.xaml/.cs` | Configurações do sistema: motor C, reverse tethering, modo IA (Estático/Ativo), dispositivos habilitados, interface USB monitor, conta InnovStock (email/senha + testar conexão), telemetria, OTA, meta de lote. |
| `RelatoriosView.xaml/.cs` | Central de relatórios: CRUD de dispositivos (Novo/Editar/Excluir), DataGrid editável, exportação PDF (FlowDocument, PdfSharpCore), CSV, etiquetas patrimoniais com QR Code (QRCoder), dashboard KPIs, download InnovStock. |
| `MunicipiosWindow.xaml/.cs` | Gestão de pacotes por município: seleção de APK/ZIP/Wallpaper, cópia assíncrona (stream buffers 1MB, `IProgress<double>`), ProgressBar, cópia mestre para `Arquivos/<Municipio>` e diretório de execução. |
| `AboutView.xaml/.cs` | Tela "Sobre": 3 abas (Guia de Módulos, FAQ, Arquitetura & Licenciamento). |
| `WelcomeWindow.xaml/.cs` | Tela de boas-vindas com cards de módulos. |
| `OtaMandatoryUpdateWindow.xaml/.cs` | Modal de atualização obrigatória (download, validação SHA-256, reinicialização). |

##### `src/ui/Assets/Icons/` — Ícones da aplicação
- `TACEui_Test_DarkBlue.ico/.png` — ícone de desenvolvimento (modo TESTE)
- `TACEui_Release_Purple.ico/.png` — ícone oficial roxo (modo RELEASE/Release PnP)

##### `src/ui/Properties/` — `AssemblyInfo.cs` (metadados da assembly)

---

## 3. OUTRAS PASTAS DO PROJETO

### 3.1 `config/` — Configurações JSON
Contém estritamente arquivos de configuração (não é versionado totalmente; `settings.json` é gitignorado por conter credenciais).

| Arquivo | Propósito |
|---------|-----------|
| `settings.json` | Configurações globais: fontes, motor C, reverse tethering, modo IA, dispositivos habilitados, Firebase (URL, API key, email, password), cloud sync, telemetria, meta de lote. |
| `first_run.json` | Configuração de primeira execução. |
| `version.txt` | Versão oficial do sistema (formato `DD.MM.YYYY.HHMM`), lida pelo OTA. |
| `municipios.csv` | Seed versionado de municípios (apenas nomes, sem credenciais em claro). |

### 3.2 `tools/` — Utilitários externos e executáveis
Concentra 100% dos executáveis e utilitários externos (não versionados no Git, exceto scripts leves).

| Subpasta | Propósito |
|----------|-----------|
| `platform-tools/` | ADB oficial do Google (adb.exe, dlls, etc.) — provisionado por `PreflightChecker`/`setup_adb.bat`. |
| `reverse_tethering/` | Gnirehtet (ponte de internet PC -> Android): `.exe`, `.apk`, `.cmd`, scripts. |
| `mac_address_finder.apk` | APK auxiliar de captura de MAC. |
| `mingw64/` | Toolchain GCC MinGW64 (somente desenvolvimento, NÃO vai para o Release). |
| `capturamac/` | Ferramenta autônoma em C + CMD para captura de MAC Wi-Fi (fonte versionada, saídas ignoradas). |

### 3.3 `scripts/scripts de controle/` — Ferramentas de administração unificadas
| Arquivo | Propósito |
|---------|-----------|
| `ControleGeral.bat` | Script único de controle: `[1]` Build TESTE (GCC + dotnet, `data/output/`), `[2]` Build RELEASE (pacote PnP `TACE_Release/`), `[3]` Build INSTALADOR (Inno Setup), `[4]` Dependências (GCC + .NET 8), `[5]` Download ADB, `[6]` Monitor de tela, `[7]` Ferramentas Dev, `[8]` Ajuda. Modo direto: `ControleGeral.bat 1..8` ou `build \| release \| installer \| deps \| adb \| monitor \| tools`. Mostra status de GCC/dotnet e versão no menu. |
| `TACE.iss` / `InstallerAssets/` | Script e assets (ícones do wizard, termo de uso) exigidos fisicamente pelo Inno Setup na opção `[3]`. |

### 3.4 `scripts/scripts de teste/` — Ferramentas de teste operacional
| Arquivo | Propósito |
|---------|-----------|
| `monitor_tela.bat` | Monitor contínuo de tela via ADB (XML + screenshots). |
| `gravar_fluxo_manual.ps1` | Gravação de fluxo manual passo a passo (PowerShell). |
| `analise_xml.ps1` | Análise de dumps XML. |
| `show_zip_node.ps1` | Exibe nós clicáveis de um dump XML. |

### 3.5 `data/` — Dados de execução e bancos locais (gitignorado)
| Subpasta/Arquivo | Propósito |
|------------------|-----------|
| `output/` | Binários de dev/teste: `BotEngine.dll`, `TACEui.exe`. |
| `bot_database.db` | Banco SQLite de dispositivos (433 registros, IDs contíguos 1..433). |
| `dispositivos_registrados.csv` | Exportação CSV de dispositivos (`ID;Patrimonio;Serial;...;IMEI;MAC`). |
| `relatorio_serial_mac.csv` | Relatório Serial-MAC (`Serial;Patrimonio;MAC;Fabricante;Modelo`). |
| `inno_database.db` | Banco legado preservado. |
| `backups/` | Backups de bancos. |
| `temp_ota/` | Temp de atualizações OTA. |
| `arquivos/` | Diretório de pacotes APK/ZIP/Wallpaper por município (runtime). |

### 3.6 `docs/` — Documentação técnica
| Arquivo | Propósito |
|---------|-----------|
| `plano_melhorias.md` | Fases e checkboxes do plano de melhorias (6 fases). |
| `relatorio_auditoria.html` | Relatório de auditoria autocontido (HTML/PDF/DOCX offline). |
| `auditoria_TACE_20260925.txt` | Relatório de auditoria em texto. |
| `ota_rpa_02.md` | Regras e histórico do protocolo OTA Firebase. |
| `relatorio_funcoes_positivo_tab_vision_7.md` | Relatório detalhado F1-F8 do Vision 7. |
| `relatorio_funcoes_multilaser_tabmulti_m7.md` | Relatório detalhado F1-F4 do TabMulti M7. |
| `relatorio_funcoes_nokia_t20.md` | Relatório detalhado F1-F3 do Nokia T20. |

### 3.7 `docs/registros/` — Logs de auditoria de build (Regra de Ouro #2)
Padrão: `docs/registros/YYYY-MM-DD/HH-MM_Plataforma_TACE_DESKTOP_BUILD`. Cada build gera uma pasta com `build.log` (hashes SHA-256, checklist de conteúdo, schema do banco). Logs de execução de dispositivo (`*_execucao.log`, `*.log`) são ignorados pelo Git; apenas auditorias de build permanecem rastreáveis.

### 3.8 `logs/` — Logs de execução local (gitignorado)
Logs de execução do motor e da UI. Nunca versionados.

### 3.9 `TACE_Release/` — Release portátil PnP (gitignorado)
Pacote de distribuição para o usuário final. Contém: `TACEui.exe`, `BotEngine.dll`, `config/`, `tools/` (platform-tools, reverse_tethering, mac_address_finder.apk), `scripts/apply_ota_update.bat`, `data/` (banco, inputs), `Arquivos/`, `logs/`, `docs/registros/`. **Sem** `tools/mingw64`.

### 3.10 `data/output/` — Binários de dev/teste (gitignorado)
`BotEngine.dll` (397-414KB) e `TACEui.exe` (~185-194MB, self-contained .NET 8).

---

## 4. ARQUITETURA HÍBRIDA E COMUNICAÇÃO

### 4.1 Camada C (Motor — `BotEngine.dll`)
- Compilada com MinGW64 GCC (`gcc -shared ... -static -lcomctl32 -lgdi32 -luser32 -lcrypt32 -mwindows`).
- Gerenciamento manual de memória (Zero-Leak).
- Sem números mágicos.
- Interage diretamente com APIs do SO (Win32) e hardware (ADB).
- Filtro SEH top-level para capturar violações de acesso sem derrubar o `.exe`.
- Sleep reescrito como `engine_sleep_check()` com polling de 100ms, respondendo a pausa/cancelamento global.

### 4.2 Camada C# (Interface — `TACEui.exe`)
- WPF .NET 8, publicado em modo Self-Contained Offline, single-file.
- Design System: Glassmorphism Cyber-Industrial, paleta corporativa (Roxo primário `#513BA6`, fundo neutro escuro `#404C4F`).
- Consome a DLL C via `[DllImport]` com `CallingConvention.Cdecl`.
- Strings trafegam com `[MarshalAs(UnmanagedType.LPUTF8Str)]` para preservar acentuação (UTF-8).
- Manterá lógicas pesadas no C e renderização fluida no C#.

### 4.3 Fluxo de execução de um pipeline
1. C# chama `Engine_ExecuteF*_...(serial, apk_path, pacote_path, ..., LogCallback cb)`.
2. Motor C executa etapas sequenciais via ADB (`adb_exec` com `CREATE_NO_WINDOW`).
3. Entre etapas, `engine_sleep_check()` ou polling XML (`adb_dump_stdout`) aguarda condições de tela.
4. Callbacks `cb()` reportam progresso ao console/log da UI em tempo real.
5. Ao final, `PIPELINE_SUCCESS (1)` ou `PIPELINE_ERROR (0)` é retornado.

---

## 5. PIPELINES (F1 A F8)

| Função | Modelo | Exportação C | Estado Final do Tablet |
|--------|--------|--------------|------------------------|
| F1: Instalação Completa | Vision 7 | `Engine_ExecuteF1_Positivo` | Desligado (`reboot -p`) |
| F1: Instalação Completa | Nokia T20 | `Engine_ExecuteF1_NokiaT20` | Ligado na tela inicial |
| F2: Reinstalação Limpa | Vision 7 | `Engine_ExecuteF2_Reinstall` | Ligado (Modo Avião/Offline) |
| F2: Reinstalação | Nokia T20 | `Engine_ExecuteF2_NokiaT20` | Ligado |
| F3: Atualização Expressa ZIP | Vision 7 | `Engine_ExecuteF3_UpdatePackage` | Ligado (importação concluída) |
| F3: Atualização Expressa ZIP | Nokia T20 | `Engine_ExecuteF3_NokiaT20` | Ligado |
| F3: Atualização Expressa ZIP | TabMulti M7 | `Engine_ExecuteF3_TabMulti_Atualizar` | — |
| F4: Gestão do Aparelho (Kiosk) | Vision 7 | `Engine_ExecuteF4_Terminal` / `Ferramenta` / `Wallpaper` | Mantém estado |
| F4: Terminal / Desligar | TabMulti M7 | `Engine_ExecuteF4_TabMulti_Desligar` | — |
| F5: Desligamento Remoto | Vision 7 | `Engine_ExecuteF5_Shutdown` | Desligado (`reboot -p`) |
| F5: Desligamento | TabMulti M7 | `Engine_ExecuteF5_TabMulti` | Desligado (`reboot -p`) |
| F6: Sincronização Nuvem | Vision 7 | `Engine_ExecuteF6_Sincronizacao` | Desligado (`reboot -p`) |
| F7: Tab-MULTI | TabMulti M7 | `Engine_ExecuteF7_TabMulti` | — |
| F8: Conexão Ativa | Vision 7 | `Engine_ExecuteF8_ActiveConnection` | Conectado (ligado) |

**Etapas canônicas (F1 Master Benchmark):** 0 → 1.1 → 1.2 → 1.5 → 1.6 → 1.7 → 1.9 → 2.0 → 2.1 → 2.5 → 3.0 → 4.0 → 4.5 → 5.0 → 6.0 → 7a → 7.1 → 7b. Modelos omitem etapas não aplicáveis sem alterar a ordem.

---

## 6. INNOAI / BOTLEARN (Inteligência Visual)

- Motor determinístico de localização de elementos via UIAutomator XML dump.
- Busca fuzzy (`inno_normalize_char`, `inno_stristr_fuzzy`) para lidar com diacríticos e encoding.
- Scoring de -100 a +100: hit incrementa, miss reduz.
- FastCache de coordenadas de fallback.
- Modo Estático (padrão): seed fixo, save no-op, zero drift, recomendado para produção.
- Modo Ativo: persiste `data/bot_learn.json`, aprendizado dinâmico, backups de 3 dias.
- Interfaces: `BotLearnView` (matriz por abas e etapas) e `BrainVisualizerWindow` (Rede Neural 3D).

---

## 7. BUILD E DEVOPS

### 7.1 Scripts de build
- `scripts/scripts de controle/ControleGeral.bat 1` — Modo TESTE (ícone azul escuro), gera `data/output/`.
- `scripts/scripts de controle/ControleGeral.bat 2` — Modo RELEASE (ícone roxo), gera `TACE_Release/`.
- `scripts/scripts de controle/ControleGeral.bat 3` — Build INSTALADOR (Inno Setup, gera setup.exe a partir de `TACE_Release/`).

### 7.2 Política de build (Regra de Ouro #4)
- Ao término de edições em C ou C#, é **obrigatório** rodar `scripts/scripts de controle/ControleGeral.bat 1`.
- Resultado exigido: **Zero Errors e Zero Warnings**.
- Versão gerada: `DD.MM.YYYY.HHMM` em `config/version.txt`.
- Registro de auditoria em `docs/registros/YYYY-MM-DD/HH-MM_Plataforma_TACE_DESKTOP_BUILD`.

### 7.3 Diretriz de build
> Conforme registrado em `memoria_TACE.md` (2026-09-03 22:00): **Não executar nenhum comando de compilação ou script de build a menos que o usuário dê a ordem explícita.**

---

## 8. REGRAS DE OURO (AGENTS.md)

1. **Checklist inicial:** Ler `memoria_TACE.md` e confirmar contexto antes de qualquer modificação.
2. **Isolamento estrito por equipamento/modelo:** Alterações em um modelo (ex: F1 Vision 7) devem ocorrer **estritamente** no arquivo correspondente (`f1_vision_tab7.c`), com **zero efeito colateral** nos demais modelos.
3. **Rastreabilidade:** Toda alteração deve ser registrada em `memoria_TACE.md` em ordem cronológica. Logs de build em `docs/registros/`. Commits em Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `style:`).
4. **Organização limpa de pastas:** Zero poluição na raiz. Binários em `data/output/` ou `TACE_Release/`. Configurações em `config/`. Ferramentas em `tools/`.
5. **Política de Zero Warning & Build Obrigatório:** `scripts/scripts de controle/ControleGeral.bat 1` após edições em C/C#.

---

## 9. SEGURANÇA E SEGREDOS

- `config/settings.json` é **gitignorado** (contém `firebase_api_key` em texto plano — pendente de rotação).
- Cofre DPAPI para credenciais de municípios (escopo `CURRENT_USER`, não portável entre máquinas/usuários).
- `config/municipios.csv` seed público sem credenciais (senha `bm@12345` segue no histórico do Git — rotação pendente).
- Firebase: leitura pública só do manifesto OTA (`ota/manifest`); escrita restrita a admin.
- Telemetria: opt-in/opt-out via `telemetry_enabled` em `settings.json`.

---

## 10. ESTADO ATUAL DO PROJETO

- Última versão: `21.10.2026.0826` (`config/version.txt`)
- Último build FINAL: `20.10.2026.1843` (Release PnP, SHA-256 disponível em `docs/registros/2026-10-20/18-43_Plataforma_TACE_DESKTOP_BUILD/`)
- Banco: 433 dispositivos, IDs contíguos 1..433, coluna `Patrimonio` vazia (a preencher manualmente)
- BotLearn: modo Estático (aprendizado desligado)
- OTA: público só-leitura via `ota/manifest` (precisa de Firebase rules públicas)
- Captura de MAC Wi-Fi: nativa e automática ao conectar tablet (motor C + `MacCaptureService`)
- Isolamento por modelo: implementado e respeitado (Ouro #1)

---

*Documentação gerada com base em `memoria_TACE.md`, `AGENTS.md`, `README.md` e inspeção completa do código-fonte em `src/`.*
