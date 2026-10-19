# 0.6.0 S2：来源、资源绑定与保留的选音链

S2已接入实际启动和正常播放选择路径。普通音频作者继续声明普通SoundDef，再用原有`SqueakVoicePackDef`或受控S1的`UsVoicePackDef`引用其defName；不填写资源owner、不新增注册Def，也不手修原版grain池。

## 作者继续引用SoundDef，不冒认其提供者

[LegacySupply.xml](examples/0.6/LegacySupply.xml)使用保留的普通格式；[AudioOnly.xml](examples/0.6/AudioOnly.xml)使用S1格式。两者都要求对应Race和音频模组实际加载。引用者可以是独立模组：条目归因仍是引用包，`AudioId`由实际音频来源的规范packageId和最终defName组成。规范owner使用实际`asset.mod.PackageId`的小写加载身份；原版真实`_steam`等后缀保留，不以PlayerFacing显示文本合并资源。普通声音引用不再要求被引用资源也使用`US_`前缀；包自身命名规则保持。

独立发行声音时，自己的SoundDef名和素材目录仍须全局唯一。只改About.packageId不能消除同名SoundDef冲突。Work的一个SoundDef包含三个OGG时，US仍只有一个资源候选，原版内部池负责选clip。

声音包启停重建有效选池，不重新装载资源。模组增减须重启。玩家已有最终回退表仍按原defName保存；适配投影为完整AudioId，不改写旧Config含义。旧字符串动作入口、scope、许可和时钟条件没有新增注册前提。

## 同一最终对象贯穿启动、选池和消费

1. Mod构造时安装窄Harmony观察。`CombineIntoUnifiedXML`的postfix只读SoundDef类型、节点、原名和真实`asset.mod`。原始观察先完成临时批次，成功后才发布；中途异常保留原因，整次缺证批次不能当成唯一来源。
2. 两个实际loader的prefix在原版MayRequire筛选后读取`XmlInheritance.GetResolvedNodeFor`的最终声明；即使随后构造失败也计入重复。postfix另记录成功对象。观察不跳过原版、不改XML、Def或assetlookup。
3. 同节点且实际asset仍匹配时，字段patch可改变最终defName，资源身份使用新名。整根替换的新节点没有来源时，只在原名来源唯一、最终声明唯一且无反证时沿原身份绑定。删除不复活；patch新建但无法证明来源的资源不绑定。
4. Mod最初排入的回调只把真正初始化追加到LongEventHandler队尾。目标1.6会继续消费增长的队列，原版ResolveReferences音频回调先完成；不是固定延时或逐帧重试。
5. 读取原版`SubSoundDef.resolvedGrains`，核对`distinctResolvedGrainsCount`与实际Distinct数。空池、半池、未知粒型、空clip和未Loaded数据均拒绝；合法Silence允许共存。US不重新枚举AudioGrain找一个好clip，也不修原版池。
6. `AudioId → 最终SoundDef`发布一次。正常`SqueakKernelAdapter.BuildEntries/BuildBuiltIn → SqueakPoolRegistry.Select → ToChoice`和受控S1后端消费同一绑定。缺绑定不退裸名；已选上下文和变体随结果保留，诊断不重抽。

资源冲突或不安全只隔离该SoundDef及其引用。无法完成的来源观察按其缺证批次拒绝依赖绑定，不能假装单资源证据完整。包身份、weight等其他无效字段仍按原边界处理，未借S2冻结新的D6默认。

## 保留的路由政策

- Fallback、Remix、Vanilla对应Kernel Off、Disabled四模式分开；Disabled在真实入口旁路，纯选池也零候选/零随机，不转成Off。
- Xenotype常规层可以沿同Race合法父路径；PackFallback只查询本次精确域，不能读取Race包fallback冒充Xeno fallback。
- Remix只在非空层等权；包权重、变体权重和声音内等权保持；完整AudioId不会改变原先按defName排序的固定roll映射。精确年龄声明存在但过滤后为空时，不解锁全年龄；资源投影也保留空的精确年龄声明。彩蛋独立过滤。
- NonPawn的Family、对象命名空间、Target与Pawn Race/Xeno字段分开，父路径为自身。目录去重/排序和诊断键保留完整身份；`NonPawn:A/Ratkin`、`NonPawn:B/Ratkin`与Pawn Ratkin互不借池或最终回退。本轮没有NonPawn宿主/播放器。
- S1仍只有一份机会/结算持有者。受控入口遵守当前来源模式，旧玩家日常调度不切换；W20/H40/actions120仍只是显式fixture。

## 自动证据与真实游戏证据分别使用

`verify-local.ps1`沿原17门追加gate18正常适配及gate19独立失败批次场景，未修改冻结stub。完整原始运行日志及最终包身份由本轮交付记录保存。

