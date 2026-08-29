param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipVerify,
    [switch]$RequireReleaseMetadata
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$aboutFile = Join-Path $root 'About\About.xml'
$versionedDir = Join-Path $root '1.6'
$assembliesDir = Join-Path $versionedDir 'Assemblies'
$failures = @()

function Assert-Check {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    if ($Condition) {
        Write-Host "[ok] $Name"
    } else {
        Write-Host "[FAIL] $Name $Detail"
        $script:failures += $Name
    }
}

# A. Version and identity
$projectXml = [xml](Get-Content -LiteralPath $projectFile -Raw)
$versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
$version = if ($null -ne $versionNode) { $versionNode.InnerText.Trim() } else { '' }
Assert-Check 'csproj <Version> present' (-not [string]::IsNullOrWhiteSpace($version))

$aboutXml = [xml](Get-Content -LiteralPath $aboutFile -Raw)
$modVersionNode = $aboutXml.SelectSingleNode('/ModMetaData/modVersion')
$modVersion = if ($null -ne $modVersionNode) { $modVersionNode.InnerText.Trim() } else { '' }
Assert-Check 'About.xml <modVersion> present' (-not [string]::IsNullOrWhiteSpace($modVersion))
Assert-Check 'About.xml <modVersion> matches csproj <Version>' ($version -eq $modVersion) "($modVersion vs $version)"

$packageIdNode = $aboutXml.SelectSingleNode('/ModMetaData/packageId')
$packageId = if ($null -ne $packageIdNode) { $packageIdNode.InnerText.Trim() } else { '' }
Assert-Check 'packageId is coahuilite.universalsqueaker' ($packageId -eq 'coahuilite.universalsqueaker') "($packageId)"

if ($RequireReleaseMetadata) {
    $descNode = $aboutXml.SelectSingleNode('/ModMetaData/description')
    $desc = if ($null -ne $descNode) { $descNode.InnerText.Trim() } else { '' }
    Assert-Check 'release description is not placeholder' ($desc -notmatch 'Placeholder|TODO') "($desc)"
}

# C. Assemblies
Assert-Check 'UniversalSqueaker.dll exists' (Test-Path -LiteralPath (Join-Path $assembliesDir 'UniversalSqueaker.dll') -PathType Leaf)
Assert-Check 'FerriteLib.UiKit.dll exists' (Test-Path -LiteralPath (Join-Path $assembliesDir 'FerriteLib.UiKit.dll') -PathType Leaf)

# .pdb files are removed by stage-package.ps1, so they are not a pre-pack blocker.

# D. Content red lines
Assert-Check 'no Extras directory' (-not (Test-Path -LiteralPath (Join-Path $root 'Extras') -PathType Container))
Assert-Check 'no 1.6/Sounds directory' (-not (Test-Path -LiteralPath (Join-Path $versionedDir 'Sounds') -PathType Container))
Assert-Check 'no PublishedFileId.txt' (-not (Test-Path -LiteralPath (Join-Path $root 'About\PublishedFileId.txt') -PathType Leaf))

# E. Privacy / neutrality quick scans (text files only; binary DLLs are not scanned)
$privacyPattern = '([A-Za-z]:\\)|(\\\\)|(api[_-]?key)|(secret)|(password)|(PublishedFileId)|((^|[^A-Za-z])token([^A-Za-z]|$))'
$privacyHits = Get-ChildItem -LiteralPath (Join-Path $root 'About'), (Join-Path $root '1.6') -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '\.(xml|txt|json|yml|yaml)$' } |
    Select-String -Pattern $privacyPattern -CaseSensitive:$false
Assert-Check 'no obvious privacy/credential strings in staged roots' (@($privacyHits).Count -eq 0)

$squeakyHits = Get-ChildItem -LiteralPath (Join-Path $root 'Source') -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue |
    Select-String -Pattern 'SqueakyRatkin' -CaseSensitive
Assert-Check 'no SqueakyRatkin type references in Source' (@($squeakyHits).Count -eq 0)

# F. Optional full verify
if (-not $SkipVerify) {
    Write-Host '[run] verify-local.ps1 ...'
    & (Join-Path $PSScriptRoot 'verify-local.ps1') -ProjectRoot $root
    if ($LASTEXITCODE -ne 0) { $failures += 'verify-local.ps1' }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "[pack-readiness] FAIL: $($failures.Count) check(s) failed:"
    $failures | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host ''
Write-Host '[pack-readiness] all checks passed.'
