# Core工作原生范围
状态：现行，US0.6.x／目标1.6.4871 rev591。责任：US Work适配维护者。以下附录身份与分支是精确覆盖集，不代表游戏已逐项运行。

## Purpose
完整覆盖已批准的Core常用工作，不将种植、医疗、驯兽等缩成七例，也不以WorkGiver标签、Job名称或患者身份推断工作。事实支持与回应scope分开。

## Requirements
### Requirement: GAMEWORK-01 完整准确支持集
Work SHALL识别附录A的20 WorkType／105具体Giver、其中102做工入口及帮助分支对应的78准确Job／driver（73driver类型）；三个Patient休息入口排除。必须同Pawn／currentJob／driver、未ended、当前Toil有效；含准备，不保证完成。
#### Scenario: 结束时保留旧Toil
- **WHEN** Pawn_JobTracker已置ended而尚未Cleanup
- **THEN** Work为False；无Job／已核无当前Toil亦False，未知Job／缺实现／主体错配具名Unknown

### Requirement: GAMEWORK-02 共享过程的来源与真实执行者
TakeInventory SHALL要求准确Train来源，VisitSickPawn要求同名准确WorkGiver；Goto／Wait、workGiver非空或反射存在不足。Worker是执行者；患者、动物、被搬运者不取代，自我照护者可同时是Worker与患者。
#### Scenario: 自动工作与强制命令
- **WHEN** 同一准确工作过程分别来自自动排程和玩家命令
- **THEN** 事实都可成立；声明／Dev Work缺省ActiveCommand，普通输入AnyOccurrence另由POL拥有，不能把scope改事实

### Requirement: GAMEWORK-03 范围外与退出
本集 SHALL不外推未核DLC／第三方新增工作或离图世界旅行。Core中的PlantSeed DLC条件、通用Comp／装运准备仍按附录分支，不重复giver原资格。Move／Social重叠仍原仲裁；输出按OUT，不增加Save字段或第二引擎。
#### Scenario: 准备工作同时移动
- **WHEN** Move更高优先中选却无音
- **THEN** 不补Work，不为掩盖此结果改变事实或总序；OneShot退出自然尾、Sustained失效结束

