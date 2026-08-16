#ifndef LEARN_ENGINE_H
#define LEARN_ENGINE_H

// ============================================================
// InnoLearn — Motor de Decisão (Score Engine)
// Interface pública para os motores F1/F2 consultarem e
// retroalimentarem o sistema de aprendizado.
// Autor: Jordan Araujo | InnovPlay
// ============================================================

#include "learn_db.h"
#include "../functions/pipeline.h"


// --- Resultado de uma busca inteligente ---
typedef struct {
    int   found;                          // 1 se elemento foi achado
    int   cx;                             // Centro X do elemento
    int   cy;                             // Centro Y do elemento
    char  winning_strategy[LEARN_MAX_QUERY_LEN]; // Query que funcionou
    int   used_fallback_coord;            // 1 se usou coord aprendida
} LearnFindResult;

// ============================================================
// Função Principal — Localiza um elemento no XML usando as
// estratégias ordenadas por score do banco de conhecimento.
// Retorna o resultado com a estratégia vencedora preenchida.
//
// Parâmetros:
//   target_id  — ID do alvo (ex: "campo_email")
//   xml        — String XML do UIAutomator dump
//   skip_count — Quantas ocorrências pular antes de retornar
//                (0 = primeira, 1 = segunda, etc.)
//   cb         — Callback de log (pode ser NULL)
// ============================================================
LearnFindResult learn_find_element(const char* target_id,
                                   const char* xml,
                                   int         skip_count,
                                   LogCallback cb);

// ============================================================
// Registra sucesso de uma estratégia (sobe o score)
// Chame APÓS confirmar que o clique teve efeito na tela.
// ============================================================
void learn_record_hit(const char* target_id, const char* strategy_query);

// ============================================================
// Registra falha de uma estratégia (baixa o score)
// ============================================================
void learn_record_miss(const char* target_id, const char* strategy_query);

// ============================================================
// Retorna a coordenada de fallback aprendida para um alvo.
// Retorna 1 e preenche x/y se existir, 0 caso contrário.
// ============================================================
int learn_get_fallback_coord(const char* target_id, int* out_x, int* out_y);

// ============================================================
// Registra uma coordenada que funcionou como fallback.
// Atualiza ou cria a entrada correspondente no banco.
// ============================================================
void learn_save_fallback_coord(const char* target_id,
                               int x, int y, int success);

// ============================================================
// Auto-Discovery: Registra uma nova estratégia descoberta
// automaticamente que não estava no banco original.
// Score inicial = LEARN_SCORE_INITIAL.
// ============================================================
void learn_auto_discover(const char* target_id, const char* new_query);

// ============================================================
// Ordena as strategies de um target por score (decrescente).
// Chamado internamente antes de cada busca — garante que a
// melhor estratégia é sempre testada primeiro.
// ============================================================
void learn_sort_strategies(LearnTarget* target);

#endif // LEARN_ENGINE_H
