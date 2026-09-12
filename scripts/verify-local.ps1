param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$PackDev,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# One-input local verification for Universal Squeaker (US-adapted from the SR runbook process).
# Fail-fast order:
#   1   kernel gate: unit tests + US 0.1.0 golden corpus byte replay + determinism
#   2   config-copy lifecycle characterization
#   3   settings migration characterization (schema migration, write bridges, baseline importer)
#   4   log protocol characterization, Release
#   5   log protocol characterization, Dev (US_DEV)
#   6   FerriteLib carrier payload present and Release-configured (measured, not assumed)
#   7   main assembly Dev build (US_DEV, TreatWarningsAsErrors)
#   8   main assembly Release build (TreatWarningsAsErrors)
#   9   US payload is single-carrier (UniversalSqueaker.dll present, FerriteLib.UiKit.dll ABSENT)
#  10   LICENSE present and un-truncated MPL-2.0, identical to the carrier's copy
#  11   Schema=2 manifests (settings page + camera overlay): present, well-formed, correctly attributed
#  12   UniversalSqueakerUiLogicTests Release (filters + attenuation math + layout math + mode-set drift guard + Schema2 source invariants + Keyed localization contract)
#  13   UniversalSqueakerKernelHostTests Release (real Schema2 Host + typed bindings + 5 workspaces x 3 viewports + narrow Mood geometry + text-fit audit against both language tables)
#  14   UI boundary audit (scripts/ui-boundary-audit.ps1): raw renderer-backend calls only inside the
#       2-file exemption whitelist (豁免一 + 豁免二), and ZERO raw Mouse.IsOver since FL P1
#  15   harness stub coverage: the carrier's reference-driven scan (read-only) over the US payload AND
#       the KernelHostTests harness assembly, against US's own exemption ledger
#       (scripts/stub-coverage-exemptions.txt). Runs after 13 on purpose - gate 13 builds both scanned
#       assemblies and the carrier's stub surface in place.
# GATE PROVENANCE: 1-5 and 10-15 are US-owned; 6 and 9 assert the carrier boundary itself. 14 shares
# its metric with the FerriteLib containment gate (its HANDOFF item B): the two whitelists must agree
# entry by entry. 15 consumes the carrier's scanner (its gate 8) but the ledger and both scanned
# assemblies (the payload and the KernelHostTests harness) are US's; the scanner's own fixture controls
# stay in the carrier's gate 8, where the scanned tree is the carrier's. The FerriteLib library gates (its harness, Dev/Release builds, neutrality grep and
# the visual-core/page-model boundary) moved to that repository, which runs them from inside with a
# positive control.
# Gate numbers are append-only: "gate N" is cited across MEMORY/TODO/docs, so a new check joins as the
# next number (this one: 15) instead of shifting the checks below it.
# -PackDev: after all checks pass, build the dev package (allows a dirty tree; auto -dirty label).
# US has no settings fixtures, voicepack authoring, or audio mirrors; those SR checks are not inherited.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$carrierDll = Join-Path (Split-Path -Parent $root) 'ferritelib\1.6\Assemblies\FerriteLib.UiKit.dll'
$uiLogicTestsProject = Join-Path $root 'tools\UniversalSqueakerUiLogicTests\UniversalSqueakerUiLogicTests.csproj'
$kernelHostTestsProject = Join-Path $root 'tools\UniversalSqueakerKernelHostTests\UniversalSqueakerKernelHostTests.csproj'
$tempLog = Join-Path ([System.IO.Path]::GetTempPath()) ("us-verify-" + [guid]::NewGuid().ToString('N') + '.log')
$buildExtraArgs = @()
if ($NoRestore) { $buildExtraArgs += '--no-restore' }

