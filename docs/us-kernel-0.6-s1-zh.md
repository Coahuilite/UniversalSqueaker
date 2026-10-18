# 0.6.0 Dev：OneShot S1

本开发分支实现受控的准确Race域 Work＋Wounded OneShot链与首版作者装配。普通玩家的旧日常路径仍使用已有策略；未冻结D3频率映射前不会自动切换。不是完整0.6首版或发布候选。

## 运行边界

- 纯生产源：`Kernel/OneShot/`和`Authoring/`；独立OneShotTests编译这些源及原选音Kernel，无游戏引用或同名stub。
- 唯一新决策状态持有机会、动作/共用截止、同tick名额和确认记录。选音复用原`SqueakPoolRegistry`，本切片只装配准确Race的一层新格式供给；既有四模式/年龄/Remix路径保持，完整新资源来源/域扩展仍属S2。
- 受控session通过CompTick／真实Wounded通知进入生产核心；后端使用已绑定SoundDef和原可用性/位置预检，然后原生PlayOneShot。正常void返回表示已调用，不承诺听见；异常按副作用阶段区分。
- R=60游戏tick是批准的明确未派发状态重检默认。W、共用间隔必须显式提供；W20/H40仅Dev菜单中明确标记的fixture，不是玩家缺省。受控入口的动作硬间隔也必须显式提供；Dev fixture两动作120，不是玩家缺省。后续完整配置迁移另按S3/S4实施。
- 受控session独占该Pawn的S1动作集，不并行执行旧冷却持有者。关闭受控session后回原路径。已有持续句柄活跃时拒绝启用；不改变Sustained启动/替换政策。销毁/离图与新游戏/读档清理受控状态，不写存档。

## 作者从实际例子开始

[AudioOnly.xml](examples/0.6/AudioOnly.xml)只需普通声音包，不需要C#提供者。根为真实`UsVoicePackDef : Verse.Def`，`declaration`字段的`LoadDataFromXmlCustom`由原版Def加载器调用；US没有另建XML/程序集加载器。`declaration/formatVersion`省略永久表示1，显式1同义；未知版本拒绝。样例仅在docs，不默认安装为内容。

[Extensions.xml](examples/0.6/Extensions.xml)展示显式`UsProviderDef`→`IUsDefinitionProvider`批次、`UsActionDef`条件组合和音频引用。示范提供者`ControlledExampleProvider`总是返回True，仅用于受控装配验证，不冒充游戏值班事实。真实扩展实现`IUsFactSource.Capture(subject)`并自行在适配中查对象，核心接口没有Pawn/Def类型。提供者在临时批次完整收集、重复检查后发布；失败不发布半批。注册时只读取一次事实Key，之后采样沿用该注册身份。S1组合的baseAction只接受内置us.core/Work、us.core/Wounded、us.core/Call；不接受组合动作作为base，与Def枚举顺序无关。

字段：精确Race、稳定entry key、动作ID、SoundDef名及可选weight/volume/enabled。省略与显式false/0通过存在位区分；volume=0合法，weight必须有限正数。未知/重复字段、重复身份和缺必需条件不会通过删条件被放行。condition必须恰有一个根表达式；ref是无属性文本叶，all/any/not只能含合法表达式，not恰有一项；entries只能含li。音源归属取实际SoundDef提供者，不接受作者owner字符串；完整重名/patch前来源取证仍待S2，不能称首版资源系统已闭环。

## 手动目标1.6观察（待验收）

由维护者在隔离Dev模组集合准备样例与相应Ratkin资源，正常启动1.6后检查作者装配诊断。启用开发模式，Debug菜单选择`S1 controlled fixture: enable W20 H40 actions120`并点选已有CompSqueaker的目标Pawn；让其执行真实主动工作、再触发受伤，观察`NativeCallReturned`记录、优先/硬保护与不立即补播。菜单disable退出受控路径。不要把没听见当成Rejected，也不要用受控fixture替换日常默认。

程序入口为`UsOneShotRuntime.EnableControlled(pawn, waitTicks, sharedTicks, workTicks, woundedTicks)`，有显式Dev模式门；旧`NotifyExternalByKey`及其scope/许可/Realtime公式仍在原默认路径，无新注册前提。受控S1只处理已声明的Work/Wounded/条件组合，不将所有旧17动作声称已迁完。

本分支自动测试会从磁盘读取上述两个真实示例，执行同一生产字段加载器、实例化示例C#提供者、装配目录、求组合合同并进入生产决策/派发驱动。它不是仅检查XML语法；但原版完整加载、真实声音/资源碰撞、US/非US实例输出、听感仍需目标游戏证据，当前未运行。

## 同步测试与兼容

`tools/UniversalSqueakerOneShotTests`接verify-local追加gate17：T01–T12适用路径、T06延迟变体、D1真值表、缺失事实键/提供者异常原因快照（不重新采样）、四结果及诊断故障、准确域、作者批次/格式/缺失处理。旧Kernel corpus继续普通重放，不全量重录。原配置/Realtime等既有characterization继续执行；纯测试、平台fixture与实机证据分开记录。

完整构建目前显式选择已固定的FL0.7 Dev；范围仍`[0.7.0,0.8.0)`，不表示FL0.8已消费。所有bin/obj/dist与Dev载荷均来自本开发工作副本，旧checkpoint及游戏目录不写入。
