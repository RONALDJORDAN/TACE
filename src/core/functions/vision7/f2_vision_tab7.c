#include "f2_vision_tab7.h"
#include "f1_vision_tab7.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// =============================================================================
//  MÓDULO F2: REINSTALAÇÃO LIMPA & MODO OFFLINE (VISION TAB 7)
// =============================================================================

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

  if (cb) cb("[-] Modo Offline ativado com sucesso!");
  return PIPELINE_SUCCESS;
}

int F2_ExecutarPipelineCompleto(const char *serial, const char *apk_path,
                                const char *pacote_path,
                                const char *package_name, const char *login,
                                const char *senha, const char *municipio,
                                LogCallback cb) {
  if (cb) cb("\n[F2 Vision7] INICIANDO FLUXO OBJETIVO F2 (REINSTALACAO LIMPA -> LOGIN -> PACOTE -> MODO OFFLINE)...\n");

  const char *pkg = (package_name && strlen(package_name) > 0) ? package_name : "br.com.yantec.innovava";

  char safe_apk[MAX_PATH] = {0};
  char safe_pacote[MAX_PATH] = {0};
  if (apk_path && strlen(apk_path) > 0) adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
  if (pacote_path && strlen(pacote_path) > 0) adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);

  // 1. Wakeup & Unlock
  if (!V7_Etapa1(serial, cb)) return PIPELINE_ERROR;

  // 2. Modo Escuro
  if (!V7_Etapa1_2_DarkMode(serial, cb)) return PIPELINE_ERROR;

  // 3. Limpeza de Armazenamento e Cópia de Arquivos (APK e Pacote)
  if (!V7_Etapa1_6(serial, safe_apk, safe_pacote, cb)) return PIPELINE_ERROR;

  // 4. Otimização de Hardware e Redes
  if (!V7_Etapa1_7(serial, cb)) return PIPELINE_ERROR;

  // 5. Limpeza do Launcher
  if (!V7_Etapa1_9_ClearLauncher(serial, cb)) return PIPELINE_ERROR;

  // 6. Instalação do APK
  if (!V7_Etapa2(serial, safe_apk, pkg, cb)) return PIPELINE_ERROR;

  // 7. Adiciona Atalho
  if (!V7_Etapa2_1_AddShortcut(serial, pkg, cb)) return PIPELINE_ERROR;

  // 8. Inicia Conexão Gnirehtet para Login
  if (!V7_Etapa2_5(serial, cb)) return PIPELINE_ERROR;

  // 9. Concessão de Permissões e Abertura do App
  if (!V7_Etapa3(serial, pkg, cb)) {
    V7_Etapa4_5_StopGnirehtet(serial, cb);
    return PIPELINE_ERROR;
  }

  // 10. Autenticação e Login Automático InnoLearn
  if (!V7_Etapa4(serial, login, senha, municipio, cb)) {
    V7_Etapa4_5_StopGnirehtet(serial, cb);
    return PIPELINE_ERROR;
  }

  // 11. Desliga Gnirehtet após Login Concluído
  V7_Etapa4_5_StopGnirehtet(serial, cb);

  // 12. Abre Gaveta Lateral -> Importar ZIP
  if (!V7_Etapa5(serial, cb)) return PIPELINE_ERROR;

  // 13. Seleciona o Pacote ZIP no DocumentsUI (SAF)
  if (!V7_Etapa6(serial, cb)) return PIPELINE_ERROR;

  // 14. Monitora extração e encerra aplicativos
  F1_Etapa7_CloseApps(serial, pkg, cb);

  // 15. Aplica Modo Offline
  F2_Etapa_Offline(serial, cb);

  if (cb) cb("\n[F2 Vision7] -> PIPELINE F2 CONCLUIDO COM SUCESSO (DISPOSITIVO PRONTO).");

  return PIPELINE_SUCCESS;
}
