# 定义、身份与独立作者合同
状态：现行，US0.6.x／首次公开基线。责任：核心定义维护者。源码与来源见[开发映射](../../../development/specification/implementation.md)、[覆盖表](../../../development/specification/coverage.md)。

## Purpose
使合同作者能在同一中立核心声明独立根、合法继承和正向事实，而不依赖Pawn宿主、声音名称或某次UI调用。能力组合不新增运行时总管。

## Requirements
### Requirement: CORE-01 平台中立与独立输入
整个Core生产集合 SHALL不依赖Verse、Unity、RimWorld、Harmony、平台替身、原生对象或可变进程全局状态。定义、配置、音频 SHALL分别按自己的寿命发布；一次RuntimeView只捕获三个引用，不成为第四份全量权威。静态池及已发布输入共享，不按Pawn复制；廉价到期比较不得变成逐tick重扫全部合同／声音包。
#### Scenario: 只改变包选择
- **WHEN** 已加载资源未变而玩家改变选包
- **THEN** 配置过滤更新，定义与AudioCatalog不因此重建
- **AND** 后续合法机会读取当时三份已发布引用

### Requirement: CORE-02 稳定主体与准确身份
运行状态 SHALL按稳定服务实例拥有，不因Race、Xenotype或标签变化另开冷却。Pawn域为准确Race及合法(Race,Xenotype)；NonPawn为Family／Namespace／Target，不借Race字段。配置域、实例、定义、条目、资源与空间定位 MUST分开。
#### Scenario: 同音频被不同条目引用
- **WHEN** 两个域或两个包引用同一AudioId
- **THEN** 仍保各自准确域、完整ActionId、包与条目身份；不混池、配置或运行owner

### Requirement: CORE-03 固定组合与不可变定义
完整命名空间ActionId SHALL贯穿声明、配置、事实、决策、映射、请求与诊断。独立根、多级继承和All／Any／Not组合均合法；继承只增加固定条件，不暗继承优先、音源、配置身份、参数或形态。发布后的定义及子树 MUST防集合别名改写。
#### Scenario: 未知条件参与组合
- **WHEN** All(true,Unknown)、All(false,Unknown)、Any(true,Unknown)、Any(false,Unknown)、Not(Unknown)求值
- **THEN** 依次为Unknown、False、True、Unknown、Unknown；仅True进入候选
- **AND** Unknown不转Call，不删条件放行

### Requirement: CORE-04 声明与依赖隔离
一个显式IUsDefinitionProvider批次 SHALL完整收集后解析引用；重复身份、环、缺父、缺必需实现或批次异常只隔离实际依赖，不发布半批或依赖加载顺序。UsActionDef.declaration必填命名空间key与一个condition根，baseAction省略为独立根，可引用同批或已注册合法父。在当前0.6声明格式中，formatVersion省略按1解析，不支持的格式明确拒绝。
#### Scenario: 独立根引用缺失事实
- **WHEN** 一项事实实现缺失而其他提供者批次完整
- **THEN** 该依赖明确不可用，其他合法定义继续；不以倍率1或False伪造实现

### Requirement: CORE-05 显式通知与公共结果
通知 MUST先由XML或一次C#声明完整合同，再用实际主体TryProcess；未知字符串、重复或冒用官方原生事实拒绝。true只表示同步处理输入，不能推为派发或听见。公开独立作者／NonPawn纯模型不得被强加官方Pawn资格或打开UI／Dev的前提；不能将allowExternalActions旧总闸自动加给所有已注册扩展。
#### Scenario: 声明的工作完成通知遇冷却
- **WHEN** 实际worker按已声明合同通知，输入可进owner但硬门未到
- **THEN** 可返回processed=true而没有输出；实际结果从响应记录／四回执读取
- **AND** 该自定义通知仍是模拟事件，不伪装玩家交互

### Requirement: CORE-06 明确机会与参数单位
自定义intervalTicks SHALL只定义模拟缺省，interactionIntervalSeconds只定义交互缺省，缺省分别0；chance默认1、priority默认0、checkShared／writeShared默认true、playback默认OneShot、requiresVocal／talkingEnabled默认false。参数按本次机会选单位，不从父动作或尾名推断。
#### Scenario: 自定义混合消费者
- **WHEN** 独立根any(SelectEvent,WorkState)分别声明两类间隔
- **THEN** 选择事件用秒，工作状态用tick；不把两道动作门叠加，仍求完整固定条件

### Requirement: CORE-07 性能改进保业务合同与生命周期
业务Kernel的响应owner、诊断记录及窗口采样 SHALL保原US职责；FL提供中立宿主，UiKit Tools只检查／操作原入口，不接管业务结算、录制或采样寿命。隔离样例不成为真实运行世界。

性能改进 SHALL保原机会时钟、资格、优先序、随机／选音语义、四回执、冷却、发布和失败／释放路径；读取缓存不成为新的权威或隐藏未采用状态。稳定读取复用已发布输入／原owner持有结果，失效对应实际输入变化；诊断正文按DIAG-04按需缓存，不新增全局证据字典或第二记录ring。

性能排查 SHALL区分Kernel、UI展示构造、FL布局／绘制和工具观察成本，用相同有效输入、真实调用及原有测量入口解释热点；未知分配／GC／GPU或FPS不补零。微优化收益有限时，可考虑索引、事件驱动或责任迁移，但须先给真实热点、保留语义、失效／寿命、迁移／恢复及资源成本的比较；不能以一个离线截图或RPC往返时间直接决定重构。
#### Scenario: 诊断读取重复格式化成为热点
- **WHEN** 同一证据对象被反复读取而保存事实未变
- **THEN** 采用该对象内按需文本复用，实质变化失效；业务结算、录制ring与正常日志条件不变
- **AND** 若剩余热点来自地图更新／主体扫描，按该owner另行判断，不把文本优化结果外推为游戏FPS提升

