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

static void f4_module_dir(char *out_path, size_t max_len) {
  if (!out_path || max_len == 0)
    return;
  out_path[0] = '\0';

  HMODULE module = GetModuleHandleA("BotEngine.dll");
  if (!module)
    module = GetModuleHandleA(NULL);

  DWORD len = GetModuleFileNameA(module, out_path, (DWORD)max_len);
  if (len == 0 || len >= max_len) {
    out_path[0] = '\0';
    return;
  }

  char *slash = strrchr(out_path, '\\');
  if (slash)
    *slash = '\0';
}

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
    f4_module_dir(exe_path, sizeof(exe_path));

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
    f4_module_dir(exe_path2, sizeof(exe_path2));

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

static const char *const F4_GOOGLE_ADD_ANOTHER[] = {
    "Add another account", "Adicionar outra conta",
    "Use another account", "Usar outra conta"};
static const char *const F4_GOOGLE_PROVIDER[] = {
    "Sign in with your Google Account", "Fazer login com sua Conta do Google",
    "Google", "Conta Google"};
static const char *const F4_GOOGLE_2FA[] = {
    "2-Step Verification", "duas etapas", "Verify it's you",
    "Verifique se e voce", "verificacao em duas etapas", "Enter the code",
    "Digite o codigo", "verification code", "Check your phone",
    "Verifique seu telefone", "Tap Yes", "Toque em Sim",
    "Try another way", "Tentar outra forma", "More ways to verify",
    "Mais formas de verificar", "Use your phone", "Use seu telefone",
    "Verify your identity", "Verifique sua identidade", "Check your email",
    "Verifique seu e-mail"};
static const char *const F4_GOOGLE_ERRORS[] = {
    "Couldn't find your Google Account",
    "Could not find your Google Account",
    "Nao foi possivel encontrar sua Conta do Google",
    "Enter a valid email", "Digite um e-mail valido", "Wrong password",
    "Senha incorreta", "A senha esta incorreta", "Your password was changed",
    "Sua senha foi alterada", "couldn't sign you in", "could not sign you in",
    "Nao foi possivel fazer login", "This account is not allowed",
    "Esta conta nao tem permissao", "This device isn't Play Protect certified",
    "dispositivo nao e certificado", "This account already exists",
    "Esta conta ja existe", "There was a problem", "Ocorreu um erro",
    "Algo deu errado"};
static const char *const F4_GOOGLE_SUCCESS[] = {
    "Account added", "Conta adicionada", "You're all set", "Tudo pronto",
    "Your account is ready", "Sua conta esta pronta"};
static const char *const F4_GOOGLE_TERMS[] = {
    "I agree", "Aceito", "Concordo", "Accept", "Aceitar", "Agree",
    "Concordar", "Continue", "Continuar", "More", "Mais", "Allow",
    "Permitir"};
static const char *const F4_GOOGLE_NEXT[] = {
    "Next", "Proximo", "Proxima", "Avancar", "Continue", "Continuar",
    "Sign in", "Entrar", "Done", "Concluir", "Finish", "Finalizar"};
static const char *const F4_GOOGLE_DISMISS[] = {
    "No thanks", "Nao, obrigado", "Not now", "Agora nao", "Skip", "Pular",
    "Later", "Mais tarde"};
static const char *const F4_GOOGLE_PASSWORD[] = {
    "Enter your password", "Digite sua senha", "Forgot password",
    "Esqueceu a senha", "Show password", "Mostrar senha"};
static const char *const F4_GOOGLE_USE_PASSWORD[] = {
    "Use your password", "Usar sua senha", "Use password instead",
    "Usar senha"};

#define F4_GOOGLE_ARRAY_COUNT(a) ((int)(sizeof(a) / sizeof((a)[0])))

static int f4_google_sleep(int ms) {
  engine_sleep_check(ms);
  return g_engine_canceled ? 0 : 1;
}

static char *f4_google_dump(const char *serial, LogCallback cb) {
  char *xml = adb_dump_stdout(serial);
  if (xml)
    return xml;

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell rm -f /sdcard/f4_google_dump.xml",
           serial);
  char *out = adb_exec(args);
  if (out)
    free(out);

  snprintf(args, sizeof(args),
           "-s %s shell uiautomator dump /sdcard/f4_google_dump.xml", serial);
  out = adb_exec(args);
  if (out)
    free(out);

  snprintf(args, sizeof(args), "-s %s shell cat /sdcard/f4_google_dump.xml",
           serial);
  out = adb_exec(args);
  if (out && (strstr(out, "<hierarchy") || strstr(out, "<?xml")))
    return out;
  if (out)
    free(out);
  return NULL;
}

