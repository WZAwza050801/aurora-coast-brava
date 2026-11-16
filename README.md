# AURORA / Costa Brava

原创 PC 街机赛车工程：**12 款可切换开源整车**（Ferrari 458 Italia CC BY 4.0 + 10 款 CC0 高模车 + CC0 BMW）、黄金时刻山谷环线（COSTA NOVA，2,607 m / 12 检查点）、漂移与氮气、三名 AI、两圈比赛。切换车身不影响已验收的物理与操控。

---

## ⚖️ 定位与许可（先读这一节）

**这是一个个人非商业研究项目（personal, non-commercial）。**

| | |
|---|---|
| 仓库代码与原创内容 | [CC BY-NC 4.0](LICENSE) |
| 第三方素材 | 各自适用原始许可，逐项见 [NOTICE.md](NOTICE.md) |
| 素材构成 | **CC0 / 公有领域 36 项**（10 款高模车 + BMW + 20 款归档低模车 + 5 个环境模型 + 4 个音频）· **CC BY 需署名 3 项**（Ferrari 458 Italia、2 个音效） |
| 需要另行授权的 | **Ferrari 商标与外观设计权** —— CC BY 只免除著作权，不免除商标 |
| 使用边界 | 个人非商业研究 / 学习。**对外发布或商业化前，请先读完 [NOTICE.md](NOTICE.md) 第 2 节的商标风险说明** |
| 明确排除 | 商业游戏提取的车模（Forza Horizon / Asphalt 8 等）不进入本工程；社区上传的 McLaren 等品牌车模也不进入本仓库（第三方无权许可车厂的设计） |

> 完整许可对齐表见 **[NOTICE.md](NOTICE.md)**；技术细节见 [`docs/ASSET_LICENSES.md`](docs/ASSET_LICENSES.md)。

---

## 🚀 快速开始

```bash
# 1. 拉取不入库的原始素材（Poly Haven / McGuire，全部 CC0，带 md5 校验）
python tools/fetch_assets.py

# 2. 生成场景并出包（场景是构建产物，仓库里不存）
.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/run_headless_build.py --tag local

# 3. 跑完整验收（八项）
build/Windows/AuroraCoast.exe -garagecheck   -screen-width 1280 -screen-height 720
build/Windows/AuroraCoast.exe -racetest      -screen-width 1280 -screen-height 720
```

**为什么场景不在仓库里**：`AuroraProjectBuilder.BuildCompleteRace()` 每次都从**空场景**重建整条赛道并覆盖 `Assets/Scenes/CostaBrava.unity`，同时清空重建 `Assets/GeneratedMeshes/`。把生成物提交进来只会有两类问题：每次构建产生 5 MB 无意义 diff，以及场景里引用的 GUID 指向只存在于某台机器上的生成网格。所以**源码是唯一事实源，场景由生成器产出**（见 `.gitignore` 注释）。

### 仓库结构

| 路径 | 内容 | 入库 |
|---|---|---|
| `Assets/Scripts/` | 玩法代码：车辆物理、AI、比赛规则、HUD、镜头、输入映射 | ✅ |
| `assets/Editor/` | **场景生成器**（赛道、道路、村落、护栏、UI 全部程序化） | ✅ |
| `assets/track_layout.json` | 赛道数据源（COSTA NOVA 的控制点与约束） | ✅ |
| `tools/` | 构建、验收、资产转换、设计工具链 | ✅ |
| `docs/` | 生产日志、赛道设计工具链、许可明细 | ✅ |
| `tests/` | 验收证据（报告 CSV/TXT + 实跑截图） | ✅ |
| `Assets/Vehicles/` | 最终入库的车模（CC0 高模车 + CC BY Ferrari + CC0 BMW） | ✅ |
| `Assets/Scenes/*.unity` | **构建产物**，由生成器产出 | ❌ |
| `Assets/GeneratedMeshes/` | **构建产物** | ❌ |
| `Assets/models/` | Poly Haven 原始下载（98 MB） | ❌ 用 `tools/fetch_assets.py` 重建 |
| `Assets/vehicles/bmw_cc0/` | McGuire 原始 OBJ（30 MB） | ❌ 同上 |
| `Assets/vehicles/*.blend` | Blender 中间产物（43 MB） | ❌ 本机 Blender 脚本生成 |

---

## English summary

**AURORA / Costa Brava** — an original PC arcade racer built in Unity 6000.0.29f1 (HDRP).

- **12 switchable car bodies** (10 CC0 high-poly models, one CC0 BMW, one CC BY 4.0 Ferrari 458 Italia), each with independent hinged bonnet/door nodes, four wheel pivots and separated material slots.
- **One circuit, COSTA NOVA** — 2,607.4 m, 12 checkpoints, min radius 18.1 m, max gradient 14.7 %, procedurally generated from `assets/track_layout.json`.
- **Arcade handling**: drift, nitro with an overdrive mode (FOV 60° → 66°), three AI opponents, two-lap races.
- Every measurement in the README comes from a scripted run, not from inspection: `-racetest`, `-garagecheck`, `-nitrotest`, `-collisiontest`, `-garagedrive`, `-padmaptest`.

