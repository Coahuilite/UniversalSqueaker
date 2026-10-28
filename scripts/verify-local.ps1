param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$PackDev,
    [string]$FerriteLibArtifactPath,
    # Explicit opt-in for integration against a local Dev/dirty carrier; never release evidence.
    [switch]$DevelopmentCarrier,
    [switch]$NoRestore,
    [string]$FerriteLibSourceRoot,
    [string]$LogDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# One-input local verification for Universal Squeaker (US-adapted from the SR runbook process).
# Fail-fast order:
#   1   kernel gate: unit tests + US 0.1.0 golden corpus byte replay + determinism
#   2   config-copy lifecycle characterization
#   3   settings migration characterization (schema migration, write bridges, baseline importer)
#   4   log protocol characterization, Release
#   5   log protocol characterization, Dev (US_DEV)
#   6   selected carrier identity (clean Release by default; explicit Dev integration opt-in)
#   7   main assembly Dev build (US_DEV, TreatWarningsAsErrors)
#   8   main assembly Release build (TreatWarningsAsErrors)
#   9   US payload is single-carrier (UniversalSqueaker.dll present, FerriteLib.UiKit.dll ABSENT)
#  10   LICENSE present and un-truncated MPL-2.0, identical to the carrier's copy
#  11   Schema=2 manifests (settings page + camera overlay): present, well-formed, correctly attributed
#  12   UniversalSqueakerUiLogicTests Release (filters + attenuation math + layout math + mode-set drift guard + Schema2 source invariants + Keyed localization contract)
#  13   UniversalSqueakerKernelHostTests (Release by default; Dev with -DevelopmentCarrier) (real Schema2 Host + typed bindings + 5 workspaces x 3 viewports + narrow Mood geometry + text-fit audit against both language tables)
#  14   UI boundary audit (scripts/ui-boundary-audit.ps1): raw renderer-backend calls only inside the
#       2-file exemption whitelist (豁免一 + 豁免二), and ZERO raw Mouse.IsOver since FL P1
#  15   harness stub coverage: the carrier's reference-driven scan (read-only) over the US payload AND
#       the KernelHostTests harness assembly, against US's own exemption ledger
#       (scripts/stub-coverage-exemptions.txt). Runs after 13 on purpose - gate 13 builds both scanned
#       assemblies. Current 0.8 doubles live in tools/FerriteLib08.Stubs, not the producer tree.
#  16   HISTORICAL ENTRY ONLY — frozen stub snapshot integrity: all five tools/FerriteLib.Stubs source files must equal the
#       sha256 values recorded in fl/frozen-handoff.json (ferritelib 0.7.x@d5af4d8), only when the
#       historical entry is explicitly invoked. It does not certify current0.8 doubles.
# GATE PROVENANCE: 1-5 and 10-16 are US-owned; 6 and 9 assert the carrier boundary itself. 14 shares
# its metric with the FerriteLib containment gate (its HANDOFF item B): the two whitelists must agree
# entry by entry. 15 consumes the carrier's scanner (its gate 8) but the ledger and both scanned
# assemblies (the payload and the KernelHostTests harness) are US's; the scanner's own fixture controls
# stay in the carrier's gate 8, where the scanned tree is the carrier's. The FerriteLib library gates (its harness, Dev/Release builds, neutrality grep and
# the visual-core/page-model boundary) moved to that repository, which runs them from inside with a
# positive control.
# Gate IDs are stable; historical gate16 is an explicit separate entry. Numbers are append-only: "gate N" is cited across MEMORY/TODO/docs, so a new check joins as the
# next number instead of shifting old references.
# -PackDev: after all checks pass, build the dev package (allows a dirty tree; auto -dirty label).
# r2 gates 32-37 invoke already-existing Core/domain/through/save/template/author checks.
# No new unit cases are defined here; native game and real Scribe acceptance remain separate.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'
$carrierDll = & (Join-Path $PSScriptRoot 'resolve-carrier.ps1') -ProjectRoot $root -FerriteLibArtifactPath $FerriteLibArtifactPath
$uiLogicTestsProject = Join-Path $root 'tools\UniversalSqueakerUiLogicTests\UniversalSqueakerUiLogicTests.csproj'
$kernelHostTestsProject = Join-Path $root 'tools\UniversalSqueakerKernelHostTests\UniversalSqueakerKernelHostTests.csproj'
$hostConfiguration = if ($DevelopmentCarrier) { 'Dev' } else { 'Release' }
$buildExtraArgs = @("-p:FerriteLibArtifactPath=$carrierDll", '--no-restore', '--disable-build-servers')
$carrierSource = if ($FerriteLibSourceRoot) { [IO.Path]::GetFullPath($FerriteLibSourceRoot, $root) }
    elseif (Test-Path -LiteralPath (Join-Path $root 'ci-ferritelib/.git')) { Join-Path $root 'ci-ferritelib' }
    else { Join-Path (Split-Path -Parent $root) 'ferritelib' }
