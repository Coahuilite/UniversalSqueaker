# 0.6 r2：一条响应链与明确的新作者合同

当前源码目标是 RimWorld **1.6.4871 / net472**。r2 是未发布开发线的破坏性合同切换：完整 ActionId、一次通知声明和新 Config 格式一起生效；不提供旧 reader、短键猜测或旧计划回退。全部十五包、Core 常用 Work 和既有选音算法保留。普通竞争、节奏、首次唯一来源、ActiveCommand 等政策仍待裁；未显式激活整体 Dev 候选时零普通派发。

## 作者先声明一次，再通知实际主体

```csharp
using UniversalSqueaker;
using UniversalSqueaker.Authoring;
using Verse;

public static class WorkFinished
{
    public static readonly NotificationContract Contract =
        new("example.worker/Finished", intervalTicks: 120);

    // 定义初始化时调用一次，早于主体开始响应。
    public static void Register() => SqueakerNotifications.Declare(Contract);
    public static bool AfterSuccessfulWork(Pawn actualWorker) =>
        SqueakerNotifications.Notify(actualWorker, Contract);
}
```

完整外部编译例为 [DeclaredNotification.cs](examples/0.6/DeclaredNotification.cs)。C# 作者无需另写 Def；XML 作者可用 [UsNotificationDef](examples/0.6/DeclaredNotification.xml)。一个通知合同确定完整 ActionId 与固定通知事实。未知字符串、重复声明及冒用官方原生通知均有明确拒绝，不猜 Call 或默认计划。通知进入当前主体原有 owner；返回值不等于已经听见声音。

需要组合正向事实时，使用 `UsActionDef` 的独立根或继承：[受伤主体的 Select](examples/0.6/IdentitySelect.xml)、[Eat 扩展](examples/0.6/EatExtensions.xml)。父固定条件只会增加，不能被来源或玩家设置取消；子动作不继承父音源、优先级、配置身份或播放形态。每个实际动作都需自己的直接音频映射。

## 定义、配置、音频各按自己的寿命发布

| 数据／职责 | 当前生产入口 | 更新时机 |
| --- | --- | --- |
| 合同、固定条件、事实及响应实现 | `Core/Definitions/DefinitionCatalog` | 定义修订；发布后不再修改该修订 |
| 来源、玩家稀疏层与系统设置 | `Core/Configuration/BehaviorConfiguration` | 有效配置改变；同一发布者的修订号局部递增 |
| 规范音频条目与原选择算法 | `Core/Audio/AudioCatalog` | 音频输入改变；选包只提供配置过滤，不重建目录 |
| 一次一致读取 | `Core/Responses/RuntimeView` | 只捕获上述三个引用，不是第四份大快照 |
| 机会、截止、持续槽及同步结算 | `Core/Responses/ResponseSession` | 每稳定主体一个 owner |
| Pawn／Job／Def、Harmony、资源及游戏寿命 | `Game/` 与 `Runtime/` 原生适配 | 当前游戏事实与实际调用时 |
| 组装和框架回调 | `UniversalSqueaker.cs` 中的 `SqueakerMod` | 模组启动／窗口与保存生命周期 |

路径相对 `Source/UniversalSqueaker/`。整个 `Core/**/*.cs` 可无游戏、引擎或平台替身独立编译；生产仍只有一个 US DLL。保留 `Kernel` namespace 的选音算法现在位于 Core/Audio，不是另一条生产链。

稳定主体使用数字 Pawn 身份；Race、Xenotype 或显示标签改变不会另开冷却 owner。准确子域须是当前原生 canonical Xenotype，且在该 Race 的音频、配置或作者来源中有准确域；另一个 Race 的同名 Xenotype 不授予资格。

## 同步输出前预留，原 owner 按四收据结算

同 tick 先预留，再做一次原生输出，最后结算 `Dispatched / AlreadyActive / Rejected / Indeterminate`。重入、准备失败、原生抛错、旧持续音 End 前后故障保持各自含义，不复制整份状态后提交，不加入 Pending 或异步效果队列。

