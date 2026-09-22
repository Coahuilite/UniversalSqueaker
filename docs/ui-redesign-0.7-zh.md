# US UI 重设计（0.7 线 · DarkGold 三栏 + 文件驱动）

> 状态：设计冻结稿，未实施。基线 = FerriteLib `0.7.x` @ `bce1ba4c88e557ee91fc1fbe87391b5107a2ff32`
> （Api 0.7.0，含 Batch 1 与 P1-E 两批；初稿写在 `d82a3ca` 上，事实面已在 §0 逐行复核）。
> US pin `[0.7.0, 0.8.0)`。本文是设计规格，不是实现记录。

## 0. 本次 FL 更新带来的新事实（决定设计的关键项）

> **复核（2026-09-21，task-27 第 0 步）：载体 = FL HEAD `bce1ba4c88e557ee91fc1fbe87391b5107a2ff32`（Api 0.7.0，Release / 249 344 B / 无 PDB）。**
> 本节初稿写在 `d82a3ca` 上；`git -C ../ferritelib log --oneline d82a3ca..bce1ba4` = **14 个提交，其中代码提交 8 个**。
> 本节现在的口径是 **「原文主张 / 是否仍成立 / 依据」**，不再只列变化。**§0.2 与 §0.4 是两条改变结论（而不是措辞）的更新，必须连带重裁 §4.1 与 §5.4。**
> FL 仓内路径一律相对于 `../ferritelib/`。

### 0.1 第 0 版本节的 5 行，逐行复核

| 原文主张（逐字保留） | 现在是否仍成立 | 依据 |
|---|---|---|
| 放置词汇（Batch 1）：`AlignX/OffsetX/AlignY/OffsetY`；`origin + fraction*(parentSpan-selfSpan) + offset`；`Overlay` 是唯一双轴放置容器，flow 子元素只接受**交叉轴 + 像素微调**；模板根拒绝全部四个名字 | **成立，本轮未变** | `Kernel/UiPlacement.cs:138-153`（既非 placement 容器也非 flow → creation 期拒绝）、`:173-190`（flow 只收交叉轴）、`:236-241`（`AlignX="Stretch"` 与显式 `Width` 互斥）；属性名门 `Kernel/UiLayoutEngine.cs:110` 与 `:118`；模板容器表 `:107-111` 含四个名字 |
| 密度触达布局（CP-0，破坏性）：容器未声明 `Padding`/`Gap` 时回落 `UiTheme.Geometry`（**两套 palette 均为 6/6**）；显式属性仍优先，`Padding="0"` 保留旧结果 | **成立，但原文的「6/6」不完整** | `Kernel/UiLayoutEngine.ParsePadding:2557-2563`（未声明 → `theme.Geometry.Padding`）、`ReadGap:2594-2599`；`Padding="0"` 的逃生口写在 `:2546-2549`。基线 `UiGeometry.Default = (Padding 6, Spacing 4, Gap 6, RowHeight 28, Hairline 1)`（`Kernel/UiTheme.cs:65`）；**US 清单已把 `RowHeight` 覆盖为 24**（`Source/UniversalSqueaker/UI/Layout.Schema2.xml:9`），所以 US 的实际 page 密度是 `(6, 4, 6, 24, 1)`。可声明的 metric 只有 `Padding/Spacing/Gap/RowHeight/Hairline`（`Kernel/UiStyleDocument.cs:515-524`） |
| 语气词汇收紧（D6）：可写 `Tone` 只剩 `Neutral/Success/Warning/Danger`；`Active`/`Disabled` 成为状态（本 minor 内仍是重定向，0.8 起拒绝） | **成立** | `Kernel/Widgets/AtomVocabulary.ParseTone:267-`（只认四个 meaning；数值不被当作 tone；`Active`/`Disabled` 重定向到它一直表示的状态 + 一条去重 note） |
| 单一强调色（D7）：`UiTheme.HoverPoint` **删除**，改为只读派生 `UiTheme.AccentHover` | **成立** | `Kernel/UiTheme.cs:159-168`（每次读派生，`HoverLift = 0.3`，alpha 保持）；`Kernel/UiStyleDocument.cs:496` 与 `Kernel/UiStyleResolver.cs:246`（文档里写 `HoverPoint` 报未知 token，永不进 switch） |
| 仍缺（不在本线）：chrome 动作插槽、`input/text-field`、`input/mode-row` 的 `Description1..8` 仍不绘制、`container/tree` 行内无子控件、页面模型无文本对齐轴、**无宽度绑定（只有 `VisibleKey` 这类 bool）** | **2 条已不成立、1 条须改写、3 条仍成立** | 逐项见 §0.1.1 |

#### 0.1.1 「仍缺」那一行的逐项判定

| 项 | 判定 | 依据 / 说明 |
|---|---|---|
| chrome 动作插槽 | **仍缺（但已不影响本设计）** | `Kernel/UiWindowHost.cs` 未改，chrome 仍只有 Title/Subtitle/Close…；维护者裁定 (5)（2026-09-20）已改为「帮助按钮 = 固定页头」，故 §4.2 的落点不变 |
| `input/text-field` | **已不成立——已发** | `Kernel/Widgets/TextFieldWidget.cs`（提交 `d687032`）；US 已在用（`Layout.Schema2.xml:61`）。它是有身份的单行字段：草稿/焦点在元素上；标签集是 `Label/LabelKey`，`Placeholder` **故意不在**标签集里（B8 口径） |
| `input/mode-row` 的 `Description1..8` 仍不绘制 | **字面成立，但角色已变（这句须改写）** | 它仍不画（`Kernel/Widgets/InputModeRowWidget.cs:156-163`），但现在是**选项级 hover 帮助文本**：`DescriptionN` → `UiSession.ClaimHover`，由消费侧的帮助面板读出（`Kernel/Widgets/OptionHelp.cs:16`）。所以它不是死属性；B8 只剩「词表半」（移除 vs 绘制）待维护者 |
| `container/tree` 行内无子控件 | **仍成立** | B9 已裁本线不做；Route A（给 tree 可选按行模板）维护者倾向但明确不在本线 |
| 页面模型无文本对齐轴 | **仍成立** | `AlignX` 是 **placement**（摆整个元素），不是文本对齐轴（B10） |
| **无宽度绑定** | **已不成立——已发（B5 `WidthKey`）** | 见 §0.2。**§4.1 的结论因此需要重新裁定** |

### 0.2 ★ `WidthKey` ⇒ §4.1 的结论要重新裁定，不是重新措辞

**已发能力（提交 `3df7bf5`，P1-E batch 1）**：`WidthKey` 是 `VisibleKey` 的**数值兄弟**。

- 位置：`Kernel/UiLayoutEngine.cs:150`（属性名常量）、`:2200-2244`（`TryFixedWidth` / `TryBoundWidth`）；属性门：容器 `Kernel/UiHost.cs:909`、widget `:884`。
- 语义：**静态声明先赢**（与 `Visible/VisibleKey` 同一优先级形状）——写了可用 `Width` 时不读 `WidthKey`；`WidthKey` 只在没有可用 `Width` 时被问，经 float 值绑定回答。
- 失败形状：键缺失或绑到别的类型 → **fail-soft + 一条去重 appearance note**（不是每帧抛），静态答案原样保留。
- 重排：经 `RecordDeclaredKeys` 注册（`:455`），所以对该键的公告**只重排声明它的节点**。

**为什么这要重裁**：§4.1 当初放弃「右栏可调」的**唯一技术理由是「manifest 的 `Width` 是字面量，可调宽度在当前公开面不可表达」**。该理由已消失，而维护者裁定 (4) 的「两档 collapse / 320」正是在**没有它**的时候定的。所以现在要重裁的是**产品形态**：两档 vs 可调。

