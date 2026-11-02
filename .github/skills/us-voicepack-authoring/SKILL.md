---
name: us-voicepack-authoring
description: >-
  制作、修改、迁移或诊断 Universal Squeaker 的 XML VoicePack 音频包；
  涵盖 PackDef、SoundDef、种族与 Xenotype 路由、年龄变体、彩蛋和回退。
  用于音频包内容制作与校验，不用于修改主模组触发实现。
---

# Universal Squeaker 语音包制作

先读[当前0.6 r2作者指南](../../../docs/us-kernel-0.6-authoring-zh.md)和[现行规格](../../../docs/specification.md)。只有明确面向已发布0.5包时才使用[旧指南](../../../docs/voicepack-authoring-0.5-zh.md)；两者不混用reader或配置格式。

## 执行约定

根据请求执行制作、修改、迁移或诊断，不把只读咨询扩展为文件变更。实现已授权时，完成范围内的内容文件及静态检查。

1. 确认目标 US 版本、实际种族 defName、依赖和可用音频；小型新包优先采用当前作者指南与AudioOnly完整示例结构。不要编造目标 Def、可播放素材或第三方依赖。
2. 保持稳定身份。迁移时检查所有交叉引用与旧行为内容，不仅替换前缀；保留用户未要求更改的素材和配置。
3. 0.6声音供给使用UsVoicePackDef；行为贡献使用独立UsBehaviorSourceDef，模板使用玩家显式导入的UsBehaviorPresetDef。新C#通知先一次声明；未知字符串拒绝，不以comp patch或旧默认计划兜底。
4. 使用完整ActionId和准确Race／Race-Xenotype域；先区分定义、来源、玩家覆盖、音频参与和系统权限。逐包正向事实、Unknown、已采用资格及明确原生缺口不能由标签猜测或制作偏好替代。
5. 用 XML 解析器检查完整生成物，核对引用、完整条目身份重复、路径和预设域；同动作同年龄可有不同变体／彩蛋，不能只按(action,age)拒绝；不为普通包自动添加 comp patch，也不修改 US 主程序来迁就内容。
6. 交付时说明实际改动、静态检查结果和实机未验证项。只要交付预设，就提供“在 Presets 选择域并 Import，随后比较可观察行为”的步骤；没有游戏证据时不要声称“能响”或所有模式通过。