配置编辑不重抽已安排等待、不清硬截止。当前事实、发声能力、准确域、可播放性、preflight 及全局输出仍在本次机会或持续维护读取。持续音配置关闭后须按当前授权结束；普通 OneShot 的自然尾音不被过程退出硬切。局部 L 在本次请求固定，后续自有 Sample 读取当前 G 与距离；不改共享 SoundDef。

## 配置写入有两种提交语义

- **普通编辑**：具体 setter 校验并立即发布配置；单一 `SettingsSaveOwner` 按实时 **0.35秒** 合并物理写入。窗口自己的 tab／filter／scroll 与 FL session 不进入业务配置。
- **来源切换／完整稀疏行替换**：先 stage＋原子持久化，成功后采用并发布。保存失败保留原内存和原文件。磁盘已提交、发布失败时仍报告 **Committed**，修复发布，不重复写盘或假回滚。

Scribe 同步执行，临时序列化字段在 `finally` 恢复。generation、失败重试和关闭一次重试均由同一窄保存 owner 管理。配置修订号只在同一个设置发布者内比较；重载产生的新实例可以重新从本地序号开始，跨实例用对象身份区分快照。

`SqueakerMod`／`GameSettings` 形成新的框架 Config 身份；强制字段 `responseConfigurationFormat=1`。缺 marker 或未知格式为不可读，不自动读写、迁移或删除旧真实 Config。自动证据使用隔离文件及 **Scribe 替身**，不表示原生 Scribe／真实游戏 Config 已验。

作者来源只保存作者贡献与 last-good 快照；玩家层在全部作者层之后按 profile→Race→准确 Race/Xenotype 逐字段覆盖。`null`／省略为不贡献，`false`、`0`是明确值。声音包参与和行为来源启用是不同权限，关包不遗忘来源。

### 模板、清字段和恢复不要混读

`UsBehaviorPresetDef` 使用同一 `BehaviorValues` 文法，见 [PlayerTemplate.xml](examples/0.6/PlayerTemplate.xml)。在 Presets 显式选择准确域并导入，才增量复制模板中存在的玩家字段；未点名字段保留。它不自动绑定作者来源。

- 清作用域：只清该字段，保其他玩家字段及模板恢复目标。
- 恢复模板：重放当前模板存在的字段，保无关玩家字段；无效或缺失模板为不可用目标。
- 恢复单行默认：删除准确玩家行及恢复目标，保作者与父层。
- 完整稀疏行替换：省略字段恢复继承，另保独立模板恢复目标。
- 全局恢复：恢复当前玩家设置全集；作者来源绑定意图、last-good 快照及独立最终回退文件均保留。遗忘来源是另一项显式操作。

## 十五包与 CoreWork 没有缩成四个演示

| 包 | 当前正向合同／边界 |
| --- | --- |
| Select | 实际接受、反馈意图、最终主体固定；玩家可控 AND 清醒 AND 未倒地三项独立可调，默认 true |
| Wounded | 受损处理正常返回；与当前 injury 事实分开 |
| Death | Kill 进入的临终反馈，不代表死亡完成 |
| Attack | 明确实际执行接缝；执行不等命中，支持集仍为隔离 Dev 候选 |
| Move | 主体自身路径移动，不是被搬运位移 |
| Equip | 正常装备加入与实际 wearer；GLOBAL `PlayerCoreEquip` 仍为待裁 Dev 资格 |
| Call | 正向闲置／原地等待，不是未知动作或失败兜底 |
| Eat | WholeProcess／Any／Any；真摄入者、三轴独立、严格 Unknown；结果事件仅路由显式消费者 |
| Sleep | 当前 driver.asleep，不由躺床或不清醒推导 |
| Social | 明确社交过程与实际参与者；双方不同 owner |
| Joy | 实际娱乐过程，不伪称已获娱乐值 |
| Work | 已核 Core 的20 WorkType／105 Giver／78准确 Job-driver过程／73 driver类型；3项患者休息排除 |
| Draft／Undraft | 玩家实际命令接缝的两个独立方向 |
| MentalBreak | 正向崩溃启动，不把任意 MentalState 当崩溃 |

