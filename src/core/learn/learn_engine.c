#include "learn_engine.h"
#include "learn_db.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// ============================================================
// InnoLearn — Motor de Decisão (Score Engine)
// Implementa a busca ordenada por score, registro de hit/miss
// e auto-discovery de novas estratégias.
// Autor: Jordan Araujo | InnovPlay
// ============================================================

// --- Helper Interno: Extrai centroide do XML (mesma lógica do f1_geraldata.c)
// Retorna 1 se achou, 0 se não. Respeita skip_count para pular ocorrências.
static int xml_get_centroid(const char* xml, const char* search_str,
                             int* out_cx, int* out_cy, int skip_count) {
    if (!xml || !search_str) return 0;
    const char* p = xml;
    int matches = 0;
    while ((p = strstr(p, search_str)) != NULL) {
        if (matches == skip_count) {
            const char* b_start = strstr(p, "bounds=\"[");
            if (b_start) {
                int x1 = 0, y1 = 0, x2 = 0, y2 = 0;
                if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"",
                           &x1, &y1, &x2, &y2) == 4) {
                    
                    // Checa se o nó correspondente tem clickable="false"
                    int is_unclickable = 0;
                    const char* tag_start = p;
                    while (tag_start > xml && *tag_start != '<' && (p - tag_start < 300)) {
                        tag_start--;
                    }
                    const char* tag_end = strstr(p, "/>");
                    if (!tag_end || (tag_end - p > 500)) tag_end = p + 200;

                    if (tag_start < tag_end) {
                        char node_buf[600];
                        int nlen = (int)(tag_end - tag_start);
                        if (nlen > 0 && nlen < (int)sizeof(node_buf)) {
                            strncpy(node_buf, tag_start, nlen);
                            node_buf[nlen] = '\0';
                            if (strstr(node_buf, "clickable=\"false\"")) {
                                is_unclickable = 1;
                            }
                        }
                    }

                    // Se for um rótulo não clicável (ex: "Município"), busca o próximo nó clicável logo abaixo
                    if (is_unclickable) {
                        const char* next_click = strstr(p, "clickable=\"true\"");
                        if (next_click && (next_click - p < 1200)) {
                            const char* nb_start = strstr(next_click, "bounds=\"[");
                            if (!nb_start) {
                                const char* rew = next_click;
                                while (rew > p && *rew != '<') rew--;
                                nb_start = strstr(rew, "bounds=\"[");
                            }
                            if (nb_start && (nb_start - p < 1500)) {
                                int nx1 = 0, ny1 = 0, nx2 = 0, ny2 = 0;
                                if (sscanf(nb_start, "bounds=\"[%d,%d][%d,%d]\"",
                                           &nx1, &ny1, &nx2, &ny2) == 4) {
                                    if (out_cx) *out_cx = (nx1 + nx2) / 2;
                                    if (out_cy) *out_cy = (ny1 + ny2) / 2;
                                    return 1;
                                }
                            }
                        }
                        // Fallback relativo: se não achou clickable="true", clica no campo logo abaixo da label
                        int h = y2 - y1;
                        if (h <= 0) h = 20;
                        if (out_cx) *out_cx = (x1 + x2) / 2;
                        if (out_cy) *out_cy = y2 + (h / 2) + 15;
                        return 1;
                    }

                    if (out_cx) *out_cx = (x1 + x2) / 2;
                    if (out_cy) *out_cy = (y1 + y2) / 2;
                    return 1;
                }
            }
            return 0;
        }
        matches++;
        p++;
    }
    return 0;
}

// ============================================================
// Ordena strategies por score (Insertion Sort — N <= 16)
// ============================================================
void learn_sort_strategies(LearnTarget* target) {
    for (int i = 1; i < target->strategy_count; i++) {
        LearnStrategy key = target->strategies[i];
        int j = i - 1;
        while (j >= 0 && target->strategies[j].score < key.score) {
            target->strategies[j + 1] = target->strategies[j];
            j--;
        }
        target->strategies[j + 1] = key;
    }
}

