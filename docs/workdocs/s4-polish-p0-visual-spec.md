# S4-Polish P0 — 视觉现代化评估稿（文档任务书）

> 状态：已交付，待按新需求修订并请维护者批准。执行者：D-Agent（one-shot）。不开工代码，只产出评估稿。
> 依赖：无。并行：可与 `s4-polish-pure-logic.md` 并行。

## 任务

产出一份维护者可批准的视觉现代化评估稿：`docs/ui-visual-modernization-zh.md`。**不修改任何代码。**

## 先读

- `docs/s4-polish-plan-zh.md`（权威计划；§1.3 S4-Vol、§1 目标、§2 现状、§3.5 视觉边界、§4 P0、§5 D1–D6、§10 fallback 矩阵）
- `Source/UniversalSqueaker/UI/Layout.xml`
- `Source/UniversalSqueaker/UI/Components/*.cs`
- `Source/UniversalSqueaker/UI/Widgets/*.cs`
- `Source/FerriteLib.UiKit/Widgets/*.cs`
- 不要读其它 workdocs，不要读其它 docs 长篇。

## 产出文档必须包含（6 点）

1. **现状盘点**：US 侧与 FerriteLib 侧的现有绘制方式、调色板、组件清单、问题清单（写实，不含修复方案）。
2. **现代简洁设计语言（全量换肤规格）**：
   - 色板令牌：给出**具体 RGBA 值**。基线（可微调但必须给值）：
     - Base `(0.10,0.10,0.10)`、Panel `(0.14,0.14,0.14)`、Raised `(0.18,0.18,0.18)`、Hover `(0.22,0.22,0.22)`、Selected `(0.20,0.17,0.10)`、AccentGold `(0.92,0.68,0.30)`、TextPrimary `(0.92,0.92,0.90)`、TextSecondary `(0.65,0.65,0.62)`、Danger `(0.55,0.18,0.15)`、Success `(0.16,0.35,0.22)`、Border `(0.30,0.30,0.28)`、BorderStrong `(0.45,0.45,0.42)`。
   - 扁平、1px 边框、无渐变/圆角/贴图；间距 2/4/6/8/10；行高 S=22、M=26、L=32、XL=50。
   - 选中态统一为**左侧 4px AccentGold 竖条**（行）+ **底部 3px AccentGold 条**（卡片）；hover 只提亮表面；danger 用 Danger 底 + 1px Danger 边框；focus 用 BorderStrong。
3. **自绘组件库规格**：现代行、segmented button、modern checkbox（18px 方框 + 金勾）、mode card、text field 皮肤（外壳包 `Widgets.TextField`）、banner、footer 双槽、全局音量 slider、可拖拽衰减编辑器（横轴相机高度 15–65、纵轴 0–100、两点 y 锁 100%/0%）、help `?`。每个组件写清：表面/边框/文本三态（normal/hover/selected）与最小尺寸。
4. **响应式三档规则**：Comfortable ≥480 / Compact 320–479 / Minimal <320；<240 页面级「Window too narrow」。逐组件给出降级行为。
5. **原版 fallback 矩阵**：照抄并细化 `docs/s4-polish-plan-zh.md` §10 的 L1/L2/L3 表，明确每个自绘组件 → 原版对应物。
6. **维护者决策摘要**：列出 D1–D6 及本文档对每项的落点（D1 已拍板全量换肤，D2/D3/D4/D6 按计划推荐，D5 不在 S4 范围）。

## 验收

- 文件存在且覆盖 6 点；色板值、组件规格可直接被 P1 实现引用，不留「待定」。
- 全文不含 `PublishedFileId`、个人路径、凭据。
- `git status` 只新增该文档；不提交（调度者决定是否提交）。

## 提交

不提交。交付后由调度者请维护者批准；批准后 P1 才可派发。
