# 授权申请材料包 · Vehicle / Brand Licensing Pack

本目录是给车厂 / 品牌授权方（McLaren、W Motors 等）用的**对外材料**，与仓库内其他文档定位不同：

- 这里的文件是**要发出去的**，所以全部用英文正文 + 中文批注；
- 所有 `[方括号]` 标记都是**发信前必须替换**的占位符，**不要带着占位符发出去**；
- 仓库里的 `docs/ASSET_LICENSES.md` 记录的是**已经拿到 / 已经有的**素材，这里记录的是**正在申请、尚未拿到**的。

---

## 为什么是两条线并行

2026-11-16 调研结论：

| | **McLaren** | **W Motors** |
|---|---|---|
| 归属 | McLaren Group（2024-12 被 CYVN Holdings 收购） | 独立私营，迪拜 |
| 品牌车 | P1、720S、765LT、Senna、F1、Artura、Speedtail、Solus GT、Elva | Lykan Hypersport、Fenyr SuperSport、Sabre、Ghiath |
| 授权部门 | 2025 新设 **McLaren Licensing**；消费品的全球授权代理是 **CAA Brand Management**（明确含 gaming 类别） | 公司本身（无中间代理） |
| 公开态度 | 2026 年**收紧并调价** | CEO 2014 年公开表态「我们愿意把车放进很多游戏」 |
| 近期先例 | 2026-07《Assoluto Racing》未续约被下架 F1/F1 LM/P1/720S；2026-09《Top Drives》因报价谈崩移除 McLaren 卡牌 | Lykan 已进《GT Racing 2》与《Drive Club》(PS4) |
| 我们的胜算 | **低**——两个中小独立开发��在最近 3 个月内刚被移除 | **中**——先例明确、决策链短、无中间商 |

**结论**：McLaren 品牌价值高但刚在涨价、独立开发���正在被挤出去；W Motors 自有品牌（Lykan / Fenyr / Sabre）没有 McLaren 的品牌包袱，决策链短、先例明确。**两封都发**：McLaren 那封成本只是一封邮件，试一下；真正可能落地的是 W Motors。

> ⚠️ 顺带提醒：`Lykan`（黎巴嫩 W Motors 造，2015《速度与激情 7》主角）与 `Sabre`（W Motors，限量 25 台）**都不是迈凯伦**。Sabre 是 W Motors 自有；**Speedtail 才是 McLaren Automotive 的**。谈的时候别说错车。

---

## 目录

| 文件 | 用途 |
|---|---|
| `01_PROJECT_BRIEF.md` | 项目简介（英文正文）。WIPO《Mastering the Game》与 CAA 都要求的标准材料：**游戏 / 平台与地区 / 发行窗口 / 请求车型 / 玩家如何接触 / 营销用途 / 开发排期**。附到邮件正文或作为 PDF 附件。 |
| `02_EMAIL_MCLAREN.md` | 发给 CAA Brand Management（抄送 McLaren Licensing / Partnerships）的邮件草稿。 |
| `03_EMAIL_WMOTORS.md` | 发给 W Motors 的邮件草稿。 |
| `04_ASSET_REQUEST_MATRIX.md` | 逐车需求表 + 需要对方提供什么资产（模型 / 贴图 / 规格 / 审批要求），用于第二轮沟通。 |

---

## 发信前必须填的占位符

| 占位符 | 说明 |
|---|---|
| `[STUDIO NAME]` | 工作室名。还没有的话，建议先起一个中性名字，**不要**在标题里用 "Asphalt" / "Crazy" 之类，容易被理解为蹭竞品。 |
| `[YOUR NAME]` / `[TITLE]` | 署名与头衔（独立开发者就写 `Founder / Solo Developer`）。 |
| `[CONTACT EMAIL]` | 用一个**能长期收到信**的邮箱（不要用临时邮箱）。 |
| `[RELEASE WINDOW]` | 目标发行窗口，例如 `Q4 2027`。别写太死，留谈判余地。 |
| `[STORE PAGE / BUILD LINK]` | 有试玩版本就放链接，没有就写 "private playable build available on request"。**这是加分的**——授权方最想看实机画面。 |
| `[BUDGET STATEMENT]` | 预算口径。参考区间：写清是自筹 / 是否有发行商 / 预算量级。**如实写**，别虚报，虚报会在后续尽调崩掉。 |

---

## 发送方式

邮件正文较长，建议**正文精简 + 附件放 brief**（授权方邮箱容易被长正文淹没）。草稿里已经按这个结构写好了。

本机可用 Agent Mail（`gsnj9411@agent.qq.com`）代发，但**发信是对外不可逆操作，需要你确认后我才执行**。你也可以自己复制到邮箱客户端发送。

---

## 并行提醒：授权周期以「月」计

行业惯例是**数周到数月**的审批 + 审批往返（WIPO 明确提示：没有统一时长，要问）。这期间车组不能停摆：

- **短期（本周）**：车组继续用 10 款 CC0 车 + CC0 BMW 跑通玩法，不依赖任何待授权资产；
- **中期**：并行推进 **AURORA SPHINX**（原创超跑）作为保底——万一授权谈崩，至少有一台原创旗舰车；
- **授权到手后**：把车型挂进 `VehicleCatalog.Cars`（单一数据源，加一行 + 一个 FBX 即可），并在 `docs/ASSET_LICENSES.md` 记录署名链与授权文号。