$logs = if ($LogDirectory) { [IO.Path]::GetFullPath($LogDirectory, $root) }
    else { Join-Path $root ('dist/verify/' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)) }
if (Test-Path -LiteralPath $logs) { throw "Use a new log directory; refusing to replace $logs" }
$null = New-Item -ItemType Directory -Path $logs
$script:gateNumber = 0
$script:results = [Collections.Generic.List[object]]::new()
$toolBuilds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$commit = ((git -C $root rev-parse HEAD) -join '').Trim()
$carrierIdentity = (& (Join-Path $PSScriptRoot 'read-assembly-stamp.ps1') -Path $carrierDll -AttributeName 'AssemblyInformationalVersionAttribute') -join ''
function Write-VerificationSummary {
    [ordered]@{ commit=$commit; carrier=$carrierDll; carrierIdentity=$carrierIdentity; carrierSource=$carrierSource;
        sdkContract='SDK10 + runtime8 (net8 tools); net472 on Windows'; noRestore=[bool]$NoRestore;
        results=@($script:results.ToArray()); notRunGateIds=@(($script:gateNumber+1)..37 | Where-Object { $_ -le 37 -and $_ -gt $script:gateNumber });
        historicalGate16='scripts/verify-historical-inputs.ps1; not a current compiler/runtime input' } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $logs 'summary.json')
}
function Invoke-Check {
    param([string]$Name, [string]$Retry, [scriptblock]$Action)
    $script:gateNumber++
    $log = Join-Path $logs ('gate-{0:00}.log' -f $script:gateNumber)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $code = 0; $failure = ''
    @("gate=$($script:gateNumber) name=$Name", "commit=$commit", "carrier=$carrierIdentity path=$carrierDll", "cwd=$((Get-Location).Path)", "start=$([DateTimeOffset]::Now.ToString('o'))", "retry_hint=$Retry") | Set-Content -LiteralPath $log
    Write-Host -NoNewline "[gate $($script:gateNumber)] $Name ... "
    try { $global:LASTEXITCODE = 0; & $Action *>> $log; $code = $LASTEXITCODE }
    catch { $code = if ($LASTEXITCODE -ne 0) { $LASTEXITCODE } else { 1 }; $failure = $_.ToString(); $failure | Add-Content -LiteralPath $log }
    $watch.Stop()
    @("exit=$code", "seconds=$($watch.Elapsed.TotalSeconds.ToString('F3',[Globalization.CultureInfo]::InvariantCulture))") | Add-Content -LiteralPath $log
    $script:results.Add([ordered]@{id=$script:gateNumber;name=$Name;exit=$code;seconds=$watch.Elapsed.TotalSeconds;log=[IO.Path]::GetFileName($log)})
    Write-VerificationSummary
    if ($code -ne 0) {
        Write-Host "FAIL ($log)"; Get-Content -LiteralPath $log -Tail 24 | Write-Host
        Write-Host "[retry] $Retry"; throw "Gate $($script:gateNumber) failed; full log and unrun IDs retained in $logs"
    }
    Write-Host ("OK ({0:F3}s; {1})" -f $watch.Elapsed.TotalSeconds, $log)
}
function Invoke-DotNet([string[]]$Arguments) {
    Write-Host ('$ dotnet ' + ($Arguments -join ' '))
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited $LASTEXITCODE" }
}
function Invoke-Tool {
    param([string]$Name, [string]$Configuration='Release', [string[]]$Arguments=@())
    $project = Join-Path $root "tools/$Name/$Name.csproj"
    $key = "$project|$Configuration|$carrierDll"
    if ($toolBuilds.Add($key)) {
        $properties = @()
        if ($Name -eq 'UniversalSqueakerKernelHostTests') {
            $properties += '-p:UsProductAlreadyBuilt=true', '-p:UsVerificationNoRestore=true',
                "-p:UsProductArtifactPath=$(Join-Path $root "dist/build/$Configuration/UniversalSqueaker.dll")"
        }
        Invoke-DotNet (@('build',$project,'-c',$Configuration,'--nologo') + $buildExtraArgs + $properties)
    } else { Write-Host "[reuse build in this invocation] $Name $Configuration (same source/config/constants/carrier)" }
    [xml]$xml = Get-Content -LiteralPath $project -Raw
    $framework = $xml.SelectSingleNode('/Project/PropertyGroup/TargetFramework').InnerText
    $assembly = $xml.SelectSingleNode('/Project/PropertyGroup/AssemblyName')
    $nameOnDisk = if ($null -eq $assembly) { $Name } else { $assembly.InnerText }
    $folder = Join-Path (Split-Path -Parent $project) "bin/$Configuration/$framework"
    if ($framework -eq 'net472') {
        $exe = Join-Path $folder ($nameOnDisk + '.exe')
        Write-Host ('$ ' + $exe + ' ' + ($Arguments -join ' '))
        & $exe @Arguments
        if ($LASTEXITCODE -ne 0) { throw "$Name process exited $LASTEXITCODE" }
    } else { Invoke-DotNet (@((Join-Path $folder ($nameOnDisk + '.dll'))) + $Arguments) }
}
Invoke-DotNet -Arguments @('--info') *> (Join-Path $logs 'dotnet-info.log')
# One restore owner. Never infer freshness from project.assets.json or cache bin/obj.
# Workflow calls this entry without -NoRestore; explicit manual -NoRestore asserts a prepared graph.
if (-not $NoRestore) {
    $setupProjects = @($projectFile)
    $setupProjects += @(Get-ChildItem -LiteralPath (Join-Path $root 'tools') -Directory | ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Filter *.csproj -File | ForEach-Object { $_.FullName }
    })
    # These two roots restore the entire current stub ProjectReference graph, not the archived0.7 tree.
    $setupProjects += (Join-Path $root 'tools/FerriteLib08.Stubs/Stubs/VerseStub/VerseStub.csproj'),
        (Join-Path $root 'tools/FerriteLib08.Stubs/Stubs/UnityEngineInputLegacyModuleStub/UnityEngineInputLegacyModuleStub.csproj')
    $setupLog = Join-Path $logs 'restore.log'; $watch = [Diagnostics.Stopwatch]::StartNew()
    $restoreExit = 0
    try {
        foreach ($project in $setupProjects) { Invoke-DotNet -Arguments @('restore',$project,'--nologo',"-p:FerriteLibArtifactPath=$carrierDll") *>> $setupLog }
    } catch { $restoreExit = 1; $_ | Out-String | Add-Content -LiteralPath $setupLog; Write-Host "Restore failed: full log $setupLog"; Write-VerificationSummary; throw }
    finally { $watch.Stop(); @("restore-exit=$restoreExit", "restore-seconds=$($watch.Elapsed.TotalSeconds)") | Add-Content -LiteralPath $setupLog }
}

