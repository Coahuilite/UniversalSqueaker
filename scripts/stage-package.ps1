# The one staging engine behind every US channel (dev rehearsal, GitHub release asset). Thin packers
# own flavor identity; everything that decides what a package IS lives here.
#
# Order is load-bearing: prove the inputs before wiping anything, prove the result after the last write.
# An assertion that runs against a tree that is still about to be rewritten proves nothing about what
# ships.
#
# Why the flavor is measured rather than accepted (FL→US round 2, S1): Dev and Release share one
# OutputPath (UniversalSqueaker.csproj) and up-to-dateness is judged per configuration, so the bytes at
# the payload path can belong to whichever configuration was built last - and `verify-local` ends on
# Release. Before this engine read the stamp, `pack-dev` could stage those Release bytes under a
# `build=dev` label. In US that is not cosmetic: US_DEV gates live code (`SqueakLog.Configure` turns
# Auto dev-logging on only under US_DEV, and Mod.cs prints the commit revision in the footer only under
# US_DEV), so a mislabeled rehearsal loses exactly the diagnostics the rehearsal exists to read, with
# nothing on disk saying so. Measured here on 2026-09-07: `verify-local` followed by the old `pack-dev`
# produced `build=dev` over a payload reporting `AssemblyConfiguration=Release`.
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][string]$StageDir,
    [Parameter(Mandatory = $true)][string]$VersionLabel,
    [ValidateSet('dev', 'release')][string]$BuildFlavor = 'dev',
    [string]$CommitLabel = 'unknown',
    # The release channel publishes to strangers: its payload must not carry a dev suffix, its base
    # version must equal the label on the box, and it must have linked a Release carrier. The dev
    # rehearsal skips this - by definition it is an unnamed build.
    [switch]$RequireReleaseIdentity,
    # Only the release channel archives. A dev rehearsal is a folder, and FL→US round 2 S2 measured that
    # the dev archive was also shaped wrong; the shape is the engine's problem now (see the writer), so
    # the switch exists but no dev path asks for it.
    [switch]$CreateZip,
    # Archive file name without extension. The release asset name is a reconciliation target (exactly
    # `UniversalSqueaker-<tag>.zip`), so the caller that knows the tag says the name. The default keeps
    # the historical label form.
    [string]$ArchiveName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-NormalizedPath([string]$Path) { [System.IO.Path]::GetFullPath($Path) }

$root = Resolve-NormalizedPath $ProjectRoot
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$payloadDll = Join-Path $root '1.6\Assemblies\UniversalSqueaker.dll'
$carrierDll = Join-Path (Split-Path -Parent $root) 'ferritelib\1.6\Assemblies\FerriteLib.UiKit.dll'
$aboutXml = Join-Path $root 'About\About.xml'
$loadFolders = Join-Path $root 'LoadFolders.xml'
$license = Join-Path $root 'LICENSE'
$contentRoot = Join-Path $root '1.6'
$stageDir = Resolve-NormalizedPath $StageDir

# What a staged US package always contains, and what it must never contain anywhere. Named rather than
# filtered: an empty filtered enumeration lets a check pass vacuously, which is the bug class both
# repositories have been burned by.
$stableFiles = @(
    'About/About.xml',
    'LoadFolders.xml',
    'LICENSE',
    'version.txt',
    '1.6/Assemblies/UniversalSqueaker.dll'
)
$forbiddenNames = @('*.pdb', '*.gitkeep', 'codemap.md', 'PublishedFileId.txt')
$forbiddenDirs = @('Extras', '1.6/Sounds')
# Build debris that is legitimately in the repo and illegitimately in a package: excluded on the way in,
# asserted absent on the way out.
$copyExclude = @('*.pdb', '*.gitkeep')

