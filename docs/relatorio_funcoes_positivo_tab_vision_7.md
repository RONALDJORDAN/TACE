# Relatório Técnico de Funções — Positivo Tab Vision 7

**Dispositivo:** Positivo Tab Vision 7  
**Arquivos do Motor:** `src/core/functions/vision7/`  
**Linguagem:** C Nativo (DLL `InnoEngine.dll`)  
**Autor / Engenharia:** Jordan Araujo | Plataforma InnovPlay  

---

## 1. Visão Geral do Equipamento

O **Positivo Tab Vision 7** é um tablet Android de 7 polegadas amplamente utilizado em projetos de avaliação educacional e coleta de dados em campo.
- **Resolução de Tela Alvo:** Portrait 600x1024 / 800x1280.
- **Trava de Orientação:** Exige trava física de rotação (`wm user-rotation lock 0`).
- **Arquivos-Fonte:** `f1_vision_tab7.c/.h` a `f8_vision_tab7.c/.h`.

---

## 2. Índice de Funções do Motor

| Código | Nome da Função | Arquivo Fonte | Descrição Resumida |
| :--- | :--- | :--- | :--- |
| **F1** | `V7_ExecutarPipelineF1` | `f1_vision_tab7.c` | Instalação Completa (Smart Wakeup, Limpeza, APK, Login, Carga ZIP, Kiosk, Offline) |
| **F2** | `F2_ExecutarPipelineCompleto` | `f2_vision_tab7.c` | Reinstalação Limpa & Offline (Preserva apps do sistema, reinstala app, importa pacote e desliga) |
| **F3** | `F3_ExecutarAtualizacaoPacote` | `f3_vision_tab7.c` | Atualização Expressa de Pacote (Push rápido do ZIP, reboot do app e acionamento do menu de importação) |
| **F4** | `F4_ExecutarTerminalKiosk` | `f4_vision_tab7.c` | Terminal Kiosk & Wallpaper (Papel de parede, brilho 70%, fonte 1.5 e trava de navegação) |
| **F5** | `F5_ExecutarDesligamento` | `f5_vision_tab7.c` | Desligamento Remoto (Desativação de rádios Wi-Fi/BT/GPS e envio de `reboot -p`) |
| **F6** | `Engine_ExecuteF6_Sincronizacao` | `f6_vision_tab7.c` | Sincronização Inteligente Condicional (Smart Wakeup, Abertura App, Leitura de Pendências, Desvio < 1 ou Sincronização Gnirehtet >= 1, Polling Confirmação, Encerramento de Redes, Limpeza de Apps e Shutdown) |
| **F8** | `V7_ActiveConnection` | `f8_vision_tab7.c` | Conexão Ativa Continuada (Mantém o túnel Gnirehtet / Wi-Fi em estado operacional) |

---

## 3. Detalhamento Passo a Passo por Função

### [F1] Instalação Completa (`f1_vision_tab7.c`)
1. **Smart Wakeup & Unlock:** Checa `dumpsys power` (se apagada, envia `keyevent 224`, `keyevent 82` e `swipe 300 1000 300 300`).
2. **Modo Escuro:** Força `cmd uimode night yes`.
3. **Preservação de Apps:** Remove aplicativos não autorizados de terceiros sem desinstalar os apps do fabricante.
4. **Push de Arquivos:** Transfere `app.apk` e `pacote.zip` para `/sdcard/`.
5. **Reinstalação & Concessão de Permissões:** Executa `pm install -r -g` e concede permissões de `CAMERA`, `READ_EXTERNAL_STORAGE` e `WRITE_EXTERNAL_STORAGE`.
6. **Túnel de Conexão Gnirehtet:** Inicia o túnel de rede local via USB.
7. **Autenticação & Seleção de Município:** Preenche usuário e senha via ADB, utiliza `inno_stristr_fuzzy` para localizar o município selecionado no XML do UIAutomator.
8. **Importação do Pacote ZIP:** Desliza a gaveta lateral (`swipe 10 500 650 500`), clica em "Importar ZIP", navega no Seletor de Arquivos do Android SAF até `/sdcard/pacote.zip`.
9. **Desativação de Conexão & Modo Offline:** Encerra o Gnirehtet e ativa o Modo Avião.

