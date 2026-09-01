# DeepSeek 任务：真实 US Consumer Slice

## 目标

把现有孤立 Kernel slice 变成一条可被事实证明的真实 US UI 链路。交付目标是“一个真实消费者从资源到 IMGUI 的闭环”，不是提前实现整页，也不是照着文件清单拼装对象。

## 进入条件

- 已读取 `07-rebuild-reset-and-execution-contract-zh.md`、`04-verification-and-acceptance-zh.md`、`05-p0-contract-baseline-zh.md`。
- 已确认当前生产设置路径和新 Kernel 路径的差异。
- 已提交短方案，说明选择的真实 slice、状态所有权、binding/action 语义、Host 生命周期和证据路径。

## 目标结果

至少一条真实链路必须成立：

```text
真实 US layout/resource
  -> Kernel manifest/registry validation
  -> 真实 US settings binding/action
  -> Settings Window-owned Host
  -> BeginFrame -> Measure/Arrange -> Draw -> EndFrame
  -> session cleanup/reopen
  -> stub regression evidence
```

DeepSeek 自行选择 banner、global volume 或其他最小真实消费者。选择标准不是实现最少，而是能暴露真实跨边界问题。

## 必须解决的边界

- 新 US Kind 必须进入 Kernel 的 widget contract；不得把旧 `WidgetRegistry` 当作新 Kernel 的注册证明。
- `Id`、`Bind`、`OptionsBind` 的职责必须有唯一解释，并被实现、资源和测试共同使用。
- 布局宽度、weight/ratio、available viewport、content rect 和坐标空间必须能从 snapshot 和实际 Draw 路径解释。
- Host 必须由真实设置窗口拥有；不得只在测试中 `new UiHost`。
- 关闭 Host 必须释放 session；重开必须获得新 session。
- 关键绘制失败必须有稳定 fallback 或明确的整页诊断路径。

## 非目标

- 不迁移 Basic/Tuning/Packs 全部页面。
- 不删除旧 `Layout.xml`、旧 `UiInteract`、旧命令桥或旧 fallback。
- 不实现 XML 循环、条件、业务脚本或弱类型字符串 DSL。
- 不为了通过测试添加只在测试存在的假 Host、假资源或假 binding。
- 不修改 Overlay；Overlay 等待 Settings Host contract 真实闭环。

## 证据要求

交付时必须报告：

- 实际修改的文件和 ownership 边界；
- 选择的 slice 及为什么它能代表真实跨边界问题；
- 创建期成功或失败的节点 ID、Kind、path；
- 真实资源读取证据；
- binding/action 类型和业务所有权；
- Host 创建、绘制、关闭、重开证据；
- focused harness 的具体命令和结果；
- 尚未由真实 RimWorld 证明的内容。

## 停止与回报

如果无法完成，停止扩大范围，返回：

```text
Fact:
Blocked boundary:
Smallest failing consumer:
Decision needed:
Evidence:
Remaining uncertainty:
```

不得用“先做完整页面再统一修”作为替代方案。通过本任务后，主代理才可以开启 `DEEPSEEK-US-SETTINGS-MIGRATION.md`。
