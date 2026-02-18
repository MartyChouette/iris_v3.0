# CLAUDE.md — Project Context for Claude Code

## What Is This Project

Iris v3.0 is a contemplative flower-trimming game (thesis project). Unity 6.0.3, URP, C#. Players cut stems with scissors, evaluated against ideal rules for score and "days alive."

The game centers on an **apartment hub** — a spline-dolly camera browses 2 areas (Kitchen, Living Room), each with a station. Stations are entered via `StationRoot` and managed by `ApartmentManager`. The apartment model is imported from Blender (`aprtment blockout.obj`) at 0.1 scale.

## Build & Run

- **Engine:** Unity 6.0.3 with Universal Render Pipeline
- **Input:** New Input System (`com.unity.inputsystem 1.16.0`)
- **Camera:** Cinemachine 3.1.2
- **No CLI build** — open in Unity Editor, play in-editor

## Editor Tools

| Menu Path | Script | What It Does |
|-----------|--------|--------------|
| Window > Iris > Flower Auto Setup | `Assets/Editor/FlowerAutoSetup.cs` | Auto-wires flower components from a model |
| Window > Iris > Build Apartment Scene | `Assets/Editor/ApartmentSceneBuilder.cs` | Generates apartment hub with Kitchen + Living Room, modular station groups, dating loop |
| Window > Iris > Build Bookcase Browsing Scene | `Assets/Editor/BookcaseSceneBuilder.cs` | Generates standalone bookcase station scene |
| Window > Iris > Build Dating Loop Scene | `Assets/Editor/DatingLoopSceneBuilder.cs` | Generates standalone dating loop test scene with full gameplay loop |
| Window > Iris > Build Camera Test Scene | `Assets/Editor/CameraTestSceneBuilder.cs` | Generates Cinemachine camera test room |
| Window > Iris > Quick Flower Builder | `Assets/Editor/QuickFlowerBuilder.cs` | One-click wizard: drag in stem/leaf/petal meshes, builds full flower hierarchy with components + SOs |

## Code Conventions

- **No namespace** on most scripts. Exceptions: `Iris.Camera`, `Iris.Apartment`, `DynamicMeshCutter`
- **Private fields:** `_camelCase`. **Static fields:** `s_camelCase`
- **Singletons:** Scene-scoped pattern (no DontDestroyOnLoad) — see `HorrorCameraManager`. Exception: `AudioManager` uses DontDestroyOnLoad
- **ScriptableObjects:** `[CreateAssetMenu]`, `[Header]`/`[Tooltip]` on all fields
- **Input:** Inline `InputAction` fallback when scene builder can't wire InputActionReferences (see `SimpleTestCharacter.cs`)
- **TMP text updates:** Use `TMP_Text.SetText()` with format args to avoid string allocation
- **Audio:** Always go through `AudioManager.Instance.PlaySFX(clip)` with null guards on both Instance and clip
- **Debug logs:** Prefixed `[ClassName]` — e.g. `[NewspaperManager]`
- **Editor scene builders:** Follow `CameraTestSceneBuilder.cs` pattern — `NewScene(EmptyScene)`, `CreateBox()` helper, URP Lit materials, `AssetDatabase.IsValidFolder()` checks

## Key Singletons & Managers

| Class | Scope | Location |
|-------|-------|----------|
| `AudioManager` | Persistent (DDoL) | `Assets/Scripts/Framework/AudioManager.cs` |
| `TimeScaleManager` | Static utility | `Assets/Scripts/Framework/TimeScaleManager.cs` |
| `HorrorCameraManager` | Scene-scoped | `Assets/Scripts/Camera/HorrorCameraManager.cs` |
| `ApartmentManager` | Scene-scoped | `Assets/Scripts/Apartment/ApartmentManager.cs` |
| `NewspaperManager` | Scene-scoped | `Assets/Scripts/Dating/NewspaperManager.cs` |
| `BookInteractionManager` | Scene-scoped | `Assets/Scripts/Bookcase/BookInteractionManager.cs` |
| `GameClock` | Scene-scoped | `Assets/Scripts/Framework/GameClock.cs` |
| `DateSessionManager` | Scene-scoped | `Assets/Scripts/Dating/DateSessionManager.cs` |
| `MoodMachine` | Scene-scoped | `Assets/Scripts/Apartment/MoodMachine.cs` |
| `PhoneController` | Scene-scoped | `Assets/Scripts/Dating/PhoneController.cs` |
| `CoffeeTableDelivery` | Scene-scoped | `Assets/Scripts/Dating/CoffeeTableDelivery.cs` |
| `DateEndScreen` | Scene-scoped | `Assets/Scripts/Dating/DateEndScreen.cs` |
| `FridgeController` | Scene-scoped | `Assets/Scripts/Apartment/FridgeController.cs` |
| `CleaningManager` | Scene-scoped | `Assets/Scripts/Mechanics/Cleaning/CleaningManager.cs` |
| `DayPhaseManager` | Scene-scoped | `Assets/Scripts/Framework/DayPhaseManager.cs` |

