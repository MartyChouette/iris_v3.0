# Iris v3.0 - Long-Term Development Plan

**Project:** Iris v3.0 - Contemplative Flower Trimming Game (Thesis)
**Engine:** Unity 6.0.3 with URP
**Last Updated:** February 9, 2026
**Forked from:** Iris v2.0

---

## Completed Work

### Phase 1: Codebase Audit & Assessment (Done)
- [x] Full codebase audit (~14,800 lines, 99 scripts, 8 subsystems)
- [x] Quality assessment document (CODEBASE_QUALITY_ASSESSMENT.md)
- [x] Identified critical, high, and medium priority issues

### Phase 2: Critical Fixes (Done)
- [x] **Time.timeScale centralization** - Created `TimeScaleManager` with priority system
  - Migrated all 6 callers: FlowerSessionController, PauseMenuController, GameOverUI, RRestart, JuiceMomentController, FlowerGradingUI
  - Priority levels: PAUSE(0) > GAME_OVER(10) > JUICE(20)
- [x] **FindFirstObjectByType validation** - Added Debug.LogError to all silent auto-find failures
  - FlowerGradingUI, FlowerHUD_GameplayFeedback, FlowerHUD_DebugTelemetry, SapOnXYTether
- [x] **Leaf/petal detachment lock bug** - Three root causes fixed:
  1. Static `cutBreakSuppressed` never reset on scene reload - added `[RuntimeInitializeOnLoadMethod]`
  2. Release coroutine used scaled `WaitForSeconds` - switched to `WaitForSecondsRealtime`
  3. `PlaneBehaviour.OnDisable()` safety to clear suppression on rapid unequip
- [x] **Virtual stem cut** - Non-destructive stem cutting that preserves original GameObject

### Phase 3: Performance Optimization (Done)
- [x] **SquishMove.cs** - Eliminated per-frame vertex array allocation (was `originalMesh.vertices` every FixedUpdate). Throttled `RecalculateNormals()` to every 3rd physics frame.
- [x] **TMP_FocusBlur.cs** - Added configurable update interval (default 33ms / ~30fps) to throttle expensive `ForceMeshUpdate()` + `UpdateGeometry()` calls.
- [x] **FlowerJointRebinder.cs** - Replaced all LINQ chains (`.Where().Select().ToArray()`) with manual loops and pre-allocated buffers. Added `StemPieceMarker` static registry to eliminate `FindObjectsByType` scene searches.
- [x] **AngleStagePlaneBehavior.cs** - Cached `FlowerStemRuntime` and `FlowerSessionController` references with 2-second refresh interval, eliminating per-frame `FindObjectsByType` calls.
- [x] **StemPieceMarker.cs** - Added static registry pattern (`OnEnable`/`OnDisable` self-registration) to support allocation-free lookups.

### Phase 4: Developer Tooling (Done)
- [x] **FlowerAutoSetup editor wizard** (`Assets/Editor/FlowerAutoSetup.cs`)
  - Detects stem, crown, leaves, petals by naming convention
  - Auto-adds all runtime components (FlowerSessionController, FlowerGameBrain, FlowerStemRuntime, FlowerPartRuntime, XYTetherJoint, SapOnXYTether, MeshTarget)
  - Creates StemAnchor/StemTip/CutNormalRef transforms
  - Generates IdealFlowerDefinition ScriptableObject with part rules
  - Validation mode to check existing setups
  - Full undo support
  - Access: Window > Iris > Flower Auto Setup

### Phase 5: Newspaper Dating Minigame (Done — v2 Scissors)
- [x] **DatePersonalDefinition / CommercialAdDefinition** — ScriptableObjects for ad content
- [x] **NewspaperPoolDefinition** — defines which ads appear in each newspaper
- [x] **NewspaperManager** — 7-state FSM: TableView → PickingUp → ReadingPaper → Cutting → Calling → Waiting → DateArrived
- [x] **ScissorsCutController** — mouse-driven scissors cutting on newspaper surface
- [x] **CutPathEvaluator** — scores cut accuracy around ads
- [x] **DayManager** — manages day progression
- [x] **NewspaperDatingSceneBuilder** — generates complete dating desk scene
- [x] Deleted legacy MarkerController and PersonalListing (replaced by scissors + ad slots)

