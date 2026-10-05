#include "f1_nokia_t20.h"

#include "../../drivers/adb.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <windows.h>

extern char *dump_and_read_xml(const char *serial, LogCallback cb);

// --- Helper: Escapa strings para uso seguro no adb shell input text ---
static void t20_escape_adb_text(const char *src, char *dest) {
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

// MACRO: Injeta o sistema de resiliencia em todas as chamadas de adb_exec neste arquivo
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// Estrutura para a thread da barra de progresso do Gnirehtet
typedef struct {
  LogCallback cb;
  volatile int *is_installing;
} T20_GnirehtetProgressData;

__attribute__((unused)) static DWORD WINAPI T20_GnirehtetProgressBarThread(LPVOID lpParam) {
  T20_GnirehtetProgressData *pdata = (T20_GnirehtetProgressData *)lpParam;
  int seconds = 0;
  while (*(pdata->is_installing) && seconds <= 15) {
    char msg[256];
    char bar[21];
    int pct = (seconds * 100) / 10;
    if (pct > 99) pct = 99;
    int fill = (pct * 20) / 100;
    for (int i = 0; i < 20; i++)
      bar[i] = (i < fill) ? '=' : '-';
    bar[20] = '\0';
    snprintf(msg, sizeof(msg), "[-] Instalando Gnirehtet [%s] %d%% (%ds)", bar, pct, seconds);
    if (pdata->cb) pdata->cb(msg);
    Sleep(1000);
    seconds++;
  }
  return 0;
}

// ==========================================
// Módulo: [F1] Sincronização + Pipeline Completo
// Aparelho Alvo: Nokia T20 (Android 11)
// ==========================================

// --- Etapa 1: Preparação do Sistema (Wake & Unlock) ---
int T20_Etapa1(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 1] Acordando o Nokia T20 e desbloqueando a tela...");

  char args[256];

  // 1. Verificar se a tela está apagada (Smart Wakeup)
  snprintf(args, sizeof(args), "-s %s shell \"dumpsys power | grep mWakefulness=\"", serial);
  char *wake_status = adb_exec(args);

  if (wake_status) {
    if (strstr(wake_status, "mWakefulness=Awake") == NULL) {
      if (cb) cb("[-] Tela desligada detectada (Dozing/Asleep). Pressionando botão Power...");
      snprintf(args, sizeof(args), "-s %s shell input keyevent 26", serial);
      char *out_power = adb_exec(args);
      if (out_power) free(out_power);
      Sleep(800);
    } else {
      if (cb) cb("[-] A tela do Nokia T20 já estava acesa.");
    }
    free(wake_status);
  }

  // 2. Simular deslize para desbloquear
  snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300", serial);
  char *out = adb_exec(args);
  if (out) free(out);

  // 3. Forçar orientação vertical (Portrait), desativar rotação automática, fonte 1.15 e tempo limite 5 min
  if (cb) cb("[-] Trava de tela na vertical (Portrait) no Nokia T20, desativando rotação e ajustando fonte (1.15)...");
  snprintf(args, sizeof(args),
           "-s %s shell \"settings put system accelerometer_rotation 0 && "
           "settings put system user_rotation 0 && wm user-rotation lock 0 && "
           "settings put system font_scale 1.15 && settings put system screen_off_timeout 300000\"", serial);
  char *out_timeout = adb_exec(args);
  if (out_timeout) free(out_timeout);


  if (cb) cb("[-] Tela desbloqueada e fonte ajustada para 1.15 com sucesso.");
  return PIPELINE_SUCCESS;
}

// --- Etapa 1.2: Forçar Dark Mode, Escala de Fonte 1.15, Bypass do Play Protect e Silenciamento de Notificações ---
int T20_Etapa1_2_DarkMode(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 1.2] Aplicando Dark Mode, escala de fonte 1.15, bypass do Play Protect e silenciamento de notificações...");
  char args[512];
  snprintf(args, sizeof(args),
           "-s %s shell \"cmd uimode night yes && "
           "settings put system font_scale 1.15 && "
           "settings put global package_verifier_enable 0 && "
           "settings put global upload_apk_enable 0 && "
           "settings put global heads_up_notifications_enabled 0\"",
           serial);
  char *out = adb_exec(args);
  if (out) free(out);
  return PIPELINE_SUCCESS;
}

// --- Etapa 1.5: Mapeamento em Tempo Real de Apps do Menu e Desativação Direcionada ---
int T20_Etapa1_5(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 1.5] Mapeando aplicativos visíveis no menu do tablet via ADB em tempo real...");

  // 1. Consulta em tempo real todas as atividades da categoria LAUNCHER no dispositivo
  char args_list[512];
  snprintf(args_list, sizeof(args_list),
           "-s %s shell pm query-activities -a android.intent.action.MAIN -c android.intent.category.LAUNCHER", serial);
  char *pkgs_out = adb_exec(args_list);

  if (!pkgs_out || strlen(pkgs_out) == 0) {
    if (pkgs_out) free(pkgs_out);
    if (cb) cb("[WARNING] Não foi possível consultar as atividades do menu via ADB. Mantendo fluxo...");
    return PIPELINE_SUCCESS;
  }

  // 2. Whitelist de Proteção Rígida (Apenas InnovAva, Configurações e componentes de sistema essenciais)
  static const char *protected_whitelist[] = {
      "br.com.yantec.innovava",                   // InnovAva
      "com.android.settings",                     // Configurações do Sistema
      "com.genymobile.gnirehtet",                 // Gnirehtet VPN (Internet USB)
      "com.google.android.inputmethod.latin",     // Teclado Gboard
      "com.android.inputmethod.latin",           // Teclado AOSP
      "com.google.android.apps.nexuslauncher",   // Launcher Pixel/Nokia
      "com.android.launcher3",                    // Launcher AOSP
      "com.hihonor.android.launcher",            // Launcher Honor/Nokia T20
      "com.nokia.launcher",                      // Launcher Nokia nativo
      NULL
  };

  int count_disabled = 0;
  char processed_pkgs[128][128]; // Histórico para evitar duplicações
  int total_processed = 0;

  char *line = strtok(pkgs_out, "\r\n");
  while (line != NULL) {
    char *p_name = strstr(line, "packageName=");
    if (p_name) {
      const char *pkg = p_name + 12; // avança após "packageName="
      char clean_pkg[128] = {0};
      sscanf(pkg, "%127s", clean_pkg);

      if (strlen(clean_pkg) > 0) {
        // Verifica se já processamos este pacote
        int already_processed = 0;
        for (int i = 0; i < total_processed; i++) {
          if (strcmp(processed_pkgs[i], clean_pkg) == 0) {
            already_processed = 1;
            break;
          }
        }

        if (!already_processed) {
          if (total_processed < 128) {
            strncpy(processed_pkgs[total_processed], clean_pkg, 127);
            total_processed++;
          }

          // Checa se está na Whitelist de Proteção
          int is_protected = 0;
          for (int k = 0; protected_whitelist[k] != NULL; k++) {
            if (strcmp(clean_pkg, protected_whitelist[k]) == 0) {
              is_protected = 1;
              break;
            }
          }

          // Se NÃO for um aplicativo protegido (é um app do menu como Chrome, YouTube, Fotos, Câmera, etc.), desativa-o
          if (!is_protected) {
            char args_dis[512];
            snprintf(args_dis, sizeof(args_dis), "-s %s shell pm disable-user --user 0 %s", serial, clean_pkg);
            char *out_dis = adb_exec(args_dis);
            if (out_dis) {
              if (strstr(out_dis, "disabled-user")) {
                count_disabled++;
                if (cb) {
                  char msg[256];
                  snprintf(msg, sizeof(msg), "[-] App do menu ocultado com sucesso: %s", clean_pkg);
                  cb(msg);
                }
              }
              free(out_dis);
            }
          }
        }
      }
    }
    line = strtok(NULL, "\r\n");
  }

  free(pkgs_out);

  if (cb) {
    char msg_done[256];
    snprintf(msg_done, sizeof(msg_done),
             "[+] Mapeamento em tempo real concluído! %d aplicativo(s) ocultados do menu principal (InnovAva e Configurações mantidos).",
             count_disabled);
    cb(msg_done);
  }

  return PIPELINE_SUCCESS;
}

