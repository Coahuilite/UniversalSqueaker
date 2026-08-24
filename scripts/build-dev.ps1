param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# One-step local dev package: build the UiKit library and US with the Dev configuration (US_DEV), then stage + zip.
# The flavor is guaranteed by construction - pack-dev never sees a foreign-flavor DLL from this entry.
$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$uikitProjectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$projectFile = Join-Path $root 'Source\UniversalSqueaker\UniversalSqueaker.csproj'

& dotnet build $uikitProjectFile -c Dev
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
