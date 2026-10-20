#define API_C_FILE

#include <windows.h>
#include <stdio.h>
#include "core/drivers/adb.h"
#include "core/data/dispositivos.h"
#include "core/data/municipios.h"
#include "core/config/config.h"
#include "core/learn/learn_db.h"


// Permite compilar como DLL sem quebrar exports
#ifndef DllExport
#define DllExport __declspec(dllexport)
#endif

// Controle global de pausa e cancelamento
volatile int g_engine_canceled = 0;
volatile int g_engine_paused = 0;

// --- Filtro Anti-Crash Top-Level SEH para a DLL BotEngine ---
static LONG WINAPI InnoUnhandledExceptionFilter(EXCEPTION_POINTERS *pExceptionInfo) {
    char exe_path[MAX_PATH];
    GetModuleFileNameA(NULL, exe_path, sizeof(exe_path));
    char *last_slash = strrchr(exe_path, '\\');
    if (last_slash) *last_slash = '\0';

    char log_path[MAX_PATH];
    snprintf(log_path, sizeof(log_path), "%s\\logs\\engine_crash.txt", exe_path);

    FILE *f = fopen(log_path, "a");
    if (f) {
        fprintf(f, "[ENGINE ANTI-CRASH] Excecao bruta isolada. Code: 0x%08lX | Address: 0x%p\n",
                (unsigned long)pExceptionInfo->ExceptionRecord->ExceptionCode,
                pExceptionInfo->ExceptionRecord->ExceptionAddress);
        fclose(f);
    }
    return EXCEPTION_EXECUTE_HANDLER;
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved) {
    (void)hinstDLL;
    (void)lpvReserved;
    switch (fdwReason) {
    case DLL_PROCESS_ATTACH:
        SetUnhandledExceptionFilter(InnoUnhandledExceptionFilter);
        learn_db_load();
        break;
    case DLL_PROCESS_DETACH:
        if (g_learn_db.dirty) {
            learn_db_save();
        }
        break;
    }
    return TRUE;
}



void engine_sleep_check(int ms) {
    int elapsed = 0;
    while (elapsed < ms) {
        if (g_engine_canceled) return;
        while (g_engine_paused) {
            if (g_engine_canceled) return;
            Sleep(100);
        }
        Sleep(100);
        elapsed += 100;
    }
}

DllExport void Engine_CancelAll() {
    g_engine_canceled = 1;
}

DllExport void Engine_SetPaused(int paused) {
    g_engine_paused = paused;
}

DllExport void Engine_ResetCancel() {
    g_engine_canceled = 0;
    g_engine_paused = 0;
}

// MantÃ©m as variÃ¡veis globais para a Engine
AdbDevice g_cached_devices[MAX_DEVICES];
int g_cached_count = 0;

DllExport int Engine_RefreshDevices() {
    g_cached_count = adb_list_devices(g_cached_devices, MAX_DEVICES);
    // Para cada dispositivo novo, registra no banco
    for (int i = 0; i < g_cached_count; i++) {
        if (g_cached_devices[i].authorized) {
            dispositivos_registrar(&g_cached_devices[i]);
        }
    }
    return g_cached_count;
}

DllExport int Engine_GetDeviceCount() {
    return g_cached_count;
}

DllExport void Engine_GetDevice(int index, AdbDevice* out_device) {
    if (index >= 0 && index < g_cached_count) {
        memcpy(out_device, &g_cached_devices[index], sizeof(AdbDevice));
    }
}


DllExport int Engine_GetRegisteredTotal() {
    DispositivoRegistro db[MAX_REGISTROS];
    return dispositivos_carregar(db, MAX_REGISTROS);
}

DllExport int Engine_ForceStopApp(const char* serial, const char* package_name) {
    return adb_force_stop(serial, package_name);
}

DllExport int Engine_UninstallApp(const char* serial, const char* package_name) {
    return adb_uninstall_app(serial, package_name);
}

#include "core/functions/vision7/f1_vision_tab7.h"
#include "core/functions/vision7/f2_vision_tab7.h"
#include "core/functions/vision7/f3_vision_tab7.h"
#include "core/functions/vision7/f4_vision_tab7.h"
#include "core/functions/vision7/f5_vision_tab7.h"
#include "core/functions/vision7/f6_vision_tab7.h"
#include "core/functions/vision7/f8_vision_tab7.h"

