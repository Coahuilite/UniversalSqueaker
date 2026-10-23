# US Kernel 0.6.x 首版规格

修订：2026-10-23，r13（GLOBAL Dev Equip入口）。状态：**十五包现有适配经同一生产owner；Work覆盖收窄为US-C3实质未完，普通范围／节奏及逐包人审／原生未关闭**。目标 RimWorld 1.6.4871，net472。

本规格面向 Kernel 开发者、合同／能力扩展作者及 VoicePack 作者。它集中定义首版目的、职责、数据含义和验收，不要求阅读开发过程中的讨论稿。0.6 当前声明与完整动作键用法见第21节；0.5.x 格式只在[旧作者指南](voicepack-authoring-0.5-zh.md)说明，不作为未发布新线兼容约束。

**效力约定：** 标为“已定”的部分是已经确认的要求；标为“设计基线”的部分是本次整编的首选方案。第 15 节列出设计基线中仍需裁定的行为与公开格式，相关切片实施前冻结，不能把本稿当成已发布 API。代码与 XML 名称只有明确引用现有实现时才表示可直接使用的入口。

## 1. 首版解决什么

US 让准确的服务对象，在有正向合同依据且满足发声资格时，从允许的声音来源中选择资源，经局部调制和 US 输出控制接入原版音频系统。

**已定目标：**

- 当前重点是 Pawn 的 Race 能力与作者自由度；Biotech 的 Xenotype 是可选细分。通用协议不强制所有服务对象具有 Race、年龄或心情。
- 包作者用 XML 使用、组合已有合同；真正新事实／逻辑通过公开 C# 接缝提供，不要求修改 Kernel 内部。
- 多个 VoicePack 增加候选声音，不增加发声机会、独立冷却或行为配置权威。
- 玩家通过一个全局值调整“多叫／少叫”；局部覆盖不能吞掉这个独立总控。音量、距离与行为活跃度是不同参数。
- 无匹配不自动等于 Call。Call 仅在自身合同成立且其他更高优先候选未被选中时，作为最低优先级选择后备。
- 资源检查在初始化完成；已加载包可在设置中启停，包增减需要重启。
- 配置、来源恢复和诊断不写入游戏存档；卸载不能损伤存档。设置与玩家覆盖保留既有 Config 生命周期。

### 首版不做什么

不实现完整 NonPawn 宿主、机械声／AI 独立分层播放器、通用长播放租约／续期、抢占矩阵、OneShot 精准抢停、自动补播队列、热装卸模组、自己的 XML／程序集加载器或通用迁移平台。也不借 Kernel 改造重做整个设置 UI。

已有 Sustained 入口与合法用法保留，按第 11 节维护；封存新机制不等于删除旧能力。音源目录和配置协议必须容纳准确 NonPawn 身份，不能以首版只实现 Pawn 为由把公开模型锁成纯 Pawn。

## 2. 职责、状态与依赖

设计基线采用五个业务域和两个运行时适配边界。分组不规定程序集或类的数量。

| 归属 | K 编号 | 负责 | 不负责 |
| --- | --- | --- | --- |
| 合同与动作语义 | K01–K03 | 准确上下文、基础条件、组合和动作包装 | 游戏查询、计时、播放句柄 |
| 发声决策与节奏 | K05–K08、K12 | 机会、门控、选择优先、冷却及结果结算 | 读素材、重新定义动作、直接改音频 |
| 声音供给与选择 | K09–K11 | 初始化准入、来源模式、候选和抽样 | 增加动作机会、改服务对象身份 |
| 配置与兼容 | K13、K17 | 作者来源、玩家覆盖、有效参数、保存与恢复 | 发明缺失事实、维护运行冷却 |
| 表现计算与空间语义 | K14、K15 | 本次局部倍率、参与规则、空间参数含义 | 写共享 SoundDef、拥有后端实例 |
| 事实／事件适配 | K04 | 读取原版／模组事实，提供公共能力与真实通知 | 把 Harmony、Pawn 或游戏查询放进纯层 |
| 播放后端 | K16 | 已选请求、原版调用、实例效果及生命周期 | 重选动作／声音、决定重试、写冷却 |
| 横切结果与验证 | K18、K19 | 可解释结果、固定输入测试、边界验收与成本 | 第二套业务决策或运行状态 |
| 封存／首版外 | K20、K21 | 保存范围边界 | 通用长播放治理、完整 NonPawn 播放实现 |

```text
原版／扩展机制
  → 显式事实适配：实际主体、参与者角色、阶段与结果
  → 主体实例＋服务对象族＋准确域门禁
  → 真实事件或到期机会的一次有效配置
  → 合同／动作选择及门控
  → 同域来源路由与选音
  → 本次局部调制
  → 原版播放适配
  → 原决策持有者按结果结算
```

**唯一持有者：** 每个运行服务实例的机会、动作／共用截止和同 tick 名额由同一个决策持有者维护；持续实例句柄由播放适配持有；定义来自其提供者；目录和有效配置只是派生只读视图。现有 Comp 可以承载这些不同职责，不为分类再造多个 Comp。

纯层不引用 Verse／RimWorld／Unity／Harmony、Config 或音频实例；公共合同不泄漏这些平台类型，纯编译／测试也不得以游戏或引擎 stub 偷渡依赖。事实、时钟和随机输入由外层提供；后端结果由外层返回。同级组件不互相回调建立隐式调度。优先使用纯转移函数、组合条件、局部策略与适配器，不引入全局 EventBus、DI 平台或通用责任链。

## 3. 服务对象与稳定身份

### 3.1 对象边界（已定）

本版分组为 Pawn／NonPawn，但分组不是完整身份，也不是共享池。

- Pawn 主域为准确 Race；启用 Biotech 时可有 `(Race, Xenotype)` 子域。
- NonPawn 使用明确对象类型／命名空间与目标身份；装甲型号不能塞进 Race。
- 来源、事件参与者、发声对象和空间定位分别表达。Pawn 进入装甲，不自动使装甲 AI 的声音属于该 Pawn。
- 声音来自实际服务对象。无定位、对象已失效等动态拒绝必须与资源损坏分开。
- 年龄是事实与筛选维度，彩蛋是独立候选标记；二者不各自变成服务域或行为来源层。
- 非 Pawn 不自动获得 Pawn 年龄、心情、器官或 Talking 事实；适用能力可以复用同一调制与配置机制。

Kiiro 与 Ratkin 不互借池；工作台不借 Pawn 死亡音。准确域约束贯穿模式、后备、混合、配置和恢复。明确允许的同 Race 异种→Race 路径不算跨界。

### 3.2 身份分别表示什么

| 身份 | 含义 |
| --- | --- |
| 模组身份 | 原版加载集合中的规范 packageId；不按作者名或内容相似性去重 |
| 定义身份 | 定义类型空间＋来源 packageId＋局部稳定键；同包可有多份定义 |
| 服务配置域 | 对象族＋准确主目标＋合法可选细分；提供者给出父路径 |
| 音源资源身份 AudioId | 实际 SoundDef 提供者 packageId＋SoundDef.defName |
| 声音条目上下文 | VoicePack／条目、动作、变体、权重与参与规则；不等于 AudioId |
| 运行实例 | 某个实际对象的本次运行身份；不作为配置覆盖层，也不持久化播放句柄 |

一个 SoundDef 可以被多个条目引用。A 明确引用 B／Core 的声音时，资源仍属于 B／Core，A 的权重和参与声明仍属于 A 的条目。packageId 由真实加载来源绑定，不接受任意 owner 字符串冒认其他提供者。

## 4. 合同、动作与事实接入

### 4.1 只读定义与三态组合（已定）

合同读取真实事实；包装是提供者维护的固定条件与公开参数，不是中央枚举限定的种类。允许独立根、多级合法继承与组合；不要求内置 Pawn 父包。继承只增加固定条件，定义及其子树不可被外部数组／集合别名、玩家配置或其他扩展改写。完整 ActionId 贯穿注册、配置、事实消费、决策、音源映射、动作保护、请求和诊断。继承来源、优先关系、配置域继承、音频复用分别声明；不把子动作折回 BaseAction 尾名。

All：任一 False 则 False，全部 True 才 True，其余 Unknown；Any：任一 True 则 True，全部 False 才 False，其余 Unknown；Not 保留 Unknown。只有 True 进入选择。缺必需实现是初始化隔离；运行 Unknown 不拓宽为更粗合同，也不制造 Call。清醒、未倒地、玩家可控、器官／Talking 是明确适用对象的资格，不隐藏在通用 Select／NonPawn 门禁中。官方 Pawn 默认资格有产品影响时须用户确认，未决不等于全放开。

### 4.2 作者与事实适配各负责什么

普通 XML 作者组合已有合同及所需参数，不必穷举未使用的精细项。真正新事实由一个显式 IUsDefinitionProvider 提供；原版适配由 US 维护，扩展新增机制由扩展维护，不 patch Kernel 求值内部。声明发现可自动化，但业务含义不能由类名、Job片段或通用 bool 返回值猜测。

注册分离完整集合收集／引用解析，环、重复身份、缺父、缺必需事实明确隔离；不靠 Def 遍历顺序取得定义权。新声明省略 formatVersion 永久为1。当前公开形状见第21节；发布前可破坏性重设 API/XML/配置，不保旧双轨，已有有效准确域、来源恢复、单 owner、四结果和实例输出合同仍保持。

事实记录是产品维护的一部分：[事实—证据—反例](us-kernel-0.6-fact-adapters-zh.md)至少持有语义身份、版本／原始方法、主体角色、能力、进入／退出／结果、依赖／Unknown、消费者、反例、维护者与源码／替身／原生分别状态。首份已填实际 Eat 依据，后续适配与测试随同更新。

### 4.3 官方目标15包，当前迁移状态分别记

