#include "f3_tabmulti_m7.h"
#include "f2_tabmulti_m7.h"
#include "m7_helpers.h"
#include "../../drivers/adb.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

int M7_ExecutarPipelineF3_AtualizarPacote(const char *serial, const char *apk_path, const char *pacote_path, const char *package_name, LogCallback cb) {
    return M7_AtualizarPacote(serial, apk_path, pacote_path, cb);
}

int M7_Terminal(const char *serial, LogCallback cb) {
    if (cb) cb("\n=======================================================");
    if (cb) cb("  [M7] INICIANDO F3 - TERMINAL (GESTAO M7)  ");
    if (cb) cb("=======================================================\n");

    char args[512];
    char *out = NULL;

    if (cb) cb("[-] Acordando tela...");
    M7_UnlockScreen(serial, cb);

    if (cb) cb("[-] Limpando cache do InnovAva...");
    snprintf(args, sizeof(args), "-s %s shell pm clear br.com.yantec.innovava", serial);
    out = adb_exec(args);
    if (out) free(out);
    
    if (cb) cb("[-] Desligando Gnirehtet (se rodando)...");
    M7_StopGnirehtet(serial, cb);
    
    if (cb) cb("[-] Terminal limpo F3 executado com sucesso.");
    return PIPELINE_SUCCESS;
}
