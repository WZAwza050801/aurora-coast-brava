# 赛道设计工具链 / 代码逻辑 / 引擎选型

> 2026-11-15 · AURORA / Costa Brava
> 目的：把「改赛道」从「翻 C# 源码猜坐标」变成「改 JSON → 秒级预览 → 一键重建 → 实跑验收」。
> 本文所有数字来自本机真实构建与遥测，不依赖推断。

---

## 0. 结论前置

| 问题 | 结论 |
|---|---|
| 用 UE / Unity / Three.js 哪个 | **不是三选一，是分层**：游戏本体继续 Unity HDRP；赛道设计器用纯 HTML/Three.js 思路做外部预览；UE 只在「换显卡 + 做写实开放世界作品集」时另开新项目 |
| 地平线 / 狂野飙车 8 的车模开源吗 | **不开源**。车模是商业版权资产，提取复用构成侵权；能自由借鉴的是**机制**（氮气、碰撞、特技、镜头），不是模型文件。**但开源整车资源其实很多**，见 2.3 |
| 工程里现在有车模吗 | **9 款可切换**：CC BY 4.0 的 Ferrari 458 Italia、7 款 3DAssets.dev CC0 年代车型、CC0 的 G3D BMW（385,079 tris）。全部已归一化 4.53 m 车长并拆出四轮独立节点 |
| 「帅一点」的最大杠杆 | 不是换模型，是**逆光下车漆太暗**。亮金属漆 + 清漆高光 + 发光灯带 + 尾翼/扩散器 + 蓝紫氮气焰 + 镜头高度，比换低模资源有效得多 |
| 赛道美术怎么写 | **单一数据源 + 全程按弧长参数化 + 分层生成 + 固定种子噪声 + 材质合并**，不要在任何装饰里写死世界坐标 |
| 氮气 / 碰撞有吗 | **已经有**：氮气存量/消耗/漂移与腾空回充/推力/喷焰/FOV；碰撞 `OnCollisionEnter` 冲击扭矩。都是简化版，本文第 7 节给出 Asphalt 8 对照与升级路线 |

---

## 1. 引擎选型：为什么建议「Unity 本体 + Web 设计器」

| 维度 | Unity 6000 HDRP（当前） | UE 5（Nanite/Lumen/Chaos） | Three.js / WebGPU |
|---|---|---|---|
| 现有资产复用 | 100%（车模、HDRI、PBR 贴图、WheelCollider、HDRP Volume、MCP 栈、构建与验收脚本） | 约 0%，全部重新导入并重写生成逻辑（C++/BP） | 约 0%，物理与后处理全部自己造 |
| 车辆物理 | `WheelCollider` + 自定义下压力/防倾杆/漂移，已调通且实跑 2 圈 | Chaos Vehicle 更强，但要重新调参 | 需引 Rapier/cannon-es，手感从零开始 |
| 画面上限 | HDRP 已够用；瓶颈在美术内容不是管线 | 写实上限最高 | 上限受浏览器与素材量限制 |
| 本机硬件 | Iris Xe 核显实测 **59.95 FPS @720p** | Nanite/Lumen 在核显上大概率个位数 FPS，验收链路作废 | 浏览器内可跑，但和 Unity 包不是同一个东西 |
| 迭代速度 | 无头重建约 3–5 min + 实跑 5 min | 编译/烘焙慢 | **秒级**，最适合设计器 |
| 分享给导师/同学 | 要发 EXE | 要发 EXE | 一个 HTML 链接即可 |

**建议**：

1. **游戏本体留 Unity**。现在不是「选引擎」阶段，是「已有可跑产品」阶段；迁移成本远大于收益，而且会丢掉已验证的 0 error 构建链和 `-racetest` 遥测。
2. **赛道设计搬到 Web**（本文交付的 `docs/track_design/track_preview.html` 就是这个思路的最小实现）：拖控制点 → 实时看俯视图/长度/曲率/坡度 → 导出 JSON。设计阶段不启动 Unity，迭代从「分钟级」变「秒级」。
3. **UE 不是不好，是目标不同**。若目标改成「地平线级写实开放世界 + 作品集展示」且换到独显机器，UE5 的 PCG/World Partition/Lumen 确实更合适——但那是新项目，不是把这个工程迁过去。
4. Three.js 只做设计器/展示层，**不要用它替代游戏本体**：没有 WheelCollider 级别的车轮物理、没有 HDRP 体积雾与 ACES，重写一遍只能得到更弱的手感。

