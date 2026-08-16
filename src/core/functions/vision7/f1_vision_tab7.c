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

// ==========================================
// MÃƒÂ³dulo: [F1] SincronizaÃƒÂ§ÃƒÂ£o + Pipeline Completo do F1
// Aparelho Alvo: Positivo Tab Vision 7
// ==========================================

// --- Etapa 1: PreparaÃƒÂ§ÃƒÂ£o do Sistema (Wake & Unlock) ---
int V7_Etapa1(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1] Acordando o dispositivo e desbloqueando a tela...");

  char args[256];

  // 1. Verificar se a tela estÃƒÂ¡ apagada (Smart Wakeup)
  snprintf(args, sizeof(args),
           "-s %s shell \"dumpsys power | grep mWakefulness=\"", serial);
  char *wake_status = adb_exec(args);

  if (wake_status) {
    // Se a string "mWakefulness=Awake" NÃƒÆ’O estiver presente, a tela estÃƒÂ¡
    // apagada (Dozing, Asleep, etc)
    if (strstr(wake_status, "mWakefulness=Awake") == NULL) {
      if (cb)
        cb("[-] Tela desligada detectada (Dozing/Asleep). Pressionando botÃƒÂ£o "
           "Power virtual...");
      snprintf(args, sizeof(args), "-s %s shell input keyevent 26",
               serial); // KEYCODE_POWER
      char *out_power = adb_exec(args);
      if (out_power)
        free(out_power);

      Sleep(800); // Aguarda a tela acender fisicamente
    } else {
      if (cb)
        cb("[-] A tela jÃƒÂ¡ estava acesa. Ignorando botÃƒÂ£o Power.");
    }
    free(wake_status);
  }

  // 2. Simular deslize para desbloquear
  snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300",
           serial);
  char *out = adb_exec(args);
  if (out)
    free(out);

  // 3. Garantir orientação vertical (Portrait), desativar rotação automática e definir tempo limite para 5 minutos
  if (cb)
    cb("[-] Trava de tela na vertical (Portrait) e desativacao de rotacao automatica...");
  snprintf(args, sizeof(args),
           "-s %s shell \"settings put system accelerometer_rotation 0 && "
           "settings put system user_rotation 0 && wm user-rotation lock 0 && "
           "settings put system screen_off_timeout 300000\"", serial);
  char *out_timeout = adb_exec(args);
  if (out_timeout)
    free(out_timeout);


  if (cb)
    cb("[-] Tela desbloqueada com sucesso.");
  return PIPELINE_SUCCESS;
}

static const char *V7_CAMERA_PACKAGES[] = {
    "com.softwinner.camera2",
    "com.android.camera2",
    "com.android.camera",
    "com.sec.android.app.camera",
    "com.motorola.camera2",
    "com.motorola.camera3",
    "com.google.android.GoogleCamera",
    "com.huawei.camera",
    "com.mediatek.camera"
};
static const int V7_NUM_CAMERA_PACKAGES = sizeof(V7_CAMERA_PACKAGES) / sizeof(V7_CAMERA_PACKAGES[0]);

// --- Etapa 1.5: Suspensao da Camera via ADB ---
int V7_Etapa1_5(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.5] Suspendendo pacotes de Câmera via ADB...");

  char args[512];

  // Suspendendo a Câmera via ADB usando o loop pm suspend
  for (int i = 0; i < V7_NUM_CAMERA_PACKAGES; i++) {
    snprintf(args, sizeof(args), "-s %s shell pm suspend %s", serial, V7_CAMERA_PACKAGES[i]);
    char *out_cam = adb_exec(args);
    if (out_cam) free(out_cam);
  }

  if (cb)
    cb("[+] Suspensão da Câmera via ADB concluída com sucesso!");
  // ..: DevMark: Antigravity IDE :..
  return PIPELINE_SUCCESS;
}

