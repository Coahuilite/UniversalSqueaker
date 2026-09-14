---
name: us-voicepack-authoring
description: >-
  制作、修改、迁移或诊断 Universal Squeaker 的 XML VoicePack 音频包；
  涵盖 PackDef、SoundDef、种族与 Xenotype 路由、年龄变体、彩蛋和回退。
  用于音频包内容制作与校验，不用于修改主模组触发实现。
---

# Universal Squeaker 语音包作者指南

本文件兼作作者说明与 AI skill；作者可从第 2 节开始，AI 助手同时遵循第 12 节。规则依据当前仓库实现核对（2026-09-13），不保证旧发行版具有相同能力。第 13 节提供维护用源码入口；普通制作无需外部模板。

一个 `SqueakVoicePackDef` 是一个可选音频包，服务一个精确域。独立内容模组可包含多个 PackDef，只需 XML 与音频，不需要 C#。依赖 Universal Squeaker（`coahuilite.universalsqueaker`）及实际使用的种族模组；原版 `Human` 不需要第三方种族依赖。安装时也须满足 US 自身依赖。不要把内容安装进 US 主模组目录。

## 1. 实现机制与边界

1. RimWorld 按激活的加载目录读取 XML 和音频。US 从 `DefDatabase<SqueakVoicePackDef>` 枚举声明，执行静态校验；不扫描某个专用语音包目录。
2. Catalog 接纳合法 PackDef，以 `packageId:PackDef.defName` 为稳定键。相同键的多个有效声明整组拒绝；`raceDefName` 与 Xenotype 的 `targetDefName` 均精确、区分大小写。
3. 启动完成时，US 对所有已接纳包声明的种族挂载默认 squeak comp。Race 与 Xenotype 包都通过 `raceDefName` 挂载；玩家是否勾选包不影响挂载。目标 ThingDef 不存在、没有 race 属性或已有 US comp 时跳过。
4. 玩家在精确 Race 或 Race + Xenotype 域勾选包后，resolver 才将其投影进候选池。安装、发现和勾选是不同阶段。
5. 生产触发通过行为门控后，按模式、年龄、彩蛋资格和声音可播放性选音，再由 comp 派发。XML 合法不等于文件可解码，也不等于当前 pawn 一定触发。

US 不读取旧 `SqueakyRatkin.SqueakVoicePackDef` 类型，也不自动桥接 `SR_` 包。迁移需同步更新 Def 类型、PackDef/SoundDef 名称与全部引用，以及依赖；若改音频目录，也要同步路径。新内容使用 `UniversalSqueaker.SqueakVoicePackDef` 和 `US_` 前缀。不要把仍含旧 C# 行为的模组仅改 XML 后称为已完成迁移。

## 2. 最小目录与依赖

先做原版 `Human` 的 Race + Call 包，验证后再换成已确认的目标种族 defName。

```text
MyStudioVoices/
|- About/About.xml
|- LoadFolders.xml
`- 1.6/Race/
   |- Defs/SoundDefs/MyStudio_Race_Sounds.xml
   `- Sounds/com.example.mystudio.voices/US_MyStudio_Human/Call/call_01.ogg
```

推荐用 `<lowercase packageId>/<PackDef.defName>/<Action>/` 隔离音频命名空间；这是防串音的目录约定，**不是 US validator 的硬性格式**。`clipFolderPath` 相对激活目录中的 `Sounds/`，不含 `Sounds/` 前缀，也不写具体音频扩展名。packageId 使用稳定、全小写的自有标识，Def 名称全局唯一。

`About/About.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <packageId>com.example.mystudio.voices</packageId>
  <name>My Studio Voices</name>
  <author>My Studio</author>
  <supportedVersions><li>1.6</li></supportedVersions>
  <description>Human voice pack for Universal Squeaker.</description>
  <modDependencies>
    <li><packageId>coahuilite.universalsqueaker</packageId><displayName>Universal Squeaker</displayName></li>
  </modDependencies>
  <loadAfter><li>coahuilite.universalsqueaker</li></loadAfter>
</ModMetaData>
```

`LoadFolders.xml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.6><li>1.6/Race</li></v1.6>
</loadFolders>
```

改为第三方种族时，同时在 `modDependencies` 和 `loadAfter` 添加实际种族模组 packageId。这里的 packageId 与下节的 `raceDefName` 是两种标识，不能互换。

