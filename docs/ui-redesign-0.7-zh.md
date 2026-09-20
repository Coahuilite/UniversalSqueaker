# US UI 重设计（0.7 线 · DarkGold 三栏 + 文件驱动）

> 状态：设计冻结稿，未实施。基线 = FerriteLib `0.7.x` @ `d82a3ca`（Api 0.7.0，含 Batch 1），
> US pin `[0.7.0, 0.8.0)`。本文是设计规格，不是实现记录。

## 0. 本次 FL 更新带来的新事实（决定设计的关键项）

| 变动 | 事实 | 对本设计的作用 |
|---|---|---|
| 放置词汇（Batch 1） | `AlignX/OffsetX/AlignY/OffsetY`；`origin + fraction*(parentSpan-selfSpan) + offset`；`Overlay` 是唯一双轴放置容器，flow 子元素只接受**交叉轴 + 像素微调**；模板根拒绝全部四个名字 | 页头/页脚的左盈右对齐、居中、悬浮徽标不再用 spacer 惯用法；**页头帮助按钮可右对齐** |
| 密度触达布局（CP-0，破坏性） | 容器未声明 `Padding`/`Gap` 时回落 `UiTheme.Geometry`（两套 palette 均为 6/6）；显式属性仍优先，`Padding="0"` 保留旧结果 | 页面间距改为 density token 驱动，不再写死 8/12 |
| 语气词汇收紧（D6） | 可写 `Tone` 只剩 `Neutral/Success/Warning/Danger`；`Active`/`Disabled` 成为状态（本 minor 内仍是重定向，0.8 起拒绝） | 不再有 `Tone="Active"` 写法；选中态由控件自身状态表达 |
| 单一强调色（D7） | `UiTheme.HoverPoint` **删除**，改为只读派生 `UiTheme.AccentHover`（= AccentGold 各通道向白抬 30%，alpha 保持）；样式文档写 `HoverPoint` 会报未知 token | 旧“一个 Hover token 同时服务行悬停与按钮悬停”的取舍消失；悬停步可用派生值 |
| 仍缺（不在本线） | chrome 动作插槽（`UiWindowHost.DoWindowContents` 仍 sealed）、`input/text-field`、`input/mode-row` 的 `Description1..8` 仍不绘制、`container/tree` 行内无子控件、页面模型无文本对齐轴、**无宽度绑定（只有 `VisibleKey` 这类 bool）** | 见 §3 的“必须保留的 kind”与 §4 的右栏结论 |

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
    <!-- 页头：不随内容滚动。Overlay = 双轴放置容器，帮助按钮右对齐 -->
    <Overlay Id="header-band" Height="60">
      <Widget Id="page-title" Kind="us/page-title" />
      <Widget Id="help-toggle" Kind="input/button" TextKey="US.Help.Drawer.Toggle"
              ActionBind="toggle-help-drawer" Width="128" Height="26"
              AlignX="Right" AlignY="Middle" />
    </Overlay>

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

    <!-- 页脚：Overlay 左侧状态 + 右侧构建标识（placement 取代 spacer） -->
    <Overlay Id="footer-band" Height="26">
      <Widget Id="footer-status" Kind="text/wrapped" Bind="save-status" AlignX="Left" />
      <Widget Id="footer-build"  Kind="text/wrapped" Bind="build-identity" AlignX="Right" TextKey="US.Footer.Build" />
    </Overlay>
  </Column>
