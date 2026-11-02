# 音频供给、资源身份与选择
状态：现行，US0.6.x。责任：音频能力维护者；原生绑定见[adapter](../game-facts/spec.md)。

## Purpose
将声音条目与真实资源分开，保留准确域、来源后备、年龄／彩蛋和两层权重，给响应owner返回一次真实选择，不增加机会或重抽预览。

## Requirements
### Requirement: AUDIO-01 资源与引用身份
AudioId SHALL为实际SoundDef提供者packageId＋defName；包／条目／动作上下文另存。普通作者引用真实SoundDef，不填写伪造owner。规范目录包含已加载未选择包、包后备、玩家映射及合法Core浏览候选；浏览不授予comp或行为资格。
#### Scenario: A包引用B包声音
- **WHEN** A以自己的权重／参与声明引用B的SoundDef
- **THEN** 资源仍属B，条目仍属A；不以AudioId倒猜条目或请求主体

### Requirement: AUDIO-02 准确来源路径与四模式
Pawn选择 SHALL依次为当前(Race,Xeno)常规→同Race常规→当前精确域已选包PackFallback→最终Race动作表。Xeno不得借Race包PackFallback。Fallback取首非空；Remix在非空层等权；Vanilla适配Kernel Off仅最终表；Disabled零派发保选择。NonPawn不借Pawn／Race后备。
#### Scenario: 精确异种常规空而Race常规可用
- **WHEN** 请求的合法父路径允许Race常规
- **THEN** 可沿父路径选择，但不能据此取得Race包后备或另一Race同名Xeno池

### Requirement: AUDIO-03 包内年龄与彩蛋
选择 SHALL先取包内精确年龄组，无精确组才用全龄，再过滤独立彩蛋许可、条件和可用资源。精确组存在但过滤空时不再回该包全龄。普通与彩蛋同动作／年龄可并存；彩蛋许可不变年龄。
#### Scenario: 精确年龄条目被彩蛋门排空
- **WHEN** 该包有精确年龄组却无当前可用项
- **THEN** 依模式考虑其他来源层，不在同包退全龄；无候选如实结束

### Requirement: AUDIO-04 两级权重与一次选择上下文
包weight SHALL决定包份额，entry weight决定该包变体份额，缺省均1；多条entry不增加包份额或机会。选择返回完整动作、条目、包、请求域、供音域／层及AudioId，调制沿选中entry，不重抽或按资源猜归因。
#### Scenario: 等权包A十条、B一条
- **WHEN** 两包都合格
- **THEN** 先按A:B等权选包，再选本包entry；不能摊平十一条抽取

