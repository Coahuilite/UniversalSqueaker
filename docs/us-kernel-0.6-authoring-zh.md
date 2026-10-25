# 为0.6 r2制作声音包与扩展

先准备普通 SoundDef，再把完整动作ID映射到该声音。声音引用只写真实 defName；US 在定义就绪后绑定最终资源对象与实际提供者，不要求作者填写或伪造 AudioId owner。

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

`MyWorkSound`须由本包或声明依赖的模组实际提供。包身份为真实 `packageId:DefName`，条目身份独立于声音资源。新包没有自动选中：在语音管理的准确域勾选。普通Release启动会自动安装[普通响应默认](us-kernel-0.6-spec-zh.md#唯一生产-owner-与普通启动)，无需Dev、重复启包或另选行为来源。勾选不代表一定出声：仍须实际事实、资格、保护与可播声音全部通过。

## 选你需要的声明，不叠一套旧格式

| 需求 | 当前声明／完整示例 |
| --- | --- |
| 只增加已有合同的声音 | `UsVoicePackDef`，[AudioOnly](examples/0.6/AudioOnly.xml)／[最小Eat](examples/0.6/EatMinimal.xml) |
| 引用另一音频模组的普通SoundDef | 同一声明，[ReferencedAudio](examples/0.6/ReferencedAudio.xml)；保依赖与真实来源 |
| 继承已有固定条件或增加独立根 | `UsActionDef`，[IdentitySelect](examples/0.6/IdentitySelect.xml)／[EatExtensions](examples/0.6/EatExtensions.xml) |
| 一个明确自定义事件 | [一次C#声明](examples/0.6/DeclaredNotification.cs) 或 [UsNotificationDef](examples/0.6/DeclaredNotification.xml) |
| 明确提交玩家稀疏行 | [CommittedOverride.cs](examples/0.6/CommittedOverride.cs)及[提交与处理语义](us-public-contracts-and-help-zh.md) |
| 提供新事实／响应实现 | `IUsDefinitionProvider.Collect`，批次中的 `IUsFactSource`／`IUsResponseRule` |
| 为准确域提供作者行为贡献 | `UsBehaviorSourceDef`，[BehaviorSource](examples/0.6/BehaviorSource.xml)，须显式绑定来源 |
| 玩家手动导入稀疏调音值 | `UsBehaviorPresetDef`，[PlayerTemplate](examples/0.6/PlayerTemplate.xml)，不自动绑定作者来源 |
| 年龄、心情、参与与持续形态 | [Responses](examples/0.6/Responses.xml)；启用、参数、参与各自独立 |
| 全部官方包装 | [OfficialFifteen](examples/0.6/OfficialFifteen.xml)及[逐包导读](us-kernel-0.6-contract-review-zh.md) |

新格式不再接受短键 `Work`、旧 `SqueakVoicePackDef`、旧 comp 动作计划或任意未声明字符串的默认计划。C# 扩展可仅用一次声明，不强迫写 XML。独立自定义 Crying／Giggling 不受官方包退役禁用，但仍须完整ID、明确合同及音频映射。

## 准确域与权限

Pawn 包声明准确 Race；可选 `child` 是该 Race 下真实 canonical Xenotype。也可写显式 `family/namespace/target/child`；`race`不能与另一套 target 声明混用。NonPawn 的 namespace＋target 身份已能表达，原生完整宿主仍未交付，不能拿 Pawn 字段冒充。

声音包只控制自己的音频和 participation。行为来源控制允许的作者默认，玩家 profile／Race／准确子域在其后逐字段覆盖；玩家 participation 可以覆盖 entry 的 Skip，作者来源不具有这项权限。缺事实为 Unknown；不删条件、不换主体、不退Call。

包根 `weight` 决定包份额，entry `weight`决定已选包内变体份额；多写entry不增加机会或包份额。`pitch`是条目校准；音量可为0、音高须有限正数。`age`、egg及参与字段文法见[当前规格§20–23](us-kernel-0.6-spec-zh.md#20-s4-ab-与-s5-当前使用方式)，完整状态／保存及删除表见[r2合同](us-kernel-0.6-refactor-zh.md)。

## 编译与验证作者调用

引用当前 `UniversalSqueaker.dll` 和匹配的 RimWorld1.6编译引用，不链接US源码或测试替身。将示例中的 `Register()`放在本模组定义初始化阶段，只调用一次；实际工作成功后才用实际worker通知。不要按声音名字、标签或显示文本猜合同。

通知用`TryProcess`，返回true只表示输入处理，不代表已派发。Mod的`TryCommit…`返回true表示实际文件已提交，out diagnostic仍可报告随后发布失败；GameSettings的行替换与普通setter不承诺即时落盘。具体边界、帮助ref／译文key及保留名字的理由见[公开合同与帮助](us-public-contracts-and-help-zh.md)。

先解析完整XML，核对完整ID、继承图、准确域、参与／参数presence与资源依赖。自动替身测试能证明调用和请求，不能证明已听见声音。获准游戏验证时，再用固定配对候选确认正常加载、选包、实际事件、保存重开、各模式与试听；不为静态验证自行启动游戏或修改真实Config。
