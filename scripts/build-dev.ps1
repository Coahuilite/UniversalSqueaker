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

# Release, deliberately, even though this is the dev path: the carrier is a different mod with its own
# flavor axis, and a US rehearsal must link the same bytes a player will install. Dev and Release share
# one carrier OutputPath, so --no-incremental is what makes "I built it" mean "the bytes at that path
# are the configuration I named" (measured 2026-09-07: after a carrier -c Dev build the payload at the
# shared path is a Dev assembly, and nothing downstream could tell). The release channel now asserts a
# Release carrier outright; this is the same rule, applied where it can still be fixed cheaply.
& dotnet build $carrierProjectFile -c Release --no-incremental --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw 'FerriteLib carrier Release build failed.'
}

& (Join-Path $PSScriptRoot 'pack-dev.ps1') -ProjectRoot $root
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