**Licensing.** The code and all original content in this repository are released under **CC BY-NC 4.0** — this is a personal, non-commercial project. Third-party assets keep their own licences: 36 items are **CC0 / public domain**, 3 are **CC BY** and require attribution, and the Ferrari 458 Italia additionally carries **Ferrari trademark and design rights that CC BY does not clear**. Read [`NOTICE.md`](NOTICE.md) before redistributing anything.

**Building it.** The scene is generated, not committed:

```bash
python tools/fetch_assets.py                                   # raw CC0 assets
.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/run_headless_build.py --tag local
```

---

## 当前状态

**已出包并实测（2026-11-16 06:35 交付包）**：`build/Windows/AuroraCoast.exe` 完成两圈比赛（总时 **05:41.551**，最快圈 **02:50.736**，1/4 名，检查点 `1→11→0` 两轮共 **24 次推进**（遥测采样 23 次 + 终点跨越），平均 **59.94 FPS** @ 1280×720 / Intel Iris Xe / D3D11）。本轮把车组从 9 款低模扩到 **12 款高模**（3.0–4.8 万面），并**新做了一条赛道 COSTA NOVA**（2,607.4 m / 12 检查点），随后又修掉了 GLB→FBX 转换管线里三个把零件搬错位置的 bug。构建 `Succeeded / 0 error / 0 warning`（223,906,099 B）。截图、逐秒遥测、12 款车实截与四份玩法报告在 `tests/race/`。详见下方「已实测结果」与 `docs/PRODUCTION_LOG.md`。

## 接手第一步（新会话 / iPad 端必读）

**不要依赖"后台任务常驻"。** 宿主会周期性回收后台任务及其整个子进程树（实测存活 2 分钟到 2 小时不等），所以 MCP 服务端和无头 Unity 随时可能掉线。可靠做法是**调用侧自动恢复**：`tools/unity_mcp.py` 的 `call` / `tools` / `raw` / `ensure` 会在每次调用前探测，掉线就自己拉起服务端和无头编辑器并等插件注册，在线时只是一次 HTTP 探测。

新会话里直接调工具即可，iPad 端不需要双击任何脚本：

```bash
cd "D:/ai_for_games/赛车游戏复刻/Alsphalt8"
.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/unity_mcp.py ensure          # 可选：显式探活并恢复
.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/unity_mcp.py instances
.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/unity_mcp.py call manage_scene get_active
.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/unity_mcp.py call manage_scene '{"action":"get_hierarchy","page_size":10}'
```

- 掉线时首次调用会花 1–3 分钟重拉无头 Unity；`UNITY_MCP_NO_ENSURE=1` 可跳过自动恢复。
- `tools/start_unity_mcp.py --watch` 仍可作为保活增强（幂等、不会双开），但同样会被宿主回收，**不能当作唯一依赖**；`--headless` 单独跑等插件注册完就返回，更不可靠。
- 无头编辑器初始化完成后可能停在空场景；需要看赛道时执行 `call manage_scene '{"action":"load","path":"Assets/Scenes/CostaBrava.unity"}'`。
- 客户端没有 `--args` 选项：完整参数直接把 JSON 作为第 3 个参数传，或用 `@file` / `-`（stdin）。

只有想让工具出现在 WorkBuddy 原生工具列表里，才需要新开一个会话（客户端里点「新建对话」即可，不需要碰 Windows 文件）。

## 环境与打开方式

- Unity `6000.0.29f1`
- HDRP `17.0.3`
- Windows x64，Mono 开发构建为当前构建器设置
- 目标测试显卡：Intel Iris Xe；不默认启用硬件光追

1. 用上述 Unity 版本打开本目录。
2. 等待 Package Manager 和脚本编译完成。
3. 先执行菜单 `Aurora > Build Minimal Driving Lab`，不要直接跳过最小驾驶验证。
4. 最小测试通过后，执行 `Aurora > Build Complete Coast Race`。
5. 构建器预期输出：
   - 最小驾驶：`build/Windows/AuroraDrivingLab.exe`
   - 完整比赛：`build/Windows/AuroraCoast.exe`

`Packages/manifest.json` 使用标准 HDRP 版本依赖。首次打开需要 Package Manager 完整解析 HDRP 的传递依赖；本机日志曾停在 `project:resolve-packages`，尚未完成该步骤。

## MCP 编辑器控制（2026-11-15 已打通并实测）

两段式结构，缺一不可：

```
WorkBuddy ──HTTP /mcp──▶ mcp-for-unity（Python，127.0.0.1:8080）
                                  ▲
                                  │ WebSocket /hub/plugin
                           Unity 编辑器插件（主动外连）
```

