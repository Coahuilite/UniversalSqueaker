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
#   4. RATCHET: the whitelist is 2 entries and may only shrink (HANDOFF §0/§1); a new exemption
#      needs a maintainer ruling. An entry that matches nothing today is reported as a NOTE so it can
#      be deleted on the next touch.
#
# Cross-repo: this is one half of a single metric. FerriteLib's containment gate (its HANDOFF item B)
# counts tree membership on the library side; the two whitelists must agree entry by entry. Since FL
# 0.3.0 landed P1/P2/P6, US holds ZERO raw hover calls and ZERO frame gates: every one of them reads
# `UiNative.IsMouseOver` / `UiNative.IsLayoutEvent` / `UiNative.Button`, and the chrome is the library's
# `UiWindowHost`. What remains here are the two exemptions HANDOFF §0 rules permanent-or-frozen.
# Exit code 0 = boundary intact. Run directly or from scripts/verify-local.ps1 (gate 14).
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The shared pattern set — deliberately identical to the FL containment gate's metric.
$BackendPattern = 'Mouse\.IsOver|Event\.current|\bGUI\.|GUIUtility|\bWidgets\.(Button|Label|BeginScrollView|EndScrollView|DrawBoxSolid|TextField)|Verse\.Widgets\.'

# Sanctioned exemptions (2, only-shrink). The reason is part of the contract: an entry with no
# ruling behind it is not an exemption. Both come from HANDOFF §0; neither is this gate's to reconsider.
# Anything the FL seams already cover must NOT be added back — the seam is the exemption.
$Whitelist = [ordered]@{
    'Diagnostics/SqueakDiagnosticsPanel.cs'                  = '豁免一 (HANDOFF §0): deliberate pure-immediate dev diagnostics panel, permanently retained; must stay usable with UiKit attached.'
    'Patches/Patch_GlobalControlsUtility_CameraIndicator.cs' = '豁免二 (HANDOFF §0): frozen experimental dual path - its pure-Verse legacy fallback may not gain any new UI content; whether that branch survives at all is a Knife 3 maintainer decision, never this gate decision.'
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

# One text -> every pattern hit in its code, as File/Line/Match/Code records.
function Find-BackendHit([string]$label, [string]$text) {
    $results = @()
    $lines = @((Remove-CSharpComments $text) -split "`r?`n")
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $code = $lines[$i]
        foreach ($m in [regex]::Matches($code, $BackendPattern)) {
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
Write-Host 'selftest: scanner armed (code hit found, comment prose ignored)'

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
