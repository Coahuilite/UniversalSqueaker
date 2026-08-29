using System.Collections.Generic;

namespace FerriteLib.UiKit;

/// <summary>
/// Stores <see cref="UiValueState"/> instances by <see cref="UiControlId"/> for the lifetime of the
/// process. <see cref="ResetFrame"/> clears transient per-frame state only; committed values persist.
/// </summary>
public static class UiValueStore
{
    private static readonly Dictionary<UiControlId, UiValueState> States = new();

    public static UiValueState GetOrCreate(UiControlId id)
    {
        if (!id.IsValid)
            return new UiValueState();

        if (!States.TryGetValue(id, out UiValueState? state))
        {
            state = new UiValueState();
            States.Add(id, state);
        }

        return state;
    }

    public static void ResetFrame()
    {
        foreach (UiValueState state in States.Values)
        {
            state.Dragging = false;
            state.Focused = false;
            state.Cursor = 0;
        }
    }
}