# --- inputs, before anything is deleted -----------------------------------------------------------
foreach ($required in @($projectFile, $payloadDll, $aboutXml, $loadFolders, $license)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing packaging input: $required"
    }
}
# US red lines, checked where they can still be fixed. Extras and audio are content red lines; the
# carrier copy is an identity red line - coahuilite.ferritelib is the single carrier, and a second copy
# binds by load order through RimWorld's global AssemblyResolve with no detector for the loser.
if (Test-Path -LiteralPath (Join-Path $root 'Extras') -PathType Container) { throw "US must not ship Extras content packs: $root\Extras" }
if (Test-Path -LiteralPath (Join-Path $contentRoot 'Sounds') -PathType Container) { throw "US must not ship built-in audio: $contentRoot\Sounds" }
if (Test-Path -LiteralPath (Join-Path $root '1.6\Assemblies\FerriteLib.UiKit.dll') -PathType Leaf) {
    throw "US must not ship the FerriteLib payload; coahuilite.ferritelib is the single carrier: $root\1.6\Assemblies\FerriteLib.UiKit.dll"
}

# Version discipline: the staged About.xml must carry exactly the label on the box. US records the full
# release-axis label in About (the rc suffix lives in the tag and the artifact name only, never in
# About - same discipline as the carrier).
[xml]$aboutXmlDoc = Get-Content -LiteralPath $aboutXml -Raw
$modVersionNode = $aboutXmlDoc.SelectSingleNode('/ModMetaData/modVersion')
if ($null -eq $modVersionNode -or [string]::IsNullOrWhiteSpace($modVersionNode.InnerText)) {
    throw 'About.xml is missing <modVersion>; the product version source must stay in sync.'
}
if ($modVersionNode.InnerText.Trim() -ne $VersionLabel.Trim()) {
    throw "About.xml <modVersion> ($($modVersionNode.InnerText.Trim())) does not match package label ($VersionLabel). Update About.xml or the csproj <Version>."
}

# --- measure, do not trust the caller's word ------------------------------------------------------
# MSBuild stamps AssemblyConfigurationAttribute from $(Configuration), so the assembly itself records
# which channel produced it. The reader is shared with the carrier gate (see
# scripts/read-assembly-stamp.ps1 for the child-process and MetadataReader reasoning).
function Get-AssemblyConfiguration {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (@(& (Join-Path $PSScriptRoot 'read-assembly-stamp.ps1') -Path $Path) -join '')
}

function Get-AssemblyInformationalVersion([string]$Path) {
    return [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path).ProductVersion
}

$stamp = Get-AssemblyConfiguration -Path $payloadDll
$expectedStamp = if ($BuildFlavor -eq 'dev') { 'Dev' } else { 'Release' }
if ([string]::IsNullOrWhiteSpace($stamp)) {
    throw "The payload carries no AssemblyConfigurationAttribute; it cannot be attributed to the $expectedStamp configuration."
}
if ($stamp -ne $expectedStamp) {
    throw "Channel '$BuildFlavor' requires a $expectedStamp-configured payload, but $payloadDll reports '$stamp'. Build the matching configuration: pack-dev forces -c Dev --no-incremental, the release channel builds -c Release."
}

# The carrier this package was compiled against is part of its identity, not an assumption: US pins its
# prerequisite range to that API and a player installs the pair. Recorded on every channel; required to
# be Release on the release channel (S5: the carrier gate used to check existence, not bytes).
$carrierStamp = 'absent'
$carrierInfo = 'absent'
if (Test-Path -LiteralPath $carrierDll -PathType Leaf) {
    $carrierStamp = Get-AssemblyConfiguration -Path $carrierDll
    $carrierInfo = Get-AssemblyInformationalVersion -Path $carrierDll
    if ([string]::IsNullOrWhiteSpace($carrierStamp)) { $carrierStamp = 'unreadable' }
    if ([string]::IsNullOrWhiteSpace($carrierInfo)) { $carrierInfo = 'unreadable' }
}