> **[2026-09-21 事实更正，U3 量价]** 下面 A 方案里那句「运行期改窗在 Verse 侧没有可靠的公开通路」**只对「声明式缝」成立，对「能不能改窗」是错的**：`UiWindowHost` 暴露 `windowRect`，而 US **已经在运行期改窗**——`UniversalSqueakerSettingsWindow.ApplyDrawerWidth()`（`:107-125`，由 `BeforeDraw` 在 `:162` 调用）在**抽屉状态边沿**上重写它：按 `SettingsWindowWidth` 算目标宽、以自身中心重定位、夹进屏幕，且只在状态变化那一帧动手（所以不与玩家拖动或每帧重排打架）。⇒ 本线**不需要「按最大宽度预留」**；缺的只是引擎的**声明式**改窗缝（`InitialSizePolicy` 只决定初始尺寸）。这条更正直接改变 (乙1) 两条候选的价签：**两者都不必为窗口预留付代价**，代价主战场转移到「谁拥有帮助列的宽度这个数字」。
> 
> **[同一次更正] §4.2 与本条的结论不受影响**（帮助按钮仍在固定页头），但 §4.1 里 (a)/(c) 两个选项按此作废，(b)（内容列被挤）变成唯一仍然存在的那一条 —— 而它正是 (乙1) 要解决的。
> 
**两条路及其真实代价**

- **A｜采纳 `WidthKey`，右栏拖拽可调。** 可表达面：`<Scroll Id="help-scroll" WidthKey="help-drawer-width" MinWidth="260" MaxWidth="480">` + host 侧 `BindValue<float>`；拖拽手柄的命中与几何属 `us/*` 自有契约（US 自绘）。
  **代价不在右栏，在窗口**：今天 `WindowChromeLayout.DrawerWidthDelta = HelpDrawerWidth + BodyRowGap` 是**编译期常量**，而窗口尺寸在 `Verse.Window` 构造时确定（引擎不提供「按内容改窗」的缝，本稿 §3 自己也这么写）。运行期宽度可调 ⇒ 三选一，且都是产品决策：
  (a) 窗口按**最大**宽度预留 → 抽屉关着时右侧留一条最多 ~480px 的空白；
  (b) 接受把内容列挤到下限；
  (c) 运行期改窗 → Verse 侧没有可靠的公开通路（这正是 FL-18 被裁「不会来」的那一类缝）。
  另需新 lane：宽绑定 → 声明宽；公告 → 重排；非 float → fail-soft + 一条 note；`MinWidth`/`MaxWidth` 夹取。
- **B｜维持两档（收起 / 320），把 `WidthKey` 记为「已具备、本线不采纳」。** 代价：长条目仍换行（`MinWidth="260"` 只是保底不是交互）。收益：窗口策略不变（常量成立）、零新增 lane、S3 的几何面小得多。

**本稿的记录**：`WidthKey` 只移除了「不可表达」这个**技术**理由，**没有增加「可调更好」的证据**；而 320 相对今天的 176 已经是 +82%，「读得下」这个需求大半被两档满足。

> **维护者裁定（2026-09-21）：走 B。** 帮助栏维持两档（收起 / 320），`WidthKey` 记为「**已具备、本线不采纳**」，任何可拖机制本轮都不做。
> **改判触发条件（写死）**：若 S3 之后的实机看下来**仍觉得帮助面板读不下**，则改走 A，且顺序固定为 **「先做窗口按最大宽度预留 → 看过那条空带 → 再把栏做成可拖」**，作为 S4 之后的独立切片。
> 于是 §4.1 的 `Width="320" + MinWidth="260"` 仍是本线形态；§4.1 里「不建议回到运行期换根/变体」那条**不变**。

> **维护者裁定（2026-09-21，第二次）：(乙1) = A**（"我同意你的提议 A，必要时可以和底栏的版本号上方公用空间"）。
> **形状（A 的代价被维护者的提示削掉）**：窄态帮助**不做包裹容器**，而是在 `body-row` 与 `footer-band` 之间加**一条独立的全宽带** `<Scroll Id="help-band" Fill="true" Padding="0" VisibleKey="help-open-narrow">`，里面是同一个 `us/help-panel`。⇒ **帧从"三条带"变成"三条带 + 一条条件带"，而不是变形**；`body-row` 自身形状不变（它 `Fill="true"`，条件带出现时自然让出高度）。
> **一个玩家意图、两个互斥呈现**：`help-open` 仍是**唯一**被开关写下的值（可写）；`help-open-wide` / `help-open-narrow` 是宿主派生的**只读** bool，判据是 `WindowChromeLayout.DrawerWidensTheWindow(screenW, screenH)`（纯函数：该屏幕放不放得下展开后的窗）。宽态元素由 `help-open` 改为 `help-open-wide`。
> **窄带的高度与滚动（lead 要求先说清）**：**不写高，用 `Fill="true"`** —— 主体行与窄带**平分剩余高度**，所以窄带**永远不会把页脚挤出窗口**，也不需要任何手写阈值；并且它是 `Scroll`，面板在自留高度内**内部滚动**。这一条是对 `UsHelpPanelWidget` 按**全目录最坏值**测带的直接回应：不设上界的天然高度会等于"最坏那条帮助"，那会把首选观感一起吃光。
> **fit 门与修复同批**：`WidthAndLanguageEvidenceSweep` 现在把 `reports.Count > 0` 记为 violation，并且**声明屏幕**——抽屉开的那一半案例被钉到"放得下它的最小屏幕"，另加一个 `1228`（1920 屏展开窗的页面宽）覆盖**宽态**。理由：736 宽的页面 + 1920 的屏幕是**产物做不出来的组合**，拿它当门会让门永远红。
> **诚实的限制**：A **不能先验保证** 736/开 达 fit=0；后备是给内容列真正的下限、或把窄带做矮/可滚动。且这一整刀**未构建/未验证**（构建冻结），变异证据 PENDING。

### 0.3 第二版新增事实：`d82a3ca` → `bce1ba4` 落地的 8 件词表

| # | 能力 | 提交 | 依据（FL 仓内） | 对本设计的作用 |
|---|---|---|---|---|
| 1 | **`WidthKey`（B5）** | `3df7bf5` | 见 §0.2 | 见 §0.2 —— 唯一需要**重裁结论**的一条 |
| 2 | **`SelectedKey`** | `3df7bf5` | `Kernel/Widgets/AtomVocabulary.cs:224-250` | bool 绑定把**元素**解析成 Active 处理（state 胜作者）；**故意不是 `ToneKey`**（回传 "Active" 会把这个本线已退役的名字重新授权）；解不出 → fail-soft + 一条 note。它是**元素级**属性（容器属性表里没有它），所以对 §5.1 里 nav 的「按卡 bool 做选中态」只是换了一种写法——nav 内部的卡片选中仍由该 composite 自绘 |
| 3 | **`WideHidden`** | `3df7bf5` | `Kernel/UiLayoutEngine.cs:2628-2638`；父级要求 `Kernel/UiHost.cs:1056-1057` | `NarrowHidden` 的**精确镜像**；父级无 `Breakpoint` 时 creation 期拒绝。⇒ §2「窄态：`NarrowHidden` 或堆叠（产品二选一）」现在**两侧都能声明**，不必再写两棵互斥子树 |
| 4 | **`chrome/banner` 拿到角色对** | `3df7bf5` | `Kernel/Widgets/ChromeBannerWidget.cs:37-48` | banner 进入 atom 的 `ToneAndEmphasis` 角色对；它读角色的**文字色**，默认 emphasis = `Muted`——这个默认值保证**无色 banner 的墨与从前完全一致**（词表增益、零视觉变化）。**这一条闭合 G5，见 §0.4** |
| 5 | **元素级 `HelpKey`** | `64af720` | `Kernel/UiLayoutEngine.cs:151`、`:610-627`；属性门 `Kernel/UiHost.cs:887-894` vs `:902-913` | 引擎在 widget **画之前**统一 claim（在 `EnterNode` 内，所以 claim 归属该元素）。**精确边界**：`HelpKey` 在 **widget 属性表**里，**容器表里没有** ⇒「只挂在 widget 上、永不挂 `Section` 容器」这条**仍然成立**，§5.4 的写法不用改。**它是 G1 的解药，见 §0.4** |
| 6 | **`input/mode-row` 的 `TitleKey1..8` + 每选项 hover help** | `64af720` / `fcc6a18` | `Kernel/Widgets/InputModeRowWidget.cs:14-27,56-73,228-238`；`Kernel/Widgets/OptionHelp.cs` | 选项标签走翻译缝（`TitleKeyN`），并且 `HoverHelpKey`（**可写 string 绑定**）把 hover 到的选项身份写出去。**G4 闭合**；`DescriptionN` 升格为选项的帮助文本 |
| 7 | **容器 `Tab`** | `ee387f1` | `Kernel/UiLayoutEngine.cs:107`、`:2655-2661`；属性门 `Kernel/UiHost.cs:904-909` | 容器可声明 `Tab`，与 widget 同一门控，**隐藏整棵子树**。US 清单已在用（`checklist-card`）；S3 的页头/三栏也可以用 workspace 门控 |
| 8 | **`UiOption`（FL-16）** | `bce1ba4` | `Kernel/UiOption.cs:17-30`；`Kernel/Widgets/DropdownWidget.cs:61-76,192-196` | `BindOptions<UiOption>` 给动态选项（display, value）；`BindOptions<string>` 语义不变（display == value）。另 **FL-23**（`26c8ac0`）：元素类型不匹配现在**先上报 fail-soft 通道、再照旧抛出**——「先探一种形态、失败再退另一种」的探针**必须走不报错的读** |

