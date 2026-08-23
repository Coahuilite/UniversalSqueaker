# Release Runbook（版本无关）

> 唯一发布流程入口。版本相关事实以当次 `docs/release_review/release-<version>-review-zh.md`（Claim Pack）为准；本文只描述流程与门禁。
> 每一步的 git/外部影响操作都需要维护者明确授权（见 `AGENTS.md`）。

## 阶段 0 · dev 发布前准备

1. **版本**：csproj `<Version>` bump（唯一主源；`About.xml <modVersion>` 跟随，发布前查两处一致）。
2. **文档同步**（与代码同批 commit）：
   - changelog（EN/SC）加 `Unreleased — X.Y.Z` 条目：开发者功能解锁细节模糊化、不写 change note、中英同步；
   - README / MEMORY / 根 codemap 的版本引用同步；
   - Workshop 页面文案（如页面内容有变）：中英对称、英文以中文版为准、页面专注模组本身（无音频统计/开发者排障/迁移说明/制作步骤，作者内容只留指南链接）、俏皮句不加解释、字符数刷新。
3. **隐私审查**：工作树全扫描（凭据/API key/token/本地路径/`PublishedFileId`）。
4. **构建 + 打包核验**：
   - Dev / GitHub flavor 构建 0 errors；
   - pack 后**包内容逐项核验**：文件数（含 `version.txt`）、排除项（`*.pdb`/`*.gitkeep`/`codemap.md`）、关键文件内容（`LoadFolders.xml` 无门控、DLL 版本/flavor/身份）、**包内 `version.txt` 三行与预期一致**（`SqueakyRatkin <版本>`/`build=<flavor>`/`commit=<sha>`）。

## 阶段 1 · PR 与 merge

5. 原子 commit → push dev → dev CI 通过。
6. PR dev→main。
7. **main/dev 分叉处理**：
   - 若 main 的 squash tree 与 dev 的 merge tree 相同 → `git merge -s ours origin/main`（dev 纯增量，避免重复三方冲突）；
   - 否则三方 merge → 注意 **auto-merged 文件不受 `git checkout --ours` 控制**（base 与 ours 相同时三方合并会采纳 theirs）；
   - merge 后必须 **`git diff <修复commit> <mergecommit>` 全面核验**（重点关键行为文件：`LoadFolders.xml`/csproj/`About.xml`）→ 重新隐私审计 + build → push。
8. PR CI 通过 → squash merge。

## 阶段 2 · GitHub 发布

9. main 树 == dev 树核验（diff 0 行）+ main 树隐私审计。
10. tag `vX.Y.Z`（严格 SemVer，基版本 = csproj `<Version>`）→ push → release CI → Release 资产核验（文件数/排除项/DLL 身份/哈希）。
11. changelog 时间替换：`Unreleased` → 最终发布时间（UTC+8；**tag 重发后必须再更新**）。

## 阶段 3 · Steam 发布（人工 + 页面核验）

12. Steam flavor 构建 + pack-steam → 包内容核验（同阶段 0 清单）。
13. 维护者：复制 stage 到本地上传副本 → 把既有 item ID **只写入副本** `About/PublishedFileId.txt` → 以同一作者 Update（后续版本绝不用 `Initial Workshop Upload`）。
14. 维护者：粘贴中英文案 → Steam 编辑器 + 实际页面双预览。
15. **页面核对（可自动化，玩家视角只读）**：`read` filedetails 页面 URL 观察——同一 item URL/ID、描述版本、Updated、visibility、preview、文件大小、change notes 数，写入观察记录。**绝不尝试登录态或编辑界面**（Steam 编辑 = 维护者人工操作；自动化只读 filedetails 玩家可见视图）。

## 阶段 4 · 收尾

16. Release Claim Pack（模板见下）写入 `docs/release-<version>-review-zh.md`。
17. dev 对账：merge main 回 dev，验证无意外树变化。
18. MEMORY / TODO 更新。

## 隐私审查门禁（每次 push 前，含临时暂存）

- 审查**完整可达范围**，不只 HEAD。
- 扫描模式：凭据（`sk-…`/API key/token）、私钥、本地绝对路径、诊断日志摘录、`PublishedFileId.txt` 值。
- 文档按**无隐私写法**编写：写作时不写入个人本地状态、展开路径、日志摘录、凭据或 ID 值，而非写后清理。

## Release Claim Pack 固定模板

```text
## GitHub Release Claim Pack
| 项 | 值 |
|---|---|
| 版本 | X.Y.Z |
| 标签 | vX.Y.Z（严格 SemVer，基版本 = csproj <Version>） |
| 源码提交 | <main squash 完整 SHA> |
| 发布时间 | <UTC 时间>（UTC+8 <时间>） |
| 构建 flavor | GitHub（CI tag 触发） |
| DLL 身份 | FileVersion <V>；Informational v<V>+<sha12> |
| CI | Release workflow run <id> success |
| 资产 | <zip 名>（<字节数> B） |
| DLL SHA256 | <hash> |
| 包内容 | 文件数；0 PDB；0 PublishedFileId.txt；0 codemap.md；关键文件内容核验；OGG 镜像校验；包内 version.txt（版本/flavor/commit） |
| 隐私审计 | 完整树扫描 0 命中；dev↔main 树一致 |

## Steam staging / 发布观察
- staging 包：文件数、排除项、DLL FileVersion、PublishedFileId=0、包内 version.txt（版本/flavor/commit 与预期一致）。
- 页面观察：同一 item URL/ID、描述版本、Updated、visibility、preview、文件大小、change notes。
- 二进制下载级验证边界如实记录（页面级核验 ≠ 玩家下载内容已验证）。

## 渠道状态
- GitHub：完整 / 待补。
- Workshop：unverified / 页面级已核验 / 完整。
```
