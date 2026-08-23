# AGENTS.md — Universal Squeaker

> 本文件是 Universal Squeaker 仓库的 AI agent 记忆协定。人类开发者请阅读 `README.md` 与 `HANDOFF.md`。

## Project identity

- Project: RimWorld 1.6 mod **Universal Squeaker**（本地分叉，尚未配置远程）。
- Confirmed identity: repo `coahuilite/UniversalSqueaker`，permanent `packageId` `coahuilite.universalsqueaker`，C# 目标命名空间 `UniversalSqueaker`，Def/日志/调试键前缀 `US_`，日志协议前缀 `usdiag`。Workshop 显示名与许可待维护者最终确认；本仓库未发布。
- Squeaky Ratkin（`coahuilite.squeakyratkin`）是独立产品，其品牌、packageId、命名空间与 `SR_` 前缀不得在本仓库中复制或占用。0.4 共存期间 US 不得定义任何 `SqueakyRatkin.*` 类型，不得含 Ratkin 装配/profile/attachment。
- 产品版本主源：未来建立 csproj 后以 csproj `<Version>` 为唯一主源；当前本地分叉尚未有产品程序集。

## Project philosophy

- **路由中立**：VoicePack 的 `raceDefName` 声明是唯一路由入口；不内置任何种族特判。HAR races、Vanilla Human、任意第三方种族路由一致。
- **依赖反射**：HAR 等可选依赖只经反射发现；缺失时静默降级，绝不崩溃。
- **卸载安全是硬规则**：移除 mod 不得影响存档。不在 save 中写永久数据（defs 运行时注入；设置与 profile 在 Config 目录）。
- **内容与内核分离**：内核编译集不得引用 Verse/Unity/RimWorld，不得含产品音键/种族种子；`SR_*`/Ratkin 种子属于 SR 内容仓库，US 提供数据驱动的 fallback profile 机制。

## Memory protocol

- 会话前读 `MEMORY.md`；继续工作前读 `TODO.md`。
- `MEMORY.md` 只保存耐久事实与当前行动面；`TODO.md` 只保存当前目标、开放行动、阻塞与明确延后。
- 已完成实现细节进 `docs/`（handoff/review），不写入 MEMORY/TODO。
- 本地分叉状态与 SR 上游状态分开记录：SR 证据只能从 SR 仓库取证，不得凭本仓库推断。

## Privacy and security

- 默认范围 = 本仓库根。向外读取需逐路径授权，不扩大范围。
- 不把个人本地路径、日志摘录、凭据、token、`PublishedFileId.txt` 写进 Git 或文档。写作时即无隐私，不做事后清洗。
- 每次 push 前对完整可达范围做隐私审查（当前本地仓库无 remote；配置 remote 或任何 push 均为外部操作，需维护者明确授权）。
- 本仓库是 SR 的本地分叉：SR 仓库的历史/tag 清理等破坏性操作不在本仓库执行。

## External-state boundaries

- 提交仅限本地：`git commit` 本地可用；`git remote`/`push`/PR/发布均为外部操作，需维护者明确授权。
- 不得声称本仓库证据能证明 SR 或 GitHub/Steam 的外部状态。
