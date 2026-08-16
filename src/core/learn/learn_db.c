#include "learn_db.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

// ============================================================
// InnoLearn — Motor de Persistência (Parser/Writer JSON)
// Parser JSON minimalista sem dependência externa.
// Formato alvo: data/inno_learn.json
// Autor: Jordan Araujo | InnovPlay
// ============================================================

// Instância global do banco de conhecimento
LearnDB g_learn_db = {0};

// --- Helpers Internos ---

// Retorna o caminho absoluto do arquivo de banco de dados
static void get_db_path(char* out_path, int max_len) {
    char exe_dir[MAX_PATH];
    GetModuleFileNameA(NULL, exe_dir, MAX_PATH);
    char* last = strrchr(exe_dir, '\\');
    if (last) *last = '\0';

    // Tenta caminho Release primeiro, depois fallback para desenvolvimento
    char candidate[MAX_PATH];
    snprintf(candidate, sizeof(candidate), "%s\\data\\inno_learn.json", exe_dir);
    if (GetFileAttributesA(candidate) == INVALID_FILE_ATTRIBUTES) {
        // Verifica se o diretório data existe no Release
        char data_dir[MAX_PATH];
        snprintf(data_dir, sizeof(data_dir), "%s\\data", exe_dir);
        if (GetFileAttributesA(data_dir) == INVALID_FILE_ATTRIBUTES) {
            // Fallback para raiz do projeto em desenvolvimento
            snprintf(candidate, sizeof(candidate),
                     "%s\\..\\..\\data\\inno_learn.json", exe_dir);
        }
    }
    strncpy(out_path, candidate, max_len - 1);
    out_path[max_len - 1] = '\0';
}

// Garante que o diretório data/ existe
static void ensure_data_dir(const char* db_path) {
    char dir[MAX_PATH];
    strncpy(dir, db_path, sizeof(dir));
    char* last = strrchr(dir, '\\');
    if (last) {
        *last = '\0';
        CreateDirectoryA(dir, NULL);
    }
}

// --- Helper de parsing JSON manual ---
// Extrai o valor de uma chave string simples: "key": "value"
static int json_get_string(const char* json, const char* key,
                            char* out, int out_max) {
    char search[128];
    snprintf(search, sizeof(search), "\"%s\":", key);
    const char* p = strstr(json, search);
    if (!p) return 0;
    p += strlen(search);
    while (*p == ' ' || *p == '\t') p++;
    if (*p != '"') return 0;
    p++;
    int i = 0;
    while (*p && *p != '"' && i < out_max - 1) {
        // Trata escape básico
        if (*p == '\\' && *(p + 1)) { p++; }
        out[i++] = *p++;
    }
    out[i] = '\0';
    return 1;
}

// Extrai o valor de uma chave inteira: "key": 42
static int json_get_int(const char* json, const char* key, int* out) {
    char search[128];
    snprintf(search, sizeof(search), "\"%s\":", key);
    const char* p = strstr(json, search);
    if (!p) return 0;
    p += strlen(search);
    while (*p == ' ' || *p == '\t') p++;
    *out = atoi(p);
    return 1;
}

