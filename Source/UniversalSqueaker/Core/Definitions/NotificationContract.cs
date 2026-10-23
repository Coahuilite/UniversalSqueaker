using System;
using UniversalSqueaker.Responses;

namespace UniversalSqueaker.Authoring;

/// <summary>An explicitly declared notification, not an arbitrary string converted into a plan.</summary>
public sealed class NotificationContract
{
    public string ActionId { get; }
    public string FactId => ActionId + "/Notification";
    private readonly long interval;
    private readonly int priority;
    private readonly Condition restriction;

    public NotificationContract(string actionId, long intervalTicks = 0, int priority = 0, Condition? restriction = null)
    {
        if (!AuthorXml.ActionId(actionId)) throw new ArgumentException("Complete ActionId required", nameof(actionId));
        if (intervalTicks < 0) throw new ArgumentOutOfRangeException(nameof(intervalTicks));
        ActionId = actionId;
        interval = intervalTicks;
        this.priority = priority;
        this.restriction = restriction ?? new Condition("all");
    }

    internal ActionDeclaration Declaration() => new()
    {
        Key = ActionId, Interval = interval, Priority = priority,
        Condition = new Condition("all", "", new Condition("ref", FactId), restriction)
    };
}
