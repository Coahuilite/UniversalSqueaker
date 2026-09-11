# UI boundary audit gate — the renderer-backend containment check (HANDOFF.md §1 item 5).
#
# "Renderer backend" here means Unity IMGUI / Verse Widgets (HANDOFF §0 term ruling): `GUI`,
# `Event.current`, `Mouse.IsOver`, `Widgets.*`, `GUIUtility`. US draws through the FerriteLib UiKit
# seams, so a raw backend call outside a sanctioned file is a boundary breach that no other gate can
# see — it compiles, it runs, and it silently re-couples the page to the backend.
#
# What it does:
#   1. SELF-TEST (always, first): runs the scanner over synthetic sources containing one real backend
#      call plus a comment decoy carrying the same tokens, and one clean control. The gate refuses to
#      report green while blind, so a broken scanner or a broken comment stripper fails here instead
#      of quietly passing the tree.
#   2. SCAN: every `Source/UniversalSqueaker/**/*.cs` file, code only — line and block comments are
#      stripped first, because prose about the backend is documentation, not coupling.
#   3. VERDICT: a hit outside the whitelist is RED. So is a whitelist entry whose file has vanished
#      (a dead exemption path is a lost boundary, not a free pass).
#   4. RATCHET: the whitelist may only shrink (HANDOFF §0/§1); a new exemption needs a maintainer ruling.
#      It holds 3 entries since 2026-09-12: the frozen camera-indicator branch, plus the in-world pawn
#      marker ratified that day (F-19b). The second one is a RATIFIED ADDITION, not a broken ratchet -
#      before it, GenMapUI was not in the pattern set, so the marker was neither exempt nor counted: the
#      gate looked clean while one boundary sat outside its view. The third entry is the same story on the
#      CONTRACT axis rather than the renderer axis: `UiNative.Button(` was missing from the pattern set, so
#      the context-free overload (which FL's api-tiers.md forbids for any caller holding a context) was
#      neither counted nor exempt - the context fix of 2026-09-12 could not be seen by any gate until the
#      term below was added (verify's finding: reverting the fix kept all 14 gates green).
#      Ratified additions get date + reason + recovery condition in the entry itself.
#      An entry that matches nothing today is reported as a NOTE so it can be deleted on the next touch.
#
# Cross-repo: this is one half of a single metric. FerriteLib's containment gate (its HANDOFF item B)
# counts tree membership on the library side. What the two halves SHARE is the pattern set (the metric);
# each whitelist is that side's own ratified product, NOT a shared list. Since FL
# 0.3.0 landed P1/P2/P6, US holds ZERO raw hover calls and ZERO frame gates: every one of them reads
#  `UiNative.IsMouseOver` / `UiNative.IsLayoutEvent` / `UiNative.Button`, and the chrome is the library's
#  `UiWindowHost`. Since the diagnostics panel moved onto the same machinery (round-9 migration, 2026-09-10)
# the whitelist holds two exemptions: the frozen camera-indicator branch and the in-world pawn marker
# (2026-09-12). Cross-repo consequence of the second one: FL's pattern set must gain `\bGenMapUI\.` so the
# shared metric stays one metric - but FL needs NO whitelist entry for it. FL draws nothing in world space
# (a map layer is a permanent non-goal there), so the term is expected to match ZERO times in its tree; an
# entry on that side would be a free pass for a boundary it does not draw. Expected shape: FL 0 entries, US 2.
# Exit code 0 = boundary intact. Run directly or from scripts/verify-local.ps1 (gate 14).
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The shared pattern set — the metric both halves run: identical to the FL containment gate's set plus ONE
# ratified addition, `\bGenMapUI\.` (maintainer ruling 2026-09-12, F-19b: the in-world pawn marker stays,
# so it must be COUNTED and then exempted, not invisible). FL's set needs the same term to stay one metric;
# that edit belongs to the FL session. Only the SET is shared — the whitelists are not.
# `UiNative.Button(` is a CONTRACT term, not a renderer token: it anchors both overloads and Find-BackendHit
# keeps only the context-free call (FL api-tiers.md: a caller holding a context must use the protected
# overload, which yields to a covering popup). The renderer terms are shared with FL's gate; this term is
# US-side today and FL's own chrome button is its documented exception.
$BackendPattern = 'Mouse\.IsOver|Event\.current|\bGUI\.|GUIUtility|\bWidgets\.(Button|Label|BeginScrollView|EndScrollView|DrawBoxSolid|TextField)|Verse\.Widgets\.|\bGenMapUI\.|UiNative\.Button\('

