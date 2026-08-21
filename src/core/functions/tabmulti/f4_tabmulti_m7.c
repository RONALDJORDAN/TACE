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
    if (cb) cb("  [F4 - TAB-MULTI M7] INICIANDO DESLIGAMENTO E PURGA DE REDES  ");
    if (cb) cb("=======================================================\n");

    char args[512];
    char *out = NULL;

    // 1. Parar o Túnel de Internet USB (Gnirehtet)
    if (cb) cb("[-] [1/5] Encerrando e desativando túnel de internet USB (Gnirehtet)...");
    M7_StopGnirehtet(serial, cb);

    snprintf(args, sizeof(args), "-s %s shell am force-stop com.genymobile.gnirehtet", serial);
    out = adb_exec(args);
    if (out) free(out);

    // 2. Desativar Wi-Fi e varreduras de fundo
    if (cb) cb("[-] [2/5] Desativando antenas e varreduras Wi-Fi...");
    snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell settings put global wifi_on 0", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell settings put global wifi_scan_always_enabled 0", serial);
    out = adb_exec(args);
    if (out) free(out);

    // 3. Desativar Bluetooth e varreduras BLE (Múltiplas camadas de fallback)
    if (cb) cb("[-] [3/5] Desativando antenas e varreduras Bluetooth...");
    snprintf(args, sizeof(args), "-s %s shell svc bluetooth disable", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell cmd bluetooth disable", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell service call bluetooth_manager 8", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell settings put global bluetooth_on 0", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell settings put global ble_scan_always_enabled 0", serial);
    out = adb_exec(args);
    if (out) free(out);

    // 4. Desativar serviços de Localização (GPS) e encerrar apps em execução
    if (cb) cb("[-] [4/5] Desativando Localização (GPS) e encerrando aplicativos...");
    snprintf(args, sizeof(args), "-s %s shell settings put secure location_mode 0", serial);
    out = adb_exec(args);
    if (out) free(out);

    snprintf(args, sizeof(args), "-s %s shell am force-stop br.com.yantec.innovava", serial);
    out = adb_exec(args);
    if (out) free(out);

    // 5. Emitir o comando de desligamento físico
    if (cb) cb("[-] [5/5] Emitindo comando de desligamento físico (reboot -p)...");
    snprintf(args, sizeof(args), "-s %s shell reboot -p", serial);
    out = adb_exec(args);
    if (out) free(out);

    if (cb) cb("[+] [F4 - TabMulti M7] Aparelho desligado, antenas de rede e túnel desativados com sucesso!");
    return PIPELINE_SUCCESS;
}