function Invoke-Check {
    param([string]$Name, [string]$Retry, [scriptblock]$Action)

    Write-Host -NoNewline "[run] $Name ... "
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = 'Stop'
    $failed = $false
    $failureMessage = $null
    try { & $Action *> $tempLog } catch { $failed = $true; $failureMessage = $_.Exception.Message } finally { $ErrorActionPreference = $previousEap }
    $code = $LASTEXITCODE
    if ($failed -or $code -ne 0) {
        Write-Host 'FAIL'
        # A gate that enforces a contract without saying which one turns every red into a scavenger
        # hunt: an assertion message used to be swallowed by this catch and never reached the console
        # (port of the carrier's own 5399aa7).
        if (-not [string]::IsNullOrWhiteSpace($failureMessage)) { Write-Host "    $failureMessage" }
        if (Test-Path -LiteralPath $tempLog) {
            Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
        }
        Write-Host "  retry: $Retry"
        Remove-Item -LiteralPath $tempLog -Force -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host 'OK'
}

# A fresh clone or a git-archive extraction has no obj/ tree, and the six tool lanes below run with a
# hard-coded --no-restore on purpose (a lane must never silently re-resolve a stale package graph).
# Without this bootstrap a fresh tree dies inside gate 1 as MSB3644 ("reference assemblies for
# .NETFramework,Version=v4.7.2 not found"), which reads like a real red - the independent verifier hit
# exactly that on an archive extraction and had to restore seven projects by hand. Same shape as the
# carrier's own [setup] restore (d0632ca), sized for US: every project a lane invokes, not just one.
# -NoRestore keeps its documented meaning: the caller asserts the graph is already restored (CI
# restores every csproj before calling with it), so nothing happens here in that mode.
if (-not $NoRestore) {
    $setupProjects = @($projectFile)
    $setupProjects += @(Get-ChildItem -LiteralPath (Join-Path $root 'tools') -Recurse -File -Filter *.csproj |
        Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' } |
        ForEach-Object { $_.FullName } |
        Sort-Object)

    # Restore is what produces obj/project.assets.json; its presence is the same signal --no-restore
    # lanes depend on, so only the projects missing it are restored and a warm tree pays nothing.
    $setupPending = @($setupProjects | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $_) 'obj\project.assets.json') -PathType Leaf) })

    Write-Host -NoNewline "[setup] restore the project graph ($($setupPending.Count) of $($setupProjects.Count) projects need it) ... "
    $setupFailedProject = ''
    foreach ($setupProject in $setupPending) {
        dotnet restore $setupProject *> $tempLog
        if ($LASTEXITCODE -ne 0) { $setupFailedProject = $setupProject; break }
    }

    if ($setupFailedProject.Length -gt 0) {
        Write-Host 'FAIL'
        if (Test-Path -LiteralPath $tempLog) {
            Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
        }
        Write-Host "  retry: dotnet restore $setupFailedProject"
        Remove-Item -LiteralPath $tempLog -Force -ErrorAction SilentlyContinue
        exit 1
    }

    Write-Host 'OK'
}

Invoke-Check 'UniversalSqueakerKernelTests (unit asserts + US 0.1.0 corpus replay + determinism)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerKernelTests -c Release' `
    { dotnet run --no-restore --project (Join-Path $root 'tools\UniversalSqueakerKernelTests') -c Release }

Invoke-Check 'UniversalSqueakerConfigCopyTests (store lifecycle A-F)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerConfigCopyTests -c Release' `
    { dotnet run --no-restore --project (Join-Path $root 'tools\UniversalSqueakerConfigCopyTests') -c Release }

Invoke-Check 'UniversalSqueakerSettingsMigrationTests (schema migration + write bridges + baseline)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerSettingsMigrationTests -c Release' `
    { dotnet run --no-restore --project (Join-Path $root 'tools\UniversalSqueakerSettingsMigrationTests') -c Release }

Invoke-Check 'UniversalSqueakerLogTests Release (usdiag v1/v2 protocol)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerLogTests -c Release' `
    { dotnet run --no-restore --project (Join-Path $root 'tools\UniversalSqueakerLogTests') -c Release }