| 范围分类 | 官方包装 | 正向语义／不得混同 |
| --- | --- | --- |
| 通用 | Select | 实际接受的选择＋反馈意图＋最终主体；仅方法被调用不够。官方Pawn三项可调默认已裁，固定接受证据不变 |
| 能力限定 | Wounded | 实际受损者／处理阶段；瞬时通知不等于现存伤口，不能自动保证正伤害阈值 |
| 能力限定 | Death | 明确进入Kill或死亡完成；当前prefix只证明前者，不把Destroy/消耗都称死亡 |
| 能力限定 | Attack | 已核机制攻击执行；不等于命中／伤害，目标与攻击者分开 |
| 能力限定 | Move | 自身移动；被搬运不是自身移动，玩家命令另为政策 |
| 能力限定 | Equip | 装备关系／装备者／被装备物及来源；旧Core玩家Equip Job限制不是机制定义 |
| Pawn | Call | 正向闲置且原地等待，不用“无其他动作”兜底 |
| Pawn | Eat | 过程／执行／物品属性／结果分别供给，见4.4 |
| Pawn | Sleep | 实际睡眠，不因有睡觉Job或未工作就成立 |
| Pawn | Social | 明确社交机制与准确Def，不凭名称片段猜测 |
| Pawn | Joy | 娱乐事实，可与Social重叠，由仲裁处理 |
| Pawn | Work | 工作来源、实际行为及阶段，workGiverDef是当前覆盖线索，不冒充全部工作 |
| Pawn | Draft、Undraft | 玩家命令前后状态真实变化及方向，不监听任意setter或制造逐机会事件 |
| Pawn | MentalBreak | 相应崩溃来源成功启动；任意InMentalState不等于崩溃，持续依据另列 |

分类不是继承树或三个执行引擎。完整 NonPawn 宿主仍首版外；目录／身份／纯链不能锁成 Pawn。官方 Crying/Giggling 包装、专属响应／开关及无消费者残余须在D-C完整移除；不改原版状态／婴儿声、不整体禁声这些Pawn，第三方仍可定义自己的独立合同。当前官方库存已迁为15，专属MentalFit patch／通知／开关及其无消费者残余退役；独立第三方合同不受名称黑名单限制，历史17字符串语料不作当前库存。

### 4.4 Eat 已定参数与退出

固定过程／角色证据与公开参数分开；缺省 **phase=WholeProcess、nutritionScope=Any、drugScope=Any**。phase可选实际摄入执行；营养可不限／仅有营养属性／仅无营养属性，判据 IsNutritionGivingIngestible；药物可不限／排除／仅药物，判据 IsDrug，两轴独立。Any不要求对应可选事实已知；选择精细分支才依赖该实现，必要事实Unknown不回Whole。

作者来源、玩家Pawn服务层、Race、合法Xeno按第9节覆盖；固定合同不可删除。最小声音包只需身份、准确目标与 Eat→可解析AudioId，不必给精细参数、另建动作或补原版适配。精确阶段不增加机会、不保证短阶段必播。OneShot在所选过程／阶段退出后停止新起播、既有自然尾；Sustained按所选依据失效结束并沿原生淡出，不能在每次chewing退出时一律结束WholeProcess。

JobDriver_Ingest的准备／执行／Finalize、工厂不等进入、FoodFeedPatient的Deliveree、dispenser目标切换、Corpse复入、物品属性与0营养结果分别记证。D-B现已按实际进入／退出、Deliveree及返回结果接入生产owner；公开入口与配置语义见§22。旧布尔联动与debugName宽化已退役。

## 5. 初始化、资源身份与目录

### 5.1 首版绑定路径（已定）

```text
原版读入 XML
  → US 在 patch 前只读记录相关定义的类型／原名／来源包
  → 原版照常 patch、构造 Def、解析引用与音频
  → US 核对最终声明、来源、重复与资源可用性
  → 发布 AudioId → 最终 SoundDef 对象的绑定及有效目录
  → 运行期选池返回 AudioId，K16 消费同一绑定
```

原版 SoundDef 仍须在同类型全局命名空间内唯一。US 不隐式改名，不克隆定义或复活被覆盖对象，不重写加载器。读取来源是启动期有限索引，不是补丁审计／回放平台。

合法字段 patch 使用最终内容。整根同类型、同名替换仅在最终定义唯一、原来源唯一且无相反来源证据时沿原身份绑定；删除不复活。patch 新建资源而无法确认来源时不能猜 owner，首版新增独立声音放普通 Defs 再引用。

最终重复检查必须覆盖解析后的声明集合，不能只看覆盖后剩一条的 DefDatabase。两份独立同名声明仍存在时，不发布该资源绑定；其他未受影响内容保留。普通作者不增加注册 Def、owner 字段或逐条 packageId。

**Ratkin 示例：** `coahuilite.ratkinusexp` 中 `US_RatkinExp_Work` 形成相应 AudioId，绑定完成 patch 的最终对象。该样例有 15 个 SoundDef、41 个 OGG；Work 的 3 个 OGG 属于一个 SoundDef 候选，不是三个 US 层候选。B 制作独立包时应同时改自己的 Def 名、引用和素材目录前缀；只改 About 的 packageId 不能解决原版重名。B 若只撞 Work，A 其他 14 个未受影响候选仍可用。

### 5.2 准入与隔离（设计基线 D6）

| 错误 | 隔离单位 |
| --- | --- |
| 根身份／准确目标不合法 | 对应声明或目标绑定；不连坐同包其他目标 |
| Race／可选 Xenotype／DLC 不可用 | 实际依赖它的绑定；不跨域降级，不删休眠配置 |
| 必需合同、筛选能力或参数无效 | 依赖它的包装／条目；不能删条件后放宽资格 |
| 音源引用、SoundDef 或实际资源池不可用 | 该 SoundDef 候选及其引用；无关声音继续 |
| 必需调制实现缺失 | 实际依赖它的响应／播放部分；不以倍率 1 掩盖缺失 |
| 提供者构造／收集异常 | 该次未完成提供者批次，不发布半批；其他完整批次继续 |
| 作者行为来源无效 | 按第 9 节恢复或暂停，不让其他方案接管 |

单个坏 OGG 若已由原版可靠排除且最终内部播放池仍可用，可保留 SoundDef；若不能确认，则隔离该 SoundDef。US 自行枚举时发现一个好 clip，不证明原版内部缓存一定安全，不为修复个别文件重写私有 grain 池。

以上粒度以原版已成功构造、US 可以识别的声明为前提。XML 文件无法解析或程序集无法加载时，不能承诺从尚未形成的声明中逐条抢救；报告原加载故障及可确定的依赖影响，不另造加载器。

目录覆盖已加载但未启用包、包后备、玩家映射及允许玩家在本次运行选入后备表的候选，包括现有编辑器可浏览的 Core 声音。浏览目录不扩大准入；无当前 Pawn 不使已加载 Race 从浏览身份目录消失。

声音包启停只改变有效选择，不重载资源或反复订阅能力。模组增减重启；初始化失败与确定缺包分开。第三方 DLL 仍处于同一进程，US 不承诺为其任意副作用提供沙箱。

## 6. 发声机会、门控、选择与冷却

D2单机会与四结果现同时适用于OneShot和Sustained新启动／替换；Call自身成立与最低优先级是已定约束。具体15包总排序仍是明示候选，不等于发行默认。

### 6.1 一份状态机会，真实事件另行唤醒

- 每个可服务 Pawn 只安排一份日常状态机会，不按动作、合同或 VoicePack 各建重检循环。
- 到期读取当时事实，按明示优先选择一个成立动作。等的是 Pawn 的机会，不预订旧 Call／Work 请求。
- 真实事件立即进入同一决策入口，不等待状态附加等待或失败重检；仍须通过自身和适用共用限制。
- 状态变化不重抽等待、不清冷却；没有可靠通知时，变化在下一机会才被观察。这是低频采样，不宣称零轮询或入态即时响应。
- 每个 Pawn 同 tick 最多进入一次新播放尝试；先占名额再调用后端。已有 Sustainer 维护不占新播放名额，失败不释放名额供同 tick 连环试播。
- 同批已知候选按明确优先择一；同级用公开稳定次序，不用注册顺序。后到且此前未知的事件不追溯改写已派发结果；未中选事件不排队补播。

选择表示“哪项动作最符合本次语义”，不表示“哪项最容易成功播放”。首选规则是选中后门控／选音／准备失败便结束本次，不改判低优先动作或 Call。动作内已明确的音源后备照常允许。

### 6.2 门控边界

| 门控 | 设计基线 |
| --- | --- |
| 系统总禁用、主体／地图／定位 | 先拒绝无效请求；不据此重写资源目录 |
| 动作启用、scope、主动命令 | 消费对应动作有效参数与真实来源事实，不能让声音包改变动作资格 |
| 动作与共用硬截止 | 在消费本次概率／选音随机前判断；这是相对旧顺序的显式变化 |
| 器官与 Talking | 分开的显式适用资格；通用Select及NonPawn不隐藏强制Pawn能力。选择了该资格的包装按真实证据执行 |
| 事件／状态的随机资格 | 只在所属策略的明确点采样一次；同一频率因素不重复作用 |
| 播放前动态检查 | 核对对象仍有效与位置可用，不重跑合同、抽样或资源首检 |

官方 Pawn 各包资格须逐项显式审阅。Select 的通用机制不含玩家控制／清醒／未倒地；官方Pawn Select三项默认已由用户裁定为true，分别以公开可调参数表达；不取消固定接受／反馈／最终主体，也不把这些条件塞入NonPawn或独立根。其余政策仍由隔离Dev输入承载。视野／定位属于服务边界，Death Talking等例外与新扩展许可须明示；不得把 allowExternalActions 自动作为所有已注册扩展的总闸。

### 6.3 计时状态与结算

新规则硬限制使用游戏 tick，附加等待非负。发布前裁定已退役旧概率、Realtime及倍率乘硬截止政策；合法字符串能力、身份、许可和scope保留并接同一owner，不再接第二旧时钟。旧字段没有被改名冒充A，新A独立保存。

