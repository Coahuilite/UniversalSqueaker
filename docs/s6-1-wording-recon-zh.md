# S6-1 侦察素材：发声规则 / 频率 / 间隔 / 衰减 的文案对照

> **状态：侦察中，未改任何语言表。** 本文件是 S6-1「以 SR 用语为基准重写标签与注文」的**输入清单**，
> 不是成品译文。任何人不得据此直接改 `1.6/Languages/**`——基准（SR 源）未确认前只做对照。
>
> 来源标注：**US 侧全部来自本仓库当前提交**（`git rev-parse HEAD` 见提交信息）。
> **SR 侧本会话无截图/无 SR 源**（工作区里没有 SR 仓库，维护者的两张截图不在我的可读范围），
> 所以第 3 列**只有表格骨架**，需要维护者或 lead 把可见中文串贴进来/转述后再填。

## 0. 本会话能核到的事实

- US 的两套 Keyed 表：`1.6/Languages/English/Keyed/UniversalSqueaker.xml`（英文）与
  `1.6/Languages/ChineseSimplified/Keyed/UniversalSqueaker.xml`（中文）；两表键集完全相同（`UiSourceInvariantTests`
  的本地化契约门在跑）。
- 本片涉及的三个帮助区块（`UsHelpCatalog.cs`）：`us/basic-tuning`（播放行为）、`us/timing`（节奏）、
  `us/attenuation-editor`（距离衰减）。帮助正文与卡片标签是**两套键**（`US.Help.*` 与 `US.Tuning.*`），
  S6-1 要同时动这两层，否则会出现「标签改了、注文没改」。

## 1. 卡片标签层（`US.Tuning.*` / `US.Distance.*`）

| # | 键 | US 现译（EN） | US 现译（ZH） | SR 可见串 | 差异类型 |
|---|---|---|---|---|---|
| 1 | `US.Section.PlaybackBehaviour` | （见语言表） | （见语言表） | 待填 | 待判 |
| 2 | `US.Tuning.EasterEggs` | Easter egg sounds | 彩蛋音效 | 待填 | 待判 |
| 3 | `US.Tuning.EasterEggs.On` | On (eggs join the pool) | 开启：彩蛋加入音效池 | 待填 | 待判 |
| 4 | `US.Tuning.EasterEggs.Off` | Off (ordinary entries only) | 关闭：仅普通语音条目 | 待填 | 待判 |
| 5 | `US.Tuning.ScaleCooldown` | Scale cooldown with time speed | 按时间流速缩放冷却 | 待填 | 待判 |
| 6 | `US.Tuning.ScaleTalking` | Scale frequency with talking | 按交谈缩放频率 | 待填 | 待判 |
| 7 | `US.Tuning.ScalePopulation` | Scale periodic with audible population | 按可闻人口缩放周期 | 待填 | 待判 |
| 8 | `US.Tuning.EatPrecision` | Only squeak while actually eating | 仅在真正进食时发声 | 待填 | 待判 |
| 9 | `US.Tuning.EatPrecision.IncludeDrugs` | Taking drugs also counts as eating | 服用成瘾品也算作进食 | 待填 | 待判 |
| 10 | `US.Tuning.CameraIndicator` | Show camera indicator | 显示镜头指示器 | 待填 | 待判 |
| 11 | `US.Tuning.GlobalVolume` | Global volume: {0} | 全局音量：{0} | 待填 | 待判 |
| 12 | `US.Section.TriggerTiming` | （见语言表） | （见语言表） | 待填 | 待判 |
| 13 | `US.Tuning.MinInterval` | Minimum trigger interval: {0} | 最小触发间隔：{0} | 待填 | 待判 |
| 14 | `US.Tuning.CooldownMultiplier` | Global cooldown multiplier | 全局冷却倍率 | 待填 | 待判 |
| 15 | `US.Section.DistanceAttenuation` | （见语言表） | （见语言表） | 待填 | 待判 |
| 16 | `US.Distance.Status` | Attenuation {0}  {1} | 衰减 {0}  {1} | 待填 | 待判 |
| 17 | `US.Distance.Preset.Conservative` | Conservative | 保守 | 待填 | 待判 |
| 18 | `US.Distance.Preset.Balanced` | Balanced | 均衡 | 待填 | 待判 |
| 19 | `US.Distance.Preset.Strong` | Strong | 强效 | 待填 | 待判 |
| 20 | `US.Distance.Preset.Custom` | Custom | 自定义 | 待填 | 待判 |

## 2. 帮助注文层（`US.Help.*`）

