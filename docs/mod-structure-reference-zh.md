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
|  |- Defs/                         # US_ 前缀 Def（后续）
|  |- Patches/                      # US 事件/装配 patch（后续）
|  `- Languages/
|     |- English/Keyed/
|     `- ChineseSimplified/Keyed/
|- Source/UniversalSqueaker/        # 未来主程序集（net472）
|- Kernel/                          # 零 Verse 内核编译集（已迁移，待去 SR 化）
|- Pure/                            # 执行层纯逻辑（已迁移）
|- tools/                           # harness（KernelCharacterization 等）
|- fixtures/                        # 语料与期望文件（byte-stable）
|- scripts/                         # build/pack/verify（与 SR 同流程，后续迁移）
|- .github/workflows/               # CI/Release（与 SR 同流程，后续迁移）
|- docs/
|  |- mod-structure-reference-zh.md # 本文件
|  `- release-runbook-zh.md         # 与 SR 同一套发布流程（已迁移）
|- AGENTS.md / MEMORY.md / TODO.md / HANDOFF.md / README.md
```

与 SR 的差异红线：

- **没有** `Extras/SqueakyRatkinExampleVoices`（内容示例属于 SR/VoicePack 侧）。
- **没有** `1.6/Sounds/coahuilite.squeakyratkin/...` 内置音频镜像；US 永远不随包分发音频种子。
- 仓库内不允许出现 `SqueakyRatkin.*` 类型、`SR_` Def 前缀、Ratkin 装配/profile/attachment（0.4 共存规则）。
- `dist/`、`1.6/Assemblies/*.dll|*.pdb` 为 gitignored 构建态。

## 快速创建清单（新仓库或重建时）

1. 根目录：`About/`、`LoadFolders.xml`、`1.6/`、`Source/`、`docs/`、`scripts/`、`tools/`、`.github/workflows/`。
2. `About/About.xml`：packageId 固定 `coahuilite.universalsqueaker`；modVersion 与 csproj `<Version>` 一致（当前占位 0.1.0）；显示名/许可待定。
3. `LoadFolders.xml`：`<li>/</li>` 与 `<li>1.6</li>`；**不得**加 Ratkin/任何内容包的 `IfModActive` 门控。
4. `1.6/`：四个子目录 `Assemblies/Defs/Patches/Languages`，除 Assemblies 构建态外都保留 `.gitkeep` 占位。
5. `.gitignore`：`dist/`、`About/PublishedFileId.txt`、`*.dll`、`*.pdb`、`bin/`、`obj/`、`.slim/`。
6. 发布流程：直接沿用 `docs/release-runbook-zh.md`；脚本与 CI 从 SR 仓库迁移时按同一套 stage/pack 纪律执行。

## 下一步使用

- 建立 `Source/UniversalSqueaker/UniversalSqueaker.csproj` 时，把 `1.6/Assemblies/.gitkeep` 替换为构建输出路径。
- 首次 `About.xml` 定稿前，维护者确认 Workshop 显示名与许可，然后同步 `docs/release-runbook-zh.md` 与页面文案。
