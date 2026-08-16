#ifndef F2_NOKIA_T20_H
#define F2_NOKIA_T20_H

#include "../pipeline.h"

// Pipeline F2: Reinstalação Limpa Nokia T20
int T20_ExecutarPipelineF2(const char* serial, const char* apk_path, const char* pacote_path, const char* wp_path, const char* package_name, const char* login, const char* senha, const char* municipio, LogCallback cb);

#endif // F2_NOKIA_T20_H