#include "core/functions/tabmulti/f1_tabmulti_m7.h"
#include "core/functions/tabmulti/f2_tabmulti_m7.h"
#include "core/functions/tabmulti/f3_tabmulti_m7.h"
#include "core/functions/tabmulti/f4_tabmulti_m7.h"
#include "core/functions/tabmulti/f5_tabmulti_m7.h"

#include "core/functions/nokia_t20/f1_nokia_t20.h"
#include "core/functions/nokia_t20/f2_nokia_t20.h"
#include "core/functions/nokia_t20/f3_nokia_t20.h"
// --- MÃ‰TODOS DE EXPORTAÃ‡ÃƒO PARA C# (DllImport) ---

// [Automação] F1: Instalação Vision 7
DllExport int Engine_ExecuteF1_Positivo(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb) {
    return V7_ExecutarPipelineCompleto(serial, apk_path, pacote_path, wp_path, package_name, login, senha, municipio, cb);
}

// [Automação] F1: Instalação Nokia T20
DllExport int Engine_ExecuteF1_NokiaT20(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb) {
    return T20_ExecutarPipelineCompleto(serial, apk_path, pacote_path, wp_path, package_name, login, senha, municipio, cb);
}

// [Automação] F2: Reinstalação Nokia T20 (Preserva apps do sistema quietos)
DllExport int Engine_ExecuteF2_NokiaT20(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb) {
    return T20_ExecutarPipelineF2(serial, apk_path, pacote_path, wp_path, package_name, login, senha, municipio, cb);
}

// [Automação] F3: Atualização de Pacote Nokia T20 (Modo Express)
DllExport int Engine_ExecuteF3_NokiaT20(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb) {
    return T20_ExecutarPipelineF3_AtualizarPacote(serial, apk_path, pacote_path, package_name, cb);
}


// [Automação] F2: Reinstalação Limpa (Vision 7)
DllExport int Engine_ExecuteF2_Reinstall(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb) {
    return F2_ExecutarPipelineCompleto(serial, apk_path, pacote_path, package_name, login, senha, municipio, cb);
}



// [Automação] F3: Atualizacao de Pacote (Recebe apk_path e pacote_path)
__declspec(dllexport) int Engine_ExecuteF3_UpdatePackage(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb) {
    if (!serial || !pacote_path || !package_name) return PIPELINE_ERROR;
    return F3_ExecutarAtualizacaoPacote(serial, apk_path, pacote_path, package_name, cb);
}

// [Automação] F4: Gestão do Aparelho (Vision 7)
DllExport int Engine_ExecuteF4_Terminal(const char* serial, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    if (cb) cb("[API] Solicitacao F4 Gestao do Aparelho recebida. Ativando Modo Restrito...");
    return F4_ExecutarFerramenta(serial, 5, cb);
}

DllExport int Engine_ExecuteF4_Ferramenta(const char* serial, int acao, LogCallback cb) {
    return F4_ExecutarFerramenta(serial, acao, cb);
}

DllExport int Engine_ExecuteF4_Wallpaper(const char* serial, const char* wp_path, LogCallback cb) {
    return F4_DefinirWallpaper(serial, wp_path, cb);
}

// [Automação] F5: Desligamento
DllExport int Engine_ExecuteF5_Shutdown(const char* serial, LogCallback cb) {
    return F5_ExecutarDesligamento(serial, cb);
}

// ---------------------------------------------------------
// NOVA FUNCAO EXPORTADA PARA F6 - Sincronizacao
// ---------------------------------------------------------
DllExport int Engine_ExecuteF6_Sincronizacao(const char* serial, LogCallback cb) {
    if (!serial) return 0;
    
    if (cb) cb("[API] Solicitacao de Sincronizacao (F6) recebida. Iniciando motor C...");
    
    int result = F6_ExecutarSincronizacao(serial, cb);
    
    if (result == PIPELINE_SUCCESS) {
        if (cb) cb("[API] Processo F6 finalizado com sucesso.");
    } else {
        if (cb) cb("[API] Erro ao executar processo F6.");
    }
    
    return result;
}