**一键启动（推荐）**：双击 `tools/start_unity_mcp.cmd`。它会先拉起 MCP 服务端，等健康检查通过后再打开编辑器，并轮询直到插件连上。必须以**普通权限**运行——Unity 拒绝在管理员进程下启动。

| 组件 | 位置 / 参数 |
|---|---|
| MCP 服务端（Python） | `.workbuddy/venvs/unity-mcp`，`--transport http --http-url http://127.0.0.1:8080` |
| 内嵌 Unity 包 | `Packages/com.coplaydev.unity-mcp`（v10.0.0，源码见 `.workbuddy/vendor/unity-mcp-v10.0.0`） |
| WorkBuddy 配置 | `~/.workbuddy/mcp.json` → `unityMCP.url = http://127.0.0.1:8080/mcp` |
| 插件自动连接 | 注册表 `HKCU\Software\Unity Technologies\Unity Editor 5.x` 中 `MCPForUnity.AutoStartOnLoad=1`、`MCPForUnity.UseHttpTransport=1`、`MCPForUnity.HttpUrl=http://127.0.0.1:8080` |
| 无头模式额外要求 | 环境变量 `UNITY_MCP_ALLOW_BATCH=1`（否则插件在 batchmode 下直接跳过自动连接） |

**已验证**：`/api/instances` 返回实例 `Alsphalt8 / 6000.0.29f1`；`tools/list` 返回 46 个工具；`manage_scene load Assets/Scenes/CostaBrava.unity` 成功并读到 `rootCount = 1147`。

**注意**：
- WorkBuddy 只在会话启动时加载 MCP 工具，配置改完需新开会话才会出现 Unity 工具。
- 同一工程只能有一个 Unity 实例持有它。若要开 GUI 编辑器，先关掉无头实例。
- Blender 侧：`tools/enable_blender_mcp.py` 启用本机插件（`localhost:9876`），2026-11-12 已用它真实重导车辆。

## 操作

| 操作 | 键鼠 | 手柄 |
|---|---|---|
| 加速 / 制动与倒车 | W / S 或方向键 | 左摇杆 Y（Vertical 轴） |
| 转向 | A / D 或方向键 | 左摇杆 X（Horizontal 轴） |
| 手刹漂移 | Space | A / Button 0 |
| 氮气 | 按住 Shift | X / RB（Button 2 / 5） |
| 复位到最近有效赛道点 | R | B / Button 1 |
| 暂停 / 继续 | Esc | Start / Button 7 |
| 开始 / 确认 | Enter 或界面按钮 | A / Button 0 |
| 暂停菜单导航 | ↑↓ 选择、←→ 调整、Enter 确认 | 左摇杆或十字键、A 确认 |
| 切换车型（主菜单 / 暂停） | Q / E 或 `[` `]` 或界面 `<` `>` | LB / RB（Button 4 / 5），暂停菜单选中 CHANGE CAR 后 ←→ |

主菜单与暂停菜单均可切换车型；主菜单改为「左文字栏 + 右转台展示窗」，选中的车在右侧转台上缓慢旋转展示。暂停菜单提供继续、重赛（直接进入倒计时）、返回主菜单、**CHANGE CAR**、画质档位、镜头效果、FOV、主音量与退出，并显示当前名次/圈数/时间/最快圈；暂停时音频一并暂停。主菜单显示当前车型、年代、署名与实际检测到的手柄名称（无设备时显示 `GAMEPAD: NONE DETECTED`）。车型选择通过 `VehicleGarage.PersistedIndex` 在重赛 / 返回菜单后保留。

车型清单由 `Assets/Scripts/VehicleCatalog.cs` 单一维护：玩家车挂载全部 12 个 inactive 车身变体与一套共享物理根，三辆对手车分别固定为 HD 中置超跑 / HD 肌肉轿跑 / HD 旅行轿跑。`Q/E` 只切可见车身与视觉轮引用，**不改 Rigidbody、BoxCollider、4 个 WheelCollider 与控制器参数**，因此不会改变已验收的操控与比赛公平性。程序化尾翼的高度按构建时实测的车顶高度自动定位（`VehicleGarage.Variant.roofHeight`），所以 12 款从 1.24 m 到 1.93 m 的车共用一套尾翼几何也不会插进车顶或悬空。逐款来源与许可见 `docs/ASSET_LICENSES.md`。

Input Manager 已配置摇杆轴 0/1（dead 0.19、Y 反向），代码再套一层 0.10 死区消除漂移。执行 `AuroraCoast.exe -gamepadcheck` 会把检测到的设备、轴读数与完整按键映射写到 `tests/race/gamepad_check.txt`，便于插上手柄后一键留证。**本机无物理手柄，真机按键手感仍未实测。**

## 自动验收

从工程根目录启动，确保遥测写入本工程的 `tests/`：

