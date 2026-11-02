# 证据层、稳定验收ID与使用限度
本页维护现行要求到既有证据的连接，不是新增测试计划。规范Scenario是prose，不是本次新增执行用例。规格重建本身没有运行产品；2026-10-29修复已运行现有受影响检查与本地编译，当前层次／盲区见[实现状态](open-issues.md)。没有运行游戏或真实Config。

| 既有层 | 实际能支持 | 不足以支持 |
| --- | --- | --- |
| 完整生产Core独立编译／固定输入 | 中立依赖、真实决策／配置／调制及输出收据转移 | 原生类型／真实声音 |
| 冻结Kernel corpus／golden | 原路由／随机消费不变，历史17字符串仍历史输入 | 当前十五库存、全新机会合同或每包原生 |
| OneShot／AudioBinding／DcNative等生产源＋Harmony和平台double | 实际caller／角色／session／请求、缺目标变体与明确反例 | 目标游戏挂接、原版方法实际运行／听感 |
| SettingsMigration／ConfigCopy／Reset | 生产settings／ExposeData／原子文件与Scribe替身往返／失败 | 原生Scribe、真实玩家Config |
| 实际DLL的Host／manifest／public Execution | 真实绑定、控件绘制／分配、取消／草稿／帮助状态 | 原生输入、真实字体／窗口栈／IME |
| 定点4871方法体／Def与目标引用 | 来源身份／阶段及API形状 | 加载／运行／听感 |
| 固定载荷内部试玩／性能接受 | 原候选、原环境、原实际观察 | 另候选的逐包／逐方法完整接受 |

## 稳定合同ID到当前唯一正文
| 旧ID | 当前归属 | 保留内容 |
| --- | --- | --- |
| C00 | CORE-01、下表各层 | 完整中立边界与证据不可互代 |
| C01 | CORE-02、AUDIO-01／02、GAME-02 | 准确域／资源／合法父路径 |
| C02 | CORE-03、ACT-Call、GAME-01 | 正向三态／Unknown／缺实现 |
| C03 | RESP-01／07 | 单状态机会、失败未来推进 |
| C04 | RESP-03至06、OUT-04、DIAG-03 | 预留／四结果／不明与诊断隔离 |
| C05 | CORE-03至06、ACT各项 | 完整ID、声明、独立根与不可变条件 |
| C06 | AUDIO-01、GAME-08 | 最终资源来源与局部隔离 |
| C07 | AUDIO-02至04 | 模式／权重／年龄／彩蛋 |
| C08 | CFG-01至03 | 显式单来源、权限／继承 |
| C09 | CFG-04至07、PUB | 缺源／坏快照／选定域恢复／保存 |
| C10 | OUT-01至03 | 中性／参与／合成／合法值 |
| C11 | OUT-05／06／09 | 自有实例／在播总控／首帧与清理 |
| C12 | OUT-07／08、RESP-08 | 单槽／替换／当前授权与生命周期 |

## V01–V15保留的验收目的
V01准确域→CORE／AUDIO／GAME；V02正向动作→CORE／ACT；V03单机会→RESP；V04事件与切状态→RESP／GAME；V05四结果→RESP／OUT；V06竞争与重叠→RESP／OUT；V07作者接入→CORE／作者例；V08资源来源→GAME-08；V09局部故障→CORE-04／GAME；V10路由抽样→AUDIO；V11默认权限→CFG／POL；V12缺源恢复→CFG／PUB；V13调制→OUT-01至03；V14实例输出→OUT-05／06／09；V15生命周期保存→RESP-08／OUT-08／CFG／PUB。

旧T01–T12及T06延迟变体是固定历史模拟fixture，不是普通W/H/I默认。原17项、未声明字符串通知、旧tick交互与自动唯一来源叙述均按后续决定替代，不能重启为当前要求。原详细记录保在[验收历史](../../archive/specs-before-20261029/us-kernel-0.6-acceptance-zh.md)与[夹具迁移历史](../../archive/specs-before-20261029/us-kernel-0.6-test-migration-zh.md)。