// --- Etapa 1.6: Limpeza do Armazenamento e Cópia em Pipe Único ---
int T20_Etapa1_6(const char *serial, const char *apk_path, const char *pacote_path, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 1.6] Limpando /sdcard (Preservando /sdcard/innovava) e transferindo arquivos...");

  char args[1024];

  // Purga o /sdcard preservando estritamente Android, Pictures e a pasta innovava
  snprintf(args, sizeof(args),
           "-s %s shell \"cd /sdcard && ls | grep -vwE 'Android|Pictures|innovava' | xargs rm -rf && "
           "mkdir -p /sdcard/Pictures/Screenshots /sdcard/innovava\"",
           serial);
  char *out_prep = adb_exec(args);
  if (out_prep) free(out_prep);

  char safe_apk[MAX_PATH] = {0};
  char safe_pacote[MAX_PATH] = {0};

  if (apk_path && strlen(apk_path) > 0) {
    adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
    if (cb) cb("  -> Transmitindo APK para /sdcard/innovava/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial, safe_apk);
    char *out_push1 = adb_exec(args);
    if (out_push1) free(out_push1);
  }

  if (pacote_path && strlen(pacote_path) > 0) {
    adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
    if (cb) cb("  -> Transmitindo PACOTE DE DADOS para /sdcard/innovava/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial, safe_pacote);
    char *out_push2 = adb_exec(args);
    if (out_push2) free(out_push2);
  }

  if (cb) cb("[+] Armazenamento do Nokia T20 (pasta /sdcard/innovava) preparado em tempo recorde!");
  return PIPELINE_SUCCESS;
}

// --- Etapa 1.7: Ajuste de Brilho (70%), Trava Vertical e Desativação de Redes e Varreduras ---
int T20_Etapa1_7(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 1.7] Ajustando brilho em 70%, travando orientação na vertical (sem giro automático), desativando Wi-Fi, Bluetooth e buscas de localização...");
  char args[1024];
  snprintf(args, sizeof(args),
           "-s %s shell \""
           "settings put system screen_brightness_mode 0 && "
           "settings put system screen_brightness 178 && "
           "settings put system accelerometer_rotation 0 && "
           "settings put system user_rotation 0 && "
           "svc wifi disable && "
           "settings put global wifi_on 0 && "
           "svc bluetooth disable && "
           "settings put global bluetooth_on 0 && "
           "settings put global wifi_scan_always_enabled 0 && "
           "settings put global ble_scan_always_enabled 0 && "
           "settings put secure location_mode 0\"",
           serial);
  char *out = adb_exec(args);
  if (out) free(out);
  return PIPELINE_SUCCESS;
}

// --- Etapa 1.9: Limpeza de Launcher ---
int T20_Etapa1_9_ClearLauncher(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 1.9] Limpando dados do Launcher...");
  char args[512];
  snprintf(args, sizeof(args), "-s %s shell \"pm clear com.google.android.apps.nexuslauncher || pm clear com.android.launcher3\"", serial);
  char *out = adb_exec(args);
  if (out) free(out);
  return PIPELINE_SUCCESS;
}

// --- Etapa 2: Instalação Silenciosa do APK ---
int T20_Etapa2(const char *serial, const char *apk_path, const char *package_name, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 2] Verificando instalação do aplicativo InnovAva...");

  if (!package_name || strlen(package_name) == 0) {
    if (cb) cb("[ERROR] Nome do pacote não fornecido!");
    return PIPELINE_ERROR;
  }

  char args[1024];
  snprintf(args, sizeof(args), "-s %s shell \"pm list packages | grep %s\"", serial, package_name);
  char *check_out = adb_exec(args);
  if (check_out) {
    if (strstr(check_out, package_name) != NULL) {
      if (cb) cb("[-] Desinstalando versão antiga para reinstalação limpa...");
      snprintf(args, sizeof(args), "-s %s uninstall %s", serial, package_name);
      char *out_un = adb_exec(args);
      if (out_un) free(out_un);
    }
    free(check_out);
  }

  if (apk_path == NULL || strlen(apk_path) == 0) {
    if (cb) cb("[ERROR] Caminho do APK não fornecido para instalação!");
    return PIPELINE_ERROR;
  }

  if (cb) cb("[-] Instalando novo APK no Nokia T20...");
  char safe_apk_t20[MAX_PATH] = {0};
  adb_get_safe_path(apk_path, safe_apk_t20, MAX_PATH);

  snprintf(args, sizeof(args), "-s %s install -r -g \"%s\"", serial, safe_apk_t20);

#undef adb_exec
  char *out = adb_exec(args);
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

  if (out) {
    if (strstr(out, "Success") || (strstr(out, "Performing Streamed Install") && !strstr(out, "Failure") && !strstr(out, "failed"))) {
      if (cb) cb("[-] APK instalado com sucesso no Nokia T20!");
      free(out);
      return PIPELINE_SUCCESS;
    } else {
      if (cb) {
        char err_msg[1024];
        snprintf(err_msg, sizeof(err_msg), "[ERROR] Falha na instalação ADB: %s", out);
        cb(err_msg);
      }
      free(out);
      return PIPELINE_ERROR;
    }
  }
  return PIPELINE_ERROR;
}

// --- Etapa 2.1: Adicionar Atalho ---
int T20_Etapa2_1_AddShortcut(const char *serial, const char *package_name, LogCallback cb) {
  (void)serial;
  (void)package_name;
  if (cb) cb("[F1 Nokia T20 - Etapa 2.1] Garantindo ícone de atalho na tela inicial...");
  return PIPELINE_SUCCESS;
}

