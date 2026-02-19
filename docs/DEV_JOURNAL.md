# Dev Journal — Iris v3.0

---

## 2026-02-19 — Audio Hookups & SFX Infrastructure

### Session Summary

Wired audio across four core systems: MoodMachine ambience/weather, ObjectGrabber pickup/place, ApartmentManager area transitions, and DayPhaseManager phase-specific ambience. All hooks are null-guarded and work silently with no clips assigned — activate when clips are added in Inspector.

---

### 1. MoodMachine — Ambience & Weather Audio Channels

Added `_ambienceClip` and `_weatherClip` fields to `MoodMachine`. On Start, both loops begin playing at volume 0 via `AudioManager.PlaySFX`. `ApplyMood()` now reads `ambienceVolume` and `weatherVolume` AnimationCurves from `MoodMachineProfile` and drives loop volume based on current mood value. `OnDestroy` stops both loops.

- New `MoodMachineProfile` fields: `ambienceVolume` and `weatherVolume` AnimationCurves
- Volume driven continuously by mood lerp, not triggered on/off

**Files:** `MoodMachine.cs`, `MoodMachineProfile.cs`

---

### 2. ObjectGrabber — Pickup & Place SFX

Added `_pickupSFX` and `_placeSFX` AudioClip fields. `PlaySFX` calls fire on grab and on successful placement. Null-guarded on both the clip and `AudioManager.Instance`.

**Files:** `ObjectGrabber.cs`

---

### 3. ApartmentManager — Area Transition SFX

Added `_areaTransitionSFX` field. Plays via `AudioManager.PlaySFX` inside `CycleArea()` when the player navigates between apartment areas.

**Files:** `ApartmentManager.cs`

---

### 4. DayPhaseManager — Phase-Specific Ambience

Added `_morningAmbienceClip` and `_explorationAmbienceClip` fields. Ambience swaps when transitioning between Morning and Exploration phases. Each clip is played through `AudioManager` and the previous one stops on transition.

**Files:** `DayPhaseManager.cs`

---

### Follow-Up Work Identified

| Task | Notes |
|------|-------|
| Per-stain sparkle VFX + completion SFX | Stain quad should deactivate or sparkle after cleaning |
| Already-clean stain gating | Clean stain spots should not trigger sponge interaction |
| Tighten tidiness scoring thresholds | Currently too lenient at 35% weight for stains |
| Visual indicator for public vs private items | Items left out for date vs stored in drawers |
| Polish highlight shaders | Tone down bright green/red placement shadow, warmer rim colors |
| Complete SFX/music asset list | ~35-40 assets needed across all systems |

---

### Commits

| Hash | Description |
|------|-------------|
| `d9f37df` | MoodMachine ambience/weather audio channels |
| `a75627f` | ObjectGrabber pickup/place SFX |
| `8f90293` | ApartmentManager area transition SFX |
| `ffa6c84` | DayPhaseManager phase-specific ambience |

---

### Files Changed (Summary)

| File | Change |
|------|--------|
| `MoodMachine.cs` | Ambience/weather clip fields, volume from profile curves, OnDestroy cleanup |
| `MoodMachineProfile.cs` | Added ambienceVolume, weatherVolume AnimationCurves |
| `ObjectGrabber.cs` | Pickup/place SFX fields with PlaySFX calls |
| `ApartmentManager.cs` | Area transition SFX field in CycleArea |
| `DayPhaseManager.cs` | Morning/exploration ambience clip fields with swap logic |

---

## 2026-02-18 — Bookcase Refactor, Cleaning Simplification & Bug Fixes

### Session Summary

Major bookcase overhaul: removed trinkets, deterministic 15-book layout on one row, expanded coffee table books to 5 with upright shelf placement, and multiple bug fixes across cleaning, perfumes, and wall placement.

---

### 1. Bookcase Refactor — Remove Trinkets

**Before:** Trinkets (small display items) on bookcase with double-click inspection, dedicated layer, separate SOs.
**After:** Trinket system removed entirely. Bookcase focuses on books, coffee table books, perfumes, and drawers.

- Deleted `TrinketVolume.cs`, `TrinketDefinition.cs`
- Removed trinket mask, hover/click logic, and Inspecting state paths from `BookInteractionManager.cs`
- Removed `TrinketNames` array, `BuildTrinketDisplaySlots()`, `"Trinkets"` layer from `BookcaseSceneBuilder.cs`

---

### 2. Deterministic 15 Books on One Row

