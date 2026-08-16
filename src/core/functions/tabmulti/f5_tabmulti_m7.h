#ifndef F5_TABMULTI_M7_H
#define F5_TABMULTI_M7_H

#include "../pipeline.h"
#include "f1_tabmulti_m7.h"
#include "m7_helpers.h"
#include "../../drivers/adb.h"
#include "../../learn/learn_db.h"
#include "../../learn/learn_engine.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

// Pipeline F5: Multilaser TabMulti M7 - Provisionamento, Ativação de Túnel Gnirehtet, Autenticação, Espera de 30s e Desligamento Total
int M7_ExecutarPipelineF5(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* login, const char* senha, const char* municipio, LogCallback cb);

#endif // F5_TABMULTI_M7_H
