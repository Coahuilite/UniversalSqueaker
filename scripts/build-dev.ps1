param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# One-step local dev rehearsal: put a Release carrier payload at the sibling path, then let pack-dev
# build and stage the Dev US assembly. The result is a folder you drop into Mods/ next to FerriteLib -
# no archive, because a rehearsal is not an artifact anybody has to verify (see pack-dev's -Zip switch).
#
# FerriteLib is a separate repository and a separate prerequisite mod (coahuilite.ferritelib). US only
# compiles against its built DLL and never ships one. The sibling layout is a stated assumption of the
# whole workspace, the same one the game's own load order depends on.
$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$carrierProjectFile = Join-Path (Split-Path -Parent $root) 'ferritelib\Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'

if (-not (Test-Path -LiteralPath $carrierProjectFile -PathType Leaf)) {
    throw "FerriteLib carrier project not found at $carrierProjectFile. US compiles against the sibling repo's payload; clone it beside this repository (or run its build first)."
}

# ---------------------------------------------------------------------------------------------------
# STOP AND READ: the command below REBUILDS THE SHARED CARRIER.
#
# Criterion - the same one scripts/verify-local.ps1 applies to its retry hints: does the command WRITE
# ../ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll, the payload every sibling checkout compiles
# against? This one does, so it is not a setup step; it is the CARRIER OWNER'S DELIVERY ACTION.
#
#   (a) It REBUILDS/FORCE-REBUILDS the shared carrier - a delivery step, not a repair.
#   (b) It REPLACES the current frozen identity: the SHA-256 moves, so a hash a downstream consumer
#       already verified stops describing these bytes. The delivery step ends with the stale-PDB
#       removal, the re-verification and a RE-ISSUED FREEZE NOTICE.
#
# A verification-only session must NOT run this script in order to "build US": US references the carrier
# through a HintPath and never ships one, so BUILDING US DOES NOT REQUIRE REBUILDING FL. On 2026-09-22
# exactly this command was copied out of a red gate's hint by a consumer-side verification session and
# moved the frozen hash - this script is the same trap's third face (the other two: verify-local's own
# carrier gate, and a gate's rebuild hint).
#
# Release, deliberately, even though this is the dev path: the carrier is a different mod with its own
# flavor axis, and a US rehearsal must link the same bytes a player will install. Dev and Release share
# one carrier OutputPath, so --no-incremental is what makes "I built it" mean "the bytes at that path
# are the configuration I named" (measured 2026-09-07: after a carrier -c Dev build the payload at the
# shared path is a Dev assembly, and nothing downstream could tell). The release channel now asserts a
# Release carrier outright; this is the same rule, applied where it can still be fixed cheaply.
# ---------------------------------------------------------------------------------------------------
$carrierDeliveryNotice = @(
    '[carrier] the next step rebuilds ../ferritelib/1.6/Assemblies/FerriteLib.UiKit.dll - the SHARED'
    '[carrier]   payload every sibling checkout compiles against - and REPLACES its frozen identity:'
    '[carrier]   the SHA-256 moves and a consumer hash verified earlier stops describing these bytes.'
    '[carrier]   It is the CARRIER OWNER''S DELIVERY action and ends with the stale-PDB removal, the'
    '[carrier]   re-verification and a re-issued FREEZE NOTICE. Building US does NOT require it: US'
    '[carrier]   references the carrier through a HintPath and never ships one.'
)
$carrierDeliveryNotice | ForEach-Object { Write-Host $_ }

& dotnet build $carrierProjectFile -c Release --no-incremental --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw 'FerriteLib carrier Release build failed.'
}

& (Join-Path $PSScriptRoot 'pack-dev.ps1') -ProjectRoot $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