static int f4_google_tap_xy(const char *serial, int cx, int cy,
                            LogCallback cb) {
  char args[256];
  snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
  char *out = adb_exec(args);
  int ok = out != NULL;
  if (out)
    free(out);
  return ok;
}

static int f4_google_xml_has_any(const char *xml, const char *const *items,
                                 int count) {
  if (!xml)
    return 0;
  for (int i = 0; i < count; i++) {
    if (inno_stristr_fuzzy(xml, items[i]))
      return 1;
  }
  return 0;
}

static int f4_google_tap_any(const char *serial, const char *xml,
                             const char *const *items, int count,
                             LogCallback cb) {
  int cx = 0;
  int cy = 0;
  for (int i = 0; i < count; i++) {
    if (adb_get_centroid_fuzzy(xml, items[i], &cx, &cy, 0)) {
      if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg), "[F4 Google] Clicando em '%s'...",
                 items[i]);
        cb(msg);
      }
      return f4_google_tap_xy(serial, cx, cy, cb);
    }
  }
  return 0;
}

static int f4_google_tap_edit(const char *serial, const char *xml,
                              LogCallback cb) {
  int cx = 0;
  int cy = 0;
  if (!adb_get_centroid_fuzzy(xml, "android.widget.EditText", &cx, &cy, 0))
    return 0;
  if (cx <= 0 || cy <= 0)
    return 0;
  return f4_google_tap_xy(serial, cx, cy, cb);
}

static int f4_google_tap_clickable_any(const char *serial, const char *xml,
                                       const char *const *items, int count,
                                       LogCallback cb) {
  if (!xml)
    return 0;
  for (int i = 0; i < count; i++) {
    const char *p = xml;
    int skip = 0;
    while ((p = inno_stristr_fuzzy(p, items[i])) != NULL) {
      const char *tag_start = p;
      while (tag_start > xml && strncmp(tag_start, "<node ", 6) != 0)
        tag_start--;
      const char *tag_end = strchr(p, '>');
      if (strncmp(tag_start, "<node ", 6) == 0 && tag_end) {
        size_t len = (size_t)(tag_end - tag_start + 1);
        if (len < 2048) {
          char tag[2048];
          memcpy(tag, tag_start, len);
          tag[len] = '\0';
          if (strstr(tag, "clickable=\"true\"") &&
              strstr(tag, "enabled=\"true\"")) {
            int cx = 0;
            int cy = 0;
            if (adb_get_centroid_fuzzy(xml, items[i], &cx, &cy, skip) &&
                cx > 0 && cy > 0) {
              char msg[128];
              snprintf(msg, sizeof(msg), "[F4 Google] Clicando em '%s'...",
                       items[i]);
              if (cb)
                cb(msg);
              return f4_google_tap_xy(serial, cx, cy, cb);
            }
          }
        }
      }
      p++;
      skip++;
    }
  }
  return 0;
}

static int f4_google_scroll_down(const char *serial, LogCallback cb) {
  char args[256];
  snprintf(args, sizeof(args),
           "-s %s shell input swipe 300 850 300 350 350", serial);
  char *out = adb_exec(args);
  int ok = out != NULL;
  if (out)
    free(out);
  return ok;
}

static void f4_google_escape_text(const char *src, char *dest,
                                  size_t dest_size) {
  size_t written = 0;
  while (*src && written + 5 < dest_size) {
    if (*src == ' ') {
      dest[written++] = '%';
      dest[written++] = 's';
    } else if (*src == '\'') {
      dest[written++] = '\'';
      dest[written++] = '\\';
      dest[written++] = '\'';
      dest[written++] = '\'';
    } else if (*src == '"') {
      dest[written++] = '\\';
      dest[written++] = '"';
    } else if (*src == '\\') {
      dest[written++] = '\\';
      dest[written++] = '\\';
    } else {
      dest[written++] = *src;
    }
    src++;
  }
  dest[written] = '\0';
}

static int f4_google_clear_and_type(const char *serial, const char *text,
                                    LogCallback cb) {
  if (!text || !*text)
    return 0;
  if (!f4_google_sleep(600))
    return 0;

  char args[8192];
  snprintf(args, sizeof(args),
           "-s %s shell \"input keyevent 123; i=0; while [ $i -lt 80 ]; do "
           "input keyevent 67; i=$((i+1)); done\"",
           serial);
  char *out = adb_exec(args);
  if (out)
    free(out);
  if (!f4_google_sleep(300))
    return 0;

  char escaped[4096];
  f4_google_escape_text(text, escaped, sizeof(escaped));
  snprintf(args, sizeof(args), "-s %s shell input text '%s'", serial, escaped);
  out = adb_exec(args);
  if (out)
    free(out);
  SecureZeroMemory(escaped, sizeof(escaped));
  SecureZeroMemory(args, sizeof(args));
  if (!f4_google_sleep(800))
    return 0;

  snprintf(args, sizeof(args), "-s %s shell input keyevent 66", serial);
  out = adb_exec(args);
  if (out)
    free(out);
  int result = f4_google_sleep(1200);
  SecureZeroMemory(args, sizeof(args));
  return result;
}

