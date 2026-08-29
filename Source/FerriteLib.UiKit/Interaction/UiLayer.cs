namespace FerriteLib.UiKit;

/// <summary>
/// Input routing layers. Higher values win when multiple registered interaction areas overlap.
/// </summary>
public enum UiLayer
{
    Background = 0,
    Content = 1,
    TopAction = 2,
    Overlay = 3
}
