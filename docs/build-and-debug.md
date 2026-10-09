# Selecting FerriteLib and validating the settings redesign

US builds into `dist/build/<Configuration>`. Existing packers still produce installable mod folders under
`dist/dev` or `dist/release`, with `1.6/Assemblies/UniversalSqueaker.dll` inside. No FL DLL is shipped by US.
`build-dev.ps1` now reads a selected FL artifact and calls the existing packer; it never rebuilds FL.

`FerriteLibArtifactPath` is the common MSBuild/script input. Relative values are resolved against the US
repository root. **There is no default carrier**: `resolve-carrier.ps1` refuses an empty path, because the old
default silently selected the repository-root Release payload - the wrong artifact for a Dev rehearsal, and one
that ordinary FL builds no longer refresh. Select the DLL from the paired FL dev package or from an explicit FL
build output. (A bare `dotnet build` still falls back to `Directory.Build.props`' root-carrier default for
compatibility; every script and the paired workflow pass an explicit path.)
The actual compiler reference hash is embedded as `AssemblyMetadata("FerriteLib.SHA256", ...)`.
Staging rejects a different selected DLL and writes `carrier-sha256` into `version.txt`.

Gate 9 stages a disposable matching package, supplies a changed carrier as a negative control, and checks
that rejection preserves the previous package and the original carrier. The Dev Host lane checks repeated
native events and repeated publisher calls. Reverting its pass-based deduplication to text-based deduplication
was measured to fail that lane. Neither check substitutes for the game acceptance below.

## Paired Dev workflow

Run the first command from the FL repository, then the remaining commands from US:

```powershell
# FL repository: build and stage its development package.
pwsh -NoProfile -File scripts/pack-dev.ps1

# US repository: select the DLL from that exact FL package.
$carrier = '../ferritelib/dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll'
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath $carrier -DevelopmentCarrier
pwsh -NoProfile -File scripts/build-dev.ps1 -FerriteLibArtifactPath $carrier
```

`-DevelopmentCarrier` requires a Dev DLL and runs the Host harness in Dev, including repeated-click
diagnostic integration. Dirty FL work is allowed in this explicitly labeled integration mode. This is not
release evidence. Without that switch, verification requires a Release DLL attributed to the current clean
FL checkout and runs the Host harness in Release. An explicit Release build can be selected with:

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1 -FerriteLibArtifactPath '../ferritelib/dist/build/Release/FerriteLib.UiKit.dll'
```

For direct `dotnet build` or `dotnet run`, pass `-p:FerriteLibArtifactPath=...`. The Host harness forwards
that input to its nested production build. It still uses the sibling FL source tree for test stubs.
Serialize FL and US harness builds because those canonical stubs are shared. Do not rebuild or replace the
selected FL artifact while a consumer build/pack is using it.

## Actual game acceptance

The validation surface remains `docs/ui-redesign-0.7-zh.md`: implement a vertical settings-page slice,
separate US composition from generic library defects, and use the real page to validate the library.
The existing Packs race-row click defect is **not closed by a stub harness pass**.

Install the paired `dist/dev/FerriteLib` and `dist/dev/UniversalSqueaker` folders through the usual manual
workflow, ensuring exactly one FL carrier is active. Restart the game after replacing loaded DLLs. Record
the installed FL DLL hash and compare it with US `version.txt`'s `carrier-sha256`. Enable detailed logging
in the existing Diagnostics workspace, then:

1. Reproduce the failed race-row selection and compare it with the working filter.
2. Repeat the same click, click another row, scroll and retry, then repeat with a popup open/closed.
3. Record window dimensions, UI scale, language, expected/actual selection and the matching `ltrace: geometry`
   block. Each event pass has its own number; identical later clicks must not disappear from the log.
4. After a targeted fix, repeat the original failing scenario and the adjacent interactions.

`event-before` / `event-after` expose native consumption. `MouseDown -> Used` can be normal capture;
`Used -> Used` means an earlier element already consumed the event. The dump is evidence about the
instrumented queries, not a full command trace or a complete explanation by itself.

Report automated contracts, package identity, and real-game E2E separately. Until step 4 is observed in
RimWorld, the game defect remains pending, even if all 15 automated gates pass.
