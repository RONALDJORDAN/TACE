#include "m7_helpers.h"
#include "../../drivers/adb.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

int M7_UnlockScreen(const char *serial, LogCallback cb) {
  if (cb) cb("[M7] Acordando e desbloqueando a tela...");
  char args[256];

  snprintf(args, sizeof(args),
           "-s %s shell \"dumpsys power | grep mWakefulness=\"", serial);
  char *wake_status = adb_exec(args);
  if (wake_status) {
    if (strstr(wake_status, "mWakefulness=Awake") == NULL) {
      snprintf(args, sizeof(args), "-s %s shell input keyevent 26", serial);
      char *out = adb_exec(args);
      if (out) free(out);
      Sleep(800);
    }
    free(wake_status);
  }

  snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300", serial);
  char *out = adb_exec(args);
  if (out) free(out);

  // Forçar orientação vertical (Portrait), desativar rotação automática e timeout de tela 5 min
  if (cb) cb("[-] Trava de tela na vertical (Portrait) e desativacao de rotacao automatica...");
  snprintf(args, sizeof(args),
           "-s %s shell \"settings put system accelerometer_rotation 0 && "
           "settings put system user_rotation 0 && wm user-rotation lock 0 && "
           "settings put system screen_off_timeout 300000\"", serial);
  char *out2 = adb_exec(args);
  if (out2) free(out2);

  if (cb) cb("[-] Tela desbloqueada.");
  return PIPELINE_SUCCESS;
}


int M7_SetDarkMode(const char *serial, LogCallback cb) {
  if (cb) cb("[M7] Ativando Modo Escuro...");
  char cmd[256];

  snprintf(cmd, sizeof(cmd), "-s %s shell cmd uimode night yes", serial);
  char *dm = adb_exec(cmd);
  if (dm) free(dm);

  snprintf(cmd, sizeof(cmd), "-s %s shell settings put secure ui_night_mode 2", serial);
  char *dm2 = adb_exec(cmd);
  if (dm2) free(dm2);

  return PIPELINE_SUCCESS;
}

int M7_ConfigureApp(const char *serial, const char *package_name, LogCallback cb) {
  if (cb) cb("[M7] Liberando permissoes e abrindo aplicativo...");
  if (!package_name || strlen(package_name) == 0) return PIPELINE_ERROR;

  char args[512];
  const char *permissoes[] = {
    "android.permission.CAMERA",
    "android.permission.READ_EXTERNAL_STORAGE",
    "android.permission.WRITE_EXTERNAL_STORAGE",
    "android.permission.ACCESS_FINE_LOCATION",
    "android.permission.ACCESS_COARSE_LOCATION"
  };

  for (int i = 0; i < 5; i++) {
    snprintf(args, sizeof(args), "-s %s shell pm grant %s %s",
             serial, package_name, permissoes[i]);
    char *out = adb_exec(args);
    if (out) free(out);
  }

  snprintf(args, sizeof(args),
           "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1",
           serial, package_name);
  char *out2 = adb_exec(args);
  if (out2) free(out2);

  if (cb) cb("[-] Aplicativo executado e em primeiro plano.");
  return PIPELINE_SUCCESS;
}

