# Iris v3.0

Contemplative flower trimming game. Thesis project built in Unity 6.0.3 with URP.

## Overview

Players tend to flowers by cutting stems, removing withered leaves, and arranging petals to achieve an ideal arrangement. The game evaluates stem length, cut angle, and part condition against an `IdealFlowerDefinition` to produce a score.

## System Architecture

```
┌────────────────────────────┐
│        Player Input        │
│ (Mouse, Scroll, Click)     │
└────────────┬───────────────┘
             │
             ▼
┌────────────────────────────┐
│  CuttingPlaneController    │
│ - Moves plane vertically   │
│ - Triggers cut input       │
│ - Gated by ScissorStation  │
└────────────┬───────────────┘
             │
             ▼
┌────────────────────────────┐
│     PlaneBehaviour /       │
│  AngleStagePlaneBehaviour  │
│ - Virtual stem cut (fast)  │
│ - DMC fallback (destructive│
└────────────┬───────────────┘
             │
             ▼
┌────────────────────────────┐
│   FlowerGameBrain          │
│ - Evaluates stem length    │
│ - Evaluates cut angle      │
│ - Tracks parts condition   │
│ - Computes score           │
└───────┬───────────┬────────┘
        │           │
        ▼           ▼
┌─────────────┐  ┌────────────────────┐
│ Gameplay HUD│  │ Debug Telemetry HUD│
│(Qualitative)│  │ (Numeric, F3 key)  │
└─────────────┘  └────────────────────┘
        │
        ▼
┌────────────────────────────┐
│   FlowerGradingUI          │
│ - Final evaluation         │
│ - Emotional framing        │
│ - End-of-session snapshot  │
└────────────────────────────┘
```

## Key Subsystems

| Subsystem | Description |
|-----------|-------------|
| **Input/Control** | Mouse-driven cutting plane, scissor equip station, angle tilt |
| **Mesh Cutting** | VirtualStemCutter (non-destructive) with DMC fallback |
| **Game Logic** | Session lifecycle, scoring brain, stem runtime tracking |
| **Physics/Joints** | XYTetherJoint custom tether, SquishMove jelly deformation |
| **Fluids/VFX** | Particle-based sap system with pooled decals |
| **UI** | Gameplay feedback HUD, grading screen, debug telemetry |
| **Audio** | Singleton AudioManager with spatial SFX |
| **Data** | ScriptableObject flower definitions for data-driven scoring |
| **Apartment Hub** | Spline-dolly camera browsing 7 areas, station enter/exit FSM, hover highlights |
| **Camera Presets** | A/B/C camera comparison: LensSettings, VolumeProfile, light overrides, editor gizmos |
| **Bookcase Station** | 4-row bookcase with books, perfumes, drawers, trinkets, coffee table books |
| **Dating Loop** | Full dating lifecycle: calendar, newspaper ads, phone, 3D date character, affection tracking, grading |
| **Mechanic Prototypes** | 10 standalone minigame prototypes (drink making, cleaning, watering, etc.) |

## Apartment Hub

The apartment is the central hub connecting all stations. A Cinemachine spline-dolly camera pans along a closed-loop 7-knot spline. Press left/right to browse areas, Enter to select.

```
ApartmentManager FSM:
  Browsing → Selecting → Selected → InStation
                ↘ (if station has its own cameras) ↗
                   direct skip to InStation
```

**7 Areas:** Entrance, Kitchen (NewspaperDating), Living Room (Bookcase), Watering Nook, Flower Room, Cozy Corner (RecordPlayer), Bathroom (MirrorMakeup)

Stations with their own Cinemachine cameras skip the intermediate Selected state and transition directly from Browsing to InStation.

## Camera Preset System

Compare different visual directions per apartment area. Each preset stores per-area camera position, rotation, full Cinemachine `LensSettings`, a URP `VolumeProfile`, and light overrides.

```
Two-layer lighting stack:

Player actions → MoodMachine → base light / ambient / fog / rain  (global mood)
                                    ↓
Camera preset  → VolumeProfile  → color grading, bloom, DoF, vignette  (per-camera look)
               → light multipliers → intensity / color tint on top of mood
```

