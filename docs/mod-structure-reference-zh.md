# Universal Squeaker 模组结构参考

> 用途：指导本仓库快速创建/校验 RimWorld 1.6 模组结构。US 采用与 Squeaky Ratkin（SR）相同的发布流程（见 `docs/release-runbook-zh.md`），但**不含 Extras 内容包、不含内置音频**——音频全部来自独立 VoicePack。

## 来源

- RimWorld Wiki 模组教程总入口：<https://rimworldwiki.com/wiki/Modding_Tutorials>
- 官方模组目录结构页：<https://rimworldwiki.com/wiki/Modding_Tutorials/Folder_structure> 与 <https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_folder_structure>
- 本仓库结构基准 = SR 仓库去掉 `Extras/` 与 `1.6/Sounds/` 内置音频镜像后的基本结构（SR 的 `dist/`、staged `1.6/Sounds/` 均为构建态，不进仓库）。

## RimWorld 模组最小目录事实（1.6）

- 模组根目录含 `About/About.xml`（`ModMetaData`：packageId、name、author、supportedVersions、modDependencies、loadAfter 等）。
- 根 `LoadFolders.xml` 声明加载哪些版本目录；1.6 常用：
  ```xml
  <loadFolders><v1.6><li>/</li><li>1.6</li></v1.6></loadFolders>
  ```
- `1.6/` 下按内容类型分目录：`Defs/`、`Patches/`、`Languages/<lang>/Keyed/`、`Assemblies/`（DLL）。
- 可选内容目录（US 不用）：`Textures/`、`Sounds/` 等；音频包内容属于独立 VoicePack 模组，不放在 US 本体。

## US 仓库目标结构

```text
UniversalSqueaker/
|- About/About.xml                  # packageId coahuilite.universalsqueaker；显示名/许可待定
|- LoadFolders.xml                  # / + 1.6 无条件加载
|- 1.6/
|  |- Assemblies/                   # 构建产物（DLL，gitignored；仓库内仅 .gitkeep）
|  |- Defs/                         # 内容包注入 US_ Defs（本体无种子 Def；保留 .gitkeep）
|  |- Patches/                      # 生产 patch 在 C#；本目录无种族硬编码 patch（.gitkeep）
|  `- Languages/
|     |- English/Keyed/UniversalSqueaker.xml
|     `- ChineseSimplified/Keyed/UniversalSqueaker.xml
|- Source/UniversalSqueaker/        # 主程序集（net472，0.1.0-dev）
|  |- Kernel/                       # 零 Verse 内核编译集（去 SR 化）
|  |- Pure/                         # 漏斗纯逻辑（零 Verse）
|  |- Catalog/ Fallback/ Models/ Settings/ Labels/ Logging/ Patches/ Runtime/ Diagnostics/ UI/
|- tools/
|  |- UniversalSqueakerKernelTests/     # 纯度门 + 单测 + US 0.1.0 语料（fixtures 在工具内）
|  |- UniversalSqueakerConfigCopyTests/ # fallback profile Config 副本生命周期门
|  `- UniversalSqueakerLogTests/        # usdiag 日志协议 v1/v2 门
|- scripts/                         # build/pack/verify（与 SR 同流程，后续迁移）
|- .github/workflows/               # CI/Release（与 SR 同流程，后续迁移）
|- docs/
|  |- mod-structure-reference-zh.md # 本文件
|  |- release-runbook-zh.md         # 与 SR 同一套发布流程（首次发布前 US 文案适配）
|  |- ui-componentization-evaluation-zh.md
|  `- ui-phase3-implementation-notes-zh.md
|- AGENTS.md / MEMORY.md / TODO.md / OBLIVIONIS.md / HANDOFF.md / README.md
```

> 重建后 `Kernel/`、`Pure/`、`fixtures/`、`sr_reference/`、旧 `tools/KernelCharacterization/` 已不在工作树中（用后即删，git 历史可查，见 `OBLIVIONIS.md`）。

与 SR 的差异红线：

- **没有** `Extras/SqueakyRatkinExampleVoices`（内容示例属于 SR/VoicePack 侧）。
- **没有** `1.6/Sounds/coahuilite.squeakyratkin/...` 内置音频镜像；US 永远不随包分发音频种子。
- 仓库内不允许出现 `SqueakyRatkin.*` 类型、`SR_` Def 前缀、Ratkin 装配/profile/attachment（0.4 共存规则）。唯一例外（维护者授权 2026-08-24）：legacy 兼容桥的薄空类 `SqueakyRatkin.SqueakVoicePackDef`，旧 SR 包经此加载时必须在日志与 UI 显式标记为旧 SR 内容。
- `dist/`、`1.6/Assemblies/*.dll|*.pdb` 为 gitignored 构建态。

## 快速创建清单（新仓库或重建时）

1. 根目录：`About/`、`LoadFolders.xml`、`1.6/`、`Source/`、`docs/`、`scripts/`、`tools/`、`.github/workflows/`。
2. `About/About.xml`：packageId 固定 `coahuilite.universalsqueaker`；modVersion 与 csproj `<Version>` 一致（当前 0.1.0-dev）；显示名/许可待定。
3. `LoadFolders.xml`：`<li>/</li>` 与 `<li>1.6</li>`；**不得**加 Ratkin/任何内容包的 `IfModActive` 门控。
4. `1.6/`：四个子目录 `Assemblies/Defs/Patches/Languages`；Languages 已有 Keyed XML，其余保留 `.gitkeep` 占位。
5. `.gitignore`：`dist/`、`About/PublishedFileId.txt`、`*.dll`、`*.pdb`、`bin/`、`obj/`、`.slim/`。
6. 发布流程：直接沿用 `docs/release-runbook-zh.md`；脚本与 CI 从 SR 仓库迁移时按同一套 stage/pack 纪律执行。

## 下一步使用

- `Source/UniversalSqueaker/UniversalSqueaker.csproj` 已建立（Version 0.1.0-dev 主源）；`1.6/Assemblies/` 为构建输出路径。
- 首次 `About.xml` 定稿前，维护者确认 Workshop 显示名与许可，然后同步 `docs/release-runbook-zh.md` 与页面文案。
