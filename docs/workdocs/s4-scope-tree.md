# 任务书：S4-Scope-Tree（A6 树形动作作用域开关）

## 工作目录
仓库根：<workspace>\UniversalSqueaker（下称 <root>）。
只读本任务书 + 你改动的源文件 + HANDOFF §8 + MEMORY checkpoint。不要读 docs 下其它长篇文档。

## 目标
实现 A6：分层动作作用域的树形开关 UI，让玩家能对每个动作设置 scope（Disabled / AnyOccurrence / ActiveCommand），并落到 ActionTuningRecord 分层表（运行时已消费，缺 UI 写入口）。

## 关键事实（已核实，勿重新推导）
- 数据面已就绪：Settings/UniversalSqueakerSettings.cs 有 public List<ActionTuningRecord> actionTuning = new();；Models/ActionTuningRecord.cs 有 IsValidLayer(out int layer)（0=Global 全空、1=Race 仅 raceDefName、2=Xenotype raceDefName+xenotypeDefName、-1=非法）。
- 运行时消费已就绪：SqueakRuntimeResolver.BuildGlobalActions(Global 层 layer==0) / BuildRaceBehavior(layer==1) / BuildBehavior(layer==2) 已读 actionTuning；作用域优先级 低→高 = C# DefaultScope < baseline Def < globalActionEnabled < actionTuning 层。
- 缺写桥：settings 目前没有「写 actionTuning」的方法。本次要新增（见下）。
- 动作显示名：SqueakLabels.Action(SqueakAction)（走 Keyed US.Action.*）；内置 17 键顺序 = BuiltInActionKeys.All；Kernel/ActionKey.cs 的 ActionKey.For(SqueakAction) + TryParseBuiltIn(string, out SqueakAction)。
- 作用域枚举：SqueakActionScope { Disabled, AnyOccurrence, ActiveCommand }（Pure/SqueakActionPlan.cs）。
- Ferrite widget 模板（已由 S4-Orphan-Sync 确立，参照它写）：
  - 新 widget 实现 FerriteLib.UiKit.IWidget（Configure/Measure/Draw），Kind 形如 "us/scope-tree"。
  - 从 ctx.TryGetViewValue(key, out object?) 读值；用 UsWidgetCommandAdapter.For(emit) 发 business UiCommand。
  - UI/UsWidgetRegistrar.cs 的 EnsureRegistered() 用 WidgetRegistry.Register(Scope, Widget.Kind, () => new ...()) 注册。
  - UI/FerriteVoicePacksPage.cs Draw() 的 viewState 字典补新键。
  - UI/Layout.xml 加一行 <Widget Id=... Kind=... />。
  - UI/Widgets/UsWidgetCommandAdapter.cs 的 For() switch 补新命令名映射。
  - FerriteVoicePacksPage.TryTranslate 的 UsCommandPayload 分支已能把 payload 转回 business UiCommand，无需改。

## 改动清单（精确）

### 1. 写桥（settings）
在 Settings/UniversalSqueakerSettings.cs 新增方法 SetActionTuningScope（actionTuning 可能为 null，先兜底 new List）：

  /// <summary>写一条分层作用域：Upsert 到 actionTuning（last-wins 按 (actionKey,raceDefName,xenotypeDefName)）。scope == null 表示清该层记录（移除）；走离散 resolver 重建 + 排队持久化。</summary>
  internal void SetActionTuningScope(string actionKey, string raceDefName, string xenotypeDefName, SqueakActionScope? scope)
  {
      // 按 IsValidLayer 语义归一：raceDefName 空 + xenotypeDefName 非空 = 非法，直接忽略。
      bool hasRace = !string.IsNullOrEmpty(raceDefName);
      bool hasXeno = !string.IsNullOrEmpty(xenotypeDefName);
      if (!hasRace && hasXeno) return;
      if (string.IsNullOrEmpty(actionKey)) return;

      ActionTuningRecord? record = null;
      foreach (ActionTuningRecord candidate in actionTuning ?? new List<ActionTuningRecord>())
          if (candidate != null
              && string.Equals(candidate.actionKey, actionKey, StringComparison.Ordinal)
              && string.Equals(candidate.raceDefName ?? "", raceDefName ?? "", StringComparison.Ordinal)
              && string.Equals(candidate.xenotypeDefName ?? "", xenotypeDefName ?? "", StringComparison.Ordinal))
              record = candidate;

      if (scope == null)
      {
          if (record != null) actionTuning.Remove(record);
      }
      else
      {
          if (record == null)
          {
              record = new ActionTuningRecord { actionKey = actionKey, raceDefName = raceDefName ?? "", xenotypeDefName = xenotypeDefName ?? "" };
              actionTuning.Add(record);
          }
          record.hasScope = true;
          record.scope = scope.Value;
      }
      NotifyDiscreteResolverRuntimeChanged();
      QueuePersistence();
  }