### Phase 6: Apartment Hub System (Done)
- [x] **ApartmentManager** — 4-state FSM (Browsing → Selecting → Selected → InStation), scene-scoped singleton
- [x] **ApartmentAreaDefinition** — ScriptableObject per area (splinePosition, stationType, selectedPosition/rotation/FOV)
- [x] **StationRoot** — activates/deactivates station manager, HUD, and Cinemachine cameras
- [x] **StationType** enum (7 types) + **IStationManager** interface
- [x] **ObjectGrabber** — spring-damper pick-and-place with grid snap and surface clamping
- [x] **PlacementSurface** — bounds-constrained placement areas
- [x] CinemachineSplineDolly on browse camera, 7-knot closed-loop spline, ShortestSplineDelta() wraparound
- [x] 7 areas: Entrance, Kitchen (NewspaperDating), Living Room (Bookcase), Watering Nook, Flower Room, Cozy Corner (RecordPlayer), Bathroom (MirrorMakeup)

### Phase 7: Enhanced Bookcase Station (Done)
- [x] **BookInteractionManager** — 11-state FSM with multi-layer raycast (Books, Drawers, Perfumes, Trinkets, CoffeeTableBooks)
- [x] **BookVolume / BookDefinition** — book interaction with pull/read/put-back states
- [x] **PerfumeBottle / PerfumeDefinition** — hold and spray interaction
- [x] **DrawerController** — open/close slide with trinket storage
- [x] **TrinketVolume / TrinketDefinition** — double-click inspection items
- [x] **CoffeeTableBook / CoffeeTableBookDefinition** — moveable display books
- [x] **ItemInspector** — double-click close-up view for items
- [x] **EnvironmentMoodController** — light color lerp per book/perfume
- [x] **ItemStateRegistry** — static dictionary tracking item states
- [x] **BookcaseSceneBuilder** — generates standalone bookcase scene

### Phase 8: Mechanic Prototypes (Done)
- [x] 10 standalone mechanic prototypes with scene builders:
  - StemSound, WiltingClock, ToolDegradation, PestSystem, BossFlowers, Grafting
  - ComboCutting + ClientEconomy, DrinkMaking, MirrorMakeup, Cleaning, Watering
- [x] RecordPlayer station (Browsing/Playing FSM, 5 record definitions)
- [x] Ambient watering system (not a station — always active, click any WaterablePlant)

### Phase 9: Shared Bookcase Refactor & Station Camera Improvements (Done)
- [x] **Shared bookcase builder** — extracted `BookcaseSceneBuilder.BuildBookcaseUnit()` used by both standalone and apartment scenes, eliminating ~590 lines of duplicate code
- [x] **Station camera skip** — stations with their own Cinemachine cameras (`StationRoot.HasStationCameras`) skip the Selected state, transitioning directly from Browsing to InStation

---

## Remaining Work

### High Priority

- [ ] **Memory profiling** - Run extended play sessions (20+ consecutive) watching for leaks. Historical concern from commit messages mentioning "memory leak!!!"
- [x] **Input gating over UI** - Movement + cutting both blocked when pointer is over UI (cached per-frame `IsPointerOverGameObject()` in CuttingPlaneController)
- [x] **Legacy Obi Fluid cleanup** - Deleted ObiEditorSettings.asset, _Recovery folder, and 5 legacy scenes with Obi components. Scripts were already clean.

### Medium Priority

- [x] **Sap emission cleanup** - Consolidated to one emission source: `FlowerPartRuntime.MarkDetached()` → `SapParticleController.EmitTearWithFollow()`. XYTetherJoint now skips `TriggerBreakFluidOrDeterministicSap()` when `_partRuntime` exists. SapOnXYTether self-guards via `isAttached` check. Removed noisy debug logs from FlowerSapController, SapDecalPool, SapDecalSpawner, and XYTetherJoint.
- [ ] **Profile on target hardware** - Test on minimum-spec machine, profile memory during particle bursts, watch for frame spikes on stem cuts
- [x] **Material instance auditing** - Added OnDestroy cleanup to BacklightPulse and BookVolume. SapDecal and TMP_MotionBlur already had proper cleanup.
- [x] **Coroutine WaitForSeconds caching** - Cached `WaitForSeconds(0.1f)` as static readonly in SapParticleController. Other usages are variable-delay (not cacheable).
- [x] **Debug log compilation stripping** - Wrapped verbose Debug.Log calls in `#if UNITY_EDITOR` in MeshCreation, FlowerSessionController, CameraZoneTrigger, HorrorCameraManager. Other files already used `debugLogs` bool guards.

