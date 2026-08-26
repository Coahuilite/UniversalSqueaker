# S4 阻塞设计：UniversalSqueakerTuningBaselineDef 字段契约

> 状态：主会话设计稿（2026-08-25）。本文件是 S4-Tuning-Backend 任务书的输入契约，不直接派给 worker 之外使用。
> 依据：计划 §4（三层数据模型）、§5（分层动作作用域）、已核实代码 SqueakActionModel.cs / SqueakRuntimeResolver.cs / CompSqueaker.ResolveMoodMod / UniversalSqueakerFallbackProfileDef.cs。

## 1. 定位

- 只读 XML Def（DefDatabase 加载，US 不写回、不 ship 种子数据；空 = 用内置默认）。多 Def 时 last-wins 合并（后加载覆盖先加载），缺省字段落回内置默认。
- 承载「玩家可调的调音 baseline」**底**：动作作用域 + 间隔/概率乘数 + mood 调制。**不含**触发配置（mode/minIntervalTicks/probabilityPerCheck 属作者 per-pack CompProperties_Squeaker.actions，不在本 Def）。
- 与 FallbackProfileDef 同模式：无产品字面量、无 race 种子；第三方 mod 可 ship 自己的 baseline Def 覆盖默认。

## 2. 字段集（定稿）

```csharp
// Runtime/UniversalSqueakerTuningBaselineDef.cs（新文件）
public class UniversalSqueakerTuningBaselineDef : Def
{
    public List<BaselineActionTuning> actions = new();
    public List<BaselineMoodTuning> moods = new();
}

public class BaselineActionTuning
{
    public string actionKey = "";                       // 内置键 = BuiltInActionKeys；外部键 = 注册方声明
    public SqueakActionScope scope = SqueakActionScope.AnyOccurrence;
    public float intervalMultiplier = 1f;
    public float probabilityMultiplier = 1f;
}

public class BaselineMoodTuning
{
    public SqueakMood mood = SqueakMood.Neutral;
    public float pitchFactor = 1f;
    public float volumeFactor = 1f;
    public FloatRange pitchJitter = FloatRange.One;
}
```

字段语义与运行时类型严格同构：
- BaselineActionTuning ↔ RuntimeActionDelta(scope, intervalMultiplier, probabilityMultiplier)。
- BaselineMoodTuning ↔ SqueakMoodMod(mood, pitchFactor, volumeFactor, pitchJitter)。

## 3. 消费点改动

### 3.1 动作作用域/乘数（SqueakRuntimeResolver.BuildGlobalActions）

当前：result[key] = new RuntimeActionDelta(settings.GetActionGlobalScope(action), 1f, 1f)，再叠加 actionTuning Global 层。

改为：
1. 新增静态辅助 BaselineTuningTable（合并所有 Def，last-wins，按 actionKey 存 BaselineActionTuning；按 SqueakMood 存 BaselineMoodTuning）。
2. BuildGlobalActions 初值：
   - scope = baseline.TryGetScope(key) ?? SqueakActionDefinitions.Get(action).DefaultScope；
   - interval = baseline.TryGetInterval(key) ?? 1f；
   - prob = baseline.TryGetProb(key) ?? 1f。
3. actionTuning Global 层记录仍叠加其上（保持现状，勿动）。

### 3.2 mood 调制（CompSqueaker.ResolveMoodMod）

当前三层：Props.moodMods(作者) → Settings.moodOverrides(玩家全局) → context.GetMoodDelta(xenotype)。

改为插入 baseline 为第二层（作者之后、玩家全局覆盖之前）：
1. Props.moodMods（作者 per-pack 默认）。
2. baseline.TryGetMood(mood)（玩家调音 baseline 底，新增）。
3. Settings.moodOverrides（玩家全局覆盖）。
4. context.GetMoodDelta(mood)（xenotype 层）。

## 4. 未定/不做

- 不做 Scribe（baseline 只读，不进存档）。
- 不做触发配置字段（mode/minIntervalTicks 等），那是作者面。
- 不删 globalActionEnabled 链路（S5 清理才删；S4-Tuning-Backend 只让 baseline + actionTuning 成为「更优先」的来源，globalActionEnabled 仍作为 actionTuning 为空时的 fallback，保持现状）。

## 5. S4-Tuning-Backend 任务书要点（下一步写）

1. 新增 Runtime/UniversalSqueakerTuningBaselineDef.cs（含 BaselineActionTuning/BaselineMoodTuning）。
2. 新增 Runtime/BaselineTuningTable.cs：单例，扫描 DefDatabase<UniversalSqueakerTuningBaselineDef>.AllDefs 合并（last-wins），暴露 TryGetScope/TryGetInterval/TryGetProb(按 actionKey) + TryGetMood(按 SqueakMood)。
3. SqueakRuntimeResolver.BuildGlobalActions 消费 baseline 初值。
4. CompSqueaker.ResolveMoodMod 插入 baseline 层。
5. 门禁：Dev 构建零警告 + kernel tests 全绿 + verify-local 12 门绿。
