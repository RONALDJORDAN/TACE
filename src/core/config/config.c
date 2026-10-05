#include "config.h"
#include <windows.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

static void get_config_path(char* out_path, int max_len) {
    char exe_dir[MAX_PATH];
    GetModuleFileNameA(NULL, exe_dir, sizeof(exe_dir));
    char* last = strrchr(exe_dir, '\\');
    if (last) *last = '\0';

    char candidate[MAX_PATH];
    snprintf(candidate, sizeof(candidate), "%s\\config\\settings.json", exe_dir);
    if (GetFileAttributesA(candidate) == INVALID_FILE_ATTRIBUTES) {
        snprintf(candidate, sizeof(candidate), "%s\\..\\..\\config\\settings.json", exe_dir);
    }
    strncpy(out_path, candidate, max_len - 1);
    out_path[max_len - 1] = '\0';
}

void config_load(AppConfig* cfg) {
    if (!cfg) return;

    // Valores Padrão de Fábrica
    cfg->font_ui_size = 14;
    cfg->font_title_size = 20;
    cfg->font_mono_size = 13;
    cfg->motor_c_enabled = 1;
    cfg->reverse_tethering_enabled = 1;
    cfg->ai_learn_active = 0; // Padrão seguro: Estático/Determinístico

    char path[MAX_PATH];
    get_config_path(path, sizeof(path));

    FILE* f = fopen(path, "r");
    if (!f) return;

    char line[256];
    while (fgets(line, sizeof(line), f)) {
        if (strstr(line, "\"font_ui_size\"")) {
            sscanf(strstr(line, ":") + 1, "%d", &cfg->font_ui_size);
        } else if (strstr(line, "\"font_title_size\"")) {
            sscanf(strstr(line, ":") + 1, "%d", &cfg->font_title_size);
        } else if (strstr(line, "\"font_mono_size\"")) {
            sscanf(strstr(line, ":") + 1, "%d", &cfg->font_mono_size);
        } else if (strstr(line, "\"motor_c_enabled\"")) {
            int val = 1;
            if (strstr(line, "false") || strstr(line, "0")) val = 0;
            cfg->motor_c_enabled = val;
        } else if (strstr(line, "\"reverse_tethering_enabled\"")) {
            int val = 1;
            if (strstr(line, "false") || strstr(line, "0")) val = 0;
            cfg->reverse_tethering_enabled = val;
        } else if (strstr(line, "\"ai_learn_active\"") || strstr(line, "\"ai_engine_active\"") || strstr(line, "\"botlearn_active\"")) {
            int val = 0;
            if (strstr(line, "true") || strstr(line, ": 1") || strstr(line, ":1")) val = 1;
            cfg->ai_learn_active = val;
        }
    }
    fclose(f);
}

void config_save(const AppConfig* cfg) {
    if (!cfg) return;

    char path[MAX_PATH];
    get_config_path(path, sizeof(path));

    FILE* f = fopen(path, "w");
    if (!f) return;

    fprintf(f, "{\n");
    fprintf(f, "  \"font_ui_size\": %d,\n", cfg->font_ui_size);
    fprintf(f, "  \"font_title_size\": %d,\n", cfg->font_title_size);
    fprintf(f, "  \"font_mono_size\": %d,\n", cfg->font_mono_size);
    fprintf(f, "  \"motor_c_enabled\": %s,\n", cfg->motor_c_enabled ? "true" : "false");
    fprintf(f, "  \"reverse_tethering_enabled\": %s,\n", cfg->reverse_tethering_enabled ? "true" : "false");
    fprintf(f, "  \"ai_learn_active\": %s\n", cfg->ai_learn_active ? "true" : "false");
    fprintf(f, "}\n");
    fclose(f);
}
