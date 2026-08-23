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
#   3   log protocol characterization, Release
#   4   log protocol characterization, Dev (US_DEV)
#   5   main assembly Dev build (US_DEV, TreatWarningsAsErrors)
#   6   main assembly Release build (TreatWarningsAsErrors)
# -PackDev: after all checks pass, build the dev package (allows a dirty tree; auto -dirty label).
# US has no settings fixtures, voicepack authoring, or audio mirrors; those SR checks are not inherited.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$tempLog = Join-Path ([System.IO.Path]::GetTempPath()) ("us-verify-" + [guid]::NewGuid().ToString('N') + '.log')
$buildExtraArgs = @()
if ($NoRestore) { $buildExtraArgs += '--no-restore' }

function Invoke-Check {
    param([string]$Name, [string]$Retry, [scriptblock]$Action)

    Write-Host -NoNewline "[run] $Name ... "
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Action *> $tempLog } finally { $ErrorActionPreference = $previousEap }
    $code = $LASTEXITCODE
    if ($code -ne 0) {
        Write-Host 'FAIL'
        Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
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

Invoke-Check 'UniversalSqueakerLogTests Release (usdiag v1/v2 protocol)' `
    'dotnet run --project tools/UniversalSqueakerLogTests -c Release' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerLogTests') -c Release }

Invoke-Check 'UniversalSqueakerLogTests Dev (US_DEV)' `
    'dotnet run --project tools/UniversalSqueakerLogTests -c Dev' `
    { dotnet run --project (Join-Path $root 'tools\UniversalSqueakerLogTests') -c Dev }

Invoke-Check 'main assembly Dev build (US_DEV, warnings as errors)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev' `
    { dotnet build $projectFile -c Dev @buildExtraArgs }

Invoke-Check 'main assembly Release build (warnings as errors)' `
    'dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release' `
    { dotnet build $projectFile -c Release @buildExtraArgs }

Write-Host '[verify] all checks passed.'

if ($PackDev) {
    Write-Host '[pack] dev package'
    & (Join-Path $PSScriptRoot 'build-dev.ps1') -ProjectRoot $root
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
