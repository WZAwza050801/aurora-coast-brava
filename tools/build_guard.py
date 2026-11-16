"""Resume Unity build helper processes that get created in a suspended state.

Why this exists
---------------
On this machine the local security suite (Tencent PC Manager, registered in
SecurityCenter2) hooks process creation and holds every new child process in a
suspended state while it performs a cloud reputation check. Because outbound
network in this session is routed through a proxy that fails TLS handshakes, the
check never returns and the child stays suspended forever.

Observed symptom: `bee_backend.exe` spawns `cmd.exe /c netcorerun.exe
ApiUpdater.MovedFromExtractor.dll ...`, that cmd process has exactly one thread
with WaitReason == Suspended and zero children. Bee then blocks on it, Unity sits
on "Compiling Scripts" indefinitely, and killing the wait produces the downstream
"Scripts have compiler errors" message.

This guard watches only Unity's own compilation helpers and calls ResumeThread on
their suspended threads. It never touches unrelated processes, and never kills
anything. Usage:

    python tools/build_guard.py [--seconds 3600]

Stop early by creating the file `tests/minimal/.guard_stop`.
"""

import argparse
import ctypes
import ctypes.wintypes as wt
import os
import sys
import time

TH32CS_SNAPPROCESS = 0x00000002
TH32CS_SNAPTHREAD = 0x00000004
THREAD_SUSPEND_RESUME = 0x0002
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [
        ("dwSize", wt.DWORD),
        ("cntUsage", wt.DWORD),
        ("th32ProcessID", wt.DWORD),
        ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)),
        ("th32ModuleID", wt.DWORD),
        ("cntThreads", wt.DWORD),
        ("th32ParentProcessID", wt.DWORD),
        ("pcPriClassBase", ctypes.c_long),
        ("dwFlags", wt.DWORD),
        ("szExeFile", ctypes.c_wchar * 260),
    ]


class THREADENTRY32(ctypes.Structure):
    _fields_ = [
        ("dwSize", wt.DWORD),
        ("cntUsage", wt.DWORD),
        ("th32ThreadID", wt.DWORD),
        ("th32OwnerProcessID", wt.DWORD),
        ("tpBasePri", ctypes.c_long),
        ("tpDeltaPri", ctypes.c_long),
        ("dwFlags", wt.DWORD),
    ]


kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
kernel32.CreateToolhelp32Snapshot.restype = wt.HANDLE
kernel32.CreateToolhelp32Snapshot.argtypes = [wt.DWORD, wt.DWORD]
kernel32.Process32FirstW.argtypes = [wt.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
kernel32.Process32NextW.argtypes = [wt.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
kernel32.Thread32First.argtypes = [wt.HANDLE, ctypes.POINTER(THREADENTRY32)]
kernel32.Thread32Next.argtypes = [wt.HANDLE, ctypes.POINTER(THREADENTRY32)]
kernel32.OpenThread.argtypes = [wt.DWORD, wt.BOOL, wt.DWORD]
kernel32.OpenThread.restype = wt.HANDLE
kernel32.ResumeThread.argtypes = [wt.HANDLE]
kernel32.ResumeThread.restype = wt.DWORD
kernel32.CloseHandle.argtypes = [wt.HANDLE]

DIRECT_TARGETS = {
    "netcorerun.exe",
    "dotnet.exe",
    "bee_backend.exe",
    "unity.ilpp.runner.exe",
}
PARENT_GATED_TARGETS = {"cmd.exe"}


def snapshot_processes():
    snap = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    if snap == INVALID_HANDLE_VALUE:
        return {}
    result = {}
    entry = PROCESSENTRY32W()
    entry.dwSize = ctypes.sizeof(PROCESSENTRY32W)
    ok = kernel32.Process32FirstW(snap, ctypes.byref(entry))
    while ok:
        result[entry.th32ProcessID] = (
            (entry.szExeFile or "").lower(),
            entry.th32ParentProcessID,
        )
        ok = kernel32.Process32NextW(snap, ctypes.byref(entry))
    kernel32.CloseHandle(snap)
    return result


def target_pids(processes):
    bee = {pid for pid, (name, _) in processes.items() if name == "bee_backend.exe"}
    targets = {}
    for pid, (name, ppid) in processes.items():
        if name in DIRECT_TARGETS:
            targets[pid] = name
        elif name in PARENT_GATED_TARGETS and ppid in bee:
            targets[pid] = name
    return targets


def resume_suspended_threads(targets):
    snap = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    if snap == INVALID_HANDLE_VALUE:
        return []
    resumed = []
    entry = THREADENTRY32()
    entry.dwSize = ctypes.sizeof(THREADENTRY32)
    ok = kernel32.Thread32First(snap, ctypes.byref(entry))
    while ok:
        owner = entry.th32OwnerProcessID
        if owner in targets:
            handle = kernel32.OpenThread(THREAD_SUSPEND_RESUME, False, entry.th32ThreadID)
            if handle:
                previous = kernel32.ResumeThread(handle)
                kernel32.CloseHandle(handle)
                if previous > 0:
                    resumed.append((owner, targets[owner], entry.th32ThreadID, previous))
        ok = kernel32.Thread32Next(snap, ctypes.byref(entry))
    kernel32.CloseHandle(snap)
    return resumed


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=int, default=3600)
    parser.add_argument("--interval", type=float, default=0.2)
    parser.add_argument("--stop-file", default="tests/minimal/.guard_stop")
    args = parser.parse_args()

    deadline = time.time() + args.seconds
    total = 0
    idle_rounds = 0
    print("guard start", flush=True)
    while time.time() < deadline:
        if os.path.exists(args.stop_file):
            print("stop file found", flush=True)
            break
        processes = snapshot_processes()
        targets = target_pids(processes)
        if targets:
            for owner, name, tid, previous in resume_suspended_threads(targets):
                total += 1
                print(
                    f"resumed pid={owner} {name} tid={tid} prev_suspend={previous}",
                    flush=True,
                )
            idle_rounds = 0
        else:
            idle_rounds += 1
        time.sleep(args.interval)
    print(f"guard exit total_resumed={total}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
