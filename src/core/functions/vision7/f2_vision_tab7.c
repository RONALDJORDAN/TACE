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
//  MÓDULO F2: REINSTALAÇÃO LIMPA & DESLIGAMENTO (VISION TAB 7)
// =============================================================================

// Etapa 7 local do F2: igual a F1_Etapa7_CloseApps, porém com espera
// pré-monitoramento de 3s (F1 mantém 5s) e final próprio: após fechar o
// InnovAva, desliga Wi-Fi/Bluetooth/Localização, fixa brilho 70% e
// desliga o aparelho (reboot -p). Isolada neste arquivo para
// efeito colateral zero no F1.
static int F2_Etapa7_CloseApps(const char *serial, const char *package_name,
                               LogCallback cb) {
  if (cb)
    cb("[F2 - Etapa 7] Pacote selecionado. Aguardando 3 segundos para inicio "
       "do processamento de importacao...");

  Sleep(3000); // Espera reduzida exclusiva do F2

  if (cb)
    cb("[-] Monitorando o reaparecimento do botao 'PROCURAR ARQUIVO .ZIP' "
       "(confirmacao de importacao)...");

  int import_sucesso = 0;
  int max_import_checks = 25; // 25 checagens de ~1s (~25s max no total)
  for (int i = 0; i < max_import_checks; i++) {
    char *xml_check = dump_and_read_xml(serial, NULL);
    if (xml_check) {
      int ix = 0, iy = 0;
      if (get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO .ZIP", &ix, &iy,
                                0) ||
          get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO ZIP", &ix, &iy,
                                0) ||
          get_centroid_from_xml(xml_check, "Procurar Arquivo", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "PROCURAR ARQUIVO", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "ARQUIVO .ZIP", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "arquivo zip", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "Procurar", &ix, &iy, 0) ||
          get_centroid_from_xml(xml_check, "PROCURAR", &ix, &iy, 0)) {
        import_sucesso = 1;
        free(xml_check);
        break;
      }
      free(xml_check);
    }
    Sleep(1000);
  }

  if (import_sucesso) {
    if (cb)
      cb("[+] Botao 'PROCURAR ARQUIVO .ZIP' reapareceu! Importacao do ZIP "
         "concluida com sucesso!");
  } else {
    if (cb)
      cb("[WARNING] Tempo limite excedido aguardando o botao 'PROCURAR ARQUIVO "
         ".ZIP'. Prosseguindo assim mesmo...");
  }

  if (cb)
    cb("[-] Fechando aplicativos abertos (App e Gerenciador)...");
  char cmd[256];

  // Pressiona 'Voltar' 3 vezes para sair de qualquer menu aberto
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 4", serial);
  for (int j = 0; j < 3; j++) {
    char *back_out = adb_exec(cmd);
    if (back_out)
      free(back_out);
    Sleep(500);
  }

  // Retorna para a tela inicial
  snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
  char *home_out = adb_exec(cmd);
  if (home_out)
    free(home_out);

  // Força a parada do aplicativo principal
  snprintf(cmd, sizeof(cmd), "-s %s shell am force-stop %s", serial,
           package_name);
  char *stop_app = adb_exec(cmd);
  if (stop_app)
    free(stop_app);

  // Força a parada do gerenciador de arquivos (Google e AOSP)
  snprintf(cmd, sizeof(cmd),
           "-s %s shell am force-stop com.google.android.documentsui", serial);
  char *stop_docs = adb_exec(cmd);
  if (stop_docs)
    free(stop_docs);

  snprintf(cmd, sizeof(cmd),
           "-s %s shell am force-stop com.android.documentsui", serial);
  char *stop_docs2 = adb_exec(cmd);
  if (stop_docs2)
    free(stop_docs2);

  // Finaliza apps em background
  snprintf(cmd, sizeof(cmd), "-s %s shell am kill-all", serial);
  char *kill_all = adb_exec(cmd);
  if (kill_all)
    free(kill_all);

  if (cb)
    cb("[-] Todos os aplicativos encerrados.");

  // Desliga Wi-Fi, Bluetooth e Localizacao, fixa brilho em 70% (178/255)
  if (cb)
    cb("[F2 - Etapa 7] Desativando Wi-Fi, Bluetooth e Localizacao; brilho 70%...");
  char cmd2[512];
  snprintf(cmd2, sizeof(cmd2),
           "-s %s shell \"svc wifi disable && svc bluetooth disable && "
           "settings put secure location_mode 0 && "
           "settings put global wifi_scan_always_enabled 0 && "
           "settings put global ble_scan_always_enabled 0 && "
           "settings put system screen_brightness_mode 0 && "
           "settings put system screen_brightness 178\"",
           serial);
  char *net_off = adb_exec(cmd2);
  if (net_off)
    free(net_off);

  // Desligamento fisico do aparelho
  if (cb)
    cb("[F2 - Etapa 7] Enviando comando de desligamento ao dispositivo...");
  snprintf(cmd, sizeof(cmd), "-s %s shell reboot -p", serial);
  char *pwr_off = adb_exec(cmd);
  if (pwr_off)
    free(pwr_off);

  if (cb)
    cb("[OK] Comando de desligamento enviado. Aparelho desligado.");
  return PIPELINE_SUCCESS;
}

int F2_ExecutarPipelineCompleto(const char *serial, const char *apk_path,
                                const char *pacote_path,
                                const char *package_name, const char *login,
                                const char *senha, const char *municipio,
                                LogCallback cb) {
  if (cb) cb("\n[F2 Vision7] INICIANDO FLUXO OBJETIVO F2 (REINSTALACAO LIMPA -> LOGIN -> PACOTE -> DESLIGAMENTO)...\n");

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

  // 5. Instalação do APK
  if (!V7_Etapa2(serial, safe_apk, pkg, cb)) return PIPELINE_ERROR;

  // 6. Adiciona Atalho
  if (!V7_Etapa2_1_AddShortcut(serial, pkg, cb)) return PIPELINE_ERROR;

  // 7. Inicia Conexão Gnirehtet para Login
  if (!V7_Etapa2_5(serial, cb)) return PIPELINE_ERROR;

  // 8. Concessão de Permissões e Abertura do App
  if (!V7_Etapa3(serial, pkg, cb)) {
    V7_Etapa4_5_StopGnirehtet(serial, cb);
    return PIPELINE_ERROR;
  }

  // 9. Autenticação e Login Automático BotLearn (F2: sem recheck de município pós-senha)
  if (!V7_Etapa4_SkipMunicipioRecheck(serial, login, senha, municipio, cb)) {
    V7_Etapa4_5_StopGnirehtet(serial, cb);
    return PIPELINE_ERROR;
  }

  // 10. Desliga Gnirehtet após Login Concluído
  V7_Etapa4_5_StopGnirehtet(serial, cb);

  // 11. Abre Gaveta Lateral -> Importar ZIP
  if (!V7_Etapa5(serial, cb)) return PIPELINE_ERROR;

  // 12. Seleciona o Pacote ZIP no DocumentsUI (SAF)
  if (!V7_Etapa6(serial, cb)) return PIPELINE_ERROR;

  // 13. Monitora extração, encerra apps, desliga redes, brilho 70% e desliga
  if (!F2_Etapa7_CloseApps(serial, pkg, cb)) return PIPELINE_ERROR;

  if (cb) cb("\n[F2 Vision7] -> PIPELINE F2 CONCLUIDO COM SUCESSO (DISPOSITIVO DESLIGADO).");

  return PIPELINE_SUCCESS;
}
