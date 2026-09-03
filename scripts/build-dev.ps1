param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# One-step local dev package: build the FerriteLib carrier payload, then US with the Dev
# configuration (US_DEV), then stage + zip. The flavor is guaranteed by construction - pack-dev never
# sees a foreign-flavor DLL from this entry.
#
# FerriteLib is a separate repository and a separate prerequisite mod (coahuilite.ferritelib). US only
# compiles against its built DLL; it never ships one. The sibling layout is a stated assumption of the
# whole workspace, the same one NGS already relies on for the game's own managed DLLs.
$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$carrierProjectFile = Join-Path (Split-Path -Parent $root) 'ferritelib\Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'

if (-not (Test-Path -LiteralPath $carrierProjectFile -PathType Leaf)) {
    throw "FerriteLib carrier project not found at $carrierProjectFile. US compiles against the sibling repo's payload; clone it beside this repository (or run its build first)."
}

& dotnet build $carrierProjectFile -c Dev
if ($LASTEXITCODE -ne 0) {
    throw "FerriteLib.UiKit Dev flavor build failed."
}

& dotnet build $projectFile -c Dev
if ($LASTEXITCODE -ne 0) {
    throw "Dev flavor build failed."
}

& (Join-Path $PSScriptRoot 'pack-dev.ps1') -ProjectRoot $root
if ($LASTEXITCODE -ne 0) {
    throw "pack-dev failed."
}