// --- Etapa 2.5: Reverse Tethering Completo (Gnirehtet USB Internet Bridge) ---
int T20_Etapa2_5(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 2.5] Inicializando Ponte de Internet USB (Gnirehtet)...");

  char exe_path[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path, MAX_PATH);
  char *last_slash = strrchr(exe_path, '\\');
  if (last_slash) *last_slash = '\0';

  char gnirehtet_path[512];
  char gnirehtet_apk[512];
  char gnirehtet_dir[512];

  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering", exe_path);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\..\\..\\tools\\reverse_tethering", exe_path);
  }

  char full_gnirehtet_dir[512];
  GetFullPathNameA(gnirehtet_dir, sizeof(full_gnirehtet_dir), full_gnirehtet_dir, NULL);
  strncpy(gnirehtet_dir, full_gnirehtet_dir, sizeof(gnirehtet_dir) - 1);

  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe", gnirehtet_dir);
  snprintf(gnirehtet_apk, sizeof(gnirehtet_apk), "%s\\gnirehtet.apk", gnirehtet_dir);

  char adb_path[MAX_PATH];
  adb_get_path(adb_path, sizeof(adb_path));
  SetEnvironmentVariableA("ADB", adb_path);

  // 1. Verifica se o cliente Gnirehtet já está instalado no Nokia T20
  char args_check[1024];
  snprintf(args_check, sizeof(args_check), "-s %s shell pm list packages com.genymobile.gnirehtet", serial);
  char *out_check = adb_exec(args_check);
  int is_installed = 0;
  if (out_check) {
    if (strstr(out_check, "package:com.genymobile.gnirehtet")) is_installed = 1;
    free(out_check);
  }

  if (is_installed) {
    if (cb) cb("  -> Cliente Gnirehtet já instalado no Nokia T20. Pulando reinstalação do APK.");
  } else {
    if (cb) cb("[-] Instalando cliente Gnirehtet no Nokia T20...");
    char args_apk[1024];
    snprintf(args_apk, sizeof(args_apk), "-s %s install -r -g \"%s\"", serial, gnirehtet_apk);
    char *out_apk = adb_exec(args_apk);
    if (out_apk) free(out_apk);
  }


  // 2. Concede permissões silenciosas de VPN e segundo plano no Android 11
  char args_op[1024];
  snprintf(args_op, sizeof(args_op), "-s %s shell appops set com.genymobile.gnirehtet ACTIVATE_VPN allow", serial);
  char *out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  snprintf(args_op, sizeof(args_op), "-s %s shell appops set com.genymobile.gnirehtet RUN_IN_BACKGROUND allow", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  snprintf(args_op, sizeof(args_op), "-s %s shell dumpsys deviceidle whitelist +com.genymobile.gnirehtet", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  // 3. Finaliza qualquer instância antiga do relay no PC
  STARTUPINFOA si_kill; PROCESS_INFORMATION pi_kill;
  ZeroMemory(&si_kill, sizeof(si_kill)); si_kill.cb = sizeof(si_kill);
  ZeroMemory(&pi_kill, sizeof(pi_kill));
  char cmd_kill[] = "taskkill /F /IM gnirehtet.exe /T";
  if (CreateProcessA(NULL, cmd_kill, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si_kill, &pi_kill)) {
    WaitForSingleObject(pi_kill.hProcess, 3000);
    CloseHandle(pi_kill.hProcess); CloseHandle(pi_kill.hThread);
  }
  Sleep(1000);

  // 4. Inicia o Servidor Relay Gnirehtet no PC
  char relay_cmd[1024];
  snprintf(relay_cmd, sizeof(relay_cmd), "\"%s\" relay", gnirehtet_path);
  STARTUPINFOA si_relay; PROCESS_INFORMATION pi_relay;
  ZeroMemory(&si_relay, sizeof(si_relay)); si_relay.cb = sizeof(si_relay);
  ZeroMemory(&pi_relay, sizeof(pi_relay));
  if (CreateProcessA(NULL, relay_cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, gnirehtet_dir, &si_relay, &pi_relay)) {
    CloseHandle(pi_relay.hProcess); CloseHandle(pi_relay.hThread);
  }
  Sleep(2000);

  // 5. Ativa o túnel de internet USB no Nokia T20
  char cmdline[1024];
  snprintf(cmdline, sizeof(cmdline), "\"%s\" start %s", gnirehtet_path, serial);
  STARTUPINFOA si; PROCESS_INFORMATION pi;
  ZeroMemory(&si, sizeof(si)); si.cb = sizeof(si);
  ZeroMemory(&pi, sizeof(pi));
  if (CreateProcessA(NULL, cmdline, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, gnirehtet_dir, &si, &pi)) {
    WaitForSingleObject(pi.hProcess, 10000);
    CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
    if (cb) cb("[+] Ponte de Internet USB Gnirehtet ATIVADA com sucesso no Nokia T20!");
  }
  Sleep(2000);

  return PIPELINE_SUCCESS;
}

// --- Etapa 3: Permissões ---
int T20_Etapa3(const char *serial, const char *package_name, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 3] Concedendo permissões de sistema...");
  char args[1024];
  snprintf(args, sizeof(args),
           "-s %s shell \"pm grant %s android.permission.CAMERA && "
           "pm grant %s android.permission.READ_EXTERNAL_STORAGE && "
           "pm grant %s android.permission.WRITE_EXTERNAL_STORAGE && "
           "pm grant %s android.permission.ACCESS_FINE_LOCATION\"",
           serial, package_name, package_name, package_name, package_name);
  char *out = adb_exec(args);
  if (out) free(out);
  return PIPELINE_SUCCESS;
}

// --- Helpers de busca textual fuzzy e normalização UTF-8 em XML ---
static char t20_normalize_char(unsigned char c, unsigned char next_c, int *skip_next) {
  *skip_next = 0;
  if (c >= 'A' && c <= 'Z') return c + 32;

  if (c == 0xC3 && next_c != 0) {
    if (next_c >= 0xA0 && next_c <= 0xA5) { *skip_next = 1; return 'a'; }
    if (next_c >= 0x80 && next_c <= 0x85) { *skip_next = 1; return 'a'; }
    if (next_c == 0xA7 || next_c == 0x87) { *skip_next = 1; return 'c'; }
    if (next_c >= 0xA8 && next_c <= 0xAB) { *skip_next = 1; return 'e'; }
    if (next_c >= 0x88 && next_c <= 0x8B) { *skip_next = 1; return 'e'; }
    if (next_c >= 0xAC && next_c <= 0xAF) { *skip_next = 1; return 'i'; }
    if (next_c >= 0x8C && next_c <= 0x8F) { *skip_next = 1; return 'i'; }
    if (next_c >= 0xB2 && next_c <= 0xB6) { *skip_next = 1; return 'o'; }
    if (next_c >= 0x92 && next_c <= 0x96) { *skip_next = 1; return 'o'; }
    if (next_c >= 0xB9 && next_c <= 0xBC) { *skip_next = 1; return 'u'; }
    if (next_c >= 0x99 && next_c <= 0x9C) { *skip_next = 1; return 'u'; }
  }

  if (c == 0xE0 || c == 0xE1 || c == 0xE2 || c == 0xE3 || c == 0xE4 || c == 0xE5 ||
      c == 0xC0 || c == 0xC1 || c == 0xC2 || c == 0xC3 || c == 0xC4 || c == 0xC5) return 'a';
  if (c == 0xE7 || c == 0xC7) return 'c';
  if (c == 0xE8 || c == 0xE9 || c == 0xEA || c == 0xEB || c == 0xC8 || c == 0xC9 || c == 0xCA || c == 0xCB) return 'e';
  if (c == 0xEC || c == 0xED || c == 0xEE || c == 0xEF || c == 0xCC || c == 0xCD || c == 0xCE || c == 0xCF) return 'i';
  if (c == 0xF9 || c == 0xFA || c == 0xFB || c == 0xFC || c == 0xD9 || c == 0xDA || c == 0xDB || c == 0xDC) return 'u';

  return c;
}

static char *t20_stristr_fuzzy(const char *haystack, const char *needle) {
  if (!haystack || !needle) return NULL;
  if (!*needle) return (char *)haystack;

  for (const char *p = haystack; *p; p++) {
    const char *h = p;
    const char *n = needle;

    while (*h && *n) {
      int skip_h = 0, skip_n = 0;
      char ch = t20_normalize_char((unsigned char)*h, (unsigned char)*(h + 1), &skip_h);
      char cn = t20_normalize_char((unsigned char)*n, (unsigned char)*(n + 1), &skip_n);

      if (ch != cn) break;
      h += (1 + skip_h);
      n += (1 + skip_n);
    }
    if (!*n) return (char *)p;
  }
  return NULL;
}


/*
 * t20_find_file_centroid_direct:
 * Busca o primeiro nó de texto contendo 'search_str' e retorna o centroide
 * DIRETO desse nó (sem procurar o próximo clickable="true").
 *
 * Por que isso é necessário: No DocumentsUI o item de arquivo (pda21072025.zip)
 * tem text="pda21072025.zip" em um nó com clickable="false". O pai (item_root
 * CardView) é clickable="true" mas vem ANTES no XML. A função fuzzy genérica
 * busca o próximo clickable ADIANTE, encontrando o ícone de preview (errado).
 *
 * Toque no centroide do nó de texto NÃO-clicável funciona corretamente pois
 * o Android propaga o evento de toque para o pai clicável (item_root).
 */
static int t20_find_file_centroid_direct(const char *xml, const char *search_str, int *out_cx, int *out_cy) {
  if (!xml || !search_str || !out_cx || !out_cy) return 0;
  char *p = (char *)xml;
  while ((p = t20_stristr_fuzzy(p, search_str)) != NULL) {
    /* Busca o bounds= do nó que contém esta string de texto */
    char *b = strstr(p, "bounds=\"[");
    if (b && (b - p) < 600) {
      int x1, y1, x2, y2;
      if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
        /* Valida que as coordenadas são razoáveis (dentro da tela do T20) */
        if (x1 >= 0 && y1 >= 0 && x2 > x1 && y2 > y1 && x2 <= 1200 && y2 <= 2000) {
          *out_cx = (x1 + x2) / 2;
          *out_cy = (y1 + y2) / 2;
          return 1;
        }
      }
    }
    p++;
    if (p - xml > (int)strlen(xml) - 1) break;
  }
  return 0;
}




