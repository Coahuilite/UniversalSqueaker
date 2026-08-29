# S4-Polish 任务书索引（workdocs）

> 临时目录。S4-Polish 全部落地后随清理提交移除。每个 worker 只读自己的任务书；调度者读 `s4-polish-kickoff.md`。
> 权威计划：`docs/s4-polish-plan-zh.md`。状态由调度者维护。

## 派发状态

| 顺序 | 任务书 | worker | provider / model / effort | 依赖 | 状态 |
|---|---|---|---|---|---|
| 0a | s4-polish-p0-visual-spec.md | D-Agent（one-shot） | deepseek-official / deepseek-v4-flash / high（qwen-token-plan-cn 不可用回退） | 无 | ✅ 已修订并提交 `36eeb57`（维护者已批准） |
| 0b | s4-polish-pure-logic.md | C-Agent（worker） | deepseek-official / deepseek-v4-flash / max（qwen-token-plan-cn 不可用回退） | 无 | ✅ 完成并提交 `ef44a0f`（已验收） |
| 0c | s4-polish-s4-vol.md | C-Agent（worker） | 同上 / max | 无（P纯 已过；可与 P0 修订并行设计） | ✅ 完成并提交 `73be00e`（已验收） |
| 0d | s4-polish-s4-nav.md | C-Agent（worker） | 同上 / max | S4-Vol 通过 | ✅ 完成并提交 `b3810d2`（已验收） |
| 1 | s4-polish-p1-skin-foundation.md | C-Agent | 同上 / max | P0 批准 + S4-Nav 通过 | ✅ 完成并提交 `b166ca9`（已验收） |
| 2 | s4-polish-p2-footer.md | C-Agent | 同上 / max | P1 通过 | ✅ 完成并提交 `5f5196c`（已验收） |
| 3 | s4-polish-p3-distance-chart.md | — | — | — | ✅ 已并入 S4-Vol，不再单独派发 |
| 4 | s4-polish-p4-filter.md | C-Agent | 同上 / max | P2 通过 | ✅ 完成并提交 `1fb7688`（已验收） |
| 5 | s4-polish-p5-help.md | C-Agent | 同上 / max | P4 通过 | ✅ 完成并提交 `663f7d0`（已验收） |
| 6 | s4-polish-p6-responsive.md | C-Agent | 同上 / max | P5 通过 | ✅ 完成并提交 `268da1f`（已验收） |
| 7 | s4-polish-p7-fallback.md | C-Agent | 同上 / max | P6 通过 | 🔄 已派发（C-Agent 执行中） |

## 通用门禁命令（worker 自跑，调度者复跑）

- 构建（Dev，零警告，TreatWarningsAsErrors）：`dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`
- 内核测试：`dotnet run --project tools/UniversalSqueakerKernelTests -c Release`
- UI 纯逻辑测试（P纯 起）：`dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`
- 全量门禁：`pwsh -File scripts/verify-local.ps1`（P纯后为 14 门）
- 隐私预检：不写 `PublishedFileId`/个人绝对路径/凭据/token。

## 派发原则（Polish 专用）

1. 编码 lane 只有 1 个 worker，全部串行；唯一并行是文档任务（P0/D-Agent）与编码任务（C-Agent）。
2. P1 的开工硬门 = 维护者批准 `docs/ui-visual-modernization-zh.md`。
3. 每本任务书自包含；worker 不读本目录其它任务书，不读 docs 下其它长篇文档（任务书点名的文件除外）。
4. 每棒一个原子提交；提交信息在任务书内指定。
5. 调度者不编码；每棒验收后更新本索引状态。
6. 全量换肤后的中性边界：FerriteLib 内不得出现 `UniversalSqueaker`/`SqueakyRatkin`/`Ratkin`/`SR_`/`US_` 产品字面量。
