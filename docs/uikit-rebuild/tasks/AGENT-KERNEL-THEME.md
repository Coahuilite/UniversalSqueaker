# 执行代理：UiKit Theme + Visual Primitives

## 目标
把当前 Dark Gold 视觉决策收敛为 Host 注入、theme-ready 的语义 token；所有 UiKit 原语只消费主题，不拥有输入权威和第二套布局系统。

## 文件 ownership

仅允许修改：

- `Source/FerriteLib.UiKit/Widgets/Palette.cs`
- `UiKitGui.cs`
- `UiKitFonts.cs`
- `SurfaceFrame.cs`
- `UiPanel.cs`
- `UiText.cs`
- `SelectionButton.cs`
- `ModeCardRenderer.cs`
- 必要时新建 `Source/FerriteLib.UiKit/Widgets/UiTheme.cs`

不修改其他 lane 文件或 US 消费者，除非主代理重新分配 ownership。可以新增主题/字体辅助文件，并可选择保留 `Palette` 作为 Dark Gold provider 的兼容内部名字，但不能保留进程级可变主题状态。

## 目标不变量

1. Host 能注入或选择一个 theme；0.1 提供 Dark Gold，未来可替换。
2. 原语读取语义 token，不读取 US shim、不散落硬编码颜色，不拥有第二套布局或输入系统。
3. `SurfaceFrame`、`UiPanel`、`UiText`、`SelectionButton` 的视觉职责保持清晰；输入仍由原生语义负责。
4. GUI 全局状态修改成对恢复。

token 的类型、provider 组织、字体映射、文件拆分和内部兼容方式由实现者选择。重点是可注入、可替换、可观察，不是强制某个 `UiTheme` 类名。

## 验收

- Dark Gold 下原语渲染角色与当前产品决策一致。
- 主题注入可被测试替换；原语不依赖 US 的 `UsSurface`、`UiPalette` 或 `UsVisualTokens`。
- 底部/左侧 accent、danger/open-field variant、字体与颜色恢复有行为证据。
- 回传完整 token 表及需 US 删除的视觉 shim 清单。
默认不运行 formatter、lint、build 或项目级测试，除非主代理授权或需要最小 focused check；不能改 US 调用点来掩盖契约问题。