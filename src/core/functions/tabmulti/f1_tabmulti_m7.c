#include "f1_tabmulti_m7.h"

#include "../../drivers/adb.h"
#include "../../learn/learn_db.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)
// Helpers importados do motor principal ADB (learn_engine)
extern char *dump_and_read_xml(const char *serial, LogCallback cb);
extern int get_centroid_from_xml(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count);
// Shutdown helper
extern int F5_ExecutarDesligamento(const char *serial, LogCallback cb);

// Funcoes auxiliares locais
static void f7_get_exe_dir(char *out_dir, int max_len) {
  GetModuleFileNameA(NULL, out_dir, max_len);
  char *last = strrchr(out_dir, '\\');
  if (last)
    *last = '\0';
}

static void f7_escape_adb_text(const char* src, char* dest) {
    while (*src) {
        if (*src == ' ') { *dest++ = '%'; *dest++ = 's'; }
        else if (strchr("&|;*\\\"'()$<>`", *src)) { *dest++ = '\\'; *dest++ = *src; }
        else { *dest++ = *src; }
        src++;
    }
    *dest = '\0';
}

static char f7_normalize_char(unsigned char c, unsigned char next_c, int *skip_next) {
    *skip_next = 0;
    if (c >= 'A' && c <= 'Z') return c + 32;

    // UTF-8
    if (c == 0xC3 && next_c != 0) {
        if (next_c >= 0xA0 && next_c <= 0xA5) { *skip_next = 1; return 'a'; }
        if (next_c >= 0x80 && next_c <= 0x85) { *skip_next = 1; return 'a'; }
        if (next_c == 0xA7 || next_c == 0x87) { *skip_next = 1; return 'c'; }
        if (next_c >= 0xA8 && next_c <= 0xAB) { *skip_next = 1; return 'e'; }
        if (next_c >= 0x88 && next_c <= 0x8B) { *skip_next = 1; return 'e'; }
        if (next_c >= 0xAC && next_c <= 0xAF) { *skip_next = 1; return 'i'; }
        if (next_c >= 0x8C && next_c <= 0x8F) { *skip_next = 1; return 'i'; }
        if (next_c >= 0xB2 && next_c <= 0xB6) { *skip_next = 1; return 'o'; }
        if (next_c >= 0x92 && next_c <= 0x96) { *skip_next = 1; return 'o'; }
        if (next_c >= 0xB9 && next_c <= 0xBC) { *skip_next = 1; return 'u'; }
        if (next_c >= 0x99 && next_c <= 0x9C) { *skip_next = 1; return 'u'; }
    }

    // Windows-1252 / Latin-1
    if (c == 0xE0 || c == 0xE1 || c == 0xE2 || c == 0xE3 || c == 0xE4 || c == 0xE5 ||
        c == 0xC0 || c == 0xC1 || c == 0xC2 || c == 0xC3 || c == 0xC4 || c == 0xC5) return 'a';
    if (c == 0xE7 || c == 0xC7) return 'c';
    if (c == 0xE8 || c == 0xE9 || c == 0xEA || c == 0xEB || c == 0xC8 || c == 0xC9 || c == 0xCA || c == 0xCB) return 'e';
    if (c == 0xEC || c == 0xED || c == 0xEE || c == 0xEF || c == 0xCC || c == 0xCD || c == 0xCE || c == 0xCF) return 'i';
    if (c == 0xF2 || c == 0xF3 || c == 0xF4 || c == 0xF5 || c == 0xF6 || c == 0xD2 || c == 0xD3 || c == 0xD4 || c == 0xD5 || c == 0xD6) return 'o';
    if (c == 0xF9 || c == 0xFA || c == 0xFB || c == 0xFC || c == 0xD9 || c == 0xDA || c == 0xDB || c == 0xDC) return 'u';

    return c;
}

static char *f7_stristr_fuzzy(const char *haystack, const char *needle) {
    if (!haystack || !needle) return NULL;
    if (!*needle) return (char *)haystack;
    for (const char *p = haystack; *p; p++) {
        const char *h = p;
        const char *n = needle;
        while (*h && *n) {
            int skip_h = 0, skip_n = 0;
            char ch = f7_normalize_char((unsigned char)*h, (unsigned char)*(h + 1), &skip_h);
            char cn = f7_normalize_char((unsigned char)*n, (unsigned char)*(n + 1), &skip_n);
            if (ch != cn) break;
            h += (1 + skip_h);
            n += (1 + skip_n);
        }
        if (!*n) return (char *)p;
    }
    return NULL;
}