| # | 键（Label / Text） | 区块 | US 现译（ZH）摘要 | SR 可见串 | 差异类型 |
|---|---|---|---|---|---|
| 21 | `US.Help.BasicTuning.Overview` | 播放行为 | 播放行为卡：彩蛋音效是否入池，加上三项运行时缩放…… | 待填 | 待判 |
| 22 | `US.Help.BasicTuning.Egg.Label` / `.Text` | 播放行为 | 彩蛋音效 / 开启后，彩蛋条目与普通条目一同参与抽取…… | 待填 | 待判 |
| 23 | `US.Help.BasicTuning.ScaleCooldown.Label` / `.Text` | 播放行为 | 时间流速缩放 / 开启后，冷却按游戏时间流速折算…… | 待填 | 待判 |
| 24 | `US.Help.BasicTuning.ScaleTalking.Label` / `.Text` | 播放行为 | 交谈缩放 / 开启后，声明了交谈门控的动作…… | 待填 | 待判 |
| 25 | `US.Help.BasicTuning.ScalePopulation.Label` / `.Text` | 播放行为 | 可闻人口缩放 / 开启后，周期语音的触发概率按可闻人口除算…… | 待填 | 待判 |
| 26 | `US.Help.BasicTuning.EatPrecision.Label` / `.Text` | 播放行为 | 进食判定 / 关闭时整段进食工作都算…… | 待填 | 待判 |
| 27 | `US.Help.BasicTuning.EatPrecision.IncludeDrugs.Label` / `.Text` | 播放行为 | 使用成瘾品 / 只在上方开关开启后出现…… | 待填 | 待判 |
| 28 | `US.Help.Timing.Overview` | 节奏 | 两项全局节奏控制：任意两次 squeak 之间必须等待的最小间隔…… | 待填 | 待判 |
| 29 | `US.Help.Timing.Interval.Label` / `.Text` | 节奏 | 最小间隔 / 两次 squeak 之间的硬性下限，以秒为单位…… | 待填 | 待判 |
| 30 | `US.Help.Timing.Multiplier.Label` / `.Text` | 节奏 | 冷却倍率 / 一次性缩放所有动作冷却与全局间隔…… | 待填 | 待判 |
| 31 | `US.Help.Attenuation.Overview` | 距离衰减 | 以镜头高度对应可闻距离：横轴为 15 至 65 的距离带…… | 待填 | 待判 |
| 32 | `US.Help.Attenuation.Chart.Label` / `.Text` | 距离衰减 | 衰减曲线 / 水平拖动两个控制点…… | 待填 | 待判 |
| 33 | `US.Help.Attenuation.Presets.Label` / `.Text` | 距离衰减 | 快速预设 / 三枚互斥按钮直接套用距离区间…… | 待填 | 待判 |
| 34 | `US.Help.Attenuation.Status.Label` / `.Text` | 距离衰减 | 预设 / 区间读数 / 曲线下方的只读状态行…… | 待填 | 待判 |

## 3. 本会话**读不到**的东西（必须先解决，否则 S6-1 无法开工）

1. **维护者的两张截图不在我的可读范围**（我没有图像输入，也拿不到那两张图）⇒ 第 3 列
   「SR 可见串」**一条也填不了**。需要 lead/维护者把图中可读到的中文串**逐条转述**（文字即可），
   或让我能读到 SR 仓库里的 `Languages/ChineseSimplified/Keyed/*.xml`。
2. **SR 的全量基准**（键名与全部串）同样不在本工作区 ⇒ 现在只能做「US 侧现状盘点 + 差异分类框架」。

## 4. 侦察阶段就已经看得出的结构性问题（与 SR 无关，属 (A)）

- **同一个概念有两套键**：卡片标签（`US.Tuning.*`）与帮助正文（`US.Help.*`）分开维护，
  「标签改了注文没改」是结构性风险；S6-1 的验收应当**成对**检查。
- **中英不一一对位的地方**：例如 `US.Tuning.EasterEggs.On/Off` 英文带括号解释、中文用「开启：……」，
  这类「英文是句子、中文是短语」的条目在改基准时最容易只改一边。
- **`US.Distance.Status` 只有一句模板**（`衰减 {0}  {1}`），预设名与区间由 host 拼装 ⇒ 若 SR 的说法
  不同，改的是**预设名键**与模板两处，不是一处。
- **`US.Tuning.CooldownMultiplier.Minus/Plus` 是我 S4-3b 新加的**（运算符），不属 SR 对照范围，
  列出以免误改。
