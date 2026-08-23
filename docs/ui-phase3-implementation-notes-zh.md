# US Phase 3 最小 UI 组件化实现笔记

> 状态：实现完成（Phase 3）。
> 规格来源：`docs/ui-componentization-evaluation-zh.md` 方案 A（响应式 view-model + 声明式即时组件）。
> 本笔记记录组件清单、命令流、崩溃安全矩阵、两族分配验收步骤与已知限制。

## 1. 组件清单 → 职责表

新代码位于 `Source/UniversalSqueaker/UI/`，全部为 Verse IMGUI 即时组件。

| 组件/文件 | 输入（只读 state） | 输出命令 | 高度测量职责 |
|---|---|---|---|
| `SectionFrame` | rect、surface kind | 无 | 无（纯绘制） |
| `StatusBanner` | rect、文案、kind | 无 | `VoicePacksLayout.BannerHeight` |
| `HelpToggle` | rect、active | 无（返回点击，由页面翻转本地 state） | 无（固定 26f） |
| `SearchField` | rect、ref 搜索字符串、hint | 无（只改页面本地搜索串） | 无（固定 30f） |
| `ModeCard` | 当前 mode、目标 mode、标题/描述 | `SetMode` | `VoicePacksLayout.ModeCardHeight` |
| `RaceLayerRow` | race summary、是否选中 | `SelectDomain` | `VoicePacksLayout.RaceLayerRowHeight` |
| `VoicePackRow` | pack key/label/作者/coverage、是否选中 | `TogglePack` | `VoicePacksLayout.VoicePackRowHeight`（74f） |
| `VoicePackChecklist` | domain view、ref 搜索字符串 | `TogglePack` / `ForgetUnavailable` | `VoicePacksLayout.ChecklistHeight` |
| `EmptyState` | 文案 | 无 | `VoicePacksLayout.EmptyStateHeight` |
| `Footer` | 文案 | 无 | `VoicePacksLayout.FooterHeight` |
| `VoicePacksLayout` | width、view state、search、`ITextMetrics` | 无 | 所有纯高度/计数计算 |
| `VoicePacksViewState` | settings/catalog 投影 | 无 | 只读 DTO |
| `UiCommand` | 命令值 | — | — |
| `VoicePacksPageModel` | settings/catalog + page state | 执行命令 | 投影 view-state |
| `VoicePacksPage` | settings 窗口 rect | 组合全部组件 | 页面级滚动 + try/catch |

## 2. 单向数据流 / 命令流图

```text
UniversalSqueakerSettings.DrawSettings(Rect)
        │
        ▼
VoicePacksPage.Draw(Rect)
        │
        ├─ 每帧投影：VoicePacksPageModel.BuildView(settings, catalog, pageState)
        │      └─ VoicePacksViewState（只读，含 Races / XenotypeDomains / SelectedDomain / BannerText）
        │
        ├─ Measure：VoicePacksLayout.MeasureContentHeight(...)（先测量，后绘制）
        │
        ├─ 绘制：Components.Draw(rect, ref state, emit)
        │      └─ emit(new UiCommand(...)) 只收集，不写业务
        │
        ├─ 帧末：VoicePacksPageModel.ExecuteAll(settings, commands, pageState)
        │      ├─ SetMode        → settings.CommitVoicePackMode(...)
        │      ├─ SelectDomain   → 仅改页面本地 state
        │      ├─ TogglePack     → settings.SetVoicePackSelection(scope, race, target, nextKeys)
        │      └─ ForgetUnavailable → settings.SetVoicePackSelection(scope, race, target, retainedKeys)
        │              └─ 写入桥内部：NotifyDiscreteResolverRuntimeChanged() + QueuePersistence()
        │
        └─ 下一帧：BuildView 重新投影 → 新 VoicePacksViewState
```

要点：
- 组件不持有 `UniversalSqueakerSettings` 或 `SqueakXenotypeCatalogSnapshot` 引用。
- 业务写入只经 `VoicePacksPageModel`，落到既有 `SetVoicePackSelection`（两族 race-aware 重载）与 `CommitVoicePackMode`。
- resolver 立即重建与 QueuePersistence 语义完全未改动。

## 3. 崩溃安全矩阵