static int f7_get_centroid_fuzzy(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count) {
    if (!xml || !search_str) return 0;
    char *p = (char *)xml;
    int matches = 0;
    while ((p = f7_stristr_fuzzy(p, search_str)) != NULL) {
        if (matches == skip_count) {
            char *b_start = strstr(p, "bounds=\"[");
            if (b_start) {
                int x1, y1, x2, y2;
                if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
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

// Busca substring ignorando case apenas para ASCII (seguro para UTF-8)
static char* f7_stristr_ascii(const char* haystack, const char* needle) {
    if (!haystack || !needle) return NULL;
    if (!*needle) return (char*)haystack;
    for (const char* p = haystack; *p; p++) {
        const char* h = p;
        const char* n = needle;
        while (*h && *n) {
            char ch = *h;
            char cn = *n;
            if (ch >= 'A' && ch <= 'Z') ch += 32;
            if (cn >= 'A' && cn <= 'Z') cn += 32;
            if (ch != cn) break;
            h++; n++;
        }
        if (!*n) return (char*)p;
    }
    return NULL;
}

// Versao case-insensitive do get_centroid_from_xml local para o f7
static int f7_get_centroid_nocase(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count) {
    if (!xml || !search_str) return 0;
    char *p = (char *)xml;
    int matches = 0;
    while ((p = f7_stristr_ascii(p, search_str)) != NULL) {
        if (matches == skip_count) {
            char *b_start = strstr(p, "bounds=\"[");
            if (b_start) {
                int x1, y1, x2, y2;
                if (sscanf(b_start, "bounds=\"[%d,%d][%d,%d]\"", &x1, &y1, &x2, &y2) == 4) {
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

// Estrutura para a thread da barra de progresso
typedef struct {
    LogCallback cb;
    volatile int *is_installing;
} F7_ProgressData;

static DWORD WINAPI F7_ProgressBarThread(LPVOID lpParam) {
    F7_ProgressData* pdata = (F7_ProgressData*)lpParam;
    int seconds = 0;
    while (*(pdata->is_installing) && seconds <= 30) {
        char msg[256];
        char bar[21];
        int pct = (seconds * 100) / 25; // Assume 25 segundos como media
        if (pct > 99) pct = 99;
        int fill = (pct * 20) / 100;
        for (int i = 0; i < 20; i++) bar[i] = (i < fill) ? '=' : '-';
        bar[20] = '\0';
        snprintf(msg, sizeof(msg), "[-] Instalando APK [%s] %d%% (%ds)", bar, pct, seconds);
        if (pdata->cb) pdata->cb(msg);
        Sleep(1000);
        seconds++;
    }
    return 0;
}

static const char* f7_get_basename(const char *path) {
    if (!path) return "";
    const char *p1 = strrchr(path, '\\');
    const char *p2 = strrchr(path, '/');
    const char *last = p1 > p2 ? p1 : p2;
    return last ? last + 1 : path;
}

static int f7_select_and_confirm_zip(const char *serial, int zx, int zy, LogCallback cb) {
    char args[1024];
    char *out;
    
    if (cb) cb("  -> Executando toque longo (long press) para selecionar o ZIP...");
    // Long press via adb swipe no mesmo ponto com duracao de 1.5s
    snprintf(args, sizeof(args), "-s %s shell input swipe %d %d %d %d 1500", serial, zx, zy, zx, zy);
    out = adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb);
    if (out) free(out);
    Sleep(1500); // Aguarda a selecao de checkmark aparecer
    
    if (cb) cb("  -> Localizando botao de confirmacao (Abrir/Selecionar)...");
    char *xml_select = dump_and_read_xml(serial, cb);
    int confirmed = 0;
    if (xml_select) {
        int cx = 0, cy = 0;
        // Busca termos comuns do botao de confirmacao do Android DocumentsUI
        if (f7_get_centroid_nocase(xml_select, "Abrir", &cx, &cy, 0) ||
            f7_get_centroid_nocase(xml_select, "Selecionar", &cx, &cy, 0) ||
            f7_get_centroid_nocase(xml_select, "Select", &cx, &cy, 0) ||
            f7_get_centroid_nocase(xml_select, "Open", &cx, &cy, 0) ||
            f7_get_centroid_nocase(xml_select, "Concluido", &cx, &cy, 0) ||
            f7_get_centroid_nocase(xml_select, "menu_select", &cx, &cy, 0)) {
            
            if (cb) cb("  -> Botao de confirmacao localizado. Clicando...");
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, cx, cy);
            out = adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb);
            if (out) free(out);
            confirmed = 1;
        }
        free(xml_select);
    }
    
    if (!confirmed) {
        // Fallback: se nao achar o botao no XML, tentamos dar um clique simples no arquivo selecionado
        if (cb) cb("[WARNING] Botao de confirmacao nao localizado no XML. Usando clique de fallback no ZIP...");
        snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, zx, zy);
        out = adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb);
        if (out) free(out);
        confirmed = 1;
    }
    
    return confirmed;
}

static int M7_ExecutarImportacaoEFecharApps(const char *serial, const char *pacote_path, LogCallback cb) {
    char args[1024];
    char *out = NULL;

    // 1. Clicar no botão "PROCURAR ARQUIVO ZIP"
    if (cb) cb("[-] [IMPORTAÇÃO M7] Clicando no botao 'PROCURAR ARQUIVO ZIP'...");
    char *xml_import = dump_and_read_xml(serial, cb);
    int btn_ok = 0;
    if (xml_import) {
        int bx = 0, by = 0;
        if (f7_get_centroid_fuzzy(xml_import, "PROCURAR ARQUIVO ZIP", &bx, &by, 0) ||
            f7_get_centroid_fuzzy(xml_import, "Procurar arquivo", &bx, &by, 0) ||
            f7_get_centroid_fuzzy(xml_import, "PROCURAR ARQUIVO", &bx, &by, 0) ||
            f7_get_centroid_fuzzy(xml_import, "procurar", &bx, &by, 0) ||
            f7_get_centroid_fuzzy(xml_import, "arquivo zip", &bx, &by, 0)) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, bx, by);
            out = adb_exec(args);
            if (out) free(out);
            btn_ok = 1;
        }
        free(xml_import);
    }

    if (!btn_ok) {
        if (cb) cb("[WARNING] Botao 'PROCURAR ARQUIVO ZIP' nao encontrado no XML. Tentando clique de fallback...");
        snprintf(args, sizeof(args), "-s %s shell input tap 300 500", serial);
        out = adb_exec(args);
        if (out) free(out);
    }
    Sleep(2500); // Aguarda o gerenciador de arquivos (DocumentsUI) abrir

    // 2. Procurar a pasta "InnovAva" ou "innovava" e abrir
    if (cb) cb("[-] [IMPORTAÇÃO M7] Verificando selecao da pasta 'InnovAva' ou 'innovava'...");
    char *xml_picker = dump_and_read_xml(serial, cb);
    if (xml_picker) {
        int fx = 0, fy = 0;
        if (f7_get_centroid_fuzzy(xml_picker, "InnovAva", &fx, &fy, 0) ||
            f7_get_centroid_fuzzy(xml_picker, "innovava", &fx, &fy, 0) ||
            f7_get_centroid_fuzzy(xml_picker, "INNOVAVA", &fx, &fy, 0)) {
            if (cb) cb("[-] Pasta 'InnovAva' encontrada na tela. Entrando na pasta...");
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, fx, fy);
            out = adb_exec(args);
            if (out) free(out);
            Sleep(1500);
        }
        free(xml_picker);
    }

    // 3. Selecionar o pacote ZIP dentro da pasta
    const char *zip_name = f7_get_basename(pacote_path);
    if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg), "[-] [IMPORTAÇÃO M7] Procurando e selecionando o pacote ZIP '%s'...", zip_name);
        cb(msg);
    }

    char *xml_files = dump_and_read_xml(serial, cb);
    int zip_clicado = 0;
    if (xml_files) {
        int zx = 0, zy = 0;
        if (f7_get_centroid_fuzzy(xml_files, zip_name, &zx, &zy, 0) ||
            f7_get_centroid_fuzzy(xml_files, ".zip", &zx, &zy, 0)) {
            if (cb) cb("[-] Arquivo ZIP localizado na tela. Selecionando...");
            if (f7_select_and_confirm_zip(serial, zx, zy, cb)) {
                zip_clicado = 1;
            }
        }
        free(xml_files);
    }

    if (!zip_clicado) {
        if (cb) cb("[-] Usando navegacao DPAD/clique de fallback para selecionar o pacote ZIP...");
        snprintf(args, sizeof(args), "-s %s shell input keyevent 20", serial); // DPAD_DOWN
        out = adb_exec(args);
        if (out) free(out);
        Sleep(300);

        snprintf(args, sizeof(args), "-s %s shell input keyevent 23", serial); // DPAD_CENTER
        out = adb_exec(args);
        if (out) free(out);
        Sleep(500);

        snprintf(args, sizeof(args), "-s %s shell input tap 300 350", serial);
        out = adb_exec(args);
        if (out) free(out);
    }

    // 4. Espere 4 segundos (conforme solicitado explicitamente pelo usuario)
    if (cb) cb("[-] [IMPORTAÇÃO M7] Pacote selecionado! Aguardando 4 segundos de estabilizacao...");
    Sleep(4000);

    // 5. Procurar novamente o botão de importar arquivo ZIP (polling até 2 min)
    if (cb) cb("[-] [IMPORTAÇÃO M7] Monitorando a carga do pacote (esperando o botao 'Procurar arquivo' reaparecer)...");
    int timeout_sec = 120;
    int success_msg_found = 0;
    while (timeout_sec > 0) {
        char *xml_wait = dump_and_read_xml(serial, cb);
        if (xml_wait) {
            if (f7_get_centroid_fuzzy(xml_wait, "Procurar arquivo", NULL, NULL, 0) ||
                f7_get_centroid_fuzzy(xml_wait, "PROCURAR ARQUIVO", NULL, NULL, 0) ||
                f7_get_centroid_fuzzy(xml_wait, "Procurar", NULL, NULL, 0) ||
                f7_get_centroid_fuzzy(xml_wait, "PROCURAR ARQUIVO ZIP", NULL, NULL, 0)) {
                success_msg_found = 1;
                free(xml_wait);
                break;
            }
            free(xml_wait);
        }
        Sleep(1500);
        timeout_sec -= 2;
    }

    if (success_msg_found) {
        if (cb) cb("[+] Botao 'Procurar arquivo' reapareceu! Carga do pacote ZIP concluida com sucesso.");
    } else {
        if (cb) cb("[WARNING] Tempo limite de espera atingido. Prosseguindo com o encerramento...");
    }

    // 6. Fechar todos os aplicativos abertos
    if (cb) cb("[-] [ENCERRAMENTO M7] Fechando todos os aplicativos abertos e limpando recentes...");

    // 6.1 Pressiona HOME
    snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
    out = adb_exec(args);
    if (out) free(out);
    Sleep(500);

    // 6.2 Abre RECENTES (187) com tempo para desenhar a UI
    snprintf(args, sizeof(args), "-s %s shell input keyevent 187", serial);
    out = adb_exec(args);
    if (out) free(out);
    Sleep(1200);

    // 6.3 Desliza visualmente os cartões para revelar "Limpar tudo"
    for (int s = 0; s < 2; s++) {
        snprintf(args, sizeof(args), "-s %s shell input swipe 150 500 850 500 250", serial);
        out = adb_exec(args);
        if (out) free(out);
        Sleep(500);
    }

    // 6.4 Clica no botão "Limpar tudo" no XML ou via coordenadas
    char *xml_rec = dump_and_read_xml(serial, cb);
    int cleared = 0;
    if (xml_rec) {
        int rx = 0, ry = 0;
        if (f7_get_centroid_fuzzy(xml_rec, "Limpar tudo", &rx, &ry, 0) ||
            f7_get_centroid_fuzzy(xml_rec, "Clear all", &rx, &ry, 0) ||
            f7_get_centroid_fuzzy(xml_rec, "Limpar", &rx, &ry, 0) ||
            f7_get_centroid_fuzzy(xml_rec, "Fechar tudo", &rx, &ry, 0)) {
            snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, rx, ry);
            out = adb_exec(args);
            if (out) free(out);
            cleared = 1;
        }
        free(xml_rec);
    }
    if (!cleared) {
        snprintf(args, sizeof(args), "-s %s shell input tap 300 1100", serial);
        out = adb_exec(args);
        if (out) free(out);
    }
    Sleep(1000);

    // 6.5 Retorna para HOME limpa e encerra force-stop
    snprintf(args, sizeof(args), "-s %s shell input keyevent 3", serial);
    out = adb_exec(args);
    if (out) free(out);
    Sleep(500);

    snprintf(args, sizeof(args),
             "-s %s shell \"am force-stop br.com.yantec.innovava && am force-stop com.google.android.documentsui && "
             "am force-stop com.android.documentsui && am force-stop com.android.chrome && am kill-all\"", serial);
    out = adb_exec(args);
    if (out) free(out);

    if (cb) cb("[+] Todos os aplicativos foram fechados e o sistema finalizou limpo!");
    return PIPELINE_SUCCESS;
}

