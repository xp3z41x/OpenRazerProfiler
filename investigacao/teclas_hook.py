"""Registra, por um tempo limitado, só as teclas usadas no teste funcional de remapeamento.

Uso: python teclas_hook.py saida.txt segundos
Teclas fora da lista aparecem como "outra tecla", sem código, para não registrar o que se digita.
"""
import ctypes
import ctypes.wintypes as wt
import sys
import time

user32 = ctypes.WinDLL('user32', use_last_error=True)
kernel32 = ctypes.WinDLL('kernel32', use_last_error=True)

WH_KEYBOARD_LL = 13
NOMES = {
    0x14: 'Caps Lock', 0x10: 'Shift', 0x11: 'Ctrl', 0xA0: 'Shift esquerdo', 0xA1: 'Shift direito',
    0xA2: 'Ctrl esquerdo', 0xA3: 'Ctrl direito', 0x2C: 'Print Screen', 0x91: 'Scroll Lock', 0x13: 'Pause',
    0x5D: 'Menu', 0x7C: 'F13', 0x7D: 'F14', 0xAD: 'Mudo', 0xAE: 'Volume -', 0xAF: 'Volume +',
}


class KBDLLHOOKSTRUCT(ctypes.Structure):
    _fields_ = [('vkCode', wt.DWORD), ('scanCode', wt.DWORD), ('flags', wt.DWORD),
                ('time', wt.DWORD), ('dwExtraInfo', ctypes.c_size_t)]


HOOKPROC = ctypes.WINFUNCTYPE(ctypes.c_ssize_t, ctypes.c_int, wt.WPARAM, wt.LPARAM)
user32.SetWindowsHookExW.argtypes = [ctypes.c_int, HOOKPROC, wt.HINSTANCE, wt.DWORD]
user32.SetWindowsHookExW.restype = wt.HHOOK
user32.CallNextHookEx.argtypes = [wt.HHOOK, ctypes.c_int, wt.WPARAM, wt.LPARAM]
user32.CallNextHookEx.restype = ctypes.c_ssize_t
user32.UnhookWindowsHookEx.argtypes = [wt.HHOOK]
kernel32.GetModuleHandleW.restype = wt.HMODULE


def main(saida, segundos):
    log = open(saida, 'w', encoding='utf-8')

    def proc(code, wparam, lparam):
        if code == 0:
            k = ctypes.cast(lparam, ctypes.POINTER(KBDLLHOOKSTRUCT)).contents
            acao = 'desce' if wparam in (0x100, 0x104) else 'sobe'
            log.write('%s %-5s %s\n' % (time.strftime('%H:%M:%S'), acao, NOMES.get(k.vkCode, 'outra tecla')))
            log.flush()
        return user32.CallNextHookEx(None, code, wparam, lparam)

    callback = HOOKPROC(proc)
    hook = user32.SetWindowsHookExW(WH_KEYBOARD_LL, callback, kernel32.GetModuleHandleW(None), 0)
    if not hook:
        raise OSError(ctypes.get_last_error())
    log.write('%s registrador ligado por %s s\n' % (time.strftime('%H:%M:%S'), segundos))
    log.flush()
    msg = wt.MSG()
    fim = time.time() + segundos
    try:
        while time.time() < fim:
            while user32.PeekMessageW(ctypes.byref(msg), None, 0, 0, 1):
                user32.TranslateMessage(ctypes.byref(msg))
                user32.DispatchMessageW(ctypes.byref(msg))
            time.sleep(0.005)
    finally:
        user32.UnhookWindowsHookEx(hook)
        log.write('%s registrador desligado\n' % time.strftime('%H:%M:%S'))
        log.close()


if __name__ == '__main__':
    main(sys.argv[1], float(sys.argv[2]))
