#ifndef PATHS_H
#define PATHS_H

#include <windows.h>

// ============================================================================
//  RESOLUCAO DE PASTAS: PROGRAMAS (instalacao) x DADOS (usuario)
// ============================================================================
//  O TACE grava na propria pasta: banco de dispositivos, cofre de
//  credenciais (municipios.csv), pacotes dos municipios, logs e registros.
//
//  Em instalacao em "C:\Program Files" essa pasta nao e gravavel pelo
//  usuario comum. Por isso o instalador define a variavel de ambiente
//  machine-wide TACE_DATA_DIR apontando para %ProgramData%\TACE.
//
//  Resolucao (identica a PathHelper.GetRootDirectory do lado C#):
//    1) TACE_DATA_DIR (definida pelo instalador);
//    2) a propria pasta do executavel, se for gravavel (modo portatil);
//    3) %ProgramData%\TACE.
//
//  O mesmo valor e gravado no arquivo "%DATA_DIR%\data_dir.txt" para que
//  o lado C# e o lado C concordem mesmo sem a variavel de ambiente.
// ============================================================================

// Retorna a pasta de DADOS do usuario (criada se necessario).
void paths_get_data_dir(char* out_path, int max_len);

// Retorna a pasta de PROGRAMAS (onde estao os binarios e tools/).
void paths_get_install_dir(char* out_path, int max_len);

// Garante que a pasta exista no disco.
void paths_ensure_dir(const char* path);

#endif // PATHS_H
