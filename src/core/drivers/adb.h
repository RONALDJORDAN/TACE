#ifndef ADB_H
#define ADB_H

#include <windows.h>

#define MAX_DEVICES 16
#define MAX_SERIAL  64
#define MAX_MODEL  128
#define MAX_IMEI    32
#define MAX_MAC     32

typedef struct {
    char serial[MAX_SERIAL];
    char model[MAX_MODEL];
    char manufacturer[32];
    char android_version[16];
    char api_level[16];
    char battery[16];
    char imei[MAX_IMEI];
    char mac[MAX_MAC];
    int  authorized; // 1 = device, 0 = unauthorized/offline
} AdbDevice;

// Retorna o caminho do adb.exe (local do projeto ou sistema)
void adb_get_path(char* out_path, int max_len);

// Lista dispositivos conectados. Retorna quantidade de devs encontrados.
int adb_list_devices(AdbDevice* devices, int max_devices);

// Busca modelo do dispositivo
void adb_get_model(const char* serial, char* out_model, int max_len);

// Busca IMEI do dispositivo
void adb_get_imei(const char* serial, char* out_imei, int max_len);

// Captura o endereco MAC Wi-Fi do dispositivo.
// O MAC so e legivel com a interface wlan0 ativa: por isso a rotina
// reproduz o ciclo da ferramenta de captura (le direct -> liga Wi-Fi ->
// extrai -> restaura o estado original). Retorna 1 se ok, 0 se falhou.
int adb_get_mac(const char* serial, char* out_mac, int max_len);


// Estrutura para um Pacote (App)
#define MAX_PACKAGES 500
#define MAX_PKG_NAME 256

typedef struct {
    char name[MAX_PKG_NAME];
} AdbPackage;

// Executa um comando ADB generico e retorna o output alocado (caller deve free())
char* adb_exec(const char* args);

// Captura ultra-rapida do XML de UI em memoria RAM via StdOut pipe
char* adb_dump_stdout(const char* serial);

// Sondagem leve de foco de janela em 30ms
int adb_get_current_focus(const char* serial, char* out_focus, int max_len);

typedef void (*AdbLogCallback)(const char* msg);

// Executa um comando ADB com sistema de retentativa
char* adb_exec_retry(const char* args, int max_retries, int delay_ms, AdbLogCallback cb);

// Retorna o numero de pacotes encontrados e preenche o array
int adb_get_packages(const char* serial, AdbPackage* packages, int max_pkgs);

// Controles de App (Retornam 1 se sucesso, 0 se erro)
int adb_uninstall_app(const char* serial, const char* package_name);
int adb_clear_app_data(const char* serial, const char* package_name);
int adb_force_stop(const char* serial, const char* package_name);

// Resolve problemas de acentos no Windows gerando o Short Path 8.3 de caminhos UTF-8
int adb_get_safe_path(const char* utf8_path, char* out_safe_path, int max_len);

// --- Tratamento Universal de Caracteres Especiais e Acentuação ---
char inno_normalize_char(unsigned char c, unsigned char next_c, int *skip_next);
const char* inno_stristr_fuzzy(const char *haystack, const char *needle);
int adb_get_centroid_fuzzy(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count);
void adb_escape_text(const char *src, char *dest, size_t dest_size);

// --- Watchdog Keep-Alive Android Anti-Crash ---
int adb_ensure_app_running(const char* serial, const char* package_name, AdbLogCallback cb);

#endif // ADB_H


