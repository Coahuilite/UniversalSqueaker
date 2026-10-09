<#
.SYNOPSIS
    Writes the M6/M11 evidence footer for a US-side run, with the artifact line DERIVED from the configuration
    that was actually triggered.

.DESCRIPTION
    Why this file exists (task-28): the first T21 footers were produced by an inline snippet that hard-coded
    dist/build/Dev/... while every run was -c Release, so six logs carried one identical value that said
    nothing about any of them. The fix is structural rather than editorial: the caller states the
    configuration, and this script derives the artifact path from it. A future configuration cannot silently
    reuse another one's fingerprint, because there is no place to hard-code it.

    It also emits the two identity lines the M11 convention asks for, and it fingerprints the files that hold
    the pins (not only the mutated sources), so a reader can bind a log to the bytes that were in force.

    The convention follows FL tools/mutation/Invoke-Mutation.ps1: the artifact is written as
    "# artifact (K3, derived from the mutated file)" when the run was a mutation, and as
    "# artifact (derived from the configuration this run triggered: <cfg>)" otherwise. FL's engine is the
    reference implementation; this is the consumer-side writer, kept small on purpose.

.PARAMETER LogPath
    The log file to append to. Must exist; a missing log is refused rather than created, so a footer can never
    describe a run whose output nobody kept.

.PARAMETER Configuration
    The configuration the run used (Release or Dev). The artifact path is derived from this value.

.PARAMETER Command
    The command line that produced the log.

.PARAMETER ExitCode
    The process exit code.

.PARAMETER MutatedPath
    Zero or more repository-relative paths that were temporarily mutated. Their CURRENT fingerprints are
    recorded, and the footer says so - a restored file's hash is not the mutated hash, and pretending
    otherwise is the defect this convention exists to prevent.

.PARAMETER PinPath
    Extra repository-relative files to fingerprint (the defaults already include the us/* kind pin).

.EXAMPLE
    pwsh -NoProfile -File tools/evidence/Write-EvidenceFooter.ps1 -LogPath dist/t21-evidence/01-kernelhost.log `
        -Configuration Release -Command 'dotnet run ... -c Release' -ExitCode 0
#>
param(
    [Parameter(Mandatory = $true)][string]$LogPath,
    [Parameter(Mandatory = $true)][ValidateSet('Release', 'Dev')][string]$Configuration,
    [Parameter(Mandatory = $true)][string]$Command,
    [Parameter(Mandatory = $true)][int]$ExitCode,
    [string[]]$MutatedPath = @(),
    [string[]]$PinPath = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path -LiteralPath $LogPath)) {
    throw "the log '$LogPath' does not exist; refusing to write a footer for a run whose output was not kept."
}

function Get-Fingerprint([string]$relative) {
    $full = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $full)) { return "$relative (missing)" }
    $item = Get-Item -LiteralPath $full
    return "$relative sha256=$((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash) bytes=$($item.Length) mtime=$($item.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
}

# The artifact line is DERIVED, never hard-coded: one place computes it, from the configuration.
$artifact = "dist/build/$Configuration/UniversalSqueaker.dll"
$generatorHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$head = (& git -C $root rev-parse HEAD).Trim()
$dirty = (& git -C $root status --porcelain) -join ';'
$batchMaterial = "$Command|$Configuration|$ExitCode|$head|$dirty"
$batchHash = [BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($batchMaterial))).Replace('-', '')

$lines = @()
$lines += '--- M6 FOOTER ---'
$lines += "# generator: sha256=$generatorHash"
$lines += "# batch: sha256=$batchHash"
$lines += "run: $Command"
$lines += "exit: $ExitCode"
$lines += "git HEAD: $head"
$lines += "git dirty: $dirty"
$lines += "# artifact (derived from the configuration this run triggered: $Configuration): $(Get-Fingerprint $artifact)"
if ($MutatedPath.Count -gt 0) {
    $lines += '# mutated files (CURRENT state - the mutated state is not recoverable after a restore, and is not claimed):'
    foreach ($m in $MutatedPath) { $lines += "  $(Get-Fingerprint $m)" }
}
$defaultPins = @('tools/UniversalSqueakerUiLogicTests/UiSourceInvariantTests.cs')
$lines += '# pin carriers (the files that hold the assertions this run relied on):'
foreach ($p in ($defaultPins + $PinPath | Select-Object -Unique)) { $lines += "  $(Get-Fingerprint $p)" }
$lines += '# convention: FL tools/mutation/ writes the same K3 artifact line; the path is derived from the run'
$lines += '#   configuration here, so no configuration can silently reuse another one fingerprint.'

Add-Content -LiteralPath $LogPath -Value $lines
Write-Host "[evidence-footer] wrote $($lines.Count) lines to $LogPath (configuration=$Configuration, artifact=$artifact)"
