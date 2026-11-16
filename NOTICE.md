# NOTICE — 素材许可与署名清单

本文件是本仓库的**许可对齐表**：仓库里每一个第三方文件，在这里都能查到它的作者、许可、署名义务和商标风险。

> **本仓库定位：个人非商业研究项目（personal, non-commercial）。**
> 仓库代码与原创内容采用 [CC BY-NC 4.0](LICENSE)；第三方素材**各自适用其原始许可**，逐项列在下表。两者不互相覆盖——CC BY-NC 4.0 不适用于第三方素材。
> 详细技术检查报告见 [`docs/ASSET_LICENSES.md`](docs/ASSET_LICENSES.md)。

---

## 1. 本仓库自己的内容

| 内容 | 许可 |
|---|---|
| `Assets/Scripts/**`、`assets/Editor/**`、`tools/**` 等全部源代码 | **CC BY-NC 4.0**（见 [`LICENSE`](LICENSE)） |
| 程序化生成内容：赛道样条与网格、地形、海面、隧道、跳台、护栏、路标、村落体块、远景山体、起跑门架、车辆物理、AI、比赛规则、UI、镜头、混音逻辑 | **CC BY-NC 4.0**，本工程原创 |
| 程序化车辆套件：金属车漆与清漆、抛光轮毂、发光尾灯带与前 DRL、碳纤维尾翼／端板／扩散器鳍片／侧裙、青紫氮气等离子粒子 | **CC BY-NC 4.0**，本工程原创，**不引入任何外部模型或贴图** |
| 赛道数据 `assets/track_layout.json`（COSTA NOVA） | **CC BY-NC 4.0**，本工程原创设计 |
| 文档（`README.md` / `docs/**`） | **CC BY-NC 4.0** |

---

## 2. 第三方素材 · 车辆