## 3. 最小 Race + Call XML

保存为 `1.6/Race/Defs/SoundDefs/MyStudio_Race_Sounds.xml`，并放入真实可播放音频：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <UniversalSqueaker.SqueakVoicePackDef>
    <defName>US_MyStudio_Human</defName>
    <label>My Studio Human</label>
    <scope>Race</scope>
    <raceDefName>Human</raceDefName>
    <actions>
      <li><action>Call</action><sounds><li>US_MyStudio_Human_Call</li></sounds></li>
    </actions>
  </UniversalSqueaker.SqueakVoicePackDef>
  <SoundDef>
    <defName>US_MyStudio_Human_Call</defName>
    <sustain>false</sustain>
    <context>MapOnly</context>
    <subSounds>
      <li>
        <grains><li Class="AudioGrain_Folder">
          <clipFolderPath>com.example.mystudio.voices/US_MyStudio_Human/Call</clipFolderPath>
        </li></grains>
        <volumeRange>45~55</volumeRange>
        <pitchRange>0.95~1.05</pitchRange>
        <distRange>15~70</distRange>
      </li>
    </subSounds>
  </SoundDef>
</Defs>
```

`FloatRange` 用 `~`；字段大小写与 C# 一致，特别是 `IsEgg`。`distRange` 是 SoundDef 初始值，US 的距离设置会覆盖其管理的地图 SubSound 范围，不能把此值当成包的永久播放半径。

### 3.5 自动 comp 与调音预设（默认无 patch）

默认 comp 有 15 个生产动作配置（不含 Crying/Giggling）和 Good/Neutral/Bad/Break 四档心情配置。例如 Call 是 `RandomOneShot`，基础间隔 864 ticks、每次检查概率 0.012；实际结果还受玩家设置及运行时门控影响。

普通音频包无需 patch。若作者需要随包提供某 Race 或 Race + Xenotype 的触发范围、单动作间隔倍率、随机触发概率倍率或心情调制，优先额外发布独立 `UniversalSqueakerTuningBaselineDef` 调音预设；它不属于 `SqueakVoicePackDef`，也不写进 comp。预设由玩家在 US 设置的 **Presets** 页面选择目标 Race/Xenotype 后点击 **Import**，才导入本地 settings 并立即重建运行时 resolver。安装包、勾选 PackDef、或自动挂载 comp 都不会自动导入预设。

目标种族已有任意 `CompProperties_Squeaker` 时，自动挂载整体跳过，**不会把 `CreateDefault()` 的其他动作或心情补进作者列表**。缺失动作走 `SqueakActionPlanFactory.Unconfigured`（当前为 RandomOneShot、300 ticks、0.02），不是上述 15 项默认表；玩家的调音仍可能覆盖行为。不要用仅含 Call 的 comp 示例冒充完整默认配置。

预设只调整既有 action plan：`scope`、`intervalMultiplier`、`probabilityMultiplier`，以及心情的 pitch/volume/jitter。它不能改 `SqueakTriggerMode`、绝对 `minIntervalTicks`、`ignoreGlobalCooldown`、`cooldownClock` 或全局 cooldown；各动作的基础触发 plan 仍由自动挂载的默认 comp 或已有 comp 提供。`intervalMultiplier` 乘在该 action 的基础间隔上，`probabilityMultiplier` 乘在 RandomOneShot 的基础概率上。使用内置 17 个动作名称作为 `actionKey`，并用非负有限倍率；`0` 是允许值，代表该 action 不再有自身间隔，并不跳过全局 cooldown 或其他发声门控。

以下为可选 Race 预设，放入激活加载目录内的 Defs 文件，例如 `1.6/Race/Defs/US_MyStudio_Human_Tuning.xml`。它与第 3 节的 Human 音频包共存，却没有任何 patch：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <UniversalSqueaker.UniversalSqueakerTuningBaselineDef>
    <defName>US_MyStudio_Human_Tuning</defName>
    <presetLabel>My Studio Human tuning</presetLabel>
    <presetDescription>Quicker Work calls; keeps the standard trigger plan.</presetDescription>
    <races>
      <li>
        <raceDefName>Human</raceDefName>
        <actions>
          <li>
            <actionKey>Work</actionKey>
            <scope>AnyOccurrence</scope>
            <intervalMultiplier>0.5</intervalMultiplier>
            <probabilityMultiplier>1.0</probabilityMultiplier>
          </li>
        </actions>
        <moods>
          <li><mood>Good</mood><pitchFactor>1.1</pitchFactor><volumeFactor>1.0</volumeFactor><pitchJitter>0.97~1.03</pitchJitter></li>
        </moods>
      </li>
    </races>
  </UniversalSqueaker.UniversalSqueakerTuningBaselineDef>
</Defs>
```

