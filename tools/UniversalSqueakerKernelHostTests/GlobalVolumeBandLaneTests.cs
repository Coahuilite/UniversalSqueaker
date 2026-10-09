using System;
using System.Collections.Generic;
using UnityEngine;

using FerriteLib.UiKit.Kernel;
using UniversalSqueaker.UI;

namespace UniversalSqueaker.KernelHostTests;

/// <summary>
/// U1 lane: the global-volume caption's band is MEASURED, and the row grows to hold it.
///
/// <para>
/// What it asserts, and why it is built the way it is. The defect was a written-down 18px band under a
/// Tiny caption, reported by the game as <c>ui.text.overflow tiny need=33 have=18 @ .../global-volume</c>.
/// A lane that draws the SHIPPED caption cannot see it: that string is short, so a one-line band is exactly
/// what it needs, and 18 would pass. So the lane stretches the caption to a sentence that cannot fit one
/// line at the viewport it draws, and asserts the two things a measured band produces there: the card GROWS,
/// and the fit audit reports nothing for the section. Reverting the measurement to the hard-coded 18 reddens
/// BOTH clauses - the card stops growing and the audit reports the overflow - which is the mutation this lane
/// exists for.
/// </para>
/// <para>
/// The viewport is 736, the innermost page width of the smallest window the shipped size policy can produce,
/// with the drawer retracted (the shipped default), so the shipped case here is the real minimum window and
/// not a synthetic one.
/// </para>
/// </summary>
internal static class GlobalVolumeBandLaneTests
{
    private const float PageWidth = 736f;
    private const float PageHeight = 720f;
    private const string SectionId = "global-volume";
    private const string CaptionKey = "US.Tuning.GlobalVolume";

    public static int RunAll()
    {
        Step("a longer volume caption grows the row instead of overflowing its band", TheCaptionBandIsMeasured);
        Console.WriteLine("GlobalVolumeBandLaneTests ALL PASS");
        return 0;
    }

    private static void TheCaptionBandIsMeasured()
    {
        var metrics = new Program.StubMetrics();
        var reports = new List<UiOverflowReport>();
        UiFitAudit.Attach(metrics, reports.Add);
        UiFitAudit.Enabled = true;
        try
        {
            Dictionary<string, string> english = Program.ReadKeyedTable("English");

            // Baseline: the SHIPPED caption. It fits one line, so the band sits on its floor, the row keeps
            // its shipped height and nothing overflows - which is also why a lane drawn only here would be
            // blind to the defect this lane is about.
            Program.SetTranslatorResolver(english);
            float baselineHeight;
            using (UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics))
            {
                host.Bindings.Invoke("set-tab", "Overview");
                UiFitAudit.Reset();
                reports.Clear();
                baselineHeight = MeasureCard(host);
                Assert(FindingsFor(reports, SectionId).Count == 0,
                    "the shipped caption must fit the measured band at the minimum real window: " + Describe(reports));
            }

            // Stretched: a sentence that cannot fit one line in this band at this width. A band that is still
            // written down as a constant cannot absorb it, so both clauses below fail under the mutation.
            string impossible = new string('测', 400);
            var stretched = new Dictionary<string, string>(english, StringComparer.Ordinal)
            {
                [CaptionKey] = impossible,
            };

            Program.SetTranslatorResolver(stretched);
            float stretchedHeight;
            using (UiHost host = UsKernelSettingsHost.Create(new RecordingSettingsSource { RichData = true }, metrics))
            {
                host.Bindings.Invoke("set-tab", "Overview");
                UiFitAudit.Reset();
                reports.Clear();
                stretchedHeight = MeasureCard(host);
                Assert(FindingsFor(reports, SectionId).Count == 0,
                    "a caption that needs more than one line must grow the measured band, never overflow it: "
                    + Describe(reports));
            }

            Assert(stretchedHeight > baselineHeight + 0.5f,
                "the global-volume card must GROW for a caption that cannot fit one line; if it does not, the"
                + " band is still a written-down height: baseline=" + baselineHeight + " stretched=" + stretchedHeight);
        }
        finally
        {
            UiFitAudit.Detach();
            UiFitAudit.Enabled = false;
            Program.SetTranslatorResolver(null);
        }
    }

    /// <summary>Arranges (twice, so the scroll write has nodes to target), draws, and returns the card rect.</summary>
    private static float MeasureCard(UiHost host)
    {
        host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        Program.SetScrollPositionById(host.Session, "content-scroll", Vector2.zero);
        UiLayoutSnapshot snapshot = host.MeasureAndArrange(new Vector2(PageWidth, PageHeight));
        host.DrawChecked(new Rect(0f, 0f, PageWidth, PageHeight));
        Assert(snapshot.RectById.TryGetValue(SectionId, out Rect card),
            "the arranged snapshot must carry the global-volume section");
        return card.height;
    }

    private static List<UiOverflowReport> FindingsFor(List<UiOverflowReport> reports, string id)
    {
        var found = new List<UiOverflowReport>();
        foreach (UiOverflowReport report in reports)
        {
            if (report.ElementPath.IndexOf(id, StringComparison.Ordinal) >= 0) found.Add(report);
        }

        return found;
    }

    private static string Describe(List<UiOverflowReport> reports)
    {
        var text = new System.Text.StringBuilder();
        text.Append(reports.Count).Append(" finding(s)");
        foreach (UiOverflowReport report in reports)
        {
            text.Append("; ").Append(report.ElementPath).Append(" ").Append(report.Axis)
                .Append(" needs ").Append(report.Needed).Append(" has ").Append(report.Available);
        }

        return text.ToString();
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("GlobalVolumeBandLaneTests step failed: " + name, ex);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
