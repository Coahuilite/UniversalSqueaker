# US 诊断屏幕标记：同一控件用于地图与工作台

依据 WB-7／DSP-US-OFFLINE-WORKBENCH-MARK-A002 r2。组件是 US Kernel 诊断的只读显示，不是 UiKit Tools 的检查器，也不提供 Pawn 点击／选择／锁定／录制入口。

## 沿原诊断 owner 读缓存，再画同一个控件

```text
Root.Update → 原 RefreshIfDue／snapshot／MarkText／MarkColor
AfterMainTabs → 原当前 map／tracked-map 门与 AllPawnsSpawned 遍历
             → 原逐 Pawn try/catch／DiagnosticsMarkDrawFailed
             → CompSqueaker.TryDrawDiagnosticsMark
             → 原 TryGetMark 的缓存 glyph／colour
             → session-owned UsDiagnosticsMarkScreen（仅投影）
             → UsDiagnosticsMarkWidget.PaintAt

MarkPreview 的显式样例屏幕锚点／状态色 → 同一 UsDiagnosticsMarkWidget.PaintAt
```

`TryGetMark`、状态判定和已有 snapshot 刷新节拍保持原 owner。游戏薄适配不读新 snapshot、不重新判断状态、不建 Pawn 列表／索引。原 `AfterMainTabs` patch 的遍历、map gate 和逐 Pawn 异常边界未改变；没有 `PostDraw` UI 调用。

一个诊断 session 最多持有一个 `UsDiagnosticsMarkScreen`，只在第一个非空缓存 mark 绘制时创建。它只有一个绘制执行器、metrics、widget 与主题，没有 Host、UiSession、UiFrame capture、业务 snapshot 或 Pawn／Map 字段。每个 Pawn 只传值类型屏幕数据。`CloseSession` 先摘除并释放绘制资源，再沿原角色／地图／窗口释放；关窗、换图、换 Game 都沿该既有路径收口。关闭诊断后没有新增绘制／采集工作。

## 世界偏移先投影，不当屏幕像素使用

保原头顶世界位置 `DrawPos.x, DrawPos.z + 1.15` 和四向世界偏移 `0.05`。游戏适配把中心及四个原世界点分别交 `Verse.UI.MapToUIPosition`，再将投影后的四个 delta 交给中立控件。没有自行再乘 UIScale，也没有把 `0.05` 当作屏幕描边宽度。

本轮本机 RimSage 的 current/full 数据确为 **1.7.5000 rev1275**；`GenMapUI.DrawText` 与 `UI.MapToUIPosition` 均可见相机投影、除以 Prefs.UIScale 和屏幕 Y 翻转。新适配经 Krafs 1.6.4871 API 编译。这里的实际方法体核对是5000源码；准确1.6投影、缩放、遮挡和游戏输入仍待实机，不以该源码或独立控件绘图代签。

## 一个作者定义，一处绘制实现

生产与 MarkPreview 都嵌入同一 `UI/Layout.DiagnosticsMark.Schema2.xml`，并使用同一 `UsDiagnosticsMarkWidget` 源码。游戏直接配置此 widget／同一样式 resolver 后调用 `PaintAt`；预览由真实 Host 的 widget.Draw 调同一个 `PaintAt`。没有预览专用绘点实现。

定义保原 Tiny glyph、状态色、四次黑描边与一次正文；正文原生 font 仍由当前实际 metrics／drawing 配对解析。就绪／等待的原 RGB 值与受阻 `UsAttention.Brush` 只迁到共用 presentation 数据，Overlay 仍负责选择状态和缓存颜色。没有引入第二个状态机。

