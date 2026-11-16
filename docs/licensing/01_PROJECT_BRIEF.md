# Project Brief — [STUDIO NAME]

**Prepared for:** [BRAND / LICENSOR NAME]
**Prepared by:** [YOUR NAME], [TITLE], [STUDIO NAME]
**Date:** 2026-11-16
**Contact:** [CONTACT EMAIL]

> 中文批注（发信前删除）：这份 brief 按 WIPO《Mastering the Game: Business and Legal Issues for Video Game Developers》与 CAA Brand Management / 车厂常见的 intake 要素组织——**游戏是什么 / 平台与地区 / 发行窗口 / 请求哪些车 / 玩家怎么接触到车 / 营销用途 / 开发与审批排期**。对方最在意三件事：**能不能给钱、有没有实机画面、会不会砸品牌**。请如实填写。

---

## 1. The game

**Working title:** [STUDIO NAME] — *Aurora Coast*
**Genre:** Arcade racing (closed-circuit time attack and short circuit races)
**One-line pitch:** A golden-hour arcade racer built around a garage of switchable, licence-ready cars — drift, nitro, an overtaking boost mode, three AI rivals and a two-lap race format, with no realism simulation layer.

The project is an **original title**. It takes its design cues from the arcade-racing genre (accessible handling, a visible garage, immediate restarts, readable HUD) rather than from any existing product's assets, code or content.

**Current production status:** playable vertical slice, built and tested daily.

| Item | Detail |
|---|---|
| Engine | Unity `6000.0.29f1`, HDRP `17.0.3` |
| Rendering | High Definition Render Pipeline, golden-hour lighting, PC target |
| Physics | Purpose-built arcade car controller (shared rigidbody + 4 wheel colliders across all car bodies) |
| Platform | Windows x64 (PC) |
| Current content | 12 switchable car bodies, 1 circuit (**COSTA NOVA** — 2,607.4 m, 12 checkpoints, min radius 18.1 m, max gradient 14.7%), 3 AI opponents, drift, nitro + overdrive mode, 2-lap race |
| Performance | 59.9 FPS @ 1280×720 on integrated graphics (Intel Iris Xe, D3D11) |
| Build size | 224 MB |
| Language | English + Simplified Chinese |
| [STORE PAGE / BUILD LINK] | [PRIVATE PLAYABLE BUILD AVAILABLE ON REQUEST / URL] |

---

## 2. Where the cars appear

The car the player selects is the same car they race, drive, and see in the garage turntable before a race. There is no separate "showroom model" and no separate "race model" — this matters to licensors, because it means the marketing imagery and the in-game product are the same depiction.

| Surface | Licensed cars used? | Notes |
|---|---|---|
| In-race (player car + 3 AI) | **Yes — primary use** | Car is driveable, damage-free, arcade-styled; no real-world brand claims about performance or specifications beyond what the licensor supplies. |
| Garage / car-select menu | Yes | Rotating turntable, rear 3/4 angle, in-race lighting. |
| Store page / key art | Yes — requested | Only if the licence covers promotional use; otherwise we substitute an unlicensed car. |
| Trailers / social media | Yes — requested | Same condition as above. |
| Press kit / screenshots | Yes — requested | Same condition as above. |

**Marketing honesty commitment:** we will not imply that the vehicle's real-world performance, homologation, price or availability relates to the game. Any supplied specification data will be used only as reference for the modelled car, and we will not advertise the game using performance claims about the real vehicle unless the licence explicitly permits it.

---

## 3. Cars we would like to license

Priority order, with the reason for each:

