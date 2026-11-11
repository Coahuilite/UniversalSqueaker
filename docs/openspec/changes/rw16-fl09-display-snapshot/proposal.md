# Proposal — RW1.6 F09 显式消费 FL0.9 保留展示准备

状态：**候选，未采用、未实施。** 工作单 WO-FL09-RETAINED-DISPLAY-20261111／SPEC。US 消费 owner：sol-compat；FL 公共合同与实现 owner：sol-tiering；采用：PM；RW1.6 原生消费验收：Terra。

## Why

同一生产 F09（调音 → 行为与来源 → 征召）已证明，C-r2 暖开把完整布局 Core／Plan／Visible 方法调用从 A 的 658／239／237 降为 26／15／9，展开 Host 仍约 9.962 ms，对 A 9.970 ms 无可分辨改善，完整帧 135/135 超预算。仍每合法同步逐字段验证、准备并逐控件调度，不是再加一个 layout cache 就能解决。原生事实见本工作单引用的 NATIVE-C；过去 0.8 配对只保历史依据。端到端目标仍为 165 FPS 的整帧 6.060606 ms，准备复用成功与整帧达标分别验收。

[UI-04／09](../../specs/ui-consumption/spec.md) 仍要求真实来源、全部字段、原编辑路径与稳定身份。新方向让**显式采用的生产源**一次准备受影响数据／几何，停手后复用；必要的 native input／ID／hit 与 Repaint 提交继续由实际后端运行。增量准备不等于可以不重画 IMGUI。

## What Changes

推荐在同一真实工厂内，只为有完整来源的生产 F09 增加 opt-in 消费路径：

- US 闭合“生产数据＋本窗展示选择”来源：已发布配置／定义／policy 与必要真实可变输入观察，加 source 私有 row-result、sheet 私有状态的自动失效。不是把普通 getter 全部改成 tracked，也不要求旧业务调用者补 Notify。
- FL 闭合独立的 editor／focus／IME／scroll／hit 与环境变化来源。US 数据凭据不能替它们宣称不变。
- 数据／几何准备、原生操作、当前 Repaint 提交分开。未变的 21 个现场字段不逐个重新调用格式化／Effective／CompactLayout／Measure／prepared-description；21 不是字段上限，实际贡献仍完整出现。
- 同域第一展开、滚入、错误、输入与关闭保真实回调顺序；新内容不能借保留旧画面跨帧摊账。旧同步 Draw 和普通来源保完整 fallback。
- 在独立 RW1.6 US 消费分支解决 FL0.9.0 net472 的编译／运行 guard／适配与真实引用，不迁移另一条 net10 US 工程，不改 FL0.8 维护线。

## Capabilities

现行 `ui-consumption` **仅新增 UI-10**，候选见 [delta](specs/ui-consumption/spec.md)。UI-01／04／05／07／09 及原场景保持，CFG／PUB／POL 不迁移权威。
[design](design.md) 是实际来源和接线义务，不定义新的 FL 类型、方法、XML 属性或生命周期 API。公共接口只消费 sol-tiering 经 PM 采用的唯一 FL 合同；届时把真实合同引用填入采用记录。

## Scope and adoption

基底 US `1ee2ae0b9c04a6e233b7e7143f15b6cc0b4a3919`；独立分支 `feat/rw16-fl09-display-snapshot-20261111`。本候选只新增文稿，不改现行规范入口、源码、测试、项目、About 或脚本。

新源只在明确 opt-in 且来源闭合时取得新的读取／准备复用条件。原 source、direct `ReadActionField`／提交入口、未采用页面、外来 `IUsKernelSettingsSource`／preview／自定义 widget 仍用原合法条件。无能力时回到同代 FL0.9 的原完整路径；独立 0.8 分支及固定 DLL 留作恢复，不把换回 0.8 冒充 0.9 off 对照。

采用门：PM 对照 UI-10 与 FL 正式候选闭合来源、native adapter 能力及失败行为，再更新唯一现行 ui-consumption。随后才实施 [tasks](tasks.md)。规格接受、工程编译、真实游戏性能和发布分别记账。
