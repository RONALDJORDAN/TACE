#include <windows.h>
#include "gui/gui_main.h"

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow) {
    return gui_main_init(hInstance, nCmdShow);
}
