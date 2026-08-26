# 任务书：S4-Diag-Foundation（诊断地基：相机指示器 + DebugAction 入口 + 翻译键）

## 工作目录
仓库根：<workspace>\UniversalSqueaker（下称 <root>）。
只读本任务书 + 你改动的源文件 + HANDOFF §8 + MEMORY checkpoint。不要读 docs 下其它长篇文档。

## 目标
落地诊断子系统的地基（本块不含面板 UI 主体，面板主体是下一块）：
1. 相机指示器从 DebugAction 移到模组设置（玩家功能，去 DevMode 门控）。
2. 在原版 Debug 菜单建立 "Universal Squeaker" 分类 + 一个 [DebugAction] 入口（选中 pawn 诊断）。
3. 恢复 localizeDebugActions 真功能：补 DebugAction_* / DebugActionCategory_* 翻译键（英/简中）。

## 关键事实（已核实，勿重新推导）
- SR 相机指示器 patch 原样：Patches/Patch_GlobalControlsUtility_CameraIndicator.cs（HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))，Postfix(float leftX, float width, ref float curBaseY)）。SR 用 Prefs.DevMode 门控 + SqueakDebug.ShowCameraIndicator 静态字段。US 要改成设置字段门控、去 DevMode。
- US 现有 Patch_DebugTabMenu_Actions.cs 已恢复（本地化 DebugAction 名/分类），常量 ActionKeyPrefix="DebugAction_"、CategoryKeyPrefix="DebugActionCategory_"，通过 settings.ApplyToRuntime 调 SetEnabled(localizeDebugActions)。
- US 现有 Diagnostics/SqueakDebug.cs 只做日志装配，无 ShowCameraIndicator 字段。
- US Settings：UniversalSqueakerSettings.cs 有字段区 + ApplyToRuntime()（约 line 94）+ NotifyCheapRuntimeChanged() + SetBasicTuning(key,value) + QueuePersistence()；ExposeData.cs 有 Scribe_Values.Look 区。
- SqueakDiagnosticSnapshot（CompSqueaker.cs internal readonly struct，21 字段）与 GetDiagnosticSnapshot() 已就绪，面板下一块用。本块不碰它。
- 翻译键文件：1.6/Languages/English/Keyed/UniversalSqueaker.xml 与 ChineseSimplified/Keyed/UniversalSqueaker.xml。
- 动作显示名走 SqueakLabels.Action(SqueakAction)（已翻译）。

## 改动清单（精确）

### 1. 相机指示器设置字段 + 开关
- Settings/UniversalSqueakerSettings.cs 字段区新增：public bool showCameraIndicator = false;
- Settings/UniversalSqueakerSettings.ExposeData.cs 的 Scribe_Values.Look 区新增：Scribe_Values.Look(ref showCameraIndicator, "showCameraIndicator", false);
- Settings/UniversalSqueakerSettings.cs 新增 setter（参照 SetBasicTuning / SetAllowEasterEggSounds 模式）：
  internal void SetCameraIndicator(bool value) { if (showCameraIndicator == value) return; showCameraIndicator = value; ApplyToRuntime(); QueuePersistence(); }
  （或只更新 SqueakDebug.ShowCameraIndicator + QueuePersistence，不必全量 ApplyToRuntime；用 NotifyCheapRuntimeChanged 更精准——见下。）
- ApplyToRuntime() 里加：SqueakDebug.ShowCameraIndicator = showCameraIndicator;
- Diagnostics/SqueakDebug.cs 加：public static bool ShowCameraIndicator = false;

### 2. 相机指示器 patch（US 化，去 DevMode）
- 新增 Patches/Patch_GlobalControlsUtility_CameraIndicator.cs，namespace UniversalSqueaker：
  HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))，Postfix(float leftX, float width, ref float curBaseY)：
  门控：if (!SqueakDebug.ShowCameraIndicator || Find.CurrentMap == null || Event.current?.type == EventType.Layout) return;
  （去掉 Prefs.DevMode。）
  显示：height = Find.Camera.transform.position.y; viewSize = Find.Camera.orthographicSize; curBaseY -= 26f; 右上角 Widgets.Label，文本用翻译键 US.Debug.CameraIndicator（参数 height 0.0、viewSize 0.0）。
  using：HarmonyLib + RimWorld + UnityEngine + Verse。

