"""Start the Unity MCP bridge and the Unity Editor for the AURORA project.

Usage:
    # Normal desktop: double-click tools/start_unity_mcp.cmd (GUI editor)
    python tools/start_unity_mcp.py --headless    # agent/iPad path: no GUI, no clicks
    python tools/start_unity_mcp.py --dry-run     # inspect state without launching

Why this exists:
  * The MCP server is a Python stdio/HTTP service; Unity's editor plugin connects
    to it over a WebSocket at ws://127.0.0.1:8080/hub/plugin.
  * Unity refuses to start when the parent process is elevated ("running with
    Administrator privileges, which is not supported"). Headless batch mode is
    exempt, so an agent can start the whole stack from an elevated shell.
  * WorkBuddy only loads MCP tools at session start. Use tools/unity_mcp.py to
    call Unity tools directly in the current session, or start a new session.
  * Run --headless as a *long-lived background task*: the agent host reaps
    orphaned children when a task finishes, which would drop the MCP link.
"""

from __future__ import annotations

import ctypes
import os
import socket
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
VENV_PYTHON = PROJECT_ROOT / ".workbuddy" / "venvs" / "unity-mcp" / "Scripts" / "python.exe"
SERVER_EXE = PROJECT_ROOT / ".workbuddy" / "venvs" / "unity-mcp" / "Scripts" / "mcp-for-unity.exe"
UNITY_EXE = Path(r"D:\桌面\Unity Hub\6000.0.29f1\Editor\Unity.exe")
BASE_URL = "http://127.0.0.1:8080"
DETACHED = 0x00000008 | 0x00000200  # DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP


def is_elevated() -> bool:
    try:
        return bool(ctypes.windll.shell32.IsUserAnAdmin())
    except Exception:
        return False


def port_open(host: str = "127.0.0.1", port: int = 8080, timeout: float = 0.4) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.settimeout(timeout)
        return sock.connect_ex((host, port)) == 0


def server_healthy() -> bool:
    try:
        with urllib.request.urlopen(f"{BASE_URL}/health", timeout=2) as resp:
            return b"healthy" in resp.read()
    except Exception:
        return False


def ensure_server() -> None:
    if server_healthy():
        print("[1/3] MCP server already healthy on 8080")
        return

    if not SERVER_EXE.exists():
        sys.exit(f"MCP server not installed: {SERVER_EXE}\nRun the install steps in docs/PRODUCTION_LOG.md")

    print("[1/3] starting MCP server ...")
    subprocess.Popen(
        [str(SERVER_EXE), "--transport", "http", "--http-url", BASE_URL],
        cwd=str(PROJECT_ROOT),
        creationflags=DETACHED,
        close_fds=True,
    )

    deadline = time.time() + 60
    while time.time() < deadline:
        if server_healthy():
            print("      server ready")
            return
        time.sleep(0.5)
    sys.exit("MCP server did not become healthy within 60s; check that port 8080 is free")


def launch_editor(headless: bool = False):
    if not UNITY_EXE.exists():
        sys.exit(f"Unity Editor not found: {UNITY_EXE}")

    args = [str(UNITY_EXE), "-projectPath", str(PROJECT_ROOT), "-disable-assembly-updater"]
    env = os.environ.copy()
    if headless:
        # The plugin skips auto-connect in batch mode unless this variable is set.
        env["UNITY_MCP_ALLOW_BATCH"] = "1"
        args += [
            "-batchmode", "-nographics",
            "-logFile", str(PROJECT_ROOT / "tests" / "mcp" / "headless.log"),
        ]

    print("[2/3] launching Unity Editor " + ("(headless)" if headless else "(GUI)") + " ...")
    return subprocess.Popen(
        args,
        cwd=str(PROJECT_ROOT),
        env=env,
        creationflags=DETACHED,
        close_fds=True,
    )


def instance_online() -> bool:
    """True when at least one Unity instance is registered with the MCP server."""
    try:
        with urllib.request.urlopen(f"{BASE_URL}/api/instances", timeout=3) as resp:
            body = resp.read().decode("utf-8", "replace")
        return '"instances":[]' not in body.replace(" ", "")
    except Exception:
        return False


def unity_process_running() -> bool:
    """Guard against starting a second Editor on a project that is already locked."""
    try:
        out = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
            capture_output=True, text=True, errors="replace", timeout=10,
        ).stdout
        return "Unity.exe" in out
    except Exception:
        return True  # Unknown -> conservative: never risk a double launch.


def wait_for_instance(timeout: float = 240.0) -> bool:
    print("[3/3] waiting for the editor plugin to connect ...")
    deadline = time.time() + timeout
    while time.time() < deadline:
        if instance_online():
            print("      connected")
            return True
        time.sleep(2.0)
    print("      timed out; open the editor and use Window > MCP for Unity > Connect")
    return False


def hold_watch() -> None:
    """Session-long keepalive: supervise the stack, only relaunch if truly gone."""
    print("[watch] 监督 MCP 栈；已有实例在线时不会启动第二个编辑器。", flush=True)
    misses = 0
    last_beat = 0.0
    while True:
        if instance_online() and server_healthy():
            misses = 0
            now = time.time()
            if now - last_beat >= 60:
                print(f"[watch] OK 实例在线 {time.strftime('%H:%M:%S')}", flush=True)
                last_beat = now
        else:
            misses += 1
            print(f"[watch] WARN 掉线计数 {misses}/3", flush=True)
            if misses >= 3:
                if unity_process_running():
                    print("[watch] Unity.exe 仍在运行，只等插件重连，不双开编辑器。", flush=True)
                else:
                    print("[watch] 重新拉起 MCP 栈 ...", flush=True)
                    ensure_server()
                    launch_editor(headless=True)
                    wait_for_instance()
                misses = 0
        time.sleep(10)


def main() -> None:
    headless = "--headless" in sys.argv[1:]
    if "--dry-run" in sys.argv[1:]:
        print("headless      :", headless)
        print("elevated      :", is_elevated())
        print("server healthy:", server_healthy())
        print("editor args   :", "-batchmode -nographics" if headless else "GUI")
        return

    if "--watch" in sys.argv[1:]:
        # Idempotent keepalive for a fresh agent session: never double-launch Unity.
        ensure_server()
        if instance_online():
            print("[1/3] MCP server healthy on 8080")
            print("[2/3] 已有 Alsphalt8 实例在线，跳过启动编辑器（避免工程锁冲突）")
            print("[3/3] connected")
        elif unity_process_running():
            print("[watch] Unity.exe 已在运行但插件未注册，等待自行重连 ...")
            wait_for_instance(timeout=60)
        else:
            launch_editor(headless=True)
            wait_for_instance()
        hold_watch()
        return

    if is_elevated() and not headless:
        print("WARNING: this shell is elevated. Unity refuses to run as Administrator.")
        print("         close this window and double-click start_unity_mcp.cmd instead.\n")

    ensure_server()
    editor = launch_editor(headless)
    wait_for_instance()

    print()
    print("MCP endpoint : " + BASE_URL + "/mcp")
    print("Project      : " + str(PROJECT_ROOT))
    print("直接调用工具 : python tools/unity_mcp.py call manage_scene get_active")
    print("WorkBuddy 的工具列表只在会话启动时加载，新会话才会出现 Unity 工具。")

    if headless:
        # Keep this process alive: the agent host reaps orphaned children when a
        # background task finishes, which would kill Unity and drop the MCP link.
        print("\n[hold] 保持无头编辑器存活；结束本任务会断开 MCP 连接。")
        try:
            editor.wait()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
