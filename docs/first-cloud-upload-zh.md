# 首次上云指南（一次性事务）

> 由 lib 侧会话 2026-09-06 撰写，US 锚点 `5f811a0`（213 提交 / 227 tracked / 无 remote / 无 tag / 单 main）。执行会话同日复测：基线全部复核，两处措辞漂移已修正（见 docs(upload) 提交）；本文档自身原内联真实路径字面量，已中性化——它不在 §2 重写白名单内，留着就是永久债。
> 本文所有「实测」数字带锚点与日期；**执行前先复测**——锚点漂移则数字失效，先改本文再照做。
> 每次发版的日常流程见 `release-runbook-zh.md`，本文只管 push 之前的一次性事务。
> 通用方法论（为何三向量分开扫、重写波及怎么量化）见 `modding_documents/privacy-debt-vector-triage-zh.md`。

## 0. 已裁决边界（maintainer 2026-09-06，执行会话不得重开）

1. **历史债务**：保留历史（消息/顺序/粒度），用定向 blob 重写清除债务；接受 hash 漂移 + 台账机械跟改。
2. **兄弟引用**：tracked 文档层匿名化；测试 fixture 不再呈现于文档；提交信息里的兄弟名不管。
3. **执行主体**：本文由 US 仓自己的会话执行；lib 仓事务由 lib 会话处理（§2 有一个跨仓通知义务例外）。
4. **文档落点**：本文（一次性）与 runbook（每次）分离；通用教训持久化在 modding_documents。

## 1. 债务向量分诊（实测，锚点 `5f811a0`）

隐私债务有三个**独立**向量，必须分开扫，不能由一个推另一个：

| 向量 | 扫描方法 | 实测结果 |
|---|---|---|
| 工作树 | `git grep -l -I -E "[A-Za-z]:[\\\\/](Users\|WorkSpace)" -- .` | **0 命中** |
| 提交信息 | `git log --all --format='%s%n%b'` 同模式 | **0 命中**（兄弟名 2 行 squeaky 口径 / 13 行宽口径 Ratkin|Kiiro|SR_，裁决 2 不管；复测修正，原记 10 行无口径可复算） |
| 历史 blob | `git grep -l -I <pat> $(git rev-list --all)` | 单分隔符模式 **7 文件**（`MEMORY.md` + 6 个 `docs/workdocs/*.md`）；四形态模式 **8 文件**（多出 `s4-polish-kickoff.md`，它只有双形态命中）。重写白名单 `^(MEMORY\.md|docs/workdocs/)` 覆盖两者，白名单外实测零命中 |

债务形态（实测枚举；本文档刻意不再内联「盘符+冒号+反斜杠」字面量，避免自身成为新债）：
- 工作区拓扑：`E 盘 \WorkSpace\AI_IDE\opencode\modding\rimworld\...` 前缀，不含用户名——单反斜杠形态 662 处；
- 个人标识：`C 盘 \Users\Fe`——单反斜杠形态 9 处，仅存在于 MEMORY.md 历史版（`5183358` 引入、`ccb5aad` 修复，存活 2 个版本）；
- **JSON 转义形态**：上述两者的双反斜杠版（`C 盘 \\Users\\Fe` 82 处、`E 盘 \\WorkSpace...` 1 处，位于 workdocs 的会话存档与本文档旧版）。**§1 的单分隔符扫描模式匹配不到双形态**——重写规则与 privacy-audit 都必须显式覆盖四种字节形态；
- `E 盘 ...\squeaky_ratkin`——兄弟仓路径，被工作区前缀规则覆盖。

身份面：213 提交 author/committer 全部已是 `Coahuilite <19252128+...@users.noreply.github.com>`，**0 命中**，无需处理。`PublishedFileId`：字面扫描 9 个 tracked 文件命中，**逐条核为政策文本、`.gitignore` 规则与负向断言，真实 Workshop ID 值（`<PublishedFileId>数字`）0 处**（复测修正，原记「0 tracked」措辞失真；隐私门禁按值扫描，不按词面）。

**结论**：债务 100% 在 blob 向量。改提交信息碰不到它；§2 的定向重写是唯一既保历史又去债务的路径。

## 2. 定向 blob 重写

前提（已实测成立，动手前复验）：无 remote、无 tag、单 main——此时重写免费；push 后重写是数日欠债。

