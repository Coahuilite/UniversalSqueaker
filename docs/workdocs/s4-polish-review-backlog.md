# S4-Polish Review Backlog（非 draw-order）

> 状态：待处理。
> 来源：`docs/review/review-s4-01..06`。
> 排除项：**help 绘制顺序 / help 可见性**已移交 `docs/workdocs/uikit-draw-order-handoff.md`，不在此 backlog。
> 处理方式：后续 fix round 由编码 worker 串行修复；每项完成后更新状态。

## Must Fix（Major，建议优先）

| ID | 功能区 | 问题 | 来源 |
|---|---|---|---|
| B-01 | Layout | ScopeTree 窄屏 stacked 层按钮 Measure/Draw 高度不一致，后续内容重叠、滚动高度低估 | R2/R4 |
| B-02 | Layout | ScopeTree DomainRow/ScopeRow 固定 96px 按钮，内容区过窄时负坐标/越界 | R4 |
| B-03 | Layout | FilterBar 窄屏横向溢出；`UiLayoutTier.ClampWidth` 未接入 | R4 |
| B-04 | Layout | 页面级窄屏 guard 使用窗口宽度而非扣除导航后的内容宽度 | R4 |
| B-05 | Fallback | `UsGuard.MeasureOrFallback` 的 lambda 只包住已算好的返回值，未保护真实测量逻辑 | R5 |
| B-06 | Fallback | 多个 US widget 与 FerriteLib 内置 widget 的 Measure 完全无防护 | R5 |
| B-07 | Fallback | `FerriteGuard.ResetSessionLog()` 从未被调用，Ferrite 内置 fallback 日志不是会话级 once | R3/R5 |
| B-08 | Fallback | 外层 catch 持续故障时每帧重复 `Log.Warning`，无 per-session 节流 | R5 |
| B-09 | Skin | 左导航 `DrawNav` 仍用旧 `UiPalette`/`SectionFrame` 和硬编码 `new Color`，active 态不符合 P0 | R3 |
| B-10 | Tests | `UsGuard`/`FerriteGuard` 注入异常测试缺失 | R6 |
| B-11 | Tests | `AttenuationEditorWidget` 拖拽/纯函数未提取测试 | R6 |
| B-12 | Tests | `LayoutEngine` 宽度变化后 Draw 缓存失效回归测试缺失 | R6 |

## Should Fix（Minor）

| ID | 功能区 | 问题 | 来源 |
|---|---|---|---|
| B-20 | Settings | `SetGlobalVolume`/`SetDistanceRange` 命令层未防御 NaN/Infinity | R1 |
| B-21 | Settings | 新增测试未覆盖“0% 不短路”与运行时副作用；`QueuePersistence` 未断言 | R1 |
| B-22 | Settings | `SetGlobalVolume`/`SetDistanceRange` 无 no-op 守卫 | R1 |
| B-23 | Settings | `SetGlobalVolume` 与 `NotifyCheapRuntimeChanged` 重复维护同一静态写入，建议提取 helper | R1 |
| B-24 | Layout | footer 高度常量不统一（28/24/26），`UsFooterWidget.Measure` 未参与布局 | R2 |
| B-25 | Layout | 切换页签不清空滚动位置，新页签可能停在旧滚动位置 | R2 |
| B-26 | Layout | `Layout.xml` footer 是死声明，与手工 `DrawFooter` 不一致 | R2 |
| B-27 | Layout | 左导航命令未走 `UsWidgetCommandAdapter`，存在两条命令路径 | R2 |
| B-28 | Skin | 现代复选框 checked 边框应为 `AccentGold`，当前为 `BorderStrong` | R3 |
| B-29 | Skin | Help `?` hover 态被画成 active 态，无法区分 | R3 |
| B-30 | Skin | ScopeTree 段按钮/mood Auto 清除钮未按 P0 danger/focus 状态绘制 | R3 |
| B-31 | Skin | SearchField 表面用了 Base 而非 Panel | R3 |
| B-32 | Skin | PresetList 仍用原版 Checkbox 和 `Color.white`，未完全令牌化 | R3 |
| B-33 | Skin | `UsSurface.DrawSegment`/`DrawHeader` 未恢复调用前 `Text.Anchor` | R3 |
| B-34 | Skin | Diagnostics 面板残留硬编码颜色，或需明确 grep 范围 | R3 |
| B-35 | Skin | 页面级 catch 进入 Vanilla 前未恢复 GUI 状态 | R3/R5 |
| B-36 | Features | FilterBar “All” 不清除作者过滤，All 选中态忽略作者过滤 | R4 |
| B-37 | Features | 帮助 banner 的 Measure 与 Draw 使用不同内容宽度 | R4 |
| B-38 | Features | AttenuationEditor 窄屏下 help 展开不绘制 banner，只留空白 | R4 |
| B-39 | Features | AttenuationEditor 拖拽状态是 static，未在会话/页签生命周期重置 | R4 |
| B-40 | Features | `ResolveSelectedDomain` fallback 时未同步 `SelectedScope` | R4 |
| B-41 | Features | `UiPackFilter.EnabledOnly`/`UiDomainFilter.Author` 是死 API，需清理或补 UI | R4 |
| B-42 | Features | `UiLayoutTier.ClampWidth` 只有测试调用，未接入 widget | R4 |
| B-43 | Features | 衰减拖拽过程中状态行仍显示旧 preset 名 | R4 |
| B-44 | Fallback | Ferrite 与 Vanilla 使用独立 `VoicePacksPageState`，切换兜底/恢复时状态不一致 | R5 |
| B-45 | Fallback | ScopeTree/PresetList 的 Draw fallback 只是 unavailable 文本，需明确验收口径或补可操作 fallback | R5 |
| B-46 | Fallback | VanillaVoicePacksPage 滚动坐标依赖 `inRect.x==0` | R5 |
| B-47 | Fallback | FerriteLib 内置信息组件没有 Draw/Measure fallback | R5 |
| B-48 | Fallback | `EmptyState.Draw` 未恢复 `Text.Font` | R5 |
| B-49 | Fallback | `VanillaVoicePacksPage` 在 settings==null 时静默返回空白 | R5 |
| B-50 | Tests | distance range clamp 断言未锁精确值 | R6 |
| B-51 | Tests | 持久化加载侧越界 clamp 未测试 | R6 |
| B-52 | Tests | SettingsMigration 测试共享静态全局状态、固定临时文件路径 | R6 |
| B-53 | Tests | `verify-local.ps1` 的 `dotnet run` 未传 `--no-restore`；测试项目无 warnings-as-errors | R6 |
| B-54 | Tests | 第 14 门名称过时（仍写 filters + distance preview） | R6 |
| B-55 | Tests | Vanilla 页不做自动单测的说明未沉淀到测试/门禁侧 | R6 |
| B-56 | Tests | `VoicePacksLayout` 零 Verse 纯函数未测试 | R6 |

## Accept / Record（Nit / 已知语义）

| ID | 功能区 | 问题 | 建议 |
|---|---|---|---|
| B-60 | Settings | 已激活 Sustained 声音不即时应用新全局音量 | 记录为“下一次触发时生效”，不阻塞 |
| B-61 | Tests | `verify-local.ps1` 成功后临时日志未清理 | 顺手修 |
| B-62 | Tests | neutrality grep retry 提示不准确 | 顺手修 |
| B-63 | Tests | FerriteLib 测试未捕获异常后静默跳过剩余用例 | 顺手修 |
