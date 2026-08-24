# 私有通用 UI 库设计规格（FerriteLib UiKit）

> 状态：已接受（2026-08-24 主会话定稿）。
> 最终命名：packageId / XML 作用域 = `coahuilite.ferritelib.uikit`；C# 命名空间 / 程序集 = `FerriteLib.UiKit`；品牌/显示名 = FerriteLib UiKit。
> 设计原则：**库必须保持中立，与 US 路由核心的中立性一致**——不引用任何业务类型、不携带任何产品字面量。

## 1. 背景与目标

- Universal Squeaker（US）的 UI 组件化实现获得好评，且项目未来不止一个模组。
- 需要把“XML 编排引擎 + 基础 UI 组件”从 US 主程序集中抽出来，做成一个**私有通用 UI 库**。
- 现阶段**只服务 US**，用 US 丰富的 UI 需求驱动库的接口演进；库成熟后再拆成私有依赖 Mod 供多个内部模组使用。

## 2. 非目标

- 不做公开/通用 UI 框架，不做 uGUI/UI Toolkit 支持（RimWorld 只有 Verse IMGUI 路径）。
- 不做反射魔法、DI 容器、代码生成器；组件注册使用显式手写注册表。
- 不在 XML 清单中嵌入代码/表达式/业务逻辑。
- 不要求 XML 热重载能新增 C# 组件 Kind（新增 Kind 必须重新编译 DLL）。

## 3. 中立性原则（硬性红线）

通用库必须像 US 路由核心一样保持中立：

- 不引用 `UniversalSqueaker.*` 或其他业务模组的任何类型。
- 不引用 `SqueakyRatkin.*`、不出现 `SR_`/`US_` 产品前缀、不出现 Ratkin/Kiiro 等产品字面量。
- 视图数据经 `object ViewState` 透传，库不解释业务 DTO。
- 命令经中立 `UiCommand(Name, Payload)` 透传，由各业务模组自行解释。
- 文本测量经 `ITextMetrics` 接口注入，生产用 Verse，测试用桩。

## 4. 命名与作用域

| 层面 | 值 |
|---|---|
| packageId / XML `Source` 作用域 | `coahuilite.ferritelib.uikit` |
| C# 根命名空间 | `FerriteLib.UiKit` |
| 程序集 | `FerriteLib.UiKit.dll` |
| 测试项目 | `tools/FerriteLib.UiKit.Tests/` |
| 未来私有依赖 Mod packageId | `coahuilite.ferritelib.uikit` |
| 库自带组件作用域 | `core`（也可写作 `ferritelib.uikit/core/...` 的长形式） |

## 5. 分阶段路线

```text
阶段 A（当前）：独立 DLL，只服务 US
    Source/FerriteLib.UiKit/         ← 通用 UI 库（独立 csproj）
    Source/UniversalSqueaker/        ← US 主程序集，引用 FerriteLib.UiKit
    tools/FerriteLib.UiKit.Tests/    ← 通用库纯逻辑单测

阶段 B（库成熟后）：拆成私有依赖 Mod
    coahuilite.ferritelib.uikit（私有 UI 库 Mod）
        把 FerriteLib.UiKit.dll 作为该 Mod 的 DLL
        其他内部模组 <modDependencies> 依赖它
        US 特有的组件可继续留在 US 或按需上提为通用组件
```

阶段 A 现在就按“多模组作用域”设计注册表，但实际只有 US 一个消费者；阶段 B 不需要改架构，只是把 DLL 挪进一个 Mod。

## 6. 架构设计

### 6.1 分层

```text
[ 通用 UI 库 DLL ]（FerriteLib.UiKit）
   ├─ LayoutManifest / LayoutEngine / WidgetRegistry / WidgetContext
   ├─ IWidget / UiCommand / ITextMetrics / UiPageState
   └─ 基础组件：chrome/banner、chrome/footer、input/mode-row 等

[ 业务模组 ]（如 coahuilite.universalsqueaker）
   ├─ 依赖 FerriteLib.UiKit
   ├─ 启动时向共享注册表注册自己的 scope + 产品组件
   └─ 提供自己的 XML 清单（Source=coahuilite.ferritelib.uikit 或业务 packageId）
```

### 6.2 目录结构（阶段 A）

```text
Source/
├─ FerriteLib.UiKit/
│  ├─ FerriteLib.UiKit.csproj
│  ├─ Layout/LayoutManifest.cs
│  ├─ Layout/LayoutEngine.cs
│  ├─ Registry/WidgetRegistry.cs
│  ├─ Context/WidgetContext.cs
│  ├─ Commands/UiCommand.cs
│  ├─ Metrics/ITextMetrics.cs
│  └─ Widgets/                     # 只放真正通用的基础组件
│     ├─ IWidget.cs
│     ├─ ChromeBannerWidget.cs
│     ├─ ChromeFooterWidget.cs
│     ├─ InputModeRowWidget.cs
│     └─ ...
├─ UniversalSqueaker/
│  ├─ UniversalSqueaker.csproj      # 引用 FerriteLib.UiKit
│  ├─ UI/Widgets/                  # US 专属组件
│  │  ├─ VoicePackRowWidget.cs
│  │  ├─ RaceLayerWidget.cs
│  │  └─ ...
│  ├─ UI/UsWidgetRegistrar.cs       # 启动时注册 US scope
│  └─ ...（现有业务逻辑不变）
└─ tools/
   └─ FerriteLib.UiKit.Tests/
```

