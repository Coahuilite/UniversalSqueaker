# US UI 大修遗留 Backlog（非阻塞）

> 来源：`docs/workdocs/us-ui-overhaul-review.md`。
> 状态：待处理。

| ID | 项 | 影响 | 建议时机 |
|---|---|---|---|
| OB-01 | UiKit Surface/Theme 公共 API 收敛（消除 `UsSurface`/`UsVisualTokens` 与 UiKit `Palette`/`SurfaceFrame` 重复） | 高：皮肤双份易漂移，阻碍 UiKit 中性化 | 下次皮肤/框架工作前 |
| OB-02 | `UiLayoutTier.ClampWidth` 接入实际 widget | 中：窄屏工具死代码，未来易复发溢出 | 下次 responsive 工作 |
| OB-03 | 加载侧越界 clamp 显式测试 | 中低：回归盲区 | 下次 Settings/Migration 改动 |
| OB-04 | `VoicePacksLayout` 纯函数测试 | 中：布局回归无保护 | 下次布局调整前 |
