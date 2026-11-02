# 升级到0.6单一机会格式

先由玩家备份自己的配置；本页解释已定升级方式，不自动访问／改写真实Config。规范分别见[机会](openspec/specs/response-opportunities/spec.md)、[格式和选定域恢复](openspec/specs/behavior-configuration/spec.md)、[提交结果](openspec/specs/configuration-publication/spec.md)。

## 来源、玩家行与恢复

新来源属性为`simulationIntervalTicks`（非负整数）和`interactionIntervalSeconds`（有限非负实数）。C#对应`BehaviorValues.SimulationIntervalTicks`与`InteractionIntervalSeconds`；自定义动作声明的`intervalTicks`是模拟缺省，新增`interactionIntervalSeconds`是交互缺省。省略不贡献，0是明确关闭该类动作间隔，作者来源后按玩家profile／Race／准确Xeno逐字段折叠；Reset只清准确层／行的贡献或回到其模板。

```xml
<action key="example/Mixed" simulationIntervalTicks="120" interactionIntervalSeconds="0.5" />
```

上述只适用于其真实固定条件允许的机会。官方交互行只接受秒参数；其他十二项只接受模拟tick参数，给错单位时准确拒绝，不将数字重解释。原`hardIntervalTicks`来源属性及玩家`hasHardIntervalTicks`记录被拒绝并指明合同／来源；原数据保留供显式恢复／替换，不自动转换18或36、不清整份配置。仅保留拒绝信息不是保留旧执行路径。未改语义的音频、包选择、外观等不需要重建。

升级前由玩家自行备份配置。旧玩家动作行用对应行恢复／替换，再按新单位填写；不要覆写自己的旧备份。对于结构合法但含旧计时属性的**已绑定作者快照**，界面仍能读取来源身份，拒绝状态保留来源ID、动作ID和原因：

1. 在“行为与来源”选择准确Race／Xenotype域，核对来源状态里的被拒对象；显示“已拒绝的来源”不等于已经恢复继承。
2. 要遗忘该域的旧快照，打开来源选择器并明确选择“继承”。要保留来源身份，先使用已经更新声明的来源，再从选择器重新选择其真实ID；即使ID与原绑定相同，也会触发一次明确重绑。
3. 该域的候选沿现有原子保存owner先提交，再采用／发布。保存失败时原XML／内存状态保留；其他域的快照（含仍被拒的旧快照）、音频选择和外观不会因这次恢复被清掉。Committed后发布失败只重试发布，不重复磁盘事务。

这条恢复只识别格式1快照中的已知hardIntervalTicks旧属性。解析会验证其余结构，旧属性及所属动作不进入有效执行；保留的原快照片段只用于拒绝／后续明确替换，启动刷新不会替玩家恢复。一般畸形XML仍拒绝，页面显示具体错误而不崩溃；不提供任意XML修复或自动迁移。旧版本不识别新字段，回退须用用户保留的对应旧配置。真实Config操作仍由用户负责，本实现不会访问或替用户删除旧文件。

动作编辑区对官方行只显示当前适用单位；自定义混合合同的机会选择器只是编辑视图，不改合同或运行时钟。数值旁显示相同生产fold的有效值／来源，空白继承与显式0分开，两个机会类别可分别恢复。普通编辑立即发布、0.35秒合并保存，原子行／来源提交保留persist-first和Committed后发布失败含义。

