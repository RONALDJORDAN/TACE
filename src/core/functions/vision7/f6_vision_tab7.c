#include "f6_vision_tab7.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_db.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

#define MAX_POLLING_ITERATIONS 30
#define POLLING_INTERVAL_MS 2000

static char *v7_dump_and_read_xml(const char *serial, LogCallback cb) {
  char args[512];
  snprintf(args, sizeof(args), "-s %s shell uiautomator dump /sdcard/window_dump.xml 2>/dev/null", serial);
  char *out = adb_exec(args);
  if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/window_dump.xml", serial);
  return adb_exec(args);
}

static int v7_get_centroid(const char *xml, const char *search_str, int *out_cx, int *out_cy) {
  return adb_get_centroid_fuzzy(xml, search_str, out_cx, out_cy, 0);
}

// Extrai a contagem de Avaliações Pendentes na tela SINCRONIZAR
static int v7_parse_pending_count(const char *xml) {
  if (!xml) return -1;

  const char *p = strstr(xml, "Avaliações Pendentes");
  if (!p) p = strstr(xml, "Avaliacoes Pendentes");
  if (!p) p = strstr(xml, "Avaliações pendentes");
  if (!p) p = strstr(xml, "Avaliacoes pendentes");
  if (!p) p = strstr(xml, "Pendentes");
  if (!p) p = strstr(xml, "pendentes");

  if (!p) return -1;

  // Primeiro tenta procurar por número no mesmo nó de texto
  const char *curr = p;
  while (*curr && *curr != '"' && (curr - p) < 120) {
    if (*curr >= '0' && *curr <= '9') {
      return atoi(curr);
    }
    curr++;
  }

  // Tenta localizar nos nós filhos/vizinhos logo abaixo
  curr = strchr(p, '>');
  if (!curr) curr = p;

  int chars_read = 0;
  while (*curr && chars_read < 800) {
    const char *text_attr = strstr(curr, "text=\"");
    if (text_attr && (text_attr - curr) < 300) {
      const char *val_start = text_attr + 6;
      while (*val_start == ' ' || *val_start == '\n' || *val_start == '\r') val_start++;
      if (*val_start >= '0' && *val_start <= '9') {
        return atoi(val_start);
      }
      curr = text_attr + 6;
    } else {
      break;
    }
    chars_read += 50;
  }

  return -1;
}

// Controle do Túnel Gnirehtet (Reverse Tethering USB)
static int v7_stop_gnirehtet(const char *serial, LogCallback cb) {
  char exe_path[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path, MAX_PATH);
  char *last_slash = strrchr(exe_path, '\\');
  if (last_slash) *last_slash = '\0';

  char gnirehtet_path[MAX_PATH];
  snprintf(gnirehtet_path, sizeof(gnirehtet_path),
           "%s\\tools\\reverse_tethering\\gnirehtet.exe", exe_path);
  if (GetFileAttributesA(gnirehtet_path) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_path, sizeof(gnirehtet_path),
             "%s\\..\\..\\tools\\reverse_tethering\\gnirehtet.exe", exe_path);
  }

  char cmd_stop[1024];
  snprintf(cmd_stop, sizeof(cmd_stop), "\"%s\" stop %s", gnirehtet_path, serial);

  STARTUPINFOA si_stop;
  PROCESS_INFORMATION pi_stop;
  ZeroMemory(&si_stop, sizeof(si_stop));
  si_stop.cb = sizeof(si_stop);
  ZeroMemory(&pi_stop, sizeof(pi_stop));
  if (CreateProcessA(NULL, cmd_stop, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si_stop, &pi_stop)) {
    WaitForSingleObject(pi_stop.hProcess, 3000);
    CloseHandle(pi_stop.hProcess);
    CloseHandle(pi_stop.hThread);
    if (cb) cb("[-] Tunel Gnirehtet DESLIGADO com sucesso.");
  }
  return 1;
}

