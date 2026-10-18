# 0.5.x 当前架构与源码入口

本文描述 2026-10-18 核对的工作树，不描述下一版目标。产品声明为 0.5.0 / RimWorld 1.6 / .NET Framework 4.7.2；游戏编译引用为 `Krafs.Rimworld.Ref 1.6.4871`。当前构建依赖见 [build-and-debug](build-and-debug.md)。

## 当前链路

```text
原版加载 Def / XML patch / 音频
  → Catalog 校验 VoicePack 与可选 Xenotype 目标
  → VoicePack 声明 ∪ 最终回退表：种族自动挂载
  → settings + 作者导入预设 + fallback profile：运行时快照
  → Pawn 周期探测 / 事件通知
  → 行为范围、概率、冷却与发声能力门控
  → 精确域选池、年龄/彩蛋过滤、分层回退
  → SoundInfo + OneShot / Sustainer
  → 尝试冷却与诊断结果
```

## 一项事实只有一个所有者

| 事实 / 职责 | 源码入口 | 边界 |
| --- | --- | --- |
| 包 XML、稳定包键、静态拒绝规则 | `Models/SqueakVoicePackModels.cs` | 包键含 packageId；SoundDef 仍由原版名字解析 |
| 有效 VoicePack 与 Xenotype 目录 | `Catalog/SqueakXenotypeCatalog.cs` | 不把缺失的可选 DLC 变成硬依赖 |
| 何种 Race 自动得到 comp | `Catalog/VoicePackCompAttach.cs` | 声明/表驱动；保留已有 comp；不改存档 |
| 音频域与纯选池 | `Kernel/Domain.cs`、`Kernel/SqueakPoolRegistry.cs` | 不引用 Verse、Unity 或产品种族种子 |
| 设置投影、上下文、原版对象转换 | `Runtime/SqueakRuntimeResolver.cs`、`Runtime/SqueakKernelAdapter.cs` | 接游戏类型；不能把显示目录当路由权限 |
| 行为计划、周期状态与事件入口 | `Runtime/SqueakActionModel.cs`、`CompSqueaker.cs` | 内置动作与外部字符串入口分开 |
| 间隔计算 | `Pure/SqueakTimingModel.cs` | 纯输入与输出；不拥有原版播放对象 |
| 可播放性缓存 | `Runtime/SqueakSoundAvailability.cs` | 按需解析已加载 grain；不是完整音频质量检查 |
| 玩家 delta、作者预设导入 | `Models/ActionTuningRecord.cs`、`Settings/BaselinePresetImporter.cs` | presence 与值分开；导入不按 PackDef 自动发生 |
| 最终回退表与 Config 副本 | `Fallback/` | 表的数据源与声音资源不同；不等于每族内置行为方案 |
| 浏览用定义目录 | `Catalog/VerseDefinitionCollector.cs`、`Pure/UsDefinitionIdentity.cs` | 独立展示能力；不改变 comp admission 或 routing |

以上路径相对 `Source/UniversalSqueaker/`。目录/类若变更，应同步本表与直接调用者，不维护第二份逐行代码地图。

## 身份与域

- VoicePack 身份为 `packageId:PackDef.defName`；服务域为准确的 Race 或 `(Race, Xenotype)`，两者不互相代替。
- 当前 catalog 对相同完整包键的有效重复组拒绝。原版全局 Def 重名可能先于 US catalog 发生；后置包键并不能防止声音被覆盖。
- 当前适配器会把 SoundDef 投影为裸 `defName`，播放时再查原版数据库。0.6.x 的完整 AudioId / 最终对象绑定接缝尚未实现。
- Runtime 的 Xenotype 上下文使用 Race + Xenotype 双键；歧义按当前 selector 的保守路径处理。生产 HAR 反射发现仍有 TODO，不应宣传为已完成的能力。

## 配置、行为与声音供给

`SqueakVoicePackDef` 只提供声音候选，不携带触发模式、冷却计划或心情数据。独立 `UniversalSqueakerTuningBaselineDef` 提供 Race/Xenotype 调音预设，玩家手动导入后成为 settings 记录；它不能修改基础 action plan 的 mode、绝对间隔、冷却时钟或豁免。

默认自动挂载有 15 个生产动作及四档心情配置；17 个合法内置音频动作键不表示默认配置了 17 个触发计划。已有作者 comp 整体保留，缺项使用 `Unconfigured` 路径，不从默认 15 项列表补齐。

`NotifyExternalByKey` 是现有 C# 字符串入口。当前非内置键需玩家允许外部动作，未找到计划时使用外部默认计划；不存在一个所有旧调用者都必须先使用的新注册系统。后续新增扩展注册不能擅自改变旧入口的条件。

## 当前时间与播放结果

`CompTick` 仍负责周期状态探测和 Sustainer 维护。普通概率动作不是下一版的机会调度器：当前 `TryTrigger` 会进行概率判定、动作及共用冷却、发声能力判定，然后选音与派发。

- 动作未启用、概率未中或时间未到，和进入发声尝试后的失败不是同一分支。
- 发声能力拒绝、没有可用声音、播放资格拒绝、派发及异常等路径目前会写入尝试冷却；诊断保留结果差异。
- `PlayOneShot` 返回不能证明实际出声；当前 `Dispatched` 只记录派发。
- 同键活跃 Sustainer 重用；异键当前会先结束旧实例。维护依赖当前探测动作、地图和可见条件。这是旧行为，不是下一版的优先级替换合同。

0.6.x 将机会、硬间隔、重检等待、结果不明保护及最小可控实例替换分开。不要用旧代码行为替代新规格，也不要把新规格倒写为旧实现。

## 调制与已知隔离缺口

音高、音量主要经 `SoundInfo` 作用于派发；全局音量在 `ResolveMoodMod` 中相乘。距离设置经 `CompSqueaker.ApplyDistanceRange` 改写共享 `SubSoundDef.distRange`，会影响同 Def 的其他使用者。当前 OneShot 没有由 US 持有的可停止句柄。

可播放性缓存只需拿到一个非空 clip 即可标记 Available，其他 grain 失败不一定使整个声音失效；它也不检测静音、响度、许可或原版私有播放池是否与这个枚举结果一致。

这些是 [0.6.x 规格](us-kernel-0.6-spec-zh.md) 的适配边界与验证风险，不应通过扩大当前“测试通过”的含义掩盖。

## 验证入口与范围

纯选池及 Remix 空层回归在 `tools/UniversalSqueakerKernelTests/`；配置迁移、日志、UI 逻辑、真实生产 Host 的 stub harness 各有独立工具项目。`scripts/verify-local.ps1` 串接当前门禁，具体清单以脚本为准。

既有验收的适用范围见 [历史与证据边界](history/README.md)。
