# 任务书：S4-Tuning-Backend（调音 baseline Def + 消费点）

## 工作目录
仓库根：<workspace>\UniversalSqueaker（下称 <root>）。
只读本任务书 + 设计契约 docs/workdocs/s4-tuning-baseline-design.md + 你改动的源文件 + HANDOFF §8 + MEMORY checkpoint。

## 前置
设计契约已由主会话定稿：docs/workdocs/s4-tuning-baseline-design.md。**字段集与消费点以该契约为唯一权威**，不要自行增删字段。

## 背景（已核实）
- RimWorld 1.6，net472，主程序集 Source/UniversalSqueaker/，TreatWarningsAsErrors=true，Dev/Release 零警告。
- SqueakActionDefinitions.Get(action) 提供 17 内置动作 DefaultScope（Runtime/SqueakActionModel.cs）。
- SqueakRuntimeResolver.BuildGlobalActions（Runtime/SqueakRuntimeResolver.cs 约 line 106-133）当前：result[key]=new RuntimeActionDelta(settings.GetActionGlobalScope(action),1f,1f)，再叠加 actionTuning Global 层。
- CompSqueaker.ResolveMoodMod（CompSqueaker.cs 约 line 548-577）当前三层：Props.moodMods(作者) → Settings.moodOverrides(玩家全局) → context.GetMoodDelta(xenotype)。
- 参考 Def 模板：Runtime/UniversalSqueakerFallbackProfileDef.cs（只读 XML Def + DefDatabase 扫描）。
- SqueakMoodMod 定义在 Settings/UniversalSqueakerSettings.cs（含 mood/pitchFactor/volumeFactor/pitchJitter，Clone()）。

## 改动清单（精确，按契约 §2/§3）

1. 新增 Runtime/UniversalSqueakerTuningBaselineDef.cs：
   - class UniversalSqueakerTuningBaselineDef : Def { List<BaselineActionTuning> actions = new(); List<BaselineMoodTuning> moods = new(); }
   - class BaselineActionTuning { string actionKey=""; SqueakActionScope scope=AnyOccurrence; float intervalMultiplier=1f; float probabilityMultiplier=1f; }
   - class BaselineMoodTuning { SqueakMood mood=Neutral; float pitchFactor=1f; float volumeFactor=1f; FloatRange pitchJitter=FloatRange.One; }
   - using 顺序与 FallbackProfileDef 一致（Verse + UniversalSqueaker.Kernel 需要的键；FloatRange 来自 Verse）。

2. 新增 Runtime/BaselineTuningTable.cs（静态单例）：
   - 扫描 DefDatabase<UniversalSqueakerTuningBaselineDef>.AllDefs；多 Def last-wins（后加载覆盖先加载）。
   - 合并为 Dictionary<string,BaselineActionTuning>（按 actionKey，Ordinal）+ Dictionary<SqueakMood,BaselineMoodTuning>。
   - 暴露：bool TryGetScope(string key, out SqueakActionScope)、TryGetInterval(string,out float)、TryGetProb(string,out float)、bool TryGetMood(SqueakMood, out BaselineMoodTuning)。
   - 空/缺失 = 找不到，调用方落回默认。扫描需可重复调用（每次全量重建，避免 DefDatabase 生命周期问题）。

3. SqueakRuntimeResolver.BuildGlobalActions 消费 baseline 初值（契约 §3.1）：
   - 初值 scope = BaselineTuningTable.TryGetScope(key) ?? SqueakActionDefinitions.Get(action).DefaultScope；
   - interval = TryGetInterval ?? 1f；prob = TryGetProb ?? 1f；
   - actionTuning Global 层记录仍叠加其上（勿动现有叠加逻辑）。

4. CompSqueaker.ResolveMoodMod 插入 baseline 层（契约 §3.2）：
   - 在 Props.moodMods 之后、Settings.moodOverrides 之前，插入 baseline：若 BaselineTuningTable.TryGetMood(mood, out b) 命中，用 b 的 pitchFactor/volumeFactor/pitchJitter 覆盖当前 mod（字段级覆盖，与 moodOverrides 同语义）。
   - 之后 Settings.moodOverrides 与 context.GetMoodDelta 逻辑保持原样。

## 红线
- 不改 Scribe 形状；baseline Def 不进存档。
- 不引入 Verse/RimWorld 到 Kernel/Pure 编译集（新增文件放 Runtime/，属于 Verse 适配层）。
- 不碰 UI、FerriteLib、1.6/Languages、8 个 Harmony patch、S3 涉及的 Sustainer/外部动作逻辑。
- 不删 globalActionEnabled 链路（S5 才删）。

## 门禁
1. dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev（零警告）
2. dotnet run --project tools/UniversalSqueakerKernelTests -c Release（全绿）
3. pwsh -File scripts/verify-local.ps1（12 门全绿）

## 完成报告（return 输出）
1. 改动文件清单（相对路径）。
2. 三条门禁退出结果。
3. 设计取舍或遗留。
