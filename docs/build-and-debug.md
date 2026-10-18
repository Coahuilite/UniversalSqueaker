# Build and debug

Select the FerriteLib artifact you intend to test with, then build US against that exact DLL. US does not build or ship FerriteLib.

## Local Dev workflow

From the US repository root:

```powershell
$carrier = '../ferritelib/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll'
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath $carrier -DevelopmentCarrier
pwsh -NoProfile -File scripts/build-dev.ps1 -FerriteLibArtifactPath $carrier
```

The carrier must already exist. Obtain it from its owner or the agreed paired delivery; do not replace a frozen carrier while another build or test uses it. These commands build/test and stage files; run them only within an authorized development task.

- `FerriteLibArtifactPath` is shared by scripts and MSBuild. Relative paths resolve against the US root.
- Scripts reject an omitted carrier. Direct MSBuild retains a compatibility fallback in `Directory.Build.props`; pass `-p:FerriteLibArtifactPath=...` explicitly.
- `-DevelopmentCarrier` selects Dev integration and the Dev Host lane. Without it, verification checks the selected Release carrier and its required source attribution.
- US builds into `dist/build/<Configuration>`. `build-dev.ps1` stages `dist/dev/UniversalSqueaker`; `pack-dev.ps1 -Zip` additionally creates an archive.
- The compiler records the carrier identity. Staging rejects a different selected DLL and records the pairing in `version.txt`; failed staging preserves the previous delivered folder.

Explicit Release verification:

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath '../ferritelib/dist/build/Release/FerriteLib.UiKit.dll'
```

CI/release workflows select a pinned FerriteLib release asset through `scripts/acquire-ferritelib-carrier.ps1`; they do not rebuild the carrier's moving default branch.

## What verification covers

The current `scripts/verify-local.ps1` defines 16 gates. Keep its commands authoritative instead of copying them into every guide. Coverage includes pure Kernel/corpus, Config-copy lifecycle, settings migration, log protocol, selected-carrier identity, Dev/Release builds, package boundary, license, manifests, UI logic, production Host on stubs, UI boundary, referenced game-member stub coverage and frozen stub integrity.

Gate 15 scans the US payload and Host harness together against their exemption ledger. Tests use US's frozen `tools/FerriteLib.Stubs` snapshot; Gate 16 checks that snapshot against its recorded delivery. This is not rebuilding the carrier's current stubs.

Pure logic, runtime doubles and native game behavior are different evidence. A live Host does not prove a widget executed rather than entered recovery; assert the actual affected output and write chain.

## Focused game verification

Install the paired candidate only when game testing is authorized. Restart after replacing loaded DLLs; keep exactly one FerriteLib carrier active.

Choose scenarios for the changed behavior: accurate Race/Xenotype selection and enablement, the original failing interaction, adjacent popup/scroll/cancel paths, settings save/reopen, or the affected sound action. Record candidate identity, game build, dependencies, language, screen/UI scale and expected/observed result without publishing personal paths or raw logs.

For UI, inspect the real page and native input. For audio, test production dispatch and hearing separately from preview. For a new domain, check admission after restart. Do not rerun accepted historical matrices without a changed premise.

Current UI limits are in [the UI contract](ui-contract-0.5-zh.md); dated evidence is scoped in [history](history/README.md).