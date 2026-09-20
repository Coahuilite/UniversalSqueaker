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
| `UsVoicePackChecklistWidget` | 317 | 226 | (i) | 扁平行集：`Repeat` ＋ 模板（`input/checkbox` ＋ 绑定 label）＋ `input/text-field`（已发）做搜索 ＋ `chrome/banner` ＋ `state/empty`；角色化 banner 落 Tone/可见性声明 |
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