// ---------------------------------------------------------
// ENGINE_EXECUTEF8 - Conexão Ativa via Gnirehtet (Vision 7)
// ---------------------------------------------------------
DllExport int Engine_ExecuteF8_ActiveConnection(const char* serial, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    
    if (cb) cb("[API] Solicitacao de Conexao Ativa (F8 - Gnirehtet) recebida. Iniciando...");
    
    int result = V7_Etapa2_5(serial, cb);
    
    if (result == PIPELINE_SUCCESS) {
        if (cb) cb("[API] Conexao Ativa (Gnirehtet) F8 estabelecida com sucesso.");
    } else {
        if (cb) cb("[API] Erro ao ativar conexao Gnirehtet F8.");
    }
    
    return result;
}

// ---------------------------------------------------------
// ENGINE_EXECUTEF7 - Motor Exclusivo Tab-Multi (M7)
// ---------------------------------------------------------
DllExport int Engine_ExecuteF7_TabMulti(const char* serial, const char* apk_path, const char* pacote_path, const char* login, const char* senha, const char* municipio, LogCallback cb) {
    if (!serial) return 0;
    if (cb) cb("[API] Solicitacao Tab-MULTI (M7) recebida. Iniciando motor exclusivo...");
    int result = M7_ExecutarPipeline(serial, apk_path, pacote_path, NULL, login, senha, municipio, cb);
    if (result == PIPELINE_SUCCESS) {
        if (cb) cb("[API] Processo M7 finalizado com sucesso.");
    } else {
        if (cb) cb("[API] Erro ao executar processo M7.");
    }
    return result;
}

DllExport int Engine_ExecuteF2_TabMulti_Atualizar(const char* serial, const char* apk_path, const char* pacote_path, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    if (cb) cb("[API] Solicitacao M7_AtualizarPacote recebida. Iniciando motor exclusivo...");
    return M7_AtualizarPacote(serial, apk_path, pacote_path, cb);
}

DllExport int Engine_ExecuteF3_TabMulti_Atualizar(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    if (cb) cb("[API] Solicitacao F3 M7 Atualizacao de Pacote recebida...");
    return M7_ExecutarPipelineF3_AtualizarPacote(serial, apk_path, pacote_path, package_name, cb);
}

DllExport int Engine_ExecuteF3_TabMulti_Terminal(const char* serial, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    if (cb) cb("[API] Solicitacao M7_Terminal (F3) recebida. Iniciando motor exclusivo...");
    return M7_Terminal(serial, cb);
}

DllExport int Engine_ExecuteF4_TabMulti_Desligar(const char* serial, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    if (cb) cb("[API] Solicitacao M7_DesligarAparelho (F4) recebida. Iniciando motor exclusivo...");
    return M7_DesligarAparelho(serial, cb);
}

DllExport int Engine_ExecuteF5_TabMulti(const char* serial, const char* apk_path, const char* pacote_path, const char* login, const char* senha, const char* municipio, LogCallback cb) {
    if (!serial) return PIPELINE_ERROR;
    if (cb) cb("[API] Solicitacao F5 Tab-MULTI (M7) recebida. Iniciando login + tunel 30s + shutdown...");
    return M7_ExecutarPipelineF5(serial, apk_path, pacote_path, NULL, login, senha, municipio, cb);
}

// [Terminal] ExecuÃ§Ã£o Livre de Shell
DllExport void Engine_RunAdbShell(const char* serial, const char* command, char* out_log, int max_len) {
    char args[512];
    snprintf(args, sizeof(args), "-s %s shell %s", serial, command);
    char* out = adb_exec(args);
    if (out) {
        strncpy(out_log, out, max_len - 1);
        free(out);
    } else {
        strncpy(out_log, "Nenhuma resposta do ADB", max_len - 1);
    }
}

// [MÃ³dulos] GestÃ£o de MunicÃ­pios
DllExport int Engine_GetMunicipios(Municipio* out_lista, int max_items) {
    char path[512];
    municipios_get_csv_path(path, sizeof(path));
    return municipios_carregar(out_lista, max_items, path);
}

DllExport int Engine_SaveMunicipios(const Municipio* lista, int count) {
    char path[512];
    municipios_get_csv_path(path, sizeof(path));
    
    // Garantir que a pasta exista
    char dir_path[512];
    strncpy(dir_path, path, sizeof(dir_path));
    char* p = strrchr(dir_path, '\\');
    if (p) {
        *p = '\0';
        CreateDirectoryA(dir_path, NULL);
    }
    
    return municipios_salvar(lista, count, path);
}

