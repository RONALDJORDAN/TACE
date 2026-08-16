#include "f3_vision_tab7.h"
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


int F3_ExecutarAtualizacaoPacote(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb) {
    if (cb) cb("\n[F3 Vision7] INICIANDO ATUALIZACAO ULTRA-RAPIDA DE PACOTE AVALIATIVO (MODO EXPRESS)...\n");

    char args[1024];
    char *out = NULL;

    // 0. Preparação
    snprintf(args, sizeof(args),
             "-s %s shell \"settings put system accelerometer_rotation 0 && settings put system user_rotation 0 && wm user-rotation lock 0 && "
             "svc wifi disable && svc bluetooth disable && settings put secure location_mode 0 && "
             "settings put global wifi_scan_always_enabled 0 && settings put global ble_scan_always_enabled 0 && "
             "settings put system screen_brightness_mode 0 && settings put system screen_brightness 178 && "
             "settings put system font_scale 1.5\"",
             serial);
    out = adb_exec(args); if (out) free(out);

    // 1. Acorda tela
    snprintf(args, sizeof(args), "-s %s shell input keyevent 224", serial);
    out = adb_exec(args); if (out) free(out);
    snprintf(args, sizeof(args), "-s %s shell input keyevent 82", serial);
    out = adb_exec(args); if (out) free(out);
    snprintf(args, sizeof(args), "-s %s shell input swipe 300 1000 300 300 300", serial);
    out = adb_exec(args); if (out) free(out);
    Sleep(800);

    // 2. Limpeza do Armazenamento (Apagar pastas internas exceto Android e Pictures)
    if (cb) cb("[F3 Vision7] Formatando /sdcard (preservando Android e Pictures) e criando diretorios...");
    snprintf(args, sizeof(args),
             "-s %s shell \"cd /sdcard && ls | grep -vwE 'Android|Pictures' | xargs rm -rf\"",
             serial);
    out = adb_exec(args); if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell mkdir -p /sdcard/Pictures/Screenshots /sdcard/avaliacoes", serial);
    out = adb_exec(args); if (out) free(out);

    // 2.1 Transferir APK (se fornecido)
    if (apk_path && strlen(apk_path) > 0) {
        char safe_apk[MAX_PATH];
        adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
        if (cb) cb("  -> Transmitindo APK para /sdcard/avaliacoes/ ...");
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/avaliacoes/", serial, safe_apk);
        out = adb_exec(args); if (out) free(out);
    }

    // 2.2 Transferir Pacote ZIP
    if (pacote_path && strlen(pacote_path) > 0) {
        char safe_pacote[MAX_PATH];
        adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
        if (cb) cb("  -> Transmitindo PACOTE ZIP para /sdcard/avaliacoes/ e /sdcard/pacote.zip ...");
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/avaliacoes/", serial, safe_pacote);
        out = adb_exec(args); if (out) free(out);
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/pacote.zip", serial, safe_pacote);
        out = adb_exec(args); if (out) free(out);
    }

    // 3. Reiniciar app
    const char *pkg = (package_name && strlen(package_name) > 0) ? package_name : "br.com.yantec.innovava";
    snprintf(args, sizeof(args), "-s %s shell am force-stop %s", serial, pkg);
    out = adb_exec(args); if (out) free(out);
    Sleep(300);

    snprintf(args, sizeof(args), "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1", serial, pkg);
    out = adb_exec(args); if (out) free(out);
    Sleep(2500);

    // 4. Abrir menu lateral e importar
    snprintf(args, sizeof(args), "-s %s shell input swipe 10 500 650 500 300", serial);
    out = adb_exec(args); if (out) free(out);
    Sleep(1500);

    char *xml = v7_dump_and_read_xml(serial, cb);
    if (xml) {
        int ix = 0, iy = 0;
        if (v7_get_centroid(xml, "Importar ZIP", &ix, &iy) || v7_get_centroid(xml, "Importar", &ix, &iy)) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, ix, iy);
            out = adb_exec(args); if (out) free(out);
        }
        free(xml);
    }
    Sleep(2000);

    if (cb) cb("[F3 Vision7] Atualizacao de pacote concluida com sucesso.");

    return PIPELINE_SUCCESS;
}
