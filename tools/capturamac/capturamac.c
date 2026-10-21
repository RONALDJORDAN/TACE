#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define ADB "..\\tools\\platform-tools\\adb.exe"
#define BUF 16384
#define MAX_DEV 32
#define CSV_PATH "capturamac_resultado.csv"
#define SCAN_INTERVAL_MS 5000

#define COR_RESET   "\033[0m"
#define COR_NEGRITO "\033[1m"
#define COR_CIANO   "\033[36m"
#define COR_VERDE   "\033[32m"
#define COR_AMARELO "\033[33m"
#define COR_AZUL    "\033[34m"
#define COR_MAGENTA "\033[35m"
#define COR_VERMELHO "\033[31m"

typedef struct {
    char serial[64];
    char mac[64];
    char modelo[64];
    char data[64];
} Device;

static void trim(char *s);
static int exec_cmd(const char *cmd, char *out, size_t outsz);
static int wifi_is_on(const char *serial);
static int wifi_turn_on(const char *serial);
static int wifi_turn_off(const char *serial);
static void get_mac(const char *serial, char *mac, size_t macsz);
static void get_model(const char *serial, char *model, size_t modelSz);
static int save_csv(const char *path, const char *serial, const char *model, const char *mac);
static int load_csv(Device devs[], int max);
static void deduplicate_csv(const char *path);
static int serial_exists(Device devs[], int n, const char *serial);
static void show_header(void);
static void show_menu(void);
static void print_line(int len, const char *cor);
static void print_table_row(int idx, const char *serial, const char *modelo, const char *mac, const char *data);
static void show_devices_table(void);
static int list_devices(Device devs[], int max);
static void safe_copy(char *dst, size_t dstsz, const char *src);
static void habilitar_cores_windows(void);
static void capturar_dispositivo(const char *serial);
static void modo_monitor(void);

static void safe_copy(char *dst, size_t dstsz, const char *src) {
    size_t len = strlen(src);
    if (len > dstsz - 1) len = dstsz - 1;
    memcpy(dst, src, len);
    dst[len] = '\0';
}

static void habilitar_cores_windows(void) {
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    if (hOut == INVALID_HANDLE_VALUE) return;
    DWORD dwMode = 0;
    if (!GetConsoleMode(hOut, &dwMode)) return;
    dwMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
    SetConsoleMode(hOut, dwMode);
}

static int exec_cmd(const char *cmd, char *out, size_t outsz) {
    FILE *p = _popen(cmd, "r");
    if (!p) return -1;
    if (out) {
        out[0] = '\0';
        char line[1024];
        while (fgets(line, sizeof(line), p)) {
            strncat(out, line, outsz - strlen(out) - 1);
        }
    } else {
        char line[1024];
        while (fgets(line, sizeof(line), p)) {}
    }
    return _pclose(p);
}

static void trim(char *s) {
    char *e = s + strlen(s) - 1;
    while (e >= s && (*e == '\n' || *e == '\r' || *e == ' ' || *e == '\t')) *e-- = '\0';
}

static int list_devices(Device devs[], int max) {
    char out[BUF * 4] = {0};
    char cmd[2048];
    snprintf(cmd, sizeof(cmd), "%s devices", ADB);
    if (exec_cmd(cmd, out, sizeof(out)) != 0) return -1;

    int n = 0;
    char *ctx = NULL;
    char *line = strtok_s(out, "\r\n", &ctx);
    while (line && n < max) {
        trim(line);
        if (strlen(line) == 0 || strstr(line, "List of devices")) {
            line = strtok_s(NULL, "\r\n", &ctx);
            continue;
        }
        char *tab = strchr(line, '\t');
        if (tab && strstr(tab + 1, "device")) {
            *tab = '\0';
            safe_copy(devs[n].serial, sizeof(devs[n].serial), line);
            trim(devs[n].serial);
            devs[n].mac[0] = '\0';
            devs[n].modelo[0] = '\0';
            n++;
        }
        line = strtok_s(NULL, "\r\n", &ctx);
    }
    return n;
}

static int wifi_is_on(const char *serial) {
    char cmd[2048];
    char out[BUF] = {0};
    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell settings get global wifi_on", ADB, serial);
    if (exec_cmd(cmd, out, sizeof(out)) != 0) return -1;
    trim(out);
    if (strcmp(out, "1") == 0) return 1;
    return 0;
}

static int wifi_turn_on(const char *serial) {
    char cmd[2048];
    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell svc wifi enable", ADB, serial);
    if (exec_cmd(cmd, NULL, 0) != 0) return -1;
    Sleep(3000);
    return 1;
}