Invoke-Check 'UniversalSqueakerLogTests Dev (US_DEV)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerLogTests -c Dev' `
    { dotnet run --no-restore --project (Join-Path $root 'tools\UniversalSqueakerLogTests') -c Dev }

Invoke-Check 'FerriteLib carrier payload present and Release-configured (sibling repo built)' `
    'dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental' `
    {
        # US compiles against the carrier mod's payload and never ships one, so the sibling build is a
        # precondition, not a convenience. Say so plainly instead of failing inside csc.
        if (-not (Test-Path -LiteralPath $carrierDll -PathType Leaf)) {
            throw "Missing FerriteLib payload: $carrierDll. Build the ferritelib repo first (scripts/build-dev.ps1 does it in order)."
        }
        # Existence was the whole gate until FL→US round 2 S5, and existence is not enough: Dev and
        # Release share one carrier OutputPath, so the bytes at that path belong to whichever
        # configuration the sibling was last built as. US's Release gate must not silently link a
        # dev-configured carrier - today FER_DEV gates no library source, and "today" is not a contract.
        # The common cause is legitimate, not mysterious: FerriteLib's own pack-dev builds the carrier
        # -c Dev and leaves those bytes at this path (its dev channel is a Dev package by design). This
        # gate is about what US publishes against, so rebuilding it Release is the whole fix.
        $carrierStamp = (& (Join-Path $PSScriptRoot 'read-assembly-stamp.ps1') -Path $carrierDll) -join ''
        if ($carrierStamp -ne 'Release') {
            throw "The carrier payload at $carrierDll is '$carrierStamp'-configured; US builds and publishes against a Release carrier (a Dev one usually means ../ferritelib's own pack-dev ran last). Rebuild: dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental"
        }

        # Presence and configuration are not identity, and identity is what this gate was missing.
        # Measured 2026-09-12: a payload built from 7402f12 stayed green here while the carrier checkout
        # was at 572c40b, so US compiled against an older carrier API and the only symptom appeared one
        # gate later as CS1503 in UsKernelSettingsHost. The carrier itself drew the same lesson in
        # d0632ca, where an existence-only payload check became an evaluated TargetPath. The payload
        # embeds its source commit in AssemblyInformationalVersion (carrier AGENTS.md), so attribute the
        # bytes to a checkout. US never builds the carrier from here - that would write another
        # repository - it only refuses to accept bytes nobody can attribute.
        $carrierRoot = Join-Path (Split-Path -Parent $root) 'ferritelib'
        $carrierRepo = $null
        foreach ($candidate in @($carrierRoot, (Join-Path $root 'ci-ferritelib'))) {
            if (Test-Path -LiteralPath (Join-Path $candidate '.git') -PathType Container) { $carrierRepo = $candidate; break }
        }
        if ($null -eq $carrierRepo) {
            throw "Cannot attribute the carrier payload: no carrier git checkout at $carrierRoot (nor at the CI layout $(Join-Path $root 'ci-ferritelib')), so its source commit cannot be proven."
        }

        $carrierInfo = (& (Join-Path $PSScriptRoot 'read-assembly-stamp.ps1') -Path $carrierDll -AttributeName 'AssemblyInformationalVersionAttribute') -join ''
        $plus = $carrierInfo.IndexOf('+')
        $payloadCommit = if ($plus -ge 0) { $carrierInfo.Substring($plus + 1).Trim() } else { '' }
        if ([string]::IsNullOrWhiteSpace($payloadCommit)) {
            throw "The carrier payload at $carrierDll reports AssemblyInformationalVersion '$carrierInfo', which carries no '+' commit suffix: the bytes cannot be attributed to a source commit. Rebuild: dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental"
        }

        $carrierHead = ((& git -C $carrierRepo rev-parse HEAD) -join '').Trim()
        if ([string]::IsNullOrWhiteSpace($carrierHead)) {
            throw "Cannot read HEAD of the carrier checkout at $carrierRepo; the payload's source commit cannot be proven."
        }

        # A dirty carrier tree means the payload may contain source that is in no commit, so its SHA
        # would be a half-truth: refuse instead of accepting an unattributable payload.
        $carrierChanges = @(& git -C $carrierRepo status --porcelain)
        if ($carrierChanges.Count -gt 0) {
            throw "The carrier checkout at $carrierRepo has uncommitted changes ($($carrierChanges.Count) path(s)), so the payload cannot be proven to come from $carrierHead. Commit or stash the carrier, rebuild it, then re-run."
        }

        if (-not [string]::Equals($payloadCommit, $carrierHead, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "The carrier payload at $carrierDll was built from commit $payloadCommit but the carrier checkout at $carrierRepo is at ${carrierHead}: the payload is a STALE build and US would compile against an older carrier API. Rebuild: dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental"
        }
    }

Invoke-Check 'main assembly Dev build (US_DEV, warnings as errors)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev' `
    { dotnet build $projectFile -c Dev @buildExtraArgs }

