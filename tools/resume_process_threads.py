"""Resume every thread of the given processes on Windows.

The WorkBuddy host can start Unity's grandchild compiler helpers in a
suspended state. Unity's Bee build waits forever for those helpers. This
utility only calls ResumeThread on the requested PIDs and reports the
previous suspend counts; it does not touch files or inject code.
"""

import ctypes
import ctypes.wintypes as wintypes
import sys

TH32CS_SNAPTHREAD = 0x00000004
THREAD_SUSPEND_RESUME = 0x0002
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value


class THREADENTRY32(ctypes.Structure):
    _fields_ = [
        ("dwSize", wintypes.DWORD),
        ("cntUsage", wintypes.DWORD),
        ("th32ThreadID", wintypes.DWORD),
        ("th32OwnerProcessID", wintypes.DWORD),
        ("tpBasePri", wintypes.LONG),
        ("tpDeltaPri", wintypes.LONG),
        ("dwFlags", wintypes.DWORD),
    ]


kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
kernel32.Thread32First.argtypes = [wintypes.HANDLE, ctypes.POINTER(THREADENTRY32)]
kernel32.Thread32Next.argtypes = [wintypes.HANDLE, ctypes.POINTER(THREADENTRY32)]
kernel32.OpenThread.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
kernel32.OpenThread.restype = wintypes.HANDLE
kernel32.ResumeThread.argtypes = [wintypes.HANDLE]
kernel32.ResumeThread.restype = wintypes.DWORD
kernel32.CloseHandle.argtypes = [wintypes.HANDLE]


def resume_process(pid):
    snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    if snapshot == INVALID_HANDLE_VALUE:
        raise OSError(ctypes.get_last_error(), "CreateToolhelp32Snapshot failed")

    entry = THREADENTRY32()
    entry.dwSize = ctypes.sizeof(THREADENTRY32)
    resumed = 0
    try:
        ok = kernel32.Thread32First(snapshot, ctypes.byref(entry))
        while ok:
            if entry.th32OwnerProcessID == pid:
                handle = kernel32.OpenThread(
                    THREAD_SUSPEND_RESUME, False, entry.th32ThreadID
                )
                if handle:
                    try:
                        previous = kernel32.ResumeThread(handle)
                        if previous and previous != 0xFFFFFFFF:
                            resumed += 1
                            print(
                                "pid={} tid={} previous_suspend_count={}".format(
                                    pid, entry.th32ThreadID, previous
                                )
                            )
                    finally:
                        kernel32.CloseHandle(handle)
            ok = kernel32.Thread32Next(snapshot, ctypes.byref(entry))
    finally:
        kernel32.CloseHandle(snapshot)
    print("pid={} threads_resumed={}".format(pid, resumed))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        raise SystemExit("usage: python resume_process_threads.py PID [PID ...]")
    for value in sys.argv[1:]:
        resume_process(int(value))
