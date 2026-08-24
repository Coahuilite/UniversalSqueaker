# US 私有通用 UI 库设计规格（占位名：Coahuilite.Ui）

> 状态：已接受（2026-08-24 主会话讨论定稿）。
> 库名尚未确定，本文使用**占位名 `Coahuilite.Ui`**（程序集 `Coahuilite.Ui.dll`、命名空间 `Coahuilite.Ui`）。正式命名确定后全局替换占位名。
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

## 4. 分阶段路线

```text
阶段 A（当前）：独立 DLL，只服务 US
    Source/Coahuilite.Ui/            ← 通用 UI 库（独立 csproj，占位名）
    Source/UniversalSqueaker/        ← US 主程序集，引用 Coahuilite.Ui
    tools/Coahuilite.Ui.Tests/       ← 通用库纯逻辑单测

阶段 B（库成熟后）：拆成私有依赖 Mod
    coahuilite.usui（占位 packageId，待定）
        把 Coahuilite.Ui.dll 作为该 Mod 的 DLL
        其他内部模组 <modDependencies> 依赖它
        US 特有的组件可继续留在 US 或按需上提为通用组件
```

阶段 A 现在就按“多模组作用域”设计注册表，但实际只有 US 一个消费者；阶段 B 不需要改架构，只是把 DLL 挪进一个 Mod。

## 5. 架构设计

### 5.1 分层

```text
[ 通用 UI 库 DLL ]（Coahuilite.Ui）
   ├─ LayoutManifest / LayoutEngine / WidgetRegistry / WidgetContext
   ├─ IWidget / UiCommand / ITextMetrics
   └─ 基础组件：chrome/banner、chrome/footer、input/mode-row 等

[ 业务模组 ]（如 coahuilite.universalsqueaker）
   ├─ 依赖 Coahuilite.Ui
   ├─ 启动时向共享注册表注册自己的 scope + 产品组件
   └─ 提供自己的 XML 清单（Source=自己的 packageId）
```

### 5.2 目录结构（阶段 A）

```text
Source/
├─ Coahuilite.Ui/
│  ├─ Coahuilite.Ui.csproj
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
│  ├─ UniversalSqueaker.csproj      # 引用 Coahuilite.Ui
│  ├─ UI/Widgets/                  # US 专属组件
│  │  ├─ VoicePackRowWidget.cs
│  │  ├─ RaceLayerWidget.cs
│  │  └─ ...
│  ├─ UI/UsWidgetRegistrar.cs       # 启动时注册 US scope
│  └─ ...（现有业务逻辑不变）
└─ tools/
   └─ Coahuilite.Ui.Tests/
```

### 5.3 核心接口（草案）

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
    public string Source { get; }             // 当前 XML 来源模组（packageId 或 scope）
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

### 5.4 作用域注册表

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

### 5.5 XML Manifest 示例

```xml
<UiPage Schema="1" Source="coahuilite.universalsqueaker">
  <Widget Kind="row/voice-pack" RowHeight="Fixed:74" Data="SelectedDomain.Packs" />
  <Widget Kind="chrome/banner" Bind="BannerText" Font="Tiny" Height="Auto" />
</UiPage>
```

- `Source` 声明 US 作用域；
- `row/voice-pack` 查 US 注册表；
- `chrome/banner` 查 US 未找到 → 回落到 `core`。

### 5.6 两遍同步引擎

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

### 5.7 热重载（事务式）

```text
文件变化（防抖 200~500ms）
   → 解析新 XML
   → 校验 schema / Kind 已注册 / 资源限制（深度、节点数、禁用 XXE）
   → 实例化 shadow 树
   → 成功才替换当前 Manifest
   → 失败保留旧树 + usdiag ui.manifest.invalid
   → 按稳定 id 恢复滚动/搜索/选中状态
```

## 6. 组件管理 SOP（新增一个 Kind）

1. 写一个类实现 `IWidget`（`Measure` + `Draw`），一个 Kind 一个文件；
2. 类里定义 `public const string Kind = "<lower-kebab-case>"`（建议带领域前缀：`chrome/`、`input/`、`row/`、`composite/`）；
3. 在 `WidgetRegistry` 里加一行 `Register(scope, Kind, () => new XxxWidget())`；
4. 在对应 XML 清单里使用该 Kind；
5. 加一致性测试（Manifest Kind 都在注册表、注册表 Kind 都有实现、无反射 API）。

## 7. 测试与门禁

- `tools/Coahuilite.Ui.Tests/`：stub `ITextMetrics`，覆盖 Measure/Draw 高度一致性、注册表解析、XML 解析失败保留旧树、作用域回退。
- `scripts/verify-local.ps1` 增加：
  - `Coahuilite.Ui.dll` 与 `UniversalSqueaker.dll` 都在 `1.6/Assemblies/`；
  - 两个 DLL 版本/构建标签一致；
  - 通用库源码 grep 无业务字面量（`UniversalSqueaker`、`SqueakyRatkin`、`SR_`、`US_`、Ratkin、Kiiro 等）。
- `Manifest` 校验可做成 `tools/validate-ui-manifest.ps1` 接入门禁。

## 8. 构建/打包调整

```text
Coahuilite.Ui.csproj     → 输出到 1.6/Assemblies/Coahuilite.Ui.dll
UniversalSqueaker.csproj → 输出到 1.6/Assemblies/UniversalSqueaker.dll
```

- `stage-package.ps1` 已递归复制 `1.6/`，两个 DLL 自动进包；
- 构建顺序改为：先 Ui 库、再 US；
- `pack-dev.ps1`/`verify-local.ps1` 相应增加依赖顺序与门禁。

## 9. 开放问题

- [ ] 库正式命名（当前占位 `Coahuilite.Ui` / 程序集 / 命名空间 / 未来私有 Mod packageId）。
- [ ] 基础组件边界：哪些上提到 `core`（建议先只上提 chrome/input 极简原语，业务复合组件留在 US）。
- [ ] 共享库版本化策略（阶段 B 需要）。
- [ ] `UiPageState` 通用字段清单（滚动/搜索/帮助/选中项）。
- [ ] 命令解释器接口：各业务模组如何把 `UiCommand(Name, Payload)` 映射到自己的写入桥。

## 10. 开发规划（阶段 A）

1. 建 `Source/Coahuilite.Ui/` 空项目 + 通用接口（`IWidget`、`WidgetRegistry`、`WidgetContext`、`UiCommand`、`ITextMetrics`、`UiPageState`）。
2. 实现 `LayoutManifest` + `LayoutEngine` 两遍同步。
3. 迁移最基础的 `StatusBanner/Footer/ModeCard` 到通用库做首批 `core` 组件。
4. US 侧接入新引擎渲染现有页面（旧 UI 可暂时并存，逐步替换）。
5. 建 `tools/Coahuilite.Ui.Tests/` 和 `verify-local.ps1` 新门禁。
6. 后续用 US 的真实 UI 需求持续上提/改进通用组件。