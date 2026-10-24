using HarmonyLib;
using UnityEngine;
using Verse;

namespace UniversalSqueaker;

/// <summary>The native composition root and framework lifecycle only.</summary>
public sealed class SqueakerMod : Mod
{
    public const string PackageId = "coahuilite.universalsqueaker";
    public static Harmony Harmony = null!;
    public static GameSettings Settings = null!;
    public static SqueakerMod? Instance { get; private set; }
    public static bool PrerequisiteVerified => GameBootstrap.PrerequisiteVerified;
    private readonly SettingsWindowOwner windows;
    private readonly GameSettingsPersistence persistence;
    public SqueakerMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<GameSettings>();
        Harmony = new Harmony(PackageId);
        persistence = new GameSettingsPersistence(Settings, content.FolderName, GetType().Name, () => base.WriteSettings());
        windows = new SettingsWindowOwner(this, persistence);
        GameBootstrap.Start(Harmony, Settings, persistence);
    }
    public override string SettingsCategory() => SqueakLabels.SettingsCategory;
    public override void DoSettingsWindowContents(Rect rect) => windows.DrawShell(rect);
    public override void WriteSettings() => persistence.Flush(true);
    public void OpenSettings() => windows.OpenSettings();
    internal void RegisterSettingsWindow(Window window) => windows.RegisterSettingsWindow(window);
    internal bool IsOwnedSettingsWindow(Window window) => windows.IsOwnedSettingsWindow(window);
    internal static void NotifySettingsWindowClosing(Window window) => Instance?.windows.NotifyClosing(window);
    internal static string BuildIdentity() => SettingsWindowOwner.BuildIdentity();
    internal void QueueSettingsSave() => persistence.Queue();
    internal void TickSettingsSaveForWindow() => persistence.Tick();
    internal void FlushQueuedSettingsSave(bool force = false, bool allowFailedGenerationRetry = false) => persistence.Flush(force, allowFailedGenerationRetry);
    internal void RetrySettingsSave() => persistence.Retry();
    internal SettingsSaveState SaveState => persistence.Owner.State;
    internal bool SaveStatusVisible => persistence.Owner.StatusVisible(Time.realtimeSinceStartup);
    internal bool IsSettingsDirty => persistence.Owner.Dirty;
    public long SaveQueueRequestCount => persistence.Owner.RequestCount;
    public long PhysicalSaveCount => persistence.Owner.PhysicalSaveCount;
    /// <summary>Persist source intent before adopting and publishing it.</summary>
    /// <returns>Whether disk committed. A Committed:RuntimePublishFailed diagnostic means publication
    /// repair is pending, not rollback; do not resubmit the disk transaction.</returns>
    public bool TryCommitBehaviorSource(BehaviorDomain domain, string? source, out string diagnostic) => persistence.TryCommitBehaviorSource(domain, source, out diagnostic);
    /// <summary>Persist one complete sparse player row before adoption/publication; absent fields inherit.</summary>
    /// <returns>Disk committed, even if the diagnostic reports a subsequent publication failure.</returns>
    public bool TryCommitBehaviorOverride(string family, string targetNamespace, string target, string child, string action,
        BehaviorValues values, out string diagnostic) => persistence.TryCommitBehaviorOverride(family, targetNamespace, target, child, action, values, out diagnostic);
}
