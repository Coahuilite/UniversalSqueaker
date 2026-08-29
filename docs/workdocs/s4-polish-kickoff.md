# S4-Polish 开工调度任务书（给下一个会话）

> 状态：待开工（本会话只做设计，不开工）。
> 角色：你是**调度者**，不是编码者。你的工作是按本任务书派发 subagent、验收其提交、维护提交链与工作树干净。编码交给 worker。
> 开工前置：维护者已拍板 **全量换肤（简洁现代 RimWorld 配色组件）+ 原版 fallback 三级（L1/L2/L3）**；规格与依赖见 `docs/s4-polish-plan-zh.md`。

## 1. 先读（按顺序，不要全仓扫描）

1. `HANDOFF.md`
2. `MEMORY.md`（最新 checkpoint）
3. `TODO.md`
4. `docs/s4-polish-plan-zh.md`（本次工作的权威计划）
5. `docs/workdocs/s4-polish-index.md`（本任务书体系索引）
6. 仅当需要核对具体文件时，按索引中列出的文件路径定点阅读。

## 2. 最小 agent 配置（已确定）

| Agent | 类型 | provider / model / effort | 职责 |
|---|---|---|---|
| D-Agent（文档） | `dispatch_subagents` one-shot | qwen-token-plan-cn / deepseek-v4-flash / low（文档任务） | P0 视觉评估稿，一次交付 |
| C-Agent（编码） | `dispatch_subagents` **worker（持久）** | qwen-token-plan-cn / deepseek-v4-flash / max | 串行执行 P纯 → P1 → P2 → P3 → P4 → P5 → P6 → P7 |

- **最小并行为 2 个 subagent**：D-Agent 与 C-Agent 的第一棒（P纯）可并行。P纯只新增纯逻辑文件 + 新测试项目，与 D-Agent 的文档写作无文件冲突。
- **编码 lane 只允许 1 个 worker**：所有代码块都 build 同一个 `UniversalSqueaker.csproj`，且 P1 之后每块都触碰 `UI/*`、`Layout.xml`、`UsWidgetRegistrar.cs`、`FerriteVoicePacksPage.cs` 中的多数文件。并行会产生 obj 争用与文件冲突。不要因为赶进度开第二个编码 agent。
- 总 worker 数：**2 个 subagent（1 文档 + 1 编码）+ 你（调度者）**。每块完成后 C-Agent 继续下一棒（`send_message` 发下一本任务书）；只有 C-Agent 失联/上下文过长时才可新建 worker 并附上已完成块清单。

## 3. 派发顺序与依赖

```text
D-Agent:  P0 视觉评估稿 ──────────────► 维护者批准 ◄── 门禁：P1 不得提前开工
C-Agent:  P纯（纯逻辑脚手架）──────────► 你验收 ──► P1（等 P0 批准）─► P2 ─► P3 ─► P4 ─► P5 ─► P6 ─► P7
```

| 顺序 | 任务书 | 依赖 | 由谁做 | 开工条件 |
|---|---|---|---|---|
| 0a | `s4-polish-p0-visual-spec.md` | 无 | D-Agent | 立即派发 |
| 0b | `s4-polish-pure-logic.md` | 无 | C-Agent | 立即派发（与 0a 并行） |
| 1 | `s4-polish-p1-skin-foundation.md` | P0 批准 + P纯验收通过 | C-Agent | P0 获维护者批准后，先验收 P纯，再发 P1 |
| 2 | `s4-polish-p2-footer.md` | P1 验收通过 | C-Agent | 前一棒通过 |
| 3 | `s4-polish-p3-distance-chart.md` | P2 验收通过 | C-Agent | 前一棒通过 |
| 4 | `s4-polish-p4-filter.md` | P3 验收通过 | C-Agent | 前一棒通过 |
| 5 | `s4-polish-p5-help.md` | P4 验收通过 | C-Agent | 前一棒通过 |
| 6 | `s4-polish-p6-responsive.md` | P5 验收通过 | C-Agent | 前一棒通过 |
| 7 | `s4-polish-p7-fallback.md` | P6 验收通过 | C-Agent | 前一棒通过 |

## 4. 每棒的验收动作（你执行，不要信任 worker 自述）

1. `git status` 工作树干净；`git log -1 --oneline` 是任务书指定的提交信息。
2. `git show --stat HEAD` 与任务书「文件范围」一致，无越界改动。
3. 隐私预检：`git show HEAD | Select-String -Pattern 'PublishedFileId|<home>|/Users/|api[_-]?key|token'` 无命中（不写个人路径/凭据）。
4. 门禁复跑（你亲自跑一遍）：
   - `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev`
   - `dotnet run --project tools/UniversalSqueakerKernelTests -c Release`
   - `pwsh -File scripts/verify-local.ps1`
   - P3 起：`dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release`
5. 通过后记录到 `docs/workdocs/s4-polish-index.md` 的状态列（状态：✅ 完成并提交 <hash>）。

## 5. 维护者批准门

- **P0 批准是硬门**：D-Agent 交付 `docs/ui-visual-modernization-zh.md` 后，你必须请维护者确认（色板/组件库/响应式/fallback 矩阵 6 点）。维护者批准前，**不得**给 C-Agent 发 P1。
- P0 若不批准：把维护者意见作为修订意见发回 D-Agent（同一 one-shot 的后续消息或新 one-shot），改到批准为止；C-Agent 只能停在 P纯完成态等待。

## 6. 派发模板（给 C-Agent 发每一棒）

用 `send_message` 给持久 C-Agent 发下一本任务书时，消息格式固定为：

```text
读 docs/workdocs/<任务书名>，按任务书完成并提交。完成后报告：提交 hash、改动文件清单、门禁结果。
```

不要注入整会话上下文；任务书自包含。

## 7. 禁止事项

- 不要自己写产品代码；只做调度、验收、提交链维护、文档任务书编写。
- 不要让两个编码 worker 并行；不要用 `subagent` 工具跑编码（复用 pro model 太贵）。
- 不要在本阶段修改 `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`。
- 不要 push / 不要配 remote / 不要发布。
- 不要提前开始 P1（P0 未批准）或跳过任何一棒。

## 8. 收尾

- P7 验收通过后，本阶段编码完成。`docs/workdocs/` 的移除不在本阶段做（按 `TODO.md` 顺序随后处理）。
- 把 `docs/workdocs/s4-polish-index.md` 状态全部更新为完成，并在会话收尾时向维护者报告最终提交链。
