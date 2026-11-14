# Universal Squeaker

> **当前开发线（0.6.x）：** 采用FL0.9，分别构建RW1.6/net472与RW1.7/net10，见[现行构建入口](docs/build-and-debug.md)。下文已发布0.8配对与历史1.6证据保原范围；1.7真实游戏验收待完成。

[English](./README.md) | **中文**

Universal Squeaker（US）让 RimWorld 的角色按**准确的种族与异种**，说出第三方语音包里的声音。
它是语音包的路由内核：自身不带音频文件，负责决定“谁、在什么时机、用哪个包里的哪条声音、以什么方式”发声。
源自 Squeaky Ratkin，采用 MPL-2.0 许可。当前仓库包含一张 Ratkin 最终回退映射，引用的是原版 Core 音频。

## 当前状态

| 项目 | 状态 |
| --- | --- |
| 版本 | 0.6.0 |
| 发布 | v0.6.0-rc1 准备中，尚未发布 |
| RimWorld 1.6 | 主要目标 |
| RimWorld 1.7 | 基础兼容候选，须与同批 FerriteLib 一起使用；声音和界面尚未经实机验收 |
| 前置 | Harmony、FerriteLib 0.8.x |

发布范围与升级注意见[发布说明](docs/releases/v0.6.0-rc1.md)；已知偏差与证据范围见[开放问题](docs/development/specification/open-issues.md)。

## 给玩家

### 安装与更新

1. 安装 **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)**（`brrainz.harmony`）和
   **FerriteLib**（`coahuilite.ferritelib`，0.8.x），并排在 US 之前。两者都单独安装，不在 US 压缩包内。
2. 安装至少一个语音包模组。US 本身不发声。
3. 更新时完全退出游戏，把旧的 US 目录整个移出 Mods，再放入新的完整目录。不要只覆盖 DLL。
   **保持原来的目录名**即可沿用原有设置；无需清理 Config 或存档。

### 打开设置

三个入口打开同一个窗口：启动主菜单、原版“选项”的类别栏中的 **US 语音**、原版“模组设置”。两个新增入口可分别隐藏。

设置窗口可以：

- 为每个种族或异种选择启用哪些语音包。默认列出类人种族与合法异种，动物需主动加入；可按类别、关键词和 Def 来源筛选。
- 按动作调整：选中、征召、攻击、进食等十五个官方动作，各有作用范围与参数。
- 编辑最终回退表：某个种族没有合适语音包时用什么声音。
- 切换外观：原版深色、Royalty、黑金。
- 查看依赖状态并录制诊断反馈。

### 反馈声音问题

在地图中打开 **设置与工具 → 诊断与依赖 → 打开回应诊断**，正常关闭设置窗，在诊断条上点 **展开**，选中或锁定角色，
然后 **开始新记录 → 正常复现 → 停止记录 → 复制反馈**，把复制内容连同问题描述一起贴出。
不需要开发者模式或详细日志。记录保留最近 64 条；退出或重新载入游戏会清除。

## 它是怎么工作的

一句话：**游戏事件 → 每个角色一个回应 owner 决定是否发声 → 从该角色准确域的语音包选音 → 调制 → 原生播放。**
决策核心（`Core/`）不引用游戏，只接收“事实”、给出“决定”；所有和游戏打交道的代码都在适配层。

```text
原版事件（Harmony 补丁、观察）    语音包与动作 Def（XML）     玩家设置
          │                            │                        │
          ▼                            ▼                        ▼
 SqueakerRuntime（1.6 适配）     DefinitionCatalog         GameSettings → 配置快照
          │ 事实                       │ 声明                     │
          └─────────────► ResponseSession（每个角色一个，串行）◄────┘
                                 │  ResponseDecision：机会、条件、冷却、结算
                                 ▼
                           AudioCatalog：按准确的种族＋异种域和选择模式选音
                                 ▼
                           局部调制：音量、音高等
                                 ▼
                 UsNativeAudioPool / UsInstanceOutput → 游戏原生声音
```