一句话：**Unity 负责「能跑、能验收」，Web 负责「能设计、能讲解」，UE 留给下一个项目。**

---

## 2. 车模资源：开源真相与合法清单

### 2.1 必须先纠正的前提

Forza Horizon（Turn 10 / Playground）与 Asphalt 8（Gameloft）的车辆模型是商业授权资产，厂商从未以开源许可发布。社区里流传的「提取包 / 转换包」在法律上属于 **rips**：即使只改颜色、只做个人学习、不商用，也不改变侵权性质；署名不能替代授权。把这类模型放进工程会污染整个 `docs/ASSET_LICENSES.md` 的合规性。

**但要纠正一个常见误判**：这不等于「开源跑车模型很少」。恰恰相反，CC0 / CC BY 的整体车模很多，既有高精度写实款（G3D BMW、Sketchfab 筛 CC BY），也有成体系的通用车型套件（3DAssets.dev Road Car Showroom Lineup 一套 70 个模型、含 19 款整车）。只要避开「商业游戏提取包」与「真实品牌商标」两个坑，做多车型车库并不难——难点只是**归一化契约**（轴向、尺寸、四轮 pivot、网格命名），而工程里已有 Blender 批处理流水线解决它。

**可以自由实现的是玩法机制**：氮气分阶、碰撞与撞毁、空中特技、漂移充能、镜头 FOV 冲击——游戏机制不受版权保护（可能受专利约束，但街机赛车这套机制是行业通用做法）。

### 2.2 工程现状（已核实）