### 6.3 核心接口（草案）

```csharp
// 通用库
public interface IWidget
{
    string Kind { get; }   // const，与 XML 一致
    float Measure(WidgetContext ctx);
    void Draw(Rect rect, WidgetContext ctx, Action<UiCommand> emit);
}

public sealed class WidgetContext
{
    public string Source { get; }             // 当前 XML 来源作用域
    public object? ViewState { get; }         // 模组自己投影的只读 DTO
    public ITextMetrics Metrics { get; }
    public UiPageState State { get; }         // 滚动/搜索/帮助等通用页面状态
}

public readonly struct UiCommand
{
    public readonly string Name;      // "TogglePack" / "SetMode" ...
    public readonly object? Payload;  // 模组自己的参数 DTO
}
```

### 6.4 作用域注册表

Kind 标签需要区分来源模组时，采用**作用域注册表 + XML 根 Source + 回退 core**：

```csharp
public static class WidgetRegistry
{
    private static readonly Dictionary<string, Dictionary<string, Func<IWidget>>> Scopes = new();

    public static void Register(string scope, string kind, Func<IWidget> factory) { ... }

    public static IWidget Resolve(string scope, string kind)
    {
        if (Scopes.TryGetValue(scope, out var reg) && reg.TryGetValue(kind, out var f))
            return f();
        if (Scopes.TryGetValue("core", out var core) && core.TryGetValue(kind, out f))
            return f();
        throw new UnknownWidgetKindException(scope, kind);
    }
}
```

- 每个模组有自己的词表；
- 公共组件放在 `core`，所有模组都能用；
- 两个模组都叫 `row/voice-pack` 也不冲突，因为 `(scope, kind)` 唯一。

### 6.5 XML Manifest 示例

```xml
<UiPage Schema="1" Source="coahuilite.universalsqueaker">
  <Widget Kind="row/voice-pack" RowHeight="Fixed:74" Data="SelectedDomain.Packs" />
  <Widget Kind="chrome/banner" Bind="BannerText" Font="Tiny" Height="Auto" />
</UiPage>
```

- `Source` 声明业务模组作用域；
- `row/voice-pack` 查业务模组注册表；
- `chrome/banner` 查业务模组未找到 → 回落到 `core`。

### 6.6 两遍同步引擎

```text
每帧：
  ① 绑定数据（把 ViewState/State 填进 WidgetContext）
  ② Measure 一遍：遍历 Manifest，每个 Widget 调 Measure，累加 contentHeight
  ③ 钳制滚动位置：ScrollPosition = Clamp(0, contentHeight - viewHeight)
  ④ Draw 一遍：遍历同一棵 Manifest，用 Measure 阶段算好的 Rect 调 Draw，收集 UiCommand
  ⑤ 帧末：业务模组解释并执行命令（如 VoicePacksPageModel.ExecuteAll）
```

关键纪律：

- Measure 必须是纯函数（不画、不改状态）；
- Draw 只消费预计算 Rect；
- 命令缓冲到 Draw 之后执行；
- 文本测量按 `(text, font, width, style)` 缓存；
- 滚动内容轴不使用 grow/weight；
- 每个交互节点使用稳定 XML `id` 派生控件 ID，不用列表下标。

### 6.7 热重载（事务式）

```text
文件变化（防抖 200~500ms）
   → 解析新 XML
   → 校验 schema / Kind 已注册 / 资源限制（深度、节点数、禁用 XXE）
   → 实例化 shadow 树
   → 成功才替换当前 Manifest
   → 失败保留旧树 + usdiag ui.manifest.invalid
   → 按稳定 id 恢复滚动/搜索/选中状态
```

## 7. 组件管理 SOP（新增一个 Kind）

1. 写一个类实现 `IWidget`（`Measure` + `Draw`），一个 Kind 一个文件；
2. 类里定义 `public const string Kind = "<lower-kebab-case>"`（建议带领域前缀：`chrome/`、`input/`、`row/`、`composite/`）；
3. 在 `WidgetRegistry` 里加一行 `Register(scope, Kind, () => new XxxWidget())`；
4. 在对应 XML 清单里使用该 Kind；
5. 加一致性测试（Manifest Kind 都在注册表、注册表 Kind 都有实现、无反射 API）。

## 8. 测试与门禁

- `tools/FerriteLib.UiKit.Tests/`：stub `ITextMetrics`，覆盖 Measure/Draw 高度一致性、注册表解析、XML 解析失败保留旧树、作用域回退。
- `scripts/verify-local.ps1` 增加：
  - `FerriteLib.UiKit.dll` 与 `UniversalSqueaker.dll` 都在 `1.6/Assemblies/`；
  - 两个 DLL 版本/构建标签一致；
  - 通用库源码 grep 无业务字面量（`UniversalSqueaker`、`SqueakyRatkin`、`SR_`、`US_`、Ratkin、Kiiro 等）。
