# S4 派发规划（待设计 → 分块）

> 本文件是 S4 的分解与依赖规划，不是给 worker 的任务书。设计完成后每个子任务另写独立任务书。

## S4 范围（HANDOFF §8 + 计划 §4-§10）

1. A1/A2 调音编辑器（行为 + 心情合并）——依赖 UniversalSqueakerTuningBaselineDef 设计。
2. A6 树形动作作用域开关（TreeRowWidget）——操作已存在的 ActionTuningRecord。
3. A3/A4/A5 + 距离预览图——旧 UI 已部分做，Ferrite 路径未同步。
4. 过滤/帮助/窄屏/美化 + footer build identity。
5. A8 DebugAction 面板 + localizeDebugActions 翻译键。
6. 反馈导入/导出（US.TuningFeedback.v1 契约）。

## 依赖图

- 阻塞点：UniversalSqueakerTuningBaselineDef 的字段集与运行时消费点未锁定（维护者只给了名字）。→ 主会话先设计，再派 A1/A2。
- 不依赖 baseline Def 设计、可并行/串行推进：
  - S4-孤儿同步：A3/A4/A5 + A7 的 Ferrite 路径同步。
  - S4-距离预览图。
  - S4-调试面板：A8 + DebugAction_* / DebugActionCategory_* 键。
  - S4-美化/过滤/帮助/窄屏/footer。
- 依赖 A1/A2 数据面：A6 树形开关 + 反馈导入/导出。

## 分块建议（后续轮次派发，串行避免 obj 争用）

| 块 | 内容 | 依赖 | 文件面 |
| --- | --- | --- | --- |
| S4-Tuning-Backend | UniversalSqueakerTuningBaselineDef + resolver 消费 | 主会话设计 | 新 Def + SqueakRuntimeResolver |
| S4-Tuning-Editor | A1/A2 调音编辑器 UI | S4-Tuning-Backend | UI/* + Ferrite widgets |
| S4-Scope-Tree | A6 TreeRowWidget | ActionTuningRecord（已有） | UI/* + Ferrite widgets |
| S4-Orphan-Sync | A3/A4/A5/A7 Ferrite 同步 + 距离预览 | 无 | UI/* + Layout.xml |
| S4-Debug-Panel | A8 DebugAction 面板 + 翻译键 | 无 | UI/* + 1.6/Languages/** |
| S4-Polish | 过滤/帮助/窄屏/美化/footer | 无 | UI/* + Ferrite core |

## 状态
- [ ] 主会话设计 UniversalSqueakerTuningBaselineDef（下一轮）。
- [ ] 按块写任务书并派发（串行）。
