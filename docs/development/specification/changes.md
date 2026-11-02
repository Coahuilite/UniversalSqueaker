# 维护现行规格与新增能力
[现行入口](../../specification.md)按行为责任组合，而非运行时总管。中文正文保留Purpose／Requirements／Requirement／Scenario／WHEN／THEN及SHALL／MUST；Scenario只说明预期，不自动授权执行测试。

## 先区分恢复与真实变化
已有保证被违反：修复实现，proposal写清恢复范围，必要design说明实际caller→入口→consumer；无需伪造delta。既有合法契约漏记：注明来源补录。保证新增／改变／撤销：才用ADDED／完整MODIFIED／REMOVED（Reason、Migration）／RENAMED。完整MODIFIED保留未改场景，不以文档整理改变旧入口条件。

普通修订不凑模板。tasks只记录获准工作与完成依据；文稿接受、规格采用、实现、运行及发布分别授权。本库手工Markdown，不需要CLI、config或集成技能。

## 正向升级路线
1. 真实需求和当前接缝不足是什么？若责任／寿命／变化原因相同，优先原系统扩展。
2. 可选依赖或寿命独立时可加独立模块；形成内聚保证时可建新核心能力。纯新增没有旧责任可迁，不编造迁移工程；泛用FL能力不要求凑两个消费者。
3. 明确权威／owner／寿命及相邻双方义务，交一条最小完整入口→消费者→失败／取消／释放切片及证据边。
4. 责任迁移时同步调用者、状态、资源和唯一文档入口；清楚旧路径退出／恢复条件。原owner提供具体约束和可行替代，不无限否决；新模块正式采用后正常维护，不永久申请例外。

普通局部判断由执行owner完成；真正产品语义／支持／兼容或权限冲突给维护者具体条款、后果和推荐。不新建治理等级、PM／CO平台、异步总线或窗口管理器。

## 固定写作来源
采用[studyzy/OpenSpec-cn概念](https://github.com/studyzy/OpenSpec-cn/blob/b4a6bd99d6b7b12cfabb1dd0192091475107f69e/docs/concepts.md)、[写作约定](https://github.com/studyzy/OpenSpec-cn/blob/b4a6bd99d6b7b12cfabb1dd0192091475107f69e/docs/writing-specs.md)及[术语](https://github.com/studyzy/OpenSpec-cn/blob/b4a6bd99d6b7b12cfabb1dd0192091475107f69e/TRANSLATION_GLOSSARY.md)。本页是针对本库的自写方法，不复制可执行模板、不要求访问本机管理目录或兄弟库。