// ============================================================
// Localiza o elemento no XML usando estratégias ordenadas
// ============================================================
LearnFindResult learn_find_element(const char* target_id,
                                   const char* xml,
                                   int         skip_count,
                                   LogCallback cb) {
    LearnFindResult result;
    memset(&result, 0, sizeof(LearnFindResult));

    if (!xml || !target_id) return result;

    LearnTarget* target = learn_db_get_or_create_target(target_id);
    if (!target || target->strategy_count == 0) {
        if (cb) {
            char msg[256];
            snprintf(msg, sizeof(msg),
                     "[InnoLearn] AVISO: Nenhuma estrategia para '%s'.",
                     target_id);
            cb(msg);
        }
        return result;
    }

    // Ordena por score antes de tentar
    learn_sort_strategies(target);

    if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg),
                 "[InnoLearn] Buscando '%s' (%d estrategias, melhor score: %d)...",
                 target_id, target->strategy_count,
                 target->strategies[0].score);
        cb(msg);
    }

    // Testa cada estratégia em ordem de score
    for (int i = 0; i < target->strategy_count; i++) {
        LearnStrategy* s = &target->strategies[i];
        int cx = 0, cy = 0;

        if (xml_get_centroid(xml, s->query, &cx, &cy, skip_count)) {
            // SUCESSO: elemento encontrado com esta estratégia
            result.found = 1;
            result.cx    = cx;
            result.cy    = cy;
            strncpy(result.winning_strategy, s->query,
                    LEARN_MAX_QUERY_LEN - 1);

            if (cb) {
                char msg[384];
                snprintf(msg, sizeof(msg),
                         "[InnoLearn] '%s' achado! Estrategia [%d/%d]:"
                         " \"%s\" (score=%d) -> (%d,%d)",
                         target_id, i + 1, target->strategy_count,
                         s->query, s->score, cx, cy);
                cb(msg);
            }
            return result;
        } else {
            // Esta estratégia falhou, tenta a próxima
            if (cb) {
                char msg[256];
                snprintf(msg, sizeof(msg),
                         "[InnoLearn]   -> Estrategia '%s' (score=%d): miss.",
                         s->query, s->score);
                cb(msg);
            }
        }
    }

    // Fallback de coordenada aprendida desativado (InnoLearn Estático)

    // Nada funcionou
    if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg),
                 "[InnoLearn] FALHA TOTAL: '%s' nao encontrado com"
                 " nenhuma estrategia.", target_id);
        cb(msg);
    }
    return result;
}

// ============================================================
// Auxiliar: Insere o resultado no histórico (deslocamento dir)
// ============================================================
static void append_history(char* hist, char result) {
    int len = (int)strlen(hist);
    if (len >= 10) {
        memmove(hist, hist + 1, 9);
        hist[9] = result;
        hist[10] = '\0';
    } else {
        hist[len] = result;
        hist[len + 1] = '\0';
    }
}

// ============================================================
// Registra SUCESSO — sobe o score da estratégia vencedora
// ============================================================
void learn_record_hit(const char* target_id, const char* strategy_query) {
    if (!target_id || !strategy_query) return;
    LearnTarget* target = learn_db_get_or_create_target(target_id);
    if (!target) return;

    for (int i = 0; i < target->strategy_count; i++) {
        if (strcmp(target->strategies[i].query, strategy_query) == 0) {
            target->strategies[i].hits++;
            target->strategies[i].score += 5;
            if (target->strategies[i].score > 100) target->strategies[i].score = 100;
            append_history(target->strategies[i].history, 'H');
            g_learn_db.dirty = 1;
            learn_db_save();
            return;
        }
    }
}

// ============================================================
// Registra FALHA — baixa o score da estratégia
// ============================================================
void learn_record_miss(const char* target_id, const char* strategy_query) {
    if (!target_id || !strategy_query) return;
    LearnTarget* target = learn_db_find_target(target_id);
    if (!target) return;

    for (int i = 0; i < target->strategy_count; i++) {
        if (strcmp(target->strategies[i].query, strategy_query) == 0) {
            target->strategies[i].misses++;
            target->strategies[i].score -= 10;
            if (target->strategies[i].score < -100) target->strategies[i].score = -100;
            append_history(target->strategies[i].history, 'M');
            g_learn_db.dirty = 1;
            learn_db_save();
            return;
        }
    }
}

// ============================================================
// Retorna coordenada fallback aprendida
// ============================================================
int learn_get_fallback_coord(const char* target_id, int* out_x, int* out_y) {
    if (!target_id || !out_x || !out_y) return 0;
    LearnCoord* c = learn_db_find_coord(target_id);
    if (c && c->last_success == 1) {
        *out_x = c->x;
        *out_y = c->y;
        return 1;
    }
    return 0;
}

// ============================================================
// Salva coordenada fallback que funcionou
// ============================================================
void learn_save_fallback_coord(const char* target_id,
                               int x, int y, int success) {
    if (!target_id || x <= 0 || y <= 0) return;
    LearnCoord* c = learn_db_get_or_create_coord(target_id);
    if (!c) return;

    c->x = x;
    c->y = y;
    c->uses++;
    c->last_success = success ? 1 : 0;
    g_learn_db.dirty = 1;
    learn_db_save();
}

// ============================================================
// Auto-Discovery: Adiciona nova estratégia ao banco (Fazer o mapa crescer)
// ============================================================
void learn_auto_discover(const char* target_id, const char* new_query) {
    if (!target_id || !new_query || strlen(new_query) == 0) return;
    LearnTarget* target = learn_db_get_or_create_target(target_id);
    if (!target) return;

    for (int i = 0; i < target->strategy_count; i++) {
        if (strcmp(target->strategies[i].query, new_query) == 0) return;
    }

    if (target->strategy_count < LEARN_MAX_STRATEGIES) {
        LearnStrategy* s = &target->strategies[target->strategy_count++];
        memset(s, 0, sizeof(LearnStrategy));
        strncpy(s->query, new_query, LEARN_MAX_QUERY_LEN - 1);
        s->score = 50;
        s->hits = 1;
        s->misses = 0;
        append_history(s->history, 'H');
        g_learn_db.dirty = 1;
        learn_db_save();
    }
}