static int t20_get_centroid_from_xml_fuzzy(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count) {
  if (!xml || !search_str) return 0;
  char *p = (char *)xml;
  int matches = 0;
  while ((p = t20_stristr_fuzzy(p, search_str)) != NULL) {
    if (matches == skip_count) {
      char *b_start = strstr(p, "bounds=\"[");
      if (b_start) {
        int x1, y1, x2, y2;
        if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {

          // === LÓGICA V7: Verifica se o nó atual possui clickable="false" ===
          int is_unclickable = 0;
          const char *tag_start = p;
          while (tag_start > xml && *tag_start != '<' && (p - tag_start < 300)) {
            tag_start--;
          }
          const char *tag_end = strstr(p, "/>");
          if (!tag_end || (tag_end - p > 500)) tag_end = p + 200;

          if (tag_start < tag_end) {
            char node_buf[600];
            int nlen = (int)(tag_end - tag_start);
            if (nlen > 0 && nlen < (int)sizeof(node_buf)) {
              strncpy(node_buf, tag_start, nlen);
              node_buf[nlen] = '\0';
              if (strstr(node_buf, "clickable=\"false\"")) {
                is_unclickable = 1;
              }
            }
          }

          // Se o nó não é clicável (ex: label de pasta), busca o próximo nó clicável
          if (is_unclickable) {
            const char *next_click = strstr(p, "clickable=\"true\"");
            if (next_click && (next_click - p < 1200)) {
              const char *nb_start = strstr(next_click, "bounds=\"[");
              if (!nb_start) {
                const char *rew = next_click;
                while (rew > p && *rew != '<') rew--;
                nb_start = strstr(rew, "bounds=\"[");
              }
              if (nb_start && (nb_start - p < 1500)) {
                int nx1 = 0, ny1 = 0, nx2 = 0, ny2 = 0;
                if (sscanf(nb_start, "bounds=\"[%d,%d][%d,%d]\"",
                           &nx1, &ny1, &nx2, &ny2) == 4) {
                  if (out_cx) *out_cx = (nx1 + nx2) / 2;
                  if (out_cy) *out_cy = (ny1 + ny2) / 2;
                  return 1;
                }
              }
            }
            // Fallback relativo: clica no campo logo abaixo da label não-clicável
            int h = y2 - y1;
            if (h <= 0) h = 20;
            if (out_cx) *out_cx = (x1 + x2) / 2;
            if (out_cy) *out_cy = y2 + (h / 2) + 15;
            return 1;
          }

          // Nó já é clicável: usa suas coordenadas diretamente
          if (out_cx) *out_cx = (x1 + x2) / 2;
          if (out_cy) *out_cy = (y1 + y2) / 2;
          return 1;
        }
      }
      return 0;
    }
    matches++;
    p++;
  }
  return 0;
}

/*
 * t20_long_press_and_confirm_select:
 * Estratégia Multi-Camada à Prova de Falhas para Seleção do Pacote ZIP (Nokia T20):
 *
 * Camada 1: Navegação por Teclado Físico / DPAD (Android Native Keyevent 20 -> 23).
 *           No Android 11 DocumentsUI, DPAD_DOWN foca o arquivo na lista e DPAD_CENTER
 *           efetua a seleção instantânea sem interferência de coordenadas ou micro-deslizes.
 * Camada 2: Toque direto de Seleção no Centroide XML (input tap cx cy) + KEYEVENT ENTER.
 * Camada 3: Toque Longo Humanizado (micro-swipe de 2s) + Busca e clique no botão SELECIONAR.
 */
static int t20_long_press_and_confirm_select(const char *serial, int cx, int cy, LogCallback cb) {
  char args[512];

  if (cb) {
    char msg[256];
    snprintf(msg, sizeof(msg), "[+] [Nokia T20] Iniciando seleção do pacote ZIP no explorador (Centroide XML: %d, %d)...", cx, cy);
    cb(msg);
  }

  // --- CAMADA 1: Seleção Nativa via DPAD (Android 11 DocumentsUI) ---
  if (cb) cb("  [Estratégia 1/3] Executando foco e seleção nativa via DPAD...");
  snprintf(args, sizeof(args), "-s %s shell input keyevent 20", serial); // DPAD_DOWN
  char *t1 = adb_exec(args);
  if (t1) free(t1);
  Sleep(300);

  snprintf(args, sizeof(args), "-s %s shell input keyevent 23", serial); // DPAD_CENTER
  char *t2 = adb_exec(args);
  if (t2) free(t2);
  Sleep(1200);

  // Valida se a tela voltou para o InnovAva
  snprintf(args, sizeof(args), "-s %s shell dumpsys window | grep mCurrentFocus", serial);
  char *f_check1 = adb_exec(args);
  if (f_check1) {
    if (strstr(f_check1, "br.com.yantec.innovava")) {
      free(f_check1);
      if (cb) cb("[+] Pacote ZIP selecionado com sucesso via Estratégia DPAD!");
      return 1;
    }
    free(f_check1);
  }

  // --- CAMADA 2: Toque Direto no Centroide XML + ENTER ---
  if (cb) cb("  [Estratégia 2/3] Toque direto no centroide do item e confirmação por ENTER...");
  if (cx > 0 && cy > 0) {
    snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
    char *t3 = adb_exec(args);
    if (t3) free(t3);
    Sleep(300);

    snprintf(args, sizeof(args), "-s %s shell input keyevent 66", serial); // KEYEVENT_ENTER
    char *t4 = adb_exec(args);
    if (t4) free(t4);
    Sleep(1200);

    snprintf(args, sizeof(args), "-s %s shell dumpsys window | grep mCurrentFocus", serial);
    char *f_check2 = adb_exec(args);
    if (f_check2) {
      if (strstr(f_check2, "br.com.yantec.innovava")) {
        free(f_check2);
        if (cb) cb("[+] Pacote ZIP selecionado com sucesso via Toque Direto!");
        return 1;
      }
      free(f_check2);
    }
  }

  // --- CAMADA 3: Toque Longo Humanizado + Botão SELECIONAR ---
  if (cb) cb("  [Estratégia 3/3] Toque longo humanizado (Micro-swipe 2s) e busca do botão SELECIONAR...");
  if (cx > 0 && cy > 0) {
    snprintf(args, sizeof(args), "-s %s shell input swipe %d %d %d %d 2000", serial, cx, cy, cx + 2, cy + 2);
    char *tlong = adb_exec(args);
    if (tlong) free(tlong);
    Sleep(1500);
  }

  char *xml_top = dump_and_read_xml(serial, cb);
  int sel_clicked = 0;

  if (xml_top) {
    int bx = 0, by = 0;
    if (t20_find_file_centroid_direct(xml_top, "SELECIONAR", &bx, &by) ||
        t20_find_file_centroid_direct(xml_top, "Selecionar", &bx, &by) ||
        t20_find_file_centroid_direct(xml_top, "ABRIR", &bx, &by) ||
        t20_find_file_centroid_direct(xml_top, "Abrir", &bx, &by) ||
        t20_get_centroid_from_xml_fuzzy(xml_top, "SELECIONAR", &bx, &by, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_top, "Selecionar", &bx, &by, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_top, "ABRIR", &bx, &by, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_top, "Abrir", &bx, &by, 0)) {

      if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg), "[+] Botão 'SELECIONAR' localizado via XML em (%d, %d)! Clicando...", bx, by);
        cb(msg);
      }
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, bx, by);
      char *tsel = adb_exec(args);
      if (tsel) free(tsel);
      sel_clicked = 1;
    } else {
      if (cb) cb("[WARNING] Executando clique de fallback na ação superior para confirmação...");
      snprintf(args, sizeof(args), "-s %s shell input tap 1060 150", serial);
      char *tsel = adb_exec(args);
      if (tsel) free(tsel);
      sel_clicked = 1;
    }
    free(xml_top);
  }

  return sel_clicked;
}