Invoke-Check 'main assembly Release build (warnings as errors)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    { dotnet build $projectFile -c Release @buildExtraArgs }

Invoke-Check 'US payload carries exactly one assembly (no second FerriteLib copy)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    {
        $assembliesDir = Join-Path $root '1.6\Assemblies'
        if (-not (Test-Path -LiteralPath (Join-Path $assembliesDir 'UniversalSqueaker.dll') -PathType Leaf)) {
            throw "Missing built assembly: $assembliesDir\UniversalSqueaker.dll"
        }
        # The inverted guard. Two mods shipping FerriteLib.UiKit.dll bind by load order through
        # RimWorld's single global AssemblyResolve, and the copy that loses never finds out - so a
        # stray DLL sitting in this folder is a defect with no other detector.
        $stray = Join-Path $assembliesDir 'FerriteLib.UiKit.dll'
        if (Test-Path -LiteralPath $stray -PathType Leaf) {
            throw "US must not ship the FerriteLib payload (coahuilite.ferritelib is the only carrier): $stray"
        }
    }

Invoke-Check 'LICENSE present and un-truncated MPL-2.0' `
    'manually' `
    {
        # MPL-2.0 is chosen for the whole series, and section 3.2 attaches an obligation to shipping a
        # DLL: recipients must be told how to get source. stage-package.ps1 copies this file into the
        # package, so the gate proves the file is real before anything is staged from it.
        $licensePath = Join-Path $root 'LICENSE'
        if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
            throw "US has no LICENSE file, yet stage-package.ps1 copies one into every package."
        }
        $text = Get-Content -LiteralPath $licensePath -Raw
        if ($text -notmatch 'Mozilla Public License Version 2\.0') { throw 'LICENSE is not the MPL-2.0 text.' }
        if ($text -notmatch 'Exhibit B') { throw 'LICENSE is truncated: Exhibit B is missing.' }
        if ($text -notmatch '10\.4\. Distributing Source Code Form') { throw 'LICENSE is truncated: section 10.4 is missing.' }
        # Scoped to the header: the reproduced licence body always contains Exhibit B's sample notice,
        # so testing the whole file would flag every correct copy.
        $separator = $text.IndexOf('-----')
        $header = if ($separator -gt 0) { $text.Substring(0, $separator) } else { $text }
        if ($header -match 'Incompatible With Secondary Licenses., as defined') {
            throw 'The applied notice declares incompatibility with secondary licenses; series policy keeps that allowed.'
        }
        # The carrier ships the same licence text; a fork between the two copies is a real defect.
        $carrierLicense = Join-Path (Split-Path -Parent $root) 'ferritelib\LICENSE'
        if (Test-Path -LiteralPath $carrierLicense -PathType Leaf) {
            $ours = (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash
            $theirs = (Get-FileHash -LiteralPath $carrierLicense -Algorithm SHA256).Hash
            if ($ours -ne $theirs) { throw "US and FerriteLib LICENSE files differ; the series licence must be one text." }
        }
    }

