#include "dispositivos.h"
#include "paths.h"
#include <stdio.h>
#include <string.h>
#include <windows.h>
#include <time.h>

static void get_csv_path(char* out_path, int max_len) {
    // CSV legado: dado do usuario -> mora na pasta de dados
    char data_dir[MAX_PATH] = {0};
    paths_get_data_dir(data_dir, sizeof(data_dir));
    snprintf(out_path, max_len, "%s\\registros_de_dispositivos\\banco_dispositivos.csv", data_dir);
}

int dispositivos_carregar(DispositivoRegistro* lista, int max) {
    char path[MAX_PATH];
    get_csv_path(path, sizeof(path));

    FILE* f = fopen(path, "r");
    if (!f) return 0;

    int count = 0;
    char line[1024];
    int first_line = 1;

    while (fgets(line, sizeof(line), f) && count < max) {
        if (first_line) { first_line = 0; continue; }

        line[strcspn(line, "\r\n")] = 0;
        if (strlen(line) < 5) continue;

        char* fields[9];
        char* token = strtok(line, ";");
        int fi = 0;
        while (token && fi < 9) {
            fields[fi++] = token;
            token = strtok(NULL, ";");
        }
        if (fi < 1) continue;

        DispositivoRegistro temp;
        memset(&temp, 0, sizeof(DispositivoRegistro));
        
        if (fi > 0) strncpy(temp.serial, fields[0], sizeof(temp.serial) - 1);
        if (fi > 1) strncpy(temp.manufacturer, fields[1], sizeof(temp.manufacturer) - 1);
        if (fi > 2) strncpy(temp.model,  fields[2], sizeof(temp.model) - 1);
        if (fi > 3) strncpy(temp.android_version, fields[3], sizeof(temp.android_version) - 1);
        if (fi > 4) strncpy(temp.api_level, fields[4], sizeof(temp.api_level) - 1);
        if (fi > 5) strncpy(temp.battery, fields[5], sizeof(temp.battery) - 1);
        if (fi > 6) strncpy(temp.imei,   fields[6], sizeof(temp.imei) - 1);
        if (fi > 7) strncpy(temp.mac,    fields[7], sizeof(temp.mac) - 1);
        if (fi > 8) strncpy(temp.primeiro_acesso, fields[8], sizeof(temp.primeiro_acesso) - 1);

        // Verifica se jÃ¡ existe na lista
        int is_dup = 0;
        for (int i = 0; i < count; i++) {
            if (strcmp(lista[i].serial, temp.serial) == 0) {
                is_dup = 1;
                break;
            }
        }

        if (!is_dup) {
            memcpy(&lista[count], &temp, sizeof(DispositivoRegistro));
            count++;
        }
    }
    fclose(f);
    return count;
}

void dispositivos_registrar(AdbDevice* dev) {
    if (!dev || strlen(dev->serial) == 0) return;

    DispositivoRegistro db[MAX_REGISTROS];
    int count = dispositivos_carregar(db, MAX_REGISTROS);

    for (int i = 0; i < count; i++) {
        if (strcmp(db[i].serial, dev->serial) == 0) {
            return; // JÃ¡ registrado
        }
    }

    char path[MAX_PATH];
    get_csv_path(path, sizeof(path));

    // Se arquivo nÃ£o existe, cria com cabeÃ§alho
    FILE* check = fopen(path, "r");
    BOOL add_header = (check == NULL);
    if (check) fclose(check);

    FILE* f = fopen(path, "a");
    if (!f) return;

    if (add_header) {
        fprintf(f, "\xEF\xBB\xBF"); // BOM UTF-8
        fprintf(f, "Serial;Fabricante;Modelo;Versao_Android;API_Level;Bateria;IMEI;MAC;Primeiro_Acesso\n");
    }

    SYSTEMTIME st;
    GetLocalTime(&st);
    char data_hora[64];
    snprintf(data_hora, sizeof(data_hora), "%02d/%02d/%04d %02d:%02d:%02d",
        st.wDay, st.wMonth, st.wYear, st.wHour, st.wMinute, st.wSecond);

    fprintf(f, "%s;%s;%s;%s;%s;%s;%s;%s;%s\n",
        dev->serial,
        strlen(dev->manufacturer) > 0 ? dev->manufacturer : "---",
        strlen(dev->model) > 0 ? dev->model : "---",
        strlen(dev->android_version) > 0 ? dev->android_version : "---",
        strlen(dev->api_level) > 0 ? dev->api_level : "---",
        strlen(dev->battery) > 0 ? dev->battery : "---",
        strlen(dev->imei) > 0 ? dev->imei : "---",
        strlen(dev->mac) > 0 ? dev->mac : "---",
        data_hora);
        
    fclose(f);
}
