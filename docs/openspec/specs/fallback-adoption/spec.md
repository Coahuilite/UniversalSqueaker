# 独立VF1保存到实际选音
状态：现行，US0.6.x。责任：VF1→音频发布连接维护者。F02的生产连接与证据层见[实现状态](../../../development/specification/open-issues.md#f02-vf1音频发布缺失)。

## Purpose
已准入Race的最终回退映射变更应到达实际selector。独立文件本身合法；物理成功、运行采用与听感是不同事实。

## Requirements
### Requirement: FA1 提交先于采用
VF1保存／恢复默认／删除 SHALL在独立文件成功后，才把已提交表交音频发布者。取消、提交前失败或合法拒绝不报告Saved、不采用候选；省略覆盖与明确空音标记不同，原值及其他动作映射不丢。
#### Scenario: 无维护者来源时要求恢复默认
- **WHEN** 玩家表没有维护者源
- **THEN** 明确拒绝恢复而非假成功；合法删除是另一操作，不重写别域配置

### Requirement: FA2 已准入映射到真实消费者
已准入Race的成功变更 SHALL经现有音频发布者形成新AudioCatalog，供原owner后续合法selector使用；仅Store.Current／配置通知／UI文字变化不足。新table-only准入重启不扩大为所有编辑必须重启。
#### Scenario: 保存新映射且后续机会合法
- **WHEN** 同一已准入主体下一次合法选音
- **THEN** 使用已提交的新映射，不清Session或W、不重挂comp，不承诺立即听见

### Requirement: FA3 准确提交后失败结果
文件已提交而重载／Audio构造／发布失败时，本次结果 SHALL区分文件已保存与运行未采用，保最近可用运行引用，不假称未保存或回滚。若另行授权恢复， SHALL仅处理已提交数据读取／发布，不重复磁盘写入或重置响应状态；不因此要求新入口／持久协议。
#### Scenario: 文件成功后Audio发布失败
- **WHEN** 磁盘提交已发生
- **THEN** 原VF1结果准确说明运行尚未采用，不重放save／restore／delete，不伪装回滚

### Requirement: FA4 独立寿命、取消与退出
VF1 SHALL保独立SafeSaver文件和字段delta，不并入ModSettings防抖。删除确认只操作发起时原Race；取消不提交，关闭不撤销已提交文件或销毁主体owner。全局Reset保此文件。
#### Scenario: 确认弹出后切换表
- **WHEN** 随后确认删除或取消
- **THEN** 确认只删原请求Race的可删玩家表，取消零写入；维护者表不可删

### Requirement: FA5 新准入与既有编辑有别
新table-only Race自动comp挂载及其撤回 SHALL在完整重启的准入阶段处理；已有作者comp保留。已准入映射按FA2刷新，不临时扫描运行Pawn挂comp。
#### Scenario: 创建原无包无表Race
- **WHEN** 明确创建空玩家表成功但本进程尚无comp
- **THEN** 可显示新准入需完整重启；选择候选本身不写，不声称立刻可响应

未裁选项：新“重新应用已保存内容”按钮、持久pending任务和跨窗口恢复保证。本次现行合同不要求这些体验；若采用需真实新增保证／delta和独立授权，不能从一般Config repair推导。

