# 0.6 S3：17动作、作者来源与Config恢复

2026-10-19。S3真实生产实现及自动证据；普通日常默认仍沿旧线。主规格的S4节奏/调制、S5输出及1.6原生/听感责任继续保留。

## 使用实际入口

Dev明确选择 `S3 CANDIDATE order: 17 actions W20 H40 actions120`，或调用 `UsOneShotRuntime.EnableControlled(..., fullDevCandidate: true)`。W20/H40/actions120是测试参数，不是玩家默认。旧S1显式调用仍默认只启用Work/Wounded/Call。关闭受控会话使用原Disable入口；换游戏会清运行会话，不写存档。

扩齐候选顺序为Death > Wounded > MentalBreak > Attack > Draft > Undraft > Equip > Crying > Giggling > Select > Sleep > Eat > Social > Joy > Move > Work > Call。此总顺序和 `AdoptUniqueDevCandidate` 的首次唯一采用只供明确注入验证，等待产品裁定；普通初始化不调用自动采用，不切日常机会/概率默认。

作者继续通过既有UsProviderDef/UsActionDef/UsVoicePackDef进入同一AuthorCatalog。`docs/examples/0.6/BehaviorSource.xml`的普通声明、Eat组合与音源已经在生产源测试中实际加载、解析、决策并派发；Extensions.xml还实际创建第三方中性事实提供者。新17作者动作不需要另写旧Comp完整动作表；自动Comp仍保留旧15项默认，Baby两项在受控作者入口受真实DLC、玩家Baby开关和来源/玩家资格限制。

## 17项正向事实与原入口条件

|动作|真实来源/阶段|保留条件与覆盖范围|
|---|---|---|
|Call|本次mindState.IsIdle、普通Wait/Wait_Wander且pather未移动|非其他动作失败；Wait_Combat/任意维持姿势不算；事实缺口不放行|
|Eat|Ingest任务；IEatingDriver.GainingNutritionNow；精确ChewIngestible；独立IsDrug事实|EatState保旧三模式及旧已声明toil未确认fallback。EatChewing不足为Unknown，不把兼容fallback叫精确事实。营养药物不被一概排除|
|Sleep|当前JobDriver.asleep|不以卧床/有rest或!Awake推睡眠；无driver为Unknown|
|Move|pather.Moving|主动命令事实独立；不把主动来源改成移动状态|
|Work|CurJob.workGiverDef|旧默认ActiveCommand及playerForced/玩家可控门保留|
|Social|Social joyKind；准确原版StandAndBeSociallyActive/GotoAndBeSociallyActive/Lovin|不再以任意名字包含Chat/Social等推True；不声称覆盖所有第三方社交|
|Joy|CurJob.def.joyKind|可与Social事实同时成立，单次仍只选一项|
|Wounded|PostApplyDamage成功通知后|无新增伤害阈值；非默认共用冷却豁免|
|Select|Selector.Select postfix且playSound=true|玩家可控、!Downed、Awake响应门；非程序性静默选择|
|Death|Pawn.Kill主体前prefix|保位置/仍Spawned阶段；只豁免Talking概率，仍有真实器官门|
|Draft/Undraft|玩家Draft gizmo原操作后确实状态改变|不patch任意Drafted赋值；玩家可控/主动来源|
|Attack|Core非Ability Verb所声明TryCastShot返回成功后|实际目标发现的近战/远程/特殊verb；不宣称全部攻击API/DLC/Ability|
|Equip|EquipmentAdded通知后，当前为玩家强制Core Equip job|AI/装载/系统装备变更不通过|
|MentalBreak|MentalBreakWorker.TryStart成功后|不是所有MentalState；不混BabyFits|
|Crying/Giggling|TryStartMentalState成功后，经真实MentalFitDef反向映射|Biotech关闭不通知、配置保留；原Baby许可仍必需|

新适配在到期/事件才采样，无额外动作循环、队列或第二机会。事件只对当次捕获为True；后续机会不重放上次事件。同批选择后失败不改Call、不释放同tick名额。新事实API的目标1.6方法体由本轮CO FACTS16核对；适配fixture不是原生场景。

## 来源选择、权限与恢复

`UsBehaviorSourceDef.declaration`支持准确单域；同Def多目标可使用多个 `<target family="Pawn" target="Ratkin" child="Xeno">...</target>` 块。来源身份由真实加载的packageId/Def名绑定，XML不能冒写owner。Pawn namespace必须空，Race/Child不接受前后空格；NonPawn必须明确namespace/target且无Pawn Child。参数语义首版缺省固定为1，普通作者无需写恢复脚本或随调值升版本。

