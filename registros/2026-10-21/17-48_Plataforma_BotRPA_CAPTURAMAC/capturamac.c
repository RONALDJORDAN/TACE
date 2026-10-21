/* CAPTURADOR DE MAC WI-FI - tablets Android via adb (Bot RPA)
 *  - Sobe o servidor adb automaticamente se estiver desligado.
 *  - Executa o adb com CreateProcess RETO (sem cmd/_popen), igual ao motor
 *    nativo: o "2>/dev/null" e passado ao shell DO APARELHO, nao ao Windows.
 *  - Resolve caminhos (adb/CSV/log) a partir da pasta do EXE (independe cwd).
 *  - Captura: leitura direta; se falhar, liga o Wi-Fi, poll de 500 ms ate 10 s,
 *    desliga ao final SOMENTE se fomos nos que ligamos.
 *  - So grava no CSV se o MAC for valido (XX:XX:XX:XX:XX:XX).
 *  CLI: --once  captura todos os conectados e sai (sem menu)
 *       --test  mostra caminhos/estado e sai
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <ctype.h>
#include <windows.h>

#define BUF           16384
#define MAX_DEV       64
#define MAC_RETRIES   20          /* 20 x 500 ms = 10 s */
#define SCAN_INTERVAL 5000
#define ADB_TIMEOUT   60000       /* ms por comando */

#define COR_RESET    "\033[0m"
#define COR_NEGRITO  "\033[1m"
#define COR_CIANO    "\033[36m"
#define COR_VERDE    "\033[32m"
#define COR_AMARELO  "\033[33m"
#define COR_AZUL     "\033[34m"
#define COR_MAGENTA  "\033[35m"
#define COR_VERMELHO "\033[31m"

typedef struct {
    char serial[64];
    char mac[32];
    char modelo[64];
    char data[40];
} Device;

static char g_adb[MAX_PATH] = {0};   /* caminho absoluto do adb.exe */
static char g_csv[MAX_PATH] = {0};   /* capturamac_resultado.csv absoluto */
static char g_log[MAX_PATH] = {0};   /* log.txt absoluto */

static int  adb_run(const char *args, char *out, size_t outsz);
static void trimsz(char *s);
static void resolver_caminhos(void);
static void start_adb(void);
static int  list_devices(Device devs[], int max);
static int  valid_mac(const char *mac);
static int  read_mac_once(const char *serial, char *mac_out, size_t macsz);
static int  wifi_state(const char *serial);
static void wifi_set(const char *serial, int on);
static void capturar_dispositivo(const char *serial);
static int  save_csv(const char *serial, const char *modelo, const char *mac);
static int  load_csv(Device devs[], int max);
static void log_linha(const char *fmt, ...);
static void habilitar_cores(void);
static void print_line(int len, const char *cor);
static void show_header(void);
static void show_table(void);
static void capturar_todos(void);
static void modo_monitor(void);
static void data_now(char *out, size_t sz);

/* ================================================================
 *  EXECUCAO DO ADB - CreateProcess direto, stdout+stderr na memoria
 * ================================================================ */
