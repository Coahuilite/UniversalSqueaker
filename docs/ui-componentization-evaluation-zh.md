# US 最小 UI 组件化评估（VoicePacks 页）

> 状态：评估文档（批次 1-B 产出）。结论：**方案 A —— 响应式 view-model + 声明式即时组件**。
> 适用范围：TODO Phase 3（HANDOFF 方案 A 的最小 UI：设置壳 + 三模式卡 + Race 层 + VoicePack 勾选域）。
> 证据来源：SR 参考实现（`Source/SqueakyRatkin/`，只读）与本仓 HANDOFF.md。

## 1. 约束澄清：RimWorld 1.6 mod UI 只有「即时模式」一条路

### 1.1 事实

- RimWorld 的 mod UI 运行在 **Unity 旧式 IMGUI（即时模式）** 上：没有持久控件树、没有节点生命周期，每帧 `OnGUI` 由调用者把状态 + 矩形再画一遍。SR 代码即证据：
  - `GUI.BeginGroup(...)/EndGroup`：`UI/SqueakyRatkinSettings.XenotypeUI.cs:102-104, 254-260, 268-274`
  - `Widgets.BeginScrollView(...)/EndScrollView` + `Listing_Standard`：同文件 `661-662, 686, 829-834`
  - 逐帧命中测试 `Mouse.IsOver(rect)` + `Widgets.ButtonInvisible`：`UI/SqueakySettingsUI.cs:42, 69, 76-77, 115`
  - 每帧文本测量 `Text.CalcHeight/CalcSize`：`XenotypeUI.cs:163, 718, 738, 820; SqueakyRatkinSettings.cs:599, 665`