static int wifi_turn_off(const char *serial) {
    char cmd[2048];
    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell svc wifi disable", ADB, serial);
    if (exec_cmd(cmd, NULL, 0) != 0) return -1;
    Sleep(3000);
    return 1;
}

static void get_mac(const char *serial, char *mac, size_t macsz) {
    char cmd[2048];
    char out[BUF] = {0};

    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell cat /sys/class/net/wlan0/address", ADB, serial);
    if (exec_cmd(cmd, out, sizeof(out)) == 0) {
        trim(out);
        if (out[0] != '\0' && strstr(out, "permission") == NULL && strstr(out, "No such") == NULL) {
            size_t len = strlen(out);
            if (len > macsz - 1) len = macsz - 1;
            memcpy(mac, out, len);
            mac[len] = '\0';
            return;
        }
    }

    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell ip link show wlan0", ADB, serial);
    if (exec_cmd(cmd, out, sizeof(out)) == 0) {
        char *ptr = strstr(out, "link/ether");
        if (ptr) {
            ptr += 10;
            while (*ptr == ' ') ptr++;
            char *end = ptr;
            while (*end && *end != ' ' && *end != '\n' && *end != '\r') end++;
            *end = '\0';
            size_t len = strlen(ptr);
            if (len > macsz - 1) len = macsz - 1;
            memcpy(mac, ptr, len);
            mac[len] = '\0';
            return;
        }
    }

    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell dumpsys wifi", ADB, serial);
    if (exec_cmd(cmd, out, sizeof(out)) == 0) {
        char *ptr = strstr(out, "MAC: ");
        if (ptr) {
            ptr += 5;
            char *end = ptr;
            while (*end && *end != ',' && *end != ' ' && *end != '\n' && *end != '\r') end++;
            *end = '\0';
            if (strlen(ptr) >= 17) {
                size_t len = strlen(ptr);
                if (len > macsz - 1) len = macsz - 1;
                memcpy(mac, ptr, len);
                mac[len] = '\0';
                return;
            }
        }
    }

    strncpy(mac, "nao encontrado", macsz - 1);
    mac[macsz - 1] = '\0';
}

static void get_model(const char *serial, char *model, size_t modelSz) {
    char cmd[2048];
    char out[BUF] = {0};
    snprintf(cmd, sizeof(cmd), "%.63s -s %.63s shell getprop ro.product.model", ADB, serial);
    if (exec_cmd(cmd, out, sizeof(out)) != 0) {
        strncpy(model, "desconhecido", modelSz - 1);
        model[modelSz - 1] = '\0';
        return;
    }
    trim(out);
    if (out[0] == '\0') {
        strncpy(model, "desconhecido", modelSz - 1);
        model[modelSz - 1] = '\0';
        return;
    }
    size_t len = strlen(out);
    if (len > modelSz - 1) len = modelSz - 1;
    memcpy(model, out, len);
    model[len] = '\0';
}

static int serial_exists(Device devs[], int n, const char *serial) {
    for (int i = 0; i < n; i++) {
        if (strcmp(devs[i].serial, serial) == 0) return i;
    }
    return -1;
}

static int save_csv(const char *path, const char *serial, const char *model, const char *mac) {
    Device devs[MAX_DEV];
    int n = load_csv(devs, MAX_DEV);

    int idx = serial_exists(devs, n, serial);
    char data[64] = {0};
    SYSTEMTIME st;
    GetSystemTime(&st);
    snprintf(data, sizeof(data), "%04d-%02d-%02d %02d:%02d:%02d",
             st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);

    if (idx >= 0) {
        safe_copy(devs[idx].modelo, sizeof(devs[idx].modelo), model);
        safe_copy(devs[idx].mac, sizeof(devs[idx].mac), mac);
        safe_copy(devs[idx].data, sizeof(devs[idx].data), data);
    } else {
        if (n >= MAX_DEV) return -1;
        safe_copy(devs[n].serial, sizeof(devs[n].serial), serial);
        safe_copy(devs[n].modelo, sizeof(devs[n].modelo), model);
        safe_copy(devs[n].mac, sizeof(devs[n].mac), mac);
        safe_copy(devs[n].data, sizeof(devs[n].data), data);
        n++;
    }

    FILE *f = fopen(path, "w");
    if (!f) return -1;

    fprintf(f, "Serial,Modelo,MAC_WiFi,Capturado_em\n");
    for (int i = 0; i < n; i++) {
        fprintf(f, "%s,%s,%s,%s\n", devs[i].serial, devs[i].modelo, devs[i].mac, devs[i].data);
    }
    fclose(f);
    return 0;
}

