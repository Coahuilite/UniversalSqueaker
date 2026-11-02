# 贡献指南 · Contributing

当前开发线为0.6.x首次公开基线，目标RimWorld1.6.4871。先读[当前作者指南](docs/us-kernel-0.6-authoring-zh.md)与[现行规格](docs/specification.md)，不要按保留的0.5 XML/API制作新线内容。

Current development targets the US0.6.x first-publication baseline and RimWorld1.6.4871. Start with the [current author contract](docs/us-kernel-0.6-authoring-zh.md) and [current specification](docs/specification.md), not the retained 0.5 format.

## 代码与内容 / Code and content

- 完整 `Source/UniversalSqueaker/Core/**/*.cs` 无Verse／Unity／RimWorld、原生对象或mutable process-global state；游戏事实、音频和生命周期在adapter。单Subject owner同步预留、输出和四收据，不建第二状态机。
- The complete Core compiles without game/platform references or doubles. Keep native sampling/resources in adapters and settlement in the one synchronous subject owner.
- packageId `coahuilite.universalsqueaker`、namespace `UniversalSqueaker`、Def前缀 `US_`、诊断前缀 `usdiag`及MPL-2.0不变。不得混入SR类型、资源、品牌或程序集。
- US主仓不携带音频、Extras或第二份FL DLL。第三方声音包提供自己的SoundDef与素材；声音资源的真实owner与引用者身份不同。
- r2明确采用一次通知声明、新Config身份与格式，无旧reader。破坏性授权按US06-PREPUBLICATION-COMPAT-20261019 r4持续至用户明确确认面向玩家公开发布，GitHub可见／push／tag不终止；不扩展到FL稳定ABI或真实Config；US不写永久存档业务数据，旧真实Config不自动触碰。
- Player-facing copy uses symmetric English／ChineseSimplified Keyed entries. UI reads canonical configuration; it must not define another scope/default policy.

## 验证与提交 / Verification and delivery

选明确固定FL0.8包；参考[构建指南](docs/build-and-debug.md)。根据受改合同运行实际入口：完整Core、选择器冻结golden、真实生产caller／回执、配置失败恢复、当前UI／作者例。不要修改golden或削弱行为期望来迎合实现，也不例行重跑未变矩阵。

Use the required project checks and affected counterexamples. Distinguish native game checks from production code exercised with platform/Scribe doubles. A drawn Host or exit0 alone does not prove the intended mode executed.

提交前审完整diff及隐私门禁；使用GitHub noreply身份。普通本地提交可以进行；远端配置、push、PR、merge、tag、release及分支／worktree清理均按维护者明确授权，不自动开展。最终推送前须检查完整待推历史范围，见[隐私门禁](docs/push-privacy-gate.md)。