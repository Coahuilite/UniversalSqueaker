# 使用固定 FL 0.8 构建当前 US

当前 US 0.6 消费固定 FerriteLib 0.8：源码 `fc010bd1eab8ed434e03de6241fdae89e94b1283`，Dev DLL 568832 bytes，ProductVersion `0.8.0-dev+fc010bd1eab8ed434e03de6241fdae89e94b1283`。运行前提为 `[0.8.0,0.9.0)`；这个交付支持 1.6 adapter，原生1.6待验，未交付1.7 adapter。

A002只读补丁保持公开签名/合法读取条件；ResolvedMetrics/ResolvedColours为缓存只读view，仍随正常声明更新。US无cast写该view的调用；既有生产Host已编译并定向验证普通属性/几何及override→update→remove露出当前来源，不重迁旧编辑/主题矩阵。

## 显式选择交付包，不依赖邻库移动输出

将 `$carrier` 指向依赖所有者交付的固定包中的 `1.6/Assemblies/FerriteLib.UiKit.dll`：

```powershell
$carrier = '<fixed-package>/1.6/Assemblies/FerriteLib.UiKit.dll'
./scripts/build-dev.ps1 -FerriteLibArtifactPath $carrier
```

直接构建和定向harness也传同一参数：

```powershell
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev "-p:FerriteLibArtifactPath=$carrier"
```

Directory.Build.props将显式输入传给生产、Host和嵌套构建，不再默认读旧0.7。脚本拒绝缺少输入、缺文件或范围外载体，不自动回退。生产Reference保持Private=false，编译收据和标准stage核对所选载体；只有FerriteLib发布runtime DLL，US载荷中不放第二份。测试输出会携FL及平台替身供独立运行，它们不是模组发布物。

远端CI/release的获取步骤仍指已发布`v0.7.0-rc1`。该输入不适用于当前源码，新的范围守门会明确拒绝；维护者须先安排真实可取得的0.8发布载体，再更新获取配置。本次没有猜测新tag/asset或修改远端。

## 保留现有页面、业务身份和写入时机

- 主设置、相机Overlay、Remix确认的三个嵌入Manifest继续使用原结构和kind；普通确认仍走UiPageWindow/UiWindowCatalog。音频包选择与启用是不同写通道，完整包/域身份、有效供音和Help入口保留。15包、退休链、Select三资格与Eat三参数不因UI载体改变。
- 原颜色按语义映射到`theme.Palette[UiPaletteRole]`，直接绘制用平台颜色indexer。原24个色值不变；SelectedBorder显式取原Accent、SuccessBorder取原StrongBorder，SuccessForeground取原SelectedForeground，三个Warning角色显式保原Danger有效处理，总计30角色。没有旧空边框fallback或Warning别名双轨。
- 切换主题只写Palette。不能对活跃bag重新整份ApplyTo颜色文档：0.8会同时撤销已有样式字体/度量。现路径保当前度量、几何、同Host/node、scroll、focus和draft；六方案的声明/绘制与旧有效颜色逐项对照。
- 当前活动声明中的搜索、总音量数值及保留条件下的数值控件明确`Live="true"`，保持先前即时写入。原自定义native返回值入口仍按其已有合法条件工作；没有共享Bind-key草稿缓存。S4已退休的旧节奏/心情编辑区仍为退役说明，不因迁移重新启用。
- 默认Button/Checkbox/Dropdown等在合格primary release请求；同值选择仍请求一次，Set先于ActionBind。Dropdown外按关闭会消费整个gesture，不把后续release送到底层命令。程序直接调用绑定不冒充该原生输入证据。
- 普通业务getters和既有修订钟继续使用；未强制改为响应平台或给旧custom Measure增加纯性前提。Host是展示owner，业务settings/保存仍由原Source/事务持有。

## 生命周期与验证入口

主窗口真实关闭释放Host/audit/Remix流；Overlay禁用或离图沿原路径Dispose，不把它改成隐藏缓存；确认窗口关闭释放自己的Host与捕获动作。Help折叠保声明身份和scroll。没有以失焦猜测隐藏，也没有新增定义热更/私表复制平台；实际需保留的隐藏Host可用SetDisplayDemand，隐藏/恢复保本地draft，外部模型变化在原通知边界Invalidate。

现有KernelHost `--lane`可运行`FL08 actual`、`DB Eat`、`DC Select`、`typed`、`pack-card result`、`overlay`、`confirmation`、`popup`、`live palette switch`、`US-ESC1/US-UI1`等目标。新增FL08实际页用native事件验证按下不写/释放一次、同值一次、外按不穿透、Live文本/数值、正常拒绝/规范化不重放、主题与隐藏恢复保稿、Cancel跟随当前模型及关闭订阅释放。模板按钮通过固定Dev载体的公开几何定位，再跑真实press/release，保Cancel主体，不再用逻辑ButtonOverride推断指针来源。

Host和DefinitionDirectory使用独立0.8平台替身，来源见`tools/FerriteLib08.Stubs/README.md`；旧0.7冻结快照不修改。检查读取公开palette、Host/context和有界诊断，不反射FL运行时私表；输入注入仅用于目标原生字符编辑器，明确属于平台double。

生产settings/ExposeData/原子文件与Scribe替身往返的D-C证据仍适用：本迁移不改保存格式/字段或业务事务。纯Kernel、Eat事实/音频和普通政策未改，不重录golden。真实游戏Config/Scribe、IME/字体像素、窗口栈、音频与听感以及15包人审仍各自待验。页面重新设计由后续定稿设计任务负责。
