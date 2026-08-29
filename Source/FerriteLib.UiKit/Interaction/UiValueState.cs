namespace FerriteLib.UiKit;

/// <summary>Per-control value state shared between slider and number field primitives.</summary>
public sealed class UiValueState
{
    public float FloatValue;
    public string EditText = "";
    public bool Dragging;
    public bool Focused;
    public int Cursor;
}