### 0.4 ★ 比 §0 更要紧：WAVE-1 的 **G1–G5 五个缺口全部已闭合**

§5.3/§5.4 是**当前**「迁移被挡住、lines deleted = 0」的结论来源，而它列出的五个缺口现在全部有实现：

| 缺口 | 原文结论 | 现状 | 依据 |
|---|---|---|---|
| **G1** 无声明式帮助/悬停钩子 | 「任何涉及帮助的迁移都静默丢覆盖，**这一条 gate 住整个 bucket (i)**」 | **闭合**：元素级 `HelpKey`，引擎统一 claim | `Kernel/UiLayoutEngine.cs:610-627`（提交 `64af720`） |
| **G2** 重复行无法上报 item key | 「layers 保持 composite，不迁移」的全部理由 | **闭合**：`input/button.PayloadKey` —— 命令收到**行自己的 key**，per-item 作用域；创建期契约随形态走（有 payload ⇒ `BindAction<string>`，无 ⇒ `BindCommand`） | `Kernel/Widgets/ButtonWidget.cs:37-41,60-71,118-147`（提交 `b3957dc`） |
| **G3** 无无外观命中区、无法按内容量高 | 同上 | **闭合，但有一条须照实记录的边界**：`Chrome="none"` 不画任何 surface、命中照常；`Height="Auto"` 的高度取**它自己 caption 的测量内容**（空 caption 回落 `RowHeight`）。**边界**：它是按**自身 caption** 量高，不是「按兄弟组合出的两行行高」；caption 仍以 `MiddleCenter` + `singleLine: true` 绘制 | `Kernel/Widgets/ButtonWidget.cs:84-99,169-174`（提交 `b3957dc`） |
| **G4** `input/mode-row` 标题是字面量、绕过翻译缝 | 本地化消费者用不了该 kind | **闭合**：`TitleKey1..8` + 每选项 hover help | `Kernel/Widgets/InputModeRowWidget.cs:56-73,228-238` |
| **G5** `chrome/banner` 没有 `Tone` | §5.4 保留状态带 composite 的**唯一依据**（「保留下来的 composite 就是 G5 的引证」） | **闭合**：banner 进入角色对，默认 `Muted` 保证零视觉变化 | `Kernel/Widgets/ChromeBannerWidget.cs:37-48` |

**这改变了什么**（不改 S3 的工作面，改 S4/Round-4 的结论与价签）：

- §5.3「(i) 的 15 个部件在布局上可表达，但 **0 个**可以在不丢帮助覆盖的前提下迁移」——**前置条件 G1 已解决**，这句话失效。
- §5.4「layers 保持 composite，它们的阻塞是 **G2（已证缺口，带锚点）**」——**该锚点已闭合**；layers 真正的剩余阻塞退回**层级行 × 行内组合**（即 Route A 那一件能力），而不是「行上报不了 key」。
- §5.4 保留状态带 composite 的理由（G5 引证）**已消失**；是否改迁 `chrome/banner` 成为一次**新的**产品选择，而不是「等 FL 补能力」。
- §5.3 的「给维护者/lead 的绕行选项 (a)/(b)/(c)」——**(a) 已经发生了**。
- **本文不改写 §5.3/§5.4 的行文**（那是结论，不是措辞）：就地加推翻横幅指向本节，重裁留给维护者/后续轮次。

### 0.5 仍未落地的（S3/S4 不得依赖）

- **chrome 动作插槽**：仍缺（维护者裁定 (5) 已放弃；帮助按钮走固定页头）。
- **`container/tree` 行内子控件**：仍缺（B9 本线不做）。
- **文本对齐轴（B10）**：仍缺——右对齐文本仍靠 placement 摆整个元素，或控件自带锚点。
- **B8 的「词表半」**：`Description1..8` 的绘制（移除 vs 绘制）仍是维护者的裁定项。
- **`input/mode-row` 的 `TitleN`/`ValueN` 配对**：`TitleN/DescriptionN` 没有对应 `ValueN` 时 creation 期拒绝（`InputModeRowWidget.cs:93-101`）。

## 1. 设计原则

1. **结构 = 容器 + 原子**；`us/*` 只保留“manifest 表达不了”的自有契约（量测/命中/几何）。
2. **外观 = 样式文档**：颜色/字体进 `<Scheme>`，间距进 `<Density>`；C# 只选 scheme，不再逐行选色。
3. **间距 = density token**：页面容器尽量不写死 `Padding`/`Gap`，由 `<Density>` 驱动；需要像素精确处才写显式值。
4. **定位 = placement**：`Overlay` 两轴放页头/页脚；flow 交叉轴做垂直居中；不再用等分 spacer。
5. **配色 = DarkGold + 单一强调色**：`AccentGold` 一个 token，悬停用 `AccentHover`；危险/成功走 `Tone`。
6. **文件驱动**：`Layout.Schema2.xml` + `Style.Schema1.xml` 作为 Mod 目录松散文件（内嵌资源作回退），
   `UiDocumentService` + `HostAttached` 接线；自动监听仅 Dev，生产默认关；破坏性文件保留 last-known-good。

## 2. 目标形态（三栏 + 固定页头）

```xml
<UiPage Schema="2" Source="coahuilite.universalsqueaker">
  <Column Id="page-root" Gap="8" Padding="12">
    <!-- [2026-09-21 更正，S3-2b 实测] 原稿这里是 <Overlay Id="header-band" Height="60"> + AlignX="Right"，
         两处都被证伪，原文保留在此以免后人以为它被凭空改掉：
         (1) Overlay 里不声明 AlignX 的子元素默认 Stretch（`UiPlacement.cs:13,77-84`）⇒ 标题会排到带子
             右缘、被后画的按钮压住；而词表里**没有 MaxWidth、没有「取剩余」装置**，`Width="Auto"` 对无
             label 集的 kind 无效，手写预留常数被 R14 禁止 ⇒ 那个字形在这套词表里**不可实现**。改用 Row。
         (2) `Height="60"` 会裁掉换行的 caption（与 footer「钉死 Height 反而被日志报 needs 33px/28px」
             同源的教训），所以删掉，改由内容量高。稳定性的职责移进 `us/page-title` 的 Measure：
             它按**五个工作区的最坏值**测带，页头才不会被页签切换推动（nav 卡片 bounds 有 lane 钉死）。
         注意 Row 的主轴已有主人 ⇒ `AlignX` 在 Row 子元素上会被创建期拒绝，开关只留 `AlignY="Middle"`；
         「右对齐」由「它是最后一个定宽子元素」自然得到，并仍被断言（右缘 == 带子内右缘）。 -->
    <Row Id="header-band" Padding="0">
      <Widget Id="page-title" Kind="us/page-title" />
      <Widget Id="help-toggle" Kind="input/button" TextKey="US.Help.Drawer.Toggle"
              ActionBind="toggle-help-drawer" Width="128" Height="26" AlignY="Middle" />
    </Row>

    <Row Id="body-row" Fill="true" Gap="12" Breakpoint="720" Narrow="Column">
      <Column Id="nav-column" Width="200" Fill="true">
        <Widget Id="nav" Kind="us/nav" />
      </Column>
      <Scroll Id="content-scroll" Fill="true" Gap="8">
        <!-- 卡片 = Section 容器；行 = 核心原子；列表 = Repeat + Template -->
      </Scroll>
      <Scroll Id="help-scroll" Width="320" MinWidth="260" Fill="true" VisibleKey="help-open">
        <Widget Id="help-panel" Kind="us/help-panel" />
      </Scroll>
    </Row>

    <!-- 页脚：Overlay 左侧状态 + 右侧构建标识（placement 取代 spacer）。
         [2026-09-21 更正，S3-3] `Height="26"` 未实现，且**不应**实现：它与 footer 当年那条教训同源
         （钉死 Height 会盖掉 wrap-aware 的 measure，实机日志报过 needs 33px / has 28px），所以带子与它的
         子元素都不写 Height，高度由内容量。S3-3 只立了**带子**（`Overlay Id="footer-band" Padding="0"`），
         里面仍是保留的 `us/footer` composite —— 把它原子化成下面两个 `text/wrapped` 是 **S4 的迁移**，
         不是这一刀的事。 -->
    <Overlay Id="footer-band" Padding="0">
      <Widget Id="footer" Kind="us/footer" />   <!-- S4: -> footer-status (AlignX=Left) + footer-build (AlignX=Right) -->
    </Overlay>
  </Column>
</UiPage>
```