if ($RequireReleaseIdentity) {
    if ($carrierStamp -ne 'Release') {
        throw "The release channel links a '$carrierStamp' carrier at $carrierDll; a published US asset must be built against the Release FerriteLib payload: dotnet build ../ferritelib/Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-incremental"
    }
    $informational = Get-AssemblyInformationalVersion -Path $payloadDll
    if ([string]::IsNullOrWhiteSpace($informational)) {
        throw 'The payload carries no ProductVersion; it cannot be attributed to a source commit.'
    }
    if ($informational -match '-dev') {
        throw "Payload identity is '$informational'; a published asset must not carry the dev suffix."
    }
    $infoBase = $informational -replace '[-+].*$', ''
    $releaseBase = ($VersionLabel -replace '^v', '') -replace '[-+].*$', ''
    if ($infoBase -ne $releaseBase) {
        throw "Payload identity is '$informational' (base $infoBase) but this channel is packing '$VersionLabel' (base $releaseBase). The build is stale or the label is wrong."
    }
}

# --- stage from scratch ---------------------------------------------------------------------------
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir '1.6\Assemblies') -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir 'About') -Force

# About.xml is copied as ONE file, never as the About directory (S3). A locally generated
# About/PublishedFileId.txt is Workshop identity; copying the directory let it ride into any package
# from any channel and a strip step cleaned up afterwards. Construction beats cleanup: there is now no
# path by which it can enter.
Copy-Item -LiteralPath $aboutXml -Destination (Join-Path $stageDir 'About\About.xml') -Force
Copy-Item -LiteralPath $loadFolders -Destination (Join-Path $stageDir 'LoadFolders.xml') -Force
# MPL-2.0 section 3.2: a distributed Executable Form must say how to obtain the Source Code Form, so the
# licence text travels inside the package instead of living only in the repository.
Copy-Item -LiteralPath $license -Destination (Join-Path $stageDir 'LICENSE') -Force
Copy-Item -LiteralPath $payloadDll -Destination (Join-Path $stageDir '1.6\Assemblies\UniversalSqueaker.dll') -Force

# The content root (`1.6/`) is legitimately open - Languages today, Defs/Patches whenever a package
# needs them - so it is not allowlisted. Build debris is excluded on the way in and the assertion below
# proves the exclusion held. Paths are taken relative to the content root and rooted back under the
# stage dir's own `1.6/`; skipping that second step spreads the content root over the package root, and
# the closed set below is what catches it.
foreach ($source in @(Get-ChildItem -LiteralPath $contentRoot -Recurse -File)) {
    $skip = $false
    foreach ($pattern in $copyExclude) {
        if ($source.Name -like $pattern) { $skip = $true; break }
    }
    if ($skip) { continue }
    $rel = $source.FullName.Substring($contentRoot.Length + 1)
    $target = Join-Path (Join-Path $stageDir '1.6') $rel
    $parent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $parent -Force
    }
    Copy-Item -LiteralPath $source.FullName -Destination $target -Force
}

# Package identity, written after the last structural copy so nothing can filter it out. It answers "how
# fresh is this folder and which carrier does it need" without opening a DLL.
$sourceUrl = if ($env:GITHUB_REPOSITORY) { "$($env:GITHUB_SERVER_URL)/$env:GITHUB_REPOSITORY" } else { 'https://github.com/Coahuilite/UniversalSqueaker' }
[System.IO.File]::WriteAllText(
    (Join-Path $stageDir 'version.txt'),
    "UniversalSqueaker $VersionLabel`r`nbuild=$BuildFlavor`r`ncommit=$CommitLabel`r`ncarrier=$carrierStamp $carrierInfo`r`nsource $sourceUrl`r`n")

# --- assertions on the finished tree --------------------------------------------------------------
foreach ($directory in $forbiddenDirs) {
    if (Test-Path -LiteralPath (Join-Path $stageDir $directory)) {
        throw "Staged package carries content US must never ship: $(Join-Path $stageDir $directory)"
    }
}

$stagedFiles = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File | ForEach-Object {
    $_.FullName.Substring($stageDir.Length + 1).Replace('\', '/').ToLowerInvariant()
})

