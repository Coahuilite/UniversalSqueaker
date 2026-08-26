# S0 数据模型设计（ActionTuningRecord / UniversalSqueakerTuningBaselineDef / US.TuningFeedback.v1）

> 状态：定稿（2026-08-25，主会话产出）。
> 范围：S2（分层表 + 动作门）与 S4（调音编辑器 + 反馈）的硬前置数据契约。
> 证据：字段 presence 范本来自 Models/SqueakXenotypePresetModels.cs；域类型来自 Kernel/Domain.cs；
> 枚举键映射来自 Kernel/ActionKey.cs + Kernel/BuiltInActionKeys.cs。

## 1. ActionTuningRecord（分层表统一记录）

替换 `globalActionEnabled`（List<GlobalActionEnabledRecord>）与 `xenotypePresets[].actionOverrides`（List<XenotypeActionBehaviorOverride>）。

### 1.1 字段与三态判定

```csharp
// net472 无 record，手写 class + 值相等（域键判定用 Ordinal 字符串比较）。
public class ActionTuningRecord : IExposable
{
    // 动作键：替代 SqueakAction 枚举。内置键 = BuiltInActionKeys（17 键，序数对齐 SqueakAction）。
    public string actionKey = "";
    // 分层域：全空=Global，仅 race=Race，race+xeno=Xenotype。
    public string raceDefName = "";
    public string xenotypeDefName = "";

    // 动作开关 + scope（沿用 SqueakActionScope: Disabled/AnyOccurrence/ActiveCommand）。
    public bool hasScope;
    public SqueakActionScope scope = SqueakActionScope.AnyOccurrence;

    // 行为调音（沿用 XenotypeActionBehaviorOverride 字段，加 hasX presence）。
    public bool hasIntervalMultiplier;
    public float intervalMultiplier = 1f;
    public bool hasProbabilityMultiplier;
    public float probabilityMultiplier = 1f;

    public void ExposeData()
    {
        Scribe_Values.Look(ref actionKey, "actionKey", "");
        Scribe_Values.Look(ref raceDefName, "raceDefName", "");
        Scribe_Values.Look(ref xenotypeDefName, "xenotypeDefName", "");
        Scribe_Values.Look(ref hasScope, "hasScope", false);
        Scribe_Values.Look(ref scope, "scope", SqueakActionScope.AnyOccurrence);
        Scribe_Values.Look(ref hasIntervalMultiplier, "hasIntervalMultiplier", false);
        Scribe_Values.Look(ref intervalMultiplier, "intervalMultiplier", 1f);
        Scribe_Values.Look(ref hasProbabilityMultiplier, "hasProbabilityMultiplier", false);
        Scribe_Values.Look(ref probabilityMultiplier, "probabilityMultiplier", 1f);
    }
}
```

三态判定（在设置层聚合时统一）：
- 全空（raceDefName=="" && xenotypeDefName==""）= Global 层；
- 仅 race（raceDefName!="" && xenotypeDefName==""）= Race 层；
- race+xeno（raceDefName!="" && xenotypeDefName!=""）= Xenotype 层；
- 非法（race 空但 xeno 非空）→ 加载时归一化为 Xenotype 层需 race，否则 drop（迁移失败 fail-closed）。

`hasX=false` = 该层不做决定、向下继承（沿用 XenotypeActionBehaviorOverride 的 presence 语义）。

### 1.2 Scribe 落点

- Settings 新增字段：`List<ActionTuningRecord> actionTuning = new()`；
- `Scribe_Collections.Look(ref actionTuning, "actionTuning", LookMode.Deep)`（与 globalActionEnabled 同 Deep）。

### 1.3 旧字段迁移方向（详见 docs/us-s0-scribe-migration-zh.md §6）

- `globalActionEnabled` → 全局层 ActionTuningRecord（actionKey=ActionKey.For(action)，race/xeno 空，hasScope=true，scope=NormalizeScope 后的值，interval/probability hasX=false）。
- `xenotypePresets[].actionOverrides` → Xenotype 层 ActionTuningRecord（携带 raceDefName + xenotypeDefName；hasEnabled→hasScope+scope=Disabled 或 AnyOccurrence）。
- Race 层无旧数据源，由 UI 新增。

