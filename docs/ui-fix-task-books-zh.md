# US UI 修复任务书（调度版）

> 状态：已完成（T0–T5 全部落地，verify-local 14 门全绿）
> 依据：`docs/ui-fix-triage-plan-zh.md`、`docs/ui-feedback-consolidated-zh.md`
> 目标：先排查后修复，每个全局风险至少一条自动化回归测试；完成后 `scripts/verify-local.ps1` 全绿。

## 总览

| 任务书 | 子代理 | 内容 | 产出 |
|---|---|---|---|
| T0 | UI/UX 行业调研 + 页面布局评估 | 行业最佳实践、当前页面布局评估 | `docs/ui-ux-industry-review-zh.md` |
| T1 | 排查工序执行 | Step1–5 系统性排查 | `docs/ui-fix-triage-report.md` |
| T2 | 修复批次 A | G2 不可用控件 + G3 下拉定位 | 代码 + 回归测试 |
| T3 | 修复批次 B | G1 文本高度/行高 | 代码 + 回归测试 |
| T4 | 修复批次 C | G4 视觉统一（原版/SR 语言） | 代码 + 回归测试 |
| T5 | 修复批次 D | M1/M2 + 低风险反馈 | 代码 + 回归测试 |

## 通用约束

- 不引入 US/SR 产品字面量到 `Source/FerriteLib.UiKit/`；UiKit 必须保持中性。
- 所有修改保持 Dev/Release 0 警告。
- 每次代码修改后运行：
  - `dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev`
  - `dotnet run --project tools/FerriteLib.UiKit.Tests -c Release`
  - `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`
  - 最终 `pwsh -File scripts/verify-local.ps1`
- 不要提交 git；子代理只改工作树，由调度者统一提交。
- 若遇到无法复现/不确定，写入报告而不是猜测。

---

## T0 — UI/UX 行业最佳实践调研 + 页面布局评估

### 背景
- 当前页面：左侧导航 + 中间内容 + 右侧帮助，窗口 60%–75% 浮动，硬下限 800×600。
- 内容分 Basic / Tuning / Packs 三组，宽屏多列，窄屏单列。
- 需要从行业视角评估布局、信息架构、可发现性、可访问性。

### 任务
1. 调研桌面软件/游戏模组设置界面的最佳实践（可引用 RimWorld、其他热门模组、行业通用准则）。
2. 评估当前 US 设置页：
   - 导航层级与内容分组是否合理；
   - 三栏布局在 800×600 / 1080p / 1440p 的可用性；
   - 帮助面板位置、内容粒度；
   - 表单控件一致性、反馈、错误/空状态；
   - 可访问性（字号、对比度、点击目标、键盘/鼠标操作）。
3. 输出具体、可执行的改进建议，标注优先级（P0/P1/P2）。

### 产出
- `docs/ui-ux-industry-review-zh.md`：调研结论 + 当前布局评估 + 改进建议。
- 不修改代码。

---

## T1 — 排查工序执行

### 背景
按 `docs/ui-fix-triage-plan-zh.md` 执行 Step1–5。

### 任务
1. **Step1 G2 交互通路**：枚举 `UiInteract.Button/Row/Protect` 所有注册点，检查每个交互区是否有可见绘制、是否可能被覆盖、滚动内命中是否有效。重点：相机指示器、相机高度预设、Forget Unavailable、Tuning layer、Mood Auto、预设行。
2. **Step2 G3 下拉定位**：复现 Tuning Editor 下拉展开位置错误；检查 `UiInteract.RegisterPopup/DrawPopups` 与 `PushScrollView/PopScrollView` 坐标链路；列出所有 `input/dropdown` 使用点。
3. **Step3 G1 文本高度**：审计硬编码行高（24/26/28/30/74 等）与多行文本；检查 Measure 是否用 `ITextMetrics.CalcHeight`；列出所有可能裁剪位置。
4. **Step4 G4 视觉语言**：收集所有“切换选取项”控件；对照原版 `Widgets` 与 SR `SqueakySettingsUI.cs` 的底部/左侧高亮条；列出替换清单。
5. **Step5 输出批次**：把问题映射到 A/B/C/D 批次，给每个全局风险写至少一条回归测试建议。

### 产出
- `docs/ui-fix-triage-report.md`：问题清单（文件/行/原因/建议）、回归测试清单、批次映射。
- 不修改业务代码；可只改测试/文档。

---

## T2 — 修复批次 A：G2 不可用控件 + G3 下拉定位