| 概念 | 含义 |
| --- | --- |
| 域 | 种族＋异种的精确组合。语音包只对它声明的域生效，不按名字猜测 |
| 动作 | 十五个官方动作及作者自定义动作，各有自己的条件与参数 |
| 机会 | 一个合法的发声时机。玩家操作（选中、征召）用真实秒，模拟事件用游戏 tick |
| 回应 owner | 每个角色一个 `ResponseSession`，串行完成决定、冷却与结算，避免同一角色重复发声 |
| 最终回退 | 某个域没有可用语音包时的兜底映射 |

**设置界面**用 FerriteLib.UiKit 搭建：`UI/Layout.Schema2.xml` 定义页面结构 → `UsKernelSettingsHost` 注册控件并建立绑定 →
`IUsKernelSettingsSource` 是界面与业务之间唯一的边界。界面只发请求，是否保存、何时生效由业务决定。

### 源码地图（`Source/UniversalSqueaker/`）

| 目录 | 内容 |
| --- | --- |
| `Core/` | 不依赖游戏的决策核心：`Definitions` 声明、`Configuration` 行为配置、`Responses` 机会与结算、`Audio` 目录、选择与调制 |
| `Game/` | 1.6 适配：运行时、启动、各类原版事件观察、设置持久化 |
| `Runtime/` | Comp、内核接缝、原生音频池与输出 |
| `Patches/` | Harmony 补丁：事件入口与设置入口 |
| `Catalog/` | 种族、异种与 Def 身份收集 |
| `Settings/` | 玩家设置的读写、迁移、重置与预设导入 |
| `Logging/`、`Diagnostics/` | 日志出口、诊断录制与反馈 |
| `UI/` | 设置窗口：页面定义、Host、控件与视图模型 |
| `tools/`（仓库根） | 测试与夹具 |

## 给语音包作者

- [作者指南](docs/us-kernel-0.6-authoring-zh.md)与[可加载示例](docs/examples/0.6/)
- 规格中的[音频选择](docs/openspec/specs/audio-selection/spec.md)与[十五个官方动作](docs/openspec/specs/official-actions/spec.md)
- 从旧版本迁移：[0.6 升级说明](docs/upgrade-0.6-zh.md)

## 给贡献者

构建需要一份固定的 FerriteLib 载体 DLL：

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath '<FerriteLib包>/1.6/Assemblies/FerriteLib.UiKit.dll'
pwsh -NoProfile -File scripts/build-dev.ps1 -FerriteLibArtifactPath '<FerriteLib包>/1.6/Assemblies/FerriteLib.UiKit.dll'
```

| 文档 | 用途 |
| --- | --- |
| [docs/specification.md](docs/specification.md) | 现行规格入口，行为保证的唯一权威 |
| [docs/openspec/specs/](docs/openspec/specs/) | 按能力拆分的规格正文 |
| [docs/development/specification/open-issues.md](docs/development/specification/open-issues.md) | 实现偏差与证据状态 |
| [docs/build-and-debug.md](docs/build-and-debug.md) | 构建、门禁与诊断 |
| [docs/fl08-consumer-migration-zh.md](docs/fl08-consumer-migration-zh.md) | 如何消费固定的 FerriteLib 0.8 |
| [docs/release-runbook-zh.md](docs/release-runbook-zh.md)、[docs/push-privacy-gate.md](docs/push-privacy-gate.md) | 发布流程与隐私门禁 |
| [docs/README.md](docs/README.md) | 完整文档目录与历史 |
| `AGENTS.md`、`MEMORY.md`、`TODO.md` | 开发代理的工作规则、持久事实与待办，不是用户文档 |

## 身份

- packageId：`coahuilite.universalsqueaker`
- 命名空间 / Def 前缀 / 诊断前缀：`UniversalSqueaker` / `US_` / `usdiag`
- FerriteLib API 区间在启动时检查：`[0.8.0, 0.9.0)`；US 不随包携带 FerriteLib 或 `0Harmony.dll`
- 许可：[MPL-2.0](LICENSE)