static void habilitar_cores(void) {
    HANDLE h = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD m = 0;
    if (h != INVALID_HANDLE_VALUE && GetConsoleMode(h, &m)) {
        SetConsoleMode(h, m | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }
}

static void trimsz(char *s) {
    size_t n = strlen(s);
    while (n > 0 && (s[n-1] == '\n' || s[n-1] == '\r' || s[n-1] == ' ' ||
                     s[n-1] == '\t' || (unsigned char)s[n-1] <= ' ')) s[--n] = '\0';
}

static int adb_run(const char *args, char *out, size_t outsz) {
    SECURITY_ATTRIBUTES sa;
    STARTUPINFOA si;
    PROCESS_INFORMATION pi;
    HANDLE rd = NULL, wr = NULL;
    BYTE *buf = NULL;
    DWORD allocated = 0;
    int rc = -1;

    if (out) out[0] = '\0';

    sa.nLength = sizeof(sa);
    sa.bInheritHandle = TRUE;
    sa.lpSecurityDescriptor = NULL;

    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES | STARTF_FORCEOFFFEEDBACK;
    si.wShowWindow = SW_HIDE;

    ZeroMemory(&pi, sizeof(pi));

    if (!CreatePipe(&rd, &wr, &sa, 0)) goto limpar;
    if (!SetHandleInformation(rd, HANDLE_FLAG_INHERIT, 0)) goto limpar;

    si.hStdOutput = wr;
    si.hStdError  = wr;   /* junta stderr no mesmo pipe, sem janelas/lixo */

    {
        char cmdlin[2100];
        snprintf(cmdlin, sizeof(cmdlin), "\"%.240s\" %.1830s", g_adb, args);
        if (!CreateProcessA(NULL, cmdlin, NULL, NULL, TRUE,
                            CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
            goto limpar;
        }
    }

    /* fecha o extremo gravo no pai para o pipe terminar com EOF */
    CloseHandle(wr);
    wr = NULL;

    /* le ate EOF */
    {
        DWORD n;
        allocated = BUF * 2;
        buf = (BYTE *)malloc(allocated + 1);
        DWORD have = 0;
        while (buf && ReadFile(rd, buf + have, allocated - have, &n, NULL) && n > 0) {
            have += n;
            if (have >= allocated) {
                allocated *= 2;
                BYTE *nv = (BYTE *)realloc(buf, allocated + 1);
                if (!nv) break;
                buf = nv;
            }
        }
        if (buf) {
            buf[have] = '\0';
            if (out && outsz > 1) {
                size_t copia = have < outsz - 1 ? have : outsz - 1;
                memcpy(out, buf, copia);
                out[copia] = '\0';
            }
        }
    }

    WaitForSingleObject(pi.hProcess, ADB_TIMEOUT);
    {
        DWORD code = 1;
        if (GetExitCodeProcess(pi.hProcess, &code) && code == 0) rc = 0;
    }

    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);

limpar:
    if (buf) free(buf);
    if (wr) CloseHandle(wr);
    if (rd) CloseHandle(rd);
    return rc;
}

static void log_linha(const char *fmt, ...) {
    FILE *f = fopen(g_log, "a");
    if (!f) return;
    SYSTEMTIME st; GetLocalTime(&st);
    fprintf(f, "[%02d/%02d/%04d %02d:%02d:%02d] ", st.wDay, st.wMonth, st.wYear,
            st.wHour, st.wMinute, st.wSecond);
    va_list ap; va_start(ap, fmt);
    vfprintf(f, fmt, ap);
    va_end(ap);
    fputc('\n', f);
    fclose(f);
}

/* Caminhos ancorados na pasta do EXE: nunca mais falha por causa do cwd */
static void resolver_caminhos(void) {
    char exe[MAX_PATH] = {0};
    GetModuleFileNameA(NULL, exe, MAX_PATH);
    char *slash = strrchr(exe, '\\');
    if (slash) *(slash + 1) = '\0'; else strcpy(exe, ".\\");

    const char *cands[] = {
        "..\\platform-tools\\adb.exe",            /* exe em tools\capturamac */
        "..\\..\\tools\\platform-tools\\adb.exe", /* exe pendurado mais fundo */
        "platform-tools\\adb.exe",
        "..\\tools\\platform-tools\\adb.exe"
    };
    for (int i = 0; i < (int)(sizeof(cands)/sizeof(cands[0])); i++) {
        char full[MAX_PATH];
        char tal[MAX_PATH];
        snprintf(full, MAX_PATH, "%s%s", exe, cands[i]);
        GetFullPathNameA(full, MAX_PATH, tal, NULL);
        if (GetFileAttributesA(tal) != INVALID_FILE_ATTRIBUTES) {
            strcpy(g_adb, tal);
            break;
        }
    }
    snprintf(g_csv, MAX_PATH, "%scapturamac_resultado.csv", exe);
    snprintf(g_log, MAX_PATH, "%slog.txt", exe);
}

/* Sobe o servidor adb se estiver desligado (idempotente e rapido) */
static void start_adb(void) {
    if (!g_adb[0]) return;
    adb_run("start-server", NULL, 0);
}

static int list_devices(Device devs[], int max) {
    char out[BUF * 4] = {0};
    if (adb_run("devices -l", out, sizeof(out)) != 0) return -1;

    int n = 0;
    char *ctx = NULL;
    char *line = strtok_s(out, "\r\n", &ctx);
    while (line && n < max) {
        trimsz(line);
        if (strlen(line) == 0 || strstr(line, "List of devices") ||
            strstr(line, "* daemon") || strstr(line, "adb server") ||
            strstr(line, "killing")) {
            line = strtok_s(NULL, "\r\n", &ctx);
            continue;
        }
        /* separador: tab OU espacos (adb devices / devices -l variam) */
        char *sep = line + strcspn(line, "\t ");
        if (*sep == '\0') {
            line = strtok_s(NULL, "\r\n", &ctx);
            continue;
        }
        *sep = '\0';
        char *rest = sep + 1;
        while (*rest == '\t' || *rest == ' ') rest++;
        char *state = rest;
        while (*state == '\t' || *state == ' ') state++;
        if (strlen(line) > 0 && strstr(state, "device") != NULL &&
            strstr(state, "offline") == NULL && strstr(state, "unauthorized") == NULL) {
            Device *d = &devs[n];
            snprintf(d->serial, sizeof(d->serial), "%.63s", line);
            d->mac[0] = '\0';
            d->modelo[0] = '\0';
            d->data[0] = '\0';
            n++;
        }
        line = strtok_s(NULL, "\r\n", &ctx);
    }
    return n;
}

static int valid_mac(const char *mac) {
    if (strlen(mac) != 17) return 0;
    for (int i = 0; i < 17; i++) {
        char c = mac[i];
        if (i % 3 == 2) { if (c != ':') return 0; }
        else if (!isxdigit((unsigned char)c)) return 0;
    }
    return 1;
}

/* Extrai o primeiro MAC valido de um texto; 1 se achou */
static int mac_de_texto(const char *txt, char *mac_out, size_t macsz) {
    const char *p = txt;
    while ((p = strstr(p, ":")) != NULL) {
        /* volta a inicio do token */
        const char *ini = p;
        while (ini > txt && (isxdigit((unsigned char)ini[-1]) || ini[-1] == ':' || ini[-1] == '-')) ini--;
        char tok[32] = {0};
        size_t k = 0;
        while (k < 17 && ini[k] && (isxdigit((unsigned char)ini[k]) || ini[k] == ':') ) {
            tok[k] = ini[k];
            k++;
        }
        tok[k] = '\0';
        /* normaliza e valida token de 17 */
        char low[32];
        snprintf(low, sizeof(low), "%.17s", tok);
        for (char *q = low; *q; q++) *q = (char)tolower((unsigned char)*q);
        if (low[2] == ':' && valid_mac(low)) {
            snprintf(mac_out, macsz, "%s", low);
            return 1;
        }
        p = ini + 1;
    }
    return 0;
}

/* Leitura SEM alterar o estado do Wi-Fi (ordem: sysfs -> ip link -> dumpsys) */
static int read_mac_once(const char *serial, char *mac_out, size_t macsz) {
    char args[512];
    char out[BUF] = {0};
    mac_out[0] = '\0';

    /* 1) /sys/class/net/wlan0/address (interpretado pelo shell do aparelho) */
    snprintf(args, sizeof(args), "-s %.63s shell cat /sys/class/net/wlan0/address", serial);
    if (adb_run(args, out, sizeof(out)) == 0) {
        trimsz(out);
        if (out[0] && strstr(out, "permission") == NULL && strstr(out, "No such") == NULL &&
            mac_de_texto(out, mac_out, macsz)) return 1;
    }

    /* 2) ip link show wlan0 */
    snprintf(args, sizeof(args), "-s %.63s shell ip link show wlan0", serial);
    if (adb_run(args, out, sizeof(out)) == 0) {
        char *ptr = strstr(out, "link/ether");
        if (ptr) {
            ptr += 10;
            while (*ptr == ' ') ptr++;
            char tok[32] = {0};
            size_t k = 0;
            while (*ptr && *ptr != ' ' && *ptr != '\n' && *ptr != '\r' && k < sizeof(tok) - 1)
                tok[k++] = *ptr++;
            char low[32];
            snprintf(low, sizeof(low), "%.31s", tok);
            for (char *q = low; *q; q++) *q = (char)tolower((unsigned char)*q);
            if (valid_mac(low)) {
                snprintf(mac_out, macsz, "%s", low);
                return 1;
            }
        }
    }

    /* 3) dumpsys wifi ("MAC: ...") */
    snprintf(args, sizeof(args), "-s %.63s shell dumpsys wifi", serial);
    if (adb_run(args, out, sizeof(out)) == 0) {
        char *ptr = strstr(out, "MAC: ");
        if (ptr) {
            ptr += 5;
            char tok[32] = {0};
            size_t k = 0;
            while (*ptr && *ptr != ',' && *ptr != ' ' && *ptr != '\n' && *ptr != '\r' && k < sizeof(tok) - 1)
                tok[k++] = *ptr++;
            char low[32];
            snprintf(low, sizeof(low), "%.31s", tok);
            for (char *q = low; *q; q++) *q = (char)tolower((unsigned char)*q);
            if (valid_mac(low)) {
                snprintf(mac_out, macsz, "%s", low);
                return 1;
            }
        }
    }
    return 0;
}

static int wifi_state(const char *serial) {
    char args[512];
    char out[BUF] = {0};
    snprintf(args, sizeof(args), "-s %.63s shell settings get global wifi_on", serial);
    if (adb_run(args, out, sizeof(out)) != 0) return -1;
    trimsz(out);
    if (strcmp(out, "1") == 0) return 1;
    if (strcmp(out, "0") == 0) return 0;
    return -1;
}

static void wifi_set(const char *serial, int on) {
    char args[512];
    snprintf(args, sizeof(args), "-s %.63s shell svc wifi %s", serial, on ? "enable" : "disable");
    adb_run(args, NULL, 0);
}

static void get_model(const char *serial, char *modelo, size_t sz) {
    char args[512];
    char out[BUF] = {0};
    snprintf(modelo, sz, "desconhecido");
    snprintf(args, sizeof(args), "-s %.63s shell getprop ro.product.model", serial);
    if (adb_run(args, out, sizeof(out)) == 0) {
        trimsz(out);
        if (out[0] && strlen(out) < 60) snprintf(modelo, sz, "%.63s", out);
    }
}

/* Leitura barata (sysfs apenas) para o poll apos ligar o Wi-Fi */
static int leitura_rapida(const char *serial, char *mac, size_t macsz) {
    char args[512];
    char out[BUF] = {0};
    snprintf(args, sizeof(args), "-s %.63s shell cat /sys/class/net/wlan0/address", serial);
    if (adb_run(args, out, sizeof(out)) == 0 && out[0] &&
        strstr(out, "permission") == NULL && strstr(out, "No such") == NULL &&
        mac_de_texto(out, mac, macsz)) return 1;
    return 0;
}

/* Captura de UM aparelho: restaura o estado original do Wi-Fi */
static void capturar_dispositivo(const char *serial) {
    char mac[32] = {0};
    char modelo[64] = {0};

    int st = wifi_state(serial);
    if (st >= 0) {
        printf("       %sWi-Fi: %s%s\n", COR_AZUL, st ? "ligado" : "desligado", COR_RESET);
    }
    int desligamos = (st != 1);
    if (st == 0) {
        printf("       %sWi-Fi desligado, ligando...%s\n", COR_AMARELO, COR_RESET);
        wifi_set(serial, 1);
        Sleep(2500);   /* tempo para a interface wlan0 subir (sem bombardeio) */
    }

    int ok = read_mac_once(serial, mac, sizeof(mac));

    for (int t = 0; !ok && t < 10 && desligamos; t++) {
        Sleep(700);
        ok = leitura_rapida(serial, mac, sizeof(mac));
    }

    if (!ok) ok = read_mac_once(serial, mac, sizeof(mac));

    get_model(serial, modelo, sizeof(modelo));

    printf("       %sSerial:%s      %s\n", COR_AZUL, COR_RESET, serial);
    printf("       %sModelo:%s     %s\n", COR_AZUL, COR_RESET, modelo);
    if (ok) {
        printf("       %sMAC Wi-Fi:%s  %s%s%s\n", COR_AZUL, COR_RESET, COR_VERDE, mac, COR_RESET);
        if (save_csv(serial, modelo, mac) == 0) {
            printf("       %sRegistrado em capturamac_resultado.csv%s\n", COR_VERDE, COR_RESET);
        } else {
            printf("       %sERRO ao gravar o CSV (pasta bloqueada?).%s\n", COR_VERMELHO, COR_RESET);
        }
        log_linha("MAC capturado: serial=%s modelo=%s mac=%s", serial, modelo, mac);
    } else {
        printf("       %sMAC nao capturado. Nao gravado no CSV.%s\n", COR_VERMELHO, COR_RESET);
        log_linha("FALHA captura: serial=%s modelo=%s wi-fi_original=%d", serial, modelo, st);
    }

    /* restaura estado original: so desliga se foi nos que ligamos */
    if (desligamos) {
        /* o aparelho pode ter saido (USB instavel): confere ainda existe */
        Device d[1];
        int existe = 0;
        if (list_devices(d, 1) > 0) {
            /* qr lista max 1 nao serve para conferir serial; reconfirma */
            char args[128];
            char out[256] = {0};
            snprintf(args, sizeof(args), "-s %.63s get-state", serial);
            if (adb_run(args, out, sizeof(out)) == 0 && strstr(out, "device") != NULL) existe = 1;
        }
        if (existe) {
            wifi_set(serial, 0);
            printf("       %sWi-Fi desligado (estado original restaurado).%s\n", COR_MAGENTA, COR_RESET);
        } else {
            printf("       %sAparelho saiu do USB; Wi-Fi nao alterado de novo.%s\n", COR_AMARELO, COR_RESET);
        }
    }
}

static int serial_exists(Device devs[], int n, const char *serial) {
    for (int i = 0; i < n; i++) {
        if (strcmp(devs[i].serial, serial) == 0) return i;
    }
    return -1;
}

static void data_now(char *out, size_t sz) {
    SYSTEMTIME st; GetLocalTime(&st);
    snprintf(out, sz, "%04d-%02d-%02d %02d:%02d:%02d",
             (int)st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
}

static int save_csv(const char *serial, const char *modelo, const char *mac) {
    Device devs[MAX_DEV];
    int n = load_csv(devs, MAX_DEV);

    char data[40];
    data_now(data, sizeof(data));

    int idx = serial_exists(devs, n, serial);
    if (idx >= 0) {
        snprintf(devs[idx].modelo, sizeof(devs[idx].modelo), "%.63s", modelo);
        snprintf(devs[idx].mac, sizeof(devs[idx].mac), "%.31s", mac);
        snprintf(devs[idx].data, sizeof(devs[idx].data), "%.39s", data);
    } else {
        if (n >= MAX_DEV) return -1;
        snprintf(devs[n].serial, sizeof(devs[n].serial), "%.63s", serial);
        snprintf(devs[n].modelo, sizeof(devs[n].modelo), "%.63s", modelo);
        snprintf(devs[n].mac, sizeof(devs[n].mac), "%.31s", mac);
        snprintf(devs[n].data, sizeof(devs[n].data), "%.39s", data);
        n++;
    }

    FILE *f = fopen(g_csv, "w");
    if (!f) return -1;
    fprintf(f, "Serial,Modelo,MAC_WiFi,Capturado_em\n");
    for (int i = 0; i < n; i++) {
        fprintf(f, "%s,%s,%s,%s\n", devs[i].serial, devs[i].modelo, devs[i].mac, devs[i].data);
    }
    fclose(f);
    return 0;
}

static int load_csv(Device devs[], int max) {
    FILE *f = fopen(g_csv, "r");
    if (!f) return 0;

    char linha[BUF];
    int n = 0;
    int primeira = 1;
    while (fgets(linha, sizeof(linha), f) && n < max) {
        trimsz(linha);
        if (strlen(linha) == 0) continue;
        if (primeira) { primeira = 0; continue; }
        char *serial = strtok(linha, ",");
        char *modelo = strtok(NULL, ",");
        char *mac    = strtok(NULL, ",");
        char *data   = strtok(NULL, ",");
        if (!serial || !modelo || !mac) continue;
        snprintf(devs[n].serial, sizeof(devs[n].serial), "%.63s", serial);
        snprintf(devs[n].modelo, sizeof(devs[n].modelo), "%.63s", modelo);
        snprintf(devs[n].mac, sizeof(devs[n].mac), "%.31s", mac);
        snprintf(devs[n].data, sizeof(devs[n].data), "%.39s", data ? data : "");
        n++;
    }
    fclose(f);
    return n;
}

static void print_line(int len, const char *cor) {
    printf("%s", cor);
    for (int i = 0; i < len; i++) putchar('-');
    printf("%s\n", COR_RESET);
}

static void show_header(void) {
    printf("\n");
    print_line(74, COR_CIANO);
    printf("   %sCAPTURADOR DE MAC - TABLETS VIA ADB (Bot RPA)%s\n", COR_NEGRITO, COR_RESET);
    print_line(74, COR_CIANO);
    printf("\n");
}

static void show_table(void) {
    Device devs[MAX_DEV];
    int n = load_csv(devs, MAX_DEV);
    printf("\n");
    print_line(90, COR_CIANO);
    printf("   %sDISPOSITIVOS REGISTRADOS%s\n", COR_NEGRITO, COR_RESET);
    print_line(90, COR_CIANO);
    if (n == 0) {
        printf("   %sNenhum registrado ainda.%s\n", COR_AMARELO, COR_RESET);
    } else {
        printf("   | %-2s | %-20s | %-16s | %-18s | %-19s |\n",
               "N", "Serial", "Modelo", "MAC Wi-Fi", "Capturado em");
        for (int i = 0; i < n; i++) {
            printf("   | %-2d | %s%-20s%s | %-16s | %s%-18s%s | %-19s |\n",
                   i + 1, COR_VERDE, devs[i].serial, COR_RESET, devs[i].modelo,
                   COR_VERDE, devs[i].mac, COR_RESET, devs[i].data);
        }
        printf("   Total: %d\n", n);
    }
    print_line(90, COR_CIANO);
    printf("\n   %sPressione Enter para voltar...%s", COR_MAGENTA, COR_RESET);
    getchar();
}

/* captura todos os conectados AGORA (uma passada) */
static void capturar_todos(void) {
    Device devs[MAX_DEV];
    int n = list_devices(devs, MAX_DEV);
    if (n <= 0) {
        if (n < 0) {
            printf("   %sERRO: adb nao respondeu.%s\n", COR_VERMELHO, COR_RESET);
        } else {
            printf("   %sNenhum tablet conectado (USB).%s\n", COR_AMARELO, COR_RESET);
        }
        return;
    }
    for (int i = 0; i < n; i++) {
        printf("\n   %s>>> [%d/%d] Tablet: %s%s\n", COR_VERDE, i + 1, n, devs[i].serial, COR_RESET);
        capturar_dispositivo(devs[i].serial);
    }
    printf("\n   %sCaptura concluida: %d aparelho(s).%s\n", COR_VERDE, n, COR_RESET);
}

static void modo_monitor(void) {
    Device proc[MAX_DEV];
    int n_proc = 0;
    printf("   %sMONITOR ATIVO - scan a cada %d s.%s Ctrl+C para voltar.\n\n",
           COR_NEGRITO, SCAN_INTERVAL / 1000, COR_RESET);
    for (;;) {
        Device devs[MAX_DEV];
        int n = list_devices(devs, MAX_DEV);
        for (int i = 0; i < n; i++) {
            if (serial_exists(proc, n_proc, devs[i].serial) < 0) {
                printf("\n   %s>>> Novo tablet: %s%s\n", COR_VERDE, devs[i].serial, COR_RESET);
                capturar_dispositivo(devs[i].serial);
                snprintf(proc[n_proc].serial, sizeof(proc[n_proc].serial), "%.63s", devs[i].serial);
                n_proc++;
            }
        }
        Sleep(SCAN_INTERVAL);
    }
}

static int menu(void) {
    for (;;) {
        show_header();
        printf("   %s[1]%s Capturar MACs agora (todos conectados, uma vez)\n", COR_VERDE, COR_RESET);
        printf("   %s[2]%s Monitor continuo (captura ao conectar)\n", COR_VERDE, COR_RESET);
        printf("   %s[3]%s Dispositivos registrados (tabela)\n", COR_VERDE, COR_RESET);
        printf("   %s[4]%s Sair\n", COR_VERMELHO, COR_RESET);
        print_line(74, COR_CIANO);
        printf("   %sOpcao: %s", COR_AMARELO, COR_RESET);

        int op = 0;
        if (scanf("%d", &op) != 1) { while (getchar() != '\n'); continue; }
        getchar();

        if (op == 4) return 0;
        if (op == 1) { capturar_todos(); while (getchar() != '\n'); }
        if (op == 2) modo_monitor();
        if (op == 3) show_table();
        if (op != 1 && op != 2 && op != 3) {
            printf("   %sOpcao invalida.%s\n", COR_VERMELHO, COR_RESET);
        }
        while (getchar() != '\n');
    }
}

int main(int argc, char **argv) {
    setvbuf(stdout, NULL, _IONBF, 0);
    habilitar_cores();
    resolver_caminhos();

    if (argc > 1 && strcmp(argv[1], "--test") == 0) {
        printf("adb: %s\n", g_adb);
        printf("csv: %s\n", g_csv);
        char raw[BUF * 4] = {0};
        int rc = adb_run("devices -l", raw, sizeof(raw));
        printf("adb_run rc=%d bytes=%llu\n", rc, (unsigned long long)strlen(raw));
        printf("RAW-INICIO>>>%s<<<RAW-FIM\n", strlen(raw) > 400 ? "..." : raw);
        Device devs[MAX_DEV];
        int n = list_devices(devs, MAX_DEV);
        printf("dispositivos: %d\n", n);
        for (int i = 0; i < n; i++) printf("  %s\n", devs[i].serial);
        return 0;
    }

    /* Sobe o servidor adb ANTES de qualquer uso (idempotente, rapido) */
    start_adb();

    if (argc > 1 && strcmp(argv[1], "--once") == 0) {
        capturar_todos();
        return 0;
    }

    printf("   adb: %s\n", g_adb);
    return menu();
}
