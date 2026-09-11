#include "f1_vision_tab7.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_engine.h"

#include "f4_vision_tab7.h"
#include <stdio.h>
#include <string.h>

// --- Helper: Escapa strings para uso seguro no adb shell input text ---
static void escape_adb_text(const char *src, char *dest) {
  while (*src) {
    if (*src == ' ') {
      *dest++ = '%';
      *dest++ = 's';
    } else if (strchr("&|;*\\\"'()$<>`", *src)) {
      *dest++ = '\\';
      *dest++ = *src;
    } else {
      *dest++ = *src;
    }
    src++;
  }
  *dest = '\0';
}

// MACRO: Injeta o sistema de resiliencia em todas as chamadas de adb_exec neste
// arquivo
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// Estrutura para a thread da barra de progresso do Gnirehtet
typedef struct {
  LogCallback cb;
  volatile int *is_installing;
} V7_GnirehtetProgressData;

static DWORD WINAPI V7_GnirehtetProgressBarThread(LPVOID lpParam) {
  V7_GnirehtetProgressData *pdata = (V7_GnirehtetProgressData *)lpParam;
  int seconds = 0;
  while (*(pdata->is_installing) && seconds <= 15) {
    char msg[256];
    char bar[21];
    int pct = (seconds * 100) / 10; // Assume 10 segundos como media
    if (pct > 99)
      pct = 99;
    int fill = (pct * 20) / 100;
    for (int i = 0; i < 20; i++)
      bar[i] = (i < fill) ? '=' : '-';
    bar[20] = '\0';
    snprintf(msg, sizeof(msg), "[-] Instalando Gnirehtet [%s] %d%% (%ds)", bar,
             pct, seconds);
    if (pdata->cb)
      pdata->cb(msg);
    Sleep(1000);
    seconds++;
  }
  return 0;
}

// =============================================================================
//  MÓDULO F1: SINCRONIZAÇÃO E PIPELINE COMPLETO DE PREPARAÇÃO
//  Dispositivo Alvo: Positivo Tab Vision 7 (AllWinner / Android 14)
//  Arquitetura: Motor C Nativo (Inno RPA Engine)
//  Isolamento Estrito: src/core/functions/vision7/ (Regra de Ouro #1)
// =============================================================================

// =============================================================================
//  [ETAPA 1] PREPARAÇÃO DO SISTEMA, SMART WAKEUP E TRAVA DE ORIENTAÇÃO
// =============================================================================
//  O que esta etapa faz:
//  1. Smart Wakeup: Consulta o subsistema de energia (`dumpsys power`) para
//     verificar se a tela está em estado de suspensão (Dozing / Asleep).
//     Se desligada, envia o KEYCODE_POWER (26) para acender a tela fisicamente.
//  2. Desbloqueio: Emite um swipe de baixo para cima (300,1000 -> 300,300) para
//     destravar o Keyguard do Android.
//  3. Trava de Orientação e Timeout: Desativa rotação automática pelo
//  acelerômetro,
//     trava a orientação fixa em Portrait (Vertical - 0 graus) e define o tempo
//     limite de tela desligada (screen_off_timeout) para 5 minutos (300000ms),
//     evitando que o display apague durante as etapas subsequentes.
// =============================================================================
int V7_Etapa1(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1] Acordando o dispositivo e desbloqueando a tela...");

  char args[256];

  // 1. Verificar se a tela está apagada (Smart Wakeup)
  snprintf(args, sizeof(args),
           "-s %s shell \"dumpsys power | grep mWakefulness=\"", serial);
  char *wake_status = adb_exec(args);

  if (wake_status) {
    // Se a string "mWakefulness=Awake" NÃO estiver presente, a tela está
    // apagada (Dozing, Asleep, etc)
    if (strstr(wake_status, "mWakefulness=Awake") == NULL) {
      if (cb)
        cb("[-] Tela desligada detectada (Dozing/Asleep). Pressionando botão "
           "Power virtual...");
      snprintf(args, sizeof(args), "-s %s shell input keyevent 26",
               serial); // KEYCODE_POWER
      char *out_power = adb_exec(args);
      if (out_power)
        free(out_power);

      Sleep(800); // Aguarda a tela acender fisicamente
    } else {
      if (cb)
        cb("[-] A tela já estava acesa. Ignorando botão Power.");
    }
    free(wake_status);
  }

  // 2. Simular deslize vertical para desbloquear o Keyguard
  snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300",
           serial);
  char *out = adb_exec(args);
  if (out)
    free(out);

  // 3. Garantir orientação vertical (Portrait), desativar rotação automática e
  // definir timeout de 5 min
  if (cb)
    cb("[-] Trava de tela na vertical (Portrait) e desativacao de rotacao "
       "automatica...");
  snprintf(args, sizeof(args),
           "-s %s shell \"settings put system accelerometer_rotation 0 && "
           "settings put system user_rotation 0 && wm user-rotation lock 0 && "
           "settings put system screen_off_timeout 300000\"",
           serial);
  char *out_timeout = adb_exec(args);
  if (out_timeout)
    free(out_timeout);

  if (cb)
    cb("[-] Tela desbloqueada e configurada com sucesso.");
  return PIPELINE_SUCCESS;
}

static const char *V7_DEBLOAT_PACKAGES[] = {
    // 1. Câmeras
    "com.softwinner.camera2", "com.android.camera2", "com.android.camera",
    "com.sec.android.app.camera", "com.motorola.camera2",
    "com.motorola.camera3", "com.google.android.GoogleCamera",
    "com.huawei.camera", "com.mediatek.camera",

    // 2. Calculadora
    "com.google.android.calculator", "com.android.calculator2",

    // 3. Google Chrome
    "com.android.chrome", "com.chrome.beta", "com.chrome.dev",
    "com.chrome.canary",

    // 4. Contatos
    "com.google.android.contacts", "com.android.contacts",

    // 5. Google Drive e Editores
    "com.google.android.apps.docs", "com.google.android.apps.docs.editors.docs",
    "com.google.android.apps.docs.editors.sheets",
    "com.google.android.apps.docs.editors.slides",

    // 6. Emergência / Safety Hub
    "com.google.android.apps.safetyhub", "com.android.emergency",

    // 7. Files by Google / Files Go (Preserva DocumentsUI para o seletor SAF)
    "com.google.android.apps.nbu.files", "com.google.android.apps.nbu.filesgo",

    // 8. Galeria Google Go / Fotos / Galeria AOSP
    "com.google.android.apps.photosgo", "com.google.android.apps.photos",
    "com.android.gallery3d", "com.android.gallery",

    // 9. Google TV / Play Filmes
    "com.google.android.videos", "com.google.android.tv",

    // 10. Gravador de Som / Google Recorder
    "com.google.android.apps.recorder", "com.android.soundrecorder",
    "com.softwinner.soundrecorder",

    // 11. Google Maps / Maps Go
    "com.google.android.apps.maps", "com.google.android.apps.mapslite",

    // 12. Google Meet / Duo
    "com.google.android.apps.tachyon", "com.google.android.apps.meetings",

    // 13. Google Play Store
    "com.android.vending",

    // 14. Relógio / DeskClock
    "com.google.android.deskclock", "com.android.deskclock",

    // 15. YouTube / YouTube Kids
    "com.google.android.youtube", "com.google.android.youtube.tv",
    "com.google.android.apps.youtube.kids",

    // 16. YouTube Music
    "com.google.android.apps.youtube.music",

    // 17. Google Search / Gmail (Bloatware Google)
    "com.google.android.googlequicksearchbox",
    "com.google.android.apps.searchlite", "com.google.android.gm",
    "com.google.android.gm.lite",

    // 18. Google Assistente / Assistant Go
    "com.google.android.apps.assistant",
    "com.google.android.apps.googleassistant",
    "com.android.hotwordenrollment.okgoogle",
    "com.android.hotwordenrollment.xgoogle",

    // 19. Google Agenda / Calendar / Calendar Go
    "com.google.android.calendar", "com.android.calendar",
    "com.google.android.syncadapters.calendar"};
static const int V7_NUM_DEBLOAT_PACKAGES =
    sizeof(V7_DEBLOAT_PACKAGES) / sizeof(V7_DEBLOAT_PACKAGES[0]);

