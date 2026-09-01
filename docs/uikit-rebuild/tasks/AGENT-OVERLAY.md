# 执行代理：Universal Squeaker Overlay Pilot

## 目标
让 Camera Indicator/诊断 Overlay 验证 UiKit 的第二个真实宿主面：不依赖 Settings Window，共享 registry、binding、theme 与 session contract，但不扩展为通用 HUD 管理器。

## 前置

UiKit kernel、core widgets 和 Settings Host contract 已通过主代理 gate；Overlay 代理必须读取冻结接口后再修改。

## 文件 ownership

仅允许修改：

- `Source/UniversalSqueaker/Diagnostics/SqueakDiagnosticsPanel.cs`
- `Source/UniversalSqueaker/Diagnostics/SqueakDiagnosticsOverlay.cs`（仅需时）
- `Source/UniversalSqueaker/Diagnostics/SqueakDebug.cs`（仅需时）
- `Source/UniversalSqueaker/Patches/Patch_GlobalControlsUtility_CameraIndicator.cs`
- `Source/UniversalSqueaker/UI/Widgets/CameraIndicatorWidget.cs`
- 与 Overlay binding/生命周期直接相关的 US settings 代码

不得修改 Settings Host 其他页面文件、UiKit kernel 或全局 Harmony 共享方法。

## 必须实现

1. OverlayHost 可独立创建、显示、隐藏、释放；无 Settings Window 也可运行。
2. 每个 Overlay 实例拥有独立 `UiSession`；不共享设置窗口 session，不留下 hotControl/popup/fallback。
3. 屏幕锚定遵守安全区；验证 800×600、1280×720、1920×1080；可选输入穿透由 native IMGUI 语义决定。
4. 复用同一 XML/binding/theme/registry mechanism；不实现拖动 HUD、世界坐标投影、吸附编辑器或动画。
5. 保留现有纯 Verse 摄像机指示 patch 的安全 map/null 防护；不要把它改成第二套输入框架。

## 验收

- 无 Settings Window 时显示/隐藏/重建 Overlay。
- 地图切换、无当前地图、分辨率/缩放变化、输入穿透均可观察。
- 关闭 Settings Window 不影响 Overlay；关闭/禁用 Overlay 完整清理 session。
- 通过 UiKit stub/build 后，再由主代理执行真实游戏验证。
默认不运行 formatter、lint、build 或项目级测试，除非主代理授权或需要最小 focused check；不要修改同一 Harmony vanilla 方法的其他 patch。