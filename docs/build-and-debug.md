# Build and debug

Select the fixed FerriteLib 0.8 package you intend to test with, then build US against that exact DLL. US does not build or ship FerriteLib. The current identity and migration conditions are in the [0.8 consumer guide](fl08-consumer-migration-zh.md).

## Local Dev workflow

From the US repository root:

```powershell
$carrier = '<fixed-package>/1.6/Assemblies/FerriteLib.UiKit.dll'
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath $carrier -DevelopmentCarrier
pwsh -NoProfile -File scripts/build-dev.ps1 -FerriteLibArtifactPath $carrier
```

The carrier must already exist. Obtain it from its owner or the agreed paired delivery; do not replace a frozen carrier while another build or test uses it. These commands build/test and stage files; run them only within an authorized development task.

- `FerriteLibArtifactPath` is shared by scripts and MSBuild. Relative paths resolve against the US root.
- Scripts and production MSBuild require an explicit carrier; no old sibling fallback remains. Pass `-p:FerriteLibArtifactPath=...` to direct builds. Scripts reject a carrier outside `[0.8.0,0.9.0)`.
- `-DevelopmentCarrier` selects Dev integration and the Dev Host lane. Without it, verification checks the selected Release carrier and its required source attribution.
- US builds into `dist/build/<Configuration>`. `build-dev.ps1` stages `dist/dev/UniversalSqueaker`; `pack-dev.ps1 -Zip` additionally creates an archive.
- The compiler records the carrier identity. Staging rejects a different selected DLL and records the pairing in `version.txt`; failed staging preserves the previous delivered folder.

Explicit Release verification:

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath '<fixed-release-package>/1.6/Assemblies/FerriteLib.UiKit.dll'
```

CI/release still acquire the published 0.7 carrier, which is not a usable input for this 0.8 consumer. The maintainer must arrange an available fixed 0.8 release before updating that acquisition pin; no new tag or asset is assumed here.

## What verification covers

The current `scripts/verify-local.ps1` defines 31 gates. Keep its commands authoritative instead of copying them into every guide. Use the affected entry points rather than repeating unrelated valid matrices. Coverage includes Kernel/corpus, configuration, logs, selected-carrier/build/package identity, UI logic/Host, adapter contracts and isolated missing-target cases.

Gate 15 scans the payload and Host against the explicit 0.8 platform doubles in `tools/FerriteLib08.Stubs`, built into `dist/fl08-migration-a001/stubs/<Configuration>`. Gate 16 retains the archived 0.7 snapshot integrity check; it does not certify the new 0.8 runtime. The producer and its held package are never build targets.

Pure logic, runtime doubles and native game behavior are different evidence. A live Host does not prove a widget executed rather than entered recovery; assert the actual affected output and write chain.

## Focused game verification

Install the paired candidate only when game testing is authorized. Restart after replacing loaded DLLs; keep exactly one FerriteLib carrier active.

Choose scenarios for the changed behavior: accurate Race/Xenotype selection and enablement, the original failing interaction, adjacent popup/scroll/cancel paths, settings save/reopen, or the affected sound action. Record candidate identity, game build, dependencies, language, screen/UI scale and expected/observed result without publishing personal paths or raw logs.

For UI, inspect the real page and native input. For audio, test production dispatch and hearing separately from preview. For a new domain, check admission after restart. Do not rerun accepted historical matrices without a changed premise.

Current UI limits are in [the UI contract](ui-contract-0.5-zh.md); dated evidence is scoped in [history](history/README.md).
