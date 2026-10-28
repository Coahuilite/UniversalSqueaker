param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($ProjectRoot)
# Historical gate16: preserved original archived0.7 snapshot integrity, not current0.8 coverage.
        $frozenStubs = @(
            @{ Path = 'tools\FerriteLib.Stubs\Stubs\VerseStub\VerseStubs.cs'; Sha = 'e36efa95fbbeea4ddd1c7b8a6df9d0e46c90049c2d8e5ac9d985be3c78905822' },
            @{ Path = 'tools\FerriteLib.Stubs\Stubs\VerseStub\VerseStubs.RimWorld.cs'; Sha = 'ad8cee6e7bfec49af374d536fae1549ce0d102a8887c4031081ac941d1e95a42' },
            @{ Path = 'tools\FerriteLib.Stubs\Stubs\UnityEngineStub\UnityStubs.cs'; Sha = '5a8d2ac4b4d83989b6e1bfb802ad652c56c6bffe2d32686bc3cf48d112b45086' },
            @{ Path = 'tools\FerriteLib.Stubs\Stubs\UnityEngineImGuiStub\ImGuiStubs.cs'; Sha = '81def94dc748fc687f69181bca292eba9ef1e47a00d3603afcb35d5a97b6d75a' },
            @{ Path = 'tools\FerriteLib.Stubs\Stubs\UnityEngineTextRenderingModuleStub\TextRenderingStubs.cs'; Sha = '2151e86613a4e6a14ccb42857f5ed25ef679b9dcbf250ce96cdfb0bef5e0934d' }
        )

        foreach ($entry in $frozenStubs) {
            $file = Join-Path $root $entry.Path
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
                throw "frozen stub snapshot file missing: $($entry.Path). Re-take the snapshot from the accepted carrier tree (d5af4d8), never edit it in place."
            }

            $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()
            if ($actual -ne $entry.Sha) {
                throw "frozen stub snapshot drift at $($entry.Path): sha256 $actual, the frozen handoff recorded $($entry.Sha). The snapshot must equal the carrier's bytes; refresh it only from a NEW accepted freeze."
            }
        }

Write-Host 'Historical gate16 archived0.7 snapshot integrity passed.'
