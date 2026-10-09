# US 主题/调色板契约（R12-US, 2026-09-28）

> 事实来源永远是代码与载体。本文是 **消费者侧**（UniversalSqueaker）的一页契约记录，用于回答三个问题：
> 调色板归谁、调色板改动允许动什么、旧写法迁到哪里。载体契约在
> `ferritelib/docs/development/0.7/05-api-contract.md` §R1/§R2 与 `ferritelib/docs/api-tiers.md`。
> 本文件中的所有结论均为 **静态阅读**（未编译、未运行）。

## 1. 所有权：载体只供 `Vanilla`，US 拥有完整基线

- 载体已删除 `UiTheme.DarkGold`（`ferritelib/Source/FerriteLib.UiKit/Kernel/UiTheme.cs`
  仅剩 `public static UiTheme Vanilla`）。US 因此不再能"继承一个暖金模板"。
- US 的基线声明全部在 `Source/UniversalSqueaker/UsTheme.cs` 的 `SchemeXml` 里，起点是
  `Configure(UiTheme.Vanilla)`。它显式声明 **23 个颜色令牌**（`UsTheme.ColorTokens`），包含：
  - `AccentGold = #d19a38`（系列身份金；值与已删除的载体模板逐通道一致，**没有移动**）；
  - 四条 per-surface 边 **按 US 自己共享令牌的值显式声明**：
    `BaseBorder`/`PanelBorder`/`RaisedBorder = Border (#575247)`、`HoverBorder = BorderStrong (#6b6459)`；
  - `DangerBorder = #c96057`（显式声明为值）；
  - 未被 S6-3 重染的 `Success = #1c3d26`。
- **四条边为什么"声明"而不是"留空让共享令牌回答"（R12-CORR 的裁定，选项 (i)）。**
  R12 之前 US 从 `UiTheme.DarkGold` 起步（四条边全为 null），然后应用 US 自己的 scheme 写入
  `Border = #575247` / `BorderStrong = #6b6459`——所以**当时真正画出来的**边就是这两个颜色，
  这就是"已提交的既有效外观"。两种写法今天画出的像素相同，差别在下一次编辑：
  - 留空会让四条边变成 `UiTheme.Vanilla` 逐令牌回退的偶然结果，而"不再继承"正是本次改造的目的；
  - 声明让"US 拥有完整基线"成为字面事实；lane 断言的是**声明值 == 自己的共享令牌**
    （不是冻结十六进制串），于是单独改动 `Border` 或单独改动某条边都会变红，必须显式对账。
  - 代价一并写明：只重染 `Border` 的扁平作用域不再能波及这四条边（页面级外观不变；扁平作用域本来
    就自己声明 `RaisedBorder` / `HoverBorder`）。
- **两个故意保持未声明的令牌，各自因为回退目标是不同语义**：
  `SelectedBorder` 的回退是**强调色**（`SelectedBorder ?? AccentGold`），且扁平作用域靠
  "把 `SelectedBorder` 设成等于 `Selected`" 表达"没有盒子"，声明它会消灭这条回退；
  `SuccessBorder` 的回退是 `BorderStrong`（结构令牌），保持未声明即是文档化契约。
- **字体不由调色板声明**：调色板是纯颜色，`UsTheme` 的 `DefaultFont` 仍来自载体基线 `Small`。
  这一条被 lane 断言，载体基线若移动会被看见而不是静默跟随。
- **缺口（当前为空）**：无。`DeclarativeOverviewLaneTests`/`UsSurfaceLaneTests`/`UsPaletteLaneTests`
  覆盖表中每一项；`UsPaletteLaneTests.ThePaletteIsCompleteAndPure` 逐令牌对照 `UiTheme.Vanilla`
  断言"没有任何 US 渲染的令牌仍在回答 Vanilla 值"。

## 2. 调色板只允许动颜色

载体的两个时钟（`ferritelib/.../UiTheme.cs`）：

| 时钟 | 写入者 | 影响 |
|---|---|---|
| `LayoutRevision` | `DefaultFont`、`Geometry` | 引擎带缓存失效 → 重新排布 |
| `ColourRevision` | 任何"值真的变了"的颜色赋值 | 仅区域主题克隆缓存失效 → 重绘 |

- 颜色赋值 **不** 动 `LayoutRevision`：`UsPaletteLaneTests.TheClocksAreDisjoint` 断言这一点，
  并断言字体/密度只动 `LayoutRevision`。
