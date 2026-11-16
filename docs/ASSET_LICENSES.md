# 实际素材来源与许可

本文件只记录已经下载、检查或处理的文件。正式场景使用的文件位于 `Assets/`；研究素材与原始下载位于 `assets/` 和 `downloads/`。

## 正式车组：12 款可切换车型（2026-11-16 起）

玩家车在主菜单 / 暂停菜单用 `Q / E`（手柄 `LB / RB`，或 `<` `>` 按钮）切换；三辆对手车各自固定为不同车型。切换只换可见车身根节点与视觉轮引用，**共享同一套 Rigidbody / BoxCollider / 4 个 WheelCollider**，因此不会改变已验收的操控与比赛公平性。清单由 `Assets/Scripts/VehicleCatalog.cs` 单一维护。

### Ferrari 458 Italia — CC BY 4.0（**必须署名**）

- 作者：vicent091036，原 Sketchfab 模型 ID `57bf6cc56931426e87494f554df1dab6`。
- 官方分发：three.js examples `webgl_materials_car` 使用 `models/gltf/ferrari.glb`，示例页明确署名 "Ferrari 458 Italia model by vicent091036"：https://github.com/mrdoob/three.js/blob/dev/examples/webgl_materials_car.html
- 许可凭证：three.js 官方 media-license 清单 issue #23089 把 `gltf/ferrari.glb` 记为 `CC-BY 4.0`：https://github.com/mrdoob/three.js/issues/23089
- 许可文本：https://creativecommons.org/licenses/by/4.0/
- 文件：`assets/vehicles/ferrari_source.glb`（1,681,572 bytes，与官方仓库 `ferrari.glb` 字节数一致）；处理后的 `Assets/Vehicles/aurora_gt.fbx`；检查报告 `docs/vehicle_inspection.json`（58 objects / 358,788 polys / 四轮独立节点）。
- 署名位置：`VehicleCatalog.Cars[0].credit`、主菜单底部、暂停菜单 `CHANGE CAR` 行、`-garagecheck` 报告。
- **风险**：原 Sketchfab 页面已停用，许可依据来自 three.js 官方分发仓库与其官方许可清单，不是原作者页面；"Ferrari 458 Italia" 名称及车身造型涉及法拉利商标，本工程为个人非商业研究用途，若对外发行 / 商业化需另行取得商标与外观授权，或改用无品牌车型。

### 3DAssets.dev "Car Park and Road Vehicle Fleet" — CC0 1.0（10 款，无需署名）

- 来源：https://3dassets.dev/packs/car-park-and-road-vehicle-fleet ；每个资产页标注 `Licence: CC0 1.0 Universal`（https://creativecommons.org/publicdomain/zero/1.0/）。站内说明：全站模型可商用、可修改、无需署名；车型是**原创通用造型**，无厂商标识与商标。
- 原始 GLB：`assets/vehicles/fleet_hd/*.glb` 共 10 个文件（10,817,120 bytes），仅作为转换源保留。直链形如 `https://cdn.3dassets.dev/assets/32495/v1/model.glb`。
- 转换脚本：`tools/prepare_hd_fleet.py`；报告：`docs/hd_fleet_inspection.json`。
- **这一批替换掉了上一批低模车**（"Road Car Showroom Lineup"，每款仅约 7.5k polygons）。本批每款 **29.7k–48.3k polygons / 21–26 renderers**，是上一批的 4–6 倍，与 35.8 万面的 Ferrari、38.5 万面的 BMW 处在同一量级。
- 统一契约：车长 4.53 m、地面 `y=0`、车头 `-Z`（FBX 内），四轮 pivot `wheel_fl/fr/rl/rr`，网格按材质类别改名为 `body/glass/rim/tire/trim/interior/lightwhite/lightamber/lightred/lightgreen`。
- 每台车都带**可开关的 bonnet / door 节点**（`door-front-left`、`door-front-right`、`bonnet`），这是该套件相对低模版多出来的细节，也是「狂野飙车式」车库展示的前提。
- 尾翼不再手工填高度：构建器实测每款车的车顶高度写入 `VehicleGarage.Variant.roofHeight`，尾翼按车顶 + 6 cm 自动定位，所以 12 款车共用一套尾翼几何也不会浮空或插进车顶。