| Priority | Vehicle | Why this vehicle | What we need it for |
|---|---|---|---|
| 1 | [PRIMARY MODEL] | [e.g. the brand's signature silhouette; the car most players will recognise, which maximises the marketing value of the partnership] | In-race + garage |
| 2 | [SECONDARY MODEL] | [e.g. a contrasting character — a modern hypercar next to a heritage car broadens the garage's appeal] | In-race + garage |
| 3 | [TERTIARY MODEL] | [e.g. an entry-level / heritage model that suits the street-legal half of the lineup] | In-race + garage |

We would also welcome a **single-car agreement** if that is easier on your side, and are happy to begin with one vehicle and expand once the relationship is established.

**Technical specifications we work to** (so your asset pipeline can deliver first time):

- Car length normalised to **4.53 m**; ground plane at `y = 0`; nose toward `-Z` in the FBX.
- **Four independent wheel pivots** named `wheel_fl` / `wheel_fr` / `wheel_rl` / `wheel_rr`, so visual wheels can be driven directly by the physics wheel colliders.
- A GLB or FBX with **separated material slots** is strongly preferred over a single baked texture — we assign our own paint, glass, tyre, trim, alloy and lamp materials at build time.
- Optional but welcome: a separate **bonnet / door** node so the car can be shown with panels open in the garage.
- Triangle budget: **30k–60k per car** is our working range. Higher is welcome; we will handle LOD.

---

## 4. Commercial position

| | |
|---|---|
| Development | Self-funded, independent studio ([SOLO / SMALL TEAM]) |
| Publisher | [SELF-PUBLISHED / PUBLISHER NAME / IN DISCUSSION] |
| Expected revenue | [E.G. "LOW-MID 7-DIGIT LIFETIME, PROJECTS TO A SIX-FIGURE FIRST YEAR" / OTHER] |
| Marketing budget | [AMOUNT OR RANGE] |
| Territory | [WORLDWIDE / GLOBAL EXCLUDING …] |
| Platforms | Windows PC first; [CONSOLE OR MOBILE CONSIDERED IN A LATER PHASE] |
| Release window | [RELEASE WINDOW] |
| Exclusivity | We are not asking for exclusivity and are not able to offer it. We understand you may license the same vehicles to others. |

We are asking for a **non-exclusive** licence and are prepared to discuss an approval process for creative use, a term length that matches our development and release window, and appropriate attribution and trademark-guideline compliance.

---

## 5. Timeline and approval needs

| Milestone | Timing |
|---|---|
| Licence request sent | 2026-11 |
| Evaluation and terms discussion | [2026-12 → 2027-01] |
| Asset delivery | [2027-Q1] |
| Feature-complete alpha | [2027-Q2] |
| Public release | [RELEASE WINDOW] |

**We understand that artwork review adds time, and we are happy to build that into our schedule.** We will not show a licensed vehicle in any public material before written approval of the depiction. If a specific livery, race number, sponsor placement or camera angle would not be acceptable, please tell us and we will work around it — our garage art is entirely under our control.

---

## 6. What we will provide on request

- Gameplay capture at 1280×720 and 1920×1080, any car in the garage.
- A short written breakdown of how each licensed car is depicted (paint, stance, lighting, camera).
- Our in-game garage turntable and race cameras, if you would like to review the depiction before delivery.
- A single named point of contact for all artwork approvals.

---

## 7. Signature

```
[YOUR NAME]
[TITLE], [STUDIO NAME]
[CONTACT EMAIL]
[PHONE / TIMEZONE]
[STUDIO COUNTRY / REGISTRATION]
```

---

### 中文速记（发信前删除）

- 「我们是原创项目」这一条必须保留 —— 授权方最怕的是「我们做了个某游戏的克隆版还来要授权」。
- 别说 Asphalt 8 / 狂野飙车。不提，就不会有人拿这个来卡你；提了反而把项目定义成竞品衍生。
- 引擎、赛道、玩法、帧率、销量区间，全部按实填写。技术规格那一节看着枯燥，但能让对方的美术团队直接开工，少一轮邮件往返。
- **重点是第 2 节**：明确说明「展示车 = 赛车 = 营销素材是同一台车」，这是授权方评估舆情风险时最关心的一句话。
- 预算别虚报。虚报在尽调阶段崩掉，比一开始就写得保守更伤。
