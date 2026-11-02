# 调制、四回执与自有音频生命周期
状态：现行，US0.6.x。责任：响应与原生输出维护者。间隔结算见RESP；参数权限见CFG。

## Purpose
把已选声音转为一次局部请求，由原生后端执行并如实返回结果；实时总控只作用于有证据属于US的实例，持续槽不成为第二状态机。

## Requirements
### Requirement: OUT-01 中性、启用与参与分开
无配置 SHALL为校准／局部音高音量1、jitter[1,1]，年龄／心情不启用、阈值不暗补。参与按包→本包动作→entry→玩家覆盖逐字段解析一次，省略表示参与已启用适用响应，不隐式启用。作者来源不得冒充玩家participation。
#### Scenario: 条目Skip年龄
- **WHEN** 年龄响应已启用而所选entry跳过年龄
- **THEN** 年龄贡献为1但其他响应仍可参与，system G不可被Skip绕过

### Requirement: OUT-02 固定独立乘算
局部pitch SHALL为来源校准×各启用参与贡献×一次jitter，局部音量L为校准×音量贡献；继承先得一个值，不重复乘层。年龄先心情后、按稳定规则ID归并，每规则读同一本次原始上下文，不读前项累计值，不开放任意排序图。新增响应与事实同一IUsDefinitionProvider.Collect批次经AddResponse登记；IUsResponseRule提供稳定Key、固定ResponseStage.Age／Mood与Evaluate(ResponseContext)，返回独立ResponseContribution，不接触前项累计结果。
#### Scenario: 年龄原生值与显式覆盖
- **WHEN** 只覆盖age.override而未覆盖age.overrideVolume
- **THEN** 对应轴替换原生voxPitch，不双乘；音量轴仍取真实voxVolume

### Requirement: OUT-03 参数合法性与异常
音量 SHALL允许有限0；pitch／jitter有限正且min≤max，合成及L×G可表示为原生float。mood.low／high启用时必填且0≤low<high≤1，等阈值属neutral；各区pitch／volume缺省1。必需实现异常／非法贡献在副作用前拒绝，不当1放行。
#### Scenario: 抖动范围非法
- **WHEN** min>max或非有限
- **THEN** 抽样前具名拒绝，不再调用后端或消耗补偿随机

### Requirement: OUT-04 四个结果
后端 SHALL只返回Dispatched、AlreadyActive、Rejected、Indeterminate。正常OneShot调用／有效新Sustainer建立为Dispatched，不等于听见；已活跃无新派发；确认未派发为Rejected；进入可能副作用但无法确认则Indeterminate。异常是原因而非第五结果。
#### Scenario: 启动辅助音可能已发生但无句柄
- **WHEN** 原生启动未能提供有效句柄且副作用无法排除
- **THEN** 返回Indeterminate，不能以“没句柄”断言零音频；交原owner结算

### Requirement: OUT-05 自有实例实时总控
US SHALL仅对确证US归属的Sample应用L×当前G及当前全局距离端点；不写／暂改再还原／克隆共享SoundDef或SubSoundDef，不接管他方音频。局部参数固定，后续更新不重抽／重跑合同；原版保位置、游戏音量、映射与淡出。
#### Scenario: G从1到0再到0.5
- **WHEN** 自有实例仍存活且正常更新
- **THEN** 可从静音恢复，无除旧值，无重复乘旧结果；已结束声音不补播

### Requirement: OUT-06 空间参数
距离 SHALL沿现有全局游戏空间定义，不称现实米、不另乘纯相机高度曲线。Balanced默认15–50，Conservative15–65，Strong15–40，Custom保玩家端点；即时指下次正常音频更新，不保证零缓冲。
#### Scenario: 共享声音被非US同时播放
- **WHEN** 玩家改变US距离或音量
- **THEN** 只更新US归属实例，非US及共享定义不变

### Requirement: OUT-07 单持续槽与替换
同主体受控Sustainer SHALL仅一个槽。同键有效则复用，不抽样／重启／刷新冷却；异键仅严格更高优先可替换，先准备再End旧音再新起。旧End与新启动分记，失败不恢复或补播，OneShot不借优先裁尾或自动End槽。
#### Scenario: End旧音失败而尚未进入新启动
- **WHEN** 已解除槽持有而旧End抛错
- **THEN** 记EndFault，新请求Rejected、不写新派发保护；不重复非幂等End

### Requirement: OUT-08 当前依据与资源退出
活跃槽 SHALL独立每60game ticks复核当前事实／授权，不受A缩放；失效或Unknown结束，自身概率未中／未中选／冷却不是依据失效。无实例无维护；离图／销毁／服务关闭释放自有资源、逐项继续清理，不停未知他方对象。
#### Scenario: 表示瞬时事件的Sustained
- **WHEN** 后续维护没有该瞬时事件依据
- **THEN** 正常结束；不能把接受选择自动延长为“持续被选择”

### Requirement: OUT-09 首帧、辅助音与池复用
归属 SHALL从真实请求token、Sample／父Sustainer关系传播；首个Sample可早于构造返回。辅助音只在真实调用点标记，不认领嵌套非US；End后晚到grain拒绝，旧淡出Sample继续消费当前总控至Cleanup，池复用清归属。
#### Scenario: 持续接缝缺失
- **WHEN** 仅Sustainer／辅助接缝不可用而Sample共同接缝可用
- **THEN** 隔离真正持续输出，不连带禁物理OneShot；共同Sample接缝缺失才影响两者