## Script Directory Map

| Directory | Purpose |
|-----------|---------|
| `Scripts/Framework/` | Core systems: AudioManager, TimeScaleManager, GameClock, CuttingPlaneController, VirtualStemCutter, ScissorStation |
| `Scripts/GameLogic/` | Scoring brain, session lifecycle, flower definitions, stem/part runtime |
| `Scripts/InteractionAndFeel/` | Physics interactions: XYTetherJoint, SquishMove, JellyMesh, GrabPull |
| `Scripts/Fluids/` | Sap particles, decal pooling |
| `Scripts/UI/` | HUD, grading screen, debug telemetry |
| `Scripts/Camera/` | HorrorCameraManager, CameraZoneTrigger, SimpleTestCharacter |
| `Scripts/DynamicMeshCutter/` | Mesh cutting engine (DMC) |
| `Scripts/Tags/` | Marker components (StemPieceMarker, LeafAttachmentMarker, etc.) |
| `Scripts/Apartment/` | Hub system: ApartmentManager, StationRoot, ObjectGrabber, PlacementSurface, MoodMachine |
| `Scripts/Bookcase/` | BookInteractionManager, BookVolume, PerfumeBottle, DrawerController, ItemInspector |
| `Scripts/Dating/` | Dating loop: DateSessionManager, PhoneController, DateCharacterController, ReactableTag, CoffeeTableDelivery, NewspaperManager, DayManager |
| `Scripts/Mechanics/` | 10 prototype minigames: DrinkMaking, Cleaning, Watering, MirrorMakeup, RecordPlayer, etc. |
| `Scripts/Prototype_LivingRoom_Scripts/` | Legacy living room prototype (not active) |

## Apartment Hub Architecture

```
ApartmentManager (Browsing → Selecting → Selected → InStation)
       │ A/D input                                 ↑ Esc
       ▼                                           │
CinemachineSplineDolly (4-knot closed-loop spline)
       │ Enter key                                 │
       ▼                                           │
Selected state (clean stains, pick up / place objects)
       │ Enter key                                 │
       ▼                                           │
StationRoot.Activate() → raises station cameras to priority 30
       │                                           │
       ▼                                           │
IStationManager (BookInteractionManager, DrinkMakingManager, RecordPlayerManager)
```

### Current Areas (2)

| Area | StationType | splinePosition | Notes |
|------|-------------|----------------|-------|
| Kitchen | DrinkMaking | 0.0 | Always accessible, fridge gates DrinkMaking entry |
| Living Room | Bookcase | 0.5 | BookInteractionManager |

### Modular Station Groups

Each station is a self-contained parent GO. Moving the parent repositions everything:
```
Station_{Name}/
  ├─ Cam_{Name}           ← CinemachineCamera (priority 0, raised to 30 on Activate)
  ├─ {Furniture GOs}
  ├─ {Manager GO}         ← XxxManager + XxxHUD components
  ├─ StationRoot           ← stationType, stationManager, hudRoot, stationCameras
  └─ ReactableTag          ← for date NPC reactions
```

### Key Components
- `ApartmentAreaDefinition` — ScriptableObject per area (splinePosition, stationType, camera settings)
- `StationRoot` — Marker on each station root, manages activate/deactivate of manager + HUD + cameras
- `FridgeController` — Click-to-open fridge door that gates DrinkMaking station via `ApartmentManager.ForceEnterStation()`
- `CleaningManager` — Only responds in Selected apartment state (stain interaction gating)
- `ObjectGrabber` — Spring-damper pick-and-place, active in Selected state
- `BookcaseSceneBuilder.BuildBookcaseUnit()` — shared builder used by both standalone and apartment scenes
- `InteractableHighlight` — Toggle-based rim light on hover (off by default, driven by `ApartmentManager` hover raycast)

### Camera Preset System

Compare different visual directions per area. Each preset is a `CameraPresetDefinition` SO (namespace `Iris.Apartment`).