// =============================================================================
//  [ETAPA 1.5] DEBLOAT: DESATIVAÇÃO, SUSPENSÃO E REMOÇÃO DE APPS GOOGLE /
//  BLOATWARES
// =============================================================================
//  O que esta etapa faz:
//  1. Processamento em Lote (Batched): Agrupa 6 comandos por requisição ADB
//  shell
//     para maximizar a velocidade de execução via USB.
//  2. Blindagem e Desativação: Para cada um dos pacotes da lista
//     (Câmeras, Calculadora, Chrome, Contatos, Drive, Google TV, Maps, Meet,
//     Play Store, Relógio, YouTube, Assistente, Agenda, etc.), executa três níveis de isolamento:
//     - `pm disable-user --user 0 <pkg>` (desativa para o usuário atual)
//     - `pm suspend <pkg>` (suspende execução de background)
//     - `pm uninstall -k --user 0 <pkg>` (remove o binário do espaço de
//     usuário)
//  3. Preservação Crítica: Preserva intencionalmente o seletor nativo do
//  sistema
//     DocumentsUI (Storage Access Framework) para permitir a importação do ZIP.
// =============================================================================
int V7_Etapa1_5(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.5] Desativando e removendo pacotes (Google, Agenda, "
       "Assistente, Calculadora, Chrome, Contatos, Drive, Emergência, Files, "
       "Galeria, TV, Gravador, Maps, Meet, Play Store, Relógio, YouTube, "
       "Câmeras)...");

  char cmd_batch[4096];
  int count_in_batch = 0;
  int offset = snprintf(cmd_batch, sizeof(cmd_batch), "-s %s shell \"", serial);
  if (offset < 0 || offset >= (int)sizeof(cmd_batch))
    return PIPELINE_ERROR;

  for (int i = 0; i < V7_NUM_DEBLOAT_PACKAGES; i++) {
    const char *pkg = V7_DEBLOAT_PACKAGES[i];
    offset += snprintf(cmd_batch + offset, sizeof(cmd_batch) - offset,
                       "pm disable-user --user 0 %s 2>/dev/null; pm suspend %s "
                       "2>/dev/null; pm uninstall -k --user 0 %s 2>/dev/null; ",
                       pkg, pkg, pkg);
    count_in_batch++;

    if (count_in_batch >= 6 || i == V7_NUM_DEBLOAT_PACKAGES - 1) {
      if (offset < (int)sizeof(cmd_batch) - 2) {
        cmd_batch[offset++] = '\"';
        cmd_batch[offset] = '\0';
      }
      char *out_dis = adb_exec(cmd_batch);
      if (out_dis)
        free(out_dis);

      // Reset para o próximo lote
      offset = snprintf(cmd_batch, sizeof(cmd_batch), "-s %s shell \"", serial);
      count_in_batch = 0;
    }
  }

  if (cb)
    cb("[+] Desativação e remoção dos pacotes concluída com sucesso!");
  // ..: DevMark: Antigravity IDE :..
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 1.6] PURGA DO ARMAZENAMENTO INTERNO E UPLOAD DE ARQUIVOS (APK + ZIP)
// =============================================================================
//  O que esta etapa faz:
//  1. Limpeza Seletiva: Apaga todos os arquivos e pastas da raiz do `/sdcard/`,
//     preservando rigorosamente as pastas do sistema `Android` e `Pictures`
//     (necessária para receber o wallpaper municipal customizado).
//  2. Criação da Estrutura de Pastas: Cria os diretórios de destino da
//  aplicação:
//     `/sdcard/innovava`, `/sdcard/InnovAva` e `/sdcard/avaliacoes/`.
//  3. Upload Seguro (ADB Push): Transmite o APK instalador e o Pacote ZIP de
//     avaliações para as três pastas estruturadas, garantindo compatibilidade
//     total com diferentes versões de compilação da aplicação Android.
// =============================================================================
int V7_Etapa1_6(const char *serial, const char *apk_path,
                const char *pacote_path, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.6] Formatando /sdcard (exceto Android e Pictures) e "
       "copiando arquivos...");

  char args[1024];

  // 1. Limpeza preservando 'Android' e 'Pictures' (necessaria para wallpaper)
  snprintf(args, sizeof(args),
           "-s %s shell \"cd /sdcard && ls | grep -vwE "
           "'Android|Pictures|innovava|InnovAva|avaliacoes' | "
           "xargs rm -rf\"",
           serial);
  char *out_limpeza = adb_exec(args);
  if (out_limpeza)
    free(out_limpeza);

  // Garante que a pasta Pictures/Screenshots existe para receber o wallpaper
  snprintf(args, sizeof(args),
           "-s %s shell mkdir -p /sdcard/Pictures/Screenshots", serial);
  char *out_mkpic = adb_exec(args);
  if (out_mkpic)
    free(out_mkpic);

  // 2. Criar as pastas innovava, InnovAva e avaliacoes
  snprintf(args, sizeof(args),
           "-s %s shell \"mkdir -p /sdcard/innovava /sdcard/InnovAva "
           "/sdcard/avaliacoes && rm -rf /sdcard/innovava/* /sdcard/InnovAva/* "
           "/sdcard/avaliacoes/*\"",
           serial);
  char *out_mkdir = adb_exec(args);
  if (out_mkdir)
    free(out_mkdir);

  // 3. Fazer o PUSH do APK
  char safe_apk[MAX_PATH] = {0};
  char safe_pacote[MAX_PATH] = {0};
  if (apk_path && strlen(apk_path) > 0) {
    adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
    if (cb)
      cb("  -> Transmitindo APK para /sdcard/innovava/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial,
             safe_apk);
    char *out_push1 = adb_exec(args);
    if (out_push1)
      free(out_push1);

    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/InnovAva/", serial,
             safe_apk);
    out_push1 = adb_exec(args);
    if (out_push1)
      free(out_push1);

    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/avaliacoes/",
             serial, safe_apk);
    out_push1 = adb_exec(args);
    if (out_push1)
      free(out_push1);
  }

  // 4. Fazer o PUSH do Pacote ZIP
  if (pacote_path && strlen(pacote_path) > 0) {
    adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
    if (cb)
      cb("  -> Transmitindo PACOTE ZIP para /sdcard/innovava/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial,
             safe_pacote);
    char *out_push2 = adb_exec(args);
    if (out_push2)
      free(out_push2);

    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/InnovAva/", serial,
             safe_pacote);
    out_push2 = adb_exec(args);
    if (out_push2)
      free(out_push2);

    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/avaliacoes/",
             serial, safe_pacote);
    out_push2 = adb_exec(args);
    if (out_push2)
      free(out_push2);
  }

  if (cb)
    cb("[+] Armazenamento do Positivo Vision 7 (pasta /sdcard/innovava) "
       "preparado com sucesso!");

  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 2] INSTALAÇÃO SILENCIOSA DO APLICATIVO PRINCIPAL (APK)
// =============================================================================
//  O que esta etapa faz:
//  1. Validação Prévia: Checa se o pacote alvo já está instalado no sistema
//     via `pm list packages`. Se encontrado, executa a desinstalação prévia
//     limpa (`adb uninstall`) para evitar conflitos de cache ou assinaturas.
//  2. Estratégia de Instalação de Alta Velocidade (Push + PM Install):
//     Transmite o APK para o armazenamento temporário
//     `/data/local/tmp/app_install.apk` e dispara o instalador de pacotes local
//     `pm install --user 0`. Esta abordagem elimina os erros de stream timeout
//     comuns em cabos USB longos.
//  3. Fallback: Se o `pm install` falhar, aciona `adb install --user 0` direto.
//  4. Otimização de Performance e Background: Concede permissão
//  `RUN_IN_BACKGROUND`
//     via AppOps e insere o pacote na lista branca do Doze Mode do Android
//     (`dumpsys deviceidle whitelist +<pkg>`), garantindo que o app não seja
//     morto.
// =============================================================================
int V7_Etapa2(const char *serial, const char *apk_path,
              const char *package_name, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 2] Verificando instalacao do aplicativo...");

  if (package_name == NULL || strlen(package_name) == 0) {
    if (cb)
      cb("[ERROR] Nome do pacote não fornecido!");
    return PIPELINE_ERROR;
  }

  char args[1024];

  // Verifica se o pacote já está instalado
  snprintf(args, sizeof(args), "-s %s shell \"pm list packages | grep %s\"",
           serial, package_name);
  char *check_out = adb_exec(args);
  int is_installed = 0;

  if (check_out) {
    if (strstr(check_out, package_name) != NULL) {
      is_installed = 1;
    }
    free(check_out);
  }

  if (is_installed) {
    if (cb)
      cb("[-] O aplicativo antigo foi encontrado. Desinstalando para garantir "
         "versao limpa...");
    snprintf(args, sizeof(args), "-s %s uninstall %s", serial, package_name);
    char *out_un = adb_exec(args);
    if (out_un)
      free(out_un);
  }

  if (cb)
    cb("[-] Iniciando copia e instalacao do novo APK...");

  if (apk_path == NULL || strlen(apk_path) == 0) {
    if (cb)
      cb("[ERROR] Caminho do APK nao fornecido para instalacao!");
    return PIPELINE_ERROR;
  }

  char safe_apk[MAX_PATH] = {0};
  adb_get_safe_path(apk_path, safe_apk, MAX_PATH);

  int install_success = 0;

  // Metodo primario: push + pm install via shell (evita Failure [null] do
  // streamed install)
  if (cb)
    cb("[-] Enviando APK ao dispositivo via push e instalando via pm "
       "install...");

  snprintf(args, sizeof(args),
           "-s %s push \"%s\" /data/local/tmp/app_install.apk", serial,
           safe_apk);
  char *out_push = adb_exec(args);
  if (out_push)
    free(out_push);

  snprintf(args, sizeof(args),
           "-s %s shell \"pm install --user 0 /data/local/tmp/app_install.apk "
           "&& rm -f /data/local/tmp/app_install.apk\"",
           serial);

#undef adb_exec
  char *out = adb_exec(args);
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

  if (out && strstr(out, "Success")) {
    install_success = 1;
  }

  // Fallback: adb install direto sem -r (instalacao limpa caso pm install
  // falhe)
  if (!install_success) {
    if (cb)
      cb("[-] pm install falhou. Tentando via adb install direto...");
    // Libera saída do pm install antes do fallback
    if (out) {
      free(out);
      out = NULL;
    }

    snprintf(args, sizeof(args), "-s %s install --user 0 \"%s\"", serial,
             safe_apk);
    char *out2 = adb_exec(args);
    if (out2) {
      if (strstr(out2, "Success")) {
        install_success = 1;
        free(out2);
        out2 = NULL;
      } else {
        // Guarda mensagem de erro para exibição posterior
        out = out2;
        out2 = NULL;
      }
    }
  }

  if (install_success) {
    if (cb)
      cb("[-] APK instalado com sucesso!");

    if (cb)
      cb("[-] Removendo restricoes de background para o InnovAva...");
    char args_bypass[1024];
    snprintf(args_bypass, sizeof(args_bypass),
             "-s %s shell appops set %s RUN_IN_BACKGROUND allow", serial,
             package_name);
    char *out_bp = adb_exec(args_bypass);
    if (out_bp)
      free(out_bp);

    snprintf(args_bypass, sizeof(args_bypass),
             "-s %s shell dumpsys deviceidle whitelist +%s", serial,
             package_name);
    out_bp = adb_exec(args_bypass);
    if (out_bp)
      free(out_bp);

    if (out)
      free(out);
    return PIPELINE_SUCCESS;
  } else {
    if (cb) {
      char err_msg[1024];
      if (out) {
        for (int i = 0; out[i] != '\0'; i++) {
          if (out[i] == '\r' || out[i] == '\n') {
            out[i] = ' ';
          }
        }
        snprintf(err_msg, sizeof(err_msg),
                 "[ERROR] Falha na instalacao. Retorno ADB: %s", out);
      } else {
        snprintf(err_msg, sizeof(err_msg),
                 "[ERROR] Falha na instalacao do APK.");
      }
      cb(err_msg);
    }
    if (out)
      free(out);
    return PIPELINE_ERROR;
  }
}

