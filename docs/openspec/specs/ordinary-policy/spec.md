# 普通玩家默认与显式Dev边界
状态：现行，US0.6.x。责任：US产品政策维护者。机制、事实与本页值各有唯一归属；源码实现状态见[映射](../../../development/specification/implementation.md)。

## Purpose
给普通Release一套已采用的可解释节奏与资格，不把开发高频输入或SR旧XML数值当成发行默认。作者与玩家仍按配置合同覆盖合法字段。

## Requirements
### Requirement: POL-01 普通自动安装
普通Release SHALL在主线程定义／最终音源／comp准入／配置就绪后自动安装正式策略。合法已选audio-only包不依赖DevMode、US_DEV、行为来源选择、重复启包或打开诊断；资源／资格／模式／玩家禁用等正常门保留。
#### Scenario: 不打开设置进入已加载地图
- **WHEN** 普通启动已就绪且包在准确域被选择
- **THEN** 实际事件或到期机会可沿正常链响应；OrdinaryActive仅说明策略已安装，不证明有音或已听见

### Requirement: POL-02 唯一普通数值与单位
普通缺省 SHALL按下表；官方动作自己的间隔单位不可互换。状态W基数600game ticks、R60game ticks、共享H216game ticks。世界输入ready后，首次新owner由Kernel用稳定混合归一化位置在InitialSpread=600游戏tick内向后对齐；600不乘目标倍率，也不是成功或重试额外窗口。无ready输入时保留旧编号初次分散与无seed合法调用。作者／玩家显式值后折叠，不由Dev或其他动作默认覆盖。

| 完整ActionId（前缀us.core/） | 动作间隔 | CHECK共享H | scope／额外普通资格 |
| --- | --- | --- | --- |
| Eat | 144游戏tick | 是 | AnyOccurrence |
| Call | 900游戏tick | 是 | AnyOccurrence |
| Move | 504游戏tick | 是 | AnyOccurrence |
| Sleep | 1200游戏tick | 是 | AnyOccurrence |
| Social | 504游戏tick | 是 | AnyOccurrence |
| Joy | 504游戏tick | 是 | AnyOccurrence |
| Work | 720游戏tick | 是 | AnyOccurrence |
| Wounded | 216游戏tick | 是 | AnyOccurrence |
| Attack | 216游戏tick | 是 | AnyOccurrence |
| Equip | 216游戏tick | 是 | ActiveCommand＋PlayerCoreEquip |
| Select | 0.3真实秒 | 否 | AnyOccurrence；三项资格见ACT-Select |
| Draft | 0.6真实秒 | 否 | ActiveCommand |
| Undraft | 0.6真实秒 | 否 | ActiveCommand |
| Death | 0游戏tick | 否 | AnyOccurrence |
| MentalBreak | 0游戏tick | 否 | AnyOccurrence |

全部官方动作WRITE共享H，包括免CHECK的五项；默认OneShot。总序为Death>Wounded>MentalBreak>Attack>Draft>Undraft>Equip>Select>Sleep>Eat>Social>Joy>Move>Work>Call。Select无隐藏器官／Talking门；Death只免Talking仍查器官；其余保器官／Talking。状态chance=1、所有事件p_base=1，无额外概率豁免。
#### Scenario: 死亡动作间隔为零
- **WHEN** Death自己的I已满足
- **THEN** 仍须真实事实、器官、模拟槽、音源和输出资格；I0不等无条件必响

### Requirement: POL-03 独立活跃度与软节奏
A SHALL为有限正数，1中性，不夹断大值，0不是禁用。状态W只在一个计算点取ceil(600×max(1,TickRateMultiplier)×max(1,共享可听人数/10)/A)，最少1tick并按long截止饱和；人数复用地图共享快照，原W倍率补偿只乘一次。事件p=min(1,p_base×A)。A不缩放硬截止／资格／音量、不造事件、不重抽已排W。

普通Release SHALL实际安装`us.state-phase/1`政策。adapter另供独立中立目标倍率，令m=max(1,目标倍率)，不能从混合人口的StateWaitFactor反推m。成功窗口为`N_success=max(1,floor(min(120×m,W/5)))`，重试窗口为`N_retry=max(1,floor(min(15×m,R/2)))`；W为本次已经合成并除A后的有效等待，R仍是基础60游戏tick。W/5、R/2以非负整数预算计算，倍率上限先floor并饱和，再取最小值；非有限倍率不扩大窗口。N=1时不额外延后。成功和重试的额外错峰最多各N−1游戏tick，不把0.25秒改作基础R周期，不增加第二次W速度乘法。

新排程 SHALL采当时的速度、W及A，并按RESP-07保留窗口。换速、改A／配置或Dev选择不会立即重排旧期限；H真抬期只沿旧机会窗口向后对齐。初次600保持独立，初次H抬期沿已采样的短重试额外预算；I、H、R仍按原游戏tick，交互仍按单调真实秒。目标倍率不是实测TPS，故近似真实秒分散不构成机器实际时长或原生听感保证。
#### Scenario: 改活跃度而状态已经排好
- **WHEN** A改变
- **THEN** 已排等待和硬保护不变，下一排程／事件才用新A；输出区域／全局恢复A为1

### Requirement: POL-04 Dev是显式进程输入
Dev策略 SHALL保DevMode要求和进程一次不同策略选择；同引用幂等，可对已有owner原位换后续输入，不清保护、占额、W或重新错峰。高频W20/H40/I120不成为普通默认。未选明确Equip资格仍Unselected，不暗放开。
#### Scenario: 普通owner存在后选择Dev
- **WHEN** 合法显式Dev选择作用于已有owner
- **THEN** owner身份与原期限保持，活动Sustained于原维护点复核，OneShot自然尾；地图释放不卸载进程输入

