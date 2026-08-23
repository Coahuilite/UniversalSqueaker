using System;
using System.Collections.Generic;

namespace Verse;

/// <summary>Characterization stub: captures SqueakLog's only Verse touchpoint, preserving emission order.</summary>
public static class Log
{
    public readonly struct Entry
    {
        public readonly string Level;
        public readonly string Text;
        public Entry(string level, string text) { Level = level; Text = text; }
    }

    public static readonly List<Entry> Captured = new();

    public static void Message(string text) => Captured.Add(new Entry("info", text));
    public static void Warning(string text) => Captured.Add(new Entry("warning", text));
    public static void Error(string text) => Captured.Add(new Entry("error", text));

    public static void Reset() => Captured.Clear();
}