// =============================================================================
//  [ETAPA 2.5] ESTABELECIMENTO DE CONEXÃO REVERSE TETHERING (GNIREHTET USB)
// =============================================================================
//  O que esta etapa faz:
//  1. Verificação e Instalação da APK Gnirehtet: Checa se o cliente de VPN
//     `com.genymobile.gnirehtet` está presente. Se ausente ou desatualizado,
//     instala com permissão global (`-g`).
//  2. Bypass de Permissão VPN via AppOps: Concede silenciosamente a autorização
//     `ACTIVATE_VPN` e `RUN_IN_BACKGROUND` sem disparar diálogos visuais na
//     tela.
//  3. Whitelist e Desativação do AWBMS: Adiciona o serviço à lista branca do
//     Doze Mode e desativa o gerenciador de background proprietário AllWinner
//     (`settings put global awbms_enabled 0`).
//  4. Inicialização do Servidor Relay e Túnel: Mata instâncias residuais na
//  porta
//     31416, sobe o executável nativo `gnirehtet relay` no Windows e ativa o
//     túnel no tablet (`gnirehtet start <serial>`), fornecendo acesso à
//     internet do PC através do cabo USB de forma 100% cabeada.
// =============================================================================
int V7_Etapa2_5(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 2.5] Inicializando Reverse Tethering (Gnirehtet)...");

  char exe_path[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path, MAX_PATH);
  char *last_slash = strrchr(exe_path, '\\');
  if (last_slash)
    *last_slash = '\0';

  char gnirehtet_path[MAX_PATH];
  char gnirehtet_apk[MAX_PATH];
  char gnirehtet_dir[MAX_PATH];

  // Tenta achar no diretório Release
  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering",
           exe_path);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    // Se não achou, fallback para diretório de Dev
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
             "%s\\..\\..\\tools\\reverse_tethering", exe_path);
  }

  // Canonicaliza o diretorio para evitar problemas no CreateProcessA com
  // caminhos relativos
  char full_gnirehtet_dir[MAX_PATH];
  GetFullPathNameA(gnirehtet_dir, MAX_PATH, full_gnirehtet_dir, NULL);
  strncpy(gnirehtet_dir, full_gnirehtet_dir, sizeof(gnirehtet_dir) - 1);

  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe",
           gnirehtet_dir);
  snprintf(gnirehtet_apk, sizeof(gnirehtet_apk), "%s\\gnirehtet.apk",
           gnirehtet_dir);

  char adb_path[MAX_PATH];
  adb_get_path(adb_path, sizeof(adb_path));
  SetEnvironmentVariableA("ADB", adb_path);

  if (cb)
    cb("  -> Verificando status do cliente Gnirehtet...");
  char args_check[1024];
  snprintf(args_check, sizeof(args_check),
           "-s %s shell pm list packages com.genymobile.gnirehtet", serial);
  char *out_check = adb_exec(args_check);
  int is_installed = 0;
  if (out_check) {
    if (strstr(out_check, "package:com.genymobile.gnirehtet")) {
      is_installed = 1;
    }
    free(out_check);
  }

  if (is_installed) {
    if (cb)
      cb("  -> Cliente Gnirehtet ja esta instalado e atualizado. Pulando "
         "instalacao.");
  } else {
    if (cb)
      cb("  -> Limpando resquicios de VPN antiga (Downgrade Fix)...");
    char args_un[1024];
    snprintf(args_un, sizeof(args_un),
             "-s %s uninstall com.genymobile.gnirehtet", serial);
    char *out_un = adb_exec(args_un);
    if (out_un)
      free(out_un);

    if (cb)
      cb("  -> Instalando cliente Gnirehtet no dispositivo...");
    char args_apk[1024];
    snprintf(args_apk, sizeof(args_apk), "-s %s install -r -g \"%s\"", serial,
             gnirehtet_apk);

    volatile int is_gnirehtet_installing = 1;
    V7_GnirehtetProgressData pdata = {cb, &is_gnirehtet_installing};
    HANDLE hThread =
        CreateThread(NULL, 0, V7_GnirehtetProgressBarThread, &pdata, 0, NULL);

#undef adb_exec
    char *out_apk = adb_exec(args_apk);
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

    is_gnirehtet_installing = 0;
    if (hThread) {
      WaitForSingleObject(hThread, 2000);
      CloseHandle(hThread);
    }

    if (out_apk)
      free(out_apk);
  }

  if (cb)
    cb("  -> Autorizando VPN silenciosamente (AppOps Bypass)...");
  char args_op[1024];
  snprintf(args_op, sizeof(args_op),
           "-s %s shell appops set com.genymobile.gnirehtet ACTIVATE_VPN allow",
           serial);
  char *out_op = adb_exec(args_op);
  if (out_op)
    free(out_op);

  if (cb)
    cb("  -> Removendo restricoes de bateria e background do Gnirehtet...");

  // Permite rodar em background irrestrito
  snprintf(
      args_op, sizeof(args_op),
      "-s %s shell appops set com.genymobile.gnirehtet RUN_IN_BACKGROUND allow",
      serial);
  out_op = adb_exec(args_op);
  if (out_op)
    free(out_op);

  // Coloca na whitelist de otimizacao de bateria (Ignorar Doze Mode)
  snprintf(args_op, sizeof(args_op),
           "-s %s shell dumpsys deviceidle whitelist +com.genymobile.gnirehtet",
           serial);
  out_op = adb_exec(args_op);
  if (out_op)
    free(out_op);

  if (cb)
    cb("  -> Desativando AWBMS (AllWinner Background Manager) via Settings...");
  snprintf(args_op, sizeof(args_op),
           "-s %s shell settings put global awbms_enabled 0", serial);
  out_op = adb_exec(args_op);
  if (out_op)
    free(out_op);

  snprintf(args_op, sizeof(args_op),
           "-s %s shell settings put global awbms_disable 1", serial);
  out_op = adb_exec(args_op);
  if (out_op)
    free(out_op);

  // Matar relays antigos para evitar conflito de porta 31416
  if (cb)
    cb("  -> Encerrando processos Gnirehtet anteriores...");

  STARTUPINFOA si_kill;
  PROCESS_INFORMATION pi_kill;
  ZeroMemory(&si_kill, sizeof(si_kill));
  si_kill.cb = sizeof(si_kill);
  ZeroMemory(&pi_kill, sizeof(pi_kill));

  char cmd_kill[] = "taskkill /F /IM gnirehtet.exe /T";
  if (CreateProcessA(NULL, cmd_kill, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL,
                     NULL, &si_kill, &pi_kill)) {
    WaitForSingleObject(pi_kill.hProcess, 3000);
    CloseHandle(pi_kill.hProcess);
    CloseHandle(pi_kill.hThread);
  }

  Sleep(1000);

  // SetEnvironmentVariableA ja foi chamado acima com ADB=<caminho absoluto>.
  // Ao usar lpEnvironment=NULL, o processo filho herda TODO o ambiente do pai,
  // incluindo PATH e a variavel ADB que acabamos de injetar. Isso replica
  // exatamente o comportamento do PowerShell ($env:ADB = "...").

  char relay_cmd[1024];
  snprintf(relay_cmd, sizeof(relay_cmd), "\"%s\" relay", gnirehtet_path);
  STARTUPINFOA si_relay;
  PROCESS_INFORMATION pi_relay;
  ZeroMemory(&si_relay, sizeof(si_relay));
  si_relay.cb = sizeof(si_relay);
  ZeroMemory(&pi_relay, sizeof(pi_relay));
  if (cb)
    cb("  -> Iniciando servidor Relay (porta 31416)...");
  if (CreateProcessA(NULL, relay_cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL,
                     gnirehtet_dir, &si_relay, &pi_relay)) {
    CloseHandle(pi_relay.hProcess);
    CloseHandle(pi_relay.hThread);
  } else {
    if (cb)
      cb("[WARNING] Falha ao criar processo do Relay.");
  }

  Sleep(3000); // Aguarda o relay subir na porta 31416

  char cmdline[1024];
  snprintf(cmdline, sizeof(cmdline), "\"%s\" start %s", gnirehtet_path, serial);

  STARTUPINFOA si;
  PROCESS_INFORMATION pi;
  ZeroMemory(&si, sizeof(si));
  si.cb = sizeof(si);
  ZeroMemory(&pi, sizeof(pi));

  if (cb)
    cb("  -> Ativando VPN no dispositivo...");
  if (CreateProcessA(NULL, cmdline, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL,
                     gnirehtet_dir, &si, &pi)) {
    WaitForSingleObject(pi.hProcess, 10000); // Aguarda start concluir
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    if (cb)
      cb("[-] Tunel Gnirehtet ATIVO. Internet no tablet via USB.");
  } else {
    if (cb)
      cb("[WARNING] Falha ao iniciar Gnirehtet. Verifique "
         "tools/reverse_tethering.");
  }

  Sleep(2000); // Aguarda VPN estabilizar no tablet
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 3] LIBERAÇÃO DE PERMISSÕES DE RUNTIME E ABERTURA DO APLICATIVO
// =============================================================================
//  O que esta etapa faz:
//  1. Concessão em Lote: Executa `pm grant` silenciosamente para as permissões
//     essenciais da aplicação (Câmera, Leitura/Escrita de Armazenamento e
//     Localização Fina/Grosseira).
//  2. Inicialização em Primeiro Plano (Monkey Launch): Utiliza o utilitário
//     `monkey -p <pkg> -c android.intent.category.LAUNCHER 1` para disparar a
//     Activity inicial diretamente, contornando restrições de inicialização.
// =============================================================================
int V7_Etapa3(const char *serial, const char *package_name, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 3] Liberando permissoes e abrindo aplicativo...");

  if (package_name == NULL || strlen(package_name) == 0) {
    if (cb)
      cb("[ERROR] Nome do pacote nao fornecido!");
    return PIPELINE_ERROR;
  }

  char args[512];

  // Lista de permissoes padrao em lote batched
  snprintf(args, sizeof(args),
           "-s %s shell \"pm grant %s android.permission.CAMERA && "
           "pm grant %s android.permission.READ_EXTERNAL_STORAGE && "
           "pm grant %s android.permission.WRITE_EXTERNAL_STORAGE && "
           "pm grant %s android.permission.ACCESS_FINE_LOCATION && "
           "pm grant %s android.permission.ACCESS_COARSE_LOCATION\"",
           serial, package_name, package_name, package_name, package_name,
           package_name);
  char *out = adb_exec(args);
  if (out)
    free(out);

  if (cb)
    cb("[-] Permissoes vitais concedidas silenciosamente em lote.");

  // Forcar abertura do aplicativo (Monkey Launch)
  snprintf(args, sizeof(args),
           "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1",
           serial, package_name);
  char *out2 = adb_exec(args);
  if (out2)
    free(out2);

  if (cb)
    cb("[-] Aplicativo executado e em primeiro plano.");
  return PIPELINE_SUCCESS;
}

// --- Helpers de busca em XML utilizando adb_get_centroid_fuzzy ---
static int get_centroid_from_xml_fuzzy(const char *xml, const char *search_str,
                                       int *out_cx, int *out_cy,
                                       int skip_count) {
  return adb_get_centroid_fuzzy(xml, search_str, out_cx, out_cy, skip_count);
}

int get_centroid_from_xml(const char *xml, const char *search_str, int *out_cx,
                          int *out_cy, int skip_count) {
  return adb_get_centroid_fuzzy(xml, search_str, out_cx, out_cy, skip_count);
}

// --- Helper: Extrai centroide da ULTIMA ocorrencia de uma tag XML ---
static int get_last_centroid_from_xml(const char *xml, const char *search_str,
                                      int *out_cx, int *out_cy) {
  if (!xml || !search_str)
    return 0;
  char *p = (char *)xml;
  int found = 0;
  while ((p = strstr(p, search_str)) != NULL) {
    char *b_start = strstr(p, "bounds=\"[");
    if (b_start) {
      int x1, y1, x2, y2;
      if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) ==
          4) {
        if (out_cx)
          *out_cx = (x1 + x2) / 2;
        if (out_cy)
          *out_cy = (y1 + y2) / 2;
        found = 1;
      }
    }
    p++; // Move forward
  }
  return found;
}

// --- Helper: Retorna o diretorio absoluto do executavel (sem trailing slash)
// ---
static void get_exe_dir(char *out_dir, int max_len) {
  GetModuleFileNameA(NULL, out_dir, max_len);
  char *last = strrchr(out_dir, '\\');
  if (last)
    *last = '\0';
}

