# 输入被处理、文件已提交、声音已派发，是三件事

当前响应时钟／必需机会事实、混合消费者及旧字段拒绝以[显式机会合同](response-opportunities-0.6-zh.md)为准；普通交互仅真实秒，模拟仅游戏tick，不保legacy或附加tick门。发布前破坏性更新持续至用户明确确认面向玩家公开发布（US06-PREPUBLICATION-COMPAT-20261019 r4），不由GitHub可见／push／tag终止。

通知一次实际完成的工作时，使用已经声明的合同：

```csharp
bool processed = SqueakerNotifications.TryProcess(actualWorker, FinishedNotification.Contract);
```

`processed=true`表示该输入已在当前主体owner同步处理。条件不成立、没有音频、硬截止未到或输出拒绝时，也可以是true。未知／未声明身份或当前不能进入owner时为false。实际输出只由 `PlaybackReceipt` 的 `Dispatched / AlreadyActive / Rejected / Indeterminate`表达；通知返回值不承诺听见声音。完整可编译调用见[DeclaredNotification.cs](examples/0.6/DeclaredNotification.cs)。

## 把原子提交和普通编辑分开调用

需要完整稀疏行的原子文件提交时：

```csharp
bool committed = mod.TryCommitBehaviorOverride("Pawn", "", "Human", "", CoreActions.Work,
    new BehaviorValues { Volume = 0 }, out string diagnostic);
```

- false：本次候选没有提交，原文件和原行保留。
- true且diagnostic为空：提交与随后发布都完成。
- true且diagnostic为`Committed:RuntimePublishFailed:…`：文件与内存已采用，运行时发布待修复。保存owner只重试发布；不要把它当回滚而重复提交文件。

[CommittedOverride.cs](examples/0.6/CommittedOverride.cs)同时给出`TryCommit`与普通排队的`TryQueue`调用；与通知例一起由外部工程仅引用产品DLL编译。`SqueakerMod.TryCommitBehaviorSource`同样先持久化，再采用／发布来源意图。

**GameSettings的行替换不保证磁盘提交。** `TryReplaceBehaviorOverride(..., acceptCandidate, out diagnostic)`先stage完整稀疏行，回调接受后替换；省略字段恢复继承，独立模板恢复目标保留。回调可以由Mod提供真实原子持久化，也可以由普通Select setter仅排队；该方法自己不写文件、不排队、不发布原生runtime。`TryChangeBehaviorSource`／`TryRefreshBehaviorSources`也由调用方提供接受策略。

普通Select／Eat／scope／response编辑仍立即发布并通过单一保存owner按**0.35秒realtime debounce**合并写盘。排队、立即可读和磁盘Committed不是同义词；不为统一命名把普通编辑改成同步保存。物理Config新格式与两种保存语义见[r2合同](us-kernel-0.6-refactor-zh.md)。

## 保留名字，是因为这些对象确实不同

| 名字／输入 | 含义与保留理由 |
| --- | --- |
| 完整ActionId，例如`example.worker/Finished` | 合同身份；不从SoundDef、显示标签或声音名猜测 |
| XML entry的`key`／请求EntryKey | 包内变体／规范条目身份；同音源可以有不同条目，不合并为AudioId |
| XML `sound`／`sounds` | 普通原生SoundDef引用；定义就绪后绑定最终资源及真实提供者，不是作者填写的AudioId |
| AudioId | 绑定后资源身份，实际提供者与音源独立于响应条目、动作及请求主体域 |
| `family/namespace/target/child`／BehaviorDomain | 准确目标身份；Pawn Race/Xeno与NonPawn命名空间不混用，不是显示文本 |
| ActionPlan | 一份不可变动作输入：目录的声明默认，或本次机会折叠后的值；不是会话状态、播放请求或已成立证明。改叫Definition会误导其本次有效用途 |
| BehaviorValues／EffectiveBehavior／BehaviorConfiguration／RuntimeView | 分别是稀疏贡献、按准确域fold的结果与归因、配置修订输入、三个修订引用；合并或同名会抹掉权限和寿命 |
| PlaybackRequest／PlaybackReceipt | 申请实际输出与实际返回的四回执，不以Notify／资格成立替代 |
| UsBehaviorPresetDef | 玩家显式导入模板、复制存在字段，另有恢复目标；它不是持续绑定的作者来源。XML名保留，避免无收益格式变更 |

