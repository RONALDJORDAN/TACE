#include <stdio.h>
#include <string.h>

typedef struct {
    char serial[64];
    char manufacturer[32];
    char model[64];
    char android_version[16];
    char api_level[16];
    char battery[16];
    char imei[64];
    char mac[64];
    char primeiro_acesso[64];
} DispositivoRegistro;

int main() {
    FILE* f = fopen("data/output/registros_de_dispositivos/banco_dispositivos.csv", "r");
    if (!f) return 0;
    int count = 0;
    char line[1024];
    int first_line = 1;
    while (fgets(line, sizeof(line), f)) {
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
        count++;
    }
    fclose(f);
    printf("Total Lidos: %d\n", count);
    return 0;
}
