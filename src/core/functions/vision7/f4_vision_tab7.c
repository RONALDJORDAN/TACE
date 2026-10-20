#include "f4_vision_tab7.h"
#include "../../drivers/adb.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

// MACRO: Injeta o sistema de resiliencia em todas as chamadas de adb_exec neste arquivo
#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// =============================================================================
//  MODULO F4: GESTAO DO APPARELHO (HARDWARE, APPS, REDE E WALLPAPER) - VISION TAB 7
// =============================================================================

static const char *PKG_SETTINGS = "com.android.settings";

static const char *CAMERA_PACKAGES[] = {
    "com.softwinner.camera2",
    "com.android.camera2",
    "com.android.camera",
    "com.sec.android.app.camera",
    "com.motorola.camera2",
    "com.motorola.camera3",
    "com.google.android.GoogleCamera",
    "com.huawei.camera",
    "com.mediatek.camera"
};

static const int NUM_CAMERA_PACKAGES = sizeof(CAMERA_PACKAGES) / sizeof(CAMERA_PACKAGES[0]);

// Pacotes de sistema ocultados na Blindagem e reativados na Restauracao.
static const char *APPARELHO_DEBLOAT_PACKAGES[] = {
    // 1. Câmeras
    "com.softwinner.camera2", "com.android.camera2", "com.android.camera",
    "com.sec.android.app.camera", "com.motorola.camera2",
    "com.motorola.camera3", "com.google.android.GoogleCamera",
    "com.huawei.camera", "com.mediatek.camera",

    // 2. Calculadora
    "com.google.android.calculator", "com.android.calculator2",

    // 3. Google Chrome
    "com.android.chrome", "com.chrome.beta", "com.chrome.dev",
    "com.chrome.canary",

    // 4. Contatos
    "com.google.android.contacts", "com.android.contacts",

    // 5. Google Drive e Editores
    "com.google.android.apps.docs", "com.google.android.apps.docs.editors.docs",
    "com.google.android.apps.docs.editors.sheets",
    "com.google.android.apps.docs.editors.slides",

    // 6. Emergência / Safety Hub
    "com.google.android.apps.safetyhub", "com.android.emergency",

    // 7. Files by Google / Files Go (Preserva DocumentsUI para SAF)
    "com.google.android.apps.nbu.files", "com.google.android.apps.nbu.filesgo",

    // 8. Galeria Google Go / Fotos / Galeria AOSP
    "com.google.android.apps.photosgo", "com.google.android.apps.photos",
    "com.android.gallery3d", "com.android.gallery",

    // 9. Google TV / Play Filmes
    "com.google.android.videos", "com.google.android.tv",

    // 10. Gravador de Som / Google Recorder
    "com.google.android.apps.recorder", "com.android.soundrecorder",
    "com.softwinner.soundrecorder",

    // 11. Google Maps / Maps Go
    "com.google.android.apps.maps", "com.google.android.apps.mapslite",

    // 12. Google Meet / Duo
    "com.google.android.apps.tachyon", "com.google.android.apps.meetings",

    // 13. Google Play Store
    "com.android.vending",

    // 14. Relógio / DeskClock
    "com.google.android.deskclock", "com.android.deskclock",

    // 15. YouTube / YouTube Kids / YouTube Music
    "com.google.android.youtube", "com.google.android.youtube.tv",
    "com.google.android.apps.youtube.kids",
    "com.google.android.apps.youtube.music",

    // 16. Google Search / Gmail / Assistente
    "com.google.android.googlequicksearchbox",
    "com.google.android.apps.searchlite", "com.google.android.gm",
    "com.google.android.gm.lite",
    "com.google.android.apps.assistant",
    "com.google.android.apps.googleassistant",
    "com.android.hotwordenrollment.okgoogle",
    "com.android.hotwordenrollment.xgoogle",

    // 17. Google Agenda / Calendar / Calendar Go
    "com.google.android.calendar", "com.android.calendar",
    "com.google.android.syncadapters.calendar"
};

