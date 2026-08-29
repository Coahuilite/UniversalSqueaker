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
#  13   UI layout manifest XML well-formedness
#  14   UniversalSqueakerUiLogicTests Release (pure UI filters + distance preview)
# -PackDev: after all checks pass, build the dev package (allows a dirty tree; auto -dirty label).
# US has no settings fixtures, voicepack authoring, or audio mirrors; those SR checks are not inherited.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$uikitProjectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$uikitTestsProject = Join-Path $root 'tools\FerriteLib.UiKit.Tests\FerriteLib.UiKit.Tests.csproj'
$uiLogicTestsProject = Join-Path $root 'tools\UniversalSqueakerUiLogicTests\UniversalSqueakerUiLogicTests.csproj'
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
    'dotnet run --project tools/UniversalSqueakerKernelTests -c Release' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerKernelTests') -c Release }

Invoke-Check 'UniversalSqueakerConfigCopyTests (store lifecycle A-F)' `
    'dotnet run --project tools/UniversalSqueakerConfigCopyTests -c Release' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerConfigCopyTests') -c Release }

Invoke-Check 'UniversalSqueakerSettingsMigrationTests (schema migration + write bridges + baseline)' `
    'dotnet run --project tools/UniversalSqueakerSettingsMigrationTests -c Release' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerSettingsMigrationTests') -c Release }

Invoke-Check 'UniversalSqueakerLogTests Release (usdiag v1/v2 protocol)' `
    'dotnet run --project tools/UniversalSqueakerLogTests -c Release' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerLogTests') -c Release }

Invoke-Check 'UniversalSqueakerLogTests Dev (US_DEV)' `
    'dotnet run --project tools/UniversalSqueakerLogTests -c Dev' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerLogTests') -c Dev }

Invoke-Check 'FerriteLib.UiKit.Tests Release' `
    'dotnet run --project tools/FerriteLib.UiKit.Tests -c Release' `
    { dotnet run --project $uikitTestsProject -c Release }

Invoke-Check 'FerriteLib.UiKit Dev build (warnings as errors)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev' `
    { dotnet build $uikitProjectFile -c Dev @buildExtraArgs }

Invoke-Check 'FerriteLib.UiKit Release build (warnings as errors)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    { dotnet build $uikitProjectFile -c Release @buildExtraArgs }

Invoke-Check 'FerriteLib.UiKit neutrality grep (no US/SR product literals)' `
    'dotnet run --project tools/FerriteLib.UiKit.Tests -c Release' `
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

Invoke-Check 'UI layout manifest XML well-formedness' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    {
        $layoutPath = Join-Path $root 'Source\UniversalSqueaker\UI\Layout.xml'
        if (-not (Test-Path -LiteralPath $layoutPath -PathType Leaf)) {
            throw "Missing UI layout manifest: $layoutPath"
        }
        $null = [xml](Get-Content -LiteralPath $layoutPath -Raw)
    }

Invoke-Check 'UniversalSqueakerUiLogicTests Release (pure UI filters + distance preview)' `
    'dotnet run --project tools/UniversalSqueakerUiLogicTests -c Release' `
    { dotnet run --project $uiLogicTestsProject -c Release }

Write-Host '[verify] all checks passed.'

if ($PackDev) {
    Write-Host '[pack] dev package'
    & (Join-Path $PSScriptRoot 'build-dev.ps1') -ProjectRoot $root
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