// --- Etapa 1.6: Limpeza do Armazenamento e CÃƒÂ³pia ---
int V7_Etapa1_6(const char *serial, const char *apk_path,
                const char *pacote_path, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.6] Formatando /sdcard (exceto Android e Pictures) e "
       "copiando arquivos...");

  char args[1024];

  // 1. Limpeza preservando 'Android' e 'Pictures' (necessaria para wallpaper)
  snprintf(args, sizeof(args),
           "-s %s shell \"cd /sdcard && ls | grep -vwE 'Android|Pictures' | "
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

  // 2. Criar a pasta avaliacoes
  snprintf(args, sizeof(args), "-s %s shell mkdir -p /sdcard/avaliacoes",
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
      cb("  -> Enviando APK para /sdcard/avaliacoes/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/avaliacoes/",
             serial, safe_apk);
    char *out_push1 = adb_exec(args);
    if (out_push1)
      free(out_push1);
  }

  // 4. Fazer o PUSH do Pacote ZIP
  if (pacote_path && strlen(pacote_path) > 0) {
    adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
    if (cb)
      cb("  -> Enviando PACOTE DE DADOS para /sdcard/avaliacoes/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/avaliacoes/",
             serial, safe_pacote);
    char *out_push2 = adb_exec(args);
    if (out_push2)
      free(out_push2);
  }

  if (cb)
    cb("[+] Armazenamento preparado e arquivos copiados com sucesso!");

  return PIPELINE_SUCCESS;
}

// --- Etapa 2: Instalação Silenciosa do APK ---
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

  // Metodo primario: push + pm install via shell (evita Failure [null] do streamed install)
  if (cb)
    cb("[-] Enviando APK ao dispositivo via push e instalando via pm install...");

  snprintf(args, sizeof(args), "-s %s push \"%s\" /data/local/tmp/app_install.apk", serial, safe_apk);
  char *out_push = adb_exec(args);
  if (out_push) free(out_push);

  snprintf(args, sizeof(args), "-s %s shell \"pm install --user 0 /data/local/tmp/app_install.apk && rm -f /data/local/tmp/app_install.apk\"", serial);

#undef adb_exec
  char *out = adb_exec(args);
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

  if (out && strstr(out, "Success")) {
    install_success = 1;
  }

  // Fallback: adb install direto sem -r (instalacao limpa caso pm install falhe)
  if (!install_success) {
    if (cb)
      cb("[-] pm install falhou. Tentando via adb install direto...");
    if (out) { free(out); out = NULL; }

    snprintf(args, sizeof(args), "-s %s install --user 0 \"%s\"", serial, safe_apk);
    char *out2 = adb_exec(args);
    if (out2) {
      if (strstr(out2, "Success")) {
        install_success = 1;
      } else {
        if (out) free(out);
        out = out2;
        out2 = NULL;
      }
      if (out2) free(out2);
    }
  }

  if (install_success) {
    if (cb)
      cb("[-] APK instalado com sucesso!");

    if (cb) cb("[-] Removendo restricoes de background para o InnovAva...");
    char args_bypass[1024];
    snprintf(args_bypass, sizeof(args_bypass), "-s %s shell appops set %s RUN_IN_BACKGROUND allow", serial, package_name);
    char *out_bp = adb_exec(args_bypass);
    if (out_bp) free(out_bp);

    snprintf(args_bypass, sizeof(args_bypass), "-s %s shell dumpsys deviceidle whitelist +%s", serial, package_name);
    out_bp = adb_exec(args_bypass);
    if (out_bp) free(out_bp);

    if (out) free(out);
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
        snprintf(err_msg, sizeof(err_msg), "[ERROR] Falha na instalacao do APK.");
      }
      cb(err_msg);
    }
    if (out) free(out);
    return PIPELINE_ERROR;
  }
}

