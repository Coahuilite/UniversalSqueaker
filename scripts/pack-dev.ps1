param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # A dev rehearsal is a folder you drop into Mods/, not an archive. FL→US round 2 S2: the one zip this
    # script used to produce was also the one a human was told to handle, and it was built from the
    # CONTENTS of the stage dir, so unzipping into Mods/ scattered a loose LoadFolders.xml. The engine can
    # still write a correctly shaped archive when someone asks for one; nothing on this path asks.
    [string]$FerriteLibArtifactPath,
    [switch]$Zip,
    # Optional fixed delivery; omitted preserves the existing dist/dev and build slots.
    [string]$OutputRoot,
    [string]$FerriteLib17ArtifactPath,
    [string]$RimWorld17Dir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

$root = Resolve-NormalizedPath -Path $ProjectRoot
$carrierDll = & (Join-Path $PSScriptRoot 'resolve-carrier.ps1') -ProjectRoot $root -FerriteLibArtifactPath $FerriteLibArtifactPath
$carrier17 = if ($FerriteLib17ArtifactPath) {
    & (Join-Path $PSScriptRoot 'resolve-carrier.ps1') -ProjectRoot $root -FerriteLibArtifactPath $FerriteLib17ArtifactPath
} else { $null }
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$devDir = if ($OutputRoot) { [IO.Path]::GetFullPath($OutputRoot, $root) } else { Join-Path $root 'dist/dev' }
$stageDir = Join-Path $devDir 'UniversalSqueaker'
$buildArguments = @()
if ($OutputRoot) {
    $distPrefix = [IO.Path]::GetFullPath((Join-Path $root 'dist')) + [IO.Path]::DirectorySeparatorChar
    if (-not $devDir.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputRoot must be under this repository dist.' }
    foreach ($output in @($stageDir, (Join-Path $devDir 'build'), (Join-Path $devDir 'obj'), (Join-Path $devDir '.staging-UniversalSqueaker'))) {
        if (Test-Path -LiteralPath $output) { throw "Choose a fresh OutputRoot; output already exists: $output" }
    }
    $buildArguments = @("-p:BaseIntermediateOutputPath=$devDir/obj/", "-p:OutputPath=$devDir/build/")
}

[xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Missing <Version> in project file: $projectFile"
}

# US version is the full release-axis label (the base the csproj currently carries);
# About.xml must carry the same value exactly. One source for the number, so the folder and the DLL
# inside it cannot disagree.
$versionLabel = $versionNode.InnerText.Trim()

# Build Dev into its own directory, against precisely the selected read-only carrier.
& dotnet build $projectFile -c Dev --no-incremental "-p:FerriteLibArtifactPath=$carrierDll" @buildArguments --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Dev build failed.' }
$payload17 = $null
if ($carrier17) {
    $arguments17 = @()
    if ($OutputRoot) { $arguments17 = @("-p:BaseIntermediateOutputPath=$devDir/obj17/", "-p:OutputPath=$devDir/build/RimWorld17/") }
    & dotnet build $projectFile -c Dev --no-incremental "-p:FerriteLibArtifactPath=$carrier17" -p:UsRimWorldTarget=RimWorld17 "-p:RimWorld17Dir=$RimWorld17Dir" @arguments17 --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Native1.7 Dev build failed.' }
    $payload17 = if ($OutputRoot) { Join-Path $devDir 'build/RimWorld17/UniversalSqueaker.dll' }
        else { Join-Path $root 'dist/build/Dev/RimWorld17/UniversalSqueaker.dll' }
}

try {
    $null = Get-Command -Name git -CommandType Application -ErrorAction Stop
}
catch {
    throw 'Git is required to label the dev package, but was not found on PATH.'
}

$shortCommitOutput = @(& git -C $root rev-parse --short HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $shortCommitOutput.Count -ne 1) {
    throw 'Failed to determine the current Git commit for dev package labeling.'
}

$shortCommit = ([string]$shortCommitOutput[0]).Trim()
if ([string]::IsNullOrWhiteSpace($shortCommit)) {
    throw 'Git returned an empty commit for dev package labeling.'
}

# A dirty rehearsal is still labelable, but it has to say so: this is the only place a dev folder can
# tell you it does not correspond to a commit anybody else can check out.
$statusOutput = @(& git -C $root status --porcelain --untracked-files=normal 2>$null)
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to determine whether the Git working tree is dirty for dev package labeling.'
}

$dirtySuffix = if ($statusOutput.Count -gt 0) { '-dirty' } else { '' }

# Named splatting, never positional: with @array the first element binds as the first PARAMETER, which is
# how a stage directory once ended up inside -BuildFlavor and ValidateSet did the shouting.
$stageArgs = @{
    ProjectRoot  = $root
    StageDir     = $stageDir
    VersionLabel = $versionLabel
    BuildFlavor  = 'dev'
    FerriteLibArtifactPath = $carrierDll
    CommitLabel  = "$shortCommit$dirtySuffix"
}
if ($carrier17) { $stageArgs.FerriteLib17ArtifactPath = $carrier17; $stageArgs.RimWorld17PayloadDllPath = $payload17 }
if ($Zip) { $stageArgs['CreateZip'] = $true }
if ($OutputRoot) { $stageArgs.PayloadDllPath = Join-Path $devDir 'build/UniversalSqueaker.dll' }

& (Join-Path $PSScriptRoot 'stage-package.ps1') @stageArgs

# The old flow dropped an empty marker file beside the folder. It duplicated version.txt - which now
# carries flavor, commit and the carrier it linked - and invited anyone to read the identity off the
# marker instead of the package, so the leftovers are cleared.
Get-ChildItem -LiteralPath $devDir -File -Filter 'UniversalSqueaker-dev-v*.txt' -ErrorAction SilentlyContinue |
    Remove-Item -Force

$fileCount = (Get-ChildItem -LiteralPath $stageDir -Recurse -File | Measure-Object).Count
Write-Host "[pack-dev] dev rehearsal (folder, no archive) -> $stageDir ($fileCount files)"