// --- Helper: Faz o Dump e Retorna o XML em Memoria RAM (Fast-Path StdOut) ---
char *dump_and_read_xml(const char *serial, LogCallback cb) {
  // 1. Fast-Path: Tenta captura em memoria RAM via StdOut pipe (~100ms)
  char *mem_xml = adb_dump_stdout(serial);
  if (mem_xml) {
    return mem_xml;
  }

  // 2. Fallback: Captura via arquivo temporario em disco se StdOut falhar
  char exe_dir[MAX_PATH];
  get_exe_dir(exe_dir, MAX_PATH);

  // Caminho absoluto para o arquivo de dump local isolado por serial
  char local_xml[MAX_PATH];
  snprintf(local_xml, sizeof(local_xml), "%s\\window_dump_%s.xml", exe_dir,
           serial);

  char adb_args[512];
  snprintf(adb_args, sizeof(adb_args),
           "-s %s shell uiautomator dump /sdcard/window_dump_%s.xml", serial,
           serial);
  char *dump_out = adb_exec(adb_args);
  if (dump_out)
    free(dump_out);

  snprintf(adb_args, sizeof(adb_args),
           "-s %s pull /sdcard/window_dump_%s.xml \"%s\"", serial, serial,
           local_xml);
  char *pull_out = adb_exec(adb_args);
  if (pull_out)
    free(pull_out);

  FILE *f = fopen(local_xml, "rb");
  if (!f) {
    if (cb)
      cb("[ERROR] dump_and_read_xml: falha ao obter XML de tela.");
    return NULL;
  }

  fseek(f, 0, SEEK_END);
  long fsize = ftell(f);
  fseek(f, 0, SEEK_SET);

  char *xml = (char *)malloc(fsize + 1);
  if (xml) {
    fread(xml, fsize, 1, f);
    xml[fsize] = 0;
  }
  fclose(f);
  return xml;
}

static void stop_gnirehtet_nosleep(const char *serial, LogCallback cb) {
  (void)cb; // Funcao silenciosa: nao emite logs, apenas encerra o processo
  char cmd_stop[1024];
  char exe_path2[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path2, MAX_PATH);
  char *last_slash2 = strrchr(exe_path2, '\\');
  if (last_slash2)
    *last_slash2 = '\0';

  char gnirehtet_path[MAX_PATH];
  char gnirehtet_dir[MAX_PATH];

  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering",
           exe_path2);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
             "%s\\..\\..\\tools\\reverse_tethering", exe_path2);
  }

  char full_gnirehtet_dir[MAX_PATH];
  GetFullPathNameA(gnirehtet_dir, MAX_PATH, full_gnirehtet_dir, NULL);

  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe",
           full_gnirehtet_dir);

  snprintf(cmd_stop, sizeof(cmd_stop), "\"%s\" stop %s", gnirehtet_path,
           serial);

  STARTUPINFOA si_stop;
  PROCESS_INFORMATION pi_stop;
  ZeroMemory(&si_stop, sizeof(si_stop));
  si_stop.cb = sizeof(si_stop);
  ZeroMemory(&pi_stop, sizeof(pi_stop));
  if (CreateProcessA(NULL, cmd_stop, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL,
                     NULL, &si_stop, &pi_stop)) {
    CloseHandle(pi_stop.hProcess);
    CloseHandle(pi_stop.hThread);
  }
}

static int ensure_device_internet(const char *serial, LogCallback cb) {
  if (cb)
    cb("[-] Verificando conexao com a internet via tunel...");

  int max_attempts = 3;
  for (int attempt = 1; attempt <= max_attempts; attempt++) {
    char args[256];
    snprintf(args, sizeof(args), "-s %s shell ping -c 1 -w 2 8.8.8.8", serial);
    char *out = adb_exec(args);
    int connected = 0;
    if (out) {
      if (strstr(out, "1 received") ||
          strstr(out, "1 packets transmitted, 1 received")) {
        connected = 1;
      }
      free(out);
    }

    if (connected) {
      if (cb)
        cb("[-] Conexao com a internet via tunel ativa e estavel.");
      return 1;
    }

    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg),
               "[WARNING] Sem internet no dispositivo. Tentativa %d/%d. "
               "Reiniciando tunel...",
               attempt, max_attempts);
      cb(msg);
    }

    stop_gnirehtet_nosleep(serial, cb);
    Sleep(1000);

    if (V7_Etapa2_5(serial, cb) != PIPELINE_SUCCESS) {
      if (cb)
        cb("[WARNING] Falha ao reiniciar o motor do Gnirehtet.");
    }

    Sleep(4000);
  }

  if (cb)
    cb("[ERROR] Nao foi possivel estabelecer internet via tunel apos as "
       "tentativas.");
  return 0;
}