```powershell
& '.\build\Windows\AuroraDrivingLab.exe' -autotest -screen-width 1600 -screen-height 900 -screen-fullscreen 0
& '.\build\Windows\AuroraCoast.exe' -racetest -screen-width 1600 -screen-height 900 -screen-fullscreen 0
& '.\build\Windows\AuroraCoast.exe' -gamepadcheck
& '.\build\Windows\AuroraCoast.exe' -pausetest
& '.\build\Windows\AuroraCoast.exe' -garagecheck
& '.\build\Windows\AuroraCoast.exe' -nitrotest
& '.\build\Windows\AuroraCoast.exe' -collisiontest
& '.\build\Windows\AuroraCoast.exe' -garagedrive
& '.\build\Windows\AuroraCoast.exe' -padmaptest
```

预期产物：

- `tests/minimal/telemetry.txt`：显卡、分辨率、末态速度/接地/氮气、平均与最低 FPS。
- `tests/minimal/driving_samples.csv`：逐秒车辆状态。
- `tests/minimal/01_acceleration.png`、`02_drift.png`。
- `tests/race/race_telemetry.txt`：两圈状态、检查点、排名、复位、FPS 和结算。
- `tests/race/race_01.png` 至 `race_finish.png`。
- `tests/race/gamepad_check.txt`：手柄设备与映射留证（`-gamepadcheck`）。
- `tests/race/main_menu.png`、`pause_menu.png`：主菜单与暂停菜单实截（`-pausetest`）。
- `tests/race/garage/garage_00_*.png` … `garage_11_*.png` + `garage_check.txt`：12 款车身逐款实截、可见 renderer 数、活跃车身数、实测车顶高度、共享物理参数（`-garagecheck`）。
- `tests/race/race_telemetry.txt` 新增 `garage_variants=12` 与 `selected_car=<id>`。
- `tests/race/nitro_test.txt` + `nitro_overdrive.png`：氮气 overdrive 的帧数、连续时长、FOV 变化与粒子发射（`-nitrotest`）。
- `tests/race/collision_test.txt` + `collision_impact.png`：碰撞计数、冲击强度、镜头抖动、对手位移与速度损失（`-collisiontest`）。
- `tests/race/garage_drive.txt` + `garage_drive_*.png`：行驶中换车时的质量、碰撞体数、轮引用与车速（`-garagedrive`）。
- `tests/race/padmap_test.txt`：手柄映射决策表的 29 条断言（`-padmaptest`）。

只有这些文件真实生成并检查后，才能把对应项目标为通过。

## 工程结构

- `Assets/Scripts/ArcadeCarController.cs`：WheelCollider、街机操控、漂移、氮气、腾空与复位。
- `Assets/Scripts/ChaseCamera.cs`：平滑追尾、速度 FOV、震动、动态模糊与遮挡射线。
- `Assets/Scripts/TrackPath.cs`：封闭 Catmull-Rom 路线、曲率和目标速度。
- `Assets/Scripts/RivalDriver.cs`：AI 前视、制动、超车、氮气与卡住复位。
- `Assets/Scripts/RaceManager.cs`：连续里程排名、跨平面检查点、圈数、计时与结算。
- `Assets/Scripts/VehicleCatalog.cs`：9 款车型目录（含署名）。
- `Assets/Scripts/VehicleGarage.cs`：运行时换车身组件（**必须独立成文件**，见上方 bug 记录）。
- `Assets/Scripts/InputMap.cs`：键鼠 / 手柄意图判定的唯一实现（纯函数，可被 `-padmaptest` 直接验证）。
- `Assets/Editor/AuroraProjectBuilder.cs`：车辆、灯光、最小测试路与 Windows 构建。
- `Assets/Editor/AuroraRaceSceneBuilder.cs`：完整海岸赛道、场景和比赛构建。
- `tools/`：真实素材的 Blender 处理脚本，以及 Unity MCP / 无头构建工具链：
  `start_unity_mcp.py`（`--headless` 拉起、`--watch` 幂等保活）、`unity_mcp.py`（免新会话的直连客户端）、
  `run_headless_build.py`（规范化环境变量后跑 `-executeMethod`，日志落盘）、`build_guard.py`（恢复被安全软件挂起的编译子进程）、
  `track_design_studio.py`（赛道设计工作台：JSON ↔ C# ↔ 可拖拽 HTML 预览）、`make_project_portable.py`（移除目录联接）、
  `prepare_cc0_fleet.py`（Blender 批量把 CC0 GLB 整车转成统一契约 FBX）、`prepare_hd_fleet.py`（10 款 HD 车的转换 + 引擎盖自动闭合）、`prepare_vehicle.py`（单款高模 Draco 解码与四轮拆分）、`audit_highpoly_cars.py`（遍历 3DAssets.dev 公开 API，按三角面数审计全站车辆资产）、`design_track_nova.py`（极坐标半径调制生成赛道 + 参数空间扫描）、`cs_lint.py`（离线 C# 语法自检）。
- `docs/ASSET_LICENSES.md`：实际素材来源、许可与处理记录。
- `docs/TRACK_DESIGN_TOOLCHAIN.md`：赛道设计工具链、逐层代码逻辑、引擎选型判断与车模资源许可清单。

