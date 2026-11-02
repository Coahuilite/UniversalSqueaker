# RimWorld 1.6事实、资源与宿主适配
状态：现行，US0.6.x／目标1.6.4871。责任：US原生适配维护者。精确符号、来源版本与替身证据见[事实映射](../../../development/specification/facts.md)。

## Purpose
把原版实际接受、事件、Job／Toil与资源事实转成平台中立输入，准确区分主体、阶段、Unknown和缺实现；不让名字、音频目录或当前错误实现扩大资格。

## Requirements
### Requirement: GAME-01 主线程组装与中立输入
游戏适配 SHALL在原主线程资源就绪入口初始化，采样当前事实、稳定身份、游戏tick／接受时单调秒和随机，Core只收中立值。Pawn／Job／Def／音频句柄留外层；同级不构成隐式第二调度器。
#### Scenario: 游戏版本接缝失效
- **WHEN** 必需方法／字段不可用
- **THEN** 按依赖具名隔离能力，不吞全局PatchAll异常、不伪造事实、不拖垮无关路径

### Requirement: GAME-02 准入、浏览与canonical域
自动comp准入 SHALL取有效包声明Race与最终表Race并集，未选择包也可授准入；二者皆无安静，作者comp保留。浏览目录只展示，不授准入。真实canonical Xeno须当前Biotech可用，并在该Race音频、配置或来源中有准确子域；另一Race同名Xeno不授权。
#### Scenario: 合法Xeno只有行为域而无Xeno声音包
- **WHEN** 原生canonical身份有效且该Race存在准确配置／来源域
- **THEN** 不因缺Xeno音频声明取消资格
- **AND** DLC停用保休眠数据，不清配置

### Requirement: GAME-03 Select与命令接受链
Select SHALL在真实嵌套SelectInternal root捕获反馈最终对象，正常返回核同Selector最终成员；异常清作用域。两目标readiness成对，缺一两patch都不安装。Draft／Undraft只包装准确gizmo原delegate，正常前后方向确认接受，避免同命令重复包装。
#### Scenario: 缺反馈方法或直接setter
- **WHEN** Select缺PlaySelectionSoundFor，或脚本仅设Drafted
- **THEN** 前者Select具名Unknown局部隔离，后者不产生玩家接受机会

### Requirement: GAME-04 执行与正常返回证据
Wounded SHALL用受损者PostApplyDamage正常返回；Death用Kill prefix。Equip用实际tracker wearer及最终成员。Attack按下表明确根，不扫描名称或任意bool。

| 攻击机制 | 原版范围／证据 |
| --- | --- |
| 近战 | TryCastShot成对Notify_MeleeAttackOn marker；正常false可为挥击，未Spawned／FullBodyBusy早退无marker不算 |
| 投射物 | Verb_LaunchProjectile true；Shoot／AbilityShoot／ShootOneUse／StaticOneUse经实际base链只通知一次 |
| 束流 | Verb_ShootBeam true及实际继承该链的ArcSprayIncinerator |
| 喷射／喷火 | 已核Verb_Spray／Verb_SpewFire正常true |
| 点火 | Verb_Ignite正常true |
| 明确轰击启动 | Verb_PowerBeam／Verb_Bombardment true，仅启动不是命中 |

BeatFire工具、Jump／CastAbilityJump、通用CastAbility／CastTargetEffect、Broadshield／Tox／Deadlife部署、Firefoam／Smoke、任意Spawn／MechCluster不凭名称纳入；不经过支持根的第三方override需显式扩展。
#### Scenario: 攻击者与受损者
- **WHEN** 同次实际执行和受损处理各有事实
- **THEN** 各进自己主体owner与准确域，执行不被当成命中／正伤保证

### Requirement: GAME-05 Eat过程、执行、物品与结果分开
Ingest自身或FoodFeedPatient.Deliveree SHALL为摄入者。Setup／Cleanup提供过程；Chew工厂只挂观察，原init正常且同driver／Job／CurToil才算执行，finish退出；Corpse可同Job复入。准备用支持Job的targetA，执行用真实itemIndex（可非A），退出还原准备；多个喂食来源不明则Unknown。
#### Scenario: dispenser目标换食物及正常零营养
- **WHEN** 原目标机器被实际可摄入物替代，Thing.Ingested正常返回0
- **THEN** 属性取实际物的IsNutritionGivingIngestible／IsDrug；0结果合法
- **AND** Destroyed／!IngestibleNow提前返回、异常或Cleanup不造成功摄入结果；结果只存本次中立快照

### Requirement: GAME-06 当前状态与角色
Move SHALL读自身pather；Call读IsIdle＋未移动＋准确Wait；Sleep读driver.asleep；Social读明确kind／准确Def；Joy读joyKind。Awake按CanBeAwake及当前driver.asleep，NotDowned按health；当前伤口为Severity>0的Hediff_Injury含仍存在伤疤，缺health／hediffSet为Unknown。
#### Scenario: 缺jobs但健康可清醒
- **WHEN** 原版Awake规则在无当前Job时给true
- **THEN** 不擅加“缺jobs必Unknown”；未启用可选资格不增加该依赖

### Requirement: GAME-07 正向MentalBreak支持集
MentalBreak SHALL观察基类TryStart与Catatonic／RunWild／WildDecree三个直达override的正常成功；HumanityBreak经base只一次。任意MentalState、MentalFit、false或throw不代替。
#### Scenario: Catatonic不进入MentalState
- **WHEN** 已核override以Hediff等路径成功
- **THEN** 按其真实成功来源承认，不强迫所有机制变成同一MentalState

### Requirement: GAME-08 资源来源与最终绑定
音频绑定 SHALL在patch前有限记录相关原来源，原版完成patch／依赖过滤／解析和内部资源就绪后核最终声明，再发布AudioId到最终对象。合法字段patch使用最终内容；唯一整根替换需无相反证据；删除不复活，重复只隔离该资源，不按覆盖后DefDatabase剩一项判唯一。单坏OGG只有已被原版可靠排除且最终内部池仍可用才保留该SoundDef，否则隔离该候选及引用；自行枚举到一个好clip不足证明内部池安全。
#### Scenario: 来源不明的patch新建声音
- **WHEN** 无法可靠确定资源提供者
- **THEN** 不猜owner或退裸名；作者应在普通Defs声明独立声音。XML／程序集根本加载失败不承诺逐条抢救

