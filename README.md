# Universal Squeaker

RimWorld 1.6 通用语音包内核（本地分叉，未发布）。

本仓库是 Squeaky Ratkin 的 **Universal Squeaker 本地分叉**。当前阶段只做本地整理与评估，不配置 remote、不 push、不发布。

- 交接与 UI 改造评估：`HANDOFF.md`
- 记忆协定：`AGENTS.md`、`MEMORY.md`
- 当前行动面：`TODO.md`

## 迁移后的本地验证

```powershell
dotnet run --project tools/KernelCharacterization -c Release
```

> 说明：迁移快照仍保留 SR 命名空间与 SR 参考内容（`sr_reference/`），仅用于验证迁移完整性；US 通用化第一阶段会清除产品字面量并重建基线。

## 身份

- packageId：`coahuilite.universalsqueaker`（已确认）
- 目标命名空间/前缀/日志：`UniversalSqueaker` / `US_` / `usdiag`
- Workshop 显示名与许可：待维护者确认
