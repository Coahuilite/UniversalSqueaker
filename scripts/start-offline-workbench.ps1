# Local standalone preview only. Does not bootstrap RimWorld or touch player Config/Saves.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $GameRoot,
    [Parameter(Mandatory)][string] $StateDirectory,
    [Parameter(Mandatory)][string] $AuthorDirectory,
    [string] $PackageRoot = $PSScriptRoot,
    [ValidateSet('zh-Hans','en')][string] $Language = 'zh-Hans',
    [string] $Page = 'coahuilite.universalsqueaker/behavior',
    [switch] $Background
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run with pwsh 7+, not Windows PowerShell.' }
$package = [IO.Path]::GetFullPath($PackageRoot)
$game = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\','/')
$state = [IO.Path]::GetFullPath($StateDirectory).TrimEnd('\','/')
$author = [IO.Path]::GetFullPath($AuthorDirectory).TrimEnd('\','/')
$server = Join-Path $package 'Tools/native17/FerriteLib.UiKit.Tools.Native17.dll'
$fl = Join-Path $package 'inputs/producer-p5-native5000/native17/FerriteLib.UiKit.dll'
$behavior = Join-Path $package 'provider/UniversalSqueaker.Preview.dll'
$mark = Join-Path $package 'provider/UniversalSqueaker.MarkPreview.dll'
foreach ($file in @($server,$fl,$behavior,$mark,(Join-Path $game 'Version.txt'))) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required local input missing: $file" }
}
if ((Get-Content -LiteralPath (Join-Path $game 'Version.txt') -Raw).Trim() -ne '1.7.5000 rev1275') {
    throw 'This fixed native reference requires exactly 1.7.5000 rev1275.'
}
if (Test-Path -LiteralPath $state) { throw 'Choose a fresh, isolated StateDirectory; existing state is never overwritten.' }
# Refuse the actual game directory or anything under it as either writable root.
$comparison = [StringComparison]::OrdinalIgnoreCase
foreach ($writable in @($state,$author)) {
    if ($writable.Equals($game,$comparison) -or $writable.StartsWith($game+[IO.Path]::DirectorySeparatorChar,$comparison)) {
        throw 'StateDirectory and AuthorDirectory must be outside the game installation.'
    }
}
$null = New-Item -ItemType Directory -Path $state
$null = New-Item -ItemType Directory -Path $author -Force
foreach ($name in @('Layout.Schema2.xml','Layout.DiagnosticsMark.Schema2.xml')) {
    $destination = Join-Path $author $name
    if (!(Test-Path -LiteralPath $destination)) { Copy-Item -LiteralPath (Join-Path $package "author/$name") -Destination $destination }
}
$arguments = @($server,'--game',$game,'--fl',$fl,'--state',$state,'--attach','--writable',
    '--registration',($behavior+'::UniversalSqueaker.Preview.UsBehaviorPreviewProvider'),
    '--registration',($mark+'::UniversalSqueaker.Preview.UsDiagnosticsMarkPreviewProvider'),
    '--author-directory',$author,'--page',$Page,'--width','1320','--height','900','--language',$Language)
if ($Background) { $arguments += '--background' }
# Windows paths cannot contain quotes. Quote each argument so paths with spaces remain one argument.
$quoted = @($arguments | ForEach-Object { '"'+$_+'"' })
$process = Start-Process -FilePath 'dotnet' -ArgumentList $quoted -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $state 'native-stdout.log') `
    -RedirectStandardError (Join-Path $state 'native-stderr.log')
$receipt = [pscustomobject]@{ process=$process.Id; package=$package; state=$state; author=$author; background=[bool]$Background; args=$arguments }
$receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $state 'launch.json')
$receipt