同一预设可在该 Race 条目下添加 `<xenotypes>`，每项以 `xenotypeDefName` 锁定 Race + Xenotype 域；`inheritFromRace` 默认 true，导入时先取父 Race 调音，再以 Xenotype 条目覆盖。Xenotype 层还要求 catalog 已有同名 target 的有效 Xenotype VoicePack 声明（可不勾选）；没有该声明时 resolver 不创建 Xenotype 上下文，导入记录会静默退回 Race/Global。导入是增量且按 `(actionKey, raceDefName, xenotypeDefName)` 覆写：不同 action 可共存，后导入的预设若写同一身份则覆盖它。预设没有 PackDef key，不能做到“玩家勾选 A 包自动采用 A 预设”。同一预设不要重复 Race 块或同 Race 下的 Xenotype 块：导入按顺序后写胜出，而“重置为预设”的查找取首个匹配块，二者会不一致。

只有必须改变预设无法表达的基础 action plan 时，才考虑 `CompProperties_Squeaker` patch。它影响该种族的所有 pawn，并会让自动挂载跳过；从第 13 节的 `CreateDefault()` 核对所需完整基线，定位目标 XML 实际节点（原版 `ThingDef` 与 HAR 的 `AlienRace.ThingDef_AlienRace` 不同），仅在目标没有该 comp 时添加，避免重复挂载。不要把 HAR 专用 XPath 用于任意种族，也不要为普通语音包无条件生成行为 patch。

## 4. PackDef 字段与年龄/彩蛋

| 字段 | 必填 | 实现语义 |
| --- | --- | --- |
| `defName` | 是 | `US_` 前缀为硬校验；前缀之后的文本自由，但它是永久身份而非路由——命名边界见下表后段 |
| `label` | 否 | 继承自 Def 的显示标签，不替代稳定身份 |
| `raceDefName` | 是 | 精确 ThingDef.defName，无首尾空白；不支持通配符或种族列表 |
| `scope` | 是 | `Race` 或 `Xenotype` |
| `targetDefName` | Xenotype 必填 | 精确 XenotypeDef.defName，无首尾空白；Race 包省略 |
| `weight` | 否 | 正有限包权重，默认 1；用于同层合格包之间抽取 |
| `actions` | 是 | 至少一条 `action` + 非空 `sounds`；可附 `ageTag`、`IsEgg` |
| `fallbacks` | 否 | 每条 `action` + 单个 `sound`；同动作只能一条 |

`defName` 不承载任何路由语义：包服务哪个种族只由 `raceDefName`（Xenotype 再加 `scope` + `targetDefName`）精确决定，comp 挂载与玩家勾选域同样只读这些字段。`US_` 前缀之后可自由命名，但两个属性使它实际不可更改：它是稳定键 `packageId:defName` 的一半，玩家勾选按该键持久化在设置中，发布后改名只会使旧勾选失配；它同时是全局 Def 名，须避开与其他模组的撞名（`duplicate_key` 整组拒绝，RimWorld 层同名 Def 也会冲突或互相覆盖）。SoundDef 名同理只查 `US_` 前缀——引用它的 `actions`/`fallbacks` 按 Def 名解析，改名必须同步全部引用。音频目录里的 `<PackDef.defName>` 段是防串音约定，校验器不核对（第 2 节）。

同一个 PackDef 内，`action + ageTag`（含省略的全年龄）不得重复；`IsEgg` 不参与去重。因此不能为同动作同年龄各写一条普通项和彩蛋项。多个声音写进同一条 `sounds`；如需可独立开关的普通包和彩蛋包，可使用两个不同 PackDef。

年龄规则：先找精确年龄项，只有不存在精确项才用全年龄项。精确项存在但被彩蛋开关过滤、或全部音频不可播放时，**不会退回同包全年龄项**，而是尝试同层其他包及后续层。