| 素材 | 文件 | 作者 / 来源 | 许可 | 署名义务 |
|---|---|---|---|---|
| **Ferrari 458 Italia** | `Assets/Vehicles/aurora_gt.fbx`<br>上游原始件 `assets/vehicles/ferrari_source.glb` | vicent091036，经 [three.js examples](https://github.com/mrdoob/three.js/blob/dev/examples/webgl_materials_car.html) 分发 | **CC BY 4.0** | **必须署名**：<br>Ferrari 458 Italia model by vicent091036, CC BY 4.0 |
| **BMW 高性能轿跑（车库 AURORA GT）** | `Assets/Vehicles/AuroraGT.fbx` | Mike Pan / Morgan McGuire，[McGuire Computer Graphics Archive](https://casual-effects.com/g3d/data10/research/model/bmw/bmw.zip) | **CC0 / Public Domain** | 无需署名（致谢可选） |
| **10 款 CC0 高模车**（中置运动跑车 / 经典肌肉轿跑 / 豪华旅行轿跑 / 运动四门 / 经典三厢 / 两厢掀背 / 三门掀背 / 行政轿车 / 城市微型车 / 电动掀背） | `Assets/Vehicles/FleetHD/*.fbx`<br>上游原始件 `assets/vehicles/fleet_hd/*.glb` | 3DAssets.dev ["Car Park and Road Vehicle Fleet"](https://3dassets.dev/packs/car-park-and-road-vehicle-fleet) | **CC0 1.0** | 无需署名 |
| **20 款 CC0 低模车**（已归档，未入正式场景，保留作可复现记录） | `assets/vehicles/cc0/*.glb` | 3DAssets.dev "Road Car Showroom Lineup" | **CC0 1.0** | 无需署名 |

**关于 CC BY 的完整许可凭证链**（不只是一句「网上说是 CC BY」）：

- 分发渠道：three.js 官方仓库 `examples/models/gltf/ferrari.glb`
- 示例页署名：https://github.com/mrdoob/three.js/blob/dev/examples/webgl_materials_car.html
- 官方许可清单：https://github.com/mrdoob/three.js/issues/23089 （记为 `CC-BY 4.0`）
- 许可全文：https://creativecommons.org/licenses/by/4.0/
- 完整性核对：本工程内 `ferrari_source.glb` 为 1,681,572 bytes，与官方仓库字节数一致

**署名在工程内的实际落点**：`Assets/Scripts/VehicleCatalog.cs` 的 `VehicleCatalog.Cars[0].credit`、主菜单底部、暂停菜单 `CHANGE CAR` 行、`-garagecheck` 报告。

### ⚠️ 商标风险（重要，请勿忽略）

**CC BY 4.0 只免除著作权，不免除商标权，也不免除外观设计权。**

- "Ferrari 458 Italia" 是**法拉利汽车 S.p.A. 的注册商标**；
- 该车型的车身造型受**外观设计权（registered design）**保护；
- 上面的 CC BY 授权只能证明：vicent091036 有权许可**他制作的这个模型文件**，不能证明他有权许可**法拉利的设计本身**。

**因此本仓库的使用边界是：个人非商业研究 / 学习用途。**
若要公开发布、商业化、或以任何形式对外分发含 Ferrari 车模的构建产物，**必须另行取得法拉利的商标与外观授权，或替换为无品牌车型**。

three.js 官方仓库本身以同样方式分发该模型并公开署名，是这一做法可公开援引的先例——但它不构成对个人使用者的授权。

---

## 3. 第三方素材 · 环境与贴图

全部来自 **Poly Haven**，许可 **CC0 1.0**（https://polyhaven.com/license），无需署名。

| 素材 | 入库文件 | 备注 |
|---|---|---|
| Venice Sunset 1K HDR | `Assets/Environment/VeniceSunset.hdr` | 天空、环境照明、车身反射 |
| Asphalt 02 2K PBR | `Assets/Textures/AsphaltColor.jpg`、`AsphaltNormal.jpg` | 路面 |
| Island Tree 01 | `Assets/Vegetation/IslandTree.fbx` | 原 1,599,403 面 → 实时 LOD 15,994 面 |
| Coast Land Rocks 02 | `Assets/Vegetation/CoastRocks.fbx` | 原 1,291,146 面 → 实时 LOD 25,821 面 |
| Shrub 01 | `Assets/Vegetation/Shrub.fbx` | 原 156,012 面 → 实时 LOD 12,480 面 |

> 原始高模下载（98 MB）不进版本库，用 `tools/fetch_assets.py` 重建；URL 与 md5 见 `tools/asset_manifest.json`。

---

## 4. 第三方素材 · 音频

| 文件 | 作者 / 来源 | 许可 | 署名义务 |
|---|---|---|---|
| `Assets/audio/Engine0.wav` … `Engine5.wav` | domasx2，[OpenGameArt](https://opengameart.org/content/racing-car-engine-sound-loops) | **CC0** | 无 |
| `Assets/audio/TireSkid.wav` | audible-edge / Tom Haigh，[OpenGameArt](https://opengameart.org/content/car-tire-squeal-skid-loop) | **CC BY 3.0** | **必须署名**：audio-edge, "Car tire squeal skid loop", CC BY 3.0 |
| `Assets/audio/BoostStart.ogg`、`BoostLoop.ogg`、`BoostEnd.ogg` | Iwan Gabovitch / qubodup，[OpenGameArt](https://opengameart.org/content/racing-speed-boost-sound) | **CC BY 3.0** | **必须署名**：Iwan Gabovitch / qubodup, "Racing Speed Boost Sound", CC BY 3.0 |
| `Assets/audio/OceanWaves.flac` | jasinski，[OpenGameArt](https://opengameart.org/content/beach-ocean-waves) | **CC0** | 无 |
| `Assets/audio/WindLoop.ogg` | [OpenGameArt](https://opengameart.org/content/wind-whoosh-loop) | **CC0** | 无 |
| `Assets/audio/Impact.wav` | Brian MacIntosh / BMacZero，[OpenGameArt](https://opengameart.org/node/118334) | **CC0** | 无 |

CC BY 3.0 全文：https://creativecommons.org/licenses/by/3.0/

---

## 5. 明确排除、不进本仓库的素材

| 被排除的 | 原因 |
|---|---|
| Forza Horizon / Asphalt 8 等商业游戏提取的车模 | 商业版权资产，无论是否仅供个人学习都不进入本工程；本工程只**借鉴玩法机制**，不复制其模型文件 |
| Sketchfab 上社区上传的 McLaren / 各类品牌车模 | 第三方自标 CC BY / CC BY-NC，**没有一个得到车厂授权**。CC 许可只覆盖文件著作权，**不覆盖商标与外观设计权**，上传者本人就无权许可。再分发风险显著高于本地自用，故不进版本库 |
| McLaren 官方授权车型 | 2026-11-16 调研结论：W Motors / McLaren 的游戏授权面向有预算的发行商；2026-07 与 2026-09 两个独立游戏刚因授权终止被移除车型。申请材料另存 `docs/licensing/`，**未取得授权前不得入包** |

---

## 6. 引用本仓库时的署名格式

如果你要引用、改编或再分发本仓库的**代码与原创内容**，请按 CC BY-NC 4.0 署名：

```
AURORA / Costa Brava — 原创街机赛车工程
by [作者]
基于 https://github.com/[账号]/[仓库名] 修改而来
许可：CC BY-NC 4.0（仅限非商业用途）
```

若你的产物中保留了 Ferrari 458 Italia 车型或 CC BY 音频，请**额外**保留上表中的对应署名行。

---

## 7. 一句话总结

| 类别 | 数量 | 状态 |
|---|---|---|
| CC0 / 公有领域 | 10 款高模车 + 1 款 BMW + 20 款归档低模车 + 5 个环境模型 + 4 个音频 | 自由使用，仅需保留本文件作为出处记录 |
| CC BY（需署名） | Ferrari 458 Italia（CC BY 4.0）、2 个音效（CC BY 3.0） | 署名落点已在工程内实现，**再分发时必须保留** |
| 需另行授权 | Ferrari 商标与外观设计权 | **个人非商业使用边界内可接受；对外发布需另行授权** |
