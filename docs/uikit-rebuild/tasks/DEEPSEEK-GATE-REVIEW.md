# DeepSeek 任务：阶段 Gate 审查与证据收口

## 目标

作为每个实现波次的事实审查者，判断当前结果是否达到下一道门。审查对象是可观察行为和生产调用图，不是文件数量、代码长度、类名一致性或测试数量。

## 审查顺序

### Gate R 审查

确认真实 US consumer slice 是否完整闭环：

- 真实资源是否被读取；
- 真实 US binding/action 是否进入 Host；
- 真实 Settings Window 是否创建并拥有 Host；
- 每帧生命周期是否完整且顺序可解释；
- 关闭/重开是否创建独立 session；
- 至少一个失败路径是否有可观察 fallback；
- focused harness 是否测试真实 slice，而不是内联替身。

### Gate U 审查

确认完整 US Settings 是否具备迁移和 cutover 条件：

- Schema2 全部 Kind、属性和 binding 是否创建期验证；
- Basic/Tuning/Packs 是否通过真实业务路径；
- 动态列表是否仍由 C# 负责；
- native IMGUI 输入是否只有一个事件权威；
- popup、scroll、group、clip、hotControl 是否无泄漏；
- 低分辨率和多分辨率 Rect/坐标是否有证据；
- fallback 是否可恢复；
- 旧路径是否已按条件删除，而非形成长期双实现。

### Overlay 审查

只有 Settings Host Gate U 通过后审查 Overlay：

- Overlay 是否不依赖 Settings Window；
- 是否独立拥有 session；
- 是否复用同一 registry/binding/theme contract；
- 是否遵守安全区和输入穿透语义；
- 是否在地图、分辨率和关闭场景清理完整。

## 审查规则

- 先画出当前实际生产调用图，再看目标调用图。
- 任何“尚未接入”的 Host、资源或 widget 都视为未交付，不接受文档声明替代。
- stub/build 只能证明对应的纯逻辑或调用契约；不能证明游戏内 IMGUI。
- 发现一个阻断缺口后，停止扩大审查范围，给出最小修复目标。
- 不要求实现者采用指定类名、文件布局或算法；只判断契约和结果。
- 不允许通过删除测试、放宽断言、屏蔽异常或增加兼容 shim 来消除失败。

## 必须检查的高风险事实

至少检查以下实际问题是否已被解决或明确记录：

- `Id` 与显式 binding 名称是否只有一种语义；
- fixed width 与 weight/ratio 是否不会混淆；
- Scroll viewport 是否与 natural content 分离；
- registry 重复注册是否可观察；
- core/US 关键 widget 是否真正接入 session fallback；
- chart 是否实现原生 MouseDown/Drag/Up 与 hotControl；
- production path 是否不再依赖旧 `UiInteract` deferred dispatch；
- 测试是否覆盖真实 Schema2 resource/Host creation。

## 输出格式

```text
Claim:
Production call graph:
Evidence:
Result: PASS / LIMITED / FAIL
Blocking findings:
Non-blocking findings:
What this gate proves:
What this gate does not prove:
Next smallest action:
```

`PASS` 只表示本 Gate 的范围通过，不表示游戏内最终验收通过。没有真实 RimWorld 条件时，必须使用 `LIMITED`，不得写成游戏内 PASS。
