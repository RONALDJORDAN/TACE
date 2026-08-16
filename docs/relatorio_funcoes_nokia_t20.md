# Relatório Técnico de Funções — Nokia T20

**Dispositivo:** Nokia T20 (10.4" 2K)  
**Arquivos do Motor:** `src/core/functions/nokia_t20/`  
**Linguagem:** C Nativo (DLL `InnoEngine.dll`)  
**Autor / Engenharia:** Jordan Araujo | Plataforma InnovPlay  

---

## 1. Visão Geral do Equipamento

O **Nokia T20** é um tablet de alta performance e grande resolução de tela (2000x1200 pixels) rodando Android 11/12 puro.
- **Resolução de Tela Alvo:** 1200x2000 (Portrait) / 2000x1200 (Landscape).
- **Ajuste de Gestos:** Coordenadas estendidas de swipe (ex: `600 1600 600 400`) devido à maior densidade de pixels.
- **Arquivos-Fonte:** `f1_nokia_t20.c/.h`, `f2_nokia_t20.c/.h` e `f3_nokia_t20.c/.h`.

---

## 2. Índice de Funções do Motor

| Código | Nome da Função | Arquivo Fonte | Descrição Resumida |
| :--- | :--- | :--- | :--- |
| **F1** | `T20_ExecutarPipelineF1` | `f1_nokia_t20.c` | Instalação Completa Nokia T20 (Wake 2K, Push, Permissões Android 11/12, Login, Importação ZIP, Shutdown) |
| **F2** | `T20_ExecutarPipelineF2` | `f2_nokia_t20.c` | Reinstalação Limpa Nokia T20 (Preserva apps do sistema, reinstala app, login com adb_escape_text e importação) |
| **F3** | `T20_ExecutarPipelineF3_AtualizarPacote` | `f3_nokia_t20.c` | Atualização Expressa de Pacote Nokia T20 (Push de ZIP 2K, reinício e acionamento de menu) |

---

## 3. Detalhamento Passo a Passo por Função

### [F1] Instalação Completa (`f1_nokia_t20.c`)
1. **Acordamento 2K & Desbloqueio:** Dispara keyevents de tela e swipe vertical adaptado para a altura de 2000 pixels (`swipe 600 1600 600 400 300`).
2. **Modo Escuro:** Ativa o tema noturno do sistema.
3. **Cópia para Armazenamento Interno:** Transfere os arquivos para `/sdcard/innovava/`.
4. **Instalação e Concessão Granular de Permissões:** Instala o APK e concede permissões via `pm grant` para Câmera e Armazenamento.
5. **Autenticação de Acesso:** Efetua o preenchimento de e-mail e senha utilizando escapador de texto `adb_escape_text` para tratar caracteres especiais e símbolos.
6. **Seleção de Município & Importação do Pacote:** Localiza o município na lista de rolagem usando `adb_get_centroid_fuzzy` e aciona a importação do pacote avaliativo.
7. **Modo Kiosk & Shutdown:** Aplica restrições de sistema e envia o sinal de desligamento seguro.

### [F2] Reinstalação Limpa Nokia T20 (`f2_nokia_t20.c`)
1. Acorda o dispositivo Nokia T20 e desbloqueia a tela.
2. Ativa o Modo Escuro.
3. Transfere os arquivos `app.apk` e `pacote.zip` para `/sdcard/innovava/`.
4. Reinstala a aplicação mantendo aplicativos padrão do sistema e concede permissões nativas.
5. Inicia o aplicativo e executa o login com sanitização universal de caracteres especiais.
6. Desliza a barra lateral (`swipe 20 600 650 600`) e realiza o clique em "Importar ZIP" usando busca fuzzy insensível a acentos.

### [F3] Atualização Expressa de Pacote Nokia T20 (`f3_nokia_t20.c`)
1. Acorda a tela do tablet.
2. Transfere diretamente o novo arquivo `pacote.zip` para a pasta do InnovAva.
3. Reinicia a aplicação via `am force-stop` e `monkey`.
4. Abre o menu lateral com o gesto de swipe adaptado para alta resolução e clica no botão "Importar ZIP".
5. Conclui o processo com sucesso.
