#include "f2_nokia_t20.h"
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


int T20_ExecutarPipelineF2(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb) {
  (void)wp_path;
  if (!serial || strlen(serial) == 0) {
    if (cb) cb("[ERROR] Serial do dispositivo Nokia T20 não informado!");
    return PIPELINE_ERROR;
  }

  if (cb) {
    char header[256];
    snprintf(header, sizeof(header), "========================================\n[F2 NOKIA T20] AUTOMAÇÃO REINSTALAÇÃO LIMPA (MODO PRESERVA APPS)\nSerial: %s | Município: %s\n========================================", serial, municipio ? municipio : "N/A");
    cb(header);
  }

  char args[1024];
  char *out = NULL;

  // 1. Acorda a tela e desbloqueia
  if (cb) cb("[F2 Nokia T20] Acordando o dispositivo e desbloqueando a tela...");
  snprintf(args, sizeof(args), "-s %s shell input keyevent 224", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell input keyevent 82", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell input swipe 600 1600 600 400 300", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1000);

  // 2. Modo Escuro
  if (cb) cb("[F2 Nokia T20] Ativando Modo Escuro...");
  snprintf(args, sizeof(args), "-s %s shell cmd uimode night yes", serial);
  out = adb_exec(args); if (out) free(out);

  // 3. Preservar apps do sistema
  if (cb) cb("[F2 Nokia T20] Preservando aplicativos nativos do sistema (Etapa 1.5 ignorada no modo F2).");

  // 4. Transferir APK e ZIP
  if (cb) cb("[F2 Nokia T20] Transferindo arquivos APK e ZIP para /sdcard/innovava/...");
  snprintf(args, sizeof(args), "-s %s shell mkdir -p /sdcard/innovava", serial);
  out = adb_exec(args); if (out) free(out);

  if (apk_path && strlen(apk_path) > 0) {
    char safe_apk[MAX_PATH];
    adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/app.apk", serial, safe_apk);
    out = adb_exec(args); if (out) free(out);
  }
  if (pacote_path && strlen(pacote_path) > 0) {
    char safe_pacote[MAX_PATH];
    adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/pacote.zip", serial, safe_pacote);
    out = adb_exec(args); if (out) free(out);
  }

  // 5. Instalar APK
  const char *pkg = (package_name && strlen(package_name) > 0) ? package_name : "br.com.yantec.innovava";
  if (cb) cb("[F2 Nokia T20] Reinstalando aplicativo InnovAva...");
  snprintf(args, sizeof(args), "-s %s shell pm install -r -g /sdcard/innovava/app.apk", serial);
  out = adb_exec(args); if (out) free(out);

  // 6. Abrir app e conceder permissões
  if (cb) cb("[F2 Nokia T20] Concedendo permissoes e executando InnovAva...");
  snprintf(args, sizeof(args), "-s %s shell pm grant %s android.permission.CAMERA", serial, pkg);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell pm grant %s android.permission.READ_EXTERNAL_STORAGE", serial, pkg);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell pm grant %s android.permission.WRITE_EXTERNAL_STORAGE", serial, pkg);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1", serial, pkg);
  out = adb_exec(args); if (out) free(out);
  Sleep(3000);

  // 7. Login e Carga
  if (cb) cb("[F2 Nokia T20] Efetuando autenticacao de acesso...");
  if (login && senha) {
    // E-mail
    char *xml = t20_dump_and_read_xml(serial, cb);
    if (xml) {
      int cx = 0, cy = 0;
      if (t20_get_centroid(xml, "E-mail", &cx, &cy) || t20_get_centroid(xml, "Email", &cx, &cy)) {
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
        out = adb_exec(args); if (out) free(out);
        Sleep(500);
        char escaped_login[512];
        adb_escape_text(login, escaped_login, sizeof(escaped_login));
        snprintf(args, sizeof(args), "-s %s shell input text \"%s\"", serial, escaped_login);
        out = adb_exec(args); if (out) free(out);

      }
      free(xml);
    }
  }

  // 8. Importar pacote ZIP
  if (cb) cb("[F2 Nokia T20] Importando pacote avaliativo...");
  snprintf(args, sizeof(args), "-s %s shell input swipe 20 600 650 600 300", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1500);

  char *xml_m = t20_dump_and_read_xml(serial, cb);
  if (xml_m) {
    int ix = 0, iy = 0;
    if (t20_get_centroid(xml_m, "Importar ZIP", &ix, &iy) || t20_get_centroid(xml_m, "Importar", &ix, &iy)) {
      snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, ix, iy);
      out = adb_exec(args); if (out) free(out);
    }
    free(xml_m);
  }
  Sleep(2000);

  if (cb) cb("[+] Pipeline F2 do Nokia T20 finalizado com sucesso (Importação concluída e apps preservados)!");
  return PIPELINE_SUCCESS;
}