**Before:** Random book count per row (3-6), spread across 4 rows with random colors/sizes.
**After:** Exactly 15 books, all on row 1, with varying heights/thickness but deterministic titles.

- All 15 `BookTitles` entries packed onto a single shelf row
- Rewrote `BuildBookSpineTitle()` using PPM (2000 pixels-per-meter) technique — canvas `sizeDelta = worldSize * PPM`, `localScale = 1/PPM` for crisp text on narrow spine faces
- Auto-sizing TMP text fills the spine proportionally

**Files:** `BookcaseSceneBuilder.cs`

---

### 3. Fix Pages Rendering Behind Book

**Problem:** When opening a book to read, the world-space pages canvas was occluded by the opaque book mesh.

**Fix:** In `BookVolume.EnterReading()`, disable `MeshRenderer` and hide `SpineTitle` child. Re-enable both in `PutBackRoutine()`.

**Files:** `BookVolume.cs`

---

### 4. Coffee Table Books — Upright on Shelf, 5 Books

**Before:** 2 small flat coffee table books stacked on shelf.
**After:** 5 upright coffee table books on row 2 with varying sizes. Click to toggle between bookcase (upright, side-by-side) and coffee table (flat, stacked). One book starts on the coffee table.

- Varying sizes per book: different thicknesses (0.035-0.06), heights, depths
- `RecalculateCoffeeTableStack()` — only repositions books on the coffee table; bookcase books stay at saved positions
- Hover: slides forward on shelf, up on table
- Spine titles on coffee table books too
- **Static field domain reload fix**: replaced `static BookcaseStackBase/CoffeeTableStackBase` (wiped on domain reload) with per-instance serialized `coffeeTableBase`/`coffeeTableRotation` fields

**Files:** `CoffeeTableBook.cs`, `BookcaseSceneBuilder.cs`, `ApartmentSceneBuilder.cs`

---

### 5. ReactableTag Cleanup

**Before:** Station-level ReactableTag on bookcase group, individual ReactableTags toggled on coffee table books when moved.
**After:** Only coffee table books get ReactableTags (always active/public). No station-level tag. Drawers remain private via DrawerController privacy toggle.

**Files:** `ApartmentSceneBuilder.cs`, `CoffeeTableBook.cs`

---

### 6. Cleaning — Sponge Only

**Before:** Cleaning had sponge + spray tools with Tab toggle, stubbornness gating (some stains needed spray first).
**After:** Sponge only. Sponge is always 100% effective. Spray mechanic removed entirely (may return later for harder stains).

- Removed: `Tool` enum, `_sprayVisual`, `SelectSponge()`/`SelectSpray()`, Tab toggle from `CleaningManager.cs`
- Removed stubbornness gating from `CleanableSurface.Wipe()` — sponge now clears stains directly
- Simplified `CleaningHUD.cs` — removed tool name label and tool buttons
- Updated `CleaningSceneBuilder.cs` and `ApartmentSceneBuilder.cs` — removed spray visual, tool buttons, spray wiring

**Files:** `CleaningManager.cs`, `CleanableSurface.cs`, `CleaningHUD.cs`, `CleaningSceneBuilder.cs`, `ApartmentSceneBuilder.cs`

---

### 7. Perfume Fixes — Lower Shelf + Re-Spraying

**Problem 1:** Perfumes were one-shot locked — `SprayOnce()` had a `if (SprayComplete) return;` guard.
**Problem 2:** Perfumes on top shelf (row 4) were hard to notice.

**Fix:** Removed one-shot guard. `SprayOnce()` now deactivates all other perfumes first (via `Deactivate()` method), then activates the sprayed one. Allows switching between perfumes freely. Moved perfumes from row 4 to row 3.

**Files:** `PerfumeBottle.cs`, `BookcaseSceneBuilder.cs`

---

### 8. Wall Pictures — Wrong Side Fix

**Problem:** Pictures picked up and re-placed on walls snapped to the wrong side of the wall surface.

**Root cause:** `PlacementSurface.ProjectOntoSurface()` always projected to `center[n] + extents[n]` (the front face), regardless of which side the player was interacting from. For walls whose `transform.forward` didn't face the room, pictures ended up on the outside.

**Fix:** For vertical (wall) surfaces, `ProjectOntoSurface()`, `SnapToGrid()`, and `ClampToSurface()` now detect which face the point is nearest to and project to that face, flipping the surface normal accordingly. Horizontal surfaces (tables/shelves) still always use the top face.

