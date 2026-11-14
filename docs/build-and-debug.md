# Build and debug

## Current0.6.x integration: adopted, blocked before dual-target delivery

The current scope is [FL0.9 dual-target adoption](openspec/changes/us06-fl09-dual-target/proposal.md).
The clean integration retains0.6.x `97aafd3` and the complete r5 `2b1a215` history. Its engineering tree
still equals r5: the current csproj, LoadFolders and stager are **RW1.6-only**, so it is not yet a
complete0.6.x push candidate. The occupied0.6.x and native1.7 worktrees are not integration inputs.

The already-remote `48b8fab` provides a reusable native1.7/net10 project, four explicit compiler
references (`Assembly-CSharp`, `UnityStubs`, `RimWorld`, `Waylith`), Private=false, and a stager that
re-roots shared source1.6 content into package1.7. Reuse only those target/build seams as needed;
its unrelated UI/diagnostics redesign and occupied-tree changes are not implicitly adopted. Keep
one shared production source and the original r5 no-attach fallback; close both selected compiler
references and real1.6/1.7 LoadFolders/payloads before delivery.

Required next inputs: FL owner's fixed ordinary0.9 RW1.7/net10 DLL and its exact1.7 reference directory,
plus an approved immutable0.9 carrier acquisition source usable by CI. The current CI helper requires
published0.8 repo/tag/full-commit coordinates; that cannot validate0.9. At the2026-11-14 read, FL0.9
had only privacy-workflow runs and no published0.9 Release carrier. Do not create a tag/Release to
work around the dependency or point CI at a moving branch. Normal0.6.x push triggers CI/privacy
checks, caches and log artifacts; Release is tag-only (`v*`), not a branch-push action.

### Preserved r5 RW1.6 engineering route

The independent `feat/rw16-fl09-display-snapshot-20261111` branch consumes **FL API [0.9.0,0.10.0)**,
net472/x64 against RimWorld1.6.4871. Select the FL owner's fixed ordinary RW1.6 Release DLL explicitly;
do not use Neutral/net10, the other US tree, or an old0.8 DLL. The bootstrap/resolver/readiness gates
enforce this new range only here. US still has Private=false and never builds or bundles FL.

```powershell
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Release --no-incremental -p:FerriteLibArtifactPath=<fixed-FL09-net472-DLL>
```

The existing RecordFerriteLibReference target records the exact resolved compiler input. Preserve
the fixed source commit, ordinary Release configuration, both actual MVIDs and the existing receipt.
Build the existing KernelHost consumer against the same FL and new US; select the affected
`ordinary getters`, `manifest`, `dynamic` and `SettingsWindowShellCarriesTheFailureContract` lanes.
They cover original/fallback callers with identified platform doubles, not the new production
participant or native performance. No new test cases/probes are introduced.

