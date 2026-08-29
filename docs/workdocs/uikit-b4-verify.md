# B4 任务书：FerriteLib.UiKit 自验证

> Worker 只读本任务书。前置依赖：B1–B3 已完成。

## 目标

对 FerriteLib.UiKit 本身做最终验证，确认：

1. 构建零警告；
2. 所有 FerriteLib.UiKit 测试通过；
3. 中性约束未被破坏；
4. 没有意外改动 US 代码。

## 范围

只读验证 + 必要的小幅修正。

允许修改：

- `Source/FerriteLib.UiKit/**`
- `tools/FerriteLib.UiKit.Tests/**`

禁止修改：

- `Source/UniversalSqueaker/**`
- `MEMORY.md` / `TODO.md` / `HANDOFF.md` / `AGENTS.md` / `OBLIVIONIS.md`
- 不 push、不发布

## 验证命令

在仓库根目录执行：

```powershell
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release
dotnet run --project tools/FerriteLib.UiKit.Tests -c Release
```

如果测试工程需要先构建 stub，则按该工程现有方式执行。

## 中性检查

对 `Source/FerriteLib.UiKit` 下非 `obj/`、非 `bin/` 的源码执行 grep，确认不包含：

```text
UniversalSqueaker
SqueakyRatkin
Ratkin
SR_
US_
```

注意：`Ratkin` 大小写不敏感；如果存在“中性示例”或注释中引用外部产品名，应移除或改为中性描述。

## 验收标准

- [ ] Dev 与 Release 构建均零警告（TreatWarningsAsErrors 通过）。
- [ ] `tools/FerriteLib.UiKit.Tests` 输出 `ALL PASS`。
- [ ] 中性 grep 无命中。
- [ ] `git status` 显示改动仅限 `Source/FerriteLib.UiKit/**` 与 `tools/FerriteLib.UiKit.Tests/**`（以及本任务书所在 workdocs 文档，若由本 Block 更新）。
- [ ] 不包含任何 US 迁移代码或 US 引用。
