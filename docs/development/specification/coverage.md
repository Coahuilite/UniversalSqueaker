# 来源覆盖与已解决冲突

本页是整理对账，不是第二份规格。完整逐条定位见[coverage.csv](coverage.csv)；旧正文按原文件名保在[归档](../../archive/specs-before-20261029/README.md)，新唯一权威从[人类入口](../../specification.md)进入。

## 采用顺序与实质修正
1. 后续显式机会合同优先于主规格／旧导读里的统一game-tick叙述：交互真实秒、模拟game tick，无legacy回退或旧tick附加门，混合消费者按本次机会选单位。
2. 已采用普通Release factory、W600/R60/H216、十五独立I／scope／总序／共享CHECK与WRITE取代“普通policy待裁”；216不是所有动作自身间隔。Dev20/40/120和历史T例仍各自原输入。
3. Work声明／Dev默认ActiveCommand与普通运行AnyOccurrence不同；仅事实支持集相同，不能将scope等同“是不是工作”。
4. 唯一作者来源自动采用只是早期候选；现行为Unconfigured／Inherit／Bound，普通仍显式选择。
5. Attack明确执行根、MentalBreak四成功来源、Equip PlayerCoreEquip＋ActiveCommand已经采用，旧“仅Dev／待范围裁定”归历史；逐包未记录处不补造通过或失败；2026-10-28总体RC接受保持，不因记录粒度新增RC前全矩阵门。
6. UI采用直接工具／当前文本底栏和后续尺寸规则，取代五工作区／整catalog最大Help／800×600或永久底栏。新正文不改语言／帮助资源。
7. 保存与运行采用诚实结果已是前序r2义务；VF1新reapply、持久pending／跨窗口恢复仍未裁，未进入现行必做。
8. 固定FL标识是候选证据，不是API规范。各阶段0.7／fc010bd／a0／36fa／833c／6ec保持各自身份；首次公开0.8范围不回退旧0.7或捏造rc2坐标。
9. 原“稳定后兼容”与发布前授权分开：US0.6允许破坏性更新直到用户明确确认面向玩家公开发布；公开仓库／push／tag不终止，也不授予发布。FL稳定ABI不受此US授权改变。

## 覆盖如何复查
CSV逐项含原文件／稳定条款、关键值或合法条件、新唯一正文、处理分类及理由。主规格§1–24、K01–K21与D1–D6均有去向；C00–C12／V01–V15在[evidence](evidence.md)保完整映射；十五动作每项有独立ACT要求与逐项记录粒度行；Core Work两个表完整保身份／分支，不用计数代替内容。

规范拥有能力和连接；原版方法／主体／Unknown与来源版本在[facts](facts.md)，实际caller与寿命在[implementation](implementation.md)，现行bug／关闭条件在[open-issues](open-issues.md)。重复条款合并，原阶段过程／测试数保历史，不变成新默认或本次运行结果。

## 来源优先级补正
- DIAG-02的隐私保证来自AGENTS及发布隐私门，范围是共享／仓库／文档／工件及发布材料；SqueakLogProtocol对ex_msg使用既有异常消息清洗，其他字段的百分号编码不等于通用隐私脱敏。没有来源支持所有任意运行时字段必被redactor处理。
- 响应合同审查WO r6及2026-10-28内部试玩决定优先于早期逐包pending措辞：总体RC接受有效，精确逐项未记录不补PASS／FAIL，也不造成新RC准备门或全矩阵重跑。
- US06-PREPUBLICATION-COMPAT-20261019 r4已撤回历史“省略永久为1”寿命约束；CORE-04仅保当前0.6声明省略按1解析及未知格式拒绝。独立必需的responseConfigurationFormat=1合同不变。

## 独立开放项
F01日志出口／失败隔离、F02 VF1音频发布、F03 canonical过滤仍是实现差距；没有用现状削弱要求。Social Goto Def跨版本内容缺口和真实Config／原生听感缺口保留。没有剩余需要本次worker自行裁定的新产品语义冲突。