// =============================================================================
//  [ETAPA 4] AUTENTICAÇÃO E LOGIN AUTOMÁTICO (INNOLEARN / SCAN DUAL EM TEMPO
//  REAL)
// =============================================================================
//  O que esta etapa faz:
//  1. Validação de Lockscreen: Envia evento KEYCODE_MENU (82) e swipe de
//  segurança
//     para garantir que a tela de bloqueio não cubra o aplicativo.
//  2. Seleção Inteligente de Município: Utiliza o InnoLearn para localizar a
//  gaveta/spinner
//     de municípios no layout XML. Se o município já estiver selecionado, pula
//     a etapa; caso contrário, abre a gaveta, localiza o nome do município (com
//     tratamento UTF-8) e clica nas coordenadas centróides exatas.
//  3. Injeção de E-mail com Foto Dinâmica: Faz o dump XML fresco da tela,
//  localiza
//     o campo de texto de login/e-mail, clica, injeta o texto com escape seguro
//     de caracteres e fecha o teclado virtual do Android (IME) para desobstruir
//     a visão.
//  4. Injeção de Senha: Tira uma nova "foto" (dump XML) da tela, localiza o
//  campo
//     de senha via InnoLearn, toca, digita a senha mascarada e esconde o
//     teclado.
//  5. Validação Prévia e Clique em Entrar: Verifica se o município correto
//  permanece
//     selecionado na tela antes do clique. Localiza o botão 'Entrar' e aciona o
//     clique centróide, autenticando a aplicação com o backend via túnel
//     Gnirehtet.
// =============================================================================
int V7_Etapa4(const char *serial, const char *login, const char *senha,
              const char *municipio, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 4] Lendo a tela e calculando pixels (UIAutomator) "
       "Dual-Scan...");

  if (!login || !senha || strlen(login) == 0 || strlen(senha) == 0) {
    if (cb)
      cb("[ERROR] Credenciais invalidas ou nao fornecidas!");
    return PIPELINE_ERROR;
  }

  char args[512];

  // Garantia de desbloqueio suave
  if (cb)
    cb("[-] [Etapa4] Garantindo que o lockscreen nao esta na frente do app...");
  snprintf(args, sizeof(args), "-s %s shell input keyevent 82",
           serial); // KEYCODE_MENU = desbloqueio
  char *unlock1 = adb_exec(args);
  if (unlock1)
    free(unlock1);
  Sleep(500);
  snprintf(args, sizeof(args), "-s %s shell input swipe 300 900 300 300 400",
           serial);
  char *unlock2 = adb_exec(args);
  if (unlock2)
    free(unlock2);
  Sleep(800);

  // Aguarda o app terminar de abrir a tela inicial (Splash screen)
  Sleep(4000);

  if (cb)
    cb("[-] [InnoLearn] SCAN 1: Procurando gaveta de Municipio...");
  char *xml1 = dump_and_read_xml(serial, cb);

  if (xml1) {
    // DUMP DEBUG para que possamos analisar a arvore se falhar
    // Usa caminho absoluto para garantir que a pasta logs/ seja encontrada
    char exe_dir_dbg[MAX_PATH];
    get_exe_dir(exe_dir_dbg, MAX_PATH);
    char dbg_xml_path[MAX_PATH];
    snprintf(dbg_xml_path, sizeof(dbg_xml_path),
             "%s\\logs\\window_dump_debug.xml", exe_dir_dbg);
    FILE *debug_f = fopen(dbg_xml_path, "w");
    if (debug_f) {
      fwrite(xml1, 1, strlen(xml1), debug_f);
      fclose(debug_f);
      if (cb)
        cb("[-] [DEBUG] XML salvo em logs/window_dump_debug.xml");
    }

    int cx = 0, cy = 0;

    // Limpa possÃ­veis quebras de linha (\r\n) vindas do CSV do C#
    char safe_municipio[128];
    strncpy(safe_municipio, municipio, sizeof(safe_municipio) - 1);
    safe_municipio[sizeof(safe_municipio) - 1] = '\0';
    int mlen = strlen(safe_municipio);
    while (mlen > 0 && (safe_municipio[mlen - 1] == '\r' ||
                        safe_municipio[mlen - 1] == '\n' ||
                        safe_municipio[mlen - 1] == ' ')) {
      safe_municipio[mlen - 1] = '\0';
      mlen--;
    }

    // Converte o nome do municipio para UTF-8 de forma segura (C# envia UTF-8
    // via LPUTF8Str)
    char utf8_municipio[256];
    WCHAR w_municipio[128];
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, safe_municipio, -1,
                            w_municipio, 128) != 0) {
      strncpy(utf8_municipio, safe_municipio, sizeof(utf8_municipio) - 1);
      utf8_municipio[sizeof(utf8_municipio) - 1] = '\0';
    } else {
      MultiByteToWideChar(CP_ACP, 0, safe_municipio, -1, w_municipio, 128);
      WideCharToMultiByte(CP_UTF8, 0, w_municipio, -1, utf8_municipio, 256,
                          NULL, NULL);
    }

    // Procura primeiro se o municÃ­pio alvo JÃ  estÃ¡ selecionado na tela
    if (get_centroid_from_xml_fuzzy(xml1, utf8_municipio, &cx, &cy, 0)) {
      if (cb)
        cb("[-] Municipio alvo ja esta selecionado! Pulando interacao de "
           "gaveta.");
    } else {
      // --- InnoLearn: busca o spinner com estratégias aprendidas V7 ---
      LearnFindResult res_mun =
          learn_find_element("v7_spinner_municipio", xml1, 0, cb);

      if (res_mun.found) {
        learn_record_hit("v7_spinner_municipio", res_mun.winning_strategy);
        if (cb)
          cb("[-] Gaveta encontrada (InnoLearn V7). Abrindo lista de "
             "municipios...");
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                 res_mun.cx, res_mun.cy);
        char *t1 = adb_exec(args);
        if (t1)
          free(t1);

        Sleep(1500); // Aguarda lista subir

        if (cb)
          cb("[-] [InnoLearn V7] SCAN 2: Cacando o municipio na lista...");
        char *xml2 = dump_and_read_xml(serial, cb);
        if (xml2) {
          if (get_centroid_from_xml_fuzzy(xml2, utf8_municipio, &cx, &cy, 0)) {
            if (cb)
              cb("[-] Municipio achado na lista! Selecionando...");
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                     cx, cy);
            char *t2 = adb_exec(args);
            if (t2)
              free(t2);
          } else {
            learn_record_miss("v7_spinner_municipio", res_mun.winning_strategy);
            if (cb)
              cb("[WARNING] O municipio alvo nao apareceu na lista visivel!");
            snprintf(args, sizeof(args), "-s %s shell input keyevent 4",
                     serial);
            char *tb = adb_exec(args);
            if (tb)
              free(tb);
          }
          free(xml2);
          Sleep(1000);
        }
      } else {
        if (cb)
          cb("[WARNING] Gaveta de municipio nao encontrada (todas estrategias "
             "falharam).");
      }
    }
    free(xml1);
  }

  // === NOVO: Bloco de credenciais com SCAN EM TEMPO REAL por etapa ===
  // Cada passo (email, senha, botao) tira sua propria "foto" da tela.
  // Isso elimina o problema de coordenadas velhas causadas pelo teclado/scroll.

  char args_local[512];
  int cx = 0, cy = 0;

  // Garante que a pasta logs/ exista no diretorio do exe
  char exe_dir_l[MAX_PATH];
  get_exe_dir(exe_dir_l, MAX_PATH);
  char logs_dir[MAX_PATH];
  snprintf(logs_dir, sizeof(logs_dir), "%s\\logs", exe_dir_l);
  CreateDirectoryA(logs_dir,
                   NULL); // Cria se nao existir (nao falha se ja existir)

  // --- PASSO 1: Injecao do E-mail (InnoLearn V7) ---
  if (cb)
    cb("[-] [InnoLearn V7] Procurando campo de Email...");
  {
    int max_attempts = 15;
    int attempt = 1;
    int found_email = 0;
    while (attempt <= max_attempts && !found_email) {
      char *xml_email = dump_and_read_xml(serial, cb);
      if (xml_email) {
        char dbg_path[MAX_PATH];
        snprintf(dbg_path, sizeof(dbg_path), "%s\\logs\\window_dump_email.xml",
                 exe_dir_l);
        FILE *dbg = fopen(dbg_path, "w");
        if (dbg) {
          fwrite(xml_email, 1, strlen(xml_email), dbg);
          fclose(dbg);
        }

        LearnFindResult res_email =
            learn_find_element("v7_campo_email", xml_email, 0, cb);
        if (res_email.found) {
          learn_record_hit("v7_campo_email", res_email.winning_strategy);
          learn_save_fallback_coord("v7_campo_email", res_email.cx,
                                    res_email.cy, 1);
          cx = res_email.cx;
          cy = res_email.cy;
          if (cb)
            cb("[-] Campo Email encontrado (InnoLearn V7). Clicando e "
               "digitando...");
          found_email = 1;
        } else {
          if (attempt == max_attempts) {
            if (cb)
              cb("[WARNING] Campo Email nao encontrado aposs limite. "
                 "Registrando miss.");
            LearnTarget *t_email = learn_db_find_target("v7_campo_email");
            if (t_email) {
              for (int _mi = 0; _mi < t_email->strategy_count; _mi++) {
                learn_record_miss("v7_campo_email",
                                  t_email->strategies[_mi].query);
              }
            }
          } else {
            if (cb)
              cb("[-] Campo Email nao encontrado. Tela carregando? Tentando "
                 "novamente...");
            Sleep(2000);
          }
        }
        free(xml_email);
      } else {
        Sleep(2000);
      }
      attempt++;
    }

    if (!found_email) {
      if (cb)
        cb("[ERROR] Nao foi possivel localizar o campo E-mail aposs varias "
           "tentativas.");
      return PIPELINE_ERROR;
    }

    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d",
             serial, cx, cy);
    char *t1 = adb_exec(args_local);
    if (t1)
      free(t1);
    char escaped_login[256];
    escape_adb_text(login, escaped_login);
    Sleep(1200);
    snprintf(args_local, sizeof(args_local), "-s %s shell input text '%s'",
             serial, escaped_login);
    char *t2 = adb_exec(args_local);
    if (t2)
      free(t2);
    Sleep(500);
    snprintf(args_local, sizeof(args_local),
             "-s %s shell \"dumpsys input_method | grep mInputShown=true && "
             "input keyevent 4 || input keyevent 111\"",
             serial);
    char *tk1 = adb_exec(args_local);
    if (tk1)
      free(tk1);
    Sleep(1000);
  }

  // --- PASSO 2: Injecao da Senha (InnoLearn V7) ---
  if (cb)
    cb("[-] [InnoLearn V7] Procurando campo de Senha...");
  {
    int max_attempts = 15;
    int attempt = 1;
    int found_senha = 0;
    while (attempt <= max_attempts && !found_senha) {
      char *xml_senha = dump_and_read_xml(serial, cb);
      if (xml_senha) {
        char dbg_path2[MAX_PATH];
        snprintf(dbg_path2, sizeof(dbg_path2),
                 "%s\\logs\\window_dump_senha.xml", exe_dir_l);
        FILE *dbg = fopen(dbg_path2, "w");
        if (dbg) {
          fwrite(xml_senha, 1, strlen(xml_senha), dbg);
          fclose(dbg);
        }

        LearnFindResult res_senha =
            learn_find_element("v7_campo_senha", xml_senha, 0, cb);
        if (res_senha.found) {
          learn_record_hit("v7_campo_senha", res_senha.winning_strategy);
          learn_save_fallback_coord("v7_campo_senha", res_senha.cx,
                                    res_senha.cy, 1);
          cx = res_senha.cx;
          cy = res_senha.cy;
          if (cb)
            cb("[-] Campo Senha encontrado (InnoLearn V7). Clicando e "
               "digitando...");
          found_senha = 1;
        } else {
          if (attempt == max_attempts) {
            if (cb)
              cb("[WARNING] Campo Senha nao encontrado aposs limite. "
                 "Registrando miss.");
            LearnTarget *t_senha = learn_db_find_target("v7_campo_senha");
            if (t_senha) {
              for (int _mi = 0; _mi < t_senha->strategy_count; _mi++) {
                learn_record_miss("v7_campo_senha",
                                  t_senha->strategies[_mi].query);
              }
            }
          } else {
            if (cb)
              cb("[-] Campo Senha nao encontrado. Tela carregando? Tentando "
                 "novamente...");
            Sleep(2000);
          }
        }
        free(xml_senha);
      } else {
        Sleep(2000);
      }
      attempt++;
    }

    if (!found_senha) {
      if (cb)
        cb("[ERROR] Nao foi possivel localizar o campo Senha aposs varias "
           "tentativas.");
      return PIPELINE_ERROR;
    }

    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d",
             serial, cx, cy);
    char *t3 = adb_exec(args_local);
    if (t3)
      free(t3);
    char escaped_senha[256];
    escape_adb_text(senha, escaped_senha);
    Sleep(1200);
    snprintf(args_local, sizeof(args_local), "-s %s shell input text '%s'",
             serial, escaped_senha);
    char *t4 = adb_exec(args_local);
    if (t4)
      free(t4);
    Sleep(500);
    snprintf(args_local, sizeof(args_local),
             "-s %s shell \"dumpsys input_method | grep mInputShown=true && "
             "input keyevent 4 || input keyevent 111\"",
             serial);
    char *tk2 = adb_exec(args_local);
    if (tk2)
      free(tk2);
    Sleep(1800);
  }

  // --- PASSO 3: Botao Entrar (InnoLearn V7) ---
  if (cb)
    cb("[-] [InnoLearn V7] Procurando botao Entrar...");
  {
    int max_attempts = 15;
    int attempt = 1;
    int found_btn = 0;
    while (attempt <= max_attempts && !found_btn) {
      char *xml_btn = dump_and_read_xml(serial, cb);
      if (xml_btn) {
        char dbg_path3[MAX_PATH];
        snprintf(dbg_path3, sizeof(dbg_path3), "%s\\logs\\window_dump_btn.xml",
                 exe_dir_l);
        FILE *dbg = fopen(dbg_path3, "w");
        if (dbg) {
          fwrite(xml_btn, 1, strlen(xml_btn), dbg);
          fclose(dbg);
        }

        // === VALIDAÇÃO PRÉVIA VIA XML: Confirma se o município alvo está
        // visível no seletor ===
        if (municipio && strlen(municipio) > 0) {
          if (get_centroid_from_xml_fuzzy(xml_btn, municipio, NULL, NULL, 0)) {
            if (cb) {
              char log_msg[256];
              snprintf(log_msg, sizeof(log_msg),
                       "  -> [Validação XML V7] OK: Município '%s' confirmado "
                       "no seletor!",
                       municipio);
              cb(log_msg);
            }
          } else {
            if (cb) {
              char log_msg[256];
              snprintf(log_msg, sizeof(log_msg),
                       "[WARNING] [Validação XML V7] Município '%s' NÃO "
                       "detectado no seletor! Re-executando seleção...",
                       municipio);
              cb(log_msg);
            }
            LearnFindResult res_retry =
                learn_find_element("v7_spinner_municipio", xml_btn, 0, cb);
            if (res_retry.found) {
              snprintf(args_local, sizeof(args_local),
                       "-s %s shell input tap %d %d", serial, res_retry.cx,
                       res_retry.cy);
              char *tr1 = adb_exec(args_local);
              if (tr1)
                free(tr1);
              Sleep(1500);
              char *xml_m_retry = dump_and_read_xml(serial, cb);
              if (xml_m_retry) {
                int mcx = 0, mcy = 0;
                if (get_centroid_from_xml_fuzzy(xml_m_retry, municipio, &mcx,
                                                &mcy, 0)) {
                  snprintf(args_local, sizeof(args_local),
                           "-s %s shell input tap %d %d", serial, mcx, mcy);
                  char *tr2 = adb_exec(args_local);
                  if (tr2)
                    free(tr2);
                  if (cb) {
                    char log_msg[256];
                    snprintf(log_msg, sizeof(log_msg),
                             "[+] [Validação XML V7] Município '%s' "
                             "re-selecionado com sucesso!",
                             municipio);
                    cb(log_msg);
                  }
                }
                free(xml_m_retry);
                Sleep(1000);
              }
            }
          }
        }

        cx = 0;
        cy = 0;
        LearnFindResult res_btn =
            learn_find_element("v7_botao_entrar", xml_btn, 0, cb);

        if (res_btn.found) {
          learn_record_hit("v7_botao_entrar", res_btn.winning_strategy);
          learn_save_fallback_coord("v7_botao_entrar", res_btn.cx, res_btn.cy,
                                    1);
          cx = res_btn.cx;
          cy = res_btn.cy;
          if (cb)
            cb("[-] Botao Entrar encontrado (InnoLearn V7). Clicando...");
          found_btn = 1;
        } else {
          if (attempt == max_attempts) {
            if (cb)
              cb("[WARNING] Botao Entrar nao encontrado aposs limite. "
                 "Registrando miss.");
            LearnTarget *t_btn = learn_db_find_target("v7_botao_entrar");
            if (t_btn) {
              for (int _mi = 0; _mi < t_btn->strategy_count; _mi++) {
                learn_record_miss("v7_botao_entrar",
                                  t_btn->strategies[_mi].query);
              }
            }
          } else {
            if (cb)
              cb("[-] Botao Entrar nao encontrado. Tela carregando? Tentando "
                 "novamente...");
            Sleep(2000);
          }
        }
        free(xml_btn);
      } else {
        Sleep(2000);
      }
      attempt++;
    }

    if (!found_btn) {
      if (cb)
        cb("[ERROR] Nao foi possivel localizar o botao Entrar aposs varias "
           "tentativas.");
      return PIPELINE_ERROR;
    }

    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d",
             serial, cx, cy);
    char *t5 = adb_exec(args_local);
    if (t5)
      free(t5);
    Sleep(1000);
  }

  // Limpa rastro
  char exe_dir_clean[MAX_PATH];
  get_exe_dir(exe_dir_clean, MAX_PATH);
  char local_xml_clean[MAX_PATH];
  snprintf(local_xml_clean, sizeof(local_xml_clean), "%s\\window_dump_%s.xml",
           exe_dir_clean, serial);
  remove(local_xml_clean);
  snprintf(args, sizeof(args), "-s %s shell rm /sdcard/window_dump_%s.xml",
           serial, serial);
  char *cln = adb_exec(args);
  if (cln)
    free(cln);

  if (cb)
    cb("[+] Login efetuado e credenciais injetadas com sucesso!");
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 5] NAVEGAÇÃO PARA IMPORTAÇÃO DO PACOTE AVALIATIVO (MENU LATERAL)
// =============================================================================
//  O que esta etapa faz:
//  1. Re-Wakeup de Segurança: Garante tela acesa e desbloqueada.
//  2. Abertura do Drawer de Navegação: Executa um swipe da borda esquerda para
//     a direita (0,500 -> 500,500) para expandir o menu lateral do InnovAva.
//  3. Detecção e Clique em 'Importar ZIP': Captura o XML de tela via dump em
//  RAM,
//     identifica as coordenadas centróides do texto 'Importar ZIP' e efetua o
//     toque.
//  4. Desativação de Redes: Imediatamente ao carregar a tela de importação,
//  desliga
//     o túnel Gnirehtet e a interface Wi-Fi para isolamento completo.
// =============================================================================
int V7_Etapa5(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 5] Garantindo tela acesa/desbloqueada e abrindo menu "
       "lateral...");

  // 0. Garantir desbloqueio de tela
  V7_Etapa1(serial, cb);
  char args[512];

  // Aguarda o login processar e a tela principal carregar
  Sleep(4000);

  if (cb)
    cb("[-] Abrindo menu lateral (Swipe da esquerda para direita)...");
  snprintf(args, sizeof(args), "-s %s shell input swipe 0 500 500 500 300",
           serial);
  char *swp = adb_exec(args);
  if (swp)
    free(swp);
  Sleep(2000); // Aguarda a animacao do menu lateral

  if (cb)
    cb("[-] Procurando a opcao 'Importar ZIP' no menu lateral...");
  char *xml_menu = dump_and_read_xml(serial, cb);
  if (xml_menu) {
    int cx = 0, cy = 0;
    if (get_centroid_from_xml(xml_menu, "Importar ZIP", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_menu, "IMPORTAR ZIP", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_menu, "Importar", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_menu, "IMPORTAR", &cx, &cy, 0)) {
      if (cb)
        cb("[-] Opcao 'Importar ZIP' localizada com precisao. Clicando...");
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
               cy);
      char *tp = adb_exec(args);
      if (tp)
        free(tp);
      Sleep(1500);

      // Desligar Gnirehtet e Wi-Fi imediatamente ao chegar em Importar ZIP
      if (cb)
        cb("[-] Tela de importacao aberta. Desligando tunel Gnirehtet e Wi-Fi "
           "imediatamente...");
      stop_gnirehtet_nosleep(serial, cb);
      snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
      char *out_w = adb_exec(args);
      if (out_w)
        free(out_w);

    } else {
      if (cb)
        cb("[WARNING] Opcao 'Importar ZIP' nao encontrada diretamente no menu "
           "lateral.");
    }
    free(xml_menu);
  }

  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 6] SELEÇÃO DO PACOTE ZIP NO SELETOR DE ARQUIVOS (DOCUMENTSUI / SAF)
