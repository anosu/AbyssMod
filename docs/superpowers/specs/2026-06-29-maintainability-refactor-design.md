# AbyssMod Maintainability Refactor Design

## Goal

Make the current AbyssMod codebase clearer and easier to maintain while preserving runtime behavior, configuration compatibility, cache file layout, and Harmony patch signatures.

## Scope

- Normalize source style where it is already inconsistent, especially namespace declarations and logging calls.
- Remove code that is clearly unused and has no observable behavior.
- Extract small helper methods for repeated translation lookup logic in patch code.
- Reduce duplicated cache load/save/hash flow in `TranslationCache` without changing remote URLs, local paths, JSON formats, manifest behavior, fallback behavior, or log intent.
- Rename misleading private helpers or flags where their names imply behavior they do not perform.

## Out Of Scope

- No new user-facing features.
- No configuration key changes.
- No public cache format changes.
- No rewrite of IL2CPP pointer handling in `MasterMapping`.
- No large class split unless required to remove direct duplication safely.

## Architecture

The existing module boundaries remain:

- `Core` owns plugin boot, config, hotkeys, and logging.
- `Services` owns translation loading, cache management, path construction, crypto, and MasterData mapping.
- `Patches` owns Harmony integration and delegates data lookup to services.

This refactor keeps those boundaries intact and improves local readability inside each file.

## Error Handling

Current failure modes remain intact:

- Translation manifest fetch still falls back to local cache.
- Translation resource fetch still falls back to stale local cache when present.
- UI text translation errors continue to log only the first warning while allowing later patch calls to continue.
- MasterData translation failures stay isolated from game startup.

## Verification

Primary verification is `dotnet build AbyssMod.sln`.

If the build cannot complete because local game interop DLLs or the private `Utility.dll` are unavailable, report the exact missing dependency or compiler output instead of claiming success.
