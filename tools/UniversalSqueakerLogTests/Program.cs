using System;
using System.Reflection;
using UniversalSqueaker;
using Verse;

namespace UniversalSqueaker.LogCharacterization;

internal static class Program
{
    private const string Build = "dev";
    private const string BuildId = "0.0.0+char";
    private const string Prefix = "[UniversalSqueaker] ";
#if US_DEV
    private static bool AutoEnablesDevLogging => true;
#else
    private static bool AutoEnablesDevLogging => false;
#endif

    private static int failures;

    /// <summary>fmt=2 event ids exercised by VerifyV2Protocol; the completeness check requires
    /// every protocol-v2 registry event to appear here (v2 characterization 完整性检查).</summary>
    private static readonly System.Collections.Generic.HashSet<string> v2CoveredEvents = new(StringComparer.Ordinal);

    private static int Main()
    {
        VerifyAllEventDefinitions();
        VerifyOnceSemantics();
        VerifyOnceLimit();
        VerifyDisabledModeGate();
        VerifySilentFailureBoundary();
        VerifyEncodingAndExceptionMetadata();
        VerifyInvalidLoggingModeFallsBackToAuto();
        VerifyV2Protocol();
        VerifyV2Completeness();

        if (failures == 0)
        {
            Console.WriteLine("SqueakLog characterization passed.");
            return 0;
        }

        Console.Error.WriteLine($"SqueakLog characterization failed: {failures} assertion(s).");
        return 1;
    }