## 附录A：具体WorkGiver与分支
来源：Core/Defs/WorkGiverDefs/WorkGivers.xml及准确原版giver返回／帮助分支。只列身份和结论，不分发反编译正文。
| WorkType | 具体WorkGiver / 类 | 实际返回Job / driver | 条件分支、主体或边界 |
|---|---|---|---|
| Firefighter | FightFires / WorkGiver_FightFires | BeatFire/RimWorld.JobDriver_BeatFire | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Patient | PatientGoToBedEmergencyTreatment / WorkGiver_PatientGoToBedEmergencyTreatment | LayDown/RimWorld.JobDriver_LayDown | 自身病人休息/接受治疗，不是做工主体 |
| Patient | PatientGoToBedTreatment / WorkGiver_PatientGoToBedTreatment | LayDown/RimWorld.JobDriver_LayDown | 自身病人休息/接受治疗，不是做工主体 |
| Doctor | DoctorTendEmergency / WorkGiver_TendOtherUrgent | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoctorTendToHumanlikes / WorkGiver_TendOther_Humanlike | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoctorTendToSelf / WorkGiver_TendSelf | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoctorTendToSelfEmergency / WorkGiver_TendSelfEmergency | TendPatient/RimWorld.JobDriver_TendPatient | JobGiver_SelfTend实际TendPatient；自我照护者仍是执行者，不是被动休息 |
| Doctor | DoctorFeedHumanlikes / WorkGiver_FeedPatient | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoBillsMedicalHumanOperation / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Doctor | DoctorRescue / WorkGiver_RescueDowned | Rescue/RimWorld.JobDriver_TakeToBed | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoctorTendToAnimals / WorkGiver_TendOther_Animal | TendPatient/RimWorld.JobDriver_TendPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoctorFeedAnimals / WorkGiver_FeedPatient | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | DoBillsMedicalAnimalOperation / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Doctor | TakeToBedToOperate / WorkGiver_TakeToBedToOperate | TakeToBedToOperate/RimWorld.JobDriver_TakeToBed | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Doctor | VisitSickPawn / WorkGiver_VisitSickPawn | VisitSickPawn/RimWorld.JobDriver_VisitSickPawn | 与Joy/Social重叠；仅准确VisitSickPawn WorkGiver来源 |
| PatientBedRest | PatientGoToBedRecuperate / WorkGiver_PatientGoToBedRecuperate | LayDown/RimWorld.JobDriver_LayDown | 自身病人休息/接受治疗，不是做工主体 |
| BasicWorker | Flick / WorkGiver_Flick | Flick/RimWorld.JobDriver_Flick | 全部列出返回分支含准备/清障；非完成/产出保证 |
| BasicWorker | BasicReleasePrisoner / WorkGiver_Warden_ReleasePrisoner | ReleasePrisoner/Verse.AI.JobDriver_ReleasePrisoner | 全部列出返回分支含准备/清障；非完成/产出保证 |
| BasicWorker | Open / WorkGiver_Open | Open/RimWorld.JobDriver_Open | 全部列出返回分支含准备/清障；非完成/产出保证 |
| BasicWorker | EjectFuel / WorkGiver_EjectFuel | EjectFuel/RimWorld.JobDriver_EjectFuel | 全部列出返回分支含准备/清障；非完成/产出保证 |
| BasicWorker | ExtractSkull / WorkGiver_ExtractSkull | ExtractSkull/RimWorld.JobDriver_ExtractSkull | Core明确提取过程；Ideology资格/Anomaly尸体检查仍属原giver，不据此承诺DLC独有库存 |
| Warden | DoExecution / WorkGiver_Warden_DoExecution | PrisonerExecution/RimWorld.JobDriver_Execute | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Warden | ExecuteGuiltyColonist / WorkGiver_ExecuteGuiltyColonist | GuiltyColonistExecution/RimWorld.JobDriver_ExecuteGuiltyColonist | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Warden | ReleasePrisoner / WorkGiver_Warden_ReleasePrisoner | ReleasePrisoner/Verse.AI.JobDriver_ReleasePrisoner | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Warden | TakePrisonerToBed / WorkGiver_Warden_TakeToBed | EscortPrisonerToBed/RimWorld.JobDriver_TakeToBed; TakeWoundedPrisonerToBed/RimWorld.JobDriver_TakeToBed | 可行走囚犯→EscortPrisonerToBed；倒地囚犯→TakeWoundedPrisonerToBed，Worker是押送者 |
| Warden | FeedPrisoner / WorkGiver_Warden_Feed | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Warden | DeliverFoodToPrisoner / WorkGiver_Warden_DeliverFood | DeliverFood/RimWorld.JobDriver_FoodDeliver | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Warden | ChatWithPrisoner / WorkGiver_Warden_Chat | PrisonerAttemptRecruit/RimWorld.JobDriver_ChatWithPrisoner | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Handling | TakeRoamingAnimalsToPen / WorkGiver_TakeRoamingAnimalsToPen | RopeToPen/RimWorld.JobDriver_RopeToPen; RopeRoamerToUnenclosedPen/RimWorld.JobDriver_RopeRoamerToUnenclosedPen; RopeRoamerToHitchingPost/RimWorld.JobDriver_RopeRoamerToHitchingPost; Unrope/RimWorld.JobDriver_Unrope | 基类四支；targetRoamingAnimals=true、allowUnenclosedPens=true，含未封闭围栏分支 |
| Handling | HandlingFeedPatientAnimals / WorkGiver_FeedPatient | FeedPatient/RimWorld.JobDriver_FoodFeedPatient | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Handling | TakeToPen / WorkGiver_TakeToPen | RopeToPen/RimWorld.JobDriver_RopeToPen; RopeRoamerToUnenclosedPen/RimWorld.JobDriver_RopeRoamerToUnenclosedPen; RopeRoamerToHitchingPost/RimWorld.JobDriver_RopeRoamerToHitchingPost; Unrope/RimWorld.JobDriver_Unrope | 基类按围栏状态/拴柱/无需拴绳分支；默认allowUnenclosedPens=false，具体条件未成立不发Job |
| Handling | Slaughter / WorkGiver_Slaughter | Slaughter/RimWorld.JobDriver_Slaughter | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Handling | ReleaseToWild / WorkGiver_ReleaseAnimalsToWild | ReleaseAnimalToWild/RimWorld.JobDriver_ReleaseAnimalToWild | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Handling | Milk / WorkGiver_Milk | Milk/RimWorld.JobDriver_Milk | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Handling | Shear / WorkGiver_Shear | Shear/RimWorld.JobDriver_Shear | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Handling | Tame / WorkGiver_Tame | Tame/RimWorld.JobDriver_Tame | Tame自身driver含先取饲料C再驯服；不是额外通用TakeInventory分支 |
| Handling | Train / WorkGiver_Train | Train/RimWorld.JobDriver_Train; TakeInventory/Verse.AI.JobDriver_TakeInventory | 缺互动饲料→TakeInventory且需准确Train来源；否则Train |
| Handling | RebalanceAnimalsInPens / WorkGiver_RebalanceAnimalsInPens | RopeToPen/RimWorld.JobDriver_RopeToPen; RopeRoamerToUnenclosedPen/RimWorld.JobDriver_RopeRoamerToUnenclosedPen; RopeRoamerToHitchingPost/RimWorld.JobDriver_RopeRoamerToHitchingPost; Unrope/RimWorld.JobDriver_Unrope | 同基类分支；ropingPriority=Balanced，未封闭旗标保持默认false |
| Cooking | DoBillsCook / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Cooking | DoBillsCookCampfire / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Cooking | DoBillsButcherFlesh / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Hauling | CookFillHopper / WorkGiver_CookFillHopper | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; HaulToCell/Verse.AI.JobDriver_HaulToCell | 储料→HaulToContainer；先清挡物→HaulToCell |
| Cooking | DoBillsBrew / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Hunting | HunterHunt / WorkGiver_HunterHunt | Hunt/RimWorld.JobDriver_Hunt | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | FixBrokenDownBuilding / WorkGiver_FixBrokenDownBuilding | FixBrokenDownBuilding/RimWorld.JobDriver_FixBrokenDownBuilding | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | Uninstall / WorkGiver_Uninstall | Uninstall/RimWorld.JobDriver_Uninstall | 全部列出返回分支含准备/清障；非完成/产出保证 |
| PlantCutting | ExtractTree / WorkGiver_ExtractTree | ExtractTree/RimWorld.JobDriver_ExtractTree | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | BuildRoofs / WorkGiver_BuildRoof | BuildRoof/RimWorld.JobDriver_BuildRoof; CutPlant/RimWorld.JobDriver_PlantCut | RoofUtility挡植物→CutPlant；否则BuildRoof |
| Construction | RemoveRoofs / WorkGiver_RemoveRoof | RemoveRoof/RimWorld.JobDriver_RemoveRoof | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | DeconstructForBlueprint / WorkGiver_DeconstructForBlueprint | DeconstructForBlueprint/RimWorld.JobDriver_DeconstructForBlueprint | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | ConstructFinishFrames / WorkGiver_ConstructFinishFrames | FinishFrame/RimWorld.JobDriver_ConstructFinishFrame; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct挡植物/散物/可拆建筑/矿物→CutPlant/HaulToCell/Deconstruct/Mine；否则FinishFrame |
| Construction | ConstructDeliverResourcesToFrames / WorkGiver_ConstructDeliverResourcesToFrames | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct清障四支；材料→HaulToContainer；附近需要者旧地板→RemoveFloor |
| Construction | ConstructDeliverResourcesToBlueprints / WorkGiver_ConstructDeliverResourcesToBlueprints | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; PlaceNoCostFrame/RimWorld.JobDriver_PlaceNoCostFrame; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | 清障四支；旧地板→RemoveFloor；材料/安装→HaulToContainer；零材料→PlaceNoCostFrame |
| Growing | Replant / WorkGiver_Replant | Replant/RimWorld.JobDriver_Replant; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct清障四支；否则Replant安装/搬运植物 |
| Construction | FillIn / WorkGiver_FillIn | FillIn/RimWorld.JobDriver_FillIn | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | Deconstruct / WorkGiver_Deconstruct | Deconstruct/RimWorld.JobDriver_Deconstruct | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | Repair / WorkGiver_Repair | Repair/RimWorld.JobDriver_Repair | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Art | PaintBuilding / WorkGiver_PaintBuilding | PaintBuilding/RimWorld.JobDriver_PaintBuilding | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Art | PaintFloor / WorkGiver_PaintFloor | PaintFloor/RimWorld.JobDriver_PaintFloor | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Art | RemovePaintBuilding / WorkGiver_RemovePaintBuilding | RemovePaintBuilding/RimWorld.JobDriver_RemovePaintBuilding | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Art | RemovePaintFloor / WorkGiver_RemovePaintFloor | RemovePaintFloor/RimWorld.JobDriver_RemovePaintFloor | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | ConstructRemoveFloors / WorkGiver_ConstructRemoveFloor | RemoveFloor/RimWorld.JobDriver_RemoveFloor | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | ConstructRemoveFoundations / WorkGiver_ConstructRemoveFoundation | RemoveFoundation/RimWorld.JobDriver_RemoveFoundation; RemoveFloor/RimWorld.JobDriver_RemoveFloor | 覆盖地板先RemoveFloor；基础层RemoveFoundation |
| Construction | ConstructSmoothFloors / WorkGiver_ConstructSmoothFloor | SmoothFloor/RimWorld.JobDriver_SmoothFloor | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Construction | ConstructSmoothWalls / WorkGiver_ConstructSmoothWall | SmoothWall/RimWorld.JobDriver_SmoothWall | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Growing | GrowerHarvest / WorkGiver_GrowerHarvest | Harvest/RimWorld.JobDriver_PlantHarvest | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Growing | GrowerSow / WorkGiver_GrowerSow | Sow/RimWorld.JobDriver_PlantSow; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell | 可种空地→Sow；挡植物→CutPlant；挡物搬开→HaulToCell |
| Mining | Mine / WorkGiver_Miner | Mine/RimWorld.JobDriver_Mine; HaulToCell/Verse.AI.JobDriver_HaulToCell | MineAIUtility先清挡物→HaulToCell；否则Mine |
| Mining | Drill / WorkGiver_DeepDrill | OperateDeepDrill/RimWorld.JobDriver_OperateDeepDrill | 全部列出返回分支含准备/清障；非完成/产出保证 |
| PlantCutting | PlantsCut / WorkGiver_PlantsCut | HarvestDesignated/RimWorld.JobDriver_PlantHarvest_Designated; CutPlantDesignated/RimWorld.JobDriver_PlantCut_Designated | 收获designation→HarvestDesignated；砍伐designation→CutPlantDesignated |
| Smithing | DoBillsMakeWeapons / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Smithing | DoBillsMachiningTable / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Smithing | DoBillsFabricationBench / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Tailoring | DoBillsMakeApparel / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Art | DoBillsSculpt / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Crafting | DoBillsUseCraftingSpot / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Crafting | DoBillsRefinery / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Crafting | DoBillsProduceDrugs / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Crafting | DoBillsStonecut / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Crafting | DoBillsSmelter / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Hauling | RearmTurrets / WorkGiver_Refuel_Turret | RearmTurret/RimWorld.JobDriver_Refuel; RearmTurretAtomic/RimWorld.JobDriver_RefuelAtomic | 同装填分支，JobStandard/Atomic明确替换为RearmTurret/Atomic |
| Hauling | Refuel / WorkGiver_Refuel | Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic | Comp要求原子装填→RefuelAtomic；否则Refuel |
| Hauling | UnloadCarriers / WorkGiver_UnloadCarriers | UnloadInventory/RimWorld.JobDriver_UnloadInventory | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | HelpGatheringItemsForCaravan / WorkGiver_HelpGatheringItemsForCaravan | PrepareCaravan_GatherItems/RimWorld.JobDriver_PrepareCaravan_GatherItems | 仅本地图集货过程；不覆盖离图世界旅行 |
| Hauling | LoadTransporters / WorkGiver_LoadTransporters | HaulToTransporter/RimWorld.JobDriver_HaulToTransporter | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | HaulToPortal / WorkGiver_HaulToPortal | HaulToPortal/RimWorld.JobDriver_HaulToPortal | Core声明/通用Comp目标过程；不外推DLC独有Giver/目标库存 |
| Hauling | Strip / WorkGiver_Strip | Strip/RimWorld.JobDriver_Strip | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | HaulCorpses / WorkGiver_HaulCorpses | HaulToCell/Verse.AI.JobDriver_HaulToCell; HaulToContainer/Verse.AI.JobDriver_HaulToContainer | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | DoBillsCremate / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Hauling | DoBillsHaulCampfire / WorkGiver_DoBill | DoBill/Verse.AI.JobDriver_DoBill; Refuel/RimWorld.JobDriver_Refuel; RefuelAtomic/RimWorld.JobDriver_RefuelAtomic; HaulToCell/Verse.AI.JobDriver_HaulToCell | 有效bill含制作/烹饪/手术→DoBill；桌缺燃料→Refuel/Atomic；WorkGiverUtility清挡物→HaulToCell |
| Hauling | TakeBeerOutOfFermentingBarrel / WorkGiver_TakeBeerOutOfFermentingBarrel | TakeBeerOutOfFermentingBarrel/RimWorld.JobDriver_TakeBeerOutOfFermentingBarrel | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | FillFermentingBarrel / WorkGiver_FillFermentingBarrel | FillFermentingBarrel/RimWorld.JobDriver_FillFermentingBarrel | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | HaulGeneral / WorkGiver_HaulGeneral | HaulToCell/Verse.AI.JobDriver_HaulToCell; HaulToContainer/Verse.AI.JobDriver_HaulToContainer | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | DeliverResourcesToFrames / WorkGiver_ConstructDeliverResourcesToFrames | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | GenConstruct清障四支；材料→HaulToContainer；附近需要者旧地板→RemoveFloor |
| Hauling | DeliverResourcesToBlueprints / WorkGiver_ConstructDeliverResourcesToBlueprints | HaulToContainer/Verse.AI.JobDriver_HaulToContainer; RemoveFloor/RimWorld.JobDriver_RemoveFloor; PlaceNoCostFrame/RimWorld.JobDriver_PlaceNoCostFrame; CutPlant/RimWorld.JobDriver_PlantCut; HaulToCell/Verse.AI.JobDriver_HaulToCell; Deconstruct/RimWorld.JobDriver_Deconstruct; Mine/RimWorld.JobDriver_Mine | 清障四支；旧地板→RemoveFloor；材料/安装→HaulToContainer；零材料→PlaceNoCostFrame |
| Hauling | HaulMerge / WorkGiver_Merge | HaulToCell/Verse.AI.JobDriver_HaulToCell | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Hauling | EmptyEggBox / WorkGiver_EmptyEggBox | EmptyThingContainer/RimWorld.JobDriver_EmptyThingContainer | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Cleaning | CleanClearSnow / WorkGiver_ClearSnowOrSand | ClearSnow/RimWorld.JobDriver_ClearSnowAndSand | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Cleaning | CleanFilth / WorkGiver_CleanFilth | Clean/RimWorld.JobDriver_CleanFilth | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Research | Research / WorkGiver_Researcher | Research/RimWorld.JobDriver_Research | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Research | LongRangeScan / WorkGiver_OperateScanner | OperateScanner/RimWorld.JobDriver_OperateScanner | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Research | GroundPenetratingScan / WorkGiver_OperateScanner | OperateScanner/RimWorld.JobDriver_OperateScanner | 全部列出返回分支含准备/清障；非完成/产出保证 |
| Growing | PlantSeed / WorkGiver_PlantSeed | PlantSeed/RimWorld.JobDriver_PlantSeed; CutPlant/RimWorld.JobDriver_PlantCut | ShouldSkip要求至少一个DLC；合法种子→PlantSeed，挡植物→CutPlant；未读DLC Def |
| Research | StudyArchotechStructures / WorkGiver_StudyInteract | StudyInteract/RimWorld.JobDriver_StudyInteract | Core声明/通用Comp目标过程；不外推DLC独有Giver/目标库存 |
| Research | Hack / WorkGiver_Hack | Hack/RimWorld.JobDriver_Hack | CompHackable正常允许→Hack；forced+Ideology AncientEnemyTerminal另拒绝，保原限制 |


## 附录B：准确Job、driver与Core来源

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