### 2. business 命令
UI/Model/UiCommand.cs 增加枚举成员 SetActionTuningScope（append 到现有枚举末尾，勿改已有序数）。

UI/Model/VoicePacksPageModel.cs 的 Execute switch 增加分支（本任务书锁定：首版只做 Global 层，即 race/xenotype 均空）：

  case UiCommandKind.SetActionTuningScope:
      if (!string.IsNullOrEmpty(command.Arg))
      {
          string[] parts = command.Arg.Split('|');
          SqueakActionScope? scope = parts.Length > 0 && !string.IsNullOrEmpty(parts[0])
              && Enum.TryParse(parts[0], true, out SqueakActionScope parsedScope) ? parsedScope : (SqueakActionScope?)null;
          string actionKey = parts.Length > 1 ? parts[1] : "";
          settings.SetActionTuningScope(actionKey, "", "", scope);
      }
      break;

约定：tree 命令 arg = "<scope>|<actionKey>"（清层则 arg = "|<actionKey>"）。用字符串管道编码，不新增 UiCommand 字段。

### 3. 视图投影
UI/Model/VoicePacksViewState.cs 新增只读投影：

  public readonly struct ActionScopeRowView
  {
      public readonly string ActionKey;
      public readonly string DisplayName;
      public readonly SqueakActionScope Scope;
      public ActionScopeRowView(string actionKey, string displayName, SqueakActionScope scope)
      { ActionKey = actionKey ?? ""; DisplayName = displayName ?? actionKey ?? ""; Scope = scope; }
  }

VoicePacksViewState 增加 public IReadOnlyList<ActionScopeRowView> ActionScopes { get; } 并在构造函数接收（BuildView 传入）。
VoicePacksPageModel.BuildView 生成 17 内置动作的行：对每个 SqueakAction，actionKey=ActionKey.For(action)，displayName=SqueakLabels.Action(action)，scope = 该 actionKey 在 actionTuning Global 层（layer==0 且 actionKey 匹配且 hasScope）的 scope，否则回退 SqueakActionDefinitions.Get(action).DefaultScope。

### 4. Ferrite widget + 注册 + viewState 键 + XML 行
- 新增 UI/Widgets/ScopeTreeWidget.cs，Kind = "us/scope-tree"，实现 IWidget：
  - Measure：读 viewState 的 "ActionScopes"（IReadOnlyList<ActionScopeRowView>），无则 0；否则 = headerHeight + rows.Count * (rowHeight + gap)。rowHeight 用 24f，gap 2f，headerHeight 用 VoicePacksLayout.SectionHeaderHeightFor("Action Scope", width, metrics)。
  - Draw：画 "Action Scope" section header，然后每动作一行：左侧显示名（SqueakLabels 已翻译），右侧三态循环按钮（短名 Off/Any/Command，对应 Disabled/AnyOccurrence/ActiveCommand）。点击发射 UiCommand(UiCommandKind.SetActionTuningScope, arg: "<scope>|<actionKey>")，循环 Disabled→AnyOccurrence→ActiveCommand→Disabled。当前 scope 高亮。
  - 复用 UsWidgetDrawing.DrawSectionHeader；视觉与 BasicTuningWidget 一致（DrawBoxSolid + SectionFrame.DrawBorder + hover）。
  - 用 Widgets.ButtonText 或 ButtonInvisible + label，确保不点穿、有 hover。
- UI/UsWidgetRegistrar.cs 注册 ScopeTreeWidget.Kind。
- UI/FerriteVoicePacksPage.cs viewState 补 ["ActionScopes"] = view.ActionScopes。
- UI/Layout.xml 在 basic-tuning 之后、race-layer 之前插入 <Widget Id="scope-tree" Kind="us/scope-tree" />。
- UI/Widgets/UsWidgetCommandAdapter.cs 的 For() switch 补 UiCommandKind.SetActionTuningScope => "SetActionTuningScope"。

## 红线
- 首版只做 Global 层树形开关（Race/Xenotype 层 UI 后续单独做，本任务书不越界）。
- 不新增 Scribe 字段（actionTuning 已存在）；不改既有 UiCommand 字段布局，用 arg 管道编码承载 actionKey+scope。
- 不碰 8 个 Harmony patch、S3/S4-Tuning-Backend/Orphan-Sync 已落地逻辑、FerriteLib 库源码、1.6/Languages。
- 内置动作显示名用 SqueakLabels.Action（已翻译）；不硬编码新翻译键。

## 门禁
1. dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev（零警告）
2. dotnet run --project tools/UniversalSqueakerKernelTests -c Release（全绿）
3. pwsh -File scripts/verify-local.ps1（12 门全绿）

## 完成报告（return 输出）
1. 改动文件清单（相对路径）。
2. 三条门禁退出结果。
3. 设计取舍或遗留（尤其：写桥的 upsert 语义、arg 管道编码、scope 三态循环是否与预期一致）。