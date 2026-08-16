#include "municipios.h"
#include <stdio.h>
#include <string.h>
#include <windows.h>

void municipios_get_csv_path(char* out_path, int max_len) {
    char exe_path[MAX_STR];
    GetModuleFileNameA(NULL, exe_path, sizeof(exe_path));
    
    // Obtém o diretório do executável
    char* p = strrchr(exe_path, '\\');
    if (p) *p = '\0';
    
    snprintf(out_path, max_len, "%s\\municipios.csv", exe_path);
}

// Converte string lida do CSV em UTF-8 para ANSI (CP_ACP) para que o P/Invoke CharSet.Ansi do C# leia sem caracteres estranhos (Ã§, Ã£)
static void convert_utf8_to_ansi(const char* in_str, char* out_str, int max_len) {
    if (!in_str || !out_str || max_len <= 0) return;
    
    WCHAR wbuf[512] = {0};
    int wlen = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, in_str, -1, wbuf, 512);
    if (wlen <= 0) {
        // Se já for ANSI/ACP, converte diretamente
        MultiByteToWideChar(CP_ACP, 0, in_str, -1, wbuf, 512);
    }
    
    WideCharToMultiByte(CP_ACP, 0, wbuf, -1, out_str, max_len, NULL, NULL);
}

// Converte string recebida do C# P/Invoke (ANSI) para UTF-8 limpo antes de salvar no arquivo CSV
static void convert_ansi_to_utf8(const char* in_str, char* out_str, int max_len) {
    if (!in_str || !out_str || max_len <= 0) return;
    
    WCHAR wbuf[512] = {0};
    MultiByteToWideChar(CP_ACP, 0, in_str, -1, wbuf, 512);
    WideCharToMultiByte(CP_UTF8, 0, wbuf, -1, out_str, max_len, NULL, NULL);
}

int municipios_carregar(Municipio* lista, int max, const char* csv_path) {
    FILE* f = fopen(csv_path, "r");
    if (!f) return 0;

    int count = 0;
    char line[2048];
    int first_line = 1;

    while (fgets(line, sizeof(line), f) && count < max) {
        // Pular cabeçalho
        if (first_line) { first_line = 0; continue; }

        // Remove newline
        line[strcspn(line, "\r\n")] = 0;
        if (strlen(line) < 2) continue;

        // Parsear CSV com delimitador ;
        // Formato: municipio;login;senha;wallpaper_path
        char* fields[4];
        char* token = strtok(line, ";");
        int fi = 0;
        while (token && fi < 4) {
            fields[fi++] = token;
            token = strtok(NULL, ";");
        }
        if (fi < 1) continue;

        Municipio* m = &lista[count];
        memset(m, 0, sizeof(Municipio));
        
        if (fi > 0) convert_utf8_to_ansi(fields[0], m->nome, sizeof(m->nome));
        if (fi > 1) strncpy(m->login, fields[1], sizeof(m->login) - 1);
        if (fi > 2) strncpy(m->senha, fields[2], sizeof(m->senha) - 1);
        if (fi > 3) convert_utf8_to_ansi(fields[3], m->wallpaper_path, sizeof(m->wallpaper_path));

        count++;
    }

    fclose(f);
    return count;
}

int municipios_salvar(const Municipio* lista, int count, const char* csv_path) {
    FILE* f = fopen(csv_path, "w");
    if (!f) return 0;
    fprintf(f, "municipio;login;senha;wallpaper_path\n");
    for (int i = 0; i < count; i++) {
        char utf8_nome[256] = {0};
        char utf8_wp[512] = {0};
        
        convert_ansi_to_utf8(lista[i].nome, utf8_nome, sizeof(utf8_nome));
        convert_ansi_to_utf8(lista[i].wallpaper_path, utf8_wp, sizeof(utf8_wp));
        
        fprintf(f, "%s;%s;%s;%s\n",
            utf8_nome[0] ? utf8_nome : lista[i].nome,
            lista[i].login,
            lista[i].senha,
            utf8_wp[0] ? utf8_wp : lista[i].wallpaper_path);
    }
    fclose(f);
    return 1;
}