The actual factory is GameSettingsWindow → UsKernelSettingsHost.Create → b-actions. Public creation
enrolls the concrete production source; unknown sources remain unregistered. Its private CreateCore
retains a no-attach branch; the real source's attachment may also be safely disposed for the same-generation
off path. No player toggle/menu/guard bypass is added. Exact native adoption timing and the off transition
are recorded by the runtime owner before matched samples, never replaced with an old0.8 pair.
See [UI-10](openspec/specs/ui-consumption/spec.md#requirement-ui-10-生产-f09-显式保留准备原生操作保持实时)
and its [implementation tasks](openspec/changes/rw16-fl09-display-snapshot/tasks.md).
An API-only carrier reporting Fallback/legacy-layout can establish compilation, not RETAIN performance.

## Retained0.8 baseline history

The following records keep their old branch/artifact identities and are not this branch's FL0.9 build input.


Select the fixed FerriteLib 0.8 package you intend to test with, then build US against that exact DLL. US does not build or ship FerriteLib. Game use also requires the external [Harmony mod](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`); the Lib.Harmony compilation reference does not supply the game runtime. Neither `0Harmony.dll` nor `FerriteLib.UiKit.dll` is bundled with US. The current identity and migration conditions are in the [0.8 consumer guide](fl08-consumer-migration-zh.md).

The RW17 basic-compatibility candidate explicitly maps both game versions to the shared `1.6` payload. It still targets net472 and compiles against Krafs.Rimworld.Ref 1.6.4871; the 1.7 API/IL review is a separate evidence layer. The release-gate repair uses the fixed `ferritelib/dist/release-gate-20261101/producer-release-r1/FerriteLib` input with `Private=false`; its actual US compiler/runtime pairing is recorded in [the fixed interface](../dist/release-gate-20261101/INTERFACE-r1.md). The earlier `rw17-basic-compat-20261030` input remains a preserved historical candidate. Native 1.7 sound and rendering remain awaiting human verification.

## Published FL rc2 Release baseline

The ordinary fixed input is [Coahuilite/FerriteLib v0.8.0-rc2](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc2),
from full source commit `197ff6b3ae99bb3e957e22eaa8be70aca5858447` and CI Release run `42591902045`.
Select the actual asset `FerriteLib-v0.8.0-rc2.zip` (asset ID `726883338`), whose server/download digest is
`sha256:97cd4ac4d6ea1412305ae5010912992bc3102390b0aff19fb4b1c5f1c1cc96f0`.
Its Release DLL is API0.8.0, MVID `b4844c02-5647-4225-b6c8-921c6f589fc0`, with SHA-256
`0511bee4de1f3c77f6aa5fa3b01c27e028d97559d15fad5b56787e71be82e159`.

US source `f9f1f93cf294ac568a3bbf10e0448c62d687fbd8` actually compiled against those downloaded bytes.
The new ordinary Release DLL is MVID `9569c393-c2f6-426c-8b21-672206b96d55`; its compiler receipt and
both original Host runs identify the same FL. Product/Host builds have zero warnings/errors;
ordinary getters (32 assertions), manifest (479) and the original Release stager passed. Both DLLs have no CodeView
entry or observed absolute-path strings. This is product-DLL/platform-double evidence, not native acceptance.

The complete local fixed pair, original downloaded ZIP, exact Source Forms and identity are at
`dist/fl08-prerelease-baseline-20261109/rc2-release-r1/` (`USE-zh.md`, `IDENTITY.json`). These are local
outputs; a public checkout can obtain the same FL from the release link above and the US source from
the full source commit. Later adoption-document/workflow commits do not relabel this binary's source.
Pass its exact `game-release-pair/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll` as ArtifactPath;
retain Private=false and [0.8.0,0.9.0). US is not independently released by this baseline.

## US06 Kernel maintenance candidate

The separate [US06 maintenance candidate r2](../dist/us06-kernel-maintenance-20261109/a001-r2/USE-zh.md)
contains the new B-source Release build and the unchanged published FL rc2 input above.
`game-release-pair` is the usable pair; `support/US-source-form.zip` and `support/US-working.diff`
identify this uncommitted implementation over `d2c02db547373a93a787a981d9a7c7b354d59f9d`.
The assembly uses a task-local dirty informational identity, not the old release's source commit.
`IDENTITY.json` and `EVIDENCE-zh.md` distinguish actual compiler/stager/Host evidence from pending native use.
The retained A001 r1 candidate has a stale-current-slot diagnostic regression and is withdrawn from use;
r2 restores the original current Response/deadline view while keeping frozen decision evidence separate.

Settings maintenance now runs from the ready Mod's main-thread `Verse.Root.Update` postfix, once per
Unity frame, including paused and no-map periods. Long events, active Scribe and destroyed roots wait
for a safe update. Windows no longer supply periodic saves; closing/Retry/explicit Flush retain their
original contracts. Technical diagnostics separate the latest decision, current slot and latest
maintenance; adopted W/phase inputs are saved at scheduling, not reconstructed by readers.
Real 1.6 Harmony scheduling, UI-free debounce, shutdown/loading transitions and capture/feedback remain
native acceptance items. This local candidate does not install, publish, update the retained Dev pair,
or change the separate 0.9 line.

## Historical response-a001 volume candidate

The isolated [responsiveness candidate](../dist/us06-ui-responsiveness-20261110/a001/USE-zh.md)
built its then-current B source against the same fixed published FL rc2 Release. Both global-volume
bindings now invalidate only after the production setter's adopted volume changes; an optional
uncached readback preserves independent legacy sources without a new prerequisite. Live editing,
clamping/tolerance, the 0.35-second save owner and the maintenance/diagnostics fixes remain unchanged.
The [scoped analysis](development/ui-responsiveness-20261110.md) separates source paths, inherited
native timings and missing F03/F05 measurements. The pair includes exact Source Forms and dirty
diffs; the original maintenance a001-r2 remains a complete fallback. Native rendering/input and
performance were not run at that original delivery; later native comparisons are in the checkpoint below.

## Native-r3 performance checkpoint

The [current F06–F09 engineering record](development/ui-attack-20261110.md) owns the accepted engineering /
partial native scope, outstanding performance, next owner and local private evidence archive.
Use the fixed [native-a001-r3 pair](../dist/usfl08-ui-attack-20261110/a001-native-r3/USE-zh.md) and its
[IDENTITY](../dist/usfl08-ui-attack-20261110/a001-native-r3/IDENTITY.json), not pre-native `a001-r3` or
published rc2. Source Forms and compiler receipt belong to that immutable pair. The following focused
existing Host commands were run with its exact DLLs; they are not commands to execute during closeout.

From the US repository root, set `$HostExe` to the already-built fixed-FL consumer executable carrying
that pair and its platform doubles. The recorded local executable was under `host-native-r1` despite
containing native-r3 DLLs; directory names do not identify the carrier. Select affected entries:

```powershell
& $HostExe '--lane=ordinary getters'
& $HostExe '--lane=manifest'
& $HostExe '--lane=dynamic'
& $HostExe '--lane=SettingsWindowShellCarriesTheFailureContract'
& $HostExe '--lane=existing diagnostics'
```

The corresponding native-r3 logs (32/479/55/13/550 assertions) and actual build recipes are in the
[private archive index](../dist/usfl08-ui-attack-20261110/handoff-evidence-local/INDEX.md).
Spaces in `ordinary getters` and `existing diagnostics` are significant; using the log filename's
hyphens as lane names does not select those entries. Original source is
`tools/UniversalSqueakerKernelHostTests/Current/Program.cs`. These are platform-double checks, not native FPS.
The older `--lane=preservation` failure is retained in the archive: its old `set-tab=Packs` fixture fails
before the click, in both pre-native r2/r3. Do not weaken it or call the combined lane green.

## Retained accurate 1.6 Dev acceptance pair

The separate instrumented local pair is [vc-us-a002-r2](../dist/rw16-validation-closure-20261109/us-a002-r2/USE-zh.md).
Keep it unchanged for Dev use and whole-pair recovery; its old local Release variant is not the published rc2 baseline above.
Its default `game-dev-pair` actually consumes the CO-cleared FL08 A002 Dev carrier (MVID
`bf3ca125-fb06-46a1-b3d5-52289260434e`) with a freshly compiled US Dev and the existing compiler-reference
receipt. Both Dev DLLs now have neutral CodeView paths and no observed personal-path text. The
ordinary `game-release-pair` reuses the accepted, unchanged US/FL Release files and evidence; it was
not rebuilt or rerun in this continuation. Both configurations are net472/x64 for accurate1.6.
The blocked `us-a002-r1` Dev is preserved as an intermediate, not relabelled as a finished candidate.

O5 separates the current production `packs-keyword`/`b-query` edit from an explicitly fixture-authored
field exercising the retained `search-text` registry/source contract. Original held-property,
Live-count, draft, palette/Host/editor identity, conflict, Cancel-zero and normalized request/read-model/
Accept-no-repeat assertions remain. The O5 sample supplies its existing complete pack facts; it does
not invent query counters or production normalization. The only earlier US product repair uses the
net472-compatible `String.Split(char[])` overload in Roster's pack-source projection. This continuation
changes no US product/fixture source, API, default, Live policy or use condition.

New Dev product/Host builds have zero warnings/errors; actual new-US/new-FL O5 passed32 and the original
manifest/bilingual-help/layout entry passed479, with compiler receipt and original staging gates passing.
Release retains r1's32/479 on its unchanged exact DLLs. These are product-DLL/platform-double checks,
not full Host-suite, game/input/IME acceptance or the original32-Host-pass sampling gate.
The [evidence sheet](../dist/rw16-validation-closure-20261109/us-a002-r2/EVIDENCE-zh.md) identifies actual
inputs, reuse and original failures. The candidate includes paired ZIPs, identities, current US Source
Form and separate FL Dev/Release Source Forms matching their actual build origins.

The [A001 saving follow-up](../dist/rw16-validation-closure-20261109/us-a001-r1/EVIDENCE-zh.md) retains
accepted SettingsMigration Release results: commit27/0 and ordinary133/0, with saving source/fixture
unchanged. FL's accepted independent checks retain their original inputs. PM selection and accurate1.6
rendering/input/IME/audio/lifetime, B-browse/A-record, Report continuity and original32-Host-pass/partial
conditions stay with their existing owners. Original a001-r2/r1/c3-dev/C4/r6 recovery pairs remain unchanged.

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

For an isolated fixed delivery, append `-OutputRoot 'dist/<fresh-attempt>'` to `build-dev.ps1`
or `pack-dev.ps1`. Build/obj/package outputs must not already exist there; omitting the option retains
the original build slots and `dist/dev` workflow. `build-dev` delegates raw selection to `pack-dev`,
which resolves once before building. The independently callable stager validates the selected input
again after the build and checks the compiler's recorded carrier identity; no caller-supplied bypass
or cross-invocation cache is accepted. Configuration/identity stamps still use the existing pwsh child
reader so packaging does not retain loaded input assemblies. No Node runtime is required.

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

### Select the checks affected by a local change

From this checkout's root, these existing entries exercise the production Core without a FerriteLib carrier:

```powershell
dotnet run --project tools/UniversalSqueakerKernelTests -c Release
dotnet run --project tools/UniversalSqueakerRefactorCoreTests -c Release
```

The first checks selector contracts, the frozen routing corpus and determinism; the second checks the current response, configuration and authoring contracts. Run the entry relevant to the changed contract, and run both when both are affected. Neither checks native adapters or the game. For a changed adapter, carrier or package, also exercise its affected existing path; keep the complete `verify-local.ps1` entry for its CI and release purpose. A documentation-only edit does not by itself invalidate unchanged code evidence.

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