来源候选进入同一AuthorCatalog，与音频启停分开；只有音频也合法。每个精确域为Unconfigured（未写）、Inherit（明确跟父）、Bound（指定Def×域）；同方案重复引用不增加候选，同身份重复独立声明不按加载顺序胜出。当前只有玩家显式 `UniversalSqueakerMod.ChangeBehaviorSource(domain, sourceId, out failure)`提交来源；sourceId为null即新行为忘记：仅该域转Inherit并清快照，保其他域、子域和player记录。不借用旧音源忘记/恢复按钮。

作者主域→作者子域→player Global→player主域→player子域，按字段覆盖。更具体作者不能覆盖player父域。稀疏字段不贡献；false/0存在位保留。既有actionTuning含sourcePresetDefName的导入值仍属player，旧interval/probability倍率不转新概率或W。

|参数|类型/单位/范围|省略|
|---|---|---|
|enabled|bool|不贡献，不自行启用|
|scope|Disabled/AnyOccurrence/ActiveCommand|沿合法父层/旧动作默认|
|hardIntervalTicks|非负整数，游戏tick|显式Dev动作基准；不从旧Realtime字段改义|
|talkingEnabled|bool|沿已有Talking策略；Death原例外保留|
|volume|有限0..float.MaxValue局部因子|不贡献，缺省1；全局音量始终另乘|
|pitch|有限正数..float.MaxValue局部校准|不贡献，缺省1|
|ageEnabled|bool存在位|读取、折叠、恢复；响应实际乘算留S4，不偷偷启用新D4|
|requires|空格分隔已注册事实身份|无额外依赖；缺实现仅暂停使用它的动作，不删条件|

快照仅保存最后有效作者贡献、必要语义和依赖身份，不存父最终结果/player/总控/事实真假/随机/截止/句柄。A缺失且有B时仍用A好快照；同身份A归来用其当前有效数据并刷新快照；局部坏更新保该项旧好值，其他有效项继续。A当前有效且主动删某动作意味着不再贡献，不从旧快照复活。

启动刷新/选择/忘记均在副本验证后通过生产Config事务保存再提交。刷新保存失败时原持久绑定、好快照及文件不变；当前有效作者Def仍是运行只读事实，尚未成功保存的新值不能宣称已可缺源恢复。故障fixture明确比较当前运行值与重启后缺源时的上次持久好值。

## Config事务与旧入口

原版ModSettings写盘会直接截断最终文件，不能用其成功后提交内存来声称磁盘原子性。新来源操作沿原Config文件名（Content.FolderName + Mod类型名，经原GenText.SanitizeFilename）、同 `SettingsBlock/ModSettings` 形状及Scribe序列化全部旧字段，先写同目录唯一临时候选，关闭并flush后File.Replace（已有目标）/Move（首次）成功才提交内存。序列化/提交失败不改原文件与原记录，失败候选保留供诊断；不作为配置加载。真实Config没有被本轮测试触碰。

旧普通设置队列未另造UI或存档状态。新来源操作不会清旧字段；既有Global/行/区域Reset继续原范围，**不等于忘记新行为来源**。独立ConfigCopy、SettingsMigration和Reset仍走原测试；来源新操作实际覆盖序列化部分写失败、完整候选后失败、Windows目标文件锁导致Replace失败、成功重读与连续恢复。

受控对象的合法未注册字符串经原NotifyExternalByKey→TryEvent进入同一DecisionState，不退出Dev、不要求新注册，也不退Comp私有冷却结算。保allowExternal、scope、玩家响应/主动来源、精确域音源、原Realtime实际采样及SqueakTimingModel倍率/时钟公式；初始旧最后记录投影给同一owner。原registry选音及旧年龄/心情覆盖/jitter含义保留，不切D4乘算。

受控变化按已批D2：硬门先于roll；选中即占同tick；只有Dispatched/Indeterminate推进保护，AlreadyActive/Rejected不伪记派发；所有17和兼容调用共享名额/已立保护。原普通非受控路径不变。兼容轨迹覆盖两个方向同tick混用、拒绝不退名额、跨入口保护、tick大跳但真实秒不足、暂停同tick与Realtime精确端点；不固定估算tick与秒关系。

## 自动证据与未决

`UniversalSqueakerOneShotTests`独立编译真实Authoring/Kernel/Pure，无游戏引用/同名stub；保旧golden，追加17事实、用途roll/次数、四回执、来源/权限/恢复、兼容时钟固定轨迹双重放。AudioBindingTests另以可控平台形状运行真实事实适配、AutoAttach、CreateDefault和资格函数；SettingsMigration另运行真实Settings/Scribe边界与原子文件事务。Host为真实产品/FL消费，原生与听感分别pending。

最终日志和RPT位于 `dist/evidence/s3/`，Dev为 `dist/dev/UniversalSqueaker/`。固定FL仍0.7.0-dev+d5af4d8、[0.7.0,0.8.0)，未消费0.8。候选不是一般安装/发布建议。S5继承Settings→ApplyDistanceRange→共享SubSoundDef.distRange仍强制阻断C11/V14/整版接受；本轮不删、临时改写恢复或clone共享定义。
