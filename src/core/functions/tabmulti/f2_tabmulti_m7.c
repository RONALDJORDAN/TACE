#include "f2_tabmulti_m7.h"
#include "m7_helpers.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

static char *m7_dump_and_read_xml(const char *serial, LogCallback cb) {
  char *mem_xml = adb_dump_stdout(serial);
  if (mem_xml) return mem_xml;

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell uiautomator dump /sdcard/window_dump_%s.xml", serial, serial);
  char *out = adb_exec(args);
  if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/window_dump_%s.xml", serial, serial);
  return adb_exec(args);
}

static int m7_get_centroid(const char *xml, const char *search_str, int *out_cx, int *out_cy) {
  return adb_get_centroid_fuzzy(xml, search_str, out_cx, out_cy, 0);
}


int M7_AtualizarPacote(const char *serial, const char *apk_path, const char *pacote_path, LogCallback cb) {
    if (!serial || strlen(serial) == 0) {
        if (cb) cb("[ERROR] Serial do dispositivo Tab M7 nao informado.");
        return PIPELINE_ERROR;
    }

    if (cb) cb("\n=======================================================");
    if (cb) cb("  [M7] INICIANDO ATUALIZAÇÃO DE PACOTE (TAB-MULTI)  ");
    if (cb) cb("=======================================================\n");

    char args[512];
    char *out = NULL;

    // 0. Travar Orientação Vertical (Portrait)
    if (cb) cb("[-] Definindo orientacao da tela do aparelho em Modo Vertical (Portrait)...");
    snprintf(args, sizeof(args),
             "-s %s shell \"settings put system accelerometer_rotation 0 && settings put system user_rotation 0 && wm user-rotation lock 0\"",
             serial);
    out = adb_exec(args);
    if (out) free(out);

    // 1. Acorda e Desbloqueia a tela
    M7_UnlockScreen(serial, cb);

    // 2. Limpar o armazenamento interno (preservando Android, classjava, cstore e dumps)
    if (cb) cb("[-] Limpando e formatando pastas internas do /sdcard/ (excecao: Android, classjava, cstore, dumps)...");
    snprintf(args, sizeof(args),
             "-s %s shell \"cd /sdcard && ls | grep -vwE 'Android|classjava|cstore' | grep -vi 'dump'\"",
             serial);
    out = adb_exec(args);
    if (out) {
      char *line = strtok(out, "\r\n");
      char *files[256];
      int total = 0;
      while (line && total < 256) {
        if (strlen(line) > 0) {
          files[total] = _strdup(line);
          total++;
        }
        line = strtok(NULL, "\r\n");
      }
      if (total > 0) {
        for (int i = 0; i < total; i++) {
          snprintf(args, sizeof(args), "-s %s shell \"rm -rf \\\"/sdcard/%s\\\"\"", serial, files[i]);
          char *rm_out = adb_exec(args);
          if (rm_out) free(rm_out);
          free(files[i]);
        }
      }
      free(out);
    }

    snprintf(args, sizeof(args), "-s %s shell \"am trim-memory br.com.yantec.innovava COMPLETE\"", serial);
    out = adb_exec(args); if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell \"mkdir -p /sdcard/InnovAva /sdcard/innovava\"", serial);
    out = adb_exec(args); if (out) free(out);

    // 3. Copiar APK (se fornecido)
    if (apk_path && strlen(apk_path) > 0) {
        char safe_apk[MAX_PATH] = {0};
        adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
        if (cb) cb("  -> Transmitindo APK atualizado para /sdcard/InnovAva/ e /sdcard/innovava/ ...");
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/InnovAva/", serial, safe_apk);
        out = adb_exec(args); if (out) free(out);
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial, safe_apk);
        out = adb_exec(args); if (out) free(out);
    }

    // 4. Copiar o pacote ZIP
    if (pacote_path && strlen(pacote_path) > 0) {
        char safe_pacote[MAX_PATH] = {0};
        adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
        if (cb) cb("  -> Transmitindo PACOTE ZIP para /sdcard/InnovAva/ e /sdcard/innovava/ ...");
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/InnovAva/", serial, safe_pacote);
        out = adb_exec(args); if (out) free(out);
        snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/", serial, safe_pacote);
        out = adb_exec(args); if (out) free(out);
    }

    // 4. Inicia Gnirehtet para ter internet
    if (cb) cb("[-] Ligando tunel Gnirehtet para garantir internet (anti-bloqueio)...");
    M7_StartGnirehtet(serial, cb);

    // 5. Inicia o app e aguarda Splash
    if (cb) cb("[-] Iniciando aplicativo InnovAva e aguardando tela inicial...");
    snprintf(args, sizeof(args), "-s %s shell am force-stop br.com.yantec.innovava", serial);
    out = adb_exec(args);
    if (out) free(out);
    
    snprintf(args, sizeof(args), "-s %s shell am start -n br.com.yantec.innovava/.MainActivity", serial);
    out = adb_exec(args);
    if (out) free(out);
    Sleep(4000);

    // 6. Tentar abrir o menu lateral (Swipe)
    if (cb) cb("[-] Abrindo o menu lateral (Swipe)...");
    snprintf(args, sizeof(args), "-s %s shell input swipe 10 500 650 500 300", serial);
    out = adb_exec(args);
    if (out) free(out);
    Sleep(1500);

    // 7. Clicar em "Importar ZIP"
    if (cb) cb("[-] Procurando a opcao 'Importar ZIP' no menu...");
    char *xml_menu = m7_dump_and_read_xml(serial, cb);
    int click_ok = 0;
    if (xml_menu) {
        int mx = 0, my = 0;
        if (m7_get_centroid(xml_menu, "Importar ZIP", &mx, &my) ||
            m7_get_centroid(xml_menu, "importar", &mx, &my)) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, mx, my);
            out = adb_exec(args);
            if (out) free(out);
            click_ok = 1;
        }
        free(xml_menu);
    }

    if (!click_ok) {
        if (cb) cb("[WARNING] Falha ao clicar na opcao 'Importar ZIP'. Abortando atualizacao.");
        M7_StopGnirehtet(serial, cb);
        return PIPELINE_ERROR;
    }
    Sleep(2000);

    // 8. Desliga o Gnirehtet na tela de selecao
    if (cb) cb("[-] Tela de importacao carregada. Desligando Gnirehtet...");
    M7_StopGnirehtet(serial, cb);

    // 9. Shutdown e conclusão
    if (cb) cb("[-] Enviando comando de SHUTDOWN para desligar o tablet...");
    snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
    out = adb_exec(args);
    if (out) free(out);

    if (cb) cb("[M7] Atualizacao de pacote F2 finalizada e aparelho desligado com sucesso.");
    return PIPELINE_SUCCESS;
}
