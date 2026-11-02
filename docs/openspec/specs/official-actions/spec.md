# 十五官方动作与参数
状态：现行，US0.6.x。责任：动作合同维护者。机制支持范围由[原版事实](../game-facts/spec.md)及[Core Work](../game-work/spec.md)限定；机会、供音、输出与默认分别引用相邻能力，不在各动作复制。

## Purpose
明确谁发生什么、谁可以发声、哪些条件可调整。十五项共用一个主体owner；共享事实与AudioId不使动作、主体或人工关口合并。

## Requirements
### Requirement: ACT-Common 固定条件和参数独立
仅True SHALL可入候选。enabled／scope／合法间隔／局部volume／pitch与显式响应可由作者后玩家逐字段覆盖，不得删固定条件或制造原生事实。子动作的配置、优先、声音及形态独立；失败不补其他动作。所有动作OneShot自然尾，Sustained按所选真实依据维护，瞬时事件不自动成为永久状态。官方支持scope：Draft／Undraft／Equip仅ActiveCommand；Attack／Work支持AnyOccurrence和ActiveCommand；其余AnyOccurrence，所有动作可Disabled。ActiveCommand另需真实playerForced且玩家可控，不由标签造命令。
#### Scenario: 独立子合同与父合同共用声音
- **WHEN** InjuredSelect显式映射同AudioId
- **THEN** 保自己的完整键、优先、配置及保护，不因缺音回父动作

### Requirement: ACT-Select 接受、反馈与最终主体
Select SHALL只响应实际接受且有本次反馈意图、最终成员提交一致的SelectedSubject；仅调用不足。通用合同无隐藏Pawn门，无US独立重复去重。官方Pawn三参数selectRequirePlayerControlled／selectRequireAwake／selectRequireNotDowned各默认true、独立可调；只适用显式SelectEvent的Pawn合同，不自动复制到子根。
#### Scenario: 只放宽清醒要求
- **WHEN** 有效Awake=false，其他两项仍true
- **THEN** 仍要求玩家可控且未倒地；接受／反馈／最终主体不被取消
- **AND** 上游无新反馈不造事件；实际新接受按root／秒合同处理

### Requirement: ACT-Wounded 受损返回
Wounded SHALL属于PostApplyDamage正常返回的DamageRecipient，不把0当正伤保证，不等于当前伤口。需要正伤或现存伤口者必须消费明确事实；伤口不从历史通知缓存。
#### Scenario: 正常0伤害或异常
- **WHEN** 原方法正常返回0，或抛异常
- **THEN** 前者保“受损处理返回”原义，后者不发正常返回事件；不换成攻击者发声

### Requirement: ACT-Death 临终Kill进入
Death SHALL表示进入Kill的DyingSubject，不表示死亡完成、任意Destroy／消耗或尸体。Kill后生命周期按原owner清理，声音不转给凶手或尸体。
#### Scenario: Kill体内后续失败
- **WHEN** prefix已观察进入而死亡完成未知
- **THEN** 仍只报告临终进入，不把异常倒推成死亡完成；OneShot不承诺精确裁尾

### Requirement: ACT-Attack 真实执行而非命中
Attack SHALL来自已核明确执行机制的实际Attacker；近战须真实挥击marker，不能把所有false当挥击；发射／束流／喷射／点火／轰击启动不保证命中。未核工具、移动、支援或任意生成不得因名字纳入。
#### Scenario: 近战早退与挥击落空
- **WHEN** 未Spawned／FullBodyBusy早退false，或实际挥击后正常返回false
- **THEN** 只有后者有挥击证据可发布；受害者Wounded仍另一主体owner

### Requirement: ACT-Move 自身路径移动
Move SHALL来自本主体自己的pather.Moving；缺pather为Unknown，被搬运位移／载客／世界旅行不代替。scope只限制回应，不改移动事实。
#### Scenario: 被搬运而位置改变
- **WHEN** Pawn位置变而自身未移动
- **THEN** 不成立Move；停止自身移动只停止新起播，已有OneShot自然尾

### Requirement: ACT-Equip 装备关系
Equip SHALL在装备tracker正常加入且实际equipment成员关系仍成立时，归实际EquipmentWearer及物品；不把穿衣、卸下或库存拾取算该事件。PlayerCoreEquip是显式回应资格，不预先篡改关系事实。
#### Scenario: 正常返回前物品已移除
- **WHEN** 最终eq不在实际wearer的equipment中
- **THEN** 不发布成功加入；普通资格仍另查玩家控制、playerForced及准确Equip Job

### Requirement: ACT-Call 正向闲置等待
Call SHALL要求IsIdle、自己未移动及准确Wait／Wait_Wander，不以“无别动作”兜底。IsIdle内的原生Spawned／未倒地条件只属该事实。
#### Scenario: 未知Job或Wait_Combat
- **WHEN** 没有其他已知动作但Job不在准确闲置集
- **THEN** 不制造Call；缺mindState／pather保持Unknown

