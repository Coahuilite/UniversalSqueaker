# 贡献指南 · Contributing

本项目接受代码与第三方语音包方向的贡献。约束契约：语音包作者指南 [`.github/skills/us-voicepack-authoring/SKILL.md`](./.github/skills/us-voicepack-authoring/SKILL.md)、模组结构参考 [`docs/mod-structure-reference-zh.md`](./docs/mod-structure-reference-zh.md)、发布流程 [`docs/release-runbook-zh.md`](./docs/release-runbook-zh.md)。UI 内核（FerriteLib.UiKit）在独立前置仓 `../ferritelib`，其改动走那边的门禁。

This project accepts code and third-party voice-pack contributions. Binding contracts: the author guide [`.github/skills/us-voicepack-authoring/SKILL.md`](./.github/skills/us-voicepack-authoring/SKILL.md), the structure reference [`docs/mod-structure-reference-zh.md`](./docs/mod-structure-reference-zh.md), and the release flow [`docs/release-runbook-zh.md`](./docs/release-runbook-zh.md). The UI kernel (FerriteLib.UiKit) lives in the separate prerequisite repository `../ferritelib` and is gated there.

## 音频 / Audio

Universal Squeaker **不携带任何音频**，也不接受向主仓添加音频的 PR——语音内容一律以第三方 VoicePack Def + 音频根目录的形式存在（见作者指南的 canonical 布局 `<lowercase packageId>/<PackDef.defName>/<Action>/`）。主仓的 `Extras/`、`1.6/Sounds/` 是门禁红线。

Universal Squeaker ships **no audio** and does not accept PRs adding audio to the main repository. Voice content exists only as third-party VoicePack defs with their own audio roots (canonical layout in the author guide). `Extras/` and `1.6/Sounds/` are gate red lines here.

## 代码 / Code

1. 从 `main` 分支切出（本仓为单 `main` 模型，无 dev 分支税）。
2. 保持内核边界：`Source/UniversalSqueaker/Kernel/` 与 `Pure/` 零 Verse/Unity 引用；产品词汇不得渗入（`SqueakyRatkin` 类型引用由 `scripts/check-pack-readiness.ps1` 负向断言拦截）。
3. 身份契约不得改动：packageId `coahuilite.universalsqueaker`、命名空间 `UniversalSqueaker`、Def 前缀 `US_`、日志前缀 `usdiag`。
4. 存档兼容红线：`Scribe` 字段名（如 `experimentalKiiroCompat`）与 `usdiag` 事件词表是持久化/诊断契约，重命名 = 静默破坏旧档或排障工具链。
5. 玩家可见文案一律走 `1.6/Languages/*/Keyed/`（中英双语对称，中文权威）；源码字面量不放英文文案。
6. 验证：

   ```powershell
   pwsh -NoProfile -File scripts/verify-local.ps1
   ```

   15 道门禁须全绿（6 个 harness、主程序集 Dev/Release 零警告、载体边界红线、MPL-2.0 许可一致、Schema=2 清单）。UI 行为改动另需实机验收清单（维护者本地维护，不入库），由维护者执行。

7. 提交前跑隐私门禁：`pwsh -NoProfile -File scripts/privacy-audit.ps1`（个人路径 / 凭据 / PublishedFileId 值 / 身份唯一性；提交身份使用 GitHub noreply 邮箱）。
8. 提 PR 到 `main`，附改动与理由。历史重写、tag、推送由维护者裁决，贡献者不做。

1. Branch from `main` (single-`main` model; no dev-branch tax).
2. Keep kernel boundaries: `Source/UniversalSqueaker/Kernel/` and `Pure/` stay zero-Verse; product vocabulary must not leak (`SqueakyRatkin` type references are caught by a negative assertion in `scripts/check-pack-readiness.ps1`).
3. Identity contract is fixed: packageId `coahuilite.universalsqueaker`, namespace `UniversalSqueaker`, Def prefix `US_`, log prefix `usdiag`.
4. Save-compatibility red lines: `Scribe` field names (e.g. `experimentalKiiroCompat`) and the `usdiag` event vocabulary are persistence/diagnostics contracts; renaming them silently breaks old saves or the triage toolchain.
5. All player-facing strings go through `1.6/Languages/*/Keyed/` (English and Simplified Chinese symmetric, Chinese authoritative); no English literals in source.
6. Verify with `pwsh -NoProfile -File scripts/verify-local.ps1` — all 15 gates green. UI behaviour changes additionally need the in-game acceptance checklist (maintainer-local, not published), run by the maintainer.
7. Run `pwsh -NoProfile -File scripts/privacy-audit.ps1` before committing (personal paths / credentials / PublishedFileId values / identity uniqueness; use the GitHub noreply identity).
8. Open a PR against `main` with the change and its rationale. History rewrites, tags and pushes are maintainer decisions.
