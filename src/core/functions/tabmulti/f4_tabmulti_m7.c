#include "f4_tabmulti_m7.h"
#include "m7_helpers.h"
#include "../../drivers/adb.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

int M7_DesligarAparelho(const char *serial, LogCallback cb) {
    if (cb) cb("\n=======================================================");
    if (cb) cb("  [M7] INICIANDO F4 - DESLIGAMENTO (TAB-MULTI)  ");
    if (cb) cb("=======================================================\n");

    if (cb) cb("[-] Desligando conexoes ativas...");
    M7_StopGnirehtet(serial, cb);

    char args[512];
    char *out = NULL;

    if (cb) cb("[-] Desativando Wi-Fi, Bluetooth e Localizacao...");
    snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell svc bluetooth disable", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell settings put secure location_mode 0", serial);
    out = adb_exec(args);
    if (out) free(out);

    if (cb) cb("[-] Enviando comando de Shutdown físico...");
    snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
    out = adb_exec(args);
    if (out) free(out);

    if (cb) cb("[M7] Aparelho Tab-Multi M7 desligado com sucesso.");
    return PIPELINE_SUCCESS;
}
