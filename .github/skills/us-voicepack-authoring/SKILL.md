---
name: us-voicepack-authoring
description: >-
  制作、修改或诊断 Universal Squeaker（coahuilite.universalsqueaker）VoicePack 音频包的完整流程，
  覆盖两种模式：canonical US 包（UniversalSqueaker.SqueakVoicePackDef + US_ 前缀，面向任意种族）与
  legacy SR 兼容包（SqueakyRatkin.SqueakVoicePackDef + SR_ 前缀，经兼容桥加载并打 Legacy SR 标记）。
  触发词：制作语音包、新建 VoicePack、语音包 XML、语音包报错、语音包校验、PackDef、clipFolderPath、
  US_ 前缀、SR_ 前缀、legacy 语音包、Legacy SR、IsEgg 彩蛋、ageTag、fallbacks、Xenotype 语音包、
  Kiiro 语音包、Nivarian 语音包、发布语音包。
---

# Universal Squeaker 语音包作者指南（canonical + legacy 双模式）

给语音包作者的人类可读指南；本文件同时是 `us-voicepack-authoring` skill 的正本，AI 助手按第 13 节执行。本文件自包含：制作语音包不需要任何外部文档或模板。

一个 VoicePack 是**独立 RimWorld 模组**，只包含 XML 和音频，不写 C#。它依赖 Universal Squeaker 与目标种族模组，不修改主模组、不把音频装进主模组目录。

## 0. 双模式速查

| 维度 | Mode A：canonical US 包（推荐新包） | Mode B：legacy SR 兼容包（旧包/迁移包） |
| --- | --- | --- |
| 根节点 | `UniversalSqueaker.SqueakVoicePackDef` | `SqueakyRatkin.SqueakVoicePackDef` |
| 前缀 | `US_`（defName 与全部 SoundDef） | `SR_`（保持旧 ABI） |
| `raceDefName` | **必填**，任意种族精确 defName | 可省略：省略 = `Ratkin`（SR 依赖语义）；声明了则按声明 |
| comp 挂载 | 包自带 `Patches/*_AddSqueakComp.xml` 给目标种族挂 `CompProperties_Squeaker` | 不需要：US legacy bridge 自动给声明种族挂默认 comp |
| UI 标记 | 无 | 金色 `Legacy SR` 行标签 + 页顶 legacy 计数 |
| 日志 | 普通 catalog 接纳 | daily：`voicepack.pack.legacy_admitted` + `voicepack.comp.legacy_auto_attached` |
| 依赖 | `coahuilite.universalsqueaker` + 种族模组 | 保持原 `coahuilite.squeakyratkin` 依赖即可（SR 缺失只产生非致命警告，US bridge 接管）；若改依赖也允许 |

## 1. 快速开始

**Mode A（canonical，三步能响）：**

1. 建目录：`About/About.xml` + `LoadFolders.xml` + `1.6/Race/Defs/SoundDefs/*.xml` + `1.6/Race/Sounds/<lowercase packageId>/<PackDef.defName>/<Action>/`。
2. 写最小 XML（第 3 节 A），再写 comp 挂载 patch（第 3.5 节）。
3. 装进游戏：排序在 US 与种族模组之后，设置选 **FALLBACK**，在对应 Race 下勾选你的 PackDef，触发 `Call` 听音。

**Mode B（legacy，旧 SR 包几乎零改动）：**

1. 保留旧包 XML（`SqueakyRatkin.SqueakVoicePackDef` + `SR_*`）不动。
2. 确认 `raceDefName` 缺失或为 `Ratkin`（缺失会被 bridge 默认成 Ratkin）。
3. 装进游戏、启用；US bridge 会自动加载、打 `Legacy SR` 标记、自动挂 comp，设置里勾选即可发声。

先只做 **Race + Call**，跑通后再加动作、年龄变体、彩蛋或 Xenotype。

## 2. 目录结构

```
MyStudioVoices/
|- About/About.xml
|- LoadFolders.xml
`- 1.6/
   |- Race/
   |  |- Defs/SoundDefs/MyStudio_Race_Sounds.xml
   |  |- Patches/MyStudio_AddSqueakComp.xml        # 仅 Mode A canonical 需要
   |  `- Sounds/com.example.mystudio.voices/US_MyStudio_Race/Call/call_01.ogg   # Mode A
   |      或 Sounds/com.example.mystudio.voices/SR_MyStudio_Race/Call/call_01.ogg  # Mode B
   `- Biotech/                                     # 仅 Xenotype 包需要
      `- Defs/SoundDefs/MyStudio_Xenotype_Sounds.xml
```

