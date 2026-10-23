using System;
using System.Collections.Generic;
using UniversalSqueaker;
using UniversalSqueaker.Kernel;

namespace UniversalSqueaker.KernelTests;

/// <summary>
/// RMX-002 回归：三层 Remix（无 PackFallback 声明路径）空层索引。
/// 全部经公开 <see cref="SqueakPoolRegistry.Select"/>，不用反射触碰私有 SelectRemixThree，
/// 也不做抽样比例断言——每个用例钉死 tier/soundKey/poolStableKey 与 Next01 消耗次数。
/// 夹具默认单条目/单变体/单声音，使消耗可精确计数：可播层数 &lt;=1 → 0 次；&gt;=2 → 恰 1 次；
/// 层内多声音/多条目另设对照，验证“层内选择按既有消耗”未被改动。
/// 期望值按语义规范手算（非由实现回推）：等权 = floor(roll * count)，索引按 [xeno, race, (packFallback), builtin]
/// 中“非空层”依次递减跳过空层。修复前本文件必须出现 FAIL。
/// </summary>
public static class RemixEmptyTierRegression
{
    private const string Act = CoreActions.Call;

    private const string XenoPackKey = "us.regression:XenoPack";
    private const string RacePackKey = "us.regression:RacePack";
    private const string RacePack2Key = "us.regression:RacePack2";
    private const string XenoSound = "US_REG_Xeno_Call";
    private const string XenoSound0 = "US_REG_Xeno_Call_0";
    private const string XenoSound1 = "US_REG_Xeno_Call_1";
    private const string RaceSound = "US_REG_Race_Call";
    private const string Race2Sound = "US_REG_Race2_Call";
    private const string BuiltInSound = "US_REG_Builtin_Call";
    private const string FallbackSound = "US_REG_PackFallback_Call";

    public static void RunAll(ref int failures)
    {
        EightCombos(ref failures);
        TierIntervalBounds(ref failures);
        GateRejectsLayer(ref failures);
        PackFallbackPaths(ref failures);
        OffAndFallbackFidelity(ref failures);
        InLayerConsumptionUnchanged(ref failures);
    }

    // ---- 1. 八种空层组合 ----------------------------------------------------

