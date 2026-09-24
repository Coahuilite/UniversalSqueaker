param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$FerriteLibArtifactPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($FerriteLibArtifactPath)) {
    $FerriteLibArtifactPath = Join-Path (Split-Path -Parent $root) 'ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll'
}
$selected = [IO.Path]::GetFullPath($FerriteLibArtifactPath, $root)
if (-not (Test-Path -LiteralPath $selected -PathType Leaf)) {
    throw "Selected FerriteLib artifact is missing: $selected. Build/stage it in its own repository, then pass -FerriteLibArtifactPath."
}
return $selected