```
# 1) 保险：镜像备份（重写不可逆）
git clone --mirror . ../UniversalSqueaker-mirror-backup.git
# 2) 替换规则文件 rules.txt：放在仓外临时目录，绝不 tracked。
#    git-filter-repo --replace-text 默认按【字面量】匹配（不是正则！），
#    每行 old==>new，因此四种字节形态各需一行：
#      E 前缀单形态==>（workspace）    E 前缀双形态==>（workspace）
#      C 家目录单形态==>（home）        C 家目录双形态==>（home）
#    （执行会话注：文档旧版误称「正则==>替换」，且示例只给双形态一行，
#      会漏掉 662+9 处单形态——已修正。）
# 3) 执行（实测修正）：--paths/--path-regr 都不是「只碰白名单」——
#    --path-regex 是【选择保留路径】，白名单外文件会被整个删除。
#    定向性由 --replace-text 自身保证：只有含字面量的 blob 会被改。
#    实测债务字面量仅存在于白名单 8 文件（四形态扫描确认白名单外 0 命中），
#    因此不加路径过滤、直接全库替换即为定向重写。
#    【必须带 --preserve-commit-hashes】：filter-repo 默认会重写提交信息里
#    出现的旧对象 ID——那直接违反裁决 1「保留历史」与验收 3「消息逐字节相同」。
#    执行会话第一次重写就踩了这个坑（消息里 73b0b6a→b0d0fc0），从镜像恢复重做。
uvx git-filter-repo --replace-text rules.txt --preserve-commit-hashes --force
```

（Python 一律 uv，`uvx` 即得；不要 pip。执行期实测注：`git grep` 只扫 tracked 文件——锚点复测时上传文档自身尚未 tracked，所以向量 1 报 0 是「当时为真」；文档首次入库后必须重扫，其内联字面量已先行中性化。）

波及量化（实测）：
- 脏 blob 引入于第 33/34 提交（`5183358`/`73056ab`），workdocs 整体删除于第 146 提交（`6065420`）。**实测修正：漂移不是「33 号之后」局部——commit-map 覆盖全部 216 提交、0 条恒等映射，根提交 `8fc8d8b` 也漂为 `8fc8d8b`**（blob 哈希入 commit 哈希，逐级上溯）。好消息：`--preserve-commit-hashes` 下消息里的旧 hash 原样保留，台账因此成为**必需**而非可选；
- 仓内 hash 引用：文档窄口径（MEMORY/TODO/AGENTS/HANDOFF/runbook）**29 处**，全 tracked md 实测 **70 处唯一值**（review/uikit-rebuild/OBLIVIONIS 也大量引用）。台账按全量 70 处理，**只替换 commit-map 里存在的键**：其中混有 lib 仓 hash（如 `fc59b60`）与 SR 仓对象（`b19d68a`），不在本仓 map 中，碰了就是造假；
- **跨仓锚点 1 处**：lib 仓 `MEMORY.md:8` 引用 US `6c7053a`（拆分溯源）。重写完成后，把 commit-map 里 `6c7053a` 对应的新 hash 报给 maintainer，由 lib 侧会话跟改——**这是本文唯一允许越仓的事项，且只报数不改对方文件**。（执行期实测：`0fe60b0 → 6c7053a`。）

验收：全历史扫描 0 命中 + 提交数不变（重写前复测值 **216**）+ `git log --format='%s%n%b'` 与重写前逐字节相同（消息保全，需 `--preserve-commit-hashes`）+ commit-map 台账 0 悬空引用。

## 3. 兄弟引用中性化（三层分类，勿混）

**功能层——保留，禁止匿名化**（改名即破坏行为，实测定位）：
- `Source/.../UniversalSqueakerSettings.ExposeData.cs`：`experimentalKiiroCompat` 是 **Scribe 存档字段名**，重命名 = 旧存档该设置静默重置；
- `scripts/check-pack-readiness.ps1:88`：`'SqueakyRatkin'` 是**负向断言模式**（防 SR 类型渗入 Source 的门禁），替换 = 门禁失明；
- Legacy bridge shim（`Source/UniversalSqueaker/Legacy`）：SR 兼容契约，maintainer 已授权例外。

