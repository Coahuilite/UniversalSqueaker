param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$FerriteLibArtifactPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# A consumer build reads a selected carrier; it never rebuilds another repository.
$root = [IO.Path]::GetFullPath($ProjectRoot)
$carrier = & (Join-Path $PSScriptRoot 'resolve-carrier.ps1') -ProjectRoot $root -FerriteLibArtifactPath $FerriteLibArtifactPath
& (Join-Path $PSScriptRoot 'pack-dev.ps1') -ProjectRoot $root -FerriteLibArtifactPath $carrier
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }