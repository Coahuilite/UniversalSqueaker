# workdocs — S3/S4 派发任务书索引

> 临时目录，任务全部落地后随清理提交移除。每个 worker 只读自己的任务书，不读本目录其它文件，不读 docs 下其它长篇文档。

> **S4-Polish 任务书已另立索引：`docs/workdocs/s4-polish-index.md`；开工会话调度书：`docs/workdocs/s4-polish-kickoff.md`。** 下方旧 S3/S4 派发状态保留至清理。

## 派发状态

| 任务书 | worker | provider / model / effort | 状态 |
| --- | --- | --- | --- |
| s3-sustainer-external.md | S3 | commandcode / deepseek-v4-flash / max | ✅ 完成并提交 f492e6c |
| s4-tuning-baseline-design.md | —（设计契约） | — | 已定稿 |
| s4-tuning-backend.md | S4-Tuning-Backend | commandcode / deepseek-v4-flash / max | 已派发（后台） |
| s4-orphan-sync.md | S4-Orphan-Sync | commandcode / deepseek-v4-flash / max | ✅ 完成并提交 f6a7ca9（widget 模板确立） |
| s4-scope-tree.md | S4-Scope-Tree | commandcode / deepseek-v4-flash / max | ✅ 完成并提交 f580ec0（含 SupportedScopes 归一） |
| s4-diag-foundation.md | S4-Diag-Foundation | commandcode / deepseek-v4-flash / max | ✅ 完成并提交 70329d5 |
| s4-diag-panel.md | S4-Diag-Panel | commandcode / deepseek-v4-flash / max | 已派发（后台，串行） |
| s4-plan.md | —（规划稿） | — | 待分块 |

## 通用门禁命令（worker 自行执行）

- 构建（Dev，零警告，TreatWarningsAsErrors）：dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev
- 内核测试：dotnet run --project tools/UniversalSqueakerKernelTests -c Release
- 全量 12 门：pwsh -File scripts/verify-local.ps1

## 派发原则

1. 先串行无法并行的（S3 与 S4 都 build 同一 csproj，且都可能动共享文件 → 串行，避免 obj 争用与文件冲突）。
2. 每个 worker 只给任务书路径，任务书自包含；不注入整会话上下文。
3. worker 用 commandcode + deepseek/deepseek-v4-flash + max 思考强度。
4. 主会话只做设计 + 验证 + 提交；worker 做编码。
5. S4-Tuning-Backend 依赖设计契约 s4-tuning-baseline-design.md（已定稿）；S3 与 S4 都 build 同一 csproj，串行派发避免 obj 争用。
6. S4-Orphan-Sync 先行（验证 Ferrite widget 扩展模式），落地提交 f6a7ca9。
7. 剩余 UI 块（Scope-Tree/Debug-Panel/Polish）都动同一组 UI 集成管道文件（UiCommand/adapter/viewState/registrar/Layout.xml），无法真并行（会争用 obj 与同一 csproj 构建），故按优先级串行派发：Scope-Tree → Debug-Panel → Polish。