static int f4_google_account_registered(const char *serial, const char *email,
                                        LogCallback cb) {
  if (!email || !*email)
    return 0;

  char args[512];
  snprintf(args, sizeof(args), "-s %s shell dumpsys account", serial);
  char *out = adb_exec(args);
  if (!out)
    return 0;

  char needle[256];
  snprintf(needle, sizeof(needle), "name=%s", email);
  int found = inno_stristr_fuzzy(out, needle) != NULL;
  free(out);
  return found;
}

static int f4_google_ping_ok(const char *serial, LogCallback cb) {
  char args[512];
  snprintf(args, sizeof(args),
           "-s %s shell \"ping -c 1 -W 2 8.8.8.8 2>/dev/null\"", serial);
  char *out = adb_exec(args);
  if (!out)
    return 0;

  int ok = strstr(out, "1 received") != NULL ||
           strstr(out, "1 packets received") != NULL ||
           strstr(out, ", 0% packet loss") != NULL;
  free(out);
  return ok;
}

static int f4_google_tunnel_active(const char *serial, LogCallback cb) {
  char args[512];
  snprintf(args, sizeof(args),
           "-s %s shell \"ip route get 8.8.8.8 2>/dev/null\"", serial);
  char *out = adb_exec(args);
  int active = out && strstr(out, "dev tun") != NULL;
  free(out);
  return active;
}

static int f4_google_ensure_internet(const char *serial, LogCallback cb) {
  if (f4_google_ping_ok(serial, cb) && f4_google_tunnel_active(serial, cb)) {
    if (cb)
      cb("[F4 Google] Internet via Gnirehtet confirmada.");
    return 1;
  }

  if (f4_google_tunnel_active(serial, cb)) {
    if (cb)
      cb("[F4 Google] Tunel Gnirehtet sem internet. Reiniciando...");
    F4_ExecutarFerramenta(serial, 9, cb);
    if (!f4_google_sleep(1500))
      return 0;
  }

  if (cb)
    cb("[F4 Google] Garantindo internet via Gnirehtet...");
  F4_ExecutarFerramenta(serial, 8, cb);

  for (int i = 0; i < 30; i++) {
    if (!f4_google_sleep(1500))
      return 0;
    if (f4_google_ping_ok(serial, cb) && f4_google_tunnel_active(serial, cb)) {
      if (cb)
        cb("[F4 Google] Internet via Gnirehtet confirmada.");
      return 1;
    }
  }

  if (cb)
    cb("[F4 Google] Erro: sem internet via Gnirehtet. Verifique o cabo USB, "
       "a rede do PC e o Gnirehtet.");
  return 0;
}

static int f4_google_prepare_signin(const char *serial, LogCallback cb) {
  char args[512];
  for (int attempt = 0; attempt < 10; attempt++) {
    char *xml = f4_google_dump(serial, cb);
    if (xml) {
      int cx = 0;
      int cy = 0;
      int is_google_ui =
          inno_stristr_fuzzy(xml, "com.google.android.gms") != NULL ||
          inno_stristr_fuzzy(xml, "com.google.android.gsf") != NULL;
      int has_password_field =
          adb_get_centroid_fuzzy(xml, "password=\"true\"", &cx, &cy, 0);
      if (is_google_ui && !has_password_field &&
          adb_get_centroid_fuzzy(xml, "android.widget.EditText", &cx, &cy, 0)) {
        free(xml);
        return 1;
      }
      if (f4_google_tap_any(serial, xml, F4_GOOGLE_ADD_ANOTHER,
                            F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_ADD_ANOTHER),
                            cb)) {
        free(xml);
        if (!f4_google_sleep(2500))
          return 0;
        continue;
      }
      if (f4_google_tap_any(serial, xml, F4_GOOGLE_PROVIDER,
                            F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_PROVIDER), cb)) {
        free(xml);
        if (!f4_google_sleep(2500))
          return 0;
        continue;
      }
      free(xml);
    }

    if (attempt == 3) {
      snprintf(args, sizeof(args),
               "-s %s shell am start -a "
               "android.settings.ADD_ACCOUNT_SETTINGS --esa accountTypes "
               "com.google",
               serial);
      char *out = adb_exec(args);
      if (out)
        free(out);
    } else if (attempt == 6) {
      snprintf(args, sizeof(args),
               "-s %s shell am start -n "
               "com.android.settings/.accounts.AddAccountSettings",
               serial);
      char *out = adb_exec(args);
      if (out)
        free(out);
    }

    if (!f4_google_sleep(1000))
      return 0;
  }
  return 0;
}