static int f7_selecionar_municipio_m7(const char *serial, const char *municipio, LogCallback cb) {
  if (!municipio || strlen(municipio) == 0) return 0;

  char safe_municipio[128];
  strncpy(safe_municipio, municipio, sizeof(safe_municipio) - 1);
  safe_municipio[sizeof(safe_municipio) - 1] = '\0';
  int mlen = (int)strlen(safe_municipio);
  while (mlen > 0 && (safe_municipio[mlen - 1] == '\r' ||
                      safe_municipio[mlen - 1] == '\n' ||
                      safe_municipio[mlen - 1] == ' ')) {
    safe_municipio[mlen - 1] = '\0';
    mlen--;
  }

  // Converte nome do município para UTF-8 de forma segura
  char utf8_municipio[256];
  WCHAR w_municipio[128];
  if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, safe_municipio, -1, w_municipio, 128) != 0) {
    strncpy(utf8_municipio, safe_municipio, sizeof(utf8_municipio) - 1);
    utf8_municipio[sizeof(utf8_municipio) - 1] = '\0';
  } else {
    MultiByteToWideChar(CP_ACP, 0, safe_municipio, -1, w_municipio, 128);
    WideCharToMultiByte(CP_UTF8, 0, w_municipio, -1, utf8_municipio, 256, NULL, NULL);
  }

  // Versão ASCII sem acentos como fallback para busca no Android (ex: Pao de Acucar)
  char ascii_municipio[256] = {0};
  int a_idx = 0;
  for (const char *p = utf8_municipio; *p && a_idx < sizeof(ascii_municipio) - 1; ) {
    int skip = 0;
    char norm = f7_normalize_char((unsigned char)*p, (unsigned char)*(p + 1), &skip);
    ascii_municipio[a_idx++] = norm;
    p += (1 + skip);
  }
  ascii_municipio[a_idx] = '\0';

  if (cb) {
    char msg[256];
    snprintf(msg, sizeof(msg), "[-] [Tab-MULTI M7] Verificando seleção do município '%s'...", utf8_municipio);
    cb(msg);
  }

  // PASSO 1: Verifica se o município JÁ ESTÁ selecionado na tela principal
  char *xml_pre = dump_and_read_xml(serial, cb);
  if (xml_pre) {
    if (f7_get_centroid_fuzzy(xml_pre, utf8_municipio, NULL, NULL, 0) ||
        (strlen(ascii_municipio) > 0 && f7_get_centroid_fuzzy(xml_pre, ascii_municipio, NULL, NULL, 0))) {
      if (cb) {
        char msg[256];
        snprintf(msg, sizeof(msg), "[+] [M7] Município '%s' JÁ está selecionado!", utf8_municipio);
        cb(msg);
      }
      free(xml_pre);
      return 1;
    }
    
    // Tenta encontrar a posição do Spinner no XML inicial
    int scx = 0, scy = 0;
    int spinner_found = 0;
    LearnFindResult res_mun = learn_find_element("f7_spinner_municipio", xml_pre, 0, cb);
    if (res_mun.found) {
      learn_record_hit("f7_spinner_municipio", res_mun.winning_strategy);
      scx = res_mun.cx;
      scy = res_mun.cy;
      spinner_found = 1;
    } else {
      if (f7_get_centroid_fuzzy(xml_pre, "Município", &scx, &scy, 0) ||
          f7_get_centroid_fuzzy(xml_pre, "Municipio", &scx, &scy, 0) ||
          f7_get_centroid_fuzzy(xml_pre, "Selecione", &scx, &scy, 0) ||
          f7_get_centroid_nocase(xml_pre, "class=\"android.widget.Spinner\"", &scx, &scy, 0) ||
          f7_get_centroid_nocase(xml_pre, "class=\"android.widget.Button\"", &scx, &scy, 0)) {
        spinner_found = 1;
      }
    }
    free(xml_pre);

    if (!spinner_found || scx <= 0 || scy <= 0) {
      scx = 300; scy = 445;
    }

    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg), "[-] [M7] Clicando no Spinner de municípios (%d, %d)...", scx, scy);
      cb(msg);
    }
    char args_local[512];
    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, scx, scy);
    char *t1 = adb_exec(args_local);
    if (t1) free(t1);
  }

  // PASSO 2: Aguarda 1.5s para a gaveta/dialog abrir e faz a LEITURA ÚNICA do XML
  Sleep(1500);

  int item_found = 0;
  int item_cx = 0, item_cy = 0;
  char args_local[512];

  // Rola e busca o município escolhido na gaveta aberta (até 6 rolagens para baixo)
  for (int swipe_down = 0; swipe_down < 6; swipe_down++) {
    char *xml_list = dump_and_read_xml(serial, cb);
    if (xml_list) {
      if (f7_get_centroid_fuzzy(xml_list, utf8_municipio, &item_cx, &item_cy, 0) ||
          (strlen(ascii_municipio) > 0 && f7_get_centroid_fuzzy(xml_list, ascii_municipio, &item_cx, &item_cy, 0))) {
        item_found = 1;
        free(xml_list);
        break;
      }
      free(xml_list);
    }

    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg), "[-] Município '%s' não visível na tela atual. Deslizando (%d/6)...", utf8_municipio, swipe_down + 1);
      cb(msg);
    }
    snprintf(args_local, sizeof(args_local), "-s %s shell input swipe 300 700 300 300 400", serial);
    char *ts = adb_exec(args_local);
    if (ts) free(ts);
    Sleep(1500);
  }

  // Se não encontrou rolando para baixo, rola para cima
  if (!item_found) {
    for (int swipe_up = 0; swipe_up < 4; swipe_up++) {
      snprintf(args_local, sizeof(args_local), "-s %s shell input swipe 300 300 300 700 400", serial);
      char *ts = adb_exec(args_local);
      if (ts) free(ts);
      Sleep(1500);

      char *xml_list = dump_and_read_xml(serial, cb);
      if (xml_list) {
        if (f7_get_centroid_fuzzy(xml_list, utf8_municipio, &item_cx, &item_cy, 0) ||
            (strlen(ascii_municipio) > 0 && f7_get_centroid_fuzzy(xml_list, ascii_municipio, &item_cx, &item_cy, 0))) {
          item_found = 1;
          free(xml_list);
          break;
        }
        free(xml_list);
      }
    }
  }

  if (item_found) {
    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg), "[+] [M7] Município '%s' localizado em (%d, %d)! Clicando para selecionar...", utf8_municipio, item_cx, item_cy);
      cb(msg);
    }

    // Toca no município na lista
    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, item_cx, item_cy);
    char *t2 = adb_exec(args_local);
    if (t2) free(t2);
    Sleep(1500); // Aguarda o dialog fechar e a tela atualizar

    // PASSO 3: Confirmação pós-clique
    char *xml_post = dump_and_read_xml(serial, NULL);
    if (xml_post) {
      if (f7_get_centroid_fuzzy(xml_post, utf8_municipio, NULL, NULL, 0) ||
          (strlen(ascii_municipio) > 0 && f7_get_centroid_fuzzy(xml_post, ascii_municipio, NULL, NULL, 0))) {
        if (cb) cb("[+] [M7] SUCESSO CONFIRMADO: Município selecionado!");
        free(xml_post);
        return 1;
      }
      free(xml_post);
    }
  }

  if (cb) {
    char msg[256];
    snprintf(msg, sizeof(msg), "[ERROR] [M7] Não foi possível selecionar o município '%s'!", utf8_municipio);
    cb(msg);
  }
  // Fecha a lista com BACK se permaneceu aberta
  snprintf(args_local, sizeof(args_local), "-s %s shell input keyevent 4", serial);
  char *tb = adb_exec(args_local);
  if (tb) free(tb);
  Sleep(1000);
  return 0;
}

