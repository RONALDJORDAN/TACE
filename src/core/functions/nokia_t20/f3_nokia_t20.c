#include "f3_nokia_t20.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// Helper local de leitura de XML
static char *t20_dump_and_read_xml(const char *serial, LogCallback cb) {
  char *mem_xml = adb_dump_stdout(serial);
  if (mem_xml) return mem_xml;

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell uiautomator dump /sdcard/window_dump_%s.xml", serial, serial);
  char *out = adb_exec(args);
  if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/window_dump_%s.xml", serial, serial);
  return adb_exec(args);
}

// Helper local de centroides universal com suporte a acentos
static int t20_get_centroid(const char *xml, const char *search_str, int *out_cx, int *out_cy) {
  return adb_get_centroid_fuzzy(xml, search_str, out_cx, out_cy, 0);
}


int T20_ExecutarPipelineF3_AtualizarPacote(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb) {
    if (!serial || strlen(serial) == 0) {
        if (cb) cb("[ERROR] Serial do dispositivo Nokia T20 não informado!");
        return PIPELINE_ERROR;
    }

    if (cb) {
        char header[256];
        snprintf(header, sizeof(header), "========================================\n[F3 NOKIA T20] ATUALIZAÇÃO EXPRESSA DE PACOTE (ZIP)\nSerial: %s\n========================================", serial);
        cb(header);
    }

    char args[1024];
    char *out = NULL;

    // 1. Acorda tela
    if (cb) cb("[F3 Nokia T20] Acordando o dispositivo...");
    snprintf(args, sizeof(args), "-s %s shell input keyevent 224", serial);
    out = adb_exec(args); if (out) free(out);
    snprintf(args, sizeof(args), "-s %s shell input keyevent 82", serial);
    out = adb_exec(args); if (out) free(out);
    snprintf(args, sizeof(args), "-s %s shell input swipe 600 1600 600 400 300", serial);
    out = adb_exec(args); if (out) free(out);
    Sleep(800);

    // 2. Limpeza do Armazenamento (Apagar pastas internas exceto Android, Pictures e innovava)
    if (cb) cb("[F3 Nokia T20] Limpando /sdcard (preservando Android, Pictures e innovava) e formatando pasta interna...");
    snprintf(args, sizeof(args),
             "-s %s shell \"cd /sdcard && ls | grep -vwE 'Android|Pictures|innovava' | xargs rm -rf && "
             "mkdir -p /sdcard/Pictures/Screenshots /sdcard/innovava && rm -rf /sdcard/innovava/*\"",
             serial);
    out = adb_exec(args); if (out) free(out);

    // 2.1 Transmitir APK (se fornecido)
    if (apk_path && strlen(apk_path) > 0) {
        char safe_apk[MAX_PATH];
        adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
        if (cb) cb("  -> Transmitindo APK para /sdcard/innovava/ ...");
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial, safe_apk);
        out = adb_exec(args); if (out) free(out);
    }

    // 2.2 Transmitir Pacote ZIP
    if (pacote_path && strlen(pacote_path) > 0) {
        char safe_pacote[MAX_PATH];
        adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
        if (cb) cb("  -> Transmitindo PACOTE ZIP para /sdcard/innovava/ ...");
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/pacote.zip", serial, safe_pacote);
        out = adb_exec(args); if (out) free(out);
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial, safe_pacote);
        out = adb_exec(args); if (out) free(out);
    }

    // 3. Abre aplicativo InnovAva
    const char *pkg = (package_name && strlen(package_name) > 0) ? package_name : "br.com.yantec.innovava";
    if (cb) cb("[F3 Nokia T20] Reiniciando aplicativo InnovAva...");
    snprintf(args, sizeof(args), "-s %s shell am force-stop %s", serial, pkg);
    out = adb_exec(args); if (out) free(out);
    Sleep(300);

    snprintf(args, sizeof(args), "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1", serial, pkg);
    out = adb_exec(args); if (out) free(out);
    Sleep(2500);

    // 4. Abrir menu lateral e importar
    if (cb) cb("[F3 Nokia T20] Abrindo menu lateral para importacao...");
    snprintf(args, sizeof(args), "-s %s shell input swipe 20 600 650 600 300", serial);
    out = adb_exec(args); if (out) free(out);
    Sleep(1500);

    char *xml = t20_dump_and_read_xml(serial, cb);
    if (xml) {
        int ix = 0, iy = 0;
        if (t20_get_centroid(xml, "Importar ZIP", &ix, &iy) || t20_get_centroid(xml, "Importar", &ix, &iy)) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, ix, iy);
            out = adb_exec(args); if (out) free(out);
        }
        free(xml);
    }
    Sleep(2000);

    if (cb) cb("[+] Pipeline F3 do Nokia T20 (Atualização de Pacote) finalizado com sucesso!");
    return PIPELINE_SUCCESS;
}
