# 音频包管理最小可见功能面（旧 UI 淘汰评估）

> 状态：评估稿（2026-08-24）。
> 背景：FerriteLib UiKit 阶段 A 已落地，`VoicePacksPage.UseFerriteUi` 提供新渲染路径但默认关闭。现在把“旧 UI 淘汰”提上日程：先明确玩家要正常使用音频包管理，**至少**哪些功能必须在新 UI 中可见，再把旧路径摘除。

## 1. 当前旧 UI 已提供的可见功能

| 区域 | 可见内容 |
|---|---|
| 标题 / 帮助 | 页面标题、“?”帮助开关、帮助横幅 |
| 状态横幅 | 模式 Off 警告、无任何域提示、Legacy SR 包数量、Biotech 未激活提示 |
| 模式卡 | Off / Fallback / Remix 三选一，立即切换路由模式 |
| Race Layer | 所有 race 域行：显示名、已启用/候选数、状态后缀；点击选中该域 |
| 选中域清单 | 搜索框、包行（名称、来源 Mod · 作者、Actions 覆盖数、启停开关、Legacy SR 标记与 tooltip）、空状态、dormant/target-unavailable/canonical-conflict 横幅、orphan 横幅 + Forget Unavailable 按钮 |
| 页脚 | 自动保存提示 |
| 滚动 | 整页滚动，长清单可滚到底部 |

## 2. 玩家“正常使用音频包管理”的最小可见功能面（红线级）

以下功能缺一不可；缺任何一项，玩家就无法完整管理音频包。

### 2.1 路由模式
- **必须可见并可切换**：Off / Fallback / Remix。
- 说明：这是全局音频策略，玩家必须知道当前模式并能在 UI 中改变。

### 2.2 域浏览
- **Race 域列表必须可见、可点击选中**。
- **Xenotype 域列表必须可见、可点击选中**（当 Biotech 活跃且存在 Xenotype 包时）。
  - 现状缺口：当前旧 UI 与 Ferrite 路径都只渲染 Race Layer；`VoicePacksViewState.XenotypeDomains` 存在于模型但没有任何入口让玩家切换过去。只装 Xenotype 包且同时存在 Race 包时，玩家无法从 UI 管理 Xenotype 包。
- 选中域后，下方清单必须切换到该域的包。

### 2.3 包清单
- 对选中域展示每个可用的 VoicePack：
  - 包名 / Label；
  - 来源 Mod 与作者；
  - Actions 覆盖数（如 `Actions 5/8`）；
  - 当前是否启用（勾选态）；
  - Legacy SR 标记与提示（桥接包需要可辨识）。
- **搜索框必须可见可用**：过滤当前域内的包。
- **启停切换必须可见可点击**：点击行/开关写入选区。

### 2.4 状态与反馈
- 空目录：没有任何域时显示明确空状态（当前有）。
- 模式 Off：明确提示“当前不会路由 VoicePack 音频”（当前有）。
- Legacy SR 包：行内标记 + 横幅计数（当前有）。
- 域级异常状态必须可见：
  - Biotech 未激活 → Xenotype 域 dormant 提示；
  - 目标 Def 未加载 → selections 保留但不可路由提示；
  - 多个 Xenotype Def 同目标 → canonical conflict 提示；
  - 已启用 key 不再存在 → orphan 提示。
- Orphan 清理：**“Forget Unavailable”必须可见可点击**，否则玩家无法清理失效选中记录。

### 2.5 稳定性（非可见但验收红线）
- 设置窗口无候选包/无 Biotech/损坏设置下可打开、可滚动、可关闭、无红字。
- 所有写入仍走既有 `VoicePacksPageModel` → `SetVoicePackSelection` / `CommitVoicePackMode`，不改变 Scribe schema。
- 渲染异常由页面级 try/catch 兜底，显示 EmptyState 而不是崩溃。

## 3. 当前 Ferrite 路径与最小面的差距

| 最小功能 | 旧 UI | Ferrite 路径 | 缺口 |
|---|---|---|---|
| 路由模式三卡 | ✅ | ✅ | 无（模式 Off 的 warning 配色未复刻，非功能缺口） |
| Race 域列表 | ✅ | ✅ | 无 |
| Xenotype 域列表 | ❌ 模型有、UI 无入口 | ❌ 同旧 UI | **需新增** |
| 包清单 / 搜索 / 启停 | ✅ | ✅ | 无 |
| Legacy SR 标记 | ✅ | ✅ | 无 |
| 域状态 / orphan / Forget | ✅ | ✅ | 无 |
| 页面级滚动 | ✅ | ✅ | 无 |
| 外层 SectionFrame / 整体视觉 | ✅ | ⚠️ 未复刻外层强调背景 | 视觉差异，不影响功能 |

真正必须补的只有一项：**Xenotype 域可见入口**。其余差异主要是视觉/打磨。

## 4. 旧 UI 淘汰门禁

只有同时满足以下条件，才允许移除旧 UI（`VoicePacksPage` 的 legacy 分支与不再使用的旧组件）：

1. Ferrite 路径覆盖第 2 节全部最小可见功能面，**并新增 Xenotype 域列表入口**。
2. 在游戏内跑通 `docs/ui-phase3-implementation-notes-zh.md` 第 4 节两族分配矩阵，并追加 Xenotype 分配用例：
   - 至少一个 Race 域 + 一个 Xenotype 域；
   - 能在 Race 与 Xenotype 域之间切换；
   - Xenotype 包只路由到对应 xenotype/race 组合。
3. `UseFerriteUi` 默认改为 `true`，维持一段并行期；确认无回归后删除旧分支。
4. 全部本地门禁（kernel/config/log/UiKit/两个 DLL/中性 grep）保持绿色。

## 5. 建议落地顺序

1. 在 Ferrite 路径补 Xenotype 域列表（可复用 `VoicePacksViewState.XenotypeDomains`，新增 `us/xenotype-layer` 或并入 `us/race-layer`）。
2. 同步补模型入口：`UiCommand.SelectDomain` 已支持 Xenotype，只需 UI 发出对应命令。
3. 实机跑分配矩阵 + Xenotype 用例。
4. 把 `UseFerriteUi` 默认开为 `true`，保留旧分支一版作回退。
5. 一版稳定后删除旧 UI 分支与专用旧组件，更新文档/TODO。