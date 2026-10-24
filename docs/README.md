# Universal Squeaker 文档

当前源码为 **0.6.0 Dev / RimWorld1.6.4871**。r2已切换完整中立Core、单主体owner、一次通知声明与新Config格式；普通政策、UI/UX定稿和十五包双人工关口仍待裁／待验。

| 你要做什么 | 当前入口 |
| --- | --- |
| 理解当前使用、配置、保存与旧能力去向 | [r2合同与删除映射](us-kernel-0.6-refactor-zh.md) |
| 写声音包、动作扩展或一次C#通知 | [当前作者指南](us-kernel-0.6-authoring-zh.md)及[可加载示例](examples/0.6/) |
| 区分输入处理／磁盘提交／实际派发和帮助身份 | [公开合同与帮助命名](us-public-contracts-and-help-zh.md) |
| 审阅全部十五包、各自SR差异和待裁体验 | [完整合同导读](us-kernel-0.6-contract-review-zh.md) |
| 核对正向事实、调用主体、版本和Unknown | [事实适配表](us-kernel-0.6-fact-adapters-zh.md) |
| 核对Core工作完整范围 | [20 WorkType／105 Giver／78过程清单](us-core-work-scope-zh.md) |
| 查具体算法、参数与未决政策 | [规格r15](us-kernel-0.6-spec-zh.md)、[验收](us-kernel-0.6-acceptance-zh.md)、[测试迁移](us-kernel-0.6-test-migration-zh.md) |
| 修改源码／现有UI读面 | [架构](architecture-0.5-zh.md)、[UI契约](ui-contract-0.5-zh.md) |
| 构建固定候选／选择FL／打包 | [Build and debug](build-and-debug.md)、[FL0.8消费](fl08-consumer-migration-zh.md)、[目录](mod-structure-reference-zh.md) |
| 准备发布 | [发布流程](release-runbook-zh.md)、[隐私门禁](push-privacy-gate.md) |

旧0.5的[功能](current-behavior-0.5-zh.md)和[XML指南](voicepack-authoring-0.5-zh.md)仅供旧下载使用，不是r2兼容承诺。[S1](us-kernel-0.6-s1-zh.md)／[S2](us-kernel-0.6-s2-zh.md)／[S3](us-kernel-0.6-s3-zh.md)保留各阶段证据与资源说明，当前API以r2与实际源码为准；[历史档案](history/README.md)不作为待办。

独立核心、生产替身、实际游戏与用户接受分开记录。测试结论只适用于其候选与输入。产品正文自足，不依赖私有管理工作区；模组stage当前不复制docs。
