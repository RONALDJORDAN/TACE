#ifndef F4_VISION_TAB7_H
#define F4_VISION_TAB7_H

#include "../pipeline.h"

// Pipeline F4: Terminal Kiosk e Aplicação de Wallpaper Positivo Vision 7
int F4_ExecutarFerramenta(const char* serial, int acao, LogCallback cb);
int F4_DefinirWallpaper(const char* serial, const char* wp_path, LogCallback cb);

#endif // F4_VISION_TAB7_H