细则：
- **`Breakpoint="720"` 对真实窗口是空条款（2026-09-21 实测并裁定）**：`Breakpoint` 比对的是**容器自己的内宽**（`UiLayoutEngine.cs:883` `width - padding.Left - padding.Right`，`IsNarrow` `:995-1010`），而 body-row 的内宽 = 窗宽 − 64。**真实窗宽地板是 800 ⇒ 内宽恒 ≥ 736 ⇒ 任何 ≤736 的断点对真实窗口零影响**（只对 480/320 两个人造探针生效）。所以**真正的窄屏保护不来自这个断点**，而来自下面的 (乙1)。
- **这一档不只是"窄"，它会真的溢出文本（S3-4b 实测，harness 度量）**：在 **736（最小真实窗的内宽） + 抽屉开**时中心列是 **168px**（滚动条预留后内容带 152px），`ui.text.overflow` 报 **7 条（EN）/ 2 条（ZH）**：`mode-row` 54/36、`global-volume` 54/18、`basic-tuning` 42.7/22、`timing` 108/90、`camera-indicator` 42.7/22（ZH：`global-volume` 36/18、`timing` 72/54）。**这条要进实机清单的 F-05 项**（"overflow 是否归零"），并且是 (乙1) 那个切片必须解决的对象，不是「窄一点而已」。
- **一个已知的玩家可见状态（裁定 (乙2) 接受，实机待看）**：`WindowChromeLayout.SettingsOpenWidth` 以屏幕宽度封顶（`:119`），所以在**逻辑屏宽 ≤ 800** 时抽屉展开无法把窗口撑宽：内宽 736 − nav 200 − 2×gap 12 − help 320 = **内容列 192px**（今天 176 的右栏是 376）。**1024 屏展开是 416px，已经好于今天**，只有 ≤800 落到 192。**注意「逻辑屏宽」是 UI 缩放后的值**：普通 1920 显示器在 UI scale ≳2.5 时就落到逻辑 768 ⇒ 同样 192px，这不是罕见硬件问题。
- **(乙1) 是 S3 之后立刻做的独立切片**（不挂在「等谁碰到 800 屏」上）：到那一步要**量完再选**两条候选并给价签——宿主算出的 `VisibleKey` 切第二呈现（叠放/覆层）**vs** 用 `WidthKey` 让宿主算出「付得起的帮助列宽」（后者要把 `DrawerWidthDelta` 从常量改成函数，它被 `UniversalSqueakerUiLogicTests/Program.cs` 钉死）。
- **页头固定**：`header-band` 是 `page-root` 的流内子元素，不参与 `content-scroll`，因此滚动时不动。
  这是当前 FL 词汇下“帮助按钮不随内容滚走”的**可实现解**；真正放进 shell chrome 需要 FL 新增插槽（见 §4.3）。
- **帮助抽屉**：`VisibleKey="help-open"` 保留 node 与 `ScrollPosition`（0.6 剪枝语义），收起时整列不占位。
- **窄态**：`body-row Breakpoint="720" Narrow="Column"`；窄态下抽屉走 `NarrowHidden` 或堆叠（产品二选一）。
- **放置的边界**：flow 子元素只接受交叉轴的像素微调；页头/页脚用 `Overlay` 才能左右定位且可用百分比。

## 3. 样式文档（配色与密度全部文件化）

```xml
<Styles Schema="1" Scheme="us-root" Density="regular">
  <Scheme Name="us-root">
    <Color Token="Base"          Value="#0f1116" />
    <Color Token="WorkspacePlane" Value="#0f1116" />
    <Color Token="Panel"         Value="#171a21" />
    <Color Token="SectionBand"   Value="#171a21" />
    <Color Token="Raised"        Value="#1f232c" />
    <Color Token="Hover"         Value="#1f232c" />
    <Color Token="Selected"      Value="#2a2312" />
    <Color Token="Border"        Value="#333a46" />
    <Color Token="BorderStrong"  Value="#3d4452" />
    <Color Token="Divider"       Value="#232833" />
    <Color Token="TextPrimary"   Value="#e6e9ee" />
    <Color Token="TextSecondary" Value="#98a1af" />
    <Color Token="TextDisabled"  Value="#98a1af" />
    <Color Token="AccentGold"    Value="#d19a38" />
    <Color Token="Danger"        Value="#3a1f1f" />
    <Font  Token="DefaultFont"   Value="Small" />
  </Scheme>
  <Scheme Name="us-attention"><Color Token="AccentGold" Value="#86e7d8" /></Scheme>
  <Density Name="regular">
    <Metric Token="Padding" Value="12" /><Metric Token="Gap" Value="8" />
    <Metric Token="Spacing" Value="4"  /><Metric Token="RowHeight" Value="24" />
    <Metric Token="Hairline" Value="1" />
  </Density>
  <Density Name="dense">
    <Metric Token="Padding" Value="8" /><Metric Token="Gap" Value="6" />
    <Metric Token="RowHeight" Value="20" />
  </Density>
</Styles>
```

> **页面节奏的落地方式：维护者裁定 (丁)（2026-09-21）——照 §2 的 XML 实现。** 页面容器用**显式** `Padding`/`Gap`（就是 §2 里 `<Column Id="page-root" Gap="8" Padding="12">` 那样），`<Density>` 的 token **保持 6/6/4/24/1 不动**。这样唯一的可见 delta 是页面边距本身的取值，**控件内缘零变化**，于是这一步不需要逐条重导原子的几何 lane。
> 与本文开头原则 3「页面容器尽量不写死」**存在张力，这是有意的**：`Padding` 一个 token 同时服务两种角色（页面节奏与控件内缘），把它整体抬到 12/8 会一次移动**每一个控件**的内缘。(甲)（全局 `12/8/4/24/1`）**没有被否掉，很可能是最终形态**，但它是 **S3 之后的独立切片**——「样式文档一处重调整套界面」是 S2 花力气做的事，必须在实机上专门看它把每个控件改成什么样。
> 下面是 §3 的**目标**样式文档（S2 的形态），不是 S3 的落地形态。

- `UsTheme.cs` 退化为 `UsTheme.Surface() => UiTheme.DarkGold`（+ US accent），**26 色表整体移入文档**；
  `HoverPoint` 已不存在，悬停步统一读 `theme.AccentHover`。
- 旧 `WindowChromeLayout`/`UsCardLayout` 的几何常量改由 `<Density>` 承载；窗口尺寸策略（开/合宽度、4:3）保留在 C#，
  因为它驱动 `Verse.Window`（引擎不提供“按内容改窗”的缝）。

## 4. 三项待决设计的技术结论

