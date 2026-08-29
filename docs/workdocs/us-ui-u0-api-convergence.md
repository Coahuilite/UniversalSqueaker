# U0 任务书：UiKit 公共 API 收敛

> 状态：待执行。
> 目标：让 FerriteLib.UiKit 成为 US 依赖的公共框架层，消除 US 侧重复实现的通用基础设施。
> 依据：`docs/workdocs/us-ui-overhaul-assessment.md` §3 U0 / §6。

## 范围

允许修改：

- `Source/FerriteLib.UiKit/**`
- `Source/UniversalSqueaker/UI/**`
- `tools/FerriteLib.UiKit.Tests/**`
- 本任务书相关文档（如需要）

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布
- FerriteLib 保持中性：不得出现 `UniversalSqueaker` / `SqueakyRatkin` / `Ratkin` / `SR_` / `US_` 字面量

## 任务

### 1. 新增 public `UiGuard`

在 `Source/FerriteLib.UiKit/Widgets/UiGuard.cs` 新增 public 静态类：

```csharp
public static class UiGuard
{
    public static float MeasureOrFallback(
        Func<float> custom, float fallbackHeight,
        string componentId, string? logScope = null);

    public static void DrawOrFallback(
        Rect rect, Action custom, Action<Rect> fallback,
        string componentId, string? logScope = null);

    public static void LogFallback(
        string componentId, string? logScope, Exception ex);

    public static void ResetSessionLog();
}
```

行为：

- 捕获异常 → 恢复 GUI 状态（`Text.Font` / `Text.Anchor` / `GUI.color`）→ 执行 fallback。
- per-session 去重：同一 `logScope + componentId` 每个会话只 `Log.Warning` 一次。
- 日志统一走 UiKit 通道，格式：
  `[FerriteLib.UiKit] fallback triggered for component '<componentId>' (<logScope>): <ex.Message>`
- `ResetSessionLog()` 清空去重集合。
- 为测试提供 `internal static Action<string>? LogWarningOverride`（测试 seam，不改变默认行为）。

### 2. 删除 `FerriteGuard`，内置 widget 改用 `UiGuard`

- `Source/FerriteLib.UiKit/Widgets/FerriteGuard.cs` → 删除。
- `InputModeRowWidget.cs`、`InputModeCardWidget.cs` 的 `FerriteGuard.DrawOrFallback(...)` → `UiGuard.DrawOrFallback(..., componentId: "<kind>", logScope: "FerriteLib")` 或等价中性 scope。

### 3. 删除 US 侧 `UsGuard`，全部调用改 `UiGuard`

- `Source/UniversalSqueaker/UI/Visuals/UsGuard.cs` → 删除。
- 替换全部 `UsGuard.MeasureOrFallback` / `UsGuard.DrawOrFallback` 调用（grep 约 24 处）：
  - `componentId = Kind`（如 `us/scope-tree`）
  - `logScope = "UniversalSqueaker"`
- `VoicePacksPage.BeginSession/EndSession` 的 `UsGuard.ResetSessionLog()` → `UiGuard.ResetSessionLog()`。
- `FerriteVoicePacksPage.Draw` 页面级 catch 的 `Log.Warning(...)` → `UiGuard.LogFallback("us/ferrite-page", "UniversalSqueaker", ex)`；fallback 页再次失败同样走 `UiGuard.LogFallback`。

### 4. （如本阶段允许）Surface / Theme / Drawing helper 收敛

若评估中的 U0 完整执行，则继续：

- 将 `Palette` / `SurfaceFrame` / `UiKitGui` 提升为 public 或提供 public `UiSurface` / `UiTheme` 入口。
- US 侧 `UsVisualTokens` / `UsSurface` / `UsWidgetDrawing` 删除，改用 UiKit 公共 API + US 主题值注入。
- 若 U0 只做 guard 收敛，则 Surface/Theme 留给 U5，但本任务书默认 **U0 应完成 guard 收敛，Surface/Theme 可延到 U5**（由调度者执行时决定，避免单 stage 过大）。

## 测试

- 新增 `UiGuard` 测试：
  1. Measure 抛异常 → fallback 高度返回；
  2. Draw 抛异常 → fallback 绘制被调用；
  3. 同组件同会话只 log 一次；
  4. `ResetSessionLog()` 后再次触发可再 log；
  5. 日志内容包含 `componentId` 与 `logScope`（通过 `LogWarningOverride` 断言）。

## 验收标准

- [ ] `UiGuard` 编译通过且为 public。
- [ ] `Source/FerriteLib.UiKit` 中无 `FerriteGuard` 残留。
- [ ] `Source/UniversalSqueaker/UI` 中无 `UsGuard` 残留（grep 0 命中）。
- [ ] 所有 fallback 日志走 `UiGuard`，不再由 US 直接 `Log.Warning` fallback。
- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev` 零警告
- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release` 零警告
- [ ] `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev` 零警告
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS
- [ ] 中性 grep：`Source/FerriteLib.UiKit` 无 US/SR 产品字面量

## 提交信息建议

`refactor(uikit): expose UiGuard and remove UsGuard/FerriteGuard duplication`