Invoke-Check 'Schema=2 manifests present, well-formed and correctly attributed' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    {
        # Schema=2 is the only shipped manifest schema since the legacy page chain was removed; a
        # manifest that parses but claims another schema or source would silently load the wrong page.
        $manifests = @(
            (Join-Path $root 'Source\UniversalSqueaker\UI\Layout.Schema2.xml'),
            (Join-Path $root 'Source\UniversalSqueaker\UI\Layout.Overlay.Schema2.xml')
        )
        foreach ($manifestPath in $manifests) {
            if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
                throw "Missing Schema=2 manifest: $manifestPath"
            }
            $doc = [xml](Get-Content -LiteralPath $manifestPath -Raw)
            if ($doc.DocumentElement.Name -ne 'UiPage') {
                throw "Manifest root must be UiPage: $manifestPath"
            }
            if ($doc.DocumentElement.Schema -ne '2') {
                throw "Manifest must declare Schema=2: $manifestPath"
            }
            if ($doc.DocumentElement.Source -ne 'coahuilite.universalsqueaker') {
                throw "Manifest Source must be the US package id: $manifestPath"
            }
            if (@($doc.SelectNodes('//Widget')).Count -eq 0) {
                throw "Manifest declares no widgets: $manifestPath"
            }
        }
    }

# The removed legacy page chain (FerriteVoicePacksPage / VanillaVoicePacksPage) is gone with its
# fallback role, so there is no second implementation left to characterize or to keep in sync.

Invoke-Check 'UniversalSqueakerUiLogicTests Release (filters + attenuation math + layout math + mode-set drift guard + Schema2 invariants + Keyed localization contract)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerUiLogicTests -c Release' `
    { dotnet run --no-restore --project $uiLogicTestsProject -c Release }

# Real embedded Schema=2 Host creation regression: the production Host adapter runs against the
# real resource, real US widget registrations and the real typed binding table (recording source).
Invoke-Check 'UniversalSqueakerKernelHostTests Release (real Schema2 Host + typed bindings + 5 workspaces x 3 viewports + narrow Mood geometry + text-fit audit)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerKernelHostTests -c Release' `
    { dotnet run --no-restore --project $kernelHostTestsProject -c Release }

Invoke-Check 'UI boundary audit (renderer-backend containment + only-shrink whitelist)' `
    'pwsh -NoProfile -File scripts/ui-boundary-audit.ps1' `
    { & (Join-Path $PSScriptRoot 'ui-boundary-audit.ps1') -ProjectRoot $root }