# Sanctioned exemptions (3, only-shrink). The reason is part of the contract: an entry with no
# ruling behind it is not an exemption. The first comes from HANDOFF §0, the second and third from the
# maintainer session of 2026-09-12 (F-19b and task-60); none is this gate's to reconsider.
# Anything the FL seams already cover must NOT be added back — the seam is the exemption.
$Whitelist = [ordered]@{
    # 豁免二 (frozen, HANDOFF §0): camera-indicator legacy fallback branch - Knife 3 owns its fate.
    'Patches/Patch_GlobalControlsUtility_CameraIndicator.cs' = 'frozen Verse Widgets.Label legacy branch (HANDOFF §0 exemption two)'

    # 裁定增补 2026-09-12 (F-19b): the in-world pawn marker. `SqueakDiagnosticsOverlay` paints the mark
    # over a spawned pawn through `Verse.GenMapUI.DrawText` (CompSqueaker.PostDrawCore, 5 calls) - a
    # world-space draw with no UiKit session seam to route through. The maintainer ruled the marker
    # STAYS as a fixed working part, so it is exempted rather than deleted;
    # RECYCLE when US stops drawing in-world markers, or when the carrier grows a world-space layer
    # with a session (then this must route through that seam like every other draw).
    'CompSqueaker.cs' = 'in-world pawn marker via GenMapUI.DrawText - maintainer ruling 2026-09-12 (F-19b); recycle when US draws no in-world marker, or the carrier gains a session-bearing world layer'

    # 裁定增补 2026-09-12 (task-60): the contract-free `UiNative.Button` overload, one call site.
    # `Mod.cs:187` is the legacy settings-window opener: it draws OUTSIDE the kernel session, so no
    # `UiWidgetContext` exists to pass and the hit stack cannot be consulted. FL documents the same
    # exception for its own window chrome. Every in-tree control uses the protected overload, and the
    # gate reports this file only while that stays true - the term was invisible before task-60.
    # RECYCLE when the opener moves into the kernel tree (it then has a context and must use
    # Button(rect, ctx)), or when the shell retires.
    'Mod.cs' = 'context-free UiNative.Button at Mod.cs:187 (settings opener, outside the kernel session) - maintainer session 2026-09-12 (task-60); recycle when the opener moves into the kernel tree or the shell retires'
}

