#ifndef COFRE_H
#define COFRE_H

// =============================================================================
//  COFRE DPAPI: criptografia local de credenciais (login/senha de municípios)
// =============================================================================
//  - Usa DPAPI do Windows (CryptProtectData, escopo CURRENT_USER): só o mesmo
//    usuário nesta máquina descriptografa. Sem senha mestra.
//  - Saída em texto com prefixo "DPAPIv1:" + Base64 (seguro para CSV ';').
//  - Idempotente: campos já protegidos são ignorados na proteção.
//  - Falhas nunca destroem dados: proteger() falha -> mantém texto original;
//    revelar() falha -> mantém o blob (chamador decide).
// =============================================================================

#define COFRE_PREFIXO "DPAPIv1:"
#define COFRE_MAX_B64 1024

// Protege `texto` -> `out_b64` (com prefixo). Retorna 1 ok / 0 falha.
int cofre_proteger(const char *texto, char *out_b64, int max_len);

// Revela campo com prefixo -> `out_texto`. Retorna 1 ok / 0 falha.
int cofre_revelar(const char *protegido, char *out_texto, int max_len);

// 1 se o campo já está protegido (tem o prefixo), 0 caso contrário.
int cofre_eh_protegido(const char *campo);

#endif // COFRE_H
