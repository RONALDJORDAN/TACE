#include "f5_tabmulti_m7.h"

#include "../../drivers/adb.h"
#include "../../learn/learn_db.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// Pipeline F5: Multilaser TabMulti M7 - Provisionamento, Ativação de Túnel Gnirehtet, Autenticação, Espera de 30s e Desligamento Total
int M7_ExecutarPipelineF5(const char *serial, const char *apk_path,
                          const char *pacote_path, const char *wp_path,
                          const char *login, const char *senha,
                          const char *municipio, LogCallback cb) {
  (void)wp_path;
  if (!serial)
    return PIPELINE_ERROR;

  if (cb)
    cb("[+] [F5 - Tab-MULTI M7] Iniciando esteira dedicada: Autenticação + Túnel Gnirehtet (30s) + Shutdown...");

  char args[512];
  char *out = NULL;

  // 1. Acordar a tela
  if (cb) cb("[-] [F5 - Etapa 0] Destravando tela...");
  M7_UnlockScreen(serial, cb);

  // 2. Configurações de exibição (Dark Mode, Brilho 70%, Retrato e Fonte 1.15)
  if (cb) cb("[-] [F5 - Etapa 1] Aplicando configurações visuais (Dark Mode, Retrato, Brilho 70%)...");
  M7_SetDarkMode(serial, cb);
  snprintf(args, sizeof(args), "-s %s shell settings put system screen_brightness 178", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell settings put system accelerometer_rotation 0", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell settings put system user_rotation 0", serial);
  out = adb_exec(args); if (out) free(out);
  snprintf(args, sizeof(args), "-s %s shell settings put system font_scale 1.15", serial);
  out = adb_exec(args); if (out) free(out);

  // 3. Configurações do App InnovAva
  if (cb) cb("[-] [F5 - Etapa 1.5] Concedendo permissões silenciosas ao InnovAva...");
  M7_ConfigureApp(serial, "br.com.yantec.innovava", cb);

  // 4. Limpeza da pasta /sdcard/innovava/ e Carga dos Arquivos
  if (cb) cb("[-] [F5 - Etapa 1.6] Preparando diretório /sdcard/innovava/...");
  snprintf(args, sizeof(args), "-s %s shell \"mkdir -p /sdcard/innovava\"", serial);
  out = adb_exec(args); if (out) free(out);

  if (apk_path && strlen(apk_path) > 0) {
    char safe_apk[MAX_PATH];
    if (adb_get_safe_path(apk_path, safe_apk, sizeof(safe_apk))) {
      if (cb) cb("  -> Copiando APK para /sdcard/innovava/...");
      snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/innovava/innovava.apk", serial, safe_apk);
      out = adb_exec(args); if (out) free(out);
    }
  }

  if (pacote_path && strlen(pacote_path) > 0) {
    char safe_pacote[MAX_PATH];
    if (adb_get_safe_path(pacote_path, safe_pacote, sizeof(safe_pacote))) {
      const char *filename = strrchr(safe_pacote, '\\');
      if (!filename) filename = strrchr(safe_pacote, '/');
      filename = filename ? filename + 1 : safe_pacote;

      char remote_pacote[512];
      snprintf(remote_pacote, sizeof(remote_pacote), "/sdcard/innovava/%s", filename);

      if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg), "  -> Enviando pacote ZIP '%s'...", filename);
        cb(msg);
      }
      snprintf(args, sizeof(args), "-s %s push \"%s\" \"%s\"", serial, safe_pacote, remote_pacote);
      out = adb_exec(args); if (out) free(out);
    }
  }

  // 5. Desinstalar versão residente e Reinstalar o aplicativo InnovAva
  if (cb) cb("[-] [F5 - Etapa 2] Removendo versão residente do aplicativo InnovAva (uninstall)...");
  snprintf(args, sizeof(args), "-s %s uninstall br.com.yantec.innovava", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1500);

  if (apk_path && strlen(apk_path) > 0) {
    char safe_apk[MAX_PATH];
    if (adb_get_safe_path(apk_path, safe_apk, sizeof(safe_apk))) {
      if (cb) cb("[-] [F5 - Etapa 2.1] Reinstalando aplicativo InnovAva...");
      snprintf(args, sizeof(args), "-s %s install -r -g \"%s\"", serial, safe_apk);
      out = adb_exec(args);
      if (out) free(out);
      Sleep(2000);
    }
  }

  // 6. Iniciar o aplicativo InnovAva
  if (cb) cb("[-] [F5 - Etapa 3] Iniciando aplicativo InnovAva...");
  snprintf(args, sizeof(args), "-s %s shell monkey -p br.com.yantec.innovava -c android.intent.category.LAUNCHER 1", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(2000);

  // 7. ATIVAÇÃO DO TÚNEL GNIREHTET (Internet USB) ASSIM QUE O APP É ABERTO
  if (cb) cb("[-] [F5 - Etapa 3.5] Ativando túnel de internet USB (Gnirehtet) assim que o aplicativo abre...");
  M7_StartGnirehtet(serial, cb);
  Sleep(2500);

  // 8. Injeção de credenciais: Selecionar Município, E-mail, Senha e clicar no botão ENTRAR
  if (cb) cb("[-] [F5 - Etapa 4] Efetuando login e acionando botão Entrar...");
  int login_res = F7_Etapa4_Login(serial, login, senha, municipio, cb);
  if (login_res != PIPELINE_SUCCESS) {
    if (cb) cb("[WARNING] [F5 - Tab-MULTI] F7_Etapa4_Login informou alerta, mas o toque de entrada foi disparado. Prosseguindo...");
  }

  // 9. ESPERA OBRIGATÓRIA DE 30 SEGUNDOS COM TÚNEL GNIREHTET ATIVO
  if (cb) cb("[+] [F5 - Tab-MULTI] Login acionado! Mantendo túnel Gnirehtet ativo por 30 segundos para sincronização completa...");
  for (int s = 30; s > 0; s--) {
    if (s % 5 == 0 || s == 30 || s <= 3) {
      char msg_wait[256];
      snprintf(msg_wait, sizeof(msg_wait), "  -> Aguardando sincronização com servidor: %d segundo(s) restante(s)...", s);
      if (cb) cb(msg_wait);
    }
    Sleep(1000);
  }

  // 10. ENCERRAMENTO E DESLIGAMENTO COMPLETO (REDES, GNIREHTET E APARELHO)
  if (cb) cb("[-] [F5 - Tab-MULTI] Fim da contagem de 30s. Encerrando conexões, redes e desligando o dispositivo...");

  // Parar Túnel Gnirehtet
  M7_StopGnirehtet(serial, cb);

  // Desativar Wi-Fi, Bluetooth e GPS
  snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell service call bluetooth_manager 8", serial);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell settings put secure location_mode 0", serial);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell settings put global wifi_scan_always_enabled 0", serial);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell settings put global ble_scan_always_enabled 0", serial);
  out = adb_exec(args); if (out) free(out);

  // Fechar o aplicativo e limpar recentes
  snprintf(args, sizeof(args), "-s %s shell am force-stop br.com.yantec.innovava", serial);
  out = adb_exec(args); if (out) free(out);

  snprintf(args, sizeof(args), "-s %s shell input keyevent 187", serial);
  out = adb_exec(args); if (out) free(out);
  Sleep(1000);

  snprintf(args, sizeof(args), "-s %s shell input tap 300 750", serial);
  out = adb_exec(args); if (out) free(out);

  // Emitir Desligamento Físico
  if (cb) cb("[+] [F5 - Tab-MULTI] Emitindo comando de desligamento físico (reboot -p)...");
  snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
  out = adb_exec(args); if (out) free(out);

  if (cb) cb("[+] [F5 - Tab-MULTI] Esteira F5 concluída com sucesso!");
  return PIPELINE_SUCCESS;
}