### 3. DebugAction 入口（新文件 Diagnostics/SqueakDebugActions.cs）
- namespace UniversalSqueaker，using LudeonTK + RimWorld + Verse。
- 一个 [DebugAction]：
  [DebugAction("Universal Squeaker", "Diagnostics: selected pawn", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
  public static void DiagnosticsSelected() { SqueakDiagnosticsOverlay.SetMode(SqueakDiagnosticsMode.Selected); }
  （SqueakDiagnosticsOverlay / SqueakDiagnosticsMode 是下一块的面板主体，本块先不创建——为让本块可独立编译，DebugAction 方法体先只做 Find.Selector.SingleSelectedThing != null 的空操作占位，或调用一个本块新增的 SqueakDebug.OpenSelectedDiagnostics() 空方法。**采用后者**：在 SqueakDebug.cs 加 public static void OpenSelectedDiagnostics() { }，DebugAction 调它，面板主体块再填充。)

### 4. 翻译键（英 + 简中）
- 1.6/Languages/English/Keyed/UniversalSqueaker.xml 与 ChineseSimplified 同名文件各加：
  <DebugActionCategory_UniversalSqueaker>Universal Squeaker</DebugActionCategory_UniversalSqueaker>  （注意：原版 category 去空格后的键；SR 用 "Squeaky Ratkin" 分类，键 DebugActionCategory_SqueakyRatkin）
  <DebugAction_DiagnosticsSelected>Diagnostics: selected pawn</DebugAction_DiagnosticsSelected>
  <US.Debug.CameraIndicator>Camera: height {0}, view size {1}</US.Debug.CameraIndicator>
  中文对应：分类 "Universal Squeaker"；动作 "诊断：选中 pawn"；相机 "相机：高度 {0}，视距 {1}"。
  （英文键值英文，中文键值中文；保持与现有文件风格一致。）

### 5. UI：相机指示器开关进设置
- 在现有 Ferrite 路径加一行开关。参照 BasicTuningWidget（UI/Widgets/BasicTuningWidget.cs，Kind="us/basic-tuning"）新增一个独立 widget 或扩展现有。
  **决策（本任务书锁定）**：新增 UI/Widgets/CameraIndicatorWidget.cs，Kind="us/camera-indicator"，实现 IWidget：Measure 返回 28f，Draw 一行 toggle（label "Show camera indicator"，读 viewState 键 "ShowCameraIndicator"，发射 UiCommandKind.ToggleBasic arg="CameraIndicator"）。
- UI/Model/UiCommand.cs：无需新增枚举（复用 ToggleBasic，arg="CameraIndicator"）。
- UI/Model/VoicePacksPageModel.cs Execute 的 ToggleBasic 分支：SetBasicTuning 目前只处理 ScaleCooldown/ScaleTalking/ScalePopulation 三个 key。需扩展：
  新增 case "CameraIndicator": settings.SetCameraIndicator(value); break;（在 SetBasicTuning 内部或 Execute 里加分支均可——推荐在 Settings.SetBasicTuning 的 switch 里加 case "CameraIndicator"）。
- UI/Model/VoicePacksViewState.cs 加 public bool ShowCameraIndicator { get; } + 构造参数；BuildView 传 settings.showCameraIndicator。
- UI/FerriteVoicePacksPage.cs viewState 补 ["ShowCameraIndicator"] = view.ShowCameraIndicator。
- UI/UsWidgetRegistrar.cs 注册 CameraIndicatorWidget.Kind。
- UI/Layout.xml 在 basic-tuning 之后插入 <Widget Id="camera-indicator" Kind="us/camera-indicator" />。

## 红线
- 本块不创建 SqueakDiagnosticsOverlay/SqueakDiagnosticsPanel（面板主体下一块）；DebugAction 只调占位方法 SqueakDebug.OpenSelectedDiagnostics()。
- 不新增 Scribe 字段之外的东西（showCameraIndicator 是唯一新增字段，Scribe 形状向后兼容——新增字段默认 false 不破坏旧档）。
- 不碰 8 个 Harmony patch、S3/S4-Tuning-Backend/Orphan-Sync/Scope-Tree 已落地逻辑、CompSqueaker 触发链、SqueakDiagnosticSnapshot。
- 相机 patch 去掉 DevMode 门控（玩家功能），改用 showCameraIndicator 设置开关。
- 不恢复 developerToolsEnabled。

## 门禁
1. dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev（零警告）
2. dotnet run --project tools/UniversalSqueakerKernelTests -c Release（全绿）
3. pwsh -File scripts/verify-local.ps1（12 门全绿）

## 完成报告（return 输出）
1. 改动文件清单（相对路径）。
2. 三条门禁退出结果。
3. 设计取舍或遗留。