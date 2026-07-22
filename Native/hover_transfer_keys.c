#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

typedef struct lua_State lua_State;

static BOOL current_process_is_foreground(void)
{
    HWND foreground_window = GetForegroundWindow();
    DWORD foreground_process_id = 0;

    if (foreground_window == NULL)
    {
        return FALSE;
    }

    if (GetWindowThreadProcessId(foreground_window, &foreground_process_id) == 0)
    {
        return FALSE;
    }

    return foreground_process_id == GetCurrentProcessId();
}

/*
 * UE4SS chama a exportacao com um marcador Lua privado. Retornar 1 preserva
 * esse marcador como resultado verdadeiro; retornar 0 produz nil/falso.
 */
__declspec(dllexport) int __cdecl hover_transfer_is_h_down(lua_State* state)
{
    (void)state;

    if (!current_process_is_foreground())
    {
        return 0;
    }

    return (GetAsyncKeyState('H') & 0x8000) != 0 ? 1 : 0;
}

