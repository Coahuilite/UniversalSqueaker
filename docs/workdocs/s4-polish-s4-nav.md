# S4-Polish S4-Nav — 左侧导航 + 三页签 + sticky footer（编码任务书）

> 状态：待派发。执行者：C-Agent（持久 worker）。依赖：S4-Vol 验收通过。
> 目标：把当前单页滚动布局重构为 RimWorld 窗口外壳内的 **左侧导航 + 右侧内容 + 右侧底部 sticky footer** 三页签结构。

## 先读

- `docs/s4-polish-plan-zh.md` §1.4、§4 S4-Nav、§4 P2（footer 数据）、§3.7。
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/Layout.xml`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs`
- `Source/UniversalSqueaker/UI/Model/UiCommand.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `Source/UniversalSqueaker/UI/UsWidgetRegistrar.cs`
- `Source/UniversalSqueaker/UI/Widgets/UsFooterWidget.cs`（若 S4-Vol 后已存在？不存在则本块只接布局，footer 视觉由 P2 做）
- 不要读其它 workdocs。

## 语义

- 窗口外壳：RimWorld `Window` 标题栏/关闭按钮/内容视口由宿主提供；本块负责内容区布局不溢出、不遮挡。
- 左侧导航固定三页签：`基础设置` / `调音` / `包清单`。
- 右侧内容区：当前页签内容在滚动区；footer 固定右侧可视底部。
- 页签状态为 ephemeral（`VoicePacksPageState.ActiveTab`），Reset 回基础设置。

## 改动清单

### 1. 页签状态与命令

- `VoicePacksPageState.cs`：新增 `ActiveTab`（如 `string` 或 enum：`Basic/Tuning/Packs`），`Reset()` 回 `Basic`。
- `UiCommand.cs`：新增 `UiCommandKind.SetActiveTab`；`Arg` 为 `"Basic"/"Tuning"/"Packs"`。
- `VoicePacksPageModel.Execute`：处理 `SetActiveTab`，校验值后写 `state.ActiveTab`。
- `FerriteVoicePacksPage`：读取 `State.ActiveTab`，只渲染对应页签内容。

### 2. 布局结构

- `Layout.xml`：重组为三个页签容器（或由 `FerriteVoicePacksPage` 在代码里选择根子树）：
  - `basic`：mode row、global volume、attenuation editor、basic toggles。
  - `tuning`：scope tree、mood tuning、preset import。
  - `packs`：filter bar、race layer、xenotype layer、voice pack checklist。
- 左侧导航：可新增 `us/nav` widget 或由 `FerriteVoicePacksPage` 直接绘制三枚按钮；点击 emit `SetActiveTab`。
- 右侧滚动区 + 底部 footer：footer 不随滚动区滚走；footer 数据投影可先沿用现有 `chrome/footer` 或 `us/footer`（P2 再完善视觉）。

### 3. 不改变

- 不改变各页签内 widget 的命令流、Measure 高度、业务写桥。
- 不改变 Settings schema。
- 不实现现代皮肤（P1 做）、不实现响应式细节（P6 做）。

## 验收

- 三个页签可切换；每个页签内容正确归属。
- 内容不溢出窗口可视区；footer 固定右侧底部。
- 构建 Dev 0 警告；kernel 测试全绿；UiLogicTests ALL GREEN；`verify-local.ps1` 14 门全绿。
- 隐私预检通过。

## 提交

一条提交：

```text
feat(S4-Nav): left nav + three tabs + sticky footer layout
```

只允许包含：`VoicePacksPageState.cs`、`UiCommand.cs`、`VoicePacksPageModel.cs`、`FerriteVoicePacksPage.cs`、`Layout.xml`、`UsWidgetRegistrar.cs`、新增 `UsNavWidget.cs`（如使用）。不得改其它文件。

## 禁止

- 不做现代皮肤；不做帮助/窄屏/fallback。
- 不改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
- 不 push / 配 remote。