Invoke-Check 'UniversalSqueakerKernelTests (unit asserts + US 0.1.0 corpus replay + determinism)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerKernelTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerKernelTests' -Configuration 'Release' }

Invoke-Check 'UniversalSqueakerConfigCopyTests (store lifecycle A-F)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerConfigCopyTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerConfigCopyTests' -Configuration 'Release' }

Invoke-Check 'UniversalSqueakerSettingsMigrationTests (schema migration + write bridges + baseline)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerSettingsMigrationTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerSettingsMigrationTests' -Configuration 'Release' }

Invoke-Check 'UniversalSqueakerLogTests Release (usdiag v1/v2 protocol)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerLogTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerLogTests' -Configuration 'Release' }

Invoke-Check 'UniversalSqueakerLogTests Dev (US_DEV)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerLogTests -c Dev' `
    { Invoke-Tool -Name 'UniversalSqueakerLogTests' -Configuration 'Dev' }

Invoke-Check 'selected FerriteLib carrier configuration and source identity' `
    'select the carrier this build linked with -FerriteLibArtifactPath (read-only; nothing in this repository rebuilds it). Rebuilding the FerriteLib payload is the CARRIER OWNER''S DELIVERY STEP: it REPLACES the current freeze, and it is complete only when the owner re-issues a FREEZE NOTICE quoting the new hash AND mtime as a pair. A consumer selects the right carrier; it never builds one.' `
    {
        $carrierStamp = (& (Join-Path $PSScriptRoot 'read-assembly-stamp.ps1') -Path $carrierDll) -join ''
        if ($carrierStamp -notin @('Dev', 'Release')) { throw "Unrecognized carrier configuration '$carrierStamp'." }
        if ($DevelopmentCarrier) {
            if ($carrierStamp -ne 'Dev') { throw '-DevelopmentCarrier requires a Dev carrier so the instrument is actually exercised.' }
            Write-Host '[integration] development carrier: source may be dirty; this is not release evidence.'
            return
        }
        if ($carrierStamp -ne 'Release') { throw 'Release verification requires a Release carrier. Use -DevelopmentCarrier only for local integration.' }
        $carrierRepo = $carrierSource
        if (-not (Test-Path -LiteralPath (Join-Path $carrierRepo '.git'))) { throw 'The selected fixed carrier source checkout is required.' }
        $carrierInfo = (& (Join-Path $PSScriptRoot 'read-assembly-stamp.ps1') -Path $carrierDll -AttributeName 'AssemblyInformationalVersionAttribute') -join ''
        $plus = $carrierInfo.IndexOf('+')
        $payloadCommit = if ($plus -ge 0) { $carrierInfo.Substring($plus + 1).Trim() } else { '' }
        $carrierHead = ((& git -C $carrierRepo rev-parse HEAD) -join '').Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($carrierHead)) { throw 'Cannot read carrier HEAD.' }
        $carrierChanges = @(& git -C $carrierRepo status --porcelain)
        if ($LASTEXITCODE -ne 0 -or $carrierChanges.Count -gt 0) { throw 'Release verification requires a clean carrier checkout and a build from that commit.' }
        if ($payloadCommit -ne $carrierHead) {
            throw "Carrier source mismatch: payload=$payloadCommit checkout=$carrierHead. Select a build from the current carrier commit."
        }
    }

Invoke-Check 'main assembly Dev build (US_DEV, warnings as errors)' `
    "dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev `"-p:FerriteLibArtifactPath=$carrierDll`"" `
    { dotnet build $projectFile -c Dev @buildExtraArgs }

