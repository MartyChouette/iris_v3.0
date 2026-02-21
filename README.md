# Iris v3.0

Contemplative flower trimming game. Thesis project built in Unity 6.0.3 with URP.

## Overview

Players live as Nema in a small apartment, dating strangers from newspaper personals ads, then trimming the flowers they leave behind. The game evaluates stem length, cut angle, and part condition against an `IdealFlowerDefinition` to produce a score. Between dates, players prepare the apartment — cleaning stains, arranging items, choosing music and perfume — and their choices shape how each date unfolds.

## System Architecture

```
┌────────────────────────────┐
│     Apartment Hub          │
│ 3 areas: Kitchen,          │
│ Living Room, Entrance      │
│ Direct pos/rot/FOV lerp    │
└────────────┬───────────────┘
             │
    ┌────────┼────────┐
    ▼        ▼        ▼
 Cleaning  Bookcase  Dating
 Watering  Records   Phone
 Drinks    Perfumes  Greeting
 Fridge    Drawers   Outfit
             │
             ▼
┌────────────────────────────┐
│   Flower Trimming Scene    │
│ (loaded additively)        │
│ CuttingPlane → VirtualCut  │
│ → FlowerGameBrain → Score  │
└────────────────────────────┘
```

## Key Subsystems

| Subsystem | Description |
|-----------|-------------|
| **Apartment Hub** | 3-area browsing (Kitchen, Living Room, Entrance) with direct camera lerp, mouse parallax |
| **Camera Presets** | A/B/C camera comparison: LensSettings, VolumeProfile, light overrides per area |
| **Object Interaction** | Spring-damper grab, surface snap, wall mounting, cross-room dragging, tether safety |
| **Tidiness System** | Per-area scoring (stains, mess, smell, floor clutter), DropZones, DailyMessSpawner |
| **Bookcase Station** | 15 books, 5 coffee table books, 3 perfumes, 2 drawers, double-click inspection |
| **Dating Loop** | 7-day calendar, newspaper ads, 3-phase dates (entrance/kitchen/living room), affection tracking |
| **Flower Trimming** | Additive scene loading, virtual stem cutting, scoring brain, living plant persistence |
| **Accessibility** | 15 settings across 5 categories, tabbed settings panel, captions, text scaling, reduce motion |
| **Text Theme** | Centralized font/color/spacing via IrisTextTheme SO, auto-applied to all TMP text |
| **PSX Rendering** | Retro shader suite: vertex snap, affine textures, pixelation, dithering (F2 toggle) |
| **Audio** | 6-channel AudioManager (SFX, Music, Ambience, Weather, Environment, UI), MoodMachine-driven |
| **Save System** | IrisSaveData with auto-save on quit/date end, plant records, date history |
| **Mechanic Prototypes** | 10 standalone minigames (drink making, cleaning, watering, makeup, etc.) |

## Apartment Hub

The apartment is the central hub. A direct pos/rot/FOV lerp camera browses 3 areas. All station managers are always active (no station gating). Press A/D to browse, click to interact.

**3 Areas:** Kitchen (DrinkMaking, Fridge, Newspaper), Living Room (Bookcase, Records, Coffee Table), Entrance (Shoe Rack, Coat Rack, Door)

## Dating Loop

```
GameClock (7-day calendar)
    │
    ▼
NewspaperManager → select ad → DateSessionManager.ScheduleDate()
    │                                │
    ▼                                ▼
PhoneController (rings) ──→ DateCharacterController arrives
                                    │
                            ┌───────┼───────┐
                            ▼       ▼       ▼
                         Phase 1  Phase 2  Phase 3
                        Entrance  Kitchen  Living Room
                        (judge)   (drinks) (investigate)
                                    │
                                    ▼
                            DateEndScreen (grade)
                                    │
                                    ▼
                            FlowerTrimmingBridge
                            (additive scene load)
                                    │
                                    ▼
                            LivingFlowerPlant spawned
```

## Accessibility & Settings

Full settings suite accessible via ESC > Settings with 6 tabs:

| Tab | Controls |
|-----|----------|
| Visual | Colorblind mode (4), High contrast, Text scale |
| Audio | Master, Music, SFX, Ambience, UI volume sliders + Captions toggle |
| Motion | Reduce Motion (disables parallax, vertex snap, text morphing), Screen Shake |
| Timing | Timer multiplier (Normal / Relaxed 1.5x / Extended 2x / No Timer) |
| Controls | Input rebinding |
| Performance | Resolution scale, Quality preset, PSX effect toggle |

## Text Theme System

Create a `IrisTextTheme` ScriptableObject (Create > Iris > Text Theme), place in `Assets/Resources/` named `IrisTextTheme`. Controls primary font, header font, body/header/subtitle/accent colors, size multipliers, and spacing — all applied globally to every TMP text component in the scene.

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
│   ├── SettingsPanelBuilder  # Generates settings panel prefab
│   └── ...SceneBuilder     # 10+ scene builders for each mechanic
├── Scripts/
│   ├── Framework/          # TimeScaleManager, AudioManager, GameClock, AccessibilitySettings
│   ├── GameLogic/          # FlowerGameBrain, FlowerSessionController, scoring
│   ├── InteractionAndFeel/ # XYTetherJoint, SquishMove, GrabPull
│   ├── DynamicMeshCutter/  # Mesh cutting engine + plane behaviors
│   ├── Fluids/             # Sap particle system, decal pooling
│   ├── UI/                 # HUD, grading, SettingsPanel, CaptionDisplay, IrisTextTheme
│   ├── Tags/               # Marker components (StemPieceMarker, etc.)
│   ├── Apartment/          # Hub: ApartmentManager, ObjectGrabber, MoodMachine, TidyScorer
│   ├── Bookcase/           # BookInteractionManager, BookVolume, PerfumeBottle, etc.
│   ├── Dating/             # DateSessionManager, GameClock, PhoneController, etc.
│   ├── Mechanics/          # 10 prototype minigames (DrinkMaking, Cleaning, etc.)
│   ├── Rendering/          # PSX retro rendering (PSXRenderController, PSXPostProcessFeature)
│   └── Camera/             # HorrorCameraManager, CameraZoneTrigger
├── Shader/                 # PSXLit, PSXPost, RimLight
├── ScriptableObjects/      # Flower defs, apartment areas, book/perfume/drink/date defs
├── Resources/              # IrisTextTheme SO (auto-loaded)
└── Scenes/                 # Game scenes
```

## Development

See [LONGTERM_PLAN.md](docs/LONGTERM_PLAN.md) for the project roadmap and remaining work items.

See [DEV_JOURNAL.md](docs/DEV_JOURNAL.md) for session-by-session development notes.

See [CODEBASE_QUALITY_ASSESSMENT.md](docs/CODEBASE_QUALITY_ASSESSMENT.md) for the full technical audit.