| 源 ID | 正式 FBX | 车型 | 面数 |
|---|---|---|---|
| 32495 | `Assets/Vehicles/FleetHD/SportsCarHD.fbx` | 中置引擎运动跑车 | 48,274 |
| 32515 | `Assets/Vehicles/FleetHD/MuscleCoupeHD.fbx` | 经典肌肉轿跑 | 29,756 |
| 32488 | `Assets/Vehicles/FleetHD/TouringCoupeHD.fbx` | 豪华旅行轿跑 | 29,732 |
| 32514 | `Assets/Vehicles/FleetHD/SportsSaloonHD.fbx` | 运动四门轿车 | 31,092 |
| 32499 | `Assets/Vehicles/FleetHD/ClassicSaloonHD.fbx` | 经典三厢轿车 | 33,516 |
| 32494 | `Assets/Vehicles/FleetHD/HotHatchHD.fbx` | 两厢运动掀背 | 31,124 |
| 32493 | `Assets/Vehicles/FleetHD/ThreeDoorHD.fbx` | 三门掀背 | 30,216 |
| 32492 | `Assets/Vehicles/FleetHD/ExecutiveHD.fbx` | 行政级轿车 | 30,788 |
| 32487 | `Assets/Vehicles/FleetHD/CityCarHD.fbx` | 城市微型车 | 30,608 |
| 32496 | `Assets/Vehicles/FleetHD/ElectricHatchHD.fbx` | 电动掀背 | 30,482 |

### 3DAssets.dev "Road Car Showroom Lineup" — CC0 1.0（**已归档**，不再入库）

- 这一批每款只有约 7.5k polygons / 25 renderers，车尾近乎一块平板，放在 35.8 万面的 Ferrari 旁边明显像低模玩具，因此被上面那批 HD 车替换。
- 原始 GLB `assets/vehicles/cc0/*.glb`（20 个）与转换脚本 `tools/prepare_cc0_fleet.py`、报告 `docs/cc0_fleet_inspection.json` 仍保留在仓库里，作为「低模年代原型」这条备选路线的可复现记录；转换出的 `Assets/Vehicles/Fleet/` 已移出工程，归档在 `build/Windows/_stale_builds/Fleet_lowpoly_removed`。
- 源 ID 与车型对应（供日后需要轻量车时直接复用）：15113 中置超跑 / 15120 1990s 运动轿跑 / 15118 1970s 肌肉轿跑 / 15116 1960s 敞篷 / 15112 双座敞篷 / 15108 现代轿跑 / 15107 四座敞篷。

### BMW high-performance coupe（车库第 9 款 / 原正式主车）
- 作者：Mike Pan；几何与材质修正：Morgan McGuire。
- 来源：McGuire Computer Graphics Archive，https://casual-effects.com/g3d/data10/research/model/bmw/bmw.zip
- 许可：**CC0 / Public Domain**，原始页面明确标注。
- 原始规模：385,079 triangles；53 个处理后的网格对象。
- 本项目处理：Blender 实际导入 OBJ；按 2.76 m 轴距归一化；车身约 4.53 × 1.98 × 1.53 m；最低点对齐；四轮按空间位置重建 `wheel_fl/fr/rl/rr` 独立层级；轮胎直径约 0.716 m；深蓝金属车漆、玻璃、橡胶和金属分别设置材质。
- 正式文件：`Assets/Vehicles/AuroraGT.fbx`；本期改为车库中第 9 个可选车身，并是**唯一**显示程序化 AURORA aero kit 的车型（低矮超跑保留原厂剪影）。
- 可编辑源：`assets/vehicles/aurora_bmw_cc0.blend`；检查报告：`docs/bmw_cc0_inspection.json`。

## 环境模型与贴图

### Poly Haven（全部 CC0）
许可页：https://polyhaven.com/license

1. **Venice Sunset 1K HDR**
   - https://polyhaven.com/a/venice_sunset
   - 文件：`Assets/Environment/VeniceSunset.hdr`。
   - 用途：HDR 天空、环境照明与车身反射。
2. **Asphalt 02 2K PBR**
   - https://polyhaven.com/a/asphalt_02
   - 文件：`Assets/Textures/AsphaltColor.jpg`、`AsphaltNormal.jpg`；原始 roughness 位于 `assets/textures/`。
