# Relatório Técnico de Funções — Multilaser TabMulti M7

**Dispositivo:** Multilaser TabMulti M7  
**Arquivos do Motor:** `src/core/functions/tabmulti/`  
**Linguagem:** C Nativo (DLL `InnoEngine.dll`)  
**Autor / Engenharia:** Jordan Araujo | Plataforma InnovPlay  

---

## 1. Visão Geral do Equipamento

O **Multilaser TabMulti M7** é um dispositivo compacto com especificações de hardware ajustadas, exigindo tempos de resposta estendidos (delays de sincronização ADB maiores) e comandos otimizados de rotação de tela.
- **Resolução de Tela Alvo:** 600x1024 (Portrait).
- **Trava de Orientação:** Exige desativação de acelerômetro e trava manual (`settings put system accelerometer_rotation 0 && wm user-rotation lock 0`).
- **Arquivos-Fonte:** `f1_tabmulti_m7.c/.h` a `f4_tabmulti_m7.c/.h` e `m7_helpers.c/.h`.

---

## 2. Índice de Funções do Motor

| Código | Nome da Função | Arquivo Fonte | Descrição Resumida |
| :--- | :--- | :--- | :--- |
| **F1 / F7** | `M7_ExecutarPipelineCompleto` | `f1_tabmulti_m7.c` | Instalação Completa M7 (Wake, Limpeza, APK, Login, Importação ZIP e Kiosk) |
| **F2** | `M7_AtualizarPacote` | `f2_tabmulti_m7.c` | Atualização de Pacote M7 (Push do ZIP, abertura de menu lateral, importação e shutdown) |
| **F3** | `M7_Terminal` | `f3_tabmulti_m7.c` | Terminal Kiosk M7 (Limpeza de cache da aplicação e interrupção do Gnirehtet) |
| **F4** | `M7_DesligarAparelho` | `f4_tabmulti_m7.c` | Desligamento Remoto M7 (Desativação de rádios e desligamento via `reboot -p`) |
| **Helper** | `M7_UnlockScreen`, `M7_StartGnirehtet` | `m7_helpers.c` | Utilitários de Hardware M7 (Desbloqueio, escuro, controle de rede USB) |

---

## 3. Detalhamento Passo a Passo por Função

### [F1 / F7] Instalação Completa (`f1_tabmulti_m7.c`)
1. **Otimização M7:** Desativa rotação automática e fixa a orientação em 0° (Portrait).
2. **Desbloqueio de Tela:** Acorda a tela e realiza o gesto de unlock otimizado para a sensibilidade do M7.
3. **Modo Escuro:** Força o tema noturno no Android.
4. **Cópia de Binários:** Cria a pasta `/sdcard/InnovAva/` e transfere os arquivos `app.apk` e `pacote.zip`.
5. **Instalação do APK:** Executa a instalação silenciosa via Package Manager (`pm install -r -g`).
6. **Rede USB Gnirehtet:** Inicia o túnel de rede local `gnirehtet` para viabilizar a comunicação de login.
7. **Login Automático:** Localiza as caixas de texto `android.widget.EditText` e preenche as credenciais com escape de caracteres especiais.
8. **Importação do Pacote ZIP:** Desliza o menu lateral (`swipe 10 500 650 500`), clica em "Importar ZIP" usando `adb_get_centroid_fuzzy` e conclui a carga.
9. **Encerramento:** Desliga o túnel Gnirehtet e coloca o tablet em prontidão.

### [F2] Atualização de Pacote M7 (`f2_tabmulti_m7.c`)
1. Trava a orientação da tela em modo Portrait.
2. Executa a rotina `M7_UnlockScreen` para garatir tela ligada.
3. Envia o novo arquivo ZIP para `/sdcard/InnovAva/`.
4. Liga o Gnirehtet para garantir estabilidade durante a verificação inicial do app.
5. Inicia a `MainActivity` do InnovAva e aguarda o término do Splash.
6. Realiza o gesto de swipe lateral e clica em "Importar ZIP" com suporte a busca fuzzy sem acentos.
7. Encerra a conexão de rede e envia o comando de desligamento (`reboot -p`).

### [F3] Terminal Kiosk M7 (`f3_tabmulti_m7.c`)
1. Acorda o dispositivo.
2. Executa a limpeza completa de cache e dados temporários do InnovAva (`pm clear br.com.yantec.innovava`).
3. Interrompe processos em background do Gnirehtet para deixar o terminal em estado limpo.

### [F4] Desligamento Remoto M7 (`f4_tabmulti_m7.c`)
1. Cancela qualquer serviço de rede ativo.
2. Desliga os seletores de Wi-Fi (`svc wifi disable`), Bluetooth (`svc bluetooth disable`) e Localização (`location_mode 0`).
3. Envia o comando de desligamento físico (`reboot -p`).

### M7 Helpers (`m7_helpers.c`)
- **`M7_UnlockScreen`**: Rotina concentrada de envio de keyevents de power e unlock.
- **`M7_StartGnirehtet` / `M7_StopGnirehtet`**: Manipulação do processo de túnel reverso ADB.
