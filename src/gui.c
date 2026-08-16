#include "gui.h"
#include "adb_manager.h"
#include "device_parser.h"
#include <stdio.h>

#define ID_BTN_SCAN 1001
#define ID_LISTBOX  1002

HWND hListBox;
HWND hBtnScan;

void perform_scan() {
    // Limpa a lista e mostra aviso de busca
    SendMessage(hListBox, LB_RESETCONTENT, 0, 0);
    SendMessage(hListBox, LB_ADDSTRING, 0, (LPARAM)"[*] Buscando dispositivos Android...");

    char* output = execute_adb_devices();
    if (output) {
        AndroidDevice devices[10];
        int count = parse_devices(output, devices, 10);

        SendMessage(hListBox, LB_RESETCONTENT, 0, 0); // limpa novamente

        if (count > 0) {
            char buffer[256];
            sprintf(buffer, "[+] Foram encontrados %d dispositivo(s):", count);
            SendMessage(hListBox, LB_ADDSTRING, 0, (LPARAM)buffer);

            for (int i = 0; i < count; i++) {
                char dev_str[256];
                sprintf(dev_str, "    Serial: %s | Status: %s", devices[i].serial, devices[i].status);
                SendMessage(hListBox, LB_ADDSTRING, 0, (LPARAM)dev_str);
            }
        } else {
            SendMessage(hListBox, LB_ADDSTRING, 0, (LPARAM)"[-] Nenhum dispositivo com depuracao ativa.");
            SendMessage(hListBox, LB_ADDSTRING, 0, (LPARAM)"    Verifique o cabo USB e a permissao na tela.");
        }
        free(output);
    } else {
        SendMessage(hListBox, LB_RESETCONTENT, 0, 0);
        SendMessage(hListBox, LB_ADDSTRING, 0, (LPARAM)"[!] ERRO: ADB nao encontrado na pasta tools/platform-tools.");
    }
}

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    switch (uMsg) {
        case WM_CREATE:
            // Criando ListBox
            hListBox = CreateWindow(
                "LISTBOX", NULL,
                WS_CHILD | WS_VISIBLE | WS_BORDER | WS_VSCROLL | LBS_NOTIFY,
                20, 20, 340, 200,
                hwnd, (HMENU)ID_LISTBOX,
                (HINSTANCE)GetWindowLongPtr(hwnd, GWLP_HINSTANCE), NULL
            );

            // Criando Botao
            hBtnScan = CreateWindow(
                "BUTTON", "Escanear Aparelhos",
                WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
                110, 240, 160, 40,
                hwnd, (HMENU)ID_BTN_SCAN,
                (HINSTANCE)GetWindowLongPtr(hwnd, GWLP_HINSTANCE), NULL
            );
            
            // Faz um scan automÃ¡tico assim que a janela abre
            perform_scan();
            return 0;

        case WM_COMMAND:
            if (LOWORD(wParam) == ID_BTN_SCAN) {
                perform_scan();
            }
            return 0;

        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
    }
    return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

int init_gui(HINSTANCE hInstance, int nCmdShow) {
    const char CLASS_NAME[] = "InnoRPA Window Class";

    WNDCLASS wc = {0};
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;
    wc.hbrBackground = (HBRUSH)(COLOR_WINDOW+1);

    RegisterClass(&wc);

    HWND hwnd = CreateWindowEx(
        0,
        CLASS_NAME,
        "Inno RPA - Scanner Android",
        WS_OVERLAPPEDWINDOW ^ WS_THICKFRAME ^ WS_MAXIMIZEBOX, // Janela com tamanho fixo
        CW_USEDEFAULT, CW_USEDEFAULT, 400, 340,
        NULL,
        NULL,
        hInstance,
        NULL
    );

    if (hwnd == NULL) {
        return 0;
    }

    ShowWindow(hwnd, nCmdShow);

    MSG msg = {0};
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    return 0;
}
