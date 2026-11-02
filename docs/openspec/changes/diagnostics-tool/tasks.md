# Tasks — 阶段 B 本地交付完成

## A. 已授权的候选与映射

- [x] A1 读取 WO／DSP、现行 diagnostics／ui-consumption、变更方法与实际 source／既有检查；保继承 dirty 基线。依据：[source-map](source-map.md)。
- [x] A2 提出完整玩家路径、状态／预算／生命周期／隐私／反馈方案；分清 DIAG-01 恢复与 DIAG-04／06／07 真正差量。依据：[proposal](proposal.md)、[design](design.md)、[spec delta](specs/diagnostics/spec.md)。
- [x] A3 交 ignored REPORT-A.md 并停止在阶段 A；报告仅声明源码／文档观察，没有运行证据。

## B. WO r2／DSP A002 放行后实际完成的切片

- [x] B1 **先闭合已保存状态读面。** 在 native adapter 结算处记诊断初态／序号，修 DiagnosticText／CaptureResponseLog 的空动作查询，提供只读 typed envelope；Overlay 记录 last-good／ReadFailed，UI 不把旧值／无请求／四回执／ActiveAction 混用。去除诊断驱动 population 维护和健康事实重采；保旧入口、Core、业务状态与原 DiagnosticText 有效含义。复用现有 caller／投影／Host 检查中实际相关部分；初态真实 Comp 路径缺动态覆盖仍单列。
- [x] B2 **加入单目标录制与回应日志续期。** 扩展原 Overlay 的一个 recording slot、64 条 detached ring、计数／字段界限、起止／清空／对象与 map／Game 生命周期；在原已结算边采用。Comp 的 response trace 改 60 秒窗／64 次尝试，mode 和必要故障保持；内存先于可选出口。保护 ptrace／ltrace／once／geometry／32 Host-pass 的原义。不能为此新增用例或测试平台。
- [x] B3 **让玩家完整走通。** 原设置诊断 Section 加普通入口，解除这两个诊断窗的 DevMode-only draw 限制；原主／详情布局加必要状态和开始／停止／清空／复制。公开旧 source／Host 入口仍合法，新控制按可选能力接入，写绑定沿原注册能力。实现 allowlist `usdiag-feedback/1` 和 native clipboard 写边，清楚反馈未知／截断／复制失败；同步 EN／ZH 必要文案及局部帮助。不重排全局工具或新增全局 UX。
- [x] B4 **采用与受影响证据。** 获采用后更新唯一 diagnostics 现行要求／导航、source／evidence 状态和 MEMORY 旧 debug-only 说法；保旧收据。复用现有 product 编译、相关 UiLogic／source guard、当前 Host existing-diagnostics／workspace 资源、LogTests 与 native adapter 相关 AudioBinding 入口。现有断言若与真正获采用语义冲突，先给独立依据，不为通过删除／放宽；不新增 case 或拓宽矩阵。

## 实施交付的证据边

- 当前没有 ring 溢出继续、60 秒续期、首评估前真实 Comp、clipboard 和全退出链的现成自动用例。源码闭合不等于已动态通过；这些项由实施 owner 明确记未覆盖，CO 按实际授权决定接受层。禁止把新单测改叫 probe／fixture／runner，或让玩家凑次数／制造异常。
- 若后续获准普通原生观察，只走正常“打开→开始→复现→停止→复制”及常规关窗／重开／离图，检查实际字体、状态、目标不漂移、报告可贴；不重跑已接受性能和十五动作矩阵，不读取真实 Config／存档或复制私人日志来补证。
- 阶段 B 的 build／现有检查和包交付已依 WO r2 执行；本 tasks 不自行授予原生权限。下一 owner 为 CO，负责本地技术收取及普通原生接受安排；worker 不自动安装、运行游戏或执行 clipboard。

本地完成依据：REPORT-B.md 当前 r2、推荐的独立 delivery-r2 及其候选身份／实际保存的标准 stage 收据；r1 保留历史身份。收尾只澄清 US panel／FL.UiKit UI-debug 用途、明确 W／槽细节未捕获，并补完整同内容 ZIP／USE。既有检查覆盖及未覆盖项按报告分层；上述勾选不表示新容量／续期／clipboard 或真实退出链已动态运行。阶段 B 的执行许可来自 WO r2，不由本 tasks 自行扩权。

