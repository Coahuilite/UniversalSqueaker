# 0.5.x 设置与诊断 UI 契约

当前事实核对于 2026-10-18。页面功能见 [当前设置](current-behavior-0.5-zh.md)；本文给维护者说明写入、布局和主题边界。

## 一个生产 Host，不保留旧页面 fallback

`UsKernelSettingsHost` 是当前生产设置路径，读取嵌入的 `UI/Layout.Schema2.xml`。相机指示器与 Remix 确认分别有独立 Schema=2 清单。旧 `FerriteVoicePacksPage` / `VanillaVoicePacksPage` 页面链和旧 `Layout.xml` 已删除；历史重建任务书中的“Gate U 前保留旧 UI”不能用于当前开发。

FerriteLib 负责通用 Host/session、布局、原生输入及控件；US 负责自己的模型、类型化 binding、业务命令、翻译与少量业务控件。XML 表达结构、样式和 binding，不表达发声业务脚本。库的内部缺陷应在其归属处修复，不能在 US 复制一套原生输入或布局系统。

## 浏览不等于写入

- Overview 定义目录独立于声音供给。浏览、切换域、搜索、展开、滚动、跳转只改变 UI 状态。
- 包快速启用复用 Packs 的同一 `ToggleVoicePack` 写链；只有准确且唯一的包身份才可快捷启用，不改变路由模式。
- 程序化导航携带准确的 Race/Xenotype 身份，清除挡住目标的筛选，并定位 materialized row；不能为“让页面有内容”改玩家声音选择。
- 首次教学折叠是保存的 UI 偏好 `uiIntroCollapsed`，不写声音设置。全局恢复把它恢复为展开。
- 最终回退表属于 Packs，有独立取消层；Tuning 的上下文退出不能清掉它的浏览状态。

## 类型化值不通过标签往返

动作范围下拉使用 `UiChoice<SqueakActionScope?>`；Auto 的值是真正的 `null`，不是空字符串。提交 `UsScopeWrite`，不把翻译标签或 `ToString()` 再解析成业务值。域下拉同样携带 `UsTuningDomainSelection`，不拼接后拆分域字符串。

预设卡片的 Race/Xenotype 行复用 FerriteLib `UiRowBand.Measure/Draw`。正文点击与复选动作分别绑定自身对象；层级缩进只施加一次，测量与绘制使用相同行高。Xenotype 图标由 Verse 适配器解析，UI Kernel 只接收可空图像；缺 DLC/Def/图像时不制造替代身份。

## 主题只有颜色权限

US 拥有完整角色色表，定义于 `Themes/UsSwatches.cs`；六组为 Core、Royalty、Biotech、Ideology、Anomaly、Odyssey，默认 Core。每组声明当前 24 个颜色角色，不从已退役的 DarkGold 模板继承产品外观。

- 更换 swatch 更新页面主题与公共 chrome，走 `ColourRevision`，不改变字体、密度或 `LayoutRevision`。
- 滚动、焦点、弹层及编辑状态不是主题重置项。
- attention 的语义颜色由 `UsAttention` 单独拥有，不随 swatch 改变。
- `SelectedBorder` 保留未声明，使其遵守强调色回退；透明是显式颜色值，不能被当成缺失。
- 字体旧写法迁移由 FerriteLib 解析器负责，US 不维护重复迁移表。

颜色角色表与主题测试源码是值的权威，本页不再复制一套 HEX 表。

## 诊断应允许边玩边观察

主诊断面板是非模态窗口：不暂停、不阻断面板外鼠标、不阻止摄像机移动、点外部不关闭、可拖动，开启不抢焦点。开发者模式下绘制；双按 Esc 在 3 秒 arming 窗口内关闭。原生事件是否应交给这个窗口由 WindowStack/载体输入边界决定，US 不抢走上层确认框的键。

诊断展示“本次求值”“最近重要结果”“最近派发”时应保持其各自含义。派发不是播放确认；没有新的事件也不是故障。

## 验证不能越级

`tools/UniversalSqueakerKernelHostTests/` 驱动真实资源、生产 source/Host 与控件调用，但底层是受控 stub。纯模型和 Host 测试不能证明原生字体、输入法、游戏窗口栈、完整 mod 列表或真实声音。

2026-10-09 交互检查点已有维护者接受记录；之后 2026-10-14/15 的总览目录、五工作区及三组主题改动不能自动继承该结论。2026-10-15 主题证据仍缺：选中绘制与目标节点的唯一关联、实际编辑 glyph/承托底、焦点与真实相邻底的对比、全部正文对比。真实 1.6 游戏和用户外观偏好也仍待验证。

不要把 UI 的初始窗口尺寸与游戏屏幕下限混为一谈；旧文档里的 800×600 实机基线已废止。布局 probe 只是该尺寸的布局测试。
