# DeepSeek 任务书：US 设置 UI 重做收口开发

## 任务性质

这是 Universal Squeaker 设置 UI 重做的收口开发任务。执行者负责检查现有工作树、补齐可达缺口并交付代码；主代理只负责任务裁决、最终审查和验收，不在本任务期间代写产品代码。

## 当前事实基线

当前正常生产调用图已经是：

```text
UniversalSqueakerSettingsWindow
  -> UsKernelSettingsHost
  -> embedded Layout.Schema2.xml
  -> UiLayoutManifest / UiHost / UiSession
  -> US Kernel widgets
  -> typed bindings/actions
  -> UniversalSqueakerSettings / VoicePacksPageModel
```

当前已存在并已由本地证据证明：

- Schema2 五工作区：Overview、Distance、Packs、Tuning、Presets。
- 左侧导航、中栏内容、右侧上下文帮助、footer。
- 真实嵌入 Schema2 Host 创建、17 个 US Kind 注册、typed binding/action 表。
- 800×600、1280×720、1920×1080 的 stub layout/完整 frame/scope cleanup。
- rich Tuning/Packs、popup/session 隔离、typed writes 的 Kernel Host harness。
- `dotnet run --project tools/UniversalSqueakerUiLogicTests/UniversalSqueakerUiLogicTests.csproj -c Release`：ALL GREEN。
- `dotnet run --project tools/UniversalSqueakerKernelHostTests/UniversalSqueakerKernelHostTests.csproj -c Release`：ALL PASS。
- `pwsh -NoLogo -NoProfile -File scripts/verify-local.ps1 -NoRestore`：15 门全绿。
- Dev 包已可构建：`dist/dev/UniversalSqueaker-dev-v0.1.0-dev-5f0c522-dirty.zip`。

当前仍明确未由自动证据证明：

- 真实 RimWorld 中 Tuning、Packs、Camera Indicator 的业务交互；
- 真实 popup/chart 热区、滚动时坐标和 hotControl；
- 真实 catalog、翻译和数据路径；
- 800×600、1280×720、1920×1080 的游戏内截图/日志；
- 连续失败后的整页 fallback 与关闭重开恢复；
- Gate U 完成后的旧路径 clean cutover 条件。

## 权威约束

执行前必须读取：

1. `docs/uikit-rebuild/README.md`
2. `docs/uikit-rebuild/01-product-and-architecture-decisions-zh.md`
3. `docs/uikit-rebuild/02-brownfield-cutover-matrix-zh.md`
4. `docs/uikit-rebuild/04-verification-and-acceptance-zh.md`
5. `docs/uikit-rebuild/07-rebuild-reset-and-execution-contract-zh.md`
6. `docs/uikit-rebuild/tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md`
7. `docs/uikit-rebuild/tasks/DEEPSEEK-GATE-REVIEW.md`
8. `MEMORY.md`、`TODO.md`、`HANDOFF.md`

必须守住：

- 原生 `Event.current` / Verse IMGUI 是唯一输入权威；不得恢复全局 deferred queue、第二事件树或第二 hot-control。
- XML 只负责结构、静态属性、有限响应式约束和翻译键；不得增加业务表达式、Repeat、循环或字符串命令 DSL。
- 业务真相归 US settings/model；scroll、popup、focus、drag、编辑缓冲、revision、熔断归 Host-owned `UiSession`。
- 每个 Host/Overlay 独立 session；关闭后不残留 popup、drag、focus、hotControl 或 fallback 状态。
- 所有 Kind、属性、binding/action 在 Host 创建期验证；不得用 `object`、`ToString` 或弱类型桥规避契约。
- 动态列表继续由 C# composite widget 迭代。
- Gate U 游戏内证据完成前，旧 `FerriteVoicePacksPage` / `VanillaVoicePacksPage` fallback 不得删除；不得宣称 clean cutover。
- 不配置 remote、不 push、不发布；不得读取或写入敏感文件。
- 不重置、回滚或覆盖工作树中无关的未提交改动。

## 执行范围

### 1. 事实审计与缺口清单

先检查现有实现，不预设文件缺陷。重点核对：

