# S4-Polish P4 — 过滤系统（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：P3 验收通过。
> 纯谓词 `VoicePacksFilters` 已在 P纯 落地，本任务做状态/命令/投影/FilterBarWidget。

## 先读

- `docs/s4-polish-plan-zh.md` §4 P4、§3.2。
- `Source/UniversalSqueaker/UI/Layout/VoicePacksFilters.cs`（P纯 已存在）
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs`、`UiCommand.cs`、`VoicePacksPageModel.cs`、`FerriteVoicePacksPage.cs`、`UsWidgetCommandAdapter.cs`、`UsWidgetRegistrar.cs`、`Layout.xml`
- `docs/ui-visual-modernization-zh.md` 段按钮规格（P0 稿）。
- 不要读其它 workdocs。

## 改动清单

### 1. 页面过滤状态

- `VoicePacksPageState` 新增：`public UiDomainFilter DomainFilter;`、`public UiPackFilter PackFilter;`（ephemeral）。
- `Reset()` 清空两者（`default`）。
- 新增两个只读结构体定义位置：**复用 P纯 的 `UiDomainFilter`/`UiPackFilter`**（同 namespace 下直接引用，不要重复定义）。

### 2. 命令

- `UiCommandKind` 新增 `SetDomainFilter`、`SetPackFilter`。
- 语义约定（Arg/Flag）：
  - `SetDomainFilter`：`Arg="EnabledOnly"|"ConflictOnly"|"OrphanOnly"`，`Flag` = 开/关。
  - `SetPackFilter`：`Arg="EnabledOnly"` 或 `Arg="Author|<name>"`，`Flag` = 开/关（作者名用 `<name>`，空名表示清除作者过滤）。
- `UsWidgetCommandAdapter` 为两个新 Kind 增加名称映射（`"SetDomainFilter"` / `"SetPackFilter"`）；`FerriteVoicePacksPage.TryTranslate` 已走 `UsCommandPayload`，无需改翻译逻辑。

### 3. 投影过滤（`VoicePacksPageModel.BuildView`）

- 用 `VoicePacksFilters` 过滤：
  - `Races`：`DomainMatches` 输入 `hasConflict:false, isDormant:false, isTargetUnavailable:false, isOrphan: state==Orphan, isEnabled: EnabledCount>0, author:""`。
  - `XenotypeDomains`：`hasConflict: HasCanonicalConflict, isDormant: IsDormant, isTargetUnavailable: IsTargetUnavailable, isOrphan: OrphanCount>0, isEnabled: EnabledCount>0, author:""`。
  - 选中域 `SelectedDomain` 的 `Packs`：`PackMatches(row.Author, row.ModName, row.IsSelected, PackFilter)`；过滤后重新构造该域视图（其余字段保持原值，`Packs` 为过滤结果）。
- 选中域被过滤掉时：`ResolveSelectedDomain` 在过滤后列表为空则返回 null；非空则回退到过滤后首个域。保证 UI 永远有可操作域或空态，不出现幽灵选中。
- 注意 `BuildView` 现有写回 state 的归一化逻辑保持不变，不新增写回。

### 4. `Execute` 新命令处理

- `SetDomainFilter` / `SetPackFilter`：解析 Arg/Flag，写 `state.DomainFilter` / `state.PackFilter`；**不写 settings、不 QueuePersistence**（纯页面状态）。
- 对未知 Arg 或空作者名：no-op。

### 5. 新增 `Source/UniversalSqueaker/UI/Widgets/FilterBarWidget.cs`

- Kind = `us/filter-bar`。
- `Measure`：单行 24f（`VoicePacksLayout.SectionHeaderHeight` 值）；窄屏（<320px）两行 48f（P6 会再强化，这里先留两行能力）。
- `Draw`：
  - 段按钮组：`All`（清除 EnabledOnly/ConflictOnly/OrphanOnly，保留作者？**All = 清除全部域过滤**）、`Enabled only`、`Conflicts`、`Orphan only`。
  - 作者循环按钮：`Author: All` → 点击循环到下一个作者。作者列表从 `ctx` 读 `"Races"`/`"XenotypeDomains"` 收集不到作者——**新增 view state 键 `"Authors"`**：`VoicePacksViewState` 增加 `IReadOnlyList<string> Authors`；`BuildView` 从全部 pack 行收集去重作者（`VoicePackRowView.Author` 非空），排序 Ordinal。`FerriteVoicePacksPage` 字典加 `"Authors"`。
  - 作者循环逻辑：当前作者在列表中则取下一个；不在或为空则回到 All；列表为空时按钮禁用（只画文本不响应）。
  - 状态显示：选中段按钮用 `UsSurface.DrawSegment` selected 样式；作者按钮显示 `Author: <当前或All>`。
  - 命令：`SetDomainFilter` / `SetPackFilter`（作者用 `Arg="Author|<name>"`）。
  - 用 `UsGuard.DrawOrFallback`，fallback = 一行原版 `Widgets.ButtonText`（All/Enabled/Conflicts/Orphan/Author 五个按钮横排，放不下换行）。

### 6. 注册与布局

- `UsWidgetRegistrar`：注册 `us/filter-bar`。
- `Layout.xml`：在 `chrome/banner` 与 `input/mode-row` 之间插入 `<Widget Id="filter-bar" Kind="us/filter-bar" />`。

## 验收

- 构建 Dev 0 警告；kernel 测试全绿；UiLogicTests ALL GREEN；`verify-local.ps1` 14 门全绿。
- 过滤后 race/xeno 列表与 checklist 同步收窄；`VoicePacksFilters` 纯函数已覆盖判定（无需新断言，但可补组合用例）。
- 空过滤结果显示空态（现有 EmptyState 路径），不崩溃。
- 过滤操作不 QueuePersistence、不写 settings（`grep` 自检 `Execute` 新分支无 `QueuePersistence`）。
- 隐私预检通过。

## 提交

```text
feat(S4): domain/pack filter bar
```

允许范围：`FilterBarWidget.cs`（新）、`VoicePacksPageState.cs`、`UiCommand.cs`、`VoicePacksPageModel.cs`、`VoicePacksViewState.cs`、`FerriteVoicePacksPage.cs`、`UsWidgetCommandAdapter.cs`、`UsWidgetRegistrar.cs`、`Layout.xml`。不得改其它文件。

## 禁止

- 不引入新业务写桥；不改 settings schema。
- 不把过滤状态持久化。
- 不 push / 配 remote。
