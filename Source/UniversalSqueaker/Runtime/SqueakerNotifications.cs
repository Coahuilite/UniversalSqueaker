using System;
using System.Collections.Generic;
using UniversalSqueaker.Authoring;
using Verse;

namespace UniversalSqueaker;

/// <summary>1.6 author entry. Declare once during definition initialization, then notify the real subject.</summary>
public static class SqueakerNotifications
{
    public static string SubjectKey(Pawn subject) => subject == null ? throw new ArgumentNullException(nameof(subject)) : SqueakerRuntime.SubjectKey(subject);
    private static readonly Dictionary<string, NotificationContract> declarations = new(StringComparer.Ordinal);
    internal static IEnumerable<NotificationContract> Declarations => declarations.Values;

    public static void Declare(NotificationContract contract)
    {
        if (contract == null) throw new ArgumentNullException(nameof(contract));
        if (declarations.ContainsKey(contract.ActionId)) throw new ArgumentException("Notification already declared: " + contract.ActionId);
        if (!SqueakerRuntime.DeclareNotification(contract)) throw new ArgumentException("Notification declaration rejected: " + contract.ActionId);
        declarations.Add(contract.ActionId, contract);
    }

    /// <summary>Synchronously offer the declared event to its actual subject's response owner.</summary>
    /// <returns>True when the input was processed. Qualification, missing audio or a rejected output
    /// can still prevent playback; only a PlaybackReceipt describes actual dispatch.</returns>
    public static bool TryProcess(Pawn subject, NotificationContract contract)
    {
        if (subject == null) throw new ArgumentNullException(nameof(subject));
        if (contract == null || !declarations.TryGetValue(contract.ActionId, out var registered) || !ReferenceEquals(contract, registered)) return false;
        return SqueakerRuntime.TryProcessDeclaredNotification(subject, contract.ActionId);
    }
    /// <summary>Process an already declared complete ActionId, without guessing from a label or sound name.</summary>
    /// <returns>Input processed, not playback success. Unknown/official native IDs or no current owner return false.</returns>
    public static bool TryProcess(Pawn subject, string actionId)
    {
        if (subject == null) throw new ArgumentNullException(nameof(subject));
        if (!AuthorXml.ActionId(actionId)) return false;
        return SqueakerRuntime.TryProcessDeclaredNotification(subject, actionId);
    }
}
