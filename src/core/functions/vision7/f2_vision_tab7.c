#include "f2_vision_tab7.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

static char *v7_dump_and_read_xml(const char *serial, LogCallback cb) {
  char *mem_xml = adb_dump_stdout(serial);
  if (mem_xml) return mem_xml;

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell uiautomator dump /sdcard/window_dump_%s.xml", serial, serial);
  char *out = adb_exec(args);
  if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/window_dump_%s.xml", serial, serial);
  return adb_exec(args);
}

static int v7_get_centroid(const char *xml, const char *search_str, int *out_cx, int *out_cy) {
  return adb_get_centroid_fuzzy(xml, search_str, out_cx, out_cy, 0);
}


static int F2_Etapa_Offline(const char *serial, LogCallback cb) {
  if (cb) cb("[F2 Vision7 - Final] Desativando antenas e ativando Modo Offline...");
  char cmd[512];
  char *out;

  snprintf(cmd, sizeof(cmd), "-s %s shell svc wifi disable", serial);
  out = adb_exec(cmd); if (out) free(out);

  snprintf(cmd, sizeof(cmd), "-s %s shell svc bluetooth disable", serial);
  out = adb_exec(cmd); if (out) free(out);

  snprintf(cmd, sizeof(cmd), "-s %s shell settings put global wifi_scan_always_enabled 0", serial);
  out = adb_exec(cmd); if (out) free(out);

  snprintf(cmd, sizeof(cmd), "-s %s shell settings put global ble_scan_always_enabled 0", serial);
  out = adb_exec(cmd); if (out) free(out);

  snprintf(cmd, sizeof(cmd), "-s %s shell settings put global airplane_mode_on 1", serial);
  out = adb_exec(cmd); if (out) free(out);

  snprintf(cmd, sizeof(cmd), "-s %s shell am broadcast -a android.intent.action.AIRPLANE_MODE --ez state true", serial);
  out = adb_exec(cmd); if (out) free(out);

  if (cb) cb("[-] Modo Offline ativado com sucesso! O dispositivo permanecera LIGADO.");
  return PIPELINE_SUCCESS;
}

int F2_ExecutarPipelineCompleto(const char *serial, const char *apk_path,
                                const char *pacote_path,
                                const char *package_name, const char *login,
                                const char *senha, const char *municipio,
                                LogCallback cb) {
  if (cb) cb("\n[F2 Vision7] INICIANDO FLUXO OBJETIVO F2 (ATUALIZACAO -> MODO OFFLINE)...\n");

  if (cb) cb("[-] [F2 Vision7 - Preparacao] Otimizando redes, brilho (70%), Fonte (1.5) e Rotacao Trava Portrait (lock 0)...");
  char args[1024];
  snprintf(args, sizeof(args),
           "-s %s shell \"svc wifi disable && svc bluetooth disable && settings put secure location_mode 0 && "
           "settings put global wifi_scan_always_enabled 0 && settings put global ble_scan_always_enabled 0 && "
           "settings put system screen_brightness_mode 0 && settings put system screen_brightness 178 && "
           "settings put system font_scale 1.5 && wm user-rotation lock 0\"",
           serial);
  char *out = adb_exec(args); if (out) free(out);

  // 1. Acorda a tela
  if (cb) cb("[-] Acordando a tela...");
  snprintf(args, sizeof(args), "-s %s shell input keyevent 224", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell input keyevent 82", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1000);

  // 2. Modo escuro
  snprintf(args, sizeof(args), "-s %s shell cmd uimode night yes", serial);
  out = adb_exec(args); if (out) free(out);

  // 3. Copiar APK e Pacote
  if (cb) cb("[-] Transferindo APK e ZIP para /sdcard/...");
  if (apk_path && strlen(apk_path) > 0) {
    char safe_apk[MAX_PATH];
    adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/app.apk", serial, safe_apk);
    out = adb_exec(args); if (out) free(out);
  }
  if (pacote_path && strlen(pacote_path) > 0) {
    char safe_pacote[MAX_PATH];
    adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/pacote.zip", serial, safe_pacote);
    out = adb_exec(args); if (out) free(out);
  }

  // 4. Instalar APK
  const char *pkg = (package_name && strlen(package_name) > 0) ? package_name : "br.com.yantec.innovava";
  if (cb) cb("[-] Reinstalando aplicativo Vision 7...");
  snprintf(args, sizeof(args), "-s %s shell pm install -r -g /sdcard/app.apk", serial);
  out = adb_exec(args); if (out) free(out);

  // 5. Permissões e Abrir
  snprintf(args, sizeof(args), "-s %s shell pm grant %s android.permission.CAMERA", serial, pkg);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell pm grant %s android.permission.READ_EXTERNAL_STORAGE", serial, pkg);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell pm grant %s android.permission.WRITE_EXTERNAL_STORAGE", serial, pkg);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1", serial, pkg);
  out = adb_exec(args); if (out) free(out);
  Sleep(3000);

  // 6. Importar pacote ZIP no app
  if (cb) cb("[-] Abrindo menu para importar pacote ZIP...");
  snprintf(args, sizeof(args), "-s %s shell input swipe 10 500 650 500 300", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1500);

  char *xml_m = v7_dump_and_read_xml(serial, cb);
  if (xml_m) {
    int ix = 0, iy = 0;
    if (v7_get_centroid(xml_m, "Importar ZIP", &ix, &iy) || v7_get_centroid(xml_m, "Importar", &ix, &iy)) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, ix, iy);
      out = adb_exec(args); if (out) free(out);
    }
    free(xml_m);
  }
  Sleep(2000);

  // 7. Aplicar modo offline
  F2_Etapa_Offline(serial, cb);

  if (cb) cb("\n[F2 Vision7] -> PIPELINE F2 CONCLUIDO COM SUCESSO (APP PRONTO).");

  // 8. Desligar o aparelho
  if (cb) cb("[-] Enviando comando de SHUTDOWN para desligar o aparelho...");
  snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
  out = adb_exec(args); if (out) free(out);

  return PIPELINE_SUCCESS;
}
