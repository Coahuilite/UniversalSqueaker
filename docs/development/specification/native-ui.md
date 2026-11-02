# 原生UI接缝实现映射

本页是目标版本源码／实现记录，不另持UI规则；规范见[UI合同](../../openspec/specs/ui-consumption/spec.md)。

### 原生入口的窄接缝

目标为 RimWorld 1.6.4871（Assembly-CSharp `1.6.9676.17735`）。`MainMenuDrawer.DoMainMenuControls` 的 transpiler 仅替换两个 `DrawOptionListing(Rect,List<ListableOption>)` 调用中的第一个：在游戏选项列表末项前插入 US，再调用原绘制方法。其余 IL、选项相对顺序和网页链接列表保持原样。调用数不是两个则命名警告并保留原菜单，可用 Mod Settings 恢复。

`Dialog_Options.DoWindowContents` postfix 根据官方类别的原过滤规则，在类别列末尾绘制一个本地入口，采用原版 `Widgets.DrawOptionBackground` 灰色类别背景、US自带透明白色麦克风20×20（左偏10、垂直居中；标签左偏40）、相同留白与原生不可见按钮命中，不用棕金命令按钮；不创建 OptionCategoryDef，不伪装官方内容，不替换选中类别、设置正文或 OK。没有足够空间或私有 `hasModSettings` 接缝缺失时不覆盖原按钮。重复激活使用缓存的 `WindowStack.windows`／`InsertAtCorrectPositionInList`；缺失时保留已有窗口并报告诊断，不以删窗重建掩盖接缝缺口。目标 build 已定点核对：插层保持本层顺序、不越高层，且安排原生排序更新；只有 GetsInput 允许才手动 focus。实际盖层、输入与其他模组交互仍需原生核验。

