#ifndef MUNICIPIOS_H
#define MUNICIPIOS_H

#define MAX_MUNICIPIOS 256
#define MAX_STR 512

typedef struct {
    char nome[128];
    char login[512];  // 512: comporta credencial cifrada DPAPI (Base64)
    char senha[512];  // 512: comporta credencial cifrada DPAPI (Base64)
    char wallpaper_path[MAX_STR];
} Municipio;

// Carrega o CSV e retorna a quantidade de municÃ­pios lidos
int municipios_carregar(Municipio* lista, int max, const char* csv_path);

// Salva a lista de municÃ­pios de volta para o CSV
int municipios_salvar(const Municipio* lista, int count, const char* csv_path);

// Resolve o caminho do CSV relativo ao executÃ¡vel
void municipios_get_csv_path(char* out_path, int max_len);

#endif // MUNICIPIOS_H
