# 打开US页，边看边改

使用已有pwsh7／.NET10及准确1.7.5000 rev1275本地资源。本工具打开的是独立宿主：黑画布没有游戏Window的底色、外框和窗口栈观感；它是0.8核心／1.7绘制参考，不是准确1.6游戏窗口。

## 先让人打开并迭代

在包根的pwsh中，选新运行目录后执行；**默认可见**：

```powershell
$package=$PWD.Path; $run=Join-Path $PWD 'us-workbench-01'
./Start-UsWorkbench.ps1 -GameRoot '<本机1.7.5000游戏根>' `
  -StateDirectory "$run/state" -AuthorDirectory "$run/author"
```

1. 左侧是真实US行为页，直接展开参数；右侧点“读取树”并选节点，看金色选中框、实际尺寸、父约束与scroll/clip。
2. 点“来源…”定位作者文件／模板；用已有编辑器改`$run/author/Layout.Schema2.xml`（含Styles），再点“重载作者”。只改自己的副本，不改包内fixed。坏稿保last-good；修复后重载继续。
3. 拖动窗口检查窄宽折行，或关顶部“布局检查”让页面占满窗口。C#修改需重建／新进程；XML重载不新开Host。

## 再让agent连接同一窗口

```powershell
Import-Module "$package/Tools/UiKitTools.Client.psm1"
$c=Connect-UiKitTools (Get-ChildItem "$run/state/*.session.json").FullName -Writable
Invoke-UiKitTools $c tools/info
Invoke-UiKitTools $c workspace/read
```

从实际Host和capture能力选择操作，查询requests/result后再读模型／草稿。重连、reload或域切换后重取目标；Handled不等于保存。完整语义／watch／来源指南见[USE](offline-workbench-use-zh.md)。

## 后台、导出、退出

需要后台运行时，在启动命令末尾显式加`-Background`并另选新state；默认启动不隐藏引擎窗口。

```powershell
Invoke-UiKitTools $c frame/export @{path="$run/view-01.png"}
Invoke-UiKitTools $c frame/state # 等Completed，勿重复排队
Stop-UiKitTools $c             # pipe只detach，保人的窗口
```

人关闭窗口，或agent对自己创建的会话调用tools/disconnect，才退出产品。1.6完整US_DEV＋FER_DEV配对另见[DEV-PAIR](offline-workbench-dev-pair-zh.md)，不要把Tools／inputs装入游戏。

本轮实际验证全部显式Background；默认可见参数链已静态核对，物理键鼠／IME与准确1.6游戏仍未验。p5/c4已实际读回Label Small/16；Editor preset fallback符合原1.7合同。