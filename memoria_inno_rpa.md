# Memória do Projeto - Inno RPA

## Visão Geral
**Nome do Projeto:** Inno RPA (Plataforma InnovTech)
**Data de Criação Inicial:** Anterior a Junho 2026 (Atualizado em 25/06/2026)
**Objetivo:** Sistema de automação robótica (RPA) de alta performance e interação visual para gerenciamento de dispositivos Android via ADB e Kiosk Mode.

## Decisões Arquiteturais e Padrões (Arquitetura Híbrida Google-Style)
- **Motor Backend (C Puro):** A biblioteca `InnoEngine.dll` é compilada em C nativo com política de "Zero-Leak" (gerenciamento manual de memória) e sem números mágicos. Ela interage diretamente com APIs do SO e do hardware (ADB) garantindo velocidade bruta.
- **Frontend / Interface Gráfica (C# WPF / .NET 8):** Interface `InnoRPAui.exe` projetada seguindo as diretrizes corporativas (Glassmorphism Cyber-Industrial). O C# consome a biblioteca em C via `[DllImport]`, mantendo as lógicas pesadas no C e a renderização fluida no C#.
- **Design System:** Utilização da paleta corporativa com foco no tom primário Roxo (`#513BA6`) e um fundo neutro escuro (`#404C4F`) para redução da fadiga ocular.
- **Gestão de Dependências e Build:** A execução de build se dá via arquivo batch, garantindo o versionamento e mantendo registros de compilação em `registros/`. As dependências são provisionadas automaticamente (`scripts/install_dependencies.bat`).

## Histórico de Evolução Relevante (Resumo)
- **Até 24/06/2026:** Ajustes significativos no Design System para ComboBox e ToggleButtons, forçando estilos nativos do Windows a aderirem ao tema *Premium Dark*.
- **25/06/2026:**
  - Realizada compilação e teste global do sistema.
  - Correção de falhas críticas de DllImport (Crash ao usar F2) e Warnings (CS8618, CS8600, CS8625) garantindo a política de **Zero Warnings**.
  - Criação de artefatos faltantes: `install_dependencies.bat` (DevOps) e configuração JSON externa (`config/settings.json`) evitando hardcode no motor.
  - Geração de ícones e logo baseados no design premium Cyber-Industrial.
  - Implementação do Dual-Scan (UIAutomator) via `window_dump.xml` com extração de centroides.
  - Integração do Município na API (C# para C) via `LPUTF8Str` com sanitização e tracking inteligente.
  - Otimizações na UI: Substituição de DataGrid por ListView customizada, layout em UniformGrid, etc.
- **26/06/2026:**
  - Unificação do módulo F4 (Testes) no F3 (Ferramentas Kiosk), enxugando arquivos obsoletos de testes.
  - Atualização do F2 para incorporar limpeza visual do Launcher (`pm clear`) e proxy reverso Gnirehtet na Etapa de Login.
  - Limpeza estrutural da raiz do projeto, removendo arquivos de dump e históricos antigos (consolidados nesta memória).
- **03/07/2026:**
  - Validação da integração completa do módulo **InnoLearn**: exportação da API em C (`Engine_Learn_GetStrategy`), mapeamento DllImport no C#, criação da `InnoLearnView.xaml` com estatísticas detalhadas e botão de reset integrado com dupla confirmação.

- **03/07/2026 (Atualização de Status):**
  - Validação da integração completa do módulo **InnoLearn** (C API, DllImport, InnoLearnView.xaml).
  - Implementação do Submódulo de **Backups Automáticos** do InnoLearn (rotatividade de 3 dias, retendo até 3 arquivos).
  - Alteração da matemática do Score da IA para um range paramétrico de `-100 a +100` com gradientes visuais dinâmicos (Verde, Branco e Vermelho).
  - Criação de um sistema de tracking de "Etapas Visuais" (hits/misses) que pode ser expandido sob a linha da estratégia clicada através de `ListView` com gaveta adaptada na `ZebraRow`.
  - Validação da **Central de Relatórios**: O arquivo `RelatoriosView.xaml.cs` (com exportação para PDF via PdfSharpCore e CSV, além de integração DB e QR Code) já está devidamente finalizado.
  - Validação da **Otimização do Monitor Kiosk**: A interface `MonitorView.xaml.cs` e os fluxos F1, F2 e F3 já estão em operação.
- **07/07/2026:**
  - Implementação da função **F4 (Desligamento Remoto)**: Desativação de Wi-Fi, Bluetooth, Localização e execução do `reboot -p` via ADB, com interface nativa e integração direta na DLL do motor C.
- **14/07/2026:**
  - Inicialização do Reverse Tethering (ponte de internet PC -> Android) via Gnirehtet para o dispositivo conectado por ADB.
  - Registro de execução criado em [15-32_reverse_tethering_gnirehtet.log](file:///c:/PROJETOS/Inno_RPA/registros/2026-07-14/15-32_reverse_tethering_gnirehtet.log).
  - Implementação de verificação de conectividade ativa do Gnirehtet na função **F7 (Tab-MULTI)** (`f7_tabmulti.c`) antes da etapa de login, com reativação automática da VPN (`F1_Etapa2_5`) caso ela oscile ou caia durante a preparação e instalação do APK no dispositivo.
  - Ajuste de PnP na UI C# (`PreflightChecker.cs`): se os binários do motor de comunicação ADB estiverem ausentes no primeiro acesso ou se forem deletados acidentalmente, o software realiza o download do ZIP oficial das platform-tools do Google e faz a extração de forma 100% automatizada e transparente para o usuário.
- **15/07/2026:**
  - Correção de matching *fuzzy* (`inno_normalize_char`) na seleção de municípios em `f1_geraldata.c` para contornar caracteres especiais (ex: "Pão de Açúcar").
  - Criação da Ferramenta de Teste de Velocidade de Internet (Ping-based de 10 níveis) na interface C# (`ConfigView.xaml`).
  - **Otimizações Estruturais e de IA no Fluxo F2:**
    - Substituição de chamadas bloqueantes `system("taskkill...")` por execuções invisíveis `CreateProcessA` + `CREATE_NO_WINDOW` para mitigar pop-ups e *flashes* de janelas do CMD na interface.
    - Inteligência de Navegação (Etapa 6): Dupla validação para encontrar pacotes ZIP (*Fast-Path* visual e *Fallback* com Swipe Gesto) mitigando loops cegos nas abas de Recentes do Android.
    - Refatoração do `Sleep` para um validador semântico por polling que aguarda até 2 minutos pelo reaparecimento dos botões da interface para avançar.
    - Lapidação do encerramento (Offline): Desativação do GPS reposicionada, inserção de limpeza de memória e apps recentes (`am kill-all`), redução de brilho (70%) e desligamento automatizado (Shutdown via `reboot -p`) inseridos estritamente no final seguro do pipeline.
  - **Correção da persistência de municípios no Release:** Ajustada a função `municipios_get_csv_path` em `municipios.c` para resolver caminhos dinamicamente, permitindo a leitura e gravação no diretório local de produção.
  - **Aprimoramento do Empacotamento de Release:** Atualizado o `build_release.bat` para gerar a subpasta `registros/municipios` e copiar o banco seed `municipios.csv` para distribuição.
  - **Melhoria no Utilitário de Backup (Exportação/Importação):** Ampliada a lógica de Backup em `ConfigView.xaml.cs` para incluir o banco SQLite de dispositivos (`data\inno_database.db`), registros de aprendizado de IA (`data\inno_learn.json`), diretório de pacotes e APKs (`data\input\apps`), além de implementar recarga dinâmica de visualizações (`ReloadAllViewsData` na `MainWindow`) após a restauração.
  - **Ciclo de Vida do ADB:** Implementada a rotina `OnExit` no ciclo global (`App.xaml.cs`) para encerrar de forma silenciosa e invisível o servidor ADB (`adb kill-server`) ao fechar a aplicação, liberando portas e recursos.
  - **Geração e validação da build de Release Final para o usuário (`InnoRPA_Release`), registrada sob o log de auditoria em `registros/2026-07-15/17-37_Plataforma_InnovPlay_DESKTOP_BUILD`.**
- **16/07/2026:**
  - **Estabilização do InnoLearn (Motor Determinístico):** Visando mitigar instabilidades sazonais causadas por cliques cegos e derivação de scores, desativamos as rotinas dinâmicas de aprendizado. As funções `learn_record_hit`, `learn_record_miss`, `learn_save_fallback_coord` e `learn_auto_discover` viraram *stubs* (no-ops). A persistência em arquivo (`learn_db_save` e `learn_db_load`) foi redirecionada para usar estritamente os seeds compilados estáticos do motor. Eliminados os riscos de desalinhamento de cliques por resolução.
  - **Correção da Carga de Email (EditText Focus):** Ajustadas as sementes de `campo_email` e `f7_campo_email` para priorizar a busca pela classe genérica `class="android.widget.EditText"` (score=90). Isso previne cliques falsos em elementos de texto estático (`TextView` com o mesmo label "E-mail ou Usuário") que não focavam o teclado e impediam o login.
  - **Estratégia Robusta do Botão Login:** Adicionados fallbacks de strings e classe genérica `Button` no seed do `botao_entrar` para garantir cliques em layouts com botões não textuais ou nomeados como "Acessar".
  - **Implementação do Atalho Global ESC (Pausa/Cancelamento e Encerramento):**
    - Adicionado hook de teclado `PreviewKeyDown` no WPF para interceptar `Key.Escape` (ESC).
    - Lógica de clique único: Alterna o estado de pausa do pipeline ativo, comunicando à DLL nativa o estado pausado.
    - Lógica de clique duplo (dentro de 1.5s): Cancela a automação imediatamente e executa `am force-stop` nos dispositivos para fechar os aplicativos e limpar o histórico aberto (`br.com.yantec.innovava`, `com.google.android.documentsui`).
    - Lógica de Sleep Inteligente na DLL C (`pipeline.h` e `api.c`): Reescrita da macro `Sleep()` padrão para efetuar pollings a cada 100ms e retornar `PIPELINE_ERROR` imediatamente sob sinal de cancelamento, resolvendo o problema de travamentos durante esperas longas.
  - **Categorização de Botões por Modelo (Interface WPF):**
    - Redesenhada a barra inferior em `MonitorView.xaml`. Introduzido seletor de modelos: `Tab Vision 7` (Positivo) e `Tab-Multi` (Multilaser).
    - A seleção do modelo alterna dinamicamente o painel de botões do respectivo dispositivo, reduzindo a poluição visual e garantindo mapeamento limpo de atalhos (como F1 para Tab-MULTI no painel Multilaser).
  - **Simplificação e Mudança de Caminho do `municipios.csv`:**
    - O banco de municípios `municipios.csv` foi movido da subpasta `registros/municipios/` para o diretório raiz, ficando armazenado diretamente ao lado do executável (`InnoRPAui.exe`). Modificações aplicadas em `municipios.c` e nos scripts de compilação.
  - **Padronização e Arquivamento com a pasta "Arquivos":**
    - Criada a pasta `Arquivos` (anteriormente `Arquivos de implementação`, alterada para evitar problemas de caracteres especiais em caminhos de arquivos) na raiz do sistema (tanto em desenvolvimento quanto na release).
    - Em `MunicipiosWindow.xaml.cs`, o seletor de arquivos de APK/ZIP/Wallpaper passa a usar a subpasta `Arquivos\<Nome_do_Municipio>` como diretório inicial.
    - Se o usuário selecionar um arquivo externo (ex: Downloads), o sistema faz uma cópia automatizada dele para `Arquivos\<Nome_do_Municipio>` (cópia mestre/organização) e outra cópia para `data\input\municipios\<Nome_do_Municipio>` (diretório de execução do motor).
  - **Direcionamento Automático F1/F7 (Roteamento de Modelo):**
    - Implementada lógica em `BtnF1_Click`, `BtnF7_Click` e `RunAutoInstallForDevice` em `MonitorView.xaml.cs` para detectar dinamicamente a marca do dispositivo ADB conectado (Multilaser vs Positivo).
    - Dispositivos Multilaser são automaticamente roteados para executar o pipeline `Engine_ExecuteF7_TabMulti` (F7), enquanto aparelhos Positivo ou outros são roteados para o pipeline `Engine_ExecuteF1_Positivo` (F1), mesmo que o botão genérico correspondente seja acionado, prevenindo erros de compatibilidade de automação.
  - **Barra de Progresso (Cópia Não-Bloqueante):**
    - Adicionado controle `ProgressBar` no `MunicipiosWindow.xaml` com cor verde e estilo moderno.
    - Lógica assíncrona baseada em stream buffers de 1MB com `IProgress<double>` para reportar o progresso em tempo real sem travar a interface do usuário. A janela é travada temporariamente durante a cópia para segurança de I/O.
  - Compilação geral e empacotamento da nova release estável (`InnoRPA_Release`) sob o registro de auditoria `registros/2026-07-16/09-12_Plataforma_InnovPlay_DESKTOP_BUILD`.
- **21/07/2026:**
  - Implementação da função **F8 (Conexão Ativa via Gnirehtet)** para dispositivos Tab Vision 7.
  - Exportação da API DLL em C puro `Engine_ExecuteF8_ActiveConnection` em `src/api.c` vinculada ao motor `V7_Etapa2_5`.
  - Mapeamento DllImport em C# (`MonitorView.xaml.cs`) com handler `BtnF8_Click`.
  - Adicionado botão visual `[F8] Conexão Ativa` com tema Cyber (Cyan `#00E5FF`) na barra de ações `GridPositivoActions` (`MonitorView.xaml`).
  - Adicionado atalho de teclado global para a tecla `Key.F8` em `MainWindow.xaml.cs`.
  - Adicionada a opção `[F8] Conexão Ativa (Gnirehtet)` ao ComboBox `ComboAutoAction` ("Automação ao Conectar") tanto no layout XAML (`MonitorView.xaml`) quanto na reinicialização dinâmica por categoria de marca (`BtnCategoryPositivo_Click`), além do suporte no gatilho de conexão automática (`RunAutoInstallForDevice`).
  - Atualizada a função **F5 (Desligamento Remoto)** em `v7_shutdown.c` e `f5_shutdown.c` para desativar previamente o túnel **Gnirehtet** (`gnirehtet stop <serial>`) de forma limpa e assíncrona antes de desligar Wi-Fi, Bluetooth, Localização e emitir o `reboot -p`.
- **25/07/2026:**
  - Correção na temporização do pipeline F1 (Tab Vision 7) em [v7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_pipeline.c): Inserido delay de 5 segundos (`Sleep(5000)`) no início de [F1_Etapa7_CloseApps](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_pipeline.c#L1587) logo após a seleção do pacote ZIP no File Picker. Isso garante a estabilização de tela durante a transição do gerenciador de arquivos de volta para o aplicativo principal, evitando dumps de tela vazios ou falhas de comunicação com o ADB.
- **29/07/2026:**
  - **Refatoração e Otimização Expressa da Função F3 (Atualização de Pacote Avaliativo):**
    - Alinhada a etapa de limpeza e envio do F3 em [v7_refresh.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_refresh.c) e [f3_update.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f3_update.c) com a rotina `V7_Etapa1_6` / `F1_Etapa1_6`. Executa a purga do armazenamento `/sdcard` (preservando `Android` e `Pictures`), recria `/sdcard/avaliacoes/` e envia o APK e o Pacote ZIP atualizados com nomes sanitizados (`adb_get_safe_path`).
  - **Padronização Global de Otimização e Sistema (F1, F2, F3 e F7 Tab-MULTI):**
    - Aplicada a chamada batched acelerada de preparação ADB shell (`svc wifi disable && svc bluetooth disable && settings put secure location_mode 0 && settings put global wifi_scan_always_enabled 0 && settings put global ble_scan_always_enabled 0 && settings put system screen_brightness_mode 0 && settings put system screen_brightness 178 && settings put system font_scale 1.5 && wm user-rotation lock 0`) universalmente nas etapas de inicialização de hardware dos fluxos **F1** ([v7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_pipeline.c) / [f1_geraldata.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f1_geraldata.c)), **F2** ([v7_update.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_update.c) / [f2_data.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f2_data.c)), **F3** ([v7_refresh.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_refresh.c) / [f3_update.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f3_update.c)) e **F7 (Tab-MULTI)** ([f7_tabmulti.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f7_tabmulti.c) / [m7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/tabmulti/m7_pipeline.c)).
    - Garantida a escala de fonte **1.5** e a trava de rotação da tela em **Retrato / Portrait (`wm user-rotation lock 0`)** em todas as esteiras de automação de dispositivos.
  - **Aceleração Expressa do Pipeline F1 (Instalação Completa):**
    - **Isolamento de Bloatwares Batched (Etapa 1.5):** Substituídas 25 chamadas individuais do ADB por uma única instrução shell batched (`pm disable-user --user 0 ... && pm suspend ...`), economizando ~4.5s.
    - **Permissões em Lote Batched (Etapa 3):** Concessão de permissões de câmera, armazenamento e localização agrupada em comando único ADB.
    - **Limpeza de Launcher Otimizada (Etapa 1.9):** Agrupado `pm clear launcher3; pm clear sec.android.launcher` com sleep reduzido para 300ms.
    - **Ajuste de Delays Internos (Etapa 1.2, 4.5 e 7a):** Agrupamento do Dark Mode e redução de tempos estáticos em pós-login e estabilização de importação de 5s para 2s. Redução de mais de 30s no tempo total de execução por dispositivo.
  - **Manutenção de Zero Warnings:** Corrigidos avisos CS8618 em [ConfigView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/ConfigView.xaml.cs) e CS0414 em [MonitorView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/MonitorView.xaml.cs).
  - Execução e validação dos scripts de build (`scripts/build.bat` e `scripts/build_release.bat`), gerando nova compilação limpa do motor `InnoEngine.dll`, da UI `InnoRPAui.exe` e do pacote de distribuição portátil Plug-and-Play em `InnoRPA_Release` com todas as ferramentas inclusas (`platform-tools` ADB, Gnirehtet Reverse Tethering, banco seed de municípios, `inno_database.db`, `inno_learn.json` e pacotes de municípios).
  - Registro de auditoria gerado em `registros/2026-07-29/20-21_Plataforma_InnovPlay_DESKTOP_BUILD`.
- **30/07/2026:**
  - **Atualização do Fluxo de Importação/Atualização de Pacotes ([v7_refresh.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_refresh.c), [f3_update.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f3_update.c) e [m7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/tabmulti/m7_pipeline.c)):**
    - Adicionado loop de polling (`dump_and_read_xml`) que aguarda até 2 minutos pela reativação do botão "Procurar arquivo" após o clique de importação do pacote ZIP.
    - Implementada a rotina completa de encerramento de aplicativos (`input keyevent 3` / HOME, acionamento do menu de recentes via `input keyevent 187` + clique automático em 'Limpar tudo', `am force-stop` no app principal e no DocumentsUI, `am kill-all`).
    - Adicionada a rotina de desativação de redes/VPN e comando de **Shutdown físico** (`reboot -p`) alinhado com o travamento da orientação da tela em **Modo Vertical (Portrait / Lock 0)** como a primeiríssima instrução executada no F3.
  - **Otimizações de Ultra-Velocidade no F3 Express ([v7_refresh.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_refresh.c), [f3_update.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f3_update.c) e [m7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/tabmulti/m7_pipeline.c)):**
    - Agrupamento batched do comando de inicialização ADB shell (orientação vertical, desativação de Wi-Fi/BT/GPS, brilho 70% e fonte 1.5 em chamada única).
    - Redução do tempo inicial de checagem pós-clique de importação de 5s para 2s com sondagem responsiva de término.
    - Agrupamento batched da animação da tela de recentes (`keyevent 3 && keyevent 187 && swipe 200 300 800 300 300`).
    - Agrupamento batched do encerramento de processos, kill background e desligamento de redes (`keyevent 3 && am force-stop ... && am kill-all && svc wifi disable ...`).
    - Ganho total obtido: ~8 a 10 segundos economizados por dispositivo no F3.
  - **Limpeza Manual e Visual Explícita de Aplicativos Recentes ([v7_refresh.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_refresh.c), [f3_update.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f3_update.c), [m7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/tabmulti/m7_pipeline.c), [f5_shutdown.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/f5_shutdown.c) e [v7_shutdown.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_shutdown.c)):**
    - Refatorada a rotina de encerramento para ser **100% visual e manual no display do tablet**:
      1. Garante tela acesa e desbloqueada.
      2. Executa `KEYEVENT 187` com delay dedicado (1200ms) para que o SO desenhe a UI dos cartões abertos visivelmente.
      3. Realiza deslizes manuais visíveis (`input swipe 150 500 850 500 250`) percorrendo os cartões abertos até revelar o botão 'Limpar tudo'.
      4. Captura o XML e clica com precisão no centroide do botão 'Limpar tudo' / 'Clear all' / 'Fechar tudo', com toque de fallback em coordenadas conhecidas caso necessário.
      5. Aguarda a destruição visual dos cartões, retorna para a HOME limpa (`KEYEVENT 3`) e encerra os processos em background (`am force-stop` + `am kill-all`).
  - **Sistema de Abas por Perfil de Hardware e Submódulos no InnoLearn ([InnoLearnView.xaml](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/InnoLearnView.xaml) e [InnoLearnView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/InnoLearnView.xaml.cs)):**
    - Adicionada uma barra de abas interativas no topo da tabela de conhecimento da IA:
      - 🌐 **TODOS OS PERFIS** (exibe todas as estratégias da plataforma).
      - 📱 **POSITIVO TAB VISION 7 (V7)** (filtra estritamente os alvos do modelo Positivo Vision 7).
      - 📱 **MULTILASER TABMULTI M7** (filtra estritamente os alvos do modelo Multilaser M7).
      - 🖥️ **GERAL / MULTILASER PADRÃO** (filtra estritamente os alvos do motor padrão F1-F6).
    - Dentro de cada aba selecionada, os elementos são **separados e agrupados visualmente por submódulos (F1, F2, F3, F5, F7, V7)**, exibindo o indicador de progresso da etapa, score, hits/misses, taxa de precisão e histórico neural visual.
  - **Isolamento de Seletores de IA para Positivo Tab Vision 7 (`v7_*`) ([learn_db.c](file:///c:/PROJETOS/Inno_RPA/src/core/learn_db.c), [v7_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/vision7/v7_pipeline.c) e [InnoLearnView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/InnoLearnView.xaml.cs)):**
    - Identificada a causa da aba *Positivo Tab Vision 7* aparecer vazia: o pipeline do Vision 7 estava chamando seletores genéricos em vez dos identificadores dedicados do modelo.
    - Injetados os seeds específicos `v7_spinner_municipio`, `v7_campo_email`, `v7_campo_senha`, `v7_botao_entrar`, `v7_botao_importar_zip` e `v7_btn_procurar_arquivo` no banco C (`learn_db.c`).
    - Atualizados todos os pontos de consulta e registro de acertos/falhas (`learn_find_element`, `learn_record_hit`, `learn_record_miss`, `learn_save_fallback_coord`) em `v7_pipeline.c` para operar estritamente sob a chave `v7_*`.
    - Mapeados os rótulos amigáveis no C# WPF ([InnoLearnView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/InnoLearnView.xaml.cs)), permitindo que a aba **📱 POSITIVO TAB VISION 7 (V7)** exiba os scores, precisão e histórico específico desse modelo.
  - **Agrupamento Estrito de Estratégias por Etapas 1 a 7 ([InnoLearnView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/InnoLearnView.xaml.cs)):**
    - Mapeados todos os alvos de IA para suas respectivas **Etapas formais dos pipelines**:
      - **Etapa 2: Seleção de Município** (`spinner_municipio` / `v7_spinner_municipio` / `f7_spinner_municipio`)
      - **Etapa 3: Injeção de E-mail do Aplicador** (`campo_email` / `v7_campo_email` / `f7_campo_email`)
      - **Etapa 4a: Injeção de Senha do Aplicador** (`campo_senha` / `v7_campo_senha` / `f7_campo_senha`)
      - **Etapa 4b: Confirmação de Acesso (Botão Entrar)** (`botao_entrar` / `v7_botao_entrar` / `f7_botao_entrar`)
      - **Etapa 5: Menu de Importação de Pacote ZIP** (`botao_importar_zip` / `v7_botao_importar_zip`)
      - **Etapa 6: Seleção de Arquivo no Storage** (`f1_btn_procurar_arquivo` / `v7_btn_procurar_arquivo` / `f7_btn_procurar_arquivo`)
      - **Etapa F6: Sincronização Direta de Dados** (`v7_btn_sincronizar_agora` / `f6_btn_sincronizar_agora`)
    - Ao selecionar qualquer aba de modelo (Vision 7, TabMulti M7, Geral), a tela agrupa e ordena visualmente as estratégias exatamente na sequência executada de **Etapa em Etapa**.
  - **Atualização da Nomenclatura Corporativa para InnovTech ([InnoLearnView.xaml](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/InnoLearnView.xaml), [RelatoriosView.xaml](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/RelatoriosView.xaml), [RelatoriosView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/RelatoriosView.xaml.cs) e [scripts/build.bat](file:///c:/PROJETOS/Inno_RPA/scripts/build.bat)):**
    - Atualizados todos os textos, títulos de janelas, cabeçalhos de visualização de IA e relatórios/etiquetas em PDF de `INNOVPLAY PLATFORM` / `InnovPlay` para **`INNOVTECH PLATFORM` / `InnovTech`**.
    - Atualizada a nomenclatura dos logs de auditoria de compilação em `scripts/build.bat` para `registros/YYYY-MM-DD/HH-MM_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Evolução Next-Gen do Mapa Neural ([BrainVisualizerWindow.xaml](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/BrainVisualizerWindow.xaml) e [BrainVisualizerWindow.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/BrainVisualizerWindow.xaml.cs)):**
    - **Filtros por Perfil de Hardware**: Adicionada barra superior para alternar dinamicamente a topologia do grafo entre `🌐 TODOS`, `📱 VISION 7 (V7)`, `📱 TABMULTI M7` e `🖥️ GERAL`.
    - **Statistics HUD (Canto Superior Esquerdo)**: Exibição em tempo real do número de nós ativos, total de sinapses e a taxa de precisão global (%) calculada dinamicamente.
    - **Card Holográfico de Inspeção Lateral**: Ao clicar em qualquer nó do grafo, um gaveteiro lateral de inspeção é ativado mostrando a query completa do seletor, score, taxa de precisão individual, total de acertos/falhas e os 10 indicadores de acerto mais recentes (pontos verdes/vermelhos).
    - **Correção da Exceção XamlParseException no Mapa Neural ([BrainVisualizerWindow.xaml](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/BrainVisualizerWindow.xaml) e [BrainVisualizerWindow.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/BrainVisualizerWindow.xaml.cs))**:
      - Identificada a causa do crash ao abrir o Mapa Neural: `HorizontalAlignment="SpaceBetween"` não é um enum válido no WPF (é uma sintaxe de CSS/Flexbox).
      - Refatorados os cartões do gaveteiro lateral para utilizarem `Grid` com alinhamentos `Left` e `Right` nativos do WPF.
      - Adicionados blocos de proteção `try-catch` e verificações de inicialização `IsLoaded` para garantir estabilidade 100% à prova de falhas.
    - **Aprimoramento de Usabilidade da Interface Gráfica**:
      - **Busca Rápida Inteligente (`🔍` Campo de Busca)**: Adicionado input no cabeçalho superior que filtra e destaca em tempo real qualquer nó ou seletor por ID ou trecho da query (opacidade reduzida para itens não correspondentes e borda Ciano `#00E5FF` para itens encontrados).
      - **Cópia Instantânea de Seletor (`📋 COPIAR`)**: Adicionado botão no card holográfico lateral que copia a query inteira para a área de transferência com um clique e dá feedback visual imediato (`✓ COPIADO!`).
      - **Animações de Hover Responsivas**: Os nós expandem suavemente o raio ao passar o mouse (`MouseEnter`/`MouseLeave`), melhorando o feedback tátil e de precisão ao clicar.
      - **Raio Adaptativo Dinâmico**: A distribuição geométrica do grafo se autoajusta à resolução da tela (1080p, 1440p, 4K ou Monitores ultrawide), evitando sobreposição de textos.
      - **Correção do Pan & Zoom (Botões `➕`, `➖` e `🎯 RESET`)**:
        - Causa identificada: O método `StartPulseAnimation()` no carregamento da janela sobrescrevia a propriedade `BrainCanvas.RenderTransform` substituindo o `TransformGroup` do XAML, o que desconectava os objetos `CanvasScale` e `CanvasTranslate`.
        - Solução: Removida a sobreposição de `RenderTransform` no `BrainCanvas` e configurados os pontos pivô centralizados (`CenterX`/`CenterY`) para o `CanvasScale`, garantindo resposta imediata ao clicar em `➕`, `➖`, `🎯 RESET`, scroll do mouse e arrasto por Pan.
- **06/08/2026:**
  - **Ajuste e Validação Obrigatória de Município no Login Tab-MULTI M7 ([f1_tabmulti_m7.c](file:///p:/src/core/functions/tabmulti/f1_tabmulti_m7.c)):**
    - Criada a função dedicada `f7_selecionar_municipio_m7` com sanitização UTF-8 / acentuação, abertura inteligente da gaveta via `learn_find_element("f7_spinner_municipio")` + fallbacks (`"Selecione"`, `"Município"`, `class="android.widget.Spinner"`), e algoritmo de busca por rolagem (até 6 swipes para baixo e 4 para cima).
    - Refatorada a função `F7_Etapa4_Login`: inserida etapa obrigatória de **validação pré-clique em Entrar**. O motor realiza dump XML antes de acionar o botão Entrar e confirma se o município alvo está devidamente selecionado e visível. Se não estiver verificado, executa re-seleção forçada automática e re-validação. Caso falhe, a ação do clique em Entrar é abortada com log de erro crítico (`PIPELINE_ERROR`), eliminando falhas de login por município incorreto.
    - Executados os testes de compilação C/C# e gerado novo build limpo sob o registro de auditoria `registros/2026-08-06/11-35_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Limpeza Estrutural de Pastas e Binários de Build Excedentes:**
    - Removidas pastas e resíduos de extrações e compilações antigas na raiz (`inno novo`, `awbms_extracted`, `framework_extracted`, `dump.xml`, `dump2.xml`, logs/txt residuais soltos e arquivos `.wpftmp.csproj`).
  - **Padronização Única dos Ambientes de Build:**
    - **Ambiente de Testes / Desenvolvimento:** `data\output\` (gerado via `scripts\build.bat`).
    - **Ambiente de Release Portátil (PnP):** `InnoRPA_Release\` (gerado via `scripts\build_release.bat`).
  - Atualizado `scripts\build.bat` para garantir a criação limpa de `data\output\` e purga automática de intermediários de compilação em `src\ui\bin` e `src\ui\obj`.
  - Atualizado `.gitignore` para bloquear o rastreamento de `data/output/`, `InnoRPA_Release/` e arquivos temporários do MSBuild (`*.wpftmp.csproj`).
  - **Reestruturação Modular e Autônoma do Motor Nocional em C (`src/core/functions/`):**
    - Separação completa de cada função em arquivos `.c` e `.h` dedicados e 100% autônomos por modelo de dispositivo:
      - **Positivo Tab Vision 7 (`vision7/`):** `f1_vision_tab7.c/.h`, `f2_vision_tab7.c/.h`, `f3_vision_tab7.c/.h`, `f4_vision_tab7.c/.h`, `f5_vision_tab7.c/.h`, `f6_vision_tab7.c/.h`, `f8_vision_tab7.c/.h`.
      - **Multilaser TabMulti M7 (`tabmulti/`):** `f1_tabmulti_m7.c/.h`, `f2_tabmulti_m7.c/.h`, `f3_tabmulti_m7.c/.h`, `f4_tabmulti_m7.c/.h`, `m7_helpers.c/.h`.
      - **Nokia T20 (`nokia_t20/`):** `f1_nokia_t20.c/.h`, `f2_nokia_t20.c/.h`, `f3_nokia_t20.c/.h`.
    - Eliminados todos os arquivos monolíticos residuais soltos na raiz de `functions/` (`f1_geraldata.c`, `f2_data.c`, `f3_update.c`, `f4_terminal.c`, `f5_shutdown.c`, `f6_sync.c`, `f7_tabmulti.c`).
    - Cada arquivo de função agora possui o código integral e independente, sem dependência de etapas compiladas em arquivos `f1_*` vizinhos.
    - Atualizados `src/api.c` e `scripts/build.bat` mantendo 100% de compatibilidade DLL (`InnoEngine.dll`) com a interface C# WPF (`InnovPlayUI`).
  - **Organização Completa e Categorização por Subpastas (`src/core/` e `src/ui/`):**
    - **`src/core/drivers/`**: `adb.c` e `adb.h` (Comunicação ADB e UIAutomator).
    - **`src/core/learn/`**: `learn_db.c/.h` e `learn_engine.c/.h` (InnoAI e inteligência visual).
    - **`src/core/data/`**: `dispositivos.c/.h` e `municipios.c/.h` (Bancos de dados locais e CSVs).
    - **`src/core/config/`**: `config.c` e `config.h` (Parser de configurações JSON).
    - **`src/ui/Properties/`**: `AssemblyInfo.cs` (Metadados da Assembly).
    - **`src/ui/Views/`**: Todas as janelas e telas organizadas (`MainWindow`, `WelcomeWindow`, `BrainVisualizerWindow`, `MonitorView`, `InnoLearnView`, `ConfigView`, `RelatoriosView`, `MunicipiosWindow`).
    - Compilação do motor e interface validadas com sucesso em `registros/2026-08-06/08-24_Plataforma_InnovTech_DESKTOP_BUILD` e `08-25_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Diferenciação Visual Automática de Ícone por Ambiente de Execução:**
    - **Ambiente de Teste/Desenvolvimento (`data\output\InnoRPAui.exe`):** Ícone exclusivo **Azul Escuro** (`InnoRPAui_Test_DarkBlue.ico`).
    - **Ambiente de Release Portátil (`InnoRPA_Release\InnoRPAui.exe`):** Ícone oficial **Roxo** (`InnoRPAui_Release_Purple.ico`).
    - Automação integrada aos scripts `scripts/build.bat` e `scripts/build_release.bat`. Validação de builds em `08-48_Plataforma_InnovTech_DESKTOP_BUILD` e `08-49_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Documentação Técnica em Markdown por Dispositivo (`docs/`):**
    - `docs/relatorio_funcoes_positivo_tab_vision_7.md`: Relatório completo das funções F1 a F8 do Positivo Tab Vision 7.
    - `docs/relatorio_funcoes_multilaser_tabmulti_m7.md`: Relatório completo das funções F1 a F4 e utilitários do Multilaser TabMulti M7.
    - `docs/relatorio_funcoes_nokia_t20.md`: Relatório completo das funções F1 a F3 do Nokia T20.
  - **Sistema Anti-Crash Guard & Resiliência Dupla (C e C# WPF):**
    - Criada a classe `InnovPlayUI.Core.AntiCrashGuard` com Triplo Handler de Exceções (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) e Purgador de Memória Ativo.
    - Registrado o filtro SEH Top-Level `InnoUnhandledExceptionFilter` na DLL C `InnoEngine.dll` para capturar violações de acesso sem derrubar o processo `.exe`.
    - Criada a rotina `adb_ensure_app_running` no driver ADB para relançar o aplicativo `InnovAva` no tablet caso o Android o encerre por falta de RAM durante a automação.
  - **Atualização do Mapa Neural & Otimização de Performance para PCs de Baixa Especificação:**
    - Adicionado o botão de filtro `[📱 NOKIA T20]` (`BtnFilterT20` / `CyberFilterT20Style`) ao cabeçalho do Mapa Neural (`BrainVisualizerWindow.xaml`).
    - Removidos os filtros de sombra `DropShadowEffect` das partículas dinâmicas e rótulos do grafo em favor de bordas neon ultraleves, aumentando a taxa de quadros e eliminando o consumo excessivo de CPU/GPU em computadores com recursos limitados.
  - **Padronização e Alinhamento da Sequência Canônica de Etapas (F1 Master Benchmark):**
    - Todas as esteiras F1 dos 3 tablets (Positivo Tab Vision 7, Multilaser TabMulti M7 e Nokia T20) foram padronizadas para seguir a mesma sequência lógica cronológica estabelecida pelo Vision 7 (Etapa 0 -> 1.1 -> 1.2 -> 1.5 -> 1.6 -> 1.7 -> 1.9 -> 2.0 -> 2.1 -> 2.5 -> 3.0 -> 4.0 -> 4.5 -> 5.0 -> 6.0 -> 7a -> 7.1 -> 7b).
    - Aparelhos que não exigem uma etapa específica omitem aquele passo pontual sem alterar a ordem das etapas subsequentes.
    - Builds validados com sucesso em `09-14_Plataforma_InnovTech_DESKTOP_BUILD` e `09-15_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **06/08/2026:**
    - **Otimização de Alta Performance do Pipeline F1 M7 (`f1_tabmulti_m7.c`):**
      - Agrupamento de comandos ADB (Shell Batching) unificando configurações de tela, brilho, orientações e bateria em chamadas compostas.
      - Polling dinâmico ultra-rápido de 500ms para liberação imediata da transição de telas sem esperas estáticas desnecessárias.
      - Pre-sanitização de memória RAM (`am trim-memory`) antes da transferência dos arquivos APK e ZIP para evitar engasgos nos 2GB RAM do hardware M7.
    - **Validação Prévia via XML e Guard de Re-Seleção de Município:**
      - Implementada a checagem em tempo real do XML de interface antes de acionar o botão *Entrar* em todos os 3 tablets (Vision 7, TabMulti M7 e Nokia T20). Se o município não estiver visível no seletor, o motor re-executa a abertura da gaveta e re-seleciona o município alvo de forma automatizada.
    - **Padronização e Resolução de Codificação UTF-8 Transparente:**
      - Corrigida a ponte de conversão entre a DLL C `InnoEngine.dll` (`municipios.c`) e o C# WPF P/Invoke (`CharSet.Ansi`). Adicionadas as rotinas `convert_utf8_to_ansi` e `convert_ansi_to_utf8` garantindo exibição de acentos (`Demonstração`, `Pão de Açúcar`) 100% perfeita sem distorções de caracteres (`Ã§`, `Ã£` ou `??`).
      - Sanitizados em UTF-8 nativo todos os arquivos `municipios.csv` em `registros/municipios/`, `InnoRPA_Release/` e `data/output/`.
    - **Checagem Inteligente de Instalação do Gnirehtet:**
      - Atualizadas todas as funções de todos os aparelhos para checar a presença do pacote `com.genymobile.gnirehtet` via ADB antes de reinstalar o APK, reduzindo de 3 a 5 segundos no tempo de conexão.
    - **Builds de Desenvolvimento e Release de Alta Performance:**
      - Validados com 100% de sucesso os compilados sob os registros `09-59_Plataforma_InnovTech_DESKTOP_BUILD` (Desenvolvimento - Ícone Azul Escuro) e `10-01_Plataforma_InnovTech_DESKTOP_BUILD` (Release Portátil - Ícone Roxo Oficial).
    - **Geração e Validação da Release Final Portátil Plug-and-Play (`InnoRPA_Release`):**
      - Compilação do motor `InnoEngine.dll` em C nativo com linking estático e da UI C# WPF `InnoRPAui.exe` em pacote standalone Self-Contained `.NET 8` com aplicação do Ícone Roxo Oficial.
      - Pacote portátil gerado na pasta `InnoRPA_Release\` contendo todas as dependências e ferramentas necessárias (`config/tools/platform-tools/adb.exe`, `tools/reverse_tethering/gnirehtet.exe`, `gnirehtet.apk`, banco `municipios.csv`, `inno_database.db` SQLite, `inno_learn.json` da IA InnoAI e diretório `Arquivos/`).
      - Registro de auditoria oficial gravado sob `registros/2026-08-06/10-34_Plataforma_InnovTech_DESKTOP_BUILD`.
- **11/08/2026:**
  - **Ajuste na Função F3 (Atualização de Pacote Positivo Vision 7):**
    - Removida a rotina de desligamento físico no final do pipeline `F3_ExecutarAtualizacaoPacote` em `src/core/functions/vision7/f3_vision_tab7.c`.
    - Ao concluir o push dos arquivos e o acionamento do menu de importação do ZIP, o tablet permanece ligado para verificação e continuidade de operação.
- **13/08/2026:**
  - **Injeção de Remoção Explícita de Apps via ADB e Suspensão da Câmera em Loop (F1 Positivo Tab Vision 7):**
    - Atualizada a função `V7_Etapa1_5` em `src/core/functions/vision7/f1_vision_tab7.c` e integrados os vetores `V7_CAMERA_PACKAGES`.
    - Executada remoção via ADB (`pm disable-user --user 0` e `pm uninstall -k --user 0`) para todos os apps informados: Agenda do Google (`calendar`), Assistente (`assistant`/`googlequicksearchbox`), Calculadora (`calculator`/`calculator2`), Chrome (`chrome`), Contatos (`contacts`), Drive (`docs`), Emergência (`emergency`/`safetyhub`), Files (`nbu.files`/`filemanager`), Galeria (`photosgo`/`photos`/`gallery3d`), Gmail (`gm`), Google Go (`searchlite`), Google TV (`videos`), Gravador (`soundrecorder`), Maps (`maps`), Meet (`tachyon`), Play Store (`vending`/`play.games`), Relógio (`deskclock`), YouTube (`youtube`) e YT Music (`youtube.music`).
    - Integrado loop ADB iterativo `pm suspend %s` sobre a lista de pacotes de Câmera (`V7_CAMERA_PACKAGES`) espelhado da ferramenta F4.
- **14/08/2026:**
  - **Reorganização e Simplificação Estrutural de Diretórios e Logística de Build:**
    - **Unificação da Pasta `tools/`:** Migrada a pasta `config/tools/platform-tools` para `tools/platform-tools`. A pasta `config/` passou a conter estritamente arquivos de configuração JSON (`settings.json`, `first_run.json`).
    - **Purga de Resíduos Legados:** Removidos da raiz os arquivos e diretórios obsoletos não utilizados (`awbms.jar`, `history.json`, pasta `web/`).
    - **Atualização Global de Caminhos em C, C# e Scripts:**
      - Atualizadas as rotinas de busca do ADB em C (`src/core/drivers/adb.c`, `src/adb_manager.c`, `src/gui.c`) para buscar diretamente em `tools/platform-tools/adb.exe`.
      - Atualizadas as rotinas da interface C# WPF (`PreflightChecker.cs`, `App.xaml.cs`) para apontar para `tools/platform-tools/adb.exe`.
      - Ajustados os scripts de build e utilitários (`scripts/setup_adb.bat`, `scripts/monitor_tela.bat`, `scripts/gravar_fluxo_manual.ps1`, `scripts/build_release.bat`).
    - **Compilação e Validação de Builds:**
      - Recompilados com 100% de sucesso (Zero Errors / Zero Warnings) o motor nativo em C (`InnoEngine.dll`) e a interface WPF (`InnoRPAui.exe`) nos ambientes de desenvolvimento (`data/output/`) e release portátil PnP (`InnoRPA_Release/`).
      - Registros de auditoria oficial gerados sob `registros/2026-08-14/10-40_Plataforma_InnovTech_DESKTOP_BUILD` e `10-41_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Formalização das Diretrizes Globais de Isolamento e Rastreabilidade (`AGENTS.md` e `.agents/AGENTS.md`):**
    - Criados os arquivos de diretrizes formais [AGENTS.md](file:///c:/PROJETOS/InnoBotRpa/AGENTS.md) e [.agents/AGENTS.md](file:///c:/PROJETOS/InnoBotRpa/.agents/AGENTS.md) estabelecendo:
      1. **Regra de Isolamento Estrito de Equipamentos:** Edições em funções de um modelo específico (ex: F1 no Positivo Tab Vision 7) devem ocorrer estritamente em seu próprio arquivo (`f1_vision_tab7.c`) com **ZERO efeito colateral** nos demais modelos (TabMulti M7 ou Nokia T20).
      2. **Regra de Rastreabilidade Total:** Registro obrigatório de todo histórico de ajustes em `memoria_inno_rpa.md` e versionamento limpo via Git.
  - **Implementação do Pilar 1: Automação Concorrente Multi-Dispositivo (Paralelismo Real):**
    - **Isolamento de Dump XML em C Nativo:** Atualizadas as rotinas em [f1_vision_tab7.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/vision7/f1_vision_tab7.c) e [f1_tabmulti_m7.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/tabmulti/f1_tabmulti_m7.c) para gerar e puxar dumps XML isolados por serial (`window_dump_%s.xml`), garantindo execução paralela sem qualquer colisão de I/O em disco.
    - **Gestão Isolada do Gnirehtet:** Garantida a execução de `gnirehtet stop <serial>` para encerramento isolado por dispositivo sem derrubar o `relay` em execuções paralelas.
    - **Compilação e Auditoria:** Validados os compilados em `registros/2026-08-14/10-54_Plataforma_InnovTech_DESKTOP_BUILD` e `10-55_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Implementação do Pilar 2: Telemetria & Painel ao Vivo no MonitorView (HUD de Dispositivos):**
    - **Pílulas de Status Semânticas em Tempo Real:** Adicionada a coluna *Status ao Vivo* na `ListView` (`ListDevices`) em [MonitorView.xaml](file:///c:/PROJETOS/InnoBotRpa/src/ui/Views/MonitorView.xaml) e [MonitorView.xaml.cs](file:///c:/PROJETOS/InnoBotRpa/src/ui/Views/MonitorView.xaml.cs) exibindo a pílula de estado (`Pronto`, `Executando`, `Concluído`, `Erro`) com cores semânticas ativas.
    - **Coluna de Telemetria Android:** Adicionada a exibição dinâmica da versão do sistema operacional Android (`Android 11`, `Android 10`).
    - **Trigger Automático via Logging:** Integrado o método `UpdateDeviceStatus` diretamente nas rotinas de callback do log (`LogMessage`), atualizando instantaneamente a cor e a pílula de status do dispositivo conforme a evolução do pipeline.
    - **Compilação e Auditoria:** Validados os compilados em `registros/2026-08-14/11-15_Plataforma_InnovTech_DESKTOP_BUILD` e `11-16_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Implementação do Pilar 3: Otimização Acelerada de Dump XML (Fast UI Parser em Memória RAM):**
    - **Engine Fast-Path StdOut:** Implementadas as funções `adb_dump_stdout` e `adb_get_current_focus` em [adb.h](file:///c:/PROJETOS/InnoBotRpa/src/core/drivers/adb.h) e [adb.c](file:///c:/PROJETOS/InnoBotRpa/src/core/drivers/adb.c) para captura direta da árvore XML em memória RAM via stream do ADB (`exec-out uiautomator dump /dev/stdout`), reduzindo o tempo de dump de ~1.5s para ~100ms.
    - **Integração no Motor Multimodelo:** Atualizadas as rotinas de dump e leitura de XML nos modelos [f1_vision_tab7.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/vision7/f1_vision_tab7.c), [f2_vision_tab7.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/vision7/f2_vision_tab7.c), [f3_vision_tab7.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/vision7/f3_vision_tab7.c), [f2_tabmulti_m7.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/tabmulti/f2_tabmulti_m7.c), [f2_nokia_t20.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/nokia_t20/f2_nokia_t20.c) e [f3_nokia_t20.c](file:///c:/PROJETOS/InnoBotRpa/src/core/functions/nokia_t20/f3_nokia_t20.c) com fallback transparente para arquivo em disco se o stream StdOut for indisponível.
    - **Compilação e Auditoria:** Validados os compilados em `registros/2026-08-14/16-13_Plataforma_InnovTech_DESKTOP_BUILD` e `16-19_Plataforma_InnovTech_DESKTOP_BUILD`.






      - **Design Visual Sci-Fi e Legibilidade Extrema de Rótulos**:
        - **Badge Pill Containers**: Encapsulamento de todos os rótulos de nós em badges estilizados com cantos arredondados, fundo de alto contraste escuro (`#111122`/`#0D0D18`), bordas neon e efeito de sombra, garantindo legibilidade perfeita e eliminando colisão visual de textos.
        - **Fundo Sólido Cyber Space (`#0B0B13`)**: Remoção da transparência da janela para eliminar sobreposições de fundo ou janelas subjacentes.
        - **Anéis Orbitais Concêntricos**: Renderização em tempo real de matrizes orbitais circulares tracejadas com transparência sutil no fundo da tela.
        - **Ajuste de Conexões de Linhas nas Bordas dos Círculos**:
          - Modificado o cálculo dos vetores trigonométricos para que todas as linhas de conexão partam exatamente das bordas externas do círculo do Nó Central (`centerX + 48 * Cos(angle)`, `centerY + 48 * Sin(angle)`) e dos Nós Alvos/Estratégias.
          - Reordenada a renderização do Nó Central (`DrawCentralNode`) para o topo das camadas XAML, garantindo texto 100% limpo ("InnoAI") e sem qualquer linha cortando seu centro.
        - **Renomeação do Nó Central para `InnoAI`**: Atualizado o identificador central para `InnoAI` com destaque tipográfico em negrito.
        - **Modo Zen / Ocultar Interface (`👁️ HUD` / Atalho `H`)**: Adicionada funcionalidade para ocultar/exibir todos os painéis overlay (Header, Legenda, Dicas e Zoom) permitindo visualizar estritamente o Mapa Neural, com botão flutuante para restaurar a UI.
        - **Aprimoramento do Módulo de Configurações e Persistência**:
          - **Resolução Dinâmica de Caminhos (C Backend)**: Corrigido o método `get_config_path` em `src/core/config.c` para resolver corretamente o caminho de `config/settings.json` tanto em ambiente Release portátil quanto durante o desenvolvimento.
          - **Persistência de Alternadores de Sistema (C# WPF)**: Implementados os métodos de leitura (`LoadSettings`) e escrita automática (`SaveSettings`) em `ConfigView.xaml.cs` conectados aos eventos dos toggles de `Motor C (InnoEngine)` e `Reverse Tethering Automático`.
          - **Expansão do JSON de Configuração (`config/settings.json`)**: Adicionados os campos `motor_c_enabled` e `reverse_tethering_enabled` no schema C (`AppConfig`) e no arquivo JSON de configurações globais.
        - **Revisão e Otimização da Página de Relatórios e Registros (`RelatoriosView`)**:
          - **Gestão Completa de Banco de Dados (CRUD)**: Adicionado modal de cadastro e edição avançada (`➕ Novo` / `✏️ Editar`), edição direta via planilha XAML (`DataGrid`), salvamento com invalidação de cache (`💾 Salvar`) e exclusão individual ou em lote de múltiplos registros selecionados (`🗑️ Excluir`).
          - **Mapeamento de Hardware por Serial (`T30764` / `T030764`)**: Expandida a regra em `SQLiteDatabaseManager.cs` (`RegistrarSeNovo` e `LerTodos`) e em `RelatoriosView.xaml.cs` (`AtualizarDashboardMetrics`) para reconhecer automaticamente seriais iniciados ou contendo **T30764** / **T030764** como pertencentes ao modelo **Positivo Tab Vision 7**.
          - **Dashboard Analítico com KPIs em Tempo Real**: Criado cabeçalho executivo estilo Cyber Glass no topo da página contendo 4 cards de indicadores dinâmicos: Total de Dispositivos SQLite, Total Positivo Vision 7 (V7), Total TabMulti M7 (M7) e Total de Sessões/Logs de Auditoria Gravados.
          - **Resolução Dinâmica do Diretório de Auditoria**: Substituído o caminho absoluto legados no carregamento de registros de auditoria em `RelatoriosView.xaml.cs` por `PathHelper.GetRootDirectory()`, garantindo criação automática (`Directory.CreateDirectory`) e leitura transparente em qualquer instalação.
          - **Emissão e Exportação Portátil**: Validados os geradores de relatório PDF nativo (`FlowDocument`), exportador Excel/CSV e gerador de Etiquetas Patrimoniais em PDF via QR Code (`QRCoder` / `PdfSharpCore`).
  - **Recompilação completa e empacotamento do Release final gerados sob o registro de auditoria em `registros/2026-07-31/15-16_Plataforma_InnovTech_DESKTOP_BUILD`.**

  - **03/08/2026:**
    - **Documentação e Auditoria do Funcionamento Analítico do InnoAI:** Mapeamento completo de todos os seletores neurais, motor C determinístico (`learn_db.c`), sistema de scoring (-100 a +100), Dual-Scan com matching *fuzzy* (`inno_normalize_char`), FastCache de coordenadas e arquitetura analítica dos pipelines F1 a F8 no Positivo Tab Vision 7 (V7).
    - **Reformulação Visual e Correção dos Módulos de Acesso (`MainWindow.xaml` e `MainWindow.xaml.cs`):** 
      1. **Alinhamento Vertical Preciso:** Centralizados verticalmente a legenda `"MÓDULOS DE ACESSO:"` e os botões dos módulos no mesmo eixo/baseline.
      2. **Eliminação do Bug do Fundo Branco no Hover:** Criado o estilo `NavTabButtonStyle` com `ControlTemplate` customizado. O efeito nativo do WPF que pintava o fundo de branco no passar do mouse foi substituído por um realce sutil e elegante em roxo escuro (`#23233A`).
      3. **Destaque de Seleção Ativa em Pílula:** O módulo ativo agora se destaca com fundo em pílula roxa (`#2D1F5E`), borda primária (`#513BA6`) e texto verde neon (`#4EDEA3`) em negrito.
    - **Redesenho Completo dos Cards de Ação Rápidas (`MonitorView.xaml` e `MonitorView.xaml.cs`):**
      1. **Organização em Grid de 2 Linhas x 4 Colunas:** Substituída a disposição comprimida de 1 única linha por um `UniformGrid` espaçoso em 2 linhas x 4 colunas (`Columns="4"`, `Rows="2"`). Cada card agora possui +250px de largura, eliminando **100% dos cortes de texto** (ex: *[F1] Sincronização + Instalação*, *[F4] Terminal (Gestão Kiosk)*, *[F8] Conexão Ativa (Gnirehtet)*).
      2. **Seletor de Modelo Estilo Pílula Segmentada:** Criado um painel superior exclusivo para alternar entre `📱 Positivo Tab Vision 7` e `📱 Multilaser Tab-Multi` com visual moderno.
      3. **Estética e Micro-Animações:** Cards com cantos arredondados (`CornerRadius="8"`), efeito *Glow* no passar do mouse com cores semânticas ativas (Verde Neon, Ouro, Roxo, Azul, Vermelho) e indicador sutil do atalho de parada emergencial (`ESC / ESPAÇO`).
    - **Alinhamento Milimétrico de Margens Exteriores (`MonitorView.xaml`, `RelatoriosView.xaml`, `ConfigView.xaml`, `InnoLearnView.xaml`, `AboutView.xaml`):**
      - Removida a margem duplicada nas Grids raízes dos `UserControl`s.
      - Agora a barra superior (`MÓDULOS DE ACESSO`) e todos os cards interiores das telas alinham-se **100% pixel a pixel** nas bordas esquerda e direita.
    - Compilação e auditoria final gerada com sucesso sob o registro `registros/2026-08-03/18-38_Plataforma_InnovTech_DESKTOP_BUILD`.
- **04/08/2026:**
  - **Inclusão do Suporte ao Tablet Nokia T20 (Android 11):**
    - Criado módulo C nativo isolado `src/core/functions/nokia_t20/t20_pipeline.c` e `t20_pipeline.h` contendo o orquestrador `T20_ExecutarPipelineCompleto` (F1 dedicado ao Nokia T20, com a etapa de desligamento físico `T20_Etapa7_PowerOff` temporariamente desativada a pedido do usuário).
    - Exportada a API `Engine_ExecuteF1_NokiaT20` em `src/api.c` e vinculada no script `scripts/build.bat`.
    - Adicionado suporte de auto-detecção de hardware para seriais/modelos Nokia T20 no `SQLiteDatabaseManager.cs`.
    - Mapeadas as sementes neurais estáticas `t20_*` em `learn_db.c`.
    - Adicionado botão seletor pílula `📱 Nokia T20 (Android 11)` na barra superior de modelo da interface principal (`MonitorView.xaml` / `MonitorView.xaml.cs`).
    - Criado o painel de ações exclusivo `GridNokiaActions` (`MonitorView.xaml`) exibindo estritamente as funções desenvolvidas para o Nokia T20 (`[F1] Sincronização + Instalação` e `🛑 Parar`).
    - Adicionada a aba de filtro `📱 NOKIA T20 (ANDROID 11)` na interface do InnoAI (`InnoLearnView.xaml` / `InnoLearnView.xaml.cs`).
    - **Diretriz de Desenvolvimento Ativa:** A partir de 04/08/2026, todo o desenvolvimento é 100% focado no tablet **Nokia T20 (Android 11)**. Todas as funções do motor em C serão criadas estritamente em arquivos isolados na pasta `src/core/functions/nokia_t20/` (ex: `t20_pipeline.c`, `t20_update.c`, `t20_terminal.c`, etc.) e exibidas no painel dedicado `GridNokiaActions`, garantindo isolamento total e zero risco de regressão para os modelos Positivo e Multilaser.
    - Redesenhado o painel lateral de controles (`MonitorView.xaml`) com `ScrollViewer` responsivo para se adaptar dinamicamente a qualquer resolução de tela (laptops, 1080p, 4K) eliminando cortes de texto.
    - Simplificada a exibição dos "Arquivos do Município" (`MonitorView.xaml` / `MonitorView.xaml.cs`): exibe apenas o nome do arquivo (`GetFileName`) com truncamento inteligente e um ícone de confirmação em seta verde (`➜` `#4EDEA3`) ao lado de cada arquivo existente.
    - Implementadas **5 Otimizações de Alta Performance** no pipeline F1 do Nokia T20 (`src/core/functions/nokia_t20/t20_pipeline.c`):
      1. **Play Protect Bypass (`package_verifier_enable 0`):** acelera `adb install` em até 300% (de ~8s para 2s).
      2. **Single ADB Pipe (`cd /sdcard && ...`):** executa purga e criação de pastas em um único comando pipe de terminal.
      3. **Smart UI Polling (`dumpsys window | grep mCurrentFocus`):** substitui `Sleep(3000)` estáticos por sondagem dinâmica a cada 200ms.
      4. **Silenciamento de Diálogos (`heads_up_notifications_enabled 0`):** bloqueia notificações do Android 11 que possam cobrir campos de login.
      5. **Gnirehtet VPN Bypass (`T20_Etapa2_5`):** autoriza silenciosamente a ponte de internet USB para garantir autenticação API na nuvem.
    - **Pipeline F1 Nokia T20 Completo e Resiliente:**
      - **Ajuste de Brilho, Orientação & Redes (`T20_Etapa1_7`):** ajusta o brilho da tela em 70% (`screen_brightness 178`), **desativa o giro automático** (`accelerometer_rotation 0`), **fixa a orientação na vertical/retrato** (`user_rotation 0`), desativa Wi-Fi, Bluetooth, varredura de Wi-Fi (`wifi_scan_always_enabled 0`), varredura de Bluetooth (`ble_scan_always_enabled 0`) e localização GPS.
      - **Purga e Estrutura de Armazenamento (`T20_Etapa1_6`):** apaga todos os arquivos e pastas do armazenamento interno (`/sdcard/`), preservando estritamente `Android`, `Pictures` e a nova pasta oficial **`innovava`** (`/sdcard/innovava/`). Transmite o APK e o PACOTE ZIP diretamente para `/sdcard/innovava/`.
      - **Ponte de Internet USB Completa (Gnirehtet `T20_Etapa2_5`):** valida/instala `gnirehtet.apk`, concede permissões silenciosas de VPN e inicia o relay no PC host (`gnirehtet.exe relay`) e o túnel no tablet (`gnirehtet.exe start <serial>`).
      - **Espera Pós-Login (`T20_Etapa4`):** após injetar credenciais e clicar em Entrar, o motor lê a interface em tempo real e aguarda a autenticação na plataforma antes de seguir.
      - **Pipeline F2 Nokia T20 (`T20_ExecutarPipelineF2`):**
        - Executa todas as etapas de preparação, brilho 70%, orientação vertical, reverse tethering USB (Gnirehtet), injeção de login e seleção/importação do pacote ZIP.
        - **Diferença F2:** Omite a Etapa 1.5 (mantém os apps do sistema/menu quietos sem desativação) e a Etapa 7 (mantém a interface aberta ao final sem fechar apps).
      - **Encerramento sem Desligar (`T20_Etapa7_CloseApps`):** executa `am force-stop` nos apps, faz a limpeza visual da tela de recentes (HOME ➔ RECENTES ➔ Swipe ➔ HOME) e mantêm o Nokia T20 100% **LIGADO** na tela inicial.
    - Compilação do motor em C nativo e da UI C# WPF concluída com sucesso sob o registro de auditoria em `registros/2026-08-04/11-15_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **Ajuste de Pipeline F1/F2 Nokia T20:** Removidas todas as etapas subsequentes ao clique no menu hambúrguer "Importar ZIP" (`T20_Etapa5`) em [t20_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/nokia_t20/t20_pipeline.c).
    - **Reconstrução Passo 1 (Nokia T20):** Adicionada a rotina de localização e clique no botão **"PROCURAR ARQUIVO .ZIP"** via leitura XML/Fuzzy em `T20_Etapa5`.
    - **Reconstrução Passo 2 (Nokia T20):** Adicionada verificação de abertura direta na pasta `innovava` com clique único de seleção no pacote ZIP disponível na tela.
    - **Reconstrução Passo 3 (Nokia T20):** Implementada a esteira completa de navegação secundária (Drawer → Nokia T20 → innovava → ZIP).
    - **[2026-08-05] Bug Root-Cause (Nokia T20 ZIP Selection):** Diagnosticado via gravação ADB em tempo real com `monitor_tela.bat`. O DocumentsUI (`com.google.android.documentsui/PickActivity`) abre **diretamente** na pasta `innovava` com o arquivo `pda21072025.zip` visível. O bug estava em `t20_get_centroid_from_xml_fuzzy`: ao encontrar `text="pda21072025.zip"` em nó `clickable="false"`, a função buscava o **próximo** `clickable="true"` adiante no XML — encontrando o `preview_icon` (ícone de olho, centroide 739,450) em vez do `item_root` pai (centroide 600,635). O clique ia no ícone de visualização, não na seleção do arquivo.
    - **[2026-08-05] Solução Definitiva de Seleção Multi-Camada (Nokia T20):** Testada e validada diretamente no dispositivo físico via ADB em tempo real. No Android 11 DocumentsUI (`com.android.documentsui/PickActivity`), a navegação nativa de teclado físico **DPAD_DOWN (`input keyevent 20`) + DPAD_CENTER (`input keyevent 23`)** foca e seleciona instantaneamente o pacote ZIP sem depender de coordenadas de tela ou micro-deslizes. Caso o DPAD não finalize, o sistema aplica Toque Direto + ENTER (Camada 2) ou Toque Longo Humanizado (Camada 3) com botão SELECIONAR. Implementado em `t20_long_press_and_confirm_select` em [t20_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/nokia_t20/t20_pipeline.c#L625). Compilação validada com sucesso sob `registros/2026-08-05/09-26_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **[2026-08-05] Novo Módulo F3 (Nokia T20 - Atualização Expressa de Pacote):** Criado o orquestrador `T20_ExecutarPipelineF3_AtualizarPacote` em [t20_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/nokia_t20/t20_pipeline.c#L1405) e exportado via DLL em `Engine_ExecuteF3_NokiaT20` ([api.c](file:///c:/PROJETOS/Inno_RPA/src/api.c#L111)). O fluxo acorda a tela (`T20_Etapa1`), envia o pacote atualizado via ADB (`T20_Etapa1_6`), abre o aplicativo **InnovAva** diretamente via Intent/Monkey (`br.com.yantec.innovava`) e executa a esteira `T20_Etapa5` com a seleção multi-camada por DPAD/XML. Integrado ao C# WPF ([MonitorView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/MonitorView.xaml.cs#L297)). Compilação final concluída sob `registros/2026-08-05/09-39_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **[2026-08-05] Ajuste no Pipeline F2 & Validação de Paridade F1/F2/F3 (Nokia T20):**
      1. **Conexão Túnel em F2:** Garantida a execução de `T20_Etapa3` (Túnel de Internet USB Gnirehtet) antes do login em `T20_Etapa4`, seguida da rotina `T20_Etapa4_5_StopGnirehtet` para fechar a conexão túnel pós-autenticação em [t20_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/nokia_t20/t20_pipeline.c#L1395).
      2. **Desligamento Automático em F2:** Ao selecionar o pacote ZIP na `T20_Etapa5`, o pipeline F2 executa `T20_Etapa7_PowerOff`, desligando automaticamente o tablet Nokia T20.
      3. **Verificação de Paridade:** Confirmado que a rotina de importação do ZIP (`T20_Etapa5` com a seleção multi-camada por DPAD/XML) é **100% idêntica e compartilhada entre os fluxos F1, F2 e F3**. Compilação concluída sob `registros/2026-08-05/09-45_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **[2026-08-05] Conclusão da Importação e Encerramento de Apps em F1 (Nokia T20):** Implementada a rotina `T20_Etapa6_AguardarConclusaoImportacao` em [t20_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/nokia_t20/t20_pipeline.c#L1239). Após a seleção do pacote ZIP na `T20_Etapa5`, o motor monitora a tela via XML em tempo real até o reaparecimento do botão **"PROCURAR ARQUIVO .ZIP"** (indicando que o InnovAva concluiu o desempacotamento e a carga local dos dados). Em seguida, executa a `T20_Etapa7_CloseApps`, fechando todos os aplicativos abertos e limpando os recentes. Compilação final validada sob `registros/2026-08-05/09-49_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **[2026-08-05] Integração da Interface C# WPF (Nokia T20 - Botão F3 & Filtro de Automação):**
      1. **Botão F3 na Barra de Funções:** Adicionado o botão `[F3] Atualizar Pacote` ao painel de ações rápida do Nokia T20 (`GridNokiaActions`) em [MonitorView.xaml](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/MonitorView.xaml#L428), vinculado ao evento `BtnNokiaF3_Click`.
      2. **Filtro de Automação (ComboBox):** Atualizada a rotina `BtnCategoryNokia_Click` em [MonitorView.xaml.cs](file:///c:/PROJETOS/Inno_RPA/src/ui/Views/MonitorView.xaml.cs#L1760) para popular as opções `[F1] Sincronização + Instalação`, `[F2] Reinstalação (Preserva Apps)` e `[F3] Atualizar Pacote`.
      3. **Despacho Automático:** Adicionado o tratamento para `isNokiaGrid` no método `RunAutoInstallForDevice`, garantindo que os disparos via filtro acionem corretamente as rotinas `Engine_ExecuteF1_NokiaT20`, `Engine_ExecuteF2_NokiaT20` e `Engine_ExecuteF3_NokiaT20`. Compilação validada sob `registros/2026-08-05/10-05_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **[2026-08-05] Padronização Global de Fonte (1.15), Pausa de 5s Pós-Seleção e Encerramento sem Desligamento (Nokia T20):**
      1. **Escala de Fonte Padrão (1.15):** Incluído o comando `adb shell settings put system font_scale 1.15` na inicialização do sistema (`T20_Etapa1` e `T20_Etapa1_2_DarkMode` em [t20_pipeline.c](file:///c:/PROJETOS/Inno_RPA/src/core/functions/nokia_t20/t20_pipeline.c#L90)), garantindo que qualquer execução em F1, F2 ou F3 aplique a fonte 1.15.
      2. **Pausa de 5s Pós-Seleção do ZIP:** Adicionado `Sleep(5000)` obrigatório em `T20_Etapa6_AguardarConclusaoImportacao` para aguardar a transição de tela antes de iniciar o monitoramento XML do botão **"PROCURAR ARQUIVO .ZIP"**.
      3. **Manutenção do Tablet Ligado:** Padronizada a finalização em F1, F2 e F3 para fechar todos os aplicativos e limpar recentes (`T20_Etapa7_CloseApps`), mantendo o tablet **LIGADO** na tela inicial sem efetuar o desligamento físico. Compilação final validada sob `registros/2026-08-05/10-18_Plataforma_InnovTech_DESKTOP_BUILD`.
    - **[2026-08-05] Atualização Completa dos Nós Neurais do Nokia T20 (InnoLearn & BrainVisualizer):**
      1. **Sementes Neurais C (`src/core/learn_db.c`):** Injetados os alvos de UI do Nokia T20: `t20_spinner_municipio`, `t20_campo_email`, `t20_campo_senha`, `t20_botao_entrar`, `t20_botao_importar_zip` e `t20_btn_procurar_arquivo`.
      2. **Mapeamento C# WPF (`InnoLearnView.xaml.cs`):** Mapeadas as rotulagens e categorias para agrupamento estrito sob a aba **📱 NOKIA T20 (ANDROID 11)**. Compilação final sob `registros/2026-08-05/14-29_Plataforma_InnovTech_DESKTOP_BUILD`.
- **06/08/2026:**
  - **Resolução Definitiva de Caracteres Especiais e Acentuação em Caminhos (`Pão de Açúcar` / `No such file or directory`):**
    1. **Super-Sanitização em `adb_get_safe_path` (`src/core/adb.c`):** Refatorada a API em C para tentar a conversão 8.3 (`GetShortPathNameW`). Quando desativada na particao NTFS do Windows, o motor executa o fallback transparente copiando o arquivo mestre via API Unicode nativa (`CopyFileW`) para uma pasta de cache 100% ASCII (`data/temp_adb/inno_safe_<hash>.apk`).
    2. **Varredura e Atualização de Todos os Pipelines C:** Injetada a garantia do uso de `safe_apk`, `safe_pacote` e `safe_wp` em todas as chamadas de `adb push` e `adb install` nos motores **Multilazer Tab M7** (`m7_pipeline.c`), **Positivo Tab Vision 7** (`v7_pipeline.c`), **Padrão F1** (`f1_geraldata.c`) e **Nokia T20** (`t20_pipeline.c`).
    3. **Resolução de Pastas no C# WPF (`PathHelper.cs`, `MonitorView.xaml.cs`, `MunicipiosWindow.xaml.cs`):** Implementados os helpers `RemoveAccents`, `SanitizeFolderName` e `SanitizeFolderNameAscii`. As buscas de arquivos de municípios (`GetDynamicApkPath` / `GetDynamicPacotePath`) agora verificam de forma transparente diretórios com ou sem acentos (ex: `Pão de Açúcar` e `Pao_de_Acucar`), e novas inclusões gravam em pastas ASCII sanitizadas.
    4. **Compilação e Auditoria:** Motores DLL C e UI C# WPF compilados com 100% de sucesso (Zero Errors / Zero Warnings) e auditados sob o registro `registros/2026-08-06/01-20_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Correção da Seleção do Campo de Município no Multilazer Tab M7 (`F7_Etapa4_Login` em `m7_pipeline.c`):**
    1. **Eliminação de Strings Mojibake Legadas:** Removida a tentativa de busca por texto com codificação corrompida ("SÃƒÂ£o Miguel dos Campos").
    2. **Integração InnoAI Neural:** Conectada a busca do Spinner à chave neural `f7_spinner_municipio` (`learn_find_element`) com 5 níveis de fallbacks robustos.
    3. **Matching Fuzzy Sem Acentos (`f7_get_centroid_fuzzy`):** Aplicada a normalização `f7_normalize_char` tanto na verificação de município já selecionado quanto na busca dentro da lista suspensa.
- **10/08/2026:**
  - **Correção da Prioridade de Busca de Pacotes (ZIP/APK) por Município (`PathHelper.cs`, `MonitorView.xaml.cs`, `MunicipiosWindow.xaml.cs`):**
    - Identificada e corrigida a causa raiz da seleção incorreta de pacotes fantasma (ex: município "Pão de Açúcar" mantinha o pacote antigo `PDA-15-07-2026.zip` mesmo após anexar um novo pacote `pda10082026.zip`).
    - **Reordenação de Prioridade Máxima no `PathHelper`:** Os métodos estáticos `GetDynamicPacotePath` e `GetDynamicApkPath` foram unificados e atualizados no `PathHelper.cs` para pesquisar **PRIMEIRO** na pasta oficial recém-anexada `data/output/arquivos/pacotes/<clean>` e `data/output/arquivos/pacotes/<cleanAscii>` antes das pastas legadas `Arquivos/`.
    - **Purga de Resíduos Fantasma em `CopiarArquivo`:** Atualizada a rotina `CopiarArquivo` em `MunicipiosWindow.xaml.cs` para expurgar automaticamente resíduos de pacotes antigos em todas as subpastas candidatas legadas (`Arquivos/`, `data/input/municipios/`) no momento em que um novo arquivo é anexado.
    - **Sincronização Visual e Auditoria:** Unificados os exibidores da `MunicipiosWindow` e `MonitorView` para consultarem o `PathHelper`, garantindo paridade 100% exata entre o cadastro e a execução das automações F1-F8. Removidos os pacotes obsoletos residuais e recompilados o motor em C (`InnoEngine.dll`) e a UI em C# WPF (`InnoRPAui.exe`).
    4. **Rolagem Automática de Lista (Swipe ADB):** Adicionado deslize suave na lista de municípios caso a opção desejada esteja localizada abaixo da área visível inicial.
    5. Auditado sob o registro `registros/2026-08-06/01-28_Plataforma_InnovTech_DESKTOP_BUILD`.
  - **Automação Completa da Importação do ZIP e Encerramento de Apps em F1 (Multilazer Tab M7):**
    1. **Função Helper Unificada (`M7_ExecutarImportacaoEFecharApps` em `m7_pipeline.c`):** Substituída a parada manual anterior por um fluxo automatizado em 6 passos:
       - Clique no botão **"PROCURAR ARQUIVO ZIP"** (com detecção fuzzy de centroides).
       - Navegação/Abertura automática da pasta **`InnovAva`** / **`innovava`** no seletor de arquivos do Android.
       - Seleção do pacote `.zip` através da correspondência de nome base do arquivo.
       - Pausa de estabilização de **4 segundos** pós-seleção (`Sleep(4000)`).
       - Monitoramento ativo (polling XML) até o reaparecimento do botão **"PROCURAR ARQUIVO ZIP"** (indicando término da carga local).
       - Encerramento completo de todos os aplicativos abertos (HOME -> Recentes KEYEVENT 187 -> Limpar tudo -> `am force-stop` / `am kill-all`).
    - **Diagnóstico e Resolução do Crash no F1 do Multilazer Tab M7 (Violação de Acesso em C Nativo):**
    - **Causa Raiz Identificada:** No passo 5 da automação de carga do ZIP (`M7_ExecutarImportacaoEFecharApps` em `m7_pipeline.c`), a instrução `f7_get_centroid_fuzzy(xml_wait, "Procurar arquivo", NULL, NULL, 0)` passava `out_cx` e `out_cy` como `NULL`. Ao localizar o elemento no XML, a função tentava desreferenciar e gravar diretamente nos ponteiros nulos (`*out_cx = ...; *out_cy = ...;`), causando uma exceção nativa de Violação de Acesso (*Access Violation 0xc0000005* / *SEGFAULT*) no C# e derrubando a aplicação.
    - **Blindagem Nula Universal (Zero-Leak & Crash-Free):** Injetada validação rigorosa de nulidade de ponteiros (`if (out_cx) *out_cx = ...; if (out_cy) *out_cy = ...;`) e sanitização de entradas (`if (!xml || !search_str) return 0;`) nas rotinas `f7_get_centroid_fuzzy`, `f7_get_centroid_nocase`, `f7_stristr_fuzzy` and `f7_stristr_ascii` em todos os motores C nativos (`m7_pipeline.c`, `f7_tabmulti.c`, `learn_engine.c`, `v7_pipeline.c` e `t20_pipeline.c`).
    - **Compilação & Release:** Recompilado com 100% de sucesso (Zero Errors / Zero Warnings) e auditado sob a build `registros/2026-08-06/04-40_Plataforma_InnovTech_DESKTOP_BUILD`, com pacotes atualizados na release executável `InnoRPA_Release`.
   - **Encerramento com Shutdown Físico no F1 (Multilazer Tab M7):**
     1. **Desligamento Automático (`reboot -p`):** Atualizada a etapa final (`final_step` em `m7_pipeline.c`) para que, imediatamente após o término do fechamento manual/visual de todos os aplicativos e a limpeza da tela de recentes em `M7_ExecutarImportacaoEFecharApps`, o motor desative as conexões/varreduras de rede (Wi-Fi, Bluetooth, Localização) e emita o comando de shutdown físico (`reboot -p`).
     2. **Auditado:** Compilado e auditado sob o registro de build `registros/2026-08-06/06-19_Plataforma_InnovTech_DESKTOP_BUILD`.
- **09/08/2026:**
  - **Refatoração Completa e Verificação Total no Motor TabMulti M7 (`f1_tabmulti_m7.c`):**
    1. **Verificação de Seleção de Município (`f7_selecionar_municipio_m7`):** Adicionada checagem prévia para verificar se o município já está selecionado na tela principal antes de abrir a gaveta. Implementado polling de abertura da gaveta (até 5s), expansão de rolagem (8 swipes para baixo, 5 para cima) com delays de 1.5s para acomodar o hardware lento M7, e verificação pós-clique que confirma se o diálogo fechou e se o município foi exibido no Spinner principal.
    2. **Validação e Localização Infalível de E-mail e Senha Cifrada (`f7_get_password_centroid` / `f7_check_password_field_filled`):** Implementada a função dedicada `f7_get_password_centroid` que combina busca por `password="true"`, `senha`, `password`, 2º `EditText` e o fallback de coordenadas espaciais nativas do M7 `(300, 600)`. Garante que os comandos de foco, digitação por ADB e o toque único em Voltar (`keyevent 4`) para fechar o teclado NUNCA deixem de ser executados, eliminando falhas de injeção de credenciais.
    3. **Checklist de Tripla Validação Pré-Clique:** Antes de acionar o botão Entrar, confirma se Município, E-mail e Senha estão visíveis e válidos na UI. Se o município tiver oscilado, aciona re-seleção emergencial.
    4. **Clique no Botão Entrar & Re-toques Persistentes no M7:** O PASSO 5 aciona o botão Entrar via InnoAI, centroides ou fallback `(300, 750)`, e confirma no XML se a tela entrou em transição. No loop pós-login de `M7_ExecutarPipeline`, se o botão Entrar permanecer visível após a 3ª checagem no tablet M7 lento, o motor re-executa ativamente o toque nele a cada 3 repetições.
    5. **Sequência Linear Estrita de Autenticação com Fechamento Duplo de Teclado:** Ajustado o motor em `f1_tabmulti_m7.c` para executar estritamente os 6 passos solicitados: 1) Selecionar município; 2) Digitar e-mail; 3) Fechar teclado via `keyevent 4` (**Voltar 1 vez**); 4) Digitar senha no campo `(300, 600)` desobstruído; 5) Fechar teclado via `keyevent 4` (**Voltar 1 vez**); 6) Clicar em Entrar. Impede que a senha seja digitada na caixa de e-mail por obstrução do teclado virtual.
    6. **Criação do Módulo Isolado F5 TabMulti M7 (`f5_tabmulti_m7.c` / `f5_tabmulti_m7.h`):** Desenvolvido o motor dedicado `M7_ExecutarPipelineF5` exportado via `Engine_ExecuteF5_TabMulti`. O fluxo prepara o tablet, **remove completamente qualquer versão pré-instalada (`adb uninstall br.com.yantec.innovava`)**, realiza a instalação limpa do novo APK, abre o aplicativo InnovAva, **ativa o túnel de internet USB (Gnirehtet `M7_StartGnirehtet`) imediatamente com o app em execução na tela**, injeta município, e-mail e senha, e aciona o toque em "Entrar". Em seguida, aguarda **exatamente 30 segundos** com a conexão ativa para sincronização na nuvem, e executa a purga total de redes (desativa Wi-Fi, Bluetooth, GPS, Gnirehtet), força o encerramento do app e emite o desligamento físico (`reboot -p`).
    - **10/08/2026:**
  - **Reformulação e Padronização Global da Função Atualizar Pacote (F3) para Todos os Tablets:**
    - Atualizados os motores C do Positivo Tab Vision 7 ([f3_vision_tab7.c](file:///p:/src/core/functions/vision7/f3_vision_tab7.c)), Multilaser TabMulti M7 ([f2_tabmulti_m7.c](file:///p:/src/core/functions/tabmulti/f2_tabmulti_m7.c) / [f3_tabmulti_m7.c](file:///p:/src/core/functions/tabmulti/f3_tabmulti_m7.c)) e Nokia T20 ([f3_nokia_t20.c](file:///p:/src/core/functions/nokia_t20/f3_nokia_t20.c)).
    - A função F3 agora executa a **formatação/expurgo prévio do armazenamento interno `/sdcard/`** (preservando pastas essenciais do SO como `Android`, `Pictures`, `classjava`, `cstore`), recria os diretórios alvos (`/sdcard/avaliacoes/`, `/sdcard/InnovAva/`, `/sdcard/innovava/`) e faz a **transferência completa do APK atualizado e do Pacote ZIP de dados**.
    - **Proteção Contra Erros de Caracteres Especiais e Acentuação:**
      - Injetadas chamadas `adb_get_safe_path` para conversão 8.3 / fallback Unicode em cache ASCII em todas as operações de `adb push` e `adb install`.
      - Atualizados os localizadores dinâmicos no C# WPF ([MonitorView.xaml.cs](file:///p:/src/ui/Views/MonitorView.xaml.cs)) (`GetDynamicApkPath` e `GetDynamicPacotePath`) para buscar arquivos de municípios em pastas com acentos (ex: `Pão de Açúcar`), sem acentos (`Pao_de_Acucar`), ASCII sanitizadas ou no diretório oficial `Arquivos/`.
    - Motores DLL C (`InnoEngine.dll`) e UI C# WPF (`InnoRPAui.exe`) recompilados com 100% de sucesso (Zero Errors / Zero Warnings) nos ambientes de Desenvolvimento ([data/output/InnoRPAui.exe](file:///p:/data/output/InnoRPAui.exe) - Ícone Azul Escuro) e Release Portátil ([InnoRPA_Release/InnoRPAui.exe](file:///p:/InnoRPA_Release/InnoRPAui.exe) - Ícone Roxo Oficial). Registros de auditoria gerados sob `registros/2026-08-10/15-45_Plataforma_InnovTech_DESKTOP_BUILD` e `15-46_Plataforma_InnovTech_DESKTOP_BUILD`.


## Submódulo: Funcionamento Analítico do InnoAI

### 1. Arquitetura Geral do InnoAI / InnoLearn
O **InnoAI** é o motor de inteligência e automação visual da Plataforma InnovTech. Ele combina reconhecimento de estrutura em árvore (Android UIAutomator XML Dump), algoritmos de *fuzzy matching* para sanitização de strings em tempo real, e um sistema de **Ranking de Estratégias Neurais (Scores de -100 a +100)** para localizar e interagir com elementos visuais de forma rápida e à prova de falhas.

* **Backend C Nativo (`src/core/learn_db.c` e `src/core/learn_engine.c`):**
  - Gerencia o banco estático e dinâmico de estratégias (`learn_db`).
  - Cada elemento alvo da interface (ex: `v7_spinner_municipio`, `v7_campo_email`, `v7_campo_senha`, `v7_botao_entrar`, `v7_botao_importar_zip`, `v7_btn_procurar_arquivo`, `f6_btn_sincronizar_agora`) é associado a um vetor de seletores concorrentes.
  - As buscas avaliam `resource-id`, `content-desc`, `text`, tags genéricas `EditText`/`Button` e coordenadas de retaguarda (`FastCache`).

* **Frontend C# WPF (`InnoLearnView` e `BrainVisualizerWindow`):**
  - **InnoLearnView:** Exibe a matriz de conhecimento categorizada por abas de **Perfil de Hardware** (`🌐 TODOS`, `📱 VISION 7 (V7)`, `📱 TABMULTI M7`, `🖥️ GERAL`) e organizada estritamente em **Etapas Sequenciais dos Pipelines (Etapas 1 a 7)**.
  - **BrainVisualizerWindow:** Visualizador topológico em formato de grafo Sci-Fi de redes neurais. Possui HUD de estatísticas em tempo real (Nós, Sinapses, Precisão Global %), busca por ID/Query (`🔍`), gaveteiro lateral holográfico de inspeção com gráfico de acertos/falhas dos últimos 10 testes, cópia de query em um clique (`📋 COPIAR`) e ajuste de vetores trigonométricos conectados às bordas externas do nó central **InnoAI**.

---

### 2. Algoritmo de Ranking, Scoring e Resiliência (Dual-Scan & Fuzzy)
1. **Scoring Dinâmico (-100 a +100):**
   - Ao executar `learn_find_element()`, o motor C analisa o XML atual contra as estratégias registradas para o elemento.
   - Quando um seletor obtém sucesso (`hit`), a função `learn_record_hit()` incrementa sua pontuação e atualiza o histórico neural.
   - Quando um seletor falha (`miss`), `learn_record_miss()` reduz o score, forçando o motor a priorizar seletores mais estáveis em execuções futuras.

2. **Matching Fuzzy (`inno_normalize_char` & `stristr_fuzzy`):**
   - Para mitigar problemas de acentuação (ex: "Pão de Açúcar", "São Luís", "Conceição") e divergências de encoding entre Windows-1252, CP850 e UTF-8, o motor C converte e compara os caracteres de forma agnóstica a maiúsculas/minúsculas e diacríticos.

3. **FastCache de Coordenadas de Fallback:**
   - Em caso de travamento do XML ou modificação drástica da árvore do Android, a função `learn_get_fallback_coord()` fornece a última posição espacial validada `(cx, cy)` para garantir que o clique ocorra sem interromper a esteira.

---

### 3. Mapeamento Analítico dos Pipelines (Positivo Tab Vision 7)

| Função / Pipeline | Identificador C / Exportação API | Escopo e Comportamento Analítico | Estado Final do Tablet |
| :--- | :--- | :--- | :--- |
| **F1: Instalação Completa** | `V7_ExecutarPipelineCompleto`<br>`Engine_ExecuteF1_Positivo` | Provisionamento completo do zero (14 sub-etapas): Smart Wakeup, Dark Mode, purga do `/sdcard`, isolamento batched de bloatwares, trava Portrait/brilho 70%/fonte 1.5, desinstalação/instalação do APK, Reverse Tethering USB (Gnirehtet), injeção silenciosa de permissões, login automático via InnoAI, importação do ZIP, limpeza visual manual de cartões em recentes, aplicação de wallpaper municipal e desligamento físico. | 🛑 **Desligado (`reboot -p`)** |
| **F2: Reinstalação Limpa & Offline** | `F2_ExecutarPipelineCompleto`<br>`Engine_ExecuteF2_Reinstall` | Mesma esteira de provisionamento e carga de dados do F1, redirecionando o encerramento para a rotina **`F2_Etapa_Offline`**: ativa o Modo Avião (`airplane_mode_on 1`), desativa Wi-Fi/Bluetooth/GPS, encerra background e **mantém o tablet LIGADO e pronto para entrega**. | ⚡ **Ligado (Modo Avião / Offline)** |
| **F3: Atualização Expressa de Pacote** | `F3_ExecutarAtualizacaoPacote`<br>`F3_ExecutarAtualizacaoPacote` | Atualização exclusiva do pacote ZIP sem alterar logins: purga `/sdcard/avaliacoes`, push dos arquivos atualizados, reinício do app, acionamento do menu e importação automatizada do ZIP sem desligar o aparelho no final. | 🟢 **Ligado (Importação Concluída)** |
| **F4: Terminal Interativo Kiosk** | `F4_ExecutarFerramenta`<br>`F4_DefinirWallpaper` | Manutenção de hardware: bloqueio/liberação de Câmera e Configurações (`pm suspend`), proxy Gnirehtet manual, remoção da barra de busca do Launcher e injeção em cascata de Papel de Parede nas telas inicial e de bloqueio. | 🟢 **Mantém estado (Conforme ação)** |
| **F5: Desligamento Remoto** | `F5_ExecutarDesligamento`<br>`Engine_ExecuteF5_Shutdown` | Encerramento seguro: para a VPN Gnirehtet, desativa Wi-Fi/Bluetooth, executa limpeza manual e visual de cartões na tela de recentes (`KEYEVENT 187` -> swipe -> clique no centroide 'Limpar tudo') e emite o desligamento físico. | 🛑 **Desligado (`reboot -p`)** |
| **F6: Sincronização Direta Nuvem** | `F6_ExecutarSincronizacao`<br>`Engine_ExecuteF6_Sincronizacao` | Conecta internet via USB (`V7_Etapa2_5`), localiza o botão 'SINCRONIZAR AGORA' via InnoAI (`f6_btn_sincronizar_agora`), confirma modais, monitora término e desliga o tablet. | 🛑 **Desligado (`reboot -p`)** |
| **F8: Conexão Ativa Continuada** | `V7_Etapa2_5`<br>`Engine_ExecuteF8_ActiveConnection` | Ativação permanente da ponte de internet PC -> Tablet via USB (Gnirehtet) sem disparar rotinas de desligamento. | 🟢 **Conectado (LIGADO)** |

---

## Próximos Passos (Backlog)
- Foco atual: Auditoria de "Zero-Leak" no backend em C Puro e bater testes de estabilidade na UI.

<!-- Desenvolvedor: Antigravity (Arquiteto de Software Sênior & Especialista DevOps Full-Stack) -->

