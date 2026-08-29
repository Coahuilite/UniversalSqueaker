# S4-Polish P2 — Footer build identity（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：P1 验收通过。

## 任务

把页面底部 `chrome/footer` 替换为 US 自绘 `us/footer` 双槽：**左 = 构建版本，右 = 保存状态**。

## 先读

- `docs/s4-polish-plan-zh.md` §4 P2、§3.6。
- `Source/UniversalSqueaker/Mod.cs`（重点 `BuildIdentity()`、`SaveState`、`requestedSaveGeneration`、`persistedSaveGeneration`）
- `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`、`Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`、`Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`、`Source/UniversalSqueaker/UI/Layout.xml`、`Source/UniversalSqueaker/UI/UsWidgetRegistrar.cs`
- `docs/ui-visual-modernization-zh.md` 的 footer 组件规格（P0 稿）。
- 不要读其它 workdocs。

## 改动清单

### 1. `Source/UniversalSqueaker/Mod.cs`

- `BuildIdentity()` 由 `private static` 改为 `internal static`（不改逻辑）。
- 新增 `internal bool IsSettingsDirty => requestedSaveGeneration > persistedSaveGeneration;`（只读）。

### 2. `Source/UniversalSqueaker/UI/Model/VoicePacksViewState.cs`

- 根 DTO 新增只读字段：`string BuildIdentity`、`string SaveStatus`、`bool IsDirty`；构造器相应增加参数与默认防御（空串/`false`）。

### 3. `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`

- `BuildView` 投影：
  - `BuildIdentity = UniversalSqueakerMod.Instance != null ? UniversalSqueakerMod.BuildIdentity() : "unknown"`。
  - `SaveStatus` = `UniversalSqueakerMod.Instance?.SaveState.ToString() ?? "Unknown"`（`SettingsSaveState` 枚举名照抄）。
  - `IsDirty = UniversalSqueakerMod.Instance?.IsSettingsDirty ?? false`。
  - 只读，不改任何设置。

### 4. `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`

- view state 字典增加三个键：`"BuildIdentity"`、`"SaveStatus"`、`"IsDirty"`。

### 5. 新增 `Source/UniversalSqueaker/UI/Widgets/UsFooterWidget.cs`

- Kind = `us/footer`。`Measure` 返回固定 24f（与旧 footer 高度一致，不得改变总高）。
- `Draw`：左槽 `BuildIdentity`（Tiny 左对齐，TextSecondary）；右槽保存状态（Tiny 右对齐）：
  - `Saving` / `IsDirty=true` → AccentGold + 前缀 `●`。
  - `Saved` → TextSecondary。
  - `Failed` → Danger。
  - `Idle` → TextSecondary。
  - 文本从 `ctx.TryGetViewValue` 读取；读不到时画空。
- 用 `UsSurface`/`UsVisualTokens`；加 `UsGuard.DrawOrFallback`，fallback = 两个 `Widgets.Label`（左版本右状态）。
- 非交互，不 emit 命令。

### 6. 注册与布局

- `UsWidgetRegistrar`：注册 `us/footer`。
- `Layout.xml`：`chrome/footer` 根节点替换为 `<Widget Id="footer" Kind="us/footer" />`。

## 验收

- 构建 Dev 0 警告；kernel 测试全绿；UiLogicTests ALL GREEN；`verify-local.ps1` 14 门全绿。
- `grep` 确认 `VoicePacksViewState`/`FerriteVoicePacksPage` 新键名一致（`"BuildIdentity"`/`"SaveStatus"`/`"IsDirty"`）。
- 隐私预检通过。

## 提交

```text
feat(S4): footer build identity with version + save state
```

允许范围：`Mod.cs`、`VoicePacksViewState.cs`、`VoicePacksPageModel.cs`、`FerriteVoicePacksPage.cs`、`UsFooterWidget.cs`（新）、`UsWidgetRegistrar.cs`、`Layout.xml`。不得改其它文件。

## 禁止

- 不改持久化/保存逻辑。
- 不把 footer 做成交互控件。
- 不 push / 配 remote。