## 帮助先按目录定位，再翻译

US的统一底部面板保留当前话题与section概述，流程为：

`us/section/item目录ref → HelpItem的LabelKey/TextKey → Host翻译 → 实际文字度量／绘制`

例如`us/mode-row/remix`是帮助条目ref，`US.Help.ModeRow.Remix.Text`是翻译资源key；`help-section-ref`是存section ref的binding地址。这三者不互猜。目录内部的 `TitleKey / OverviewKey / LabelKey / TextKey` 明确存资源key；`HelpPanelDisplay.TitleText / LabelText / BodyText`才是解析后的文字。四个模式各有显式目录话题，跨section帮助先查完整目录，不被当前内容区限制。

没有选项专属help时，不覆盖控件的一般帮助，也不关闭section概述或统一面板。当前固定FL的session保15-pass grace；离开指针不是立即清空。当前话题过期后才回section概述，窗口本地的面板开合、tab、scroll仍各有自己的owner。度量和绘制都通过相同译文入口，不让切话题改变面板高度。

## 本次更名与固定依赖的边界

| 原名 | 当前名／处理 |
| --- | --- |
| `SqueakerNotifications.Notify` | `TryProcess`：bool指输入处理，不指派发 |
| `SqueakerMod.ChangeBehaviorSource`／`TryChangeBehaviorOverride` | `TryCommitBehaviorSource`／`TryCommitBehaviorOverride`，out `diagnostic`可报告Committed后的发布问题 |
| `GameSettings.TryChangeBehaviorOverride` | `TryReplaceBehaviorOverride`，`acceptCandidate`是调用方策略，不承诺落盘；`TryChangeBehaviorSource`／`TryRefreshBehaviorSources`保留 |
| 底层`BehaviorSourceSaveFailed`／`PlayerOverrideSaveFailed` | `BehaviorSourceCandidateRejected`／`PlayerOverrideCandidateRejected`：接受回调拒绝，不凭空认定已做磁盘IO |
| HelpSection.Key／Title／Overview；HelpItem.Key／Label／Text | `Ref / TitleKey / OverviewKey`；`Ref / LabelKey / TextKey` |
| `SectionHelpKeyOf`／`SectionHelpKey`／`help-section-key` | `SectionHelpRefOf`／`SectionHelpRef`／`help-section-ref` |
| Resolve的`helpHoverKey` | `currentHelpRef`；包括session的有效grace，不是本帧原始命中 |

当前固定输入为 FL **833c14f4 / local.incremental4**；下列public API迁移起于原7eb8073阶段，现继续沿用。XML／API 已迁至 `HelpRef / CurrentHelpRef / CurrentHelpSource / PublishHelp / HelpGracePasses / BeginHelpPass / ActiveTabBind` 与真实 binding 的 `*Bind` 后缀。`UiChoice<T>.HelpRef` 为空时不发布专属帮助；统一面板直接读 session，不要求可选 `HoveredOptionHelpOutputBind`。注册仍由 `UiBindings` 完成，访问／代理使用完整 `IUiBindingAccess`。新中立执行器、typed choices、项地址和生命周期用法见[固定消费者导读](fl08-consumer-migration-zh.md)，不保旧签名双轨。

这些更正不改变全部15合同、CoreWork清单、已裁Select／Eat参数、单主体owner或同步四回执；普通政策、真实Config/Scribe、原生图像／声音和逐包双人工关口保持原状态。
