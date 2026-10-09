# Acquire the AUTHORITATIVE CI-built FerriteLib release carrier as US's single compile payload.
#
# Why this is one script: both workflows (ci.yml and release.yml) must consume the same artifact -
# the release asset published by the FerriteLib release workflow - instead of checking out a mutable
# branch and building a second FerriteLib runtime. A second build is a second carrier: two copies of
# FerriteLib.UiKit.dll bind by load order through RimWorld's global AssemblyResolve and the copy that
# loses has no way to find out. So this helper downloads the exact named asset for an explicit
# repo+tag (never `latest`), verifies the release flags and the asset API sha256 digest when the
# platform reports one, verifies the CLOSED payload set inside the zip, re-reads version.txt and the
# DLL's own embedded commit and configuration, asserts the commit equals the pinned source
# checkout's HEAD, and stages that one DLL at the sibling path every downstream gate already reads.
#
# Rehearsal input: -AssetZipPath uses an already-downloaded zip instead of the network (the digest
# checks stay active). Acquiring is read-only from the carrier repository's perspective.
#
# Filesystem ownership (2026-10-09 review): every run creates a FRESH exclusive child directory
# (us-carrier-<guid>) under a work parent (the caller's -WorkDir, or the system temp directory).
# Preexisting content is never the run directory and is never deleted or overwritten; the only
# recursive cleanup allowed is the run directory this process created, verified immediately before
# deletion as exactly that child (owned name, direct child of the resolved parent, inside the system
# temp root, not a reparse point). Staging refuses a sibling target inside the pinned checkout,
# inside this repository's own tree, or any directory that is itself a git checkout.

[CmdletBinding()]
param(
    [string]$Repo = 'Coahuilite/FerriteLib',
    [Parameter(Mandatory = $true)][string]$Tag,
    # The carrier source checkout pinned at the release tag (for the harness support the sibling
    # needs); its HEAD must equal the asset DLL's embedded commit or pairing fails.
    [string]$SourceCheckout = 'ci-ferritelib',
    # Where the verified DLL is staged. Default: the sibling path the csproj HintPath and both
    # workflows use. Absolute or repo-relative.
    [string]$SiblingRoot,
    [string]$AssetZipPath,
    [string]$ExpectedDigest,
    # Work PARENT for this run. A fresh exclusive child (us-carrier-<guid>) is created under it and
    # nothing preexisting is ever used as the run directory; the child is auto-cleaned only when the
    # parent lies inside the system temp root. Default: the system temp directory.
    [string]$WorkDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
function Resolve-InputPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $root $Path))
}

if ([string]::IsNullOrWhiteSpace($SiblingRoot)) {
    $SiblingRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $root) 'ferritelib'))
} else {
    $SiblingRoot = Resolve-InputPath $SiblingRoot
}
$checkoutRoot = Resolve-InputPath $SourceCheckout
$expectedDigest = $ExpectedDigest
$assetZip = if ([string]::IsNullOrWhiteSpace($AssetZipPath)) { $null } else { Resolve-InputPath $AssetZipPath }

# An explicit release tag only: a floating ref (latest/main/default) cannot pin bytes and is refused
# by construction, and the dialect matches the release workflows' own vBASE / vBASE-rcN rule.
if ($Tag -notmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc([1-9]\d*))?$') {
    throw "An explicit release tag vMAJOR.MINOR.PATCH[-rcN] is required (never 'latest'): $Tag"
}
$baseVersion = ($Tag -replace '^v', '') -replace '-rc.*$', ''
$assetName = "FerriteLib-$Tag.zip"

if ([string]::IsNullOrWhiteSpace($checkoutRoot) -or -not (Test-Path -LiteralPath (Join-Path $checkoutRoot '.git'))) {
    throw "The pinned carrier source checkout is required for pairing and was not found at $checkoutRoot."
}
if ($null -ne $assetZip -and -not (Test-Path -LiteralPath $assetZip -PathType Leaf)) {
    throw "Rehearsal asset not found: $assetZip"
}

