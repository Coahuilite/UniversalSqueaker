# 主代理任务书：UiKit 重建与 US 设置 UI 重做

## Problem

当前新 Kernel 已有可编译的 isolated slice，但源码审查证明它尚未成为可交付内核：`UsKernelSettingsHost` 没有生产调用方，完整 Schema2 页面包含尚未注册的 US Kind，Host binding 不完整，比例宽度与引擎语义不一致，root Scroll viewport 未闭合，core widget fallback/chart 交互也未形成完整证据。生产设置页仍然使用旧 UiKit 路径。

目标：先让一个真实 US consumer 证明 Kernel 的跨边界契约，再迁移完整 Settings Host，最后做 Overlay 和 clean cutover。不要把孤立夹具扩展成更大的孤立夹具。

## 唯一 owner

主代理负责：事实基线、方案裁决、阶段门、ownership、集成、构建/测试/游戏内验收、clean cutover、HANDOFF/TODO/MEMORY 更新。执行代理只负责被授予的目标范围；不得越过 ownership 修改消费者。

## 权威入口

执行前必须读取：

1. `docs/uikit-rebuild/README.md`
2. `01-product-and-architecture-decisions-zh.md`
3. `02-brownfield-cutover-matrix-zh.md`
4. `03-execution-dag-and-agent-map-zh.md`
5. `04-verification-and-acceptance-zh.md`
6. `05-p0-contract-baseline-zh.md`
7. `07-rebuild-reset-and-execution-contract-zh.md`
8. `tasks/DEEPSEEK-REAL-CONSUMER-SLICE.md`
9. `tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md`
10. `tasks/DEEPSEEK-GATE-REVIEW.md`

## 调度原则

- 先完成 Gate R，再开始全量 US Settings 迁移；Gate R 不是“Kernel core fixture 通过”。
- 任务书只冻结目标、不变量、证据和停止条件；DeepSeek 自主决定类名、文件布局、API 形状和内部算法。
- 同一公共契约只能有一种解释。发现 `Id`/`Bind`、fixed/weight、viewport/content 或 action 类型歧义时，先裁决再实现。
- 不因为旧调用点存在而恢复兼容 shim、全局 deferred queue、二次命令翻译或第二套 hot-control。
- 测试/构建/游戏内验证由主代理统一收口；执行代理只做必要 focused check，并回报证据和限制。
- 不配置 remote、不 push、不发布。

## Phase R：真实消费者切片（串行）

读取并执行 `tasks/DEEPSEEK-REAL-CONSUMER-SLICE.md`。

### Gate R

只有同时具备以下证据才通过：

- 真实 US Schema/resource 被读取；
- 真实 settings getter/setter 或 typed action 被使用；
- 实际 Settings Window 拥有 Host；
- 每帧生命周期和关闭/重开 session 可观察；
- binding/Kind/property/layout 失败在创建期暴露；
- focused harness 覆盖真实 slice；
- 明确列出尚未由游戏内证明的部分。

Gate R 失败时，主代理必须拒绝全量迁移，并给出唯一最小下一步。

## Phase U：US Settings 全量迁移（串行）

读取并执行 `tasks/DEEPSEEK-US-SETTINGS-MIGRATION.md`。

### Gate U

接受标准：完整 Basic/Tuning/Packs 生产调用图成立，所有 Kind/binding/action/属性创建期验证成立，临时状态与业务状态分离，native IMGUI 输入单一，布局/popup/scroll/fallback 可观察，自动证据和游戏内证据分开记录。

Gate U 未通过前，旧路径必须保留为明确 fallback；不得进行 clean cutover。

## Phase O：Overlay

Gate U 通过后才执行现有 `tasks/AGENT-OVERLAY.md`。Overlay 是第二个真实 Host，不是 Settings UI 的并行修补面，也不是通用 HUD 项目。

## Phase C：审查、验证与 clean cutover

使用 `tasks/DEEPSEEK-GATE-REVIEW.md` 输出每道门的 Claim/Evidence/Result/Remaining uncertainty/Next smallest action。最终必须：

1. 运行完整自动门禁和新真实资源/Host regression；
2. 扫描旧 engine、旧 UiInteract、旧 page session、命令桥和视觉 shim；
3. 在真实 RimWorld 完成设置页和 Overlay 验收；
4. 只有验证后删除旧路径和仅服务旧路径的代码；
5. 更新项目记忆，不能把未完成或未实机验证的内容写成完成。

## 失败处理

- 首开异常：保留完整堆栈、节点 ID/Kind/path 和 session 状态，先定位，不以重开有效代替根因。
- 错位/点击无效：检查 snapshot、坐标空间和原生事件消费权，不加盲目偏移。
- fallback 后 GUI 污染：检查所有 group/clip/popup/hotControl 的清理，不只恢复颜色字体。
- 契约不一致：暂停扩展，报告事实、受影响消费者、替代方案和验证方式。

## 交付标准

只有 UiKit 真实消费者、core Kind、US Settings Host、Overlay end-to-end 均有对应自动和游戏内证据，旧路径已清理或有明确保留理由，且项目记忆与实际状态一致，才可称为完成。
