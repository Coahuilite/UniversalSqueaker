param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][string]$FerriteLibArtifactPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
$carrier = & (Join-Path $PSScriptRoot 'resolve-carrier.ps1') -ProjectRoot $root -FerriteLibArtifactPath $FerriteLibArtifactPath
$dist = [IO.Path]::GetFullPath((Join-Path $root 'dist'))
$fixture = Join-Path $dist ('.verify-artifact-' + [guid]::NewGuid().ToString('N'))
$stage = Join-Path $fixture 'UniversalSqueaker'
$null = New-Item -ItemType Directory -Path $fixture

function Read-TreeIdentity([string]$Directory) {
    return (@(Get-ChildItem -LiteralPath $Directory -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($Directory.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash
    }) -join "`n")
}
try {
    $carrierBefore = (Get-FileHash -LiteralPath $carrier).Hash + ':' + (Get-Item -LiteralPath $carrier).LastWriteTimeUtc.Ticks
    [xml]$project = Get-Content -LiteralPath (Join-Path $root 'Source/UniversalSqueaker/UniversalSqueaker.csproj') -Raw
    $version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $arguments = @{ ProjectRoot = $root; StageDir = $stage; BuildFlavor = 'dev'; VersionLabel = $version; CommitLabel = 'verification-fixture' }
    & (Join-Path $PSScriptRoot 'stage-package.ps1') @arguments -FerriteLibArtifactPath $carrier
    $built = Join-Path $root 'dist/build/Dev/UniversalSqueaker.dll'
    $packed = Join-Path $stage '1.6/Assemblies/UniversalSqueaker.dll'
    if ((Get-FileHash -LiteralPath $built).Hash -ne (Get-FileHash -LiteralPath $packed).Hash) {
        throw 'The staged DLL differs from Dev output (a legacy content DLL may have overwritten it).'
    }
    $before = Read-TreeIdentity $stage
    $mismatch = Join-Path $fixture 'FerriteLib.UiKit.dll'
    Copy-Item -LiteralPath $carrier -Destination $mismatch
    $stream = [IO.File]::Open($mismatch, [IO.FileMode]::Append)
    try { $stream.WriteByte(0) } finally { $stream.Dispose() }
    $refused = $false
    try { & (Join-Path $PSScriptRoot 'stage-package.ps1') @arguments -FerriteLibArtifactPath $mismatch }
    catch {
        if ($_.Exception.Message -notlike 'Selected carrier does not match the compiler reference:*') { throw }
        $refused = $true
    }
    if (-not $refused) { throw 'A different carrier was accepted by the stager.' }
    if ((Read-TreeIdentity $stage) -ne $before) { throw 'Rejected staging changed the previous package.' }
    $carrierAfter = (Get-FileHash -LiteralPath $carrier).Hash + ':' + (Get-Item -LiteralPath $carrier).LastWriteTimeUtc.Ticks
    if ($carrierBefore -ne $carrierAfter) { throw 'Consumer staging modified its selected carrier.' }
    Write-Host '[artifact-selection] matching input accepted; changed hash refused; previous stage and carrier unchanged.'
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    if (-not $resolvedFixture.StartsWith($dist + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Fixture cleanup escaped dist.'
    }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}
