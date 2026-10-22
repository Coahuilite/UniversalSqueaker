# Universal Squeaker 文档

当前源码为 **0.6.0 开发分支**，目标 **RimWorld 1.6.4871**。统一生产owner及实例输出已有基础；D-A交付完整合同身份技术候选，Eat新分层／官方15包扩齐／发行默认／原生和逐包人审仍分阶段记录。

## 先找到你要做的事

| 读者 / 任务 | 入口 |
| --- | --- |
| 使用设置、理解模式或排查无声 | [当前功能与设置](current-behavior-0.5-zh.md) |
| 制作或迁移 XML 音频包 | [0.5.x VoicePack 作者指南](voicepack-authoring-0.5-zh.md) |
| 维护当前运行时与服务对象边界 | [当前架构与源码入口](architecture-0.5-zh.md) |
| 修改设置界面、主题或诊断交互 | [当前 UI 契约](ui-contract-0.5-zh.md) |
| 使用0.6受控S1、作者示例与验证边界 | [OneShot S1](us-kernel-0.6-s1-zh.md) |
| 核对0.6资源来源、绑定、作者入口与S2证据边界 | [来源与资源S2](us-kernel-0.6-s2-zh.md) |
| 查阅S3历史17动作／来源恢复技术交付 | [动作与配置恢复S3（历史范围）](us-kernel-0.6-s3-zh.md) |
| 使用当前0.6统一owner、响应作者格式与实例输出；核对未决默认 | [US Kernel 0.6.x 首版目标规格·按切片实现](us-kernel-0.6-spec-zh.md) |
| 为0.6实现准备合同用例、测试迁移及FL联调 | [合同与固定输入](us-kernel-0.6-acceptance-zh.md)、[测试迁移与显式Dev消费](us-kernel-0.6-test-migration-zh.md) |
| 组合独立根／继承扩展、完整ActionId与真实选音 | [主规格·当前作者入口 §21](us-kernel-0.6-spec-zh.md#21-d-a-当前作者入口) |
| 审阅首三包与SR体验差异 | [Select／Eat／Attack完整导读](us-kernel-0.6-contract-review-zh.md) |
| 维护适配及反例 | [事实—证据—反例](us-kernel-0.6-fact-adapters-zh.md) |
| 本地构建、选择 FerriteLib、验证改动 | [Build and debug](build-and-debug.md) |
| 核对仓库和安装包目录 | [模组结构](mod-structure-reference-zh.md) |
| 准备发布 | [发布流程](release-runbook-zh.md)、[隐私门禁](push-privacy-gate.md) |
| 了解旧设计为何失效、历史验证覆盖了什么 | [历史结论与证据边界](history/README.md) |

## 状态与证据怎么读

- **当前实现**来自本仓库源码、XML、脚本及项目声明。本次活动0.6文档核对日期为 2026-10-23；工作树可能包含未发布修改，不等同于某个下载版本。
- **目标规格**规定下一版要做什么；字段示意、待定决策和验收条件在规格内标明。
- **测试通过**只适用于记录的候选与断言。静态阅读、自动化 stub 测试、游戏内观察和发布确认不能互相替代。
- 当前入口不再维护一套任务调度记录。78 份旧逐轮稿集中在 [archive](archive/README.md)，保留历史与当前权威指引，不应按当时的“待办”继续执行。需要清理存档时可由维护者手动删除。

本目录可随源码分发。当前模组打包脚本不复制 `docs/`；仓库内有规格不表示已有模组 ZIP 携带规格。
