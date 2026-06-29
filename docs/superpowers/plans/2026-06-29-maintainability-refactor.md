# Maintainability Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor AbyssMod for clearer maintenance while keeping runtime behavior unchanged.

**Architecture:** Keep the current `Core`, `Services`, and `Patches` boundaries. Prefer private helper extraction inside existing files over new abstractions, and preserve Harmony patch signatures, config keys, cache paths, and JSON formats.

**Tech Stack:** C# net6.0, BepInEx Unity IL2CPP, Harmony, System.Text.Json, HttpClient.

---

## File Structure

- Modify `AbyssMod/Core/Config.cs`: convert namespace style and route setting-change logs through `Logger`.
- Modify `AbyssMod/Core/Hotkey.cs`: remove unused private `IsAltPressed`.
- Modify `AbyssMod/Models/Manifest.cs`: convert namespace style only.
- Modify `AbyssMod/Services/TranslationCache.cs`: convert namespace style and extract generic cache file helpers for load/save/hash operations.
- Modify `AbyssMod/Services/TranslationCrypto.cs`: convert namespace style only.
- Modify `AbyssMod/Patches/TranslationPatch.cs`: extract translation lookup helpers and rename misleading UI error helper.
- Modify `AbyssMod/Patches/MasterDataPatch.cs`: route direct plugin logging through `Logger`.

### Task 1: Baseline And Style Cleanup

**Files:**
- Modify: `AbyssMod/Core/Config.cs`
- Modify: `AbyssMod/Core/Hotkey.cs`
- Modify: `AbyssMod/Models/Manifest.cs`
- Modify: `AbyssMod/Services/TranslationCrypto.cs`
- Modify: `AbyssMod/Patches/MasterDataPatch.cs`

- [ ] **Step 1: Verify baseline build**

Run: `dotnet build AbyssMod.sln`
Expected: build exits 0 with 0 errors.

- [ ] **Step 2: Apply mechanical cleanup**

Convert block-scoped namespaces to file-scoped namespaces in the listed files, remove `Hotkey.IsAltPressed`, and replace direct `Plugin.Log` calls with `Logger`.

- [ ] **Step 3: Verify cleanup build**

Run: `dotnet build AbyssMod.sln`
Expected: build exits 0 with 0 errors.

### Task 2: TranslationPatch Helper Extraction

**Files:**
- Modify: `AbyssMod/Patches/TranslationPatch.cs`

- [ ] **Step 1: Identify repeated lookup behavior**

Preserve these behaviors: title translation uses `titles`, description uses `descriptions`, names use `names`, novel body text uses the current novel dictionary, and UI text lookup first checks direct text then transform path.

- [ ] **Step 2: Extract private helpers**

Add private helpers that return translated values without mutating input when no translation exists. Keep all Harmony method signatures unchanged.

- [ ] **Step 3: Rename misleading UI error helper**

Rename `DisableUiTextTranslationAfterError` to a name that reflects current behavior: log the first UI text translation error and continue future attempts.

- [ ] **Step 4: Verify patch build**

Run: `dotnet build AbyssMod.sln`
Expected: build exits 0 with 0 errors.

### Task 3: TranslationCache Duplication Reduction

**Files:**
- Modify: `AbyssMod/Services/TranslationCache.cs`

- [ ] **Step 1: Extract generic file helpers**

Create generic private helpers for JSON file load/save and normalized hash calculation so dictionary and static bundle flows share the same file operation shape.

- [ ] **Step 2: Update existing call sites**

Replace `LoadFromFile`, `LoadBundleFromFile`, `SaveToFile`, `SaveBundleToFile`, `HashFile`, and `HashBundleFile` with calls to the generic helpers while preserving their warning/error messages.

- [ ] **Step 3: Verify cache build**

Run: `dotnet build AbyssMod.sln`
Expected: build exits 0 with 0 errors.

### Task 4: Final Review

**Files:**
- Review all modified files.

- [ ] **Step 1: Inspect diff**

Run: `git diff --stat` and `git diff --check`.
Expected: no whitespace errors; diff is limited to the planned files.

- [ ] **Step 2: Run final build**

Run: `dotnet build AbyssMod.sln`
Expected: build exits 0 with 0 errors.