| 状态 | 归属／起算 |
| --- | --- |
| 动作硬截止 `H_action` | Pawn＋动作键；由新派发或结果不明保护推进 |
| 共用硬截止 `H_shared` | Pawn；约束遵守共用限制的动作，不是全种族／全玩家共享 |
| 状态附加等待 `W` | Pawn 下一份日常机会；安排时抽一次，不逐 tick 重抽 |
| 失败重检等待 `R` | 推进同一份状态机会；有独立于滑块、严格大于 1 tick 的下限 |
| 同 tick 名额 | Pawn；防止多来源同步突发，与跨 tick 冷却分开 |

动作与共用截止同时适用时取较晚值，不串行相加。首选保留“豁免共用检查不等于免写其他动作的共用保护”的旧语义，具体内置动作权限与 D2 一并冻结。

令本次 tick 为 `t`：

- 新派发：从本次派发 tick 更新硬截止，不等声音结束。状态来源按 `max(t, 适用 H_shared) + W` 安排下一机会，并满足最小唤醒间隔。
- 明确未派发：不刷新硬截止，状态机会安排到 `max(t + R, 适用 H_shared)` 之后。
- 结果不明：从可能派发的 tick 保守推进适用硬保护，不写“最近确认派发”；状态按重检路径且满足保护截止。
- 同键已有持续实例：不刷新派发时间或冷却；状态机会若仍运行，推进到未来检查，不滞留到期入口逐 tick 尝试。
- 动作自己的长冷却不冻结整个 Pawn 的机会，也不为它创建独立计时任务。
- 后来的事件推迟共用截止时，未消费的状态机会须服从新截止，不重抽／追加整段 W。首次可服务时安排错峰，不虚构上次派发。
- 状态供给全关闭或主体不可服务时停止日常机会；相关配置／生命周期恢复后再安排。真实事件的拒绝／失败／不明都结束本次，不到期补叫。

这与旧线“进入后段尝试就统一收冷却”的区别是结算依据与机会归属，不是简单把等待从派发前移到派发后。

## 7. 玩家单值“多叫／少叫”

**已定：** 全局活跃度是独立输入，局部倾向不能覆盖它；不靠负等待抵消硬冷却，不改变音量、合同真假或包启用。

**已定 D3 核心映射：** 活跃度 `A > 0`，`A = 1` 为不缩放；系统禁用仍有自己的明确语义。

- 状态：对局部策略给出的非负基础等待样本 `W_base`，使用 `W = ceil(W_base / A)`，只应用一次。有效等待仍满足内部最小唤醒间隔。
- 事件：对允许节奏抽样的回应，以 `p = min(1, p_base × A)` 决定接纳；不再为同一全局活跃度叠加另一项事件等待。仅真实事件到来时求值，不创造事件或补播。
- 必回应动作若由动作合同明确指定，则不被节奏概率抽掉，但仍服从系统禁用、能力与硬限制；Wounded 不因名称自动成为必回应。
- 人口／游戏速度如保留节奏响应，进入状态基础等待的一个明确计算点，不同时缩放硬冷却和概率；Talking 仍按能力门自身语义处理。
- 修改活跃度不重置已记硬截止、不重抽已有等待；首选从下一次排程／下一真实事件生效。音量和距离的在播即时响应另按第 10 节。

采用两种映射是为了尊重输入差别：状态机会可以安排到未来，瞬时事件只能决定是否回应当次事实。它们共用一个玩家总控，不为事件额外维护软等待状态。

更多声音受真实事件数量、合同成立、供给、硬限制和概率上限约束。A 加倍不是实际发声数必然加倍。待裁定的 UI 范围、初始等待分布、事件基准接纳率、关键例外及人口／速度数值集中在 D3，不由实现者把旧逐 tick 概率直接搬成“每次新机会的同一概率”。

## 8. 声音来源、候选与抽样

设计基线复用现有路由和抽样，完整身份代替裸资源名，不因新合同体系重写算法。

### 8.1 来源与模式

```text
当前 (Race, Xenotype) 的动作声音
  → 当前 Race 的动作声音
  → 当前精确域已启用包的 PackFallback
  → 最终 Race 后备表的本动作映射
  → 无候选
```

Xenotype 请求可按明确路径使用 Race 常规声音，但不会因此借用 Race 包的 PackFallback。最终表是准确域与动作的显式映射，不表示所有 Race 自动拥有原版音源，更不是种族行为 baseline。

| 模式 | 保留含义 |
| --- | --- |
| Fallback | 返回第一个可用来源层 |
| Remix | 在非空可用来源层之间等权选择；不把所有音频摊平抽取 |
| Vanilla（界面“原版”） | 适配为 Kernel `Off`，只使用最终 Race 后备表；不承诺恢复全部原版发声行为 |
| Disabled | 不派发；保留选择记录，不偷偷启用后备 |

这是产品设置模式，不是 VoicePack XML 字段。现有适配把 Disabled 投影为 Kernel `Disabled`，生产入口也提前旁路；不能据此把“禁用”和“原版”合成一个公开语义。

### 8.2 层内筛选

年龄与彩蛋独立。优先使用包内精确年龄候选，无精确年龄才用全龄；再过滤彩蛋许可、条件和资源可用性。按包权重、变体权重和有效声音抽取，本次选中条目上下文与 AudioId 一起保留。

旧实现中精确年龄组存在但过滤后为空，不在该包内再退全龄，而是按模式处理其他来源层。本规格首选保留此边界，任何改变必须作为选择语义变更而非“修复身份”的副作用。普通／彩蛋同动作同年龄变体可并存，不能沿用旧 XML 校验器仅按 `(action, ageTag)` 唯一的限制。

彩蛋总许可关闭时排除彩蛋候选，开启时是附加入池，不是年龄替代。降低包权重只改变与其他合格候选的相对占比，不等于减少 Pawn 总机会。诊断或设置预览不能再次抽样来猜测本次实际结果。

## 9. 行为默认、玩家覆盖与恢复

### 9.1 作用域与权限

**已定：** Race 是本轮 Pawn 配置主域，Xenotype 为可选覆盖；通用服务域不要求 Race。US 不生成每种族内置行为 baseline；声音后备表与公共能力缺省不是作者方案。

设计基线对同一参数先折叠作者层，再折叠玩家层，最后应用独立总控：

```text
已声明动作／能力缺省 → 已采用主域作者贡献 → 已采用子域作者差量
                    → 玩家服务对象 profile → 玩家主域 → 玩家合法子域
                    ＋ 独立 system profile 总控
```

作者更具体的 Xenotype 默认不能盖过玩家 Race 覆盖。未写字段是不贡献，不等于显式 false、0 或 1。旧窗口Global行为行只属于Pawn服务层，不能影响NonPawn；system总控独立，域不能重新启用或抵消。完整profile现通过生产actionTuning稀疏行、ExposeData及同一原子保存入口持久化；同一个字段不兼任两种语义。

可继承的 Pawn 策略包括动作资格／scope、动作与共用硬间隔基准、局部等待倾向、适用的 Talking／进食／人口节奏响应和调制参数。真实器官／年龄、DLC 可用性、身份、总音量、全局活跃度与彩蛋总许可不由 Race 覆盖伪造。距离仍只有当前全局定义，不增加逐种族范围。

### 9.2 每个精确域只选一份作者来源

S3实际入口、字段、17动作对账和保存故障边界见[S3实施页](us-kernel-0.6-s3-zh.md)。下表首次唯一自动采用仍为产品候选，仅显式Dev注入测试；普通初始化不采用。既有恢复默认不自动遗忘新行为来源。

| 来源意图 | 当前处理 | 后来出现新方案 |
| --- | --- | --- |
| `Unconfigured` 尚未配置 | 无来源沿合法父路径与能力缺省求值 | 唯一候选可自动采用；多个提示冲突，不按加载顺序选 |
| `Inherit` 明确继承 | 动态跟随父域，不固定当前值 | 不自动采用本域新方案 |
| `Bound(source)` 已采用 | 使用选定来源及玩家覆盖 | 保留原来源并提示竞争者；仅玩家明确切换 |

三态与来源域恢复已定；首次完整候选集合的自动采用规则是本规格的设计基线。先来后到指已经成功采用并保存的来源，不指文件遍历顺序。多个包引用同一方案只算一个来源；作者不必共同依赖某份 Ratkin baseline。仅提供音频也合法。

方案可以稀疏到只启用年龄响应；同域仍只选一份，不因新包“只补一项”就自动拼装作者配置。推荐一份 Def 服务一个对象，但允许同包多 Def 或一份声明多目标块；选择和恢复按来源 Def×精确域区分。

停用声音包不等于卸载行为定义。已绑定来源仍加载时，声音可停用而默认继续可读；Race 选 A 不替所有 Xenotype 写入 Inherit，未配置与明确继承必须区分。

### 9.3 缺源恢复（已定最小范围）

- 每个已绑定来源域保存一份最后有效的**作者贡献**：稳定身份、字段存在性和值，包括关闭、false、合法 0，以及 US 自动取得的必要参数语义。
- 不保存父层最终值、玩家覆盖、总控、对象事实、合同真假、随机样本、冷却、句柄或委托。关闭动作在首次采用时即有快照，不等第一次发声。
- 当前来源有效则使用当前数据并完整替换快照；来源缺失／坏更新时使用兼容快照，无法可靠解释则暂停受影响部分。坏更新不覆盖好快照，缺失期结果不回写成作者数据。
- B 不自动接管缺失 A；同身份同域 A 有效归来且玩家未忘记／改选时自动重连。明确继承的子域跟随父域恢复，不另复制父结果。
- 参数可恢复不等于实现可恢复；缺失合同／能力／资源仍阻止依赖它的路径。US 不缓存上一次 true 来替代实现。
- 普通 XML 作者不维护恢复版本／迁移脚本。参数提供者维护正常语义，US 负责兼容判定、快照和已知格式读取。
- 忘记失效音源只移除音源选择；忘记行为来源解除本域绑定并清对应快照，转为明确继承，保留玩家覆盖；删除整个目标是另一项更大操作。不能扩张旧按钮的删除范围。

