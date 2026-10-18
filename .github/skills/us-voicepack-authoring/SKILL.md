---
name: us-voicepack-authoring
description: >-
  制作、修改、迁移或诊断 Universal Squeaker 的 XML VoicePack 音频包；
  涵盖 PackDef、SoundDef、种族与 Xenotype 路由、年龄变体、彩蛋和回退。
  用于音频包内容制作与校验，不用于修改主模组触发实现。
---

# Universal Squeaker 语音包制作

先读取 [0.5.x VoicePack 作者指南](../../../docs/voicepack-authoring-0.5-zh.md)。它是 XML 形状、服务域、校验、预设导入、素材与验证步骤的唯一教程；本文件只保留 AI 执行约定，不维护另一份示例。指南描述当前工作树，不代表每个旧发行版。

若任务面向下一版，另读 [US Kernel 0.6.x 首版规格](../../../docs/us-kernel-0.6-spec-zh.md)，先区分目标要求与当前可用入口，不把规格示意用于当前生产包。

## 执行约定

根据请求执行制作、修改、迁移或诊断，不把只读咨询扩展为文件变更。实现已授权时，完成范围内的内容文件及静态检查。

1. 确认目标 US 版本、实际种族 defName、依赖和可用音频；小型新包优先采用作者指南第 2–3 节结构。不要编造目标 Def、可播放素材或第三方依赖。
2. 保持稳定身份。迁移时检查所有交叉引用与旧行为内容，不仅替换前缀；保留用户未要求更改的素材和配置。
3. 用户要求调节动作触发范围、单动作间隔/概率或心情时，优先创建独立 `UniversalSqueakerTuningBaselineDef`，而非 comp patch；明确它须由玩家手动导入，且不按 PackDef 自动绑定。Xenotype 预设同时需要一个有效 Xenotype PackDef 声明该 target。用户要求 mode、绝对间隔、全局 cooldown、冷却时钟或其他基础 plan 字段时，说明预设无法表达该需求，再评估 comp patch。
4. 区分硬性 validator 规则、运行时门控和制作建议。预设没有 PackDef 同等级的 actionKey validator：只写 17 个精确内置名称，避免未知键被导入后在 resolver 中忽略。当前源码与指南不一致时，先核对作者指南第 12 节的实际实现，再说明适用版本与差异。
5. 用 XML 解析器检查完整生成物，核对引用、重复 action/age、路径和预设域；不为普通包自动添加 comp patch，也不修改 US 主程序来迁就内容。
6. 交付时说明实际改动、静态检查结果和实机未验证项。只要交付预设，就提供“在 Presets 选择域并 Import，随后比较可观察行为”的步骤；没有游戏证据时不要声称“能响”或所有模式通过。
