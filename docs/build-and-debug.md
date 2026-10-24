# Build and debug

Select the fixed FerriteLib 0.8 package you intend to test with, then build US against that exact DLL. US does not build or ship FerriteLib. Game use also requires the external [Harmony mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`); the Lib.Harmony compilation reference does not supply the game runtime. Neither `0Harmony.dll` nor `FerriteLib.UiKit.dll` is bundled with US. The current identity and migration conditions are in the [0.8 consumer guide](fl08-consumer-migration-zh.md).

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

## Isolated candidates and the r2 boundaries

For a task that must preserve existing build/stage directories, pass independent `BaseIntermediateOutputPath`, `MSBuildProjectExtensionsPath` and `OutputPath` to MSBuild. Pass the same isolation roots through nested stub builds; keep `TEMP`, `TMP`, `DOTNET_CLI_HOME` and NuGet caches inside the authorized task output. In a process that must not generate development certificates, set `DOTNET_GENERATE_ASPNET_CERTIFICATE=false` before starting dotnet.

The standard staging engine also accepts an explicit isolated DLL without changing existing channel defaults:

```powershell
pwsh -NoProfile -File scripts/stage-package.ps1 `
  -StageDir 'dist/<candidate>/UniversalSqueaker' `
  -VersionLabel '<candidate-label>' -CommitLabel '<source-commit>' `
  -BuildFlavor dev -PayloadDllPath '<isolated-output>/UniversalSqueaker.dll' `
  -FerriteLibArtifactPath $carrier
```

`-RequireReleaseIdentity` additionally requires a Release carrier and publishable version identity. Compiling a Release US DLL against a fixed Dev carrier is a technical build, not a publishable paired release. No install or publication follows from staging.

The [r2 contract](us-kernel-0.6-refactor-zh.md) and [author guide](us-kernel-0.6-authoring-zh.md) describe the breaking declaration/configuration boundary. `UniversalSqueakerRefactorCoreTests` compiles the **whole production Core** without a platform reference or game double. `UniversalSqueakerDomainAdapterTests` checks the production configuration/domain adapter separately. AudioBinding tests exercise production callers/output with explicit platform doubles; SettingsMigration and Reset exercise production settings/ExposeData/atomic files with **Scribe doubles**. None proves native Scribe or real game Config.

## What verification covers

The current `scripts/verify-local.ps1` defines 31 gates. Keep its commands authoritative instead of copying them into every guide. Use the affected entry points rather than repeating unrelated valid matrices. Coverage includes Kernel/corpus, configuration, logs, selected-carrier/build/package identity, UI logic/Host, adapter contracts and isolated missing-target cases.

Gate 15 scans the payload and Host against the explicit 0.8 platform doubles in `tools/FerriteLib08.Stubs`, built into `dist/fl08-migration-a001/stubs/<Configuration>`. Gate 16 retains the archived 0.7 snapshot integrity check; it does not certify the new 0.8 runtime. The producer and its held package are never build targets.

The gate list is an entry point, not a claim that an arbitrary candidate has passed all 31 gates. Reuse evidence only while its inputs remain applicable, and record focused modes' actual arguments and assertion output. Pure logic, runtime doubles and native game behavior are different evidence. A live Host does not prove a widget executed rather than entered recovery; assert the actual affected output and write chain.

## Focused game verification

Install the paired candidate only when game testing is authorized. Install and enable Harmony and the paired FerriteLib package separately, with both before Universal Squeaker in the mod list. Restart after replacing loaded DLLs; keep one external provider for each prerequisite.

Choose scenarios for the changed behavior: accurate Race/Xenotype selection and enablement, the original failing interaction, adjacent popup/scroll/cancel paths, settings save/reopen, or the affected sound action. Record candidate identity, game build, dependencies, language, screen/UI scale and expected/observed result without publishing personal paths or raw logs.

For UI, inspect the real page and native input. For audio, test production dispatch and hearing separately from preview. For a new domain, check admission after restart. Do not rerun accepted historical matrices without a changed premise.

Current UI limits are in [the UI contract](ui-contract-0.5-zh.md); dated evidence is scoped in [history](history/README.md).
