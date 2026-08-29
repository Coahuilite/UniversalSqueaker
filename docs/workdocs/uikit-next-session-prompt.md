# 下一个接手会话提示词

你正在接手 Universal Squeaker 仓库中的一个 **FerriteLib.UiKit 交互路由与值控件**任务。请亲自按顺序完成以下四个 Block，**不要派发 subagent，不要调用 subagent / dispatch_subagents / workflow 类工具**。

## 背景

当前 FerriteLib.UiKit 的绘制顺序即输入顺序，导致 help 等顶层操作可能被内容 surface 覆盖，且大面积 row 按钮可能抢占其它组件输入。规划采用分层交互路由 + 值控件状态存储 + 滑条/数值输入框复合控件来解决。实施此前已延后，现在由你执行。

## 开始前

1. 阅读 `AGENTS.md`、`MEMORY.md`、`TODO.md`，了解仓库约束。
2. 阅读以下任务书（只读这些与本任务直接相关的 workdocs，不读其它无关 workdocs）：
   - `docs/workdocs/uikit-interaction-index.md`
   - `docs/workdocs/uikit-b1-interact-core.md`
   - `docs/workdocs/uikit-b2-value-store.md`
   - `docs/workdocs/uikit-b3-slider-number-field.md`
   - `docs/workdocs/uikit-b4-verify.md`

## 执行顺序

严格按 B1 → B2 → B3 → B4 串行执行：

1. **B1**：分层交互路由核心（`UiInteract` + `LayoutEngine` 集成）。
2. **B2**：值控件状态存储 + `Slider` / `NumberField` 原语。
3. **B3**：`input/number-slider` 复合控件。
4. **B4**：FerriteLib.UiKit 自验证。

每个 Block 完成后，必须运行该任务书要求的构建与测试；通过后再进入下一个 Block。若某个 Block 无法通过验收，停止并报告 blocker，不要跳过。

## 范围约束

- 只允许修改：
  - `Source/FerriteLib.UiKit/**`
  - `tools/FerriteLib.UiKit.Tests/**`
- 禁止修改：
  - `Source/UniversalSqueaker/**`
  - `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布、不创建 remote。
- FerriteLib 保持中性：不得出现 `UniversalSqueaker`、`SqueakyRatkin`、`Ratkin`、`SR_`、`US_` 字面量。

## 常用命令

在仓库根目录执行：

```powershell
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release
dotnet run --project tools/FerriteLib.UiKit.Tests -c Release
```

如果测试工程需要先构建 stub，按该工程现有方式执行。

## 完成标准

- B1–B3 各自验收标准全部满足。
- B4 最终验证通过：Dev/Release 零警告、测试 `ALL PASS`、中性 grep 无命中。
- `git status` 显示改动仅限 `Source/FerriteLib.UiKit/**`、`tools/FerriteLib.UiKit.Tests/**` 以及 workdocs 文档（如果必须更新）。
- 最后输出简明总结：每个 Block 完成情况、测试结果、git 改动范围、有无遗留问题。

## 重要

你亲自执行所有编码、测试、修正。不要派发 subagent，不要交给其它会话。