// --- Injeta os seeds (DNA) sem limpar o banco (seguro para atualizacoes) ---
static void learn_db_inject_seeds(void) {

    // ---------------------------------------------------------------
    // Seed: spinner_municipio (dropdown de seleção de município)
    // ---------------------------------------------------------------
    LearnTarget* t_mun = learn_db_get_or_create_target("spinner_municipio");
    if (t_mun && t_mun->strategy_count == 0) {
        strncpy(t_mun->strategies[0].query, "content-desc=\"Município\"", LEARN_MAX_QUERY_LEN - 1);
        t_mun->strategies[0].score = 95;
        strncpy(t_mun->strategies[1].query, "Município", LEARN_MAX_QUERY_LEN - 1);
        t_mun->strategies[1].score = 90;
        strncpy(t_mun->strategies[2].query, "class=\"android.widget.Button\"", LEARN_MAX_QUERY_LEN - 1);
        t_mun->strategies[2].score = 85;
        strncpy(t_mun->strategies[3].query, "class=\"android.widget.Spinner\"", LEARN_MAX_QUERY_LEN - 1);
        t_mun->strategies[3].score = 55;
        strncpy(t_mun->strategies[4].query, "Selecione", LEARN_MAX_QUERY_LEN - 1);
        t_mun->strategies[4].score = 45;
        strncpy(t_mun->strategies[5].query, "text=\"Selecione", LEARN_MAX_QUERY_LEN - 1);
        t_mun->strategies[5].score = 40;
        t_mun->strategy_count = 6;
    }

    // ---------------------------------------------------------------
    // Seed: campo_email (campo de e-mail na tela de login)
    // ---------------------------------------------------------------
    LearnTarget* t_email = learn_db_get_or_create_target("campo_email");
    if (t_email && t_email->strategy_count == 0) {
        strncpy(t_email->strategies[0].query, "E-mail ou Usu",
                LEARN_MAX_QUERY_LEN - 1);
        t_email->strategies[0].score = 70;
        strncpy(t_email->strategies[1].query, "Digite seu e-mail",
                LEARN_MAX_QUERY_LEN - 1);
        t_email->strategies[1].score = 65;
        strncpy(t_email->strategies[2].query, "content-desc=\"E-mail",
                LEARN_MAX_QUERY_LEN - 1);
        t_email->strategies[2].score = 60;
        strncpy(t_email->strategies[3].query, "Digite seu",
                LEARN_MAX_QUERY_LEN - 1);
        t_email->strategies[3].score = 50;
        strncpy(t_email->strategies[4].query, "aplicador",
                LEARN_MAX_QUERY_LEN - 1);
        t_email->strategies[4].score = 45;
        strncpy(t_email->strategies[5].query, "class=\"android.widget.EditText\"",
                LEARN_MAX_QUERY_LEN - 1);
        t_email->strategies[5].score = 90;
        t_email->strategy_count = 6;
    }

    // ---------------------------------------------------------------
    // Seed: campo_senha (campo de senha na tela de login)
    // ---------------------------------------------------------------
    LearnTarget* t_senha = learn_db_get_or_create_target("campo_senha");
    if (t_senha && t_senha->strategy_count == 0) {
        strncpy(t_senha->strategies[0].query, "password=\"true\"",
                LEARN_MAX_QUERY_LEN - 1);
        t_senha->strategies[0].score = 75;
        strncpy(t_senha->strategies[1].query, "content-desc=\"Senha\"",
                LEARN_MAX_QUERY_LEN - 1);
        t_senha->strategies[1].score = 65;
        strncpy(t_senha->strategies[2].query, "Senha",
                LEARN_MAX_QUERY_LEN - 1);
        t_senha->strategies[2].score = 50;
        strncpy(t_senha->strategies[3].query, "senha",
                LEARN_MAX_QUERY_LEN - 1);
        t_senha->strategies[3].score = 45;
        strncpy(t_senha->strategies[4].query, "........",
                LEARN_MAX_QUERY_LEN - 1);
        t_senha->strategies[4].score = 35;
        t_senha->strategy_count = 5;
    }

    // ---------------------------------------------------------------
    // Seed: botao_entrar (botão de login/entrar)
    // ---------------------------------------------------------------
    LearnTarget* t_btn = learn_db_get_or_create_target("botao_entrar");
    if (t_btn && t_btn->strategy_count == 0) {
        strncpy(t_btn->strategies[0].query, "content-desc=\"Entrar\"",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[0].score = 80;
        strncpy(t_btn->strategies[1].query, "Entrar",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[1].score = 65;
        strncpy(t_btn->strategies[2].query, "ENTRAR",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[2].score = 50;
        strncpy(t_btn->strategies[3].query, "Login",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[3].score = 40;
        strncpy(t_btn->strategies[4].query, "Acessar",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[4].score = 35;
        strncpy(t_btn->strategies[5].query, "ACESSAR",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[5].score = 30;
        strncpy(t_btn->strategies[6].query, "class=\"android.widget.Button\"",
                LEARN_MAX_QUERY_LEN - 1);
        t_btn->strategies[6].score = 20;
        t_btn->strategy_count = 7;
    }

    // ===============================================================
    // ISOLAMENTO F7: Tab-MULTI (Seed específico para não misturar)
    // ===============================================================
    
    // Seed: f7_spinner_municipio
    LearnTarget* f7_mun = learn_db_get_or_create_target("f7_spinner_municipio");
    if (f7_mun && f7_mun->strategy_count == 0) {
        strncpy(f7_mun->strategies[0].query, "content-desc=\"Município\"", LEARN_MAX_QUERY_LEN - 1);
        f7_mun->strategies[0].score = 95;
        strncpy(f7_mun->strategies[1].query, "Município", LEARN_MAX_QUERY_LEN - 1);
        f7_mun->strategies[1].score = 90;
        strncpy(f7_mun->strategies[2].query, "class=\"android.widget.Button\"", LEARN_MAX_QUERY_LEN - 1);
        f7_mun->strategies[2].score = 85;
        strncpy(f7_mun->strategies[3].query, "class=\"android.widget.Spinner\"", LEARN_MAX_QUERY_LEN - 1);
        f7_mun->strategies[3].score = 55;
        strncpy(f7_mun->strategies[4].query, "Selecione", LEARN_MAX_QUERY_LEN - 1);
        f7_mun->strategies[4].score = 45;
        f7_mun->strategy_count = 5;
    }

    // Seed: f7_campo_email
    LearnTarget* f7_email = learn_db_get_or_create_target("f7_campo_email");
    if (f7_email && f7_email->strategy_count == 0) {
        strncpy(f7_email->strategies[0].query, "class=\"android.widget.EditText\"", LEARN_MAX_QUERY_LEN - 1);
        f7_email->strategies[0].score = 90;
        strncpy(f7_email->strategies[1].query, "E-mail ou Usu", LEARN_MAX_QUERY_LEN - 1);
        f7_email->strategies[1].score = 70;
        strncpy(f7_email->strategies[2].query, "Digite seu e-mail", LEARN_MAX_QUERY_LEN - 1);
        f7_email->strategies[2].score = 65;
        strncpy(f7_email->strategies[3].query, "content-desc=\"E-mail", LEARN_MAX_QUERY_LEN - 1);
        f7_email->strategies[3].score = 60;
        f7_email->strategy_count = 4;
    }

    // Seed: f7_campo_senha
    LearnTarget* f7_senha = learn_db_get_or_create_target("f7_campo_senha");
    if (f7_senha && f7_senha->strategy_count == 0) {
        strncpy(f7_senha->strategies[0].query, "password=\"true\"", LEARN_MAX_QUERY_LEN - 1);
        f7_senha->strategies[0].score = 75;
        strncpy(f7_senha->strategies[1].query, "content-desc=\"Senha\"", LEARN_MAX_QUERY_LEN - 1);
        f7_senha->strategies[1].score = 65;
        strncpy(f7_senha->strategies[2].query, "Senha", LEARN_MAX_QUERY_LEN - 1);
        f7_senha->strategies[2].score = 50;
        f7_senha->strategy_count = 3;
    }

    // Seed: f7_botao_entrar
    LearnTarget* f7_btn = learn_db_get_or_create_target("f7_botao_entrar");
    if (f7_btn && f7_btn->strategy_count == 0) {
        strncpy(f7_btn->strategies[0].query, "content-desc=\"Entrar\"", LEARN_MAX_QUERY_LEN - 1);
        f7_btn->strategies[0].score = 80;
        strncpy(f7_btn->strategies[1].query, "Entrar", LEARN_MAX_QUERY_LEN - 1);
        f7_btn->strategies[1].score = 65;
        strncpy(f7_btn->strategies[2].query, "ENTRAR", LEARN_MAX_QUERY_LEN - 1);
        f7_btn->strategies[2].score = 50;
        strncpy(f7_btn->strategies[3].query, "Acessar", LEARN_MAX_QUERY_LEN - 1);
        f7_btn->strategies[3].score = 35;
        strncpy(f7_btn->strategies[4].query, "ACESSAR", LEARN_MAX_QUERY_LEN - 1);
        f7_btn->strategies[4].score = 30;
        strncpy(f7_btn->strategies[5].query, "class=\"android.widget.Button\"", LEARN_MAX_QUERY_LEN - 1);
        f7_btn->strategies[5].score = 20;
    }

    // ===============================================================
    // ISOLAMENTO V7: Positivo Tab Vision 7 (Seeds V7)
    // ===============================================================

    // Seed: v7_spinner_municipio
    LearnTarget* v7_mun = learn_db_get_or_create_target("v7_spinner_municipio");
    if (v7_mun && v7_mun->strategy_count == 0) {
        strncpy(v7_mun->strategies[0].query, "content-desc=\"Município\"", LEARN_MAX_QUERY_LEN - 1);
        v7_mun->strategies[0].score = 95;
        strncpy(v7_mun->strategies[1].query, "Município", LEARN_MAX_QUERY_LEN - 1);
        v7_mun->strategies[1].score = 90;
        strncpy(v7_mun->strategies[2].query, "class=\"android.widget.Button\"", LEARN_MAX_QUERY_LEN - 1);
        v7_mun->strategies[2].score = 85;
        strncpy(v7_mun->strategies[3].query, "class=\"android.widget.Spinner\"", LEARN_MAX_QUERY_LEN - 1);
        v7_mun->strategies[3].score = 55;
        strncpy(v7_mun->strategies[4].query, "Selecione", LEARN_MAX_QUERY_LEN - 1);
        v7_mun->strategies[4].score = 45;
        v7_mun->strategy_count = 5;
    }

    // Seed: v7_campo_email
    LearnTarget* v7_email = learn_db_get_or_create_target("v7_campo_email");
    if (v7_email && v7_email->strategy_count == 0) {
        strncpy(v7_email->strategies[0].query, "class=\"android.widget.EditText\"", LEARN_MAX_QUERY_LEN - 1);
        v7_email->strategies[0].score = 90;
        strncpy(v7_email->strategies[1].query, "E-mail ou Usu", LEARN_MAX_QUERY_LEN - 1);
        v7_email->strategies[1].score = 70;
        strncpy(v7_email->strategies[2].query, "Digite seu e-mail", LEARN_MAX_QUERY_LEN - 1);
        v7_email->strategies[2].score = 65;
        strncpy(v7_email->strategies[3].query, "content-desc=\"E-mail", LEARN_MAX_QUERY_LEN - 1);
        v7_email->strategies[3].score = 60;
        v7_email->strategy_count = 4;
    }

    // Seed: v7_campo_senha
    LearnTarget* v7_senha = learn_db_get_or_create_target("v7_campo_senha");
    if (v7_senha && v7_senha->strategy_count == 0) {
        strncpy(v7_senha->strategies[0].query, "password=\"true\"", LEARN_MAX_QUERY_LEN - 1);
        v7_senha->strategies[0].score = 75;
        strncpy(v7_senha->strategies[1].query, "content-desc=\"Senha\"", LEARN_MAX_QUERY_LEN - 1);
        v7_senha->strategies[1].score = 65;
        strncpy(v7_senha->strategies[2].query, "Senha", LEARN_MAX_QUERY_LEN - 1);
        v7_senha->strategies[2].score = 50;
        v7_senha->strategy_count = 3;
    }

    // Seed: v7_botao_entrar
    LearnTarget* v7_btn = learn_db_get_or_create_target("v7_botao_entrar");
    if (v7_btn && v7_btn->strategy_count == 0) {
        strncpy(v7_btn->strategies[0].query, "content-desc=\"Entrar\"", LEARN_MAX_QUERY_LEN - 1);
        v7_btn->strategies[0].score = 80;
        strncpy(v7_btn->strategies[1].query, "Entrar", LEARN_MAX_QUERY_LEN - 1);
        v7_btn->strategies[1].score = 65;
        strncpy(v7_btn->strategies[2].query, "ENTRAR", LEARN_MAX_QUERY_LEN - 1);
        v7_btn->strategies[2].score = 50;
        strncpy(v7_btn->strategies[3].query, "Acessar", LEARN_MAX_QUERY_LEN - 1);
        v7_btn->strategies[3].score = 35;
        strncpy(v7_btn->strategies[4].query, "class=\"android.widget.Button\"", LEARN_MAX_QUERY_LEN - 1);
        v7_btn->strategies[4].score = 20;
        v7_btn->strategy_count = 5;
    }

    // ---------------------------------------------------------------
    // Seed: botao_importar_zip (menu lateral após login - F1 Etapa 5)
    // ---------------------------------------------------------------
    LearnTarget* t_zip = learn_db_get_or_create_target("botao_importar_zip");
    if (t_zip && t_zip->strategy_count == 0) {
        strncpy(t_zip->strategies[0].query, "Importar ZIP",
                LEARN_MAX_QUERY_LEN - 1);
        t_zip->strategies[0].score = 60;
        strncpy(t_zip->strategies[1].query, "Importar pacote",
                LEARN_MAX_QUERY_LEN - 1);
        t_zip->strategies[1].score = 55;
        strncpy(t_zip->strategies[2].query, "Importar",
                LEARN_MAX_QUERY_LEN - 1);
        t_zip->strategies[2].score = 45;
        strncpy(t_zip->strategies[3].query, "importar zip",
                LEARN_MAX_QUERY_LEN - 1);
        t_zip->strategies[3].score = 40;
        strncpy(t_zip->strategies[4].query, "importar",
                LEARN_MAX_QUERY_LEN - 1);
        t_zip->strategies[4].score = 35;
        t_zip->strategy_count = 5;
    }

    // ---------------------------------------------------------------
    // Seed: f1_btn_procurar_arquivo (gerenciador de arquivos - F1 Etapa 6)
    // ---------------------------------------------------------------
    LearnTarget* t_proc = learn_db_get_or_create_target("f1_btn_procurar_arquivo");
    if (t_proc && t_proc->strategy_count == 0) {
        strncpy(t_proc->strategies[0].query, "Procurar arquivo",
                LEARN_MAX_QUERY_LEN - 1);
        t_proc->strategies[0].score = 60;
        strncpy(t_proc->strategies[1].query, "PROCURAR ARQUIVO",
                LEARN_MAX_QUERY_LEN - 1);
        t_proc->strategies[1].score = 55;
        strncpy(t_proc->strategies[2].query, "Procurar",
                LEARN_MAX_QUERY_LEN - 1);
        t_proc->strategies[2].score = 45;
        strncpy(t_proc->strategies[3].query, "PROCURAR",
                LEARN_MAX_QUERY_LEN - 1);
        t_proc->strategies[3].score = 40;
        strncpy(t_proc->strategies[4].query, "Browse",
                LEARN_MAX_QUERY_LEN - 1);
        t_proc->strategy_count = 5;
    }

    // ===============================================================
    // ISOLAMENTO NOKIA T20: Seeds Nokia T20 (Android 11)
    // ===============================================================
    LearnTarget* t20_mun = learn_db_get_or_create_target("t20_spinner_municipio");
    if (t20_mun && t20_mun->strategy_count == 0) {
        strncpy(t20_mun->strategies[0].query, "content-desc=\"Município\"", LEARN_MAX_QUERY_LEN - 1);
        t20_mun->strategies[0].score = 95;
        strncpy(t20_mun->strategies[1].query, "Município", LEARN_MAX_QUERY_LEN - 1);
        t20_mun->strategies[1].score = 90;
        strncpy(t20_mun->strategies[2].query, "class=\"android.widget.Spinner\"", LEARN_MAX_QUERY_LEN - 1);
        t20_mun->strategies[2].score = 80;
        t20_mun->strategy_count = 3;
    }

    LearnTarget* t20_email = learn_db_get_or_create_target("t20_campo_email");
    if (t20_email && t20_email->strategy_count == 0) {
        strncpy(t20_email->strategies[0].query, "class=\"android.widget.EditText\"", LEARN_MAX_QUERY_LEN - 1);
        t20_email->strategies[0].score = 90;
        strncpy(t20_email->strategies[1].query, "E-mail ou Usuário", LEARN_MAX_QUERY_LEN - 1);
        t20_email->strategies[1].score = 75;
        t20_email->strategy_count = 2;
    }

    LearnTarget* t20_btn = learn_db_get_or_create_target("t20_botao_entrar");
    if (t20_btn && t20_btn->strategy_count == 0) {
        strncpy(t20_btn->strategies[0].query, "ENTRAR", LEARN_MAX_QUERY_LEN - 1);
        t20_btn->strategies[0].score = 95;
        strncpy(t20_btn->strategies[1].query, "Entrar", LEARN_MAX_QUERY_LEN - 1);
        t20_btn->strategies[1].score = 85;
        t20_btn->strategy_count = 2;
    }

    LearnTarget* t20_senha = learn_db_get_or_create_target("t20_campo_senha");
    if (t20_senha && t20_senha->strategy_count == 0) {
        strncpy(t20_senha->strategies[0].query, "password=\"true\"", LEARN_MAX_QUERY_LEN - 1);
        t20_senha->strategies[0].score = 90;
        strncpy(t20_senha->strategies[1].query, "content-desc=\"Senha\"", LEARN_MAX_QUERY_LEN - 1);
        t20_senha->strategies[1].score = 75;
        strncpy(t20_senha->strategies[2].query, "Senha", LEARN_MAX_QUERY_LEN - 1);
        t20_senha->strategies[2].score = 65;
        t20_senha->strategy_count = 3;
    }

    LearnTarget* t20_zip = learn_db_get_or_create_target("t20_botao_importar_zip");
    if (t20_zip && t20_zip->strategy_count == 0) {
        strncpy(t20_zip->strategies[0].query, "Importar ZIP", LEARN_MAX_QUERY_LEN - 1);
        t20_zip->strategies[0].score = 95;
        strncpy(t20_zip->strategies[1].query, "IMPORTAR ZIP", LEARN_MAX_QUERY_LEN - 1);
        t20_zip->strategies[1].score = 85;
        strncpy(t20_zip->strategies[2].query, "Importar", LEARN_MAX_QUERY_LEN - 1);
        t20_zip->strategies[2].score = 70;
        t20_zip->strategy_count = 3;
    }

    LearnTarget* t20_proc = learn_db_get_or_create_target("t20_btn_procurar_arquivo");
    if (t20_proc && t20_proc->strategy_count == 0) {
        strncpy(t20_proc->strategies[0].query, "PROCURAR ARQUIVO .ZIP", LEARN_MAX_QUERY_LEN - 1);
        t20_proc->strategies[0].score = 95;
        strncpy(t20_proc->strategies[1].query, "PROCURAR ARQUIVO", LEARN_MAX_QUERY_LEN - 1);
        t20_proc->strategies[1].score = 90;
        strncpy(t20_proc->strategies[2].query, "Procurar arquivo", LEARN_MAX_QUERY_LEN - 1);
        t20_proc->strategies[2].score = 80;
        t20_proc->strategy_count = 3;
    }
}

// --- Inicializa o banco com valores padrão do zero ---
static void learn_db_init_defaults(void) {
    memset(&g_learn_db, 0, sizeof(g_learn_db));
    g_learn_db.version = 1;
    learn_db_inject_seeds();
    g_learn_db.dirty = 1; // Força save inicial com o seed completo
}

// ============================================================
// LOAD — Carrega o banco do JSON
// ============================================================
int learn_db_load(void) {
    // Inicializa sempre com os valores padrão de seed (InnoLearn Estático)
    learn_db_init_defaults();
    return 1;
}

// ============================================================
// SAVE — Persiste o banco em JSON
// ============================================================
int learn_db_save(void) {
    // No-op (InnoLearn Estático)
    return 1;
}

// ============================================================
// Funções de Acesso ao Banco
// ============================================================

LearnTarget* learn_db_find_target(const char* id) {
    for (int i = 0; i < g_learn_db.target_count; i++) {
        if (strcmp(g_learn_db.targets[i].id, id) == 0) {
            return &g_learn_db.targets[i];
        }
    }
    return NULL;
}

LearnTarget* learn_db_get_or_create_target(const char* id) {
    LearnTarget* existing = learn_db_find_target(id);
    if (existing) return existing;
    if (g_learn_db.target_count >= LEARN_MAX_TARGETS) return NULL;
    LearnTarget* t = &g_learn_db.targets[g_learn_db.target_count++];
    memset(t, 0, sizeof(LearnTarget));
    strncpy(t->id, id, LEARN_MAX_ID_LEN - 1);
    return t;
}

LearnCoord* learn_db_find_coord(const char* id) {
    for (int i = 0; i < g_learn_db.coord_count; i++) {
        if (strcmp(g_learn_db.coords[i].id, id) == 0) {
            return &g_learn_db.coords[i];
        }
    }
    return NULL;
}

LearnCoord* learn_db_get_or_create_coord(const char* id) {
    LearnCoord* existing = learn_db_find_coord(id);
    if (existing) return existing;
    if (g_learn_db.coord_count >= LEARN_MAX_COORDS) return NULL;
    LearnCoord* c = &g_learn_db.coords[g_learn_db.coord_count++];
    memset(c, 0, sizeof(LearnCoord));
    strncpy(c->id, id, LEARN_MAX_ID_LEN - 1);
    return c;
}

void learn_db_register_device(const char* model, int screen_w, int screen_h) {
    for (int i = 0; i < g_learn_db.device_count; i++) {
        if (strcmp(g_learn_db.devices[i].model, model) == 0) return;
    }
    if (g_learn_db.device_count >= LEARN_MAX_DEVICES) return;
    LearnDeviceProfile* dp =
        &g_learn_db.devices[g_learn_db.device_count++];
    memset(dp, 0, sizeof(LearnDeviceProfile));
    strncpy(dp->model, model, LEARN_MAX_MODEL_LEN - 1);
    dp->screen_width  = screen_w;
    dp->screen_height = screen_h;
    g_learn_db.dirty = 1;
}

void learn_db_record_run(const char* model, int success) {
    for (int i = 0; i < g_learn_db.device_count; i++) {
        if (strcmp(g_learn_db.devices[i].model, model) == 0) {
            g_learn_db.devices[i].total_runs++;
            if (success) g_learn_db.devices[i].success_runs++;
            g_learn_db.dirty = 1;
            return;
        }
    }
}
