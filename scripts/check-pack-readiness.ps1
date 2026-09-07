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

# The other half of the carrier boundary: US ships no FerriteLib payload, so it must name the carrier
# as a dependency or the mod fails to load with no explanation. RimWorld's modDependencies cannot
# carry a version (ModRequirement parses only packageId/alternativePackageIds/displayName), so the
# API-range assert is in code - UniversalSqueakerMod's constructor calls FerriteLibVersion.Require.
function Test-PackageIdList([string]$xpath) {
    $nodes = @($aboutXml.SelectNodes($xpath))
    foreach ($node in $nodes) {
        if ($node.InnerText.Trim() -eq 'coahuilite.ferritelib') { return $true }
    }
    return $false
}
Assert-Check 'About.xml declares coahuilite.ferritelib as a prerequisite' (Test-PackageIdList '/ModMetaData/modDependencies/li/packageId')
Assert-Check 'About.xml loads after coahuilite.ferritelib' (Test-PackageIdList '/ModMetaData/loadAfter/li')

Assert-Check 'LICENSE present at repo root and MPL-2.0' (
    (Test-Path -LiteralPath (Join-Path $root 'LICENSE') -PathType Leaf) -and
    ((Get-Content -LiteralPath (Join-Path $root 'LICENSE') -Raw) -match 'Mozilla Public License Version 2\.0'))
if ($RequireReleaseMetadata) {
    $descNode = $aboutXml.SelectSingleNode('/ModMetaData/description')
    $desc = if ($null -ne $descNode) { $descNode.InnerText.Trim() } else { '' }
    Assert-Check 'release description is not placeholder' ($desc -notmatch 'Placeholder|TODO') "($desc)"

    # Version-axis lock (the lib MEMORY 0.1.0/0.2.0 drift lesson; see MEMORY.md "First cloud
    # upload: durable decisions"). This pins the axes that are statically visible in THIS repo and
    # are release-specific. The csproj <Version> == About.xml <modVersion> agreement is already asserted above (section A); here we
    # add the two release-only facts: a release pack must not carry the -dev suffix, and Mod.cs must
    # declare the prerequisite range. The stronger invariant - that the range actually contains the
    # Api of the carrier DLL this build linked - is proven by the KernelHost harness gate
    # (PrerequisiteRangeTracksCompiledApi), which reflects the loaded assembly; reading the sibling
    # repo's source here would be both redundant and CI-fragile (the runner keeps the carrier source
    # under ci-ferritelib/, not ../ferritelib/Source/), so it is deliberately not done.
    Assert-Check 'release modVersion carries no -dev suffix' ($modVersion -notmatch '-dev') "($modVersion)"

    $modCs = Get-Content -LiteralPath (Join-Path $root 'Source\UniversalSqueaker\Mod.cs') -Raw
    Assert-Check 'Mod.cs declares PrerequisiteApiMin' ($modCs -match 'PrerequisiteApiMin\s*=\s*new\s+Version\(')
    Assert-Check 'Mod.cs declares PrerequisiteApiMax' ($modCs -match 'PrerequisiteApiMax\s*=\s*new\s+Version\(')
}

# C. Assemblies
Assert-Check 'UniversalSqueaker.dll exists' (Test-Path -LiteralPath (Join-Path $assembliesDir 'UniversalSqueaker.dll') -PathType Leaf)
# Inverted since FerriteLib became its own prerequisite mod: coahuilite.ferritelib is the single
# carrier, and a second copy would bind by load order through RimWorld's global AssemblyResolve with
# no other detector for whichever copy lost.
Assert-Check 'no FerriteLib.UiKit.dll in the US package (single carrier)' (-not (Test-Path -LiteralPath (Join-Path $assembliesDir 'FerriteLib.UiKit.dll') -PathType Leaf))

# Build debris does not enter a package by construction any more (stage-package excludes *.pdb and
# *.gitkeep on the copy and then asserts the closed set), so a pdb at the payload path is not a pre-pack
# blocker. What this script still checks is the repo, which no longer has a strip step to hide behind.

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
