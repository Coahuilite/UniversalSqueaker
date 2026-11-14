# US06 音量同值窄修与有限性能定位

本页保留 response-a001 的原时点。后续 F06–F09 实施与当前证据见
[生命周期与 UI 工作量攻坚候选](ui-attack-20261110.md)，不把本页旧 FL rc2 身份用于新载体。

本轮对应 WO-US06-UI-RESPONSIVENESS-20261110 r1 / DSP-US06-UI-RESPONSE-A001 r1。
新隔离配对入口：`dist/us06-ui-responsiveness-20261110/a001/USE-zh.md`。
工程候选交 CO／PM 核收；真实游戏渲染、输入与性能没有在本轮运行。

## 同有效值不再由音量绑定失效整页

两个绑定仍为 `global-volume`（0..1）和 `global-volume-percent`（0..100）。百分比换算、
XML 的 Live／范围／宽度、FL rc2 编辑器及原业务 setter 都未改。

原路径为绑定调用 `UsKernelSettingsSource.SetGlobalVolume` → `GameSettings.SetGlobalVolume`，
随后无条件 `bump()`。业务 setter 已拒绝非有限值，将有限值钳到 0..1，并在与当前值差的
绝对值小于 0.0001f 时返回；因此同值、钳位后同值及原容差内请求仍推进 ContentRevision，
下一次 BuildView 可重建完整页面投影。此为源码可达反例，未做新增动态 case。

现在两条绑定共享一个写闭包：在原调用前后读取业务实际采用的 `settings.globalVolumeFactor`，
仅实际读回不同才 bump。可选 `IUsGlobalVolumeSettingsSource.EffectiveGlobalVolume` 提供不经
BuildView 的只读回读；没有复制钳位／容差算法，也没有为判断是否变化额外构造整页。
生产 `UsKernelSettingsSource` 实现此能力；未实现的独立旧 source 保原 setter＋bump 路径，
不新增其使用前提、不强制修改既有 recording source。

每次请求仍到原 void setter。实际变化仍同步发布配置、排队保存，然后即时 bump；
直接调用 source／GameSettings 的旧入口、参数、返回合同不变。原 0.35 真实秒防抖、
Root.Update 维护、publication repair、关闭 Flush／Retry、Kernel 及诊断修复均保原实现。
独立的保存状态、帮助、尺寸等合法失效仍可发生，不能把这次窄修称为整窗停止工作。

FL `NumberFieldWidget` → `UiEditSession.Publish` 仍在请求后读实际 model，Live 不替换草稿；
滑块仍请求原业务值，数字框／caption 从同一已发布配置读回。没有清 edit、换 Host、
改变焦点／IME、禁止长草稿、截断输入或改为失焦才发布。草稿结束及关闭沿原 FL／US 合同。

## 快速合法编辑：同步工作与落盘分开

以下为当前源码路径分析，没有新游戏耗时测量。US 源码路径相对 `Source/UniversalSqueaker/`；FL 路径相对固定
`support/FL-source-197ff6b.zip` 中的 `Source/FerriteLib.UiKit/`。