`ageTag` 接受 `Baby`、`Toddler`、`Child`、`Adult`。当前生产适配器把 Newborn/Baby 映射为 Baby、Child 映射为 Child，其余或缺失阶段映射为 Adult，**不会产生 Toddler**；仅写 Toddler 的音频不能指望当前正常生产路径命中。

`IsEgg` 默认 false；玩家彩蛋开关默认关闭。开启只授予候选资格，不提供额外权重，也不绕过年龄或播放门控。先按包级 `weight` 抽包，再在该项可播放的 SoundDef 列表位置间等权抽取；文件夹内 clip 的选择由 RimWorld grain 机制处理。XML 没有动作项或单个 sound 的 weight 字段。

## 5. 静态校验与运行时可播放性

对 `actions` 引用的每个 SoundDef，US 静态要求：

- defName 以 `US_` 开头，引用非空。
- `context` 为 `MapOnly`。
- 至少一个非空 SubSound；每个 SubSound 的 `onCamera` 为 false，且有非空 grains，列表内不能有 null。

违反 PackDef 字段规则或上述结构规则时，整个 PackDef 不进入 catalog。US 静态校验**不检测**文件内容、响度、静音或目录命名格式。

`fallbacks` 当前只静态检查动作合法且不重复、sound 非空及 `US_` 前缀，未复用 actions 的完整声音结构检查。这是实现差异，不是制作捷径：作者仍应让 fallback SoundDef 满足相同的地图音频规范。

运行时会解析 grain，至少取得一个非空 `ResolvedGrain_Clip`/AudioClip 才有音频候选；无 clip 或解析失败的声音被过滤，其他候选与回退层仍可继续。生产播放还要求 MapOnly、纯地图 SubSound，以及存活、已生成且位于当前地图的 pawn 和 Playing 状态。预览的可播放判定与生产不同，试听成功不能替代生产验证。缓存不分析波形，全静音但可解码的 clip 仍可能被判为 Available。

当前实现允许 `sustain=true` 进入生产池，不再一律拒包。只有触发模式为 `Sustained` 且选中 sustained SoundDef 才调用 `TrySpawnSustainer`；Sustained 模式选中非 sustained 声音则按一次性播放。普通一次性模式走 `PlayOneShot`，不会仅凭 XML 的 sustain 标志启动持续维护。普通短语音推荐 `sustain=false`；持续音需要匹配行为配置并实机检查维持、停止及地图切换。

## 6. 动作、四层回退与模式

PackDef 的 action/fallback action 只接受这 17 个内置键：

`Call / Eat / Sleep / Wounded / Select / Move / Social / Joy / Death / Draft / Undraft / Attack / Work / Equip / MentalBreak / Crying / Giggling`

声明音频不新增触发源；Crying/Giggling 虽为合法键，也不能由此推断默认 comp 已配置这两个动作。任意外部动作字符串不能直接填进这些枚举字段。

| 模式 | 选音行为 |
| --- | --- |
| `Fallback` | XenotypePack → RacePack → PackFallback → BuiltInFallback，取第一个有结果的层 |
| `Remix` | 目标语义为可用层等权；四层分支如此实现，但无当前动作 fallback 声明的三层分支有下述缺陷 |
| `Off` | 只查按种族的 BuiltInFallback profile；不是关闭 US 的生产入口 |
| `Disabled` | 在生产入口旁路 US 发声逻辑 |

**当前 Remix 限制：** 精确池中没有当前动作的 fallback 声明时，会走 `SelectRemixThree`。它按非空层数量抽索引，却未压缩空槽位。例如 Xenotype 层为空、Race 与 BuiltIn 层均有效时，会以一半概率返回空结果，另一半选 Race，BuiltIn 不会命中。仅 Xenotype 与 BuiltIn 有效时也有类似问题。不要把这种无声归因于包格式；需要可靠逐层回退时用 Fallback。声明了当前动作 fallback 的分支走 `SelectRemixFour`，即使该 fallback 暂不可播放也按非空层正确计数。本指南记录该现存实现缺陷，不要求作者添加虚假 fallback 来绕过它。

