# FL 0.8 platform doubles for the US consumer harness

The `.cs` files are a source snapshot from FerriteLib commit
`564f78b1e88f5450aff075e5bd3f07db2a894c1f`,
`tools/FerriteLib.UiKit.Tests/Stubs`. They model the 1.6 Core/IMGUI/TextRendering/InputLegacy
and Verse/Harmony surfaces used by the fixed 0.8 DLL. They are test inputs, not game binaries.

A003 synchronizes `UnityEngineImGuiStub/ImGuiStubs.cs`, `VerseStub/VerseStubs.cs` and
`VerseStub/TooltipStubs.cs` from the fixed producer commit
`7eb8073492a1380aa18c67cc30960d19645efbd1`. These supply its actual native adapter
contracts (focus/control-name/text outlet and tooltip); no FL runtime implementation is copied.
The remaining snapshot stays at the source above.

The CI/rc preparation adds only `GUI.DrawTexture(Rect,Texture)` as the native convenience overload,
forwarding stretch/alpha=true to the existing four-argument recording/failure outlet. It is a
consumer-owned extension, not a new producer snapshot. The gate15 exact native-menu/world/window
boundaries remain in `scripts/stub-coverage-exemptions.txt`; neither that table nor the shell protocol
fixtures establish real Options/MainMenu, game-window ownership, simulation speed or camera sound.

Project output/intermediate paths are consumer-local. Defaults stay in this repository's
`dist/fl08-migration-a001`; isolated Host builds may pass `FerriteLibStubOutputRoot`
and `FerriteLibStubObjRoot` (directories ending with a separator). Both follow the nested
stub builds and runtime copies; existing callers retain their defaults. No producer source
or output is a build target. The earlier
`tools/FerriteLib.Stubs` 0.7 freeze is retained unchanged for its historical evidence.
The ordinary Host harness uses these 0.8 doubles; DefinitionDirectory retains its separately
declared model extension. Neither establishes native game/Config/font/input acceptance.

These files remain under the repository's MPL-2.0 license. No stub assembly ships in the mod.