| 层 | 触发与仍存在的成本 | 定位依据 |
| --- | --- | --- |
| 配置发布 | 每个实际采用的音量变化同步构造 BehaviorConfiguration；复制玩家行／选包集合并解析来源 XML。不是重建 Audio 目录 | `Settings/GameSettings.cs::SetGlobalVolume`、`GameSettings.BehaviorSources.cs::BuildBehaviorConfiguration`、`Core/Configuration/BehaviorConfiguration.cs` |
| US 视图 | bump 改共享 revision；configuration.Id 亦参与缓存键。下一次读取构造完整 VoicePacksViewState，包括名册／包投影及全部 action 的 Explain、玩家 override、说明文本，不仅音量控件 | `UI/UsKernelSettingsSource.cs::BuildView`、`UI/Model/VoicePacksPageModel.cs::BuildView/BuildActionScopes` |
| Arrange | FL `UiHost.Invalidate` 推进 ContentRevision；`UiLayoutEngine.Pipeline.cs::TryIncremental` 遇 externalContentChanged 拒绝增量入口，交完整候选路径。度量缓存仍有自身规则，不能等同每项都重测 | FL `Kernel/UiHost.cs`、`UiLayoutEngine.cs::ArrangeRoots` 及 `UiLayoutEngine.Pipeline.cs` |
| Draw | 每次 Host GUI pass 仍走实际可见节点、原生控件、当前值／草稿、文本与按需帮助／fit；合法业务变更和仅本地草稿变化不是同一种工作 | FL `Kernel/UiHost.cs::DrawFrame/Draw`、`Widgets/NumberFieldWidget.cs/SliderWidget.cs` |
| 物理保存 | 有效编辑只延后原保存 owner 的到期；Root.Update 在安全主线程、非 long event／Scribe 忙时维护。未到期不写，失败代次不自旋，关闭／显式 Flush 可另行写 | `Game/GameSettingsPersistence.cs`、`GameSettingsMaintenance.cs`、`Core/Configuration/SettingsSaveOwner.cs` |

有效值不变时现在免去的是 US 音量闭包主动发起的 bump；原 setter 也不发布／排保存。
快速合法变化仍会走表中同步成本。保存状态从 Saving 到 Saved／Idle 可另触发
`SynchronizeSaveState` 失效，发生次数不能按键数推算。没有现场 PhysicalSaveCount／写耗时，
既不能说每键都写盘，也不能把编辑间隙到期或关闭写入排除。

## 高级参数：首次建立、再次展开与静止是不同路径

`UsKernelSettingsHost.Behavior.cs` 的 advanced 命令只切换选中动作并 bump；
`VoicePacksPageModel.BehaviorState.cs` 只改窗口 state，不直接发布配置或排保存。
若点击先结束已有编辑，原编辑自己的提交仍可能发生，不能归给展开命令。

| 状态 | 确认的触发与成本边界 |
| --- | --- |
| 同域／同动作首次展开 | 视图身份／context 改变使 b-action-keys 重算；展开行合并基础与 contribution 字段，首次 Fields.Add／BindItem；FL 为新键建立兼容节点并排列／绘制 |
| 同域／同动作再次展开 | 原字段键、binding、节点保留；Contains 防止重绑。仍有 bump、完整 US 视图重建、action projection／keys 刷新与字段枚举，不能说再次展开零成本 |
| 展开静止 | revision／configuration／definition／policy 不变时 BuildView 缓存；keys 的 view＋context 缓存阻止每 pass 重建／重绑。更多可见字段仍要画与读值；ReadActionField 每次读取玩家稀疏行，EffectiveFieldSummary 的 fold 则按 view＋action/domain 缓存，不是每字段每帧重新 Explain |
| 折叠 | 只切选中动作并 bump，下一投影／排列隐藏字段；字段键仍留在 Repeat 中保草稿和兼容节点，不清业务值／持久状态。不再绘制隐藏子树，但声明／可见性处理不等于消失 |

依据另含 `UsSettingsBehaviorPageSource.cs::ReadView`、
`UsKernelSettingsSource.Roster.cs::ReadEffectiveFields/ContributionFields`、
`UsKernelSettingsSource.Voices.cs::ReadActionField` 与 `UI/Layout.Schema2.xml` 的 b-action-row 模板。
此前“更多可见控件”的候选解释仍成立；“每帧重复注册全部字段”不符合这些缓存／去重条件。
没有 F05 四阶段的对应计时，不能据此决定保留字段就是持续约50 FPS的根因。

## 已有原生计时只支持开窗线索

继承 SUM-US06-NATIVE-LOG-20261109 r1、原 worker REPORT r1 及其私有解析索引，未重采日志。
实际环境是 RW1.6.4871 rev591／Unity2022.3.35f1、旧维护 a001-r2＋同一固定 FL rc2；
detail／fit 开启。下面是八个完整开窗样本的原有聚合值，单位 ms：

