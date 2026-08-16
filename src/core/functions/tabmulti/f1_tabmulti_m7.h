#ifndef F1_TABMULTI_M7_H
#define F1_TABMULTI_M7_H

#include "../pipeline.h"
#include "m7_helpers.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_db.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

// Pipeline F1 / F7: Instalação Completa Multilaser TabMulti M7
int M7_ExecutarPipeline(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* login, const char* senha, const char* municipio, LogCallback cb);

// Etapa 4: Login no TabMulti M7
int F7_Etapa4_Login(const char *serial, const char *login, const char *senha, const char *municipio, LogCallback cb);

// Funções auxiliares de infraestrutura M7
int M7_UnlockScreen(const char *serial, LogCallback cb);
int M7_SetDarkMode(const char *serial, LogCallback cb);
int M7_StartGnirehtet(const char *serial, LogCallback cb);
int M7_StopGnirehtet(const char *serial, LogCallback cb);
int M7_ConfigureApp(const char *serial, const char *package_name, LogCallback cb);

#endif // F1_TABMULTI_M7_H