### [F2] Reinstalação Limpa & Offline (`f2_vision_tab7.c`)
1. Trava a orientação da tela em modo Vertical (`wm user-rotation lock 0`).
2. Acorda a tela e ativa o Modo Escuro.
3. Transfere novos arquivos `app.apk` e `pacote.zip`.
4. Reinstala o aplicativo mantendo dados do usuário e concede permissões nativas.
5. Inicia o app, abre o menu lateral e clica em "Importar ZIP" usando `adb_get_centroid_fuzzy`.
6. Ativa o Modo Offline completo (desativa Wi-Fi, Bluetooth e varredura BLE).
7. Envia o comando de SHUTDOWN (`reboot -p`).

### [F3] Atualização Expressa de Pacote (`f3_vision_tab7.c`)
1. Executa a preparação rápida de tela e brilho.
2. Transfere apenas o novo arquivo `pacote.zip` para `/sdcard/pacote.zip`.
3. Força a reinicialização do aplicativo InnovAva (`am force-stop` seguido de `monkey`).
4. Abre o menu lateral via gesture swipe e clica em "Importar ZIP".

### [F4] Kiosk & Wallpaper (`f4_vision_tab7.c`)
1. Configura o papel de parede oficial da avaliação.
2. Define o nível de brilho da tela fixo em 70% (178/255) e escala de fonte em 1.5x para máxima legibilidade.
3. Oculta a barra de navegação padrão se o modo Kiosk estiver habilitado.

### [F5] Desligamento Remoto (`f5_vision_tab7.c`)
1. Encerra qualquer túnel de rede ativo.
2. Desativa conexões de hardware (Wi-Fi `svc wifi disable`, Bluetooth `svc bluetooth disable` e Localização `location_mode 0`).
3. Envia o sinal de encerramento do sistema de arquivos (`reboot -p`).

### [F6] Sincronização Inteligente Condicional (`f6_vision_tab7.c`)
1. **Smart Wakeup & Unlock:** Acorda o tablet, desbloqueia a tela e trava a orientação em Portrait (`wm user-rotation lock 0`).
2. **Limpeza Prévia de Apps em Aberto:** Retorna à HOME (`keyevent 3`) e encerra previamente aplicativos em segundo plano (`am force-stop` + `am kill-all`) garantindo inicialização do InnovAva em estado limpo.
3. **Abertura do App:** Lança o aplicativo principal (`br.com.yantec.innovava`) em primeiro plano.
4. **Leitura de Avaliações Pendentes:** Exibe a gaveta lateral caso necessário e extrai a quantidade de pendências via UIAutomator XML (`v7_parse_pending_count`).
5. **Avaliação Condicional:**
   - **Se Pendentes < 1 (Zero):** Exibe log informativo e pula as etapas de rede/sincronização, avançando diretamente para o encerramento seguro.
   - **Se Pendentes >= 1:** Ativa a Wi-Fi (`svc wifi enable`) e o **Túnel Gnirehtet (Reverse Tethering USB)**, dispara o clique em "SINCRONIZAR AGORA", monitora o retorno de confirmação de sucesso (até 60s) e encerra o túnel Gnirehtet.
6. **Desativação Total de Rádios:** Desativa Wi-Fi, Bluetooth, Localização e varreduras de segundo plano.
7. **Limpeza Manual Visual de Recentes:** Abre os apps recentes (`keyevent 187`), desliza cartões, clica em 'Limpar tudo' e encerra processos em background (`am force-stop` + `am kill-all`).
8. **Desligamento Físico:** Dispara o comando final de shutdown (`reboot -p`).

### [F8] Conexão Ativa Continuada (`f8_vision_tab7.c`)
1. Mantém a interface Wi-Fi ligada e estabelece o túnel Gnirehtet para auditoria remota contínua.
