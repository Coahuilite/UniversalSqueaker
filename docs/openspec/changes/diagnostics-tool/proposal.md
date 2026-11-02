# Proposal — 玩家可用的有界诊断工具

状态：阶段 A 方案已由 WO-US06-DIAGNOSTICS-TOOL-20261029 r2／DSP A002 r1 采用，并完成本地源码实施。唯一现行合同为 [diagnostics spec](../../specs/diagnostics/spec.md)，本目录保变更依据；技术证据与原生未覆盖项见 [实现状态](../../../development/specification/open-issues.md#d06-玩家诊断状态录制与反馈)。源码基线为 `3e42cc180f91f5f58b040724e4ad76ea060802e6` 加继承的规格重建与 r4 修复。

## Why

现有诊断有真实的选中／视口／锁定会话，却只有 Debug Action 入口、两个 DevMode-only 窗口；设置里的“报告”实际是开发几何报告，不是回应反馈。玩家还不能直接开始一次有界记录并复制反馈。详细回应日志每 Comp 的前 64 条耗尽后可长期沉默，重开诊断窗口不恢复这个预算。

首次决策前，native adapter 的默认 `LastOutcome.Action` 可为 null；诊断文本把它送进业务截止字典。这是 DIAG-01 的实现恢复，不是新能力。现有快照失败还没有读失败／旧值标记，容易把未评估或旧数据误读为当前结论。[源码映射](source-map.md)区分了实际能力与缺口。

## What Changes

**推荐：保留现有窗口阅读，另加显式的单目标记录和复制。** 不用“打开窗口”暗示已在录制，不要求玩家打开详细日志或 DevMode。

- 恢复 DIAG-01：native 读面识别无 owner／尚未评估／真实结果；空动作不查询动作截止，不改 Core 字典接受 null。读失败保最后成功值与失败标记。
- 在现有“诊断与依赖”页加普通“打开回应诊断”入口；复用现有主窗、搜索、选中与锁定，不新增工具页或重排全局 UI。
- 显式开始后只记录点名的一个稳定主体，保最近 64 条已结算结果；满额覆盖最旧记录并计数，观察继续。停止、清空、切对象与退出有明确含义。
- 把已有可选回应日志的终身前缀改为可续期限流：每 Comp 每 60 个真实秒最多尝试输出 64 条；后续结果仍进入相应内存观察，下一时间窗自动恢复。模式本身与不依赖窗口的合法调用保留。
- 增加白名单字段的“复制反馈”，交出实际载入身份、数据阶段、主体、记录范围、精确结果及缺失／覆盖／限流状态；不导出原始日志、姓名、私人路径或整份配置。

## Capabilities

### New Capabilities

无新的独立子系统。现有 `diagnostics` 内新增 DIAG-06（玩家入口与显式记录）和 DIAG-07（有界反馈复制）。

### Modified Capabilities

`diagnostics`：完整替换 DIAG-04 的回应 trace 前缀政策；保留 UI 成本 Enabled 开窗 32 Host-pass、提前关闭 drain／partial、证据分层及其他独立通道。完整候选见 [spec delta](specs/diagnostics/spec.md)。

DIAG-01／02／03／05 不改保证。初态恢复、真实时钟／结果显示和只读边界修复列在 design，不伪写 ADDED。MEMORY 的旧“仅 Debug 入口、设置不加入口”记录，在 DIAG-06 获采用时须同步退休，不能把这次玩家入口称为原有功能。

## Impact

入口到消费者：设置诊断页 → 原 `SqueakDiagnosticsOverlay` → native 已结算结果的只读投影 → 原主／详情窗口及新增复制出口。录制状态、单目标引用和有界记录由现有 Overlay 持有；业务结果仍由原 `PawnResponseAdapter`／`ResponseSession` 持有。

不改 Core、动作资格、机会、随机、选音、发声、冷却、owner 身份、Config／存档格式或 FL。已有公开诊断 source／Host 入口不新增录制前提；新控制通过可选能力接入。详细日志仍可不打开窗口使用。原几何报告与成本通道不改造为反馈系统。

阶段 A 仅交合同／接线方案；随后 CO 已放行并完成[最小实施切片](tasks.md)的本地实现／既有检查和独立技术候选。未操作游戏／真实 clipboard／Config；原生接受仍分开。RavenRace、外部包旧字段、全局 UX 和回退表调音不在本变更。
