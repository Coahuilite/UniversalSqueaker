# Universal Squeaker 0.6.x 规格

让准确主体在真实事实与合法机会成立时，从允许的声音供给选音，经局部调制和自有原生输出回应；保存、采用、派发和听见分别有自己的含义。

**状态：现行。** 本入口及下列正文自2026-10-29在本地取代旧整编规格。适用US0.6.x／RW1.6（net472）及RW1.7（net10），按下列双目标范围分别构建与验证；API及Config面向首次公开基线。规格采用不是发布或运行验收；[实现偏差与证据](development/specification/open-issues.md)可直接查到。

## 当前开发线与双目标（2026-11-14采用）

`0.6.x` 是唯一当前开发线，采用 FL API **[0.9.0,0.10.0)** 与 [UI-10](openspec/specs/ui-consumption/spec.md#requirement-ui-10-生产-f09-显式保留准备原生操作保持实时)。r5 独立候选保留其 RW1.6 原貌；当前线合入须保持已有0.6.x提交历史，不以仅1.6候选替换整条版本线。

构建及交付 SHALL 区分 RW1.6（net472／原准确1.6引用）和 RW1.7（net10／准确1.7原生引用），分别选择同目标的外置 FL0.9；US 不构建或捆绑 FL。LoadFolders SHALL 让1.6与1.7选择各自真实 payload，不把1.6 DLL称作1.7原生包。共享业务／内容／公开入口保原合同，必要平台差异留在适配及既有构建入口。缺少对应载体、引用或路由验据时不得称完整双目标可用。

UI-10 的来源、编辑／输入、故障、首次实画及无opt-in回退保证在两目标均保留；实现／构建／原生覆盖／兼容与发布分别记账。1.6性能结果不是1.7实测；采用和普通push不授权tag／Release／部署。旧0.8固定载体和旧线保证保留。采用来源与实施门见[合流change](openspec/changes/us06-fl09-dual-target/proposal.md)。

## 组合与唯一归属

```text
原版／扩展事实 ──原生adapter──┐
定义修订 ──────────定义输入 ─┤
作者／玩家／系统 ──配置输入 ─┼─ 三引用读取 → 稳定主体响应owner
声音条目／VF1 ─────音频输入 ─┘                  ↓
                                      合同择一 → 选音 → 局部调制
                                                    ↓
                                  原生输出四结果 → 原owner结算
UI／帮助／诊断 ← 有效配置解释、当次结果和真实实例证据
```

| 唯一规范正文 | 拥有的保证 | 先读者 |
| --- | --- | --- |
| [定义与作者](openspec/specs/core-contracts/spec.md) | 中立Core、身份、固定组合、声明与公共结果 | 扩展／合同作者 |
| [机会与结算](openspec/specs/response-opportunities/spec.md) | 单主体owner、root、真实秒／模拟tick、W／R／保护 | 核心维护者 |
| [十五官方动作](openspec/specs/official-actions/spec.md) | 正向动作含义、实际主体、Select／Eat可调参数 | 合同与包作者 |
| [普通默认](openspec/specs/ordinary-policy/spec.md) | 启动、十五独立I／scope、H／W／A、总序与豁免 | 玩家／政策维护者 |
| [音频选择](openspec/specs/audio-selection/spec.md) | AudioId、准确来源、四模式、年龄／彩蛋／权重 | 声音包作者 |
| [调制与输出](openspec/specs/audio-output/spec.md) | 参数、参与、四结果、在播总控与Sustained槽 | 音频维护者 |
| [行为配置](openspec/specs/behavior-configuration/spec.md) | 作者／玩家权限、来源／快照、格式、模板／Reset | 配置维护者 |
| [配置提交→采用](openspec/specs/configuration-publication/spec.md) | 普通排队与persist-first、失败／关闭／发布 | 设置与公开API作者 |
| [VF1→实际选音](openspec/specs/fallback-adoption/spec.md) | 独立文件成功后的真实消费者、准确反馈、准入重启 | VF1维护者 |
| [原版事实与资源](openspec/specs/game-facts/spec.md) | 真实接缝、canonical域、依赖隔离与资源绑定 | 1.6适配维护者 |
| [完整Core Work](openspec/specs/game-work/spec.md) | 20 WorkType／105 Giver／78 Job-driver精确范围 | Work适配维护者 |
| [UI消费](openspec/specs/ui-consumption/spec.md) | 同窗／草稿、过滤、工具、帮助、布局与颜色默认；UI-10显式保留准备（双目标范围已采用，工程／原生分别验证） | UI消费者 |
| [诊断与证据](openspec/specs/diagnostics/spec.md) | mode／级别／失败隔离、结果和运行证据层 | 日志／诊断维护者 |
| [同源开发预览](openspec/specs/developer-preview/spec.md) | 隔离样例、完整窗口、热改／agent、工具与业务诊断分工 | UI／开发辅助维护者 |

不按类数或审计行数拆spec，不要求新增运行时连接类。具体符号与当前生产者→入口→消费者在[实现映射](development/specification/implementation.md)，逐条来源、合并和后续替代在[覆盖表](development/specification/coverage.md)。两者不另定义行为。

## 为玩家、作者和贡献者阅读

- 玩家：先读[README](../README.zh-CN.md)，有问题按普通默认→对应动作→UI消费查具体条件。无声不等于没有处理；必须有该动作可用音源。
- 作者：从[0.6作者指南](us-kernel-0.6-authoring-zh.md)和[可加载示例](examples/0.6/)入手，再读本页关联能力。独立根、多级继承和NonPawn中立模型合法；完整NonPawn原生宿主仍非本版交付。
- 修改实现：读相应能力及直接连接，再看[事实／版本映射](development/specification/facts.md)、[开放项](development/specification/open-issues.md)和[证据ID](development/specification/evidence.md)，不要从bug倒改合同。
- 新增模块／内聚新核心／责任迁移：沿[轻量变更方法](development/specification/changes.md)给实际需要、owner／寿命、最小完整切片和采用／退出；无新平台或消费者数量门槛。
- 构建与发布：沿[构建](build-and-debug.md)、[发布流程](release-runbook-zh.md)与[隐私门禁](push-privacy-gate.md)；[固定FL0.8消费者](fl08-consumer-migration-zh.md)保旧线身份，不作为本分支0.9接线方法。0.9的guard／精确引用迁移与同代off切线见[采用后任务](openspec/changes/rw16-fl09-display-snapshot/tasks.md)及[消费设计](openspec/changes/rw16-fl09-display-snapshot/design.md)。按r3运行相关既有检查和普通构建；真实游戏归原运行owner，不授权发布。

## 历史、兼容与尚未兑现的保证

[保内容归档](archive/specs-before-20261029/README.md)保留被替代正文及原阶段／来源；旧入口只导航，不是第二套current。2026-10-28的US ea610fa／FL833c14f4已获RC级总体接受并可进入RC准备；总体接受与性能证据按原载荷和层使用。逐包记录未细化处是已知证据粒度，不是PASS或FAIL，不撤回该总体接受，也不因缺逐项记录新增本次文档／RC准备前置门或要求重跑十五矩阵／已接受性能；未来实际受改行为仍按具体影响验证。

日志出口／失败隔离F01、VF1音频发布F02、canonical身份F03已完成本地实现；当前技术与原生证据层见[实现状态](development/specification/open-issues.md)。新VF1重新应用按钮、持久pending与跨窗恢复未裁，不是恢复F02的必做范围。

US06-PREPUBLICATION-COMPAT-20261019 r4：破坏性更新授权持续至用户明确确认面向玩家公开发布；GitHub可见、push、tag或候选存在不终止，也不授权外部发布。它不扩展到FL稳定ABI、SR、真实Config或存档；旧字段不暗重解释，未变音频／包／外观不连带抛弃。

