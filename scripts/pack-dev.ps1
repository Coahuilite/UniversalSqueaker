param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # A dev rehearsal is a folder you drop into Mods/, not an archive. FL→US round 2 S2: the one zip this
    # script used to produce was also the one a human was told to handle, and it was built from the
    # CONTENTS of the stage dir, so unzipping into Mods/ scattered a loose LoadFolders.xml. The engine can
    # still write a correctly shaped archive when someone asks for one; nothing on this path asks.
    [switch]$Zip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

$root = Resolve-NormalizedPath -Path $ProjectRoot
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$devDir = Join-Path $root 'dist\dev'
$stageDir = Join-Path $devDir 'UniversalSqueaker'

[xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Missing <Version> in project file: $projectFile"
}

# US version is the full release-axis label (during an rc window it is the frozen base, e.g. 0.2.0);
# About.xml must carry the same value exactly. One source for the number, so the folder and the DLL
# inside it cannot disagree.
$versionLabel = $versionNode.InnerText.Trim()

# Build the flavor this channel is named for, here, fresh. Two things are being forced, both measured
# on 2026-09-07 rather than assumed:
#
# 1. Truth. This folder's version.txt says build=dev, and US_DEV gates live code - Auto dev-logging is
#    on only under US_DEV, and the footer commit revision prints only under US_DEV. stage-package now
#    reads the configuration stamp out of the DLL instead of taking this script's word for it, so a
#    rehearsal can no longer be mislabeled by accident.
# 2. Freshness. Dev and Release share one OutputPath and up-to-dateness is judged per configuration, so
#    an incremental build can report itself current while the file at the payload path was written by the
#    other configuration - which is exactly how the old flow shipped `build=dev` over Release bytes.
#    --no-incremental removes the guess.
& dotnet build $projectFile -c Dev --no-incremental --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Dev build failed.' }

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
    CommitLabel  = "$shortCommit$dirtySuffix"
}
if ($Zip) { $stageArgs['CreateZip'] = $true }

& (Join-Path $PSScriptRoot 'stage-package.ps1') @stageArgs

# The old flow dropped an empty marker file beside the folder. It duplicated version.txt - which now
# carries flavor, commit and the carrier it linked - and invited anyone to read the identity off the
# marker instead of the package, so the leftovers are cleared.
Get-ChildItem -LiteralPath $devDir -File -Filter 'UniversalSqueaker-dev-v*.txt' -ErrorAction SilentlyContinue |
    Remove-Item -Force

$fileCount = (Get-ChildItem -LiteralPath $stageDir -Recurse -File | Measure-Object).Count
Write-Host "[pack-dev] dev rehearsal (folder, no archive) -> $stageDir ($fileCount files)"