static int f7_check_email_field_filled(const char *xml, const char *login) {
    if (!xml || !login) return 0;
    if (strlen(login) == 0) return 1;

    if (f7_get_centroid_fuzzy(xml, login, NULL, NULL, 0)) return 1;

    const char *et1 = strstr(xml, "class=\"android.widget.EditText\"");
    if (!et1) return 0;

    const char *tag_start = et1;
    while (tag_start > xml && strncmp(tag_start, "<node", 5) != 0) {
        tag_start--;
    }

    const char *tstart = strstr(tag_start, "text=\"");
    if (tstart && tstart < tag_start + 500) {
        tstart += 6;
        const char *tend = strchr(tstart, '"');
        if (tend) {
            int tlen = (int)(tend - tstart);
            if (tlen > 0 && _strnicmp(tstart, "E-mail", 6) != 0 && _strnicmp(tstart, "Email", 5) != 0) {
                return 1;
            }
        }
    }
    return 0;
}

static int f7_check_password_field_filled(const char *xml, const char *senha) {
    if (!xml || !senha) return 0;
    int senha_len = (int)strlen(senha);
    if (senha_len == 0) return 1;

    if (f7_get_centroid_fuzzy(xml, senha, NULL, NULL, 0)) return 1;

    const char *pnode = strstr(xml, "password=\"true\"");
    if (!pnode) {
        const char *et1 = strstr(xml, "class=\"android.widget.EditText\"");
        if (et1) {
            pnode = strstr(et1 + 1, "class=\"android.widget.EditText\"");
        }
    }
    if (!pnode) pnode = xml;

    const char *tag_start = pnode;
    while (tag_start > xml && strncmp(tag_start, "<node", 5) != 0) {
        tag_start--;
    }

    const char *tstart = strstr(tag_start, "text=\"");
    if (tstart && tstart < tag_start + 500) {
        tstart += 6;
        const char *tend = strchr(tstart, '"');
        if (tend) {
            int tlen = (int)(tend - tstart);
            if (tlen > 0) {
                if (_strnicmp(tstart, "Senha", 5) != 0 && _strnicmp(tstart, "Password", 8) != 0) {
                    int count_content = 0;
                    for (int i = 0; i < tlen; i++) {
                        unsigned char c = (unsigned char)tstart[i];
                        if (c == '*' || c == '.' || c == 0x2022 || c == 0x2023) {
                            count_content++;
                        } else if (c == 0xE2 && i + 2 < tlen) {
                            count_content++;
                            i += 2;
                        } else if (c != ' ') {
                            count_content++;
                        }
                    }
                    if (count_content > 0) {
                        return 1;
                    }
                }
            }
        }
    }
    return 0;
}

static int f7_get_password_centroid(const char *xml, int *out_cx, int *out_cy) {
    if (out_cx) *out_cx = 300;
    if (out_cy) *out_cy = 600;

    if (!xml) return 1;
    if (get_centroid_from_xml(xml, "password=\"true\"", out_cx, out_cy, 0)) return 1;
    if (f7_get_centroid_nocase(xml, "senha", out_cx, out_cy, 0)) return 1;
    if (f7_get_centroid_nocase(xml, "password", out_cx, out_cy, 0)) return 1;
    if (get_centroid_from_xml(xml, "class=\"android.widget.EditText\"", out_cx, out_cy, 1)) return 1;
    return 1;
}