```
CameraPresetDefinition (SO)
  └─ AreaCameraConfig[] (per area, index-matched to ApartmentManager.areas[])
       ├─ position, rotation          ← camera transform
       ├─ LensSettings lens           ← FOV, near/far, dutch, ortho, physical camera
       ├─ VolumeProfile               ← URP post-processing (color grading, bloom, DoF)
       ├─ lightIntensityMultiplier    ← scales directional light on top of MoodMachine
       └─ lightColorTint              ← tints directional light on top of MoodMachine
```

**Two-layer lighting:**
- Layer 1: `MoodMachine` (player actions → global mood → base light/ambient/fog/rain)
- Layer 2: Camera preset (`VolumeProfile` + light overrides → per-camera visual treatment)

**Controls:** `1`/`2`/`3` apply presets, backtick clears. Mouse parallax works on top of presets.

**Editor:** Select a preset SO → frustum wireframes in Scene View + "Capture Scene View" buttons per area.

**Files:** `CameraPresetDefinition.cs`, `CameraTestController.cs`, `CameraPresetDefinitionEditor.cs`

### Scene Builder Position Config

Newspaper layout is controlled by constants at top of `ApartmentSceneBuilder.cs` (lines 38-47):

| Constant | Default | Purpose |
|----------|---------|---------|
| `NewspaperCamPos` | (-5.5, 1.3, 3.0) | Read camera position |
| `NewspaperCamRot` | (5, 0, 0) | Read camera rotation (degrees) |
| `NewspaperCamFOV` | 48 | Read camera field of view |
| `NewspaperSurfacePos` | (-5.5, 1.1, 5.0) | Newspaper quad position |
| `NewspaperSurfaceScl` | (2.5, 1.75, 1) | Newspaper quad scale |
| `NewspaperCanvasOff` | (0, 0, 0.05) | Canvas offset from surface |
| `NewspaperCanvasScl` | 0.0025 | Canvas world-space scale |
| `NewspaperTossPos` | (-3.5, 0.42, 3.0) | Where newspaper lands after reading |
| `NewspaperTossRot` | (90, 10, 0) | Tossed newspaper rotation |

Station group positions (lines 30-32):

| Constant | Default | Purpose |
|----------|---------|---------|
| `BookcaseStationPos` | (-6.3, 0, 3.0) | Bookcase station in living room |
| `RecordPlayerStationPos` | (-2, 0, 5) | Record player in living room |
| `DrinkMakingStationPos` | (-4, 0, -5.2) | Drink station in kitchen |

Edit these, then re-run **Window > Iris > Build Apartment Scene** to regenerate.

## Vertical Slice — Full Game Flow

```
Menu → Tutorial Card → Name Entry (mirror) → Photo/Ad Intro → Newspaper
→ Preparation (timed) → Date (3 phases) → Couch Scene → Flower Trimming → End of Day
```

### 1. Menu → Start
- Main menu with start button
- Tutorial card shown between menu and game start (direct, explains controls)

### 2. Name Entry (Bathroom Mirror) — DEFERRED, separate scene added later
- Separate scene (hard cut). 3D "Nema" model in front of bathroom mirror, looping idle animation
- Text input for player name with **profanity filter** (block slurs / bad words)
- On confirm: camera takes a "photo" of Nema posing

### 3. Photo → Newspaper Intro
- Photo of Nema goes black & white
- Photo appears next to her personals ad in the newspaper
- Transition into the newspaper reading view

### 4. Newspaper (Morning Phase)
- **Visual:** Half-folded newspaper (cliche folded-in-half look), open to personals section
- **Layout:** 3 personal ads + 2 commercial ad slots per day
- Button-based ad selection — click a personal ad to choose your date
- Each of the 3 dates has unique preferences for apartment items, drinks, decor
- Scissors cutting mechanic preserved in code but not active (deferred)

### 5. Preparation Phase (Timed)
After selecting a date, a timer starts. The apartment starts messy (bottles, wine stains, possible blood stains from last night). Player prepares:

| Action | Station/System | Notes |
|--------|---------------|-------|
| Clean stains (wine, blood, trash, bottles) | CleaningManager | Sponge + spray tools |
| Water plants | WateringManager | One-shot perfect pour mechanic (click timing) |
| Choose coffee table book | BookInteractionManager | Leave on coffee table for date to see |
| Choose vinyl record | RecordPlayerManager | Playing during date affects reactions |
| Spray perfume | PerfumeBottle | Changes hue, filter, weather, environment SFX via MoodMachine |
| Leave out trinket/gundam | DrawerController → shelf | Shelf display for date to react to |
| Choose outfit | **NEW: OutfitSelector** | Judged in date Phase 1 |