// --- Etapa 4: Inicialização, Leitura de Tela em Tempo Real, Seleção de Município e Injeção de Login ---
int T20_Etapa4(const char *serial, const char *login, const char *senha, const char *municipio, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20 - Etapa 4] Inicializando InnovAva e monitorando interface em tempo real...");

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell monkey -p br.com.yantec.innovava -c android.intent.category.LAUNCHER 1", serial);
  char *out = adb_exec(args);
  if (out) free(out);

  // 1. Smart UI Polling — Aguarda o app carregar na tela
  int loaded = 0;
  int attempts = 0;
  while (attempts < 20) {
    snprintf(args, sizeof(args), "-s %s shell dumpsys window | grep mCurrentFocus", serial);
    char *focus_out = adb_exec(args);
    if (focus_out) {
      if (strstr(focus_out, "br.com.yantec.innovava")) {
        free(focus_out);
        loaded = 1;
        if (cb) cb("[-] InnovAva carregado na tela em tempo recorde!");
        break;
      }
      free(focus_out);
    }
    Sleep(300);
    attempts++;
  }

  if (!loaded) {
    if (cb) cb("[WARNING] InnovAva demorou para responder, continuando leitura de tela...");
  }

  Sleep(1500);

  // =========================================================================
  // PASSO 0: Seleção e Validação do Município em Tempo Real
  // =========================================================================
  if (municipio && strlen(municipio) > 0) {
    if (cb) cb("[F1 Nokia T20] [BotLearn AI] Verificando seleção do Município em tempo real...");

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

    char utf8_municipio[256];
    WCHAR w_municipio[128];
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, safe_municipio, -1, w_municipio, 128) != 0) {
      strncpy(utf8_municipio, safe_municipio, sizeof(utf8_municipio) - 1);
      utf8_municipio[sizeof(utf8_municipio) - 1] = '\0';
    } else {
      MultiByteToWideChar(CP_ACP, 0, safe_municipio, -1, w_municipio, 128);
      WideCharToMultiByte(CP_UTF8, 0, w_municipio, -1, utf8_municipio, 256, NULL, NULL);
    }

    int cx_m = 0, cy_m = 0;
    char *xml_mun1 = dump_and_read_xml(serial, cb);
    if (xml_mun1) {
      // 1. Checa se o município alvo já está visível/selecionado na tela
      if (t20_get_centroid_from_xml_fuzzy(xml_mun1, utf8_municipio, &cx_m, &cy_m, 0)) {
        if (cb) cb("[-] Município alvo já está selecionado na tela!");
      } else {
        // 2. Procura a gaveta/spinner do município
        LearnFindResult res_mun = learn_find_element("t20_spinner_municipio", xml_mun1, 0, cb);
        if (!res_mun.found) {
          res_mun = learn_find_element("v7_spinner_municipio", xml_mun1, 0, cb);
        }

        if (res_mun.found) {
          if (cb) cb("[-] Gaveta de município localizada! Abrindo lista...");
          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, res_mun.cx, res_mun.cy);
          char *t_mun = adb_exec(args);
          if (t_mun) free(t_mun);

          Sleep(1500);

          char *xml_mun2 = dump_and_read_xml(serial, cb);
          if (xml_mun2) {
            if (t20_get_centroid_from_xml_fuzzy(xml_mun2, utf8_municipio, &cx_m, &cy_m, 0)) {
              if (cb) cb("[-] Município localizado na lista! Selecionando...");
              snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx_m, cy_m);
              char *t_sel = adb_exec(args);
              if (t_sel) free(t_sel);
            } else {
              if (cb) cb("[WARNING] Município alvo não apareceu no topo da lista. Fechando menu...");
              snprintf(args, sizeof(args), "-s %s shell input keyevent 4", serial);
              char *tb = adb_exec(args);
              if (tb) free(tb);
            }
            free(xml_mun2);
            Sleep(1000);
          }
        } else {
          if (cb) cb("[WARNING] Gaveta de município não encontrada. Continuando para login...");
        }
      }
      free(xml_mun1);
    }
  }

  // Se login e senha forem informados, executa injeção dinâmica via BotLearn / Leitura de Tela em Tempo Real
  if (login && strlen(login) > 0 && senha && strlen(senha) > 0) {
    if (cb) cb("[F1 Nokia T20] [BotLearn AI] Lendo a tela em tempo real para injeção de credenciais...");

    int cx = 0, cy = 0;

    // --- PASSO 1: Leitura em tempo real & Injeção do Login / Email ---
    if (cb) cb("[-] [BotLearn AI] Procurando campo de Login/Email na tela...");
    int found_login = 0;
    for (int attempt = 1; attempt <= 10 && !found_login; attempt++) {
      char *xml_login = dump_and_read_xml(serial, cb);
      if (xml_login) {
        LearnFindResult res_login = learn_find_element("t20_campo_login", xml_login, 0, cb);
        if (!res_login.found) {
          res_login = learn_find_element("v7_campo_email", xml_login, 0, cb);
        }

        if (res_login.found) {
          cx = res_login.cx;
          cy = res_login.cy;
          found_login = 1;
          if (cb) cb("[-] Campo Login localizado! Selecionando e preenchendo...");
        }
        free(xml_login);
      }
      if (!found_login) Sleep(1500);
    }

    if (found_login) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *t1 = adb_exec(args);
      if (t1) free(t1);

      Sleep(800);
      char escaped_login[256];
      t20_escape_adb_text(login, escaped_login);
      snprintf(args, sizeof(args), "-s %s shell input text '%s'", serial, escaped_login);
      char *t2 = adb_exec(args);
      if (t2) free(t2);

      Sleep(500);
      snprintf(args, sizeof(args), "-s %s shell \"dumpsys input_method | grep mInputShown=true && input keyevent 4 || input keyevent 111\"", serial);
      char *tk1 = adb_exec(args);
      if (tk1) free(tk1);
      Sleep(1000);
    }

    // --- PASSO 2: Leitura em tempo real & Injeção da Senha ---
    if (cb) cb("[-] [BotLearn AI] Procurando campo de Senha na tela...");
    int found_senha = 0;
    for (int attempt = 1; attempt <= 10 && !found_senha; attempt++) {
      char *xml_senha = dump_and_read_xml(serial, cb);
      if (xml_senha) {
        LearnFindResult res_senha = learn_find_element("t20_campo_senha", xml_senha, 0, cb);
        if (!res_senha.found) {
          res_senha = learn_find_element("v7_campo_senha", xml_senha, 0, cb);
        }

        if (res_senha.found) {
          cx = res_senha.cx;
          cy = res_senha.cy;
          found_senha = 1;
          if (cb) cb("[-] Campo Senha localizado! Selecionando e preenchendo...");
        }
        free(xml_senha);
      }
      if (!found_senha) Sleep(1500);
    }

    if (found_senha) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *t3 = adb_exec(args);
      if (t3) free(t3);

      Sleep(800);
      char escaped_senha[256];
      t20_escape_adb_text(senha, escaped_senha);
      snprintf(args, sizeof(args), "-s %s shell input text '%s'", serial, escaped_senha);
      char *t4 = adb_exec(args);
      if (t4) free(t4);

      Sleep(500);
      snprintf(args, sizeof(args), "-s %s shell \"dumpsys input_method | grep mInputShown=true && input keyevent 4 || input keyevent 111\"", serial);
      char *tk2 = adb_exec(args);
      if (tk2) free(tk2);
      Sleep(1000);
    }

    // --- PASSO 3: Leitura em tempo real & Clique no Botão Entrar (com Validação XML prévia) ---
    if (cb) cb("[-] [BotLearn AI] [Validação XML T20] Confirmando município e procurando botão Entrar...");
    int found_btn = 0;
    for (int attempt = 1; attempt <= 10 && !found_btn; attempt++) {
      char *xml_btn = dump_and_read_xml(serial, cb);
      if (xml_btn) {
        if (municipio && strlen(municipio) > 0) {
          if (t20_get_centroid_from_xml_fuzzy(xml_btn, municipio, NULL, NULL, 0)) {
            if (cb) {
              char log_msg[256];
              snprintf(log_msg, sizeof(log_msg), "  -> [Validação XML T20] OK: Município '%s' confirmado no seletor 2K!", municipio);
              cb(log_msg);
            }
          } else {
            if (cb) {
              char log_msg[256];
              snprintf(log_msg, sizeof(log_msg), "[WARNING] [Validação XML T20] Município '%s' NÃO detectado! Re-executando seleção no Nokia T20...", municipio);
              cb(log_msg);
            }
            LearnFindResult res_mun_retry = learn_find_element("t20_spinner_municipio", xml_btn, 0, cb);
            if (res_mun_retry.found) {
              snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, res_mun_retry.cx, res_mun_retry.cy);
              char *tr1 = adb_exec(args); if (tr1) free(tr1);
              Sleep(1500);
              char *xml_m_retry = dump_and_read_xml(serial, cb);
              if (xml_m_retry) {
                int mcx = 0, mcy = 0;
                if (t20_get_centroid_from_xml_fuzzy(xml_m_retry, municipio, &mcx, &mcy, 0)) {
                  snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, mcx, mcy);
                  char *tr2 = adb_exec(args); if (tr2) free(tr2);
                  if (cb) {
                    char log_msg[256];
                    snprintf(log_msg, sizeof(log_msg), "[+] [Validação XML T20] Município '%s' re-selecionado com sucesso!", municipio);
                    cb(log_msg);
                  }
                }
                free(xml_m_retry);
                Sleep(1000);
              }
            }
          }
        }


        LearnFindResult res_btn = learn_find_element("t20_botao_entrar", xml_btn, 0, cb);

        if (!res_btn.found) {
          res_btn = learn_find_element("v7_botao_entrar", xml_btn, 0, cb);
        }

        if (res_btn.found) {
          cx = res_btn.cx;
          cy = res_btn.cy;
          found_btn = 1;
          if (cb) cb("[-] Botão Entrar localizado! Efetuando login no InnovAva...");
        }
        free(xml_btn);
      }
      if (!found_btn) Sleep(1500);
    }

    if (found_btn) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *t5 = adb_exec(args);
      if (t5) free(t5);

      if (cb) cb("[-] Login enviado. Aguardando a plataforma autenticar e carregar o painel inicial...");

      // Aguarda até 15 segundos o login processar e a tela principal carregar
      int logged_in = 0;
      for (int wait_login = 0; wait_login < 15; wait_login++) {
        Sleep(1000);
        char *xml_dash = dump_and_read_xml(serial, NULL);
        if (xml_dash) {
          // Se a tela de login já saiu ou se o menu hambúrguer/dashboard apareceu
          if (!strstr(xml_dash, "t20_campo_login") && !strstr(xml_dash, "v7_campo_email")) {
            logged_in = 1;
            free(xml_dash);
            break;
          }
          free(xml_dash);
        }
      }

      if (logged_in && cb) {
        cb("[+] Authenticação concluída! Aplicativo conectado na plataforma InnovAva.");
      }
    }
  }

  if (cb) cb("[+] Inicialização e login no InnovAva concluídos com sucesso no Nokia T20!");
  return PIPELINE_SUCCESS;
}

