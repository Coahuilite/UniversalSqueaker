# MeowingKiiro 正式版 VoicePack 制作计划（2026-09-14）

依据 `.github/skills/us-voicepack-authoring/SKILL.md` 修订版（工作区改动版，含
`UniversalSqueakerTuningBaselineDef` 预设机制）与已核验事实制定。音频格式化已完成，
见 `docs/audio-formatting-meowing-kiiro-zh.md`。

> **状态更新（同日开工）**：定位 = 依修订版 skill 制作的**第一个正式音频包**，用于验证 skill
> 流程本身（与旧 `Kiiro-US-EXP` 实验包无关）。身份定稿：packageId `saryaki.meowingkiiro.voices`
> （作者命名空间小写约定）、作者 Saryaki、音频许可已确认（合作关系）。骨架已生成于
> `dist/voicepacks/MeowingKiiro/`，静态自查与 53/53 字节一致性校验通过；About `<description>`
> 按作者要求留 **TBD**，交由 Saryaki 撰写。剩余 = 实机验证。

## 已确认输入

| 项 | 值 | 来源 |
| --- | --- | --- |
| 模组显示名 | MeowingKiiro | 维护者裁定 |
| packageId | `saryaki.meowingkiiro.voices`（作者命名空间小写约定） | 维护者裁定 2026-09-14 |
| 作者 | Saryaki（音频原作者，合作关系授权，许可已确认） | 维护者裁定 2026-09-14 |
| About 介绍 | **TBD**，交由作者撰写 | 维护者裁定 2026-09-14 |
| scope / raceDefName | `Race` / `Kiiro_Race` | 维护者裁定 + `Ancot.KiiroRace` 1.6 `Race_Kiiro.xml` 核验 |
| Xenotype | 无（不建 Biotech 目录、不写 Xenotype 包） | 维护者裁定 |
| 动作覆盖 | 15 个内置键（文件名前缀即动作提示），无 Crying/Giggling | 维护者裁定 + raw 目录实证 |
| 素材 | `dist/voicepacks/raw/Meowing Kiiro/` 53 条，ogg/vorbis/mono/22050，逐样本验证通过 | 格式化记录 |
| 种族依赖 | `erdelf.humanoidalienraces` + `ancot.ancotlibrary`（Kiiro Race 实际依赖） | Workshop About.xml |

## 包结构

```text
MeowingKiiro/
|- About/About.xml
|- LoadFolders.xml                       # <v1.6><li>1.6/Race</li></v1.6>
`- 1.6/Race/
   |- Defs/SoundDefs/US_MeowingKiiro_Kiiro_Sounds.xml
   `- Sounds/saryaki.meowingkiiro.voices/US_MeowingKiiro_Kiiro/
      |- Attack/attack_01..03.ogg        (3)
      |- Call/call_01..03.ogg            (3)
      |- Death/death_01..03.ogg          (3)
      |- Draft/draft_01..04.ogg          (4)
      |- Eat/eat_01..05.ogg              (5)
      |- Equip/equip_01..03.ogg          (3)
      |- Joy/joy_01..05.ogg              (5)
      |- MentalBreak/mentalbreak_01..03.ogg (3)
      |- Move/move_01..03.ogg            (3)
      |- Select/select_01..04.ogg        (4)
      |- Sleep/sleep_01..03.ogg          (3)
      |- Social/social_01..04.ogg        (4)
      |- Undraft/undraft_01..03.ogg      (3)
      |- Work/work_01..04.ogg            (4)
      `- Wounded/wounded_01..03.ogg      (3)   合计 53
```

- **clip 文件名零改动**：raw 的 `call_01.ogg` 风格与 skill §2 示例一致；唯一处理是按
  动作前缀复制到 PascalCase `<Action>/` 目录（`mentalbreak_*` → `MentalBreak/`）。
- `clipFolderPath` 写 `<packageId>/US_MeowingKiiro_Kiiro/<Action>`，不含 `Sounds/`、不含扩展名。

## Def 清单

- 1 × `UniversalSqueaker.SqueakVoicePackDef`：`US_MeowingKiiro_Kiiro`，
  `label=MeowingKiiro`，`scope=Race`，`raceDefName=Kiiro_Race`，`weight=1`，
  `actions` 15 条（每条 `action` + 该动作全部 SoundDef），无 ageTag / IsEgg / fallbacks。
- 15 × `SoundDef`：`US_MeowingKiiro_<Action>`，`sustain=false`，`context=MapOnly`，
  单 SubSound（隐式 `onCamera=false` + 一个 `AudioGrain_Folder`），
  `volumeRange=45~55`，`pitchRange=0.95~1.05`，`distRange=15~70`（US 距离设置覆盖之，仅为初值）。
- **不附带** `UniversalSqueakerTuningBaselineDef` 预设（无行为调音需求），
  **不附带** `CompProperties_Squeaker` patch（skill §3.5：普通包两者皆不需）。

## About.xml 要点

- `packageId=saryaki.meowingkiiro.voices`；`name=MeowingKiiro`；`author=Saryaki`；
  `supportedVersions` 仅 1.6；`description` 留 TBD（作者撰写）。
- `modDependencies`：`coahuilite.universalsqueaker`、`erdelf.humanoidalienraces`、
  `ancot.kiirorace`；`loadAfter` 同集合（排序在 US 与种族之后）。

## 制作步骤

1. 建骨架 + About.xml + LoadFolders.xml（packageId 三处一致：About、音频目录、clipFolderPath）。 ✅
2. 复制 53 clip 到 `<Action>/` 目录；计数校验（3/3/3/4/5/3/5/3/3/4/3/4/3/4/3 = 53）；
   SHA256 全量比对 raw↔pack 字节一致。 ✅
3. 写 `US_MeowingKiiro_Kiiro_Sounds.xml`：PackDef + 15 SoundDef。 ✅
4. 静态自查：XML 解析器过全文；action 名逐字对照 17 键表；`US_` 前缀；
   `clipFolderPath` 与实际目录逐一对应；`action+ageTag` 无重复；引用非空。 ✅（pwsh 静态校验通过）
5. 实机验证（游戏证据，缺一不可声称）：Race `Kiiro_Race` 域勾选 + Fallback，测 Call
   （概率触发，配合 Select 更易复现）与其余动作抽查；Off/Fallback/Remix/Disabled 过一遍；
   派发日志 `Audio route: <action> -> <sound> (<tier>)` 与听感对照；结果记录进本文件。

## 已知边界与阻塞项

- 旧实验包（`US_MeowingKiiroExp`，`coahuilite.squeakyratkin.meowingkiiroexp`）保持 archive
  原样：不迁移、不引用；其类型/前缀与 US 正式规范不符，属 SR 时代产物。
- 玩家 Config 中旧 EXP 勾选残留随占位 packageId 换用自然失配，无发布影响（旧包未发布）。
- Remix 模式在「当前动作无 fallback 声明」时有已知实现缺陷（skill §6）——交付文案推荐
  Fallback 模式，不为此伪造 fallback。
- ~~音频许可与署名未确认~~ **已解除（2026-09-14）**：Saryaki 创作并授权，合作关系。
  发布/上传动作仍需维护者授权（AGENTS.md 外部状态边界）。
- 实机验证未执行前，本包只承诺静态正确性；作为 skill 流程测试包，验证结果（含 skill 与
  实现不符之处）应回写本文件并反馈到 skill 修订。