3. **Island Tree 01**
   - https://polyhaven.com/a/island_tree_01
   - 原始 1,599,403 polygons，实时 LOD 为 15,994 polygons。
   - 文件：`Assets/Vegetation/IslandTree.fbx`。
4. **Coast Land Rocks 02**
   - https://polyhaven.com/a/coast_land_rocks_02
   - 原始 1,291,146 polygons，实时 LOD 为 25,821 polygons。
   - 文件：`Assets/Vegetation/CoastRocks.fbx`。
5. **Shrub 01**
   - https://polyhaven.com/a/shrub_01
   - 原始 156,012 polygons，实时 LOD 为 12,480 polygons。
   - 文件：`Assets/Vegetation/Shrub.fbx`。

模型检查与减面数据：`docs/vegetation_inspection.json`。高面数 Pine Tree 01 只查询过，没有下载，也未在场景中使用。

## 音频

1. **Racing car engine sound loops** — domasx2，CC0
   - https://opengameart.org/content/racing-car-engine-sound-loops
   - 六个不同音高循环，文件：`Assets/Audio/Engine0.wav` 至 `Engine5.wav`。
2. **Car tire squeal skid loop** — audible-edge / Tom Haigh，CC BY 3.0
   - https://opengameart.org/content/car-tire-squeal-skid-loop
   - 文件：`Assets/Audio/TireSkid.wav`。
3. **Racing Speed Boost Sound** — Iwan Gabovitch / qubodup，CC BY 3.0（本工程选择该许可）
   - https://opengameart.org/content/racing-speed-boost-sound
   - 文件：`Assets/Audio/BoostStart.ogg`、`BoostLoop.ogg`、`BoostEnd.ogg`。
4. **Beach Ocean Waves** — jasinski，CC0
   - https://opengameart.org/content/beach-ocean-waves
   - 文件：`Assets/Audio/OceanWaves.flac`。
5. **Wind whoosh loop** — OpenGameArt 来源页标注 CC0
   - https://opengameart.org/content/wind-whoosh-loop
   - 文件：`Assets/Audio/WindLoop.ogg`。
6. **Metal Impact Sounds / thud2** — Brian MacIntosh / BMacZero，CC0
   - https://opengameart.org/node/118334
   - 文件：`Assets/Audio/Impact.wav`。

CC BY 3.0 文本：https://creativecommons.org/licenses/by/3.0/

## 原创程序化内容

道路样条与网格、地形、海面运动、隧道、跳台、近路、护栏、路标、制动牌、村落体块与屋顶、远景山体、起跑门架、车辆物理、AI、比赛规则、UI、镜头、混音逻辑均为本工程代码生成，不依赖未列出的外部模型。

2026-11-15 追加：车辆外观套件（亮金属铜色车漆与清漆、抛光轮毂、发光尾灯带与前 DRL、碳纤维尾翼／端板／扩散器鳍片／侧裙、青紫氮气等离子粒子）全部为工程内程序化材质与基础几何，挂在物理根节点上且不添加碰撞体，**不引入任何新的外部模型或贴图**。地平线 / 狂野飙车 8 的车模属于商业版权资产，本工程只借鉴玩法机制，不使用其模型文件。

## 许可复核结论与变更记录

- **早期判断修正**：最初只找到"Sketchfab 页面已停用、原页许可无法复核"这一条证据，因此把 three.js Ferrari 458 列为研究素材、未进入正式包。后续复核到 three.js 官方仓库仍在分发该 GLB、官方示例页有作者署名、官方 issue #23089 的 media-license 清单明确记为 `CC-BY 4.0`，且工程内文件字节数与官方仓库一致，据此改判为**可用、但必须署名**，并已提升到正式车组。判断依据和残留风险写在上文，不做"许可已完全无风险"的表述。
- **开源车模并非稀缺，而且高精度的也有**：3DAssets.dev 的 "Car Park and Road Vehicle Fleet" 是 CC0 1.0 的通用车型套件，本工程选用 10 款运动 / 经典造型转换入库，每款 2.97–4.83 万面；加上 CC0 的 BMW 与 CC BY 4.0 的 Ferrari，正式车组共 **12 款**。通用车型无品牌标识，规避了商标风险；Ferrari 是唯一带真实品牌的一款。审计脚本 `tools/audit_highpoly_cars.py` 会遍历 3DAssets.dev 的公开 API，把全站车辆资产按三角面数排序，避免「用低模凑数」。
- **仍然排除**：商业游戏（Forza Horizon / Asphalt 8 等）提取的车模 rips，无论是否只作个人学习，均不进入本工程。
- 逐款可视证据：`AuroraCoast.exe -garagecheck` → `tests/race/garage/garage_check.txt` + 每款一张截图。