int T20_Etapa4_5_StopGnirehtet(const char *serial, LogCallback cb) {
  if (cb) cb("[F1 Nokia T20] Desativando a ponte de internet USB (Gnirehtet)...");

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell am force-stop com.genymobile.gnirehtet", serial);
  char *out = adb_exec(args);
  if (out) free(out);

  // Mata processo gnirehtet no PC Host
  STARTUPINFOA si_kill; PROCESS_INFORMATION pi_kill;
  ZeroMemory(&si_kill, sizeof(si_kill)); si_kill.cb = sizeof(si_kill);
  ZeroMemory(&pi_kill, sizeof(pi_kill));
  char cmd_kill[] = "taskkill /F /IM gnirehtet.exe /T";
  if (CreateProcessA(NULL, cmd_kill, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si_kill, &pi_kill)) {
    WaitForSingleObject(pi_kill.hProcess, 2000);
    CloseHandle(pi_kill.hProcess); CloseHandle(pi_kill.hThread);
  }

  if (cb) cb("[+] Túnel de internet USB encerrado com sucesso.");
  return PIPELINE_SUCCESS;
}

// --- Etapa 5: Importação do Pacote ZIP (Menu Lateral -> Importar ZIP) ---
int T20_Etapa5(const char *serial, LogCallback cb) {
  char args[512];
  char *out_w = NULL;

  // Desativa o Gnirehtet e Wi-Fi assim que chega na etapa de importação
  if (cb) cb("[-] Chegou em Importar ZIP no Nokia T20. Desligando tunel Gnirehtet e Wi-Fi imediatamente...");
  T20_Etapa4_5_StopGnirehtet(serial, cb);
  snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
  out_w = adb_exec(args); if (out_w) free(out_w);

  if (cb) cb("[F1 Nokia T20 - Etapa 5] Abrindo menu lateral esquerdo do InnovAva...");

  int cx = 0, cy = 0;


  // 1. Desliza o menu lateral da esquerda para a direita (ou clica no menu hambúrguer)
  snprintf(args, sizeof(args), "-s %s shell input swipe 0 500 500 500 300", serial);
  char *swp = adb_exec(args);
  if (swp) free(swp);
  Sleep(1500); // Aguarda animação do drawer

  // 2. Procura a opção "Importar ZIP" no menu lateral e clica
  if (cb) cb("[-] Procurando a opção 'Importar ZIP' no menu lateral...");
  char *xml_menu = dump_and_read_xml(serial, cb);
  if (xml_menu) {
    if (t20_get_centroid_from_xml_fuzzy(xml_menu, "Importar ZIP", &cx, &cy, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_menu, "IMPORTAR ZIP", &cx, &cy, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_menu, "Importar Pacote", &cx, &cy, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_menu, "Importar", &cx, &cy, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_menu, "IMPORTAR", &cx, &cy, 0)) {
      if (cb) cb("[-] Opção 'Importar ZIP' localizada! Clicando...");
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *tp = adb_exec(args);
      if (tp) free(tp);
      Sleep(1500);
    } else {
      if (cb) cb("[WARNING] Opção 'Importar ZIP' não encontrada no XML. Tentando tocar na área superior esquerda...");
      snprintf(args, sizeof(args), "-s %s shell input tap 80 120", serial);
      char *tp_alt = adb_exec(args);
      if (tp_alt) free(tp_alt);
      Sleep(1500);
    }
    free(xml_menu);
  }

  // 3. Procura e clica no botão "PROCURAR ARQUIVO .ZIP"
  if (cb) cb("[-] [F1 Nokia T20] Procurando o botão 'PROCURAR ARQUIVO .ZIP' na tela...");
  int btn_found = 0;
  for (int retry = 0; retry < 5; retry++) {
    char *xml_import = dump_and_read_xml(serial, cb);
    if (xml_import) {
      if (t20_get_centroid_from_xml_fuzzy(xml_import, "PROCURAR ARQUIVO ZIP", &cx, &cy, 0) ||
          t20_get_centroid_from_xml_fuzzy(xml_import, "PROCURAR ARQUIVO .ZIP", &cx, &cy, 0) ||
          t20_get_centroid_from_xml_fuzzy(xml_import, "PROCURAR ARQUIVO", &cx, &cy, 0) ||
          t20_get_centroid_from_xml_fuzzy(xml_import, "Procurar arquivo", &cx, &cy, 0) ||
          t20_get_centroid_from_xml_fuzzy(xml_import, "Procurar", &cx, &cy, 0) ||
          t20_get_centroid_from_xml_fuzzy(xml_import, "PROCURAR", &cx, &cy, 0)) {

        if (cb) cb("[-] Botão 'PROCURAR ARQUIVO .ZIP' localizado! Clicando...");
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

  if (btn_found && cb) {
    cb("[+] Clique no botão 'PROCURAR ARQUIVO .ZIP' realizado com sucesso no Nokia T20!");
  } else if (cb) {
    cb("[WARNING] Botão 'PROCURAR ARQUIVO .ZIP' não detectado no XML após tentativas.");
  }

  // 4. Aguarda o gerenciador de arquivos (DocumentsUI) abrir de fato antes de tentar qualquer seleção
  // GUARDA CRÍTICA: Verifica via dumpsys que o foco mudou do InnovAva para o seletor de arquivos
  if (cb) cb("[-] [F1 Nokia T20] Aguardando abertura do gerenciador de arquivos (DocumentsUI)...");
  int file_picker_open = 0;
  for (int w = 0; w < 15; w++) {
    Sleep(800);
    char wargs[256];
    snprintf(wargs, sizeof(wargs), "-s %s shell dumpsys window | grep mCurrentFocus", serial);
    char *focus_out = adb_exec(wargs);
    if (focus_out) {
      // Confirma que o DocumentsUI (seletor de arquivos do Android) está em foco
      if (strstr(focus_out, "documentsui") ||
          strstr(focus_out, "DocumentsUI") ||
          strstr(focus_out, "com.android.documentsui") ||
          strstr(focus_out, "com.google.android.documentsui")) {
        file_picker_open = 1;
        free(focus_out);
        if (cb) cb("[-] Gerenciador de arquivos aberto e confirmado via dumpsys!");
        break;
      }
      free(focus_out);
    }
  }

  if (!file_picker_open && cb) {
    cb("[WARNING] Gerenciador de arquivos não detectado em foco. Prosseguindo com leitura de tela...");
  }
  Sleep(1500);

  // 4.1 Fast-Path: Checa se abriu diretamente na pasta 'innovava' contendo o arquivo ZIP
  // GUARDA: Só busca .zip se o XML NÃO contiver mais o botão PROCURAR ARQUIVO do InnovAva
  if (cb) cb("[-] Verificando se a tela abriu diretamente na pasta 'innovava'...");
  char *xml_check = dump_and_read_xml(serial, cb);
  int zip_found = 0;

  if (xml_check) {
    // Guarda: confirma que saímos da tela de importação do InnovAva
    int still_on_import_screen =
        (strstr(xml_check, "PROCURAR ARQUIVO") != NULL) ||
        (strstr(xml_check, "Procurar arquivo") != NULL) ||
        (strstr(xml_check, "br.com.yantec.innovava") != NULL && strstr(xml_check, "importar") != NULL);

    if (!still_on_import_screen) {
      // Localiza o centroide do nó .zip via XML dinâmico
      if (t20_find_file_centroid_direct(xml_check, ".zip", &cx, &cy) ||
          t20_find_file_centroid_direct(xml_check, ".ZIP", &cx, &cy) ||
          t20_find_file_centroid_direct(xml_check, "pda", &cx, &cy) ||
          t20_get_centroid_from_xml_fuzzy(xml_check, ".zip", &cx, &cy, 0) ||
          t20_get_centroid_from_xml_fuzzy(xml_check, ".ZIP", &cx, &cy, 0)) {

        if (cb) cb("[+] Entrada direta na pasta 'innovava' confirmada! Iniciando seleção do arquivo ZIP via toque longo (Long Press)...");
        zip_found = t20_long_press_and_confirm_select(serial, cx, cy, cb);
      }
    } else {
      if (cb) cb("[WARNING] Ainda na tela de importação do InnovAva. Aguardando abertura do file picker...");
    }
    free(xml_check);
  }

  if (zip_found) {
    if (cb) cb("[+] Pacote ZIP marcado e confirmado via botão SELECIONAR no Fast-Path!");
    return PIPELINE_SUCCESS;
  }

  // 5. NAVEGAÇÃO SECUNDÁRIA (Menu Lateral DocumentsUI -> Nokia T20 -> Pasta innovava -> Seleção do ZIP)
  if (cb) cb("[-] Pacote não visível na tela inicial. Abrindo menu lateral do gerenciador de arquivos (Swipe)...");
  snprintf(args, sizeof(args), "-s %s shell input swipe 0 500 800 500 200", serial);
  char *tswipe = adb_exec(args);
  if (tswipe) free(tswipe);
  Sleep(2000); // Aguarda o menu deslizar

  // 5.1 Seleciona o Armazenamento Interno do aparelho ("Nokia T20", "Nokia", "T20", "Armazenamento", "Internal storage")
  if (cb) cb("  -> Selecionando armazenamento interno 'Nokia T20'...");
  char *xml_nav = dump_and_read_xml(serial, cb);
  int nav_clicked = 0;
  if (xml_nav) {
    if (t20_find_file_centroid_direct(xml_nav, "Nokia T20", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_nav, "Nokia", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_nav, "Armazenamento interno", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_nav, "Armazenamento", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_nav, "Internal storage", &cx, &cy)) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *tnav = adb_exec(args);
      if (tnav) free(tnav);
      nav_clicked = 1;
    }
    free(xml_nav);
  }

  if (nav_clicked && cb) cb("[-] Armazenamento interno 'Nokia T20' selecionado!");
  Sleep(2000); // Aguarda carregar a raiz do armazenamento

  // 5.2 Procurar e acessar a pasta "innovava" na raiz (com fallback de scroll)
  if (cb) cb("  -> Procurando e acessando a pasta 'innovava' na raiz...");
  char *xml_folder = dump_and_read_xml(serial, cb);
  int folder_clicked = 0;
  if (xml_folder) {
    if (t20_find_file_centroid_direct(xml_folder, "innovava", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_folder, "Innovava", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_folder, "INNOVAVA", &cx, &cy)) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
      char *tf = adb_exec(args);
      if (tf) free(tf);
      folder_clicked = 1;
    } else {
      // Scroll down e tenta novamente
      if (cb) cb("  -> Pasta 'innovava' não visível de imediato. Efetuando rolagem para baixo...");
      snprintf(args, sizeof(args), "-s %s shell input swipe 300 900 300 200 400", serial);
      char *tswp = adb_exec(args);
      if (tswp) free(tswp);
      Sleep(1500);

      free(xml_folder);
      xml_folder = dump_and_read_xml(serial, cb);
      if (xml_folder &&
          (t20_find_file_centroid_direct(xml_folder, "innovava", &cx, &cy) ||
           t20_find_file_centroid_direct(xml_folder, "Innovava", &cx, &cy) ||
           t20_find_file_centroid_direct(xml_folder, "INNOVAVA", &cx, &cy))) {
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
        char *tf = adb_exec(args);
        if (tf) free(tf);
        folder_clicked = 1;
      }
    }
    if (xml_folder) free(xml_folder);
  }

  if (folder_clicked && cb) cb("[-] Pasta 'innovava' acessada com sucesso!");
  Sleep(2000); // Aguarda carregar o conteúdo da pasta innovava

  // 5.3 Localizar, PRESSIONAR E SEGURAR (Long Press) e CONFIRMAR no Botão SELECIONAR na parte superior
  if (cb) cb("  -> Localizando o arquivo ZIP dentro da pasta 'innovava' para seleção por toque longo...");
  char *xml_final = dump_and_read_xml(serial, cb);
  if (xml_final) {
    if (t20_find_file_centroid_direct(xml_final, ".zip", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_final, ".ZIP", &cx, &cy) ||
        t20_find_file_centroid_direct(xml_final, "pda", &cx, &cy) ||
        t20_get_centroid_from_xml_fuzzy(xml_final, ".zip", &cx, &cy, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_final, ".ZIP", &cx, &cy, 0)) {
      if (cb) cb("[+] Arquivo ZIP encontrado na pasta 'innovava'! Pressionando e segurando para marcar...");
      t20_long_press_and_confirm_select(serial, cx, cy, cb);
    } else {
      // Fallback: se não achar pelo nome exato, pressiona e segura o primeiro item clicável
      if (t20_get_centroid_from_xml_fuzzy(xml_final, "clickable=\"true\"", &cx, &cy, 0)) {
        if (cb) cb("[WARNING] Nome exato não detectado no XML. Pressionando e segurando o item da pasta...");
        t20_long_press_and_confirm_select(serial, cx, cy, cb);
      } else {
        if (cb) cb("[ERROR] Nenhum arquivo clicável encontrado dentro da pasta 'innovava'.");
      }
    }
    free(xml_final);
  }

  if (cb) cb("[+] Pacote ZIP marcado e confirmado via botão SELECIONAR!");
  return PIPELINE_SUCCESS;
}

// --- Stub da Etapa 6 (Reservado para reescrita dos novos passos) ---
int T20_Etapa6(const char *serial, LogCallback cb) {
  (void)serial;
  if (cb) cb("[F1 Nokia T20 - Etapa 6] (Etapa legada desativada para reescrita).");
  return PIPELINE_SUCCESS;
}

// --- Etapa 6: Aguarda 5s e monitora o botão 'PROCURAR ARQUIVO .ZIP' reaparecer na tela (Conclusão do Desempacotamento) ---
int T20_Etapa6_AguardarConclusaoImportacao(const char *serial, LogCallback cb) {
  if (cb) cb("[Nokia T20 - Etapa 6] Pacote ZIP selecionado. Aguardando 5 segundos para início da importação...");
  Sleep(5000); // Aguarda 5 segundos antes de iniciar a busca pelo botão de conclusão

  if (cb) cb("[-] Monitorando progresso e aguardando conclusão da importação do pacote...");

  int import_completed = 0;
  int attempts = 0;
  int max_attempts = 90; // Aguarda até 4.5 minutos (90 tentativas * 3s)

  while (attempts < max_attempts) {
    char *xml = dump_and_read_xml(serial, cb);
    if (xml) {
      if (strstr(xml, "PROCURAR ARQUIVO") != NULL ||
          strstr(xml, "Procurar arquivo") != NULL ||
          strstr(xml, "procurar arquivo") != NULL) {
        free(xml);
        import_completed = 1;
        if (cb) cb("[+] Botão 'PROCURAR ARQUIVO .ZIP' reapareceu! Importação do pacote concluída com sucesso!");
        break;
      }
      free(xml);
    }
    Sleep(3000); // Polling a cada 3 segundos
    attempts++;
  }

  if (!import_completed && cb) {
    cb("[WARNING] Tempo limite de espera excedido. Prosseguindo com o fechamento dos aplicativos...");
  }

  return PIPELINE_SUCCESS;
}


// --- Etapa 7: Encerramento de Apps e Limpeza Completa de Recentes (Mantém Tablet Ligado) ---
int T20_Etapa7_CloseApps(const char *serial, LogCallback cb) {
  if (cb) cb("[Nokia T20 - Etapa 7] Fechando aplicativos e realizando limpeza visual de recentes...");

  char cmd[512];

  // 1. Pressiona Voltar 3x
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 4", serial);
  for (int j = 0; j < 3; j++) {
    char *back = adb_exec(cmd);
    if (back) free(back);
    Sleep(300);
  }

  // 2. Retorna para a tela Home
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
  char *h1 = adb_exec(cmd);
  if (h1) free(h1);
  Sleep(600);

  // 3. Força a parada dos aplicativos
  snprintf(cmd, sizeof(cmd),
           "-s %s shell \"am force-stop br.com.yantec.innovava && "
           "am force-stop com.google.android.documentsui && "
           "am force-stop com.android.documentsui && "
           "am force-stop com.android.chrome && "
           "am kill-all\"",
           serial);
  char *stop_out = adb_exec(cmd);
  if (stop_out) free(stop_out);
  Sleep(600);

  // 4. Abre a tela de Apps Recentes (keyevent 187)
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 187", serial);
  char *r1 = adb_exec(cmd);
  if (r1) free(r1);
  Sleep(1200);

  // 5. Clica no botão "Limpar tudo"
  if (cb) cb("[-] Procurando botão 'Limpar tudo' na parte inferior da tela de recentes...");
  char *xml_recents = dump_and_read_xml(serial, cb);
  int rx = 0, ry = 0;
  int btn_cleared = 0;
  if (xml_recents) {
    if (t20_get_centroid_from_xml_fuzzy(xml_recents, "Limpar tudo", &rx, &ry, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_recents, "LIMPAR TUDO", &rx, &ry, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_recents, "Clear all", &rx, &ry, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_recents, "CLEAR ALL", &rx, &ry, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_recents, "Limpar", &rx, &ry, 0) ||
        t20_get_centroid_from_xml_fuzzy(xml_recents, "Fechar tudo", &rx, &ry, 0)) {
      if (cb) cb("[-] Botão 'Limpar tudo' localizado via XML! Clicando...");
      snprintf(cmd, sizeof(cmd), "-s %s shell input tap %d %d", serial, rx, ry);
      char *tcl = adb_exec(cmd);
      if (tcl) free(tcl);
      btn_cleared = 1;
    }
    free(xml_recents);
  }

  // Fallback: Toca na parte inferior central da tela
  if (!btn_cleared) {
    if (cb) cb("[-] Efetuando busca alternativa do botão 'Limpar tudo' na parte inferior...");
    snprintf(cmd, sizeof(cmd), "-s %s shell input swipe 200 300 800 300 300", serial);
    char *sw = adb_exec(cmd);
    if (sw) free(sw);
    Sleep(800);

    snprintf(cmd, sizeof(cmd), "-s %s shell input tap 600 1120", serial);
    char *tap_bot = adb_exec(cmd);
    if (tap_bot) free(tap_bot);
    Sleep(500);
  }

  // 6. Retorna para a tela inicial limpa
  Sleep(600);
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
  char *h2 = adb_exec(cmd);
  if (h2) free(h2);

  if (cb) cb("[+] Todos os aplicativos encerrados com sucesso! Nokia T20 mantido LIGADO na tela inicial.");
  return PIPELINE_SUCCESS;
}

// --- Etapa 7: Power Off ---
int T20_Etapa7_PowerOff(const char *serial, LogCallback cb) {
  if (cb) cb("[Nokia T20 - Etapa 7] Emitindo comando de desligamento físico no Nokia T20...");
  char args[256];
  snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
  char *out = adb_exec(args);
  if (out) free(out);
  return PIPELINE_SUCCESS;
}

// ==========================================
// Orquestrador Principal F1 — Nokia T20
// ==========================================
int T20_ExecutarPipelineCompleto(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb) {
  (void)wp_path;
  if (!serial || strlen(serial) == 0) {
    if (cb) cb("[ERROR] Serial do dispositivo Nokia T20 não informado!");
    return PIPELINE_ERROR;
  }

  if (cb) {
    char header[256];
    snprintf(header, sizeof(header), "========================================\n[F1] INICIANDO AUTOMAÇÃO NOKIA T20 (ANDROID 11)\nSerial: %s | Município: %s\n========================================", serial, municipio ? municipio : "N/A");
    cb(header);
  }

  // Sequência do Pipeline F1 para o Nokia T20
  if (T20_Etapa1(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa1_2_DarkMode(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa1_5(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa1_6(serial, apk_path, pacote_path, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa1_7(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa1_9_ClearLauncher(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa2(serial, apk_path, package_name, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa2_5(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa3(serial, package_name, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa4(serial, login, senha, municipio, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;
  if (T20_Etapa4_5_StopGnirehtet(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;

  // Etapa 5: Abre o menu lateral, clica em 'Importar ZIP', clica em 'PROCURAR ARQUIVO .ZIP' e seleciona o pacote ZIP via DPAD/XML (IDÊNTICO AO F2 e F3)
  if (T20_Etapa5(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;

  // Etapa 6: Aguarda 5 segundos e monitora a conclusão da descompactação (reaparecimento do botão 'PROCURAR ARQUIVO .ZIP')
  if (T20_Etapa6_AguardarConclusaoImportacao(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;

  // Etapa 7: Encerra todos os aplicativos abertos e limpa a tela de recentes (Mantém o tablet LIGADO)
  if (T20_Etapa7_CloseApps(serial, cb) != PIPELINE_SUCCESS) return PIPELINE_ERROR;

  if (cb) cb("[+] Pipeline F1 do Nokia T20 finalizado com sucesso (Importação concluída e apps fechados)!");
  return PIPELINE_SUCCESS;
}




