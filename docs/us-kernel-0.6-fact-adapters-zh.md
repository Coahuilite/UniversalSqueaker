# 0.6 事实—证据—反例维护记录

修订：2026-10-23，r1。维护者：US 1.6 适配维护者。消费者及合同见[规格](us-kernel-0.6-spec-zh.md)、[逐包验收](us-kernel-0.6-acceptance-zh.md)、[首批导读](us-kernel-0.6-contract-review-zh.md)。本轮先交 Eat 的实际调查记录；新 Eat settings／阶段适配纵切属于 D-B。

## Eat：正在摄入、物品属性、结果是三类证据

Ratkin p1 走向一份饭时，可以处于摄入全过程，但尚未进入摄入执行。啤酒既可有营养属性又属于药物；一次摄入返回 0 营养也不能据此判定没有摄入。这三点必须在适配中分开。

**版本依据：** 继承的既有项目定点源码核对记录：RimWorld 1.6.4871 Assembly-CSharp方法体，程序集1.6.9676.17735；Version.txt rev590／程序集算法rev591差异保留。具体符号和支持的结论逐行列于下表，本次没有重新读取游戏安装。参考程序集只证明API；源码核对、替身与原生状态分别列出。

| 稳定语义／候选事实身份 | 主体与原始来源 | 进入／退出／结果；依赖及 Unknown | 当前消费者／差距 |
| --- | --- | --- | --- |
| 摄入全过程／us.core/EatWholeJob（当前键） | 实际摄入者；RimWorld.JobDriver_Ingest.MakeNewToils | 准备、取食、执行、Finalize 是不同阶段；中断结束过程，不合成成功事件。无当前 driver 的必要证据为 Unknown | UsPawnFacts.Capture 目前只认 JobDefOf.Ingest；EatState 仍经 PeriodicStateBinding 的旧布尔政策。被喂食覆盖未实现 |
| 摄入执行／us.core/EatChewing（当前键） | 实际 chewer；Toils_Ingest.ChewIngestible 所构造 Toil 的实际进入与退出 | 工厂被调用只证明 Toil 已构造，不证明已进入；下一次复入须是新阶段观察。所选执行模式 Unknown 不回 WholeProcess | 当前只比较 CurToilString 与 ChewingToilDebugName；不构成可靠阶段覆盖，D-B 须替换 |
| 被喂食角色／后续适配事实 | JobDriver_FoodFeedPatient.MakeNewToils，Deliveree 为 chewer | Job 执行者是喂食者，接收者才是摄入者；接收者自己的当前 Job 不必是 Ingest | 当前只采 Pawn 自己的 Ingest，不能把喂食者当摄入者；D-B 需要按接收者实例发布事实 |
| 当前摄入物／后续适配事实 | Toils_Ingest.TakeMealFromDispenser | targetA 从 dispenser 变为实际食物，必须随阶段读取已解析食物；初始机器不代表最终摄入物 | 当前用 job.targetA.Thing.def.IsDrug；没有独立物品快照或目标切换证据 |
| 阶段复入／后续适配事实 | JobDriver_Ingest 的 Corpse 分支 | 同 Job 可再次进入 chewing；一次阶段退出不代表全过程退出 | 当前无阶段代次／退出观察；D-B 正反例必须覆盖同 Job 二次进入 |
| 物品营养属性／后续正式事实 | ThingDef.IsNutritionGivingIngestible | 可摄入且缓存营养正值的物品属性，不是 Pawn 当次实得营养；Any 不读取该可选筛选，选仅有／无属性才依赖 | 当前 us.core/EatNutrition 来自 IEatingDriver.GainingNutritionNow，混合物品属性与当前 Toil，不能作为独立营养属性消费 |
| 药物属性／us.core/EatDrug（当前键） | 实际摄入物 ThingDef.IsDrug | drugCategory 独立判断；营养与药物可交叉。Any 不要求该事实已知；治疗并非全部摄入 | 当前 targetA 读取有目标切换缺口；无摄入时 False，缺必要目标时 Unknown |
| 摄入结果／后续独立结果事件 | Toils_Ingest.FinalizeIngest → Thing.Ingested 返回值及后续记账 | Finalize 调用后才处理返回营养；0 不统一等于失败；Job 结束不统一等于成功。只发布能证明的摄入者、物品、数值／阶段，不伪造成功 bool | 当前没有结果消费者。AfterIngestion 应独立根，不继承仍在执行的固定条件 |