    private static readonly (bool X, bool R, bool B, double Roll, int Draws, ChainTier? Tier, string? Sound, string? Pool)[] EightComboTable =
    {
        // 000 无层：None，且不抽签（脚本给了值也不得被消费）。
        (false, false, false, 0.0, 0, null, null, null),
        (false, false, false, 0.9999, 0, null, null, null),
        // 100 仅 xeno：唯一非空层直接返回，不抽签。
        (true, false, false, 0.0, 0, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, false, false, 0.9999, 0, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        // 010 仅 race。
        (false, true, false, 0.0, 0, ChainTier.RacePack, RaceSound, RacePackKey),
        (false, true, false, 0.9999, 0, ChainTier.RacePack, RaceSound, RacePackKey),
        // 001 仅 builtin（PoolStableKey 恒 null）。
        (false, false, true, 0.0, 0, ChainTier.BuiltInFallback, BuiltInSound, null),
        (false, false, true, 0.9999, 0, ChainTier.BuiltInFallback, BuiltInSound, null),
        // 110 xeno/race：压缩索引与固定位置恰好对齐 → 旧实现也正确（对照组）。
        (true, true, false, 0.0, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, true, false, 0.4999, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, true, false, 0.5, 1, ChainTier.RacePack, RaceSound, RacePackKey),
        (true, true, false, 0.9999, 1, ChainTier.RacePack, RaceSound, RacePackKey),
        // 101 xeno/builtin：中间层空 → 索引 1 必须落到 builtin（旧实现返回 race=None）。
        (true, false, true, 0.0, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, false, true, 0.4999, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, false, true, 0.5, 1, ChainTier.BuiltInFallback, BuiltInSound, null),
        (true, false, true, 0.9999, 1, ChainTier.BuiltInFallback, BuiltInSound, null),
        // 011 race/builtin：前置层空 → 索引 0 必须落到 race，索引 1 落到 builtin
        //（旧实现：0 → None，1 → race，builtin 永不可达）。
        (false, true, true, 0.0, 1, ChainTier.RacePack, RaceSound, RacePackKey),
        (false, true, true, 0.4999, 1, ChainTier.RacePack, RaceSound, RacePackKey),
        (false, true, true, 0.5, 1, ChainTier.BuiltInFallback, BuiltInSound, null),
        (false, true, true, 0.9999, 1, ChainTier.BuiltInFallback, BuiltInSound, null),
        // 111 三层齐全：等权（压缩索引 = 固定位置，对照组）。
        (true, true, true, 0.0, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, true, true, 0.33, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey),
        (true, true, true, 0.34, 1, ChainTier.RacePack, RaceSound, RacePackKey),
        (true, true, true, 0.66, 1, ChainTier.RacePack, RaceSound, RacePackKey),
        (true, true, true, 0.67, 1, ChainTier.BuiltInFallback, BuiltInSound, null),
        (true, true, true, 0.9999, 1, ChainTier.BuiltInFallback, BuiltInSound, null),
    };

    private static void EightCombos(ref int failures)
    {
        foreach ((bool x, bool r, bool b, double roll, int draws, ChainTier? tier, string? sound, string? pool) in EightComboTable)
        {
            SqueakPoolRegistry registry = Layers(x, r, b);
            string label = "combo " + Bit(x) + Bit(r) + Bit(b) + " remix roll=" + roll.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
            Expect(label, registry, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { roll }, draws, tier, sound, pool, ref failures);
        }
    }

    // ---- 2. tier 区间 0 / 边界两侧 / 近 1 ----------------------------------

    private static void TierIntervalBounds(ref int failures)
    {
        // 恰两层可播：区间 [0,0.5) → 首个非空层，[0.5,1) → 次个非空层；1.0 被 RollIndex clamp 到末层。
        SqueakPoolRegistry raceBuiltin = Layers(false, true, true);
        SqueakPoolRegistry xenoBuiltin = Layers(true, false, true);
        Expect("bounds 011 roll=0.0 (lower edge)", raceBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("bounds 011 roll=0.4999999 (below half)", raceBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.4999999 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("bounds 011 roll=0.5 (at half)", raceBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("bounds 011 roll=0.9999999 (near 1)", raceBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.9999999 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("bounds 011 roll=1.0 (clamped)", raceBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 1.0 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("bounds 101 roll=0.0 (lower edge)", xenoBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("bounds 101 roll=0.5 (at half)", xenoBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("bounds 101 roll=1.0 (clamped)", xenoBuiltin, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 1.0 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 三层齐全：三等分边界两侧。
        SqueakPoolRegistry all = Layers(true, true, true);
        Expect("bounds 111 roll=0.3333333 (below third)", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.3333333 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("bounds 111 roll=0.3334 (above third)", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.3334 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("bounds 111 roll=0.6666 (below two thirds)", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.6666 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("bounds 111 roll=0.6667 (above two thirds)", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.6667 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("bounds 111 roll=1.0 (clamped)", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 1.0 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
    }

    // ---- 3. gate 拒绝某层 → 剩余两层可播（对照） ----------------------------

    private static void GateRejectsLayer(ref int failures)
    {
        SqueakPoolRegistry all = Layers(true, true, true);

        // 对照基线：gate 全开时索引 0 = xeno、索引 1 = race。
        Expect("gate-all roll=0.0 -> xeno", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("gate-all roll=0.5 -> race", all, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);

        // gate 关掉 xeno 层 → 剩余 race+builtin 可播（等价 011）。
        ISoundGate denyXeno = new DenyGate(XenoSound);
        Expect("gate-deny-xeno roll=0.0 -> race", all, RaceOrXeno(true), SelectionMode.Remix, denyXeno, new[] { 0.0 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("gate-deny-xeno roll=0.5 -> builtin", all, RaceOrXeno(true), SelectionMode.Remix, denyXeno, new[] { 0.5 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // gate 关掉中间的 race 层 → 剩余 xeno+builtin 可播（等价 101）。
        ISoundGate denyRace = new DenyGate(RaceSound);
        Expect("gate-deny-race roll=0.0 -> xeno", all, RaceOrXeno(true), SelectionMode.Remix, denyRace, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("gate-deny-race roll=0.5 -> builtin", all, RaceOrXeno(true), SelectionMode.Remix, denyRace, new[] { 0.5 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // gate 关掉 builtin 层 → 剩余 xeno+race 可播（压缩索引与固定位置对齐，对照组）。
        ISoundGate denyBuiltIn = new DenyGate(BuiltInSound);
        Expect("gate-deny-builtin roll=0.0 -> xeno", all, RaceOrXeno(true), SelectionMode.Remix, denyBuiltIn, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("gate-deny-builtin roll=0.5 -> race", all, RaceOrXeno(true), SelectionMode.Remix, denyBuiltIn, new[] { 0.5 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);

        // gate 关掉两层 → 唯一非空层直接返回，不抽签。
        ISoundGate denyTwo = new DenyGate(XenoSound, BuiltInSound);
        Expect("gate-deny-xeno+builtin -> race only, no draw", all, RaceOrXeno(true), SelectionMode.Remix, denyTwo, new[] { 0.0 }, 0, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);

        // gate 关掉全部三层 → None，不抽签。
        ISoundGate denyAll = new DenyGate(XenoSound, RaceSound, BuiltInSound);
        Expect("gate-deny-all -> None, no draw", all, RaceOrXeno(true), SelectionMode.Remix, denyAll, new[] { 0.0 }, 0, null, null, null, ref failures);
    }

    // ---- 4. PackFallback 声明可播 / 声明但被 gate 拒绝（四层）对三层 --------

    private static void PackFallbackPaths(ref int failures)
    {
        // 四层：xeno 条目在精确域声明 Call 的 PackFallback，四层全可播 → 四等权。
        SqueakPoolRegistry fourPlayable = Layers(true, true, true, xenoFallback: FallbackSound);
        Expect("4tier playable roll=0.0 -> xeno", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("4tier playable roll=0.25 -> race", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.25 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("4tier playable roll=0.5 -> packFallback", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5 }, 1, ChainTier.PackFallback, FallbackSound, XenoPackKey, ref failures);
        Expect("4tier playable roll=0.75 -> builtin", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.75 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("4tier playable roll=0.9999 -> builtin", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.9999 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 声明但被 gate 拒绝：仍走四层入口（三/四层不合并），p=None → count=3 递减跳过。
        ISoundGate denyFallback = new DenyGate(FallbackSound);
        Expect("4tier declared-but-denied roll=0.0 -> xeno", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, denyFallback, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("4tier declared-but-denied roll=0.34 -> race", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, denyFallback, new[] { 0.34 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("4tier declared-but-denied roll=0.67 -> builtin", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, denyFallback, new[] { 0.67 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 四层 + 前置 xeno 层被 gate 拒：count=3 → [race, packFallback, builtin]。
        Expect("4tier deny-xeno roll=0.0 -> race", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, new DenyGate(XenoSound), new[] { 0.0 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("4tier deny-xeno roll=0.34 -> packFallback", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, new DenyGate(XenoSound), new[] { 0.34 }, 1, ChainTier.PackFallback, FallbackSound, XenoPackKey, ref failures);
        Expect("4tier deny-xeno roll=0.67 -> builtin", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, new DenyGate(XenoSound), new[] { 0.67 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 四层 + 前两层被拒：只剩 [packFallback, builtin] → 0.0→p，0.5→builtin。
        ISoundGate denyXR = new DenyGate(XenoSound, RaceSound);
        Expect("4tier deny-xeno+race roll=0.0 -> packFallback", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, denyXR, new[] { 0.0 }, 1, ChainTier.PackFallback, FallbackSound, XenoPackKey, ref failures);
        Expect("4tier deny-xeno+race roll=0.5 -> builtin", fourPlayable, RaceOrXeno(true), SelectionMode.Remix, denyXR, new[] { 0.5 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 三层对照：声明放在 race 条目上，ctx=xeno 时精确域池读不到 → 仍走 SelectRemixThree。
        SqueakPoolRegistry raceDeclared = Layers(true, true, true, raceFallback: FallbackSound);
        Expect("3tier xeno-ctx ignores race-domain declaration roll=0.0 -> xeno", raceDeclared, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("3tier xeno-ctx ignores race-domain declaration roll=0.34 -> race", raceDeclared, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.34 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("3tier xeno-ctx ignores race-domain declaration roll=0.67 -> builtin", raceDeclared, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.67 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 同夹具换 race-only ctx：声明被读到 → 四层，且 x 层天然为空（前置空层，四层递减逻辑）。
        Expect("4tier race-ctx roll=0.0 -> race", raceDeclared, RaceOrXeno(false), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("4tier race-ctx roll=0.34 -> packFallback", raceDeclared, RaceOrXeno(false), SelectionMode.Remix, AllowAll, new[] { 0.34 }, 1, ChainTier.PackFallback, FallbackSound, RacePackKey, ref failures);
        Expect("4tier race-ctx roll=0.67 -> builtin", raceDeclared, RaceOrXeno(false), SelectionMode.Remix, AllowAll, new[] { 0.67 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 缺陷面：三层路径 + 前置空（无 xeno 池）+ 恰两层可播。
        SqueakPoolRegistry leadingEmpty = Layers(false, true, true, raceFallback: FallbackSound);
        Expect("3tier leading-empty roll=0.0 -> race", leadingEmpty, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("3tier leading-empty roll=0.75 -> builtin", leadingEmpty, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.75 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 缺陷面：三层路径 + 中间空（无 race 池、无声明）→ 可播集 [xeno, builtin]。
        SqueakPoolRegistry threeMiddleEmpty = Layers(true, false, true);
        Expect("3tier middle-empty roll=0.0 -> xeno", threeMiddleEmpty, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("3tier middle-empty roll=0.75 -> builtin", threeMiddleEmpty, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.75 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 四层对照：同可播集 [xeno, builtin]，但走四层入口（声明存在、p 被 gate 拒）→ 结果必须一致。
        SqueakPoolRegistry fourMiddleEmpty = Layers(true, false, true, xenoFallback: FallbackSound);
        Expect("4tier middle-empty(fallback denied) roll=0.0 -> xeno", fourMiddleEmpty, RaceOrXeno(true), SelectionMode.Remix, denyFallback, new[] { 0.0 }, 1, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("4tier middle-empty(fallback denied) roll=0.75 -> builtin", fourMiddleEmpty, RaceOrXeno(true), SelectionMode.Remix, denyFallback, new[] { 0.75 }, 1, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
    }

    // ---- 5. Off 仅内置 / Fallback 四级短路保真 ------------------------------

    private static void OffAndFallbackFidelity(ref int failures)
    {
        SqueakPoolRegistry full = Layers(true, true, true);
        SqueakPoolRegistry four = Layers(true, true, true, xenoFallback: FallbackSound);

        // Off：池存在也只读内置表，零抽签。
        Expect("Off full registry -> builtin only", full, RaceOrXeno(true), SelectionMode.Off, AllowAll, new[] { 0.0 }, 0, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("Off without builtin profile -> None", Layers(true, true, false), RaceOrXeno(true), SelectionMode.Off, AllowAll, new[] { 0.0 }, 0, null, null, null, ref failures);
        Expect("Off ignores pack fallback declaration", four, RaceOrXeno(true), SelectionMode.Off, AllowAll, new[] { 0.0 }, 0, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // Fallback：xeno → race → packFallback → builtin 短路，零 tier 抽签。
        Expect("Fallback prefers xeno", full, RaceOrXeno(true), SelectionMode.Fallback, AllowAll, new[] { 0.0 }, 0, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("Fallback xeno denied -> race", full, RaceOrXeno(true), SelectionMode.Fallback, new DenyGate(XenoSound), new[] { 0.0 }, 0, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);
        Expect("Fallback xeno+race denied -> packFallback", four, RaceOrXeno(true), SelectionMode.Fallback, new DenyGate(XenoSound, RaceSound), new[] { 0.0 }, 0, ChainTier.PackFallback, FallbackSound, XenoPackKey, ref failures);
        Expect("Fallback pack denied too -> builtin", four, RaceOrXeno(true), SelectionMode.Fallback, new DenyGate(XenoSound, RaceSound, FallbackSound), new[] { 0.0 }, 0, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);
        Expect("Fallback everything denied incl. builtin -> None", four, RaceOrXeno(true), SelectionMode.Fallback, new DenyGate(XenoSound, RaceSound, FallbackSound, BuiltInSound), new[] { 0.0 }, 0, null, null, null, ref failures);
        Expect("Fallback race ctx reads own pack fallback", Layers(false, true, true, raceFallback: FallbackSound), RaceOrXeno(false), SelectionMode.Fallback, new DenyGate(RaceSound), new[] { 0.0 }, 0, ChainTier.PackFallback, FallbackSound, RacePackKey, ref failures);
    }

    // ---- 6. 层内选择按既有消耗（多声音 / 多条目） ---------------------------

    private static void InLayerConsumptionUnchanged(ref int failures)
    {
        // xeno 层 2 声音：先抽层内 sound，再抽 tier；消耗顺序 = [层内, tier]。
        SqueakPoolRegistry twoSounds = Registry(BuiltIn(),
            Pack(XenoPackKey, Scenarios.XenoDomain, XenoSound0, XenoSound1),
            Pack(RacePackKey, Scenarios.RaceDomain, RaceSound));
        Expect("in-layer 2 sounds [0.0,0.0] -> xeno sound0", twoSounds, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.0, 0.0 }, 2, ChainTier.XenotypePack, XenoSound0, XenoPackKey, ref failures);
        Expect("in-layer 2 sounds [0.5,0.5] -> race (xeno sound1 drawn first)", twoSounds, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5, 0.5 }, 2, ChainTier.RacePack, RaceSound, RacePackKey, ref failures);

        // xeno 层 2 条目：DrawEntry 1 次 + tier 1 次。
        SqueakPoolRegistry twoEntries = Registry(BuiltIn(),
            Pack(XenoPackKey, Scenarios.XenoDomain, XenoSound),
            Pack("us.regression:XenoPack2", Scenarios.XenoDomain, "US_REG_Xeno2_Call"),
            Pack(RacePackKey, Scenarios.RaceDomain, RaceSound));
        Expect("in-layer 2 entries [0.1,0.1] -> xeno entry0", twoEntries, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.1, 0.1 }, 2, ChainTier.XenotypePack, XenoSound, XenoPackKey, ref failures);
        Expect("in-layer 2 entries [0.5,0.9] -> builtin (tier index2)", twoEntries, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5, 0.9 }, 2, ChainTier.BuiltInFallback, BuiltInSound, null, ref failures);

        // 唯一可播层：只消耗层内抽签，绝不追加 tier 抽签。
        SqueakPoolRegistry onlyXeno = Registry(BuiltInFallbackTable.Empty,
            Pack(XenoPackKey, Scenarios.XenoDomain, XenoSound0, XenoSound1));
        Expect("single playable layer consumes only in-layer draw", onlyXeno, RaceOrXeno(true), SelectionMode.Remix, AllowAll, new[] { 0.5 }, 1, ChainTier.XenotypePack, XenoSound1, XenoPackKey, ref failures);
    }

    // ---- helpers -----------------------------------------------------------

    private static readonly ISoundGate AllowAll = new DenyGate();

    /// <summary>true = xeno ctx（三层都可参与）；false = race-only ctx（x 层天然为空）。</summary>
    private static AudioDomain RaceOrXeno(bool xeno) => xeno ? Scenarios.XenoDomain : Scenarios.RaceDomain;

    private static string Bit(bool value) => value ? "1" : "0";

    /// <summary>[xeno, race, builtin] 存在性组合夹具；xenoFallback/raceFallback 在对应条目上声明 Call 的 PackFallback。</summary>
    private static SqueakPoolRegistry Layers(bool xeno, bool race, bool builtIn, string? xenoFallback = null, string? raceFallback = null)
    {
        List<VoicePackEntry> entries = new();
        if (xeno) entries.Add(PackWithFallback(XenoPackKey, Scenarios.XenoDomain, xenoFallback, XenoSound));
        if (race) entries.Add(PackWithFallback(RacePackKey, Scenarios.RaceDomain, raceFallback, RaceSound));
        return new SqueakPoolRegistry(entries, builtIn ? BuiltIn() : BuiltInFallbackTable.Empty);
    }

    private static SqueakPoolRegistry Registry(BuiltInFallbackTable builtIn, params VoicePackEntry[] entries)
        => new(entries, builtIn);

    private static VoicePackEntry Pack(string packKey, AudioDomain domain, params string[] soundKeys)
        => PackWithFallback(packKey, domain, null, soundKeys);

    private static VoicePackEntry PackWithFallback(string packKey, AudioDomain domain, string? fallbackSound, params string[] soundKeys)
    {
        Dictionary<string, IReadOnlyList<ActionSoundSet>> actions = new();
        actions[Act!] = new[] { new ActionSoundSet(soundKeys, null, 1f) };
        Dictionary<string, string>? fallback = fallbackSound == null
            ? null
            : new Dictionary<string, string> { [Act!] = fallbackSound };
        return new VoicePackEntry(packKey, domain, 1f, actions, fallback);
    }

    /// <summary>内置表只播种 Call → 测试声音键（内核不携带产品种子）。</summary>
    private static BuiltInFallbackTable BuiltIn()
        => new BuiltInFallbackTable(new[]
        {
            new FallbackProfile(Scenarios.RaceA, 1, new Dictionary<string, string> { [Act!] = BuiltInSound }),
            new FallbackProfile(Scenarios.RaceB, 1, new Dictionary<string, string> { [Act!] = BuiltInSound }),
        });

    private sealed class DenyGate : ISoundGate
    {
        private readonly string[] denied;

        public DenyGate(params string[] denied) => this.denied = denied ?? Array.Empty<string>();

        public bool Playable(string soundKey, SelectionContext ctx)
        {
            if (soundKey == null) return false;
            foreach (string key in denied)
                if (string.Equals(key, soundKey, StringComparison.Ordinal)) return false;
            return true;
        }
    }

    /// <summary>脚本化可计数随机源：按序出牌并记录 Next01 消耗次数；越界取 0 并标记（该断言必失败）。</summary>
    private sealed class ScriptedRolls : IRollSource
    {
        private readonly double[] script;
        private int index;

        public ScriptedRolls(double[] script) => this.script = script ?? Array.Empty<double>();

        public int Consumed => index;
        public bool Overflowed => index > script.Length;

        public double Next01()
        {
            double value = index < script.Length ? script[index] : 0.0;
            index++;
            return value;
        }
    }

    private static void Expect(string label, SqueakPoolRegistry registry, AudioDomain domain, SelectionMode mode, ISoundGate gate,
        double[] script, int expectedConsumed, ChainTier? expectedTier, string? expectedKey, string? expectedPool, ref int failures)
    {
        ScriptedRolls rolls = new(script);
        SelectionContext ctx = new(domain, Act!, AgeBucket.Adult, false, false);
        ChainResult result = registry.Select(ctx, mode, gate, rolls);
        bool ok = result.Tier == expectedTier
            && string.Equals(result.SoundKey, expectedKey, StringComparison.Ordinal)
            && string.Equals(result.PoolStableKey, expectedPool, StringComparison.Ordinal)
            && rolls.Consumed == expectedConsumed
            && !rolls.Overflowed;
        if (ok)
        {
            Console.WriteLine("  ok: " + label);
            return;
        }
        Console.Error.WriteLine("  FAIL: " + label
            + " [expected " + Describe(expectedTier) + "/" + Describe(expectedKey) + "/" + Describe(expectedPool) + "/draws=" + expectedConsumed
            + " | actual " + Describe(result.Tier) + "/" + Describe(result.SoundKey) + "/" + Describe(result.PoolStableKey)
            + "/draws=" + rolls.Consumed + (rolls.Overflowed ? "+script-overflow" : "") + "]");
        failures++;
    }

    private static string Describe(string? value) => value ?? "-";
    private static string Describe(ChainTier? tier) => tier?.ToString() ?? "-";
}