| 作者字段 | 含义 |
| --- | --- |
| `Styles` 中 `us-diagnostics-mark` 的 Font／可选 FontSize | 同一字体解析／度量／绘制链；默认仍 Tiny preset |
| `GlyphOffsetX`／`GlyphOffsetY` | 投影后的逻辑 GUI 位移；默认保原文字顶部 `-2` |
| `OutlineScale` | 放大／缩小输入的四个已投影 delta，0 为作者显式关闭描边 |
| `OutlineInk` | 描边颜色，默认原黑色；删去声明恢复默认 |
| `Width`／`Height` | 预览布局声明；实际区域读tree（c1根节点为900×96），不是glyph大小或世界距离，游戏直接屏幕绘制不创建该Host区域 |

`UsDiagnosticsScreenMark` 只含 glyph、颜色、屏幕锚点与四个 delta，没有 Pawn／Map／业务 owner。预览的样例 delta 是显式逻辑 GUI 单位，不模拟相机缩放。

独立 preview 的只读描述提供**实际最后一次 paint**的 glyph rect、含描边 bounds、有效 font 和 painted-frame；未绘制时准确报 not-painted，读取描述不重新度量或绘制。tree的组件区域与实际字体度量后用于绘制的glyph矩形分开；该矩形不冒充像素墨迹包围盒；坐标标为 drawing-local logical。未显示／已删除节点及 scroll／clip 的当前状态仍由 FL 实际树／几何工具提供。

## 消费接缝

MarkPreview 使用独立程序集 `UniversalSqueaker.MarkPreview.dll`，可与 A001 固定 `UniversalSqueaker.Preview.dll` 共存，不覆盖 u1。注册类型为 `UniversalSqueaker.Preview.UsDiagnosticsMarkPreviewProvider`，`Register(UiPreviewContext)` 返回 ready／blocked／pending 三个只读样例注册；旧三参入口保留。

作者文件是 `Layout.DiagnosticsMark.Schema2.xml`；可选独立 `Styles.DiagnosticsMark.xml` 在注册时存在则明确覆盖 inline Styles。外置文档仍由 FL 原 AddProjectedLayout／Attach／watch／last-good 事务拥有。没有作者目录时仅 embedded，不猜路径；watch 必须显式作者目录。修改 C#、游戏嵌入定义或重新配置新 style 文件需重建／重启；工具只负责当前预览热更新，不写真实配置。

## 本轮已证实与待接续

源结构与 net472／neutral 编译已取得。UiLogic 的既有资源清单按新增生产定义精确更新为四个 UI XML 加一份色表，仍是封闭名单与精确数量；没有新增 case 或断言。UI boundary audit 通过，`CompSqueaker.cs` 已无直接 renderer-backend 调用；未清理其原历史白名单。

既有 diagnostics Dev lane 用 p2 Release 时，触及 FER_TOOLS 的显式 Tools 门而失败；该旧场景要求完整 FER_DEV 配对。固定源码保留 FER_DEV 原分支，同一当前 US source 与原 r6 Dev carrier 配对的575 assertions通过。失败记录保留，不将配置不匹配签成新控件缺陷，也不让 US 自动连接 Tools 绕门。

固定c1＋p3准确1.7.5000 rev1275已实际消费。m1首次创建因根OffsetX/Y与FL布局词汇冲突被拒；保失败记录，改为控件专属GlyphOffsetX/Y并另出m2，不覆写m1。m2同一Host4上默认Tiny glyph=60.5,90,7,18、outline=59.5,89,9,20；watch热改GlyphOffsetX=20、Small、OutlineScale=2后glyph=80,90,8,22、outline=78,88,12,26，修复作者副本恢复默认。ready／blocked／pending均实际导出并看图；targets为空，控件无语义动作。坐标是最后实际paint的drawing-local logical，字体为Tiny／Small原生preset，无数值字号成功主张。证据在real-c1-us-c2/evidence，固定工程候选candidate-us-c2。未消费旧p1／p2 native17作原生证据。现有 diagnostics lane 使用 fake source，不触达新地图标记或真实 Pawn。没有新增 case／probe／runner；没有 FPS、地图成本或新控件实际绘制耗时结论。Refactoring UI 检测沿既有 UiKit Schema2／主题／状态色，保原视觉含义，像素核查留真实 renderer。