- `Manifest` 校验可做成 `tools/validate-ui-manifest.ps1` 接入门禁。

## 9. 构建/打包调整

```text
FerriteLib.UiKit.csproj    → 输出到 1.6/Assemblies/FerriteLib.UiKit.dll
UniversalSqueaker.csproj   → 输出到 1.6/Assemblies/UniversalSqueaker.dll
```

- `stage-package.ps1` 已递归复制 `1.6/`，两个 DLL 自动进包；
- 构建顺序改为：先 UiKit 库、再 US；
- `pack-dev.ps1`/`verify-local.ps1` 相应增加依赖顺序与门禁。

## 10. 开发大纲（阶段 A）

> 这是压缩会话后的接续大纲。按顺序执行，每步可独立提交。

1. **建库项目骨架**
   - 新建 `Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj`（net472，参照 US csproj：TreatWarningsAsErrors、输出到 `..\..\1.6\Assemblies\`）。
   - 新建目录 `Layout/`、`Registry/`、`Context/`、`Commands/`、`Metrics/`、`Widgets/`。
   - 建立 `AssemblyInfo` 或 csproj 版本标签（版本先与 US 同步或独立 0.1.0-dev，待定）。

2. **核心接口**
   - `IWidget`、`WidgetContext`、`UiCommand`、`ITextMetrics`、`UiPageState`、`UnknownWidgetKindException`。
   - 全部位于 `FerriteLib.UiKit` 命名空间，零业务引用。

3. **作用域注册表**
   - `WidgetRegistry.Register(scope, kind, factory)` / `Resolve(scope, kind)` / `KnownKinds`。
   - `core` 作用域常量；解析回退逻辑。

4. **Manifest 解析与校验**
   - `LayoutManifest`：解析 XML → `UiElementSpec` 树；校验 schemaVersion、Kind 已注册、深度/节点数限制、禁用外部实体（XXE）。
   - 解析失败抛出带 `kind`/行的可读异常；热重载场景保留旧树。

5. **两遍同步引擎**
   - `LayoutEngine.Measure` 累加高度并生成 Rect 列表；
   - `LayoutEngine.Draw` 消费 Rect 并收集 `UiCommand`；
   - 滚动位置统一 `Clamp`；
   - 内容高度 `Math.Max(1f, ...)` 防零/NaN。

6. **首批 core 组件**
   - 从 US 现有实现迁移/重写：`ChromeBannerWidget`、`ChromeFooterWidget`、`InputModeRowWidget`（可再加 `SectionHeader`、`EmptyState`）。
   - 每个组件一个文件，`Measure`/`Draw` 字体与宽度一致。

7. **测试项目**
   - 新建 `tools/FerriteLib.UiKit.Tests/`（可先不带 Verse，纯 stub metrics）。
   - 覆盖：注册表回退、Manifest 解析失败、Measure/Draw 高度一致性、滚动 clamp、未知 Kind 报错。

8. **US 侧接入**
   - `UniversalSqueaker.csproj` 引用 `FerriteLib.UiKit`。
   - 建 `UI/UsWidgetRegistrar.cs`：注册 US 专属组件到 scope `coahuilite.universalsqueaker`。
   - 把 US 设置页逐步切换到 `LayoutEngine`（旧 UI 可并存，先加开关或直接替换）。
   - 准备 `UI/Layout.xml`（Source=coahuilite.universalsqueaker）作为 US 页面清单。

9. **构建/打包/门禁**
   - 调整 `pack-dev.ps1`/`stage-package.ps1`：先构建 UiKit 再构建 US；确认两个 DLL 进包。
   - `verify-local.ps1` 增加：两个 DLL 存在、版本一致、中性 grep、注册表/Manifest 一致性测试。
   - 跑通 Dev/Release 构建 + 现有 kernel/config/log 测试 + 新 UiKit 测试。

10. **阶段 B 准备（不立即执行）**
    - 记录私有 Mod `coahuilite.ferritelib.uikit` 的拆分 checklist：About/LoadFolders/依赖；US 改为 `<modDependencies>`；上提通用组件的标准。

## 11. 开放问题（已收敛）

- [x] 库正式命名：`coahuilite.ferritelib.uikit` / `FerriteLib.UiKit`。
- [ ] 基础组件边界：哪些上提到 `core`（建议先只上提 chrome/input 极简原语，业务复合组件留在 US）。
- [ ] 共享库版本化策略（阶段 B 需要）。
- [ ] `UiPageState` 通用字段清单（滚动/搜索/帮助/选中项）。
- [ ] 命令解释器接口：各业务模组如何把 `UiCommand(Name, Payload)` 映射到自己的写入桥。
- [ ] 阶段 A 的库版本号策略（与 US 0.1.0-dev 同步还是独立）。