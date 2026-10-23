# Universal Squeaker 仓库与安装包结构

当前声明：US **0.6.0 Dev**，RimWorld **1.6**，packageId **`coahuilite.universalsqueaker`**，许可 **MPL-2.0**。版本以 `Source/UniversalSqueaker/UniversalSqueaker.csproj` 为主源，`About/About.xml` 同步。

## 仓库中的权威入口

```text
UniversalSqueaker/
├─ About/About.xml
├─ LoadFolders.xml
├─ 1.6/
│  ├─ Assemblies/                 # 本地构建态，不是打包的载荷来源
│  ├─ Defs/                       # Ratkin 最终回退表 + Core 素材引用
│  ├─ Patches/
│  └─ Languages/
├─ Source/UniversalSqueaker/
│  ├─ Core/                       # 完整中立定义/配置/选音/决策/会话
│  ├─ Game/                       # 原生事实/输出接入/生命周期
│  └─ Catalog/ Runtime/ Models/ Settings/ Fallback/ UI/ ...
├─ tools/                        # Kernel、迁移、日志、UI 与 Host 等测试
├─ scripts/                      # 构建、载体选择、打包、验证、隐私
├─ .github/workflows/
├─ docs/README.md
└─ LICENSE
```

旧Kernel/Pure源码已迁入完整Core或实际adapter，不保第二条计划链。

US 不带自己的音频文件，不带 Extras 音频包，也不复制 FerriteLib DLL。当前 Ratkin fallback XML 是数据层的原版音频引用，不是 Kernel 中的种族特判。旧 SR C# 类型桥接不在当前产品中；音频包迁移见 [作者指南](us-kernel-0.6-authoring-zh.md)。

## 安装包由脚本构造

`scripts/stage-package.ps1` 从所选构建载荷与内容根构造暂存候选，校验后替换交付目录：

```text
UniversalSqueaker/
├─ About/About.xml
├─ LoadFolders.xml
├─ LICENSE
├─ version.txt
└─ 1.6/
   ├─ Assemblies/UniversalSqueaker.dll
   └─ Defs/、Languages/ 等当前内容
```

- `About` 只复制 `About.xml`，不复制 Workshop 标识文件。
- `version.txt` 记录产品版本、build、commit、carrier、carrier-sha256 与源码地址，不是旧稿所说的固定三行。
- `*.pdb`、`*.gitkeep`、`PublishedFileId.txt`、`codemap.md`、`Extras/`、`1.6/Sounds/` 和第二份 FerriteLib 载荷不应进入包。
- 当前 staging 不复制 `docs/` 或根 README。本次交付属于仓库文档面；以后若把文档装进 mod ZIP，需同步打包范围与包验收。

更多操作见 [构建指南](build-and-debug.md) 与 [发布流程](release-runbook-zh.md)。