# Meowing Kiiro 原始音频格式化记录（2026-09-14）

本机 ffmpeg 8.1.2（`D:\Program Files\FFmpeg\bin`）对 `dist/voicepacks/raw/Meowing Kiiro/`
就地格式化（直接覆盖）。**不含**正式版音频包制作计划——该计划待 skill 修订完成后再制定。

## 处理前状态

53 个 `.ogg` 文件，容器与真实编码不一致：

| 类别 | 数量 | 实际编码 |
| --- | --- | --- |
| 伪装成 `.ogg` 的 FLAC | 31 | Vorbis 容器 + FLAC, stereo, 44100 Hz |
| 已符合建议格式 | 22 | Vorbis, mono, 22050 Hz |

即 31 个文件只是扩展名叫 `.ogg`，并未真正转码——这正是 skill 第 8 节警示的“只改扩展名”情形。
总时长 62.2 s，2.6 MB。

## 采用的处理链

```
silenceremove(peak, -45 dBFS, 首尾各一遍, 中间 areverse)
→ aformat=22050 Hz / mono
→ volume=<标定增益> dB
→ libvorbis -q:a 5
```

原始素材备份于 `dist/voicepacks/backup/MeowingKiiro-raw-20260914/`（53 文件，未改动）；
所有输出都从该备份重新生成，因此不存在“在已处理结果上叠加处理”的代际损失。

### 响度标定要点

第一次尝试用 `loudnorm` 失败：素材普遍 0.4–3.2 s，短于该滤波器的统计窗口，
`input_i` 返回 `-inf`，无法作为两遍模式输入。改用直接方案：

- 目标 RMS −16 dBFS（各 clip 响度对齐，避免忽大忽小）；
- 峰值上限 −1.0 dBFS，预测阶段留 −1.5 dBFS 余量。

关键陷阱：**重采样与 Vorbis 重建会使峰值高于编码前测得的峰值**（实测过冲 0.0–0.8 dB）。
按“编码前峰值”算增益时，31 个原 FLAC 素材里有 30 个输出出现真实削波（样本绝对值 > 1.0，
峰值最高 +1.03 dBFS）。因此最终流程改为**往返标定**：先以单位增益跑一遍同链，解码实测重建
过冲并计入增益，编码后再逐文件解码复核，仍有超限则按超出量衰减重编（最多 4 轮）。

## 逐文件复核结果（53/53 通过）

对每个输出文件解码为 float64 mono 后直接统计样本，不依赖 ffmpeg 文本日志：

- 容器/编码：全部 `ogg` + `vorbis` + `channels=1` + `22050 Hz`；
- 非空判定：全部样本数 > 2000、时长 0.39–3.24 s、字节数 > 2000，无空白或静音占位；
- 无解码错误（`Invalid data` / `error while decoding` 计数为 0）；
- 零超限样本（`|x| > 1.0` 总数 0），最高峰值 −1.07 dBFS；
- RMS 范围 −22.0 … −15.9 dBFS：36 个精确对齐到 −16 dBFS，其余 17 个因峰值天花板受限而低于目标
  （`attack_02` −22.0、`move_01` −21.5、`sleep_01/03`、`attack_03`、`wounded_01/03`、`death_02/03`、
  `draft_03/04`、`sleep_02`、`move_02`、`eat_02/04`、`social_03`、`select_04`）；
- 首尾静音裁除：总时长 62.2 s → 51.9 s（−16.6%），无输出长于源文件的情况。

按动作分布：`eat`/`joy` 5 条，`draft`/`select`/`social`/`work` 4 条，其余 7 类各 3 条；
覆盖 15 个内置动作键，未提供 `Crying`/`Giggling`。

## 已知限制

- 以上为机器可复核的静态属性，**不等于**游戏内解码与触发已验证；
- 人耳试听（点击声、齿音、意外素材）尚未进行；
- 音频许可与署名仍**未确认**，不得分发；
- 正式版音频包的结构、Def 命名与校验规则待 skill 修订版给出指导后再制定。


## 种族身份核验（2026-09-14，维护者已裁定 Race 级、无 Xenotype）

- `raceDefName` = **`Kiiro_Race`**（精确、区分大小写）。来源：Workshop 模组
  `I:\SteamLibrary\steamapps\workshop\content\294100\2988200143`（`Ancot.KiiroRace`，
  display name “Kiiro Race”）`1.6/Defs/ThingDefs_Race/Race_Kiiro.xml` 中
  `AlienRace.ThingDef_AlienRace` 的 `<defName>`；该模组 `supportedVersions` 含 1.6。
- 依赖：`erdelf.HumanoidAlienRaces`（HAR）与 `ancot.ancotlibrary`（同列表激活）；
  当前 `ModsConfig.xml` 中 `ancot.kiirorace` 处于激活状态。
- 旁证：旧实验包 `US_MeowingKiiroExp`（`dist/voicepacks/final-test/Kiiro-US-EXP`）与
  路由测试包均声明 `Kiiro_Race`，且旧 packageId `coahuilite.squeakyratkin.meowingkiiroexp`
  仍残留于玩家 Config 的 US 勾选记录中——正式版换占位 packageId 后该旧键自然失效，
  属预期（未发布，无玩家勾选失配问题）。
- 环境中另有 `ZuoYao.KiiroSiamese`（`KiiroXenotype_Siamese`），按本次裁定不建 Xenotype 包。
- 模组显示名确定为 **MeowingKiiro**；音频文件名覆盖 15 个动作键，与默认生产动作表一致
  （无 Crying/Giggling）。