## 10. 调制与 US 全局输出

### 10.1 中性缺省与参与（已定方向）

没有显式配置时，US 额外音高／音量／校准倍率均为 1，抖动为 `[1,1]`，年龄／心情响应不启用，心情阈值未配置。原 SoundDef 自有设置保留；“全部置一”不适用于开关、概率、阈值或冷却。

响应是否启用、参数值、选中素材是否参与分别表达。注册能力、声明 Apply 或只改倍率都不能隐式启用响应。关闭时可保存参数预设，界面应说明未生效原因；显式启用前检查必需输入，不暗补旧风格阈值。

通用段表达校准等输出参数；能力段引用有明确适用对象的响应。年龄／心情为 Pawn 首批能力，其他对象可复用通用合成并接入自己的能力提供者，不冒用 Pawn 的事实。

设计基线：参与声明逐字段解析 `包默认 → 包内动作默认 → 单条例外`，再应用玩家对同一参与参数的覆盖；只得到一份结果，不加工三次。最终省略视为参与已经启用且适用的响应，不会自行启用它。

### 10.2 倍率合成（已定 D4）

```text
局部 pitch = 来源校准 × 各启用且参与的响应贡献 × 一次抖动样本
局部 volume L = 来源校准 × 各启用且参与的音量贡献
播放时刻 t 的 US 音量贡献 = L × 当前全局总音量 G(t)
```

同一参数的继承先求一个有效值，不把各层重复相乘。同一规则身份不因多包引用而重复应用。每个规则读同一本次上下文、返回自己的贡献，不读写前一规则累计结果或共享声音对象。

音量允许合法 0；音高与抖动为有限正值，输入与最终输出需符合公开范围。必需规则异常或输出非法值时终止准备并定位原因，不静默当作 1。按稳定规则 ID 固定归并／诊断次序，不开放 priority、before／after、任意中途 clamp 或 DSP 排序图。

启用年龄响应时，原生对应轴分别来自真实 `voxPitch`／`voxVolume`；显式年龄覆盖只替代对应轴，不双乘，未覆盖轴仍取原生值。年龄后心情为固定合成阶段，每项读取同一原始上下文；旧心情覆盖年龄政策已退役。

**A／B 示例：** 同一 Race 启用年龄响应 1.2。A 已录好儿童音色并 Skip 年龄，其贡献为 1；B 的中性素材 Apply，贡献为 1.2。A 仍可参与心情等其他响应。玩家关闭域年龄响应后，两包年龄贡献都为 1；总音量始终应用，不能被 Skip 绕过。

### 10.3 空间与在播总控

**已定边界：** 保留声音由服务对象发出、现有全局摄像机距离衰减定义与听感目标；局部距离设置仅留提议，不开放外部空间调制。US 总控只影响 US 请求产生的实例，不接管游戏主音量或他方独立声音。

保留现有距离端点与预设：默认／Balanced 为 15–50，Conservative 为 15–65，Strong 为 15–40，Custom 沿用玩家端点。它们是当前游戏声音距离定义的参数，不改称现实米数，也不把现有空间计算简化成仅由摄像机高度决定的另一条曲线。

设计基线 D5：局部调制在本次请求固定；全局音量与距离设置沿原版已有音频更新作用于在播 OneShot、Sustained 及其后续采样，不重启、不重抽、不重跑合同。即时指下一次正常音频更新，不保证零缓冲延迟。

- 不写共享 SoundDef／SubSoundDef，不暂改再恢复，不复制整份定义。
- 在 US 请求上标记归属，不以 Pawn 类型、Def 名或“US 曾使用过”推断归属。
- 复用原版 Sample 更新，仅在首帧或设置变化时更新当前 US 因子与实例距离端点；音量替换贡献，不乘回上次最终结果。
- 原版继续处理空间位置、映射、游戏音量、淡出与并发身份；不在混音后再乘一个摄像机高度系数。
- 总音量 `1→0→0.5` 不靠除以旧值，仍存活实例可恢复；已结束实例不补播。首帧、持续音起止辅助音和池化复用均需正确传播／释放归属。
- 适配不可用时明确报告并阻止依赖它的新派发，不回退共享定义写入或绕过玩家静音。已有无句柄声音控制失败必须如实说明。

目标1.6的加载、内部资源池及Sample首帧/更新方法体已经定点核对；实际实现与证据边界见[S2](us-kernel-0.6-s2-zh.md)。原生播放、在播更新和听感仍须验证。纯 Kernel 不暴露这些原版类型；游戏升级主要修复对应适配，不要求普通声音包跟随内部 API 修改。

## 11. 播放结果与已有持续音

### 11.1 请求与结果（OneShot与Sustained新派发均已采用）

请求只含准确对象／定位、已选 AudioId 与绑定元数据、局部参数、播放形态及允许的实例操作、动作／条目归因与尝试 tick；当前全局输出从已发布状态读取。无需持久请求队列，K16 不重新建池或抽取。

| 结果 | 证据与结算含义 |
| --- | --- |
| Dispatched | OneShot 调用正常返回，或新 Sustainer 有效建立；按新派发结算，不承诺已听见 |
| AlreadyActive | 同键受控实例已经活跃；无新派发，不刷新冷却 |
| Rejected | 确认本请求未新派发；保留原因，状态重检／事件结束 |
| Indeterminate | 已进入可能产生音频副作用的调用，结果无法确认；保留硬保护，不记确认派发、不立即补播 |

异常是原因，不是第五种互斥结果。调用前异常属于明确未提交；调用中无法排除副作用则结果不明；调用正常完成后的诊断故障不能撤销 Dispatched。无音源是上游结果，不伪造原版拒绝。

正常 void 返回可能未真正起音；反之，未取得 Sustainer 句柄前也可能已经发出启动音。只报告可观察证据，不用没有句柄推断没有副作用。

### 11.2 Sustained 的最小范围（已定）

仅对同一服务对象、K16 实际持有的既有单 Sustainer 槽位：

1. 同键有效实例复用，不重启、不因换包而替换、不刷新派发时间。
2. 异键旧实例仍有效时，只允许严格更高优先级的新动作替换；同级、更低或无法证明更高时保留旧实例。复用动作优先关系，不引入第二套抢占排序。
3. 新请求先通过门控与准备，再核对、结束旧实例并尝试新启动。旧结束与新启动分别记录，失败不自动恢复或补播。
4. 旧动作自身状态依据失效时正常结束；本轮未被选中、概率未中或仍在派发冷却，不代表持续依据失效。
5. 只为活跃受控实例保留一个内部低频检查点：适配提供事实、合同层求旧状态、决策入口决定、K16 执行 Maintain／End。无实例无检查；明确结束通知／离图／销毁可提前结束。检查节奏独立于“多叫／少叫”。
6. 实际复检无法判断时结束并记录故障；不做失败计数、宽限、续期或自动恢复。原版所需 Maintain 与简单到期比较不等于逐 tick 合同轮询。

普通 OneShot 不新增抢停能力；冷却豁免不等于播放抢占，合法新请求可能与旧音重叠。OneShot 也不因优先级自动结束受控 Sustainer。既有 Sustained 使用非 sustain 素材的降级用法保留，使用已选声音，若涉及槽位替换仍服从相同优先条件。

无停止句柄、未知来源或他方对象不作为可主动停止的目标。未知 Call 后允许豁免的受伤声音，只能说明可能叠音，不能声称已停止 Call。End 的淡出不等于原子静音；新启动失败不刷新旧派发记录。

## 12. 兼容、保存与生命周期

**已定：** 本轮未发行Config和测试包允许直接采用新语义，不保旧概率／Realtime／心情覆盖年龄双轨；稳定的音频路由、身份、许可、scope、字段存在性及保存恢复合同仍保持。US不自动读取、重写或清空真实旧Config。发布后的真实变化再按原义读取或唯一等价转换处理，不开发通用旧包转换平台。

- 不把模组发行版本当作每个参数的语义版本。普通作者改值、增添声音或合法可选参数不维护恢复版本表。
- 若公开格式允许省略格式标记，省略必须永久表示首版格式，而非“当前最新版”；未来由 US 的读取边界识别和兼容，不要求普通作者随每次发行改标记。
- 新语义不暗用旧身份。必要旧字段须在丢弃前读取，不能等 Def 已丢信息再加空迁移接口。
- 第三方 XML 不由 US 改写；配置／快照在副本校验完成后提交，失败保留原记录，关闭项、字段存在性、准确域和玩家权限不丢失。
- 发布前可重设枚举、字符串、XML和配置入口；迁移实际调用／测试／作者样例，不保兼容双轨，也不删除真实Config或恢复载荷。已有玩家导入值仍属于玩家覆盖，来源标签不将其变为作者权威。
- 正常关闭／换图／销毁释放自有订阅、实例和到期记录；不清除玩家选择，不扫描停止他方声音。包恢复重建有效视图，不重置仍有效运行冷却。
- 停用 Biotech 时保留异种配置为休眠；移除来源、目标暂不可用与目录加载失败分开，不自动删除。

## 13. 诊断与运行成本

设计基线要求一次结果至少能回答：哪个对象／动作、由什么机会或事件触发、在哪一阶段停止、选中哪个条目和资源、参数来自哪里、是否已提交以及下次允许尝试的依据。

区分无成立动作、事实不足、资格拒绝、冷却／名额限制、无候选、无效绑定、调制失败、后端拒绝、已派发、已有实例和结果不明。初始化错误包括包／Def／目标／条目及实际依赖，不能只显示“没声音”。

诊断读本次保存的事实与抽样结果，不重跑有副作用求值或随机；历史派发参数不因设置变化改写。当前实例输出证据与请求证据分开，无实例证据不伪造“已应用／已听见”。错误汇总和必要状态保持可查，不逐 tick／每帧刷日志。