## 必须保留的判别性输入

| 输入 | 合同应答 | 当前实现／检查状态 |
| --- | --- | --- |
| 准备中 Ingest，尚未进入执行 | WholeProcess 可成立；执行模式 False，不能因工厂已构造而 True | S3PawnFactTests 仅证明现有采样；新阶段替身待 D-B |
| 正在饮用非营养药物 | WholeProcess／执行阶段独立于营养筛选；drug 为 True；营养 Any 不排除 | 当前 EatNutrition 与状态耦合，目标行为待 D-B |
| 啤酒：营养 True、药物 True | Any/Any 纳入；仅营养＋排药排除；不能把两属性当互斥 | 源码依据已核，组合及真实 settings 消费待 D-B |
| FoodFeedPatient：喂食者 a、Deliveree b | b 获得摄入事实，a 不因执行该 Job 获得 Eat | 角色适配与替身待 D-B；原生未跑 |
| dispenser → 食物 | 属性与结果跟随食物，不跟随机器 | 数据流依据已核；新消费待 D-B |
| Corpse 同 Job 执行 → 退出 → 再执行 | 精确 Sustained 在所选阶段退出时结束；WholeProcess 不因一次阶段退出结束 | 复入适配与新反例待 D-B |
| 中断／异常，未到 Finalize | 不制造完成结果；持续依据失效或 Unknown 结束，保原因 | 通用 Sustained 合同已有纯／平台替身基础；新 Eat 生命周期待 D-B |
| Thing.Ingested 返回 0 | 保留 0 的事实，不等同失败；不据此撤销已证摄入调用 | 源码已核；结果事件待 D-B |
| 精确阶段 Unknown，Whole 已知 True | 精确仍 Unknown、无新起播，不改用 Whole | 当前旧 EatState 有宽化路径，必须在 D-B 关闭 |
| phase=WholeProcess／Execution 的正常退出 | OneShot 停新起播、已起播自然尾；Sustained 按所选依据失效结束并沿原生淡出 | 生命周期基础可复用；参数选择与事实联动待 D-B |

## 维护和验收状态

- 固定缺省为 **WholeProcess／nutritionScope=Any／drugScope=Any**。新字段尚未进入 settings／保存／恢复／session，不把本记录当实现完成。
- 源码核对：上述1.6.4871方法体结论来自继承的既有项目定点源码核对记录；本次不重新反编译安装。当前代码核对：UsPawnFacts、PeriodicStateBinding、SqueakEatOccurrence、UsOneShotRuntime。
- 现有替身：tools/UniversalSqueakerAudioBindingTests/S3PawnFactTests.cs；纯旧政策：tools/UniversalSqueakerOneShotTests/S3Tests.cs。只证明其实际断言，不证明新 Eat 覆盖。
- 新 Eat 替身／生产纵切：D-B 待实现；原生与听感：未运行，须后续获准并由用户协助。
- 改源后按“受影响事实 → 消费包装 → 适配／反例 → 本表”更新；主体、阶段、结果或产品范围变化交 CO/PM，不按方法名称猜合同。

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

## 当前伤口：不是历史受伤通知

`us.core/HasCurrentInjury`由`UsPawnFacts.Capture`读取该Pawn当前`health.hediffSet.hediffs`：存在`Hediff_Injury`且`Severity>0`为True，包括仍存在的伤疤；无此项为False，缺health或hediffSet为Unknown。消费者为示例`example/InjuredSelect`固定附加条件；它不改普通Select，也不从先前Wounded事件缓存True。

生产事实读取和普通／受伤扩展调用已由DaSelectCallerTests的加伤、移除、缺health序列执行；类型／API经目标1.6.4871引用编译，健康集合为明确平台替身。真实游戏伤口／伤疤样本尚未运行。此判据是示例明示的合同，不给全部“受伤”扩展规定永久统一定义。