- 同值重复赋值两个时钟都不动（幂等）：同一 lane 断言。
- 区域克隆：`UiStyleResolver.ThemeFor` 在 **任一** 时钟移动时丢弃克隆，所以页面级重染会重建
  区域主题（旧色缺陷的修复），而作用域自己的覆盖仍然生效
  （`UsPaletteLaneTests.ARetintRepaintsScopedPaint`）。
- **两半证据是分开的，且不互相冒充**（R12-CORR 的 FIX 2）：
  - "调色板真的到达了绘制"：`TheRetintIsDrawnAndReusesTheArrangement` 用真实 host 画两次，
    比较 stub 的 `LabelColors` 记录（`DrawBoxSolidColors` 数量必须不变）；再重排一次，
    断言 rect 映射逐键相同、`ContentRevision` 不动、区域克隆带新调色板。
  - "调色板不是结构性事件"：`APaletteChangeLeavesSessionStateAlone` 只断言会话自身的簿记
    （节点身份、滚动位置、hover claim、活动节点、已打开 popup 的 id/anchor、`ContentRevision`），
    **并明确声明它不是"重绘后状态不丢"的证明**——那条属性属于载体自己的 resolver/popup lane，
    因为 US lane 无法把重绘瞄准到某个控件。
- **两条时钟的断言位置**：`TheClocksAreDisjoint`（含"同值重复赋值两钟都不动"、字体/密度只动
  `LayoutRevision`、以及"选择调色板不施加任何字体/密度默认值"）。

## 3. 缺色 / 显式透明 / 未声明边——三种不同答案

| 写法 | 含义 | 证据 |
|---|---|---|
| 文档不声明该令牌 | 回退到载体的 `Vanilla` 值 | `UiTheme` 的 per-token claim bit + `VanillaTemplate.Defaults` |
| `<Color Token='X' Value='#RRGGBB00' />` | **是一个值**：保持完全透明 | 显式赋值即 claim，不被 Vanilla 覆盖 |
| 未声明的 per-surface 边（`null`） | "没有覆盖"，落回共享令牌 | `EdgeOf(...) ?? 共享令牌`（`UiTheme.cs` 的 `XxxSurface` getter） |

US 侧的实际选择：四条边 **声明为值**（见 §1），`SelectedBorder` **保持未声明**（落回强调色）。
`UsPaletteLaneTests.MissingTransparentAndUnclaimedEdge` 断言三种答案互不塌缩。

## 4. 旧字体写法 → 后继写法（R12-T）

**US 不再维护自己的迁移表。** `Source/UniversalSqueaker/UsFontMigration.cs` 已删除：载体现在有一条
真正保留字体语义的旧写法重定向，消费者侧再放一张表就是"第二套约定"，而且可能与之矛盾。
`UsTheme.SchemeIssues` 只做一件事：把载体解析器自己的 issue 复制到一处供宿主/ lane 读取。

载体侧的迁移（`ferritelib/Source/FerriteLib.UiKit/Kernel/UiStyleDocument.cs`）：

| 旧写法 | 后继写法 | 说明 |
|---|---|---|
| `<Scheme>` 内的 `<Font Value="Tiny｜Small｜Medium" />` | 该 scheme 的字体 metric：`<Metric Token="Font" Value="…" />` | 仍可用；解析器记录一条 issue 指明旧写法与后继，并通过 appearance 通道报告重定向；**映射到字体，不映射到行高** |
| 旧写法与字体 metric 同时出现 | 以 **metric** 为准 | 遮蔽被记录，不静默 |

US 自己的文档没有使用任何旧写法（`SchemeXml` 无 `<Font>`/`<Metric>`，lane 断言），所以这段迁移
在 US 侧目前是 **零调用面**：这是"载体承接、消费者不复制"的正确状态，而不是待办。

## 5. 证据等级与未验证项

- (a) CURRENT INSPECTED：§1–§4 的每一个 `path:line` 结论，来自本轮对
  `UniversalSqueaker/Source/**`、`UniversalSqueaker/tools/**` 与只读 `ferritelib/Source/**` 的阅读。
- (b) DATED HISTORICAL：载体模板的旧值（`#d19a38` 等）来自已删除的 `UiTheme.DarkGold` 工厂，
  本轮以"载体源中已不存在 `DarkGold`"为交叉验证。
- (c) INFERENCE：lane 的运行时行为、以及"扁平作用域下 OFF 半高仍然可见"的组合结论。
- (d) UNVERIFIED RUNTIME：**本轮没有编译、没有运行任何 gate / harness / 游戏**。真机配色与观感
  仍需要一次人工检查；本 foundation slice 没有人类关卡。
