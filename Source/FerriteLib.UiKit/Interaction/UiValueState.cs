namespace FerriteLib.UiKit;

/// <summary>
/// Per-control value state shared between value primitives and composite controls.
/// <see cref="Open"/> and <see cref="StringValue"/> support self-drawn select controls and
/// persist across frames; transient interaction fields are cleared by <see cref="UiValueStore.ResetFrame"/>.
/// </summary>
public sealed class UiValueState
{
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;
    public bool Focused;
    public int Cursor;
    public bool Open;
    public string? StringValue;
}
