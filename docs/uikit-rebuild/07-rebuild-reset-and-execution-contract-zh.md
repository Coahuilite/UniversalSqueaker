# UiKit 重建：事实修订后的执行契约

> 本文件根据第一轮源码审查修订执行顺序。它不规定类名、文件拆分、参数排列或内部算法，只规定目标、边界、证据和不可跳过的门。
>
> 当前裁决：现有 Kernel 是孤立垂直切片，不是可交付内核；现有 US 设置页仍运行旧 UiKit 路径。任何与此事实冲突的旧计划，以本文件为准。
> **修正（2026-09-04，历史证据保留原文）**：本文件 §1 的"两条路径"是调查期事实。现已只有一条：kernel/Schema=2 路径即生产路径，旧 `VoicePacksPage`/`FerriteVoicePacksPage`/旧 `Layout.xml` 链已在 `e25c686` 整体删除；"未接入垂直切片"的表述不再成立。当前事实以 `HANDOFF.md` 与代码为准。

## 1. 当前问题

已有实现同时存在两条不相等的路径：

- 新路径：`FerriteLib.UiKit.Kernel`、Schema=2、`UsKernelSettingsHost` 的未接入垂直切片。
- 生产路径：`UniversalSqueakerMod` → `UniversalSqueakerSettings` → `VoicePacksPage` → `FerriteVoicePacksPage` → 旧 `Layout.xml`、旧 `WidgetRegistry`、旧 `UiInteract`。

因此，以下结果不能互相替代：

- Kernel core fixture 通过 ≠ 完整 US Host 可创建。
- `verify-local.ps1` 全绿 ≠ 新设置页已接入。
- XML 可解析 ≠ binding、Kind、属性和布局语义成立。
- stub 绘制成功 ≠ RimWorld IMGUI、WindowStack、popup、hotControl 和分辨率行为成立。

## 2. 两道不可跳过的交付门

### Gate R：真实 US Consumer Slice

目标：证明 Kernel 能承载一个真实 US 消费者，而不是只承载内联测试 fixture。

必须能观察到：

1. 读取真实嵌入的 Schema2 资源，或等价的真实 US slice 资源。
2. 使用真实 `UniversalSqueakerSettings` 的 getter/setter 或真实业务 action。
3. 由一个实际 Settings Window-owned Host 完成创建、验证、每帧布局和绘制生命周期。
4. binding key、Kind、静态属性、布局尺寸语义在创建期一致。
5. Host 关闭后释放 session，重开得到新 session。
6. 关键控件的异常进入 session 级 fallback，并保留节点 ID、Kind、path 和完整堆栈。
7. stub harness 有针对该真实 slice 的失败敏感测试。

Gate R 未通过前，禁止：

- 把完整 Schema2 页面接入生产设置窗口；
- 删除旧页面、旧事件路径或 fallback；
- 宣称 P1 Kernel 完成；
- 派发全量 US UI 迁移作为并行任务。

### Gate U：US Settings Integration Ready

目标：证明完整 Basic/Tuning/Packs 页面具备迁移条件。

必须能观察到：

- 所有 Schema2 US Kind 都是 Kernel `IUiWidget` 或明确的 Host-owned composite；
- 所有 binding/action 都有类型、所有权和创建期验证；
- XML 的 `Id`、`Bind`、`OptionsBind`、尺寸/比例属性语义唯一；
- 根 Scroll 的 viewport 与 content 尺寸分离；
- core 和 US 关键控件都具备实际 fallback 边界；
- dropdown、stepper/number field、chart、filter、动态列表均有真实消费者路径；
- 旧 deferred dispatch、二次字符串命令桥和重复 UI 状态尚未被新路径继续依赖；
- 自动测试覆盖真实 Schema2/Host 创建，而不是只覆盖内联最小 XML。

Gate U 未通过前，禁止 clean cutover。

## 3. 实现者自主权

DeepSeek 可以自行决定：

- Host、Session、Widget、Composite 的类名和文件布局；
- binding 使用 getter/setter、结果对象或类型化 action；
- layout 内部使用递归树、预编译计划或其他等价实现；
- popup 使用 RimWorld 原生窗口还是 session-owned host；
- 首条 slice 选 banner、volume 或其他能证明真实边界的控件。

但必须守住：

- 原生 IMGUI 是唯一事件权威；
- 不引入全局 deferred queue、第二套事件树或第二套 hot-control；
- XML 不成为业务脚本、循环 DSL 或字符串命令系统；
- 临时状态归 Host-owned session，业务真相归 US；
- 创建期错误可观察，不能拖到 Draw 中途才发现；
- 任何偏离公共契约的选择必须报告事实、影响、替代方案和证据。

## 4. 目标型工作波次

### R0：重新建立契约事实

产物不是代码清单，而是可审查的契约记录：

- 真实 US slice 的输入、输出、生命周期和失败边界；
- 每个真实 binding/action 的类型与业务所有权；
- 尺寸、比例、Scroll viewport、坐标空间的语义；
- Kernel 与旧路径的明确生产调用图；
- 当前证据已证明和未证明的内容。

### R1：完成真实消费者 slice

只实现足以通过 Gate R 的最小范围。若某个已有设计无法承载真实 slice，修改设计和测试，不通过新增 shim 隐藏差距。

### U1：完成 US Settings integration readiness

在 Gate R 之后迁移完整页面。动态业务列表继续由 C# 复合组件负责；XML 只负责结构和静态约束。

### V1：自动与游戏内验证

自动证据和游戏内证据分开记录。真实 RimWorld 不可用时，结果必须标记为“受限”，不得改写为已验收。

### C1：验证后 clean cutover

只有 Gate U、游戏内设置页验证、Overlay 验证和最终审阅都通过，才删除旧路径和仅服务旧路径的 shim。

## 5. 停止条件

遇到以下任一情况，DeepSeek 必须停止扩大范围并回报，而不是继续堆实现：

- 共享契约仍有两个解释；
- 真实消费者无法在创建期通过验证；
- 生产调用图仍没有新 Host；
- 测试只覆盖内联 fixture；
- 一个修复需要恢复全局 deferred dispatch 或弱类型字符串桥；
- layout 结果与实际 Rect/坐标空间无法解释；
- fallback 可能遗留 group、clip、popup 或 hotControl 状态；
- 只能通过 stub 证明而真实游戏证据缺失。

回报格式：

```text
Fact:
Decision:
Affected consumer:
Alternative rejected:
Evidence:
Remaining uncertainty:
Next smallest slice:
```

## 6. 完成定义

本轮重建只有在以下事实全部成立时才可称为完成：

1. 新 Kernel 由真实 US consumer slice 证明可创建、布局、绘制和恢复。
2. 完整 US Settings Host 生产接入并覆盖 Basic/Tuning/Packs。
3. 自动 harness、build、旧路径扫描和契约测试通过。
4. 真实 RimWorld 中的设置页和 Overlay 验收有截图/日志/场景记录。
5. 旧 deferred 事件路径、重复状态、二次命令桥和视觉 shim 已删除，或有明确文件级保留理由。
6. 交接记忆只记录已证实事实和剩余不确定性。
