# 单主体机会、时钟与结算
状态：现行，US0.6.x。责任：响应核心维护者。普通默认另见[policy](../ordinary-policy/spec.md)；输出收据由[输出合同](../audio-output/spec.md)拥有。

## Purpose
让一次真实操作、一次模拟事件或到期状态进入同一稳定主体owner，按明确身份和时钟同步决定、预留及结算；不会因包数、暂停或失败重复制造机会。

## Requirements
### Requirement: RESP-01 单状态机会与择一
每主体 SHALL仅有一份日常状态机会，不按动作／包造循环。到期采当前事实，按优先及完整键稳定顺序择一；选后无音、概率未中、准备或输出失败都结束本次，不补低项或Call、不排队。
#### Scenario: 工作与移动同时成立
- **WHEN** 一次状态机会同时有Move与Work，较高项中选但无音源
- **THEN** 本次不改选Work；下一次仍是该主体的一份未来机会

### Requirement: RESP-02 事件只送显式消费者
非状态事件 SHALL只让编译后固定条件树显式引用本次EventFact的合同参加，含独立、多级继承、嵌套和混合消费者。无消费者不创建owner或改变机会／保护／诊断；事件不能唤醒无关状态。
#### Scenario: 摄入正常返回而只有普通Eat
- **WHEN** 过程仍True、下一状态尚未来，0或正营养结果到来但无Result消费者
- **THEN** 不额外起普通Eat、不消费状态机会、不更新其冷却；有显式结果根时才交原owner仲裁

### Requirement: RESP-03 单一动作间隔与两道机会
交互 SHALL以StateOpportunity=false提供实际EventFact、同值Interaction.EventFact、正整数OperationId真实接受root和有限非负单调秒；模拟事件提供EventFact和game tick；状态明确StateOpportunity。缺必需事实拒绝，不legacy回退。交互只查秒截止，不CHECK共享H、不占或清模拟槽；模拟只用tick动作门、同tick槽与适用H。
#### Scenario: 游戏暂停后再次接受选择
- **WHEN** 同game tick另一次真实接受有新root，且该动作秒截止已到
- **THEN** 不因冻结tick或先前模拟槽拒绝；仍经固定资格、概率、音源和输出门
- **AND** 不停止较早OneShot尾音

### Requirement: RESP-04 root消费与过期重放
同owner SHALL按事实通道记录最大已消费root，在资格／随机／输出前消费；相同root重入和旧root晚于新root重放均拒绝。新root可在同tick成立，root不是帧号／鼠标按钮／tick。Core只接外层给的中立时间，不读系统或平台时钟。
#### Scenario: 新root之后重放旧root
- **WHEN** root12已消费后再次收到root11或12
- **THEN** 不进入随机或输出；不能因换配置、Dev策略或返回Rejected重试该root

### Requirement: RESP-05 模拟预算与硬门
模拟新尝试 SHALL在副作用前占主体同tick名额，失败不退额。硬截止到点允许、动作与适用共享截止取max而不相加，先于随机。后到未知事件不改写已派发历史；Death等优先不提供追溯或新特权。
#### Scenario: 模拟尝试后同tick到达Death
- **WHEN** 同一主体模拟槽已占，随后才收到Death
- **THEN** 可因槽拒绝且不排队；优先级不撤回较早结果

### Requirement: RESP-06 按实际结果结算
Dispatched／Indeterminate SHALL从本次适用时钟推进动作保护并WRITE声明的game-H；前者记确认，后者不记。Rejected不推进派发保护，AlreadyActive不刷新；交互拒绝仍消费root。诊断失败不得撤销已完成结算。
#### Scenario: 输出调用内结果不明
- **WHEN** 已进入可能有副作用的原生调用但不能确认结果
- **THEN** 保守硬保护、无确认派发，不立即补播；未进入副作用边界的失败为明确未派发

### Requirement: RESP-07 状态W和R的寿命
状态新派发后 SHALL以max(t,适用H)+W为合法下界；未派发、AlreadyActive或不明结果以max(t+R,适用H)为下界，未选中动作的早期失败保留既有H保护，已选动作按实际CheckShared处理。R内部下限严格大于1游戏tick。普通相位按POL-03只在下界之后对齐，不能提前、重抽音源或补播不确定结果；动作长截止不冻结整主体。全关／不可服务停状态，恢复后再安排。

Kernel SHALL唯一持有首次与后续状态排程。算法`us.state-phase/1`将世界seed和稳定主体ID按其uint32位模式读入，在unchecked块中依次执行：seed异或`0x55535031`，加ID乘`0x9e3779b9`，右移16异或，乘`0x85ebca6b`，右移13异或，乘`0xc2b2ae35`，最后右移16异或。归一化位置为所得pattern除以`2^32`，映射N槽时使用精确整数`floor(pattern×N/2^32)`；以该槽在N游戏tick周期中的位置向后对齐，额外为0至N−1，所有tick加法按既有long截止饱和。该稳定位置不是每轮随机抽样，也不承诺永不碰撞。

每次真正新排程 SHALL保留其采用的窗口与主体pattern。后到事件只有在本次WRITE确实提高H且H超过旧状态机会时才向后对齐；对齐沿旧机会已采纳的窗口，不再追加W，也不重新采速度或A。首次尚未派发的机会若被H抬期，使用初次采样倍率下的短重试额外预算，不能沿600初相范围延后。事件与交互本身仍即时进入原owner，资源维护Stop不产生排程。

adapter SHALL只供应已恢复世界值、稳定ID和独立目标倍率。0是合法seed，未ready则没有输入。旧无seed公共CLR入口及合法条件保留，随后绑定输入只影响未来新排程。入离图或owner重建在同世界同ID重建同归一化位置；跨Game清旧owner和输入，窗口关闭不清主体冷却，不新增US存档字段。
#### Scenario: 已安排机会被后到事件推迟
- **WHEN** 状态机会1060、旧W20，而1050事件使H到1090
- **THEN** 状态不得早于1090，不变1110、不新增等待抽样
- **AND** 配置变化本身不清截止或重抽等待

### Requirement: RESP-08 授权复核与释放
持续授权 SHALL按当前定义／配置／音频快照身份变化及维护点复核，不能只比局部revision数字。Pawn／Map退出释放自有槽与记录；窗口关闭不释放主体owner。进程策略不因地图释放卸载。
#### Scenario: 同数字修订来自新发布者
- **WHEN** 新配置对象沿用局部revision值
- **THEN** 仍识别快照变化并复核当前授权，不重置owner／W／保护