Invoke-Check 'main assembly Release build (warnings as errors)' `
    "dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release `"-p:FerriteLibArtifactPath=$carrierDll`"" `
    { dotnet build $projectFile -c Release @buildExtraArgs }

Invoke-Check 'US payload carries exactly one assembly (no second FerriteLib copy)' `
    "dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release `"-p:FerriteLibArtifactPath=$carrierDll`"" `
    {
        $assembliesDir = Join-Path $root 'dist\build\Release'
        if (-not (Test-Path -LiteralPath (Join-Path $assembliesDir 'UniversalSqueaker.dll') -PathType Leaf)) {
            throw "Missing built assembly: $assembliesDir\UniversalSqueaker.dll"
        }
        # The inverted guard. Two mods shipping FerriteLib.UiKit.dll bind by load order through
        # RimWorld's single global AssemblyResolve, and the copy that loses never finds out - so a
        # stray DLL sitting in this folder is a defect with no other detector.
        $stray = Join-Path $assembliesDir 'FerriteLib.UiKit.dll'
        if (Test-Path -LiteralPath $stray -PathType Leaf) {
            throw "US must not ship the FerriteLib payload (coahuilite.ferritelib is the only carrier): $stray"
        }
        & (Join-Path $PSScriptRoot 'verify-artifact-selection.ps1') -ProjectRoot $root -FerriteLibArtifactPath $carrierDll
    }

Invoke-Check 'LICENSE present and un-truncated MPL-2.0' `
    'manually' `
    {
        # MPL-2.0 is chosen for the whole series, and section 3.2 attaches an obligation to shipping a
        # DLL: recipients must be told how to get source. stage-package.ps1 copies this file into the
        # package, so the gate proves the file is real before anything is staged from it.
        $licensePath = Join-Path $root 'LICENSE'
        if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
            throw "US has no LICENSE file, yet stage-package.ps1 copies one into every package."
        }
        $text = Get-Content -LiteralPath $licensePath -Raw
        if ($text -notmatch 'Mozilla Public License Version 2\.0') { throw 'LICENSE is not the MPL-2.0 text.' }
        if ($text -notmatch 'Exhibit B') { throw 'LICENSE is truncated: Exhibit B is missing.' }
        if ($text -notmatch '10\.4\. Distributing Source Code Form') { throw 'LICENSE is truncated: section 10.4 is missing.' }
        # Scoped to the header: the reproduced licence body always contains Exhibit B's sample notice,
        # so testing the whole file would flag every correct copy.
        $separator = $text.IndexOf('-----')
        $header = if ($separator -gt 0) { $text.Substring(0, $separator) } else { $text }
        if ($header -match 'Incompatible With Secondary Licenses., as defined') {
            throw 'The applied notice declares incompatibility with secondary licenses; series policy keeps that allowed.'
        }
        # The carrier ships the same licence text; a fork between the two copies is a real defect.
        $carrierLicense = Join-Path $carrierSource 'LICENSE'
        if (Test-Path -LiteralPath $carrierLicense -PathType Leaf) {
            $ours = (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash
            $theirs = (Get-FileHash -LiteralPath $carrierLicense -Algorithm SHA256).Hash
            if ($ours -ne $theirs) { throw "US and FerriteLib LICENSE files differ; the series licence must be one text." }
        }
    }

