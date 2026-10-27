/*
 * ControleGeralLauncher
 *
 * Executavel nativo (sem dependencia de .NET) que localiza e executa o
 * ControleGeral.bat mantendo o mesmo menu, cores e comportamento interativo.
 *
 * Pode ser colocado:
 *   - na raiz do projeto
 *   - na mesma pasta do ControleGeral.bat
 *   - em qualquer subpasta do projeto (procura nos diretorios superiores)
 */

#include <windows.h>
#include <shellapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <wchar.h>

#define MAX_CMD 32768

static BOOL FileExistsW(const wchar_t *path)
{
    DWORD attrs = GetFileAttributesW(path);
    return attrs != INVALID_FILE_ATTRIBUTES && !(attrs & FILE_ATTRIBUTE_DIRECTORY);
}

static void RemoveFileName(wchar_t *path)
{
    wchar_t *slash = wcsrchr(path, L'\\');
    if (slash) *slash = L'\0';
}

static BOOL TryPath(const wchar_t *dir, const wchar_t *relative, wchar_t *found, size_t foundCount)
{
    if (swprintf(found, foundCount, L"%ls\\%ls", dir, relative) < 0)
        return FALSE;

    return FileExistsW(found);
}

static BOOL FindControleGeral(wchar_t *found, size_t foundCount)
{
    static const wchar_t *relativePaths[] = {
        L"ControleGeral.bat",
        L"scripts\\scripts de controle\\ControleGeral.bat"
    };
    const size_t relativeCount = sizeof(relativePaths) / sizeof(relativePaths[0]);

    wchar_t base[MAX_PATH];
    if (GetModuleFileNameW(NULL, base, MAX_PATH) == 0)
        return FALSE;
    RemoveFileName(base);

    for (size_t i = 0; i < relativeCount; i++)
    {
        if (TryPath(base, relativePaths[i], found, foundCount))
            return TRUE;
    }

    wchar_t dir[MAX_PATH];
    wcsncpy(dir, base, MAX_PATH);
    dir[MAX_PATH - 1] = L'\0';

    for (int level = 0; level < 6; level++)
    {
        RemoveFileName(dir);
        if (dir[0] == L'\0' || (wcslen(dir) == 2 && dir[1] == L':'))
            break;

        for (size_t i = 0; i < relativeCount; i++)
        {
            if (TryPath(dir, relativePaths[i], found, foundCount))
                return TRUE;
        }
    }

    return FALSE;
}

static void AppendArgument(wchar_t *cmd, size_t cmdCount, const wchar_t *arg)
{
    size_t len = wcslen(cmd);
    if (len + 4 >= cmdCount)
        return;

    cmd[len++] = L' ';
    cmd[len++] = L'"';
    cmd[len] = L'\0';

    wcsncat(cmd, arg, cmdCount - len - 1);

    len = wcslen(cmd);
    if (len + 1 < cmdCount)
    {
        cmd[len++] = L'"';
        cmd[len] = L'\0';
    }
}

int main(void)
{
    int argc = 0;
    wchar_t **argv = CommandLineToArgvW(GetCommandLineW(), &argc);

    wchar_t script[MAX_PATH];
    if (!FindControleGeral(script, MAX_PATH))
    {
        wprintf(L"[ERRO] ControleGeral.bat nao encontrado.\n");
        wprintf(L"Coloque este executavel na raiz do projeto ou na mesma pasta do ControleGeral.bat.\n");
        system("pause");
        if (argv) LocalFree(argv);
        return 1;
    }

    wchar_t cmd[MAX_CMD];
    if (swprintf(cmd, MAX_CMD, L"\"\"%ls\"", script) < 0)
    {
        wprintf(L"[ERRO] Caminho do ControleGeral.bat muito longo.\n");
        system("pause");
        if (argv) LocalFree(argv);
        return 1;
    }

    if (argv)
    {
        for (int i = 1; i < argc; i++)
            AppendArgument(cmd, MAX_CMD, argv[i]);

        LocalFree(argv);
    }

    size_t len = wcslen(cmd);
    if (len + 1 < MAX_CMD)
    {
        cmd[len++] = L'"';
        cmd[len] = L'\0';
    }

    int rc = _wsystem(cmd);
    if (rc == -1)
    {
        wprintf(L"[ERRO] Nao foi possivel executar o ControleGeral.bat.\n");
        system("pause");
        return 1;
    }

    return rc;
}
