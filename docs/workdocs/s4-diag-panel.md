# 任务书：S4-Diag-Panel（诊断面板主体：overlay + 可拖拽面板 + 头顶标志点）

## 工作目录
仓库根：<workspace>\UniversalSqueaker（下称 <root>）。
只读本任务书 + 你改动的源文件 + HANDOFF §8 + MEMORY checkpoint + 只读证据源 ../squeaky_ratkin/Source/SqueakyRatkin/Debug/ 下的 SqueakDiagnosticsOverlay.cs / SqueakDiagnosticsPanel.cs / SqueakDiagnosticsMode（枚举在 overlay 里）。不要读 docs 下其它长篇文档。

## 目标
US 化重建诊断面板主体。**关键差异**：头顶标志点不恢复 SR 的 MapInterface 反射 hook（不稳定），改用 CompSqueaker.PostDraw()（公开 ThingComp 钩子）绘制。面板 Window 保持可拖拽非模态，按钮切换双模式，Esc 双击/X/关闭按钮关闭。

## 关键事实（已核实，勿重新推导）
- CompSqueaker 是挂在 pawn 上的 ThingComp，public class，已有 internal GetDiagnosticSnapshot() 返回 SqueakDiagnosticSnapshot（21 字段）。
- ThingComp.PostDraw() 是公开空 virtual（RimWorld Verse/ThingComp.cs），会在 ThingWithComps.Comps_PostDraw() 被调。**在本组件 override PostDraw() 画头顶标志点**，天然跟随 pawn 移动。
- SqueakRecentOutcome 现在含 PoolStableKey（提交 84c7a64），可显示「包:音频」。
- GenMapUI.DrawText(Vector2 worldPos, string text, Color textColor) 公开（Tiny 字体 + 自带灰底）。SR 用 4 方向偏移黑 DrawText 做描边再叠主色（见 SR SqueakDiagnosticsOverlay.DrawMark）。
- SqueakDebug.OpenSelectedDiagnostics() 已存在（占位，本块填充）；SqueakDebug.ShowCameraIndicator 已存在。
- SqueakActionDefinitions / SqueakLabels.Action(SqueakAction) 已存在（动作显示名）。
- CompSqueaker.DiagnosticsEnabled 静态字段已存在（RecordOutcome 用它门控是否存 SqueakRecentOutcome）。
- SR 参照（只读）：SqueakDiagnosticsOverlay.cs（SetMode/MaintainLifecycle/RefreshIfDue/DrawCached/RefreshSelected/RefreshVisible/RefreshSnapshot/DrawMark/RemoveUnrefreshedPawns/ClearSession/NotifyPanelClosed/ReadyFor）、SqueakDiagnosticsPanel.cs（Window 子类，双模式 DrawSelected/DrawVisible，字段网格）。

## 布局设计（本任务书定稿，照此实现）

### 面板窗口（Window）
- 非模态可拖拽：forcePause=false、absorbInputAroundWindow=false、preventCameraMotion=false、draggable=true、doCloseX=true、closeOnCancel=false、closeOnAccept=false、closeOnClickedOutside=false、onlyOneOfTypeAllowed=true、focusWhenOpened=false、onlyDrawInDevMode=true。
- 初始尺寸 InitialSize = (420, 600)。
- 顶部标题行：左标题 "Universal Squeaker Diagnostics"（或翻译键），右一个模式切换按钮（Selected / Visible 两态循环，点击切换 SqueakDiagnosticsOverlay.Mode）。
- 关闭：原生 X（doCloseX）直接关；Esc 双击（3 秒内两次）关（OnCancelKeyPressed 里 arming 逻辑，同 SR）；关闭按钮 = 复用 X。
- 底部固定 22px hint 槽：Esc 已 armed 时显示提示（如 "Press Esc again to close"），未 armed 时空白占位避免跳动。

### Selected 模式（单 pawn 详情）
分两个区，竖向滚动区（Window 内容超高时 Widgets.BeginScrollView）：
区 1（2 行）：
  1. 当前动作：SqueakLabels.Action(CurrentTimingAction) 或 "—"。
  2. 发配音频：LastSignificantOutcome 里 Outcome==Dispatched 时显示 "PoolStableKey : Sound.defName"（PoolStableKey 空则只 Sound.defName；无则 "—"）。
