# 执行代理：UiKit Harness + Review

## 目标
把现有测试改造成新内核的证据路径，并在代码集成前后做结构/安全/契约审阅。测试必须防御可观察行为，不得为了通过而降低门禁。

## 文件 ownership

仅允许修改：

- `tools/FerriteLib.UiKit.Tests/Program.cs`
- `tools/FerriteLib.UiKit.Tests/*Tests.cs`
- 必要时 `tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj`
- `tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs`（仅为新契约更新断言）
- `scripts/verify-local.ps1`（仅门禁描述/新的一致性检查）

不得修改 product source；发现实现缺陷应回传复现、证据和建议，不在测试中掩盖。

## 必须覆盖

- Schema=2 安全解析、ID/kind/property/binding 创建期错误。
- constrained Measure/Arrange、snapshot、hidden、breakpoint、自然文本高度、cache、scroll clamp。
- UiSession 生命周期与跨窗口隔离。
- 原生 IMGUI button/slider/text/dropdown/range graph 的事件和 hotControl 语义。
- UiGuard 日志去重、完整堆栈、槽位 fallback、session 恢复。
- Dark Gold token 与视觉 primitive 的状态恢复和 variant。
- 所有 core Kind 的注册/测量/绘制边界。
- US 纯逻辑、source invariant、Layout.xml well-formedness、neutrality 约束。

## 审阅重点

1. 是否重新引入全局 deferred event queue、layer dispatch、手工坐标世界、第二套 hotControl。
2. 是否存在进程级 session/widget 状态或跨 Window/Overlay 泄漏。
3. XML 是否越权成为业务脚本、字符串命令或循环 DSL。
4. fallback 是否在错误后继续不安全绘制、是否能在新 session 恢复。
5. registry、schema、binding、翻译 key 是否存在静默失败或重复注册。
6. US 旧 shim、旧路径、旧调用点是否全部迁移/删除。

## 验收与自主权

- 测试和审阅目标是证明可观察契约，不是强制某种实现风格。
- 可以为新契约重组测试、增加 focused harness 或选择更合适的断言，只要不降低失败敏感性。
- 默认不运行 formatter、lint、build 或项目级测试，除非主代理授权或需要最小 focused check；回传实际输出、实现文件、风险级别和最小建议。
- 发现实现缺陷应回传证据，不在测试中掩盖。
