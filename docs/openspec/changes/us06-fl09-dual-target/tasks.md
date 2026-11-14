# 实施状态

- [x] 独立集成树从已提交0.6.x父线建立；规格与开发CI差量先于对应源码独立采用。
- [x] 真正merge r5全部历史，保留97aafd3和占用树／旧载体。
- [x] 共享生产源，原net472入口与显式native1.7/net10目标、隔离restore/output、正确About／LoadFolders。
- [x] 既有stager可组合真实双DLL；原单载体调用保诚实1.6-only包，旧参数位置不变。原Dev caller及精确载体拒绝／保留检查通过。
- [x] 普通Release双版、实际compiler ref／双身份、原consumer32／479／55／shell13／old-v4十三及UI-logic通过；记录见现行build guide。
- [x] 开发CI固定FL公开完整commit、普通Release来源、原完整US入口／载体收据；正式Release门槛不改。不外发专有1.7依赖。

完整Host仍在已登记的旧PackCards preservation场景失败；本地完整CI rehearsal另停在gate2的临时目录创建权限拒绝（gate1通过，后续未到达）。两项分别记录，保留原完整CI入口，不删lane或弱化语义断言。不能把本次双目标工程通过称为整套CI绿色或1.7原生验收。

推送门：公开FL pin实际可达、实际index及完整可达历史隐私门禁通过后，以正常fast-forward推进0.6.x；实际远端tip由Git ref唯一记录，私有执行履历交原scratch。无tag／Release／部署。