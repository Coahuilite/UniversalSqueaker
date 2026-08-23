---
name: us-voicepack-authoring
description: >-
  制作、修改或诊断 Universal Squeaker（coahuilite.universalsqueaker）VoicePack 音频包的完整流程。
  新包一律使用 canonical 形态（UniversalSqueaker.SqueakVoicePackDef + US_ 前缀，面向任意种族）。
  旧 Squeaky Ratkin 包（SR_ 前缀）无需作者改写：US legacy bridge 自动加载、默认 Ratkin、
  自动挂 comp，并打 Legacy SR 标记。
  触发词：制作语音包、新建 VoicePack、语音包 XML、语音包报错、语音包校验、PackDef、clipFolderPath、
  US_ 前缀、SR_ 前缀、legacy 语音包、Legacy SR、IsEgg 彩蛋、ageTag、fallbacks、Xenotype 语音包、
  发布语音包。
---

# Universal Squeaker 语音包作者指南

给语音包作者的人类可读指南；本文件同时是 `us-voicepack-authoring` skill 的正本，AI 助手按第 12 节执行。本文件自包含：制作语音包不需要任何外部文档或模板。

一个 VoicePack 是**独立 RimWorld 模组**，只包含 XML 和音频，不写 C#。它依赖 Universal Squeaker 与目标种族模组，不修改主模组、不把音频装进主模组目录。

## 0. 旧 SR 包怎么办（无需作者操作）

历史 Squeaky Ratkin 语音包（`SqueakyRatkin.SqueakVoicePackDef` + `SR_` 前缀）**不需要改写**：

- US legacy bridge 自动加载它们并打上 `Legacy SR` 标记（设置行金色标签 + 页顶计数，日志 `usdiag voicepack.pack.legacy_admitted`）；
- 未声明 `raceDefName` 的旧包一律按 `Ratkin` 处理（SR 依赖语义）；作者显式声明了别的种族则按声明走；
- bridge 会自动给这些种族挂默认 `CompProperties_Squeaker`（日志 `usdiag voicepack.comp.legacy_auto_attached`），包侧无需 patch；
- 唯一注意：不要与 SR 本体同时启用（`SqueakyRatkin.SqueakVoicePackDef` first-wins）。

因此本指南的**唯一作者模式是 canonical**：新包、新种族、新音频都走 `US_` 形态。

## 1. 快速开始（三步能响）

1. 建目录：`About/About.xml` + `LoadFolders.xml` + `1.6/Race/Defs/SoundDefs/*.xml` + `1.6/Race/Sounds/<lowercase packageId>/<PackDef.defName>/<Action>/`。
2. 写最小 XML（第 3 节）与 comp 挂载 patch（第 3.5 节）。
3. 装进游戏：排序在 US 与目标种族模组之后，设置选 **FALLBACK**，在对应 Race 下勾选 PackDef，触发 `Call` 听音。

先只做 **Race + Call**，跑通后再加动作、年龄变体、彩蛋或 Xenotype。

## 2. 目录结构

```
MyStudioVoices/
|- About/About.xml
|- LoadFolders.xml
`- 1.6/
   |- Race/
   |  |- Defs/SoundDefs/MyStudio_Race_Sounds.xml
   |  |- Patches/MyStudio_AddSqueakComp.xml
   |  `- Sounds/com.example.mystudio.voices/US_MyStudio_Race/Call/call_01.ogg
   `- Biotech/                                     # 仅 Xenotype 包需要
      `- Defs/SoundDefs/MyStudio_Xenotype_Sounds.xml
```

音频根规则：`<lowercase packageId>/<PackDef.defName>/<Action>/`。`packageId` 全小写（字母/数字/`._-`）；PackDef defName 必须 `US_` 开头且全局唯一。

### About.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <packageId>com.example.mystudio.voices</packageId>
  <name>My Studio Voices</name>
  <author>My Studio</author>
  <supportedVersions><li>1.6</li></supportedVersions>
  <description>Independent voice pack for Universal Squeaker.</description>
  <modDependencies>
    <li><packageId>coahuilite.universalsqueaker</packageId><displayName>Universal Squeaker</displayName></li>
    <li><packageId>目标种族模组packageId</packageId><displayName>目标种族</displayName></li>
  </modDependencies>
  <loadAfter>
    <li>coahuilite.universalsqueaker</li>
    <li>目标种族模组packageId</li>
    <li>ludeon.rimworld.biotech</li><!-- 仅 Xenotype 包 -->
  </loadAfter>
