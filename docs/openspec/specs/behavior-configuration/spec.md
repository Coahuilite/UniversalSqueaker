# 稀疏行为、来源、模板与格式
状态：现行，US0.6.x。责任：配置维护者。物理提交与发布见[连接](../configuration-publication/spec.md)。

## Purpose
使作者贡献与玩家权限按准确域逐字段得到可解释的有效值，保留缺省、false、0、拒绝数据和last-good；音频选择、事实及运行状态不成为配置权威。

## Requirements
### Requirement: CFG-01 权限与折叠顺序
有效参数 SHALL依声明／普通缺省→已选主域作者→已选合法子域作者→玩家服务profile→玩家主域→玩家合法子域折叠；所有作者先于玩家。省略／null不贡献，false／0明确。system服务模式、G、A、彩蛋总许可与距离独立，域不可抵消。
#### Scenario: 作者Xeno与玩家Race都设置字段
- **WHEN** 两者提供同字段
- **THEN** 玩家Race覆盖作者Xeno；玩家Pawn profile不是system或NonPawn profile

### Requirement: CFG-02 合法域与依赖
Pawn SHALL按空域服务层／Race／合法Xeno；NonPawn按自己的Namespace服务层／Target，不接受Pawn专属Eat、Select资格、Age／Mood／Talking／scope字段。配置不能造事实、删除父固定条件或因Unknown改粗合同；显式非法值暂停对应贡献，不暗修默认。
#### Scenario: 独立Result根配置Eat阶段
- **WHEN** 不含摄入过程／执行固定条件却填写Eat三轴
- **THEN** 报EatParametersRequireIngestionContract；不自动继承普通Eat

### Requirement: CFG-03 来源三态与显式采用
每准确域 SHALL只选一份作者来源；Unconfigured沿父路径／缺省，Inherit动态跟父，Bound保持明确身份。普通不自动采用唯一候选或后到竞争者，停声音包不遗忘行为来源，音频-only无需来源。
#### Scenario: 缺失A时B仍存在
- **WHEN** 本域绑定A而A不可用
- **THEN** B不接管；关闭音频包不清A的意图或好快照

### Requirement: CFG-04 last-good只保存作者贡献
快照 SHALL存来源身份、字段存在性及值含关闭／false／0；不存父最终值、玩家层、事实真假、随机、截止、句柄或委托。有效来源更新完整替换；坏更新保好快照，可解释则使用、不可解释部分暂停；同身份同域归来可重连。
#### Scenario: 缺实现但参数快照可读
- **WHEN** 来源参数能恢复而必需事实或资源仍缺
- **THEN** 依赖路径仍拒绝，不以缓存上次True伪造运行资格

### Requirement: CFG-05 单一时钟字段与旧数据拒绝
来源／玩家间隔 SHALL用simulationIntervalTicks（非负整数）或interactionIntervalSeconds（有限非负），按本次机会／适用合同读取；官方错单位拒绝。hardIntervalTicks及hasHardIntervalTicks旧字段不执行、不解释为秒、不自动改写。responseConfigurationFormat必需为1；缺失／未知格式不可读。
#### Scenario: 旧快照含18或36
- **WHEN** 读取结构合法格式1绑定但含已知旧计时字段
- **THEN** 保来源ID、ActionId、具体原因及拒绝原片段，不把18当18秒、不清音频／包／外观
- **AND** 一般畸形XML拒绝，不提供泛用修复框架

### Requirement: CFG-06 选定域显式恢复
旧已知被拒绑定 SHALL允许安全读取及玩家明确选定域原子继承／重绑；同ID重选也可触发明确更新。启动刷新不替玩家恢复，失败原数据不变，其他域含被拒数据、音频与外观保留。此为既有来源恢复，不授予VF1新reapply协议。
#### Scenario: 只解除一个旧绑定
- **WHEN** 玩家在行为与来源选准确域，再明确选择继承
- **THEN** 只替换该域意图／快照；成功才采用，另域不被清；返回旧版本须用玩家自己的对应旧配置

### Requirement: CFG-07 模板与Reset分别定义
模板 SHALL仅在显式选目标并导入后增量复制存在的玩家字段，不自动绑定来源。清字段只清该presence，恢复模板只重放模板点名项，恢复单行删除玩家行与其恢复目标，原子替行保独立模板目标；全局恢复保来源意图／last-good及VF1文件。
#### Scenario: 空响应集合恢复继承
- **WHEN** ReplaceResponseTuning以空集合替换准确行响应字段
- **THEN** 只清本层响应覆盖，保scope、作者绑定和其他动作；输出／全局恢复A回1

### Requirement: CFG-08 解释与不可变读取
配置发布 SHALL复制来源与玩家行；Read返回副本，旧视图不被后续编辑污染。解释包含每字段来源、依赖、局部暂停与模板目标；revision仅同发布者可比，跨实例以身份区分。
#### Scenario: 打开自定义混合动作编辑
- **WHEN** 切换要编辑的机会类别
- **THEN** 只是秒／tick字段视图，不改变固定合同、运行时钟或别类参数

