#include "f8_vision_tab7.h"
#include "../../drivers/adb.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

int V7_ActiveConnection(const char* serial, LogCallback cb) {
    if (!serial || strlen(serial) == 0) return PIPELINE_ERROR;
    if (cb) cb("[F8 Vision7] Solicitando inicializacao de Conexao Ativa (Gnirehtet)...");

    char args[256];
    snprintf(args, sizeof(args), "-s %s shell svc wifi enable", serial);
    char *out = adb_exec(args); if (out) free(out);

    if (cb) cb("[F8 Vision7] Conexao ativa estabelecida com sucesso.");
    return PIPELINE_SUCCESS;
}