音频根规则：`<lowercase packageId>/<PackDef.defName>/<Action>/`。`packageId` 全小写（字母/数字/`._-`）；PackDef defName 按模式用 `US_` 或 `SR_` 前缀且全局唯一。

### About.xml（canonical）

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

### About.xml（legacy）

保留原 SR 包身份（packageId/描述可不动），依赖可以继续写 `coahuilite.squeakyratkin`；US bridge 会在 SR 不存在时接管。若希望完全以 US 为依赖，把依赖改成 `coahuilite.universalsqueaker` 即可。

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

### Mode A（canonical）

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

### Mode B（legacy）

把根节点写成 `SqueakyRatkin.SqueakVoicePackDef`、defName 与 SoundDef 全部保持 `SR_` 前缀即可。`<raceDefName>Ratkin</raceDefName>` 可写可不写：不写时 bridge 默认 Ratkin；写其它种族则按声明路由（作者自担正确性）。

细节：`fallbacks` 不要写成单数 `fallback`；`IsEgg` 保持大小写；`FloatRange` 用 `~`，不要用逗号或圆括号。

## 3.5 Mode A 必读：comp 挂载 patch

canonical 包必须自己把 `CompProperties_Squeaker` 挂到目标种族（US 本体不发布任何种族 patch）。模板：

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

legacy 包不需要此 patch：US bridge 会对 legacy 包声明的种族（默认 Ratkin）自动挂默认 comp；若包/种族已有 comp 则跳过、不覆盖。

## 4. PackDef 字段参考

| 字段 | Mode A | Mode B | 说明 |
| --- | --- | --- | --- |
| `defName` | 必填，`US_` 前缀 | 必填，`SR_` 前缀 | 全局唯一，玩家看到的包身份 |
| `raceDefName` | 必填 | 可省略（= Ratkin） | 精确、区分大小写的种族 `ThingDef.defName` |
| `scope` | 必填 | 必填 | `Race` 或 `Xenotype` |
| `targetDefName` | Xenotype 必填 | Xenotype 必填 | 精确 `XenotypeDef.defName`；Race 包不得写 |
| `weight` | 否 | 否 | 包级正有限抽取权重，省略 = 1 |
| `fallbacks` | 否 | 否 | 逐动作 `action` + `sound` |
| `actions` | 是 | 是 | 至少一条：`action` + `sounds`，可带 `ageTag`/`IsEgg` |

- 同一 action 同一 `ageTag`（含 all-age）只能出现一次；action 只用第 7 节 17 个内置键。
- `<ageTag>`：`Baby`/`Toddler`/`Child`/`Adult`；省略 = 全年龄；exact-age 优先。
- `<IsEgg>true</IsEgg>`：彩蛋条目，玩家开关默认关；关时不进候选，开时与同域普通条目同权混抽。
- `Crying`/`Giggling` 可写进 actions/fallbacks，但主模组内置表没有这两键音频，未声明则静默。

## 5. 生产 SoundDef 契约（两种模式同规则，前缀随模式）

- defName 以 `US_`（Mode A）或 `SR_`（Mode B）开头；
- `<sustain>false</sustain>`；`<context>MapOnly</context>`；
- 至少一个 SubSound，每个 SubSound 至少一个 grain（通常 `AudioGrain_Folder` + `clipFolderPath`）；
- `onCamera` 省略或 false；
- 禁止 `sustain=true`、loop、camera/map 混用、空文件与静音占位。

违反任何一条，整个 PackDef 被拒绝，回退链继续。

## 6. Xenotype 包

- `<scope>Xenotype</scope>` + 种族 `raceDefName` + 精确 `targetDefName`（如 `KiiroXenotype`、`RK_XenoType_Ratkin`）。
- LoadFolders 用 `IfModActive="ludeon.rimworld.biotech"` 门控。
- 目标缺失时该层不解析，Race 层继续回退。

## 7. 17 个固定动作与回退

`Call / Eat / Sleep / Wounded / Select / Move / Social / Joy / Death / Draft / Undraft / Attack / Work / Equip / MentalBreak / Crying / Giggling`（append-only）。

- **FALLBACK**：Xenotype → Race → pack fallback → 内置 profile → 无声；
- **REMIX**：可播放 tier 等权；
- **OFF**：仅内置 profile。

