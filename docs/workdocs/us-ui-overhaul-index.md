# US UI 大修 Stage 任务书索引

> 状态：待执行。
> 依据：`docs/workdocs/us-ui-overhaul-assessment.md`。
> 执行方式：串行，每 stage 完成后构建/测试，全部完成后对照任务书做最终 review。

| Stage | 任务书 | 内容 | 状态 |
|---|---|---|---|
| U0 | `us-ui-u0-api-convergence.md` | UiKit 公共 API 收敛，删除 UsGuard/FerriteGuard | ⏳ 待执行 |
| U1 | `us-ui-u1-interaction-routing.md` | 交互路由迁移（help/row/button/protect） | ⏳ 待执行 |
| U2 | `us-ui-u2-value-controls.md` | 全局音量 + 衰减编辑器值控件/状态 | ⏳ 待执行 |
| U3 | `us-ui-u3-layout-narrow.md` | 布局/窄屏修复 | ⏳ 待执行 |
| U4 | `us-ui-u4-fallback-robustness.md` | Fallback/健壮性 | ⏳ 待执行 |
| U5 | `us-ui-u5-skin-tokens.md` | Skin/令牌化 | ⏳ 待执行 |
| U6 | `us-ui-u6-tests-gates.md` | 测试/门禁补齐 | ⏳ 待执行 |
| Review | — | 全部完成后对照任务书做最终 review | ⏳ 待执行 |

## 门禁（每 stage 至少）

- `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev` 零警告
- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev` 零警告
- `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` → ALL PASS
- 相关 US 测试通过
- 最终 `pwsh -File scripts/verify-local.ps1` 全绿
