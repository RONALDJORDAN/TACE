#ifndef PIPELINE_H
#define PIPELINE_H

#include <windows.h>

#define PIPELINE_SUCCESS 1
#define PIPELINE_ERROR 0

typedef void (*LogCallback)(const char* message);

// Controle global de pausa e cancelamento
extern volatile int g_engine_canceled;
extern volatile int g_engine_paused;

void engine_sleep_check(int ms);

#ifndef API_C_FILE
#ifdef Sleep
#undef Sleep
#endif
#define Sleep(ms) do { \
    engine_sleep_check(ms); \
    if (g_engine_canceled) return 0; \
} while(0)
#endif

// F1: Sincronização// Assinaturas Exportáveis (API)
int Engine_ExecuteF1_Positivo(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb);

// F2: Reinstalação Limpa
int Engine_ExecuteF2_Reinstall(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb);

// F3 - Atualizacao de Pacote
int F3_ExecutarAtualizacaoPacote(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb);

// F4: Ferramenta Kiosk
int Engine_ExecuteF4_Ferramenta(const char* serial, int acao, LogCallback cb);
int Engine_ExecuteF4_Wallpaper(const char* serial, const char* wp_path, LogCallback cb);

// F5: Desligamento Remoto
int Engine_ExecuteF5_Shutdown(const char* serial, LogCallback cb);

// Execucao da Ferramenta 6: Sincronizacao Avancada
int Engine_ExecuteF6_Sincronizacao(const char* serial, LogCallback cb);

#endif // PIPELINE_H
