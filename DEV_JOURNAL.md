# Dev Journal — Iris v3.0

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