// =============================================================================
//  O que esta etapa faz:
//  1. Acionamento do Seletor: Localiza o botão 'PROCURAR ARQUIVO .ZIP' na UI
//     e clica para abrir o gerenciador nativo de arquivos do Android.
//  2. Otimização Fast-Path: Verifica se o pacote `.zip` já está imediatamente
//     visível na pasta atual. Se encontrado, seleciona sem necessidade de
//     navegação.
//  3. Navegação Hierárquica no Storage Access Framework:
//     - Realiza swipe lateral para abrir a árvore de unidades do DocumentsUI.
//     - Clica na unidade de Armazenamento Interno ('T307G' / 'Armazenamento
//     interno').
//     - Localiza e entra na pasta `avaliacoes`.
//     - Clica no arquivo `.zip` da prova com cálculo de coordenadas centróides.
// =============================================================================
int V7_Etapa6(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 6] Garantindo desbloqueio e buscando o botao 'Procurar "
       "arquivo'...");

  // 0. Garantir desbloqueio de tela antes da selecao de arquivo
  V7_Etapa1(serial, cb);

  char args[512];
  int cx = 0, cy = 0;
  int btn_found = 0;

  // 1. Clicar no botão 'Procurar arquivo .zip' com busca precisa (sem clique
  // cego em Exportar)
  Sleep(2000); // Aguarda tela de importaçao
  for (int retry = 0; retry < 3; retry++) {
    char *xml_import = dump_and_read_xml(serial, cb);
    if (xml_import) {
      if (get_centroid_from_xml(xml_import, "PROCURAR ARQUIVO ZIP", &cx, &cy,
                                0) ||
          get_centroid_from_xml(xml_import, "PROCURAR ARQUIVO", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "Procurar arquivo", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "Procurar", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "PROCURAR", &cx, &cy, 0)) {

        if (cb)
          cb("[-] Botao 'Procurar arquivo' localizado. Clicando...");
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
                 cy);
        char *t1 = adb_exec(args);
        if (t1)
          free(t1);
        btn_found = 1;
        free(xml_import);
        break;
      }
      free(xml_import);
    }
    Sleep(1500);
  }

  if (!btn_found) {
    if (cb)
      cb("[WARNING] Botao 'Procurar arquivo' nao detectado no XML. "
         "Re-acendendo tela...");
    V7_Etapa1(serial, cb);
  }

  Sleep(3000); // Aguarda o gerenciador de arquivos nativo abrir

  // [FAST-PATH RESTAURADO E MELHORADO A PEDIDO]
  // O sistema verifica primeiro se ja esta na pasta avaliacoes e o arquivo zip
  // esta visivel.
  if (cb)
    cb("[-] Verificando se o pacote ZIP ja esta visivel na tela...");
  char *xml_fast = dump_and_read_xml(serial, cb);
  int fast_found = 0;
  if (xml_fast) {
    if (!strstr(xml_fast, "Procurar arquivo") &&
        !strstr(xml_fast, "PROCURAR ARQUIVO")) {
      if (get_last_centroid_from_xml(xml_fast, ".zip", &cx, &cy) ||
          get_last_centroid_from_xml(xml_fast, ".ZIP", &cx, &cy) ||
          get_last_centroid_from_xml(xml_fast, "pacote", &cx, &cy) ||
          get_last_centroid_from_xml(xml_fast, "zip", &cx, &cy) ||
          get_last_centroid_from_xml(xml_fast, "ZIP", &cx, &cy)) {

        if (cb)
          cb("[-] FAST-PATH: Arquivo ZIP detectado! Selecionando...");
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
                 cy);
        char *tzf = adb_exec(args);
        if (tzf)
          free(tzf);
        fast_found = 1;
      }
    }
    free(xml_fast);
  }

  if (fast_found) {
    if (cb)
      cb("[-] Importacao iniciada no Fast-Path direto da pasta.");
    return PIPELINE_SUCCESS;
  }

  // Se o Fast-Path falhar (esta em Recentes ou outra pasta), forca menu lateral
  // via Swipe
  if (cb)
    cb("[-] Pacote nao detectado na tela atual. Abrindo menu lateral "
       "(Swipe)...");

  // Realiza um swipe do canto esquerdo da tela (x=0) para a direita (x=800)
  snprintf(args, sizeof(args), "-s %s shell input swipe 0 500 800 500 200",
           serial);
  char *tswipe = adb_exec(args);
  if (tswipe)
    free(tswipe);
  Sleep(2000); // Aguarda o menu deslizar

  // 2. Procura "T307G" ou "Armazenamento interno" no menu lateral
  if (cb)
    cb("  -> Selecionando armazenamento interno do aparelho (T307G)...");
  char *xml_nav = dump_and_read_xml(serial, cb);
  if (xml_nav) {
    if (get_centroid_from_xml(xml_nav, "T307G", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_nav, "T307", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_nav, "Vision Tab 7", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_nav, "Vision Tab", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_nav, "Armazenamento", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_nav, "Internal storage", &cx, &cy, 0)) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
               cy);
      char *tnav = adb_exec(args);
      if (tnav)
        free(tnav);
    }
    free(xml_nav);
  }
  Sleep(2000); // Aguarda carregar a raiz do T307G

  // 3. Procura pasta "avaliacoes"
  if (cb)
    cb("  -> Procurando a pasta 'avaliacoes' na raiz...");
  char *xml_folder = dump_and_read_xml(serial, cb);
  if (xml_folder) {
    if (get_centroid_from_xml(xml_folder, "avaliacoes", &cx, &cy, 0) ||
        get_centroid_from_xml(xml_folder, "Avaliacoes", &cx, &cy, 0)) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
               cy);
      char *tf = adb_exec(args);
      if (tf)
        free(tf);
    } else {
      // Scroll down forte e tenta novamente
      snprintf(args, sizeof(args),
               "-s %s shell input swipe 300 900 300 200 400", serial);
      char *tswp = adb_exec(args);
      if (tswp)
        free(tswp);
      Sleep(1500);

      free(xml_folder);
      xml_folder = dump_and_read_xml(serial, cb);
      if (xml_folder &&
          (get_centroid_from_xml(xml_folder, "avaliacoes", &cx, &cy, 0) ||
           get_centroid_from_xml(xml_folder, "Avaliacoes", &cx, &cy, 0))) {
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
                 cy);
        char *tf = adb_exec(args);
        if (tf)
          free(tf);
      }
    }
    if (xml_folder)
      free(xml_folder);
  }
  Sleep(2000);

  // 4. Finalmente, procura o .zip na pasta avaliacoes e seleciona
  if (cb)
    cb("  -> Localizando o arquivo ZIP final no T307G...");
  char *xml_final = dump_and_read_xml(serial, cb);
  if (xml_final) {
    if (get_last_centroid_from_xml(xml_final, ".zip", &cx, &cy) ||
        get_last_centroid_from_xml(xml_final, ".ZIP", &cx, &cy) ||
        get_last_centroid_from_xml(xml_final, "pacote", &cx, &cy) ||
        get_last_centroid_from_xml(xml_final, "zip", &cx, &cy) ||
        get_last_centroid_from_xml(xml_final, "ZIP", &cx, &cy)) {
      if (cb)
        cb("[-] Arquivo ZIP encontrado na pasta! Selecionando...");
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
               cy);
      char *tzf = adb_exec(args);
      if (tzf)
        free(tzf);
    } else {
      // Fallback: se nao achar o nome, tenta o ultimo item clicavel
      if (get_last_centroid_from_xml(xml_final, "clickable=\"true\"", &cx,
                                     &cy)) {
        if (cb)
          cb("[WARNING] Nome exato nao detectado. Selecionando o ultimo item "
             "clicavel por seguranca.");
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx,
                 cy);
        char *tzf = adb_exec(args);
        if (tzf)
          free(tzf);
      } else {
        if (cb)
          cb("[ERROR] Nenhum arquivo clicavel encontrado dentro da pasta "
             "avaliacoes.");
      }
    }
    free(xml_final);
  }

  if (cb)
    cb("[-] Tunel mantido ATIVO para sincronizacao e importacao.");
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 7a] MONITORAMENTO DA IMPORTAÇÃO E FECHAMENTO FORÇADO DE APLICATIVOS
// =============================================================================
//  O que esta etapa faz:
//  1. Tempo Mínimo de Descompressão: Aguarda 5 segundos para que a rotina
//  interna
//     do InnovAva descompacte as avaliações no SQLite local.
//  2. Monitoramento de Reaparecimento: Varre o XML da tela a cada segundo até
//  detectar
//     o reaparecimento do botão 'PROCURAR ARQUIVO .ZIP', confirmando que a
//     extração terminou.
//  3. Encerramento de Segurança: Envia eventos Back (x3) e Home, executando em
//  seguida
//     `am force-stop` na aplicação principal e no DocumentsUI, finalizando com
//     `am kill-all`.
// =============================================================================
int F1_Etapa7_CloseApps(const char *serial, const char *package_name,
                        LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 7] Pacote selecionado. Aguardando 5 segundos para inicio "
       "do processamento de importacao...");

  Sleep(5000); // 5 segundos obrigatorios solicitados pelo usuario para
               // descompactar/processar

  if (cb)
    cb("[-] Monitorando o reaparecimento do botao 'PROCURAR ARQUIVO .ZIP' "
       "(confirmacao de importacao)...");

  int import_sucesso = 0;
  int max_import_checks = 25; // 25 checagens de ~1s (~25s max no total)
  for (int i = 0; i < max_import_checks; i++) {
    char *xml_check = dump_and_read_xml(serial, NULL);
    if (xml_check) {
      int ix = 0, iy = 0;
      if (get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO .ZIP", &ix, &iy,
                                0) ||
          get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO ZIP", &ix, &iy,
                                0) ||
          get_centroid_from_xml(xml_check, "Procurar Arquivo", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "ARQUIVO .ZIP", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "arquivo zip", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "Procurar", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "PROCURAR", &ix, &iy, 0)) {
        import_sucesso = 1;
        free(xml_check);
        break;
      }
      free(xml_check);
    }
    Sleep(1000);
  }

  if (import_sucesso) {
    if (cb)
      cb("[+] Botao 'PROCURAR ARQUIVO .ZIP' reapareceu! Importacao do ZIP "
         "concluida com sucesso!");
  } else {
    if (cb)
      cb("[WARNING] Tempo limite excedido aguardando o botao 'PROCURAR ARQUIVO "
         ".ZIP'. Prosseguindo assim mesmo...");
  }

  if (cb)
    cb("[-] Fechando aplicativos abertos (App e Gerenciador)...");
  char cmd[256];

  // Pressiona 'Voltar' 3 vezes para sair de qualquer menu aberto
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 4", serial);
  for (int j = 0; j < 3; j++) {
    char *back_out = adb_exec(cmd);
    if (back_out)
      free(back_out);
    Sleep(500);
  }

  // Retorna para a tela inicial
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
  char *home_out = adb_exec(cmd);
  if (home_out)
    free(home_out);

  // Força a parada do aplicativo principal
  snprintf(cmd, sizeof(cmd), "-s %s shell am force-stop %s", serial,
           package_name);
  char *stop_app = adb_exec(cmd);
  if (stop_app)
    free(stop_app);

  // Força a parada do gerenciador de arquivos (Google e AOSP)
  snprintf(cmd, sizeof(cmd),
           "-s %s shell am force-stop com.google.android.documentsui", serial);
  char *stop_docs = adb_exec(cmd);
  if (stop_docs)
    free(stop_docs);

  snprintf(cmd, sizeof(cmd),
           "-s %s shell am force-stop com.android.documentsui", serial);
  char *stop_docs2 = adb_exec(cmd);
  if (stop_docs2)
    free(stop_docs2);

  // Finaliza apps em background
  snprintf(cmd, sizeof(cmd), "-s %s shell am kill-all", serial);
  char *kill_all = adb_exec(cmd);
  if (kill_all)
    free(kill_all);

  if (cb)
    cb("[-] Todos os aplicativos encerrados.");
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 7b] LIMPEZA DA TELA DE RECENTES, PARADA FORÇADA E SHUTDOWN FÍSICO
// =============================================================================
//  O que esta etapa faz:
//  1. Limpeza Visual de Recentes: Abre a lista de multitarefas
//  (`KEYCODE_APP_SWITCH` 187),
//     desliza os cartões para a direita e clica em 'Limpar tudo' / 'Clear all'
//     via XML.
//  2. Purga Final de Processos: Garante que os processos em background sejam
//     encerrados via `am force-stop` e `am kill-all`.
//  3. Desligamento Físico do Aparelho: Realiza uma contagem regressiva de 5
//  segundos
//     e dispara o comando nativo `reboot -p`, desligando o hardware do tablet
//     por completo.
// =============================================================================
int V7_Etapa7_PowerOff(const char *serial, LogCallback cb) {
  char cmd[512];
  char msg[128];

  // 1. Fecha visualmente todos os apps (limpa recentes via HOME + RECENTS)
  if (cb)
    cb("[-] Fechando todos os apps abertos (limpeza visual)...");
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial); // HOME
  char *h1 = adb_exec(cmd);
  if (h1)
    free(h1);
  Sleep(600);
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 187",
           serial); // RECENTES
  char *r1 = adb_exec(cmd);
  if (r1)
    free(r1);
  Sleep(1000);

  // Desliza a tela para a direita duas vezes para garantir que o botao 'Limpar
  // tudo' fique visivel
  if (cb)
    cb("[-] Deslizando os apps recentes para a direita...");
  snprintf(cmd, sizeof(cmd), "-s %s shell input swipe 200 300 800 300 300",
           serial);
  char *sw1 = adb_exec(cmd);
  if (sw1)
    free(sw1);
  Sleep(400);
  char *sw2 = adb_exec(cmd);
  if (sw2)
    free(sw2);
  Sleep(800);

  // Tenta clicar em "Limpar tudo" / "Clear all" na tela de recentes
  snprintf(cmd, sizeof(cmd),
           "-s %s shell uiautomator dump /sdcard/dump_recents.xml 2>/dev/null",
           serial);
  char *dr = adb_exec(cmd);
  if (dr)
    free(dr);
  snprintf(cmd, sizeof(cmd), "-s %s shell cat /sdcard/dump_recents.xml",
           serial);
  char *xr = adb_exec(cmd);
  if (xr) {
    char *p = strstr(xr, "Limpar tudo");
    if (!p)
      p = strstr(xr, "Clear all");
    if (!p)
      p = strstr(xr, "Limpar");
    if (p) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          snprintf(cmd, sizeof(cmd), "-s %s shell input tap %d %d", serial,
                   (x1 + x2) / 2, (y1 + y2) / 2);
          char *tc = adb_exec(cmd);
          if (tc)
            free(tc);
          if (cb)
            cb("[-] 'Limpar tudo' clicado na tela de recentes.");
        }
      }
    }
    free(xr);
  }
  Sleep(800);
  // Volta para HOME
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
  char *h2 = adb_exec(cmd);
  if (h2)
    free(h2);
  Sleep(1000);

  // 2. Forca parada de todos os aplicativos principais e processos em
  // background
  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"input keyevent 3 && am force-stop "
           "br.com.yantec.innovava && "
           "am force-stop com.google.android.documentsui && am force-stop "
           "com.android.documentsui && "
           "am force-stop com.android.chrome && am kill-all\"",
           serial);
  char *ka = adb_exec(cmd);
  if (ka)
    free(ka);

  // 3. Contagem regressiva e desligamento
  if (cb)
    cb("[-] Preparacao finalizada. Iniciando contagem para desligar...");
  for (int i = 5; i >= 1; i--) {
    snprintf(msg, sizeof(msg), "[-] Desligando em %d segundo%s...", i,
             i == 1 ? "" : "s");
    if (cb)
      cb(msg);
    Sleep(1000);
  }

  if (cb)
    cb("[F1 - Etapa 7] Enviando comando de desligamento ao dispositivo...");
  snprintf(cmd, sizeof(cmd), "-s %s shell reboot -p", serial);
  char *out = adb_exec(cmd);
  if (out)
    free(out);

  if (cb)
    cb("[OK] Comando de desligamento enviado. Pipeline finalizado.");
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 1.7] OTIMIZAÇÃO DE ENERGIA, HARDWARE, BRILHO (70%) E ESCALA DE FONTE
//  (1.5)
// =============================================================================
//  O que esta etapa faz:
//  1. Desligamento de Rádios e Sensores: Desativa Wi-Fi, Bluetooth e GPS
//  (`location_mode 0`),
//     além de proibir a varredura contínua de Wi-Fi e BLE em segundo plano.
//  2. Calibração Visual: Define o brilho fixo em 70% (178/255) sem ajuste
//  automático
//     e aumenta a escala da fonte para 1.5, garantindo legibilidade perfeita.
//  3. Trava de Rotação: Garante que a orientação do sistema permaneça fixa em
//  Portrait.
// =============================================================================
int V7_Etapa1_7(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.7] Otimizando redes, brilho (70%), Fonte (1.5) e Rotacao "
       "Trava Portrait (lock 0)...");
  char cmd[1024];

  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"svc wifi disable && svc bluetooth disable && "
           "settings put secure location_mode 0 && "
           "settings put global wifi_scan_always_enabled 0 && settings put "
           "global ble_scan_always_enabled 0 && "
           "settings put system screen_brightness_mode 0 && settings put "
           "system screen_brightness 178 && "
           "settings put system font_scale 1.5 && wm user-rotation lock 0\"",
           serial);
  char *t1 = adb_exec(cmd);
  if (t1)
    free(t1);

  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 4.5] ENCERRAMENTO DO REVERSE TETHERING GNIREHTET