static int load_csv(Device devs[], int max) {
    FILE *f = fopen(CSV_PATH, "r");
    if (!f) return 0;

    char linha[BUF];
    int n = 0;
    int primeira = 1;

    while (fgets(linha, sizeof(linha), f)) {
        trim(linha);
        if (strlen(linha) == 0) continue;
        if (primeira) { primeira = 0; continue; }

        char *serial = strtok(linha, ",");
        char *modelo = strtok(NULL, ",");
        char *mac = strtok(NULL, ",");
        char *data = strtok(NULL, ",");

        if (!serial || !modelo || !mac) continue;

        safe_copy(devs[n].serial, sizeof(devs[n].serial), serial);
        safe_copy(devs[n].modelo, sizeof(devs[n].modelo), modelo);
        safe_copy(devs[n].mac, sizeof(devs[n].mac), mac);
        safe_copy(devs[n].data, sizeof(devs[n].data), data ? data : "");
        n++;
        if (n >= max) break;
    }

    fclose(f);
    return n;
}

static void deduplicate_csv(const char *path) {
    Device devs[MAX_DEV];
    int n = load_csv(devs, MAX_DEV);
    if (n <= 1) return;

    FILE *f = fopen(path, "w");
    if (!f) return;

    fprintf(f, "Serial,Modelo,MAC_WiFi,Capturado_em\n");
    for (int i = 0; i < n; i++) {
        fprintf(f, "%s,%s,%s,%s\n", devs[i].serial, devs[i].modelo, devs[i].mac, devs[i].data);
    }
    fclose(f);
}

static void capturar_dispositivo(const char *serial) {
    printf("\n   %s>>> Tablet detectado: %s%s\n", COR_VERDE, serial, COR_RESET);

    int st = wifi_is_on(serial);
    if (st == 0) {
        printf("       %sWi-Fi desligado. Ligando...%s\n", COR_AMARELO, COR_RESET);
        int r = wifi_turn_on(serial);
        if (r == 1) printf("       %sWi-Fi ligado com sucesso.%s\n", COR_VERDE, COR_RESET);
        else if (r == 0) printf("       %sAviso: nao foi possivel ligar o Wi-Fi automaticamente.%s\n", COR_AMARELO, COR_RESET);
        else printf("       %sErro ao verificar Wi-Fi.%s\n", COR_VERMELHO, COR_RESET);
    } else if (st == 1) {
        printf("       %sWi-Fi ja esta ligado.%s\n", COR_VERDE, COR_RESET);
    } else {
        printf("       %sErro ao verificar estado do Wi-Fi.%s\n", COR_VERMELHO, COR_RESET);
    }

    char mac[64] = {0};
    char modelo[64] = {0};
    get_mac(serial, mac, sizeof(mac));
    get_model(serial, modelo, sizeof(modelo));

    printf("       %sSerial:%s      %s\n", COR_AZUL, COR_RESET, serial);
    printf("       %sModelo:%s      %s\n", COR_AZUL, COR_RESET, modelo);
    printf("       %sMAC Wi-Fi:%s   %s\n", COR_AZUL, COR_RESET, mac);

    save_csv(CSV_PATH, serial, modelo, mac);
    printf("       %sRegistrado em %s%s\n", COR_VERDE, CSV_PATH, COR_RESET);

    int off = wifi_turn_off(serial);
    if (off == 1) printf("       %sWi-Fi desligado apos captura.%s\n", COR_VERDE, COR_RESET);
    else if (off == 0) printf("       %sAviso: nao foi possivel desligar o Wi-Fi automaticamente.%s\n", COR_AMARELO, COR_RESET);
    else printf("       %sErro ao desligar Wi-Fi.%s\n", COR_VERMELHO, COR_RESET);
}

static void show_header(void) {
    printf("\n");
    print_line(74, COR_CIANO);
    printf("   %sCAPTURADOR DE MAC - TABLETS VIA ADB%s\n", COR_NEGRITO, COR_RESET);
    print_line(74, COR_CIANO);
    printf("\n");
}

static void show_menu(void) {
    printf("   %sMENU PRINCIPAL%s\n", COR_NEGRITO, COR_RESET);
    print_line(74, COR_CIANO);
    printf("   %s[1]%s Iniciar monitoramento automatico (captura ao conectar)\n", COR_VERDE, COR_RESET);
    printf("   %s[2]%s Exibir dispositivos registrados (tabela)\n", COR_VERDE, COR_RESET);
    printf("   %s[3]%s Sair\n", COR_VERMELHO, COR_RESET);
    print_line(74, COR_CIANO);
    printf("   %sEscolha uma opcao: %s", COR_AMARELO, COR_RESET);
}