**Files:** `PlacementSurface.cs`

---

### Bookcase Layout (After Refactor)

| Row | Contents |
|-----|----------|
| 0 | Empty |
| 1 | 15 normal books (upright, varying heights) |
| 2 | 5 coffee table books (upright, varying sizes) |
| 3 | 3 perfume bottles |
| 4 | Empty |

---

### Files Changed (Summary)

| File | Change |
|------|--------|
| `TrinketVolume.cs` | DELETED |
| `TrinketDefinition.cs` | DELETED |
| `BookInteractionManager.cs` | Removed trinket logic |
| `BookVolume.cs` | Hide mesh/spine when reading |
| `CoffeeTableBook.cs` | Upright shelf, stacking, serialized fields |
| `CoffeeTableBookDefinition.cs` | Widened thickness range |
| `PerfumeBottle.cs` | Re-spraying, deactivate others |
| `BookcaseSceneBuilder.cs` | 15 books on row 1, 5 coffee books row 2, perfumes row 3, PPM spine titles |
| `ApartmentSceneBuilder.cs` | Coffee table target wiring, ReactableTag cleanup, cleaning simplification |
| `CleaningManager.cs` | Sponge only, removed spray/tool switching |
| `CleanableSurface.cs` | Removed stubbornness gating |
| `CleaningHUD.cs` | Removed tool UI |
| `CleaningSceneBuilder.cs` | Removed spray wiring |
| `PlacementSurface.cs` | Two-sided wall projection |

---

## 2026-02-14 — Apartment Polish Batch + Camera Preset System

### Session Summary

Major polish pass on the apartment scene and a full camera preset system for visual direction exploration.

---

### 1. Perfume — One-Click Puff

**Before:** Pick up perfume → hold LMB → wait 0.5s → release → escape. Too many steps.
**After:** Single click on perfume bottle → big particle burst → mood set → done. Bottle stays on shelf.

- Rewrote `PerfumeBottle.cs` — new `SprayOnce()` method replaces the multi-step pick-up/hold/spray flow
- Removed `HoldingPerfume` and `Spraying` states from `BookInteractionManager.cs`
- Click perfume → spray → stay in Browsing state

**Files:** `PerfumeBottle.cs`, `BookInteractionManager.cs`

---

### 2. Picture Position Drift Fix

**Problem:** Wall placeables shift slightly on every scene rebuild because `ApplyCrookedOffset()` applied a new random rotation before `RestoreLayout()` overwrote it.

**Fix:** Skip `ApplyCrookedOffset()` when `PlaceableLayout.json` already exists. Only apply crook on first-ever build.

**Files:** `ApartmentSceneBuilder.cs` (`CreateWallPlaceable`)

---

### 3. UI — Spread to Edges

**Problem:** All HUD elements anchored around screen center, causing overlap.

**Fix:** Repositioned apartment browse UI:
- Area name panel → top-center (anchor 0.5, 1)
- Browse hints → bottom-center (anchor 0.5, 0)
- Nav left arrow → left edge (anchor 0, 0.5)
- Nav right arrow → right edge (anchor 1, 0.5)

**Files:** `ApartmentSceneBuilder.cs` (`CreateUIPanel`, `CreateNavButton`)

---

### 4. Prep Timer "Watch" Panel

**Problem:** `DayPhaseManager` had `_prepTimerText` and `_prepTimerPanel` fields but the scene builder never created UI for them.

**Fix:** Built a small panel (top-right corner, 180x50) with clock icon + MM:SS countdown text. Wired to DayPhaseManager's serialized fields. Auto-shows/hides when prep starts/ends.

**Files:** `ApartmentSceneBuilder.cs` (`BuildDayPhaseManager`)

---

### 5. Hover Highlight (Rim Light on Mouse-Over)

**Before:** `InteractableHighlight` was always-on (static rim light overlay on every interactable).
**After:** Highlight starts OFF. Appears only when the mouse hovers over an interactable object.

- Rewrote `InteractableHighlight.cs` — caches base and highlighted material arrays, `SetHighlighted(bool)` swaps them
- Added hover raycast in `ApartmentManager.Update()` — tracks `_hoveredHighlight`, calls `SetHighlighted(true/false)` on enter/exit

**Files:** `InteractableHighlight.cs`, `ApartmentManager.cs`

---

### 6. NavMesh Crash Fix

**Problem:** `SetDestination can only be called on an active agent placed on NavMesh` when date character arrives.