- **纯生产源（无游戏引用、无同名stub）：** `UniversalSqueakerOneShotTests`保留S1原173检查，另测完整身份、15资源只撞一条、NonPawn隔离、四模式、权重、年龄/egg及用途标签roll。路由golden只记录Selected/NoCandidate；独立请求golden调用真实OneShotDecision，注入四种后端结果，记录事实tick、完整条目/AudioId、真实调用数和结算。两条轨迹各运行两遍且与各自独立golden比较。旧Kernel corpus保持字节零差异，不批量更新。
- **适配fixture：** `UniversalSqueakerAudioBindingTests`编译真实观察/资源池/正常调用投影源码，使用明确标注的控制double，并让真实Harmony执行两条loader观察。覆盖字段及defName patch、整根替换、删除、最终重复且其中一项构造失败、MayRequire跳过、半池/坏clip、实际对象相等和启停不重载。独立子进程`--partial-observation`证明半原始索引不能因后续删除冲突节点而猜出owner。该工程不是纯层或原版加载执行。
- **1.6方法体依据：** CO的`Coordinator/us/evidence/S2-20261019/SEAMS16.md`及同目录原始回执，核对远端1.6.4850和本机目标1.6.4871/Unity2022.3.35f1。产品编译引用固定Krafs.Rimworld.Ref 1.6.4871。
- **原生运行/听感：** 未启动游戏，真实15个Ratkin SoundDef/41 OGG完整加载、实际音频解码/首次播放和玩家听感仍待维护者观察。fixture不关闭这些验收。

## S5可用接缝与必须保留的缺口

目标1.6的SampleOneShot/SampleSustainer在`Play`前已读`SanitizedVolume`并执行首次Update。仅在TryMakeAndPlay的postfix标记太晚：S5须让显式请求归属在首次音量读取前可见，再在正常Sample更新应用独立局部L和当前全局G，实例距离写AudioSource端点，不写共享Def。

后续Sustainer采样须从实际父实例传播归属；End/Cleanup辅助音会新建SoundInfo，不能靠原参数自动传播。世界/相机AudioSource会池化复用，归属应绑定请求/Sample并在Cleanup释放，不能绑共享Def或可复用source。0→恢复不得除旧G。CO的目标版本方法体支持这条实现路线；本轮没有实现完整在播总控、辅助音归属传播或原生听感验证。

**必须由S5替换的现存路径：** `Settings.ApplySettingsRuntimeSideEffects → CompSqueaker.ApplyDistanceRange → SubSoundDef.distRange`仍活动并写共享定义。借用Core/他方同一SoundDef时，旧距离设置也可能影响非US声音。PM明确允许它不阻断本轮S2身份/来源/路由/绑定技术接收，但它阻断C11/V14/S5及整个0.6产品接受；不是可选优化。本轮保持旧距离功能，未新增共享定义写入，也不以暂改恢复/Def克隆规避。此候选仅供隔离Dev技术验证，不推荐一般安装或发布。

## 复现本轮边界

在仓库根，给验证子进程设置`TEMP/TMP=dist/test-temp`，以显式`-FerriteLibArtifactPath`选择任务书固定的0.7 Dev DLL，再执行`verify-local.ps1 -DevelopmentCarrier`。FL范围保持`[0.7.0,0.8.0)`，不是0.8消费。`build-dev.ps1`在同一输入下生成隔离0.6.0 Dev；包自身version.txt及程序集收据标明实际commit/载体，不凭移动HEAD推定身份。

V01/V08/V09/V10的本轮纯层和适配责任有自动证据；原生加载/声音观察未关闭。V14本轮交目标版本可行性依据，完整责任仍在S5；S3动作/配置恢复和S4默认/调制不因S2完成而视为已实现。

## 本轮相对基线的含义

| 基线含义 → S2含义 | 保留/变化与反例 | 语料处理 |
| --- | --- | --- |
| 裸defName在运行时重新查库 → 实际加载owner/最终名绑定到同一对象 | §5.1已定要求；引用包A不能冒认B的资源，缺绑定不得退裸名 | 旧选池输入仍可复核；新增完整身份/实际消费fixture |
| 只凭最终数据库不能见到被覆盖重复 → loader前的最终有效声明也计重复 | 两同名且一个构造失败仍拒该资源，其他十四条保留 | 新增独立来源/失败批次用例，旧corpus零差异 |
| 自行枚举得到好clip → 原版实际池完整且安全才可用 | 好clip与坏池不能互相抵消；已由原版排除坏文件且完整池合法可保留 | 适配fixture分测半池、空clip、未知grain与Loaded状态 |
| Disabled靠外层提前门 → 外层保留且Kernel另有Disabled值 | 行为不变；直接选池也不能把Disabled解释为Off最终回退 | 新增零候选/零roll反例，不修改旧Off语料 |
| Pawn域键 → 兼容保留Pawn并新增独立NonPawn目录身份 | 不以RaceKey承载NonPawn目标；同文字跨family/namespace不借池 | 旧Pawn/xeno corpus字节零差异；新独立域用例 |
| 原声音按defName排序、年龄/egg、包/变体权重 → 原政策保持 | AudioId的owner不能改变固定roll对应的声序；精确年龄坏绑定不解锁全年龄 | 原corpus不更新，新增跨owner声序与带用途roll断言 |
