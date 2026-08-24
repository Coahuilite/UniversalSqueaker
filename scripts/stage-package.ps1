param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][string]$StageDir,
    [string]$VersionLabel,
    [string]$BuildFlavor = 'unknown',
    [string]$CommitLabel = 'unknown',
    [switch]$CreateZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-NormalizedPath([string]$Path) { [System.IO.Path]::GetFullPath($Path) }

$root = Resolve-NormalizedPath $ProjectRoot
$stageDir = Resolve-NormalizedPath $StageDir
$aboutSource = Join-Path $root 'About'
$loadFoldersSource = Join-Path $root 'LoadFolders.xml'
$versionedSource = Join-Path $root '1.6'
$assemblyPath = Join-Path $versionedSource 'Assemblies\UniversalSqueaker.dll'
$uikitAssemblyPath = Join-Path $versionedSource 'Assemblies\FerriteLib.UiKit.dll'

# US red lines: no Extras content packs and no built-in audio mirror may ever enter a package.
if (Test-Path -LiteralPath (Join-Path $root 'Extras') -PathType Container) { throw "US must not ship Extras content packs: $root\Extras" }
if (Test-Path -LiteralPath (Join-Path $versionedSource 'Sounds') -PathType Container) { throw "US must not ship built-in audio: $versionedSource\Sounds" }

# Version discipline (all channels): the staged About.xml must carry exactly the csproj <Version> label.
# US records the full prerelease string in About (0.1.0-dev), so the comparison is exact, not base-only.
if (-not [string]::IsNullOrWhiteSpace($VersionLabel)) {
    [xml]$aboutXml = Get-Content -LiteralPath (Join-Path $aboutSource 'About.xml') -Raw
    $modVersionNode = $aboutXml.SelectSingleNode('/ModMetaData/modVersion')
    if ($null -eq $modVersionNode -or [string]::IsNullOrWhiteSpace($modVersionNode.InnerText)) {
        throw "About.xml is missing <modVersion>; product version source must stay in sync."
    }
    if ($modVersionNode.InnerText.Trim() -ne $VersionLabel.Trim()) {
        throw "About.xml <modVersion> ($($modVersionNode.InnerText.Trim())) does not match package label ($VersionLabel). Update About.xml or the csproj <Version>."
    }
}
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw "Missing built assembly: $assemblyPath. Build the desired flavor before staging." }
if (-not (Test-Path -LiteralPath $uikitAssemblyPath -PathType Leaf)) { throw "Missing built assembly: $uikitAssemblyPath. Build FerriteLib.UiKit before staging." }

if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path $stageDir -Force
Copy-Item -LiteralPath $aboutSource -Destination (Join-Path $stageDir 'About') -Recurse -Force
Copy-Item -LiteralPath $loadFoldersSource -Destination (Join-Path $stageDir 'LoadFolders.xml') -Force
Copy-Item -LiteralPath $versionedSource -Destination (Join-Path $stageDir '1.6') -Recurse -Force

$publishedFileId = Join-Path $stageDir 'About\PublishedFileId.txt'
if (Test-Path -LiteralPath $publishedFileId) { Remove-Item -LiteralPath $publishedFileId -Force }
Get-ChildItem -LiteralPath $stageDir -Recurse -File -Filter *.pdb | Remove-Item -Force
Get-ChildItem -LiteralPath $stageDir -Recurse -File -Filter *.gitkeep | Remove-Item -Force
# Navigation docs are repository tooling, not distribution content (kept as a defensive filter).
Get-ChildItem -LiteralPath $stageDir -Recurse -File -Filter 'codemap.md' | Remove-Item -Force
# Package identity label: lets anyone verify how fresh a distributed package is without
# inspecting the DLL. Written after all exclusion steps so it is never filtered out.
if (-not [string]::IsNullOrWhiteSpace($VersionLabel)) {
    $labelContent = "UniversalSqueaker $VersionLabel`r`nbuild=$BuildFlavor`r`ncommit=$CommitLabel`r`n"
    [System.IO.File]::WriteAllText((Join-Path $stageDir 'version.txt'), $labelContent)
}
$fileCount = (Get-ChildItem -LiteralPath $stageDir -Recurse -File | Measure-Object).Count
Write-Host "[stage-package] Staged $fileCount files to $stageDir; no Extras/audio mirrors present."

if ($CreateZip) {
    $zipPath = Join-Path (Split-Path -Parent $stageDir) "UniversalSqueaker-$BuildFlavor-v$VersionLabel-$CommitLabel.zip"
    if (Test-Path -LiteralPath $zipPath -PathType Leaf) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zipPath
    Write-Host "[stage-package] Created zip $zipPath"
}