### 4.1 右栏宽度：`Auto` 不可用；可调需要 FL 的宽度绑定
- `Width="Auto"` 的定义是“该 **kind 注册的 label 集**的文本自然宽”（`UiLayoutEngine` → `UiWidgetRegistry.GetLabelAttributes`）。
  容器默认没有 label 集；给容器注册同名 kind 是 FL 自己（X-30）标注“**不作为正式补救**”的旁路。
  即使能给 label 集，帮助面板的宽度是产品参数，不该由某条字符串的像素宽决定。
- 本设计采用：**固定 `Width="320"` + `MinWidth="260"` 保底 + `VisibleKey` 开合**。
- 若必须“拖拽可调”：FL 目前**没有宽度/数值绑定**（只有 bool 的 `VisibleKey`），manifest 的 `Width` 是字面量，
  因此可调宽度在当前公开面**不可表达**。可行路径二选一：
  (a) 向 FL 提 `WidthKey`（bool 的 `VisibleKey` 的数值兄弟，把“已声明元素的可绑定宽度”补成对称能力）；
  (b) 接受两档（收起 / 320）作为产品形态。
  **不建议**回到“运行期换根/变体”的旧写法（那正是 0.6 剪枝之下已失效并被删除的机制）。

### 4.2 帮助按钮位置
- shell chrome 插槽仍不存在（本轮 FL 未改 `UiWindowHost`）；`DoWindowContents` 仍 sealed，chrome 只有
  Title/Subtitle/CloseText/TitleBarHeight/SidePadding/CloseButtonSize/AccentBarHeight。
- 因此落点为 **固定页头 `Overlay`**（§2）：不随内容滚动、可右对齐、可用 `input/button` + action 绑定。
- 若你坚持“在关闭按钮左侧的 chrome 带”：需要 FL 新增声明式不滚动区域/动作插槽（台账 FL-18），
  届时把 `help-toggle` 从 `header-band` 移到该区域即可，绑定与动作键不变。

### 4.3 文件驱动
- FL 侧零改动：`UiDocumentService.Add(source, embeddedFallbackXml)` / `Attach(host, layoutId, styleId)` /
  `Signal` / `Reload` / `Pump`（由 `UiHost.BeginFrame` 驱动）+ `UiReloadPolicy` + last-known-good。
- US 侧：两个 `UiDocumentSource`（Layout/Style 松散文件路径）+ 内嵌回退 + `HostAttached` 挂载 + 失败告警；
  自动监听默认 Dev。验收三条（改宽度/颜色/Breakpoint → 关窗重开可见且 DLL 未重编译；坏文件 → 旧页仍在 + 一条告警；同进程内）。

## 5. `us/*` kind 盘点（25 → 目标 ≤7）

| 处理 | 对象 | 依据 |
|---|---|---|
| **删除（原子/容器取代）** | `us/global-volume`、`us/camera-indicator`、`us/basic-tuning` 行、`us/race-layer`、`us/xenotype-layer`、`us/filter-bar`、`us/diagnostics`、`us/mode-row`、`us/camera-readout`、`us/footer`（拆成 `Overlay` + 原子）、`us/page-title` 的帮助开关部分 | 行=label+checkbox/slider/field/rule，卡片=`Section`，banner=`chrome/banner`，空态=`state/empty` |
| **保留（自有契约）** | `us/nav`（等高三卡 + 省略）、`us/help-panel`（hover 不变最坏 band）、`us/scope-tree`（mood 三因子同宽网格）、`us/preset-list`（两层复选框树；tree 无行内控件）、`us/timing`（单位换算 + 预留 caption band）、`us/attenuation-editor`（chart + 预设 + 窄态）、`us/page-title`（标题量测，去掉开关预留） | 各自拥有 manifest 表达不了的量测/命中/几何规则 |
| **~~被 FL 缺口挡住~~（2026-09-20 复核后重写）** | `us/voice-pack-checklist` 的搜索框：`input/text-field` **已发**（本轮载体）；`us/diagnostics` 分段行：`input/mode-row` **已存在**，只有 Description 的**绘制**（B8 词表半）仍待维护者；`us/preset-list`：FL 已裁定 tree 行内子控件**本线不做**（B9），故保留自有 kind。真正的剩余缺口见 §5.1 | 台账 FL-15/FL-17/FL-19；实测 2026-09-20 |

### 5.1 可行性盘点（2026-09-20 实测）：18 个手写部件有多少能被现有词表取代

**先修正一个已失效的前提。** 本文 §5 与 `MEMORY.md` 曾写「FL 只有 12 个核心 kind、没有 checkbox 原子」——**这是错的**，它把「US 布局粒度止步于卡片」这个结论的一部分建立在假前提上；按维护者的 (A)/(B) 规则，**那一部分属于 (A) US 自己的欠账，不是 FL 缺口**（不需要向 FL 提请求，也不需要抬 minor）。

实测（载体 `0.7.x`）：FL 发 **17 个 widget kind** —— `text/wrapped`、`container/tree`、`input/text-field`、`input/stepper-slider`、`input/slider`、`section/header`、`chrome/rule`、`Repeat`、`display/progress`、`input/number-field`、`chart/line`、`input/mode-row`、`state/empty`、`input/dropdown`、`chrome/banner`、`input/checkbox`、`input/button` —— 外加 11 个容器 kind。`input/checkbox`（0.5.0 / P3，带自动验证）、`Repeat` + `<Templates>`、`container/tree`、`state/empty`、`display/progress`、`input/text-field` **全部已发**。US 侧 `Source/UniversalSqueaker/UI/*.xml` 里 `Repeat` / `Templates` 的出现次数是 **0**。

**词表已经能做什么（逐条读 FL 源码的注册表）。** `Repeat` 的 `Items` 是 `IReadOnlyList<string>` 值绑定、`Template` 指向 `<Templates>` 中的子树；引擎按 item key 物化声明的子树（item-local 键 `<Items>.<key>.<declaredKey>`），行内命中照常路由到内层控件，`input/checkbox` 在模板里解析 item-local 键且未绑定时 fail-soft。`text/wrapped` 有 `Bind`（字符串值绑定）＋ `Text`/`TextKey`，`input/checkbox` 有 `Bind` ＋ `ActionBind` ＋ `Label`/`LabelKey`，所以**行内文本与状态都可以数据驱动**，不必为每行写 C#。

**边界同样实测（这就是 (ii) 的全部内容）。** `container/tree` 的 `Bind` 是 `IReadOnlyList<UiTreeRow>`（key / depth / text / expandable / expanded）：**一行只画一个 label band，属性表里没有 Template，不接受行内子控件**，只有一个 `ActionBind`（payload = 行 key）；`Repeat` 的属性表是 `Items` / `Template` / `Padding` / `Gap` / `Height` ＋ 可见性，**没有 level / indent / expansion**。因此「**层级行 ＋ 行内组合子树**」今天确实不可表达，而且不是给 tree 打个补丁能解决的——那是「控件不能组合自己的部件」这个部件能力缺口。

计数口径：**code** ＝ 非空且非 `//`、`///` 注释的行；**draw** ＝ `Draw*` 方法体行数（声明式化后消失的绘制/几何代码；模型、绑定与适配器仍是 C#，所以这不是整文件行数）。18 个部件合计 **3,027 code / 1,646 draw**。