// =============================================================================
//  O que esta etapa faz:
//  1. Delay de Conclusão: Aguarda 2 segundos após a autenticação para permitir
//  a
//     troca de pacotes de token e autorização na rede.
//  2. Finalização do Túnel: Dispara `gnirehtet stop <serial>`, desativando a
//  VPN USB
//     e encerrando a interface de rede virtual no tablet com segurança.
// =============================================================================
int V7_Etapa4_5_StopGnirehtet(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 4.5] Aguardando 2s para concluir login e Desligando "
       "Gnirehtet...");

  // Aguardar 2 segundos apos o clique no botao Entrar
  Sleep(2000);

  char cmd_stop[1024];
  char exe_path2[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path2, MAX_PATH);
  char *last_slash2 = strrchr(exe_path2, '\\');
  if (last_slash2)
    *last_slash2 = '\0';

  char gnirehtet_path[MAX_PATH];
  char gnirehtet_dir[MAX_PATH];

  // Identifica o caminho de Dev ou Release
  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering",
           exe_path2);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
             "%s\\..\\..\\tools\\reverse_tethering", exe_path2);
  }

  char full_gnirehtet_dir[MAX_PATH];
  GetFullPathNameA(gnirehtet_dir, MAX_PATH, full_gnirehtet_dir, NULL);

  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe",
           full_gnirehtet_dir);

  snprintf(cmd_stop, sizeof(cmd_stop), "\"%s\" stop %s", gnirehtet_path,
           serial);

  STARTUPINFOA si_stop;
  PROCESS_INFORMATION pi_stop;
  ZeroMemory(&si_stop, sizeof(si_stop));
  si_stop.cb = sizeof(si_stop);
  ZeroMemory(&pi_stop, sizeof(pi_stop));
  if (CreateProcessA(NULL, cmd_stop, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL,
                     NULL, &si_stop, &pi_stop)) {
    if (cb)
      cb("[-] Tunel Gnirehtet DESLIGADO.");
    CloseHandle(pi_stop.hProcess);
    CloseHandle(pi_stop.hThread);
  } else {
    if (cb)
      cb("[WARNING] Falha ao tentar desligar o Gnirehtet.");
  }
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 1.9] LIMPEZA DA ÁREA DE TRABALHO E ATALHOS DO LAUNCHER
// =============================================================================
//  O que esta etapa faz:
//  1. Reset do Launcher: Executa `pm clear com.android.launcher3` e
//     `pm clear com.sec.android.app.launcher`, removendo widgets, pastas e
//     ícones fantasmas, entregando a tela inicial limpa e padronizada.
// =============================================================================
int V7_Etapa1_9_ClearLauncher(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.9] Limpando a Tela Inicial (Removendo atalhos e "
       "widgets)...");
  char cmd[512];

  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"pm clear com.android.launcher3; pm clear "
           "com.sec.android.app.launcher\"",
           serial);
  char *c1 = adb_exec(cmd);
  if (c1)
    free(c1);

  Sleep(300);
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 2.1] CRIAÇÃO DE ATALHO DO APLICATIVO NA TELA INICIAL
// =============================================================================
//  O que esta etapa faz:
//  1. Broadcast de Atalho: Emite intenção `INSTALL_SHORTCUT` direcionada à
//     MainActivity da aplicação instalada, posicionando o ícone do sistema
//     no centro da tela inicial do dispositivo.
// =============================================================================
int V7_Etapa2_1_AddShortcut(const char *serial, const char *package_name,
                            LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 2.1] Tentando adicionar o atalho do aplicativo na Tela "
       "Inicial...");
  char cmd[1024];

  snprintf(cmd, sizeof(cmd),
           "-s %s shell am broadcast -a "
           "com.android.launcher.action.INSTALL_SHORTCUT "
           "-e Intent.EXTRA_SHORTCUT_INTENT "
           "'intent:#Intent;action=android.intent.action.MAIN;"
           "category=android.intent.category.LAUNCHER;component=%s/"
           ".MainActivity;end' "
           "-e Intent.EXTRA_SHORTCUT_NAME 'InnovPlay'",
           serial, package_name);

  char *out = adb_exec(cmd);
  if (out)
    free(out);

  Sleep(1000);
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  [ETAPA 1.2] ATIVAÇÃO DO MODO NOTURNO (DARK THEME)
// =============================================================================
//  O que esta etapa faz:
//  1. Tema Escuro do Sistema: Habilita `cmd uimode night yes` e define
//     `ui_night_mode 2` nas configurações seguras do Android, reduzindo o
//     consumo de energia do display e proporcionando maior conforto visual.
// =============================================================================
int V7_Etapa1_2_DarkMode(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.2] Ativando Modo Escuro no dispositivo...");
  char cmd[512];
  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"cmd uimode night yes && settings put secure "
           "ui_night_mode 2\"",
           serial);
  char *dm = adb_exec(cmd);
  if (dm)
    free(dm);
  return PIPELINE_SUCCESS;
}

