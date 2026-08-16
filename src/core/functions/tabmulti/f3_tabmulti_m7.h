#ifndef F3_TABMULTI_M7_H
#define F3_TABMULTI_M7_H

#include "../pipeline.h"

// Pipeline F3: Atualização de Pacote & Terminal Kiosk Multilaser TabMulti M7
int M7_ExecutarPipelineF3_AtualizarPacote(const char *serial, const char *apk_path, const char *pacote_path, const char *package_name, LogCallback cb);
int M7_Terminal(const char *serial, LogCallback cb);

#endif // F3_TABMULTI_M7_H
