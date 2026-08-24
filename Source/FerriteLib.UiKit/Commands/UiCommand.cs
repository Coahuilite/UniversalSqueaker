using System;

namespace FerriteLib.UiKit;

/// <summary>Neutral command emitted by widgets during Draw. The owning mod interprets it.</summary>
public readonly struct UiCommand
{
    public string Name { get; }

    public object? Payload { get; }

    public UiCommand(string name, object? payload = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Payload = payload;
    }
}