- `Source/UniversalSqueaker/UI/Layout.Schema2.xml`
- `Source/UniversalSqueaker/UI/UsKernelSettingsHost.cs`
- `Source/UniversalSqueaker/UI/Kernel/*`
- `Source/FerriteLib.UiKit/Kernel/*`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageState.cs`
- `Source/UniversalSqueaker/UI/Model/VoicePacksPageModel.cs`
- `Source/UniversalSqueaker/UI/UniversalSqueakerSettingsWindow.cs`
- `Source/UniversalSqueaker/UI/FerriteVoicePacksPage.cs`
- `Source/UniversalSqueaker/UI/VanillaVoicePacksPage.cs`
- `tools/UniversalSqueakerKernelHostTests/*`
- `tools/UniversalSqueakerUiLogicTests/*`

输出事实清单，区分：已满足、代码缺口、只能由游戏内验证的缺口。发现第一处阻断契约后停止无关扩展，先修最小根因。

### 2. 完成代码层可达缺口

只修实际存在且属于本次重做的缺口。至少覆盖以下类别：

- 五工作区入口与状态切换：默认主题、活动项、滚动定位、帮助主题必须一致；不得残留把新版状态清回旧三页的路径。
- 中栏功能入口：Overview、Distance、Packs、Tuning、Presets 的所有真实能力必须各有一个主要入口；不得新增无实现的按钮；不得遗失 mode、volume、distance preset/chart、scale toggles、camera indicator、filters、domain/checklist、scope、mood、baseline import。
- 右侧帮助：跟随活动工作区和 hover/selection；section/item 文本来自现有 catalog/翻译边界；Measure 与 Draw 高度公式必须一致；窄宽度不得遮挡中栏。
- 调音与预设：layer/domain、action scope、mood slider/number/stepper、Auto clear、baseline expand/race/xenotype/import 的 typed action 必须写回正确业务层；无 Def 时显示诚实空状态，不得伪造 Export。
- Packs：All 清空所有 UI 表示的过滤器；race/xenotype/author/search/dropdown/Forget/selection 的状态、空列表和可用性必须一致；placeholder 只能视觉显示，不能写入业务状态。
- Distance：preset、当前 range、chart hover 与端点拖拽保持一个工作区；端点约束、最小间距和后续 UI 绘制必须安全。
- IMGUI/布局安全：所有 Begin/End 结构成对；native event 只消费一次；popup、scroll、group、clip、hotControl 清理可解释；控件 rect 不越界；Measure/Draw 共用同一几何公式。
- 首帧失败：已有自动重试和完整堆栈日志必须保持；不得通过吞异常、永久禁用新 Host 或无条件进入旧页隐藏问题。
- 旧路径边界：旧实现只能是明确 fallback；如果修复触及旧路径，保持其故障降级职责，不把它重新变成第二个正常实现。

### 3. 只增加必要的 focused evidence

若现有 harness 无法对一个新的可观察契约失败敏感，可以增加最小 focused assertion；不得降低现有断言、删除测试、把 stub 结果写成游戏内验收。

优先验证：

- Schema2 真实资源和所有 Kind 创建期验证；
- 五工作区 × 三视口 measure/draw；
- rich Tuning/Packs 动态列表；
- popup/session 隔离、disposed host；
- 所有 typed writes；
- filter clear、placeholder、dropdown 空值、mood rect、distance graph 边界；
- native scope 深度和异常恢复。

## 非目标

- 不设计通用 HUD 管理器、动画系统、完整键盘导航、第三方稳定 API。
- 不扩展 XML DSL。
- 不引入第二套页面、第二套主题、第二套命令系统。
- 不实现缺少真实 Def 的虚假 preset export 或示例数据。
- 不删除旧 fallback。
- 不进行 Git remote、push、发布或历史重写。
- 不把真实游戏内验证伪装成自动测试证据。

## 验证要求

执行者可以运行必要的局部检查，但不要用格式化、降级断言或大范围重构替代修复。完成前至少提供：

```text
Implemented goal:
Production call graph:
Business ownership:
Contract decisions:
Changed files:
Evidence:
Deleted or retained old paths:
Remaining uncertainty:
Next smallest gate:
```

主代理收口时会独立运行并审查：

```text
pwsh -NoLogo -NoProfile -File scripts/verify-local.ps1 -NoRestore
pwsh -NoLogo -NoProfile -File scripts/build-dev.ps1
```

并检查所有变更 diff、生产入口、旧路径边界、敏感信息和任务书声明。若没有真实 RimWorld 条件，结果必须写为 `LIMITED`，不得写为游戏内 `PASS`。

## 停止条件

出现以下任一项立即停止扩展并回报：

- 公共契约有两种解释；
- 只能通过弱类型字符串桥或全局状态修复；
- 真实 Schema2 Host 创建期无法验证；
- 布局坐标、viewport/content 或 popup 坐标无法解释；
- fallback 可能泄漏 GUI scope、popup 或 hotControl；
- 缺少真实游戏条件却要求宣称游戏内通过；
- 需要删除旧 fallback 才能让新路径通过。

## 外部 DeepSeek 调度补充

DeepSeek 本身负责调度，不必亲自包办全部调查和实现。建议按以下最小分工派发：

1. **UI 几何 agent**：只审查并实现 `UsScopeTreeWidget` 的窄宽 Mood compact/stacked 布局；输出统一的 Measure/Draw 几何方案和控件 rect 规则。
2. **测试 agent**：只补 focused geometry/interaction 测试；必须证明 800 宽三栏下 Pitch/Volume/Jitter、slider、number、minus/plus、Auto 均存在、在 body 内且互不重叠。
3. **契约 reviewer**：只审查变更 diff、typed `set-mood-tuning` 路由、UiSession 所有权、IMGUI 输入唯一性、旧 fallback 未删除；不得改代码。

调度约束：

- UI 几何 agent 与测试 agent 可并行调查，但测试实现必须基于几何 agent 的明确布局契约；共享文件的最终修改由一个 integration agent 顺序合并。
- 所有 agent 跳过 formatter、lint 和项目级全套测试；DeepSeek 在合并后统一运行 focused checks。
- 发现需要改 XML DSL、恢复全局 deferred queue、删除 fallback、扩大任务范围或无法解释坐标空间时，立即停止并回报，不得自行绕过。
- 合并后必须由契约 reviewer 复核，再运行原有 15 门验证和 Dev 构建。
- 最终报告只需说明：改了什么、800 宽布局如何成立、focused evidence、复核结果、真实 RimWorld 剩余不确定性。