static void print_line(int len, const char *cor) {
    for (int i = 0; i < len; i++) putchar('-');
    putchar('\n');
    printf("%s", cor);
}

static void print_table_row(int idx, const char *serial, const char *modelo, const char *mac, const char *data) {
    printf("   | %s%-2d%s | %-20s | %-16s | %-18s | %-19s |\n",
           COR_VERDE, idx, COR_RESET, serial, modelo, mac, data);
}

static void show_devices_table(void) {
    Device devs[MAX_DEV];
    int n = load_csv(devs, MAX_DEV);

    printf("\n");
    print_line(88, COR_CIANO);
    printf("   %sDISPOSITIVOS REGISTRADOS%s\n", COR_NEGRITO, COR_RESET);
    print_line(88, COR_CIANO);

    if (n == 0) {
        printf("   %sNenhum dispositivo registrado ainda.%s\n", COR_AMARELO, COR_RESET);
        print_line(88, COR_CIANO);
        printf("\n   %sPressione Enter para voltar...%s", COR_MAGENTA, COR_RESET);
        getchar();
        return;
    }

    printf("   | %-2s | %-20s | %-16s | %-18s | %-19s |\n",
           "N", "Serial", "Modelo", "MAC Wi-Fi", "Capturado em");
    print_line(88, COR_CIANO);

    for (int i = 0; i < n; i++) {
        print_table_row(i + 1, devs[i].serial, devs[i].modelo, devs[i].mac, devs[i].data);
    }

    print_line(88, COR_CIANO);
    printf("   %sTotal: %d dispositivo(s) registrado(s).%s\n", COR_VERDE, n, COR_RESET);
    print_line(88, COR_CIANO);
    printf("\n   %sPressione Enter para voltar...%s", COR_MAGENTA, COR_RESET);
    getchar();
}

static void modo_monitor(void) {
    Device processados[MAX_DEV];
    int n_proc = 0;

    system("cls");
    show_header();
    printf("   %sMONITOR AUTOMATICO ATIVO%s\n", COR_NEGRITO, COR_RESET);
    print_line(74, COR_CIANO);
    printf("   %sAguardando tablets conectados via ADB...%s\n", COR_AZUL, COR_RESET);
    printf("   Scan a cada %d segundos.\n", SCAN_INTERVAL_MS / 1000);
    printf("   %sPressione Ctrl+C para parar e voltar ao menu.%s\n\n", COR_VERMELHO, COR_RESET);

    while (1) {
        Device devs[MAX_DEV];
        int n = list_devices(devs, MAX_DEV);

        if (n > 0) {
            for (int i = 0; i < n; i++) {
                int ja_proc = serial_exists(processados, n_proc, devs[i].serial);
                if (ja_proc < 0) {
                    capturar_dispositivo(devs[i].serial);
                    safe_copy(processados[n_proc].serial, sizeof(processados[n_proc].serial), devs[i].serial);
                    safe_copy(processados[n_proc].modelo, sizeof(processados[n_proc].modelo), devs[i].modelo);
                    safe_copy(processados[n_proc].mac, sizeof(processados[n_proc].mac), devs[i].mac);
                    n_proc++;
                }
            }
        }

        Sleep(SCAN_INTERVAL_MS);
    }
}

int main(void) {
    int opcao;

    setvbuf(stdout, NULL, _IONBF, 0);
    setvbuf(stderr, NULL, _IONBF, 0);

    habilitar_cores_windows();

    deduplicate_csv(CSV_PATH);

    system("cls");

    while (1) {
        show_header();
        show_menu();
        if (scanf("%d", &opcao) != 1) {
            while (getchar() != '\n');
            continue;
        }
        getchar();

        if (opcao == 3) {
            printf("\n   Saindo...\n");
            break;
        } else if (opcao == 2) {
            system("cls");
            show_header();
            show_devices_table();
            system("cls");
        } else if (opcao == 1) {
            modo_monitor();
            system("cls");
        } else {
            printf("\n   %sOpcao invalida.%s\n", COR_VERMELHO, COR_RESET);
            printf("   %sPressione Enter para continuar...%s", COR_MAGENTA, COR_RESET);
            getchar();
            system("cls");
        }
    }

    return 0;
}
