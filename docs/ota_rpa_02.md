# OTA_RPA_02 — Atualização OTA pública só-leitura (sem login)

**Projeto:** Bot_RPA — Plataforma Bot_RPA
**Data:** 28/09/2026
**Escopo:** tornar a verificação/instalação de atualização OTA um caminho único de APENAS LEITURA, sem exigir login Firebase, mantendo todo o resto do banco InnovStock protegido por autenticação.
**Status:** código implementado + build validado. Pendente (operador): publicar as regras abaixo no Firebase Console.

---

## 1. Pedido do usuário

> Para atualizar via OTA o sistema não precisar de login ou algo parecido — ser mais um caminho único de apenas leitura — para que o resto do banco (InnovStock, com diversos dados e ferramentas) tenha segurança.

---

## 2. Diagnóstico (auditoria OTA do dia 28/09/2026)

Antes da mudança, `CheckForUpdatesAsync` e `SendTelemetryPingAsync` passavam por `FirebaseAuthService` (exigia `firebase_email/senha`). Sem credencial, o update falhava silenciosamente e a UI dizia "você está na versão mais recente" (falso-positivo). Leitura anônima de `ota/manifest.json` retornava **401** (confirmado via teste HTTP).

Outros bugs mapeados na auditoria (fora do escopo deste documento, pendentes):
- `web/ota_dashboard.html` deletado no commit `2e81bb5`, mas `controle/Atualizacao.*` ainda referencia o painel.
- Layout do ZIP OTA (gera `version.txt` na raiz; o leitor espera `config/version.txt`) — risco de loop de update.
- `scripts/apply_ota_update.bat` quebra com espaços no caminho; script ausente na Release.
- Flag `manifest.mandatory` ignorada; fallback de `CurrentVersion` suprime updates; telemetria sem opt-out; `build.bat` bumpa versão a cada build de teste; senha Firebase em plaintext no `settings.json`.

---

## 3. Alteração de código (só `src/ui/Core/OtaUpdateManager.cs`)

Nenhum `.c` de dispositivo tocado (Regra de Ouro #1 — isolamento por modelo preservado).

- **`CheckForUpdatesAsync`**: GET anônimo direto via `HttpClient` em `ota/manifest.json` (sem login, sem token). Mantém fallback autenticado só se houver credenciais (compat com DB ainda privado).
- **`SendTelemetryPingAsync`**: PUT anônimo best-effort + fallback autenticado. Falha silenciosa, nunca bloqueia o update.
- **Download do ZIP** (`DownloadAndVerifyUpdateAsync`): já era anônimo (sem auth) — inalterado.
- **Sync InnovStock intocado:** `SQLiteDatabaseManager.cs` (linhas 571, 614, 659, 723) continua 100% via `FirebaseAuthService` autenticado.

**Validação:** `scripts/build.bat` — compilação TESTE `28.09.2026.0830`, Zero Errors / Zero Warnings. Auditoria: `registros/2026-09-28/08-31_Plataforma_BotRPA_DESKTOP_BUILD`. Registro cronológico em `memoria_bot_rpa.md` (entrada `[2026-09-28 08:30]`).

---

## 4. Regras Firebase — texto final VALIDADO (publicar no console)

**Onde:** console.firebase.google.com → projeto `matematica-em-foco` → Realtime Database → aba **Rules** → colar (bloco `ota` inserido entre `rpa_devices` e `audit_logs`, resto byte a byte igual) → **Publish**.

```json
{
  "rules": {
    ".read": false,
    ".write": false,

    "user_emails": {
      ".read": true,
      "$key": {
        ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador')",
        ".validate": "newData.isString() && newData.val().contains('@')"
      }
    },

    "users": {
      ".read": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador')",
      "$uid": {
        ".read": "auth != null && (auth.uid === $uid || root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador')",
        ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || (auth.uid === $uid && newData.exists() && (!newData.child('role').exists() || newData.child('role').val() === data.child('role').val()) && (!newData.child('status').exists() || newData.child('status').val() === data.child('status').val()) && (data.exists() || !root.child('users').exists())))",
        ".validate": "newData.hasChildren(['email', 'role']) && newData.child('email').isString() && (newData.child('role').val() === 'admin' || newData.child('role').val() === 'admin_operacional' || newData.child('role').val() === 'Administrador' || newData.child('role').val() === 'operacional' || newData.child('role').val() === 'aplicador' || newData.child('role').val() === 'visualizador') && (!newData.hasChild('password') || (data.hasChild('password') && newData.child('password').val() === data.child('password').val())) && (!newData.hasChild('passHash') || (data.hasChild('passHash') && newData.child('passHash').val() === data.child('passHash').val())) && (!newData.hasChild('pass') || (data.hasChild('pass') && newData.child('pass').val() === data.child('pass').val())) && (!newData.hasChild('senha') || (data.hasChild('senha') && newData.child('senha').val() === data.child('senha').val())) && (!newData.hasChild('hash') || (data.hasChild('hash') && newData.child('hash').val() === data.child('hash').val()))"
      }
    },

    "employees": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "inventory": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "operational_assets": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "materials": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "movements": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "cities": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "schools": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "classes": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "applications": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "calendar_events": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "sticky_notes": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },

    "rpa_devices": {
      ".read": "auth != null",
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')"
    },
    "ota": {
      "manifest": {
        ".read": true,
        ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador')",
        ".validate": "newData.hasChildren(['version', 'download_url', 'sha256'])"
      },
      "devices": {
        ".read": "auth != null",
        "$deviceId": {
          ".write": "auth != null",
          ".validate": "newData.hasChildren(['hostname', 'version', 'last_seen', 'status'])"
        }
      }
    },

    "audit_logs": {
      ".read": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador')",
      "$logId": {
        ".write": "auth != null && !data.exists()",
        ".validate": "newData.hasChildren(['action', 'timestamp', 'epoch'])"
      }
    },

    "access_logs": {
      ".read": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador')",
      "$logId": {
        ".write": "auth != null && !data.exists()",
        ".validate": "newData.hasChildren(['uid', 'timestamp'])"
      }
    },

    "doc_proofs": {
      ".read": true,
      ".write": "auth != null && (root.child('users').child(auth.uid).child('role').val() === 'admin' || root.child('users').child(auth.uid).child('role').val() === 'admin_operacional' || root.child('users').child(auth.uid).child('role').val() === 'Administrador' || root.child('users').child(auth.uid).child('role').val() === 'operacional' || root.child('users').child(auth.uid).child('role').val() === 'aplicador')",
      "$proofId": {
        ".validate": "newData.hasChildren(['protocol', 'hash'])"
      }
    }
  }
}
```

### Resultado de segurança

| Caminho | Leitura | Escrita |
|---|---|---|
| `ota/manifest` | pública (app lê sem login) | só admin / admin_operacional / Administrador |
| `ota/devices` (telemetria) | só logado | só logado |
| Todo o resto | inalterado (fechado) | inalterado (fechado) |

Publicar nova versão: logado, via console (aba Data, que sobrepõe as regras) ou ferramenta autenticada. Ninguém anônimo adultera o manifesto.

---

## 5. Verificação pós-Publish

1. Aba anônima: `https://matematica-em-foco-default-rtdb.firebaseio.com/ota/manifest.json` → retorna o JSON (antes: 401).
2. Aba anônima: `.../rpa_devices.json` → continua `Permission denied`.
3. No app sem conta configurada: Configurações → OTA → Verificar Atualizações → funciona.