**文档层——中性化（普通新提交，不进 §2 重写）**：tracked md 命中 20+ 文件。分类处理：
- **SR**：`README.md:5` 的分叉溯源**保留**——SR 是公开仓，且 MPL 对衍生作品要求来源声明，抹掉反而可疑；其余「兄弟仓路径/本地状态」措辞中性化；
- **NGS（NivarianGrandStructure）**：**未公开项目，最高优先级**——TODO 9 处、MEMORY 4 处点名即曝光未发布产品，改中性措辞（"一个兄弟模组"）；
- **Ratkin/Kiiro 种族名**：作为游戏实体/defName 出现的保留（公开游戏内容），作为项目引用的中性化。

**fixture 层**：实测 0 tracked（`dist/` 已 ignore），无需动作；裁决 2 的「不再呈现于文档」并入文档层执行。

## 4. 发布面缺口（实测，push 前补齐）

- `About/About.xml` `<description>` 仍是 "Placeholder … Replace before any release"——玩家可见，必须写；
- 无 `CONTRIBUTING.md`；README 单语中文（SR 案例为双语互链；US 是产品 mod，建议对齐）；
- `.github/workflows/` 是空壳（仅 `.gitkeep`）——`ci.yml`/`release.yml` 从零写；
- **无 `global.json`**：SR 案例盲抄 `dotnet-version: 8.0.x` 炸出 MSB4068——runner SDK 版本自己测，别抄；
- 版本轴现状：`<Version>0.1.0-dev` / `<modVersion>0.1.0-dev` / `PrerequisiteApi 0.2.0–0.3.0`；lib 侧 `Api=0.2.0`/`modVersion=0.2.0`。正式发布时多轴一致性要一条断言全锁（lib MEMORY 的 0.1.0/0.2.0 漂移教训）；
- **CI 依赖链（硬约束）**：csproj `HintPath` 指向 `..\..\..\ferritelib\1.6\Assemblies\FerriteLib.UiKit.dll`——runner 上不存在兄弟目录，且 lib 仓**不 tracked 该 DLL**（`1.6/Assemblies/` 只有 `.gitkeep`，产物由构建生成）。两条可行路径：① `actions/checkout@v4` 以 `repository: Coahuilite/ferritelib` + **`path: ../ferritelib`** 检出兄弟仓再 `dotnet build`（默认 path 会落在 workspace 内部，所有 pin 死的相对引用全部落空）；② 从 lib 的 GitHub release 页下载资产解出 DLL（按 `PrerequisiteApi` 的 `[0.2.0, 0.3.0)` 区间解析最高满足版本，不拼死 tag）。maintainer 裁决「链接不复制」管的是**玩家分发面**（US 包内不得有第二份 DLL），CI 构建期两条都不违反；选哪条由首跑绿的那条定，写进 MEMORY 带证据。
- 仓库命名契约：lib 被 US 以相对路径 `../ferritelib`（小写）引用；US 自身仓名一旦被任何仓相对引用即成构建契约，建名前核对大小写。

## 5. 推送顺序（依赖约束，非偏好）

```
lib 侧：建仓 → push → tag v0.2.0-rc1 → release 资产可下载
US 侧（可与 lib 并行准备，仅 push 被 lib 卡死）：
  §2 重写 → §3 中性化 → §4 补缺口 → 写 scripts/privacy-audit.ps1 并跑绿
  → git remote add（不 push）→ git status 终检 → push
push 后：分支保护（见 CleanCloudRelease_Workflow §9）→ CI 首跑 → §6 闭环
```

`privacy-audit.ps1` 规格（US 会话自写，runbook 与本节共用）：三向量扫描（§1 表）+ 凭据模式（`ghp_`/`github_pat_`/`-----BEGIN`/`sk-`）+ `PublishedFileId` + identity 唯一性；参数 `-FullHistory`；退出码 0 = 全干净。runbook 的「隐私审查门禁」节应改为引用此脚本而非内联描述。

## 6. 推送后核验（闭环）

复用 lib 仓 `scripts/verify-release.ps1`（已参数化 `-Tag -Repo -AssetPrefix`），`-AssetPrefix` 换 US 产物名。六项对账：draft 状态 / prerelease 旗标 / 资产名 / 服务端 digest / `/releases/latest` 语义 / 悬空 tag。退出码非 0 = 发布未完成，先修再宣告。

## 7. 明确不在范围

- **分支模型改造**：US 实测单 main、无 dev——SR 案例的 main/dev 税在 US 不存在，不要引入；
- Workshop / Steam 渠道：另行裁决；
- lib 仓内部事务：lib 会话管辖（§2 跨仓通知义务除外，且只报数不改文件）。
