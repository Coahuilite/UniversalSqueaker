using System;

// Menu registration metadata only. The real production callback/policy is linked;
// invoking its delegate is not a native game menu click.
namespace LudeonTK
{
    public enum DebugActionType { Action }
    public enum AllowedGameStates { PlayingOnMap }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DebugActionAttribute : Attribute
    {
        public string Category { get; }
        public string Name { get; }
        public DebugActionType actionType;
        public AllowedGameStates allowedGameStates;
        public DebugActionAttribute(string category, string name) { Category = category; Name = name; }
    }
}