Invoke-Check 'Schema=2 manifests present, well-formed and correctly attributed' `
    "dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release `"-p:FerriteLibArtifactPath=$carrierDll`"" `
    {
        # Schema=2 is the only shipped manifest schema since the legacy page chain was removed; a
        # manifest that parses but claims another schema or source would silently load the wrong page.
        $manifests = @(
            (Join-Path $root 'Source\UniversalSqueaker\UI\Layout.Schema2.xml'),
            (Join-Path $root 'Source\UniversalSqueaker\UI\Layout.Overlay.Schema2.xml'),
            (Join-Path $root 'Source\UniversalSqueaker\UI\Layout.RemixConfirm.Schema2.xml')
        )
        foreach ($manifestPath in $manifests) {
            if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
                throw "Missing Schema=2 manifest: $manifestPath"
            }
            $doc = [xml](Get-Content -LiteralPath $manifestPath -Raw)
            if ($doc.DocumentElement.Name -ne 'UiPage') {
                throw "Manifest root must be UiPage: $manifestPath"
            }
            if ($doc.DocumentElement.Schema -ne '2') {
                throw "Manifest must declare Schema=2: $manifestPath"
            }
            if ($doc.DocumentElement.Source -ne 'coahuilite.universalsqueaker') {
                throw "Manifest Source must be the US package id: $manifestPath"
            }
            if (@($doc.SelectNodes('//Widget')).Count -eq 0) {
                throw "Manifest declares no widgets: $manifestPath"
            }
        }
    }

# The removed legacy page chain (FerriteVoicePacksPage / VanillaVoicePacksPage) is gone with its
# fallback role, so there is no second implementation left to characterize or to keep in sync.

Invoke-Check 'UniversalSqueakerUiLogicTests Release (filters + attenuation math + layout math + mode-set drift guard + Schema2 invariants + Keyed localization contract)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerUiLogicTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerUiLogicTests' }

# Real embedded Schema=2 Host creation regression: the production Host adapter runs against the
# real resource, real US widget registrations and the real typed binding table (recording source).
Invoke-Check "UniversalSqueakerKernelHostTests $hostConfiguration (real Schema2 Host + layout + diagnostic integration)" `
    "dotnet run --no-restore --project tools/UniversalSqueakerKernelHostTests -c $hostConfiguration `"-p:FerriteLibArtifactPath=$carrierDll`"" `
    { Invoke-Tool -Name 'UniversalSqueakerKernelHostTests' -Configuration $hostConfiguration }

Invoke-Check 'UI boundary audit (renderer-backend containment + only-shrink whitelist)' `
    'pwsh -NoProfile -File scripts/ui-boundary-audit.ps1' `
    { & (Join-Path $PSScriptRoot 'ui-boundary-audit.ps1') -ProjectRoot $root }