## 渲染与性能取舍

HDRP Lit 金属车漆、HDRISky + 8.5° 低角度暖阳与冷色补光、可见落日盘、ACES + 暖色 colorFilter / Bloom / 暖高度雾、PBR 沥青与实时阴影。地形改用 112×112 网格、低频丘陵与程序化噪声 albedo；远景为三层多频噪声山脊环（取代 15 个光滑球）；四段村落共 108 栋按材质合并为 5 个 draw call，贴地并带暖色窗光。扫描树木/岩石/灌木已减面。画质预算以车辆、道路和弯道可读性优先。

## 已实测结果（2026-11-12）

测试设备：Intel Iris Xe 核显，Direct3D 11，1280×720 窗口，Development Build。

| 项目 | 最小驾驶实验室 | 完整两圈比赛 |
|---|---|---|
| 平均 FPS | 59.69 | 59.84 |
| 最低 FPS | 3.0（首帧装载） | 10.1（首帧装载） |
| 结果 | 四轮全程着地，极速 131 km/h，氮气释放/再生正常，跳台腾空后稳定落地 | `completed`，总时 04:56.7，最快圈 02:28.1，名次 2/4，检查点无误，复位 0 次 |

赛道实测 2,282.6 m，两圈约 5 分钟。构建产物：`build/Windows/AuroraDrivingLab.exe` 与 `build/Windows/AuroraCoast.exe`（0 error / 0 warning）。截图与逐秒遥测在 `tests/minimal/` 与 `tests/race/`。构建与排查细节见 `docs/PRODUCTION_LOG.md`；在腾讯电脑管家拦截进程创建的本机上编译 Unity 前，先运行 `python tools/build_guard.py`。

**2026-11-15 17:25 最终包（美术 / 输入 / 菜单更新后，同一设备与分辨率）**：三次 `-batchmode -executeMethod AuroraProjectBuilder.BuildCompleteRace` 全部 `Succeeded / 0 error / 0 warning`，最终 EXE 199,276,751 B。最终 `-racetest`：`completed`，总时 **04:57.246**，最快圈 **02:28.521**，**2/4 名**，平均 **59.95 FPS**，最低 5.91（首帧装载）；遥测检查点序列 `1→9→0` 两轮共 **20 次推进无误**，圈数 0→1。实跑截图 `tests/race/race_01.png` 至 `race_finish.png`，菜单截图 `main_menu.png` / `pause_menu.png`，手柄留证 `gamepad_check.txt`；旧基线备份在 `tests/race/baseline_2026-11-15_1551/`。

**2026-11-15 18:02 交付包（车辆外观 / 赛道设计工作台 / 氮气与碰撞强化）**：`Succeeded / 0 error / 0 warning`，EXE **199,323,535 B**（构建日志 `tests/mcp/build_20261115_1801.out`）。最终 `-racetest`：`completed`，总时 **04:56.361**，最快圈 **02:28.075**，**2/4 名**，平均 **59.94 FPS**，最低 10.25（首帧装载）；检查点 `1→9→0` 两轮共 **20 次推进无误**，遥测新增 `collision_events=2`（碰撞系统真实触发）、`nitro_overdrive_frames=0`（自动验收的氮气是间歇短按，未连续 1.5 s，overdrive 需真人长按验证）。

玩家车从逆光黑块改为亮金属铜色清漆 + 抛光轮毂 + 尾翼／端板／扩散器／侧裙 + 前后发光灯件 + 青紫氮气等离子粒子；对手车改成蓝/银/紫可读色；镜头高度 2.75→2.5、距离 7.4→7.1。玩法侧新增持续氮气 >1.5 s 的 **overdrive**（推力 ×1.22、紫色 HUD、FOV +3）、碰撞事件计数、对手推挤、护栏限速衰减与冲击镜头震动。赛道控制点改为 `assets/track_layout.json` → `TrackLayout.generated.cs` 单一数据源，配套 `tools/track_design_studio.py` 与可拖拽 HTML 预览。详见 `docs/TRACK_DESIGN_TOOLCHAIN.md`。

**2026-11-15 20:31 最终包（9 款可切换车型 / 多车型车库）**：`Succeeded / 0 error / 0 warning`，构建日志 `tests/mcp/build_20261115_garage_FINAL.out`，总产物 **214,691,123 B**。

| 验收项 | 实跑结果 |
|---|---|
| `-racetest` 两圈 | `completed`，总时 **04:56.963**，最快圈 **02:28.397**，**2/4 名** |
| 检查点 / 圈数 | `1→9→0` 两轮 **20 次推进无误**，圈数 0→1→2 |
| 性能 | 平均 **59.91 FPS**，最低 10.24（首帧装载） |
| 碰撞 / 氮气 | `collision_events=1`；`nitro_overdrive_frames=0`（自动验收短按，overdrive 仍需真人长按） |
| 车库 | `garage_variants=9`、`selected_car=ferrari458`；`-garagecheck` 产出 9 张逐款实截 + `garage_check.txt`，每款 `active_bodies=1`、`wheel_visuals=4`，共享 `physics_wheelcolliders=4` / `physics_mass_kg=1350` |
| 菜单 / 手柄 | `-pausetest` 主菜单与暂停菜单实截；`-gamepadcheck` `devices=NONE` |

