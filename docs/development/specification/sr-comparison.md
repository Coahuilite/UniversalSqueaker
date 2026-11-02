# SR固定对照与玩家体验差异
开发比较材料，非US默认权威。固定SR0.3.4／v0.3.4-tagfix1 d06b607c7b1720bb3c32a28db3e57e989a61d434；复核b28f414b5b4cb40e772f29c6d964dec4eb56689e运行源码相同，仅文档不同。复用原已接受源码核对，不重读SR或复制资源。

SR旧方法：CompSqueaker.IdentityGateAllows／TryTrigger／CurrentMood、Kernel/Modulation.ComposeModulation、Selector／Verb patches及Ratkin_AddSqueakComp。旧数值不是US已实现频率；US现行资格、间隔与时钟只由ACT／POL／RESP定义。

| 包 | SR实际机制／旧节奏 | US保留或明确改变 |
| --- | --- | --- |
| Select | 原入参Pawn／playSound，隐藏玩家可控／清醒／未倒地；18实时tick | 实际接受反馈最终主体；三资格显式独立，root单调秒；不额外重复去重 |
| Eat | WholeJob；父子精确开关与名称未知拓宽；144game tick | Whole／Execution及营养／药物三轴独立，精确Unknown不拓宽；被喂者、结果消费者分开 |
| Attack | 按无参bool TryCastShot／名字排Ability／true，216 | 明确执行根；补近战挥击false与实际AbilityShoot，排工具／跳跃／支援生成 |
| Wounded | 同PostApplyDamage postfix，不要求正值；216 | 保处理返回原义与受损者，四结果及事件A不等旧尝试收费 |
| Death | Kill prefix，0且ignoreGlobal | 保临终阶段；免CHECK仍WRITE，I0不免模拟槽／器官 |
| Move | 自己pather；504、p.012 | 保自己移动，不把搬运位移当真 |
| Equip | Core玩家Equip Job、正常加入；216 | 固定成员／wearer证据与显式PlayerCoreEquip资格分开；服装／卸下不扩大 |
| Call | 排完其他状态后兜底；864、p.012 | 收紧为正向IsIdle＋Wait／Wait_Wander，不给未知Job兜底 |
| Sleep | LayingInBed且rest need；1080、p.008 | 当前driver.asleep，清醒卧床不算 |
| Social | Chat／Social／Visit／Lovin／Entertain片段；504、p.016 | 准确kind／Def过程，与Joy同真；Goto当前Core Def缺口明示 |
| Joy | joyKind非空；504、p.016 | 保正向过程，不宣称实际Joy gain |
| Work | workGiverDef，出厂ActiveCommand；720、p.012 | 完整Core准确过程；声明ActiveCommand与普通AnyOccurrence独立 |
| Draft | 准确gizmo变更，36实时tick，ignoreGlobal | 保接受方向，单root／真实秒，与Undraft身份分开 |
| Undraft | 同gizmo反向，36实时tick，ignoreGlobal | 独立动作秒截止和条目，尾音可重叠 |
| MentalBreak | 只基类成功，0、ignoreGlobal | 补三个直达成功override，Humanity一次；任意MentalState不替代 |

SR ignoreGlobal动作仍更新共享钟，许多无声／失败尝试也写旧时钟；US四结果、独立W／H／I和双时钟机会不可从旧XML数值推等频。SR CurrentMood用任意InMentalState分类Break、心情显式值覆盖年龄；US年龄×心情独立乘算，不复制该误分类。SR没有US自有Sustained单槽；共享SubSoundDef距离写入不移植。对比须固定实际素材／年龄／彩蛋／局部调制／距离／配置，频率由真实轨迹支持。原始逐包导读保在[归档](../../archive/specs-before-20261029/us-kernel-0.6-contract-review-zh.md)，其中过时pending不再是当前政策。