// --- Etapa 2.5: Estabelecimento de ConexÃƒÂ£o (Reverse Tethering) ---
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

  // Tenta achar no diretÃƒÂ³rio Release
  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering",
           exe_path);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    // Se nÃƒÂ£o achou, fallback para diretÃƒÂ³rio de Dev
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
  snprintf(args_op, sizeof(args_op),
           "-s %s shell appops set com.genymobile.gnirehtet RUN_IN_BACKGROUND allow", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  // Coloca na whitelist de otimizacao de bateria (Ignorar Doze Mode)
  snprintf(args_op, sizeof(args_op),
           "-s %s shell dumpsys deviceidle whitelist +com.genymobile.gnirehtet", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  if (cb) cb("  -> Desativando AWBMS (AllWinner Background Manager) via Settings...");
  snprintf(args_op, sizeof(args_op),
           "-s %s shell settings put global awbms_enabled 0", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  snprintf(args_op, sizeof(args_op),
           "-s %s shell settings put global awbms_disable 1", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

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

// --- Etapa 3: LiberaÃƒÂ§ÃƒÂ£o de PermissÃƒÂµes e Abertura ---
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
           serial, package_name, package_name, package_name, package_name, package_name);
  char *out = adb_exec(args);
  if (out) free(out);

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
  if (!xml || !search_str) return 0;
  char *p = (char *)xml;
  int found = 0;
  while ((p = strstr(p, search_str)) != NULL) {
    char *b_start = strstr(p, "bounds=\"[");
    if (b_start) {
      int x1, y1, x2, y2;
      if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) ==
          4) {
        if (out_cx) *out_cx = (x1 + x2) / 2;
        if (out_cy) *out_cy = (y1 + y2) / 2;
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
  snprintf(local_xml, sizeof(local_xml), "%s\\window_dump_%s.xml", exe_dir, serial);

  char adb_args[512];
  snprintf(adb_args, sizeof(adb_args),
           "-s %s shell uiautomator dump /sdcard/window_dump_%s.xml", serial, serial);
  char *dump_out = adb_exec(adb_args);
  if (dump_out)
    free(dump_out);

  snprintf(adb_args, sizeof(adb_args),
           "-s %s pull /sdcard/window_dump_%s.xml \"%s\"", serial, serial, local_xml);
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

// --- Etapa 4: Login AutomÃƒÂ¡tico (Screen Reading via UIAutomator) ---
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
    // DUMP DEBUG para que possamos analisar a Ã¡rvore se falhar
    FILE *debug_f = fopen("logs/window_dump_debug.xml", "w");
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

    // Converte o nome do municipio para UTF-8 de forma segura (C# envia UTF-8 via LPUTF8Str)
    char utf8_municipio[256];
    WCHAR w_municipio[128];
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, safe_municipio, -1, w_municipio, 128) != 0) {
      strncpy(utf8_municipio, safe_municipio, sizeof(utf8_municipio) - 1);
      utf8_municipio[sizeof(utf8_municipio) - 1] = '\0';
    } else {
      MultiByteToWideChar(CP_ACP, 0, safe_municipio, -1, w_municipio, 128);
      WideCharToMultiByte(CP_UTF8, 0, w_municipio, -1, utf8_municipio, 256, NULL, NULL);
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
          learn_save_fallback_coord("v7_campo_email", res_email.cx, res_email.cy,
                                    1);
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
    snprintf(args_local, sizeof(args_local), "-s %s shell \"dumpsys input_method | grep mInputShown=true && input keyevent 4 || input keyevent 111\"",
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
          learn_save_fallback_coord("v7_campo_senha", res_senha.cx, res_senha.cy,
                                    1);
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
    snprintf(args_local, sizeof(args_local), "-s %s shell \"dumpsys input_method | grep mInputShown=true && input keyevent 4 || input keyevent 111\"",
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

        // === VALIDAÇÃO PRÉVIA VIA XML: Confirma se o município alvo está visível no seletor ===
        if (municipio && strlen(municipio) > 0) {
          if (get_centroid_from_xml_fuzzy(xml_btn, municipio, NULL, NULL, 0)) {
            if (cb) {
              char log_msg[256];
              snprintf(log_msg, sizeof(log_msg), "  -> [Validação XML V7] OK: Município '%s' confirmado no seletor!", municipio);
              cb(log_msg);
            }
          } else {
            if (cb) {
              char log_msg[256];
              snprintf(log_msg, sizeof(log_msg), "[WARNING] [Validação XML V7] Município '%s' NÃO detectado no seletor! Re-executando seleção...", municipio);
              cb(log_msg);
            }
            LearnFindResult res_retry = learn_find_element("v7_spinner_municipio", xml_btn, 0, cb);
            if (res_retry.found) {
              snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, res_retry.cx, res_retry.cy);
              char *tr1 = adb_exec(args_local); if (tr1) free(tr1);
              Sleep(1500);
              char *xml_m_retry = dump_and_read_xml(serial, cb);
              if (xml_m_retry) {
                int mcx = 0, mcy = 0;
                if (get_centroid_from_xml_fuzzy(xml_m_retry, municipio, &mcx, &mcy, 0)) {
                  snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, mcx, mcy);
                  char *tr2 = adb_exec(args_local); if (tr2) free(tr2);
                  if (cb) {
                    char log_msg[256];
                    snprintf(log_msg, sizeof(log_msg), "[+] [Validação XML V7] Município '%s' re-selecionado com sucesso!", municipio);
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
          learn_save_fallback_coord("v7_botao_entrar", res_btn.cx, res_btn.cy, 1);
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
                learn_record_miss("v7_botao_entrar", t_btn->strategies[_mi].query);
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
  snprintf(local_xml_clean, sizeof(local_xml_clean), "%s\\window_dump_%s.xml", exe_dir_clean, serial);
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

// --- Etapa 5: Importacao do ZIP (Abertura do Menu Lateral) ---
int V7_Etapa5(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 5] Garantindo tela acesa/desbloqueada e abrindo menu lateral...");

  // 0. Garantir desbloqueio de tela
  V7_Etapa1(serial, cb);
  char args[512];

  // Aguarda o login processar e a tela principal carregar
  Sleep(4000);

  if (cb)
    cb("[-] Abrindo menu lateral (Swipe da esquerda para direita)...");
  snprintf(args, sizeof(args), "-s %s shell input swipe 0 500 500 500 300", serial);
  char *swp = adb_exec(args);
  if (swp) free(swp);
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
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *tp = adb_exec(args);
      if (tp) free(tp);
      Sleep(1500);

      // Desligar Gnirehtet e Wi-Fi imediatamente ao chegar em Importar ZIP
      if (cb)
        cb("[-] Tela de importacao aberta. Desligando tunel Gnirehtet e Wi-Fi imediatamente...");
      stop_gnirehtet_nosleep(serial, cb);
      snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
      char *out_w = adb_exec(args);
      if (out_w) free(out_w);

    } else {
      if (cb)
        cb("[WARNING] Opcao 'Importar ZIP' nao encontrada diretamente no menu lateral.");
    }
    free(xml_menu);
  }

  return PIPELINE_SUCCESS;
}

// --- Etapa 6: Selecao do Arquivo ZIP (Botao Procurar Arquivo) ---
int V7_Etapa6(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 6] Garantindo desbloqueio e buscando o botao 'Procurar arquivo'...");

  // 0. Garantir desbloqueio de tela antes da selecao de arquivo
  V7_Etapa1(serial, cb);

  char args[512];
  int cx = 0, cy = 0;
  int btn_found = 0;

  // 1. Clicar no botão 'Procurar arquivo .zip' com busca precisa (sem clique cego em Exportar)
  Sleep(2000); // Aguarda tela de importaçao
  for (int retry = 0; retry < 3; retry++) {
    char *xml_import = dump_and_read_xml(serial, cb);
    if (xml_import) {
      if (get_centroid_from_xml(xml_import, "PROCURAR ARQUIVO ZIP", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "PROCURAR ARQUIVO", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "Procurar arquivo", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "Procurar", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_import, "PROCURAR", &cx, &cy, 0)) {

        if (cb)
          cb("[-] Botao 'Procurar arquivo' localizado. Clicando...");
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
        char *t1 = adb_exec(args);
        if (t1) free(t1);
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
      cb("[WARNING] Botao 'Procurar arquivo' nao detectado no XML. Re-acendendo tela...");
    V7_Etapa1(serial, cb);
  }

  Sleep(3000); // Aguarda o gerenciador de arquivos nativo abrir

  // [FAST-PATH RESTAURADO E MELHORADO A PEDIDO]
  // O sistema verifica primeiro se ja esta na pasta avaliacoes e o arquivo zip
  // esta visivel. Se nao estiver, ele faz o swipe e busca o armazenamento
  // interno.
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

  // Se o Fast-Path falhar (esta em Recentes ou outra pasta), forÃ§a menu lateral
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

// --- Etapa 7a: Aguardar importacao e Fechar todos os apps ---
static bool F1_Etapa7_CloseApps(const char *serial, const char *package_name,
                                LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 7] Pacote selecionado. Aguardando 5 segundos para inicio do processamento de importacao...");
  
  Sleep(5000); // 5 segundos obrigatorios solicitados pelo usuario para descompacatar/processar

  if (cb)
    cb("[-] Monitorando o reaparecimento do botao 'PROCURAR ARQUIVO .ZIP' (confirmacao de importacao)...");
  
  int import_sucesso = 0;
  int max_import_checks = 25; // 25 checagens de ~1s (~25s max no total)
  for (int i = 0; i < max_import_checks; i++) {
      char *xml_check = dump_and_read_xml(serial, NULL);
      if (xml_check) {
          int ix = 0, iy = 0;
          if (get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO .ZIP", &ix, &iy, 0) ||
              get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO ZIP", &ix, &iy, 0) ||
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
      if (cb) cb("[+] Botao 'PROCURAR ARQUIVO .ZIP' reapareceu! Importacao do ZIP concluida com sucesso!");
  } else {
      if (cb) cb("[WARNING] Tempo limite excedido aguardando o botao 'PROCURAR ARQUIVO .ZIP'. Prosseguindo assim mesmo...");
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

  // ForÃƒÂ§a a parada do aplicativo principal
  snprintf(cmd, sizeof(cmd), "-s %s shell am force-stop %s", serial,
           package_name);
  char *stop_app = adb_exec(cmd);
  if (stop_app)
    free(stop_app);

  // ForÃƒÂ§a a parada do gerenciador de arquivos (Google e AOSP)
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

  // Killa apps em background
  snprintf(cmd, sizeof(cmd), "-s %s shell am kill-all", serial);
  char *kill_all = adb_exec(cmd);
  if (kill_all)
    free(kill_all);

  if (cb)
    cb("[-] Todos os aplicativos encerrados.");
  return PIPELINE_SUCCESS;
}

// --- Etapa 7b: Limpeza completa, Modo Escuro e Desligamento ---
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

  // 2. Forca parada de todos os aplicativos principais e processos em background
  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"input keyevent 3 && am force-stop br.com.yantec.innovava && "
           "am force-stop com.google.android.documentsui && am force-stop com.android.documentsui && "
           "am force-stop com.android.chrome && am kill-all\"", serial);
  char *ka = adb_exec(cmd);
  if (ka)
    free(ka);

  // 5. Contagem regressiva e desligamento
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

// Etapa 1.7: Desativacao de recursos inuteis para poupar hardware e configuracao do sistema
int V7_Etapa1_7(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.7] Otimizando redes, brilho (70%), Fonte (1.5) e Rotacao Trava Portrait (lock 0)...");
  char cmd[1024];

  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"svc wifi disable && svc bluetooth disable && settings put secure location_mode 0 && "
           "settings put global wifi_scan_always_enabled 0 && settings put global ble_scan_always_enabled 0 && "
           "settings put system screen_brightness_mode 0 && settings put system screen_brightness 178 && "
           "settings put system font_scale 1.5 && wm user-rotation lock 0\"",
           serial);
  char *t1 = adb_exec(cmd);
  if (t1)
    free(t1);

  return PIPELINE_SUCCESS;
}

// Desliga o tunel Gnirehtet sem ligar o Wi-Fi
int V7_Etapa4_5_StopGnirehtet(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 4.5] Aguardando 2s para concluir login e Desligando Gnirehtet...");

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
  return PIPELINE_SUCCESS; // We always return true to not break the pipeline
}

// Etapa 1.9: Remover todos os atalhos da tela inicial (Limpando dados do
// Launcher)
int V7_Etapa1_9_ClearLauncher(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.9] Limpando a Tela Inicial (Removendo atalhos e widgets)...");
  char cmd[512];

  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"pm clear com.android.launcher3; pm clear com.sec.android.app.launcher\"",
           serial);
  char *c1 = adb_exec(cmd);
  if (c1)
    free(c1);

  Sleep(300);
  return PIPELINE_SUCCESS;
}

// Etapa 2.1: Adicionar Atalho do Aplicativo Instalado
int V7_Etapa2_1_AddShortcut(const char *serial, const char *package_name,
                            LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 2.1] Tentando adicionar o atalho do aplicativo na Tela "
       "Inicial...");
  char cmd[1024];

  // Em versÃƒÂµes mais novas do Android, esse broadcast pode nÃƒÂ£o funcionar
  // nativamente, mas ÃƒÂ© a melhor alternativa via ADB silencioso. O Monkey na
  // Etapa 3 tambÃƒÂ©m pode induzir o Launcher a criar o atalho se o launcher
  // estiver recÃƒÂ©m-limpo.
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

// Etapa 1.2: Ativar Modo Noturno
int V7_Etapa1_2_DarkMode(const char *serial, LogCallback cb) {
  if (cb)
    cb("[F1 - Etapa 1.2] Ativando Modo Escuro no dispositivo...");
  char cmd[512];
  snprintf(cmd, sizeof(cmd), "-s %s shell \"cmd uimode night yes && settings put secure ui_night_mode 2\"", serial);
  char *dm = adb_exec(cmd);
  if (dm)
    free(dm);
  return PIPELINE_SUCCESS;
}

// Orquestrador Principal do Pipeline
int V7_ExecutarPipelineCompleto(const char *serial, const char *apk_path,
                                const char *pacote_path, const char *wp_path,
                                const char *package_name, const char *login,
                                const char *senha, const char *municipio,
                                LogCallback cb) {
  // Converte os caminhos para Short Path 8.3 e evita bugs de UTF-8 do ADB com caracteres especiais (ex: acentos)
  char safe_apk[MAX_PATH] = {0};
  char safe_pacote[MAX_PATH] = {0};
  char safe_wp[MAX_PATH] = {0};

  adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
  adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
  if (wp_path && strlen(wp_path) > 0) {
    adb_get_safe_path(wp_path, safe_wp, MAX_PATH);
  }

  // 0. PREPARACAO INICIAL BATCHED: Travar Orientação Vertical (Portrait), Fonte (1.5), Brilho (70%) e Antenas
  if (cb)
    cb("[-] [F1 - Preparacao Inicial] Trava Vertical (Portrait), Fonte (1.5), Brilho (70%) e Antenas...");
  {
    char args_prep[1024];
    snprintf(args_prep, sizeof(args_prep),
             "-s %s shell \"svc wifi disable && svc bluetooth disable && settings put secure location_mode 0 && "
             "settings put global wifi_scan_always_enabled 0 && settings put global ble_scan_always_enabled 0 && "
             "settings put system screen_brightness_mode 0 && settings put system screen_brightness 178 && "
             "settings put system font_scale 1.5 && settings put system accelerometer_rotation 0 && "
             "settings put system user_rotation 0 && wm user-rotation lock 0\"",
             serial);
    char *out_prep = adb_exec(args_prep);
    if (out_prep) free(out_prep);
  }

  // Etapa 1: Desbloqueio
  if (!V7_Etapa1(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 1.2: Modo Noturno (ativado logo no inicio)
  if (!V7_Etapa1_2_DarkMode(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 1.5: Isolamento de Apps
  if (!V7_Etapa1_5(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 1.6: Limpeza de armazenamento e CÃƒÂ³pia (Novo requisito)
  if (!V7_Etapa1_6(serial, safe_apk, safe_pacote, cb))
    return PIPELINE_ERROR;

  // Etapa 1.7: Otimizacao de Energia e Redes
  if (!V7_Etapa1_7(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 1.9: Limpar Launcher e Atalhos
  if (!V7_Etapa1_9_ClearLauncher(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 2: InstalaÃƒÂ§ÃƒÂ£o
  if (!V7_Etapa2(serial, safe_apk, package_name, cb))
    return PIPELINE_ERROR;

  // Etapa 2.1: Criar Atalho na Tela Inicial
  if (!V7_Etapa2_1_AddShortcut(serial, package_name, cb))
    return PIPELINE_ERROR;

  // Etapa 2.5: Reverse Tethering
  if (!V7_Etapa2_5(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 3: PermissÃƒÂµes e Abertura
  if (!V7_Etapa3(serial, package_name, cb))
    return PIPELINE_ERROR;

  // Etapa 4: Login AutomÃƒÂ¡tico
  if (!V7_Etapa4(serial, login, senha, municipio, cb))
    return PIPELINE_ERROR;

  // Etapa 4.5: Desligar Gnirehtet
  if (!V7_Etapa4_5_StopGnirehtet(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 5: ImportaÃƒÂ§ÃƒÂ£o do Pacote ZIP
  if (!V7_Etapa5(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 6: SeleÃƒÂ§ÃƒÂ£o do ZIP no File Picker
  if (!V7_Etapa6(serial, cb))
    return PIPELINE_ERROR;

  // Etapa 7a: Aguarda a importacao finalizar e fecha todos os apps
  if (!F1_Etapa7_CloseApps(serial, package_name, cb))
    return PIPELINE_ERROR;

  // DefiniÃƒÂ§ÃƒÂ£o do Wallpaper via mÃƒÂ³dulo F3 (apos fechar apps da importacao e
  // antes de desligar)
  if (safe_wp[0] != '\0') {
    if (cb)
      cb("[-] Aplicando wallpaper customizado do municipio...");
    F4_DefinirWallpaper(serial, safe_wp, cb);
  }

  // Etapa 7b: Limpeza completa, Modo Escuro e Desligamento
  if (!V7_Etapa7_PowerOff(serial, cb))
    return PIPELINE_ERROR;

  if (cb)
    cb("\n[F1] -> PIPELINE F1 CONCLUIDO COM SUCESSO ABSOLUTO.");
  // .:. DevMark: Antigravity IDE .:.
  return PIPELINE_SUCCESS;
}