static const int NUM_APPARELHO_DEBLOAT =
    sizeof(APPARELHO_DEBLOAT_PACKAGES) / sizeof(APPARELHO_DEBLOAT_PACKAGES[0]);

int F4_ExecutarFerramenta(const char *serial, int acao, LogCallback cb) {
  char args[1024];
  char msg[256];

  // Guard de validação: verificar o range ANTES de qualquer processamento
  if (acao < 1 || acao > 12) {
    if (cb) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s]: Opcao (%d) invalida. Utilize numeros de 1 a 12.",
               serial, acao);
      cb(msg);
    }
    return PIPELINE_ERROR;
  }

  if (acao == 1 || acao == 5) { // Bloquear Câmeras
    for (int i = 0; i < NUM_CAMERA_PACKAGES; i++) {
        snprintf(args, sizeof(args), "-s %s shell pm suspend %s", serial, CAMERA_PACKAGES[i]);
        char *out = adb_exec(args);
        if (out) free(out);
    }
    if (cb && acao == 1) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Cameras BLOQUEADAS com sucesso.", serial);
      cb(msg);
    }
  }

  if (acao == 2 || acao == 5) { // Restringir Configurações
    snprintf(args, sizeof(args), "-s %s shell pm suspend %s",
             serial, PKG_SETTINGS);
    char *out = adb_exec(args);
    if (out)
      free(out);
    if (cb && acao == 2) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Configuracoes do sistema RESTRITAS.",
               serial);
      cb(msg);
    }
    if (cb && acao == 5) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Modo Restrito ATIVADO (Cameras e Configuracoes restritas).",
               serial);
      cb(msg);
    }
  }

  if (acao == 3 || acao == 6) { // Desbloquear Câmeras
    for (int i = 0; i < NUM_CAMERA_PACKAGES; i++) {
        snprintf(args, sizeof(args), "-s %s shell pm unsuspend %s", serial, CAMERA_PACKAGES[i]);
        char *out = adb_exec(args);
        if (out) free(out);
    }
    if (cb && acao == 3) {
      snprintf(msg, sizeof(msg), "[F4] Aparelho [%s] -> Cameras LIBERADAS.",
               serial);
      cb(msg);
    }
  }

  if (acao == 4 || acao == 6) { // Liberar Configurações
    snprintf(args, sizeof(args), "-s %s shell pm unsuspend %s", serial,
             PKG_SETTINGS);
    char *out = adb_exec(args);
    if (out)
      free(out);
    if (cb && acao == 4) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Configuracoes do sistema LIBERADAS.", serial);
      cb(msg);
    }
    if (cb && acao == 6) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Modo Restrito DESATIVADO (Acesso total liberado).",
               serial);
      cb(msg);
    }
  }

  if (acao == 8) {
    if (cb)
      cb("[F4] Ferramenta 8: Iniciando Gnirehtet Proxy (LIGAR)...");
    char exe_path[MAX_PATH];
    GetModuleFileNameA(NULL, exe_path, MAX_PATH);
    char *last_slash = strrchr(exe_path, '\\');
    if (last_slash)
      *last_slash = '\0';

    char gnirehtet_path[MAX_PATH];
    char gnirehtet_apk[MAX_PATH];
    char gnirehtet_dir[MAX_PATH];

    // Tenta achar no diretório Release
    snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
             "%s\\tools\\reverse_tethering", exe_path);
    if (GetFileAttributesA(gnirehtet_dir) == INVALID_FILE_ATTRIBUTES) {
      // Se não achou, fallback para diretório de Dev
      snprintf(gnirehtet_dir, sizeof(gnirehtet_dir),
               "%s\\..\\..\\tools\\reverse_tethering", exe_path);
    }
    snprintf(gnirehtet_path, sizeof(gnirehtet_path), "%s\\gnirehtet.exe",
             gnirehtet_dir);
    snprintf(gnirehtet_apk, sizeof(gnirehtet_apk), "%s\\gnirehtet.apk",
             gnirehtet_dir);

    char adb_path[MAX_PATH];
    adb_get_path(adb_path, sizeof(adb_path));
    SetEnvironmentVariableA("ADB", adb_path);

    if (cb) cb("  -> Verificando se o cliente Gnirehtet já está instalado...");
    char args_check[1024];
    snprintf(args_check, sizeof(args_check), "-s %s shell pm list packages com.genymobile.gnirehtet", serial);
    char *out_check = adb_exec(args_check);
    int is_installed = 0;
    if (out_check) {
      if (strstr(out_check, "package:com.genymobile.gnirehtet") != NULL) is_installed = 1;
      free(out_check);
    }

    if (is_installed) {
      if (cb) cb("  -> Cliente Gnirehtet já instalado. Pulando reinstalação.");
    } else {
      if (cb) cb("  -> Instalando cliente Gnirehtet no dispositivo...");
      char args_apk[1024];
      snprintf(args_apk, sizeof(args_apk), "-s %s install -r -g \"%s\"", serial, gnirehtet_apk);
      char *out_apk = adb_exec(args_apk);
      if (out_apk) free(out_apk);
    }

    if (cb)
      cb("  -> Autorizando VPN silenciosamente (AppOps Bypass)...");
    char args_op[1024];
    snprintf(
        args_op, sizeof(args_op),
        "-s %s shell appops set com.genymobile.gnirehtet ACTIVATE_VPN allow",
        serial);
    char *out_op = adb_exec(args_op);
    if (out_op)
      free(out_op);

    char relay_cmd[1024];
    snprintf(relay_cmd, sizeof(relay_cmd), "\"%s\" relay", gnirehtet_path);
    STARTUPINFOA si_relay;
    PROCESS_INFORMATION pi_relay;
    ZeroMemory(&si_relay, sizeof(si_relay));
    si_relay.cb = sizeof(si_relay);
    ZeroMemory(&pi_relay, sizeof(pi_relay));
    if (CreateProcessA(NULL, relay_cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                       NULL, gnirehtet_dir, &si_relay, &pi_relay)) {
      CloseHandle(pi_relay.hProcess);
      CloseHandle(pi_relay.hThread);
    }

    Sleep(500);

    char cmdline[1024];
    snprintf(cmdline, sizeof(cmdline), "\"%s\" start %s", gnirehtet_path,
             serial);

    STARTUPINFOA si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    ZeroMemory(&pi, sizeof(pi));

    if (CreateProcessA(NULL, cmdline, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL,
                       gnirehtet_dir, &si, &pi)) {
      if (cb)
        cb("[+] Tunel Gnirehtet proxy CRIADO em background.");
      CloseHandle(pi.hProcess);
      CloseHandle(pi.hThread);
    } else {
      if (cb)
        cb("[ERROR] Falha ao iniciar Gnirehtet.");
    }
    return PIPELINE_SUCCESS;
  }

  if (acao == 9) {
    if (cb)
      cb("[F4] Ferramenta 9: Parando Gnirehtet Proxy (DESLIGAR)...");
    char cmd_stop[1024];
    char exe_path2[MAX_PATH];
    GetModuleFileNameA(NULL, exe_path2, MAX_PATH);
    char *last_slash2 = strrchr(exe_path2, '\\');
    if (last_slash2)
      *last_slash2 = '\0';

    char gnirehtet_path[MAX_PATH];
    snprintf(gnirehtet_path, sizeof(gnirehtet_path),
             "%s\\tools\\reverse_tethering\\gnirehtet.exe", exe_path2);
    if (GetFileAttributesA(gnirehtet_path) == INVALID_FILE_ATTRIBUTES) {
      snprintf(gnirehtet_path, sizeof(gnirehtet_path),
               "%s\\..\\..\\tools\\reverse_tethering\\gnirehtet.exe",
               exe_path2);
    }
    snprintf(cmd_stop, sizeof(cmd_stop), "\"%s\" stop %s", gnirehtet_path,
             serial);

    STARTUPINFOA si_stop;
    PROCESS_INFORMATION pi_stop;
    ZeroMemory(&si_stop, sizeof(si_stop));
    si_stop.cb = sizeof(si_stop);
    ZeroMemory(&pi_stop, sizeof(pi_stop));
    if (CreateProcessA(NULL, cmd_stop, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                       NULL, NULL, &si_stop, &pi_stop)) {
      if (cb)
        cb("[-] Tunel Gnirehtet DESLIGADO.");
      CloseHandle(pi_stop.hProcess);
      CloseHandle(pi_stop.hThread);
    }
    return PIPELINE_SUCCESS;
  }

  if (acao == 10) {
    if (cb)
      cb("[F4] Ferramenta 10: Remocao visual da barra de pesquisa...");
    char args2[512];

    if (cb)
      cb("[-] Abrindo as configuracoes da tela inicial...");
    snprintf(args2, sizeof(args2),
             "-s %s shell am start -n "
             "com.android.launcher3/.settings.SettingsActivity",
             serial);
    char *st = adb_exec(args2);
    if (st)
      free(st);

    Sleep(2000);
    if (cb)
      cb("[-] Mapeando elementos da tela...");

    snprintf(args2, sizeof(args2),
             "-s %s shell uiautomator dump /sdcard/dump.xml", serial);
    char *dm = adb_exec(args2);
    if (dm)
      free(dm);

    snprintf(args2, sizeof(args2), "-s %s shell cat /sdcard/dump.xml", serial);
    char *xml = adb_exec(args2);

    if (xml) {
      char *p = strstr(xml, "pesquisa");

      if (p) {
        char *switch_p = strstr(p, "android.widget.Switch");
        if (switch_p && (switch_p - p < 1500)) {

          char *checked_p = strstr(switch_p, "checked=\"true\"");
          if (checked_p && (checked_p - switch_p < 300)) {
            if (cb)
              cb("[-] A chavinha de remocao ja esta LIGADA. Nenhuma acao "
                 "necessaria.");
          } else {
            char *b_start = strstr(switch_p, "bounds=\"[");
            if (b_start) {
              int x1, y1, x2, y2;
              if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2,
                         &y2) == 4) {
                int cx = (x1 + x2) / 2;
                int cy = (y1 + y2) / 2;

                if (cb)
                  cb("[-] Chavinha encontrada DESLIGADA. Clicando para LIGAR e "
                     "remover a barra...");
                snprintf(args2, sizeof(args2), "-s %s shell input tap %d %d",
                         serial, cx, cy);
                char *tp = adb_exec(args2);
                if (tp)
                  free(tp);

                Sleep(1000);
                snprintf(args2, sizeof(args2), "-s %s shell input keyevent 3",
                         serial);
                char *hm = adb_exec(args2);
                if (hm)
                  free(hm);

                free(xml);
                return PIPELINE_SUCCESS;
              }
            }
          }
        } else {
          if (cb)
            cb("[WARNING] A chavinha (Switch) nao foi encontrada ao lado do "
               "texto.");
        }
      } else {
        if (cb)
          cb("[WARNING] O texto da configuracao nao foi encontrado na tela.");
      }
      free(xml);
    } else {
      if (cb)
        cb("[ERROR] Falha ao ler a tela (UI Automator).");
    }
    return PIPELINE_SUCCESS;
  }



  if (acao == 11) { // Blindagem: Ocultar Aplicativos Dispensáveis
    if (cb) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Aplicando Blindagem (Ocultando aplicativos dispensaveis)...",
               serial);
      cb(msg);
    }

    char cmd_batch[4096];
    int count_in_batch = 0;
    int offset = snprintf(cmd_batch, sizeof(cmd_batch), "-s %s shell \"", serial);

    for (int i = 0; i < NUM_APPARELHO_DEBLOAT; i++) {
        const char *pkg = APPARELHO_DEBLOAT_PACKAGES[i];
        offset += snprintf(cmd_batch + offset, sizeof(cmd_batch) - offset,
                           "pm disable-user --user 0 %s 2>/dev/null; pm suspend %s 2>/dev/null; pm uninstall -k --user 0 %s 2>/dev/null; ",
                           pkg, pkg, pkg);
        count_in_batch++;

        if (count_in_batch >= 6 || i == NUM_APPARELHO_DEBLOAT - 1) {
        if (offset < (int)sizeof(cmd_batch) - 2) {
          cmd_batch[offset++] = '\"';
          cmd_batch[offset] = '\0';
        }
        char *out_b = adb_exec(cmd_batch);
        if (out_b) free(out_b);

        offset = snprintf(cmd_batch, sizeof(cmd_batch), "-s %s shell \"", serial);
        count_in_batch = 0;
      }
    }

    // Limpa o cache do launcher para os icones sumirem da tela inicial imediatamente
    snprintf(args, sizeof(args), "-s %s shell pm clear com.android.launcher3 2>/dev/null", serial);
    char *out_cl = adb_exec(args);
    if (out_cl) free(out_cl);

    if (cb) {
      snprintf(msg, sizeof(msg),
               "[+] Aparelho [%s] -> Blindagem concluida! Aplicativos ocultados com sucesso.",
               serial);
      cb(msg);
    }
    return PIPELINE_SUCCESS;
  }

  if (acao == 12) { // Restauração de Fábrica: Reativar Todos os Aplicativos do Sistema
    if (cb) {
      snprintf(msg, sizeof(msg),
               "[F4] Aparelho [%s] -> Restaurando aplicativos de fabrica (Reativando pacotes do sistema)...",
               serial);
      cb(msg);
    }

    char cmd_batch[4096];
    int count_in_batch = 0;
    int offset = snprintf(cmd_batch, sizeof(cmd_batch), "-s %s shell \"", serial);

    for (int i = 0; i < NUM_APPARELHO_DEBLOAT; i++) {
        const char *pkg = APPARELHO_DEBLOAT_PACKAGES[i];
        offset += snprintf(cmd_batch + offset, sizeof(cmd_batch) - offset,
                           "cmd package install-existing %s 2>/dev/null; pm enable %s 2>/dev/null; pm unsuspend %s 2>/dev/null; ",
                           pkg, pkg, pkg);
        count_in_batch++;

        if (count_in_batch >= 6 || i == NUM_APPARELHO_DEBLOAT - 1) {
        if (offset < (int)sizeof(cmd_batch) - 2) {
          cmd_batch[offset++] = '\"';
          cmd_batch[offset] = '\0';
        }
        char *out_b = adb_exec(cmd_batch);
        if (out_b) free(out_b);

        offset = snprintf(cmd_batch, sizeof(cmd_batch), "-s %s shell \"", serial);
        count_in_batch = 0;
      }
    }

    // Desbloqueia Cameras e Configuracoes
    for (int i = 0; i < NUM_CAMERA_PACKAGES; i++) {
      snprintf(args, sizeof(args), "-s %s shell pm unsuspend %s 2>/dev/null", serial, CAMERA_PACKAGES[i]);
      char *out_c = adb_exec(args);
      if (out_c) free(out_c);
    }
    snprintf(args, sizeof(args), "-s %s shell pm unsuspend %s 2>/dev/null", serial, PKG_SETTINGS);
    char *out_s = adb_exec(args);
    if (out_s) free(out_s);

    // Notifica o launcher para atualizar os icones restaurados
    snprintf(args, sizeof(args), "-s %s shell am broadcast -a android.intent.action.BOOT_COMPLETED 2>/dev/null", serial);
    char *out_bc = adb_exec(args);
    if (out_bc) free(out_bc);

    if (cb) {
      snprintf(msg, sizeof(msg),
               "[+] Aparelho [%s] -> Restauracao de fabrica concluida! Todos os aplicativos foram reativados com sucesso.",
               serial);
      cb(msg);
    }
    return PIPELINE_SUCCESS;
  }

  // Acao valida processada acima - retorno padrao de sucesso
  return PIPELINE_SUCCESS;
}

