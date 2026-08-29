# U6 任务书：测试 / 门禁补齐

> 状态：待执行。
> 目标：补齐 U0–U5 相关测试缺口，并更新 verify-local 门禁。
> 依赖：U0–U5 完成。
> 依据：`docs/workdocs/s4-polish-review-backlog.md` B-10..B-12、B-50..B-56；`docs/workdocs/us-ui-overhaul-assessment.md` §3 U6。

## 范围

允许修改：

- `tools/**`
- `scripts/verify-local.ps1`
- `Source/UniversalSqueaker/UI/**`（如测试暴露需要小改可测性）
- `Source/FerriteLib.UiKit/**`（如测试 seam）

禁止：

- 修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布

## 任务

### Must Fix

- **B-10**：`UiGuard` 注入异常测试（fallback 生效、per-session 去重、component id 日志断言）。
- **B-11**：`AttenuationEditorWidget` 拖拽/纯函数测试。
- **B-12**：`LayoutEngine` 宽度变化后 Draw 缓存失效回归测试。

### Should Fix

- **B-50**：distance range clamp 断言锁精确值。
- **B-51**：持久化加载侧越界 clamp 测试。
- **B-52**：SettingsMigration 测试共享静态全局状态、固定临时文件路径清理。
- **B-53**：`verify-local.ps1` 的 `dotnet run` 传 `--no-restore`；测试项目启用 warnings-as-errors。
- **B-54**：第 14 门名称更新（不再只写 filters + distance preview）。
- **B-55**：Vanilla 页不自动单测的说明沉淀到测试/门禁侧。
- **B-56**：`VoicePacksLayout` 零 Verse 纯函数测试。

### 新增门禁建议

- verify-local 增加 grep：`Source/UniversalSqueaker/UI` 不得出现 `UsGuard`。
- verify-local 增加 grep：`Source/FerriteLib.UiKit` 不得出现 `FerriteGuard`。
- 最终全量跑：14 门 + FerriteLib tests + US tests。

## 验收标准

- [ ] 上述 B-10..B-12、B-50..B-56 全部完成或明确记录为 Accept。
- [ ] `verify-local.ps1` 更新后全绿（14 门）。
- [ ] Dev/Release 构建零警告。
- [ ] `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS。
- [ ] `dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release` → 通过。
- [ ] `dotnet run --project tools/UniversalSqueakerSettingsMigrationTests -c Release` → 通过。
- [ ] `dotnet run --project tools/UniversalSqueakerKernelTests -c Release` → 通过。

## 提交信息建议

`test(ui): close review test gaps and update verify gates`
