# Arquitetura do Pipeline Inno RPA

O Inno RPA opera através de um motor C puro `InnoEngine.dll` injetado na interface C# WPF. Este documento descreve as etapas sequenciais das funções de automação (Pipelines).

## F1: Sincronização Positivo (F1_ExecutarPipelineCompleto)
O fluxo principal `F1` é acionado para configurar um aparelho do zero, isolar a infraestrutura e injetar credenciais no app.

### Etapa 1: Wake Up e Desbloqueio (`F1_Etapa1`)
- **Ação:** Acorda a tela (`input keyevent 26`).
- **Desbloqueio:** Dá um swipe de baixo para cima (`input swipe 500 1000 500 200`) para burlar lockscreens sem senha.

### Etapa 1.5: Isolamento de Sistema (`F1_Etapa1_5`)
- **Ação:** Remove aplicativos restritos de fábrica (YouTube, PlayStore) via `pm uninstall -k --user 0`.
- **Objetivo:** Preparar o tablet para ser um quiosque acadêmico/focado no InnovPlay.

### Etapa 1.6: Limpeza e Cópias (`F1_Etapa1_6`)
- **Ação:** Remove a pasta do município no device (`rm -rf /sdcard/input/municipios/*`).
- **Cópia:** Faz o push do pacote ZIP contendo provas e do APK de avaliação para dentro do aparelho (`adb push`).

### Etapa 2: Instalação Limpa (`F1_Etapa2`)
- **Instalação:** Desinstala qualquer versão velha do app e realiza um `pm install -r` do novo APK copiado na Etapa 1.6.

### Etapa 2.5: Estabelecimento de Conexão (Reverse Tethering) (`F1_Etapa2_5`)
- **Ação:** Criação de um túnel de proxy usando `gnirehtet autorun`.
- **Objetivo:** Injetar a internet do computador no tablet via cabo USB. Isso permite que o aplicativo faça a autenticação inicial pós-login sem depender da conexão Wi-Fi do usuário/escola.

### Etapa 3: Permissões e Abertura (`F1_Etapa3`)
- **Permissões:** Garante acessos de armazenamento, câmera, microfone, GPS, etc via `pm grant`.
- **Lançamento:** Abre o aplicativo com `monkey -p <pacote> -c android.intent.category.LAUNCHER 1`.

### Etapa 4: Visão Computacional / XML Dump (`F1_Etapa4`)
- **Dump:** Usa `uiautomator dump` para salvar a árvore de elementos nativa do Android em um XML local (`window_dump.xml`).
- **Parse C:** O motor em C varre as tags procurando texto específico (ex: "Miguel" para a gaveta de Município).
- **Injeção:** Calcula as coordenadas matemáticas da tela (resolução agnóstica) baseada no `bounds` do XML e injeta cliques (`input tap`) e o teclado (`input text "..."`) para login, senha e clique no botão de "Entrar".

## F2: AppLock Kiosk
*(Fluxo em construção)*
Responsável por travar o aplicativo na tela usando permissões de Administrador de Dispositivo, bloqueando botões Home, Back e Recentes.