</ModMetaData>
```

### LoadFolders.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.6>
    <li>1.6/Race</li>
    <li IfModActive="ludeon.rimworld.biotech">1.6/Biotech</li><!-- 仅 Xenotype 包 -->
  </v1.6>
</loadFolders>
```

## 3. 最小 Race + Call XML

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <UniversalSqueaker.SqueakVoicePackDef>
    <defName>US_MyStudio_Race</defName>
    <scope>Race</scope>
    <raceDefName>Ratkin</raceDefName><!-- 或 Kiiro_Race / NivarianRace_Pawn / 任意种族 -->
    <actions><li><action>Call</action><sounds><li>US_MyStudio_Race_Call</li></sounds></li></actions>
  </UniversalSqueaker.SqueakVoicePackDef>
  <SoundDef>
    <defName>US_MyStudio_Race_Call</defName>
    <sustain>false</sustain>
    <context>MapOnly</context>
    <subSounds><li><grains><li Class="AudioGrain_Folder">
      <clipFolderPath>com.example.mystudio.voices/US_MyStudio_Race/Call</clipFolderPath>
    </li></grains><volumeRange>45~55</volumeRange><pitchRange>0.95~1.05</pitchRange><distRange>15~70</distRange></li></subSounds>
  </SoundDef>
</Defs>
```

细节：根节点严格写 `UniversalSqueaker.SqueakVoicePackDef`；`fallbacks` 不要写成单数 `fallback`；`IsEgg` 保持大小写；`FloatRange` 用 `~`，不要用逗号或圆括号。

## 3.5 必读：comp 挂载 patch

US 本体不发布任何种族 patch；canonical 包必须自己把 `CompProperties_Squeaker` 挂到目标种族。模板：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Patch>
  <Operation Class="PatchOperationConditional">
    <xpath>/Defs/AlienRace.ThingDef_AlienRace[defName="目标defName"]/comps</xpath>
    <nomatch Class="PatchOperationAdd">
      <xpath>/Defs/AlienRace.ThingDef_AlienRace[defName="目标defName"]</xpath>
      <value><comps /></value>
    </nomatch>
  </Operation>
  <Operation Class="PatchOperationAdd">
    <xpath>/Defs/AlienRace.ThingDef_AlienRace[defName="目标defName"]/comps</xpath>
    <value>
      <li Class="UniversalSqueaker.CompProperties_Squeaker">
        <globalMinIntervalTicks>216</globalMinIntervalTicks>
        <scaleFrequencyWithTalking>true</scaleFrequencyWithTalking>
        <actions>
          <li><action>Call</action><mode>RandomOneShot</mode><minIntervalTicks>864</minIntervalTicks><probabilityPerCheck>0.012</probabilityPerCheck></li>
          <!-- 其余动作可选；省略的动作走未配置默认 -->
        </actions>
        <moodMods>
          <li><mood>Good</mood><pitchFactor>1.2</pitchFactor><pitchJitter>0.97~1.03</pitchJitter><volumeFactor>1.3</volumeFactor></li>
          <li><mood>Neutral</mood><pitchFactor>1.0</pitchFactor><pitchJitter>0.97~1.03</pitchJitter><volumeFactor>1.0</volumeFactor></li>
        </moodMods>
        <distancePresets>
          <li><preset>Balanced</preset><range>15~50</range></li>
        </distancePresets>
      </li>
    </value>
  </Operation>
</Patch>
```

若目标种族已有该 comp（例如别的包已挂），RimWorld 会按 patch 语义追加或你可用条件 patch 避免重复；US 运行时不会自动替 canonical 包挂载。

## 4. PackDef 字段参考

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `defName` | 是 | `US_` 开头，全局唯一；玩家看到的包身份 |
| `raceDefName` | 是 | 精确、区分大小写的种族 `ThingDef.defName` |
| `scope` | 是 | `Race` 或 `Xenotype` |
| `targetDefName` | Xenotype 必填 | 精确 `XenotypeDef.defName`；Race 包不得写 |
| `weight` | 否 | 包级正有限抽取权重，省略 = 1 |
| `fallbacks` | 否 | 逐动作 `action` + `sound` |
| `actions` | 是 | 至少一条：`action` + `sounds`，可带 `ageTag`/`IsEgg` |

- 同一 action 同一 `ageTag`（含 all-age）只能出现一次；action 只用第 6 节 17 个内置键。
- `<ageTag>`：`Baby`/`Toddler`/`Child`/`Adult`；省略 = 全年龄；exact-age 优先。
- `<IsEgg>true</IsEgg>`：彩蛋条目，玩家开关默认关；关时不进候选，开时与同域普通条目同权混抽。
- `Crying`/`Giggling` 可写进 actions/fallbacks，但主模组内置表没有这两键音频，未声明则静默。