**End of prep:** Player calls on phone to start date early, OR timer expires and doorbell rings.

### 6. Date Phase 1 — Entrance (First Impressions)
Date NPC arrives at door. Three judgments with Sims-style thought bubble + emote + SFX:

1. **Outfit** — based on date's style preferences
2. **Perfume** — "What is that perfume?" reaction
3. **Welcome/greeting** — how player greets them

Each judgment: thought bubble appears → emote icon (heart/meh/frown) → SFX → affection ±

### 7. Date Phase 2 — Kitchen (Drink Making)
- Date stands by kitchen counter
- Player is given drink recipes (shown on HUD)
- **Select** correct alcohol bottle from shelf
- **Perfect pour** — one-shot click-timing mechanic (same as plant watering)
- Drink scored → date reacts with thought bubble
- If passed → proceed to Phase 3

### 8. Date Phase 3 — Living Room (Apartment Judging)
- Date takes their drink to living room
- Walks around, investigates key items (ReactableTags):
  - Coffee table book, vinyl playing, perfume scent, trinket on shelf, cleanliness
- Each item: thought bubble → reaction → affection ±
- Phase ends after duration or all items investigated

### 9. Win/Lose Outcome
- **Win (score threshold met):** Couch cuddling scene — Nema and date on couch, Nema secretly holding scissors behind her back
- **Hard cut** to flower trimming scene
- Load flower, trim it, get score
- **End of Day 1**

### 10. Vertical Slice Notes
- Day 1 MUST start with a mess from previous night (pre-spawned stains, bottles, trash)
- Tutorial card at start is mandatory — direct instructions, not subtle
- This is a single-day vertical slice demonstrating the full loop

## Existing Systems (What's Built)

| System | Status | Key Files |
|--------|--------|-----------|
| Apartment hub (2 areas) | Working | ApartmentManager, ApartmentSceneBuilder |
| Spline camera browsing | Working | CinemachineSplineDolly, 4-knot loop |
| Newspaper (button selection) | Working | NewspaperManager, NewspaperAdSlot |
| Cleaning (sponge + spray) | Working | CleaningManager, CleanableSurface |
| Object grab/place | Working | ObjectGrabber, PlacementSurface |
| Bookcase interaction | Working | BookInteractionManager, BookVolume |
| Record player | Working | RecordPlayerManager, RecordPlayerHUD |
| Perfume spray | Working | PerfumeBottle, EnvironmentMoodController |
| Drink making | Working | DrinkMakingManager (needs rework to perfect-pour) |
| Fridge door mechanic | Working | FridgeController |
| Date session (3 phases) | Working | DateSessionManager, DateCharacterController |
| Date reactions | Working | ReactionEvaluator, DateReactionUI, ReactableTag |
| Coffee table delivery | Working | CoffeeTableDelivery |
| Date end screen | Working | DateEndScreen |
| Flower trimming | Working | FlowerGameBrain, FlowerSessionController |
| Day phase management | Working | DayPhaseManager, ScreenFade |
| MoodMachine | Working | MoodMachine, MoodMachineProfile |
| GameClock | Working | GameClock (7-day calendar) |

## Not Yet Built

| System | Notes |
|--------|-------|
| Main menu | Scene + UI |
| Tutorial card | Overlay between menu and gameplay |
| Name entry (mirror scene) | Deferred — separate scene, hard cut, added later |
| Photo intro sequence | Camera pose → B&W photo → newspaper transition |
| Half-folded newspaper visual | Rework newspaper mesh/canvas to folded look |
| Outfit selection | New system — choose outfit, date judges in Phase 1 |
| Perfect pour mechanic | One-shot click-timing (shared by watering + drink making) |
| Date Phase 1 rework | Entrance judgments: outfit, perfume, welcome |
| Date Phase 2 rework | Kitchen counter, recipe selection, bottle pick, perfect pour |
| Couch win scene | Cuddling + scissors behind back |
| Flower trimming transition | Hard cut from apartment to flower scene, load flower from DatePersonalDefinition |
| Flower → apartment integration | Score = days alive as apartment plant, score → mess intensity, score on calendar |
| Living plant decoration | Trimmed flower persists in apartment for N days, wilts, feeds MoodMachine |
| Pre-spawned mess | Day 1 starting state with stains, bottles, trash |
| Profanity filter | Block bad words in name entry |

## Roadmap Reference

See `LONGTERM_PLAN.md` for full backlog and implementation phases.