| 场景 | 实现位置 | 为何安全 |
|---|---|---|
| 无候选 pack（空 catalog） | `VoicePacksPageModel.BuildView` / `VoicePacksPage` | `RaceDefNames` 与 `GetTargetCandidates` 为空时产生空列表；页面渲染 BannerText + EmptyState；无索引访问、无空引用。 |
| 无 Biotech | `BuildXenotypeDomains` / `VoicePackChecklist` | `ModsConfig.BiotechActive == false` 时 Xenotype 域标记 `IsDormant` 并显示提示；`XenotypeByDefName` 为空只影响 label 回退到 defName，不抛异常。 |
| 无 HAR | `VoicePacksPageModel` / catalog 投影 | HAR hints 未被投影为行；UI 完全不遍历 `HarHintDefNames`，因此无 HAR 与无 HAR 支持等价于无额外行。 |
| 无选中 pawn | 整个 UI 不读取 Pawn/Find | 设置页只处理 settings/catalog；pawn 路由在 resolver 内对 null pawn 走全局/静默路径。 |
| 损坏设置 | `BuildView` / `NormalizeMode` / `Execute` | Scribe PostLoadInit 已归一化；`voicePackSelections` 等使用 null 合并；非法 mode 在投影时归一化为 `Off`；残余异常由页面级 try/catch 兜底。 |
| fallback profile 缺失 | UI 不引用 `SqueakFallbackProfileStore` | 缺失/损坏由启动期 `LoadOrRebuild` 自愈；UI 的 Fallback 模式只写 `voicePackMode`，不直接触碰 profile 文件。 |
| 空搜索 | `VoicePacksLayout.CountShownPacks` / `VoicePackChecklist` | 过滤结果为 0 时渲染 EmptyState“无匹配”，不产生 0 行滚动或除零。 |
| 窗口极窄 / 内容高度为 0 | `VoicePacksLayout` / `VoicePacksPage` | 高度计算使用 `Math.Max(1f, ...)`；绘制前检查 `rect.width/height <= 1f`；整页滚动可容纳窄布局。 |
| 渲染中组件异常 | `VoicePacksPage.Draw` 外层 try/catch | 捕获异常后 `Log.Warning` 并绘制 EmptyState；设置窗口仍可关闭，绝不抛穿到框架。 |

## 4. 两族分配矩阵验收步骤

维护者需在游戏内执行以下矩阵（RaceA/RaceB 为中性占位名，不是内置种族）：

1. 安装两个 VoicePack：一个 `raceDefName=RaceA`，一个 `raceDefName=RaceB`。
2. 打开 Universal Squeaker 设置。
3. 在 Race Layer 点击 RaceA 行，勾选 RaceA 的 VoicePack；点击 RaceB 行，勾选 RaceB 的 VoicePack。
4. 验证即时路由：
   - 生成/操作 RaceA pawn，听到 RaceA 包的声音；
   - 生成/操作 RaceB pawn，听到 RaceB 包的声音；
   - RaceA 的包绝不路由到 RaceB pawn，反之亦然。
5. 分别取消勾选 / 重新勾选，确认路由立即变化（resolver 离散重建）。
6. 切换 Off / Fallback / Remix 三张模式卡，确认 Off 不路由、Fallback 走内建 fallback、Remix 混合启用包。
7. 移除/重装 RaceA 包：
   - 勾选记录进入 orphan 状态；
   - 点击“Forget Unavailable”清理后，配置可持久化重载；
   - 重装包后勾选记录可恢复。
8. 关闭并重开设置窗口，确认选择已持久化。

## 5. 已知限制

- **本地无法实机运行**：本环境只能完成编译与工具门禁验证，无法启动 RimWorld；需要维护者在游戏内跑第 4 节矩阵。
- **本地化**：因本次文件所有权未包含语言 XML，新增 UI 文案暂为代码内英文直写；后续如需多语言应迁入 `1.6/Languages/*/Keyed/UniversalSqueaker.xml`。
- **页面形态**：当前是单页滚动 + 选中域清单，而非左右分栏编辑器；符合最小 UI 范围，但不复刻旧参考的窄屏分步编辑器。
- **HAR 发现**：catalog 的通用 HAR 反射发现仍为 TODO，因此 UI 不投影 HAR hint 行。
- **Xenotype 行为编辑器 / SoundMood / Diagnostics / 浏览器 / 统计**：按 Phase 3 范围全部未实现。

## 6. 验证记录

- `dotnet build Source\UniversalSqueaker\UniversalSqueaker.csproj -c Release`：0 警告 0 错误。
- `dotnet build Source\UniversalSqueaker\UniversalSqueaker.csproj -c Dev`：0 警告 0 错误。
- `dotnet run --project tools\UniversalSqueakerKernelTests -c Release`：通过。
- `dotnet run --project tools\UniversalSqueakerConfigCopyTests -c Release`：通过。
- `dotnet run --project tools\UniversalSqueakerLogTests -c Release`：通过。
- 新代码已人工复核并 grep 确认不含旧品牌、旧前缀、旧诊断关键字。