**Fix:** In `DateCharacterController.Initialize()`:
1. Disable agent, teleport, re-enable
2. `NavMesh.SamplePosition()` to find nearest valid point
3. `NavMeshAgent.Warp()` before any `SetDestination` call
4. All `SetDestination` calls guarded with `_agent.isOnNavMesh`

**Files:** `DateCharacterController.cs`

---

### 7. Camera Preset System (Full Feature)

Built a complete camera preset comparison system for exploring different visual directions per apartment area.

#### 7a. Core Preset Infrastructure
- `CameraPresetDefinition` ScriptableObject (namespace `Iris.Apartment`) with per-area configs
- `CameraTestController` — applies presets, smooth lerp transitions between areas
- Keyboard shortcuts: `1`/`2`/`3` to apply presets, backtick to clear
- UI buttons (bottom-left) for mouse access
- Scene builder creates 3 default presets: V1 High Angle, V2 Low & Wide, V3 Isometric Ortho

#### 7b. Mouse Parallax in Preset Mode
- Presets feed position through `ApartmentManager.SetPresetBase()` so mouse parallax works on top of preset cameras
- `ApartmentManager` applies parallax offset to base position without feedback loop

#### 7c. SO Preservation on Rebuild
- `CreateAreaDef()` and `CreateCameraPreset()` only write default values on first creation (`isNew`)
- Subsequent rebuilds preserve user edits to SO fields

#### 7d. Full Cinemachine LensSettings Per Area
- Replaced `float fovOrOrthoSize` with `LensSettings lens` on `AreaCameraConfig`
- Full control: FOV, near/far clip, dutch angle, ortho size, mode override
- Physical camera support: aperture, focus distance, ISO, shutter speed, sensor size, anamorphism, barrel clipping
- All properties lerp smoothly during transitions

#### 7e. Editor Gizmos + Capture Button
- `CameraPresetDefinitionEditor.cs` — custom editor for `CameraPresetDefinition` SOs
- Draws frustum wireframes in Scene View for every area config (yellow = perspective, cyan = ortho)
- Sphere + label at each camera position
- "Capture Scene View → [Area]" button per area — grabs position, rotation, and full lens settings from Scene View camera

#### 7f. VolumeProfile + Light Overrides Per Area
- Each `AreaCameraConfig` has a `VolumeProfile` reference for per-camera post-processing (color grading, bloom, vignette, DoF)
- Light intensity multiplier and color tint fields — applied on top of MoodMachine's base values
- `CameraTestController` captures baseline light values on Start, restores on clear
- `PresetVolume` (global Volume, priority 10) created by scene builder, profile swapped instantly on preset change

**Architecture (two-layer lighting):**
```
Player actions → MoodMachine → base light / ambient / fog / rain
                                    ↓
Camera preset  → VolumeProfile  → color grade, bloom, DoF, vignette
               → light multipliers → intensity / color tint on top of mood
```

**Files:**
- `CameraPresetDefinition.cs` — SO + AreaCameraConfig struct
- `CameraTestController.cs` — runtime preset application
- `CameraPresetDefinitionEditor.cs` — editor gizmos + capture
- `ApartmentSceneBuilder.cs` — preset SO creation, Volume + light wiring
- `ApartmentManager.cs` — preset base + parallax integration

---

### 8. Namespace Fix

**Problem:** `CameraPresetDefinition` was in global namespace, causing SO gear icon issues.
**Attempted fix:** Wrapped in `namespace Iris` — but this conflicted with existing `Iris.Camera` namespace, making `Camera` ambiguous across the whole project.
**Final fix:** Used `namespace Iris.Apartment` — no conflict with `Iris.Camera`. Consumers use `using Iris.Apartment;`.

---

### Files Changed (Summary)

| File | Change |
|------|--------|
| `PerfumeBottle.cs` | One-click spray, removed hold states |
| `BookInteractionManager.cs` | Removed HoldingPerfume/Spraying states |
| `InteractableHighlight.cs` | Toggle-based highlight (off by default) |
| `ApartmentManager.cs` | Hover raycast, preset base + parallax |
| `CameraPresetDefinition.cs` | NEW — SO with LensSettings + VolumeProfile + light overrides |
| `CameraTestController.cs` | NEW — preset application with keyboard shortcuts |
| `CameraPresetDefinitionEditor.cs` | NEW — editor gizmos + scene capture |
| `ApartmentSceneBuilder.cs` | UI layout, drift fix, prep timer, Volume wiring |
| `DateCharacterController.cs` | NavMesh warp fix |