**PackFallback 只读取当前解析域的精确池。** 有 Xenotype 域时只读该 Race + Xenotype 域中已勾选包的 fallbacks，不再读 Race 包的 fallbacks；Race 普通 actions 仍是第二层。当前域为纯 Race 时才读该 Race 池的 fallbacks。fallback 不受 ageTag/IsEgg 控制，并按包级 weight 抽取。

BuiltInFallback 是数据驱动的种族 profile，不等于自动查找原版种族叫声。当前仓库未附带 profile 种子或音频；没有外部 profile 数据时，Off 与回退末层都可能无声。不要承诺“缺什么动作都会有原版声音兜底”。

`SqueakVoicePackDef` 本身不携带触发、心情、频率或动作范围；SoundDef 提供音频参数，独立 comp patch 可改变种族行为基线，玩家设置仍参与最终调音。

## 7. Xenotype 与 fallback 示例

Xenotype 包必须同时指定种族和 Xenotype，不能只按同名 Xenotype 跨种族通用。目标需存在于已加载的 Biotech Def 数据库；当前没有 HAR 反射推导。Biotech 未启用或目标缺失时，不建立对应 Xenotype 上下文，转向可用的 Race 上下文，否则使用 Global；目标名称歧义时，resolver 直接 fail-closed 到 Global 上下文。

对于带可选 Xenotype 内容的包，把相关 Defs 与音频放进 `1.6/Biotech/`，在上面的 LoadFolders 的 `v1.6` 内添加：

```xml
<li IfModActive="ludeon.rimworld.biotech">1.6/Biotech</li>
```

这是插入片段，不是完整 LoadFolders。若目标还依赖第三方模组，应声明对应依赖或为可选内容加相应加载条件；`loadAfter` 只控制顺序，不提供依赖或加载门控。

以下是第二个 PackDef 示例，放入独立 `<Defs>` 文件。它复用第 3 节已加载的 Call SoundDef：覆盖 Call，在其他普通动作层没有 Select 候选时，让 Select 使用该声音。`Baseliner` 仅作 Biotech 示例，实际包替换为所需的精确目标。

```xml
<Defs>
  <UniversalSqueaker.SqueakVoicePackDef>
    <defName>US_MyStudio_Human_Baseliner</defName>
    <scope>Xenotype</scope>
    <raceDefName>Human</raceDefName>
    <targetDefName>Baseliner</targetDefName>
    <weight>1</weight>
    <fallbacks>
      <li><action>Select</action><sound>US_MyStudio_Human_Call</sound></li>
    </fallbacks>
    <actions>
      <li><action>Call</action><sounds><li>US_MyStudio_Human_Call</li></sounds></li>
    </actions>
  </UniversalSqueaker.SqueakVoicePackDef>
</Defs>
```

不能只写 fallbacks 而把 actions 留空。测试该示例时在 Human + Baseliner 域勾选此包；Race 域的包选择不会自动启用它。

## 8. 音频处理

推荐 OGG Vorbis、mono；22050 Hz mono 可作素材参考，不是 US 强制采样率。实际转码，不要只改扩展名。剪掉首尾多余静音和点击声，试听并调整响度以免 clipping；空文件、静音占位与无权重分发素材不应交付。制作建议不能代替游戏解码验证。

## 9. 安装与验证

1. 用 XML 解析器检查完整文件，再核对 Def 引用、大小写和实际 clip 路径。重启游戏加载修改后的内容，不假定音频缓存支持热更新。
2. 启用独立内容模组及依赖，排序在 US 和目标种族之后。在对应域勾选 PackDef，选择 Fallback，确认未 Disabled。
3. 若附带调音预设，打开 **Presets**，展开该预设，勾选它声明的准确 Race 或 Race + Xenotype，再点击 **Import**；重开设置页确认该域的调音记录带有该预设来源。仅看到预设、仅勾选 PackDef 都不能证明调音已生效。
4. 在当前地图上用目标 pawn 测 Call。Call 默认是概率触发，不保证立即发声；定位无声时查看生产门控与冷却。需要更易复现的选择事件时，可另加 Select action 复用 Call SoundDef。
5. 对预设至少实测一项可观察的行为差异：例如 Work 的 `intervalMultiplier` 前后差异，或将 Joy 设为 `Disabled` 后确认不再派发。测试 Xenotype 项时还要保留一个相同 targetDefName 的有效 Xenotype PackDef，确认上下文实际建立。结果仍会经过全局 cooldown、时间倍率、人口倍率、Talking gate 和音频可播放性；不要把单次没有声音直接归因于预设。
6. 检查开发者派发日志：`Audio route: <action> -> <sound> (<tier>[, egg][, nonplayer]).` 诊断面板的层和包键可帮助区分 Race、Xenotype 与 Pack fallback。派发日志仍需结合实际听感。
7. 验证当前包实际使用的特性：部分动作覆盖、fallback、精确年龄及其不可用情形、彩蛋开关、不同包权重和 Xenotype 匹配；不要只测预览。
8. 测 Off/Fallback/Remix/Disabled；Off 没有 profile 时无声是当前可预期结果。测试 PackFallback 时只移除待测 action 的普通项，保留另一个合法 action，避免因 actions 为空而整包拒绝。