## 待授权资产（申请中，**尚未取得，不得入包**）

本节记录的是**正在申请、尚未拿到**的资产。规则：**在书面授权到手之前，这些资产不得出现在任何构建产物、截图、预告片或商店页里**。申请材料在 `docs/licensing/`。

### 授权调研结论（2026-11-16）

| 品牌 | 技术上可得 | 许可 / 授权状态 | 结论 |
|---|---|---|---|
| **McLaren**（P1、720S、765LT、Senna、F1、Artura、Speedtail、Solus GT、Elva） | Sketchfab 上 **24 个可下载**模型，面数 6.9 千 – **306 万面**（2025 765LT）；另有多份非商业版（CC BY-NC / CC BY-NC-SA） | **没有一个 CC0，没有一个 W Motors 官方授权**。第三方自标 CC 的，**只覆盖文件著作权，不覆盖商标与外观设计权**，上传者本身无权许可 | **不可用于对外发行**。Sketchfab 上也没有 McLaren 官方账号，全部是第三方上传 |
| **W Motors 自有品牌**（Lykan Hypersport、Fenyr SuperSport、Sabre、Ghiath） | 无公开 3D 资产 | 公司本身持有，**无中间代理商**。CEO 2014 年公开表态愿意让车进游戏；Lykan 已有《GT Racing 2》《Drive Club》(PS4) 先例 | **首选谈判对象** |
| 其他 CC0 源 | 3DAssets.dev 全站 644 个车辆资产已审计：**无任何品牌命名资产**，顶配就是本工程在用的 4.87 万面中置超跑；Sketchfab CC0 全库车辆仅 `1939.678 Streamline Automobile`（10 万面）与 `SY Carola`（50 万面，1960s 意大利警车） | CC0 | 无可用超跑 |
| McGuire CC0 档案 | 直链探测 `corvette / porsche / lotus / ford / toyota / audi / nissan / volvo` **全部 404**；仅 `bmw` 存在（本工程已用） | CC0 | 无更多车型 |

> **车型归属提醒（避免谈错对象）**：`Lykan Hypersport`、`Fenyr SuperSport`、`Sabre` 属 **W Motors**，**与迈凯伦无隶属关系**（2015《速度与激情 7》主角车是 Lykan）；**`Speedtail` 才是 McLaren Automotive 的**。

### 授权路线现状

- 2024-12 CYVN Holdings 收购 McLaren Group 全部流通股；2025 新设 **McLaren Licensing** 部门；消费品授权代理为 **CAA Brand Management**（明确覆盖 gaming 品类）。
- **负面情报（同一时间窗内）**：2026-07《Assoluto Racing》未与 McLaren Automotive 续约，F1 / F1 LM / P1 / 720S 被下架；2026-09 独立工作室 Hutch Games《Top Drives》移除 McLaren 车型卡牌，公开说法是**无法接受 McLaren 的授权报价**。→ 授权门槛与报价在收紧，中小独立项目胜算低。
- 因此采用**两线并行**：McLaren 走 CAA（成本仅一封邮件，值得试）+ W Motors 走公司直联（现实落点更高）。

申请材料见 `docs/licensing/`：项目简介、两封邮件草稿、逐车资产需求矩阵。

### 待授权清单

| 品牌 | 目标车型 | 状态 | 阻塞的玩法 |
|---|---|---|---|
| McLaren | [待定，优先 720S / P1 / 765LT] | **未申请** | — |
| W Motors | Lykan Hypersport（首选）/ Fenyr SuperSport / Sabre | **未申请** | — |

**当前正式车组不依赖以上任何一项**：12 款车全部来自已核验许可的素材，玩法验收（氮气 overdrive / 碰撞推挤 / 行驶中换车 / 手柄映射 / 两圈实跑）全部以它们为准并已通过。
