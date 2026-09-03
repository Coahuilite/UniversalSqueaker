# Universal Squeaker

RimWorld 1.6 通用语音包内核（本地分叉，未发布）。

本仓库是 Squeaky Ratkin 的 **Universal Squeaker 本地分叉**。当前阶段只做本地整理、重建与评估，不配置 remote、不 push、不发布。

- 交接与 UI 改造评估：`HANDOFF.md`
- 记忆协定：`AGENTS.md`、`MEMORY.md`
- 冷档案：`OBLIVIONIS.md`
- 当前行动面：`TODO.md`

## 本地验证

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1          # 12 道门禁（6 个 harness + 主程序集 Dev/Release + 载体载荷存在与单载体红线 + Schema=2 清单校验；含双语 Keyed 本地化契约与文本适配扫描）
pwsh -NoProfile -File scripts/build-dev.ps1             # 先建 ../ferritelib 载荷，再 Dev 构建 + 打 dev 包（dist/dev/）

# UI 库已拆为独立前置模组仓库 ../ferritelib（coahuilite.ferritelib），有自己的一套门禁。
# US 只按相对路径引用它编译出来的 DLL，不再随包携带。
pwsh -NoProfile -File scripts/build-dev.ps1             # Dev 构建 + 打 dev 包（dist/dev/）
dotnet run --project tools/UniversalSqueakerKernelTests -c Release
```

## 身份

- packageId：`coahuilite.universalsqueaker`（已确认）
- 命名空间/前缀/日志：`UniversalSqueaker` / `US_` / `usdiag`
- Workshop 显示名与许可：待维护者确认

- 模组结构参考：docs/mod-structure-reference-zh.md
- UI 组件化评估与实现笔记（历史资料：描述已于 2026-09-02 删除的旧 UI 实现）：docs/ui-componentization-evaluation-zh.md、docs/ui-phase3-implementation-notes-zh.md
- 发布流程：docs/release-runbook-zh.md（与 SR 同一套，首次发布前需 US 文案适配）
