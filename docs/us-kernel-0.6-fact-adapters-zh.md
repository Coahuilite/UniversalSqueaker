# 0.6 事实—证据—反例维护记录

修订：2026-10-23，r3。维护者：US 1.6 适配维护者。消费者及合同见[规格](us-kernel-0.6-spec-zh.md)、[逐包验收](us-kernel-0.6-acceptance-zh.md)、[首批导读](us-kernel-0.6-contract-review-zh.md)。D-B已接入完整有效配置与Eat生产纵切，下面分别记录直接源码、继承依据、平台替身与原生状态。

## Eat：过程、执行、物品属性与返回结果分别维护

维护者：US 1.6适配维护者。消费者：内置Eat、InjuredEat固定扩展及独立AfterIngestion根；正式字段与入口以[主规格§22](us-kernel-0.6-spec-zh.md#22-d-b-有效配置与eat当前入口)为准。

**来源版本：** 继承的既有项目定点源码核对记录为目标1.6.4871、Assembly-CSharp 1.6.9676.17735，Version.txt rev590／算法rev591差异保留。本轮直接读回RimSage 1.6.4850（程序集1.6.9655.19392）的下列方法体／类型头，并按Krafs 1.6.4871 API编译；4850不是4871同版本复核。精确目标安装只读许可尚待协调，本轮没有读取安装。

| 正式事实／数据 | 原始来源与主体 | 观察和Unknown／依赖 | 生产消费者 |
| --- | --- | --- | --- |
| us.core/IngestionProcess | JobDriver.SetupToils／Cleanup；JobDriver_Ingest自身，FoodFeedPatient.job.targetB.Pawn即Deliveree | 仅支持这两种明确driver；driver、当前Job一致才活跃。自己Ingest另有正向Job事实；feed接缝缺失则未知，不猜喂食者 | Eat固定过程；Whole保持至全过程退出 |
| us.core/IngestionExecution | Toils_Ingest.ChewIngestible(Pawn,float,TargetIndex,TargetIndex)，参数chewer为实际摄入者 | 工厂只挂观察；原init正常返回、driver／Job／CurToil同一才进入，finish关闭；每次真实进入增代。依赖成组Setup/Cleanup及Chew/CurToil；缺Chew不安装半套精确观察 | phase=Execution及固定精确扩展 |
| us.core/IngestibleNutrition | 实际物品ThingDef.IsNutritionGivingIngestible | 物品属性，不是IEatingDriver标记、不是当次营养值；缺实际可摄入物为Unknown。Any不加此依赖 | nutritionScope非Any时 |
| us.core/IngestibleDrug | 同一实际物品ThingDef.IsDrug | 与营养独立；Any不加此依赖。通过actor当前Job targetA重新读取，机器→食物后不沿用机器属性 | drugScope非Any时 |
| us.core/IngestionResultEvent及IngestionResultFact | FinalizeIngest调用Thing.Ingested(Pawn,float)；捕获实际ingester及item，正常返回营养值 | 原版Destroyed／!IngestibleNow的提前返回不构造成功摄入；合法0结果保留。异常不发结果，Job Cleanup也不发结果；事件只存本次快照，结果身份／0值进入ResponseContext和PlaybackRequest | 独立AfterIngestion根，不继承仍在执行的条件 |

**实际数据流：** FoodFeedPatient.MakeNewToils调用ChewIngestible(Deliveree,1.5,A)，但Toil.actor仍是喂食者；物品从actor的Job取、声音归Deliveree的唯一session。FinalizeIngest读取actor.jobs.curJob实际目标并调用Thing.Ingested；FoodFeedPatient会替换Finalize Toil.finishActions，所以结果不挂在该易被覆盖的finish列表。普通Ingest的Corpse分支可跳回同一chewing Toil；一次chewing退出不等于全过程退出。多个同时有效的feed来源指向同一接收者时，执行／物品保持Unknown，不猜一个。

| 判别输入 | 本轮实际检查及结果边界 |
| --- | --- |
| 准备、工厂已构造未进入、原init抛异常 | Whole可成立；Execution不成立，不以工厂调用造进入 |
| 真实进入→退出→同Job再进入 | 精确True→False→True，代次随真实进入增加；诊断读已捕获值，不重采事实／随机 |
| 营养×药物四种物品组合×各筛选值 | 生产采事实与条件组合相符；两属性不互斥，Any不因属性Unknown失效 |
| dispenser→实际食物 | 缺可摄入物为Unknown；目标替换后读取食物属性 |
| 喂食者a、接收者b | b拥有过程／执行及其声音，a不被当摄入者；不要求b自己运行Ingest |
| 正常0返回、无效物品0提前返回、摄入异常、中断Cleanup | 仅正常摄入返回产生独立结果；0不统一失败，后三者不伪造结果 |
| Whole／Execution持续退出 | 同一生产owner／持续槽；Whole不因一次chewing退出停，Execution按阶段退出End，Cleanup关闭Whole |
| OneShot退出 | 不再新起播；不调用旧OneShot裁尾或停止他方声音 |
| 真正编译移除ChewIngestible | PatchAll完成；Whole仍可用，Execution为Unknown／所选精确贡献暂停，具名缺目标；其他过程／结果／Select能力仍可用 |

实现位置：UsIngestionObservation的短生命周期记录与四个窄Harmony hook；UsPawnFacts一次采样；UsPawnPlaybackSession当前有效配置／同一仲裁和持续槽。记录在session reset清除，不Scribe、不写save、不造第二时钟／调度器。Any/Whole的参数不会添加未选可选依赖；固定扩展条件始终保留。

证据：DbEatTests的真实Harmony＋生产适配／settings解析／session／选音／输出链；DbEatAuthorTests运行公开XML和真实loader；DbEffectiveConfigTests及ModSaveCallerTests运行生产settings/ExposeData/原子文件与Scribe替身往返及失败恢复；DbEatUiTests运行真实Source／Host、平台替身。原版Job/Toil/物品/音频为明确double，尸体是复入机制形状而非真实Corpse运行；没有据此签原生Scribe、真实游戏Config、完整4871方法体、原生挂接、屏幕或听感通过。改源时同步事实／消费者／反例，不按新方法名猜合同。
## Select：最终反馈主体与已提交选择分别观察

**来源版本：** RimSage返回的 `Source/RimWorld/Selector.cs`，程序集1.6.9655.19392（1.6.4850）。当前编译目标仍是Krafs引用1.6.4871；二者不是同一个方法体版本。

`Selector.Select`调用私有`SelectInternal(object,bool,bool)`；代理经`CompSelectProxy`递归到最终对象。所核版本在实际接受分支按`playSound`调用`PlaySelectionSoundFor(obj)`，随后加入选择集合。只看公共方法入参或已有集合无法证明本次反馈接受。

生产观察器在每个SelectInternal调用建立线程局部嵌套上下文，捕获实际反馈调用的最终对象；正常返回后核对同一个Selector的`IsSelected`。只有反馈意图、最终Pawn和提交证据相符才发布`us.core/SelectEvent`。异常经finalizer清理；不知道最终主体时不借别的已选Pawn补足。未观察到本次反馈不发布事件。重复选择是否获得反馈由上游实际接受路径决定，US不另加去重政策。

| 反例／输入 | 消费与证据 |
| --- | --- |
| 原调用被拒、无反馈意图、反馈后提交失败 | DaSelectCallerTests实际Harmony路径证明零错误事件／owner；原版Selector为替身 |
| 代理解析为Ratkin、另有Kiiro包 | 最终Ratkin进入同一session，只选准确Ratkin条目 |
| 上游对已有选择再发反馈 | 无US额外去重；仍按同owner的tick／动作／共享硬规则处理 |
| 普通与InjuredSelect同时成立 | 原生通知发布事实而非强制父ActionId，显式优先级仲裁一次，完整子键进配置／条目／截止／诊断 |

以上定向适配检查已执行；精确1.6.4871方法体、真实Harmony挂接、原生音频与听感待后续环境观察。通用Select不含隐藏Pawn资格；官方Pawn的清醒／控制资格仍待用户，当前仅可显式选择进程级开发输入。

### 缺少目标方法：成对停用Select观察，不中断模组启动

SelectInternal与PlaySelectionSoundFor共同实现一个观察能力。仅发现其中一个不能装半套patch。原实现直接返回AccessTools.Method：在明确缺少反馈方法的编译替身上，实际Harmony2.4.2 PatchAll抛HarmonyException（TargetMethod返回null），且另一端已经挂接。Mod构造未捕获该调用；从调用位置可知该异常会传播，未运行原生Mod构造。

现在两patch共用缓存目标与Prepare：任一目标缺失时两者都跳过，一次记录`capability=SelectObservation`、`us.core/SelectEvent`、`SelectObservationUnavailable:MissingTarget:<完整目标>`。没有全局PatchAll异常吞掉或对其他能力的停用。缺实现时SelectEvent保留Unknown并携带具名缺口，不能被直接通知造为True；显式requires该事实的行为贡献暂停，意图／快照不删除。普通Select、继承子合同和独立引用该事实的根均不能起播，无关合同继续成立。

`DaSelectMissingTargetTests`用编译时真正移除的方法，分别验证缺SelectInternal、缺PlaySelectionSoundFor，各13项通过：PatchAll返回、无半套挂接、单次具名诊断、原版选择保留、直接通知／作者调用无伪造、相关来源暂停及无关Work仍派发。两端存在的原63项另通过。这里使用真实Harmony与生产源、明确Selector／音频double，未据此关闭目标1.6.4871原生证据。

## 当前伤口：不是历史受伤通知

`us.core/HasCurrentInjury`由`UsPawnFacts.Capture`读取该Pawn当前`health.hediffSet.hediffs`：存在`Hediff_Injury`且`Severity>0`为True，包括仍存在的伤疤；无此项为False，缺health或hediffSet为Unknown。消费者为示例`example/InjuredSelect`固定附加条件；它不改普通Select，也不从先前Wounded事件缓存True。

生产事实读取和普通／受伤扩展调用已由DaSelectCallerTests的加伤、移除、缺health序列执行；类型／API经目标1.6.4871引用编译，健康集合为明确平台替身。真实游戏伤口／伤疤样本尚未运行。此判据是示例明示的合同，不给全部“受伤”扩展规定永久统一定义。
