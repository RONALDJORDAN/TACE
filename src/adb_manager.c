#include "adb_manager.h"
#include <stdlib.h>
#include <string.h>
#include <windows.h>

#define ADB_COMMAND "adb devices"

char adb_local_path[1024];
char adb_local_cmd[1024];

int check_adb_availability() {
    int result = system("adb version > NUL 2>&1");
    if (result == 0) return 1;

    // Pega o diretÃ³rio do executÃ¡vel (data\output)
    char exe_path[MAX_PATH];
    GetModuleFileName(NULL, exe_path, MAX_PATH);
    char* last_slash = strrchr(exe_path, '\\');
    if (last_slash) *last_slash = '\0';
    
    // Resolve o caminho subindo duas pastas: data\output -> Inno_RPA -> tools\platform-tools
    sprintf(adb_local_path, "%s\\..\\..\\tools\\platform-tools\\adb.exe", exe_path);
    sprintf(adb_local_cmd, "\"%s\" version > NUL 2>&1", adb_local_path);
    
    result = system(adb_local_cmd);
    if (result == 0) return 2;

    // Fallback: Tenta no diretorio atual (se estiver rodando da raiz)
    result = system("tools\\platform-tools\\adb.exe version > NUL 2>&1");
    if (result == 0) return 3;

    return 0;
}

char* execute_adb_devices() {
    int adb_status = check_adb_availability();
    if (adb_status == 0) {
        printf("ERRO: ADB nao encontrado. Por favor, execute o script scripts\\setup_adb.bat\n");
        return NULL;
    }

    char cmd[1024];
    if (adb_status == 1) strcpy(cmd, ADB_COMMAND);
    else if (adb_status == 2) sprintf(cmd, "\"%s\" devices", adb_local_path);
    else if (adb_status == 3) strcpy(cmd, "tools\\platform-tools\\adb.exe devices");
    
    FILE *fp;
    char path[1035];
    char* output = malloc(4096);
    if (!output) return NULL;
    output[0] = '\0';

    fp = _popen(cmd, "r");
    if (fp == NULL) {
        printf("Falha ao executar comando ADB\n");
        free(output);
        return NULL;
    }

    while (fgets(path, sizeof(path), fp) != NULL) {
        strcat(output, path);
    }

    _pclose(fp);
    return output;
}
