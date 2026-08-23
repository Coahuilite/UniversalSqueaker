# MEMORY

## 当前耐久状态
- 本仓库是 Squeaky Ratkin（SR）的 **Universal Squeaker（US）本地分叉**，2026-08-23 建立；**无 remote、未发布**。
- 分叉源 = SR `0.3.x` 分支 tip `b19d68a`（0.3.2-pre1 发布后的 `.slim` 清理提交，提交链 C34–C42）。
- 已确认身份：repo=`coahuilite/UniversalSqueaker`、packageId=`coahuilite.universalsqueaker`、命名空间/前缀/日志 = `UniversalSqueaker`/`US_`/`usdiag`；Workshop 显示名与许可待最终确认。
- 0.4 共存策略（已定案）：SR 独占 Ratkin、US 只服务其他种族、可同时启用、不写 `incompatibleWith`；US 0.4 不含任何 Ratkin 装配/profile/attachment。
- SR 1.0.0 时 SR 收缩为纯音频包、US 成为前置；legacy 桥离线原型在 SR 仓库（`tools/LegacyBridgePrototype`/`LegacyBridgeHarness`），US 接管版本再启用。

## 权威入口
- 分叉交接与 UI 改造评估：`HANDOFF.md`（本次分叉的单一入口）。
- 内核编译集：`Kernel/`（零 Verse 编译集，当前仍为 SR 快照：命名空间/种子未迁移）。
- 内核 harness：`tools/KernelCharacterization/`（已改链本地 `Kernel/`+`Pure/`，fixtures 与 SR 参考快照已迁移）。
- SR 权威上游（只读证据源）：`<workspace>\squeaky_ratkin`（不写入、不凭本仓库推断其外部状态）。

## 工程决定与交接
- **本分叉只做本地**：不配置 remote、不 push；`git init` 后的本地提交是唯一允许的 git 操作，直到维护者授权。
- **迁移清单（2026-08-23）**：`Kernel/*.cs`（9 文件）、`Pure/SqueakActionPlan.cs`+`SqueakTimingModel.cs`、`tools/KernelCharacterization/*`（7 文件 + fixtures + `sr_reference/`）、记忆协定（AGENTS/MEMORY/TODO/HANDOFF/README）。
- **当前技术债（继承自 SR 快照）**：内核/纯文件命名空间仍是 `SqueakyRatkin*`；`BuiltInFallbackTable` 含 Ratkin 种子与 `SR_*` 音键；harness 的 `ActionAudioKeyMirror`/五处同步仍以 SR 内容为参照。这些必须在 US 通用化第一阶段清除。
- **目标基线**：内核零产品字面量（race/音键/前缀均数据注入）；UI 只需语音包分配可用，其余页面允许摘除但不得崩溃；全部种族平等路由，不对 Ratkin 特判。

## 结构参考
- 模组结构参考：[docs/mod-structure-reference-zh.md](./docs/mod-structure-reference-zh.md)（RimWorld Wiki + SR 去 Extras/内置音频基线）。
- 发布流程：与 SR 同一套，已迁移 [docs/release-runbook-zh.md](./docs/release-runbook-zh.md)。
- 仓库骨架已按参考建立：About/、LoadFolders.xml、1.6/{Assemblies,Defs,Patches,Languages}、Source/UniversalSqueaker/、scripts/、.github/workflows/（占位）。
