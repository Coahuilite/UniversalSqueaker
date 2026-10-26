# 暂停时也能回应：显式机会与单一间隔

普通选择默认间隔 **0.3真实秒**，征召、解除征召各 **0.6真实秒**。暂停不会冻结这些间隔；同一Pawn仍须满足已经接受、反馈、最终主体和三项可调资格。其他十二项继续使用原游戏tick节奏。活跃度、音源、资格和输出失败仍可能使一次操作无声。

这是当前发布前合同（WO-US06-RESPONSE-CONTRACT-AUDIT-20261027 r3）。按决定 **US06-PREPUBLICATION-COMPAT-20261019 r4**，US0.6可破坏性更新至用户明确确认面向玩家公开发布；GitHub公开、push、tag或候选存在不终止该授权，也不授权任何发布操作。FL/SR不在此范围。

## 一次机会只检查一种动作间隔

| 明确机会 | 事实与时钟 | 本次动作间隔 |
| --- | --- | --- |
| 状态机会 | `StateOpportunity=true`，当前状态事实和游戏Tick | `SimulationIntervalTicks`；按W／R60及适用H调度 |
| 模拟事件 | `StateOpportunity=false`，实际`EventFact`与游戏Tick，无Interaction | `SimulationIntervalTicks`，模拟同tick槽及适用H |
| 已接受交互 | `StateOpportunity=false`，实际`EventFact`和`InteractionOpportunity` | `InteractionIntervalSeconds`；不检查动作tick截止／H，不占或清模拟槽 |

每次事件只让编译后固定条件显式引用本次EventFact的合同参与，包括独立根、继承和状态组合；无关Sleep／Eat／Call等状态不会借事件绕W。候选仍按固定优先择一，失败不补低项，不排队。声明式C#通知由`SqueakerNotifications.TryProcess`提供自己已经登记的通知事实，仍是模拟事件，不伪装成玩家交互。

混合消费者可在同一固定条件中消费不同机会，例如`any(SelectEvent, WorkState)`；它自己分别声明两类机会的间隔。一次Select机会只用真实秒，一次Work状态机会只用游戏tick，绝不将两道动作门叠加。`all(SelectEvent, WorkState)`则仍需真实选择当下正在工作；不会放宽固定条件。参数不从父动作或相同尾名继承，独立交互缺省0秒。

所有成功／不明派发仍WRITE游戏H216（显式Dev输入按所选H）；它只影响查共享保护的后续模拟机会。交互可覆盖较早OneShot尾音；不截断已返回声音、不追溯全局优先。Death／MentalBreak没有新增例外，先前模拟尝试占额仍可能使后到事件丢失。自定义state免CHECK只免该次硬检查，不取得独立于Pawn状态机会和H预算的唤醒任务。

## 操作身份来自接受链，不来自鼠标帧

Select沿实际SelectInternal栈记录root序号，消费实际反馈最终Pawn，并在正常返回核同Selector最终成员；嵌套代理沿同root。Draft／Undraft沿真实Core Gizmo delegate的正常前后转换确认接受；setter、加载、无变化或异常不造机会。单调秒由Game适配层的Stopwatch在接受时采样，Core只收值，不读平台对象或系统时钟。

同owner按事件通道记录已消费最大root序号：相同root递归／重复和晚于新root的旧root重放都拒绝。另一次真实接受即使在同game tick仍是新root。消费发生在资格、随机和输出副作用之前；Rejected不重试同root，但不写秒截止。Dispatched／Indeterminate写秒截止，AlreadyActive不刷新。更改配置／Dev输入不清owner、已预留截止或W。

纯Core／宿主调用须明确提供机会。已有对象初始化器可按当前真实事实填写：

```csharp
frame.StateOpportunity = false;
frame.EventFact = actualAcceptedFact;
frame.Interaction = new InteractionOpportunity(actualAcceptedFact, rootId, monotonicSeconds);
```

模拟事件只给实际EventFact，不提供Interaction；状态机会明确设StateOpportunity。缺EventFact或已知交互缺root／时间，返回具名拒绝，不回退旧game-tick执行。不要用game tick、帧号或鼠标按钮当root，也不要用UTC或游戏timescale时间代替单调秒。普通调用实际已接受事实仍由其宿主负责。

## 来源、玩家行与恢复

新来源属性为`simulationIntervalTicks`（非负整数）和`interactionIntervalSeconds`（有限非负实数）。C#对应`BehaviorValues.SimulationIntervalTicks`与`InteractionIntervalSeconds`；自定义动作声明的`intervalTicks`是模拟缺省，新增`interactionIntervalSeconds`是交互缺省。省略不贡献，0是明确关闭该类动作间隔，作者来源后按玩家profile／Race／准确Xeno逐字段折叠；Reset只清准确层／行的贡献或回到其模板。

```xml
<action key="example/Mixed" simulationIntervalTicks="120" interactionIntervalSeconds="0.5" />
```

上述只适用于其真实固定条件允许的机会。官方交互行只接受秒参数；其他十二项只接受模拟tick参数，给错单位时准确拒绝，不将数字重解释。原`hardIntervalTicks`来源属性及玩家`hasHardIntervalTicks`记录被拒绝并指明合同／来源；原数据保留供显式恢复／替换，不自动转换18或36、不清整份配置。仅保留拒绝信息不是保留旧执行路径。未改语义的音频、包选择、外观等不需要重建。

升级前由玩家自行备份配置，再明确恢复／替换被点名的动作行或来源，用新单位重新填写；有意保留旧配置时不要覆写该备份。自动迁移和真实Config访问都不由本实现执行。旧版本不识别新字段，回退须用用户自己保留的对应旧配置，而不是让新版本猜测或抛弃数据。

动作编辑区对官方行只显示当前适用单位；自定义混合合同的机会选择器只是编辑视图，不改合同或运行时钟。数值旁显示相同生产fold的有效值／来源，空白继承与显式0分开，两个机会类别可分别恢复。普通编辑立即发布、0.35秒合并保存，原子行／来源提交保留persist-first和Committed后发布失败含义。

## 收件与原生验收

沿现有开发者日志等级明确选Enabled，`usdiag response`每Comp最多64条，禁用期间有响应会重置本地预算；到限会标exhausted。行内包含实际机会、EventFact、root、game tick／单调秒、动作适用间隔及来源、截止、H/W、模拟占额、最终门／回执；不重新取事实或随机数。未输出摘要不等于没有成本或没有拒绝。

获准实机时，用同一固定配对：不选Dev，暂停，确认满足资格／有对应可用音源的同Pawn，两次分别被原版接受并反馈的选择相隔超过0.3秒；再看短于间隔的拒绝是否具名。征召两方向各按0.6秒，运行／变速仅作对照；普通状态不能被不合资格选择唤醒。旧root精确重放、同root重入及部分失败组合没有现成原生执行场景时保留为证据缺口，不新增测试入口。全十五人审与真实听感门不被本地替身PASS代替。