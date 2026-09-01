# FerriteLib.UiKit 重建：验证与验收证据链

> 目标：每个阶段都能用可观察证据判断“成立、受限或失败”。
> 关键限制：runtime stub 只能证明纯逻辑和调用契约；RimWorld 游戏内证据才足以证明真实 IMGUI、WindowStack、翻译、FloatMenu、clip/hotControl 与分辨率行为。

## 1. 总体主张与失败模式

### 主张 A：UiKit 是可预测的 IMGUI-native 内核

需要保持：同一 `Event.current` 只被消费一次；布局先 Measure/Arrange，再在同一 rect 同步绘制；结构作用域严格成对；无进程级控件状态。

可能推翻主张的证据：重复点击、错位点击、MouseDown/Drag/Up 丢失、跨窗口 dropdown/focus 泄漏、未知 group/scroll 导致后续 UI 损坏。

### 主张 B：XML 负责结构，C# 负责动态业务

需要保持：Schema=2 的 XML 能在 Host 创建期验证 ID/kind/静态 schema；动态列表仍由 C# 复合组件迭代；无 Repeat/条件/业务脚本。

可能推翻主张的证据：绘制中途才发现 binding 缺失、XML 读取业务字典执行表达式、重复/缺失 ID 未拒绝、未知 kind 静默吞掉。

### 主张 C：临时状态严格按 Window/Overlay 隔离

需要保持：关闭 Host 释放 session；重开创建新 session；两个 host 的 scroll、popup、drag、focus、熔断互不影响。

可能推翻主张的证据：第二个窗口继承下拉选项、旧 session 的熔断永久存在、关闭后仍有 hotControl、Overlay 改变 Settings 状态。

### 主张 D：US 设置页功能完整且 fallback 可恢复

需要保持：Basic/Tuning/Packs 功能入口、业务写入、下拉/stepper/chart/FilterBar 均可操作；关键控件异常只熔断当前 session 槽位，关闭重开后主控件重新尝试；整页 schema/binding 故障有稳定 Vanilla fallback。

可能推翻主张的证据：首次打开崩溃、点击无反应、控件错位、fallback 后重复绘制、关闭重开仍不能恢复、业务值写错层级。

### 主张 E：Overlay 共享内核但不依赖 Window

需要保持：Camera Indicator 使用相同 registry/binding/theme/session contract，独立于设置窗口创建/销毁，遵守屏幕安全区和可选输入穿透。

可能推翻主张的证据：无设置窗口时不能显示、分辨率变化出界、Overlay 截获不应截获的输入、关闭设置页后 overlay session 未释放。

## 2. 自动验证门

### V0 契约事实门

- 先画实际生产调用图，再画目标调用图；标出新 Kernel 是否有真实 US caller。
- 检查 `Id`/`Bind`、fixed/weight、viewport/content、binding/action 的唯一语义。
- 检查 ownership、XML ID、Kind、binding、翻译 key 和资源路径唯一性。
- 产物：契约差异清单和唯一下一步；存在未裁决歧义时不得进入实现扩张。

### VR 真实消费者门

VR 必须证明一条真实 US consumer slice，而不是内联测试 fixture：

- 读取真实嵌入 Schema2 resource；
- 调用真实 `UniversalSqueakerSettings` getter/setter 或 typed action；
- 由真实 Settings Window-owned Host 完成生命周期；
- 创建期验证 Kind、属性和 binding；
- 关闭/重开 session 隔离；
- 至少一个关键失败路径有 session fallback；
- 测试 runner 执行真实 slice regression。

VR 未通过时，Kernel 只能标记为 isolated slice，禁止全量 US 迁移。

### V1 UiKit runtime harness

命令：

```text
scripts/verify-local.ps1
```

至少覆盖：

- Schema=2 安全解析、未知 kind、重复 ID、缺失 binding、属性 schema 错误。
- Measure/Arrange：fixed/auto/weight、min/max、padding/gap、alignment、breakpoint、hidden、scroll clamp、自然文本高度、`TryGetElementY`。
- UiSession：Begin/End、窗口隔离、关闭清理、ContentRevision。
- Native interaction：Button/slider/text/dropdown/range graph 的 Event.current 与 hotControl 契约；不得重新引入 deferred queue。
- UiGuard：异常堆栈、每 session 去重、槽位 fallback、恢复。
- 真实 Schema2 resource/Host creation regression；不能只运行 `Source="test"` 的内联 fixture。

如果现有脚本无法覆盖新契约，应修改 harness；不得只降低门禁。harness 通过不等于 VR 通过，更不等于游戏内验收通过。

## 3. US 自动与构建验证

- `dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj --no-restore`（或仓库现有等价命令）。
- `dotnet build tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj --no-restore`。
- 运行测试可执行文件，确认 exit code 0 和每个行为断言通过。
- 扫描旧符号：`LayoutEngine` flat API、`UiPageState` 临时字段、`UsCommandPayload`/二次命令翻译、进程级静态交互状态、旧 `Begin/End` deferred 队列。
- 扫描所有 US callsite，确认没有遗漏迁移、没有兼容 shim。

## 4. 游戏内设置页验收（必须截图/日志/记录）

使用 dev 包，在真实 RimWorld 1.6 中至少记录：

### 800×600

- 首次打开不抛 `Value cannot be null. Parameter name: source`。
- 关闭后立即重开得到干净 session。
- 左侧分组导航和滚动定位正确；右侧帮助可读且不遮挡。
- Basic：全局音量、距离 preset、scale toggles、camera indicator 可写入。
- Tuning：层/域选择、mood stepper-slider、scope 选择可写入正确 `(race,xeno,layer,domain)`。
- Packs：race/xenotype/author filter、下拉、preset import/forget、voicepack selection 可用。
- 衰减图：hover 高亮，MouseDown→Drag→MouseUp 改值且不破坏后续 UI。
- 文本：无截断、无重叠、翻译键显示正确。

### 1280×720 和 1920×1080

- 响应式列数/帮助面板切换符合约束；宽度变化不需要重启窗口。
- popup/FloatMenu 在页面和窗口坐标正确；滚动时不漂移。
- 所有控件热区与视觉 surface 一致。

### 故障恢复

- 通过可控异常或缺失数据触发关键控件 fallback。
- 同一 session 后续帧不重复刷屏；fallback 尺寸稳定。
- 关闭窗口后重新打开主控件重新尝试，不继承旧熔断。
- schema/binding/容器错误进入整页诊断 fallback，日志含节点 ID、kind、布局路径和完整堆栈。

## 5. Overlay 验收

- 无 Settings Window 时创建/显示/隐藏 Camera Indicator。
- 地图切换与当前 map 缺失时安全退出。
- 800×600、1280×720、1920×1080 下位于安全区，不遮挡关键日期栏操作。
- 输入穿透开关实际影响点击；Overlay 不建立第二套 hotControl。
- 设置窗口反复开关后，Overlay 仍只有一个 session，关闭/禁用时完全释放。
- 语言切换后翻译更新；不使用个人本地路径或敏感信息作为证据。

## 6. 验收输出格式

每个阶段必须记录：

- **Claim**：本阶段声称什么。
- **Evidence**：运行的确切命令、游戏场景、日志/截图位置。
- **Result**：成立 / 受限 / 失败。
- **Remaining uncertainty**：无法由当前证据证明的部分。
- **Next action**：唯一下一步，不扩大范围。

缺少游戏内条件时，只能声称自动验证成立；不得把 stub harness 结果写成游戏内 UI 已验收。
