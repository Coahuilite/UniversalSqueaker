# DeepSeek 任务：Kernel 交付目标

## 目标

把 `FerriteLib.UiKit.Kernel` 从“能通过孤立 core fixture 的实现”提升为“能承载真实 US consumer slice 的 Kernel”。交付标准是可观察契约，不是新增文件数量，也不是复刻旧 API。

## 进入条件

- 已读取 `07-rebuild-reset-and-execution-contract-zh.md`。
- 已读取并理解 `DEEPSEEK-REAL-CONSUMER-SLICE.md`。
- 已提交当前源码事实、冲突契约和最小验证方案。

## 必须达到的结果

### 创建期契约

- manifest、Kind、静态属性、binding/action 在创建阶段完成验证；错误带 source、节点 ID、Kind 和 path。
- `Id`、显式 `Bind`/options/action 标识的语义唯一，并被所有 core/US consumer 一致使用。
- 重复 Kind 注册、重复 binding、未知属性和非法属性组合不会静默覆盖或延迟到 Draw。

### 布局与坐标

- fixed、auto、weight/ratio 的语义互不混淆；真实 US 的分栏不会被解释为零点几像素。
- available viewport、natural content、Scroll offset 和绘制坐标空间能在 snapshot 与实际 IMGUI 调用之间闭合。
- Scroll、Clip、Overlay 的结构作用域始终成对清理；异常后后续 UI 不被污染。
- 缓存只依赖真正影响布局的输入，且 widget 实例不携带跨 Host 临时状态。

### 原生交互与恢复

- Button、slider、text、dropdown 使用当前 IMGUI/Verse 原生事件语义，不建立第二事件系统。
- 关键交互控件的 fallback 真正接入 session guard；异常后停止不安全主绘制，后续帧尺寸稳定，重开 Host 可重新尝试。
- range/chart 控件实现所需的 MouseDown→Drag→MouseUp 与 `GUIUtility.hotControl` 生命周期；不使用进程级 hot-control 替身。
- GUI、font、anchor、matrix 及结构作用域在正常和异常路径都恢复。

### 真实消费者证明

- 至少一条真实 US binding/resource/Host 生命周期链路通过，而不是只通过 `Source="test"` 的内联 XML。
- 真实 Settings Window 能拥有并关闭 Host；关闭后 session 清理，重开得到新 session。

## 实现自主权

DeepSeek 自行选择 API 形状、文件拆分、内部算法、兼容期局部组织和 focused slice。不得：

- 恢复全局 deferred queue、旧 `UiInteract` 作为新输入层或字符串命令桥；
- 通过弱类型 object/string conversion 绕过 binding 验证；
- 把 US 业务循环塞进 XML；
- 以增加测试豁免、屏蔽异常或修改断言来制造通过结果；
- 在真实消费者门之前迁移完整 Settings 页面。

## 证据

必须提供：

- 契约决策及被拒绝的替代方案；
- 真实 consumer slice 的生产调用图；
- 每个关键边界的 focused test 和结果；
- 创建期失败、fallback、关闭/重开和坐标布局证据；
- 自动证据可以证明的范围与不能证明的范围。

## 停止条件

以下任一项无法解释时停止扩大实现：

- 同一属性存在两种语义；
- 新 Host 仍没有生产 caller；
- 新页面仍依赖旧 registry 或 deferred dispatcher；
- fallback 不能证明结构/输入状态清理；
- 只剩 stub 证据而没有真实 consumer 证据。

回报格式：

```text
Kernel goal reached:
Contract decisions:
Real consumer:
Production call graph:
Evidence:
Remaining uncertainty:
Next gate:
```
