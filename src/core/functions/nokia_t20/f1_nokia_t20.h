#ifndef F1_NOKIA_T20_H
#define F1_NOKIA_T20_H

#include "../pipeline.h"

// Orquestrador principal Nokia T20 - Modo F1 Completo
int T20_ExecutarPipelineCompleto(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb);

// Etapas individuais exportadas Nokia T20
int T20_Etapa1(const char *serial, LogCallback cb);
int T20_Etapa1_2_DarkMode(const char *serial, LogCallback cb);
int T20_Etapa1_5(const char *serial, LogCallback cb);
int T20_Etapa1_6(const char *serial, const char *apk_path, const char *pacote_path, LogCallback cb);
int T20_Etapa1_7(const char *serial, LogCallback cb);
int T20_Etapa1_9_ClearLauncher(const char *serial, LogCallback cb);
int T20_Etapa2(const char *serial, const char *apk_path, const char *package_name, LogCallback cb);
int T20_Etapa2_1_AddShortcut(const char *serial, const char *package_name, LogCallback cb);
int T20_Etapa2_5(const char *serial, LogCallback cb);
int T20_Etapa3(const char *serial, const char *package_name, LogCallback cb);
int T20_Etapa4(const char *serial, const char *login, const char *senha, const char *municipio, LogCallback cb);
int T20_Etapa4_5_StopGnirehtet(const char *serial, LogCallback cb);
int T20_Etapa5(const char *serial, LogCallback cb);
int T20_Etapa6(const char *serial, LogCallback cb);
int T20_Etapa6_AguardarConclusaoImportacao(const char *serial, LogCallback cb);
int T20_Etapa7_CloseApps(const char *serial, LogCallback cb);
int T20_Etapa7_PowerOff(const char *serial, LogCallback cb);

#endif // F1_NOKIA_T20_H
