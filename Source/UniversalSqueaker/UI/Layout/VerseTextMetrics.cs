using UnityEngine;
using Verse;

namespace UniversalSqueaker.UI;

/// <summary>Production text metrics backed by Verse's IMGUI text engine.</summary>
public sealed class VerseTextMetrics : ITextMetrics
{
    public static readonly VerseTextMetrics Instance = new();

    public float CalcHeight(string text, float width)
    {
        return Text.CalcHeight(text ?? "", Mathf.Max(1f, width));
    }

    public float CalcWidth(string text)
    {
        return Text.CalcSize(text ?? "").x;
    }
}