| 部件 | code | draw | 桶 | 依据 / 代价（一句） |
|---|---:|---:|---|---|
| `UsScopeTreeWidget` | 678 | 405 | **(ii)** | 层级 scope 行（每行内容是循环按钮/下拉）＋ 4 条 mood 行；tree 无 template、Repeat 无 level，两者无法组合。附带第二个独立需求：mood 三因子**跨行等宽网格**（tree 与 Repeat 都不提供） |
| `UsPresetListWidget` | 255 | 145 | **(ii)** | preset → race → xenotype 的复选树，每行含 checkbox、preset 行含 Import 按钮：层级行需要组合子树 |
| `UsHelpPanelWidget` | 125 | 36 | **(iii)** | 内容是按**当前 hover claim 索引的目录**，band 按**全目录最坏值**测量以免 hover 时布局跳动；绑定文本量的是当前字符串，任何声明都表达不了这条规则 |
| `UsVoicePackChecklistWidget` | 322 | 226 | (i) | 扁平行集：`Repeat` ＋ 模板（`input/checkbox` ＋ 绑定 label）＋ `input/text-field`（已发）做搜索 ＋ `chrome/banner` ＋ `state/empty`；角色化 banner 落 Tone/可见性声明 |
| `UsFilterBarWidget` | 203 | 138 | (i) | 4 个互斥 chip ＝ `input/mode-row`（`Bind` 是 string）＋ 3 个嵌套 `input/dropdown`（`OptionsBind`） |
| `UsTimingWidget` | 176 | 117 | (i) | `input/slider` ＋ `input/number-field` ＋ 按钮；**代价**：今天「按最坏值预留 caption band」要退化成声明的 `Height`（词表没有 `MinHeight`），失去按内容计算那一条 |
| `UsBasicTuningWidget` | 172 | 95 | (i) | 6 行静态 label+checkbox；F6 的父子两行 = 子行 `VisibleKey` 直接读父行的 bool 绑定 |
| `UsRaceLayerWidget` | 153 | 53 | (i) | 扁平行集：`Repeat` ＋ 模板行（绑定 label ＋ `ActionBind` 回传 key） |
| `UsAttenuationEditorWidget` | 148 | 86 | (i) | `chart/line` **本身已拥有拖拽命中与几何**；两端锁定与 15..65→0..100 的域映射是 C# 业务/数据投影（声明式化不会删掉），预设是 `input/button` |
| `UsXenotypeLayerWidget` | 128 | 56 | (i) | 同 Race Layer，行集来自 `xenotype-domains` 绑定 |
| `UsPageTitleWidget` | 125 | 43 | (i) | `text/wrapped`（`TextKey` 或 `Bind`）＋ 每个 workspace 一个 `Tab` 门控标题；帮助开关按 §4.2 移到固定页头后不再属于它 |
| `UsDiagnosticsWidget` | 118 | 64 | (i) | 三选一 = `input/mode-row`；开关 = `input/checkbox`（Description 的绘制仍是 B8 词表半） |
| `UsNavWidget` | 106 | 54 | (i) | 静态 5 元素导航：`input/button` ＋ 绑定 label/subtitle ＋ 声明的共享卡高；**代价**：今天「取最高卡」的计算规则变成声明常量 |
| `UsFooterWidget` | 99 | 42 | (i) | `Overlay` ＋ 两个 `text/wrapped`（左/右由 `AlignX` 摆放），非交互 |
| `UsGlobalVolumeWidget` | 67 | 37 | (i) | `input/slider` ＋ `input/number-field`（百分比） |
| `UsModeRowWidget` | 62 | 18 | (i) | 核心 kind `input/mode-row` 已存在；本部件存在的原因只是「枚举↔字符串转换不过 Host 边界」，那是 C# 数据层 |
| `UsCameraIndicatorWidget` | 61 | 25 | (i) | 一行开关：`input/checkbox` ＋ `ActionBind` |
| `UsCameraReadoutWidget` | 34 | 6 | (i) | 一行右对齐只读文本：`Overlay` ＋ `AlignX=Right` ＋ `text/wrapped`（`Bind`） |

**合计。** (i) **15 个部件 / 1,969 code / 1,060 draw** —— 用今天已发的词表就能做，**不需要 FL 任何改动**；(ii) **2 个部件 / 933 code / 550 draw** —— 全部卡在同一个能力（层级行 ＋ 组合子树）；(iii) **1 个部件 / 125 code / 36 draw** —— 自有量测契约。

> **2026-09-20 实测修正（步骤 B 落地后）**：checklist 一件已实际迁移（§5.7）。它的 code 实测 **322 @46a580b**（原表 317 是更早一次测量，其间 step A 加了 TitleHidden 分支），迁移后 **154 code / 236 行**，widget 侧删 **230 行**，但 `Source/**` **净 +68 行**。所以 (i) 的 **1,969 code 是「可搬走的绘制代码上限」，不是「可删除的净行数」**；与「删除」直接对应的是 **1,060 draw** 那一列，而每件还要另付模板、per-item 投影、lane 与保留 composite 的代价。第一件付出的脚手架里约六成是一次性的（后续继承），因此「迁移 15 件 = 15 × 第一件」不成立；但第二件旗舰（race/xenotype 行集）的摩擦类别不同（G2/G3 整行 hover 命中），它的边际成本由交互形状决定，**Route A 不宜用一个数据点拍板**。

**这张表给维护者的价签**：(i) 是 US 自己的迁移工作量（0 FL 依赖，约占 18 部件 code 行的 65%、可删 draw 代码的 64%）；要价的缺口只服务 (ii) 的 **933 行**（两个部件），并且它的正确形状是「部件可组合自身部件」而不是「tree 支持行内控件」。是否值这个价由维护者定，本盘点不下结论。

### 5.2 采纳计划（bucket (i) 的 15 个部件）：每个采纳**验证哪个 FL 组件**、预期摩擦属于 (A) 还是 (B)

**本阶段的成功判据不是「US 把自己那块屏做完」，而是「FL 的组件被真实页面用过，摩擦被分类并回传」。** 维护者 2026-09-20 明说：把 FL 与 US 排在同一轮、允许钉 `FL 0.7.x` × `US 0.5.x`，就是为了**用 US 的实践验证 FL 的组件**。因此下表按「验证价值」排序，而不是按部件大小：`Repeat` + `<Templates>` 目前**消费侧证据为零**，FL 自己的晋升门要求「真实消费者被逼着用过」才算证明——bucket (i) 的采纳就是那份证据。

| 部件 | 验证的 FL 组件 | 预期摩擦 | 归类 | 建议轮次 |
|---|---|---|---|---|
| `UsVoicePackChecklistWidget` | `Repeat` + `<Templates>`（旗舰，零消费证据）、`input/checkbox`（item-local）、`input/text-field`、`state/empty`、`chrome/banner`、`chrome/rule` | ① 每个行字段都要在 item-local 命名空间里注册一个绑定访问器（今天部件直接读模型）；② `chrome/banner` **没有 Tone**、`state/empty` **不接受 `Bind`** —— 角色化 banner 与动态空态文本要退化成 `text/wrapped` + `chrome/rule`（丢掉这两个 kind 自带的 band 契约） | ① (A) 用法改动；② **(B) 候选**（待采纳时证实：`Tone` 与 `Bind` 是否该对这两个 kind 可用） | 1 |
| `UsRaceLayerWidget` / `UsXenotypeLayerWidget` | `Repeat` + `<Templates>` + `ActionBind`（payload = 行 key） | ① 同上 item 投影；② 每行「选中」高亮若要走 `Tone`/`Emphasis`，二者都是**字面量属性**（无 `ToneBind`）→ 只能用「每个 tone 一份模板 + item-local `VisibleKey`」复制模板 | ① (A)；② **(B) 候选**（per-item tone/emphasis 绑定；任何数据驱动列表都需要） | 1 |
| `UsBasicTuningWidget` | `input/checkbox`、`section/header`、`chrome/rule`、`text/wrapped`、`VisibleKey`（F6 的父子规则） | 几乎没有：父子两行是静态声明的，子行 `VisibleKey` 直接读父行 bool | (A) 纯用法 | 2 |
| `UsCameraIndicatorWidget` | `input/checkbox` + `ActionBind`（command/disabled 漏斗） | 无 | (A) | 2 |
| `UsGlobalVolumeWidget` | `input/slider`、`input/number-field`（已有 `Format`） | 0..1 ↔ 百分比是绑定侧的投影（C#，不删） | (A) | 2 |
| `UsModeRowWidget` | `input/mode-row`（`Bind` 为 string）**＋ `HoverHelpKey`**（本轮载体新增：hover 到的选项把它的帮助标识写入一个可写 string 绑定，未 hover 时空串） | 关闭的枚举 ↔ string 转换落在 Host 边界（今天正是为了不给边界引入字符串才自造 kind）。**价值上升**：US 的 help claim 通道（`UsKernelDraw.HelpHover`）可以在声明式模式下直接驱动，不必再自造行 | (A) | 3 |
| `UsDiagnosticsWidget` | `input/mode-row`、`input/checkbox` | 同上枚举↔string；`Description1..8` 的**绘制**仍是 B8 词表半（维护者裁定） | (A) ＋ 待裁定 | 3 |
| `UsFilterBarWidget` | `input/mode-row`（4 个互斥 chip）、`input/dropdown` + `OptionsBind` | 4 值过滤器是封闭枚举 → string；三个下拉的联动是 OptionsBind 数据（可声明） | (A) | 3 |
| `UsTimingWidget` | `input/slider`、`input/number-field`、`input/button`、`text/wrapped` | 「按最坏值预留 caption band」无处声明（**没有 `MinHeight`／预留带属性**）→ 要么声明固定 `Height`，要么接受跳动 | **(B) 候选**（预留带；采纳时用真实跳动证实） | 3 |
| `UsNavWidget` | `input/button`、`text/wrapped`、（`Tab` 或按卡 bool 做选中态） | 「取最高卡 + 预留一行 subtitle」从计算规则变成声明常量；若必须按内容取高，则同样缺预留带 | (A) ＋ (B) 候选同 Timing | 3 |
| `UsPageTitleWidget` | `text/wrapped`（`TextKey`/`Bind`）、`Tab` 门控 | 每个 workspace 一个 Tab 门控标题（或一个绑定标题）；同样缺预留带；帮助开关按 §4.2 移出 | (A) ＋ 同上 | 3 |
| `UsFooterWidget` | `Overlay` + `AlignX`（Batch 1 落位词表） | 无 —— 这是 Batch 1 落位词表在真实页面上的复现证据 | (A) | 3 |
| `UsCameraReadoutWidget` | `Overlay` + `AlignX=Right` + `text/wrapped`（`Bind`） | 若读数必须在**流式容器内**右对齐，则回到 B10（无文本对齐轴）；放在 `Overlay` 里则今天可做 | (A)，或 (B) 候选 = B10 | 3 |
| `UsAttenuationEditorWidget` | `chart/line`（今天经 US 自己的包装使用）、`input/button` | 两端锁定与 15..65→0..100 域映射是 C# 业务/数据（不删）；采纳验证的是**不套 US 包装**直接用 `chart/line` | (A) | 3 |