int F4_DefinirWallpaper(const char *serial, const char *wp_path,
                        LogCallback cb) {
  char args[2048];
  char msg[512];

  if (!wp_path || strlen(wp_path) == 0) {
    if (cb)
      cb("[F4] Erro: O municipio nao possui Wallpaper configurado.");
    return PIPELINE_ERROR;
  }

  char safe_wp[MAX_PATH] = {0};
  adb_get_safe_path(wp_path, safe_wp, MAX_PATH);

  if (cb)
    cb("[F4] Iniciando definicao de Wallpaper...");

  // ----------------------------------------------------------------
  // PASSO 1: Garante a pasta Pictures/Screenshots e envia a imagem
  // ----------------------------------------------------------------
  snprintf(args, sizeof(args),
           "-s %s shell mkdir -p /sdcard/Pictures/Screenshots", serial);
  char *out = adb_exec(args);
  if (out)
    free(out);

  // Limpa qualquer wallpaper anterior para nao confundir a selecao
  snprintf(args, sizeof(args),
           "-s %s shell rm -f /sdcard/Pictures/Screenshots/wallpaper_inno.jpg",
           serial);
  out = adb_exec(args);
  if (out)
    free(out);

  snprintf(args, sizeof(args),
           "-s %s push \"%s\" /sdcard/Pictures/Screenshots/wallpaper_inno.jpg",
           serial, safe_wp);
  out = adb_exec(args);
  if (out) {
    if (cb)
      cb("[-] Imagem enviada para Pictures/Screenshots/wallpaper_inno.jpg");
    free(out);
  } else {
    if (cb)
      cb("[ERRO] Falha ao enviar o arquivo de wallpaper.");
    return PIPELINE_ERROR;
  }

  // Notifica o MediaScanner para o arquivo ser indexado imediatamente
  snprintf(args, sizeof(args),
           "-s %s shell am broadcast -a "
           "android.intent.action.MEDIA_SCANNER_SCAN_FILE "
           "-d file:///sdcard/Pictures/Screenshots/wallpaper_inno.jpg",
           serial);
  out = adb_exec(args);
  if (out)
    free(out);
  Sleep(1500);

  // ----------------------------------------------------------------
  // PASSO 2: Abre as Configurações de Tela e clica em "Plano de fundo"
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Abrindo as configuracoes de tela...");
  snprintf(args, sizeof(args),
           "-s %s shell am start -a android.settings.SETTINGS", serial);
  out = adb_exec(args);
  if (out)
    free(out);
  Sleep(2500);

  // Dumpa a tela e procura o item "Plano de fundo" nas configuracoes
  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/dump_cfg.xml 2>/dev/null",
           serial);
  char *dm1 = adb_exec(args);
  if (dm1)
    free(dm1);
  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_cfg.xml", serial);
  char *xml1 = adb_exec(args);
  if (xml1) {
    char *p = strstr(xml1, "Plano de fundo");
    if (!p)
      p = strstr(xml1, "plano de fundo");
    if (!p)
      p = strstr(xml1, "Wallpaper");
    if (!p)
      p = strstr(xml1, "wallpaper");
    if (p) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                   (x1 + x2) / 2, (y1 + y2) / 2);
          char *tp = adb_exec(args);
          if (tp)
            free(tp);
          if (cb)
            cb("[-] Clicando em 'Planos de fundo'...");
        }
      }
    }
    free(xml1);
  }
  Sleep(2000);

  // ----------------------------------------------------------------
  // PASSO 3: Na tela de Plano de fundo, clica em "Planos de fundo" novamente
  // (o menu tem 2 niveis com o mesmo nome)
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Entrando em 'Planos de fundo' (segundo nivel)...");
  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/dump_wp1.xml 2>/dev/null",
           serial);
  char *dm2 = adb_exec(args);
  if (dm2)
    free(dm2);
  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_wp1.xml", serial);
  char *xml2 = adb_exec(args);
  if (xml2) {
    char *p = strstr(xml2, "Planos de fundo");
    if (!p)
      p = strstr(xml2, "planos de fundo");
    if (p) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                   (x1 + x2) / 2, (y1 + y2) / 2);
          char *tp = adb_exec(args);
          if (tp)
            free(tp);
        }
      }
    }
    free(xml2);
  }
  Sleep(2000);

  // ----------------------------------------------------------------
  // PASSO 4: Dialogo "Selecionar de..." -> clica em "Planos de fundo" (2a opcao)
  // Estrategia em cascata:
  //   [A] Busca texto "Planos de fundo" apos ancora "Plano de fundo e estilo"
  //   [B] Posicionamento relativo: pega bounds da 1a opcao e calcula a 2a abaixo
  //   [C] Fallback por coordenada fixa centralizada
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Aguardando dialogo 'Selecionar de...'...");
  Sleep(1200); // aguarda o dialogo aparecer completamente
  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/dump_sel.xml 2>/dev/null",
           serial);
  char *dm3 = adb_exec(args);
  if (dm3)
    free(dm3);
  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_sel.xml", serial);
  char *xml3 = adb_exec(args);
  int found_fotos = 0;
  if (xml3) {
    // --- ESTRATEGIA PRINCIPAL: Posicionamento Relativo ---
    char *p_first = strstr(xml3, "Planos de fundo");
    if (p_first) {
      char *b1 = strstr(p_first, "bounds=\"[");
      if (b1) {
        int x1, y1, x2, y2;
        if (sscanf(b1, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          int row_h = y2 - y1;    // altura de uma linha do dialogo
          int cx = (x1 + x2) / 2; // centro horizontal
          int cy = y2 + (row_h / 2);

          snprintf(msg, sizeof(msg),
                   "[-] Opcao 1 achada em Y:%d-%d. Clicando no card abaixo em (%d,%d)...",
                   y1, y2, cx, cy);
          if (cb)
            cb(msg);

          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                   cx, cy);
          char *tp = adb_exec(args);
          if (tp)
            free(tp);
          found_fotos = 1;
        }
      }
    }

    // --- FALLBACK 1: Galeria direta se a tela ja mostrar fotos ---
    if (!found_fotos) {
      char *p = strstr(xml3, "Minhas fotos");
      if (!p)
        p = strstr(xml3, "My photos");
      if (!p)
        p = strstr(xml3, "Galeria");
      if (p) {
        char *b = strstr(p, "bounds=\"[");
        if (b) {
          int x1, y1, x2, y2;
          if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                     (x1 + x2) / 2, (y1 + y2) / 2);
            char *tp = adb_exec(args);
            if (tp)
              free(tp);
            found_fotos = 1;
            if (cb)
              cb("[-] [C] 'Minhas fotos' encontrado e clicado.");
          }
        }
      }
    }
    free(xml3);
  }
  if (!found_fotos) {
    // Fallback absoluto: coordenada fixa centralizada da 2a opcao (Vision Tab 7 1024x600)
    if (cb)
      cb("[-] [Fallback] Tocando coordenada fixa da 2a opcao (512,550)...");
    snprintf(args, sizeof(args), "-s %s shell input tap 512 550", serial);
    char *tp = adb_exec(args);
    if (tp)
      free(tp);
  }
  Sleep(2500);

  // ----------------------------------------------------------------
  // PASSO 5: Seleciona a imagem do wallpaper (a unica disponivel - cor azul)
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Selecionando a imagem unica do wallpaper...");
  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/dump_wp3.xml 2>/dev/null",
           serial);
  char *dm4 = adb_exec(args);
  if (dm4)
    free(dm4);
  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_wp3.xml", serial);
  char *xml4 = adb_exec(args);
  int found_img = 0;
  if (xml4) {
    char *p = xml4;
    while ((p = strstr(p, "android.widget.ImageView")) != NULL) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          int width = x2 - x1;
          int height = y2 - y1;
          if (y1 > 150 && width > 100 && height > 100) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                     (x1 + x2) / 2, (y1 + y2) / 2);
            char *tp = adb_exec(args);
            if (tp)
              free(tp);
            found_img = 1;
            if (cb)
              cb("[-] Thumbnail da imagem detectada e clicada.");
            break;
          }
        }
      }
      p++;
    }
    free(xml4);
  }
  if (!found_img) {
    if (cb)
      cb("[-] Fallback: tocando na posicao esperada do 1o card (200,300)...");
    snprintf(args, sizeof(args), "-s %s shell input tap 200 300", serial);
    char *tp = adb_exec(args);
    if (tp)
      free(tp);
  }
  Sleep(2500);

  // ----------------------------------------------------------------
  // PASSO 6: Clica em "Definir plano de fundo" (botao superior esquerdo)
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Clicando em 'Definir plano de fundo'...");
  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/dump_wp4.xml 2>/dev/null",
           serial);
  char *dm5 = adb_exec(args);
  if (dm5)
    free(dm5);
  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_wp4.xml", serial);
  char *xml5 = adb_exec(args);
  int found_btn = 0;
  if (xml5) {
    char *p = strstr(xml5, "Definir plano de fundo");
    if (!p)
      p = strstr(xml5, "Definir papel de parede");
    if (!p)
      p = strstr(xml5, "Set wallpaper");
    if (p) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                   (x1 + x2) / 2, (y1 + y2) / 2);
          char *tp = adb_exec(args);
          if (tp)
            free(tp);
          found_btn = 1;
          if (cb)
            cb("[-] Botao 'Definir plano de fundo' clicado.");
        }
      }
    }
    free(xml5);
  }
  if (!found_btn) {
    if (cb)
      cb("[-] Fallback: botao superior esquerdo (80,30)...");
    snprintf(args, sizeof(args), "-s %s shell input tap 80 30", serial);
    char *tp = adb_exec(args);
    if (tp)
      free(tp);
  }
  Sleep(2000);

  // ----------------------------------------------------------------
  // PASSO 7: Janela flutuante - clica em "Tela inicial e bloqueio de tela"
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Selecionando 'Tela inicial e bloqueio de tela'...");
  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/dump_wp5.xml 2>/dev/null",
           serial);
  char *dm6 = adb_exec(args);
  if (dm6)
    free(dm6);
  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/dump_wp5.xml", serial);
  char *xml6 = adb_exec(args);
  if (xml6) {
    char *p = strstr(xml6, "Tela inicial e bloqueio");
    if (!p)
      p = strstr(xml6, "tela inicial e bloqueio");
    if (!p)
      p = strstr(xml6, "Tela inicial e tela de bloqueio");
    if (!p)
      p = strstr(xml6, "Ambas");
    if (!p)
      p = strstr(xml6, "Home and lock screen");
    if (!p)
      p = strstr(xml6, "Tela inicial");
    if (p) {
      char *b = strstr(p, "bounds=\"[");
      if (b) {
        int x1, y1, x2, y2;
        if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial,
                   (x1 + x2) / 2, (y1 + y2) / 2);
          char *tp = adb_exec(args);
          if (tp)
            free(tp);
          if (cb)
            cb("[-] 'Tela inicial e bloqueio de tela' confirmado.");
        }
      }
    } else {
      if (cb)
        cb("[WARNING] Dialogo de destino nao encontrado. Tentando pressionar Enter...");
      snprintf(args, sizeof(args), "-s %s shell input keyevent 66", serial);
      char *tp = adb_exec(args);
      if (tp)
        free(tp);
    }
    free(xml6);
  }
  Sleep(4000);

  // ----------------------------------------------------------------
  // Finalizacao: Volta para a tela inicial e encerra processos abertos
  // ----------------------------------------------------------------
  if (cb)
    cb("[-] Retornando para a tela inicial e fechando processos abertos...");

  snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
  out = adb_exec(args);
  if (out)
    free(out);

  Sleep(1000);

  // Mata os processos em background
  snprintf(args, sizeof(args), "-s %s shell am kill-all", serial);
  out = adb_exec(args);
  if (out)
    free(out);

  if (cb)
    cb("[F4] Wallpaper definido com sucesso e sistema limpo!");
  return PIPELINE_SUCCESS;
}
