#include "paths.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

// Pasta de DADOS resolvida (cache em memoria do processo).
static char g_data_dir[MAX_PATH] = {0};

// Pasta de PROGRAMAS (onde estao os binarios).
static char g_install_dir[MAX_PATH] = {0};

// Testa se e possivel gravar um arquivo na pasta informada.
static int paths_is_writable(const char* dir) {
    char probe[MAX_PATH];
    snprintf(probe, sizeof(probe), "%s\\.TACE_write_test.tmp", dir);

    HANDLE h = CreateFileA(probe, GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                            NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY, NULL);
    if (h == INVALID_HANDLE_VALUE) return 0;

    CloseHandle(h);
    DeleteFileA(probe);
    return 1;
}

void paths_ensure_dir(const char* path) {
    if (!path || !*path) return;
    CreateDirectoryA(path, NULL);
}

void paths_get_install_dir(char* out_path, int max_len) {
    if (!out_path || max_len <= 0) return;
    if (g_install_dir[0] != '\0') {
        strncpy(out_path, g_install_dir, max_len - 1);
        out_path[max_len - 1] = '\0';
        return;
    }

    char exe_path[MAX_PATH];
    GetModuleFileNameA(NULL, exe_path, sizeof(exe_path));
    char* p = strrchr(exe_path, '\\');
    if (p) *p = '\0';

    strncpy(g_install_dir, exe_path, sizeof(g_install_dir) - 1);
    g_install_dir[sizeof(g_install_dir) - 1] = '\0';
    strncpy(out_path, g_install_dir, max_len - 1);
    out_path[max_len - 1] = '\0';
}

void paths_get_data_dir(char* out_path, int max_len) {
    if (!out_path || max_len <= 0) return;
    if (g_data_dir[0] != '\0') {
        strncpy(out_path, g_data_dir, max_len - 1);
        out_path[max_len - 1] = '\0';
        return;
    }

    char resolved[MAX_PATH] = {0};

    // 1) Variavel de ambiente definida pelo instalador
    const char* env = getenv("TACE_DATA_DIR");
    if (env && env[0] != '\0') {
        DWORD attr = GetFileAttributesA(env);
        if (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY)) {
            strncpy(resolved, env, sizeof(resolved) - 1);
        }
    }

    // 2) Modo portatil: a propria pasta do executavel, se gravavel
    if (resolved[0] == '\0') {
        char install[MAX_PATH];
        paths_get_install_dir(install, sizeof(install));
        if (paths_is_writable(install)) {
            strncpy(resolved, install, sizeof(resolved) - 1);
        }
    }

    // 3) Instalacao em Program Files: %ProgramData%\TACE
    if (resolved[0] == '\0') {
        char program_data[MAX_PATH] = {0};
        DWORD r = GetEnvironmentVariableA("ProgramData", program_data, sizeof(program_data));
        if (r == 0 || r >= sizeof(program_data)) {
            program_data[0] = '\0';
        }
        if (program_data[0] != '\0') {
            snprintf(resolved, sizeof(resolved), "%s\\TACE", program_data);
        } else {
            snprintf(resolved, sizeof(resolved), "%s\\TACE", "C:\\ProgramData");
        }
    }

    paths_ensure_dir(resolved);

    strncpy(g_data_dir, resolved, sizeof(g_data_dir) - 1);
    g_data_dir[sizeof(g_data_dir) - 1] = '\0';
    strncpy(out_path, g_data_dir, max_len - 1);
    out_path[max_len - 1] = '\0';
}