## 十五包逐项记录与总体接受
2026-10-28内部试玩对US ea610fa／FL833c14f4给出work fine并获准进入US rc1／FL rc2准备；响应合同审查WO r6明确记录该RC级总体接受。它没有附逐包案例、分辨率／输入法或十五动作明细。下表“未逐项记录”仅表示没有该粒度记录，不表示通过、失败或等待另一轮完整人工矩阵；不撤销总体接受，不增加本次文档／RC准备前置条件，不要求重跑已接受性能。具体未记录范围继续如实保留；未来实际受改行为仍按其具体影响验证。阶段说明可参见[RC范围与升级说明](../../releases/v0.6.0-rc1.md)。

| 包 | 合同审阅记录粒度 | 成果／原生听感记录粒度 | 原有技术覆盖定位 |
| --- | --- | --- | --- |
| Select | 未逐项记录 | 未逐项记录 | DaSelectCaller／MissingTarget；DcSelectSettings／Config；真实root与秒模型相关既有入口 |
| Wounded | 未逐项记录 | 未逐项记录 | DcNative受损返回、0／正／throw、真缺目标 |
| Death | 未逐项记录 | 未逐项记录 | DcNative Kill体前请求与退出 |
| Attack | 未逐项记录 | 未逐项记录 | DcNative近战marker／早退、支持根、包装单通知 |
| Move | 未逐项记录 | 未逐项记录 | DcNative自身移动／被动位移及持续失效 |
| Equip | 未逐项记录 | 未逐项记录 | 真实GLOBAL菜单→policy→EquipmentAdded；准确成员／wearer |
| Call | 未逐项记录 | 未逐项记录 | 正向Wait／Wait_Wander／Wait_Combat与未知Job |
| Eat | 未逐项记录 | 未逐项记录 | DbEat127、DbResultRouting68、缺Chew及真实loader |
| Sleep | 未逐项记录 | 未逐项记录 | asleep／清醒／被喂时角色 |
| Social | 未逐项记录 | 未逐项记录 | 准确Def／kind、与Joy同真；Goto内容缺口 |
| Joy | 未逐项记录 | 未逐项记录 | Social重叠／非Social kind的真实请求 |
| Work | 未逐项记录 | 未逐项记录 | --work-core、MissingSow、--work-care-lifetime，原七组与新增71组各按原身份 |
| Draft | 未逐项记录 | 未逐项记录 | gizmo方向／重复包装／setter与无变化 |
| Undraft | 未逐项记录 | 未逐项记录 | 反向身份／条目／角色，独立于Draft人验 |
| MentalBreak | 未逐项记录 | 未逐项记录 | 四成功来源、Humanity一次、false／throw |

不从总体接受推造上表逐项PASS，也不从逐项未记录推造FAIL或新增整轮重验要求。159f651 P1技术关闭保留精确旧快照／同ID重绑／另一拒绝域及原生Config点击的原缺口。F02真实提交→selector、F03真实collector→独立域及精确throwing sink组合不由旧绿外推。2026-10-29源码已接通这些生产边，既有检查只按实际编译／调用层使用；未新增case／probe。旧运行命令与日志保原交付身份。


## 2026-10-29修复技术配对

US3e42+dirty实际消费FLcf1cf64+dirty固定Release producer-release-r1：Dev／Release编译、完整当前Host1747项、DLL-only作者编译及标准stage通过；分发目录带同一FL，不能由ProductVersion只含HEAD推断clean。旧DefinitionDirectory独立夹具在HEAD已有九处0.7 API／私有接缝形状，编译失败、运行未执行，CO保留该事实而不顺带迁移。完整verify-local入口因原gate6要求clean FL源码而未执行，各整入口ID保未运行；已有独立检查和本轮明确消费分别使用，不称完整入口PASS。F01精确sink异常、F02连续提交→selector及F03原生独立域组合仍按实现状态保源码层与实际缺口。