**Controls:** Press `1`/`2`/`3` to switch presets, backtick to clear. UI buttons in bottom-left corner.

**Editor tools:**
- Select a `CameraPresetDefinition` SO → frustum wireframes appear in Scene View
- "Capture Scene View → [Area]" button grabs position, rotation, and full lens settings

**LensSettings** includes physical camera properties (aperture, focus distance, ISO, shutter speed, sensor size, anamorphism). All properties lerp smoothly during transitions.

## Dating Loop

A full dating gameplay loop spans the 7-day calendar. Each day: wake up, read the newspaper, cut out a personal ad, call the date, prepare the apartment, host the date, then sleep.

```
GameClock (7-day calendar, real-time hour ticking, feeds MoodMachine "TimeOfDay")
    │
    ▼
NewspaperManager → cut ad → DateSessionManager.StartWaiting()
    │                              │
    ▼                              ▼
PhoneController (rings) ──→ DateSessionManager.OnDateCharacterArrived()
                                   │
                                   ▼
                           DateCharacterController (NavMesh NPC)
                           - walks to couch, sits
                           - periodic excursions to ReactableTag objects
                           - reactions: Like / Neutral / Dislike → affection
                                   │
                                   ▼
                           DateSessionManager.EndDate() → DateEndScreen (grade)
                                   │
                                   ▼
                           GameClock.GoToBed() → next day
```

- **ReactableTag** marks apartment objects (books, plants, perfume, records) for date reactions
- **DatePreferences** on each `DatePersonalDefinition` define liked/disliked tags, mood range, drinks
- **MoodMachine** mood matching multiplies affection gains/losses
- **CoffeeTableDelivery** auto-delivers drinks after `DrinkMakingManager` scores
- **DateEndScreen** shows letter grade (S/A/B/C/D) and summary
- **DateHistory** static registry tracks all dates across the calendar

## Creating a New Flower Level

1. Import your flower model into the scene
2. Open **Window > Iris > Flower Auto Setup**
3. Select the flower root in the hierarchy
4. Verify detected parts (stem, crown, leaves, petals)
5. Click **Setup Flower** to auto-wire all components
6. Adjust the generated `IdealFlowerDefinition` for scoring rules
7. Wire UI events (OnGameOver, OnResult) to your HUD prefabs

## Project Structure

```
Assets/
├── Editor/                 # Scene builders and editor tools
│   ├── FlowerAutoSetup     # Auto-wiring wizard for new flowers
│   ├── ApartmentSceneBuilder # Generates full apartment hub scene
│   ├── BookcaseSceneBuilder  # Generates bookcase station (shared builder)
│   └── ...SceneBuilder     # 10+ scene builders for each mechanic
├── Scripts/
│   ├── Framework/          # TimeScaleManager, VirtualStemCutter, AudioManager, GameClock
│   ├── GameLogic/          # FlowerGameBrain, FlowerSessionController, scoring
│   ├── InteractionAndFeel/ # XYTetherJoint, SquishMove, GrabPull
│   ├── DynamicMeshCutter/  # Mesh cutting engine + plane behaviors
│   ├── Fluids/             # Sap particle system, decal pooling
│   ├── UI/                 # HUD, grading, debug telemetry
│   ├── Tags/               # Marker components (StemPieceMarker, etc.)
│   ├── Apartment/          # Hub system: ApartmentManager, StationRoot, MoodMachine, CameraPresets
│   ├── Bookcase/           # BookInteractionManager, BookVolume, PerfumeBottle, etc.
│   ├── Dating/             # Dating loop: DateSessionManager, GameClock, PhoneController, etc.
│   └── Mechanics/          # 10 prototype minigames (DrinkMaking, Cleaning, etc.)
├── ScriptableObjects/      # Flower defs, apartment areas, book/perfume/drink defs
└── Scenes/                 # Game scenes
```

## Development

See [LONGTERM_PLAN.md](docs/LONGTERM_PLAN.md) for the project roadmap and remaining work items.

See [DEV_JOURNAL.md](docs/DEV_JOURNAL.md) for session-by-session development notes.

See [CODEBASE_QUALITY_ASSESSMENT.md](docs/CODEBASE_QUALITY_ASSESSMENT.md) for the full technical audit.
