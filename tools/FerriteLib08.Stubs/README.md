# FL 0.8 platform doubles for the US consumer harness

The `.cs` files are a source snapshot from FerriteLib commit
`564f78b1e88f5450aff075e5bd3f07db2a894c1f`,
`tools/FerriteLib.UiKit.Tests/Stubs`. They model the 1.6 Core/IMGUI/TextRendering/InputLegacy
and Verse/Harmony surfaces used by the fixed 0.8 DLL. They are test inputs, not game binaries.

Only project output/intermediate paths differ: all builds stay in this repository's
`dist/fl08-migration-a001`. No producer source or output is a build target. The earlier
`tools/FerriteLib.Stubs` 0.7 freeze is retained unchanged for its historical evidence.
The ordinary Host harness uses these 0.8 doubles; DefinitionDirectory retains its separately
declared model extension. Neither establishes native game/Config/font/input acceptance.

These files remain under the repository's MPL-2.0 license. No stub assembly ships in the mod.
