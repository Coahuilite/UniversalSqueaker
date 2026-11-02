# Universal Squeaker

已接受的选择现使用0.3真实秒，征召／解除征召各0.6真实秒；模拟机会仍用游戏tick。新版显式机会合同拒绝缺失事实和废弃计时字段，不回退旧路径；见[间隔与升级说明](docs/openspec/specs/response-opportunities/spec.md)。

当前准备 **v0.6.0-rc1**，RimWorld1.6预发布、非Latest。ea610fa＋FL833c14f4已获整体内部试玩接受；原生细项保留已知限制。见[发布与升级说明](docs/releases/v0.6.0-rc1.md)。本地新固定输入不冒称已发布RC2。

本地 RimWorld 1.7 基础兼容候选须与同次配对的 FerriteLib 一起使用；由于尚未进行实机观察，声音与渲染仍待人工验收。原 1.6 入口与内容继续保留，当前证据和限制见[基础兼容记录](docs/development/specification/open-issues.md#rw17-基础兼容候选)。

0.6：[现行规格](docs/specification.md)采用完整中立Core、同步单owner及明确通知声明。普通回应现于启动就绪时自动装配，无需Dev；原生与人工接受仍待完成。

[English](./README.md) | **中文**

RimWorld 通用语音包路由内核，源自 Squeaky Ratkin，采用 MPL-2.0 许可。

US 把第三方语音接入准确的种族/异种域，提供分层调音、心情调制、彩蛋资格、摄像机高度衰减与诊断。US 不分发自己的音频文件；当前仓库包含 Ratkin 的最终回退映射，引用原版 Core 音频。

从启动主菜单、原版设置类别栏的 **US 语音** 或原 Mod Settings 进入同一窗口。默认“语音管理”集中模式、音量、搜索与精确目标包开关；“设置与工具”直达本窗口上次工具，同级标签等宽直切；显式帮助仅按需展开在业务正文下方。外观从单一嵌入数据选择原版深色、Royalty 或黑金。两新增入口可独立隐藏；宽窄编排与原生待验边界见[当前 UI 合同](docs/openspec/specs/ui-consumption/spec.md)。

定义目录默认类人种族与合法异种，动物须主动选择；类别、关键词和真实Def来源共同筛选，来源可搜索翻页，结果完整可达。已有回退表、新建Race候选与Core声音各保独立浏览状态；选候选不自动创建。1024×768逻辑屏幕使用居中864×640窗口，不额外缩放；原版设置入口带US麦克风图标。

## 反馈声音问题

反馈声音问题：在地图内打开 **设置与工具 → 诊断与依赖 → 打开回应诊断**，正常关闭设置窗，点击诊断条的 **展开**，选中或锁定对象后 **开始新记录 → 正常复现 → 停止记录 → 复制反馈**。无需 Dev 或详细日志；仅开窗不录制，保最近 64 条并标覆盖／缺失。关闭主窗会停止记录，同一游戏重开可复制；退出游戏或重新载入会清除。粘贴反馈并描述实际问题即可，不必找配置或原始日志。

## 运行要求与身份

- 本候选用于 RimWorld 1.6 或 1.7.4990 rev1069；1.7 声音与渲染仍待实机验收。
- 前置模组 **[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)**（`brrainz.harmony`），独立安装；US 不内嵌 `0Harmony.dll`。
- 前置模组 **FerriteLib**（`coahuilite.ferritelib`）。US 引用其 DLL，不随包复制；支持的 API 区间在代码中检查。
- packageId：`coahuilite.universalsqueaker`。
- 命名空间 / Def 前缀 / 诊断前缀：`UniversalSqueaker` / `US_` / `usdiag`。
- 许可：[MPL-2.0](LICENSE)。

安装并启用 Harmony 与配对 FerriteLib，将它们排在 Universal Squeaker 之前。两个前置均需独立安装，不包含在 US 压缩包内。

当前构建显式消费固定FerriteLib **0.8**包，范围`[0.8.0,0.9.0)`；构建输入、保留的界面行为与原生待验边界见[消费者迁移说明](docs/fl08-consumer-migration-zh.md)。

当前分支准备 **0.6.0-rc1**：[全部15包与已裁Select三项可调资格](docs/openspec/specs/official-actions/spec.md)已接入同一生产owner，含可加载作者示例和逐包SR导读；普通默认已采用并列于规格；原生观察与十五包装人工验收仍待完成。

## 文档

[现行规格](docs/specification.md)是能力、连接、原版事实和普通默认的唯一权威入口。[当前偏差](docs/development/specification/open-issues.md)与合同分开，十五包逐项原生／人工门保实际证据范围。

- [作者指南与可加载示例](docs/us-kernel-0.6-authoring-zh.md)
- [升级与选定来源域恢复](docs/upgrade-0.6-zh.md)
- [构建与验证](docs/build-and-debug.md)
- [发布流程](docs/release-runbook-zh.md)与[隐私门禁](docs/push-privacy-gate.md)
- [文档目录与历史](docs/README.md)

当前产品声明为 0.6.0。文档描述本仓库源码，可能包含尚未进入旧下载版本的修改。开发工作约束仍在 `AGENTS.md`，持久状态与待办分别在 `MEMORY.md` 和 `TODO.md`。
