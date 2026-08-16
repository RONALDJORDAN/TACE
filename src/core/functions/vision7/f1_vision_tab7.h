#ifndef F1_VISION_TAB7_H
#define F1_VISION_TAB7_H

#include "../pipeline.h"

// Orquestrador principal F1 (Instalação Completa Positivo Vision 7)
int V7_ExecutarPipelineCompleto(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb);

// Etapas individuais exportadas
int V7_Etapa1(const char *serial, LogCallback cb);
int V7_Etapa1_2_DarkMode(const char *serial, LogCallback cb);
int V7_Etapa1_5(const char *serial, LogCallback cb);
int V7_Etapa1_6(const char *serial, const char *apk_path, const char *pacote_path, LogCallback cb);
int V7_Etapa1_7(const char *serial, LogCallback cb);
int V7_Etapa1_9_ClearLauncher(const char *serial, LogCallback cb);
int V7_Etapa2(const char *serial, const char *apk_path, const char *package_name, LogCallback cb);
int V7_Etapa2_1_AddShortcut(const char *serial, const char *package_name, LogCallback cb);
int V7_Etapa2_5(const char *serial, LogCallback cb);
int V7_Etapa3(const char *serial, const char *package_name, LogCallback cb);
int V7_Etapa4(const char *serial, const char *login, const char *senha, const char *municipio, LogCallback cb);
int V7_Etapa4_5_StopGnirehtet(const char *serial, LogCallback cb);
int V7_Etapa5(const char *serial, LogCallback cb);
int V7_Etapa6(const char *serial, LogCallback cb);
int V7_Etapa7_PowerOff(const char *serial, LogCallback cb);

// Helpers exportados
int get_centroid_from_xml(const char *xml, const char *search_str, int *out_cx, int *out_cy, int skip_count);
char *dump_and_read_xml(const char *serial, LogCallback cb);

#endif // F1_VISION_TAB7_H