成本约束：初始化资源检查一次；日常机会每主体一份；合同仅在到期／事件／活跃实例复检执行；静态池与配置共享，不按 Pawn 复制；实例无效后无残留工作。允许既有入口做廉价到期比较，不允许为“是否该叫”逐 tick 重新遍历合同或所有声音包。采用现有测试与发布入口，不为本稿新建基准平台。

## 14. 验收与实施切片

以下是待实现验收，不是本次文档整理已运行的测试。纯测试、目标版本接入和实机听感分别记证据，不能互相代替。关键合同C00–C12、固定事实／tick／随机／后端结果T01–T12及S1–S5完整切片覆盖见[合同—验收附页](us-kernel-0.6-acceptance-zh.md)。附页沿用本文的已定／候选效力，不冻结D1–D6。

| 场景 | 必须观察到的结果 |
| --- | --- |
| V01 准确域 | Kiiro／Ratkin 不串池；身份／目录用例能区分不同 NonPawn 目标；合法 Xenotype→Race 路径保留，不以此要求完整 NonPawn 宿主 |
| V02 正向动作 | 没有成立合同不返回 Call；Call 自身成立且其他候选未中选时最低优先兜底 |
| V03 单一机会 | 多包、多动作不增加状态任务；失败推进同一未来机会，所有分支均不逐 tick 重试 |
| V04 事件与切状态 | 状态等待不挡真实受伤入口；事件仍服从硬限制，Work 在下次机会按新事实判断 |
| V05 结果结算 | 明确未提交、正常派发、已有实例与不明分别更新；诊断异常不撤销已派发；不明不立即补播 |
| V06 仲裁与重叠 | 同 tick 至多一次新尝试；低优先持续请求不打断高优先在播者；OneShot 不伪称已抢停 |
| V07 作者接入 | 普通 Ratkin XML 可供给，XML 可组合已提供条件；新 C# 事实经公共入口使用且不修改 Kernel |
| V08 资源绑定 | Ratkin 15 声音准确绑定；独立重名只隔离受影响资源；合法字段／整根 patch 使用最终内容；缺绑定不退裸名 |
| V09 局部故障 | 缺 Biotech 不连坐 Race；坏 Work 不清全部包；未知必需合同不被删除后放行 |
| V10 路由／抽样 | 四模式、精确域 PackFallback、年龄与彩蛋独立、空层 Remix、权重和固定随机输入可复核 |
| V11 默认权限 | 单来源自动采用与后加入冲突区分；明确继承保持；作者子域不覆盖玩家父域；总控不被吞掉 |
| V12 缺源恢复 | A 的关闭／false／0 在缺源后保留；B 不接管；同身份归来重连；实现缺失仍暂停；单域遗忘不误删 |
| V13 调制 | 无配置恒等；改倍率不隐式启用；A 跳过／B 参与年龄；同规则不重复；非法贡献可定位 |
| V14 实例输出 | 同一 SoundDef 的 US／非 US 同播，改音量／距离只影响 US；在播／首帧／辅助音／池复用正确，定义不变 |
| V15 生命周期与保存 | 已有持续模式可用，自己的状态失效或检查故障才结束；离图清理；旧入口条件与玩家配置保持；无新存档负担 |

### 推荐执行顺序

1. 冻结首个切片需要的 D1／D2／D6，形成可加载 XML、最小 C# 提供者和固定输入用例；不是先实现所有未来能力。
2. 在获准的新线环境优先验证目标 1.6 的 patch 前来源、最终声明、资源就绪和 US 实例输出接缝；接缝失败在适配内修复或明确重新取舍，不假称 1.7 参考即通过。
3. 以 Ratkin 一个状态动作＋Wounded 打通完整链和失败路径；这只是切片顺序，随后扩齐第 4.3 节及公开基础合同覆盖。
4. 完成行为来源／恢复、调制和既有 Sustained，复用有效旧测试，补变更语义的针对性测试。
5. 目标版本集成、设置保存重载与实机声音观察通过后，才更新文档为已实现状态；当前代码版本不因规格入 docs 而改变。

## 15. 需要裁定的有限清单

D1、D2单机会/四结果、D3独立A机制、D4固定独立合成、D5持续音最小政策及D6单提供者/省略格式1已采用。剩余数值默认和来源自动采用只阻对应产品默认，不阻明示输入的技术链。

| 编号 | 首选方案及影响 | 应在何时确认 |
| --- | --- | --- |
| D1 合同结果（已采用） | 三态与 All／Any／Not 的第 4.1 节语义；未知不放行，初始化缺依赖另隔离 | 合同／XML 求值切片前 |
| D2 发声决策（已采用） | 每 Pawn 一份状态机会、稳定优先、选后失败不跨动作补位、同 tick 一次尝试；明确未派发不收硬冷却、不明保留保护；逐动作资格保持，数值排序／关键豁免仍须明示 | 第一个状态＋事件切片前 |
| D3 全局活跃度（机制已采用） | 状态等待反比缩放，事件接纳率缩放；硬限制不缩放；只在下一排程／事件用新值；人口／速度进入单一软节奏计算点 | 发行默认前冻结等待分布、A界面范围、事件基准、人口／速度和关键例外 |
| D4 局部调制（已采用） | 独立响应乘算、来源参与逐字段覆盖、固定阶段、一次抖动；明确区别于旧心情覆盖年龄 | 现用中性缺省、显式阈值和公开数值校验；不内置隐藏风格 |
| D5 后端执行 | 四类结果、实例归属与原版更新实时应用总控；不回退共享 Def 写入 | 原生适配实现／验收前；目标 1.6 技术验证不可由文档代替 |
| D6 公共数据与准入 | 一个显式提供者入口、最小声明／参数合同、完整内置 Pawn 覆盖、按语义单位隔离及来源自动采用表 | 对应作者入口／目录／配置切片前；同时冻结可加载样例与公开签名 |

K20 封存、K21 完整实现首版外、种族／服务对象边界、K13／K17 最小恢复、Call 最低有条件后备、已确认音源绑定及已有持续音最小维护，不作为上述裁定的可选删减项。

## 16. 基线与阅读入口

当前0.6作者入口见§21、统一owner及响应格式见§20；[文档目录](README.md)区分当前候选与历史交付。[0.5.x作者指南](voicepack-authoring-0.5-zh.md)只描述旧格式。以下代码用于解释保留／变化的基线，不要求实现保持原类结构：

- [现有池与路由](../Source/UniversalSqueaker/Kernel/SqueakPoolRegistry.cs)、[池数据](../Source/UniversalSqueaker/Kernel/Pool.cs)。
- [现有声明](../Source/UniversalSqueaker/Models/SqueakVoicePackModels.cs)、[当前资源投影](../Source/UniversalSqueaker/Runtime/SqueakKernelAdapter.cs)。
- [现有 Comp 入口／生命周期](../Source/UniversalSqueaker/CompSqueaker.cs)、[动作元数据](../Source/UniversalSqueaker/Runtime/SqueakActionModel.cs)。
- [唯一决策与结算](../Source/UniversalSqueaker/Kernel/OneShot/OneShotDecision.cs)、[独立响应](../Source/UniversalSqueaker/Kernel/OneShot/ResponseModulation.cs)、[持续槽](../Source/UniversalSqueaker/Kernel/OneShot/SustainedPlayback.cs)、[实例输出](../Source/UniversalSqueaker/Runtime/UsInstanceOutput.cs)。旧时序／调制纯函数只保历史政策语料，不是活动生产决策。

S2已核对远端1.6.4850及本机目标1.6.4871/Unity2022.3.35f1的关键方法体，见[S2实施页](us-kernel-0.6-s2-zh.md)；方法体依据和目标引用编译不是原生运行验收。本文没有把旧 UI 接受、历史工具测试或正常派发回执扩大成新 Kernel 的产品验收。

## 17. r2 历史阶段与迁移入口

r2阶段新增合同与固定输入附页，强化纯核心／平台适配边界；V01–V15和首版范围不缩减。D1/D2/D6及D5最小协议仍待相关切片前裁定。本轮只改产品docs，没有新架构源码、测试实现、版本或API/ABI变更；既有0.5 checkpoint与旧载荷继续是独立输入。真实测试分类、V01–V15责任证据、旧政策差异及固定FL Dev消费条件见[测试迁移附页](us-kernel-0.6-test-migration-zh.md)。

## 18. 已采用的首批范围

D1三态、D2的OneShot单机会/同tick名额与四结果/R60、Work与Wounded同批优先且无新增共用豁免、D6单提供者入口与格式号省略永久为1已获批准。该S1阶段没有批准Sustained新启动/替换、D3完整日常频率或D4调制；当前已批机制与技术实现按第20节，数值发行默认仍未冻结。实际接口、普通包/扩展示例、受控入口及证据边界见[OneShot S1](us-kernel-0.6-s1-zh.md)。

## 19. S2实现状态与后续责任

S2复用原选择器，将真实patch前来源、通过原版依赖过滤后的最终解析声明（含构造失败）、内部池完成事实及最终对象发布接到正常调用和S1。条目归因与实际AudioId分离；合法字段/更名及唯一整根替换使用最终内容，删除/冲突/缺证不猜owner。NonPawn以独立Family/Namespace/Target表示，Pawn Race/Xeno保留原API和合法父路径。来源观察原子发布，异常批次不形成半索引唯一性结论。同原节点/匹配asset已确证的来源按依赖过滤后的最终声明判唯一，不把MayRequire前同名数量误作资源冲突；只有缺asset的整根替换恢复才要求原来源唯一。

作者入口、生产路径、四模式/年龄/egg/权重保留、独立双重放与适配fixture、V01/V08/V09/V10责任和V14接缝盲区统一见[S2实施页](us-kernel-0.6-s2-zh.md)。本轮没有引入owner字段或普通音频注册Def，没有批准D3/D4、新Sustained或完整D6默认。旧黄金语料不重录；纯层、适配、目标1.6源码依据及原生观察/听感分别记证据。S3–S5和S1原生缺口不随此状态自动关闭。

