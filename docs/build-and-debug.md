# Build and debug

Select the fixed FerriteLib 0.8 package you intend to test with, then build US against that exact DLL. US does not build or ship FerriteLib. Game use also requires the external [Harmony mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`); the Lib.Harmony compilation reference does not supply the game runtime. Neither `0Harmony.dll` nor `FerriteLib.UiKit.dll` is bundled with US. The current identity and migration conditions are in the [0.8 consumer guide](fl08-consumer-migration-zh.md).

The RW17 basic-compatibility candidate explicitly maps both game versions to the shared `1.6` payload. It still targets net472 and compiles against Krafs.Rimworld.Ref 1.6.4871; the 1.7 API/IL review is a separate evidence layer. The release-gate repair uses the fixed `ferritelib/dist/release-gate-20261101/producer-release-r1/FerriteLib` input with `Private=false`; its actual US compiler/runtime pairing is recorded in [the fixed interface](../dist/release-gate-20261101/INTERFACE-r1.md). The earlier `rw17-basic-compat-20261030` input remains a preserved historical candidate. Native 1.7 sound and rendering remain awaiting human verification.

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

CI/release require approved `FERRITELIB_CARRIER_REPOSITORY`, `FERRITELIB_CARRIER_TAG` (0.8) and full `FERRITELIB_CARRIER_COMMIT` before checkout. They acquire the exact Release asset and pair it to that source; there is no 0.7 or default-branch fallback. A local Dev package does not satisfy this Release input.

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

The [r2 contract](specification.md) and [author guide](us-kernel-0.6-authoring-zh.md) describe the breaking declaration/configuration boundary. `UniversalSqueakerRefactorCoreTests` compiles the **whole production Core** without a platform reference or game double. `UniversalSqueakerDomainAdapterTests` checks the production configuration/domain adapter separately. AudioBinding tests exercise production callers/output with explicit platform doubles; SettingsMigration and Reset exercise production settings/ExposeData/atomic files with **Scribe doubles**. None proves native Scribe or real game Config.

## What verification covers

`scripts/verify-local.ps1` retains gate IDs1–37; gate16 is now a separate historical-input entry, so the current entry executes36 groups. Coverage includes Kernel/corpus, configuration, Release/Dev logs and product, selected-carrier/stage identity, current Schema Host/UI, adapter contracts and isolated missing-target builds. Gates32–37 retain whole-Core, exact-domain, through-chain, atomic commit, public-template and DLL-only author coverage. The workflows install .NET8 for the two net8.0 fixtures and .NET10 as SDK; neither is removed.

Gate15 scans the payload and Host against `tools/FerriteLib08.Stubs`. Host builds these through the Verse and InputLegacy roots: their ProjectReferences supply the same five doubles. No current csproj references `tools/FerriteLib.Stubs`; the separate definition-directory harness also references the0.8 sources. Historical gate16 is preserved in `scripts/verify-historical-inputs.ps1`, with its original recorded integrity expectations. It protects archived0.7 sources, not the current product. The producer and its package are never build targets. Standalone Reset/DefinitionDirectory projects remain separately callable; they are not silently counted in the current full-entry groups.

The list is an entry point, not proof that a candidate passed it. Reuse evidence only for applicable inputs. Pure logic, runtime doubles and native game are different evidence; a live Host is not proof that a widget avoided recovery.

### One restore owner, one build per identical tool input

CI/release call verify-local without `-NoRestore`: that entry restores the current graph, then all builds use `--no-restore`, including the Host's nested doubles. A manual `-NoRestore` asserts that the exact graph/properties/SDK are already restored; the mere presence of `project.assets.json` is not freshness evidence. Cache NuGet downloads, not bin/obj.

Within one invocation the ordinary AudioBinding Release is built once; every existing mode still gets a separate process, including failed-startup partial observation. Dev and five genuine missing-target compile variants remain distinct. SettingsMigration similarly reuses its build for ordinary/commit/template processes. The Host consumes the product already built by gates7/8 rather than rebuilding it. Release workflow retains the final clean Release identity build before packaging.

Every group writes a complete `gate-NN.log` under a new `dist/verify/<run>/`, with actual build/run arguments, configuration, fixed carrier, exit and elapsed seconds. `summary.json` keeps successful/failed/unrun IDs and source dirty state; failures keep full logs and print a usable tail. Gate15 can report failure and continue independent Core/adapter groups, but final success/stage still fails. `dotnet-info.log` and `restore.log` identify environment/preparation. `-LogDirectory` selects a fresh explicit directory; CI uploads logs even on failure. Same-run deterministic build-call reduction is not a measured speedup; timings are comparable only on the same inputs and environment.

For immutable local delivery, pass `-FerriteLibSourceRoot <fixed-checkout>` as well as the exact artifact path. Source identity, licence and stub scanner then come from that checkout. CI explicitly supplies its pinned checkout. Download cache keys include the real repo/tag/full commit; cache hits still run fresh release flag/digest, closed payload and source-pair validation. Missing futureRC2 coordinates never silently select RC1 or a moving branch.

## Focused game verification

Install the paired candidate only when game testing is authorized. Install and enable Harmony and the paired FerriteLib package separately, with both before Universal Squeaker in the mod list. Restart after replacing loaded DLLs; keep one external provider for each prerequisite.

Choose scenarios for the changed behavior: accurate Race/Xenotype selection and enablement, the original failing interaction, adjacent popup/scroll/cancel paths, settings save/reopen, or the affected sound action. Record candidate identity, game build, dependencies, language, screen/UI scale and expected/observed result without publishing personal paths or raw logs.

For UI, inspect the real page and native input. For audio, test production dispatch and hearing separately from preview. For a new domain, check admission after restart. Do not rerun accepted historical matrices without a changed premise.

Current UI limits are in [the UI contract](openspec/specs/ui-consumption/spec.md); dated evidence is scoped in [history](history/README.md).