static int v7_start_gnirehtet(const char *serial, LogCallback cb) {
  char exe_path[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path, MAX_PATH);
  char *last_slash = strrchr(exe_path, '\\');
  if (last_slash) *last_slash = '\0';

  char gnirehtet_dir[MAX_PATH];
  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering", exe_path);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\..\\..\\tools\\reverse_tethering", exe_path);
  }

  char full_gnirehtet_dir[MAX_PATH];
  GetFullPathNameA(gnirehtet_dir, MAX_PATH, full_gnirehtet_dir, NULL);
  strncpy(gnirehtet_dir, full_gnirehtet_dir, sizeof(gnirehtet_dir) - 1);

  char gnirehtet_path[MAX_PATH];
  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe", gnirehtet_dir);

  char adb_path[MAX_PATH];
  adb_get_path(adb_path, sizeof(adb_path));
  SetEnvironmentVariableA("ADB", adb_path);

  if (cb) cb("  -> Autorizando permissões de VPN Gnirehtet via AppOps...");
  char cmd_op[512];
  snprintf(cmd_op, sizeof(cmd_op), "-s %s shell appops set com.genymobile.gnirehtet ACTIVATE_VPN allow", serial);
  char *out = adb_exec_retry(cmd_op, 2, 1000, (AdbLogCallback)cb); if (out) free(out);

  snprintf(cmd_op, sizeof(cmd_op), "-s %s shell appops set com.genymobile.gnirehtet RUN_IN_BACKGROUND allow", serial);
  out = adb_exec_retry(cmd_op, 2, 1000, (AdbLogCallback)cb); if (out) free(out);

  snprintf(cmd_op, sizeof(cmd_op), "-s %s shell dumpsys deviceidle whitelist +com.genymobile.gnirehtet", serial);
  out = adb_exec_retry(cmd_op, 2, 1000, (AdbLogCallback)cb); if (out) free(out);

  // Mata processos Gnirehtet anteriores no Windows para evitar conflito de porta
  STARTUPINFOA si_kill;
  PROCESS_INFORMATION pi_kill;
  ZeroMemory(&si_kill, sizeof(si_kill));
  si_kill.cb = sizeof(si_kill);
  ZeroMemory(&pi_kill, sizeof(pi_kill));
  char cmd_kill[] = "taskkill /F /IM gnirehtet.exe /T";
  if (CreateProcessA(NULL, cmd_kill, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si_kill, &pi_kill)) {
    WaitForSingleObject(pi_kill.hProcess, 2000);
    CloseHandle(pi_kill.hProcess);
    CloseHandle(pi_kill.hThread);
  }
  Sleep(500);

  // Inicia o Servidor Relay
  char relay_cmd[1024];
  snprintf(relay_cmd, sizeof(relay_cmd), "\"%s\" relay", gnirehtet_path);
  STARTUPINFOA si_relay;
  PROCESS_INFORMATION pi_relay;
  ZeroMemory(&si_relay, sizeof(si_relay));
  si_relay.cb = sizeof(si_relay);
  ZeroMemory(&pi_relay, sizeof(pi_relay));
  if (cb) cb("  -> Iniciando servidor Relay do Gnirehtet...");
  if (CreateProcessA(NULL, relay_cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, gnirehtet_dir, &si_relay, &pi_relay)) {
    CloseHandle(pi_relay.hProcess);
    CloseHandle(pi_relay.hThread);
  }
  Sleep(2000);

  // Inicia a VPN no dispositivo
  char cmd_start[1024];
  snprintf(cmd_start, sizeof(cmd_start), "\"%s\" start %s", gnirehtet_path, serial);

  STARTUPINFOA si_start;
  PROCESS_INFORMATION pi_start;
  ZeroMemory(&si_start, sizeof(si_start));
  si_start.cb = sizeof(si_start);
  ZeroMemory(&pi_start, sizeof(pi_start));
  if (CreateProcessA(NULL, cmd_start, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, gnirehtet_dir, &si_start, &pi_start)) {
    WaitForSingleObject(pi_start.hProcess, 5000);
    CloseHandle(pi_start.hProcess);
    CloseHandle(pi_start.hThread);
    if (cb) cb("[+] Tunel Gnirehtet e conexão automatica de internet ATIVADOS com sucesso.");
  } else {
    if (cb) cb("[WARNING] Nao foi possivel iniciar o executavel do Gnirehtet.");
  }
  return 1;
}

