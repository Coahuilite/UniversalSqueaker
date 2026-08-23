# TODO

## 当前（本地分叉阶段）
- [x] 从 SR `0.3.x` `b19d68a` 迁移 Kernel/Pure/harness/fixtures/记忆协定到本地仓库并 `git init`。
- [x] 适配 harness 链接到本地 `Kernel/`+`Pure/`，SR 内容快照放 `sr_reference/`。
- [x] 留下 `HANDOFF.md`：UI 最小适配评估（语音包分配可用、其余可摘除、不崩溃、种族通用）。

## 第一阶段 · 内核去 SR 化（US 通用状态）
- [ ] 命名空间迁移：`SqueakyRatkin.Kernel`/`SqueakyRatkin` → `UniversalSqueaker.Kernel`/`UniversalSqueaker`；harness 与引用同步。
- [ ] 清除内核产品字面量：`BuiltInFallbackTable` 的 Ratkin 种子与 `SR_*` 音键移出内核（改数据注入/空表启动）；`ActionAudioKeyMirror` 与五处同步改 US 数据面或暂时冻结为迁移护栏。
- [ ] 重建 harness 语料基线：去 SR 参考后仍零 delta；新增「两种族平等路由」场景（非 Ratkin 专用断言）。
- [ ] 建立 US 程序集骨架（csproj/About/packageId）——暂不发布，仅本地可构建。

## 第二阶段 · 运行时与装配通用化
- [ ] `SqueakProductDomainFilter` 等价物删除：catalog/resolver 不再白名单 `{Ratkin}`；域由 pack `raceDefName` 数据驱动，全部种族默认可装配。
- [ ] 种族发现：Race 层从 VoicePack/fallback profile 数据枚举，不读 HAR Ratkin 特判；Xenotype 层保持 Biotech 门控与 `targetDefName` 精确匹配。
- [ ] 内置 fallback profile 机制通用化：无产品种子；profile 源改为数据文件/内容包。
- [ ] 日志与键前缀迁移：`SR_`→`US_`、`[SqueakyRatkin]`→`[UniversalSqueaker]`（或 `usdiag` 协议前缀）；v1/v2 协议按 US 重新冻结。

## 第三阶段 · UI 最小可用改造（按 HANDOFF.md 评估执行）
- [ ] 保留：模式选择（Off/Fallback/Remix）+ Race/Xenotype 语音包勾选页 + 基本保存/刷新。
- [ ] 摘除/降级：SoundMood 工作台、Debug/Diagnostics 页、音频浏览器、统计/overlay/mote 诊断、行为编辑器；确保无引用、无崩溃。
- [ ] 崩溃安全矩阵：空目录/无候选/无 Biotech/无 HAR/无选中 pawn/损坏设置全部可打开设置页。
- [ ] 种族通用验收：至少两种族各挂 VoicePack，同域只路由同种族，Race 列表不出现 Ratkin 特判。

## 待确认
- [ ] Workshop 显示名与许可；US 起始版本号（建议本地 0.1.0-dev，0.4 双发时统一抬 0.4.0）。
- [ ] 是否在本仓库做完整历史/tag 隐私清理（SR 仓库由 SR 侧单独决策，不在本仓库执行）。
