"""Tiny MCP-over-HTTP client for the running Unity MCP bridge.

Why: WorkBuddy only loads MCP tools at session start, and the iPad client cannot
launch local Windows scripts. This lets any shell (including this session) call
Unity's MCP tools directly over the already-running HTTP server, with no new
session and no GUI interaction required.

Examples:
    python tools/unity_mcp.py health
    python tools/unity_mcp.py instances
    python tools/unity_mcp.py ensure          # restore the whole stack if it died
    python tools/unity_mcp.py tools scene
    python tools/unity_mcp.py call manage_scene get_active
    python tools/unity_mcp.py call manage_asset '{"action":"search","pattern":"t:Material"}'
    python tools/unity_mcp.py call execute_code @payload.json
    echo '{"action":"get_active"}' | python tools/unity_mcp.py call manage_scene -

Auto-recovery:
    The agent host reaps background tasks (and their child process trees) after a
    few minutes, so the MCP server / headless Editor can vanish between turns.
    `call`, `tools`, `raw` and `ensure` therefore restore the stack on demand.
    Set UNITY_MCP_NO_ENSURE=1 to skip that probe.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

BASE_URL = "http://127.0.0.1:8080"
ENDPOINT = BASE_URL + "/mcp"
DEFAULT_TIMEOUT = 120

PROJECT_ROOT = Path(__file__).resolve().parents[1]
VENV_BIN = PROJECT_ROOT / ".workbuddy" / "venvs" / "unity-mcp" / "Scripts"
SERVER_EXE = VENV_BIN / "mcp-for-unity.exe"
UNITY_EXE = Path(r"D:\桌面\Unity Hub\6000.0.29f1\Editor\Unity.exe")
DETACHED = 0x00000008 | 0x00000200  # DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP
LOG_DIR = PROJECT_ROOT / "tests" / "mcp"


class McpError(RuntimeError):
    pass


def _get(path: str, timeout: float = 3.0) -> str:
    with urllib.request.urlopen(BASE_URL + path, timeout=timeout) as response:
        return response.read().decode("utf-8", "replace")


def _server_alive() -> bool:
    try:
        return "healthy" in _get("/health", 2.0)
    except Exception:
        return False


def _instance_online() -> bool:
    try:
        return '"instances":[]' not in _get("/api/instances", 3.0).replace(" ", "")
    except Exception:
        return False


def _unity_running() -> bool:
    try:
        out = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/FO", "CSV", "/NH"],
            capture_output=True, text=True, errors="replace", timeout=10,
        ).stdout
        return "Unity.exe" in out
    except Exception:
        return True  # Unknown -> never risk launching a second editor.


def _spawn(argv: list[str], extra_env: dict | None = None) -> None:
    env = os.environ.copy()
    # Duplicate lowercase proxy vars trip up child process launches on this host.
    for key in ("https_proxy", "http_proxy", "all_proxy", "no_proxy"):
        env.pop(key, None)
    if extra_env:
        env.update(extra_env)
    subprocess.Popen(
        argv,
        cwd=str(PROJECT_ROOT),
        env=env,
        creationflags=DETACHED,
        close_fds=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def ensure_stack(verbose: bool = False) -> None:
    """Idempotently restore the MCP server and the headless Unity instance.

    This is deliberately called from the *client* rather than relying on a
    long-lived watcher: the agent host kills background tasks and their whole
    process trees, so any "always on" process can disappear between turns.
    """
    if os.environ.get("UNITY_MCP_NO_ENSURE") == "1":
        return

    def log(msg: str) -> None:
        if verbose:
            print("[ensure] " + msg, file=sys.stderr, flush=True)

    if not _server_alive():
        if not SERVER_EXE.exists():
            raise McpError(f"MCP 服务端不存在: {SERVER_EXE}")
        log("服务端掉线，正在重启 ...")
        _spawn([str(SERVER_EXE), "--transport", "http", "--http-url", BASE_URL])
        deadline = time.time() + 60
        while time.time() < deadline:
            if _server_alive():
                break
            time.sleep(1)
        else:
            raise McpError("MCP 服务端 60 秒内未就绪")

    if _instance_online():
        return

    # An editor may already be loading; give it a chance to register first.
    if _unity_running():
        log("Unity.exe 已在运行但插件未注册，等待自行重连 ...")
        deadline = time.time() + 90
        while time.time() < deadline:
            if _instance_online():
                return
            time.sleep(3)
        raise McpError("Unity 已在运行，但插件 90 秒内未注册；请检查编辑器状态")

    if not UNITY_EXE.exists():
        raise McpError(f"Unity Editor 不存在: {UNITY_EXE}")
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    trace_dir = PROJECT_ROOT / "Library" / "Bee"
    for stale in trace_dir.glob("*traceevents*"):
        try:
            stale.unlink()
        except OSError:
            pass
    log("实例掉线，正在启动无头 Unity ...")
    _spawn(
        [
            str(UNITY_EXE), "-batchmode", "-nographics", "-disable-assembly-updater",
            "-projectPath", str(PROJECT_ROOT),
            "-logFile", str(LOG_DIR / "autostart.log"),
        ],
        extra_env={"UNITY_MCP_ALLOW_BATCH": "1"},
    )
    deadline = time.time() + 300
    while time.time() < deadline:
        if _instance_online():
            log("实例已注册")
            return
        time.sleep(3)
    raise McpError("Unity 插件 300 秒内未注册，详见 tests/mcp/autostart.log")


def _post(payload: dict, session_id: str | None = None, timeout: int = DEFAULT_TIMEOUT):
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if session_id:
        headers["mcp-session-id"] = session_id

    request = urllib.request.Request(
        ENDPOINT,
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers=headers,
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        raw = response.read().decode("utf-8", "replace")
        new_session = response.headers.get("mcp-session-id")
    return raw, new_session


def _decode(raw: str):
    """Streamable-HTTP returns SSE (`data: {...}`); some calls return plain JSON."""
    stripped = raw.strip()
    if stripped.startswith("{"):
        return json.loads(stripped)
    for line in raw.splitlines():
        line = line.strip()
        if line.startswith("data:"):
            body = line[5:].strip()
            if body and body != "[DONE]":
                try:
                    return json.loads(body)
                except json.JSONDecodeError:
                    continue
    raise McpError(f"无法解析服务端响应: {raw[:400]}")


def handshake(timeout: int = 30) -> str:
    payload = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "aurora-cli", "version": "1.0"},
        },
    }
    raw, session_id = _post(payload, timeout=timeout)
    reply = _decode(raw)
    if "error" in reply:
        raise McpError(f"initialize 失败: {reply['error']}")
    if not session_id:
        raise McpError("服务端没有返回 mcp-session-id")

    # Required by the MCP streamable-HTTP transport.
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, session_id, timeout=15)
    return session_id


def call(method: str, params: dict, timeout: int = DEFAULT_TIMEOUT):
    session_id = handshake()
    raw, _ = _post({"jsonrpc": "2.0", "id": 2, "method": method, "params": params}, session_id, timeout)
    reply = _decode(raw)
    if "error" in reply:
        raise McpError(f"{method} 失败: {reply['error']}")
    return reply.get("result", {})


def _print_result(result: dict) -> None:
    # Prefer structuredContent (already a dict); otherwise unwrap the text block.
    if isinstance(result.get("structuredContent"), dict):
        print(json.dumps(result["structuredContent"], ensure_ascii=False, indent=2))
        return
    for item in result.get("content", []):
        if item.get("type") == "text":
            text = item.get("text", "")
            try:
                print(json.dumps(json.loads(text), ensure_ascii=False, indent=2))
            except json.JSONDecodeError:
                print(text)
            return
    print(json.dumps(result, ensure_ascii=False, indent=2))


def _plain_get(path: str) -> None:
    with urllib.request.urlopen(BASE_URL + path, timeout=10) as response:
        body = response.read().decode("utf-8", "replace")
    try:
        print(json.dumps(json.loads(body), ensure_ascii=False, indent=2))
    except json.JSONDecodeError:
        print(body)


def _parse_args(spec: str | None) -> dict:
    if spec is None or spec == "":
        return {}
    if spec == "-":
        spec = sys.stdin.read()
    elif spec.startswith("@"):
        spec = Path(spec[1:]).read_text(encoding="utf-8")
    elif not spec.lstrip().startswith("{"):
        # Shorthand: `call manage_scene get_active` -> {"action": "get_active"}
        return {"action": spec.strip()}
    parsed = json.loads(spec)
    if not isinstance(parsed, dict):
        raise McpError("工具参数必须是 JSON 对象")
    return parsed


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__)
        return 1

    command = argv[0]
    try:
        if command not in ("health", "instances"):
            ensure_stack(verbose=(command == "ensure"))
        if command == "health":
            _plain_get("/health")
        elif command == "instances":
            _plain_get("/api/instances")
        elif command == "ensure":
            _plain_get("/health")
            _plain_get("/api/instances")
        elif command == "tools":
            needle = argv[1].lower() if len(argv) > 1 else ""
            tools = call("tools/list", {}, timeout=60).get("tools", [])
            for tool in tools:
                if needle and needle not in tool["name"].lower():
                    continue
                description = (tool.get("description") or "").splitlines()[0][:110]
                print(f"{tool['name']:28s} {description}")
            print(f"\n共 {len(tools)} 个工具")
        elif command == "call":
            if len(argv) < 2:
                raise McpError("用法: call <tool> [args_json | @file | -]")
            tool_name = argv[1]
            args = _parse_args(argv[2] if len(argv) > 2 else None)
            _print_result(call("tools/call", {"name": tool_name, "arguments": args}))
        elif command == "raw":
            # Escape hatch for any MCP method: raw <method> '<json params>'
            method = argv[1]
            params = _parse_args(argv[2] if len(argv) > 2 else None)
            print(json.dumps(call(method, params), ensure_ascii=False, indent=2))
        else:
            raise McpError(f"未知命令: {command}")
    except urllib.error.URLError as exc:
        print(f"连接失败 ({ENDPOINT}): {exc}。请确认 mcp-for-unity 服务端已启动。", file=sys.stderr)
        return 2
    except (McpError, json.JSONDecodeError, OSError) as exc:
        print(f"错误: {exc}", file=sys.stderr)
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