// ============================================================
// [BotLearn] Consulta de Estatísticas para o C#
// ============================================================

// Retorna o número de alvos registrados no banco BotLearn
DllExport int Engine_Learn_GetTargetCount() {
    return g_learn_db.target_count;
}

// Estrutura de uma estratégia serializada para o C# (via DllImport)
// Offsets fixos — NÃO alterar a ordem dos campos sem sincronizar com o C#
typedef struct {
    char target_id[64];   // ID do alvo (ex: "campo_email")
    char query[128];      // String de busca no XML
    int  score;           // Score atual
    int  hits;            // Acertos acumulados
    int  misses;          // Falhas acumuladas
    char history[16];     // Últimos 10 resultados ('1' hit, '0' miss)
} LearnStrategyExport;

// Retorna a estratégia na posição linear index (varrendo todos os targets)
// index vai de 0 até (soma de strategy_count de todos os targets) - 1
// Retorna 1 se encontrou, 0 se index fora de intervalo
DllExport int Engine_Learn_GetStrategy(int index, LearnStrategyExport* out_strategy) {
    if (!out_strategy) return 0;
    int global_idx = 0;
    for (int i = 0; i < g_learn_db.target_count; i++) {
        LearnTarget* t = &g_learn_db.targets[i];
        for (int j = 0; j < t->strategy_count; j++) {
            if (global_idx == index) {
                strncpy(out_strategy->target_id, t->id,
                        sizeof(out_strategy->target_id) - 1);
                out_strategy->target_id[sizeof(out_strategy->target_id) - 1] = '\0';
                strncpy(out_strategy->query, t->strategies[j].query,
                        sizeof(out_strategy->query) - 1);
                out_strategy->query[sizeof(out_strategy->query) - 1] = '\0';
                out_strategy->score  = t->strategies[j].score;
                out_strategy->hits   = t->strategies[j].hits;
                out_strategy->misses = t->strategies[j].misses;
                strncpy(out_strategy->history, t->strategies[j].history, sizeof(out_strategy->history) - 1);
                out_strategy->history[sizeof(out_strategy->history) - 1] = '\0';
                return 1;
            }
            global_idx++;
        }
    }
    return 0;
}

// Retorna o total de estratégias somando todos os targets
DllExport int Engine_Learn_GetStrategyCount() {
    int total = 0;
    for (int i = 0; i < g_learn_db.target_count; i++) {
        total += g_learn_db.targets[i].strategy_count;
    }
    return total;
}

// Reseta completamente o banco BotLearn (scores, coords, devices)
// e persiste o estado inicial em disco imediatamente
DllExport int Engine_Learn_ResetDB() {
    memset(&g_learn_db, 0, sizeof(g_learn_db));
    g_learn_db.dirty = 1;
    learn_db_save();   // Salva o JSON vazio primeiro (no-op se Estático)
    learn_db_load();   // Recarrega os defaults (seed inicial)
    return 1;
}

// Define o modo da IA: 0 = Estático/Determinístico, 1 = Ativo/Aprendiz
// Retorna o modo efetivo após a troca (recarrega overlay se Ativo)
DllExport int Engine_Learn_SetActiveMode(int active) {
    learn_set_active_mode(active);
    learn_db_load();
    return learn_get_active_mode();
}

// Retorna o modo atual da IA: 0 = Estático, 1 = Ativo
DllExport int Engine_Learn_GetActiveMode() {
    return learn_get_active_mode();
}

// ============================================================================
// [Nova Função] Captura de Endereço MAC Wi-Fi via ADB
// ============================================================================
// A implementacao real vive no driver (adb_get_mac), que reproduz o mesmo
// ciclo da ferramenta de captura: tenta a leitura direta, e se a interface
// wlan0 estiver inativa, liga o Wi-Fi, extrai o endereco e restaura o
// estado original (o pipeline exige o Wi-Fi desligado).
// Retorna 1 se obteve MAC válido, 0 caso contrário.
// ============================================================================
DllExport int Engine_CapturarMAC(const char* serial, char* out_mac, int max_len) {
    if (!serial || !out_mac || max_len <= 0) return 0;
    return adb_get_mac(serial, out_mac, max_len);
}


