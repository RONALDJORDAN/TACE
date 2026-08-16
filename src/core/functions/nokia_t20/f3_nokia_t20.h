#ifndef F3_NOKIA_T20_H
#define F3_NOKIA_T20_H

#include "../pipeline.h"

// Pipeline F3: Atualização de Pacote Express Nokia T20
int T20_ExecutarPipelineF3_AtualizarPacote(const char* serial, const char* apk_path, const char* pacote_path, const char* package_name, LogCallback cb);

#endif // F3_NOKIA_T20_H