- **Unity uGUI（Canvas/GameObject）与 Unity UI Toolkit 没有 RimWorld mod 的公开接入路径**：Verse 的 `Window`/`ModSettings`/`Widgets` 体系是唯一受支持面（外部佐证：RimWorld Wiki [ModSettings 教程](https://rimworldwiki.com/wiki/Modding_Tutorials/ModSettings) 只描述 OnGUI 式的 `DoSettingsWindowContents`）。因此“现代 UI 编排”在 RimWorld mod 中只能翻译为：
  1. **即时组件组合**：小函数/小类，每帧 `Draw(rect, ref state)`；
  2. **声明式 builder/布局**：先 Measure/Arrange，后 Draw；
  3. **响应式 view-model**：状态在模型层，UI 只读 + 发命令；
  4. **确定高度测量**：滚动容器必须先算内容高度；
  5. **Verse Widgets 原语**：`Widgets.Label/DrawBoxSolid/ButtonInvisible/BeginScrollView` 等。

### 1.2 对“更进一步组件化”的可行性结论

| 诉求 | RimWorld 内可行落点 |
|---|---|
| 组件化 | ✅ 即时组件：无状态 `Draw(Rect, state, emit)` 函数族 |
| 现代编排 | ✅ 单向数据流：Model → ViewState → Components → Commands → Model |
| 响应式 | ✅ 保留 SR 已验证的“写入桥 → resolver 重建 + 缓存失效”机制 |
| UI Toolkit/uGUI | ❌ 不可用；不做 diff 树、不做节点生命周期 |

## 2. 原项目已有的响应式实践盘点（证据清单）

SR 0.3.x 已具备“写入桥 → 立即生效 + 缓存失效”的雏形，组件化不是从零开始：

| 机制 | 证据（文件:行） | 作用 |
|---|---|---|
| 选择写入桥 | `SqueakyRatkinSettings.cs:239-255` `SetVoicePackSelection` | 同域 last-wins、去重、写 record → `NotifyDiscreteResolverRuntimeChanged()` + `QueuePersistence()` |
| 离散重建立即生效 | `SqueakRuntimeResolver.cs:61-71` `NotifyDiscreteResolverChange` | `FlushPendingRuntimeChanges(true)`，**返回前**新快照已发布 |
| 连续输入节流 | `SqueakRuntimeResolver.cs:47-59` | 尾随 75ms、上限 150ms（滑块类编辑） |
| 快照发布与降级 | `SqueakRuntimeResolver.cs:98-109` `TryPublish` | 构建不可变 `SqueakRuntimeSnapshot`；异常 → 日志 + fallback，绝不崩 UI |
| 模式卡写入 | `XenotypeUI.cs:66-74` `CommitVoicePackMode` | 改 `voicePackMode` → resolver 重建 + 持久化 + 行缓存失效 |
| 勾选行写入 | `XenotypeUI.cs:995-1013` `DrawVoicePackRow` | toggle 变化 → 组装新 keys → `SetVoicePackSelection` → `InvalidateXenotypeRowCache()` |
| 行缓存失效 | `XenotypeUI.cs:543-548`；`467-476`（ReferenceEquals 检查 catalog 与语言） | 视图数据缓存，状态变化才重建 |
| 过滤器签名缓存 | `XenotypeUI.cs:528-540` `EnsureXenotypeRowFilter` | `query|filters` 签名不变则复用筛选结果 |
| 先测量后绘制 | `XenotypeUI.cs:120-169`（`MeasureXenotypeManagementHeight`→`ArrangeXenotypeManagement`）；`710-733, 735-760`；`814-828` | 布局纯计算与绘制分离；滚动高度预先确定 |
| Scribe 持久化 | `SqueakyRatkinSettings.ExposeData.cs:48-132` | `voicePackMode`/`voicePackSelections` 等经 Scribe_Values/Collections 读写；PostLoadInit 归一化；迁移事务化 |
| UI 基元库 | `UI/SqueakySettingsUI.cs`：`SelectableCard:39`、`Button:72`、`Toggle:120`、`Tab:151`、`SearchField:198`、`FilterChip:221`、`PanelFrame:326`、`SectionHeader:373`、`EmptyState:393`、`StatusPanel:354`、`HelpIndicator/Toggle:240/260` | 已是“手绘即时组件”雏形：rect 入、bool/string 出 |
| 确认对话框 | `XenotypeUI.cs:76-97` `RequestVoicePackMode`（Remix 两段确认，`Dialog_SqueakyCompactMessageBox`） | 危险操作走命令 + 窗口栈，不直接改状态 |

**关键事实（HANDOFF 已确认）**：数据面与 UI 解耦——`SetVoicePackSelection(scope, targetDefName, enabledKeys)` 以 `(scope, raceDefName, xenotypeDefName)` 记录并立即重建 resolver。因此 UI 层可以放心重组为组件，只要命令最终落到同一批写入桥。

## 3. 三方案对比与推荐

| 维度 | A：响应式 view-model + 声明式即时组件 | B：Plan A 原样裁剪（现状最小改） | C：完整 observable + diff 组件树 |
|---|---|---|---|
| 崩溃安全 | 页面级护栏 + 组件无状态、null 安全；高 | 与现状相同；可行但护栏散落 | 高，但 diff/订阅系统本身是崩溃源 |
| 可测试性 | 高度/布局纯函数可单测；命令可记录回放；高 | 低：全部混在私有 Draw 方法里 | 中：diff 可测，但 Verse 绑定难测 |
| 改动量 | 中（新写组件层 + 精简设置壳） | **低** | 高（需自建 observable/diff 运行时） |
| 可维护性 | 组件边界清晰、命令显式；高 | 中：仍是千行私有方法 | 低：为 IMGUI 引入不适配的生命周期抽象 |
| RimWorld 兼容性 | **原生**：纯 Verse Widgets | 原生 | 原生，但复杂且无收益 |
| 推荐 | ✅ **推荐** | 兜底 | ❌ 不适合 |

理由：IMGUI 没有节点生命周期，diff 组件树（方案 C）的“挂载/卸载/补丁”概念完全落空；方案 B 不解决“千行私有方法 + 状态散落”问题；方案 A 恰好把 SR 已有的两种实践——**无状态基元（SqueakySettingsUI）** 与 **写入桥/缓存失效（Settings/XenotypeUI）**——合并成一条显式单向数据流。

## 4. 组件清单草案（US 重建时的 UI/ 布局）

```text
Source/UniversalSqueaker/UI/
├─ Model/
│  ├─ VoicePacksViewState.cs        # 每帧从 settings/catalog 投影的只读视图状态
│  ├─ UiCommand.cs                  # 命令类型（SetMode/OpenRace/TogglePack/...）
│  └─ VoicePacksPageModel.cs        # 唯一业务入口：执行命令 → 调用写入桥 → 重建 view-state
├─ Components/                      # 全部无状态：只读 state，只 emit 命令
│  ├─ SectionFrame.cs / StatusBanner.cs / HelpToggle.cs / SearchField.cs
│  ├─ ModeCard.cs
│  ├─ RaceLayerRow.cs
│  ├─ VoicePackRow.cs
│  ├─ VoicePackChecklist.cs
│  └─ EmptyState.cs
├─ VoicePacksPage.cs                # 组合 + 页面级 try/catch 护栏
└─ Layout/                          # 纯高度/矩形计算（可脱离游戏单测）
   └─ VoicePacksLayout.cs
```

### 组件规格

| 组件 | 输入状态 | 输出命令 | 高度测量职责 |
|---|---|---|---|
| `ModeCard` | 当前 mode、三卡 label/desc、Biotech 是否激活 | `SetMode(mode)`（Remix 时 `RequestRemixConfirm`） | `Layout.ModeCardHeight(width)` |
| `RaceLayerRow` | race summary（defName/label/已启用数/候选数）、是否选中 | `OpenRaceEditor()` | `Layout.RaceLayerHeight(width)` |
| `VoicePackRow` | pack key/label/作者/状态、是否选中、是否禁止改动 | `TogglePack(key, on)` | 固定 74f（沿用 SR 行高，见 `XenotypeUI.cs:1000`） |
| `VoicePackChecklist` | packs、selected keys、mode、conflict/dormant/orphan 状态、search | `TogglePack` / `ForgetUnavailable` | 汇总行高 + 横幅高（等价 `MeasureVoicePackDomainHeight` 的纯化版） |
| `EmptyState` | 文案 | 无 | `Text.CalcHeight` 封装后计算 |
| `SectionFrame` | rect、表面种类 | 无 | 无（纯绘制） |
| `Footer` | dirty 标志、保存提示 | 无（保存由设置关闭路径负责） | 固定高度 |

**红线**：
- 组件**不持有** `SqueakyRatkinSettings`/`SqueakXenotypeCatalog` 引用，只接收投影后的只读 state；
- 所有写操作 = `emit(UiCommand)`，由 `VoicePacksPageModel` 落到 `SetVoicePackSelection`/`CommitVoicePackMode`/`QueuePersistence` 写入桥；
- **Scribe schema 不变**：`voicePackSelections`/`voicePackMode`/resolver 相关字段名、record 形状、ExposeData 顺序都不动；
- `Layout/` 高度函数只依赖 `(width, 文本, 计数)`，文本测量经 `ITextMetrics` 接口注入（生产用 Verse `Text`，测试用桩），保证可脱离游戏单测。

## 5. 示例草图（伪 C#，展示组合与命令回传）

```csharp
// 命令：只读 struct，页面模型统一执行
readonly struct UiCommand
{
    public readonly UiCommandKind Kind;      // SetMode / OpenRace / TogglePack / ForgetUnavailable
    public readonly string Arg;              // mode 名或 pack key
    public readonly bool Flag;               // TogglePack 的 on/off
}

// 组件：无状态，只画、只 emit
static class ModeCard
{
    public static void Draw(Rect rect, VoicePackMode mode, VoicePackMode target,
                            string title, string desc, Action<UiCommand> emit)
    {
        bool selected = mode == target;
        SqueakySettingsUI.SelectableCard(rect, title, desc, selected);
        if (Widgets.ButtonInvisible(rect))
            emit(new UiCommand(UiCommandKind.SetMode, target.ToString(), false));
    }
}

// 页面：Measure → 组合组件 → 收集命令 → 统一执行（护栏 + 单向流）
partial class VoicePacksPage
{
    public void Draw(Rect rect)
    {
        try
        {
            VoicePacksViewState view = VoicePacksPageModel.BuildView(settings, catalog);
            var commands = new List<UiCommand>();
            Action<UiCommand> emit = commands.Add;

            SectionFrame.Draw(Layout.Section(rect), SurfaceKind.Emphasized);
            foreach (var mode in Modes)
                ModeCard.Draw(Layout.ModeCard(rect, mode), view.Mode, mode, ... , emit);

            RaceLayerRow.Draw(Layout.RaceLayer(rect), view.RaceLayer, emit);
            VoicePackChecklist.Draw(Layout.Checklist(rect, view), view.Checklist, emit);

            foreach (var cmd in commands) VoicePacksPageModel.Execute(cmd); // → SetVoicePackSelection 等写入桥
        }
        catch (Exception ex)
        {
            // 页面级护栏：渲染失败显示可恢复错误，绝不抛出红字或破坏设置窗口
            Widgets.Label(rect, "US.Settings.RenderFailed".Translate());
            SqueakLog.UiRenderFailed(ex);
        }
    }
}
```

要点：`Layout.*` 全部是纯矩形计算；组件不写业务字段；命令在帧末统一执行（等价 SR 的 toggle 处理路径 `XenotypeUI.cs:1007-1013`，但显式化）。

## 6. 实施检查单（Phase 3 验收）

**崩溃安全矩阵**（设置页打开/操作/关闭全程无红字）：
- [ ] 无候选 pack；无 Biotech；无 HAR；无选中 pawn
- [ ] 损坏设置文件（缺字段/非法 enum/非法 record）
- [ ] fallback profile 缺失；catalog 为 null 或 snapshot 过期
- [ ] 窗口极窄（<760f，窄布局分支）；滚动内容高度为 0
- [ ] 渲染中组件异常 → 页面级护栏生效，窗口仍可关闭

**两族分配矩阵**（通用性验收）：
- [ ] Race A、Race B 各装一个 VoicePack；勾选/取消/切换模式立即反映到路由
- [ ] Race A 的包绝不路由到 Race B；Race 列表来自 `catalog.RacePacks.Select(p => p.raceDefName).Distinct(Ordinal)`，无 Ratkin 特判
- [ ] 移除/重装某 pack 后：orphan/forget 路径正确，配置可持久化重载

**代码红线**：
- [ ] `voicePackSelections`/`voicePackMode`/resolver 的 Scribe schema 零变化
- [ ] `UI/Components` 与 `UI/Layout` 零业务写入、零 settings/catalog 直接引用
- [ ] 双 flavor 编译绿（Dev/Release），`TreatWarningsAsErrors`
