# 为0.6制作声音包与扩展

当前响应时钟／必需机会事实、混合消费者及旧字段拒绝以[显式机会合同](openspec/specs/response-opportunities/spec.md)为准；普通交互仅真实秒，模拟仅游戏tick，不保legacy或附加tick门。发布前破坏性更新持续至用户明确确认面向玩家公开发布（US06-PREPUBLICATION-COMPAT-20261019 r4），不由GitHub可见／push／tag终止。

先准备普通 SoundDef，再把完整动作ID映射到该声音。声音引用只写真实 defName；US 在定义就绪后绑定最终资源对象与实际提供者，不要求作者填写或伪造 AudioId owner。

本轮 1.7 基础兼容沿用相同的声音包声明、准确来源、准入与模式语义，因此作者不需要增加版本专用字段或恢复旧格式。候选仍以 net472／1.6 引用编译，并须配套同次固定 FL；1.7 API／IL 核对不代替包的实际声音验收，作者仍应按自己的准确域与合法机会复验。

```xml
<Defs>
  <UniversalSqueaker.UsVoicePackDef>
    <defName>US_MyHumanVoice</defName>
    <declaration>
      <race>Human</race>
      <entries>
        <li><key>work</key><action>us.core/Work</action><sound>MyWorkSound</sound></li>
      </entries>
    </declaration>
  </UniversalSqueaker.UsVoicePackDef>
</Defs>
```

`MyWorkSound`须由本包或声明依赖的模组实际提供。包身份为真实 `packageId:DefName`，条目身份独立于声音资源。新包没有自动选中：在语音管理的准确域勾选。普通Release启动会自动安装[普通响应默认](openspec/specs/ordinary-policy/spec.md)，无需Dev、重复启包或另选行为来源。勾选不代表一定出声：仍须实际事实、资格、保护与可播声音全部通过。

## 选你需要的声明，不叠一套旧格式

| 需求 | 当前声明／完整示例 |
| --- | --- |
| 只增加已有合同的声音 | `UsVoicePackDef`，[AudioOnly](examples/0.6/AudioOnly.xml)／[最小Eat](examples/0.6/EatMinimal.xml) |
| 引用另一音频模组的普通SoundDef | 同一声明，[ReferencedAudio](examples/0.6/ReferencedAudio.xml)；保依赖与真实来源 |
| 继承已有固定条件或增加独立根 | `UsActionDef`，[IdentitySelect](examples/0.6/IdentitySelect.xml)／[EatExtensions](examples/0.6/EatExtensions.xml) |
| 一个明确自定义事件 | [一次C#声明](examples/0.6/DeclaredNotification.cs) 或 [UsNotificationDef](examples/0.6/DeclaredNotification.xml) |
| 明确提交玩家稀疏行 | [CommittedOverride.cs](examples/0.6/CommittedOverride.cs)及[提交与处理语义](specification.md) |
| 提供新事实／响应实现 | `IUsDefinitionProvider.Collect`，批次中的 `IUsFactSource`／`IUsResponseRule` |
| 为准确域提供作者行为贡献 | `UsBehaviorSourceDef`，[BehaviorSource](examples/0.6/BehaviorSource.xml)，须显式绑定来源 |
| 玩家手动导入稀疏调音值 | `UsBehaviorPresetDef`，[PlayerTemplate](examples/0.6/PlayerTemplate.xml)，不自动绑定作者来源 |
| 年龄、心情、参与与持续形态 | [Responses](examples/0.6/Responses.xml)；启用、参数、参与各自独立 |
| 全部官方包装 | [OfficialFifteen](examples/0.6/OfficialFifteen.xml)及[逐包导读](openspec/specs/official-actions/spec.md) |

新格式不再接受短键 `Work`、旧 `SqueakVoicePackDef`、旧 comp 动作计划或任意未声明字符串的默认计划。C# 扩展可仅用一次声明，不强迫写 XML。独立自定义 Crying／Giggling 不受官方包退役禁用，但仍须完整ID、明确合同及音频映射。

## 准确域与权限

Pawn 包声明准确 Race；可选 `child` 是该 Race 下真实 canonical Xenotype。也可写显式 `family/namespace/target/child`；`race`不能与另一套 target 声明混用。NonPawn 的 namespace＋target 身份已能表达，原生完整宿主仍未交付，不能拿 Pawn 字段冒充。

纯模型可直接将 NonPawn 的 `BehaviorDomain` 放入 `ResponseFrame`，经 `ResponseSession.Respond(RuntimeView, ResponseFrame, policy, ...)` 使用同一配置折叠、择一与结算；持续授权 `Review` 采用相同主体族默认。NonPawn 不注入 Pawn 的 scope、Talking、Eat 或 Select 资格默认，也不运行 Pawn Talking 门；显式填写这些非法配置仍拒绝。手动解释配置时，`BehaviorContract.DeclaredDefaults(action, family)`／`Defaults(action, policy, family)` 接受准确的 `"Pawn"` 或 `"NonPawn"`；原无 family 重载保留原 Pawn 语义，高层调用无需改走 `DecisionInput`。

声音包只控制自己的音频和 participation。行为来源控制允许的作者默认，玩家 profile／Race／准确子域在其后逐字段覆盖；玩家 participation 可以覆盖 entry 的 Skip，作者来源不具有这项权限。缺事实为 Unknown；不删条件、不换主体、不退Call。

包根 `weight` 决定包份额，entry `weight`决定已选包内变体份额；多写entry不增加机会或包份额。`pitch`是条目校准；音量可为0、音高须有限正数。`age`／egg筛选见[音频选择](openspec/specs/audio-selection/spec.md)，校准／参与见[调制与输出](openspec/specs/audio-output/spec.md)，保存／删除映射及能力组合见[现行规格](specification.md)。

## 编译与验证作者调用

引用当前 `UniversalSqueaker.dll` 和匹配的 RimWorld1.6编译引用，不链接US源码或测试替身。将示例中的 `Register()`放在本模组定义初始化阶段，只调用一次；实际工作成功后才用实际worker通知。不要按声音名字、标签或显示文本猜合同。

通知用`TryProcess`，返回true只表示输入处理，不代表已派发。Mod的`TryCommit…`返回true表示实际文件已提交，out diagnostic仍可报告随后发布失败；GameSettings的行替换与普通setter不承诺即时落盘。提交边界见[配置提交](openspec/specs/configuration-publication/spec.md)，帮助ref／译文key见[UI帮助](openspec/specs/ui-consumption/spec.md)，保留名字的理由见[实现映射](development/specification/implementation.md)。

先解析完整XML，核对完整ID、继承图、准确域、参与／参数presence与资源依赖。自动替身测试能证明调用和请求，不能证明已听见声音。获准游戏验证时，再用固定配对候选确认正常加载、选包、实际事件、保存重开、各模式与试听；不为静态验证自行启动游戏或修改真实Config。