### Requirement: ACT-Eat 三轴与实际摄入者
Eat SHALL按实际ActualIngester的所选过程／执行及两独立属性轴求值。phase缺省WholeProcess，可选Execution；nutritionScope缺省Any，可选NutritionGiving／NonNutritionGiving；drugScope缺省Any，可选ExcludeDrugs／OnlyDrugs。Any不增加对应可选依赖，精确Unknown不拓宽Whole。
#### Scenario: 病人被喂食与执行退出
- **WHEN** FoodFeedPatient照护者喂病人，Chew真正进入再退出
- **THEN** Eat归病人，不归喂食者；Whole不因一次Chew退出停止，Execution按执行依据退出
- **AND** OneShot自然尾，Sustained按所选依据失效结束
#### Scenario: 普通Eat与独立摄入结果
- **WHEN** Thing.Ingested正常0或正返回
- **THEN** 仅显式Result消费者按RESP-02获得事件；营养属性不以返回值代替，Job结束／异常不造结果

### Requirement: ACT-Sleep 实际睡眠
Sleep SHALL取当前driver.asleep，角色Sleeper；不从躺床、持有睡觉Job或!Awake推导。driver缺失Unknown，存在未睡False。
#### Scenario: 睡眠中被喂食
- **WHEN** Sleep与Eat同时True
- **THEN** 按同owner仲裁；中选Sleep仍标Sleeper，不误用ActualIngester角色

### Requirement: ACT-Social 明确社交过程
Social SHALL只消费joyKind Social或准确StandAndBeSociallyActive／GotoAndBeSociallyActive／Lovin过程，属于当前SocialParticipant，不保证互动成功／关系改变或替对方自动回应。Goto的当前内容证据缺口见开发开放项。
#### Scenario: 与Joy重叠
- **WHEN** Social kind使Social与Joy同真
- **THEN** 不改Joy为False，按总序择一；Social无音不补Joy

### Requirement: ACT-Joy 正向娱乐声明
Joy SHALL来自当前Job.def.joyKind非空的整个过程，包括准备；不是实际Joy gain／执行Toil，也不从“非工作”推导。未声明kind的第三方机制不自动覆盖。
#### Scenario: 准备娱乐而尚未获娱乐值
- **WHEN** 当前合法Job声明joyKind
- **THEN** 可成立Joy过程，但不得报告已获娱乐；结束后OneShot自然尾

### Requirement: ACT-Work 准确工作过程
Work SHALL按GAMEWORK-01完整Core集识别同Pawn／Job／driver／有效Toil且未ended的Worker，含准备／取料／走动，不保证完成。患者或物品不变Worker；事实与ActiveCommand回应scope分开。
#### Scenario: 照护与患者休息
- **WHEN** 照护者执行FeedPatient而患者LayDown
- **THEN** 照护者可Work、患者可Eat／Sleep；三个Patient休息入口不作为Work
- **AND** 普通AnyOccurrence不改声明基线的ActiveCommand语义

### Requirement: ACT-Draft 实际征召方向
Draft SHALL只来自准确玩家gizmo原delegate正常执行后的false→true，角色DraftedPawn；setter、加载、无变化和异常不造机会。
#### Scenario: 重复获取同命令
- **WHEN** 同gizmo被多次获取但仅一次真实变更
- **THEN** 不叠加包装或重复事件；按真实接受root与秒间隔处理

### Requirement: ACT-Undraft 独立解除方向
Undraft SHALL来自同一gizmo正常true→false，使用独立ActionId／条目／动作秒截止；当前false不是周期事件，关Draft不连带关闭Undraft。
#### Scenario: 快速来回征召
- **WHEN** 两方向都实际接受
- **THEN** 各自秒截止结算，可与先前尾音重叠，不因两个声音包增加机会

### Requirement: ACT-MentalBreak 正向成功启动
MentalBreak SHALL只含已核基类及Catatonic／RunWild／WildDecree四成功来源；HumanityBreak经base只观察一次。任意MentalState、SocialFighting或婴儿MentalFit不能替代成功启动。
#### Scenario: WildDecree成功但Wander未成功
- **WHEN** 已核override正常true且崩溃命令执行
- **THEN** 保该正向启动原义，不假称特定MentalState已进入；false／throw不发布

### Requirement: ACT-Retired 官方库存与独立扩展
当前官方库存 MUST恰为上述十五项；Crying／Giggling专属回应与无消费者残余退役，不改原版状态／婴儿音频、不整体禁声这些Pawn，不用名称黑名单禁止独立作者根。
#### Scenario: 第三方定义independent/Crying
- **WHEN** 合法完整声明、事实、域与音源存在
- **THEN** 可按自己的合同接入；历史17字符串语料不成为官方库存或强迫重录golden