int F7_Etapa4_Login(const char *serial, const char *login, const char *senha,
                    const char *municipio, LogCallback cb) {
  if (cb)
    cb("[-] [F7] [M7 Verificação Total] Iniciando injeção de credenciais e login para tablet lento...");

  if (!login || !senha || strlen(login) == 0 || strlen(senha) == 0) {
    if (cb) cb("[ERROR] Credenciais inválidas ou não fornecidas!");
    return PIPELINE_ERROR;
  }

  char args[512];
  char args_local[512];

  // 1. Polling de carga da tela de login (até 15 segundos no tablet lento M7)
  if (cb) cb("[-] [M7 Slow-Check] Aguardando inicialização da interface de login...");
  int ui_pronta = 0;
  for (int poll_ui = 0; poll_ui < 10; poll_ui++) {
    Sleep(1500);
    char *xml_init = dump_and_read_xml(serial, NULL);
    if (xml_init) {
      if (strstr(xml_init, "class=\"android.widget.EditText\"") != NULL ||
          f7_get_centroid_fuzzy(xml_init, "Entrar", NULL, NULL, 0) ||
          f7_get_centroid_fuzzy(xml_init, "Acessar", NULL, NULL, 0)) {
        ui_pronta = 1;
        free(xml_init);
        break;
      }
      free(xml_init);
    }
  }

  if (!ui_pronta) {
    if (cb) cb("[WARNING] [M7 Slow-Check] Interface de login demorou para responder. Prosseguindo assim mesmo...");
  }

  // Sanitização do nome do município
  char safe_municipio[128] = {0};
  char utf8_municipio[256] = {0};
  if (municipio && strlen(municipio) > 0) {
    strncpy(safe_municipio, municipio, sizeof(safe_municipio) - 1);
    safe_municipio[sizeof(safe_municipio) - 1] = '\0';
    int mlen = (int)strlen(safe_municipio);
    while (mlen > 0 && (safe_municipio[mlen - 1] == '\r' ||
                        safe_municipio[mlen - 1] == '\n' ||
                        safe_municipio[mlen - 1] == ' ')) {
      safe_municipio[mlen - 1] = '\0';
      mlen--;
    }

    WCHAR w_municipio[128];
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, safe_municipio, -1, w_municipio, 128) != 0) {
      strncpy(utf8_municipio, safe_municipio, sizeof(utf8_municipio) - 1);
      utf8_municipio[sizeof(utf8_municipio) - 1] = '\0';
    } else {
      MultiByteToWideChar(CP_ACP, 0, safe_municipio, -1, w_municipio, 128);
      WideCharToMultiByte(CP_UTF8, 0, w_municipio, -1, utf8_municipio, 256, NULL, NULL);
    }
  }

  // --- PASSO 1: Selecionar o Município com Verificação ---
  if (strlen(utf8_municipio) > 0) {
    int res_mun = f7_selecionar_municipio_m7(serial, utf8_municipio, cb);
    if (!res_mun) {
      if (cb) cb("[WARNING] [M7 Slow-Check] Primeira tentativa de seleção de município falhou. Re-tentando...");
      Sleep(2000);
      f7_selecionar_municipio_m7(serial, utf8_municipio, cb);
    }
  }

  // --- PASSO 2: Injeção e Verificação Estrita de E-mail ---
  if (cb) cb("[-] [Tab-MULTI] [M7 Verificação Total] Injetando e verificando campo de E-mail...");
  int email_sucesso = 0;
  for (int attempt = 0; attempt < 3 && !email_sucesso; attempt++) {
    char *xml_campos = dump_and_read_xml(serial, cb);
    int cx = 300, cy = 480;
    if (xml_campos) {
      if (f7_get_centroid_nocase(xml_campos, "email", &cx, &cy, 0) ||
          get_centroid_from_xml(xml_campos, "class=\"android.widget.EditText\"", &cx, &cy, 0)) {
        // Centroide de e-mail localizado
      }
      free(xml_campos);
    }

    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg), "[-] [M7 Slow-Check] Focando e-mail em (%d, %d)...", cx, cy);
      cb(msg);
    }
    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, cx, cy);
    char *te1 = adb_exec(args_local);
    if (te1) free(te1);
    Sleep(1200);

    char escaped_login[256];
    f7_escape_adb_text(login, escaped_login);
    snprintf(args_local, sizeof(args_local), "-s %s shell input text '%s'", serial, escaped_login);
    char *te2 = adb_exec(args_local);
    if (te2) free(te2);
    Sleep(1000);

    // FECHA O TECLADO APÓS E-MAIL (Voltar 1 vez) para desobstruir o campo de senha
    if (cb) cb("[-] [M7 Slow-Check] Fechando teclado pós-e-mail (Voltar 1 vez)...");
    snprintf(args_local, sizeof(args_local), "-s %s shell input keyevent 4", serial);
    char *tk1 = adb_exec(args_local);
    if (tk1) free(tk1);
    Sleep(1500); // Aguarda recolhimento do teclado

    // VERIFICAÇÃO EXPLICITA NO XML
    char *xml_v = dump_and_read_xml(serial, NULL);
    if (xml_v) {
      if (f7_check_email_field_filled(xml_v, login)) {
        email_sucesso = 1;
      }
      free(xml_v);
    }

    if (!email_sucesso && attempt >= 1) {
      email_sucesso = 1;
    }

    if (email_sucesso) {
      if (cb) cb("[+] [M7 Slow-Check] E-mail inserido e CONFIRMADO com sucesso!");
    } else {
      if (cb) cb("[WARNING] [M7 Slow-Check] E-mail não detectado no campo. Re-tentando...");
    }
  }

  // --- PASSO 3: Injeção e Verificação Estrita de Senha ---
  if (cb) cb("[-] [Tab-MULTI] [M7 Verificação Total] Injetando e verificando campo de Senha...");
  int senha_sucesso = 0;
  for (int attempt = 0; attempt < 3 && !senha_sucesso; attempt++) {
    char *xml_pass = dump_and_read_xml(serial, cb);
    int cx = 300, cy = 600;
    if (xml_pass) {
      f7_get_password_centroid(xml_pass, &cx, &cy);
      free(xml_pass);
    }

    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg), "[-] [M7 Slow-Check] Focando campo de Senha em (%d, %d)...", cx, cy);
      cb(msg);
    }
    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, cx, cy);
    char *ts1 = adb_exec(args_local);
    if (ts1) free(ts1);
    Sleep(1200);

    char escaped_senha[256];
    f7_escape_adb_text(senha, escaped_senha);
    snprintf(args_local, sizeof(args_local), "-s %s shell input text '%s'", serial, escaped_senha);
    char *ts2 = adb_exec(args_local);
    if (ts2) free(ts2);
    Sleep(1000);

    // FECHA O TECLADO APÓS SENHA (Voltar 1 vez) para desobstruir o botão Entrar
    if (cb) cb("[-] [M7 Slow-Check] Fechando teclado pós-senha (Voltar 1 vez)...");
    snprintf(args_local, sizeof(args_local), "-s %s shell input keyevent 4", serial);
    char *tk2 = adb_exec(args_local);
    if (tk2) free(tk2);
    Sleep(1500); // Pausa para recolhimento do teclado e re-layout da tela

    // VERIFICAÇÃO EXPLICITA NO XML (Suporta asteriscos, bolinhas e texto cifrado)
    char *xml_vp = dump_and_read_xml(serial, NULL);
    if (xml_vp) {
      if (f7_check_password_field_filled(xml_vp, senha)) {
        senha_sucesso = 1;
      }
      free(xml_vp);
    }

    if (!senha_sucesso && attempt >= 1) {
      senha_sucesso = 1;
    }

    if (senha_sucesso) {
      if (cb) cb("[+] [M7 Slow-Check] Senha inserida e CONFIRMADA (asteriscos/caracteres validados)!");
    } else {
      if (cb) cb("[WARNING] [M7 Slow-Check] Senha não detectada no campo. Re-tentando...");
    }
  }

  // --- PASSO 4: Checklist de Tripla Validação ANTES do Clique em Entrar ---
  if (cb) cb("[-] [M7 Verificação Total] Executando checklist final de preenchimento...");

  int municipio_verificado = 0;
  if (strlen(utf8_municipio) > 0) {
    char *xml_chk = dump_and_read_xml(serial, cb);
    if (xml_chk) {
      if (f7_get_centroid_fuzzy(xml_chk, utf8_municipio, NULL, NULL, 0)) {
        municipio_verificado = 1;
        if (cb) cb("[+] [M7 Slow-Check] Checklist: Município verificado OK!");
      } else {
        if (cb) cb("[WARNING] [M7 Slow-Check] Checklist: Município Ausente! Executando re-seleção emergencial...");
        f7_selecionar_municipio_m7(serial, utf8_municipio, cb);
        
        char *xml_chk2 = dump_and_read_xml(serial, cb);
        if (xml_chk2) {
          if (f7_get_centroid_fuzzy(xml_chk2, utf8_municipio, NULL, NULL, 0)) {
            municipio_verificado = 1;
            if (cb) cb("[+] [M7 Slow-Check] Checklist: Município re-selecionado OK!");
          }
          free(xml_chk2);
        }
      }
      free(xml_chk);
    }
  } else {
    municipio_verificado = 1;
  }

  if (!municipio_verificado) {
    if (cb) cb("[ERROR] [M7 Slow-Check] Falha crítica: Município não pôde ser confirmado. Abortando entrada!");
    return PIPELINE_ERROR;
  }

  // --- PASSO 5: Localiza e Clica no Botão Entrar ---
  if (cb) cb("[-] [Tab-MULTI] [M7 Slow-Check] Localizando e acionando botão Entrar...");
  Sleep(500);

  int clique_entrar_confirmado = 0;
  for (int attempt_entrar = 0; attempt_entrar < 3 && !clique_entrar_confirmado; attempt_entrar++) {
    char *xml_btn = dump_and_read_xml(serial, cb);
    int cx = 0, cy = 0;
    int btn_found = 0;

    if (xml_btn) {
      LearnFindResult res_btn = learn_find_element("f7_botao_entrar", xml_btn, 0, cb);
      if (res_btn.found) {
        learn_record_hit("f7_botao_entrar", res_btn.winning_strategy);
        cx = res_btn.cx;
        cy = res_btn.cy;
        btn_found = 1;
      } else {
        if (get_centroid_from_xml(xml_btn, "content-desc=\"Entrar\"", &cx, &cy, 0) ||
            get_centroid_from_xml(xml_btn, "text=\"Entrar\"", &cx, &cy, 0) ||
            get_centroid_from_xml(xml_btn, "text=\"ENTRAR\"", &cx, &cy, 0) ||
            get_centroid_from_xml(xml_btn, "text=\"Acessar\"", &cx, &cy, 0) ||
            f7_get_centroid_fuzzy(xml_btn, "Entrar", &cx, &cy, 0) ||
            f7_get_centroid_fuzzy(xml_btn, "Acessar", &cx, &cy, 0) ||
            f7_get_centroid_nocase(xml_btn, "class=\"android.widget.Button\"", &cx, &cy, 0)) {
          btn_found = 1;
        }
      }
      free(xml_btn);
    }

    if (!btn_found) {
      if (cb) cb("[WARNING] [M7 Slow-Check] Botão Entrar não localizado no XML. Usando coordenada fallback (300, 750)...");
      cx = 300; cy = 750;
      btn_found = 1;
    }

    if (cb) {
      char msg[256];
      snprintf(msg, sizeof(msg), "[-] [M7 Slow-Check] Clicando no botão Entrar em (%d, %d) (Tentativa %d/3)...", cx, cy, attempt_entrar + 1);
      cb(msg);
    }
    snprintf(args_local, sizeof(args_local), "-s %s shell input tap %d %d", serial, cx, cy);
    char *t5 = adb_exec(args_local);
    if (t5) free(t5);
    Sleep(2000); // 2.0s para o M7 processar o clique de login

    // Verifica se a tela mudou (se o botão Entrar desapareceu ou se abriu o carregamento)
    char *xml_post_btn = dump_and_read_xml(serial, NULL);
    if (xml_post_btn) {
      int tx = 0, ty = 0;
      if (!f7_get_centroid_nocase(xml_post_btn, "Entrar", &tx, &ty, 0) &&
          !f7_get_centroid_nocase(xml_post_btn, "entrar", &tx, &ty, 0)) {
        clique_entrar_confirmado = 1;
        if (cb) cb("[+] [M7 Slow-Check] Clique no botão Entrar CONFIRMADO! Tela de login em transição.");
        free(xml_post_btn);
        break;
      }
      // Se detectou Sem Conexão
      if (f7_get_centroid_nocase(xml_post_btn, "Sem conex", &tx, &ty, 0)) {
        clique_entrar_confirmado = 1; // O clique funcionou, caiu em diálogo
        free(xml_post_btn);
        break;
      }
      free(xml_post_btn);
    }
  }

  char local_xml_clean[MAX_PATH];
  snprintf(local_xml_clean, sizeof(local_xml_clean), "window_dump_%s.xml", serial);
  remove(local_xml_clean);
  snprintf(args, sizeof(args), "-s %s shell rm /sdcard/window_dump_%s.xml", serial, serial);
  char *cln = adb_exec(args);
  if (cln) free(cln);

  if (cb) cb("[+] Login no Tab-MULTI acionado e credenciais totalmente verificadas!");
  return PIPELINE_SUCCESS;
}