**S5当前状态：** 上述历史共享距离写入已由实例级适配替代，活动路径只发布全局端点，再在已归属US的Sample更新实例AudioSource；无共享Def写入或克隆。首帧、首个／后续持续采样、起止辅助音、晚到回调与池复用已有真实Harmony替身反例；真实1.6输出及听感仍待维护者授权观察，不能由自动绿直接关闭C11/V14或完整0.6。

## 20. S4-A/B 与 S5 当前使用方式

### 唯一生产 owner 与临时激活门

`CompSqueaker.CompTick`、十五包适用通知和`NotifyExternalByKey`均进入`UsOneShotRuntime`，每Pawn只有一份`UsPawnPlaybackSession`。注册组合保留固定父条件与附加条件，自己的优先／形态／参数独立声明；直接调用注册key只执行该合同，失败不退裸字符串或其他动作。真正未注册字符串无需补注册，但仍受外部许可、scope、身份、同tick名额与同一硬截止约束。

产品数值默认尚未冻结。本开发版本未选择整体策略时返回可读状态`PolicySelectionRequired`，零派发、零每Pawn候选初始化，不回旧链。开发模式菜单`S4AB-S5 GLOBAL Dev candidate: W20 H40 I120 spread120 p1 N/speed1; Equip=PlayerCoreEquip (pending)`一次性注入全局隔离候选，显式选择待裁的玩家Core Equip回应资格；重复点同一工具不会清保护。若不用菜单，也可在新的开发进程中显式调用同一组候选输入：

```csharp
UsOneShotRuntime.SelectDevelopmentPolicy(
    new UsDevelopmentPolicy(20, 40, 120, 120, 120, 1, 1,
        equipQualification: UsEquipQualification.PlayerCoreEquip)); // 待裁Dev资格
UniversalSqueakerMod.Settings.SetActivity(1.5);
```

这些数值是高频验证输入，不是日常推荐值。参数顺序为基础W、共用硬间隔、普通动作硬间隔、Wounded硬间隔、初始错峰宽度、事件基准p、状态基础等待因子。固定W是本候选的分布；初始错峰只设下一机会，不虚构派发。人口／速度因子由本候选显式提供为1，未暗读旧滑块；将来若启用，该状态等待因子只进入W_base一次，不改事件p_base或硬截止。活动A独立以`activity`保存，有限正数，1中性；`SetActivity`沿原保存队列排队，实际机会采新值，不重建session或重抽已安排等待。临时开发策略不写公开Config，无每Pawn模式开关；数值默认冻结后移除该门。

第八项明确Equip资格。省略该项仍为`Unselected`，官方Equip会具名拒绝，不自动放开AI／非Core／加载回应。`PlayerCoreEquip`要求玩家控制、playerForced且准确JobDefOf.Equip，不改变正常返回和真实装备成员关系的来源要求。

### 普通作者：校准、参与、形态与行为来源

[Responses.xml](examples/0.6/Responses.xml)由正常Def字段加载器读取，示例不默认安装。`UsVoicePackDef.declaration`新增可选`pitch`条目校准、`participation`包默认、`actionParticipation`本包动作默认及entry自己的`participation`。参与li只接受准确响应key与`Apply`／`Skip`；包内动作li只接受准确`action`属性。它们先折叠一次：

```xml
<participation><li key="us.core/Age">Skip</li></participation>
<actionParticipation>
  <li action="example/SustainedWork"><li key="us.core/Age">Apply</li></li>
</actionParticipation>
```

同一action在另一包中不继承这里的动作默认。entry声明最后覆盖；全部省略表示参与已启用的响应，不启用响应。`UsActionDef.declaration/playback`接受`OneShot`（省略缺省）或`Sustained`，ActionDef只定义合同和形态，不持跨包参与权威。Sustained请求选到非sustain素材仍用已选声音降级一次，不重选或补播。

准确域的`UsBehaviorSourceDef`提供动作启用／scope／硬间隔及稀疏响应启用、参数。新增`moodEnabled`与既有`ageEnabled`同为可缺省bool；扩展响应用`<response key="example/Response" enabled="true"/>`，参数用`<parameter key="..." value="..."/>`。同一动作不可同时用`ageEnabled`和同名通用response重复定义年龄启用（心情同理）。行为来源不能写`participation`冒充玩家覆盖；该字段明确拒绝，来源快照仍只保存作者贡献。

| 数值key | 实际消费与缺省 |
| --- | --- |
| `age.override`／`age.overrideVolume` | 可分别替代原生`voxPitch`／`voxVolume`；未覆盖的轴仍须有真实原生事实，不双乘 |
| `mood.low`／`mood.high` | 启用心情后必填，有限且0≤low＜high≤1；事实`mood.level`由本次真实Pawn需要读取 |
| `mood.low.pitch/volume`、`mood.neutral.pitch/volume`、`mood.high.pitch/volume` | 对应区间未写为1；边界等于阈值属于neutral，不暗补旧风格阈值 |
| `jitter.min`／`jitter.max` | 缺省均1，有限正数且min≤max；范围非恒定时只抽一次，非法范围抽样前拒绝 |

音量贡献允许0，音高为有限正值；合成及L×当前G必须能表示为原生float，否则在原生副作用前拒绝。原生自身映射、音量、位置和淡出保留。参数存在或Apply均不等于启用；本次固定局部L，后续更新只读取当前全局G，不重算年龄／心情或抖动。

真正新增响应与事实共用一个`IUsDefinitionProvider.Collect(ProviderBatch)`批次。调用`batch.AddResponse(IUsResponseRule)`，实现只暴露稳定`Key`、固定`ResponseStage.Age/Mood`和`Evaluate(ResponseContext)`；返回`ResponseContribution(pitch, volume)`。它读取本次不可变数值context及subject，不接触前项累计结果；与该批事实一起完整验证后发布。缺实现／异常／非法贡献有具体原因，不删规则后放行，不开放任意排序图。

### 玩家精确层覆盖与保存

玩家仍使用`actionTuning`的Global（race/xeno均空）、Race、Xenotype准确身份。每行的`responses`保存`key`、`hasEnabled/enabled`、`hasParticipation/apply`；`responseParameters`保存数值`key/value`。`has=false`为不贡献，显式false／0保留。来源先折叠，玩家Global→Race→Xeno再逐字段覆盖；只有玩家参与覆盖能压过选中entry的Skip。

公开`Settings.ReplaceResponseTuning(actionKey, race, xeno, responses, parameters)`只替换该准确身份的响应字段；先验证／深拷贝，再交原保存队列，不清scope、导入来源或作者绑定。空集合清本层响应覆盖以恢复继承。`ResetActionTuningRow`／动作区域恢复也清自身响应覆盖，全局恢复另外把A还原1；都不遗忘行为来源或好快照。来源切换继续走S3原子stage→replace保存并接原generation，不建立第二队列。普通参数编辑失败沿原dirty/retry反馈；来源事务提交后的运行发布故障不能冒称未提交。

### 实例输出、持续槽与证据

US请求使用短寿命SoundInfo token确证创建归属；Sample／父Sustainer用弱键表关联本次局部贡献，不以Pawn、SoundDef或音源名称推断。首个持续Sample可在父构造返回前创建，从真实info捕获；后续样本沿显式父关系。起止辅助音仅在原生两个实际调用点标记，新建info不借环境栈认领，因此嵌套非US请求不被误归属。

基础Sample音量postfix应用L×当前G，派生Sustainer淡出仍由原版完成；Update在首次Play前及正常更新写自身AudioSource距离，结束和池复用释放Sample归属。End非幂等，US只结束仍活跃的自有句柄。父Cleanup完成、停止辅助音创建之后撤活动归属，保弱键关闭负身份以拒晚到grain；End后的新grain也拒绝，已有淡出Sample仍消费当前G／距离至实际Cleanup，不补播。必要Harmony目标或辅助调用布局不符，依赖的新派发明确拒绝。

持续槽只接受有效新句柄确认Dispatched；同键维护先过当前来源／许可／发声资格，不抽随机、不刷新保护；低优先拒绝，高优先先准备再结束旧音。未知新句柄保Indeterminate硬保护；旧音结束／新启动分别记录，失败不恢复。活跃槽独立60tick复检，当前资格／状态失效或Unknown结束；无槽不复检，离图／销毁／服务关闭释放自有槽。

自动证据分为纯生产源重放、实际调用／Harmony替身、Config与Host。诊断从该owner已存结果读取action／原因／四结果／确认tick／硬截止／下一机会和持续槽，不再显示旧私有时钟伪结果；旧Timing／概率／心情编辑控件在原FL0.7页面显示退役原因，保有效scope、单动作恢复、Eat及声音供给控件。原生1.6音频、听感及产品数值默认仍分别由维护者／PM接收。这是S4AB/S5原0.7候选的历史边界；当前固定FL0.8消费见§24。

新`UsVoicePackDef`不是永久启用：验证后的声明投影进既有Packs目录，沿`packageId:DefName`选择身份及准确Race域启停；未显式选择沿原目录规则为未选入，不自动采用。当前声明支持准确Race／合法Xeno及纯NonPawn域；平台完整宿主仍仅Pawn，子域选择不代替主域选择。Runtime只合入当前已选声明的条目，保其局部校准和参与元数据。关闭音频包不卸载独立行为来源，也不改变其绑定意图；Fallback/Remix/Vanilla/Disabled仍按原模式合同。

声音包根`weight`为包权重（省略1），entry的`weight`仅在已选包内作变体权重。多个entry不增加包份额；A含10条、B含1条且包等权时仍先A:B=1:1，再选本包entry。保留Kernel返回的SelectedVariant准确映射到entry校准与参与，不用AudioId猜来源。