**建议顺序**：第 1 轮先做 `Repeat` 的两个旗舰（checklist、race/xenotype 行集）——它们产生 FL 最缺的消费侧证据，并且是唯一会触发上面两个 (B) 候选的采纳；第 2 轮做静态行集（basic-tuning、camera-indicator、global-volume）；第 3 轮做原子对换（mode-row、diagnostics、filter-bar、timing、nav、page-title、footer、camera-readout、attenuation-editor）。**每个采纳都要按 FL 的格式回报：验证了哪个组件、摩擦是什么、必须修的是 (A) 还是 (B)，并附失败敏感的 lane。**

**维护者倾向（记录，不在本轮执行）**：对 bucket (ii) 的缺口，维护者倾向 **Route A —— 给 `container/tree` 一个可选的按行模板**，但明确要求**先等 US 真正用过现有组件**再决定；本节的 (ii) 数字（2 个部件 / 933 code 行）就是那个决定的价格依据。本轮不实现、不给 FL 提交请求。
### 5.3 WAVE 1 采纳实测（2026-09-20）：迁移被三个**实测缺口**挡住，bucket (i) 必须修正

> **⚠ 已被 §0.4 推翻（2026-09-21 复核，载体 `bce1ba4`）：G1–G5 五个缺口全部已闭合，本节据此得出的结论需要重裁。**
> 本节**逐字保留**，因为「为何曾经如此」是证据的一部分：当时的三个缺口（G1 无声明式帮助/悬停钩子、G2 重复行无法上报自己的 item key、G3 无无外观命中区 / 无法按内容量高）**确实存在**，`Repeat` 的采纳**确实**被依赖面挡住，而且当时**还没有** `WidthKey`、元素级 `HelpKey`、`input/text-field`、容器的 `Tab`。重裁（bucket (i) 是否解锁、S4 的价签）由维护者/PM 进行，本文不改写本节结论。

**结论先说**：checklist 与 race/xenotype 行集的 `Repeat` + `<Templates>` 采纳**在动一行代码之前就撞墙**，而且是结构性的，不是排版细节。撞墙点不是 `Repeat` 本身——`Repeat` 的契约（`Items` = `IReadOnlyList<string>` 值绑定、`Template` 指向 `<Templates>`、item 局部绑定 `<Items>.<key>.<declaredKey>`、身份 `<declaredId>#<key>`）**实测可用**；挡住迁移的是它的**依赖面**：US 的悬停帮助、行交互与本地化。

**G1（B，通用）没有任何声明式的帮助/悬停钩子。** FL 全库只有一个帮助形状的属性：`input/mode-row` 的 `HoverHelpKey`（且它是**选项级**的，不是元素级）；`Section` 的 schema 只有 `Title`/`TitleKey`。US 的悬停帮助由 **`UsKernelDraw.HelpHover` 调用 43 处 / 22 个 UI 文件**声明（本会话口径：grep `HelpHover` over `Source/**`；lead 独立复测为 **46 处 / 23 个文件**，口径不同、结论不变），另加清单里 **12 处 `HelpKey=`** 区块声明（`Layout.Schema2.xml:33-44`，由 `UsSectionWidgetBase:109` 消费），并且**双向**钉在 46 项帮助目录上（`Program.cs:306`）。**18 个部件文件里每一个都至少带一条 claim。**推论：**把任何区块/行迁成声明式元素都会静默丢掉它的帮助覆盖**——双向测试会红，而删掉目录项等于发布一次帮助功能回退。通用性：任何带 inspect/帮助浮层的消费者都要把帮助身份挂到声明式控件上；与既有的 `Bind`/`ActionBind`/`LabelKey` 词表对称；FL 自己已经为 `input/mode-row` 单独发了 `HoverHelpKey`，那就是需求存在的证据。
**G2（B，通用）重复行无法上报自己的 item key。** `input/button` 触发的是**无 payload 的命令**（`ButtonWidget.cs:83`）；`input/checkbox` 只能写自己的 item 局部 bool；`container/tree` 能上报行 key，但**不吃模板**、一行只画一个 label band（无两行、无换行生长）。可用绕法（属 (A)）：**item 限定绑定键本身就是身份载体**（每项一个 bool setter / 每项一个命令键），所以 `Repeat` + checkbox/button 行能为「自己那一项」动作；但点击目标必须是自带外观的控件，「点在一整行空白处」不可表达。通用性：任何「非按钮行」的列表（时间线、文件列表、域列表）都要；与 `container/tree` 既有的「ActionBind 上报行 key」对称，缺的只是**模板行做不到同样的事**。
**G3（B，通用）没有无外观的命中区，也无法拉伸到内容测高的整行。** 所有可点击的核心 kind 都自绘外观；`input/button` 的高度是 `RowHeight` 或声明的 `Height`，所以「两行文本测高而成的行」之上铺一个整行命中区不可表达。通用性与对称性同 G2。
**G4（B，通用，本轮新发现）`input/mode-row` 的选项标题是字面量，绕过翻译缝。** schema 只有 `Title1..8`/`Description1..8`（没有 `TitleKeyN`），源码里 `DrawOption` 直接用 `option.Title`，**全文件没有一处 `Translate`**。所以**本地化消费者用不了这个 kind**：US 的 `us/mode-row` 四个选项在 EN/ZH 两套 Keyed 表里。与每个 kind 上的 `LabelKey`/`TextKey` 对称。
**G5（B，源码确认）`chrome/banner` 没有 `Tone`。** 它的 schema 是 `Bind/Text/TextKey/Height/Tab/Hidden`，没有 `ToneAndEmphasis`；US 的四条 banner 是**角色映射**的（冲突/目标缺失 = attention 青色带；dormant = 不可用 hatch），声明式表达不了角色。绕法：`text/wrapped Tone=…` + `chrome/rule` 组合，代价是丢掉 banner 自己的 band 契约。

**四个候选的实测判定（本轮，而不是假设）**：

