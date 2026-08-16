#ifndef LEARN_DB_H
#define LEARN_DB_H

// ============================================================
// InnoLearn — Motor de Persistência de Conhecimento de UI
// Armazena estratégias de localização de elementos, scores e
// coordenadas fallback aprendidas em execuções anteriores.
// Autor: Jordan Araujo | InnovPlay
// ============================================================

#define LEARN_MAX_STRATEGIES     16
#define LEARN_MAX_TARGETS        32
#define LEARN_MAX_COORDS         32
#define LEARN_MAX_DEVICES        16
#define LEARN_MAX_ID_LEN         64
#define LEARN_MAX_QUERY_LEN     128
#define LEARN_MAX_MODEL_LEN      64

#define LEARN_SCORE_INITIAL       0
#define LEARN_SCORE_HIT          10
#define LEARN_SCORE_MISS          8
#define LEARN_SCORE_MIN        -100
#define LEARN_SCORE_MAX         100

// --- Uma única estratégia de busca para um elemento ---
typedef struct {
    char   query[LEARN_MAX_QUERY_LEN]; // String buscada no XML do UIAutomator
    int    score;                      // Confiança atual (1–200)
    int    hits;                       // Quantidade de vezes que funcionou
    int    misses;                     // Quantidade de vezes que falhou
    char   history[16];                // Últimos 10 resultados ('1' hit, '0' miss)
} LearnStrategy;

// --- Um alvo de UI com N estratégias ordenadas por score ---
typedef struct {
    char           id[LEARN_MAX_ID_LEN];               // Ex: "campo_email"
    LearnStrategy  strategies[LEARN_MAX_STRATEGIES];
    int            strategy_count;
} LearnTarget;

// --- Coordenada de fallback aprendida ---
typedef struct {
    char  id[LEARN_MAX_ID_LEN];  // Ex: "campo_email_fallback"
    int   x;
    int   y;
    int   uses;
    int   last_success;          // 1 = última vez funcionou, 0 = falhou
} LearnCoord;

// --- Perfil de dispositivo ---
typedef struct {
    char  model[LEARN_MAX_MODEL_LEN];
    int   screen_width;
    int   screen_height;
    int   total_runs;
    int   success_runs;
} LearnDeviceProfile;

// --- Banco de Conhecimento Completo ---
typedef struct {
    int                version;
    LearnTarget        targets[LEARN_MAX_TARGETS];
    int                target_count;
    LearnCoord         coords[LEARN_MAX_COORDS];
    int                coord_count;
    LearnDeviceProfile devices[LEARN_MAX_DEVICES];
    int                device_count;
    int                dirty; // 1 = houve alterações que precisam ser salvas
} LearnDB;

// --- Variável Global do Banco (acesso direto pelos motores) ---
extern LearnDB g_learn_db;

// --- Interface de Persistência ---

// Carrega o banco de knowledge de data/inno_learn.json
// Retorna 1 se carregou com sucesso, 0 se criou banco novo
int learn_db_load(void);

// Salva o banco em data/inno_learn.json
// Retorna 1 se salvou com sucesso, 0 se falhou
int learn_db_save(void);

// Retorna ponteiro para um alvo pelo ID (NULL se não existir)
LearnTarget* learn_db_find_target(const char* id);

// Retorna ponteiro para um alvo pelo ID, criando se não existir
LearnTarget* learn_db_get_or_create_target(const char* id);

// Retorna ponteiro para coord pelo ID (NULL se não existir)
LearnCoord* learn_db_find_coord(const char* id);

// Retorna ponteiro para coord pelo ID, criando se não existir
LearnCoord* learn_db_get_or_create_coord(const char* id);

// Registra/atualiza perfil de dispositivo
void learn_db_register_device(const char* model, int screen_w, int screen_h);

// Marca execução como concluída (sucesso = 1) no perfil do dispositivo
void learn_db_record_run(const char* model, int success);

#endif // LEARN_DB_H
