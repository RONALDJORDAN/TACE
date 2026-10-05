#include "cofre.h"
#include <windows.h>
#include <wincrypt.h>
#include <stdio.h>
#include <string.h>

// Entropia adicional amarra o blob a esta aplicação (não é segredo).
static const char COFRE_ENTROPIA[] = "BotRPA-InnovTech-Cofre-v1";

// --- Base64 (suficiente para blobs DPAPI; sem dependências) ---
static const char B64_ALFABETO[] =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

static int b64_codificar(const unsigned char *in, int in_len, char *out,
                         int max_len) {
  int o = 0;
  for (int i = 0; i < in_len; i += 3) {
    unsigned int n = (unsigned int)in[i] << 16;
    int grupo = in_len - i;
    if (grupo > 1) n |= (unsigned int)in[i + 1] << 8;
    if (grupo > 2) n |= (unsigned int)in[i + 2];
    if (o + 4 >= max_len) return 0;
    out[o++] = B64_ALFABETO[(n >> 18) & 0x3F];
    out[o++] = B64_ALFABETO[(n >> 12) & 0x3F];
    out[o++] = (grupo > 1) ? B64_ALFABETO[(n >> 6) & 0x3F] : '=';
    out[o++] = (grupo > 2) ? B64_ALFABETO[n & 0x3F] : '=';
  }
  if (o >= max_len) return 0;
  out[o] = '\0';
  return 1;
}

static int b64_valor(char c) {
  if (c >= 'A' && c <= 'Z') return c - 'A';
  if (c >= 'a' && c <= 'z') return c - 'a' + 26;
  if (c >= '0' && c <= '9') return c - '0' + 52;
  if (c == '+') return 62;
  if (c == '/') return 63;
  return -1;
}

static int b64_decodificar(const char *in, unsigned char *out, int max_len) {
  int len = (int)strlen(in);
  if (len % 4 != 0) return -1;
  int o = 0;
  for (int i = 0; i < len; i += 4) {
    int v[4];
    for (int k = 0; k < 4; k++) {
      if (in[i + k] == '=') {
        v[k] = 0;
      } else {
        v[k] = b64_valor(in[i + k]);
        if (v[k] < 0) return -1;
      }
    }
    if (o + 3 > max_len) return -1;
    out[o++] = (unsigned char)((v[0] << 2) | (v[1] >> 4));
    if (in[i + 2] != '=') out[o++] = (unsigned char)((v[1] << 4) | (v[2] >> 2));
    if (in[i + 3] != '=') out[o++] = (unsigned char)((v[2] << 6) | v[3]);
  }
  return o;
}

int cofre_eh_protegido(const char *campo) {
  if (!campo) return 0;
  return strncmp(campo, COFRE_PREFIXO, strlen(COFRE_PREFIXO)) == 0;
}

int cofre_proteger(const char *texto, char *out_b64, int max_len) {
  if (!texto || !out_b64 || max_len <= 0) return 0;
  if (texto[0] == '\0') return 0;
  if (cofre_eh_protegido(texto)) return 0; // idempotência: já protegido

  DATA_BLOB entrada, saida, entropia;
  entrada.pbData = (BYTE *)texto;
  entrada.cbData = (DWORD)(strlen(texto) + 1); // inclui terminador
  entropia.pbData = (BYTE *)COFRE_ENTROPIA;
  entropia.cbData = (DWORD)(sizeof(COFRE_ENTROPIA) - 1);
  memset(&saida, 0, sizeof(saida));

  if (!CryptProtectData(&entrada, L"BotRPA", &entropia, NULL, NULL,
                        CRYPTPROTECT_UI_FORBIDDEN, &saida)) {
    return 0;
  }

  int prefixo = (int)strlen(COFRE_PREFIXO);
  int ok = 0;
  if (prefixo + ((int)saida.cbData + 2) / 3 * 4 + 1 <= max_len) {
    memcpy(out_b64, COFRE_PREFIXO, prefixo);
    ok = b64_codificar(saida.pbData, (int)saida.cbData, out_b64 + prefixo,
                       max_len - prefixo);
  }
  SecureZeroMemory(saida.pbData, saida.cbData);
  LocalFree(saida.pbData);
  if (!ok) out_b64[0] = '\0';
  return ok;
}

int cofre_revelar(const char *protegido, char *out_texto, int max_len) {
  if (!protegido || !out_texto || max_len <= 0) return 0;
  if (!cofre_eh_protegido(protegido)) return 0;

  const char *b64 = protegido + strlen(COFRE_PREFIXO);
  unsigned char bruto[COFRE_MAX_B64];
  int bruto_len = b64_decodificar(b64, bruto, sizeof(bruto));
  if (bruto_len <= 0) return 0;

  DATA_BLOB entrada, saida, entropia;
  entrada.pbData = bruto;
  entrada.cbData = (DWORD)bruto_len;
  entropia.pbData = (BYTE *)COFRE_ENTROPIA;
  entropia.cbData = (DWORD)(sizeof(COFRE_ENTROPIA) - 1);
  memset(&saida, 0, sizeof(saida));

  int ok = 0;
  if (CryptUnprotectData(&entrada, NULL, &entropia, NULL, NULL,
                         CRYPTPROTECT_UI_FORBIDDEN, &saida)) {
    if ((int)saida.cbData < max_len) {
      memcpy(out_texto, saida.pbData, saida.cbData);
      out_texto[saida.cbData] = '\0';
      ok = 1;
    }
    SecureZeroMemory(saida.pbData, saida.cbData);
    LocalFree(saida.pbData);
  }
  SecureZeroMemory(bruto, sizeof(bruto));
  if (!ok) out_texto[0] = '\0';
  return ok;
}