# The harness compiles against the game's reference assemblies and executes on the carrier's stubs, so a
# member only the reference declares compiles green and dies at run time INSIDE THE HARNESS: the session
# guard swaps the element for its recovery band, the frame survives, and a lane that asserts no more than
# a live session stays green while the path under test never ran (measured 2026-09-12: Verse.GenUI.
# ContractedBy, then Mathf.Clamp(int, int, int) - the trip guard found both, no assertion did). The
# carrier's scan is reference-driven: every member a scanned assembly takes from a stub-replaced game
# assembly must be declared by the stubs or be named in this repository's own ledger with a reason.
#
# Two targets, ONE scan: the payload is the product surface, the harness is the lane surface - a lane's own
# helper can take a member the payload never touches, and gate 13 then fails INSIDE the lane (a JIT failure,
# not an assertion) while nothing names the member. They are scanned together, not in two passes, because
# the exemption ledger is a union: one pass per assembly makes every entry the other assembly needs look
# STALE (measured 2026-09-12 - the harness half alone stranded 165 entries). The scanner reports each
# target's own reference/declared counts, so findings stay attributable. It must be invoked through
# -Command with an array: after -File, a second value following -Assembly is dropped by the child pwsh's
# parameter binding (measured 2026-09-12), which silently scans one assembly and looks clean. This gate
# sits after 13 because gate 13 builds both of the assemblies it scans.
Invoke-Check 'harness stub coverage (every game member the US payload or the harness references resolves on the carrier stubs, or is exempted with a reason)' `
    'pwsh -NoProfile -Command "& ''../ferritelib/scripts/stub-coverage-scan.ps1'' -Path . -Assembly @(''dist/build/Release/UniversalSqueaker.dll'',''tools/UniversalSqueakerKernelHostTests/bin/Release/net472/UniversalSqueakerKernelHostTests.exe'') -StubsDir dist/fl08-migration-a001/stubs/Release -Exemptions scripts/stub-coverage-exemptions.txt"' `
    {
        $stubCoverageScan = Join-Path $carrierSource 'scripts/stub-coverage-scan.ps1'
        if (-not (Test-Path -LiteralPath $stubCoverageScan -PathType Leaf)) {
            throw "The carrier's stub-coverage scanner is missing at $stubCoverageScan. It ships with the sibling ferritelib checkout (scripts/stub-coverage-scan.ps1); a scan that cannot run is not a clean result."
        }

        $stubDir = Join-Path $root ("dist/fl08-migration-a001/stubs/" + $hostConfiguration)
        $stubExemptions = Join-Path $root 'scripts\stub-coverage-exemptions.txt'
        $stubTargets = @(
            (Join-Path $root 'dist\build\Release\UniversalSqueaker.dll'),
            (Join-Path $root "tools\UniversalSqueakerKernelHostTests\bin\$hostConfiguration\net472\UniversalSqueakerKernelHostTests.exe")
        )

        # Built through -Command and an array so the scanner receives both paths as separate values; every
        # path is single-quote escaped so a path containing an apostrophe cannot break out of the string.
        $stubQuote = { param($value) $value.Replace("'", "''") }
        $stubCommand = "& '" + (& $stubQuote $stubCoverageScan) + "' -Path '" + (& $stubQuote $root) +
            "' -Assembly @('" + (& $stubQuote $stubTargets[0]) + "','" + (& $stubQuote $stubTargets[1]) +
            "') -StubsDir '" + (& $stubQuote $stubDir) + "' -Exemptions '" + (& $stubQuote $stubExemptions) + "'"

        # An assembly that was never built is passed through rather than skipped: the scan reads it as its own
        # NOT SCANNED (exit 3), and an unscanned half is not a clean half.
        $stubShellName = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'pwsh.exe' } else { 'pwsh' }
        $stubShell = Join-Path $PSHOME $stubShellName
        $stubOutput = @(& $stubShell -NoProfile -Command $stubCommand *>&1)
        $stubCode = $LASTEXITCODE
        foreach ($stubLine in $stubOutput) { Write-Host $stubLine }

        if ($stubCode -ne 0) {
            # The scan prints one line per finding, but a gate's log is shown as a tail and a sorted
            # MISSING list can leave the new reference far from it - so the findings are repeated here,
            # where they cannot be trimmed away.
            $stubFindings = @($stubOutput | Where-Object { $_ -match 'MISSING |STALE |NO-REASON |NOT SCANNED' } | Select-Object -First 10)
            throw ("stub-coverage-scan.ps1 exited $stubCode (0 = clean, 2 = unresolved/stale/unreasoned finding, 3 = not scanned; " + $stubFindings.Count + " finding line(s) quoted). Findings: " + ($stubFindings -join ' ; ') + " -- declare the member in the carrier's stubs, or add it to scripts/stub-coverage-exemptions.txt with a reason code from that file's legend; a STALE entry must be deleted instead.")
        }
    }
# Gate16 retains its identifier in the explicit historical entry, not the current build path.
$script:gateNumber = 16
Write-Host '[history gate16] scripts/verify-historical-inputs.ps1; no current project consumes tools/FerriteLib.Stubs'
# Gate 17: production-source OneShot decision and real author field-loader/assembly contracts.
Invoke-Check 'OneShot S1 + author declarations (zero platform references)' `
    'dotnet run --project tools/UniversalSqueakerOneShotTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerOneShotTests' -Configuration 'Release' }
# Gate 18: controlled adapters are explicitly separate from the pure gate 17.
Invoke-Check 'S2 real binding/pool adapter sources over controlled doubles' `
    'dotnet run --project tools/UniversalSqueakerAudioBindingTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' }