int F6_ExecutarSincronizacao(const char *serial, LogCallback cb) {
  if (cb) cb("[F6 Vision7] 🔄 INICIANDO ROTINA DE SINCRONIZACAO DIRETA AVANCADA (VISION TAB)...");

  char args[512];
  char *out;

  // -------------------------------------------------------------
  // Etapa 1: Smart Wakeup, Unlock & Trava de Orientação Portrait
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 1] Acordando o dispositivo, desbloqueando a tela e travando a orientacao...");
  snprintf(args, sizeof(args), "-s %s shell \"dumpsys power | grep mWakefulness=\"", serial);
  char *wake_status = adb_exec(args);

  if (wake_status) {
    if (strstr(wake_status, "mWakefulness=Awake") == NULL) {
      if (cb) cb("[-] Tela desligada detectada. Pressionando botao Power virtual...");
      snprintf(args, sizeof(args), "-s %s shell input keyevent 26", serial);
      out = adb_exec(args); if (out) free(out);
      Sleep(800);
    }
    free(wake_status);
  }

  snprintf(args, sizeof(args), "-s %s shell input keyevent 82", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(300);

  snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(800);

  // Trava orientação Portrait (0)
  snprintf(args, sizeof(args), "-s %s shell wm user-rotation lock 0", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(300);

  // -------------------------------------------------------------
  // Etapa 1.5: Conexão Automática de Internet Imediata (Wi-Fi + Gnirehtet USB)
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 1.5] Ativando conexao automatica de internet (Wi-Fi + Reverse Tethering Gnirehtet)...");
  snprintf(args, sizeof(args), "-s %s shell svc wifi enable", serial);
  out = adb_exec(args); if (out) free(out);
  v7_start_gnirehtet(serial, cb);
  Sleep(2000);

  // -------------------------------------------------------------
  // Etapa 1.8: Limpeza Prévia de Apps em Aberto (Garante estado limpo)
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 1.8] Executando limpeza previa de aplicativos abertos...");
  snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(300);

  snprintf(args, sizeof(args),
           "-s %s shell \"am force-stop br.com.yantec.innovava && "
           "am force-stop com.google.android.documentsui && "
           "am force-stop com.android.documentsui && "
           "am force-stop com.android.chrome && am kill-all\"", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(500);

  // -------------------------------------------------------------
  // Etapa 2: Foco e Abertura do Aplicativo Principal
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 2] Iniciando o aplicativo da avaliacao (InnovAva)...");
  snprintf(args, sizeof(args),
           "-s %s shell \"am start -n br.com.yantec.innovava/.MainActivity 2>/dev/null || "
           "monkey -p br.com.yantec.innovava 1\"", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(2500);

  // -------------------------------------------------------------
  // Etapa 3: Navegação até a Tela com Título 'SINCRONIZAR'
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 3] Navegando para o menu 'Sincronizar Nuvem'...");
  int menu_cx = 0, menu_cy = 0;
  int menu_found = 0;

  char *xml_home = v7_dump_and_read_xml(serial, cb);
  if (xml_home) {
    if (v7_get_centroid(xml_home, "Sincronizar Nuvem", &menu_cx, &menu_cy) ||
        v7_get_centroid(xml_home, "Sincronizacao Nuvem", &menu_cx, &menu_cy) ||
        v7_get_centroid(xml_home, "Sincronização", &menu_cx, &menu_cy) ||
        v7_get_centroid(xml_home, "Sincronizar", &menu_cx, &menu_cy)) {
      menu_found = 1;
    }
  }

  // Se o item de menu não estiver visível na home, abre a gaveta de menu lateral
  if (!menu_found) {
    if (cb) cb("[-] Item de menu nao visivel na tela principal. Deslizando gaveta lateral...");
    snprintf(args, sizeof(args), "-s %s shell input swipe 10 500 650 500 250", serial);
    out = adb_exec(args); if (out) free(out);
    Sleep(1000);

    if (xml_home) free(xml_home);
    xml_home = v7_dump_and_read_xml(serial, cb);
    if (xml_home) {
      if (v7_get_centroid(xml_home, "Sincronizar Nuvem", &menu_cx, &menu_cy) ||
          v7_get_centroid(xml_home, "Sincronizacao Nuvem", &menu_cx, &menu_cy) ||
          v7_get_centroid(xml_home, "Sincronização", &menu_cx, &menu_cy) ||
          v7_get_centroid(xml_home, "Sincronizar", &menu_cx, &menu_cy)) {
        menu_found = 1;
      }
    }
  }

  if (menu_found) {
    if (cb) cb("[-] Clicando no item de menu 'Sincronizar Nuvem'...");
    snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, menu_cx, menu_cy);
    out = adb_exec(args); if (out) free(out);
    Sleep(2000);
  } else {
    if (cb) cb("[WARNING] Opção de menu 'Sincronizar Nuvem' não identificada. Inspecionando a tela atual...");
  }
  if (xml_home) free(xml_home);

  // -------------------------------------------------------------
  // Etapa 4: Leitura de Avaliações Pendentes na Tela 'SINCRONIZAR'
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 4] Lendo a quantidade abaixo de 'Avaliações Pendentes' na tela SINCRONIZAR...");
  char *xml_sync_screen = v7_dump_and_read_xml(serial, cb);

  int btn_big_cx = 0, btn_big_cy = 0;
  int btn_big_found = 0;

  if (xml_sync_screen) {
    if (v7_get_centroid(xml_sync_screen, "SINCRONIZAR AGORA", &btn_big_cx, &btn_big_cy) ||
        v7_get_centroid(xml_sync_screen, "Sincronizar Agora", &btn_big_cx, &btn_big_cy)) {
      btn_big_found = 1;
    }
  }

  int pending_count = v7_parse_pending_count(xml_sync_screen);
  if (xml_sync_screen) free(xml_sync_screen);

  char log_pending[256];
  if (pending_count >= 0) {
    snprintf(log_pending, sizeof(log_pending), "[F6 Vision7] 📊 Número lido abaixo de Avaliações Pendentes: %d", pending_count);
    if (cb) cb(log_pending);
  } else {
    if (cb) cb("[F6 Vision7] 📊 Rótulo de pendências não lido no centro da tela. Assumindo padrão seguro (>= 1).");
  }

  // -------------------------------------------------------------
  // Etapa 5: Avaliação Condicional (Pendentes 0 vs Pendentes >= 1)
  // -------------------------------------------------------------
  if (pending_count == 0) {
    if (cb) cb("[F6 Vision7] ℹ️ Número de Avaliações Pendentes e igual a 0.");
    if (cb) cb("[-] O sistema NAO precisa sincronizar. NAO clicando no botao grande 'SINCRONIZAR AGORA'.");
    if (cb) cb("[-] Desativando a rede/tunel e avancando para o encerramento seguro...");
    v7_stop_gnirehtet(serial, cb);
  } else {
    if (cb) cb("[F6 Vision7] 🔄 Número de Avaliações Pendentes e maior ou igual a 1. Sincronização necessária!");

    // Clica no botão grande 'SINCRONIZAR AGORA'
    if (btn_big_found) {
      if (cb) cb("[-] Clicando no botão grande 'SINCRONIZAR AGORA' na tela...");
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, btn_big_cx, btn_big_cy);
      out = adb_exec(args); if (out) free(out);
      Sleep(2000);
    } else {
      if (cb) cb("[WARNING] Botão grande 'SINCRONIZAR AGORA' não localizado via centroide. Tentando toque central de fallback...");
      snprintf(args, sizeof(args), "-s %s shell input tap 300 650", serial);
      out = adb_exec(args); if (out) free(out);
      Sleep(2000);
    }

    // Polling e Monitoramento da Mensagem de Confirmação
    if (cb) cb("[F6 - Etapa 5c] Aguardando confirmação de sincronização concluída na tela...");
    int confirmed = 0;
    for (int iter = 0; iter < MAX_POLLING_ITERATIONS; iter++) {
      char *xml_poll = v7_dump_and_read_xml(serial, cb);
      if (xml_poll) {
        if (strstr(xml_poll, "sucesso") || strstr(xml_poll, "SUCESSO") ||
            strstr(xml_poll, "Sincronizado") || strstr(xml_poll, "SINCRONIZADO") ||
            strstr(xml_poll, "Concluído") || strstr(xml_poll, "Concluido") ||
            strstr(xml_poll, "CONCLUIDO") || strstr(xml_poll, "Enviado") ||
            strstr(xml_poll, "ENVIADO") || strstr(xml_poll, "Dados enviados")) {
          confirmed = 1;
          free(xml_poll);
          break;
        }
        free(xml_poll);
      }
      Sleep(POLLING_INTERVAL_MS);
    }

    if (confirmed) {
      if (cb) cb("[F6 Vision7] ✔️ Confirmação de sincronização detectada com sucesso!");
    } else {
      if (cb) cb("[WARNING] Tempo limite de aguarde de sincronização atingido. Prosseguindo...");
    }

    // Desativa o Túnel Gnirehtet
    v7_stop_gnirehtet(serial, cb);
  }

  // -------------------------------------------------------------
  // Etapa 6: Desativação Total de Rádios e Conectividades
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 6] Desativando Wi-Fi, Bluetooth, GPS e varreduras de rede...");
  snprintf(args, sizeof(args),
           "-s %s shell \"svc wifi disable && svc bluetooth disable && "
           "settings put secure location_mode 0 && "
           "settings put global wifi_scan_always_enabled 0 && "
           "settings put global ble_scan_always_enabled 0\"", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(500);

  // -------------------------------------------------------------
  // Etapa 7: Limpeza Manual Visual de Aplicativos Recentes & Kill
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 7] [LIMPEZA MANUAL VISUAL] Fechando todos os aplicativos abertos...");

  snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(400);

  snprintf(args, sizeof(args), "-s %s shell input keyevent 187", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1200);

  snprintf(args, sizeof(args), "-s %s shell input swipe 150 500 850 500 250", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(500);

  int recents_cleared = 0;
  snprintf(args, sizeof(args), "-s %s shell uiautomator dump /sdcard/dump_recents_f6.xml 2>/dev/null", serial);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_recents_f6.xml", serial);
  char *xml_recents = adb_exec(args);
  if (xml_recents) {
    char *p = strstr(xml_recents, "Limpar tudo");
    if (!p) p = strstr(xml_recents, "LIMPAR TUDO");
    if (!p) p = strstr(xml_recents, "Clear all");
    if (!p) p = strstr(xml_recents, "CLEAR ALL");
    if (!p) p = strstr(xml_recents, "Fechar tudo");
    if (!p) p = strstr(xml_recents, "FECHAR TUDO");
    if (p) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, (x1 + x2) / 2, (y1 + y2) / 2);
          char *tc = adb_exec(args); if (tc) free(tc);
          recents_cleared = 1;
        }
      }
    }
    free(xml_recents);
  }

  if (!recents_cleared) {
    snprintf(args, sizeof(args), "-s %s shell input tap 300 1100", serial);
    out = adb_exec(args); if (out) free(out);
  }
  Sleep(800);

  snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(400);

  snprintf(args, sizeof(args),
           "-s %s shell \"am force-stop br.com.yantec.innovava && "
           "am force-stop com.google.android.documentsui && "
           "am force-stop com.android.documentsui && "
           "am force-stop com.android.chrome && am kill-all\"", serial);
  out = adb_exec(args); if (out) free(out);

  // -------------------------------------------------------------
  // Etapa 8: Desligamento Físico do Aparelho (Shutdown)
  // -------------------------------------------------------------
  if (cb) cb("[F6 - Etapa 8] Executando Shutdown (reboot -p)... O aparelho será desligado fisicamente.");
  snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
  out = adb_exec(args); if (out) free(out);

  if (cb) cb("[F6 Vision7] ✔️ PROCESSO F6 CONCLUIDO COM SUCESSO!");
  return PIPELINE_SUCCESS;
}