    private static void VerifyAllEventDefinitions()
    {
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.StartupIdentity();
        SqueakLog.StartupReady(37);
        SqueakLog.LoggingModeChanged(SqueakDevLoggingMode.Enabled, true);
        SqueakLog.LoggingModeChanged(SqueakDevLoggingMode.Disabled, false);
        SqueakLog.LoggingModeChanged(SqueakDevLoggingMode.Auto, true);
        SqueakLog.LoggingModeChanged(SqueakDevLoggingMode.Auto, false);
        SqueakLog.SettingsOpenApiUnavailable();
        SqueakLog.SettingsOpenFailed(new Exception("settings failed"));
        SqueakLog.CatalogRefreshFailed(new Exception("catalog failed"));
        SqueakLog.PackRejected("coahuilite.universalsqueaker:US_Example", 2);
        SqueakLog.ResolverRebuildFailed(new Exception("resolver failed"));
        SqueakLog.TargetRejected("target-1", "reason-1");
        SqueakLog.TriggerAttemptFailed("Select", new Exception("trigger failed"));
        SqueakLog.AudioNoSound("Move");
        SqueakLog.AudioDispatchFailed("Attack", "US_Attack_1", new Exception("dispatch failed"));
        SqueakLog.AudioDispatchOk("Select", "12345", "US_OfficialExample_Race_Select", 0, "Mousy", "Thing_Race12345");
        SqueakLog.TriggerOutcomeSummary(7, 2);
        SqueakLog.HookAttackUnavailable();
        SqueakLog.HookAttackTargetSkipped("999", "not player ordered");
        SqueakLog.HookMentalBreakUnavailable();

        AssertLines(nameof(VerifyAllEventDefinitions),
            D("info", "daily", "mod.start.identity", $"Universal Squeaker started with {Build} build {BuildId}."),
            D("info", "daily", "mod.start.ready", "Universal Squeaker startup completed.", trailing: " count=37"),
            D("info", "daily", "logging.mode.enabled", "Detailed diagnostic logging is enabled.", trailing: " enabled=true"),
            D("info", "daily", "logging.mode.disabled", "Detailed diagnostic logging is disabled.", trailing: " enabled=false"),
            D("info", "daily", "logging.mode.auto_enabled", "Detailed diagnostic logging is enabled by Auto mode.", trailing: " enabled=true"),
            D("info", "daily", "logging.mode.auto_disabled", "Detailed diagnostic logging is disabled by Auto mode.", trailing: " enabled=false"),
            D("warning", "daily", "settings.open.api_unavailable", "Mod Settings API is unavailable."),
            D("warning", "daily", "settings.open.failed", "Mod Settings could not be opened.", trailing: " ex_type=System.Exception ex_msg=settings%20failed"),
            D("error", "daily", "voicepack.catalog.refresh_failed", "VoicePack catalog refresh failed.", trailing: " ex_type=System.Exception ex_msg=catalog%20failed"),
            D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "coahuilite.universalsqueaker:US_Example", trailing: " reason=duplicate_key count=2"),
            D("error", "daily", "voicepack.resolver.rebuild_failed", "VoicePack resolver rebuild failed.", trailing: " ex_type=System.Exception ex_msg=resolver%20failed"),
            D("warning", "daily", "voicepack.target.rejected", "A Xenotype VoicePack target was rejected.", target: "target-1", trailing: " reason=reason-1"),
            D("error", "daily", "trigger.attempt.failed", "Squeak trigger attempt failed.", action: "Select", trailing: " ex_type=System.Exception ex_msg=trigger%20failed"),
            D("warning", "daily", "audio.dispatch.no_sound", "No fallback SoundDef was found.", action: "Move"),
            D("error", "daily", "audio.dispatch.failed", "Squeak audio dispatch failed.", action: "Attack", trailing: " sound=US_Attack_1 ex_type=System.Exception ex_msg=dispatch%20failed"),
            D("info", "dev_only", "audio.dispatch.ok", "Squeak audio dispatched.", action: "Select", target: "12345", trailing: " sound=US_OfficialExample_Race_Select suppressed_detail=0 pawn=Mousy pawn_id=Thing_Race12345"),
            D("info", "dev_only", "trigger.outcome.summary", "Squeak trigger outcome summary was recorded.", trailing: " dispatched=7 suppressed_detail=2"),
            D("error", "daily", "hook.attack.unavailable", "Attack squeak hook is unavailable."),
            D("warning", "dev_only", "hook.attack.target_skipped", "An Attack hook target was skipped.", target: "999", trailing: " reason=not%20player%20ordered"),
            D("error", "daily", "hook.mental_break.unavailable", "Mental-break squeak hook is unavailable."));
    }

    private static void VerifyOnceSemantics()
    {
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.PackRejected("p1", 1);
        SqueakLog.PackRejected("p1", 2);
        SqueakLog.PackRejected("p2", 1);
        AssertLines(nameof(VerifyOnceSemantics),
            D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "p1", trailing: " reason=duplicate_key count=1"),
            D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "p2", trailing: " reason=duplicate_key count=1"));

        SqueakLog.ResetSession();
        Verse.Log.Reset();
        SqueakLog.PackRejected("p1", 1);
        AssertLines(nameof(VerifyOnceSemantics) + " reset", D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "p1", trailing: " reason=duplicate_key count=1"));
    }

    private static void VerifyOnceLimit()
    {
        Reset(SqueakDevLoggingMode.Enabled);
        for (int i = 0; i < 1024; i++) SqueakLog.PackRejected("cap" + i, 1);
        SqueakLog.PackRejected("p1", 1);
        SqueakLog.PackRejected("cap0", 1);

        AssertEqual(1026, Verse.Log.Captured.Count, nameof(VerifyOnceLimit) + " count");
        AssertEqual(D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "cap0", trailing: " reason=duplicate_key count=1"), Format(Verse.Log.Captured[0]), nameof(VerifyOnceLimit) + " first");
        AssertEqual(D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "cap1023", trailing: " reason=duplicate_key count=1"), Format(Verse.Log.Captured[1023]), nameof(VerifyOnceLimit) + " limit");
        AssertEqual(D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "p1", trailing: " reason=duplicate_key count=1"), Format(Verse.Log.Captured[1024]), nameof(VerifyOnceLimit) + " reclaimed");
        AssertEqual(D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "cap0", trailing: " reason=duplicate_key count=1"), Format(Verse.Log.Captured[1025]), nameof(VerifyOnceLimit) + " cleared prior key");
    }

    private static void VerifyDisabledModeGate()
    {
        Reset(SqueakDevLoggingMode.Disabled);
        SqueakLog.StartupIdentity();
        SqueakLog.AudioDispatchOk("Select", "1", "s", 0, "P", "Thing_Race1");
        SqueakLog.HookAttackUnavailable();
        SqueakLog.PackRejected("p1", 1);

        AssertLines(nameof(VerifyDisabledModeGate),
            Human("info", $"Universal Squeaker started with {Build} build {BuildId}."),
            Human("error", "Attack squeak hook is unavailable."),
            Human("warning", "A VoicePack was rejected."));
    }

    private static void VerifySilentFailureBoundary()
    {
        Reset(SqueakDevLoggingMode.Enabled);
        MethodInfo? emit = typeof(SqueakLog).GetMethod("Emit", BindingFlags.NonPublic | BindingFlags.Static);
        if (emit == null)
        {
            Fail(nameof(VerifySilentFailureBoundary) + ": Emit method was not found.");
            return;
        }

        try
        {
            emit.Invoke(null, new object[] { (SqueakLogEvent)999, default(SqueakLogData), false });
        }
        catch (Exception ex)
        {
            Fail(nameof(VerifySilentFailureBoundary) + ": Emit escaped " + ex.GetType().FullName + ".");
        }

        AssertEqual(0, Verse.Log.Captured.Count, nameof(VerifySilentFailureBoundary) + " output");
    }

    private static void VerifyEncodingAndExceptionMetadata()
    {
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.SettingsOpenFailed(CreateNestedException());
        SqueakLog.AudioDispatchFailed("Attack", "US_Attack_1", new Exception(new string('a', 300)));
        SqueakLog.TargetRejected("t 1", "N/A");
        SqueakLog.AudioNoSound("中文");
        SqueakLog.PackRejected("p a+b?c", 1);

        AssertLines(nameof(VerifyEncodingAndExceptionMetadata),
            D("warning", "daily", "settings.open.failed", "Mod Settings could not be opened.", trailing: " ex_type=System.ApplicationException ex_inner=System.InvalidOperationException ex_site=UniversalSqueaker.LogCharacterization.Program.CreateNestedException ex_msg=boom%20at%20%3Cpath%3E%20Mods%5Cfile.c%3Cpath%3E%20second%20line"),
            D("error", "daily", "audio.dispatch.failed", "Squeak audio dispatch failed.", action: "Attack", trailing: " sound=US_Attack_1 ex_type=System.Exception ex_msg=" + new string('a', 256)),
            D("warning", "daily", "voicepack.target.rejected", "A Xenotype VoicePack target was rejected.", target: "t%201", trailing: " reason=-"),
            D("warning", "daily", "audio.dispatch.no_sound", "No fallback SoundDef was found.", action: "%E4%B8%AD%E6%96%87"),
            D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "p%20a+b%3Fc", trailing: " reason=duplicate_key count=1"));

        AssertEqual("-", SqueakLogText.PercentEncode(null), nameof(VerifyEncodingAndExceptionMetadata) + " null encoding");
        AssertEqual("-", SqueakLogText.PercentEncode("N/A"), nameof(VerifyEncodingAndExceptionMetadata) + " N/A encoding");
        AssertEqual("a+b%3Fc", SqueakLogText.PercentEncode("a+b?c"), nameof(VerifyEncodingAndExceptionMetadata) + " reserved encoding");
        AssertEqual(256, SqueakLogText.SanitizeExceptionMessage(new string('a', 300)).Length, nameof(VerifyEncodingAndExceptionMetadata) + " exception truncation");
    }

    private static void VerifyInvalidLoggingModeFallsBackToAuto()
    {
        Reset((SqueakDevLoggingMode)999);
        AssertEqual(SqueakDevLoggingMode.Auto, SqueakLog.Mode, nameof(VerifyInvalidLoggingModeFallsBackToAuto) + " mode");
        AssertEqual(AutoEnablesDevLogging, SqueakLog.EffectiveDevLogging, nameof(VerifyInvalidLoggingModeFallsBackToAuto) + " effective mode");
        SqueakLog.StartupReady(1);

        if (AutoEnablesDevLogging)
            AssertLines(nameof(VerifyInvalidLoggingModeFallsBackToAuto), D("info", "daily", "mod.start.ready", "Universal Squeaker startup completed.", trailing: " count=1"));
        else
            AssertLines(nameof(VerifyInvalidLoggingModeFallsBackToAuto), Human("info", "Universal Squeaker startup completed."));
    }

    /// <summary>usdiag v2: fmt=2 header, fixed v2 core order with race/[xenotype],
    /// string action keys, tier, settings.origin, and the log-v2 once domain. All v1 asserts above
    /// re-verify that the fmt=1 bytes are unchanged for the retained event surface.</summary>
    private static void VerifyV2Protocol()
    {
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.FreshCreated);
        SqueakLog.AudioRouteSelected("Select", "RaceA", null, "12345", "US_OfficialExample_Race_Select", "race_pack", "coahuilite.universalsqueaker:US_OfficialExample_Race", pawnControlled: true, pawnFaction: "PlayerColony");
        SqueakLog.AudioRouteSelected("coahuilite.universalsqueaker.external_action", "RaceA", "Baseliner", "777", "US_Baseliner_Select", "xenotype_pack", "coahuilite.universalsqueaker:US_Baseliner", pawnControlled: false, pawnFaction: "Pirate");
        SqueakLog.AudioRouteSelected("Move", "RaceA", null, "1", "US_Move_1", "vanilla", null, pawnControlled: true, pawnFaction: "PlayerColony");
        // 0.3.2 egg/log 重排：audio.route.selected 承载 egg/pawn/suppressed/faction/pawn_ctrl 完整明细。
        SqueakLog.AudioRouteSelected("Joy", "RaceA", null, "888", "US_EggTest_Select_Joy", "race_pack", "coahuilite.universalsqueaker.eggtest:US_EggTest_Select", true, 3, "Mousy", "Thing_Race888", false, "Pirate");
        SqueakLog.FallbackProfileStoreFailed("RaceA", new Exception("profile write failed"));
        SqueakLog.HookMentalFitUnavailable();

        AssertLines(nameof(VerifyV2Protocol) + " enabled",
            V2("info", "daily", "settings.origin", "Mod settings origin: FreshCreated.", trailing: " settings_origin=FreshCreated"),
            V2("info", "dev_only", "audio.route.selected", "Audio route: Select -> US_OfficialExample_Race_Select (race_pack).", action: "Select", target: "12345", pack: "coahuilite.universalsqueaker:US_OfficialExample_Race", race: "RaceA", trailing: " sound=US_OfficialExample_Race_Select tier=race_pack egg=false suppressed_detail=0 pawn_faction=PlayerColony pawn_ctrl=player"),
            V2("info", "dev_only", "audio.route.selected", "Audio route: coahuilite.universalsqueaker.external_action -> US_Baseliner_Select (xenotype_pack, nonplayer).", action: "coahuilite.universalsqueaker.external_action", target: "777", pack: "coahuilite.universalsqueaker:US_Baseliner", race: "RaceA", xenotype: "Baseliner", trailing: " sound=US_Baseliner_Select tier=xenotype_pack egg=false suppressed_detail=0 pawn_faction=Pirate pawn_ctrl=nonplayer"),
            V2("info", "dev_only", "audio.route.selected", "Audio route: Move -> US_Move_1 (vanilla).", action: "Move", target: "1", pack: "-", race: "RaceA", trailing: " sound=US_Move_1 tier=vanilla egg=false suppressed_detail=0 pawn_faction=PlayerColony pawn_ctrl=player"),
            V2("info", "dev_only", "audio.route.selected", "Audio route: Joy -> US_EggTest_Select_Joy (race_pack, egg, nonplayer).", action: "Joy", target: "888", pack: "coahuilite.universalsqueaker.eggtest:US_EggTest_Select", race: "RaceA", trailing: " sound=US_EggTest_Select_Joy tier=race_pack egg=true suppressed_detail=3 pawn=Mousy pawn_id=Thing_Race888 pawn_faction=Pirate pawn_ctrl=nonplayer"),
            V2("warning", "dev_only", "fallback.profile.store_failed", "Fallback profile store operation failed.", race: "RaceA", trailing: " ex_type=System.Exception ex_msg=profile%20write%20failed"),
            V2("error", "daily", "hook.mental_fit.unavailable", "Baby-fits squeak hook is unavailable."));
        CaptureV2Coverage();
        // log-v2 once: the first settings.origin claim wins per session; ResetSession reopens the domain.
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.LoadedFromFile);
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.FreshCreated);
        AssertLines(nameof(VerifyV2Protocol) + " once",
            V2("info", "daily", "settings.origin", "Mod settings origin: LoadedFromFile.", trailing: " settings_origin=LoadedFromFile"));
        CaptureV2Coverage();
        SqueakLog.ResetSession();
        Verse.Log.Reset();
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.FreshCreated);
        AssertLines(nameof(VerifyV2Protocol) + " once reset",
            V2("info", "daily", "settings.origin", "Mod settings origin: FreshCreated.", trailing: " settings_origin=FreshCreated"));
        CaptureV2Coverage();

        // log-v1 and log-v2 once domains are independent: identical payload fields claim separate keys.
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.FreshCreated);
        SqueakLog.PackRejected("p1", 1);
        SqueakLog.PackRejected("p1", 2);
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.LoadedFromFile);
        AssertLines(nameof(VerifyV2Protocol) + " independent once",
            V2("info", "daily", "settings.origin", "Mod settings origin: FreshCreated.", trailing: " settings_origin=FreshCreated"),
            D("warning", "daily", "voicepack.pack.rejected", "A VoicePack was rejected.", pack: "p1", trailing: " reason=duplicate_key count=1"));
        CaptureV2Coverage();

        // v2 values flow through the same percent-encoding/sanitization rules as v1; human text stays raw.
        Reset(SqueakDevLoggingMode.Enabled);
        SqueakLog.AudioRouteSelected("some package.action", "Ra tin", null, "t 1", "US_1", "race_pack", null);
        AssertLines(nameof(VerifyV2Protocol) + " encoding",
            V2("info", "dev_only", "audio.route.selected", "Audio route: some package.action -> US_1 (race_pack).", action: "some%20package.action", target: "t%201", race: "Ra%20tin", trailing: " sound=US_1 tier=race_pack egg=false suppressed_detail=0"));
        CaptureV2Coverage();

        // Gating: v2 Daily keeps the human-only shape while detailed logging is ineffective; v2 DevOnly is silent.
        Reset(SqueakDevLoggingMode.Disabled);
        SqueakLog.SettingsOrigin(SqueakSettingsOrigin.FreshCreated);
        SqueakLog.AudioRouteSelected("Select", "RaceA", null, "1", "US_1", "race_pack", null);
        AssertLines(nameof(VerifyV2Protocol) + " disabled",
            Human("info", "Mod settings origin: FreshCreated."));
        CaptureV2Coverage();
    }

    /// <summary>Collect fmt=2 event ids from the last assertion window (called after every v2 block).</summary>
    private static void CaptureV2Coverage()
    {
        foreach (Verse.Log.Entry entry in Verse.Log.Captured)
        {
            string text = entry.Text;
            int fmt = text.IndexOf("fmt=2", StringComparison.Ordinal);
            if (fmt < 0) continue;
            int evt = text.IndexOf(" evt=", fmt, StringComparison.Ordinal);
            if (evt < 0) continue;
            int start = evt + " evt=".Length;
            int end = text.IndexOf(' ', start);
            if (end < 0) end = text.Length;
            string id = text.Substring(start, end - start);
            if (id.Length > 0) v2CoveredEvents.Add(id);
        }
    }

    /// <summary>v2 characterization 完整性检查：注册表内每个协议版本 2 的事件都必须被 VerifyV2Protocol
    /// 实际发射过；反之发射过的 id 必须来自注册表。新 v2 事件加入 SqueakLogProtocol 而不同步
    /// characterization 时此处直接 FAIL。</summary>
    private static void VerifyV2Completeness()
    {
        int before = failures;
        System.Collections.Generic.HashSet<string> expected = new(StringComparer.Ordinal);
        foreach (SqueakLogEvent e in Enum.GetValues(typeof(SqueakLogEvent)))
        {
            SqueakLogDefinition definition = SqueakLogRegistry.Definition(e, Build, BuildId);
            if (definition.Version >= 2) expected.Add(SqueakLogRegistry.EventId(e));
        }

        AssertEqual(4, expected.Count, nameof(VerifyV2Completeness) + " v2 registry size");
        foreach (string id in expected)
            AssertEqual(true, v2CoveredEvents.Contains(id), nameof(VerifyV2Completeness) + " exercised " + id);
        foreach (string id in v2CoveredEvents)
            AssertEqual(true, expected.Contains(id), nameof(VerifyV2Completeness) + " unexpected covered id " + id);

        if (failures == before)
            Console.WriteLine("v2 protocol characterization complete: all " + expected.Count + " fmt=2 registry events exercised.");
    }

    private static Exception CreateNestedException()
    {
        try
        {
            throw new InvalidOperationException("inner");
        }
        catch (Exception inner)
        {
            try
            {
                throw new ApplicationException("boom at C:\\My Mods\\file.cs:42\nsecond line", inner);
            }
            catch (Exception outer)
            {
                return outer;
            }
        }
    }

    private static void Reset(SqueakDevLoggingMode mode)
    {
        SqueakLog.Configure(mode);
        SqueakLog.ResetSession();
        Verse.Log.Reset();
    }

    private static string D(string level, string visibility, string eventId, string human, string action = "-", string target = "-", string pack = "-", string trailing = "")
    {
        return level + "|" + Prefix + human + " || usdiag fmt=1 lvl=" + level + " vis=" + visibility + " evt=" + eventId + " action=" + action + " target=" + target + " pack=" + pack + " build=" + Build + " build_id=" + BuildId + trailing;
    }

    private static string Human(string level, string text) => level + "|" + Prefix + text;
    private static string Format(Verse.Log.Entry entry) => entry.Level + "|" + entry.Text;

    /// <summary>v2 expected line: fixed core order fmt=2 lvl vis evt action target pack race [xenotype] build build_id.
    /// xenotype = null omits the optional field; pass "-" explicitly to assert an explicit dash.</summary>
    private static string V2(string level, string visibility, string eventId, string human, string action = "-", string target = "-", string pack = "-", string race = "-", string? xenotype = null, string trailing = "")
    {
        return level + "|" + Prefix + human + " || usdiag fmt=2 lvl=" + level + " vis=" + visibility + " evt=" + eventId + " action=" + action + " target=" + target + " pack=" + pack + " race=" + race + (xenotype == null ? "" : " xenotype=" + xenotype) + " build=" + Build + " build_id=" + BuildId + trailing;
    }

    private static void AssertLines(string name, params string[] expected)
    {
        if (Verse.Log.Captured.Count != expected.Length)
        {
            Fail(name + ": expected " + expected.Length + " lines, got " + Verse.Log.Captured.Count + ".");
            return;
        }

        for (int i = 0; i < expected.Length; i++)
            AssertEqual(expected[i], Format(Verse.Log.Captured[i]), name + " line " + i);
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!Equals(expected, actual))
            Fail(name + ": expected '" + expected + "', got '" + actual + "'.");
    }

    private static void Fail(string message)
    {
        failures++;
        Console.Error.WriteLine(message);
    }
}