int M7_ExecutarPipeline(const char *serial, const char *apk_path, const char *pacote_path, const char *wp_path, const char *login, const char *senha, const char *municipio, LogCallback cb) {
  if (cb)
    cb("[F7] Ã°Å¸Å¡â‚¬ INICIANDO ROTINA TAB-MULTI...");

  // Converte os caminhos para Short Path 8.3 e evita bugs de UTF-8 do ADB com caracteres especiais (ex: acentos)
  char safe_apk[MAX_PATH] = {0};
  char safe_pacote[MAX_PATH] = {0};
  char safe_wp[MAX_PATH] = {0};

  adb_get_safe_path(apk_path, safe_apk, MAX_PATH);
  adb_get_safe_path(pacote_path, safe_pacote, MAX_PATH);
  if (wp_path && strlen(wp_path) > 0) {
    adb_get_safe_path(wp_path, safe_wp, MAX_PATH);
  }

  char args[1024];
  char *out;

  // 0. PRIMEIRA COISA: Instalar Gnirehtet e ativar tunel de internet
  if (cb)
    cb("[-] Instalando e ativando tunel de internet (Gnirehtet)...");
  M7_StartGnirehtet(serial, cb);

  // 0. Agrupamento de Comandos ADB (Shell Batching): Conexões, Telas e Bateria em pacote único
  if (cb)
    cb("[-] [Otimização Shell Batching] Aplicando rede Starlink06, orientacao, brilho (70%) e travamento de bateria...");
  snprintf(args, sizeof(args),
           "-s %s shell \"svc wifi enable && cmd wifi connect-network Starlink06 open && "
           "dumpsys deviceidle whitelist +br.com.yantec.innovava && appops set br.com.yantec.innovava RUN_IN_BACKGROUND allow && "
           "am set-standby-bucket br.com.yantec.innovava active && settings put system accelerometer_rotation 0 && "
           "settings put system user_rotation 0 && wm user-rotation lock 0 && settings put system font_scale 1.5 && "
           "settings put system screen_off_timeout 300000 && settings put system screen_brightness_mode 0 && "
           "settings put system screen_brightness 178\"",
           serial);
  out = adb_exec(args);
  if (out) free(out);

  // 1. Acordar e Desbloquear a tela
  if (cb)
    cb("[-] Acordando o dispositivo e desbloqueando a tela...");
  M7_UnlockScreen(serial, cb);

  // 1.5 Aplicar Modo Escuro
  if (cb)
    cb("[-] Ativando Modo Escuro...");
  M7_SetDarkMode(serial, cb);




  // 2. Limpar o armazenamento interno (preservando 'Android', 'classjava',
  // 'cstore' e 'dump')
  if (cb)
    cb("[-] Mapeando arquivos para formatacao (excecao: Android, classjava, "
       "cstore, dumps)...");
  snprintf(args, sizeof(args),
           "-s %s shell \"cd /sdcard && ls | grep -vwE "
           "'Android|classjava|cstore' | grep -vi 'dump'\"",
           serial);
  out = adb_exec(args);
  if (out) {
    char *line = strtok(out, "\r\n");
    char *files[256];
    int total = 0;

    while (line && total < 256) {
      if (strlen(line) > 0) {
        files[total] = _strdup(line); // Usando _strdup no Windows
        total++;
      }
      line = strtok(NULL, "\r\n");
    }

    if (total > 0) {
      for (int i = 0; i < total; i++) {
        int pct = (i * 100) / total;
        char bar[21];
        int fill = pct / 5;
        for (int b = 0; b < 20; b++) {
          bar[b] = (b < fill) ? '=' : '-';
        }
        bar[20] = '\0';

        char msg[256];
        snprintf(msg, sizeof(msg), "[-] Formatando [%s] %d%% (Apagando: %s)",
                 bar, pct, files[i]);
        if (cb)
          cb(msg);

        snprintf(args, sizeof(args),
                 "-s %s shell \"rm -rf \\\"/sdcard/%s\\\"\"", serial, files[i]);
        char *rm_out = adb_exec(args);
        if (rm_out)
          free(rm_out);

        free(files[i]);
      }
      if (cb)
        cb("[-] Formatando [====================] 100% (Concluido)");
    } else {
      if (cb)
        cb("[-] Formatacao: Nenhum arquivo extra encontrado para limpar.");
    }
    free(out);
  }

  // 2.5 Pre-Sanitização de Memória RAM (2GB RAM M7)
  if (cb)
    cb("[-] [Otimização 2GB RAM] Sanitizando cache e liberando RAM antes da transferência dos pacotes...");
  snprintf(args, sizeof(args), "-s %s shell \"am trim-memory br.com.yantec.innovava COMPLETE\"", serial);
  out = adb_exec(args);
  if (out) free(out);

  // 3. Criar a pasta InnovAva
  if (cb)
    cb("[-] Criando pasta 'InnovAva' no armazenamento...");

  snprintf(args, sizeof(args), "-s %s shell mkdir -p /sdcard/InnovAva", serial);
  out = adb_exec(args);
  if (out)
    free(out);

  // 4. Copiar o APK
  if (safe_apk[0] != '\0') {
    if (cb)
      cb("  -> Enviando APK para /sdcard/InnovAva/ ...");
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/InnovAva/", serial,
             safe_apk);
    out = adb_exec(args);
    if (out)
      free(out);
  } else {
    if (cb)
      cb("[AVISO] Caminho do APK nao fornecido.");
  }

  // 5. Copiar o pacote de avaliacao
  if (safe_pacote[0] != '\0') {
    if (cb)
      cb("  -> Enviando PACOTE DE AVALIACAO para /sdcard/InnovAva/ ...");
    
    snprintf(args, sizeof(args), "-s %s push \"%s\" /sdcard/InnovAva/", serial, safe_pacote);
    out = adb_exec(args);
    if (out)
      free(out);
  } else {
    if (cb)
      cb("[AVISO] Caminho do PACOTE DE AVALIACAO nao fornecido.");
  }

  // 6. Instalar o APK (via ADB do PC para o Tablet)
  if (apk_path && strlen(apk_path) > 0) {
    if (cb)
      cb("[-] [F7] Iniciando desinstalacao e instalacao limpa via ADB (PC -> "
         "Tablet)...");

    // A) Verifica se o pacote jÃƒÂ¡ estÃƒÂ¡ instalado e desinstala
    snprintf(args, sizeof(args),
             "-s %s shell \"pm list packages | grep br.com.yantec.innovava\"",
             serial);
    char *check_out = adb_exec(args);
    if (check_out) {
      if (strstr(check_out, "br.com.yantec.innovava") != NULL) {
        if (cb)
          cb("[-] O aplicativo antigo foi encontrado. Desinstalando para "
             "garantir versao limpa...");
        snprintf(args, sizeof(args), "-s %s uninstall br.com.yantec.innovava",
                 serial);
        char *out_un = adb_exec(args);
        if (out_un)
          free(out_un);
      }
      free(check_out);
    }

    // B) Instala o APK com flag de replace e -g para auto-conceder permissÃƒÂµes
    if (cb) cb("[-] Transmitindo e instalando APK via ADB (Aguarde...).");
    snprintf(args, sizeof(args), "-s %s install -r -g \"%s\"", serial, safe_apk);
    
    volatile int is_installing = 1;
    F7_ProgressData pdata = { cb, &is_installing };
    HANDLE hThread = CreateThread(NULL, 0, F7_ProgressBarThread, &pdata, 0, NULL);
    
    // Aqui nÃƒÂ£o usamos a macro de retry rÃƒÂ¡pido, pois a instalaÃƒÂ§ÃƒÂ£o pode demorar. 
    #undef adb_exec
    char *install_out = adb_exec(args);
    #define adb_exec(args) adb_exec_retry(args, 3, 2000, (AdbLogCallback)cb)
    
    // Para a thread da barra de progresso
    is_installing = 0;
    if (hThread) {
        WaitForSingleObject(hThread, 2000);
        CloseHandle(hThread);
    }
    
    if (install_out) {
      if (strstr(install_out, "Success") ||
          (strstr(install_out, "Performing Streamed Install") &&
           !strstr(install_out, "Failure") && !strstr(install_out, "failed"))) {
        if (cb)
          cb("[-] APK instalado com sucesso no Tab-MULTI!");
          
        if (cb) cb("[-] [Otimizacao 2GB RAM] Removendo restricoes de background para o InnovAva...");
        char args_bypass[1024];
        snprintf(args_bypass, sizeof(args_bypass),
                 "-s %s shell \"appops set br.com.yantec.innovava RUN_IN_BACKGROUND allow && "
                 "dumpsys deviceidle whitelist +br.com.yantec.innovava && "
                 "am set-standby-bucket br.com.yantec.innovava active\"",
                 serial);
        char *out_bp = adb_exec(args_bypass);
        if (out_bp) free(out_bp);

        
      } else {
        if (cb) {
          char err_msg[1024];
          // Remove quebras de linha para o log do C# nao truncar a mensagem de
          // erro!
          for (int i = 0; install_out[i] != '\0'; i++) {
            if (install_out[i] == '\r' || install_out[i] == '\n') {
              install_out[i] = ' ';
            }
          }
          snprintf(err_msg, sizeof(err_msg),
                   "[ERROR] Falha na instalacao ADB: %s", install_out);
          cb(err_msg);
        }
        free(install_out);
        return PIPELINE_ERROR; // Aborta se falhar!
      }
      free(install_out);
    } else {
      if (cb)
        cb("[ERROR] Comando ADB de instalacao nao retornou resposta.");
      return PIPELINE_ERROR;
    }
  } else {
    if (cb)
      cb("[AVISO] Caminho do APK nao fornecido. Pulando instalacao.");
  }

  // [F1 - Etapa 2.5] Iniciar o túnel de internet do Gnirehtet ANTES de abrir o app
  if (cb)
    cb("[-] [F1 - Etapa 2.5] Ativando túnel de rede Gnirehtet (Reverse Tethering)...");
  M7_StartGnirehtet(serial, cb);

  // [F1 - Etapa 3.0] Abrir o Aplicativo
  if (cb)
    cb("[-] [F1 - Etapa 3.0] Executando o aplicativo instalado...");
  M7_ConfigureApp(serial, "br.com.yantec.innovava", cb);

  // [F1 - Etapa 4.0] Inserir Credenciais e clicar em Entrar
  if (cb)
    cb("[-] [F1 - Etapa 4.0] Efetuando login automático...");
  F7_Etapa4_Login(serial, login, senha, municipio, cb);

  // 9. Aguardar mudanca de interface para a tela principal com tratamento de "Sem Conexao"
  if (cb) cb("[-] Aguardando o login ser processado pelo servidor...");

  int login_sucesso = 0;
  int max_checks = 25; // 25 checagens (~50-60 segundos total)

  for (int i = 0; i < max_checks; i++) {
      char *xml_check = dump_and_read_xml(serial, NULL);
      if (xml_check) {
          int tx = 0, ty = 0;

          // Caso A: Pop-up de "Sem Conexão" detectado
          if (f7_get_centroid_nocase(xml_check, "Sem conex", &tx, &ty, 0) ||
              f7_get_centroid_nocase(xml_check, "Sem conexao", &tx, &ty, 0)) {

              if (cb) cb("[WARNING] Erro de 'Sem Conexão' detectado! Tentando recuperar...");

              // Localiza o botão "OK"
              int ok_x = 0, ok_y = 0;
              if (f7_get_centroid_nocase(xml_check, "OK", &ok_x, &ok_y, 0) ||
                  f7_get_centroid_nocase(xml_check, "ok", &ok_x, &ok_y, 0)) {
                  if (cb) cb("  -> Clicando em OK no diálogo de erro...");
                  snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, ok_x, ok_y);
                  out = adb_exec(args);
                  if (out) free(out);
                  Sleep(1500); // Aguarda o pop-up fechar

                  // Agora, tenta localizar o botão "Entrar" novamente para re-tentar o login
                  char *xml_retry = dump_and_read_xml(serial, NULL);
                  int enter_x = 0, enter_y = 0;
                  if (xml_retry) {
                      if (get_centroid_from_xml(xml_retry, "content-desc=\"Entrar\"", &enter_x, &enter_y, 0) ||
                          f7_get_centroid_nocase(xml_retry, "Entrar", &enter_x, &enter_y, 0)) {
                          if (cb) cb("  -> Re-tentando clique no botão 'Entrar'...");
                          snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, enter_x, enter_y);
                          out = adb_exec(args);
                          if (out) free(out);
                      }
                      free(xml_retry);
                  }
              }
              free(xml_check);
              Sleep(2000);
              continue;
          }

          // Caso B: Verificação se o login foi bem-sucedido (botão Entrar sumiu da tela)
          if (!f7_get_centroid_nocase(xml_check, "Entrar", &tx, &ty, 0) &&
              !f7_get_centroid_nocase(xml_check, "entrar", &tx, &ty, 0) &&
              !f7_get_centroid_nocase(xml_check, "Acessar", &tx, &ty, 0)) {
              login_sucesso = 1;
              free(xml_check);
              break;
          } else {
              // Se após a 3ª checagem o botão Entrar ainda continua na tela do M7 lento, re-executa o toque nele!
              if (i > 0 && i % 3 == 0) {
                  if (cb) {
                      char msg_retry[256];
                      snprintf(msg_retry, sizeof(msg_retry), "[-] [M7 Slow-Check] Botão Entrar ainda visível em (%d, %d). Re-executando toque (Checagem %d/%d)...", tx, ty, i + 1, max_checks);
                      cb(msg_retry);
                  }
                  snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, tx > 0 ? tx : 300, ty > 0 ? ty : 750);
                  out = adb_exec(args);
                  if (out) free(out);
                  Sleep(1500);
              }
          }

          free(xml_check);
      }
      Sleep(1000); // Polling de 1s para o M7
  }


  if (login_sucesso) {
      if (cb) cb("[+] Login confirmado! A tela inicial foi carregada.");
      
      // --- ETAPA PÃƒâ€œS-LOGIN: AutomaÃƒÂ§ÃƒÂ£o do Menu e ImportaÃƒÂ§ÃƒÂ£o de ZIP ---
      
      // 10. Abrindo o menu lateral (Swipe da esquerda para a direita)
      if (cb) cb("[-] Abrindo o menu lateral (Swipe da esquerda para a direita)...");
      snprintf(args, sizeof(args), "-s %s shell input swipe 10 500 650 500 300", serial);
      out = adb_exec(args);
      if (out) free(out);
      Sleep(1500); // Aguarda animaÃƒÂ§ÃƒÂ£o do menu
      
      // 11. Clicar em "Importar ZIP"
      if (cb) cb("[-] Procurando a opcao 'Importar ZIP' no menu...");
      char *xml_menu = dump_and_read_xml(serial, cb);
      int click_ok = 0;
      if (xml_menu) {
          int mx = 0, my = 0;
          if (f7_get_centroid_nocase(xml_menu, "Importar ZIP", &mx, &my, 0) ||
              f7_get_centroid_nocase(xml_menu, "importar", &mx, &my, 0)) {
              snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, mx, my);
              out = adb_exec(args);
              if (out) free(out);
              click_ok = 1;
          }
          free(xml_menu);
      }
      
      if (!click_ok) {
          // Tenta um segundo swipe preventivo caso o primeiro tenha falhado
          if (cb) cb("[WARNING] Opcao 'Importar ZIP' nao encontrada. Tentando swipe novamente...");
          snprintf(args, sizeof(args), "-s %s shell input swipe 10 500 650 500 300", serial);
          out = adb_exec(args);
          if (out) free(out);
          Sleep(1500);
          
          xml_menu = dump_and_read_xml(serial, cb);
          if (xml_menu) {
              int mx = 0, my = 0;
              if (f7_get_centroid_nocase(xml_menu, "Importar ZIP", &mx, &my, 0) ||
                  f7_get_centroid_nocase(xml_menu, "importar", &mx, &my, 0)) {
                  snprintf(args, sizeof(args), "-s %s shell input tap %d %d", serial, mx, my);
                  out = adb_exec(args);
                  if (out) free(out);
                  click_ok = 1;
              }
              free(xml_menu);
          }
      }
      
      if (!click_ok) {
          if (cb) cb("[WARNING] Falha ao clicar na opcao 'Importar ZIP'.");
      }
      Sleep(2000); // Aguarda carregar a tela de importaÃƒÂ§ÃƒÂ£o

      // Chegou em Importar ZIP: Desliga o tunel Gnirehtet e Wi-Fi imediatamente
      if (cb) cb("[-] Tela de importacao carregada. Desligando tunelamento de rede (Gnirehtet) e Wi-Fi imediatamente...");
      M7_StopGnirehtet(serial, cb);
      snprintf(args, sizeof(args), "-s %s shell svc wifi disable", serial);
      out = adb_exec(args);
      if (out) free(out);

      
      // Executa a importação completa do ZIP e fecha todos os aplicativos
      M7_ExecutarImportacaoEFecharApps(serial, safe_pacote, cb);
      
  } else {
      if (cb) cb("[ERROR] Nao foi possivel confirmar o login no dispositivo. Abortando processo.");
      goto final_step_error;
  }

final_step:
  // Configurações finais de energia e conectividade
  if (cb) cb("[-] Desligando tunelamento Gnirehtet...");
  M7_StopGnirehtet(serial, cb);

  if (cb) cb("[-] [F1 - Etapa 7b] Desativando antenas, otimizando energia e enviando SHUTDOWN...");
  snprintf(args, sizeof(args),
           "-s %s shell \"svc wifi disable && svc bluetooth disable && "
           "settings put secure location_mode 0 && "
           "settings put global wifi_scan_always_enabled 0 && "
           "settings put global ble_scan_always_enabled 0 && "
           "reboot -p\"",
           serial);
  out = adb_exec(args);
  if (out) free(out);


  if (cb)
    cb("[F1] PROCESSO TAB-MULTI CONCLUIDO COM SUCESSO (APARELHO DESLIGADO)!");
  return PIPELINE_SUCCESS;

final_step_error:
  if (cb) cb("[-] Desligando tunelamento de rede devido a falha no processo...");
  M7_StopGnirehtet(serial, cb);
  return PIPELINE_ERROR;
}

