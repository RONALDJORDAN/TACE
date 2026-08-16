#ifndef M7_HELPERS_H
#define M7_HELPERS_H

#include "../pipeline.h"

int M7_UnlockScreen(const char *serial, LogCallback cb);
int M7_SetDarkMode(const char *serial, LogCallback cb);
int M7_StartGnirehtet(const char *serial, LogCallback cb);
int M7_StopGnirehtet(const char *serial, LogCallback cb);
int M7_ConfigureApp(const char *serial, const char *package_name, LogCallback cb);

#endif // M7_HELPERS_H