| 文件 | 来源 | 许可 | 状态 |
|---|---|---|---|
| `Assets/Vehicles/aurora_gt.fbx` | three.js examples 的 Ferrari 458 Italia（vicent091036），官方许可清单 issue [#23089](https://github.com/mrdoob/three.js/issues/23089) 记为 CC-BY 4.0 | **CC BY 4.0 / 必须署名** | ✅ 车库第 1 款；51 renderers / 358,788 polys / 四轮独立节点。原 Sketchfab 页停用，依据为官方仓库 + 官方许可清单，残留商标风险见 `ASSET_LICENSES.md` |
| `Assets/Vehicles/Fleet/*.fbx` | [3DAssets.dev Road Car Showroom Lineup](https://3dassets.dev/packs/road-car-showroom-lineup) | **CC0 1.0 / 无需署名** | ✅ 7 款通用年代车型，各 25 renderers、约 7.5k polys；`tools/prepare_cc0_fleet.py` 批量转换 |
| `Assets/Vehicles/AuroraGT.fbx` | Mike Pan / Morgan McGuire, McGuire Computer Graphics Archive（[链接](https://casual-effects.com/g3d/data10/research/model/bmw/bmw.zip)） | **CC0 / Public Domain** | ✅ 车库第 9 款（原正式主车），385,079 tris → 归一化 4.53 × 1.98 × 1.53 m，四轮独立节点；唯一显示 aero kit 的车型 |
| Poly Haven HDRI/PBR、植被 | [polyhaven.com/license](https://polyhaven.com/license) | CC0 | ✅ 天空、沥青、树、岩石、灌木 |
| OpenGameArt 音频 | 见 `ASSET_LICENSES.md` | CC0 / CC BY 3.0 | ✅ 引擎、胎噪、氮气、海浪、撞击音 |

### 2.3 合法可用的车模资源（真实检索，2026-11-15）

| 来源 | 许可 | 风格 / 精度 | 适合 |
|---|---|---|---|
| [Kenney Car Kit](https://kenney.nl/assets/car-kit) / [Racing Kit](https://kenney.nl/assets/racing-kit) | CC0 | 低模、风格统一、45 / 110 个文件，GLTF/FBX/Blend | 原型、赛道 tile、交通车 |
| [Quaternius](https://quaternius.com) | CC0 | 低模精品，含 Sports Car（约 5.8k tris） |  stylized 赛车、对手车 |
| [KayKit](https://kaylousberg.com) | 多数 CC0 | 低模、含动画 | 成套美术 |
| [Poly Pizza](https://poly.pizza) | 多为 CC-BY | 低模 GLB | 需署名，风格统一 |
| [Sketchfab](https://sketchfab.com) | 混合，**逐个筛 Downloadable + 许可** | 高精度最多 | 精挑 CC0/CC-BY 高模 |
| [Poly Haven](https://polyhaven.com) | CC0 | HDRI / 材质 / 扫描道具，**没有整车** | 反射环境与 PBR |
| Fab（Megascans 免费包）、Unity/UE 商店免费区 | 各自 EULA | 中高精度 | 需逐条核对许可文本 |
| [3DAssets.dev Road Car Showroom Lineup](https://3dassets.dev/packs/road-car-showroom-lineup) | **CC0 1.0** | 一套 70 个模型、其中 19 款整车，8–10k tris/车，四轮 / 车门 / 机盖均有命名 pivot，无品牌标识 | **本期已用 7 款**，做多车型车库最省事：轴向与命名统一，批处理转换即可 |
| G3D BMW（本项目已用） | CC0 | **38.5 万面写实车模** | 已满足「帅」的几何基础 |

**另一条流程纪律**：每次重建前确认 `tests/race/build_result.txt` 是 `warnings=0`。`PersistMesh()` 曾因 `GenerateUniqueAssetPath()` 无限累积 `Assets/GeneratedMeshes/*.asset`，到第 10 次重建后 `warnings=1` 且 `level0` 序列化损坏，玩家启动直接崩溃；已在 `ConfigureAssets()` 里加自动清理。

**判断（已按老板反馈修正）**：开源整车不稀缺，所以本期直接做了 9 款可切换车库，而不是继续只打磨一款。两个关键取舍：① 用共享物理根 + 多个 inactive 车身变体，切换不影响已验收操控；② 通用 CC0 车型负责「丰富」，CC BY 4.0 的 Ferrari 负责「名牌帅感」，但保留署名与商标风险披露。Kenney 低模仍是原型 / 交通车的好选择，只是不会比高模更帅。

### 2.4 换模型的标准流水线（已有，可复用）

```
下载 GLB/OBJ → tools/prepare_vehicle.py（单款）/ tools/prepare_cc0_fleet.py（批量，Blender 导入 / Draco 解码）
→ 归一化车长 4.53 m、车头 +Z、地面 y=0、四轮拆成 wheel_fl/fr/rl/rr、网格按材质名分类
→ 导出 FBX 到 Assets/Vehicles/ → 在 VehicleCatalog.Cars 加一行（id / 署名 / assetPath / yaw / 车漆）
→ 无头重建 + -garagecheck（逐款截图）+ -racetest（两圈验收）
→ 把来源、作者、许可、URL 写进 docs/ASSET_LICENSES.md
```

**最容易踩的坑（本期实测）**：GLB 经 Blender 导出的 FBX 会在**根节点**留下轴转换变换（`localRotation=(270,0,0)`、`localScale≈100`），Ferrari 与 7 款 CC0 车都是如此；只有从 OBJ 处理的 BMW 是 `(0,0,0)/(1,1,1)`。如果用 `model.transform.localRotation = Quaternion.Euler(0, yaw, 0)` 直接给模型设朝向，就会把轴转换抹掉，车会立起来或压成平板。正确做法：**每款车的 yaw 施加在父节点上，模型根节点自身的 rotation/scale 一律不动**。判别方法：在 Unity 里读 FBX 根节点的 `localRotation/localScale`，并用四轮 pivot 的 `wheel_fl.z - wheel_rl.z` 确认车头方向；再用 `-garagecheck` 的 3/4 视角截图肉眼复核，不能只看正面截图。

---

## 3. 赛道美术：怎么写才不塌

核心原则一句话：**所有几何都由「路径 + 参数」派生，任何装饰都不写死世界坐标。**

1. **单一数据源**：控制点只有一份（`assets/track_layout.json`）。道路、护栏、村落、隧道、跳台、检查点、AI 目标速度、小地图全部从它派生。改线时它们自动跟随。
2. **弧长参数化**：用 `t = distance / TotalLength ∈ [0,1)` 定位一切。村落写成 `spans = [[0.06,0.18], ...]`，跳台写 `[0.45,0.47]`，不要写「x=350, z=160」。当前赛道 2,282.6 m / 576 采样点 / 10 检查点。
3. **分层生成**（由近到远，预算依次递减）：
   - 路面 mesh + PBR 沥青 + 白线/路肩（几何最重、最影响可读性）
   - 护栏、路灯、制动牌（沿 `side` 向量偏移）
   - 近景植被/岩石（密度随曲率、坡度变化，固定种子）
   - 中景叙事元素：村落、隧道、跳台、近路（按 t 区间）
   - 远景山脊环带（多频噪声起伏，emissive 补光避免纯黑剪影）
   - 天空 / 雾 / 曝光 / ACES / Bloom（决定「黄金时刻」的第一因素）
4. **噪声而非球体**：山体用多频 Perlin 生成锯齿山脊；地形用低频丘陵 + 高频细节；散布用固定 `System.Random(seed)` 保证可复现。上一轮把 15 个光滑球穹顶换成 3 圈山脊，画面立刻脱离草模感。
5. **性能纪律**：按材质合并 renderer（村落 108 栋 → 5 个 renderer）、远景降面、树/岩石 LOD（树 1,599,403 → 15,994 polys）、粒子上限、固定 60 FPS 目标。
6. **验证闭环**：改参数 → 无头重建 → `-racetest` 实跑 → 截图 + 遥测对比。任何「更好看/更快」的结论必须有图或数字。

**六个已经踩过的坑**：① 球体堆山 = 黑色半球；② 村落只放一段 = 半程空场；③ 逆光下山体纯黑剪影，需要 emissive 补光；④ 地表贴图 UV 尺度太大 = 看不出纹理；⑤ 装饰写死坐标 = 改赛道全崩；⑥ 几何不合并 = renderer 暴涨掉帧。

---

## 3.5 「自己走一遍赛道设计」的完整方法（2026-11-16 实做）

这一节不是理论，是本轮真的走了一遍之后沉淀下来的流程。新赛道 **COSTA NOVA** 由
`tools/design_track_nova.py` 生成，走完整条链路并实跑验收。

### 为什么「先在 Python 里设计」而不是「在引擎里摆」

在 Unity 里摆控制点是**反向工作流**：每改一个点就要等一次无头构建（3–4 分钟），
一天只能试十几条线。而赛道的约束（总长、最小半径、最大坡度）全是**纯几何量**，
在 Python 里算只要毫秒级。所以正确的顺序是：**Python 里把几何调到合规 → 导出 C# →
进 Unity 只做场景装饰 → 实跑验收**。

### 五步法

**第 1 步：定形状骨架，不要先画点。**
用极坐标半径调制生成闭合曲线：

```
r(t) = R0 + A1·cos(k1·t) + A2·cos(k2·t + φ)
x = r(t)·sin(t)·sx     z = r(t)·cos(t)·sz
```

- `R0` 基准半径决定赛道整体大小；
- `k1`（低频）决定**有几个大弯**，`k2`（高频）决定**碎弯密度**；
- `A1/A2` 决定弯有多急；
- `sx / sz` 把圆形拉成椭圆，赛道立刻不像「齿轮」。

这个形式保证曲线**一定闭合、一定不自交、���率连续**，比手摆控制点稳得多。
街机赛道的经验值：`k1 = 3`（3 个大弯）+ `k2 = 5`（5 个碎弯）+ `A1/A2 ≈ R0 的 40%/18%`。

**第 2 步：高度用独立低频函数。**
`y(t) = H0 + H1·cos(k1'·t) + H2·sin(k2'·t)`，`k1' = 2` 保证坡道是长坡而不是台阶。
关键比例：**H 的幅度必须跟 A 匹配**——弯越急、路线越长，同样的高度差落在越短的
水平距离上，坡度就越陡。本轮第一次把 `A1` 从 118 提到 148，结果全部设计都撞上
24–30% 坡度超标（0 组合规），把 `A1` 降到 132、`H1` 从 15 降到 11 才有 63 组合规。

**第 3 步：程序化扫参数空间，而不是手工试。**
`design_track_nova.py` 的 `search()` 会遍历 588 组 `(控制点数, 缩放, sx, sz)`，
每组都算总长 / 最小半径 / 最大坡度，按约束过滤。本轮结果：
- `A1 = 118` → 60/270 组合规，选中 2,602.9 m / Rmin 17.3 m / 19.5%
- `A1 = 148` → **0/588 组合规**（全部坡度超标）
- `A1 = 132` → 63/588 组合规，选中 2,566.1 m / Rmin 18.3 m / 15.2%

评分函数里加了「最小半径越接近 16 m 越好」的项，所以选出来的是**又急又合规**的解，
而不是仅仅没被淘汰的解。

**第 4 步：用同一条算法在 Python 与 C# 两侧验算。**
`design_track_nova.py` 的 `analyse()` / `report()` 是 `TrackPath.BuildSamples()` 的
Python 复刻（同样的 Catmull-Rom、同样的 4 m 细分、同样的弧长累积）。两边算出的
总长必须对得上（2,566.1 vs 2,607.4 m，差值来自采样密度），否则说明公式抄错了。

**第 5 步：落盘 → 导出 → 进引擎 → 实跑。**

```bash
python tools/design_track_nova.py --write --backup   # 写 assets/track_layout.json
python tools/track_design_studio.py --all            # 预览 + 导出 C# + 约束校验
python tools/run_headless_build.py --tag <tag>       # 无头重建
AuroraCoast.exe -racetest                            # 两圈实跑验收
```

### 关键不变量（换赛道也不用改）

- `TrackLayout.generated.cs` 是**生成文件**，永不手改；
- 装饰物全部挂在 `t ∈ [0,1)` 弧长区间上，改线自动跟随；
- `TrackPath.CheckpointCount` 由 JSON 的 `checkpoints` 决定（Costa Nova = 12）；
- 地形网格从赛道包围盒自动推导并加 220 m 余量，新赛道外扩到 ±430 m 也不用改代码；
- 村��� / 隧道 / 跳台的 `spans` 用归一化区间，与具体坐标无关。

### 成果对照

| | COSTA BRAVA（旧） | COSTA NOVA（新） |
|---|---|---|
| 控制点 | 24 | 22 |
| 总长 | 2,282.6 m | **2,607.4 m** |
| 采样点 | 576 | 653 |
| 检查点 | 10 | **12** |
| 路宽 | 13.0 m | 12.5 m |
| 最小半径 | 17.7 m | 18.1 m |
| 最大坡度 | 20.3% | **14.7%** |
| 高度范围 | — | 9.59 ~ 29.11 m |
| 主要弯道 | 1 处 | 2 处（R26.1 / R18.1） |

### 引擎选型的最终判断（补完第 1 节的结论）

「UE / Unity / Three.js 选哪个」这个问题的正确答法不是三选一，而是**按职责分层**：

| 职责 | 选型 | 理由 |
|---|---|---|
| 跑游戏本体 | **Unity** | 已经跑通并验收：HDRP 17.0.3、2,607 m 赛道、12 款车、60 FPS @ Iris Xe。换引擎等于重写全部玩法与验收。 |
| 赛道/车库**设计器** | **Three.js / Web** | 拖拽控制点、实时统计、导出 JSON，浏览器里秒级响应；不装引擎就能用，天然适合「设计器」这种低频重交互场景。工程里的 `track_preview.html` 就是这个思路的最小实现。 |
| 下一代高画质项目 | **UE** | Nanite / Lumen / PCG 在**写实赛道与大规模植被**上是量级优势。但本项目的美术是**程序化低多边形**风格（山脊环带 + 方块民居），用不上 Nanite 的主要收益，而迁移成本极高。 |

一句话：**Unity 跑本体，Three.js 做设计器，UE 留给下一个预算更高的项目。**
这一轮新增的 `track_preview.html` 就是「Three.js 做设计器」的现成实现——纯静态 HTML +
内联 JSON，拖拽控制点、实时显示长度/最小半径/最大坡度，不需要装任何东西。

---

## 4. 代码逻辑逐层拆解

| 层 | 文件 / 函数 | 输入 | 输出 / 关键公式 |
|---|---|---|---|
| ① 设计源 | `assets/track_layout.json` | 24 个 `[x,y,z]` 控制点 + 跨度配置 | 单一数据源 |
| ② 导出 | `tools/track_design_studio.py --emit-csharp` | JSON | `Assets/Scripts/TrackLayout.generated.cs`（`TrackLayout.ControlPoints`） |
| ③ 控制点 | `TrackPath.ControlPoints()` | 生成的静态数组 | `Vector3[]`，缩放已烘焙，不再二次变换 |
| ④ 样条 | `TrackPath.BuildSamples()` | 控制点 | 闭环 Catmull-Rom：`0.5*((2p1)+(-p0+p2)t+(2p0-5p1+4p2-p3)t²+(-p0+3p1-3p2+p3)t³)`；每段 `steps = max(12, ceil(|p2-p1|/4))` → **576 个采样点** |
| ⑤ 弧长 / 标架 | 同上 | 采样点 | `distance` 累积；`tangent = normalize(next - prev)`；`side = cross(up, tangent)`；`TotalLength = 末段距离 + 闭环回接` → **2,282.6 m** |
| ⑥ 查询 API | `At / PointAt(index, lane) / RotationAt / IndexAtDistance / NearestIndex / Curvature / TargetSpeed` | 索引或距离 | 所有上层逻辑的唯一入口；`NearestIndex` 带 hint 窗口 55 的局部搜索优化 |
| ⑦ 路面 | `AuroraRaceSceneBuilder.CreateRoad()` | `path.Samples` | 按 `side` 偏移生成三角网格 + UV + 沥青 PBR 材质；白线/路肩/路缘同层生成 |
| ⑧ 依附物 | `CreateGuardrails / CreateLamps / CreateBrakingBoards / CreateVillage / CreateTunnel / CreateJump / CreateShortcut` | `PointAt(i, lane)` + `t` 区间 | 全部随路径走；村落 4 段 108 栋按材质合并 |
| ⑨ 地形与远景 | `CreateTerrain / CreateDistantLandscape / CreateSea` | 路径包围盒 + 噪声 | 112² 地形网格、3 圈山脊、程序化地表贴图 |
| ⑩ 光照 | `AuroraProjectBuilder.CreateLighting()` | HDRI + Volume | 8.5° 暖阳 + 冷补光 + 落日盘 + 暖雾 + 固定曝光 + ACES + Bloom + ColorAdjustments |
| ⑪ 玩法 | `RaceManager / RivalDriver / ArcadeCarController / RaceHud` | `TrackPath` 查询 | 10 检查点、2 圈、4 车、AI 目标速度由 `Curvature` 推导 |
| ⑫ 生成入口 | `AuroraProjectBuilder.BuildCompleteRace` | — | 依次调用上述 → 保存 `Assets/Scenes/CostaBrava.unity` → 出 `build/Windows/AuroraCoast.exe` |
| ⑬ 验收 | `FullRaceSmokeTest` (`-racetest`) | 运行中的比赛 | `tests/race/race_telemetry.txt` + 5 张截图 + `build_result.txt` |

**关键不变量**：`TrackPath.CheckpointCount = 10`、`Samples` 数量随控制点变化但查询接口不变、所有跨度用 `t` 而非索引。这样改线不会破坏比赛规则。

---

## 5. 工具链：命令表

| 步骤 | 命令 | 耗时 |
|---|---|---|
| 0 保活 MCP 栈 | `.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/start_unity_mcp.py --headless --watch`（后台长任务） | 常驻 |
| 1 设计 | 打开 `docs/track_design/track_preview.html` 拖控制点 → 「导出 JSON」→ 覆盖 `assets/track_layout.json` | 秒级 |
| 2 分析 | `.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/track_design_studio.py --preview` | <1 s |
| 3 导出 C# | `.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/track_design_studio.py --emit-csharp` | <1 s |
| 4 编译守护 | `.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/build_guard.py --seconds 5400`（后台长任务，绕过管家挂起编译子进程） | 常驻 |
| 5 重建 | 清 `Library/Bee/**/*traceevents` + `Temp/UnityLockfile` → `.workbuddy/venvs/unity-mcp/Scripts/python.exe tools/run_headless_build.py --tag <tag>` | 3–5 min |
| 6 验收 | `./build/Windows/AuroraCoast.exe -racetest -screen-width 1280 -screen-height 720 -screen-fullscreen 0` | ~5 min |
| 7 留证 | `tests/race/race_telemetry.txt`、`race_01..05.png`、`build_result.txt`、Player.log | — |

> 环境纪律（已踩过）：无头编辑器与 watchdog 不能同时抢工程锁；改 C# 后必须重建 + 实跑，不能凭代码推断；`-logFile -` 才能拿到完整尾部日志。

---

## 6. 「自己走一遍赛道设计」实操 5 步

1. 打开 `docs/track_design/track_preview.html`，拖动橙色控制点。实时显示长度、最小半径、最大坡度；红线是急弯，蓝点是 10 个检查点，灰线是路宽边界。
2. 点「导出 JSON」，把下载的文件覆盖 `assets/track_layout.json`。想微调线形可点「平滑一次」（三点加权 0.25/0.5/0.25）。
3. 跑 `--emit-csharp`，`TrackLayout.generated.cs` 更新；**不要手改这个文件**。
4. 跑第 5 节的重建命令。此时道路、护栏、村落、隧道、跳台、检查点、AI 目标速度、小地图全部自动跟着新线走。
5. `-racetest` 实跑，对照 `race_telemetry.txt`：完赛时间、名次、检查点 1→9→0 两轮共 20 次推进、平均 FPS、复位情况。

**需要手动复核的**：村落/隧道/跳台的 `t` 区间是否还落在合适路段（山脊与海面是围绕路径包围盒生成的，改线幅度大时要看截图）；发夹弯半径是否小于 AI 可通过的阈值（当前最急 R=17.7 m，最大坡度 20.3%，racetest 已验证可通过）。

---

## 7. 氮气 / 碰撞：现状与 Asphalt 8 对照

### 7.1 工程现状（`ArcadeCarController.cs`）

| 机制 | 实现位置 | 当前行为 |
|---|---|---|
| 氮气存量 | `nitro = 55`，上限 100 | 漂移 `+5~16/s`、直线稳定 `+0.65/s`、腾空 `+10/s` |
| 消耗与推力 | `FixedUpdate` | `-24/s`，`AddForce(forward * 10500 * boostFade)`，极速 57 → 76 m/s |
| 触发 | `Update` | Shift / 手柄 X / RB；0.35 s 缓冲；需速度 >5 m/s 且未拉手刹 |
| 视觉 | `UpdateFlames` + `ChaseCamera` | 双排气粒子 + 点光源 + FOV +12/+5 + 抖动 + Motion Blur |
| 碰撞 | `OnCollisionEnter` | 0.3 s 冷却；`relativeVelocity > 4` 时加俯仰冲击扭矩 |
| 复位 | `ResetCar / ResetToPose` | 掉出 -35 m 或翻车自动回到 `LastSafePosition` |

### 7.2 对照 Asphalt 8 还缺什么

| Asphalt 8 标志性机制 | 工程状态 | 建议优先级 |
|---|---|---|
| 三段 / 完美氮气（短按普通、满槽触发更强的紫焰） | 缺 | ★★★ 手感与视觉收益最大 |
| 撞毁 wreck（高速撞护栏/对手，车身旋转、镜头慢镜） | 缺 | ★★ 演出感强，但会影响 racetest 稳定性 |
| 护栏碰撞的速度惩罚与火花 / 震动 / 音效 | 部分（只有扭矩，有 `Impact.wav` 未充分用） | ★★★ 成本最低 |
| 对手车互相推挤 | 部分（Rigidbody 天然碰撞，无专门推力） | ★★ |
| 空中特技（翻转/桶滚 + 落地氮气奖励） | 腾空仅 `+10/s` 氮气 | ★ 需改镜头与物理，风险高 |
| 撞击/漂移的屏幕震动与色差 | 仅氮气抖动 | ★★ |

**实现原则**：机制可以照着做，但每加一条都要重跑 `-racetest`，确认两圈仍能 `completed` 且 FPS 不明显退化——街机感不能以牺牲可验证性为代价。

---

## 8. 验收纪律与风险

- 任何性能、画面、完赛结论必须有真实截图 / 遥测 / 日志；无法验证的明确列出来。
- 改 C# / 场景生成代码 → `-batchmode -executeMethod AuroraProjectBuilder.BuildCompleteRace` 重建 → `AuroraCoast.exe -racetest` 实跑，不用代码推断代替验收。
- 当前基线（2026-11-15 17:25 包）：`completed / 04:57.246 / 2 of 4 / 最快圈 02:28.521 / 59.95 FPS @1280×720 Iris Xe D3D11`，检查点两轮 20 次推进无误。
- 未完成：真人驾驶手感、镜头遮挡、碰撞细节、物理手柄真机按键（`gamepad_check.txt` 显示 `devices=NONE`）。