| Host session | Create | View total | Arrange max | Draw max |
| --- | ---: | ---: | ---: | ---: |
| 1 | 59.231 | 68.555 | 191.378 | 35.095 |
| 3 | 9.959 | 8.698 | 69.371 | 4.933 |
| 6 | 9.552 | 8.235 | 55.192 | 9.600 |
| 8 | 9.942 | 8.305 | 55.448 | 9.394 |
| 10 | 9.597 | 8.270 | 54.067 | 7.934 |
| 12 | 9.789 | 8.892 | 58.327 | 5.175 |
| 16 | 9.850 | 8.467 | 57.108 | 2.684 |
| 18 | 9.667 | 8.391 | 55.438 | 2.425 |

各样本为32个 Host passes、3408次 view reads／2次 builds；不是32个显示帧。
View 与 Arrange／Draw 可嵌套，不能相加当帧耗时。首次与后续差异提示冷／暖成本，
未唯一定位 JIT、资源、US 或 FL 子阶段，也未精确绑定 F04 的约5帧变化。
这些旧样本不是新候选的性能结果，不能推广到 F03 编辑或 F05 展开。

帮助开启时 `PrepareHelpLayout` 每次先解析当前内容／字体环境；只有缓存键失效才进入
最多两轮预算排列，帮助节点尚不存在时另有初始排列。不是每帧无条件两轮；
实际高度变化才交兼容 UpdateDefinition，保 Host／editor。F02 的切换对象仍未知，
不能擅自补成切页、切 Pawn 或 hover，也没有帮助开／关耗时对照。

## 下一项优先决定

**优先量化同一对象快速合法音量变化时的配置构造、US 完整 BuildView（尤其全动作解释）
与 Host Arrange／Draw 的各自成本。** F03 是连续操作反馈且路径明确；窄修只除业务 no-op，
真实变化仍会强制失效。应先用现有采样／原生分析入口在获准现场运行后区分各层，
再决定是否授权局部投影／缓存修复，不改 Live 或保存合同。

F05 同一动作首次／再次展开、展开静止和折叠是下一独立对照；F04 保冷／暖开窗线索；
F02 先由现场 owner 明确切换对象。当前缺少这些操作的绑定计时、配置／目录规模、
实际落盘耗时、GC／完整帧归属及 detail／fit 关闭对照，无法量化本修复省时或 FPS贡献。
没有新增观测框架、性能 probe 或推测性全面优化，也未提出必须修改 FL 的结论。

## 本轮检查与继承边界

最终 Release 产品构建0警告0错误，使用 Krafs.Rimworld.Ref1.6.4871、Harmony2.4.2、
net472/x64 与固定 FL v0.8.0-rc2。既有 SettingsMigration ordinary133／commit27、
Host ordinary getters32／manifest479及原 Release stager通过。Host 实际消费产品 DLL 与同一 FL，
使用五个明确的平台 doubles；编辑 lane 使用原 recording source，覆盖旧 source 的使用条件、
Live／draft／冲突／取消／确认，不动态覆盖新生产回读 no-op 分支或原生数字框／IME。
没有新增测试／case／等价 probe，也未改断言。

同值／钳位／容差 no-op 与实际变化的前后机制有完整源码差量；精确 Host revision 连续链
没有新增动态验证。相关既有检查通过不替代该缺口或游戏渲染检查。
未变 Core／OneShot／AudioBinding、维护调度及诊断消费者沿已接受维护 REPORT r4 的原输入层复用，
本轮未重跑。用户 A01主菜单／暂停保存正常、A02录A看B不串保留为旧现场结果；
重启、持续槽真实复核／终止及当前诊断两方向仍待原现场 owner，不由本轮代签。

交付 `IDENTITY.json`、`USE-zh.md` 与对应 Source Form 给出准确程序集、复建入口和未验范围；
完整原始命令／退出／私有日志由本轮 Worker REPORT 交 CO 保存，不随产品包公开。
