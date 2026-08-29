# S4-Polish Post-Development Review Plan

> 状态：已派发。每个 reviewer 独立只读代码，写报告到 `docs/review/`，不修改源码、不提交。
> 基线与待审范围：S4-Polish UI 链完成后（HEAD 到 `bf7342f`）。

## Review 分区

| # | 功能区 | 报告文件 | 状态 |
|---|---|---|---|
| R1 | Settings / Runtime | `docs/review/review-s4-01-settings-runtime.md` | 已派发 |
| R2 | Layout / Navigation / Footer | `docs/review/review-s4-02-layout-nav.md` | 已派发 |
| R3 | Visual Skin / Neutrality | `docs/review/review-s4-03-skin-neutrality.md` | 已派发 |
| R4 | Feature Widgets | `docs/review/review-s4-04-widgets-features.md` | 已派发 |
| R5 | Fallback / Robustness | `docs/review/review-s4-05-fallback-robustness.md` | 已派发 |
| R6 | Tests / Gates | `docs/review/review-s4-06-tests-gates.md` | 已派发 |

## 通用要求

- 只读 review，不修改任何源码/配置/文档（除自己的报告文件）。
- 报告使用 Markdown，包含：`## 结论`、`## 发现`（按 Severity：Blocker / Major / Minor / Nit）、`## 建议`。
- 引用具体文件与行号。
- 不提交，不 push。