# Gate 19: a separate process owns the failed-startup batch fixture.
Invoke-Check 'S2 incomplete original-observation batch fails closed' `
    'dotnet run --project tools/UniversalSqueakerAudioBindingTests -c Release -- --partial-observation' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--partial-observation') }
# Gates 20/21: fixed accepted-feedback chain and the settled three independent player qualifications.
Invoke-Check 'D-A actual Select/extension production chain over platform doubles' `
    'dotnet run --no-restore --project tools/UniversalSqueakerAudioBindingTests -c Release -- --da-select' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--da-select') }
Invoke-Check 'D-C Select defaults, sparse player qualifiers and fixed accepted-feedback evidence' `
    'dotnet run --no-restore --project tools/UniversalSqueakerAudioBindingTests -c Release -- --dc-select-settings' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--dc-select-settings') }
# Gates 22/23 compile genuinely absent targets, not a bool override of production readiness.
foreach ($selectFixtureTarget in @('MissingInternal', 'MissingFeedback')) {
    $selectFixtureOutput = Join-Path $logs "variants/select/$selectFixtureTarget"
    $selectFixtureObj = Join-Path $logs "variants/obj/select/$selectFixtureTarget"
    Invoke-Check "D-A Select missing target $selectFixtureTarget (paired isolation)" `
        "build AudioBindingTests with -p:SelectFixtureTarget=$selectFixtureTarget and isolated output/obj; run --da-select-missing-target" `
        {
            dotnet build (Join-Path $root 'tools\UniversalSqueakerAudioBindingTests') -c Release --no-restore -m:1 `
                "-p:SelectFixtureTarget=$selectFixtureTarget" "-p:OutputPath=$selectFixtureOutput/" "-p:IntermediateOutputPath=$selectFixtureObj/"
            if ($LASTEXITCODE -ne 0) { throw "Select fixture build failed: $selectFixtureTarget" }
            & (Join-Path $selectFixtureOutput 'UniversalSqueakerAudioBindingTests.exe') --da-select-missing-target
        }
}
# Gates 24/25: D-B production Eat/config consumer and physically missing precision seam.
Invoke-Check 'D-B Eat native-shape production owner and independent result root' `
    'dotnet run --no-restore --project tools/UniversalSqueakerAudioBindingTests -c Release -- --db-eat' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--db-eat') }
Invoke-Check 'D-B missing Chew target isolates precision without disabling Whole' `
    'build AudioBindingTests -p:EatFixtureTarget=MissingChew with isolated output/obj; run --db-eat-missing' `
    {
        $eatOutput = Join-Path $logs 'variants/eat/MissingChew'
        $eatObj = Join-Path $logs 'variants/obj/eat/MissingChew'
        dotnet build (Join-Path $root 'tools/UniversalSqueakerAudioBindingTests') -c Release --no-restore -m:1 `
            -p:EatFixtureTarget=MissingChew "-p:OutputPath=$eatOutput/" "-p:IntermediateOutputPath=$eatObj/"
        if ($LASTEXITCODE -ne 0) { throw 'Eat missing-target fixture build failed' }
        & (Join-Path $eatOutput 'UniversalSqueakerAudioBindingTests.exe') --db-eat-missing
    }
# Gate 26: result notifications cannot turn a still-true state into an event opportunity.
Invoke-Check 'D-B Result routes only to explicit fixed-contract consumers (truly Eat-only pack included)' `
    'dotnet run --no-restore --project tools/UniversalSqueakerAudioBindingTests -c Release -- --db-result-routing' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--db-result-routing') }
# Gates 27-29: all fifteen actual notification/fact callers, inactive gate and local missing-capability isolation.
Invoke-Check 'D-C fifteen native-shape production callers and owner lifecycles' `
    'AudioBindingTests --dc-native' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--dc-native') }
Invoke-Check 'D-C inactive runtime/unselected qualification and actual GLOBAL Dev Equip menu' `
    'AudioBindingTests -c Release --dc-unselected; -c Dev --equip-dev-entry (separate processes)' `
    {
        Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--dc-unselected')
        if ($LASTEXITCODE -ne 0) { throw 'D-C unselected fixture failed' }
        Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Dev' -Arguments @('--equip-dev-entry')
    }