| 候选 | 判定 | 依据 |
|---|---|---|
| `Tone`/`Emphasis` 只有字面量（无 `ToneBind`） | **CONFIRMED** | 三处都需要数据驱动的颜色：footer 的 save-status 颜色（`UsFooterWidget.cs:81-90`）、race 行的选中态、banner 角色；`AtomVocabulary` 只有 `ToneAttribute`/`EmphasisAttribute`，没有绑定形态 |
| `chrome/banner` 没有 `Tone` | **CONFIRMED（源码）** | schema 无 `ToneAndEmphasis`（`ChromeBannerWidget.Register`）；未写 lane，因为迁移未开始 |
| `state/empty` 不接受 `Bind` | **NOT PROVEN** | 它的两个句子（空域 / 空搜索）可以用两个 `VisibleKey` 门控的字面量元素表达，不构成阻塞 |
| 没有 `MinHeight`/预留带 | **NOT PROVEN** | 本轮没有任何迁移走到「按最坏值预留 band」那一步；不重复上一轮的假设 |

**§5.1 的修正（按维护者要求，接触现实后改表）**：bucket (i) 的 15 个部件在**布局**意义上确实可用现有词表表达，但**在 G1 未解决之前，没有一个可以无损迁移**——18 个部件全部带帮助 claim（22 个 UI 文件、43 处）。所以正确的表述是：**(i) = 15 个部件 / 1,969 code 行在布局上可表达，其中 0 个可以在不丢帮助覆盖的前提下迁移**；真正的前置条件是 G1，而不是「US 愿不愿意做」。这也解释了为什么 FL 的 `Repeat` 至今消费侧证据为零：**采纳被依赖面挡住，不是被 US 的意愿挡住**。

### 5.10 S4-3a 摩擦报告：`us/timing` 消解为声明式卡片（2026-09-22）

**结论先说**：13 -> **12**，263 行 widget 删除，卡片是 `Section` + `section/header` + 两个 `Row` +
`input/slider` / `input/number-field` / `text/wrapped` / `input/button`。新增绑定 6 个：
`interval-ticks`（float，`input/slider` 只收 float；业务侧仍是 int ticks）、`interval-seconds`（÷60 投影）、
`timing-interval-caption`（只读串）、`timing-multiplier-minus` / `timing-multiplier-plus`（`BindCommand`）。
`min-interval` int 绑定与它一起退役。

#### 5.10.1 摩擦①（采样串 vs 绘制串）是**被修掉**，不是被绕开
旧部件把区间说明的 band 按一个**手写最坏样本常量**（`"10.0 s"`）预留，因为 measure 与 draw 必须一致。
声明式词表**没有预留带属性**，所以说明变成只读字符串绑定：host 构造**唯一一份**句子
（`US.Tuning.MinInterval` + 实时秒值），atom 量的就是它自己画的那串 ⇒ measure == draw **由构造成立**，
样本常量可能漂移的路径消失。

#### 5.10.2 未被复制的部分：预留带本身
预留带的地板来自那个样本，而词表没有 `MinHeight`/预留带属性；要保留就只能声明固定 `Height` 并可能截断。
声明式走另一条分支：说明是取行内剩余宽度的 flex 兄弟（与 global-volume 卡相同）。**实测后果**：在中列最窄处
说明会多折一行，卡片在那儿更高（1024/736/480/320 EN：154.67 / 197.33 / 197.33 / 218.67px）——这是
【仍需实机】的可见差异，不是缺陷。

#### 5.10.3 一条新的实测原子事实：Row 子项的固定 `Width` 会把子项画到行外
抽屉的退化排布把中列压到低于声明宽度后，两个 number field 被画到行右缘之外，
`FrameGeometryLaneTests` 报了 5 处违规。把两个 field 都改成 flex 兄弟后（`Width`/`WidthKey` 都不声明）
在**每个宽度**都在行内。结论：**声明式行只有在其子项可 flex 时才是「按构造在父内」**。
（同轮附带实测：widget 声明了 `WidthKey` 而该 key 未注册时，引擎只记一条 `fit.style-fallback` 并保持未定宽。）

#### 5.10.4 S4-3b（`us/attenuation-editor`，12 -> 11）—— 设计冻结，未开工
按 task-5 描述：`Section` + `section/header` + `chart/line`（`Editable=true`、`EditablePoints=1,2`、
`Height=64`，**直接声明**，不在 C# 里手搓 `UiElementSpec`）+ 只读状态行 + 三个预设 `input/button`
（`SelectedKey` 恢复选中态）。已实测可直接使用的两条前设：`WideHidden`（B2(2)，`UiLayoutEngine.cs:2635-2640`）
已落地 ⇒ 窄态分支可以声明（`Breakpoint` + `NarrowHidden`/`WideHidden`）；`SelectedKey` 已按 item 作用域化
（载体 `e929fa11`）。`SettingsGeometryLaneTests` 的 `CompositeSections` 已在本片收窄为 `{diagnostics}`，
attenuation 离开 `Sections` 时要一并重裁。

#### 5.10.5 变异台账（本片）
| 变异 | 结果 |
|---|---|
| 把 seconds field 重新绑到未注册 key | 红：host 创建期报该元素路径缺绑定 |
| 把 slider 重新绑到未注册 key | 红：同上 |
| 给 multiplier field 声明固定 `Width=150` | **绿**（未测到几何断言）—— 诚实记录。**归属**：该性质**不是缺口**，而是**不在那条 lane 的范围内**——它由**帧 lane 的 containment** 覆盖：本片正是靠 `FrameGeometryLaneTests` 抓到 5 条 frame violation（§5.10.3），所以「固定宽度在 Row 被压时画到行外」是有 lane 的，只是那条 lane 不是 `DeclarativeTimingLaneTests` |
| 恢复 `us/timing` 的 manifest 行 + Registrar 行 | 红（kind 集 pin / 创建期 kind 解析）—— 由本片与 `SettingsGeometryLaneTests` 的负断言共同承载 |

#### 5.10.6 「预期红」的代价与记录更正（2026-09-22，lead 核验后补）
`a2e9547` 的提交信息写了 "harness ALL PASS + verify-local"，**实际没有跑 verify-local**（只跑了 kernel-host
harness）。lead 其后执行：**门 6 FAIL / EXIT 1**（doc-first：载荷由 `e929fa1` 构建、carrier checkout 在
`d1f2c50`），**门 6 之后的门全部 UNRUN**；载体只读复验未变。

**这条红藏住了一个真缺陷**：门 6 一停，后面的门都没跑，而 `UiSourceInvariantTests`（**UiLogicTests 项目**，
不是 kernel-host harness）里的注册集基数仍是 **13**。本批重裁为 **12**，并把两个步进按钮的说明从 manifest
字面量 `Text` 移到 Keyed 表（`US.Tuning.CooldownMultiplier.Minus`/`.Plus`）——同一个项目的本地化守卫
拒绝 shipped manifest 上的字面量 `Text`，所以这一改是隐藏缺陷的第二半。

**规则（本批建立）**：遇到预期红的门，**照跑**，但把**该门之后的一切显式标为 UNRUN，并单独把那几道跑掉**。
本批已单独跑：harness、KernelTests、ConfigCopyTests、SettingsMigrationTests、LogTests(Release/Dev)、
UiLogicTests —— 全部通过。

#### 5.10.7 玩家可见差异（全部【仍需实机】）
1. 说明文字仍带实时秒值，但所在 band **不再按最坏样本预留** ⇒ 最窄中列多折一行、卡片更高；
2. 说明从 `RowLeftPadding` 起排改为 **卡片内容左缘**（贴 12px 卡内边距）；
3. 秒数字段与倍率字段**不再固定宽**（flex），其横向位置随中列宽度变化，不再与分隔式控件列的右缘对齐；
4. 倍率步进簇由「按列右缘 - 10px 内缩」改为**终止在卡片内容右缘**（声明式行的右缘）；
5. 步进按钮的绘制从 composite 的 `SelectionButton` 改为核心 `input/button`（尺寸保持 26x20，外观 ink 未验证）；
6. `input/number-field` 在本页是**首次实机使用**（焦点环、提交手感）。