## 10. 排错

| 现象 | 先查什么 |
| --- | --- |
| 包未出现或被拒绝 | XML 类型/引用解析错误、`voicepack` 拒绝日志的具体 invalid 原因；一个坏 action SoundDef 可使整包拒绝 |
| `duplicate_key` | 同 `packageId:defName` 的有效声明重复；同时检查新旧版本和重复加载目录 |
| 包已显示但不响 | 是否在正确域勾选、模式与动作门控、真实音频可播放性、pawn 是否在当前地图 |
| comp 未挂载 | `voicepack.comp.attach_skipped` 的 reason：`race_not_found`、`no_race_props`、`author_patch` 或 `exception`；`author_patch` 表示已有 comp，不等于失败 |
| 精确年龄不响但全年龄有声音 | 精确项存在会遮蔽全年龄项，即使其彩蛋关闭或声音不可用；Toddler 见第 4 节 |
| Race fallback 未用于 Xenotype pawn | 第 6 节的精确域规则；不要把 Race actions 与 Race fallbacks 混为一层 |
| 未覆盖动作无声 / Off 无声 | 是否真的提供了该种族 fallback profile；US 不附带默认音频种子 |
| 预设显示但频率/冷却未变 | 是否在正确 Race/Xenotype 域勾选并点击 **Import**；确认 actionKey 是内置精确名称，且实际触发使用的基础 action plan、全局 cooldown 和其他门控没有掩盖倍率效果 |
| Xenotype 预设导入后无效 | 除导入域外，确认存在该 targetDefName 的有效 Xenotype VoicePack 声明；没有 canonical Xenotype 上下文时只会用 Race/Global 调音 |
| Xenotype 不匹配 | Biotech 是否启用、真实种族及目标 defName、目标是否存在或有歧义、是否在该域勾选 |
| 串音或改名后旧勾选失效 | 全局 Def 名、路径命名空间、稳定 `packageId:PackDef.defName` 是否改变 |
| 旧 SR 类型加载红字 | 按第 1 节迁移类型、标识、引用与依赖；US 不含旧类型桥接 |

## 11. 交付检查

- [ ] 自有且稳定的 packageId、名称、作者；所有 PackDef 与引用 SoundDef 使用唯一 `US_` 名称。
- [ ] 声明精确种族/目标与必要依赖；加载目录门控正确。
- [ ] 音频实际存在，路径与 XML 一致；无空文件、静音占位或意外素材。
- [ ] 完整 XML 可解析，actions/fallbacks 满足第 4–5 节，引用目标均可加载。
- [ ] 有行为调音需求时，提供了独立 `UniversalSqueakerTuningBaselineDef`，且写明玩家需要在 Presets 中导入；没有用 comp patch 代替可用的预设。
- [ ] 仅当必须改变 action plan 的基础字段时才提供 comp patch，未误用部分配置代替完整默认基线。
- [ ] 已记录哪些模式和内容特性通过实机验证，未验证项明确列出。
- [ ] 音频许可与署名清楚；上传或发布在用户授权范围内进行。

## 12. 给 AI 助手的执行约定

根据请求执行制作、修改、迁移或诊断，不把只读咨询扩展为文件变更。实现已授权时，完成范围内的内容文件及静态检查。

