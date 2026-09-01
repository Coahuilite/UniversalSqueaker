# DeepSeek 任务：US Settings 全量迁移

## 目标

将 Universal Squeaker 的 Basic、Tuning、Packs 设置页收敛为一个由新 UiKit Kernel 驱动的 Settings Host，同时保留 US 业务语义和动态列表能力。目标是完成一条单一生产调用图，不是同时维护两套 UI，也不是逐文件复刻旧页面。

## 进入条件

只有在主代理明确记录 Gate R 通过后才能开始。Gate R 必须已经证明：真实 US consumer slice 可由真实资源创建，能进入实际 Settings Window-owned Host 生命周期，并有自动化证据。

若 Gate R 未通过，本任务保持阻塞；不得以已有 Kernel fixture、编译成功或文档状态解除阻塞。

## 目标结果

生产调用图最终应满足：

```text
Settings Window
  -> one Settings Host
  -> one session
  -> one validated manifest/component tree
  -> synchronous native IMGUI draw/input
  -> US typed bindings/actions/model
```

Basic、Tuning、Packs 的可观察功能必须保留：

- Basic：模式、音量、距离 preset、开关、camera indicator 入口；
- Tuning：层/域选择、scope、mood、stepper/slider、baseline/preset；
- Packs：race/xenotype/author filter、下拉、导入/忘记、voicepack selection；
- 衰减图：hover 与 MouseDown→Drag→MouseUp 修改值；
- 帮助、滚动、响应式布局、翻译和 fallback。

## 必须守住的边界

- US 持有业务真相；UiKit session 只持有 scroll、popup、focus、drag、编辑缓冲和 fallback 等临时状态。
- 动态列表由 C# composite widget 迭代；XML 不增加 Repeat、条件、表达式或业务脚本。
- 每个 Kind 的 binding/action 类型、缺失行为和业务 owner 必须明确；不得通过 `object`、`ToString` 或字符串命令绕过验证。
- 同一个 `OnGUI` 事件只由原生 IMGUI/Verse 语义消费一次；不得保留旧 deferred dispatch 作为新路径的输入层。
- popup、scroll、clip、group 的坐标和 Begin/End 生命周期必须由 Host/结构容器负责；叶控件不得建立自由结构 scope。
- 关键控件错误只熔断当前 session 槽位；schema、binding、容器错误必须在创建期进入整页诊断/fallback。
- 关闭设置窗口后不存在旧 session 的 popup、drag、focus、hotControl 或 fallback 污染。

## 实现自主权

DeepSeek 自行决定：

- Host 和 composite 的类/文件拆分；
- typed setter、typed action 或结果对象的局部形状；
- US 页面分区如何映射到 manifest/component tree；
- popup 使用原生 FloatMenu/WindowStack 还是 session-owned host；
- 迁移顺序，只要先完成可验证的窄切片再扩展。

不得自行改变：

- IMGUI 单一事件权威；
- session 与业务状态所有权；
- XML 不成为业务 DSL；
- 无全局 deferred queue/第二套 hot-control；
- 新路径通过 Gate U 前不删除旧 fallback。

## 非目标

- 不实现通用 HUD 管理器、动画系统、完整键盘导航或第三方稳定 API。
- 不把旧页面包装成“兼容适配器”长期保留。
- 不在没有游戏内证据时删除旧路径。
- 不通过扩大范围来掩盖某个 widget、binding 或 layout 契约失败。

## Gate U 交付条件

主代理只有在以下结果全部有证据后才能接受迁移：

1. 完整 Schema2 页面可在创建期验证；所有 US Kind 可解析。
2. 所有 binding/action 覆盖实际消费者，并且类型错误在创建期失败。
3. Settings Window 只拥有一个 Host/session；关闭重开隔离成立。
4. 800×600、1280×720、1920×1080 的布局、popup、滚动和热区可解释。
5. Basic/Tuning/Packs 所有高风险业务路径可操作，值写入正确业务层级。
6. chart 拖拽、dropdown、stepper、filter、动态列表和 fallback 有自动证据。
7. 真实 RimWorld 设置页有截图/日志/操作记录；stub 结果单独标记。
8. 旧事件队列、二次命令桥、重复 page session 和重复视觉层已删除，或留下文件级理由。

## 回报格式

```text
Implemented goal:
Production call graph:
Business ownership:
Contract decisions:
Evidence:
Deleted or retained old paths:
Remaining uncertainty:
Next smallest gate:
```