### 已知根因（供参考，需验证）
- G3：`DropdownWidget` 的 popup 闭包捕获的是内容局部坐标；`DrawPopups()` 在 `PopScrollView()` 之后执行，`UiInteract` 的 `ScrollTransforms` 已清空，导致 popup 的按钮/绘制坐标未从内容坐标转换到页面坐标。
  - 建议：在 `UiInteract` 暴露 `ToPageSpace(Rect)`（internal 或 public），`DropdownWidget` 在 `RegisterPopup` 前把 `fieldRect`/列表坐标转换为页面坐标，闭包内直接用页面坐标。
  - 回归测试：模拟 `PushScrollView(outRect, scroll)` 后绘制 dropdown、打开、再 `DrawPopups`，验证选项行按钮注册到页面坐标（点击选项可命中）。
- G2：需要从 T1 报告确认具体不可用控件；常见根因是只注册 `UiInteract.Button` 但未绘制可见表面，或按钮被后续 surface 覆盖，或行高不足导致命中区过小。

### 任务
1. 修复 G3：`UiInteract.ToPageSpace` + `DropdownWidget` popup 坐标修正；补回归测试。
2. 按 T1 报告修复 G2 清单中所有确认的不可用/不可见/不可点击控件。
3. 为每个修复补回归测试或可自动检查的门禁（如交互注册点必须有对应绘制断言）。

### 验收
- Tuning Editor domain/scope 下拉在滚动视图内于触发框正下方展开并可点击。
- 相机指示器、相机高度预设、Forget Unavailable、Tuning layer、Mood Auto 等确认控件可点且视觉可发现。
- `verify-local.ps1` 全绿。

---

## T3 — 修复批次 B：G1 文本高度/行高

### 已知问题
- `BasicTuningWidget` 两行文本放 26/28 高度内，子标签可能截断。
- `VoicePackRow` 74 高内放 3 行文本（Label 25 + mod/author 20 + coverage 20），不同字体/字号可能裁剪。
- 多处硬编码行高未用 `ITextMetrics.CalcHeight`。

### 任务
1. 审计所有 US widget 的 Measure/Draw 行高，对多行文本改用 `ITextMetrics.CalcHeight` 或至少保证 `rect.height >= 文本实际高度`。
2. 统一“行高基线”：单行 24–26、双行 ≥40、三行 ≥64；卡片/行内部 padding 一致。
3. 修复确认的裁剪点（Basic toggles、VoicePackRow、帮助面板、Mood 行、预设描述等）。
4. 补回归测试：文本换行时 Measure 高度 >= `CalcHeight`。

### 验收
- 800×600 下三栏无文本下半截断。
- `verify-local.ps1` 全绿。

---

## T4 — 修复批次 C：G4 视觉统一（原版/SR 语言）

### 背景
- SR 证据：`../squeaky_ratkin/Source/SqueakyRatkin/UI/SqueakySettingsUI.cs`
  - Primary/Danger 按钮底部金色/红色条 `rect.yMax - 3f`；
  - SettingSelector 左侧金色条 `rect.x+1, rect.y+1, 3, height-2`；
  - SelectableCard 选中底部金色条。
- 当前自定义按钮（`UsSurface.DrawSegment` 等）被维护者认为不如原版。

### 任务
1. 在 UiKit 层新增“原版语言包装”的选择/切换控件（或升级现有 `InputModeRowWidget`/`ModeCardRenderer`），统一选中态（底部/左侧高亮条）。
2. 替换 US 侧所有“切换选取项”：模式卡、Tuning layer segment、Action scope 下拉触发框、筛选 chip、预设按钮等。
3. 保持中性：UiKit 组件不得含 US/SR 文案；颜色/条样式作为 Palette/token 暴露。
4. 补视觉回归测试（至少检查选中态绘制调用或交互注册不变）。

### 验收
- 模式卡、Tuning layer、筛选/预设按钮选中态有清晰的原版/SR 高亮条。
- `verify-local.ps1` 全绿。

---

## T5 — 修复批次 D：中/低风险反馈

### 范围
- M1：Tuning Editor 的 Tuning Layer sticky/常驻。
- M2：Action Scope 分组（自主行为/可操作行为）+ 隐藏 Biotech 防御性动作（哭泣、咯咯笑）。
- 低风险：
  - 包管理 race/xenotype 并列 + 选择 race 自动筛选 xeno；
  - 作者筛选下拉；
  - 未列出 xenotype 黯淡显示（“无可用包”而非“不支持”）；
  - 衰减图可拖节点包边/悬停高亮；
  - Tuning Editor 高度协调。

### 任务
按 T1 报告和反馈文档逐项修复；每项至少一条自动化断言（纯逻辑可抽到 Kernel/Pure 或 UI 布局测试）。

### 验收
- 反馈清单中对应项标记已修复。
- `verify-local.ps1` 全绿。
