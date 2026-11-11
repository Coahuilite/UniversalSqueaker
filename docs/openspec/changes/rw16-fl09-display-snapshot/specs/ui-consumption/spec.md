# Spec Delta — F09 的保留准备与实时操作

状态：**proposed**。仅适用于独立 RW1.6／FL0.9 消费分支；未并入[现行 ui-consumption](../../../../specs/ui-consumption/spec.md)。UI-01／04／05／07／09 不改。

## ADDED Requirements

### Requirement: UI-10 生产 F09 显式保留准备，原生操作保持实时

US SHALL 在真实 `GameSettingsWindow → UsKernelSettingsHost.Create → b-actions` 路径显式消费已采用的 FL0.9 保留展示合同，不建立第二份页面、字段身份或 FL 公共 API。新资格仅属于明确采用的来源／作用域，不能由普通 BindValue、已有 view identity、Bump 或“本页调用”自动推断。

US SHALL 由每 Host 的生产 F09 准备 owner 组合完整当前输入：配置／定义／policy／目录与实现可用性、实际 own／effective 与来源意图、准确层／域／action、贡献 keys、row-result、本窗 details／timing／选择状态及译文／格式文化。它 SHALL 自动观察仍由旧读面可见的可变输入，并在所有自有写边更新失效；失败提交也在内。不能以公开旧写入口额外 Notify、Host、展开状态或预热为新前提。外来／不能闭合的来源继续原 fallback。

US SHALL 将已采用 FL 合同中由原 editor／native owner 提供的 draft、valid／modified、焦点／IME、提交／取消／冲突、popup／capture、scroll／clip／hit 与窗口／环境失效作为独立当前事实。若该条来源未闭合，对应作用域不得取得完整准备复用资格，原路径继续可用。US model view 没变不代表这些事实没变，不能自行缓存 native editing 的结果或复用旧 hit 凭据。

准备数据／派生几何／展示描述、必须运行的原生操作、Repaint 实际提交 SHALL 分开：未变作用域在展开后停手时不再逐现场 21 个字段重做业务投影、格式化、测量／布局验证或 widget 展示准备。允许在合法边界作完整的 root 输入观察，但不能把同一逐字段派生移入“快照捕获”再称省略。字段数由真实 canonical／贡献集合决定，不写死 21 或删减字段。

每个所需 native callback SHALL 仍完成原 ID 次序、资格、编辑、按键、读回、帮助、capture、scroll/hit 等操作；新准备／重排不得重放业务请求。IMGUI 后端在必要 Repaint 仍提交当前可见内容；仅后端确有能力时才可省对应提交。屏外不能当作无须运行原操作的授权。首次出现、重新展开、滚入、错误及变更后的第一合法呈现 SHALL 有完整当前状态和相符几何／命中，不延后到后续帧摊账。

稀疏空白继承、显式 false／0、准确 domain、persist-first 结果、原非 Live 数值确认与已有 Live 立即发布、保存 debounce／反馈／失败与恢复 SHALL 保原 owner。确认／Live 的读回仍取原 owner 的当前采用值，不以旧展示快照或请求文本冒充 read-model。记录失败／未知环境时不能晋升完整成功凭据；新作用域失败沿 FL 采用的故障／完整同步路径，不以旧几何提供当前可操作命中。页面定义、widget／binding backing 或能力替换须重新确认资格，不能沿用旧来源凭据。关闭／替换使对应凭据失效并按原 owner 清理；关闭不重放提交或销毁业务数据。

#### Scenario: 生产展开后停手
- **GIVEN** 首次完整准备已成功，生产输入、UI 交互状态、几何和度量环境均有当前凭据
- **WHEN** 展开 F09 后无变化，经过后续合法输入／Repaint 回调
- **THEN** root 仍按合同观察来源，但不逐字段重建 effective／字符串／几何／展示描述；必要 native 操作及实际重画分别发生并计量

#### Scenario: 无 Bump 的真实失败结果
- **GIVEN** 征召全局域已展开，RowResult 原为空
- **WHEN** 原公开 `TryCommitActionField("volume", "not-a-number", "us.core/Draft", "", "")` 返回 false，业务 view／配置 revision 未变
- **THEN** 自有错误来源自动失效，结果行及其几何在第一合法呈现采用，不要求调用者另作 Notify／Bump，不复算无关字段

#### Scenario: 未提交 draft 改变错误和高度
- **WHEN** 原生数值输入改变 draft／valid／modified，而业务模型未提交
- **THEN** 原 editor 立即拥有该变化；新错误、反馈、布局与 hit 在正确回调反映，其他未变字段保持准备；IME、确认、失焦和 Cancel 不被快照吞掉

#### Scenario: 旧公开写入口不补通知
- **WHEN** 自有 source 的旧 setter、提交／拒绝／恢复或公开可变状态在合法条件下改变了原读面可见的 F09 输入
- **THEN** 生产 owner 的写边或边界观察发现变化；新准备消费当前 own／effective／结果／选择，不让旧调用者新增 Host／Notify 前提

#### Scenario: Live 与确认各守原义
- **WHEN** 已有 Live 操作发生或非 Live 数值执行 Enter／失焦确认
- **THEN** 分别沿原发布条件、准确 field/domain 和同一持久化 owner 执行；操作后的失效不重放提交，不把 accepted request／physical commit／publication success 混为一项

#### Scenario: 收起、重开与滚动
- **WHEN** 同一动作收起／重开，或字段滚入／滚出当前视口
- **THEN** retained keys、节点、draft 和精确对象不重建；当前 scroll／clip／hit 与 native ID 次序仍有效，首个应绘 Repaint 显示全部适用字段，不用旧坐标或少显示一帧换收益

#### Scenario: 字体尺寸语言或关闭变化
- **WHEN** theme、宽度／breakpoint／scrollbar、字体环境、语言／格式文化变化，或 Host 关闭／替换
- **THEN** 相应几何／paint／寿命凭据失效；未知度量保持原鲜读，颜色不无端毁掉草稿；新会话不复用旧 hit／editor，也不重放旧效果

#### Scenario: 无 opt-in 的旧消费者
- **WHEN** 页面、source、widget 或后端未明确采用所需保留能力
- **THEN** 原独立入口和普通 getter／同步 Draw／写／Live／故障条件仍可用；不强制 revision、可靠环境、先 Prepare 或新增通知