</UiPage>
```

细则：
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

**给维护者/lead 的绕行选项（本轮不自行决定）**：(a) FL 增加一个**元素级帮助/悬停身份钩子**（例如 `HelpKey` 走一个消费者绑定，与 `VisibleKey` 对称）→ 一次解锁全部 15 个；(b) US 接受迁移区块的帮助回退（删除对应目录项与两套语言键，46 → 更少）；(c) US 只在**新的、无帮助覆盖的页面**上用 `Repeat`（本轮不产生证据）。

**本轮没有提交任何迁移（lines deleted = 0），这是有意的**：在 (a)/(b)/(c) 里选一条之前，任何迁移要么发布帮助回退，要么产出假证据。三个缺口的证据（FL 源码行 + US 的 43 处 claim + 46 项目录钉）已经写进本节，可供 FL 转录入 `MEMORY.md`。
### 5.4 维护者裁定（2026-09-20）：checklist 的**部分**迁移；layers 保持 composite

**裁定形态**：checklist 卡片改为声明式——`Section` + `section/header`（`HelpKey` 挂在 widget 上，**永不挂在 `Section` 容器上**）+ `input/text-field`（搜索）+ `Repeat` + `<Templates>`（`input/checkbox` 行）+ `state/empty`（两个空态，`VisibleKey` 门控）。**一次验证四个 FL 组件**：`Repeat`（今天消费侧证据为零）、`input/checkbox`、`input/text-field`、`state/empty`。

**角色化状态带保持 US 自有 composite kind，本轮不迁 `chrome/banner`。** 理由要写进证据、而不是当作妥协：**G5 是已记录、源码确认的缺口**（`ChromeBannerWidget.Register` 的 schema 没有 `ToneAndEmphasis`），对已记录缺口的诚实回应是**在 FL 提供能力之前保留消费侧 kind**，而不是为了让迁移看起来完整而发布一次玩家可见的颜色回退。**保留下来的 composite 就是 G5 的引证。**（选择：保留整条状态带 composite，而不是切出更小的 kind——代码更少、语义边界更清楚。）

**layers 保持 composite，不迁移。** 它们的阻塞是 **G2（已证缺口，带锚点）**：`Repeat` 的行无法上报 item key——`input/button` 触发的是无 payload 命令（`ferritelib .../Widgets/ButtonWidget.cs:83`），`input/checkbox` 只能写自己的 item 局部 bool，`container/tree` 不吃模板且一行只画一个 band（`.../Widgets/TreeWidget.cs:41-47`）。**已证缺口的正确处置是记录并作为后续轮次候选**，而不是为了让迁移完整而扭曲 UI（加一个点击目标、改交互语义）。

**被否决的两条路（记录理由）**：(i) 给行加 `input/button` = 玩家可见的外观回退（按钮表面盖住纯悬停行），架构目标不值得用它换；(ii) 把行改成 `input/checkbox` = **交互语义**变化（点选变勾选、点已选行由重选变取消选择），比外观变化更重。两条都不做。

**顺序**：迁移**不必**在本轮落地。若 freeze 时 checklist 迁移未完成且不干净，就只验证并提交已就绪的部分（S2 increment 1 + `IsHelpSelected` 的裸键等值修复），迁移带着自己的验证进入下一轮。**半迁移的清单不会靠近任何一次门禁运行。**

**本轮已就绪（未提交）**：S2 increment 1（`UsTheme` 的 16 色表改为样式文档 + `UiStyleResolver.ApplyTo`）；(A) 修复（`UsSectionWidgetBase.IsHelpSelected` 接受裸区块键）。
### 5.5 S2 第三条（清掉逐行选色）的实测结论：**它依赖迁移，不是迁移的前置**

**测量**：US 侧逐行读主题色的点共 **77 处 / 20 个文件**（`ctx.Theme.TextPrimary|TextSecondary|TextDisabled|TextOnGold|TextOnDanger|Danger|Selected|Hover|Raised|Panel`；最多的是 `UsDiagnosticsWidgets.cs` 18、`UsScopeTreeWidget.cs` 11、`UsVoicePackChecklistWidget.cs` 10、`UsPresetListWidget.cs` 6、`UsFooterWidget.cs` 5）。清单里今天声明的 `Tone=` 属性数量是 **0**。

**为什么这 77 处不是「顺手换成语义角色」就能清掉的**：它们绝大多数是**行状态**驱动的——选中行的墨、不可用行的墨、danger 状态的墨——而 `Tone`/`Emphasis` 是**元素级静态属性**：同一个元素的所有行共用一条角色，行与行之间的状态差异表达不了。这正是 §5.3 里已 CONFIRMED 的 `Tone`/`Emphasis` 只有字面量那条缺口的第二个后果。

**可用的角色缝确实存在**（对消费侧公开）：`UiResolvedStyleTable.Resolve(tone, emphasis, writable)`（`UiResolvedStyle.cs:107`）与 `UiStyleResolver.Resolve(theme, nearestFirst, writable)`（`UiStyleResolver.cs:141`）；`UiWidgetContext` 也公开 `Theme` 与 `StyleChain`。但**缝存在不等于能表达行状态**：它解析的是「这个元素是什么角色」，不是「这一行处于什么状态」。

**结论与改序**：S2 第三条**只在行已经声明式之后才可执行**——那时每一行的状态可以用「按状态一份模板 + item 局部 `VisibleKey`」或 per-item 的 Tone 变体表达；对**保留为 composite 的 kind**（例如 §5.4 保留的状态带）它**永远不可执行**，因为那条缺口没有被补。所以正确顺序是：**先迁移（§5.4），行变成声明式之后再做逐行选色清理**；把它当迁移的前置会得到一个无法验证的 77 处大扫除。维护者 2026-09-20 的裁定（「S2 第三条作为独立已验证增量」）因此**改序为迁移之后的增量**，这一改动记录在此。

**顺带记录（供 FL 侧计价）**：如果 FL 想要「消费侧自定义 kind 也能按角色取墨」，缺口不是「没有 Resolve」，而是**没有 per-row 的角色输入面**（元素级 Tone 无法表达行状态，而 per-item 的角色绑定不存在）。这与 §5.3 的 Tone 条目是同一条，不另开请求。
### 5.6 步骤 B 的两份证据规格（先写规格，再写代码；避免事后补叙事）

**B-1 「过滤后的 key 列表不说谎」lane 的设计说明**（lead 点名要保留的性质，未来每一个声明式列表都依赖它）：

- **要证明的性质**：`Items` 绑定给出的 key 列表，与同一帧里搜索框的文本**必然一致**——即「列表里出现的每一项都通过当前搜索谓词，且每一个通过谓词的项都出现在列表里」。两个方向都要断言：只测一个方向会漏掉另一半（多列 = 说谎；少列 = 静默丢数据）。
- **为什么必须有这条 lane**：投影与搜索框分属两个时钟——搜索写入 bump 内容修订，`Items` 由 host 投影重新计算。如果投影用了**上一帧**的搜索文本（或忘了在搜索变化时重新投影），屏幕上是**完全看不出来**的：列表画得整整齐齐，只是内容是旧的。这正是「看起来正确」的 bug 类别，也是为什么它值得自己的 lane 而不是搭在别处。
- **必须覆盖的失败模式**：① 搜索变化后 `Items` 未重算（陈旧列表）；② `Items` 里含被搜索拒绝的 key（多列）；③ 通过谓词的 key 缺失（少列）；④ 同一 key 在列表里出现两次（重复身份，`Repeat` 会拒绝重复 key，但那会让整行消失而不是报错）；⑤ key 与 per-item 绑定不同步（列表里有 key，但 `enabled`/`label` 等读不到——`input/checkbox` 对缺失的 item-local 键是 fail-soft 的，所以它会画成默认态而不报错，这条只能由 lane 抓）。
- **形状**：先断言基线（无搜索时 `Items` == 选中域的全部 pack key，数量与顺序都与源一致）；写入一个只匹配子集的搜索串；再断言两个方向 + 无重复 + 每个 key 的 per-item 绑定都能读出非默认值；最后清空搜索并断言回到基线（可逆性）。**顺序与数量都断言**，因为 `Repeat` 按 key 复用节点，顺序错误是状态错位的来源。

**B-2 G1 的记账方法**（迁移落地时逐条数出来，不估算）：

- 迁移前对每个 claim 站点记录：`path:line` + 该处的 key 表达式（字面量 / 计算式）。迁移后重新 grep `HelpHover(`，对**每一个被删除的站点**记一行「删除了哪个、由哪个元素的 `HelpKey` 接管」。
- 三个必须给出的数：① **删掉的站点数**（预期 30 个静态站点中的大多数，具体数以 grep 为准）；② **有没有站点其实无法用字面量 `HelpKey` 表达**（已预判的例外：`UsScopeTreeWidget.cs:388` 与 `:616` 两处按行状态取键，保持 imperative；`UsModeRowWidget.cs:64` 与 `UsKernelDraw.cs:499` 两处是**选项级**，由 `HoverHelpKey` 接管，不算站点删除）；③ **帮助目录或其 46 项 pin 是否需要改动**——预期**不需要**（键名不变，只是声明位置从 C# 移到 manifest），若需要改动必须写明改了什么、为什么。
- 记账的判定口径：**键名集合在迁移前后必须完全一致**（迁移是搬家，不是改名）。任何键名变化都要单独列出并说明，否则「帮助目录没动」这句话就没有意义。
### 5.7 步骤 B 摩擦报告（2026-09-20 实测：checklist 迁移已落地）

**提交**：`cdcc675`（B-1 过滤后的 key 投影 + 不说谎 lane）、`340526f`（B-2 声明式卡体 + `Tab="Packs"` 门控 + 删一 bool shim）。证据：harness **ALL PASS / EXIT 0**、`verify-local` **15/15 EXIT 0**（载体 `c898a6b3`，Release，无 PDB）；6 个变异各自红，且红在预期断言上（见末段）。

**行数（`git numstat` 实测，非估算）**

| 范围 | 增 | 删 |
|---|---:|---:|
| `UsVoicePackChecklistWidget.cs` | 35 | **230**（431 → 236 行） |
| `Source/**` 全量 | 311 | 243 → **净 +68** |
| `UsChecklistFilter.cs`（新） | 69 | 0 |
| host 侧 per-item 绑定命名空间（新） | ~80 | 0 |
| `Layout.Schema2.xml` | 36 | 5 |
| 工具侧（新 lane 409、几何 lane 重导、UI 逻辑门） | 488 | 17 |

按本盘点的 code 口径（非空且非注释行）：widget **322 → 154**（−168）。

**结论一：删除是真的，节省是假的（尚未摊销）。** bucket (i) 说「1,969 code 行今天可表达」——本件证明的是**可表达**，不是**可省**：把一张卡体的绘制搬进 manifest 要先付一次脚手架，所以第一件的 Source 净行数是 **+68**。真正消失的是 **draw** 那一列（本件约 226 draw 行的大部分）；模型/绑定/适配器照旧是 C#。

**结论二：成本两桶（lead 指定口径）**

- **一次性脚手架（后续 widget 继承）**：① 投影函数形状（一个谓词 + 一个有序 key 投影，两个消费者共用，`UsChecklistFilter`）；② **per-item 绑定命名空间的按需注册**（投影负责它刚列出的行的 item-local 键——注册表没有前缀解析，pack 集是运行期投影，所以「注册恰好引擎即将物化的那几行」是唯一诚实的形状）；③ 「不说谎」lane 的设计与骨架（283 code 中约七成可复用）；④ 卡高关系的多子元素重导形状；⑤ **两条 help 门的加宽**（manifest `HelpKey` 可以是 item 键，且算作 claim）——这一条一次性解锁后续所有 widget 的声明式帮助；⑥ `UsPacksText.Format(IUiTranslation,…)` 这类「host 需要翻译缝」的形状；⑦ `Tab`-on-container 一致性修复（FL 侧，免费）。
- **每件都要重付（widget 专属）**：① 模板的具体形状与随之而来的**视觉/交互 delta 取舍**；② per-item 键集与取值器（本件 label/meta/coverage/enabled）；③ 该件的 fixture 与 lane 断言；④ 状态可见性 bool（has-domain / 两种空态）；⑤ 任何 pin 了旧 composite 几何的 lane 都要重导（本件 1 条）；⑥ 保留 composite 的切分（哪些部分因 G5/G2/G3 留下）；⑦ 删旧 kind 的绘制代码 + 更新 kind 清单与源码不变式。

**结论三：widget #2 的代价——诚实答案是「管道那半已摊销，交互那半还不知道」，而后者才是决定项。**

- 可复用部分约占非 lane 新增的 60%，lane 骨架约七成。因此 #2 的**管道**边际成本是「模板 ~15 行 + per-item 键集 ~25 行 + fixture/lane ~60 行 + 状态绑定 ~10 行」。
- 但 §5.2 排在第 1 轮的第二个旗舰是 race/xenotype 行集，它的摩擦**类别不同**：那是一整行纯 hover 命中区（G2/G3），没有 `input/checkbox` 这种「行本身就是该语义」的原子可用。用 `input/button` 会发布按钮外观回退（§5.4 已否决），用 US 行 composite 则一行绘制代码都不删。所以 #2 的边际成本由**交互形状的决定**支配，不由管道支配。
- 对 Route A 的直接含义：**不要用一个数据点拍板**；同时可以确定，Route A 要买的不是「管道」——管道已被本轮证明是一次性的，要买的是「层级行 × 行内组合」这一件能力。

**结论四：G1 记账（5.6 口径，逐条）**

- **删除的站点：恰好 2 个**（`Source/**` 的 `HelpHover(` 代码行 35 → 33）。
  1. `Source/UniversalSqueaker/UI/Kernel/UsVoicePackChecklistWidget.cs:281`（迁移前）键字面量 `us/voice-pack-checklist/search` → 由 `Layout.Schema2.xml` 的 `checklist-search`（`input/text-field`）元素 `HelpKey` 接管。
  2. 同文件 `:335` 键字面量 `us/voice-pack-checklist/row` → 由模板的四个 widget 元素（`checklist-row-label`/`-meta`/`-coverage`/`-check`）`HelpKey` 接管。
- **保持 imperative、未删除**：同文件 `:261` `us/voice-pack-checklist/forget`（保留 composite 内的破坏性按钮）。
- **无法用字面量 `HelpKey` 表达的站点：本轮 0 个。** 既有例外清单不变：`UsScopeTreeWidget.cs:388`/`:616`（按行状态取键）保持 imperative；`UsModeRowWidget.cs:64` 与 `UsKernelDraw.cs:499` 是选项级，由 `HoverHelpKey` 接管，不算删除。
- **键名集合前后完全一致**：`{us/voice-pack-checklist, /search, /row, /forget}` → 同一集合（只是声明位置从 C# 移到 manifest）。**目录与 46 项 pin 无需改动**（gate 12 绿即为证）。
- 唯一「目录邻近」的改动是**门本身**：manifest `HelpKey` 校验从「必须是 section」放宽为「item 或 section 皆可」，claim↔item 双向钉把 manifest `HelpKey` 计入 claim 来源（合计 39 行）。改的是门的口径，不是目录内容；`PlaceholderKey` 同时加入被收集的 manifest key 属性表（搜索提示从 C# 移入 manifest 后必须仍被断言存在）。

**结论五：五条被点名的行为变化（写进提交信息，不是隐含）**
1. orphan 带从行列表**下方移到上方**（widget 元素是一个矩形，无法跨 manifest 兄弟节点）。
2. 行的点击目标从**整行**缩到 **checkbox 带**（G2/G3 的活标本：记录，不绕开）。
3. 行的 surface / hover / 选中轨消失——逐行状态选色正是 §5.5 排在迁移**之后**的条款。
4. meta/coverage 带改用原子的主题字号（Small），行略高。
5. 搜索占位符改用 `TextSecondary`（原子的既定选择）而非 `TextDisabled`。

**变异证据（每条都红，且红在预期断言上）**：M1 谓词多收一行 → `the list contains a key the search rejects`；M2 投影不跟随搜索写入 → `the probe query must narrow the list`；M3 去重移除 → `a blank key and a repeated key must collapse to one entry each`；M4 不注册 per-item `enabled` → `the projection must have registered 'checklist-pack-keys.us.alpha.enabled'`（**屏幕上看不出来**，fail-soft）；M5 卡片 `Tab` 改错 → `Packs workspace hidden by default`；M6 丢掉一个通过谓词的 key → 基线数量断言红。

**盘点修正（§5.1 表）**：`UsVoicePackChecklistWidget` 的 code 实测为 **322 @46a580b**（原表 317 是更早一次测量，其间 step A 给它加了 TitleHidden 分支）；draw 未复测（本轮不再宣称该列数字）。更重要的一句：**bucket (i) 的 1,969 code 是「可搬走的绘制代码上限」，不是「可删除的净行数」**——每件还要付模板、per-item 投影、lane 与保留 composite 的代价，第一件的 Source 净行数是 +68。真正与「删除」直接对应的是 **1,060 draw** 那一列。

## 6. 实施切片（0.5.x 线，短命分支）

| 切片 | 内容 | 门 |
|---|---|---|
| S1 | 载入 Release 载体的 0.7 载荷；pin 已抬 `[0.7.0, 0.8.0)`；绑定写入注册表 + `UiInvalidation` 分类 | 15 门 + 既有 lane |
| S2 | 样式文档落地（26 色 + 5 尺寸）+ `UsTheme` 退化 + 逐行选色清理 | token 相等 lane 改为断言文档值 |
| S3 | 新清单骨架（页头 `Overlay` + 三栏 + 页脚 `Overlay`）+ density 间距 | 五视口几何/无重叠/负宽 |
| S4 | 逐工作区原子化（Overview/Distance/Packs/Tuning/Presets） | 每区一套失败敏感 lane |
| S5 | 文件驱动接线（Layout/Style 松散文件 + 内嵌回退 + 失败保留） | 三条机械验收 + 坏文件 lane |
| S6 | Diagnostics 面板 + 详情窗 + overlay 同批重构 | 既有 mood/几何 lane 按新几何重导 |
| S7 | 清理门（kind 白名单、旧写法零命中）+ 实机矩阵 | 新扫描门 + 维护者实机 |

## 7. 风险

- 载体当前是 **Dev 配置**，US 门 6 要求 Release 载体（见 §8）。
- Batch 1 的 density 回落会让未声明 `Padding`/`Gap` 的容器从 0 变 6：新清单必须显式声明或用 density 语义。
- 页面模型无文本对齐轴 → 右对齐文本仍需控件自带锚点（`us/camera-readout` 类）或 `AlignX` 摆放整个元素。
- FL 未实机验收；本设计的几何主张仍需实机矩阵。

## 8. 当前阻塞

US 门 6 失败：`ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll` 是 **Dev** 配置
（`ProductVersion 0.7.0-dev+d82a3ca8db83`），US 构建/发布要求 Release 载体。
FL 侧命令（在其仓库内）：
```powershell
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental
```
之后 US 侧重跑 `scripts/verify-local.ps1 -NoRestore`。