# ---- comment stripping -------------------------------------------------------
# Literals are tracked because the repo does keep `//` inside string content (a path regex in
# Logging/SqueakLogProtocol.cs), and a naive "cut at the first //" would silently narrow what the
# gate can see on such a line. Comparison operands are strings, never chars, so the empty lookahead
# at end-of-file cannot throw under Set-StrictMode.
function Remove-CSharpComments([string]$text) {
    $sb = New-Object System.Text.StringBuilder
    $i = 0
    $n = $text.Length
    $state = 'code'
    while ($i -lt $n) {
        $c = [string]$text[$i]
        $d = if ($i + 1 -lt $n) { [string]$text[$i + 1] } else { [string]::Empty }
        $advance = 1
        switch ($state) {
            'code' {
                if ($c -eq '/' -and $d -eq '/') { $state = 'line'; $advance = 2 }
                elseif ($c -eq '/' -and $d -eq '*') { $state = 'block'; $advance = 2 }
                elseif ($c -eq '@' -and $d -eq '"') { [void]$sb.Append(' '); $state = 'verbatim'; $advance = 2 }
                elseif ($c -eq '"') { [void]$sb.Append($c); $state = 'string' }
                elseif ($c -eq "'") { [void]$sb.Append($c); $state = 'char' }
                else { [void]$sb.Append($c) }
            }
            'line' {
                if ($c -eq "`r" -or $c -eq "`n") { [void]$sb.Append($c); $state = 'code' }
            }
            'block' {
                if ($c -eq '*' -and $d -eq '/') { [void]$sb.Append('  '); $state = 'code'; $advance = 2 }
                elseif ($c -eq "`r" -or $c -eq "`n") { [void]$sb.Append($c) }
            }
            'string' {
                if ($c -eq '\') { [void]$sb.Append('  '); $advance = 2 }
                elseif ($c -eq '"') { [void]$sb.Append($c); $state = 'code' }
                elseif ($c -eq "`r" -or $c -eq "`n") { [void]$sb.Append($c); $state = 'code' }
                else { [void]$sb.Append($c) }
            }
            'verbatim' {
                if ($c -eq '"' -and $d -eq '"') { [void]$sb.Append('  '); $advance = 2 }
                elseif ($c -eq '"') { [void]$sb.Append($c); $state = 'code' }
                else { [void]$sb.Append($c) }
            }
            'char' {
                if ($c -eq '\') { [void]$sb.Append('  '); $advance = 2 }
                elseif ($c -eq "'") { [void]$sb.Append($c); $state = 'code' }
                elseif ($c -eq "`r" -or $c -eq "`n") { [void]$sb.Append($c); $state = 'code' }
                else { [void]$sb.Append($c) }
            }
        }
        $i += $advance
    }
    return $sb.ToString()
}

# True when the call whose argument list starts right after $start (depth is already 1) carries a
# top-level comma, i.e. the caller passed a context. Walks with paren-depth counting so a nested call as
# the sole argument is still context-free; a top-level comma inside a string literal would fool it, and
# no such call exists in this tree (documented boundary, not an oversight).
function Test-UiNativeButtonHasContext([string]$code, [int]$start) {
    $depth = 1
    for ($i = $start; $i -lt $code.Length; $i++) {
        $c = $code[$i]
        if ($c -eq '(') { $depth++ }
        elseif ($c -eq ')') { $depth--; if ($depth -eq 0) { return $false } }
        elseif ($c -eq ',' -and $depth -eq 1) { return $true }
    }
    return $false
}

# One text -> every pattern hit in its code, as File/Line/Match/Code records.
function Find-BackendHit([string]$label, [string]$text) {
    $results = @()
    $lines = @((Remove-CSharpComments $text) -split "`r?`n")
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $code = $lines[$i]
        foreach ($m in [regex]::Matches($code, $BackendPattern)) {
            # `UiNative.Button(` anchors BOTH overloads; only the context-free one is a violation, so the
            # refinement lives here rather than in the pattern - a regular expression cannot count parens,
            # and a naive comma test on the raw text would also reject a nested single-argument call.
            if ($m.Value -eq 'UiNative.Button(' -and (Test-UiNativeButtonHasContext $code ($m.Index + $m.Length))) { continue }
            $results += [pscustomobject]@{
                File  = $label
                Line  = $i + 1
                Match = $m.Value
                Code  = $code.Trim()
            }
        }
    }
    return $results
}

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$sourceRoot = Join-Path $root 'Source\UniversalSqueaker'
$failures = @()

function Add-Failure([string]$detail) { $script:failures += $detail }

# ---- 1. self-test: the gate must be able to go red ---------------------------
$probeOffending = @'
using UnityEngine;
using Verse;
public class Offending {
    // A comment that says Widgets.ButtonInvisible(rect) and Mouse.IsOver(rect) is still a comment.
    void Draw(Rect r) { if (Widgets.ButtonInvisible(r)) { Close(); } }
}
'@
$probeClean = @'
using UnityEngine;
public class Clean {
    // GUI.Label is avoided here because it does not clip; see the boundary gate.
    void Draw(Rect r) { UiThemeDraw.Surface(r, theme, fill, border); }
}
'@
$offendingHits = @(Find-BackendHit 'selftest/Offending.cs' $probeOffending)
$cleanHits = @(Find-BackendHit 'selftest/Clean.cs' $probeClean)
if ($offendingHits.Count -ne 1) {
    Write-Host 'UI BOUNDARY AUDIT FAILED: self-test - the scanner did not find exactly one backend call in the control sample.'
    Write-Host "  found $($offendingHits.Count) hit(s); a gate that cannot go red cannot be trusted."
    exit 1
}
if ($offendingHits[0].Code -notmatch 'ButtonInvisible') {
    Write-Host 'UI BOUNDARY AUDIT FAILED: self-test - the hit it reports is the comment decoy, not the code line.'
    exit 1
}
if ($cleanHits.Count -ne 0) {
    Write-Host 'UI BOUNDARY AUDIT FAILED: self-test - the scanner flagged clean code or comment prose.'
    foreach ($h in $cleanHits) { Write-Host "  $($h.File):$($h.Line) $($h.Match) <- $($h.Code)" }
    exit 1
}
$probeOverloads = @'
using Verse;
public class Overloads {
    void DrawRaw(Rect r) { if (UiNative.Button(r)) { Close(); } }
    void DrawWithContext(Rect r, UiWidgetContext ctx) { if (UiNative.Button(r, ctx)) { Close(); } }
    void DrawNested(Rect r) { if (UiNative.Button(new Rect(0f, 0f, r.width, r.height))) { Close(); } }
}
'@
$overloadHits = @(Find-BackendHit 'selftest/Overloads.cs' $probeOverloads)
if ($overloadHits.Count -ne 2) {
    Write-Host "UI BOUNDARY AUDIT FAILED: self-test - the context-free UiNative.Button overload must be found exactly twice (raw + nested single argument), found $($overloadHits.Count)."
    foreach ($h in $overloadHits) { Write-Host "  $($h.File):$($h.Line) $($h.Match)" }
    exit 1
}
if ($overloadHits[0].Code -notmatch 'DrawRaw' -or $overloadHits[1].Code -notmatch 'DrawNested') {
    Write-Host 'UI BOUNDARY AUDIT FAILED: self-test - the overload hits must be the raw call and the nested single-argument call; the context-carrying overload must NOT be reported.'
    exit 1
}
Write-Host 'selftest: scanner armed (code hit found, comment prose ignored); overload discrimination armed (raw + nested found, Button(rect, ctx) ignored)'

# ---- 2. scan the tree -------------------------------------------------------
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    Write-Host "UI BOUNDARY AUDIT FAILED: missing source root $sourceRoot"
    exit 1
}
# Build output is not source: obj/bin hold generated AssemblyInfo copies, and a gate that scans them
# can be tripped by a stale artifact from a deleted file.
$files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } |
    Sort-Object FullName)
