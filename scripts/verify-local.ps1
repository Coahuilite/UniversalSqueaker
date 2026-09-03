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
#   6   FerriteLib carrier payload present (the sibling repo has been built; US compiles against it)
#   7   main assembly Dev build (US_DEV, TreatWarningsAsErrors)
#   8   main assembly Release build (TreatWarningsAsErrors)
#   9   US payload is single-carrier (UniversalSqueaker.dll present, FerriteLib.UiKit.dll ABSENT)
#  10   LICENSE present and un-truncated MPL-2.0, identical to the carrier's copy
#  11   Schema=2 manifests (settings page + camera overlay): present, well-formed, correctly attributed
#  12   UniversalSqueakerUiLogicTests Release (filters + attenuation math + layout math + mode-set drift guard + Schema2 source invariants + Keyed localization contract)
#  13   UniversalSqueakerKernelHostTests Release (real Schema2 Host + typed bindings + 5 workspaces x 3 viewports + narrow Mood geometry + text-fit audit against both language tables)
# GATE PROVENANCE: 1-5 and 10-13 are US-owned; 6 and 9 assert the carrier boundary itself. The
# FerriteLib library gates (its harness, Dev/Release builds, neutrality grep and the
# visual-core/page-model boundary) moved to that repository, which runs them from inside with a
# positive control.
# The library gates (harness, Dev/Release build, neutrality grep, visual-core boundary) are no longer
# run from here; they live in the carrier repository. Two gates here guard the carrier boundary itself,
# and one asserts the series licence is present and un-truncated before a package can be staged.
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
    try { & $Action *> $tempLog } catch { $failed = $true } finally { $ErrorActionPreference = $previousEap }
    $code = $LASTEXITCODE
    if ($failed -or $code -ne 0) {
        Write-Host 'FAIL'
        if (Test-Path -LiteralPath $tempLog) {
            Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
        }
        Write-Host "  retry: $Retry"
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

Invoke-Check 'FerriteLib carrier payload present (sibling repo built)' `
    'dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    {
        # US compiles against the carrier mod's payload and never ships one, so the sibling build is a
        # precondition, not a convenience. Say so plainly instead of failing inside csc.
        if (-not (Test-Path -LiteralPath $carrierDll -PathType Leaf)) {
            throw "Missing FerriteLib payload: $carrierDll. Build the ferritelib repo first (scripts/build-dev.ps1 does it in order)."
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

Write-Host '[verify] all checks passed.'

if ($PackDev) {
    Write-Host '[pack] dev package'
    & (Join-Path $PSScriptRoot 'build-dev.ps1') -ProjectRoot $root
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
