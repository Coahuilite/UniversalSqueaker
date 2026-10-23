# Universal Squeaker

0.6 r2：[当前作者／配置合同](docs/us-kernel-0.6-refactor-zh.md)采用完整中立Core、同步单owner及明确通知声明。普通政策、原生与人工接受仍待完成。

[English](./README.md) | **中文**

RimWorld 1.6 通用语音包路由内核，源自 Squeaky Ratkin，采用 MPL-2.0 许可。

US 把第三方语音接入准确的种族/异种域，提供分层调音、心情调制、彩蛋资格、摄像机高度衰减与诊断。US 不分发自己的音频文件；当前仓库包含 Ratkin 的最终回退映射，引用原版 Core 音频。

独立设置窗口有五个工作区：**总览 / 音频包 / 规则与调制 / 预设导入 / 高级与诊断**，并有可展开的底部帮助面板。

## 运行要求与身份

- RimWorld 1.6。
- 前置模组 **FerriteLib**（`coahuilite.ferritelib`）。US 引用其 DLL，不随包复制；支持的 API 区间在代码中检查。
- packageId：`coahuilite.universalsqueaker`。
- 命名空间 / Def 前缀 / 诊断前缀：`UniversalSqueaker` / `US_` / `usdiag`。
- 许可：[MPL-2.0](LICENSE)。

当前Dev显式消费固定FerriteLib **0.8**包，范围`[0.8.0,0.9.0)`；构建输入、保留的界面行为与原生待验边界见[消费者迁移说明](docs/fl08-consumer-migration-zh.md)。

当前分支为 **0.6.0 Dev**：[全部15包与已裁Select三项可调资格](docs/us-kernel-0.6-spec-zh.md#23-d-c-十五包select资格与明确事实边界)已接入同一生产owner，含可加载作者示例和逐包SR导读；普通范围／政策、原生观察与人工验收继续分开记录。

## 文档

从 [文档总入口](docs/README.md) 开始。这里区分 **保留0.5.x指南**与 **当前0.6.x接口及候选**。

- [当前功能与设置](docs/us-kernel-0.6-refactor-zh.md)
- [当前0.6 r2作者指南](docs/us-kernel-0.6-authoring-zh.md)
- [Kernel 0.6.x 首版规格](docs/us-kernel-0.6-spec-zh.md)
- [构建与验证](docs/build-and-debug.md)
- [发布流程](docs/release-runbook-zh.md)与[隐私门禁](docs/push-privacy-gate.md)

当前产品声明为 0.6.0。文档描述本仓库源码，可能包含尚未进入旧下载版本的修改。开发工作约束仍在 `AGENTS.md`，持久状态与待办分别在 `MEMORY.md` 和 `TODO.md`。
