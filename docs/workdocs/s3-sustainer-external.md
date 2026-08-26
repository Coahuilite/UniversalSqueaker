# 任务书：S3 Sustainer 通路 + 外部动作端到端发声

## 工作目录
仓库根：<workspace>\UniversalSqueaker（下称 <root>，所有路径相对它）。
你有独立的 read/write/pwsh 工具，直接在此工作区操作。只读本任务书；必要时读 HANDOFF.md 第 8 节、MEMORY.md 末尾 2026-08-25 checkpoint。

## 背景（已核实，勿重新推导）
- RimWorld 1.6 mod，net472，主程序集 Source/UniversalSqueaker/，TreatWarningsAsErrors=true，当前 Dev/Release 零警告。
- 动作键已字符串化：内置 17 键 = BuiltInActionKeys（枚举名）；外部动作经 ActionEntryRegistry 注册，键为任意字符串。
- ActionEntry / TriggerBinding / ActionEntryRegistry 已落地（Runtime/ActionEntry.cs）；TriggerBinding.Maintain 是空实现，注释「S3 落地」。
- SqueakTriggerMode.Sustained 是死模式：CompSqueaker.CompTick 的 Sustained 分支 break；NotifyExternal/TryTrigger 不校验 mode，会退化为一次性 PlayOneShot。
- 验证器 SqueakVoicePackValidator（Models/SqueakVoicePackModels.cs）拒绝 sound.sustain；SqueakSoundAvailability（Runtime/SqueakSoundAvailability.cs）把 sound.sustain 判为不可播。

## 目标
分两个阶段，按顺序完成。

### 阶段 1：外部动作端到端发声（plan 构建未接）
现状：CompSqueaker.NotifyExternalByKey（约 line 313）查 actionPlans（仅 17 内置键），外部键查不到 → 静默早退。外部动作经 NotifyExternalByKey(externalKey, origin, source) 应能真正发声，且受 allowExternalActions 动作门约束（IsActionAllowedByKey 已存在，勿改）。

改动清单（精确）：
1. Source/UniversalSqueaker/Runtime/SqueakActionModel.cs 的 SqueakActionPlanFactory 增加：
   public static SqueakActionPlan External(string actionKey) { ... }
   构造外部动作 plan：mode=RandomOneShot、minIntervalTicks=300、probabilityPerCheck=0.02f、ignoreGlobalCooldown=false、cooldownClock=GameTicks；Definition 用带 actionKey 的构造函数：action 传 SqueakAction.Call 作哨兵（外部动作不读 .Action）、ActionKey=actionKey（外部字符串键）、DisplayKey=actionKey、AudioKey=""、VocalGatePolicy=ApplyTalkingGate、SupportedScopes=AnyOccurrence、DefaultScope=AnyOccurrence。
2. Source/UniversalSqueaker/CompSqueaker.cs 增加私有方法：
   private bool TryGetPlan(string actionKey, out SqueakActionPlan plan)
   先查 actionPlans；未命中则查 ActionEntryRegistry.Current.Get(actionKey)，若存在且 !entry.IsBuiltIn → plan = SqueakActionPlanFactory.External(actionKey) 返回 true；否则 false。
3. NotifyExternalByKey 改用 TryGetPlan（替换 actionPlans.TryGetValue）。
4. CompTick 里 actionPlans.TryGetValue 同样改用 TryGetPlan（内置不受影响，更健壮）。
5. 关键正确性修复：EvaluateTiming（约 line 490-493）把两处 ActionKeyOf(plan.Definition.Action) 改为 plan.ActionKey（否则外部动作 .Action=Call 哨兵会错误共享 Call 冷却）。

阶段 1 门禁：dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev 零警告；dotnet run --project tools/UniversalSqueakerKernelTests -c Release 全绿。

### 阶段 2：Sustainer 通路
原版 API（已核实，直接用，using Verse.Sound）：
- SoundDef.TrySpawnSustainer(SoundInfo info) → Sustainer（非 sustain 或无效返回 null）。
- SoundInfo.InMap(new TargetInfo(pawn), MaintenanceType.PerTick)。
- Sustainer.Maintain() / .End() / .Ended。

改动清单：
1. Models/SqueakVoicePackModels.cs 的 SqueakVoicePackValidator：放开对 sound.sustain 的拒绝（允许 sustained SoundDef 进生产池；其余校验保留，尤其 prefix/MapOnly/SubSounds 校验不动）。
2. Runtime/SqueakSoundAvailability.cs：为 sustain 音增加 playability 分支——GetProductionPlayability / GetNativePlayability 不再对 sound.sustain 直接返回 SustainerUnsupported，新增 Sustainer 可播路径（生产可播 = pawn 存活、已生成、在当前地图、ProgramState.Playing、sound.context==MapOnly）。
3. CompSqueaker.cs：
   - 增加字段 private Sustainer? activeSustainer; 与 private string activeSustainerKey = "";（using Verse.Sound）。
   - CompTick 的 case SqueakTriggerMode.Sustained: 不再 break：当该动作处于持续状态（PeriodicStateBinding.Probe 仍返回该动作）时 spawn/维持 sustainer；状态消失则 End。
   - 新增私有方法 TryPlaySustained(string actionKey, ResolvedSqueakContext context, SqueakRuntimeSnapshot snapshot)：plan.Mode==Sustained 时，选音用 snapshot.ChooseProductionSoundByKey(context, actionKey, Pawn)；选中 SoundDef 是 sustain → TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(Pawn), MaintenanceType.PerTick)) 存入 activeSustainer 并记 activeSustainerKey；非 sustain → 退化为一次性 PlayOneShot（优雅降级，不崩）。
   - TryTrigger 的 PlayOneShot 调用点：当 plan.Mode==Sustained 时改走 TryPlaySustained。
   - CompTick 每帧：activeSustainer != null 时，判断 pawn 仍在当前地图且未摧毁且在可视范围，则 Maintain()，否则 End()+置空。
   - PostDestroy：activeSustainer != null → End()+置空。
4. 不改 ActionEntry.cs 中 TriggerBinding.Maintain 签名（S3 sustainer 生命周期放 CompSqueaker，不强制走 TriggerBinding）。

阶段 2 门禁：同阶段 1，另跑 pwsh -File scripts/verify-local.ps1 全 12 门绿（含 log 测试）。

## 红线
- 不改存档 Scribe 形状（不新增/删除已序列化字段）。
- 不引入 Verse/RimWorld 到 Kernel/Pure 编译集。
- 不改 8 个 Harmony patch 的既有判断条件。
- 不碰 UI 文件（Source/UniversalSqueaker/UI/**）、FerriteLib、1.6/Languages/**。

## 完成报告（return 时输出）
1. 改动文件清单（相对路径）。
2. 每条门禁命令的退出结果（build / kernel tests / verify-local）。
3. 设计取舍或遗留（若有）。

## 约束
这是分配给你的唯一任务；除本任务书 + 你改动的文件 + HANDOFF §8 + MEMORY checkpoint 外，不读 docs 下其它长篇文档。
