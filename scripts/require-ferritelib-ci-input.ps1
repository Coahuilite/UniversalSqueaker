param(
    [string]$Repository = $env:FL_CARRIER_REPOSITORY,
    [string]$Tag = $env:FL_CARRIER_TAG,
    [string]$Commit = $env:FL_CARRIER_COMMIT
)
$ErrorActionPreference = 'Stop'
# The maintainer supplies real published coordinates. Empty input must fail before checkout can
# interpret an empty ref as a moving default branch; the former 0.7 carrier is not a fallback.
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or
    $Tag -notmatch '^v0\.8\.(0|[1-9][0-9]*)(-rc[1-9][0-9]*)?$' -or
    $Commit -notmatch '^[0-9a-f]{40}$') {
    throw 'FerriteLib CI input pending: set approved FERRITELIB_CARRIER_REPOSITORY, FERRITELIB_CARRIER_TAG (0.8), and FERRITELIB_CARRIER_COMMIT repository variables. The release asset must be Release-configured, digest-verified and paired with that exact source. No 0.7/default-branch fallback is allowed.'
}
if ($Tag -ceq 'v0.8.0-rc1') {
    throw 'US0.6 rc1 preparation requires the newly approved published FerriteLib input after rc2 exists. The old0.8.0-rc1 coordinates are retained remote state, not a fallback. Maintainer must set the real repository/tag/full main commit and verify the actual asset; do not insert a local candidate SHA. Local preparation uses an explicit immutable ArtifactPath instead.'
}
Write-Host "FerriteLib CI selection: $Repository $Tag $Commit; Release asset pairing is verified by acquire-ferritelib-carrier.ps1."