1. 确认目标 US 版本、实际种族 defName、依赖和可用音频；小型新包优先采用第 2–3 节结构。不要编造目标 Def、可播放素材或第三方依赖。
2. 保持稳定身份。迁移时检查所有交叉引用与旧行为内容，不仅替换前缀；保留用户未要求更改的素材和配置。
3. 用户要求调节动作触发范围、单动作间隔/概率或心情时，优先创建独立 `UniversalSqueakerTuningBaselineDef`，而非 comp patch；明确它须由玩家手动导入，且不按 PackDef 自动绑定。Xenotype 预设同时需要一个有效 Xenotype PackDef 声明该 target。用户要求 mode、绝对间隔、全局 cooldown、冷却时钟或其他基础 plan 字段时，说明预设无法表达该需求，再评估 comp patch。
4. 区分硬性 validator 规则、运行时门控和制作建议。预设没有 PackDef 同等级的 actionKey validator：只写 17 个精确内置名称，避免未知键被导入后在 resolver 中忽略。当前源码与本文件不一致时，先核对第 13 节的实际实现，再说明适用版本与差异。
5. 用 XML 解析器检查完整生成物，核对引用、重复 action/age、路径和预设域；不为普通包自动添加 comp patch，也不修改 US 主程序来迁就内容。
6. 交付时说明实际改动、静态检查结果和实机未验证项。只要交付预设，就提供“在 Presets 选择域并 Import，随后比较可观察行为”的步骤；没有游戏证据时不要声称“能响”或所有模式通过。

## 13. 维护时的源码入口

下列路径相对 US 仓库根；链接相对本 skill 目录。它们是维护事实的入口，不是普通作者的前置阅读清单。

| 实现 | 核对内容 |
| --- | --- |
| [SqueakVoicePackModels.cs](../../../Source/UniversalSqueaker/Models/SqueakVoicePackModels.cs) | XML 字段、稳定键、`SqueakVoicePackValidator.GetErrors` |
| [SqueakXenotypeCatalog.cs](../../../Source/UniversalSqueaker/Catalog/SqueakXenotypeCatalog.cs) | 接纳、重复键、目标发现与歧义 |
| [VoicePackCompAttach.cs](../../../Source/UniversalSqueaker/Catalog/VoicePackCompAttach.cs) | 声明种族自动挂载及跳过原因 |
| [SqueakKernelAdapter.cs](../../../Source/UniversalSqueaker/Runtime/SqueakKernelAdapter.cs) | 已勾选域投影、动作项固定权重、fallback profile 数据源 |
| [SqueakPoolRegistry.cs](../../../Source/UniversalSqueaker/Kernel/SqueakPoolRegistry.cs) | 年龄先行过滤、包权重、精确域 fallback、模式选层 |
| [SqueakLifeStageResolver.cs](../../../Source/UniversalSqueaker/Runtime/SqueakLifeStageResolver.cs) | 生产年龄映射，无 Toddler 输出 |
| [SqueakSoundAvailability.cs](../../../Source/UniversalSqueaker/Runtime/SqueakSoundAvailability.cs) | clip 解析缓存、预览与生产可播放门控 |
| [CompSqueaker.cs](../../../Source/UniversalSqueaker/CompSqueaker.cs) | `CreateDefault`、一次性/持续派发、距离覆盖和 Disabled 入口 |
| [SqueakActionModel.cs](../../../Source/UniversalSqueaker/Runtime/SqueakActionModel.cs) | 17 个动作及 `Unconfigured` 基线 |
| [UniversalSqueakerTuningBaselineDef.cs](../../../Source/UniversalSqueaker/Runtime/UniversalSqueakerTuningBaselineDef.cs) | 独立预设 XML 形状与可表达字段 |
| [BaselinePresetImporter.cs](../../../Source/UniversalSqueaker/Settings/BaselinePresetImporter.cs) | 手动导入、Race/Xenotype 域、继承与同身份覆写 |
| [SqueakRuntimeResolver.cs](../../../Source/UniversalSqueaker/Runtime/SqueakRuntimeResolver.cs) | settings 调音层投影为运行时 action/mood delta |
| [SqueakTimingModel.cs](../../../Source/UniversalSqueaker/Pure/SqueakTimingModel.cs) | 基础 action interval 与各倍率、全局 cooldown 的计算边界 |

维护这些部分时同步复核本文件。XML 解析只能证明语法，源码对照只能证明当前实现语义，两者都不能代替音频与生产路径的实机测试。