## 5. 生产 SoundDef 契约

- defName 以 `US_` 开头；
- `<sustain>false</sustain>`；`<context>MapOnly</context>`；
- 至少一个 SubSound，每个 SubSound 至少一个 grain（通常 `AudioGrain_Folder` + `clipFolderPath`）；
- `onCamera` 省略或 false；
- 禁止 `sustain=true`、loop、camera/map 混用、空文件与静音占位。

违反任何一条，整个 PackDef 被拒绝，回退链继续。

## 6. 17 个固定动作与回退

`Call / Eat / Sleep / Wounded / Select / Move / Social / Joy / Death / Draft / Undraft / Attack / Work / Equip / MentalBreak / Crying / Giggling`（append-only）。

- **FALLBACK**：Xenotype → Race → pack fallback → 内置 profile → 无声；
- **REMIX**：可播放 tier 等权；
- **OFF**：仅内置 profile。

语音包只能提供声音，不能改触发、心情、频率、距离或动作范围。

## 7. Xenotype 包

- `<scope>Xenotype</scope>` + 种族 `raceDefName` + 精确 `targetDefName`（如 `KiiroXenotype`、`RK_XenoType_Ratkin`）。
- LoadFolders 用 `IfModActive="ludeon.rimworld.biotech"` 门控。
- 目标缺失时该层不解析，Race 层继续回退。

## 8. 音频处理

推荐 OGG Vorbis、mono（22050 Hz mono 为参考，非强制）；剪掉首尾静音/点击声，做响度处理避免 clipping；不要只改扩展名。

## 9. 安装与测试

1. 独立模组安装，排序在 US 与目标种族模组之后。
2. 设置选 **FALLBACK**，勾选 PackDef（发现 ≠ 自动启用）。
3. 先测 Race `Call`；确认后移除该动作验证回退，再测 Xenotype/部分覆盖。
4. dev 日志成功派发：`Audio route: <action> -> <sound> (<tier>[, egg][, nonplayer]).`
5. 诊断旧 SR 包时看 daily 日志：`voicepack.pack.legacy_admitted`（带 Legacy SR 标记）与 `voicepack.comp.legacy_auto_attached`。

## 10. 排错

| 现象 | 处理 |
| --- | --- |
| canonical 包不响 | 检查第 3.5 节 comp patch 目标 defName 与 patch 是否生效；`raceDefName` 精确匹配 |
| 包被拒 `duplicate_key` | 同 `packageId:defName` 出现多份；检查是否新旧两版包同时安装 |
| 有包仍听到 Vanilla | 模式不是 OFF、PackDef 已勾选、动作已覆盖、目录有可播放文件 |
| Xenotype 不匹配 | `targetDefName` 精确等于 `XenotypeDef.defName` |
| 与其他包串音 | 每个 DefName 与音频根使用自己的稳定 token |
| 旧 SR 包加载红字 | 确认没有同时启用 SR 本体与 US legacy bridge（`SqueakyRatkin.SqueakVoicePackDef` first-wins） |

## 11. 发布检查清单

- [ ] 自己的 packageId/名称/作者；defName 以 `US_` 开头且全局唯一。
- [ ] 声明 exact raceDefName + 自带 comp patch。
- [ ] `clipFolderPath`、实际目录、`<lowercase packageId>/<PackDef.defName>/<Action>/` 一致。
- [ ] 每个生产 SoundDef 满足第 5 节契约；每个已列 action 有可播放音频。
- [ ] OFF/FALLBACK/REMIX 与回退已实机验证。
- [ ] 音频已试听、无空/意外素材，完成剪辑与响度处理。
- [ ] 已声明音频许可与署名，不重分发无权内容。

## 12. 给 AI 助手（skill 执行约定）

触发：用户请求制作/修改/诊断 US 语音包。按顺序执行：

1. 新包一律 canonical：`US_` 前缀 + 精确 `raceDefName` + 自带 comp patch；旧 SR 包原则上不改写，只在诊断时引用第 0 节。
2. 校验 packageId（全小写）与 defName；音频路径 = `<lowercase packageId>/<PackDef.defName>/<Action>/`；提醒作者替换占位文件并声明许可。
3. 用 XML 解析器检查生成物；对照第 4–5 节逐项检查；不要替作者声称实机测试，要求按第 9 节验证。
4. 排错优先查第 10 节；引用本文件具体小节编号，不要凭记忆改写契约。

## 13. 自包含

本文件是 US 语音包制作唯一正本，无需外部模板。US 仓库暂无 `new-voicepack.ps1` 脚手架，按第 2–3 节手工创建即可。
