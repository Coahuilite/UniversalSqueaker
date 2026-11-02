# 设置提交到运行采用
状态：现行，US0.6.x。责任：配置→运行发布连接维护者。VF1独立连接见[fallback-adoption](../fallback-adoption/spec.md)。

## Purpose
区分普通立即发布、排队保存、原子持久提交与随后运行发布，使UI／公开调用报告的层次与真实副作用一致，不创建第二保存队列。

## Requirements
### Requirement: PUB-01 两种合法提交语义
普通setter SHALL校验后立即发布，并由同一保存owner按0.35真实秒合并物理写入；来源切换／完整稀疏行原子入口 SHALL先stage＋持久化成功后采用／发布。页面tab、filter、scroll、FL session不进入业务配置。
#### Scenario: 普通Select编辑只排队
- **WHEN** GameSettings行候选回调只QueuePersistence并返回true
- **THEN** 可立即发布但不能称磁盘Committed，不强迫同步落盘修名字

### Requirement: PUB-02 原子成功与发布失败分开
TryCommitBehaviorSource／Override SHALL在候选未提交时false且保旧文件／内存；物理提交后true，随后发布失败用Committed:RuntimePublishFailed诊断，不假回滚、不重复物理事务，只修运行发布。
#### Scenario: 磁盘成功而运行发布抛错
- **WHEN** 原子提交已完成
- **THEN** 结果仍Committed；保存owner不重写该已成功文件，音频／owner／保护不被日志故障撤销

### Requirement: PUB-03 纯行替换不冒称物理提交
GameSettings.TryReplaceBehaviorOverride SHALL只stage完整稀疏候选并交acceptCandidate；回调接受后替换，自己不写盘、不排队、不发布原生runtime。TryChange／Refresh来源亦由调用方提供接受策略，不将合法纯作者根强加原生窗口前提。
#### Scenario: 作者提供内存接受回调
- **WHEN** 纯行候选获接受而未进行磁盘事务
- **THEN** 可报告替换成功，不能推为物理Committed；Mod原子caller仍履行PUB-02

### Requirement: PUB-04 同步保存、失败和关闭
同一保存owner SHALL管理requested／persisted／failed generation、0.35秒到期、失败脏态、显式Retry与关闭一次重试。Scribe临时字段finally恢复，序列化／原子替换失败保旧记录；不在Draw或读取Config期间构造新owner。
#### Scenario: 保存失败后关闭窗口
- **WHEN** 仍有脏代次且关闭触发一次flush
- **THEN** 由原owner处理一次重试，准确保Failed或Saved；不另建关闭写入链

### Requirement: PUB-05 生效不制造机会
新配置 SHALL供后续合法读取与持续维护采用，不清owner／硬截止／已排等待。修改音源输入沿音频发布者；选包仅配置过滤，不将普通setter全改Audio重建。
#### Scenario: 来源切换时已有状态等待
- **WHEN** 新来源已提交发布
- **THEN** 原W和截止保留，到合法机会才用新有效值；不为采用立即补叫