foreach ($required in $stableFiles) {
    if ($stagedFiles -notcontains $required.ToLowerInvariant()) {
        throw "Staged package is missing $required."
    }
}

# Closed set where the surface is stable, named sweep where it is open. The package root and `About/`
# are enumerated exactly, so anything else at those levels is an intruder; inside `1.6/` only the named
# debris is refused, because that is the one place a mod legitimately grows.
$allowed = @($stableFiles | ForEach-Object { $_.ToLowerInvariant() })
$intruders = @()
foreach ($file in $stagedFiles) {
    if ($allowed -contains $file) { continue }
    if (-not $file.StartsWith('1.6/')) { $intruders += $file; continue }
    $name = Split-Path -Leaf $file
    foreach ($pattern in $forbiddenNames) {
        if ($name -like $pattern) { $intruders += $file; break }
    }
}
if ($intruders.Count -gt 0) {
    throw "Staged package carries files it must not: $($intruders -join ', ')"
}

$assemblyCount = @($stagedFiles | Where-Object { $_ -like '*.dll' }).Count
if ($assemblyCount -ne 1) {
    throw "The payload must be exactly one assembly; found $assemblyCount."
}

Write-Host "[stage-package] flavor=$BuildFlavor measured=$stamp label=$VersionLabel commit=$CommitLabel carrier=$carrierStamp $carrierInfo"
Write-Host "[stage-package] staged $($stagedFiles.Count) files -> $stageDir"

# --- optional archive, deterministic by construction ----------------------------------------------
# FL→US round 2 S4: two writers made two names, and Compress-Archive stamps entries from the staged
# files' mtimes - which staging itself rewrites - so two packs of one commit hashed differently over
# identical content. A digest nobody else can reproduce is a receipt, not a check. Pinning the file
# mtimes first does not fix it either (NTFS re-dirties a directory during the compressor's own walk),
# so the timestamps go into the archive directly: names normalised to `/`, sorted, every entry stamped
# with the commit's author date, and rooted at exactly one top-level mod folder - the shape that tells a
# player to unzip into Mods/ and get a valid mod directory. US's release job used to work around the old
# shape inline; that workaround is gone because the producer no longer needs it.
if ($CreateZip) {
    $commitDate = [DateTimeOffset]::Parse((& git -C $root log -1 --format=%aI)).ToUniversalTime()
    $name = if ([string]::IsNullOrWhiteSpace($ArchiveName)) { "UniversalSqueaker-$BuildFlavor-v$VersionLabel-$CommitLabel" } else { $ArchiveName }
    $zipPath = Join-Path (Split-Path -Parent $stageDir) "$name.zip"
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

    $top = Split-Path -Leaf $stageDir
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $entries = @(Get-ChildItem -LiteralPath $stageDir -Recurse -Force | ForEach-Object {
            $rel = $_.FullName.Substring($stageDir.Length + 1).Replace('\', '/')
            if ($_.PSIsContainer) { "$rel/" } else { $rel }
        } | Sort-Object)
        foreach ($rel in $entries) {
            $entry = $archive.CreateEntry("$top/$rel", [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $commitDate
            if ($rel.EndsWith('/')) { continue }
            $in = [System.IO.File]::OpenRead((Join-Path $stageDir ($rel.Replace('/', '\'))))
            $out = $entry.Open()
            try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
        }
        # The root entry, last, and stamped like everything else: an unstamped entry defaults to "now",
        # which is precisely the wall clock that made two packs of one commit hash differently.
        $rootEntry = $archive.CreateEntry("$top/", [System.IO.Compression.CompressionLevel]::NoCompression)
        $rootEntry.LastWriteTime = $commitDate
    } finally { $archive.Dispose() }

    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    Write-Host "[stage-package] asset  -> $zipPath"
    Write-Host "[stage-package] sha256 -> $hash"
    Write-Host ("[stage-package] bytes  -> {0}" -f (Get-Item -LiteralPath $zipPath).Length)
}