高优先替换时，旧`End`故障单独记录为`EndFault`；若新启动回调尚未进入，新请求为Rejected，不写新动作/共用硬保护或确认tick。槽先解除持有，避免重复非幂等End，不恢复旧音或补播；生命周期释放记录一次警告并继续释放其他自有session。只有进入新启动边界后不能确认的新请求才是Indeterminate。

## 21. D-A 当前作者入口

要让“选择一个当前有伤口的 Ratkin”使用普通 Select 的同一声音，复制[完整可加载声明](examples/0.6/IdentitySelect.xml)并替换为自己的包身份、准确目标和声音引用。两个动作各自直接映射同一个资源，不建立缺音后回父动作的路径。

```xml
<declaration>
  <key>example/InjuredSelect</key>
  <baseAction>us.core/Select</baseAction>
  <priority>40</priority>
  <condition><ref>us.core/HasCurrentInjury</ref></condition>
</declaration>
```

本例固定组合“实际接受选择反馈＋当前正严重度 Hediff_Injury”。伤疤仍作为当前存在的 injury 纳入；它不是“本轮曾触发Wounded”。缺健康／hediff集合为Unknown。`priority=40`是明确的示例竞争输入，不是发行总序。父包不变、子包有自己的配置、条目与动作截止；给子包关闭或换声不修改普通Select。

### 根、继承与公开字段

`UsActionDef.declaration`必填命名空间化`key`（如`example/AfterIngestion`）和一个`condition`根。省略`baseAction`是独立根；可引用同批或已注册的任意合法父动作，多级引用不依赖Def顺序。父固定条件不可删除；循环、重复、缺父及缺必需事实隔离相应定义。`Condition.Children`是防别名写入的只读集合，目录返回的数组不赋予修改定义权。

| 字段 | 当前读取的缺省／单位 | 含义 |
| --- | --- | --- |
| formatVersion | 省略永久为1 | 其他格式明确拒绝 |
| baseAction | 省略为空 | 继承固定条件，不继承音源、配置身份、优先或播放形态 |
| priority | 0，整数 | 自己声明的竞争值；同级按完整键序 |
| intervalTicks | 0，非负游戏tick | 扩展的硬间隔；作者来源可显式覆盖，0与省略的贡献不同 |
| chance | 1，0..1 | 动作机会接纳；事件再按当前显式开发政策和活动A映射一次 |
| checkShared / writeShared | true / true | 检查共用保护与写保护分别声明 |
| requiresVocal | false | 显式Pawn发声器官资格；父包已要求时子包不能取消 |
| talkingEnabled | false | 本定义是否选择Talking概率政策，不隐式代表器官完好；允许的行为配置可覆盖该政策 |
| playback | OneShot | 可显式Sustained；省略不复制父形态，持续依据须真实可维护 |

运行事实仍由提供者／原版适配给出，不能用profile伪造。直接调用注册动作只求该完整合同，不把子键解成父事件；真实Select通知会让适用的普通／扩展合同在同一owner竞争。新增事实仍经`IUsDefinitionProvider.Collect`与`IUsFactSource.Capture(subject)`，缺实现不删条件后放行。

### 同一身份进入配置、准确域与请求

行为来源使用完整键，例如`<action key="example/InjuredSelect" enabled="true" hardIntervalTicks="50" volume="0.25"/>`。现有玩家动作行／响应API也接受完整键；`ResetActionTuningRow`只清该准确行的玩家字段，不清来源意图或作者快照。旧枚举格式在其适配边界映射为`us.core/<name>`，不会把`example/Select`或继承动作尾名当内置键。新完整服务层持久化及Eat参数见§22。

`UsVoicePackDef.declaration`可用`race`简写Pawn Race；也可用`family=Pawn`、`target`及可选`child`表达准确Race/Xeno。不要同时给`race`和`target`。目标须是实际Pawn定义；子域须已加载且Biotech可用，实际运行还服从resolver给该Pawn解析的合法子域。子域选择不替代主域启用。

纯目录另接受`family=NonPawn`、非空`namespace`和`target`，不借用Race或Pawn全局行为行；当前平台宿主未实现NonPawn，加载时明确报`NonPawnHostUnavailable`，不伪装成Race缺失。两种NonPawn命名空间可以直接引用同一AudioId，但候选与行为贡献仍隔离。

`AudioCandidate`与请求保留完整动作、条目、包、实际供音域／来源层、AudioId、主体角色及请求域。普通新声明、旧包变体、精确PackFallback及最终表均有自己的来源身份。局部调制复制已选上下文，不用AudioId倒猜条目；回执和诊断交还原owner，不重新抽选。

### Select 的当前适配和已裁资格

当前观察`Selector.SelectInternal`内实际`PlaySelectionSoundFor`的最终主体，返回后核`IsSelected`。代理递归、无反馈、失败提交和异常清理分别处理；没有US自建的重复去重状态。继承RimSage方法体为1.6.4850；本次另核指定安装的1.6.4871 rev591两方法体，目标API引用编译和实际原生挂接分别记录。完整事实边界见[维护记录](us-kernel-0.6-fact-adapters-zh.md)。

两个观察目标共用缓存readiness与Prepare；任一缺失则两patch均跳过，只停用Select观察能力，一次具名记录缺失目标。SelectEvent为带SelectObservationUnavailable原因的Unknown，直接通知不能伪造接受事实；依赖该实现的来源贡献暂停，无关合同仍运行。缺目标替身与两目标存在路径分别实证，详见[事实记录](us-kernel-0.6-fact-adapters-zh.md)。

用户已裁定官方Pawn Select默认玩家可控 AND 清醒 AND 未倒地；落实为`selectRequirePlayerControlled`、`selectRequireAwake`、`selectRequireNotDowned`三个独立bool?公开字段，包装缺省各true。null是缺失继承，false只放宽本项，不连带其他两项；玩家Pawn服务／Race／合法Xeno逐字段覆盖全部作者贡献。临时Unselected枚举已删除，现有设置下拉／Help及保存／Reset入口见§23；整体Dev激活门保留，其余发行节奏不随本默认项关闭。

## 22 D-B 有效配置与Eat当前入口

### 一次决策只读一个配置视图

`UniversalSqueakerSettings.CaptureBehaviorConfiguration()`复制来源状态与玩家行；每个动作从同一视图解析，字段来源、实际依赖与view Id进入诊断。返回值的Read再给副本，后续编辑不修改旧视图或共享合同定义。合法Xeno来自现有resolver的当前准确上下文，不直接相信raw genes；NonPawn仍只有中性配置／目录／核心链，没有完整原生宿主。

三类控制分开：

- **system**：声音服务模式、system总音量G、活动A、距离及彩蛋总许可等原全局控制。域enabled不能重开Disabled；局部音量不能抵消G=0。已派发声音保留L，在原生实例更新按L×当前G一次输出；0音量不改四结果结算为失败。
- **服务对象profile**：Pawn的空Race／空Xeno动作行；NonPawn则为自己的Namespace＋空Target行。不是system，不互相继承。NonPawn不接收Pawn的Scope、Talking、Age/Mood、Eat或Select资格字段。
- **准确域覆盖**：Pawn Race／合法Xeno；NonPawn自己的Namespace＋Target。同名Target位于两个Namespace仍是两份配置。

字段按“包装缺省 → 选中主域作者 → 选中合法子域作者 → 玩家服务对象profile → 玩家主域 → 玩家合法子域”稀疏折叠；所有作者先于玩家，最终才补缺省。false、0是贡献，不是继承。完整ActionId是身份，父固定条件继承不复制父参数。未配置的可选能力不成为依赖；非法显式值暂停相应配置，不修成默认。固定条件必需事实仍在依赖集中，不能由Whole参数抹掉固定Execution限制。

玩家行沿现有`actionTuning`／`ActionTuningRecord.ExposeData`持久化。新增presence/value字段承载enabled、hardIntervalTicks、talkingEnabled、volume、pitch及三轴Eat／三项Select资格；既有response/parameter/participation保持各自权限。旧interval/probability倍率不翻译成新chance／硬tick。服务族／Namespace／Target独立持久化；Pawn原行仍按原身份读取，旧键只在边界规范成完整内置键。

`UniversalSqueakerMod.TryChangeBehaviorOverride(family, namespace, target, child, fullActionId, values, out failure)`替换该准确身份的新稀疏贡献，复用完整SettingsBlock／UsAtomicConfigFile及原保存代次：序列化／替换失败保旧内存和文件，成功才发布新配置；旧倍率／预设来源字段仍保留给原调用方。成功提交后的运行时发布失败报告Committed状态，不再重写已成功文件。普通可见设置沿既有队列保存／失败显脏／显式重试，不另设配置平台。来源仍为Unconfigured/Inherit/Bound；last-good仅存作者贡献，缺A不让B接管，同身份A可重连，不烘玩家值、事实或句柄。

### 三个Eat参数可以独立设置

当前公开schema为：

| 字段 | 合法值 | 缺省／额外依赖 |
| --- | --- | --- |
| phase | WholeProcess / Execution | WholeProcess；Execution增加us.core/IngestionExecution |
| nutritionScope | Any / NutritionGiving / NonNutritionGiving | Any；非Any增加us.core/IngestibleNutrition |
| drugScope | Any / ExcludeDrugs / OnlyDrugs | Any；非Any增加us.core/IngestibleDrug |

例如作者行为来源可写：

```xml
<action key="us.core/Eat" phase="Execution"
        nutritionScope="Any" drugScope="ExcludeDrugs" />
```

玩家在既有“规则与调制”选择Pawn／Race／合法Xeno层，再编辑三个独立下拉框；“继承此字段”只清该层该字段。Basics恢复只清Pawn服务级Eat参数，Race/Xeno用准确动作行／层恢复。没有旧精确／包含药物bool、调试名称识别或Unknown→Whole兼容分支。摄入结果根没有活跃阶段参数；在不带摄入过程／执行固定条件的合同上显式配置这些Eat字段会报告EatParametersRequireIngestionContract。

