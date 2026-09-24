param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$FerriteLibArtifactPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($FerriteLibArtifactPath)) {
    # NO default carrier, on purpose (maintainer ruling 2026-09-24: local testing uses Dev packages only).
    # The old default resolved the repository-root Release carrier - the WRONG artifact for a Dev rehearsal,
    # and one that ordinary FL builds no longer refresh - so a silent selection was worse than a refusal.
    $message = @'
No FerriteLib carrier selected. Pass -FerriteLibArtifactPath <path to FerriteLib.UiKit.dll>.
  There is deliberately no default: the old one resolved the repository-root Release carrier, which is the
  wrong artifact for a Dev rehearsal and is no longer refreshed by ordinary FL builds.
  Select the DLL from the paired FL dev package (staged by the carrier owner) or from an explicit FL build:
      pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath <carrier> -DevelopmentCarrier
      pwsh -NoProfile -File scripts/build-dev.ps1 -FerriteLibArtifactPath <carrier>
  Selecting a carrier is read-only: this repository never rebuilds the FerriteLib payload.
'@
    throw $message
}
$selected = [IO.Path]::GetFullPath($FerriteLibArtifactPath, $root)
if (-not (Test-Path -LiteralPath $selected -PathType Leaf)) {
    throw "Selected FerriteLib artifact is missing: $selected. The FerriteLib payload is staged by its own owner (the carrier repository); select the DLL from the paired package or from an explicit FL build output."
}
return $selected
