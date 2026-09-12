# Universal Squeaker

[English](./README.md) | **中文**

RimWorld 1.6 通用语音包路由内核。本仓库是 Squeaky Ratkin 的 **Universal Squeaker 本地分叉**（MPL-2.0，来源声明按许可证要求保留），正在准备首次云端发布。

Universal Squeaker 本身**不含任何语音内容**：它是一个路由内核，把第三方语音包按种族/异种域挂到 pawn 上，并提供分层调音（全局/种族/异种三级覆盖）、距离衰减曲线、心情调制、彩蛋门与相机指示器。设置界面为独立窗口，五个工作区（总览/距离/语音包/调音/预设）配右侧悬停帮助面板；内置 `usdiag` 诊断协议，排障时按等级输出到玩家日志。

## 运行要求

- RimWorld 1.6
- 前置模组 **FerriteLib**（`coahuilite.ferritelib`）：承载 UI 内核 `FerriteLib.UiKit.dll`。US 只按相对路径引用其编译产物，**不随包携带**；API 区间校验在代码内（`FerriteLibVersion.Require`），不在 `About.xml`（RimWorld 的 modDependencies 无法表达版本）。

## 身份

- packageId：`coahuilite.universalsqueaker`
- 命名空间 / Def 前缀 / 日志前缀：`UniversalSqueaker` / `US_` / `usdiag`
- 许可：MPL-2.0（Workshop 显示名待维护者确认）

## 本地验证与构建

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1   # 15 道门禁（6 个 harness + 主程序集 Dev/Release + 载体边界与单载体红线 + MPL-2.0 许可一致 + Schema=2 清单校验 + UI 边界审计 + harness stub 覆盖）
pwsh -NoProfile -File scripts/build-dev.ps1      # 先在 ../ferritelib 建 Release 载主载荷，再 Dev 构建并 stage 成可直接安装的**目录**（dist/dev/UniversalSqueaker）——不打归档；需要 zip 时用 pack-dev -Zip
pwsh -NoProfile -File scripts/privacy-audit.ps1  # 隐私门禁（三向量 + 凭据 + PublishedFileId 值 + 身份唯一性；-FullHistory 为全历史模式）
```

第 15 门把载体的引用驱动 stub 扫描（`../ferritelib/scripts/stub-coverage-scan.ps1`）只读地跑在 US 载荷上，配 US 自己的豁免账 `scripts/stub-coverage-exemptions.txt`：载荷引用的每个游戏成员，要么被载体 stub 声明，要么带理由被豁免，否则判红。这一类的可见性车道给不了——只在参考程序集里存在的成员编译全绿、运行时才死，而 harness 里会话守卫会把它换成一个恢复带，只断言「会话还活着」的车道照样绿。

UI 库已拆为独立前置模组仓库 `../ferritelib`（`coahuilite.ferritelib`），有自己的门禁体系。第 14 门 `scripts/ui-boundary-audit.ps1` 是同一 containment 指标的消费侧：白名单只剩两条裁定豁免（dev 诊断面板、冻结的相机指示器 fallback），其外的裸 Unity IMGUI / Verse Widgets 调用一律判红；裸 `Mouse.IsOver` 更是任何位置都不允许——悬停、帧门、窗口 chrome 全部走库缝（`UiNative.IsMouseOver` / `UiNative.IsLayoutEvent` / `UiWindowHost`）。

## 文档索引

- 交接与验收清单：`HANDOFF.md`（维护者本地文档，不入库）
- 记忆协定 / 持久事实 / 行动面 / 冷档案：`AGENTS.md`、`MEMORY.md`、`TODO.md`、`OBLIVIONIS.md`
- 发布流程（每次发版）：`docs/release-runbook-zh.md`
- 首次上云：2026-09-06 已执行完毕；持久决策与推送顺序见 `MEMORY.md`（"First cloud upload"节），PASS 台账见 `TODO.md`。
- 模组结构参考：`docs/mod-structure-reference-zh.md`
- 语音包作者指南：`.github/skills/us-voicepack-authoring/SKILL.md`

历史资料（描述已于 2026-09-02 删除的旧 UI 实现）：`docs/ui-componentization-evaluation-zh.md`、`docs/ui-phase3-implementation-notes-zh.md`。