int M7_StartGnirehtet(const char *serial, LogCallback cb) {
  if (cb) cb("[M7] Inicializando Tunel Gnirehtet (Tab-Multi)...");

  char exe_path[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path, MAX_PATH);
  char *last_slash = strrchr(exe_path, '\\');
  if (last_slash) *last_slash = '\0';

  char gnirehtet_path[MAX_PATH];
  char gnirehtet_apk[MAX_PATH];
  char gnirehtet_dir[MAX_PATH];

  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering", exe_path);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
             "%s\\..\\..\\tools\\reverse_tethering", exe_path);
  }

  char full_gnirehtet_dir[MAX_PATH];
  GetFullPathNameA(gnirehtet_dir, MAX_PATH, full_gnirehtet_dir, NULL);
  strncpy(gnirehtet_dir, full_gnirehtet_dir, sizeof(gnirehtet_dir) - 1);

  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe", gnirehtet_dir);
  snprintf(gnirehtet_apk, sizeof(gnirehtet_apk), "%s\\gnirehtet.apk", gnirehtet_dir);

  char adb_path[MAX_PATH];
  adb_get_path(adb_path, sizeof(adb_path));
  SetEnvironmentVariableA("ADB", adb_path);

  char args_check[1024];
  snprintf(args_check, sizeof(args_check),
           "-s %s shell pm list packages com.genymobile.gnirehtet", serial);
  char *out_check = adb_exec(args_check);
  int is_installed = 0;
  if (out_check) {
    if (strstr(out_check, "package:com.genymobile.gnirehtet")) is_installed = 1;
    free(out_check);
  }

  if (is_installed) {
    if (cb) cb("  -> Cliente Gnirehtet já está instalado no M7. Pulando reinstalação do APK.");
  } else {
    if (cb) cb("  -> Instalando cliente Gnirehtet no M7...");
    char args_apk[1024];
    snprintf(args_apk, sizeof(args_apk), "-s %s install -r -g \"%s\"", serial, gnirehtet_apk);
#undef adb_exec
    char *out_apk = adb_exec(args_apk);
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)
    if (out_apk) free(out_apk);
  }


  char args_op[1024];
  snprintf(args_op, sizeof(args_op),
           "-s %s shell appops set com.genymobile.gnirehtet ACTIVATE_VPN allow", serial);
  char *out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  snprintf(args_op, sizeof(args_op),
           "-s %s shell appops set com.genymobile.gnirehtet RUN_IN_BACKGROUND allow", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  snprintf(args_op, sizeof(args_op),
           "-s %s shell dumpsys deviceidle whitelist +com.genymobile.gnirehtet", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  snprintf(args_op, sizeof(args_op), "-s %s shell settings put global awbms_enabled 0", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);
  snprintf(args_op, sizeof(args_op), "-s %s shell settings put global awbms_disable 1", serial);
  out_op = adb_exec(args_op);
  if (out_op) free(out_op);

  STARTUPINFOA si_kill; PROCESS_INFORMATION pi_kill;
  ZeroMemory(&si_kill, sizeof(si_kill)); si_kill.cb = sizeof(si_kill);
  ZeroMemory(&pi_kill, sizeof(pi_kill));
  char cmd_kill[] = "taskkill /F /IM gnirehtet.exe /T";
  if (CreateProcessA(NULL, cmd_kill, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                     NULL, NULL, &si_kill, &pi_kill)) {
    WaitForSingleObject(pi_kill.hProcess, 3000);
    CloseHandle(pi_kill.hProcess); CloseHandle(pi_kill.hThread);
  }
  Sleep(1000);

  char relay_cmd[1024];
  snprintf(relay_cmd, sizeof(relay_cmd), "\"%s\" relay", gnirehtet_path);
  STARTUPINFOA si_relay; PROCESS_INFORMATION pi_relay;
  ZeroMemory(&si_relay, sizeof(si_relay)); si_relay.cb = sizeof(si_relay);
  ZeroMemory(&pi_relay, sizeof(pi_relay));
  if (CreateProcessA(NULL, relay_cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                     NULL, gnirehtet_dir, &si_relay, &pi_relay)) {
    CloseHandle(pi_relay.hProcess); CloseHandle(pi_relay.hThread);
  }
  Sleep(3000);

  char cmdline[1024];
  snprintf(cmdline, sizeof(cmdline), "\"%s\" start %s", gnirehtet_path, serial);
  STARTUPINFOA si; PROCESS_INFORMATION pi;
  ZeroMemory(&si, sizeof(si)); si.cb = sizeof(si);
  ZeroMemory(&pi, sizeof(pi));
  if (CreateProcessA(NULL, cmdline, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                     NULL, gnirehtet_dir, &si, &pi)) {
    WaitForSingleObject(pi.hProcess, 10000);
    CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
    if (cb) cb("[-] Tunel Gnirehtet M7 ATIVO.");
  }
  Sleep(2000);

  if (cb) cb("[M7] Conectando Wi-Fi Starlink06...");
  char cmd_wifi[256];
  snprintf(cmd_wifi, sizeof(cmd_wifi), "-s %s shell svc wifi enable", serial);
  char *out_w = adb_exec(cmd_wifi);
  if (out_w) free(out_w);
  snprintf(cmd_wifi, sizeof(cmd_wifi), "-s %s shell cmd wifi connect-network Starlink06 open", serial);
  out_w = adb_exec(cmd_wifi);
  if (out_w) free(out_w);

  if (cb) cb("[M7] Removendo limites de background do InnovAva...");
  char cmd_bg[256];
  snprintf(cmd_bg, sizeof(cmd_bg),
           "-s %s shell dumpsys deviceidle whitelist +br.com.yantec.innovava", serial);
  char *out_bg = adb_exec(cmd_bg);
  if (out_bg) free(out_bg);
  snprintf(cmd_bg, sizeof(cmd_bg),
           "-s %s shell appops set br.com.yantec.innovava RUN_IN_BACKGROUND allow", serial);
  out_bg = adb_exec(cmd_bg);
  if (out_bg) free(out_bg);
  snprintf(cmd_bg, sizeof(cmd_bg),
           "-s %s shell am set-standby-bucket br.com.yantec.innovava active", serial);
  out_bg = adb_exec(cmd_bg);
  if (out_bg) free(out_bg);

  return PIPELINE_SUCCESS;
}

int M7_StopGnirehtet(const char *serial, LogCallback cb) {
  if (cb) cb("[M7] Desligando tunel Gnirehtet...");
  Sleep(5000);

  char exe_path[MAX_PATH];
  GetModuleFileNameA(NULL, exe_path, MAX_PATH);
  char *last_slash = strrchr(exe_path, '\\');
  if (last_slash) *last_slash = '\0';

  char gnirehtet_path[MAX_PATH];
  char gnirehtet_dir[MAX_PATH];
  snprintf(gnirehtet_dir, sizeof(gnirehtet_dir), "%s\\tools\\reverse_tethering", exe_path);
  if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
             "%s\\..\\..\\tools\\reverse_tethering", exe_path);
  }
  char full_dir[MAX_PATH];
  GetFullPathNameA(gnirehtet_dir, MAX_PATH, full_dir, NULL);
  snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe", full_dir);

  char cmd_stop[1024];
  snprintf(cmd_stop, sizeof(cmd_stop), "\"%s\" stop %s", gnirehtet_path, serial);

  STARTUPINFOA si_stop; PROCESS_INFORMATION pi_stop;
  ZeroMemory(&si_stop, sizeof(si_stop)); si_stop.cb = sizeof(si_stop);
  ZeroMemory(&pi_stop, sizeof(pi_stop));
  if (CreateProcessA(NULL, cmd_stop, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                     NULL, NULL, &si_stop, &pi_stop)) {
    if (cb) cb("[-] Tunel M7 Gnirehtet DESLIGADO.");
    CloseHandle(pi_stop.hProcess); CloseHandle(pi_stop.hThread);
  }
  return PIPELINE_SUCCESS;
}
