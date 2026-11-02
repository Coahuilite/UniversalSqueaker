# Core常用Work：完整入口、过程与边界

当前r2实际结构、通知新前提、Config与删除映射见[r2合同](us-kernel-0.6-refactor-zh.md)；本页保留各包原有的产品语义／来源／未决与人工关口。

目标来源：指定安装DLL元数据1.6.9676.17735，经VersionControl映射为RimWorld1.6.4871 rev591；Core/Defs/WorkTypeDefs/WorkTypes.xml、WorkGiverDefs/WorkGivers.xml及下列JobDefs。这里只公开身份、符号和机制结论，不分发反编译正文。

用户批准补齐的Core常用工作现按20个WorkType、105个具体WorkGiver逐行盘点；102个做工入口的返回/帮助分支落到78个准确Job/driver，另3个病人休息入口排除。相对原七过程新增71组Job映射，73种driver。

## 使用同一个Work合同

普通作者继续为`us.core/Work`映射一条或多条声音，不需每种Job另录、不增临时七类/完整版模式。普通启动已独立装配，Work缺省AnyOccurrence、I720，现总序和W600/H216见[普通默认](us-kernel-0.6-spec-zh.md#唯一生产-owner-与普通启动)；作者／玩家可保显式ActiveCommand。事实成立仍不保证仲裁中选或可播声音，原生人验未关闭。

捕获条件是准确Job/driver、driver.pawn为当前Pawn、driver.job与当前Job同一、未ended、当前Toil有效。观察整个过程，包括准备/走到目标/取材料/等待/执行，不承诺已挥工具、产出或成功。Pawn_JobTracker先置ended再Cleanup/ClearDriver，因此退出窗口仍保旧Toil也为False；无Job/已核但无Toil为False。未知Job/缺实现/主体或driver不符为具名Unknown，不靠名称片段、workGiver非空或反射方法存在猜True。

共用TakeInventory须准确Train来源，VisitSickPawn须准确同名WorkGiver来源；普通拾物、Joy来源探病、Goto/Wait不自动成为Work。来源只是准确机制的附加证据，不能替代Job/driver/阶段。

Worker始终是该driver的执行Pawn；患者、被救者、被押送者、动物或物品目标不变成Worker。自我治疗的执行者同时是患者仍可为Worker；单纯作为Patient/LayDown不是做工。Care/feed同时保照护者Work与摄入者Eat两个真实主体，不生成第二条业务引擎。

## 全部Core WorkGiver入口

每行源均为Core WorkGivers.xml中的具体Def及`RimWorld.<giverClass>`的JobOnThing/JobOnCell/NonScanJob，继承/帮助分支按表注明；对应JobDefs/完整driver见下一表。未满足原giver资格时不返回Job，运行捕获不重跑这些资格或把来源缺证默认为成功。

| WorkType | 具体WorkGiver / 类 | 实际返回Job / driver | 条件分支、主体或边界 | 当前状态 |
|---|---|---|---|---|
| Firefighter | FightFires / WorkGiver_FightFires | BeatFire/RimWorld.JobDriver_BeatFire | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Patient | PatientGoToBedEmergencyTreatment / WorkGiver_PatientGoToBedEmergencyTreatment | LayDown/RimWorld.JobDriver_LayDown | 自身病人休息/接受治疗，不是做工主体 | 排除：患者，不将WorkGiver排程泛化为工作 |
| Patient | PatientGoToBedTreatment / WorkGiver_PatientGoToBedTreatment | LayDown/RimWorld.JobDriver_LayDown | 自身病人休息/接受治疗，不是做工主体 | 排除：患者，不将WorkGiver排程泛化为工作 |
| Doctor | DoctorTendEmergency / WorkGiver_TendOtherUrgent | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorTendToHumanlikes / WorkGiver_TendOther_Humanlike | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorTendToSelf / WorkGiver_TendSelf | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorTendToSelfEmergency / WorkGiver_TendSelfEmergency | TendPatient/RimWorld.JobDriver_TendPatient | JobGiver_SelfTend实际TendPatient；自我照护者仍是执行者，不是被动休息 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorFeedHumanlikes / WorkGiver_FeedPatient | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoBillsMedicalHumanOperation / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorRescue / WorkGiver_RescueDowned | Rescue/RimWorld.JobDriver_TakeToBed | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorTendToAnimals / WorkGiver_TendOther_Animal | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoctorFeedAnimals / WorkGiver_FeedPatient | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | DoBillsMedicalAnimalOperation / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | TakeToBedToOperate / WorkGiver_TakeToBedToOperate | TakeToBedToOperate/RimWorld.JobDriver_TakeToBed | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Doctor | VisitSickPawn / WorkGiver_VisitSickPawn | VisitSickPawn/RimWorld.JobDriver_VisitSickPawn | 与Joy/Social重叠；仅准确VisitSickPawn WorkGiver来源 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| PatientBedRest | PatientGoToBedRecuperate / WorkGiver_PatientGoToBedRecuperate | LayDown/RimWorld.JobDriver_LayDown | 自身病人休息/接受治疗，不是做工主体 | 排除：患者，不将WorkGiver排程泛化为工作 |
| BasicWorker | Flick / WorkGiver_Flick | Flick/RimWorld.JobDriver_Flick | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| BasicWorker | BasicReleasePrisoner / WorkGiver_Warden_ReleasePrisoner | ReleasePrisoner/Verse.AI.JobDriver_ReleasePrisoner | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| BasicWorker | Open / WorkGiver_Open | Open/RimWorld.JobDriver_Open | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| BasicWorker | EjectFuel / WorkGiver_EjectFuel | EjectFuel/RimWorld.JobDriver_EjectFuel | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| BasicWorker | ExtractSkull / WorkGiver_ExtractSkull | ExtractSkull/RimWorld.JobDriver_ExtractSkull | Core明确提取过程；Ideology资格/Anomaly尸体检查仍属原giver，不据此承诺DLC独有库存 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | DoExecution / WorkGiver_Warden_DoExecution | PrisonerExecution/RimWorld.JobDriver_Execute | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | ExecuteGuiltyColonist / WorkGiver_ExecuteGuiltyColonist | GuiltyColonistExecution/RimWorld.JobDriver_ExecuteGuiltyColonist | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | ReleasePrisoner / WorkGiver_Warden_ReleasePrisoner | ReleasePrisoner/Verse.AI.JobDriver_ReleasePrisoner | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | TakePrisonerToBed / WorkGiver_Warden_TakeToBed | EscortPrisonerToBed/RimWorld.JobDriver_TakeToBed; TakeWoundedPrisonerToBed/RimWorld.JobDriver_TakeToBed | 可行走囚犯→EscortPrisonerToBed；倒地囚犯→TakeWoundedPrisonerToBed，Worker是押送者 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | FeedPrisoner / WorkGiver_Warden_Feed | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | DeliverFoodToPrisoner / WorkGiver_Warden_DeliverFood | DeliverFood/RimWorld.JobDriver_FoodDeliver | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Warden | ChatWithPrisoner / WorkGiver_Warden_Chat | PrisonerAttemptRecruit/RimWorld.JobDriver_ChatWithPrisoner | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | TakeRoamingAnimalsToPen / WorkGiver_TakeRoamingAnimalsToPen | RopeToPen/RimWorld.JobDriver_RopeToPen; RopeRoamerToUnenclosedPen/RimWorld.JobDriver_RopeRoamerToUnenclosedPen; RopeRoamerToHitchingPost/RimWorld.JobDriver_RopeRoamerToHitchingPost; Unrope/RimWorld.JobDriver_Unrope | 基类四支；targetRoamingAnimals=true、allowUnenclosedPens=true，含未封闭围栏分支 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | HandlingFeedPatientAnimals / WorkGiver_FeedPatient | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | TakeToPen / WorkGiver_TakeToPen | RopeToPen/RimWorld.JobDriver_RopeToPen; RopeRoamerToUnenclosedPen/RimWorld.JobDriver_RopeRoamerToUnenclosedPen; RopeRoamerToHitchingPost/RimWorld.JobDriver_RopeRoamerToHitchingPost; Unrope/RimWorld.JobDriver_Unrope | 基类按围栏状态/拴柱/无需拴绳分支；默认allowUnenclosedPens=false，具体条件未成立不发Job | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | Slaughter / WorkGiver_Slaughter | Slaughter/RimWorld.JobDriver_Slaughter | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | ReleaseToWild / WorkGiver_ReleaseAnimalsToWild | ReleaseAnimalToWild/RimWorld.JobDriver_ReleaseAnimalToWild | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | Milk / WorkGiver_Milk | Milk/RimWorld.JobDriver_Milk | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | Shear / WorkGiver_Shear | Shear/RimWorld.JobDriver_Shear | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | Tame / WorkGiver_Tame | Tame/RimWorld.JobDriver_Tame | Tame自身driver含先取饲料C再驯服；不是额外通用TakeInventory分支 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | Train / WorkGiver_Train | Train/RimWorld.JobDriver_Train; TakeInventory/Verse.AI.JobDriver_TakeInventory | 缺互动饲料→TakeInventory且需准确Train来源；否则Train | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Handling | RebalanceAnimalsInPens / WorkGiver_RebalanceAnimalsInPens | RopeToPen/RimWorld.JobDriver_RopeToPen; RopeRoamerToUnenclosedPen/RimWorld.JobDriver_RopeRoamerToUnenclosedPen; RopeRoamerToHitchingPost/RimWorld.JobDriver_RopeRoamerToHitchingPost; Unrope/RimWorld.JobDriver_Unrope | 同基类分支；ropingPriority=Balanced，未封闭旗标保持默认false | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Cooking | DoBillsCook / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Cooking | DoBillsCookCampfire / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Cooking | DoBillsButcherFlesh / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | CookFillHopper / WorkGiver_CookFillHopper | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; HaulToCell/Verse.AI.JobDriver_HaulToCell | 储料→HaulToContainer；先清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Cooking | DoBillsBrew / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hunting | HunterHunt / WorkGiver_HunterHunt | Hunt/RimWorld.JobDriver_Hunt | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | FixBrokenDownBuilding / WorkGiver_FixBrokenDownBuilding | FixBrokenDownBuilding/RimWorld.JobDriver_FixBrokenDownBuilding | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | Uninstall / WorkGiver_Uninstall | Uninstall/RimWorld.JobDriver_Uninstall | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| PlantCutting | ExtractTree / WorkGiver_ExtractTree | ExtractTree/RimWorld.JobDriver_ExtractTree | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | BuildRoofs / WorkGiver_BuildRoof | BuildRoof/RimWorld.JobDriver_BuildRoof; CutPlant/RimWorld.JobDriver_PlantCut | RoofUtility挡植物→CutPlant；否则BuildRoof | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | RemoveRoofs / WorkGiver_RemoveRoof | RemoveRoof/RimWorld.JobDriver_RemoveRoof | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | DeconstructForBlueprint / WorkGiver_DeconstructForBlueprint | DeconstructForBlueprint/RimWorld.JobDriver_DeconstructForBlueprint | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructFinishFrames / WorkGiver_ConstructFinishFrames | FinishFrame/RimWorld.JobDriver_ConstructFinishFrame; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct挡植物/散物/可拆建筑/矿物→CutPlant/HaulToCell/Deconstruct/Mine；否则FinishFrame | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructDeliverResourcesToFrames / WorkGiver_ConstructDeliverResourcesToFrames | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct清障四支；材料→HaulToContainer；附近需要者旧地板→RemoveFloor | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructDeliverResourcesToBlueprints / WorkGiver_ConstructDeliverResourcesToBlueprints | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; PlaceNoCostFrame/RimWorld.JobDriver_PlaceNoCostFrame; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | 清障四支；旧地板→RemoveFloor；材料/安装→HaulToContainer；零材料→PlaceNoCostFrame | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Growing | Replant / WorkGiver_Replant | Replant/RimWorld.JobDriver_Replant; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct清障四支；否则Replant安装/搬运植物 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | FillIn / WorkGiver_FillIn | FillIn/RimWorld.JobDriver_FillIn | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | Deconstruct / WorkGiver_Deconstruct | Deconstruct/RimWorld.JobDriver_Deconstruct | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | Repair / WorkGiver_Repair | Repair/RimWorld.JobDriver_Repair | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Art | PaintBuilding / WorkGiver_PaintBuilding | PaintBuilding/RimWorld.JobDriver_PaintBuilding | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Art | PaintFloor / WorkGiver_PaintFloor | PaintFloor/RimWorld.JobDriver_PaintFloor | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Art | RemovePaintBuilding / WorkGiver_RemovePaintBuilding | RemovePaintBuilding/RimWorld.JobDriver_RemovePaintBuilding | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Art | RemovePaintFloor / WorkGiver_RemovePaintFloor | RemovePaintFloor/RimWorld.JobDriver_RemovePaintFloor | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructRemoveFloors / WorkGiver_ConstructRemoveFloor | RemoveFloor/RimWorld.JobDriver_RemoveFloor | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructRemoveFoundations / WorkGiver_ConstructRemoveFoundation | RemoveFoundation/RimWorld.JobDriver_RemoveFoundation; RemoveFloor/RimWorld.JobDriver_RemoveFloor | 覆盖地板先RemoveFloor；基础层RemoveFoundation | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructSmoothFloors / WorkGiver_ConstructSmoothFloor | SmoothFloor/RimWorld.JobDriver_SmoothFloor | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Construction | ConstructSmoothWalls / WorkGiver_ConstructSmoothWall | SmoothWall/RimWorld.JobDriver_SmoothWall | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Growing | GrowerHarvest / WorkGiver_GrowerHarvest | Harvest/RimWorld.JobDriver_PlantHarvest | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Growing | GrowerSow / WorkGiver_GrowerSow | Sow/RimWorld.JobDriver_PlantSow; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell | 可种空地→Sow；挡植物→CutPlant；挡物搬开→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Mining | Mine / WorkGiver_Miner | Mine/RimWorld.JobDriver_Mine; HaulToCell/Verse.AI.JobDriver_HaulToCell | MineAIUtility先清挡物→HaulToCell；否则Mine | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Mining | Drill / WorkGiver_DeepDrill | OperateDeepDrill/RimWorld.JobDriver_OperateDeepDrill | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| PlantCutting | PlantsCut / WorkGiver_PlantsCut | HarvestDesignated/RimWorld.JobDriver_PlantHarvest_Designated; CutPlantDesignated/RimWorld.JobDriver_PlantCut_Designated | 收获designation→HarvestDesignated；砍伐designation→CutPlantDesignated | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Smithing | DoBillsMakeWeapons / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Smithing | DoBillsMachiningTable / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Smithing | DoBillsFabricationBench / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Tailoring | DoBillsMakeApparel / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Art | DoBillsSculpt / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Crafting | DoBillsUseCraftingSpot / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Crafting | DoBillsRefinery / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Crafting | DoBillsProduceDrugs / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Crafting | DoBillsStonecut / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Crafting | DoBillsSmelter / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | RearmTurrets / WorkGiver_Refuel_Turret | RearmTurret/RimWorld.JobDriver_Refuel; RearmTurretAtomic/RimWorld.JobDriver_RefuelAtomic | 同装填分支，JobStandard/Atomic明确替换为RearmTurret/Atomic | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | Refuel / WorkGiver_Refuel | Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic | Comp要求原子装填→RefuelAtomic；否则Refuel | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | UnloadCarriers / WorkGiver_UnloadCarriers | UnloadInventory/RimWorld.JobDriver_UnloadInventory | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | HelpGatheringItemsForCaravan / WorkGiver_HelpGatheringItemsForCaravan | PrepareCaravan_GatherItems/RimWorld.JobDriver_PrepareCaravan_GatherItems | 仅本地图集货过程；不覆盖离图世界旅行 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | LoadTransporters / WorkGiver_LoadTransporters | HaulToTransporter/RimWorld.JobDriver_HaulToTransporter | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | HaulToPortal / WorkGiver_HaulToPortal | HaulToPortal/RimWorld.JobDriver_HaulToPortal | Core声明/通用Comp目标过程；不外推DLC独有Giver/目标库存 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | Strip / WorkGiver_Strip | Strip/RimWorld.JobDriver_Strip | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | HaulCorpses / WorkGiver_HaulCorpses | HaulToCell/Verse.AI.JobDriver_HaulToCell; HaulToContainer/Verse.AI.JobDriver_HaulToContainer | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | DoBillsCremate / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | DoBillsHaulCampfire / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | TakeBeerOutOfFermentingBarrel / WorkGiver_TakeBeerOutOfFermentingBarrel | TakeBeerOutOfFermentingBarrel/RimWorld.JobDriver_TakeBeerOutOfFermentingBarrel | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | FillFermentingBarrel / WorkGiver_FillFermentingBarrel | FillFermentingBarrel/RimWorld.JobDriver_FillFermentingBarrel | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | HaulGeneral / WorkGiver_HaulGeneral | HaulToCell/Verse.AI.JobDriver_HaulToCell; HaulToContainer/Verse.AI.JobDriver_HaulToContainer | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | DeliverResourcesToFrames / WorkGiver_ConstructDeliverResourcesToFrames | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct清障四支；材料→HaulToContainer；附近需要者旧地板→RemoveFloor | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | DeliverResourcesToBlueprints / WorkGiver_ConstructDeliverResourcesToBlueprints | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; PlaceNoCostFrame/RimWorld.JobDriver_PlaceNoCostFrame; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | 清障四支；旧地板→RemoveFloor；材料/安装→HaulToContainer；零材料→PlaceNoCostFrame | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | HaulMerge / WorkGiver_Merge | HaulToCell/Verse.AI.JobDriver_HaulToCell | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Hauling | EmptyEggBox / WorkGiver_EmptyEggBox | EmptyThingContainer/RimWorld.JobDriver_EmptyThingContainer | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Cleaning | CleanClearSnow / WorkGiver_ClearSnowOrSand | ClearSnow/RimWorld.JobDriver_ClearSnowAndSand | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Cleaning | CleanFilth / WorkGiver_CleanFilth | Clean/RimWorld.JobDriver_CleanFilth | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Research | Research / WorkGiver_Researcher | Research/RimWorld.JobDriver_Research | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Research | LongRangeScan / WorkGiver_OperateScanner | OperateScanner/RimWorld.JobDriver_OperateScanner | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Research | GroundPenetratingScan / WorkGiver_OperateScanner | OperateScanner/RimWorld.JobDriver_OperateScanner | 全部列出返回分支含准备/清障；非完成/产出保证 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Growing | PlantSeed / WorkGiver_PlantSeed | PlantSeed/RimWorld.JobDriver_PlantSeed; CutPlant/RimWorld.JobDriver_PlantCut | ShouldSkip要求至少一个DLC；合法种子→PlantSeed，挡植物→CutPlant；未读DLC Def | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Research | StudyArchotechStructures / WorkGiver_StudyInteract | StudyInteract/RimWorld.JobDriver_StudyInteract | Core声明/通用Comp目标过程；不外推DLC独有Giver/目标库存 | 已支持；逐Job生产捕获/请求，旧七组复用 |
| Research | Hack / WorkGiver_Hack | Hack/RimWorld.JobDriver_Hack | CompHackable正常允许→Hack；forced+Ideology AncientEnemyTerminal另拒绝，保原限制 | 已支持；逐Job生产捕获/请求，旧七组复用 |

## 准确Job/driver与Core定义源

| JobDef | 完整driver | Core/Defs相对来源 | 额外准确来源 |
|---|---|---|---|
| BeatFire | RimWorld.JobDriver_BeatFire | JobDefs/Jobs_Misc.xml |  |
| BuildRoof | RimWorld.JobDriver_BuildRoof | JobDefs/Jobs_Work.xml |  |
| Clean | RimWorld.JobDriver_CleanFilth | JobDefs/Jobs_Work.xml |  |
| ClearSnow | RimWorld.JobDriver_ClearSnowAndSand | JobDefs/Jobs_Misc.xml |  |
| CutPlant | RimWorld.JobDriver_PlantCut | JobDefs/Jobs_Work.xml |  |
| CutPlantDesignated | RimWorld.JobDriver_PlantCut_Designated | JobDefs/Jobs_Work.xml |  |
| Deconstruct | RimWorld.JobDriver_Deconstruct | JobDefs/Jobs_Work.xml |  |
| DeconstructForBlueprint | RimWorld.JobDriver_DeconstructForBlueprint | JobDefs/Jobs_Work.xml |  |
| DeliverFood | RimWorld.JobDriver_FoodDeliver | JobDefs/Jobs_Work.xml |  |
| DoBill | Verse.AI.JobDriver_DoBill | JobDefs/Jobs_Work.xml |  |
| EjectFuel | RimWorld.JobDriver_EjectFuel | JobDefs/Jobs_Misc.xml |  |
| EmptyThingContainer | RimWorld.JobDriver_EmptyThingContainer | JobDefs/Jobs_Misc.xml |  |
| EscortPrisonerToBed | RimWorld.JobDriver_TakeToBed | JobDefs/Jobs_Work.xml |  |
| ExtractSkull | RimWorld.JobDriver_ExtractSkull | JobDefs/Jobs_Work.xml |  |
| ExtractTree | RimWorld.JobDriver_ExtractTree | JobDefs/Jobs_Work.xml |  |
| FeedPatient | RimWorld.JobDriver_FoodFeedPatient | JobDefs/Jobs_Work.xml |  |
| FillFermentingBarrel | RimWorld.JobDriver_FillFermentingBarrel | JobDefs/Jobs_Misc.xml |  |
| FillIn | RimWorld.JobDriver_FillIn | JobDefs/Jobs_Work.xml |  |
| FinishFrame | RimWorld.JobDriver_ConstructFinishFrame | JobDefs/Jobs_Work.xml |  |
| FixBrokenDownBuilding | RimWorld.JobDriver_FixBrokenDownBuilding | JobDefs/Jobs_Work.xml |  |
| Flick | RimWorld.JobDriver_Flick | JobDefs/Jobs_Misc.xml |  |
| GuiltyColonistExecution | RimWorld.JobDriver_ExecuteGuiltyColonist | JobDefs/Jobs_Work.xml |  |
| Hack | RimWorld.JobDriver_Hack | JobDefs/Jobs_Work.xml |  |
| Harvest | RimWorld.JobDriver_PlantHarvest | JobDefs/Jobs_Work.xml |  |
| HarvestDesignated | RimWorld.JobDriver_PlantHarvest_Designated | JobDefs/Jobs_Work.xml |  |
| HaulToCell | Verse.AI.JobDriver_HaulToCell | JobDefs/Jobs_Work.xml |  |
| HaulToContainer | Verse.AI.JobDriver_HaulToContainer | JobDefs/Jobs_Work.xml |  |
| HaulToPortal | RimWorld.JobDriver_HaulToPortal | JobDefs/Jobs_Misc.xml |  |
| HaulToTransporter | RimWorld.JobDriver_HaulToTransporter | JobDefs/Jobs_Misc.xml |  |
| Hunt | RimWorld.JobDriver_Hunt | JobDefs/Jobs_Work.xml |  |
| Milk | RimWorld.JobDriver_Milk | JobDefs/Jobs_Work.xml |  |
| Mine | RimWorld.JobDriver_Mine | JobDefs/Jobs_Work.xml |  |
| Open | RimWorld.JobDriver_Open | JobDefs/Jobs_Misc.xml |  |
| OperateDeepDrill | RimWorld.JobDriver_OperateDeepDrill | JobDefs/Jobs_Work.xml |  |
| OperateScanner | RimWorld.JobDriver_OperateScanner | JobDefs/Jobs_Work.xml |  |
| PaintBuilding | RimWorld.JobDriver_PaintBuilding | JobDefs/Jobs_Work.xml |  |
| PaintFloor | RimWorld.JobDriver_PaintFloor | JobDefs/Jobs_Work.xml |  |
| PlaceNoCostFrame | RimWorld.JobDriver_PlaceNoCostFrame | JobDefs/Jobs_Work.xml |  |
| PlantSeed | RimWorld.JobDriver_PlantSeed | JobDefs/Jobs_Work.xml |  |
| PrepareCaravan_GatherItems | RimWorld.JobDriver_PrepareCaravan_GatherItems | JobDefs/Jobs_Caravan.xml |  |
| PrisonerAttemptRecruit | RimWorld.JobDriver_ChatWithPrisoner | JobDefs/Jobs_Work.xml |  |
| PrisonerExecution | RimWorld.JobDriver_Execute | JobDefs/Jobs_Work.xml |  |
| RearmTurret | RimWorld.JobDriver_Refuel | JobDefs/Jobs_Misc.xml |  |
| RearmTurretAtomic | RimWorld.JobDriver_RefuelAtomic | JobDefs/Jobs_Misc.xml |  |
| Refuel | RimWorld.JobDriver_Refuel | JobDefs/Jobs_Misc.xml |  |
| RefuelAtomic | RimWorld.JobDriver_RefuelAtomic | JobDefs/Jobs_Misc.xml |  |
| ReleaseAnimalToWild | RimWorld.JobDriver_ReleaseAnimalToWild | JobDefs/Jobs_Work.xml |  |
| ReleasePrisoner | Verse.AI.JobDriver_ReleasePrisoner | JobDefs/Jobs_Work.xml |  |
| RemoveFloor | RimWorld.JobDriver_RemoveFloor | JobDefs/Jobs_Work.xml |  |
| RemoveFoundation | RimWorld.JobDriver_RemoveFoundation | JobDefs/Jobs_Work.xml |  |
| RemovePaintBuilding | RimWorld.JobDriver_RemovePaintBuilding | JobDefs/Jobs_Work.xml |  |
| RemovePaintFloor | RimWorld.JobDriver_RemovePaintFloor | JobDefs/Jobs_Work.xml |  |
| RemoveRoof | RimWorld.JobDriver_RemoveRoof | JobDefs/Jobs_Work.xml |  |
| Repair | RimWorld.JobDriver_Repair | JobDefs/Jobs_Work.xml |  |
| Replant | RimWorld.JobDriver_Replant | JobDefs/Jobs_Work.xml |  |
| Rescue | RimWorld.JobDriver_TakeToBed | JobDefs/Jobs_Work.xml |  |
| Research | RimWorld.JobDriver_Research | JobDefs/Jobs_Work.xml |  |
| RopeRoamerToHitchingPost | RimWorld.JobDriver_RopeRoamerToHitchingPost | JobDefs/Jobs_Work.xml |  |
| RopeRoamerToUnenclosedPen | RimWorld.JobDriver_RopeRoamerToUnenclosedPen | JobDefs/Jobs_Work.xml |  |
| RopeToPen | RimWorld.JobDriver_RopeToPen | JobDefs/Jobs_Work.xml |  |
| Shear | RimWorld.JobDriver_Shear | JobDefs/Jobs_Work.xml |  |
| Slaughter | RimWorld.JobDriver_Slaughter | JobDefs/Jobs_Work.xml |  |
| SmoothFloor | RimWorld.JobDriver_SmoothFloor | JobDefs/Jobs_Work.xml |  |
| SmoothWall | RimWorld.JobDriver_SmoothWall | JobDefs/Jobs_Work.xml |  |
| Sow | RimWorld.JobDriver_PlantSow | JobDefs/Jobs_Work.xml |  |
| Strip | RimWorld.JobDriver_Strip | JobDefs/Jobs_Misc.xml |  |
| StudyInteract | RimWorld.JobDriver_StudyInteract | JobDefs/Jobs_Work.xml |  |
| TakeBeerOutOfFermentingBarrel | RimWorld.JobDriver_TakeBeerOutOfFermentingBarrel | JobDefs/Jobs_Misc.xml |  |
| TakeInventory | Verse.AI.JobDriver_TakeInventory | JobDefs/Jobs_Misc.xml | Train |
| TakeToBedToOperate | RimWorld.JobDriver_TakeToBed | JobDefs/Jobs_Work.xml |  |
| TakeWoundedPrisonerToBed | RimWorld.JobDriver_TakeToBed | JobDefs/Jobs_Work.xml |  |
| Tame | RimWorld.JobDriver_Tame | JobDefs/Jobs_Work.xml |  |
| TendPatient | RimWorld.JobDriver_TendPatient | JobDefs/Jobs_Work.xml |  |
| Train | RimWorld.JobDriver_Train | JobDefs/Jobs_Work.xml |  |
| Uninstall | RimWorld.JobDriver_Uninstall | JobDefs/Jobs_Work.xml |  |
| UnloadInventory | RimWorld.JobDriver_UnloadInventory | JobDefs/Jobs_Misc.xml |  |
| Unrope | RimWorld.JobDriver_Unrope | JobDefs/Jobs_Work.xml |  |
| VisitSickPawn | RimWorld.JobDriver_VisitSickPawn | JobDefs/Jobs_Joy.xml | VisitSickPawn |

## 竞争、退出与扩展边界

- Move与准备阶段Work可同时True，当前Dev仍Move先于Work；最小Work-only包遇Move中选但无Move音频时不补Work。实际VisitSickPawn的joyKind=Social，可能Social先中选。范围补齐没有提高Work优先或改事实回避竞争。
- 全部Core文件中定义的上述过程都按准确机制观察。PlantSeed有DLC启用条件；Core中通用CompStudy/Portal/Hack等观察不等于列齐或支持DLC/第三方新增WorkGiver/目标。没有读取未授权DLC文件，也不把未查内容说成不是工作。
- 地图内PrepareCaravan_GatherItems/装运过程按表纳入；离图世界旅行/世界任务不是当前Pawn地图Work宿主。真正BeatFire来自Core消防工作；逃跑、倒地、被救援/患者休息等不由其类别名字推断。
- 继承子保固定Work条件和自己的完整键、scope/参数/条目/硬保护。新工作不共用物品/患者的域；准确供音域仍来自Worker。
- OneShot发出后正常结束或中断不精准裁尾；Sustained只在当前依据失效/Unknown或宿主退出时结束自有槽，不停止非US或其他对象的声音。没有新增Save字段或永久工作状态。

## 证据与尚待观察

源代码/Def定点审计与生产捕获不同：新增71组逐一经过实际UsWorkObservation/UsPawnFacts、CompTick、有效配置、同session及准确条目请求；覆盖无当前Toil、同Job/主体/driver错误、正常结束保旧Toil等反例。真缺Sow类型独立编译证明仅局部Unknown，CutPlant仍工作；另有真实摄入观察的喂食角色分离、OneShot自然尾与Sustained正常/中断释放。

可维护目标位于[WorkCoreTests](../../../tools/UniversalSqueakerAudioBindingTests/WorkCoreTests.cs)及[独立期待表](../../../tools/UniversalSqueakerAudioBindingTests/WorkCoreExpected.tsv)，运行--work-core，真缺实现用WorkFixtureTarget=MissingSow/--work-missing；--work-care-lifetime仅执行新增照护/尾音判别。Job/driver/Toil/调度/音频是明确平台替身，WorkGiver原体未在游戏内执行，原生加载/调度/声音与逐包合同/成果人审仍pending。