// =============================================================================
//  ORQUESTRADOR MESTRE: EXECUÇÃO SEQUENCIAL DO PIPELINE COMPLETO (F1)
// =============================================================================
//  Fluxo de Execução:
//  [0] Preparação Inicial: Trava Portrait, Fonte 1.5, Brilho 70%, Antenas Off
//  [1] Etapa 1: Smart Wakeup e Desbloqueio de Tela
//  [1.2] Etapa 1.2: Ativação do Modo Noturno (Dark Theme)
//  [1.5] Etapa 1.5: Debloat em Lote (Remoção e Suspensão de Apps Google)
//  [1.6] Etapa 1.6: Purga do Armazenamento e Upload (APK + Pacote ZIP)
//  [1.7] Etapa 1.7: Otimização de Hardware, Energia e Redes
//  [1.9] Etapa 1.9: Limpeza do Launcher e Remoção de Atalhos Fantasmas
//  [2] Etapa 2: Instalação Silenciosa do APK (Push + PM Install)
//  [2.1] Etapa 2.1: Criação de Atalho na Tela Inicial
//  [2.5] Etapa 2.5: Inicialização do Reverse Tethering Gnirehtet USB
//  [3] Etapa 3: Liberação de Permissões de Runtime e Abertura do App
//  [4] Etapa 4: Autenticação e Login Automático (InnoLearn Dual-Scan)
//  [4.5] Etapa 4.5: Desligamento do Túnel Gnirehtet e Isolamento de Rede
//  [5] Etapa 5: Navegação no Menu Lateral para 'Importar ZIP'
//  [6] Etapa 6: Seleção do Pacote ZIP no DocumentsUI (SAF)
//  [7a] Etapa 7a: Monitoramento da Importação e Fechamento de Aplicativos
//  [WP] Definição de Wallpaper Municipal Customizado (F4)
//  [7b] Etapa 7b: Limpeza da Tela de Recentes e Desligamento Remoto (reboot -p)
// =============================================================================
int V7_ExecutarPipelineCompleto(const char *serial, const char *apk_path,
                                const char *pacote_path, const char *wp_path,
                                const char *package_name, const char *login,
                                const char *senha, const char *municipio,
                                LogCallback cb) {
  // Converte os caminhos para Short Path 8.3 e evita bugs de UTF-8 do ADB com
  // caracteres especiais
  char safe_apk[MAX_PATH] = {0};
  char safe_pacote[MAX_PATH] = {0};
  char safe_wp[MAX_PATH] = {0};

  adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
  adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
  if (wp_path && strlen(wp_path) > 0) {
    adb_get_safe_path(wp_path, safe_wp, MAX_PATH);
  }

  // 0. PREPARACAO INICIAL BATCHED: Travar Orientação Vertical (Portrait), Fonte
  // (1.5), Brilho (70%) e Antenas
  if (cb)
    cb("[-] [F1 - Preparacao Inicial] Trava Vertical (Portrait), Fonte (1.5), "
       "Brilho (70%) e Antenas...");
  {
    char args_prep[1024];
    snprintf(args_prep, sizeof(args_prep),
             "-s %s shell \"svc wifi disable && svc bluetooth disable && "
             "settings put secure location_mode 0 && "
             "settings put global wifi_scan_always_enabled 0 && settings put "
             "global ble_scan_always_enabled 0 && "
             "settings put system screen_brightness_mode 0 && settings put "
             "system screen_brightness 178 && "
             "settings put system font_scale 1.5 && settings put system "
             "accelerometer_rotation 0 && "
             "settings put system user_rotation 0 && wm user-rotation lock 0\"",
             serial);
    char *out_prep = adb_exec(args_prep);
    if (out_prep)
      free(out_prep);
  }

  // Etapa 1: Desbloqueio
  // CORREÇÃO: PIPELINE_SUCCESS == 1, então a checagem correta é !=
  // PIPELINE_SUCCESS (o operador ! transformava !1 em 0/false, nunca abortando
  // o pipeline)
  if (V7_Etapa1(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 1.2: Modo Noturno (ativado logo no inicio)
  if (V7_Etapa1_2_DarkMode(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 1.5: Isolamento de Apps
  if (V7_Etapa1_5(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 1.6: Limpeza de armazenamento e Copia de Arquivos
  if (V7_Etapa1_6(serial, safe_apk, safe_pacote, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 1.7: Otimizacao de Energia e Redes
  if (V7_Etapa1_7(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 1.9: Limpar Launcher e Atalhos
  if (V7_Etapa1_9_ClearLauncher(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 2: Instalacao Silenciosa do APK
  if (V7_Etapa2(serial, safe_apk, package_name, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 2.1: Criar Atalho na Tela Inicial
  if (V7_Etapa2_1_AddShortcut(serial, package_name, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 2.5: Reverse Tethering (Internet via USB)
  if (V7_Etapa2_5(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 3: Permissoes de Runtime e Abertura
  if (V7_Etapa3(serial, package_name, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 4: Login Automatico
  if (V7_Etapa4(serial, login, senha, municipio, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 4.5: Desligar Gnirehtet
  if (V7_Etapa4_5_StopGnirehtet(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 5: Importacao do Pacote ZIP
  if (V7_Etapa5(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 6: Selecao do ZIP no File Picker
  if (V7_Etapa6(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Etapa 7a: Aguarda a importacao finalizar e fecha todos os apps
  if (F1_Etapa7_CloseApps(serial, package_name, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  // Definicao do Wallpaper via modulo F4 (apos fechar apps da importacao e
  // antes de desligar)
  if (safe_wp[0] != '\0') {
    if (cb)
      cb("[-] Aplicando wallpaper customizado do municipio...");
    F4_DefinirWallpaper(serial, safe_wp, cb);
  }

  // Etapa 7b: Limpeza completa de Recentes e Desligamento Fisico
  if (V7_Etapa7_PowerOff(serial, cb) != PIPELINE_SUCCESS)
    return PIPELINE_ERROR;

  if (cb)
    cb("\n[F1] -> PIPELINE F1 CONCLUIDO COM SUCESSO ABSOLUTO.");
  // .:. DevMark: Antigravity IDE .:.
  return PIPELINE_SUCCESS;
}
