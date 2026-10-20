#include "adb.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static char g_adb_path[512] = {0};

static int file_exists(const char* path) {
    DWORD attr = GetFileAttributesA(path);
    return (attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY));
}

// Executa silenciosamente sem exibir janela preta (CREATE_NO_WINDOW)
char* adb_exec(const char* args) {
    char adb_path[512];
    adb_get_path(adb_path, sizeof(adb_path));
    if (adb_path[0] == '\0') return NULL;

    char cmd[8192];
    if (strcmp(adb_path, "adb") == 0)
        snprintf(cmd, sizeof(cmd), "adb %s", args);
    else
        snprintf(cmd, sizeof(cmd), "\"%s\" %s", adb_path, args);

    HANDLE hReadPipe, hWritePipe;
    SECURITY_ATTRIBUTES sa = { sizeof(SECURITY_ATTRIBUTES), NULL, TRUE };

    if (!CreatePipe(&hReadPipe, &hWritePipe, &sa, 0)) return NULL;
    SetHandleInformation(hReadPipe, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOW si;
    ZeroMemory(&si, sizeof(STARTUPINFOW));
    si.cb = sizeof(STARTUPINFOW);
    si.hStdOutput = hWritePipe;
    si.hStdError = hWritePipe;
    si.dwFlags |= STARTF_USESTDHANDLES;

    PROCESS_INFORMATION pi;
    ZeroMemory(&pi, sizeof(PROCESS_INFORMATION));

    WCHAR wcmd[8192];
    if (MultiByteToWideChar(CP_UTF8, 0, cmd, -1, wcmd, 8192) == 0) {
        MultiByteToWideChar(CP_ACP, 0, cmd, -1, wcmd, 8192);
    }

    if (!CreateProcessW(NULL, wcmd, NULL, NULL, TRUE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
        CloseHandle(hReadPipe);
        CloseHandle(hWritePipe);
        return NULL;
    }
    CloseHandle(hWritePipe);

    char* output = malloc(131072);
    if (!output) {
        CloseHandle(hReadPipe);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return NULL;
    }
    output[0] = '\0';
    int total_read = 0;

    DWORD bytesRead, dwAvail;
    char buffer[1024];
    while (1) {
        if (!PeekNamedPipe(hReadPipe, NULL, 0, NULL, &dwAvail, NULL)) break;
        if (dwAvail > 0) {
            if (ReadFile(hReadPipe, buffer, sizeof(buffer) - 1, &bytesRead, NULL) && bytesRead > 0) {
                if (total_read + bytesRead < 131071) {
                    memcpy(output + total_read, buffer, bytesRead);
                    total_read += bytesRead;
                    output[total_read] = '\0';
                }
            }
        } else {
            if (WaitForSingleObject(pi.hProcess, 50) == WAIT_OBJECT_0) {
                while (PeekNamedPipe(hReadPipe, NULL, 0, NULL, &dwAvail, NULL) && dwAvail > 0) {
                    if (ReadFile(hReadPipe, buffer, sizeof(buffer) - 1, &bytesRead, NULL) && bytesRead > 0) {
                        if (total_read + bytesRead < 131071) {
                            memcpy(output + total_read, buffer, bytesRead);
                            total_read += bytesRead;
                            output[total_read] = '\0';
                        }
                    }
                }
                break;
            }
        }
    }

    CloseHandle(hReadPipe);
    if (WaitForSingleObject(pi.hProcess, 3000) == WAIT_TIMEOUT) {
        TerminateProcess(pi.hProcess, 1);
    }
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

    return output;
}

char* adb_dump_stdout(const char* serial) {
    if (!serial) return NULL;
    char args[512];
    // Fast-Path 1: exec-out uiautomator dump /dev/stdout em memoria RAM
    snprintf(args, sizeof(args), "-s %s exec-out uiautomator dump /dev/stdout", serial);
    char* xml = adb_exec(args);
    if (xml && (strstr(xml, "<?xml") != NULL || strstr(xml, "<hierarchy") != NULL)) {
        return xml;
    }
    if (xml) free(xml);

    // Fast-Path 2: shell uiautomator dump /dev/stdout
    snprintf(args, sizeof(args), "-s %s shell \"uiautomator dump /dev/stdout 2>/dev/null\"", serial);
    xml = adb_exec(args);
    if (xml && (strstr(xml, "<?xml") != NULL || strstr(xml, "<hierarchy") != NULL)) {
        return xml;
    }
    if (xml) free(xml);

    return NULL;
}

int adb_get_current_focus(const char* serial, char* out_focus, int max_len) {
    if (!serial || !out_focus || max_len <= 0) return 0;
    out_focus[0] = '\0';
    char args[512];
    snprintf(args, sizeof(args), "-s %s shell \"dumpsys window | grep mCurrentFocus\"", serial);
    char* out = adb_exec(args);
    if (out) {
        strncpy(out_focus, out, max_len - 1);
        out_focus[max_len - 1] = '\0';
        free(out);
        return 1;
    }
    return 0;
}

char* adb_exec_retry(const char* args, int max_retries, int delay_ms, AdbLogCallback cb) {
    int attempts = 0;
    while (attempts < max_retries) {
        char* out = adb_exec(args);
        
        int has_error = 0;
        if (out == NULL) {
            has_error = 1;
        } else if (strstr(out, "error: device offline") || 
                   strstr(out, "error: device not found") || 
                   strstr(out, "error: no devices/emulators found") || 
                   strstr(out, "closed") ||
                   strstr(out, "device unauthorized")) {
            has_error = 1;
        }
        
        if (!has_error) {
            return out;
        }
        
        if (out) free(out);
        attempts++;
        if (cb && attempts < max_retries) {
            char msg[256];
            snprintf(msg, sizeof(msg), "[WARNING] Falha de conexao ADB. Tentativa %d/%d. Reconectando em %dms...", attempts, max_retries, delay_ms);
            cb(msg);
        }
        Sleep(delay_ms);
    }
    if (cb) cb("[ERROR] Comando ADB falhou repetidamente apos varias tentativas.");
    return NULL;
}

static int test_global_adb() {
    char* out = adb_exec("version");
    if (out) {
        int success = (strstr(out, "Android Debug Bridge") != NULL);
        free(out);
        return success;
    }
    return 0;
}

void adb_get_path(char* out_path, int max_len) {
    if (g_adb_path[0] != '\0') {
        strncpy(out_path, g_adb_path, max_len - 1);
        return;
    }

    // 1. Tenta adb na versÃ£o Release (plug & play)
    char exe_dir[MAX_PATH];
    WCHAR wexe_path[MAX_PATH];
    GetModuleFileNameW(NULL, wexe_path, MAX_PATH);
    WideCharToMultiByte(CP_UTF8, 0, wexe_path, -1, exe_dir, sizeof(exe_dir), NULL, NULL);
    
    char* p = strrchr(exe_dir, '\\');
    if (p) *p = '\0';
    
    char local_adb[MAX_PATH];
    snprintf(local_adb, sizeof(local_adb), "%s\\tools\\platform-tools\\adb.exe", exe_dir);
    
    if (!file_exists(local_adb)) {
        // Modo de Desenvolvimento (voltando 2 pastas a partir do data/output)
        snprintf(local_adb, sizeof(local_adb), "%s\\..\\..\\tools\\platform-tools\\adb.exe", exe_dir);
    }

    if (!file_exists(local_adb)) {
        // Fallback secundario para pasta tools/adb
        snprintf(local_adb, sizeof(local_adb), "%s\\tools\\adb\\adb.exe", exe_dir);
    }

    if (file_exists(local_adb)) {
        char full_adb[MAX_PATH];
        GetFullPathNameA(local_adb, MAX_PATH, full_adb, NULL);
        strncpy(g_adb_path, full_adb, sizeof(g_adb_path) - 1);
        strncpy(out_path, full_adb, max_len - 1);
        return;
    }

    // 2. Tenta adb global
    strncpy(g_adb_path, "adb", sizeof(g_adb_path) - 1);
    if (test_global_adb()) {
        strncpy(out_path, "adb", max_len - 1);
        return;
    }

    g_adb_path[0] = '\0';
    out_path[0] = '\0';
}

// ============================================================================
//  CACHE DE MAC POR SERIAL
// ============================================================================
//  O endereco MAC de um aparelho nao muda. Guardamos o resultado para que
//  o comando de refresh (que roda a cada 5s) nao precise repetir o
//  "dumpsys wifi" (~200ms por aparelho) em todos os ciclos.
// ============================================================================
typedef struct {
    char serial[MAX_SERIAL];
    char mac[MAX_MAC];
} MacCacheEntry;

static MacCacheEntry g_mac_cache[MAX_DEVICES];
static int g_mac_cache_count = 0;

static int mac_cache_get(const char* serial, char* out_mac, size_t out_size) {
    for (int i = 0; i < g_mac_cache_count; i++) {
        if (strcmp(g_mac_cache[i].serial, serial) == 0) {
            strncpy(out_mac, g_mac_cache[i].mac, out_size - 1);
            out_mac[out_size - 1] = '\0';
            return 1;
        }
    }
    return 0;
}

static void mac_cache_set(const char* serial, const char* mac) {
    if (!serial || !mac || !*mac) return;

    for (int i = 0; i < g_mac_cache_count; i++) {
        if (strcmp(g_mac_cache[i].serial, serial) == 0) {
            strncpy(g_mac_cache[i].mac, mac, MAX_MAC - 1);
            g_mac_cache[i].mac[MAX_MAC - 1] = '\0';
            return;
        }
    }

    if (g_mac_cache_count < MAX_DEVICES) {
        strncpy(g_mac_cache[g_mac_cache_count].serial, serial, MAX_SERIAL - 1);
        g_mac_cache[g_mac_cache_count].serial[MAX_SERIAL - 1] = '\0';
        strncpy(g_mac_cache[g_mac_cache_count].mac, mac, MAX_MAC - 1);
        g_mac_cache[g_mac_cache_count].mac[MAX_MAC - 1] = '\0';
        g_mac_cache_count++;
    }
}

// Verifica se um texto tem o formato de MAC (XX:XX:XX:XX:XX:XX)
static int is_valid_mac(const char* s) {
    if (!s) return 0;
    int digits = 0, colons = 0;
    for (const char* p = s; *p; p++) {
        if (*p == ':') { colons++; continue; }
        if ((*p >= '0' && *p <= '9') || (*p >= 'a' && *p <= 'f') || (*p >= 'A' && *p <= 'F')) { digits++; continue; }
        return 0;
    }
    return (digits == 12 && colons == 5);
}

// Normaliza para minusculas e remove espacos/newlines
static void normalize_mac(const char* in, char* out, size_t out_size) {
    size_t j = 0;
    for (const char* p = in; *p && j + 1 < out_size; p++) {
        if (*p == '\n' || *p == '\r' || *p == ' ' || *p == '\t') continue;
        out[j++] = (*p >= 'A' && *p <= 'F') ? (char)(*p + 32) : *p;
    }
    out[j] = '\0';
}

// Extrai o MAC do Wi-Fi a partir de um texto que contenha "link/ether XX:..."
static int extract_ether_from_text(const char* text, char* out_mac, size_t out_size) {
    if (!text) return 0;
    const char* p = strstr(text, "link/ether");
    if (!p) return 0;
    p += 10;
    while (*p == ' ' || *p == '\t') p++;
    char buf[64] = {0};
    size_t k = 0;
    while (*p && *p != ' ' && *p != '\n' && *p != '\r' && k + 1 < sizeof(buf)) buf[k++] = *p++;
    buf[k] = '\0';
    normalize_mac(buf, out_mac, out_size);
    return is_valid_mac(out_mac);
}

// Extrai o MAC do Wi-Fi a partir de um texto que contenha "MAC: XX:..."
static int extract_mac_label_from_text(const char* text, char* out_mac, size_t out_size) {
    if (!text) return 0;
    const char* p = strstr(text, "MAC: ");
    if (!p) return 0;
    p += 5;
    char buf[64] = {0};
    size_t k = 0;
    while (*p && *p != ',' && *p != ' ' && *p != '\n' && *p != '\r' && k + 1 < sizeof(buf)) buf[k++] = *p++;
    buf[k] = '\0';
    normalize_mac(buf, out_mac, out_size);
    return is_valid_mac(out_mac);
}

// ============================================================================
//  CAPTURA DO MAC WI-FI
// ============================================================================
//  O MAC so e legivel com a interface wlan0 ativa. Por isso a rotina
//  reproduz o mesmo ciclo da ferramenta externa de captura:
//    1) Tenta ler o MAC direto (cache do supplicant / wlan0 ja ativa).
//    2) Se nao conseguir, LIGA o Wi-Fi, espera a interface subir, extrai
//       e volta o Wi-Fi ao estado original (desligado).
//  Ordem de leitura: /sys/class/net/wlan0/address -> ip link show wlan0
//                      -> dumpsys wifi ("MAC: ...")
// ============================================================================

// Le o MAC a partir de um unico comando (sem alterar o estado do Wi-Fi)
static int adb_read_mac_once(const char* serial, char* out_mac, size_t out_size) {
    char args[512];
    char* out = NULL;
    int found = 0;

    // 1) /sys/class/net/wlan0/address
    snprintf(args, sizeof(args), "-s %s shell cat /sys/class/net/wlan0/address 2>/dev/null", serial);
    out = adb_exec(args);
    if (out) {
        char buf[64] = {0};
        size_t olen = strlen(out);
        while (olen > 0 && (out[olen - 1] == '\n' || out[olen - 1] == '\r' ||
                            out[olen - 1] == ' ' || out[olen - 1] == '\t')) {
            out[--olen] = '\0';
        }
        if (olen > 0 && olen < sizeof(buf)) {
            memcpy(buf, out, olen);
            buf[olen] = '\0';
            if (strstr(buf, "permission") == NULL && strstr(buf, "No such") == NULL) {
                normalize_mac(buf, out_mac, out_size);
                if (is_valid_mac(out_mac)) found = 1;
            }
        }
        free(out);
    }
    if (found) return 1;

    // 2) ip link show wlan0
    snprintf(args, sizeof(args), "-s %s shell ip link show wlan0 2>/dev/null", serial);
    out = adb_exec(args);
    if (out) {
        found = extract_ether_from_text(out, out_mac, out_size);
        free(out);
    }
    if (found) return 1;

    // 3) dumpsys wifi -> "MAC: xx:xx:xx:xx:xx:xx"
    snprintf(args, sizeof(args), "-s %s shell dumpsys wifi 2>/dev/null", serial);
    out = adb_exec(args);
    if (out) {
        found = extract_mac_label_from_text(out, out_mac, out_size);
        free(out);
    }
    return found;
}

// Le o estado do Wi-Fi: 1 ligado, 0 desligado, -1 desconhecido
static int adb_wifi_state(const char* serial) {
    char args[256];
    snprintf(args, sizeof(args), "-s %s shell settings get global wifi_on", serial);
    char* out = adb_exec(args);
    if (!out) return -1;
    int state = -1;
    char* nl = strpbrk(out, "\r\n");
    if (nl) *nl = '\0';
    if (strstr(out, "1")) state = 1;
    else if (strstr(out, "0")) state = 0;
    free(out);
    return state;
}

// Liga ou desliga o Wi-Fi
static void adb_wifi_set(const char* serial, int on) {
    char args[256];
    snprintf(args, sizeof(args), "-s %s shell svc wifi %s", serial, on ? "enable" : "disable");
    char* out = adb_exec(args);
    if (out) free(out);
}

int adb_get_mac(const char* serial, char* out_mac, int max_len) {
    if (!serial || !out_mac || max_len <= 0) return 0;
    out_mac[0] = '\0';

    // 1) Tentativa direta (sem mexer no Wi-Fi) - cobre o caso em que a
    //    interface ja esta ativa ou o supplicant ainda guarda o endereco.
    if (adb_read_mac_once(serial, out_mac, (size_t)max_len)) {
        mac_cache_set(serial, out_mac);
        return 1;
    }

    // 2) Ciclo completo da ferramenta de captura: liga o Wi-Fi (necessario
    //    para a interface wlan0 existir), extrai e restaura o estado original.
    int estado_anterior = adb_wifi_state(serial);
    if (estado_anterior == 1) {
        // Ja estava ligado: nao altera nada, apenas tenta novamente.
        Sleep(1500);
        if (adb_read_mac_once(serial, out_mac, (size_t)max_len)) {
            mac_cache_set(serial, out_mac);
            return 1;
        }
        return 0;
    }

    adb_wifi_set(serial, 1);

    // Aguarda a interface subir e o supplicant popular o endereco (max ~6s)
    int extraiu = 0;
    for (int i = 0; i < 6; i++) {
        Sleep(1000);
        if (adb_read_mac_once(serial, out_mac, (size_t)max_len)) { extraiu = 1; break; }
    }

    // Restaura o estado original do Wi-Fi. O pipeline F1/F2/F5 exige o
    // Wi-Fi DESLIGADO (o trafego passa pelo tunel Gnirehtet), portanto
    // deixar o Wi-Fi ligado quebraria a etapa de login.
    adb_wifi_set(serial, 0);

    if (extraiu) mac_cache_set(serial, out_mac);

    return extraiu;
}

int adb_list_devices(AdbDevice* devices, int max_devices) {
    char* out = adb_exec("devices");
    if (!out) return 0;

    int count = 0;
    char* outer_context = NULL;
    char* line = strtok_s(out, "\n", &outer_context);
    while (line && count < max_devices) {
        // Pula a linha de cabeÃ§alho
        if (strstr(line, "List of devices")) { line = strtok_s(NULL, "\n", &outer_context); continue; }
        // Remove \r
        char* cr = strchr(line, '\r');
        if (cr) *cr = '\0';
        if (strlen(line) < 3) { line = strtok_s(NULL, "\n", &outer_context); continue; }

        char serial[MAX_SERIAL] = {0}, status[32] = {0};
        if (sscanf(line, "%63s %31s", serial, status) == 2) {
            AdbDevice* d = &devices[count];
            memset(d, 0, sizeof(AdbDevice));
            strncpy(d->serial, serial, MAX_SERIAL - 1);
            d->authorized = (strcmp(status, "device") == 0) ? 1 : 0;
            if (d->authorized) {
                /* --- OTIMIZACAO: 1 unico processo ADB para coletar todos os dados --- */
                /* Antes: 6 processos sequenciais (~3-4s por dispositivo)             */
                /* Depois: 1 processo consolidado (~300ms por dispositivo)            */
                /* O bloco do MAC so entra no batched quando ainda nao temos o       */
                /* endereco em cache (o MAC nao muda: ~200ms economizados/refresh)  */
                char mac_cacheado[MAX_MAC] = {0};
                int mac_ja_conhecido = mac_cache_get(serial, mac_cacheado, sizeof(mac_cacheado));

                char args[1024];
                if (mac_ja_conhecido) {
                    snprintf(args, sizeof(args),
                        "-s %s shell \""
                        "echo MODEL:$(getprop ro.product.model);"
                        "echo MFR:$(getprop ro.product.manufacturer);"
                        "echo VER:$(getprop ro.build.version.release);"
                        "echo SDK:$(getprop ro.build.version.sdk);"
                        "echo BAT:$(dumpsys battery 2>/dev/null | grep ' level:' | tr -d ' ');"
                        "echo IMEI:$(service call iphonesubinfo 1 2>/dev/null | grep -oE '[0-9a-f]{8}' | tr -d '\\\\n' || echo N/A)\"",
                        serial);
                } else {
                    snprintf(args, sizeof(args),
                        "-s %s shell \""
                        "echo MODEL:$(getprop ro.product.model);"
                        "echo MFR:$(getprop ro.product.manufacturer);"
                        "echo VER:$(getprop ro.build.version.release);"
                        "echo SDK:$(getprop ro.build.version.sdk);"
                        "echo BAT:$(dumpsys battery 2>/dev/null | grep ' level:' | tr -d ' ');"
                        "echo IMEI:$(service call iphonesubinfo 1 2>/dev/null | grep -oE '[0-9a-f]{8}' | tr -d '\\\\n' || echo N/A);"
                        "echo MAC:$(dumpsys wifi 2>/dev/null | grep -m1 -oE 'MAC: [0-9a-fA-F]{2}(:[0-9a-fA-F]{2}){5}' | cut -d' ' -f2 | tr -d '\\\\n')\"",
                        serial);
                }

                char* bulk = adb_exec(args);
                /* Defaults */
                strcpy(d->model, "Desconhecido");
                strcpy(d->manufacturer, "---");
                strcpy(d->android_version, "---");
                strcpy(d->api_level, "---");
                strcpy(d->battery, "---");
                strcpy(d->mac, "N/A");
                strcpy(d->imei, "N/A");

                /* MAC vindo do cache (ja conhecido em ciclo anterior) */
                if (mac_ja_conhecido) {
                    strncpy(d->mac, mac_cacheado, MAX_MAC - 1);
                    d->mac[MAX_MAC - 1] = '\0';
                }

                if (bulk) {
                    /* Parseia linha a linha */
                    char* inner_context = NULL;
                    char* inner_line = strtok_s(bulk, "\n", &inner_context);
                    while (inner_line) {
                        char* nl2 = strpbrk(inner_line, "\r");
                        if (nl2) *nl2 = '\0';

                        if (strncmp(inner_line, "MODEL:", 6) == 0 && strlen(inner_line) > 6)
                            strncpy(d->model, inner_line + 6, MAX_MODEL - 1);
                        else if (strncmp(inner_line, "MFR:", 4) == 0 && strlen(inner_line) > 4)
                            strncpy(d->manufacturer, inner_line + 4, 31);
                        else if (strncmp(inner_line, "VER:", 4) == 0 && strlen(inner_line) > 4)
                            strncpy(d->android_version, inner_line + 4, 15);
                        else if (strncmp(inner_line, "SDK:", 4) == 0 && strlen(inner_line) > 4)
                            strncpy(d->api_level, inner_line + 4, 15);
                        else if (strncmp(inner_line, "BAT:", 4) == 0 && strlen(inner_line) > 4) {
                            /* Formato: "level:X" â€” extrai sÃ³ o numero */
                            char* colon = strchr(inner_line + 4, ':');
                            if (colon && strlen(colon) > 1) {
                                snprintf(d->battery, 15, "%s%%", colon + 1);
                            }
                        }
                        else if (strncmp(inner_line, "IMEI:", 5) == 0 && strlen(inner_line) > 5) {
                            strncpy(d->imei, inner_line + 5, MAX_IMEI - 1);
                            if (strstr(d->imei, "ffffffff") || strstr(d->imei, "ermission"))
                                strcpy(d->imei, "N/A (Seguranca)");
                        }
                        else if (strncmp(inner_line, "MAC:", 4) == 0 && strlen(inner_line) > 4) {
                            char mac_buf[64] = {0};
                            normalize_mac(inner_line + 4, mac_buf, sizeof(mac_buf));
                            if (is_valid_mac(mac_buf)) {
                                strncpy(d->mac, mac_buf, MAX_MAC - 1);
                                d->mac[MAX_MAC - 1] = '\0';
                                /* Guarda em cache: evita repetir o dumpsys wifi nos proximos ciclos */
                                mac_cache_set(serial, d->mac);
                            }
                        }
                        inner_line = strtok_s(NULL, "\n", &inner_context);
                    }
                    free(bulk);
                }
            }
            count++;
        }
        line = strtok_s(NULL, "\n", &outer_context);
    }
    free(out);
    return count;
}

void adb_get_model(const char* serial, char* out_model, int max_len) {
    char args[256];
    snprintf(args, sizeof(args), "-s %s shell getprop ro.product.model", serial);
    char* out = adb_exec(args);
    if (!out) { strncpy(out_model, "Desconhecido", max_len - 1); return; }
    // Remove espaÃ§os e newlines
    char* nl = strpbrk(out, "\r\n");
    if (nl) *nl = '\0';
    strncpy(out_model, strlen(out) > 0 ? out : "Desconhecido", max_len - 1);
    free(out);
}

void adb_get_imei(const char* serial, char* out_imei, int max_len) {
    char args[256];
    // Envolvendo o comando inteiro em aspas para o shell do android interpretar os pipes
    snprintf(args, sizeof(args),
        "-s %s shell \"service call iphonesubinfo 1 | grep -oE '[0-9a-f]{8}' | tr -d '\\n'\"",
        serial);
    char* out = adb_exec(args);
    if (!out || strlen(out) < 3) {
        strncpy(out_imei, "N/A", max_len - 1);
        if (out) free(out);
        return;
    }
    char* nl = strpbrk(out, "\r\n");
    if (nl) *nl = '\0';
    
    // Filtro de sujeira do Android 10+
    if (strstr(out, "ffffffff") || strstr(out, "Permission") || strstr(out, "denied") || strstr(out, "Parcel")) {
        strncpy(out_imei, "N/A (Seguranca)", max_len - 1);
    } else {
        strncpy(out_imei, out, max_len - 1);
    }
    free(out);
}



int adb_get_packages(const char* serial, AdbPackage* packages, int max_pkgs) {
    char args[256];
    // Lista pacotes de terceiros (-3). Para todos, seria sÃ³ list packages.
    snprintf(args, sizeof(args), "-s %s shell pm list packages -3", serial);
    char* out = adb_exec(args);
    if (!out) return 0;

    int count = 0;
    char* line = strtok(out, "\n");
    while (line && count < max_pkgs) {
        char* cr = strchr(line, '\r');
        if (cr) *cr = '\0';
        
        // A linha vem como "package:com.exemplo.app"
        if (strncmp(line, "package:", 8) == 0) {
            strncpy(packages[count].name, line + 8, MAX_PKG_NAME - 1);
            count++;
        }
        line = strtok(NULL, "\n");
    }
    free(out);
    return count;
}

int adb_uninstall_app(const char* serial, const char* package_name) {
    char args[512];
    snprintf(args, sizeof(args), "-s %s uninstall %s", serial, package_name);
    char* out = adb_exec(args);
    int success = out && strstr(out, "Success");
    if (out) free(out);
    return success;
}

int adb_clear_app_data(const char* serial, const char* package_name) {
    char args[512];
    snprintf(args, sizeof(args), "-s %s shell pm clear %s", serial, package_name);
    char* out = adb_exec(args);
    int success = out && strstr(out, "Success");
    if (out) free(out);
    return success;
}

int adb_force_stop(const char* serial, const char* package_name) {
    char args[512];
    snprintf(args, sizeof(args), "-s %s shell am force-stop %s", serial, package_name);
    char* out = adb_exec(args);
    // am force-stop nÃ£o retorna nada se der certo
    int success = (out != NULL);
    if (out) free(out);
    return success;
}

// Helper para pegar o diretorio base do executavel
static void adb_internal_get_exe_dir(char* out_dir, int max_len) {
    GetModuleFileNameA(NULL, out_dir, max_len);
    char* last = strrchr(out_dir, '\\');
    if (last) *last = '\0';
}

// Converte caminho UTF-8 para Short Path (8.3) ANSI ou copia para diretorio safe ASCII
int adb_get_safe_path(const char* utf8_path, char* out_safe_path, int max_len) {
    if (!utf8_path || strlen(utf8_path) == 0) {
        if (max_len > 0) out_safe_path[0] = '\0';
        return 0;
    }
    
    // 1. Converter UTF-8 para UTF-16
    WCHAR wPath[MAX_PATH];
    if (MultiByteToWideChar(CP_UTF8, 0, utf8_path, -1, wPath, MAX_PATH) == 0) {
        // Fallback para ANSI
        MultiByteToWideChar(CP_ACP, 0, utf8_path, -1, wPath, MAX_PATH);
    }
    
    // Tenta validar existencia via API Unicode do Windows
    DWORD attr = GetFileAttributesW(wPath);
    if (attr == INVALID_FILE_ATTRIBUTES) {
        // Se nao encontrou via CP_UTF8, tenta CP_ACP
        MultiByteToWideChar(CP_ACP, 0, utf8_path, -1, wPath, MAX_PATH);
        attr = GetFileAttributesW(wPath);
    }
    
    // 2. Tentar obter Short Path (8.3)
    WCHAR wShort[MAX_PATH];
    DWORD res = GetShortPathNameW(wPath, wShort, MAX_PATH);
    if (res > 0 && res < MAX_PATH) {
        // Verifica se o resultado e 100% ASCII (nenhum char > 127)
        int is_pure_ascii = 1;
        for (DWORD i = 0; i < res; i++) {
            if (wShort[i] > 127) {
                is_pure_ascii = 0;
                break;
            }
        }
        if (is_pure_ascii) {
            WideCharToMultiByte(CP_UTF8, 0, wShort, -1, out_safe_path, max_len, NULL, NULL);
            return 1;
        }
    }
    
    // 3. Fallback Supremo: Se o Short Path falhar (ex: 8.3 desativado na particao NTFS)
    // ou ainda contiver acentos, copia o arquivo via CopyFileW (Unicode puro) para uma pasta
    // de cache 100% ASCII (data/temp_adb/).
    if (attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY)) {
        char base_dir[MAX_PATH];
        adb_internal_get_exe_dir(base_dir, sizeof(base_dir));
        
        char safe_dir[MAX_PATH];
        snprintf(safe_dir, sizeof(safe_dir), "%s\\data\\temp_adb", base_dir);
        
        WCHAR wSafeDir[MAX_PATH];
        MultiByteToWideChar(CP_UTF8, 0, safe_dir, -1, wSafeDir, MAX_PATH);
        CreateDirectoryW(wSafeDir, NULL);
        
        const char* ext = strrchr(utf8_path, '.');
        if (!ext) ext = ".bin";
        
        // Hash unico para o arquivo baseado no caminho original
        unsigned int h = 5381;
        for (const char* p = utf8_path; *p; p++) {
            h = ((h << 5) + h) + (unsigned char)(*p);
        }
        
        char temp_filename[MAX_PATH];
        snprintf(temp_filename, sizeof(temp_filename), "%s\\inno_safe_%u%s", safe_dir, h, ext);
        
        WCHAR wTempFile[MAX_PATH];
        MultiByteToWideChar(CP_UTF8, 0, temp_filename, -1, wTempFile, MAX_PATH);
        
        if (CopyFileW(wPath, wTempFile, FALSE)) {
            strncpy(out_safe_path, temp_filename, max_len - 1);
            out_safe_path[max_len - 1] = '\0';
            return 1;
        }
    }
    
    // Se tudo mais falhar, retorna o caminho original
    strncpy(out_safe_path, utf8_path, max_len - 1);
    out_safe_path[max_len - 1] = '\0';
    return 0;
}

// ============================================================
// Normaliza caractere ignorando acentos e maiúsculas/minúsculas
// Suporta UTF-8, ASCII e codificações CP1252/CP850 do Windows.
// ============================================================
char inno_normalize_char(unsigned char c, unsigned char next_c, int *skip_next) {
    *skip_next = 0;
    if (c >= 'A' && c <= 'Z') return c + 32;

    if (c == 0xC3 && next_c != 0) {
        *skip_next = 1;
        if ((next_c >= 0x80 && next_c <= 0x85) || (next_c >= 0xA0 && next_c <= 0xA5)) return 'a';
        if ((next_c >= 0x88 && next_c <= 0x8B) || (next_c >= 0xA8 && next_c <= 0xAB)) return 'e';
        if ((next_c >= 0x8C && next_c <= 0x8F) || (next_c >= 0xAC && next_c <= 0xAF)) return 'i';
        if ((next_c >= 0x92 && next_c <= 0x96) || (next_c >= 0xB2 && next_c <= 0xB6)) return 'o';
        if ((next_c >= 0x99 && next_c <= 0x9C) || (next_c >= 0xB9 && next_c <= 0xBC)) return 'u';
        if (next_c == 0x87 || next_c == 0xA7) return 'c';
        if (next_c == 0x91 || next_c == 0xB1) return 'n';
    }

    if (c >= 0xC0 && c <= 0xC5) return 'a';
    if (c >= 0xE0 && c <= 0xE5) return 'a';
    if (c >= 0xC8 && c <= 0xCB) return 'e';
    if (c >= 0xE8 && c <= 0xEB) return 'e';
    if (c >= 0xCC && c <= 0xCF) return 'i';
    if (c >= 0xEC && c <= 0xEF) return 'i';
    if (c >= 0xD2 && c <= 0xD6) return 'o';
    if (c >= 0xF2 && c <= 0xF6) return 'o';
    if (c >= 0xD9 && c <= 0xDC) return 'u';
    if (c >= 0xF9 && c <= 0xFC) return 'u';
    if (c == 0xC7 || c == 0xE7) return 'c';
    if (c == 0xD1 || c == 0xF1) return 'n';

    return c;
}

// ============================================================
// Busca de substring imune a acentos, maiúsculas e encodings
// ============================================================
const char* inno_stristr_fuzzy(const char *haystack, const char *needle) {
    if (!haystack || !needle) return NULL;
    if (*needle == '\0') return haystack;

    for (const char *h = haystack; *h != '\0'; h++) {
        const char *h_ptr = h;
        const char *n_ptr = needle;
        int match = 1;

        while (*n_ptr != '\0') {
            if (*h_ptr == '\0') {
                match = 0;
                break;
            }

            int h_skip = 0, n_skip = 0;
            char hc = inno_normalize_char((unsigned char)*h_ptr, (unsigned char)*(h_ptr + 1), &h_skip);
            char nc = inno_normalize_char((unsigned char)*n_ptr, (unsigned char)*(n_ptr + 1), &n_skip);

            if (hc != nc) {
                match = 0;
                break;
            }

            h_ptr += 1 + h_skip;
            n_ptr += 1 + n_skip;
        }

        if (match) return h;
    }
    return NULL;
}

// ============================================================
// Extrator universal de centroide XML imune a acentos e maiúsculas
// ============================================================
int adb_get_centroid_fuzzy(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count) {
    if (!xml || !search_str || !out_cx || !out_cy) return 0;
    int current_skip = 0;
    const char *p = xml;

    while ((p = inno_stristr_fuzzy(p, search_str)) != NULL) {
        if (current_skip < skip_count) {
            current_skip++;
            p++;
            continue;
        }

        const char *b = strstr(p, "bounds=\"[");
        if (!b) {
            const char *start_node = p;
            while (start_node > xml && *start_node != '<') start_node--;
            b = strstr(start_node, "bounds=\"[");
        }

        if (b) {
            int x1, y1, x2, y2;
            if (sscanf(b, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
                *out_cx = (x1 + x2) / 2;
                *out_cy = (y1 + y2) / 2;
                return 1;
            }
        }
        p++;
    }
    return 0;
}

// ============================================================
// Escapador universal de texto para o adb shell input text
// ============================================================
void adb_escape_text(const char *src, char *dest, size_t dest_size) {
    if (!src || !dest || dest_size == 0) return;
    size_t written = 0;

    while (*src && written + 3 < dest_size) {
        unsigned char c = (unsigned char)*src;
        if (c == ' ') {
            dest[written++] = '%';
            dest[written++] = 's';
        } else if (strchr("&|;*\\\"'()$<>`", c)) {
            dest[written++] = '\\';
            dest[written++] = c;
        } else {
            dest[written++] = c;
        }
        src++;
    }
    dest[written] = '\0';
}



// ============================================================
// Watchdog Anti-Crash Android — Garante que o app alvo esteja rodando
// Se o Android matar o app por falta de RAM, esta função detecta e relança.
// ============================================================
int adb_ensure_app_running(const char* serial, const char* package_name, AdbLogCallback cb) {
    if (!serial || !package_name) return 0;
    const char *pkg = (package_name && strlen(package_name) > 0) ? package_name : "br.com.yantec.innovava";

    char args[512];
    snprintf(args, sizeof(args), "-s %s shell pidof %s", serial, pkg);
    char *out = adb_exec(args);

    int is_running = 0;
    if (out && strlen(out) > 0 && strspn(out, " \r\n\t") != strlen(out)) {
        is_running = 1;
    }
    if (out) free(out);

    if (!is_running) {
        if (cb) {
            char msg[256];
            snprintf(msg, sizeof(msg), "[Watchdog Anti-Crash] App '%s' fechou no tablet. Relancando automaticamente...", pkg);
            cb(msg);
        }
        snprintf(args, sizeof(args), "-s %s shell monkey -p %s -c android.intent.category.LAUNCHER 1", serial, pkg);
        out = adb_exec(args); if (out) free(out);
        Sleep(2500);
        return 1;
    }
    return 0;
}