$hitsByFile = @{}
$hoverCalls = 0
foreach ($f in $files) {
    $rel = ($f.FullName.Substring($sourceRoot.Length) -replace '^[\\/]', '') -replace '\\', '/'
    $hits = @(Find-BackendHit $rel (Get-Content -LiteralPath $f.FullName -Raw))
    if ($hits.Count -eq 0) { continue }
    $hitsByFile[$rel] = $hits
    $hoverCalls += @($hits | Where-Object { $_.Match -eq 'Mouse.IsOver' }).Count
}

foreach ($rel in @($hitsByFile.Keys | Where-Object { -not $Whitelist.Contains($_) } | Sort-Object)) {
    $detail = (@($hitsByFile[$rel] | Group-Object Line | ForEach-Object {
        $hitMatches = @($_.Group | ForEach-Object { $_.Match }) -join ', '
        "      $($_.Name): $($_.Group[0].Code)   [$hitMatches]"
    }) -join "`n")
    Add-Failure "raw renderer-backend call outside the whitelist: $rel`n$detail"
}

# Zero is the FL 0.3.0 contract (P1): hover reads UiNative.IsMouseOver, which honours the harness
# mouse-position seam that a raw Verse call cannot. The number the FL containment gate counts on the
# other half of this metric.
if ($hoverCalls -ne 0) {
    Add-Failure "expected 0 raw Mouse.IsOver in the tree since FL P1 (read UiNative.IsMouseOver instead), found $hoverCalls"
}
foreach ($rel in $Whitelist.Keys) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $rel) -PathType Leaf)) {
        Add-Failure "whitelist entry points at a missing file: $rel - delete the entry or restore the file"
    }
}

# ---- 3. verdict -------------------------------------------------------------
Write-Host "scan: $($files.Count) files; $($hitsByFile.Count) file(s) hold backend calls; raw Mouse.IsOver = $hoverCalls; whitelist $($Whitelist.Count) entries (only-shrink)"
foreach ($rel in @($Whitelist.Keys | Where-Object { -not $hitsByFile.Contains($_) } | Sort-Object)) {
    Write-Host "  NOTE    $rel matches nothing today - the exemption can be deleted (ratchet: only-shrink)"
}
foreach ($rel in @($hitsByFile.Keys | Where-Object { $Whitelist.Contains($_) } | Sort-Object)) {
    Write-Host "  exempt  $rel  x$(@($hitsByFile[$rel]).Count)"
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'UI BOUNDARY AUDIT FAILED:'
    foreach ($f in $failures) { Write-Host "- $f" }
    Write-Host ''
    Write-Host '  Fix by routing through the UiKit seam. Adding a whitelist entry requires a maintainer ruling.'
    exit 1
}
Write-Host 'UI BOUNDARY AUDIT CLEAN'
exit 0
