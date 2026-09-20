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
| **被 FL 缺口挡住** | `us/voice-pack-checklist` 搜索框 → 需 `input/text-field`；`us/diagnostics` 分段行 → 需 mode-row 的 Description 落地；`us/preset-list` 若 FL 给 tree 行内容器则可再删 | 台账 FL-15/FL-19/FL-17 |

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
