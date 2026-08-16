#ifndef ADB_MANAGER_H
#define ADB_MANAGER_H

#include <stdio.h>

// Verifica se o ADB estÃ¡ disponÃ­vel ou se o executÃ¡vel existe no caminho configurado
int check_adb_availability();

// Executa 'adb devices' e retorna a string de saÃ­da contendo os aparelhos
char* execute_adb_devices();

#endif // ADB_MANAGER_H
