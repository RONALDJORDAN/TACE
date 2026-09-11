#include "f8_vision_tab7.h"
#include "f1_vision_tab7.h"
#include "../../drivers/adb.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)

// =============================================================================
//  MÓDULO F8: CONEXÃO ATIVA CONTINUADA GNIREHTET (VISION TAB 7)
// =============================================================================
//  O que esta função faz:
//  - Inicializa o túnel reverso Gnirehtet e ativa a conectividade de internet
//    completa via USB no dispositivo Positivo Tab Vision 7.
// =============================================================================
int V7_ActiveConnection(const char* serial, LogCallback cb) {
    if (!serial || strlen(serial) == 0) return PIPELINE_ERROR;
    if (cb) cb("[F8 Vision7] Solicitando inicializacao de Conexao Ativa (Gnirehtet)...");

    int res = V7_Etapa2_5(serial, cb);
    if (res == PIPELINE_SUCCESS) {
        if (cb) cb("[F8 Vision7] [+] Conexao ativa Gnirehtet estabelecida com sucesso.");
        return PIPELINE_SUCCESS;
    } else {
        if (cb) cb("[F8 Vision7] [!] Falha ao estabelecer conexao ativa Gnirehtet.");
        return PIPELINE_ERROR;
    }
}