int F4_AdicionarContaGoogle(const char *serial, const char *email,
                            const char *senha, LogCallback cb) {
  if (!serial || !*serial || !email || !*email || !senha || !*senha) {
    if (cb)
      cb("[F4 Google] Erro: serial, e-mail e senha sao obrigatorios.");
    return PIPELINE_ERROR;
  }
  if (!strchr(email, '@') || strlen(email) > 200 || strlen(senha) > 800) {
    if (cb)
      cb("[F4 Google] Erro: e-mail invalido ou credencial muito longa.");
    return PIPELINE_ERROR;
  }

  char msg[512];
  char args[512];
  snprintf(msg, sizeof(msg),
           "[F4 Google] Aparelho [%s] -> preparando inclusao da conta %s...",
           serial, email);
  if (cb)
    cb(msg);

  if (f4_google_account_registered(serial, email, cb)) {
    if (cb)
      cb("[F4 Google] A conta ja esta cadastrada neste aparelho.");
    return PIPELINE_SUCCESS;
  }

  snprintf(args, sizeof(args),
           "-s %s shell \"input keyevent 224 && input keyevent 82\"", serial);
  char *out = adb_exec(args);
  if (out)
    free(out);
  if (!f4_google_sleep(500))
    return PIPELINE_ERROR;

  snprintf(args, sizeof(args), "-s %s shell wm dismiss-keyguard", serial);
  out = adb_exec(args);
  if (out)
    free(out);
  if (!f4_google_sleep(500))
    return PIPELINE_ERROR;

  if (!f4_google_ensure_internet(serial, cb)) {
    if (cb)
      cb(g_engine_canceled
             ? "[F4 Google] Operacao cancelada."
             : "[F4 Google] Sem internet via Gnirehtet; inclusao abortada.");
    return PIPELINE_ERROR;
  }

  if (cb)
    cb("[F4 Google] Abrindo a tela de adicionar conta...");
  snprintf(args, sizeof(args),
           "-s %s shell am start -a android.settings.ADD_ACCOUNT_SETTINGS",
           serial);
  out = adb_exec(args);
  if (out)
    free(out);
  if (!f4_google_sleep(2500))
    return PIPELINE_ERROR;

  if (!f4_google_prepare_signin(serial, cb)) {
    if (cb)
      cb(g_engine_canceled
             ? "[F4 Google] Operacao cancelada."
             : "[F4 Google] Nao foi possivel abrir o login do Google. "
               "Verifique se o tablet esta desbloqueado, as Configuracoes "
               "estao liberadas e a internet esta ativa.");
    return PIPELINE_ERROR;
  }

  char *xml = f4_google_dump(serial, cb);
  if (!xml || !f4_google_tap_edit(serial, xml, cb)) {
    if (xml)
      free(xml);
    if (cb)
      cb("[F4 Google] Campo de e-mail nao encontrado.");
    return PIPELINE_ERROR;
  }
  free(xml);

  if (cb)
    cb("[F4 Google] Preenchendo e-mail...");
  if (!f4_google_clear_and_type(serial, email, cb)) {
    if (cb)
      cb("[F4 Google] Falha ao preencher o e-mail.");
    return PIPELINE_ERROR;
  }
  if (!f4_google_sleep(2500))
    return PIPELINE_ERROR;

  int password_ready = 0;
  for (int attempt = 0; attempt < 30; attempt++) {
    xml = f4_google_dump(serial, cb);
    if (xml) {
      if (f4_google_xml_has_any(xml, F4_GOOGLE_ERRORS,
                                F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_ERRORS))) {
        if (cb)
          cb("[F4 Google] O Google recusou o e-mail. Verifique a conta.");
        free(xml);
        return PIPELINE_ERROR;
      }

      if (f4_google_tap_any(serial, xml, F4_GOOGLE_USE_PASSWORD,
                            F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_USE_PASSWORD),
                            cb)) {
        free(xml);
        if (!f4_google_sleep(1500))
          return PIPELINE_ERROR;
        continue;
      }

      int cx = 0;
      int cy = 0;
      int has_password_attr =
          adb_get_centroid_fuzzy(xml, "password=\"true\"", &cx, &cy, 0);
      if (has_password_attr && (cx <= 0 || cy <= 0))
        has_password_attr = 0;
      int is_password_screen =
          f4_google_xml_has_any(xml, F4_GOOGLE_PASSWORD,
                                F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_PASSWORD));
      if (has_password_attr || is_password_screen) {
        if (has_password_attr)
          f4_google_tap_xy(serial, cx, cy, cb);
        else
          f4_google_tap_edit(serial, xml, cb);
        free(xml);
        password_ready = 1;
        break;
      }

      f4_google_tap_any(serial, xml, F4_GOOGLE_NEXT,
                        F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_NEXT), cb);
      free(xml);
    }
    if (!f4_google_sleep(1000))
      return PIPELINE_ERROR;
  }

  if (!password_ready) {
    if (cb)
      cb("[F4 Google] Campo de senha nao encontrado. O fluxo do Google pode "
         "ter mudado.");
    return PIPELINE_ERROR;
  }

  if (cb)
    cb("[F4 Google] Preenchendo senha...");
  if (!f4_google_clear_and_type(serial, senha, cb)) {
    if (cb)
      cb("[F4 Google] Falha ao preencher a senha.");
    return PIPELINE_ERROR;
  }
  if (!f4_google_sleep(3000))
    return PIPELINE_ERROR;

  int account_ok = 0;
  int twofa_notified = 0;
  ULONGLONG wait_started = GetTickCount64();
  while (GetTickCount64() - wait_started < 180000ULL) {
    if (g_engine_canceled)
      return PIPELINE_ERROR;

    if (f4_google_account_registered(serial, email, cb)) {
      account_ok = 1;
      break;
    }

    xml = f4_google_dump(serial, cb);
    if (xml) {
      if (f4_google_xml_has_any(xml, F4_GOOGLE_ERRORS,
                                F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_ERRORS))) {
        if (cb)
          cb("[F4 Google] O Google retornou erro de login. Verifique a "
             "senha/conta.");
        free(xml);
        return PIPELINE_ERROR;
      }

      if (f4_google_xml_has_any(xml, F4_GOOGLE_2FA,
                                F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_2FA))) {
        if (!twofa_notified) {
          if (cb)
            cb("[F4 Google] Verificacao em duas etapas solicitada. Conclua no "
               "tablet; aguardando ate 180s...");
          twofa_notified = 1;
        }
        free(xml);
        if (!f4_google_sleep(1000))
          return PIPELINE_ERROR;
        continue;
      }

      if (f4_google_xml_has_any(xml, F4_GOOGLE_SUCCESS,
                                F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_SUCCESS))) {
        account_ok = 1;
        free(xml);
        break;
      }

      if (f4_google_tap_clickable_any(
              serial, xml, F4_GOOGLE_TERMS,
              F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_TERMS), cb)) {
        free(xml);
        if (!f4_google_sleep(2000))
          return PIPELINE_ERROR;
        continue;
      }
      if (f4_google_xml_has_any(xml, F4_GOOGLE_TERMS,
                                F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_TERMS))) {
        if (cb)
          cb("[F4 Google] Termo encontrado, mas botao ainda nao clicavel. "
             "Rolando a tela...");
        free(xml);
        f4_google_scroll_down(serial, cb);
        if (!f4_google_sleep(1200))
          return PIPELINE_ERROR;
        continue;
      }

      if (f4_google_tap_any(serial, xml, F4_GOOGLE_DISMISS,
                            F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_DISMISS), cb)) {
        free(xml);
        if (!f4_google_sleep(1500))
          return PIPELINE_ERROR;
        continue;
      }

      if (f4_google_tap_any(serial, xml, F4_GOOGLE_NEXT,
                            F4_GOOGLE_ARRAY_COUNT(F4_GOOGLE_NEXT), cb)) {
        free(xml);
        if (!f4_google_sleep(2000))
          return PIPELINE_ERROR;
        continue;
      }

      free(xml);
    }

    if (!f4_google_sleep(1000))
      return PIPELINE_ERROR;
  }

  if (!account_ok && f4_google_account_registered(serial, email, cb))
    account_ok = 1;

  if (account_ok) {
    if (cb)
      cb("[F4 Google] Conta Google adicionada com sucesso.");
    snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
    out = adb_exec(args);
    if (out)
      free(out);
    return PIPELINE_SUCCESS;
  }

  if (cb)
    cb("[F4 Google] Tempo esgotado: a conta nao foi confirmada. Verifique o "
       "tablet.");
  return PIPELINE_ERROR;
}