## 2. UniversalSqueakerTuningBaselineDef（独立只读 XML Def，最终名已定）

### 2.1 边界

- **独立只读 Def**：DefDatabase 加载，模组不写回，不防玩家手改文件。
- **不进 CompProperties_Squeaker、不进 SqueakVoicePackDef**（这是玩家调音 baseline，与作者挂载面分离）。

### 2.2 字段清单（作者 baseline 的调音内容）

```csharp
public class UniversalSqueakerTuningBaselineDef : Def
{
    // 动作级：动作键 → 触发/调音默认（对应 CompProperties_Squeaker.actions 的玩家可调部分）。
    public List<BaselineActionTuning> actions = new();
    // 心情级：心情 → 音调/音量/抖动默认（对应 CompProperties_Squeaker.moodMods）。
    public List<SqueakMoodMod> moodMods = new();
}

public class BaselineActionTuning
{
    public string actionKey = "";   // 必须 ∈ BuiltInActionKeys
    public SqueakTriggerMode mode = SqueakTriggerMode.RandomOneShot;
    public int minIntervalTicks = 216;
    public float probabilityPerCheck = 0.02f;
    public bool ignoreGlobalCooldown = false;
    public SqueakCooldownClock cooldownClock = SqueakCooldownClock.GameTicks;
}
```

字段对应关系（作者面 → 玩家 baseline）：
- `actions`（动作级 mode/minInterval/probability/ignoreGlobalCooldown/cooldownClock）与 CompProperties_Squeaker.actions 的 SqueakActionConfig 字段一一对应。
- `moodMods` 复用 SqueakMoodMod（pitchFactor/volumeFactor/pitchJitter，已有 IExposable + Clone）。

### 2.3 字段级 last-wins 合并规则

baseline Def → 玩家 override（ActionTuningRecord / moodOverride）逐字段覆盖 → 运行时生效。
- 每个字段按 presence 标志判断：玩家层 hasX=true 才覆盖 baseline 对应字段；hasX=false 继承 baseline。
- 动作级合并键 = actionKey；心情级合并键 = SqueakMood。

## 3. US.TuningFeedback.v1（导入/导出 delta 契约）

只含玩家 override delta（不含 baseline），作为分享 + 向作者反馈的途径。

### 3.1 JSON 形状 schema

```json
{
  "contract": "US.TuningFeedback",
  "version": 1,
  "actions": [
    {
      "actionKey": "Call",
      "raceDefName": "",
      "xenotypeDefName": "",
      "scope": "AnyOccurrence",
      "intervalMultiplier": 1.2,
      "probabilityMultiplier": 1.0
    }
  ],
  "moods": [
    {
      "mood": "Good",
      "raceDefName": "",
      "xenotypeDefName": "",
      "pitchFactor": 1.1,
      "volumeFactor": 1.0,
      "pitchJitter": [0.97, 1.03]
    }
  ]
}
```

### 3.2 导入校验规则

1. contract 字段必须等于 "US.TuningFeedback"，version 必须 == 1。
2. actionKey 必须 ∈ BuiltInActionKeys（未知键丢弃 + 一条告警，不整体失败）。
3. raceDefName/xenotypeDefName 三态合法（xeno 非空必须 race 非空）。
4. scope 必须 ∈ {Disabled, AnyOccurrence, ActiveCommand}。
5. 数值 NaN/Infinity 拒绝（沿用 Sanitize 语义）。
6. 导入 = 覆写对应分层域的玩家 override（last-wins），不删未出现在文件里的其他域。

### 3.3 导出内容

- 导出 = 当前 settings.actionTuning 里所有玩家 override 记录 + mood override 记录（delta，非合并结果）。
- 不含 baseline Def 内容（baseline 是作者资产，玩家反馈只含自己的 delta）。

## 4. 参考约束

- Kernel 只认 string 键 + 域类型（Kernel/Domain.cs 的 RaceKey/XenotypeKey/AudioDomain），不认 Pawn/ThingDef/SoundDef。
- actionKey 双向映射已就绪：Kernel/ActionKey.cs（For/TryParseBuiltIn），内置键权威 Kernel/BuiltInActionKeys.cs。
- net472 无 record，手写 class + Ordinal 字符串相等。
