# S0 日志协议扩展设计（audio.disabled）

> 状态：定稿（2026-08-25，主会话产出）。
> 范围：S2 的 M2（Disabled 旁路日志）硬前置。

## 1. 事件设计

### 1.1 新事件 audio.disabled

- SqueakLogEvent 枚举新增：`AudioDisabled`（append 到现有 36 项之后，不重排）。
- 字符串键：`audio.disabled`（追加到 EventId switch，SqueakLogProtocol.cs:115-155）。
- 可见性/级别：`SqueakLogVisibility.Daily` + `SqueakLogLevel.Info`（旁路是正常状态，不是错误/警告）。
- 协议版本：`Version = 2`（v2 扩展事件，与 AudioRouteSelected/AudioVanillaFallback 一致）。
- Human 文案：`Squeak audio is disabled (true bypass): <action> not intercepted.`

### 1.2 新增 v2 事件 vs 复用既有事件加 disabled 标记

**决策：新增 v2 事件 `audio.disabled`**（不复用既有事件加标记）。理由：
1. v1 的 28 事件字节面必须不变——复用事件加字段会污染 v1 记录结构。
2. `audio.disabled` 是全新语义（旁路），与 `audio.route.selected`（路由选中）/`audio.dispatch.vanilla_fallback`（回落）正交。
3. Version=2 的既有 v2 事件（AudioRouteSelected 等）都携带 action/target/pack 字段，新增事件沿用同一 v2 字段面，一致性好。

### 1.3 SqueakLogData 字段

- Action 字段复用（记录触发源动作键），其余（sound/tier/pack）留空。
- 无需新增字段；Action 已存在于 SqueakLogData。

## 2. 只打一条 + once 门控

### 2.1 复用 SqueakLogOnce（SqueakLogProtocol.cs:157-181）

SqueakLogOnce.Claim 的 key 构成已含 `log-v<version>` 前缀（:172），fmt=2 事件天然走 v2 域。
audio.disabled 的 once key 构成建议：
```
coahuilite.universalsqueaker|log-v2|AudioDisabled|<action>|<target>|<pack>|<reason>|-
```
- 同一触发源（同 action）只 claim 一次，之后同一 action 触发不再写。
- 不同 action 各 claim 一次（符合「不因同一触发源反复写」的语义）。

### 2.2 短路位置（M6）

在 CompSqueaker 的触发入口（CompTick:263-293 与 NotifyExternal:339-359）**进 TryTrigger 前**统一前置判断：
```csharp
if (SqueakRuntimeResolver.Current.VoicePackMode == SqueakVoicePackMode.Disabled)
{
    // 只打一次（SqueakLogOnce 门控），之后直接 return，不采样、不投影、不写派发日志。
    SqueakLog.AudioDisabled(actionKey);
    return;
}
```
- 短路必须发生在 audio.route.selected / audio.dispatch.vanilla_fallback / TriggerOutcomeSummary 之前。
- `NormalizeMode`（SqueakRuntimeResolver.cs:207）必须让 Disabled 原样穿透（见 S0-a §5）。

## 3. 协议版本扩展规则

### 3.1 v1 冻结面保持

- SqueakLogFormatter 的 v1/v2 分支（SqueakLogProtocol.cs:183-189）不变：v1 只含 28 事件的字节面，新事件只进 v2。
- 新事件 Version=2，不进入 v1 记录。

### 3.2 改动文件清单

| 文件 | 改动 |
|---|---|
| Source/UniversalSqueaker/Logging/SqueakLogProtocol.cs | ① SqueakLogEvent 枚举加 AudioDisabled（append）；② SqueakLogRegistry.Definition switch 加 AudioDisabled 定义（Daily+Info，Version=2）；③ EventId switch 加 `audio.disabled` 键（:115-155）；④ HumanSentence 加 AudioDisabled 分支（若 Action 非空）；⑤ SqueakLog 增加 AudioDisabled 便捷方法（需查证 SqueakLog.cs 现有便捷方法模式） |
| tools/UniversalSqueakerLogTests | 新增 fixture（见 §4） |

## 4. 测试用例清单（tools/UniversalSqueakerLogTests）

1. 事件键：AudioDisabled → `audio.disabled`（EventId 正确）。
2. 版本：AudioDisabled 定义 Version=2，记录走 v2 格式。
3. v1 字节回归：现有 28 事件 v1 字节面不变（新增事件不影响 v1 fixture）。
4. once 门控：同一 action 连续两次触发，只产生一条 audio.disabled（第二次 claim 返回 false）。
5. 旁路日志抑制：Disabled 态下无 audio.route.selected / audio.dispatch.vanilla_fallback / trigger.outcome.summary 记录。
6. Human 文案：`Squeak audio is disabled (true bypass): <action> not intercepted.` 正确渲染。

## 5. 依赖

- 依赖 S0-a（Disabled 枚举 + NormalizeMode 穿透）。
- 依赖 S2（CompSqueaker 触发入口短路 + SqueakLog.AudioDisabled 方法）。