### 最小包与两个不同方向的扩展

[EatMinimal.xml](examples/0.6/EatMinimal.xml)只声明包Def身份、Ratkin准确目标及Eat→Core Click音源，不要求行为来源／精确字段／作者适配。Click用于可解析示例，不是语音设计；实际声音包可换成自己声明的SoundDef。US不复制Core或SR资源。

[EatExtensions.xml](examples/0.6/EatExtensions.xml)给出可加载的：

- `example/InjuredEat`：继承Eat固定过程，增加当前伤口；有自己的三轴、硬间隔与局部音量配置，直接复用同一AudioId。
- `example/AfterIngestion`：独立引用`us.core/IngestionResultEvent`，不继承已经退出的chewing条件；音源和配置独立。例中的行为来源仍须显式选中，音频开关不选行为来源。

原版观察先区分driver执行者、实际摄入者和当时物品：Setup/Cleanup维护支持的Ingest／FoodFeedPatient过程；Chew工厂只加观察，原init正常返回且当前Toil一致才进入，finish退出。Whole持续不因一次Chew退出停，Execution按所选阶段退出沿既有槽End；OneShot自然尾。Thing.Ingested仅在进入时可摄入且正常返回后发布结果；合法0值保留，异常／Job结束不伪造成功。

普通Eat消费过程／执行的状态机会，不消费摄入结果事件。结果通知只把编译后固定条件树显式引用`us.core/IngestionResultEvent`的合同送入同一owner仲裁，包括嵌套引用与继承该条件的扩展；各自条件、优先级、同tick及硬截止仍照常执行。即使普通Eat的过程此刻仍True，也不能借结果通知绕过状态等待；没有任何结果消费者时，不创建owner、不运行决策或改写保护／诊断。已有独立结果合同可以在其硬门允许时先于下一状态机会回应，但仍按既有共享保护结算，不回退普通Eat补位。

中性`IngestionResultFact`携Ingester／Item／ItemDef／Nutrition，进入本次FactSnapshot、ResponseContext.Ingestion及PlaybackRequest.Ingestion；不传Pawn／Thing，不缓存成下一机会事件。事实、角色、版本与反例见[维护记录](us-kernel-0.6-fact-adapters-zh.md)。缺执行接缝按成组readiness局部隔离，Whole／无关能力保留；不会用全局PatchAll catch藏故障。

本切片证据为生产纯源／真实loader、生产settings/ExposeData/原子文件与Scribe替身往返、真实Harmony＋明确平台double，以及实际Source/Host绑定／布局。目标引用1.6.4871；继承远端4850与本次指定安装4871 rev591的定点方法体分别记录。真实游戏Config／挂接／音频和屏幕听感未跑；全部15包合同人审／成果人验pending。D-C全15导读和事实见§23，不拿其中一个共同通过替代每包覆盖。

## 23 D-C 十五包、Select资格与明确事实边界

先用[逐包导读](us-kernel-0.6-contract-review-zh.md)选择要审阅的实际效果，再用[事实登记](us-kernel-0.6-fact-adapters-zh.md)核主体、版本／方法／Def、阶段、False／Unknown／缺实现及正反fixture。官方库存恰15：Select、Wounded、Death、Attack、Move、Equip、Call、Eat、Sleep、Social、Joy、Work、Draft、Undraft、MentalBreak；每包都沿现有同一owner，不增加轮询或第二引擎。

### 调整Select的一项资格，不改另外两项

官方Pawn Select三项默认true。作者来源的当前schema例如：

```xml
<action key="us.core/Select" selectRequirePlayerControlled="true"
        selectRequireAwake="false" selectRequireNotDowned="true" />
```

这里只放宽清醒要求，玩家控制／未倒地仍要求。省略字段继承；显式false有值，不表示继承。包装缺省只补`us.core/Select`，父合同参数不自动复制到子合同；[SelectQualifications.xml](examples/0.6/SelectQualifications.xml)展示继承固定接受条件的子包如何自行选择三资格，以及没有自动Pawn门的独立根。三项字段只用于显式引用SelectEvent的Pawn合同；不适用的合同报SelectParametersRequireSelectionContract，NonPawn拒绝这些字段。它们不删除固定条件树中的任何接受、反馈、最终主体证据。

| 字段／值 | 必需事实／Unknown | 设置与恢复 |
| --- | --- | --- |
| selectRequirePlayerControlled=true | us.core/PlayerControlled必须True | 可在Pawn服务／Race／合法Xeno逐项改；false不取消另外两项 |
| selectRequireAwake=true | us.core/Awake必须True，health缺失Unknown | Awake按4871原生可清醒能力和实际driver.asleep，不以躺床替代 |
| selectRequireNotDowned=true | us.core/NotDowned必须True，health缺失Unknown | 单项继承仅清该层presence，所选false不要求该可选实现 |

现有调音页沿当前层显示三个下拉及Help；不完整Race/Xeno目标不回落写服务profile。`ActionTuningRecord`每字段独立has/value，ExposeData／clone／清理都保留缺失与false差别。生产Settings完整原子保存／重开与序列化失败恢复使用既有事务；队列保存失败保脏态／重试。Basics清服务profile三项，准确动作行／层Reset清对应玩家字段，保作者来源和last-good快照。公开程序入口仍为`UniversalSqueakerMod.TryChangeBehaviorOverride`，`BehaviorValues.SelectRequirePlayerControlled/SelectRequireAwake/SelectRequireNotDowned`各为bool?。

### 每包当前支持什么，必须在实际调用中成立

- Wounded是受损Pawn的PostApplyDamage正常返回，0不自动变正伤；当前伤口另读。Death是Kill进入前，不是死亡完成或任意Destroy。Equip是tracker正常加入且实际成员关系，装备者与物品不换位；PlayerCoreEquip另为显式Dev回应资格候选。
- Attack显式Dev支持近战成对scope＋实际挥击marker、投射物、已核束流／喷射、点火和明确轰击启动；false近战须有marker才算，早退不算，执行不保证命中。工具／跳跃／支援部署／任意生成等排除／待扩展逐机制在导读列出，正式覆盖pending。整体Dev不激活就不采用该支持集为普通产品行为。
- Call取正向IsIdle＋Wait/Wait_Wander＋自身未动，不作兜底；Sleep用driver.asleep；Move用自己的pather.Moving。Social有明确kind／准确Def过程支持集，Joy取明确joyKind，两者可同真交同owner；不按名称片段或非工作猜测。Social Goto Def的当前Core内容缺口在事实页明示。
- Work七对Def/driver及当前同Pawn／Job／CurToil是明确开发过程覆盖，包括准备Toils，不凭workGiver标签或名字称所有工作。这收窄了已有Dev覆盖，US-C3实质未完，不能只列为听感／人审待验或将七项当已批准全集；常用缺项范围由PM带用户决定，再按获准范围补工程，本次Equip入口修复不扩Work。命令来源是另一个回应scope。实际Comp已解析scope也进入同一捕获配置视图，避免把资格留在视图之外。继承子完整键消费真实当前命令，而非只识别内置Work尾名。
- Draft／Undraft只认原玩家gizmo正常执行后的真实状态方向；MentalBreak用基类及三个已核直达override的正向成功来源，Humanity包装只观察base一次，不把任意MentalState当崩溃。扩围是显式Dev候选，逐目标readiness和具名诊断，不吞全局PatchAll异常。

[OfficialFifteen.xml](examples/0.6/OfficialFifteen.xml)给每包完整ActionId和直接Core Click音频映射，不选行为来源、不激活Dev、不给发行总序／频率。真实语音包替换自己的SoundDef；US不复制SR音频。所选合同显式携带主体角色；喂食与Sleep重叠时，Sleep仍Sleeper、Eat才ActualIngester，直接AudioId复用不抹条目／包／供音域。

### 退役与证据边界

官方Crying/Giggling、专属MentalFit通知／patch／日志事件／开关和无消费者残余已经删除；不保旧数值enum槽，不改原版婴儿声音／状态，不禁这些Pawn的其他合同。独立作者仍可注册自己的正向合同，甚至使用独立命名空间的同名尾部；没有全局类型／名字黑名单。冻结17字符串历史语料只服务旧路由不变性，活跃官方枚举／目录／metadata及覆盖分母均15。

指定安装程序集已核到1.6.4871 rev591；公开正文只给身份、符号和结论，反编译原文不分发。4871定点源码、目标引用编译、生产纯源、真实Harmony＋平台double、Scribe替身往返、原生游戏／听感各自成立，不能互相替代。当前全部15包库存、现有适配纵切与人审入口已具备；Work仍有上述US-C3实质工程缺口。合同人审／成果人验仍逐包pending，普通政策／覆盖范围和首次来源选择由PM用户处理。本节D-C输入仍为固定0.7；后续实际0.8消费者迁移见§24。

## 24 固定FL0.8技术消费，不预定下一版页面

当前依赖为FL源码564f78b1e88f5450aff075e5bd3f07db2a894c1f的固定0.8 Dev，范围[0.8.0,0.9.0)、Private=false、单carrier。生产Host/三个Manifest、主窗口/Overlay/确认、公开Palette颜色、显式Live编辑和实际绑定/请求/生命周期已经迁移，完整用法与检查入口见[消费者说明](fl08-consumer-migration-zh.md)。原24色值和旧有效边框/警告处理保为30明确语义角色；活跃主题只写颜色，不撤销页面度量。普通getters与业务settings保存的所有权/原条件不变。

这是现有消费者技术迁移，不改变15包事实、已裁Select三资格/Eat三轴、Dev政策门或声音决策；不固化Dashboard/页面数/Help新方案。后续UI/UX定稿与实现另行安排。原生1.6/真实Config/Scribe/字体/音频/听感及逐包两个人工关口仍待验；固定FL未交付1.7 adapter。CI获取尚指旧0.7，须维护者另排真实0.8发布载体，不能静默回退。
