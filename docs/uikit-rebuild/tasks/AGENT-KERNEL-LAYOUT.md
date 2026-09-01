# 执行代理：UiKit Schema + Constrained Layout

## 目标
把 XML 变成结构、静态属性、翻译键和有限响应式约束的权威；实现受限的 Measure/Arrange 与稳定 `LayoutSnapshot`。不做 CSS、脚本、循环或通用表达式语言。

## 文件 ownership

仅允许修改：

- `Source/FerriteLib.UiKit/Layout/UiElementSpec.cs`
- `Source/FerriteLib.UiKit/Layout/LayoutManifest.cs`
- `Source/FerriteLib.UiKit/Layout/LayoutEngine.cs`
- 必要时新建 `Source/FerriteLib.UiKit/Layout/LayoutSnapshot.cs`

不修改其他 lane 文件或 US 文件，除非主代理重新分配 ownership。可以新建必要的窄接口/辅助文件；可根据真实消费者调整内部节点模型，但跨 lane 契约变化必须回报。

## 目标不变量

1. Schema=2 严格安全解析；重复 ID、未知 Kind/不支持属性等创建期错误必须可观察。
2. XML 能表达既定结构、静态属性和有限响应式约束；不扩展为 CSS、脚本、循环或业务 DSL。
3. Measure/Arrange 产出稳定、可定位、可用于绘制和滚动的 snapshot 等价结果。
4. 缓存只依赖真实布局输入；稳定输入不得无谓重复解析、建树或布局。
5. 保留自然文本高度、scroll clamp 和节点定位等可观察行为；结构 scope 由 Host/容器管理。

具体节点类、容器归一方式、snapshot 集合类型、缓存 key 表达和 breakpoint 表达方式由实现者选择。先用真实 US `Layout.xml` 和测试需求验证设计，再提交实现。

## 验收

- 纯测试覆盖 schema 安全、重复/未知节点、容器组合、fixed/auto/weight、min/max、breakpoint、hidden、自然文本高度、scroll clamp、宽度缓存和 ID 定位。
- 任意 XML schema/binding/容器错误在 Host 创建期可观察，不延迟到 Draw 中途。
- 明确向主代理报告 schema 变更、兼容切换点和所有需要 US `Layout.xml` 原子迁移的字段。
默认不运行 formatter、lint、build 或项目级测试，避免与其他波次互相阻塞；可以运行解析/布局问题所需的最小 focused check，并报告命令与结果。
不得改 US `Layout.xml`，除非主代理重新分配 ownership；需要迁移的字段以变更提案回传。