# Staging-target discipline (checked before any work is done): the carrier is staged only into a
# plain, separate sibling directory. Refuse a target inside the pinned carrier checkout (writing
# there would corrupt the source the pairing reads), inside this repository's own tree (which must
# never receive a carrier copy - the single-carrier red line), or any directory that is itself a
# git checkout (an accepted carrier/repository location, not a staging area).
if ($SiblingRoot -eq $checkoutRoot -or $SiblingRoot.StartsWith($checkoutRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to stage: SiblingRoot is inside the pinned carrier source checkout ($SiblingRoot)."
}
if ($SiblingRoot -eq $root -or $SiblingRoot.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to stage into this repository's own tree ($SiblingRoot); the sibling must lie outside it."
}
if (Test-Path -LiteralPath (Join-Path $SiblingRoot '.git')) {
    throw "Refusing to stage into a git checkout ($SiblingRoot); the sibling must be a plain staged directory."
}

# Work area: every run gets its OWN fresh child under a work parent - the caller's -WorkDir when
# given, the system temp directory otherwise. A preexisting directory is only ever a parent, never
# the run directory, so nothing preexisting can be deleted or overwritten by this script.
$workParent = if ([string]::IsNullOrWhiteSpace($WorkDir)) { [IO.Path]::GetFullPath([IO.Path]::GetTempPath()) } else { Resolve-InputPath $WorkDir }
if (-not (Test-Path -LiteralPath $workParent -PathType Container)) { $null = New-Item -ItemType Directory -Path $workParent -Force }
$workParent = (Resolve-Path -LiteralPath $workParent).Path
if ($workParent.Length -gt 3) { $workParent = $workParent.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) }
$work = Join-Path $workParent ('us-carrier-' + [guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $work) { throw "Refusing a non-exclusive work directory (already exists): $work" }
$runDirCreated = $false
$null = New-Item -ItemType Directory -Path $work
$runDirCreated = $true

try {
    # ---- 1. release lookup + asset identity (public API read; a token is used only if present) ----
    $apiDigest = ''
    if ($null -eq $assetZip) {
        $headers = @{ 'User-Agent' = 'us-acquire-ferritelib-carrier'; 'Accept' = 'application/vnd.github+json' }
        if ($env:GH_TOKEN) { $headers['Authorization'] = "Bearer $($env:GH_TOKEN)" }
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/tags/$Tag" -Headers $headers -Method Get
        if ($release.draft) { throw "Release $Tag on $Repo is a DRAFT; an acquirable carrier must be published." }
        $expectPrerelease = [bool]($Tag -match '-rc[1-9]\d*$')
        if ([bool]$release.prerelease -ne $expectPrerelease) {
            throw "Release $Tag on $Repo has prerelease=$($release.prerelease); expected $expectPrerelease for this tag shape (a mis-flagged rc silently becomes the repository's Latest)."
        }
        $assets = @($release.assets | Where-Object { $_.name -ceq $assetName })
        if ($assets.Count -ne 1) { throw "Expected exactly one asset named '$assetName' on $Repo@$Tag; found $($assets.Count)." }
        $asset = $assets[0]
        if ($asset.digest) {
            if ($asset.digest -notmatch '^sha256:[0-9a-f]{64}$') { throw "Asset digest is not a sha256 digest: $($asset.digest)" }
            $apiDigest = [string]$asset.digest
        }
        $downloadUrl = [string]$asset.browser_download_url
        $expectedUrl = "https://github.com/$Repo/releases/download/$Tag/$assetName"
        if ($downloadUrl -cne $expectedUrl) { throw "Asset download URL drifted from the exact named route: $downloadUrl" }
        $assetZip = Join-Path $work $assetName
        Invoke-WebRequest -Uri $downloadUrl -OutFile $assetZip -Headers @{ 'User-Agent' = 'us-acquire-ferritelib-carrier' }
    }
    $zipHash = (Get-FileHash -LiteralPath $assetZip -Algorithm SHA256).Hash.ToLowerInvariant()

    # ---- 2. digest discipline: server digest when available, expected digest when given -------------
    $digestSource = 'none (server digest unavailable; locally computed hash recorded)'
    if ($apiDigest) {
        if ("sha256:$zipHash" -cne $apiDigest) { throw "Asset digest mismatch: the API reports $apiDigest, the zip hashes to sha256:$zipHash." }
        $digestSource = 'api'
    }
    if (-not [string]::IsNullOrWhiteSpace($expectedDigest)) {
        if ($expectedDigest -notmatch '^sha256:[0-9a-f]{64}$') { throw "-ExpectedDigest must be sha256:<64-hex>: $expectedDigest" }
        if ($expectedDigest -cne "sha256:$zipHash") { throw "Asset digest mismatch: expected $expectedDigest, the zip hashes to sha256:$zipHash." }
        $digestSource = if ($digestSource -eq 'api') { 'api+expected' } else { 'expected' }
    }

    # ---- 3. extract + closed payload identity -----------------------------------------------------
    $extract = Join-Path $work 'extract'
    if (Test-Path -LiteralPath $extract) { throw "Refusing to overwrite preexisting extract content: $extract" }
    Expand-Archive -LiteralPath $assetZip -DestinationPath $extract
    $topDirs = @(Get-ChildItem -LiteralPath $extract -Directory)
    if ($topDirs.Count -ne 1 -or $topDirs[0].Name -ne 'FerriteLib') {
        throw 'The archive must contain exactly one top-level FerriteLib/ folder.'
    }
    $payloadRoot = $topDirs[0].FullName
    $expectedFiles = @('about/about.xml', 'loadfolders.xml', 'license', 'version.txt', '1.6/assemblies/ferritelib.uikit.dll')
    $files = @(Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | ForEach-Object {
        $_.FullName.Substring($payloadRoot.Length + 1).Replace('\', '/').ToLowerInvariant()
    })
    $missing = @($expectedFiles | Where-Object { $files -notcontains $_ })
    if ($missing.Count -gt 0) { throw "Closed payload set is missing: $($missing -join ', ')" }
    $unexpected = @($files | Where-Object { $expectedFiles -notcontains $_ })
    if ($unexpected.Count -gt 0) { throw "Closed payload set carries unexpected files: $($unexpected -join ', ')" }
    $dllCount = @($files | Where-Object { $_ -like '*.dll' }).Count
    if ($dllCount -ne 1) { throw "The payload must be exactly one assembly; found $dllCount." }

    # ---- 4. version.txt + DLL provenance ----------------------------------------------------------
    $versionLines = @(Get-Content -LiteralPath (Join-Path $payloadRoot 'version.txt') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($versionLines.Count -ne 5) { throw "version.txt must carry exactly five non-empty lines; found $($versionLines.Count)." }
    if ($versionLines[0] -cne "FerriteLib $Tag") { throw "version.txt label '$($versionLines[0])' does not match the requested release (FerriteLib $Tag)." }
    if ($versionLines[1] -cne 'build=github') { throw "version.txt build line is '$($versionLines[1])'; the release channel must be 'build=github'." }
    if ($versionLines[2] -notmatch '^commit=([0-9a-f]{12})$') { throw "version.txt commit line is malformed: $($versionLines[2])" }
    $shortCommit = $Matches[1]
    if ($versionLines[3] -notmatch '^payload-sha256=([0-9a-fA-F]{64})$') { throw "version.txt payload-sha256 line is malformed: $($versionLines[3])" }
    $payloadHashFromText = $Matches[1].ToLowerInvariant()
    if ($versionLines[4] -cne "source https://github.com/$Repo") { throw "version.txt source line is '$($versionLines[4])'; expected the repository URL for $Repo." }

    [xml]$about = Get-Content -LiteralPath (Join-Path $payloadRoot 'About/About.xml') -Raw
    $aboutId = $about.SelectSingleNode('/ModMetaData/packageId').InnerText.Trim()
    if ($aboutId -cne 'coahuilite.ferritelib') { throw "Asset About.xml packageId '$aboutId' is not coahuilite.ferritelib." }
    $aboutVersion = $about.SelectSingleNode('/ModMetaData/modVersion').InnerText.Trim()
    if ($aboutVersion -cne $baseVersion) { throw "Asset About.xml modVersion '$aboutVersion' does not match the tag base $baseVersion." }

    $dllPath = Join-Path $payloadRoot '1.6/Assemblies/FerriteLib.UiKit.dll'
    $dllHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($dllHash -cne $payloadHashFromText) { throw "Asset DLL sha256 $dllHash does not match version.txt payload-sha256=$payloadHashFromText." }

    $stampScript = Join-Path $PSScriptRoot 'read-assembly-stamp.ps1'
    $config = (& $stampScript -Path $dllPath) -join ''
    if ($config -cne 'Release') { throw "The carrier payload must be Release-configured; the asset DLL reports '$config'." }
    $informational = (& $stampScript -Path $dllPath -AttributeName 'AssemblyInformationalVersionAttribute') -join ''
    $ivPattern = '^' + [regex]::Escape($baseVersion) + '\+([0-9a-f]{40})$'
    if ($informational -notmatch $ivPattern) { throw "The asset DLL's informational version '$informational' is not <$baseVersion>+<full 40-hex commit>." }
    $fullCommit = $Matches[1]
    if (-not $fullCommit.StartsWith($shortCommit)) { throw "Asset DLL commit $fullCommit does not extend version.txt commit $shortCommit." }

    # ---- 5. pairing with the pinned source checkout -----------------------------------------------
    $head = ((& git -C $checkoutRoot rev-parse 'HEAD^{commit}') -join '').Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)) { throw "Cannot read the pinned carrier checkout HEAD at $checkoutRoot." }
    if ($head -cne $fullCommit) {
        throw "Pairing mismatch: the asset DLL was built from $fullCommit but the pinned source checkout is at $head. Reconcile the pinned tag before proceeding."
    }

    # ---- 6. stage the single DLL at the sibling path (+ identity re-read after the copy) -----------
    $siblingDll = Join-Path $SiblingRoot '1.6/Assemblies/FerriteLib.UiKit.dll'
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $siblingDll) -Force
    Copy-Item -LiteralPath $dllPath -Destination $siblingDll -Force
    $stagedHash = (Get-FileHash -LiteralPath $siblingDll -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($stagedHash -cne $dllHash) { throw 'The staged DLL does not match the verified asset bytes.' }

    # ---- 7. expose the pairing metadata (logs, step summary, step outputs) -------------------------
    Write-Host "[carrier] repo=$Repo tag=$Tag asset=$assetName asset-sha256=$zipHash dll-sha256=$dllHash source-commit=$fullCommit digest-source=$digestSource"
    Write-Host "[carrier] staged -> $siblingDll"
    if ($env:GITHUB_OUTPUT) {
        "fl-tag=$Tag" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8NoBOM
        "fl-commit=$fullCommit" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8NoBOM
        "fl-dll-sha256=$dllHash" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8NoBOM
        "fl-asset-sha256=$zipHash" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8NoBOM
    }
    if ($env:GITHUB_STEP_SUMMARY) {
        @"
### FerriteLib carrier (dependency pinning)

| | |
|---|---|
| Dependency | ``$Repo`` release ``$Tag`` |
| Asset | ``$assetName`` (zip sha256 ``$zipHash``; digest source: $digestSource) |
| Carrier DLL sha256 | ``$dllHash`` |
| Source commit | ``$fullCommit`` |
"@ | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8NoBOM
    }
}
finally {
    # Recursive cleanup is bounded to the run directory THIS process created: it must still resolve
    # to exactly the fresh us-carrier-<guid> child of the resolved work parent, the parent must lie
    # inside the system temp root, and the target must not be a reparse point. Anything else is kept
    # as-is; nothing outside the owned run directory is ever deleted.
    $keepReason = $null
    if (-not $runDirCreated) { $keepReason = 'not created by this run' }
    elseif (-not (Test-Path -LiteralPath $work)) { $keepReason = 'already gone' }
    elseif (-not (Test-Path -LiteralPath $work -PathType Container)) { $keepReason = 'not a directory' }
    elseif ((Split-Path -Parent $work) -ne $workParent) { $keepReason = 'not a direct child of its resolved work parent' }
    elseif ((Split-Path -Leaf $work) -notmatch '^us-carrier-[0-9a-f]{32}$') { $keepReason = 'unrecognized run-directory name' }
    elseif (-not $work.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) { $keepReason = 'outside the system temp root' }
    elseif (((Get-Item -LiteralPath $work -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { $keepReason = 'reparse point' }
    if ($null -eq $keepReason) {
        Remove-Item -LiteralPath $work -Recurse -Force
        Write-Host "[carrier] work-dir cleaned: $work"
    }
    elseif ($keepReason -eq 'already gone') {
        Write-Host "[carrier] work-dir already gone: $work"
    }
    else {
        Write-Host "[carrier] work-dir kept ($keepReason): $work"
    }
}
