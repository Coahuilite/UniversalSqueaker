# The one implementation of "read one named attribute off these bytes", shared by the staging engine
# (scripts/stage-package.ps1) and the carrier gate (scripts/verify-local.ps1 gate 6). The default is the
# configuration stamp ("which configuration produced these bytes"); AssemblyInformationalVersion is the
# other caller, because it embeds the source commit and is how a payload gets attributed to a checkout.
# A second copy of this probe is exactly the shape FL→US round 2 S4 counted in the archive writers, so
# nobody gets to grow their own.
#
# Read in a CHILD process on purpose: callers include packaging paths that copy or rebuild this very DLL
# moments later, and an Assembly.LoadFile in the caller's session would hold a handle on it for the rest
# of the run. MetadataReader is not the route either - the Store build of PowerShell ships a trimmed
# System.Reflection.Metadata whose PEReader has no GetMetadataReader (measured 2026-09-07) - and a gate
# that works only on one host is worse than no gate.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    # Attribute type name to read. Default keeps every existing caller's contract byte-for-byte.
    [string]$AttributeName = 'AssemblyConfigurationAttribute'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolved = (Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue)
if ($null -eq $resolved) { throw "Cannot read a stamp from a missing file: $Path" }

$env:UNIVERSALSQUEAKER_STAMP_TARGET = $resolved.Path
try {
    $shell = Join-Path $PSHOME 'pwsh.exe'
    if (-not (Test-Path -LiteralPath $shell -PathType Leaf)) { $shell = 'pwsh' }
    $probe = '$a=[System.Reflection.Assembly]::LoadFile($env:UNIVERSALSQUEAKER_STAMP_TARGET); foreach ($t in [System.Reflection.CustomAttributeData]::GetCustomAttributes($a)) { if ($t.AttributeType.Name -eq "' + $AttributeName + '") { $t.ConstructorArguments[0].Value; break } }'
    $out = @(& $shell -NoProfile -NonInteractive -Command $probe)
    if ($LASTEXITCODE -ne 0) { throw "Child probe failed (exit $LASTEXITCODE) reading the stamp of $Path." }
    (@($out | ForEach-Object { [string]$_ } | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join '')
} finally {
    Remove-Item Env:UNIVERSALSQUEAKER_STAMP_TARGET -ErrorAction SilentlyContinue
}