语音包只能提供声音，不能改触发、心情、频率、距离或动作范围。

## 8. 音频处理

推荐 OGG Vorbis、mono（22050 Hz mono 为参考，非强制）；剪掉首尾静音/点击声，做响度处理避免 clipping；不要只改扩展名。

## 9. 安装、测试与 legacy 标记验证

1. 独立模组安装，排序在 US 与目标种族模组之后。
2. 设置选 **FALLBACK**，勾选 PackDef（发现 ≠ 自动启用）。
3. 先测 Race `Call`；确认后移除该动作验证回退，再测 Xenotype/部分覆盖。
4. **Mode B 验证**：设置行应有金色 `Legacy SR` 标签，页顶 banner 有 legacy 计数；Player.log（daily，无需 dev 日志）出现：
   - `Legacy SR VoicePack admitted: <packageId>:<defName> (race Ratkin).`
   - `Legacy compatibility auto-attached the squeak comp to race Ratkin.`
5. dev 日志成功派发：`Audio route: <action> -> <sound> (<tier>[, egg][, nonplayer]).`

## 10. 排错

| 现象 | 处理 |
| --- | --- |
| 包被拒 `duplicate_key` | 同 `packageId:defName` 出现多份；检查是否新旧两版包同时安装 |
| canonical 包不响 | 检查第 3.5 节 comp patch 的目标 defName 与 patch 是否生效；`raceDefName` 精确匹配 |
| legacy 包不响 | 检查是否装了最新 US（bridge auto-attach）；或手动加 comp patch |
| 有包仍听到 Vanilla | 模式不是 OFF、PackDef 已勾选、动作已覆盖、目录有可播放文件 |
| Xenotype 不匹配 | `targetDefName` 精确等于 `XenotypeDef.defName` |
| 与其他包串音 | 每个 DefName 与音频根使用自己的稳定 token |
| 旧 SR 包加载红字 | 确认没有同时启用 SR 本体与 US legacy bridge（`SqueakyRatkin.SqueakVoicePackDef` first-wins） |

## 11. 发布检查清单

- [ ] 自己的 packageId/名称/作者；defName 前缀与模式匹配且全局唯一。
- [ ] Mode A：声明 exact raceDefName + 自带 comp patch；Mode B：raceDefName 缺失或 Ratkin，不依赖额外 patch。
- [ ] `clipFolderPath`、实际目录、`<lowercase packageId>/<PackDef.defName>/<Action>/` 一致。
- [ ] 每个生产 SoundDef 满足第 5 节契约；每个已列 action 有可播放音频。
- [ ] OFF/FALLBACK/REMIX 与回退已实机验证。
- [ ] 音频已试听、无空/意外素材，完成剪辑与响度处理。
- [ ] 已声明音频许可与署名，不重分发无权内容。
- [ ] Mode B 若面向新旧并存：说明包是 legacy SR 包、会被 US 标记为 `Legacy SR`。

## 12. 兼容承诺

- canonical ABI：`US_` 前缀 + `UniversalSqueaker.SqueakVoicePackDef`；字段只增不改；17 动作 append-only；非法包整体拒绝、回退继续。
- legacy ABI：`SR_` 前缀 + `SqueakyRatkin.SqueakVoicePackDef` 经兼容桥保持可加载；缺 `raceDefName` 默认 `Ratkin`；在 US 中永远显式标记为旧 SR 内容。

## 13. 给 AI 助手（skill 执行约定）

触发：用户请求制作/修改/诊断 US 语音包。按顺序执行：

1. 先确认模式：新包或任意种族 → Mode A；旧 SR 包/要求 legacy 标记 → Mode B。
2. 校验 packageId（全小写）与 defName 前缀；Mode A 必须写精确 `raceDefName` 并生成 comp patch；Mode B 保留 `SR_` 形态、可省略 raceDefName（= Ratkin）。
3. 音频路径 = `<lowercase packageId>/<PackDef.defName>/<Action>/`；提醒作者替换占位文件并声明许可。
4. 用 XML 解析器检查生成物；对照第 4–5 节逐项检查；不要替作者声称实机测试，要求按第 9 节验证。
5. 排错优先查第 10 节；引用本文件具体小节编号，不要凭记忆改写契约。

## 14. 自包含

本文件是 US 语音包制作唯一正本，无需外部模板。US 仓库暂无 `new-voicepack.ps1` 脚手架，按第 2–3 节手工创建即可。