逐包源版本、调用角色、SR 差异、推荐与待裁体验见[完整十五包导读](us-kernel-0.6-contract-review-zh.md)；准确方法与 Unknown 见[事实适配表](us-kernel-0.6-fact-adapters-zh.md)；完整工作清单见[CoreWork](us-core-work-scope-zh.md)。这些表是十五份完整合同，不以共有证据豁免任一包的人审。SR 比较固定至 `b28f414b5b4cb40e772f29c6d964dec4eb56689e`／csproj0.3.4；SR 资源、命名与程序集不进入 US。

## UI 与诊断读真实结果

现有 Overview／Packs／Tuning／Presets／Advanced 和底部 Help 保留。scope 支持集、默认值、设置归一由 `BuiltInPawnContracts` 同一合同查询提供；`ActionPresentations` 只持标签。响应详情读取实际有效值、按字段来源、依赖、局部暂停与模板恢复目标，不维护另一套规则。

旧全局冷却、概率乘数、人口／速度与独立四档心情存储没有当前消费者，控件与死 Help 条目已退休。响应规则的年龄／心情参数与 participation 仍由新配置、来源、模板和公开设置方法表达并在详情显示，不把退休的乘数编辑器称作仍然生效。诊断展示真实最后决策／收据和硬截止，可播放性未采样则保持未定，不预测“下一次一定能响”。新页面方案与普通政策不在本次切换中裁定。

## 删除与能力去向

全部已删除生产源及逐文件替代位置见[删除映射](refactor-removals.csv)；下表按能力说明变化。

| 退休路径／前提 | 当前去向 |
| --- | --- |
| `Kernel/ActionKey.cs`、`SqueakAction`短键身份、Call哨兵 | `CoreActions` 完整ID；未知或未声明通知拒绝 |
| `SqueakActionConfig`／`Pure/SqueakActionPlan`／Comp 第二计划表 | canonical `ActionPlan`＋当前有效配置 |
| `SqueakRuntimeResolver`／`WithDefinitionIds`／新Def投旧Def再pool | 规范 AudioCatalog 直接使用保留算法及已选 entry 元数据 |
| `UsOneShotRuntime`／`UsPawnPlaybackSession` | 原生 `SqueakerRuntime`／`PawnResponseAdapter` 调用中立 `ResponseSession` |
| `PeriodicStateBinding.Probe` | 删除无消费者旧名字／bed／workGiver探针；`UsPawnFacts` 与精确 Work／Eat／事件观察提供正向事实 |
| `VerseEventBinding` | 各真实 Harmony 接缝，不保无消费者事件映射 |
| `Mod.cs`／旧设置 handle／迁移 reader | `UniversalSqueaker.cs`＋新 GameSettings 格式，旧真实文件不触碰 |
| 旧 TuningBaseline／MoodTuning／XenotypePreset／Timing模型 | `UsBehaviorPresetDef`、稀疏 BehaviorValues 与明确当前响应参数；旧数值不猜成新硬间隔或概率 |
| 官方 Crying／Giggling 包、默认挂载／设置／专属入口 | 官方库存移除；独立自定义同名动作仍可声明，原版声音不改 |

未改变：声音包权重与变体权重分开、年龄／彩蛋过滤、Remix 空层、准确来源回退、选包身份、实例输出归属、四收据及保存失败恢复。冻结选择器 corpus／golden 不为新身份修改；夹具只在边界转换历史标签。

## 固定依赖与验收状态

当前固定 FL **0.8.0-dev / fc010bd1eab8ed434e03de6241fdae89e94b1283**（A002），`Private=false`、范围 `[0.8.0,0.9.0)`，US 包不带 FL DLL。新的跨库候选与真实远端0.8 CI载体待维护者交付；旧0.7 CI输入不能证明本候选可构建。使用明确固定路径，不追移动输出。

自动检查按各候选输入区分独立 Core、生产适配替身、生产设置／ExposeData／原子文件与 Scribe 替身、真实 Host 读面以及最终 DLL 外部作者编译。游戏内1.6、真实 Config／Scribe、画面、试听和十五包双人工关口仍需用户。技术候选、整轮工作接受与发布是三个不同状态。