车型库由 `Assets/Scripts/VehicleCatalog.cs` 单一维护；`VehicleGarage` 只切可见车身、视觉轮引用与尾翼高度，物理根始终不变。逐款来源、署名链与 Ferrari 商标风险见 `docs/ASSET_LICENSES.md`。

### 本轮定位并修掉的两个真实 bug

1. **GLB→FBX 根节点轴变换被覆盖**：Ferrari 与 7 款 CC0 车的 FBX 根节点带 `(270°, 0°, 0°)` 旋转与 `×100` 缩放，旧的 `CreateCar()` 用 `localRotation = Euler(0, yaw, 0)` 直接覆盖它，导致这 8 款车全部立起来 / 压成平板（只有根节点为零旋转的 BMW 正常）。`-garagecheck` 的 3/4 实截暴露后，改为把 yaw 放在父节点 `BODY / <id>` 上、保留模型自身 `localRotation/localScale`，9 款车才全部恢复正常车体。
2. **生成网格资产无限累积破坏 level0**：`PersistMesh()` 用 `AssetDatabase.GenerateUniqueAssetPath()` 每次重建都新增约 150 个 `.asset`。累积到 327 个后，构建会出现 `BuildReport warnings=1` 且产出**无法启动**的 `level0`（玩家日志 `level0 is corrupted / Position out of bounds`）。修复：`ConfigureAssets()` 在每次构建开头清掉旧生成网格，构建恢复 `0 warning` 且可运行。**排查判据**：`tests/race/build_result.txt` 里 `warnings=1` 就必须重建，不要拿这个包出结论。

归档的旧构建数据放在 `build/Windows/_stale_builds/`（约 1.3 GB，均为可重建产物，可手动删除）。

**2026-11-16 06:35 交付包（12 款高模车 + 新赛道 COSTA NOVA + 转换管线修复）**：`Succeeded / 0 error / 0 warning`，构建日志 `tests/mcp/build_20261116_nova14_fix.out`，总产物 **223,906,099 B**。

| 验收项 | 实跑结果 | 留证 |
|---|---|---|
| `-racetest` 两圈 | `completed`，总时 **05:41.551**，最快圈 **02:50.736**，**1/4 名**，`collision_events=0` | `tests/race/race_telemetry.txt` |
| 检查点 / 圈数 | 12 个检查点 `1→11→0` 两轮 **24 次推进**，圈数 0→1→2 | 同上 |
| 性能 | 平均 **59.92 FPS**，最低 11.46（首帧装载） | 同上 |
| 赛道长度 | **2,607.4 m**，653 采样点，Rmin **18.1 m**，最大坡度 **14.7%** | `assets/track_layout.json` |
| `-garagecheck` 车库 | `variants=12`、`missing_script_components=0`、`bodies_switching_ok=**12/12**`、物理全程不变 | `tests/race/garage/garage_check.txt` |
| `-nitrotest` 氮气 overdrive | **191 帧** overdrive，连续 **4.68 s**；FOV **60.00 → 62.75（boost）→ 66.22（overdrive）** | `tests/race/nitro_test.txt` |
| `-collisiontest` 碰撞推挤 | `collision_count_delta=1`、`impact_strength=1.000`、相机抖动 **0.0438 m**、对手横向推挤 **0.05 m**、速度损失成立 | `tests/race/collision_test.txt` |
| `-garagedrive` 行驶中换车 | 连换 3 次，`mass=1350`、`wheelcolliders=4`、`wheel_visuals=4` 全程不变，行驶未中断 | `tests/race/garage_drive.txt` |
| `-padmaptest` 手柄映射 | **29/29 通过** | `tests/race/padmap_test.txt` |

### 本轮修掉的七个真实 bug

