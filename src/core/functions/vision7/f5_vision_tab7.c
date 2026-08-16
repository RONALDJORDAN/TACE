#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#include "f5_vision_tab7.h"
#include "../../drivers/adb.h"



int F5_ExecutarDesligamento(const char* serial, LogCallback cb) {
    char cmd[512];
    char* out;
    char log_buf[256];

    if (cb) {
        snprintf(log_buf, sizeof(log_buf), "[F5] Iniciando desligamento remoto para aparelho %s...", serial);
        cb(log_buf);
    }

    // 0. Desativar Tunel Gnirehtet antes de desligar
    if (cb) cb("[F5 - Etapa 0] Desativando tunel Gnirehtet (Reverse Tethering)...");
    
    char exe_path[MAX_PATH];
    GetModuleFileNameA(NULL, exe_path, MAX_PATH);
    char *last_slash = strrchr(exe_path, '\\');
    if (last_slash) *last_slash = '\0';

    char gnirehtet_path[MAX_PATH];
    snprintf(gnirehtet_path, sizeof(gnirehtet_path),
             "%s\\tools\\reverse_tethering\\gnirehtet.exe", exe_path);
    if (GetFileAttributesA(gnirehtet_path) == INVALID_FILE_ATTRIBUTES) {
        snprintf(gnirehtet_path, sizeof(gnirehtet_path),
                 "%s\\..\\..\\tools\\reverse_tethering\\gnirehtet.exe", exe_path);
    }

    char cmd_stop[1024];
    snprintf(cmd_stop, sizeof(cmd_stop), "\"%s\" stop %s", gnirehtet_path, serial);

    STARTUPINFOA si_stop;
    PROCESS_INFORMATION pi_stop;
    ZeroMemory(&si_stop, sizeof(si_stop));
    si_stop.cb = sizeof(si_stop);
    ZeroMemory(&pi_stop, sizeof(pi_stop));
    if (CreateProcessA(NULL, cmd_stop, NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si_stop, &pi_stop)) {
        WaitForSingleObject(pi_stop.hProcess, 3000);
        CloseHandle(pi_stop.hProcess);
        CloseHandle(pi_stop.hThread);
        if (cb) cb("[-] Tunel Gnirehtet DESLIGADO.");
    } else {
        if (cb) cb("[WARNING] Falha ao tentar desativar tunel Gnirehtet.");
    }
    Sleep(500);

    // 1. Desativar WiFi
    if (cb) cb("[F5 - Etapa 1] Desativando Wi-Fi...");
    snprintf(cmd, sizeof(cmd), "-s %s shell svc wifi disable", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    Sleep(500);

    // 2. Desativar Bluetooth
    if (cb) cb("[F5 - Etapa 2] Desativando Bluetooth...");
    snprintf(cmd, sizeof(cmd), "-s %s shell svc bluetooth disable", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    Sleep(500);

    // 3. Limpar todos os aplicativos abertos (Visual Manual no Tablet + Processos)
    if (cb) cb("[F5 - Etapa 3] [LIMPEZA MANUAL VISUAL] Abrindo a tela de aplicativos recentes no tablet...");

    // 3a. Garante tela acesa e desbloqueada para interagir com a UI de recentes
    snprintf(cmd, sizeof(cmd), "-s %s shell \"input keyevent 224 && input keyevent 82\"", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    Sleep(500);

    // 3b. Ir para HOME
    snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    Sleep(500);

    // 3c. Abre a tela de Recentes (KEYEVENT 187) com tempo para a UI dos cartões abertos aparecer na tela
    snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 187", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    Sleep(1200); // Aguarda a tela de recentes carregar visualmente no tablet

    // 3d. Desliza visualmente os apps abertos para a direita
    if (cb) cb("[-] [LIMPEZA MANUAL VISUAL] Deslizando os apps abertos para localizar o botao 'Limpar tudo'...");
    for (int s = 0; s < 2; s++) {
        snprintf(cmd, sizeof(cmd), "-s %s shell input swipe 150 500 850 500 250", serial);
        out = adb_exec(cmd);
        if (out) free(out);
        Sleep(500);
    }

    // 3e. Lê o XML e clica no botão 'Limpar tudo'
    int v7_cleared = 0;
    snprintf(cmd, sizeof(cmd), "-s %s shell uiautomator dump /sdcard/dump_recents.xml 2>/dev/null", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    snprintf(cmd, sizeof(cmd), "-s %s shell cat /sdcard/dump_recents.xml", serial);
    out = adb_exec(cmd);
    if (out) {
        char *p = strstr(out, "Limpar tudo");
        if (!p) p = strstr(out, "LIMPAR TUDO");
        if (!p) p = strstr(out, "Clear all");
        if (!p) p = strstr(out, "CLEAR ALL");
        if (!p) p = strstr(out, "Limpar");
        if (!p) p = strstr(out, "LIMPAR");
        if (!p) p = strstr(out, "Fechar tudo");
        if (!p) p = strstr(out, "FECHAR TUDO");
        if (p) {
            char *b = strstr(p, "bounds=\"[");
            if (b) {
                int x1, y1, x2, y2;
                if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
                    snprintf(cmd, sizeof(cmd), "-s %s shell input tap %d %d", serial, (x1 + x2) / 2, (y1 + y2) / 2);
                    char *tc = adb_exec(cmd);
                    if (tc) free(tc);
                    if (cb) cb("[-] [LIMPEZA MANUAL VISUAL] Botao 'Limpar tudo' clicado na tela de recentes.");
                    v7_cleared = 1;
                }
            }
        }
        free(out);
    }

    if (!v7_cleared) {
        snprintf(cmd, sizeof(cmd), "-s %s shell input swipe 150 500 850 500 250", serial);
        out = adb_exec(cmd);
        if (out) free(out);
        Sleep(500);

        if (cb) cb("[-] [LIMPEZA MANUAL VISUAL] Toque de fallback no botao de limpar recentes...");
        snprintf(cmd, sizeof(cmd), "-s %s shell input tap 300 1100", serial);
        out = adb_exec(cmd);
        if (out) free(out);
    }
    Sleep(1000); // Aguarda visualmente os cartões sumirem

    // 3f. Volta para a HOME
    snprintf(cmd, sizeof(cmd), "-s %s shell input keyevent 3", serial);
    out = adb_exec(cmd);
    if (out) free(out);
    Sleep(500);

    // 3g. Encerramento forçado de todos os apps e processos em background
    snprintf(cmd, sizeof(cmd),
             "-s %s shell \"am force-stop br.com.yantec.innovava && am force-stop com.google.android.documentsui && "
             "am force-stop com.android.documentsui && am force-stop com.android.chrome && am kill-all\"", serial);
    out = adb_exec(cmd);
    if (out) free(out);

    // 4. Desligamento do aparelho (shutdown)
    if (cb) cb("[F5 - Etapa 4] Executando Shutdown (reboot -p)... O aparelho será desligado fisicamente.");
    snprintf(cmd, sizeof(cmd), "-s %s shell reboot -p", serial);
    out = adb_exec(cmd);
    if (out) free(out);

    if (cb) cb("[F5 - CONCLUIDO COM SUCESSO] Aparelho desligado com sucesso.");
    return PIPELINE_SUCCESS;
}
