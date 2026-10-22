using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Explicit selection/persistence platform double. The Harmony observers, settings resolver, session and backend are production.
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public enum LookMode { Deep }
    public static class Scribe_Values { public static void Look<T>(ref T value, string name, T defaultValue) { } }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> value, string name, LookMode mode) { } }
}
namespace RimWorld
{
    public sealed class Selector
    {
        public readonly List<object> Selected = new();
        public readonly Dictionary<object, object> Proxies = new();
        public bool Reject, FailAfterFeedback, EmitFeedbackForExisting;
        public bool IsSelected(object obj) => Selected.Contains(obj);
        public void Select(object obj, bool playSound = true) => SelectInternal(obj, playSound, true);
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SelectInternal(object obj, bool playSound, bool forceDesignatorDeselect)
        {
            if (Reject) return;
            if (Proxies.TryGetValue(obj, out var target)) { SelectInternal(target, playSound, forceDesignatorDeselect); return; }
            if (IsSelected(obj) && !EmitFeedbackForExisting) return; // Source version's branch, not a US dedup policy.
            if (playSound) PlaySelectionSoundFor(obj);
            if (FailAfterFeedback) throw new InvalidOperationException("selection interrupted before commit");
            if (!IsSelected(obj)) Selected.Add(obj);
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private void PlaySelectionSoundFor(object obj) { }
    }
}