Invoke-Check 'D-C physically missing Wounded and melee marker isolates only those mechanisms' `
    'build DcFixtureTarget=MissingWoundedAndMeleeMarker with isolated output/obj; run --dc-missing' `
    {
        $dcOutput = Join-Path $logs 'variants/dc/MissingWoundedAndMeleeMarker'
        $dcObj = Join-Path $logs 'variants/obj/dc/MissingWoundedAndMeleeMarker'
        dotnet build (Join-Path $root 'tools/UniversalSqueakerAudioBindingTests') -c Release --no-restore -m:1 `
            -p:DcFixtureTarget=MissingWoundedAndMeleeMarker "-p:OutputPath=$dcOutput/" "-p:IntermediateOutputPath=$dcObj/"
        if ($LASTEXITCODE -ne 0) { throw 'D-C missing-target fixture build failed' }
        & (Join-Path $dcOutput 'UniversalSqueakerAudioBindingTests.exe') --dc-missing
    }
# Gates 30/31: approved Core Work return branches and one physically absent implementation.
Invoke-Check 'Core Work exact processes, current phase/owner/config and care lifecycles' `
    'AudioBindingTests -c Release --work-core' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--work-core') }
Invoke-Check 'Core Work physically missing Sow leaves unrelated work usable' `
    'build WorkFixtureTarget=MissingSow with isolated output/obj; run --work-missing' `
    {
        $workOutput = Join-Path $logs 'variants/work/MissingSow'
        $workObj = Join-Path $logs 'variants/obj/work/MissingSow'
        dotnet build (Join-Path $root 'tools/UniversalSqueakerAudioBindingTests') -c Release --no-restore -m:1 `
            -p:WorkFixtureTarget=MissingSow "-p:OutputPath=$workOutput/" "-p:IntermediateOutputPath=$workObj/"
        if ($LASTEXITCODE -ne 0) { throw 'Core Work missing-Sow fixture build failed' }
        & (Join-Path $workOutput 'UniversalSqueakerAudioBindingTests.exe') --work-missing
    }
# Gates 32-37: current r2 entry points already exercised by the refactor's existing fixtures.
Invoke-Check 'r2 whole production Core without platform references' `
    'dotnet run --no-restore --project tools/UniversalSqueakerRefactorCoreTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerRefactorCoreTests' -Configuration 'Release' }
Invoke-Check 'r2 actual GameRuntime exact canonical-domain adapter' `
    'dotnet run --no-restore --project tools/UniversalSqueakerDomainAdapterTests -c Release' `
    { Invoke-Tool -Name 'UniversalSqueakerDomainAdapterTests' -Configuration 'Release' }
Invoke-Check 'r2 Select/Eat/Attack and declared C# production through-chain' `
    'AudioBindingTests -c Release --refactor-through' `
    { Invoke-Tool -Name 'UniversalSqueakerAudioBindingTests' -Configuration 'Release' -Arguments @('--refactor-through') }
Invoke-Check 'r2 ordinary queued edits versus physical atomic commit (Scribe doubles)' `
    'SettingsMigrationTests -c Release --commit-contract' `
    { Invoke-Tool -Name 'UniversalSqueakerSettingsMigrationTests' -Configuration 'Release' -Arguments @('--commit-contract') }
Invoke-Check 'r2 public player template through the production value loader' `
    'SettingsMigrationTests -c Release --public-template' `
    { Invoke-Tool -Name 'UniversalSqueakerSettingsMigrationTests' -Configuration 'Release' -Arguments @('--public-template') }
Invoke-Check 'r2 DLL-only external author examples against the built Release product' `
    'dotnet build tools/UniversalSqueakerExternalAuthorTests -c Release with explicit UniversalSqueakerArtifactPath' `
    { dotnet build (Join-Path $root 'tools/UniversalSqueakerExternalAuthorTests') -c Release --no-restore --nologo "-p:UniversalSqueakerArtifactPath=$(Join-Path $root 'dist/build/Release/UniversalSqueaker.dll')" }
if ($DevelopmentCarrier) { Write-Host '[verify] all integration checks passed (development carrier; not release evidence).' }
else { Write-Host '[verify] all checks passed.' }

if ($PackDev) {
    Write-Host '[pack] dev package'
    & (Join-Path $PSScriptRoot 'build-dev.ps1') -ProjectRoot $root -FerriteLibArtifactPath $carrierDll
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
