#ifndef DISPOSITIVOS_H
#define DISPOSITIVOS_H

#include "../drivers/adb.h"


#define MAX_REGISTROS 1000

typedef struct {
    char serial[MAX_SERIAL];
    char manufacturer[32];
    char model[MAX_MODEL];
    char android_version[16];
    char api_level[16];
    char battery[16];
    char imei[MAX_IMEI];
    char mac[MAX_MAC];
    char primeiro_acesso[64];
} DispositivoRegistro;

// Retorna a quantidade de registros lidos
int dispositivos_carregar(DispositivoRegistro* lista, int max);

// Registra um dispositivo se ele ainda nÃ£o existir no CSV
void dispositivos_registrar(AdbDevice* dev);

#endif