区 2（门禁链，17 行，每行三态）：
  每行 = 门禁名（左）+ 状态（右），状态三色：
    - 绿 = 通过（不阻塞）
    - 琥珀黄 = 确定性阻塞（当前卡住的原因）
    - 蓝 = 随机/参数门（概率门显示概率参数、交谈门显示 TalkingChance）
  门禁清单（顺序）与判定（用 SqueakDiagnosticSnapshot 字段 + 即时求值）：
  G0 Disabled 旁路：VoicePackMode==Disabled → 琥珀阻塞（否则绿，标 N/A 因面板在 DevMode 下）
  G1 离图：pawn.Spawned && MapHeld==CurrentMap → 绿，否则琥珀阻塞
  G2 离屏：CameraDriver.CurrentViewRect.ExpandedBy(10).Contains(pawn.Position) → 绿，否则琥珀阻塞
  G3 plan 缺失/未配置：snapshot.CurrentTimingAction 有值且 plan 可用 → 绿，否则琥珀（当前无动作时标 N/A）
  G4 身份门（外部路径）：非外部时 N/A；外部时按 IsPlayerControlled/Downed/Awake 求值
  G5 动作门（外部动作）：内置动作 → 绿；外部动作需 AllowExternalActions
  G6 作用域 Enabled：snapshot.CurrentActionEnabled → 绿，否则琥珀阻塞
  G7 作用域匹配（ActiveCommand）：snapshot.CurrentTriggerMode 相关，非 ActiveCommand scope 时 N/A
  G8 启动相位：snapshot.StartupPending → 琥珀（等启动相位），否则绿
  G9 概率门：snapshot.BaseProbability/EffectiveProbability（随机，蓝色显示概率值）
  G10 动作冷却：snapshot.Timing.ActionReady → 绿，否则琥珀（显示 ActionRemainingTicks）
  G11 全局冷却：snapshot.Timing.GlobalReady → 绿，否则琥珀（显示 GlobalRemainingTicks）
  G12 声带门：snapshot.VocalCapability.VocalOrganEfficiency > VocalSilenceThreshold → 绿，否则琥珀
  G13 交谈门：snapshot.VocalCapability.TalkingChance（蓝色显示概率）
  G14 音频池无候选：snapshot.LastEvaluation.Outcome==NoSoundFallback → 琥珀
  G15 可播性拒绝：LastEvaluation.Outcome==EligibilityRejected → 琥珀
  G16 派发成功：LastEvaluation.Outcome==Dispatched → 绿（显示 Sound.defName + PoolStableKey）
  冷却时间显示单位：tick（数值后加 "t"）；面板顶部加一个「tick/秒」切换小按钮，点后换算成秒（tick/60，保留 2 位小数，后缀 "s"）。换算只改显示，不改数据。

### Visible 模式（多 pawn 列表）
  每 pawn 一行（最多 16 行，按 mapPawns.AllPawnsSpawned 顺序，含 CompSqueaker 且在视口内的）：
  列：●(颜色点) | pawn 名(ellipsized) | 动作(短) | 冷却剩余(动作/全局 tick) | 包:音频(最近派发) | Ready/Blocked(右对齐)
  颜色点 = 头顶标志点同色：绿=Ready、琥珀=Blocked。

### 头顶标志点（CompSqueaker.PostDraw）
- 在 CompSqueaker 里 override public override void PostDraw()：
  若 SqueakDiagnosticsOverlay.Mode != Off 且本 pawn 在 cachedPawns 中且可见，画 mark（"●"）+ 4 方向黑描边 + 主色。
  主色三态：绿 = ReadyFor；琥珀 = 确定性阻断；蓝 = 随机门待定（若需区分，否则绿/琥珀两色即可——按用户 Q1 增强：绿/琥珀/蓝三态 + 描边）。
  坐标：pawn.DrawPos 上方偏移 (x, z+1.15f)，用 GenMapUI.DrawText。

## 改动清单（精确）

### 1. 新建 Diagnostics/SqueakDiagnosticsOverlay.cs（US 化，去 MapInterface hook）
- enum SqueakDiagnosticsMode { Off, Selected, Visible }（放本文件）。
- static class SqueakDiagnosticsOverlay：
  维护 List<CachedPawn> + Dictionary<Pawn,CachedPawn> + HashSet<Pawn> refreshedPawns + mode + cachedMap + selectedPawn + nextRefreshRealtime + revision + panel 引用（同 SR 结构，去掉 HookAvailable 相关）。
  CachedPawn 内部类：Pawn + Comp + Snapshot + MarkText + MarkColor。
  ReadyFor(s)：同 SR（EffectiveTimingReady && CurrentActionEnabled && VocalOrganEfficiency > VocalSilenceThreshold）。
  SetMode(newMode)：ClearSession()；若 Off 返回；若 Find.CurrentMap==null 返回；mode=newMode；cachedMap=Find.CurrentMap；CompSqueaker.DiagnosticsEnabled=true；OpenPanel()。
  MaintainLifecycle()：mode!=Off 时，若 map 变/无 → ClearSession()。（不再需要 Prefs.DevMode 检查——面板本身 onlyDrawInDevMode + DebugAction 入口已 DevMode 门控；但保险起见保留 Find.CurrentMap 检查。）
  RefreshIfDue()：mode!=Off 时，CompSqueaker.MaintainPeriodicPopulationDiagnostics()（已存在 static 方法）；Selected 模式刷新选中 pawn（0.25s），Visible 模式刷新视口内 pawn（0.5s，最多 16）。
  DrawCached()：**不再由 MapInterface 调**——US 版改为空或删除；头顶标志点改由 CompSqueaker.PostDraw 画。保留 cachedPawns 供 PostDraw/面板读。
  RefreshSelected/RefreshVisible/RefreshSnapshot/RemoveUnrefreshedPawns/ClearSession/ClearTrackedPawns/NotifyPanelClosed/OpenPanel/ClosePanel：照 SR 逻辑 US 化（namespace 改 UniversalSqueaker，去 SR 字面量）。
  RefreshSnapshot 里 MarkColor 三态：ReadyFor → 绿；否则若 StartupPending 或 !ActionReady 或 !GlobalReady → 琥珀；否则蓝（随机门/参数门）。
  internal static IReadOnlyList<CachedPawn> CachedPawns、Revision、Mode、SelectedPawn 暴露给面板。

