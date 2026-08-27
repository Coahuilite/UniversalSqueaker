# 诊断面板窗口行为：模态 vs 非模态

> 状态：定稿（2026-08-25）。本文档记录诊断面板（`SqueakDiagnosticsPanel`）的窗口行为设计，以及「模态 / 非模态」这对 UI 术语在本项目里的确切含义。
> 用途：后续维护者改面板、或评估「面板是否该改成模态」时，先读本文，避免无意间破坏「边玩边诊断」的核心体验。

## 1. 术语：模态（modal）与非模态（non-modal）

- **模态窗口**：打开后必须先处理它，才能与界面其他部分交互。典型例子是 RimWorld 的确认弹窗——弹出「确定要放弃这个殖民地吗？」时，点不了地图、点不了别的按钮，必须先点「确定 / 取消」把它关掉。
- **非模态窗口**：打开后只是「浮在那里」，**不阻断**你与界面其他部分的交互——可以同时点地图、选 pawn、移动相机、点主菜单。

一句话：模态 = 抢焦点、挡住一切；非模态 = 浮着、不挡。

## 2. 为什么诊断面板必须是非模态

诊断面板的用途是「边玩边观察 pawn 为什么没发声」。如果是模态的，就得不停开关面板才能继续玩游戏，无法实时看门禁变化。

非模态让「游戏照跑、面板常驻、随时点开地图看别的 pawn」成为可能，这正是诊断工具应有的形态。SR 原版诊断面板就是如此设计，本面板照搬其已验证行为。

## 3. 非模态在代码里的具体落点

`SqueakDiagnosticsPanel`（`Window` 子类）通过下列 flag 精确表达「非模态 + 可拖拽」，每一组对应一个具体含义：

| flag | 值 | 含义 |
| --- | --- | --- |
| `forcePause` | `false` | 打开面板不暂停游戏（游戏继续跑） |
| `absorbInputAroundWindow` | `false` | 不吸收窗口周围的鼠标输入（点在面板外，点击穿透到游戏，不被面板吞掉） |
| `preventCameraMotion` | `false` | 打开面板不阻止相机移动（仍可滚屏 / 缩放） |
| `closeOnClickedOutside` | `false` | 点面板外不自动关闭它 |
| `draggable` | `true` | 面板可拖动 |
| `closeOnCancel` | `false` | Esc 不立即关（改用手动「双击 Esc」arming 逻辑） |
| `closeOnAccept` | `false` | 无 accept 按钮关窗语义 |
| `onlyOneOfTypeAllowed` | `true` | 同类型窗口只允许一个实例（避免重复开） |
| `focusWhenOpened` | `false` | 打开时不抢键盘焦点 |
| `onlyDrawInDevMode` | `true` | 仅 DevMode 下绘制（入口本身已由原版 Debug 菜单 DevMode 门控） |

## 4. 与用户两个硬要求的对应

1. **「不能被主菜单拦截，但也不能妨碍主动点击主菜单」** → `absorbInputAroundWindow=false` 使面板不挡主菜单点击；面板自身仍可拖拽、可点关闭（原生 X + 双击 Esc）。
2. **「诊断面板在暂停和游戏运行时都可使用」** → `forcePause=false` 使面板不强迫游戏暂停；无论游戏暂停还是运行，面板照常显示、照常刷新数据。

## 5. 关闭行为

- 原生 X（`doCloseX=true`）直接关闭。
- Esc 采用「双击关闭」：第一次按 Esc 在 3 秒窗口内 arming，第二次按 Esc 才真正关闭（`OnCancelKeyPressed` 里的 arming 状态机）。
- 关闭即 tear down 整个诊断会话（`PreClose` → `SqueakDiagnosticsOverlay.NotifyPanelClosed()`）。

## 6. 何时可以违反「非模态」约定

原则上诊断面板应永远保持非模态。只有当未来出现一个「必须玩家先决策、且决策前不能碰游戏世界」的场景时，才考虑为那个新窗口单独设为模态——而不是改这个诊断面板。
