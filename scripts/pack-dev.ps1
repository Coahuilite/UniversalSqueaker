param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

$root = Resolve-NormalizedPath -Path $ProjectRoot
$devDir = Join-Path $root "dist\dev"
$stageDir = Join-Path $root "dist\dev\UniversalSqueaker"
$projectFile = Join-Path $root "Source\UniversalSqueaker\UniversalSqueaker.csproj"

[xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
$versionNode = $projectXml.SelectSingleNode("/Project/PropertyGroup/Version")
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "Missing <Version> in project file: $projectFile"
}

$version = $versionNode.InnerText.Trim()

# US version is the full label (0.1.0-dev); About.xml must carry the same value exactly.
$versionLabel = $version

try {
    $null = Get-Command -Name git -CommandType Application -ErrorAction Stop
}
catch {
    throw "Git is required to create the dev package label, but was not found on PATH."
}

$shortCommitOutput = @(& git -C $root rev-parse --short HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $shortCommitOutput.Count -ne 1) {
    throw "Failed to determine the current Git commit for dev package labeling."
}

$shortCommit = ([string]$shortCommitOutput[0]).Trim()
if ([string]::IsNullOrWhiteSpace($shortCommit)) {
    throw "Git returned an empty commit for dev package labeling."
}

$statusOutput = @(& git -C $root status --porcelain --untracked-files=normal 2>$null)
if ($LASTEXITCODE -ne 0) {
    throw "Failed to determine whether the Git working tree is dirty for dev package labeling."
}

$dirtySuffix = if ($statusOutput.Count -gt 0) { "-dirty" } else { "" }

if (Test-Path -LiteralPath $devDir -PathType Container) {
    Get-ChildItem -LiteralPath $devDir -File -Filter "UniversalSqueaker-dev-v*.txt" | Remove-Item -Force
}
& (Join-Path $PSScriptRoot "stage-package.ps1") -ProjectRoot $root -StageDir $stageDir -VersionLabel $versionLabel -BuildFlavor dev -CommitLabel "$shortCommit$dirtySuffix" -CreateZip

$fileCount = (Get-ChildItem -LiteralPath $stageDir -Recurse -File | Measure-Object).Count
Write-Host "[pack-dev] Staged $fileCount files to $stageDir"

$labelPath = Join-Path $devDir "UniversalSqueaker-dev-v$versionLabel-$shortCommit$dirtySuffix.txt"
$null = New-Item -ItemType File -Path $labelPath -Force
Write-Host "[pack-dev] Created dev package label $labelPath"
