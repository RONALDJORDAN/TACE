#include "device_parser.h"
#include <string.h>
#include <stdio.h>

int parse_devices(const char* adb_output, AndroidDevice* devices, int max_devices) {
    if (!adb_output || !devices) return 0;

    int count = 0;
    // O adb output vem no formato:
    // List of devices attached
    // 0000000000000000	device

    char buffer[4096];
    strncpy(buffer, adb_output, sizeof(buffer) - 1);
    buffer[sizeof(buffer)-1] = '\0';

    char *line = strtok(buffer, "\n");
    while (line != NULL && count < max_devices) {
        if (strstr(line, "List of devices attached") || strlen(line) < 3) {
            line = strtok(NULL, "\n");
            continue;
        }

        char serial[128] = {0};
        char status[64] = {0};
        
        if (sscanf(line, "%127s %63s", serial, status) == 2) {
            if (strcmp(status, "device") == 0) {
                strncpy(devices[count].serial, serial, sizeof(devices[count].serial) - 1);
                strncpy(devices[count].status, status, sizeof(devices[count].status) - 1);
                count++;
            }
        }

        line = strtok(NULL, "\n");
    }

    return count;
}