### 2. 新建 Diagnostics/SqueakDiagnosticsPanel.cs（Window，布局见上）
- internal sealed class SqueakDiagnosticsPanel : Window，照 SR 骨架 + 本任务书布局（最小字段集，非 SR 全字段）。
- PreClose → SqueakDiagnosticsOverlay.NotifyPanelClosed()。
- OnCancelKeyPressed → Esc 双击 arming（3 秒窗口）。
- DoWindowContents → 标题行 + 模式切换按钮 + tick/秒切换按钮 + 双模式内容（BeginScrollView）+ 底部 hint 槽。
- Selected 模式：区 1（当前动作 + 发配音频含包名）+ 区 2（17 门禁链三态，按上述清单）。
- Visible 模式：16 行列表（● + pawn + 动作 + 冷却剩余 tick + 包:音频 + Ready/Blocked）。
- 门禁链状态判定用纯函数（private static，输入 SqueakDiagnosticSnapshot + Pawn + plan），不碰生产逻辑。

### 3. 改 CompSqueaker.cs：override PostDraw()
- 加 using Verse（已有）+ GenMapUI（Verse 命名空间内）。
- 加 public override void PostDraw()：若 SqueakDiagnosticsOverlay.Mode != Off，查 cachedPawns 是否有本 pawn（SqueakDiagnosticsOverlay 提供 static 方法 TryGetMark(Pawn, out string mark, out Color color)），有则 DrawMark。
- DrawMark 逻辑（可放 overlay 静态方法或 CompSqueaker 私有）：4 方向黑描边 + 主色，位置 (pawn.DrawPos.x, pawn.DrawPos.z + 1.15f)。

### 4. 改 Diagnostics/SqueakDebug.cs：填充 OpenSelectedDiagnostics()
- OpenSelectedDiagnostics() 改为：若 Find.Selector.SingleSelectedThing is Pawn pawn 且 pawn.GetComp<CompSqueaker>() != null，则 SqueakDiagnosticsOverlay.SetMode(SqueakDiagnosticsMode.Selected)；否则 SetMode(Visible)（无选中时退到 Visible）。
- 移除占位注释。

### 5. 生命周期 patch（新建 Patches/Patch_Root_DiagnosticsLifecycle.cs）
- [HarmonyPatch(typeof(Root), nameof(Root.Update))] Postfix：SqueakDiagnosticsOverlay.MaintainLifecycle()。
- 这是唯一新增 patch（不恢复 MapInterface 反射 hook）。

### 6. 翻译键（英/简中）
- 新增面板标题、模式名、门禁名、关闭提示等键（用 US.Diagnostics.* 前缀）。最少需：
  US.Diagnostics.Title / US.Diagnostics.Mode.Selected / US.Diagnostics.Mode.Visible / US.Diagnostics.CloseHint / US.Diagnostics.Ready / US.Diagnostics.Blocked / US.Diagnostics.Gate.* （17 门禁名，可用代码内英文字符串兜底，翻译键可选）
  为控 token，门禁名可先硬编码英文（代码里 const string），仅 Title/Mode/CloseHint/Ready/Blocked 走翻译键。

## 红线
- 不恢复 Patch_MapInterface_DiagnosticsOverlay（不用 MapInterface 反射 hook）。
- 头顶标志点只用 CompSqueaker.PostDraw（公开 API）。
- 不新增 Scribe 字段；不改 8 个既有 Harmony patch、S3/S4-Tuning-Backend/Orphan-Sync/Scope-Tree/Diag-Foundation 已落地逻辑、CompSqueaker 触发链逻辑（只加 PostDraw override）。
- 不恢复 developerToolsEnabled；面板 onlyDrawInDevMode=true + DebugAction 入口已 DevMode 门控。
- 门禁链判定是纯只读投影，不消费 Rand、不改时间戳、不写生产状态（GetDiagnosticSnapshot 已保证）。

## 门禁
1. dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev（零警告）
2. dotnet run --project tools/UniversalSqueakerKernelTests -c Release（全绿）
3. pwsh -File scripts/verify-local.ps1（12 门全绿）

## 完成报告（return 输出）
1. 改动文件清单（相对路径）。
2. 三条门禁退出结果。
3. 设计取舍或遗留（尤其：门禁链三态判定的纯函数、tick/秒换算、PostDraw 标志点的可见性/性能）。