# 打包前可用性检查清单

> 目标：在任何 `pack-dev` / 发布打包前，确认模组可以安全、完整、可加载地进入分发物。
> 自动化脚本：`scripts/check-pack-readiness.ps1`（未实现则本清单为人工检查）。

## A. 版本与身份

- [ ] `Source/UniversalSqueaker/UniversalSqueaker.csproj` `<Version>` 存在且非空。
- [ ] `About/About.xml` `<modVersion>` 与 csproj `<Version>` **完全一致**（含 `-dev` 后缀）。
- [ ] `<packageId>` = `coahuilite.universalsqueaker`。
- [ ] `<name>` / `<author>` / `<supportedVersions>` 有效。
- [ ] 发布包（非 dev）不得保留 placeholder description / 未确认 display name / license。

## B. 构建与门禁

- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev` 零警告。
- [ ] `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release` 零警告。
- [ ] `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev` 零警告。
- [ ] `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release` 零警告。
- [ ] `pwsh -File scripts/verify-local.ps1` 全绿（14 门）。
- [ ] FerriteLib.UiKit.Tests ALL PASS。

## C. 程序集与依赖

- [ ] `1.6/Assemblies/UniversalSqueaker.dll` 存在。
- [ ] `1.6/Assemblies/FerriteLib.UiKit.dll` 存在。
- [ ] 程序集目录没有 `.pdb`、旧版本/无关 DLL、测试桩 DLL。
- [ ] 程序集引用可解析（Krafs.Rimworld.Ref、Lib.Harmony 为编译期/游戏侧依赖，不随包分发）。

## D. 目录与内容

- [ ] 包内结构为 `About/`、`LoadFolders.xml`、`1.6/`。
- [ ] 没有 `Extras/`、`1.6/Sounds/`（US 红线）。
- [ ] 没有 `About/PublishedFileId.txt`。
- [ ] 没有 `.git`、`obj/`、`bin/`、`dist/`、`docs/`、`scripts/`、`tools/`、`Source/`。
- [ ] 没有 `.pdb`、`.gitkeep`、`codemap.md`。
- [ ] `1.6/Defs/` 为空（当前合法）或其中 XML 全部 well-formed。
- [ ] `1.6/Languages/*/Keyed/UniversalSqueaker.xml` 存在且 well-formed。
- [ ] `1.6/Patches/` 若存在，XML 全部 well-formed。
- [ ] `LoadFolders.xml` well-formed 且路径正确。

## E. 隐私与中性

- [ ] 包内无个人绝对路径、凭据、API key、token、`PublishedFileId`。
- [ ] 包内无 `SqueakyRatkin.*` 类型、Ratkin 内容、`SR_` 前缀产物。
- [ ] `Source/FerriteLib.UiKit` 中性 grep 无 US/SR 产品字面量。

## F. 运行时/加载

- [ ] 内嵌 `UI/Layout.xml` well-formed（verify-local 已覆盖）。
- [ ] 无对已删除类型/方法的引用（构建通过即基本保证）。
- [ ] 可选依赖（HAR 等）走反射，缺失时静默降级。

## G. 分发元数据

- [ ] `version.txt` 内容为 `UniversalSqueaker <version>` + build/commit 信息。
- [ ] dev 包使用 `-dirty` 标记（如果工作树脏）。
- [ ] 包 zip 命名正确：`UniversalSqueaker-<flavor>-v<version>-<commit>.zip`。

## H. 最终人工/游戏内（maintainer）

- [ ] 游戏内 Mod 列表无红字。
- [ ] 设置页可打开，Ferrite UI 正常渲染。
- [ ] 全局音量/衰减/页签/过滤/帮助可交互。
- [ ] 触发一次 Ferrite fallback 后 Vanilla 页仍可用。
- [ ] 存档可正常读写，无残留脏数据。
