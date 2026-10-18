# Universal Squeaker 发布流程

以当次源码、打包脚本和 CI 为执行依据；本文不保存某次发布的临时任务表。提交、远端操作、打标签、发布和 Workshop 更新按当次维护者授权执行，不能从这份流程取得授权。

## 1. 准备版本与候选

1. 同步 csproj 的 `Version` / `VersionPrefix` 与 About 的 `modVersion`。身份轴使用纯数字版本；rc 后缀属于标签和工件名。
2. 更新中英文变更说明、README 与受影响文档。保持 [当前行为](current-behavior-0.5-zh.md) 和 [0.6.x 目标规格](us-kernel-0.6-spec-zh.md) 的状态区别，不因规划完成就宣称实现。
3. 按 [构建指南](build-and-debug.md) 选择准确 FerriteLib 载体并执行相关验证。纯逻辑、Host stub 和实机结果分别记录。
4. 检查候选安装包而不只检查源码：产品/载体配对、版本、内容、资源、许可和排除项。包结构以 [结构参考](mod-structure-reference-zh.md) 与 `stage-package.ps1` 为准。

当前没有独立 `pack-release.ps1` 或 `pack-steam.ps1`，也没有定义 `US_STEAM` 的构建轴。不要执行旧 runbook 中不存在的 Steam flavor 步骤。

## 2. 发布前隐私与集成

- 按 [隐私门禁](push-privacy-gate.md) 审查准备传输的完整可达范围；当前树通过不等于历史通过。
- 对新增/修改图片和不透明二进制做人审；不把个人路径、日志摘录、凭据或 Workshop 标识值放进文档。
- 合并方式服从实际历史与维护者流程，不把一次历史上的特殊合并当通用处方。集成后检查实际 diff 与包，必要时验证受影响路径。
- 只把完成验收的候选作为发布输入；保留上一可用候选和准确恢复路径。

## 3. GitHub 发布

`.github/workflows/release.yml` 由版本标签触发，接受 `vX.Y.Z` 或 `vX.Y.Z-rcN`，并检查标签、源码版本与 main 归属。它获取固定 FerriteLib release 载体、构建 US、stage 包并上传资产。

获得发布授权后按当前 workflow 执行。核对实际资产、版本、前置载体和源码可获取性；不能只凭 CI 绿色宣布玩家下载内容正确。没有发布动作时不补写远端状态。

## 4. Workshop

Workshop 操作由维护者在授权的上传副本执行，不往仓库写标识值。更新既有条目时使用原身份；新条目、显示名、说明和可见性由维护者确认。

发布观察区分页面可见性/说明与实际下载包。页面级核验不能冒充下载级验证；不把修改 Config、Steam 安装或存档作为文档整理步骤。

## 5. 保留精简发布结论

正式 Claim Pack 放在 `docs/release_review/release-<version>-review-zh.md`，只记录该发布的事实：

- 产品版本、标签、源码提交、发布日期和渠道。
- 构建 flavor、载体版本及配对证据、CI 运行和具体资产。
- 包内容与关键行为的实际检查、实机覆盖范围。
- 隐私审查范围和结论。
- 未验证项、已知限制与回退办法。
- GitHub / Workshop 各自的发布确认范围。

没有真实发布时不创建空 Claim Pack；过程日志、代理调度和未完成矩阵不进入结论。