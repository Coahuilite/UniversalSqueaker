param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$FerriteLibArtifactPath,
    [string]$FerriteLib17ArtifactPath,
    [string]$RimWorld17Dir,
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# pack-dev owns selection for this invocation; it validates before any build or write.
$root = [IO.Path]::GetFullPath($ProjectRoot)
& (Join-Path $PSScriptRoot 'pack-dev.ps1') -ProjectRoot $root -FerriteLibArtifactPath $FerriteLibArtifactPath -OutputRoot $OutputRoot -FerriteLib17ArtifactPath $FerriteLib17ArtifactPath -RimWorld17Dir $RimWorld17Dir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