1. **引擎盖立起来**：HD 车队的 bonnet 网格在 Unity 里的 `localRotation` 是 308°，而车身是 270°——Blender 的 FBX 导出对「父级有旋转 + 子级沿负轴偏移」做了镜像。排查绕了四轮（Blender 侧量出的是水平盖子、Unity 侧却是翘起的），最终定位到 `body_bonnet.001` 与 `body_boot.001` 的父级 `localRotation` 不同。修复：`prepare_hd_fleet.py` 的 `flatten_panels()` 把盖板网格直接挂到车根、删掉中间的铰链 empty。
2. **父子缩放不一致**：Blender 导出时 `apply_unit_scale=True` 把单位缩放写进 FBX 根节点（×100）却不写进子节点，Unity 里根是 ×100、铰链是 ×1。修复：关掉 `apply_unit_scale`，并把归一化系数**烘焙进网格顶点**，所有对象缩放归 1。
3. **检查点数被硬编码覆盖**：`TrackPath.CheckpointCount` 是写死的 `10`，而 JSON 里 COSTA NOVA 要 12 个。后果是第 11 个检查点的索引溢出后 `Wrap` 回起点，车在第二圈起点原地打转。修复：`CheckpointCount` 改为读 `TrackLayout.CheckpointCount`，**单一数据源真正生效**。
4. **车库截图被赛道护栏与对手车干扰**：3/4 视角下远处的对手车尾翼与赛道广告牌横在车身上方，看起来像浮空零件。修复：`-garagecheck` 截图前隐藏对手车并抬高相机。
5. **GLB→FBX 转换把零件搬错了地方（三个叠在一起的根因）**：`bpy.ops.object.transform_apply` 对**带父级**的子物体不可靠（会把父级变换折进结果），而且它作用于**整个选择集**——GLB 导入后所有物体都处于选中状态，于是同一个变换被反复施加。症状是三处：CityCar / HotHatch / ThreeDoor 的**后尾门伸到车尾外 0.9 m**（像一块黄板挂在车尾）、**车轮塌到原点**、车身整体浮空 28 cm。修复：改用 `bmesh` 直接把缩放与旋转烘焙进顶点（`bake_basis()`），位置缩放只做一次且只作用于 empty，卸父级时显式写回世界矩阵。
6. **座椅头枕穿出车顶**：interior 网格最高点比车顶高 12 cm，于是每台双门轿跑的车顶上都坐着三个黑方块。修复：`clip_interior_to_roof()` 把超出车顶线的顶点删掉（那部分本来就在车顶里面，看不见）。
7. **「自动闭合尾门」差点把车弄坏**：第一版修复用搜索法去找「最贴合车身的旋转」，结果把尾门转到了车厢内部。查明尾门本来就是闭合的（真凶是第 5 条的转换 bug）后，把该逻辑改成**只读校验**：面板一旦超出车体 0.35 m 就直接让转换失败，不许偷偷挪几何。

### 车组升级：9 款低模 → 12 款高模

| | 旧（Road Car Showroom Lineup） | 新（Car Park and Road Vehicle Fleet） |
|---|---|---|
| 面数 | 约 **7.5k** | **29.7k – 48.3k** |
| 车型 | 7 款年代原型 | 10 款通用造型（含性能 / 肌肉 / 旅行 / 掀背 / 行政 / 城市 / 电动） |
| 细节 | 车尾近乎一块平板 | 带可开关的引擎盖 / 车门节点、原厂灯组、真实内饰 |

另有 `tools/audit_highpoly_cars.py`：遍历 3DAssets.dev 公开 API，把全站 406 个车辆类资产按三角面数排序，**避免「用低模凑数」**。逐款来源与许可见 `docs/ASSET_LICENSES.md`。

### 新赛道 COSTA NOVA：怎么自己设计一条

工具链不需要改一行代码就能换赛道，完整方法见 `docs/TRACK_DESIGN_TOOLCHAIN.md` 第 3.5 节。要点：

1. **先在 Python 里把几何调到合规，再进引擎**。赛道约束（总长 / 最小半径 / 最大坡度）全是纯几何量，Python 里算毫秒级，Unity 里试错要 3–4 分钟一次构建。
2. **用极坐标半径调制生成形状骨架**，不要手摆控制点：`r(t) = R0 + A1·cos(k1·t) + A2·cos(k2·t + φ)`，保证闭合、不自交、曲率连续。
3. **程序化扫参数空间**而不是手工试。`tools/design_track_nova.py` 遍历 588 组参数并按约束过滤；`A1=148` 时 **0 组合规**（坡度全部超标），`A1=132` + 高度幅度减半后才有 63 组。
4. 落盘 → `track_design_studio.py --all` → 无头重建 → `-racetest` 实跑。

| | COSTA BRAVA（旧） | COSTA NOVA（新） |
|---|---|---|
| 总长 | 2,282.6 m | **2,607.4 m** |
| 检查点 | 10 | **12** |
| 最小半径 | 17.7 m | 18.1 m |
| 最大坡度 | 20.3% | **14.7%** |
| 采样点 | 576 | 653 |

## 可移植工程（2026-11-15 17:28）

`Library` 原为指向 `D:\UnityProjectCaches\Alsphalt8-20261112\Library` 的 NTFS 目录联接；现已是工程内真实目录（缓存 2.43 GB / 28,999 文件复制回来，`reparse=False`），`Temp` 为普通目录，二者均被 `.gitignore` 忽略。旧缓存目录保留未删，可手动清理。`python tools/make_project_portable.py` 可幂等重复该处理。

验证：移除联接后无头编辑器正常启动、MCP 实例 `Alsphalt8 / 6000.0.29f1` 注册、console 0 error、`manage_scene load Assets/Scenes/CostaBrava.unity` 成功（`rootCount=1048`）。工程可整体拷贝/移动；若不带 `Library`，Unity 首次打开会重新导入（本机需先跑 `python tools/build_guard.py`）。

## 赛道设计工作台（2026-11-15）

赛道只有一份数据源 `assets/track_layout.json`（24 个控制点 + 村落/隧道/跳台跨度）。打开 `docs/track_design/track_preview.html` 可以直接拖动控制点，实时看俯视图、长度、最小转弯半径、最大坡度与 10 个检查点；算法与 `TrackPath.BuildSamples()` 的 Catmull-Rom 完全一致，所以预览即游戏里的赛道。

```
# 设计 → 预览 → 导出 → 重建 → 验收
python tools/track_design_studio.py --preview       # 生成可拖拽 HTML + 控制台分析
python tools/track_design_studio.py --emit-csharp   # 写 Assets/Scripts/TrackLayout.generated.cs
python tools/run_headless_build.py --tag <tag>      # 先清 traceevents 与工程锁、起 build_guard
.\build\Windows\AuroraCoast.exe -racetest
```

`TrackPath.ControlPoints()` 现在直接返回 `TrackLayout.ControlPoints`，缩放已烘焙进 JSON，不做二次变换。改线后道路、护栏、村落、隧道、跳台、检查点、AI 目标速度与小地图全部自动跟随；需要人工复核的是跨度区间是否仍落在合适路段、发夹弯 AI 是否可通过。完整工具链与代码逻辑见 `docs/TRACK_DESIGN_TOOLCHAIN.md`。

## 尚未验证 / 未完成

- **真机手柄未实测**：本机 `devices=NONE`（`AuroraCoast.exe -gamepadcheck` 留证于 `tests/race/gamepad_check.txt`）。映射决策表已由 `-padmaptest` 用 **29/29** 合成信号验证（死区 0.10、轴向、A/B/X/RB/LB/START、菜单确认与换车键），且游戏代码与测试调用同一份 `InputMap` 实现；但**插上手柄后的实际手感、摇杆曲线与按键延迟仍待人工确认**。
- **键盘换车（Q/E）与暂停菜单 `CHANGE CAR` 的真人按键未实测**：键盘路径已由 `-garagedrive` 证明换车后物理不变、车能继续行驶（连换 3 次、行驶 68.6 m），但按键本身的按下节奏与菜单中的左右切换手感需人工试玩。
- 玩家真人驾驶手感、镜头遮挡与碰撞细节仍需人工游玩验证。
- 画面仍有程序化痕迹：三角锯齿山脊、方块民居、远景地面纹理细节不明显；本轮只做到明显优于旧的光滑穹顶 / 单段村落 / 冷白光，不冒充照片级。
- 遥测 schema 没有复位次数列，本轮无法从 CSV 断言复位次数。
- **AI 比赛中仍不会出现氮气 overdrive**：`nitro_overdrive_frames=0`（正常比赛遥测）。原因是 AI 是间歇短按，`nitroStreak` 攒不到 1.5 s；overdrive 分支本身已由 `-nitrotest` 实跑证明（183 帧 / 4.57 s / FOV 69.52°）。若希望 AI 也能用出 overdrive，需要改 AI 的氮气策略而不是改 overdrive 判定。
- **10 款 HD 车是 3 万–4.8 万面的通用造型**，比上一批 7.5k 低模高 4–6 倍，已与 35.8 万面的 Ferrari / 38.5 万面的 BMW 同一量级；但仍不是照片级扫描车，也没有真实品牌标识（这是刻意选择，用来规避商标风险）。造型是「通用轿跑/掀背/旅行车」，没有真实品牌轮廓，这批资产的强项是**机械细节**（可开关的引擎盖与车门节点、四轮 pivot、8–9 个分材质）与**比例准确**（车长统一归一到 4.53 m）。
- **Ferrari 458 名称与车身造型涉及商标**：许可依据来自 three.js 官方仓库与其官方许可清单（原 Sketchfab 页已停用），非商业研究用途之外的发行需另行评估；详见 `docs/ASSET_LICENSES.md`。
- **主菜单转台仍是程序化布景**：车在起跑线上原地转圈，背景是赛道而非独立展台；已在 `-pausetest` 实截确认不遮挡 UI，但「展台」观感与真实展台棚还差一步。
- 车尾连续 LED 灯带在实跑截图里不够明显（尾灯本体可见），emissive 强度受曝光压制，属于待打磨的视觉细节。
- 地平线 / 狂野飙车 8 的车模是商业版权资产，本工程只借鉴机制、不使用其模型；正式车模为 CC BY 4.0 Ferrari 458、7 款 CC0 通用车型与 CC0 BMW，均非照片级扫描车。
- GUI 编辑器仍需用户以普通权限双击 `tools/start_unity_mcp.cmd`（agent shell 为管理员令牌，Unity 拒绝启动 GUI）。
