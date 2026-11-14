# RW1.6 F09 显式消费 FL0.9 保留展示准备：采用记录

状态：**已采用；r3实现候选待验证**。WO-FL09-RETAINED-DISPLAY-20261111 r2；PM 核定 US `a023fee` 的 UI-10 与 FL `bb7da49` 的显式区域合同（新增接缝 Public-unstable），并裁定下述两项澄清。US 消费 owner：sol-compat；FL 公共机制：sol-tiering；规格核定／源码放行：PM；RW1.6 原生消费验收：Terra。

## Current 与历史

唯一规范正文是 [ui-consumption／UI-10](../../specs/ui-consumption/spec.md#requirement-ui-10-生产-f09-显式保留准备原生操作保持实时)，总入口是 [specification](../../../specification.md)。本目录保留接线设计、采用来源和任务，不持并列 current。

- `a023fee`：原 proposal／ADDED UI-10／design／tasks 候选，源码仍基于 `1ee2ae0`。
- 2026-11-11 r2：PM 正式核定采用；不可变 choices 有区分 value 的独立 snapshot 只读通道，可同 key，纯准备不能回普通 getter；Repaint 仅在有效 clip 为空或全部实际绘制范围被证明／强制裁尽时延期纯 Paint，layout rect 不自动提供证明。输入／native IDs 与不确定 paint 照常。
- 本次采用提交：UI-10 已移入 current，两项澄清、完整来源和旧合同边界并入；[原delta](specs/ui-consumption/spec.md)改为历史指针。产品源码、guard／adapter／构建和运行未动。待 PM 核对两库 current，再发实施。

r3核实FL `8f02776`／US `3e32a57`均先于源码独立采用，现已放行实施；产品变更从上述规格提交之后另行提交，详见[tasks](tasks.md)。

## Why：已有优化与仍欠的工作

同一生产 F09（调音 → 行为与来源 → 征召）已证明，C-r2 暖开把完整布局 Core／Plan／Visible 方法调用从 A 的 658／239／237 降为 26／15／9，展开 Host 仍约 9.962 ms，对 A 9.970 ms 无可分辨改善，完整帧 135/135 超预算。仍每合法同步逐字段验证、准备并逐控件调度，不是再加一个 layout cache 就能解决。原生事实见工单所引 NATIVE-C；0.8 A／C-r2 配对只保历史依据，不作新0.9对照。

端到端目标仍为165 FPS的整帧6.060606 ms。新0.9需在同代开／关固定配对中，分别证明首次完整准备／暖开合、停手后的区域复用、必要原生操作与实际 Repaint；不得只报暖命中而漏首个新状态实画，准备计数减少与整帧达标分别裁定。

## 已选消费边界

- 同一真实工厂、manifest、`b-actions` Repeat 与全部 canonical／贡献 fields。US 闭合生产 source／本窗 sheet 的完整变化来源；不能只看 view/Bump，真实 silent row-result、可变玩家记录／selector 与 raw XML 均覆盖，不要求旧业务 caller 补 Notify。
- FL 独立拥有 editor／draft／focus／IME、scroll／hit 与环境变化；US 不复制其公共 API，只按实际 capability 采用显式来源映射和 participant 分界。新 capability 不可用或不能闭合的需求沿同代原路径。
- 纯准备、必须运行的 native 操作、实际 Repaint 分开；未变的21个现场字段不重复 formatted own/effective／CompactLayout／Measure／展示准备，也不把逐字段成本移到 capture factory。21不是上限。首次展开／滚入／错误／编辑与关闭保持原合法顺序，不跨帧摊账或重放业务请求。
- UI-01至09、CFG／PUB／POL 的原权威不变。[design](design.md)给真实来源、工厂和切线；不另定义 FL 类型、方法或 XML 属性。

## 分支及兼容范围

原独立采用阶段仅 `feat/rw16-fl09-display-snapshot-20261111` 采用 **FL API [0.9.0,0.10.0)／RW1.6 net472**。原 `perf/tiering-20261111`、0.8维护合同／固定DLL与另一net10工程均不迁移。

原 source、direct `ReadActionField`／提交入口、未采用页面、外来 `IUsKernelSettingsSource`／preview／自定义 widget 保原合法调用条件。无能力时回到同代0.9完整路径，不伪造产品开关、菜单或绕 guard。r3实现候选已迁移本分支实际guard／resolver／readiness；可运行性仍由准确载体的工程／原生证据分别建立。后续源码、工程验证、原生性能和发布按 [tasks](tasks.md) 分别记账。

2026-11-14：原r5分支及其1.6证据保持原貌；当前0.6.x范围由[双目标采用](../us06-fl09-dual-target/proposal.md)接续，UI-10仍以上述唯一正文为准。本历史分支限制不作为当前双目标的新限制。
