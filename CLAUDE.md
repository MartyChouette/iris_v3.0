# CLAUDE.md — Project Context for Claude Code

## What Is This Project

Iris v3.0 is a contemplative flower-trimming game (thesis project). Unity 6.0.3, URP, C#. Players cut stems with scissors, evaluated against ideal rules for score and "days alive."

The game centers on an **apartment hub** — a spline-dolly camera browses 7 areas, each with a station (bookcase, newspaper dating, record player, mirror makeup, etc.). Stations are entered via `StationRoot` and managed by `ApartmentManager`.

## Build & Run

- **Engine:** Unity 6.0.3 with Universal Render Pipeline
- **Input:** New Input System (`com.unity.inputsystem 1.16.0`)
- **Camera:** Cinemachine 3.1.2
- **No CLI build** — open in Unity Editor, play in-editor

## Editor Tools

| Menu Path | Script | What It Does |
|-----------|--------|--------------|
| Window > Iris > Flower Auto Setup | `Assets/Editor/FlowerAutoSetup.cs` | Auto-wires flower components from a model |
| Window > Iris > Build Apartment Scene | `Assets/Editor/ApartmentSceneBuilder.cs` | Generates full apartment hub with all 7 areas and stations |
| Window > Iris > Build Bookcase Browsing Scene | `Assets/Editor/BookcaseSceneBuilder.cs` | Generates standalone bookcase station scene |
| Window > Iris > Build Newspaper Dating Scene | `Assets/Editor/NewspaperDatingSceneBuilder.cs` | Generates newspaper dating desk scene |
| Window > Iris > Build Dating Loop Scene | `Assets/Editor/DatingLoopSceneBuilder.cs` | Generates standalone dating loop test scene with full gameplay loop |
| Window > Iris > Build Camera Test Scene | `Assets/Editor/CameraTestSceneBuilder.cs` | Generates Cinemachine camera test room |
| Window > Iris > Quick Flower Builder | `Assets/Editor/QuickFlowerBuilder.cs` | One-click wizard: drag in stem/leaf/petal meshes, builds full flower hierarchy with components + SOs |

## Code Conventions

- **No namespace** on most scripts. Exceptions: `Iris.Camera`, `DynamicMeshCutter`
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
       │ left/right input                          ↑ (skip if station has cameras)
       ▼                                           │
CinemachineSplineDolly (7-knot closed-loop spline)
       │ Enter key
       ▼
StationRoot.Activate() → raises station cameras to priority 30
       │
       ▼
IStationManager (BookInteractionManager, NewspaperManager, RecordPlayerManager, MirrorMakeupManager, PhoneController)
```

- `ApartmentAreaDefinition` — ScriptableObject per area (splinePosition, stationType, camera settings)
- `StationRoot` — Marker on each station root, manages activate/deactivate of manager + HUD + cameras
- Stations with their own `stationCameras` skip the Selected state entirely (direct Browsing → InStation)
- `BookcaseSceneBuilder.BuildBookcaseUnit()` — shared builder used by both standalone and apartment scenes

## Dating Loop Architecture (v3 — Full Loop)

```
GameClock (7-day calendar, real-time hour tick, MoodMachine "TimeOfDay" source)
       │ each morning
       ▼
DayManager.AdvanceDay() → NewspaperManager regenerates ads
       │ click newspaper → cut ad
       ▼
NewspaperManager (TableView → PickingUp → ReadingPaper → Cutting → Calling → Waiting → DateArrived)
       │ DateArrived
       ▼
PhoneController.StartRinging() → player clicks phone → AnswerPhone()
       │
       ▼
DateSessionManager (Idle → WaitingForArrival → DateInProgress → DateEnding)
       │ spawns character
       ▼
DateCharacterController (WalkingToCouch → Sitting → GettingUp → WalkingToTarget → Investigating → Returning)
       │ investigates ReactableTag objects
       ▼
ReactionEvaluator → ReactionType (Like/Neutral/Dislike) → affection ±
       │ player ends date or forced bedtime
       ▼
DateEndScreen (grade: S/A/B/C/D) → DateHistory records entry
       │ continue
       ▼
GameClock.GoToBed() → next morning
```

### Key Components
- `DatePreferences` on `DatePersonalDefinition` — liked/disliked tags, mood range, drinks
- `ReactableTag` — lightweight marker + static registry on apartment objects
- `CoffeeTableDelivery` — auto-delivers drinks after DrinkMakingManager scores
- `MoodMachine` mood matching multiplies affection gains/losses
- `DateReactionUI` — world-space thought bubble (? → heart/meh/frown)
- All cross-references use `?.` null-checks — works in standalone test scene or apartment

## Roadmap Reference

See `LONGTERM_PLAN.md` for full backlog. Key remaining items:
- Memory profiling (historical leak concern)
- Profile on target hardware
