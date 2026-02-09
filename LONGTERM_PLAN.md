# Iris v3.0 - Long-Term Development Plan

**Project:** Iris v3.0 - Contemplative Flower Trimming Game (Thesis)
**Engine:** Unity 6.0.3 with URP
**Last Updated:** February 3, 2026
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

### Phase 5: Newspaper Personals Dating Minigame (Done)
- [x] **DatePersonalDefinition** (`Assets/Scripts/Dating/DatePersonalDefinition.cs`)
  - ScriptableObject data container for one date character (name, ad text, arrival time, portrait, character prefab)
- [x] **PersonalListing** (`Assets/Scripts/Dating/PersonalListing.cs`)
  - Per-listing state machine: Available → BeingCircled → Circled
  - Populates TMP labels from definition in Awake
- [x] **MarkerController** (`Assets/Scripts/Dating/MarkerController.cs`)
  - Mouse-driven Sharpie marker following cursor on newspaper surface via raycast
  - Circle-drawing coroutine: ~48 points, Perlin-noise wobble, ellipse variation, 30° overshoot, 0.6s ease-in-out animation
  - Mouse release cancels partial circle and resets listing
  - Inline InputAction fallback (same pattern as SimpleTestCharacter)
- [x] **NewspaperManager** (`Assets/Scripts/Dating/NewspaperManager.cs`)
  - Scene-scoped singleton (Browsing → Calling → Waiting → DateArrived)
  - 2s calling overlay, countdown timer, arrival notification
  - UnityEvent OnDateArrived for downstream hooks
- [x] **NewspaperDatingSceneBuilder** (`Assets/Editor/NewspaperDatingSceneBuilder.cs`)
  - Editor tool: Window > Iris > Build Newspaper Dating Scene
  - Generates: desk, newspaper with world-space Canvas + 4 sample listings, marker system with LineRenderer, screen-space UI overlay
  - Auto-creates "Newspaper" physics layer and DatePersonalDefinition assets

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
| Dating Minigame | NewspaperManager, MarkerController, PersonalListing |

### Creating a New Flower Level (Quick Start)
1. Import your flower model into the scene
2. Open **Window > Iris > Flower Auto Setup**
3. Select the flower root in the hierarchy
4. Verify detected parts (stem, crown, leaves, petals)
5. Click **Setup Flower** - all components are auto-wired
6. Adjust the generated IdealFlowerDefinition values for scoring
7. Wire UI events (OnGameOver, OnResult) to your HUD prefabs
8. Test with the F3 debug telemetry HUD
