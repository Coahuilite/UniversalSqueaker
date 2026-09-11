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
# GATE PROVENANCE: 1-5 and 10-14 are US-owned; 6 and 9 assert the carrier boundary itself. 14 shares
# its metric with the FerriteLib containment gate (its HANDOFF item B): the two whitelists must agree
# entry by entry. The FerriteLib library gates (its harness, Dev/Release builds, neutrality grep and
# the visual-core/page-model boundary) moved to that repository, which runs them from inside with a
# positive control.
# Gate numbers are append-only: "gate N" is cited across MEMORY/TODO/docs, so a new check joins as 14
# instead of shifting the thirteen below it.
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

Write-Host '[verify] all checks passed.'

if ($PackDev) {
    Write-Host '[pack] dev package'
    & (Join-Path $PSScriptRoot 'build-dev.ps1') -ProjectRoot $root
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
