using System;
using System.Threading;

namespace UniversalSqueaker;

/// <summary>
/// Fixed, settings-owned production gate. It deliberately has no resolver, catalog, pawn, or audio dependency.
/// Preview paths do not consult this policy.
/// </summary>
public sealed class SqueakGlobalActionPolicy
{
    private static SqueakGlobalActionPolicy current = CreateDefaults();
    private readonly SqueakActionScope[] scopes;

    private SqueakGlobalActionPolicy(SqueakActionScope[] scopes) => this.scopes = scopes;

    public static SqueakGlobalActionPolicy Current => Volatile.Read(ref current);

    public static void Publish(UniversalSqueakerSettings settings)
    {
        SqueakActionScope[] values = new SqueakActionScope[SqueakActionDefinitions.Count];
        for (int i = 0; i < values.Length; i++)
        {
            SqueakAction action = (SqueakAction)i;
            values[i] = settings.GetActionGlobalScope(action);
        }
        Volatile.Write(ref current, new SqueakGlobalActionPolicy(values));
    }

    public SqueakActionScope GetScope(SqueakAction action) => SqueakActionDefinitions.IsKnown(action)
        ? scopes[(int)action]
        : SqueakActionScope.Disabled;

    private static SqueakGlobalActionPolicy CreateDefaults()
    {
        SqueakActionScope[] values = new SqueakActionScope[SqueakActionDefinitions.Count];
        for (int i = 0; i < values.Length; i++) values[i] = SqueakActionDefinitions.Get((SqueakAction)i).DefaultScope;
        return new SqueakGlobalActionPolicy(values);
    }
}
