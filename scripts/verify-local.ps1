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
#   6   FerriteLib.UiKit tests, Release
#   7   FerriteLib.UiKit Dev build (TreatWarningsAsErrors)
#   8   FerriteLib.UiKit Release build (TreatWarningsAsErrors)
#   9   FerriteLib.UiKit neutrality grep (no US/SR product literals)
#  10   main assembly Dev build (US_DEV, TreatWarningsAsErrors)
#  11   main assembly Release build (TreatWarningsAsErrors)
#  12   built assembly presence (FerriteLib.UiKit.dll + UniversalSqueaker.dll)
#  13   Schema=2 manifests (settings page + camera overlay): present, well-formed, correctly attributed
#  14   UniversalSqueakerUiLogicTests Release (filters + attenuation math + layout math + mode-set drift guard + Schema2 source invariants + Keyed localization contract)
#  15   UniversalSqueakerKernelHostTests Release (real Schema2 Host + typed bindings + 5 workspaces x 3 viewports + narrow Mood geometry + text-fit audit against both language tables)
# -PackDev: after all checks pass, build the dev package (allows a dirty tree; auto -dirty label).
# US has no settings fixtures, voicepack authoring, or audio mirrors; those SR checks are not inherited.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$uikitProjectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$uikitTestsProject = Join-Path $root 'tools\FerriteLib.UiKit.Tests\FerriteLib.UiKit.Tests.csproj'
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

Invoke-Check 'FerriteLib.UiKit.Tests Release' `
    'dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release' `
    { dotnet run --no-restore --project $uikitTestsProject -c Release }

Invoke-Check 'FerriteLib.UiKit Dev build (warnings as errors)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev' `
    { dotnet build $uikitProjectFile -c Dev @buildExtraArgs }

Invoke-Check 'FerriteLib.UiKit Release build (warnings as errors)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    { dotnet build $uikitProjectFile -c Release @buildExtraArgs }

Invoke-Check 'FerriteLib.UiKit neutrality grep (no US/SR product literals)' `
    'dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release' `
    {
        $uikitSrc = Join-Path $root 'Source\FerriteLib.UiKit'
        $uikitTests = Join-Path $root 'tools\FerriteLib.UiKit.Tests'
        $pattern = 'UniversalSqueaker|SqueakyRatkin|Ratkin|Kiiro|SR_|US_'
        $hits = Get-ChildItem -LiteralPath $uikitSrc, $uikitTests -Recurse -File |
            Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.Name -match '\.(cs|csproj|xml|md|json|props|targets|sln|txt)$' } |
            Select-String -Pattern $pattern -CaseSensitive
        if ($hits) {
            $first = $hits | Select-Object -First 5 | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
            throw "Neutrality violation(s):`n$($first -join "`n")"
        }
    }

Invoke-Check 'main assembly Dev build (US_DEV, warnings as errors)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev' `
    { dotnet build $projectFile -c Dev @buildExtraArgs }

Invoke-Check 'main assembly Release build (warnings as errors)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    { dotnet build $projectFile -c Release @buildExtraArgs }

Invoke-Check 'built assemblies present (FerriteLib.UiKit.dll + UniversalSqueaker.dll)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    {
        $assembliesDir = Join-Path $root '1.6\Assemblies'
        if (-not (Test-Path -LiteralPath (Join-Path $assembliesDir 'FerriteLib.UiKit.dll') -PathType Leaf)) {
            throw "Missing built assembly: $assembliesDir\FerriteLib.UiKit.dll"
        }
        if (-not (Test-Path -LiteralPath (Join-Path $assembliesDir 'UniversalSqueaker.dll') -PathType Leaf)) {
            throw "Missing built assembly: $assembliesDir\UniversalSqueaker.dll"
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