### Low Priority / Nice-to-Have

- [x] **Unit tests** for `FlowerGameBrain.EvaluateFlower()` scoring logic — 24 NUnit tests in `Assets/Editor/Tests/FlowerGameBrainTests.cs` covering stem length, cut angle, parts, game-over conditions, weighted averages, and edge cases
- [x] **Automated scene validation** — Editor tool (`Assets/Editor/SceneValidator.cs`) accessible via Window > Iris > Scene Validator. Checks singleton duplicates, required components, flower hierarchy wiring, UI references, cutting system, audio, and fluids.
- [x] **Accessibility** — `AccessibilitySettings` static utility with 4 colorblind palettes (Normal, Protanopia, Deuteranopia, Tritanopia), PlayerPrefs persistence, `AccessibilityDropdownUI` for TMP_Dropdown binding
- [x] **Input rebinding** — `InputRebindManager` static utility with JSON override persistence, `InputOverrideLoader` MonoBehaviour for wiring, `InputRebindUI` with interactive rebinding rows
- [x] **Performance quality presets** — `IrisQualityPreset` ScriptableObject (sap, decals, physics, UI tuning), `IrisQualityManager` scene-scoped singleton, `QualityDropdownUI` for TMP_Dropdown binding
- [x] **Save/Load system** — `SessionSaveData` serializable container, `SaveManager` static utility persisting rolling 50-session history to `iris_sessions.json`
- [x] **Analytics hooks** — `IrisAnalytics` static utility logging timestamped JSON-lines telemetry (cuts, detachments, sessions) to `iris_analytics.jsonl`
- [x] **Localization** — `LocalizationTable` ScriptableObject for per-language string tables, `LocalizationManager` static utility with key-based lookup, language switching, English fallback, PlayerPrefs persistence
- [x] **Crash reporting** — `CrashReporter` static utility hooking `Application.logMessageReceived`, captures errors/exceptions/asserts to rolling `iris_crashlog.txt` with dedup suppression

---

## Architecture Notes

### Key Design Patterns
- **Event-driven:** UnityEvents for loose coupling (OnGameOver, OnResult, OnEquipScissors)
- **Data-driven:** ScriptableObjects for flower definitions (IdealFlowerDefinition)
- **Singleton:** AudioManager (persistent across scenes)
- **Object pooling:** SapDecalPool, SapParticleController
- **Static registries:** StemPieceMarker.All, TimeScaleManager priorities

### Subsystems
| Subsystem | Key Scripts |
|-----------|------------|
| Input/Control | CuttingPlaneController, PlaneAngleTiltController, ScissorStation |
| Mesh Cutting | PlaneBehaviour, AngleStagePlaneBehaviour, VirtualStemCutter |
| Game Logic | FlowerGameBrain, FlowerSessionController, FlowerStemRuntime |
| Physics/Joints | XYTetherJoint, FlowerJointRebinder, SquishMove |
| Fluids/VFX | FlowerSapController, SapParticleController, SapDecalPool |
| UI | FlowerGradingUI, FlowerHUD_GameplayFeedback, FlowerHUD_DebugTelemetry |
| Audio | AudioManager, JointBreakAudioResponder |
| Data | IdealFlowerDefinition, FlowerTypeDefinition, DatePersonalDefinition |
| Apartment Hub | ApartmentManager, StationRoot, ObjectGrabber, ApartmentAreaDefinition |
| Bookcase Station | BookInteractionManager, BookVolume, PerfumeBottle, DrawerController |
| Dating Minigame | NewspaperManager, ScissorsCutController, DayManager, CutPathEvaluator |
| Mechanics | DrinkMakingManager, CleaningManager, WateringManager, MirrorMakeupManager |

### Creating a New Flower Level (Quick Start)
1. Import your flower model into the scene
2. Open **Window > Iris > Flower Auto Setup**
3. Select the flower root in the hierarchy
4. Verify detected parts (stem, crown, leaves, petals)
5. Click **Setup Flower** - all components are auto-wired
6. Adjust the generated IdealFlowerDefinition values for scoring
7. Wire UI events (OnGameOver, OnResult) to your HUD prefabs
8. Test with the F3 debug telemetry HUD
