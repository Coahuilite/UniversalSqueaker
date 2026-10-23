# 当前0.6 r2架构与源码入口

入口文件名保留原文档链接；本页现描述r2源码。完整作者前提、保存语义、十五包和删除表见[r2合同](us-kernel-0.6-refactor-zh.md)。旧0.5发布时间与验收不能替代当前候选。

## 生产链只有一个状态owner

```text
定义修订 → DefinitionCatalog
来源／玩家／系统变更 → BehaviorConfiguration
音频输入变化 → AudioCatalog
                   ↓ RuntimeView捕获三引用
当前主体／准确域／事实 → ResponseSession预留本tick
                     → 原生输出 → 原owner四收据结算
UI／Help／诊断 ← 同一配置解释及最后真实运行记录
```

| 职责 | 当前源码位置（相对Source/UniversalSqueaker） |
| --- | --- |
| 中立声明、组合、事实／响应扩展 | `Core/Definitions/` |
| 稀疏配置、来源归因、last-good与窄保存状态 | `Core/Configuration/` |
| 规范音频输入、保留选音算法、准确域 | `Core/Audio/` |
| 决策、单会话、四收据、持续当前授权 | `Core/Responses/` |
| 游戏事实／方法观察、Pawn适配、发布 | `Game/` |
| Harmony／资源绑定／自有音频实例 | `Patches/`、`Runtime/UsAudioBindings.cs`、`Runtime/UsInstanceOutput.cs` |
| 新配置物理字段、模板导入与准确恢复 | `Settings/GameSettings*`、`Game/UsBehaviorPresetDef.cs` |
| 原子文件与同步持久化adapter | `Settings/UsAtomicConfigFile.cs`、`Game/GameSettingsPersistence.cs` |
| 定义浏览、comp准入 | `Catalog/`，两者不互相授予权限 |
| 当前设置读模型／Host／窗口 | `UI/Model/`、`UsKernelSettingsSource`、`UsKernelSettingsHost`、`UI/SettingsWindowOwner.cs` |
| 仅组装及框架生命周期 | `UniversalSqueaker.cs` 的 `SqueakerMod` |

完整Core生产源无游戏、引擎或替身；游戏对象不经object／反射暗渡核心。原生adapter在主线程进入中立串行owner，不把线程政策固化成Unity核心。生产保持单DLL，没有第二resolver、短键计划或兼容reader。

配置编辑不重置已安排等待和截止。Sustained复核当前配置与事实，OneShot正常尾音保留。音频选择直接使用目录中的entry及校准／参与元数据，不反查AudioId猜来源。总音量和距离仅作用于自有实例，不改共享Def。

## 当前检查边界

`UniversalSqueakerRefactorCoreTests`递归链接整个Core；Kernel corpus保原算法与golden。OneShot／AudioBinding运行生产owner／caller和明确平台替身。SettingsMigration／Reset运行生产settings、ExposeData、原子文件与Scribe替身；DomainAdapter运行实际GameRuntime的canonical子域查询。Host使用实际产品DLL、资源及固定FL，平台绘制／输入为替身。各入口证明自己的边界，均不表示真实游戏Config／画面／听感已验。