#ifndef DEVICE_PARSER_H
#define DEVICE_PARSER_H

typedef struct {
    char serial[128];
    char status[64];
} AndroidDevice;

// Faz o parse do output do ADB e popula o array de aparelhos
// Retorna a quantidade de aparelhos conectados com status "device"
int parse_devices(const char* adb_output, AndroidDevice* devices, int max_devices);

#endif // DEVICE_PARSER_H