# The harness compiles against the game's reference assemblies and executes on the carrier's stubs, so a
# member only the reference declares compiles green and dies at run time INSIDE THE HARNESS: the session
# guard swaps the element for its recovery band, the frame survives, and a lane that asserts no more than
# a live session stays green while the path under test never ran (measured 2026-09-12: Verse.GenUI.
# ContractedBy, then Mathf.Clamp(int, int, int) - the trip guard found both, no assertion did). The
# carrier's scan is reference-driven: every member a scanned assembly takes from a stub-replaced game
# assembly must be declared by the stubs or be named in this repository's own ledger with a reason.
#
# Two targets, ONE scan: the payload is the product surface, the harness is the lane surface - a lane's own
# helper can take a member the payload never touches, and gate 13 then fails INSIDE the lane (a JIT failure,
# not an assertion) while nothing names the member. They are scanned together, not in two passes, because
# the exemption ledger is a union: one pass per assembly makes every entry the other assembly needs look
# STALE (measured 2026-09-12 - the harness half alone stranded 165 entries). The scanner reports each
# target's own reference/declared counts, so findings stay attributable. It must be invoked through
# -Command with an array: after -File, a second value following -Assembly is dropped by the child pwsh's
# parameter binding (measured 2026-09-12), which silently scans one assembly and looks clean. This gate
# sits after 13 because gate 13 builds both of the assemblies it scans.
Invoke-Check 'harness stub coverage (every game member the US payload or the harness references resolves on the carrier stubs, or is exempted with a reason)' `
    'pwsh -NoProfile -Command "& ''../ferritelib/scripts/stub-coverage-scan.ps1'' -Path . -Assembly @(''1.6/Assemblies/UniversalSqueaker.dll'',''tools/UniversalSqueakerKernelHostTests/bin/Release/net472/UniversalSqueakerKernelHostTests.exe'') -StubsDir ../ferritelib/tools/FerriteLib.UiKit.Tests/bin/stubs -Exemptions scripts/stub-coverage-exemptions.txt"' `
    {
        $stubCoverageScan = Join-Path (Split-Path -Parent $root) 'ferritelib\scripts\stub-coverage-scan.ps1'
        if (-not (Test-Path -LiteralPath $stubCoverageScan -PathType Leaf)) {
            throw "The carrier's stub-coverage scanner is missing at $stubCoverageScan. It ships with the sibling ferritelib checkout (scripts/stub-coverage-scan.ps1); a scan that cannot run is not a clean result."
        }

        $stubDir = Join-Path (Split-Path -Parent $root) 'ferritelib\tools\FerriteLib.UiKit.Tests\bin\stubs'
        $stubExemptions = Join-Path $root 'scripts\stub-coverage-exemptions.txt'
        $stubTargets = @(
            (Join-Path $root '1.6\Assemblies\UniversalSqueaker.dll'),
            (Join-Path $root 'tools\UniversalSqueakerKernelHostTests\bin\Release\net472\UniversalSqueakerKernelHostTests.exe')
        )

        # Built through -Command and an array so the scanner receives both paths as separate values; every
        # path is single-quote escaped so a path containing an apostrophe cannot break out of the string.
        $stubQuote = { param($value) $value.Replace("'", "''") }
        $stubCommand = "& '" + (& $stubQuote $stubCoverageScan) + "' -Path '" + (& $stubQuote $root) +
            "' -Assembly @('" + (& $stubQuote $stubTargets[0]) + "','" + (& $stubQuote $stubTargets[1]) +
            "') -StubsDir '" + (& $stubQuote $stubDir) + "' -Exemptions '" + (& $stubQuote $stubExemptions) + "'"

        # An assembly that was never built is passed through rather than skipped: the scan reads it as its own
        # NOT SCANNED (exit 3), and an unscanned half is not a clean half.
        $stubOutput = @(& pwsh -NoProfile -Command $stubCommand *>&1)
        $stubCode = $LASTEXITCODE
        foreach ($stubLine in $stubOutput) { Write-Host $stubLine }

        if ($stubCode -ne 0) {
            # The scan prints one line per finding, but a gate's log is shown as a tail and a sorted
            # MISSING list can leave the new reference far from it - so the findings are repeated here,
            # where they cannot be trimmed away.
            $stubFindings = @($stubOutput | Where-Object { $_ -match 'MISSING |STALE |NO-REASON |NOT SCANNED' } | Select-Object -First 10)
            throw ("stub-coverage-scan.ps1 exited $stubCode (0 = clean, 2 = unresolved/stale/unreasoned finding, 3 = not scanned; " + $stubFindings.Count + " finding line(s) quoted). Findings: " + ($stubFindings -join ' ; ') + " -- declare the member in the carrier's stubs, or add it to scripts/stub-coverage-exemptions.txt with a reason code from that file's legend; a STALE entry must be deleted instead.")
        }
    }

Write-Host '[verify] all checks passed.'

if ($PackDev) {
    Write-Host '[pack] dev package'
    & (Join-Path $PSScriptRoot 'build-dev.ps1') -ProjectRoot $root
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
