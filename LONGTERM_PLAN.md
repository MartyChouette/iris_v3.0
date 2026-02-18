# Iris v3.0 - Long-Term Development Plan

**Project:** Iris v3.0 - Contemplative Flower Trimming Game (Thesis)
**Engine:** Unity 6.0.3 with URP
**Last Updated:** February 18, 2026
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

### Phase 5: Newspaper Dating Minigame (Done — v3 Button Selection)
- [x] **DatePersonalDefinition / CommercialAdDefinition** — ScriptableObjects for ad content
- [x] **NewspaperPoolDefinition** — defines which ads appear in each newspaper
- [x] **NewspaperManager** — 3-state FSM: ReadingPaper → Calling → Done. Button-based ad selection (click personal ad to select)
- [x] **NewspaperAdSlot** — `[RequireComponent(typeof(Button))]`, click handler calls `NewspaperManager.SelectPersonalAd()`
- [x] **DayManager** — manages day progression
- [x] Scissors cutting mechanic preserved in code (`ScissorsCutController`, `CutPathEvaluator`, `NewspaperSurface`) but not active — deferred for future use

### Phase 6: Apartment Hub System (Done — v2 Modular Stations)
- [x] **ApartmentManager** — 4-state FSM (Browsing → Selecting → Selected → InStation), scene-scoped singleton
- [x] **ApartmentAreaDefinition** — ScriptableObject per area (splinePosition, stationType, selectedPosition/rotation/FOV)
- [x] **StationRoot** — activates/deactivates station manager, HUD, and Cinemachine cameras
- [x] **StationType** enum (9 types) + **IStationManager** interface
- [x] **ObjectGrabber** — spring-damper pick-and-place with grid snap and surface clamping
- [x] **PlacementSurface** — bounds-constrained placement areas
- [x] CinemachineSplineDolly on browse camera, 4-knot closed-loop spline
- [x] 2 active areas: Kitchen (DrinkMaking, always accessible), Living Room (Bookcase)
- [x] **Modular station groups** — each station is a self-contained parent GO (camera, furniture, manager, HUD, ReactableTag)
- [x] **FridgeController** — click-to-open fridge door gates DrinkMaking station entry via `ForceEnterStation()`
- [x] **Apartment model import** — loads `aprtment blockout.obj` from Blender at 0.1 scale, procedural fallback
- [x] **Interaction flow** — Browsing → Selected (clean, pick up objects) → Enter to enter station → Esc back
- [x] **CleaningManager gating** — stains only interactable in Selected apartment state
- [x] **ReactableTags** on key objects for date NPC reactions (plants, books, record player, drinks)

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

### Phase 10: Full Dating Loop System (Done — v2 Three-Phase Dates)
- [x] **GameClock** — Scene-scoped singleton driving a 7-day calendar with configurable real-time hour ticking, feeds MoodMachine "TimeOfDay" source, forced bedtime at 2am, player-initiated sleep
- [x] **DatePreferences** — Serializable class on DatePersonalDefinition defining liked/disliked tags, preferred mood range, liked/disliked drinks, reaction strength
- [x] **ReactableTag** — Lightweight marker component with static registry; tags apartment objects for date character discovery
- [x] **DateSessionManager** — Scene-scoped singleton with 3 date sub-phases:
  - Phase 1 (Arrival): NPC walks in, sits on couch → `OnSatDown` event
  - Phase 2 (DrinkJudging): Player makes drink, delivers to coffee table → NPC judges
  - Phase 3 (ApartmentJudging): NPC excursions enabled, wanders to ReactableTags, auto-ends after `apartmentJudgingDuration` (default 60s)
- [x] **PhoneController** — Dual-mode: IStationManager station AND ambient-clickable. Rings after newspaper timer, click to answer and trigger date arrival
- [x] **DateCharacterController** — NavMesh NPC with 7-state FSM. Excursions gated by `_excursionsEnabled` (only during ApartmentJudging). `OnSatDown` event fires when first sitting.
- [x] **ReactionEvaluator** — Static utility evaluating reactables, drinks, and mood against DatePreferences → ReactionType (Like/Neutral/Dislike)
- [x] **DateReactionUI** — World-space billboard bubble: question mark (notice) → heart/neutral/frown (opinion) with SFX and fade
- [x] **CoffeeTableDelivery** — Auto-delivers drinks to coffee table after DrinkMakingManager scores, spawns visual cup, notifies DateSessionManager
- [x] **DateEndScreen** — Results screen with letter grade (S≥90, A≥75, B≥60, C≥40, D<40), summary text, continue button
- [x] **DateHistory** — Static registry tracking all completed dates across the calendar
- [x] **DateSessionHUD** — Overlay showing date name, affection bar, clock, day number
- [x] **DayPhaseManager** — Single authority for daily phase transitions (Morning → Exploration → DateInProgress → Evening), camera priorities, screen fades
- [x] **ScreenFade** — Full-screen black overlay with FadeIn/FadeOut coroutines
- [x] **DatingLoopSceneBuilder** — Full standalone test scene
- [x] Modified `NewspaperManager` to use button-based ad selection (scissors deferred)
- [x] Modified `DrinkMakingManager` to bridge to CoffeeTableDelivery after scoring
- [x] Modified `RecordPlayerManager` and `PerfumeBottle` to toggle ReactableTag.IsActive on play/stop and spray/putdown
- [x] Added `Phone`, `DrinkMaking` to `StationType` enum
- [x] Added `MoodMachine` + `MoodMachineProfile` for scene-wide mood system (light, ambient, fog, rain)

### Phase 11.5: Flower Physics Fixes (Done)
- [x] **SquishMove passiveMode** — Added `passiveMode` flag that skips mouse input, rigidbody driving, and constraint setting. Leaves/petals use passive mode so GrabPull owns physics.
- [x] **FreezePositionZ conflict fix** — Removed world-Z position freeze from both `XYTetherJoint` and `SquishMove`. ConfigurableJoint already locks Z in joint-local space; world-Z freeze created conflicting constraints on rotated parts, pinning them to a 1D line.
- [x] **FlowerBreathing snap-back fix** — FlowerBreathing was writing `transform.localPosition` every frame, overriding GrabPull physics. Now rebinds `_initialLocalPos` on grab→release transition so leaves stay where physics left them.
- [x] **Removed unused idealLocalPosition/idealLocalEuler** — Cleaned out from `IdealFlowerDefinition`, `FlowerPartRuntime`, and `FlowerTypeAuthoring`.

### Phase 11: Apartment Polish & Camera Preset System (Done)
- [x] **Perfume one-click puff** — Single click spray replaces multi-step pick-up/hold/spray flow. Bottle stays on shelf.
- [x] **Picture position drift fix** — Skip `ApplyCrookedOffset()` when `PlaceableLayout.json` exists (only apply crook on first build)
- [x] **UI layout spread** — Repositioned browse UI to screen edges (area name top-center, hints bottom-center, nav arrows at edges)
- [x] **Prep timer panel** — Countdown UI (top-right) wired to DayPhaseManager's `_prepTimerPanel` and `_prepTimerText`
- [x] **Hover highlight** — `InteractableHighlight` now toggle-based (off by default), `ApartmentManager` hover raycast drives highlight on/off
- [x] **NavMesh crash fix** — `DateCharacterController.Initialize()` uses `NavMesh.SamplePosition` + `Warp` before any `SetDestination`
- [x] **Camera preset system** — Full A/B/C camera comparison tool:
  - `CameraPresetDefinition` SO (namespace `Iris.Apartment`) with per-area configs
  - `CameraTestController` — keyboard shortcuts (1/2/3, backtick), smooth lerp transitions
  - Full `LensSettings` per area (FOV, near/far, dutch, ortho, physical camera properties)
  - `VolumeProfile` per area for post-processing (color grading, bloom, vignette, DoF)
  - Light intensity multiplier + color tint per area (applied on top of MoodMachine base)
  - Mouse parallax works on top of preset cameras via `ApartmentManager.SetPresetBase()`
  - `CameraPresetDefinitionEditor` — Scene View frustum gizmos + "Capture" button per area
  - SOs preserved across rebuilds (only write defaults on first creation)
- [x] **Two-layer lighting architecture** — MoodMachine (player actions → global mood) + preset system (per-camera VolumeProfile + light overrides)

---

## Vertical Slice Remaining Work

Full game flow: Menu → Tutorial → Name Entry → Photo Intro → Newspaper → Prep (timed) → Date (3 phases) → Couch Scene → Flower Trimming → End of Day 1

### VS-1: Core Flow (Not Yet Built)

- [ ] **Main menu scene** — Start button, title screen
- [ ] **Tutorial card** — Overlay shown once between menu and gameplay, direct control instructions
- [ ] **Name entry (mirror scene)** — Separate scene (hard cut). 3D Nema model with loop animation in front of bathroom mirror. Text input with profanity filter. Deferred to later — placeholder skip for now.
- [ ] **Profanity filter** — Block slurs/bad words in name input. Word list + substring check.
- [ ] **Photo intro sequence** — Nema poses, camera takes photo, B&W filter, photo placed next to personals ad in newspaper. Cinematic transition.
- [ ] **Half-folded newspaper visual** — Rework newspaper mesh/canvas to cliche folded-in-half look. 3 personal ads + 2 commercial slots per day.
- [ ] **Couch win scene** — Date succeeds → couch cuddling scene, Nema holding scissors behind her back. Separate camera angle.
- [ ] **Flower trimming transition** — Hard cut from apartment to flower trimming scene. Load flower, get score. End of day.

### VS-1b: Flower ↔ Apartment Integration (Not Yet Built)

Each date character brings a specific flower. The flower trimming score determines how long the plant lives in the apartment and affects next-day mess.

- [ ] **Flower scene transition** — After date success + couch scene, hard cut to flower trimming scene. Load flower type from `DatePersonalDefinition.flowerPrefab`. Score and dismiss. Fade back to apartment Evening phase.
- [ ] **DayPhaseManager flower phase** — New phase between DateInProgress and Evening: `FlowerTrimming`. Triggers scene load, waits for `FlowerSessionController.OnResult`, records score.
- [ ] **Flower results → save data** — Pipe `OnResult(eval, score, days)` into `RichDateHistoryEntry` (flower score, days alive). Record in `IrisSaveData` for calendar display.
- [ ] **Living plant in apartment** — Trimmed flower spawns as decoration in apartment. Persists for N days (from flower score). Wilts progressively, then dies/disappears. Feeds MoodMachine while alive ("LivingPlant" source, value decays as days pass).
- [ ] **Flower score → mess intensity** — `AftermathMessGenerator` reads last flower score. Bad trim = heavy mess next morning, good trim = light mess. Threshold-based (e.g. score < 40 = extra stains, score > 80 = minimal mess).
- [ ] **Flower score on calendar** — `ApartmentCalendar` shows flower grade alongside date grade per day. Visual indicator of plant health remaining.
- [ ] **Per-character flower types** — Wire `DatePersonalDefinition.flowerPrefab` field on all 4 date characters (Livii, Sterling, Sage, Clover). Each brings a different species.

### VS-2: Preparation Phase (Partially Built)

- [x] **Cleaning** — CleaningManager with sponge + spray (working)
- [x] **Object grab/place** — ObjectGrabber (working)
- [x] **Record player** — RecordPlayerManager (working)
- [x] **Perfume spray** — PerfumeBottle + MoodMachine (working, changes hue/filter/weather/environment SFX)
- [x] **Coffee table books** — BookInteractionManager (working)
- [x] **Trinkets on shelf** — DrawerController + TrinketVolume (working)
- [x] **Preparation timer UI** — Countdown panel (top-right) wired to DayPhaseManager. Auto-shows/hides on prep start/end.
- [ ] **Outfit selection** — New system. Player chooses outfit during prep. Date judges in Phase 1.
- [ ] **Perfect pour mechanic** — Shared one-shot click-timing game used by both plant watering and drink making. Single click at right moment for perfect pour.
- [ ] **Pre-spawned mess** — Day 1 must start with bottles, wine stains, possible blood stains, trash from previous night. ApartmentStainSpawner needs "day 1 heavy" preset.
- [ ] **Plant watering rework** — Convert WateringManager to use perfect-pour mechanic

### VS-3: Date Phase Rework

- [x] **DateSessionManager 3 phases** — Arrival, DrinkJudging, ApartmentJudging (framework done)
- [x] **DateCharacterController excursion gating** — Only wanders in Phase 3 (done)
- [x] **ReactableTag system** — Static registry, date NPC discovery (done)
- [x] **DateReactionUI** — Thought bubble with emotes (done)
- [ ] **Phase 1 rework (Entrance)** — Date arrives at door (not couch). Three sequential judgments: outfit, perfume, welcome greeting. Each with Sims-style thought bubble + emote + SFX.
- [ ] **Phase 2 rework (Kitchen Drinks)** — Date stands by kitchen counter. Player sees drink recipe HUD, selects correct alcohol bottle from shelf, does perfect-pour. Score → date reacts.
- [ ] **Phase 3 living room flow** — Date walks to living room with drink. Investigates: coffee table book, vinyl playing, perfume scent, shelf trinket, apartment cleanliness.
- [ ] **Phase pass/fail gating** — If Phase 1 or 2 fails badly, date can leave early.

### VS-4: Nema's Life Systems (Design Doc: DESIGN_NEMA_LIFE.md)

- [ ] **Calendar system** — Physical in-apartment calendar (wall/desk), clickable, shows current day, scheduled dates, completed dates, disappeared dates. `CalendarData` backing store.
- [ ] **Time gates** — Mail arrives at 4pm daily, dates don't start until 8pm. GameClock expansion.
- [ ] **Mail system** — Daily 4pm delivery via GameClock event. Contains: newspaper, letters from dates, bills/flyers, missing person notices (escalating horror). SO-driven content pool.
- [ ] **Nema placeholder** — Visible player character in kitchen and living room. Contextual idle animations (leaning on counter, sitting on couch, swaying to music, reading mail). `NemaController` with room-aware state machine. All meshes/animations swappable via SO/prefab references.
- [ ] **Disco ball** — Clickable ceiling object, triggers Nema dance animation at current position. Particle effects (mirror reflections). Swappable mesh + particles.
- [ ] **Date disappearance mechanic** — Dates that go "very well" (high affection) → person vanishes. Stops appearing in newspaper, doesn't respond to calls. No explicit violence — horror through implication.
- [ ] **Souvenir system** — After date disappears, one of their items appears in apartment (necklace, ring, hat, sweater, watch, scarf). Persistent across days. Accumulates. New dates react to them via ReactableTag ("whose ring is that?"). `SouvenirDefinition` SO per item.
- [ ] **Repeat dates** — Same person returns, remembers previous visits. Relationship deepens. Eventually they might disappear too.
- [ ] **Convention demo mode** — 7-minute timer, curated slice of the full loop.
- [ ] **Feedback system** — Easy player feedback collection when time limits expire (convention demo end, demo end).
- [ ] **Save game system** — Full game state persistence: calendar day, date history, knowledge, item states, souvenirs, apartment layout. Auto-save on sleep, manual save from pause. `GameSaveData` JSON container. Existing `SaveManager` extended or replaced.
- [ ] **Player knowledge system (dating journal)** — Per-date, per-phase insight unlocks. Even if rejected at Phase 2, player keeps Phase 1+2 knowledge. Reveals DatePreferences progressively. Journal UI accessible from apartment (notebook/phone). Tracks: preferences learned, times encountered, highest phase reached, disappeared status. Integrates with ReactionEvaluator + DateEndScreen.

### VS-5: Deferred

- [ ] **Bathroom mirror scene** — Separate scene with hard cut. 3D Nema, mirror, name entry. Will be added later.
- [ ] **Scissors cutting mechanic** — Code preserved (`ScissorsCutController`, `CutPathEvaluator`, `NewspaperSurface`). Not active in vertical slice.
- [ ] **Additional apartment areas** — Entrance, Cozy Corner, Watering Nook, Flower Room, Bathroom. Code partially exists.
- [ ] **Memory profiling** — Extended play session leak testing
- [ ] **Profile on target hardware** — Min-spec performance testing

### Previously Completed

- [x] **Input gating over UI** — `IsPointerOverGameObject()` in CuttingPlaneController
- [x] **Legacy Obi Fluid cleanup** — Deleted ObiEditorSettings.asset, _Recovery, legacy scenes
- [x] **Sap emission cleanup** — Single emission source via FlowerPartRuntime
- [x] **Material instance auditing** — OnDestroy cleanup in BacklightPulse, BookVolume
- [x] **Coroutine WaitForSeconds caching** — Static readonly in SapParticleController
- [x] **Debug log compilation stripping** — `#if UNITY_EDITOR` wrapping

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
- **Static registries:** StemPieceMarker.All, ReactableTag.All, DateHistory.Entries, TimeScaleManager priorities

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
| Data | IdealFlowerDefinition, FlowerTypeDefinition, DatePersonalDefinition, DatePreferences |
| Apartment Hub | ApartmentManager, StationRoot, ObjectGrabber, MoodMachine, FridgeController, ApartmentAreaDefinition, InteractableHighlight |
| Camera Presets | CameraPresetDefinition, CameraTestController, CameraPresetDefinitionEditor (gizmos + capture) |
| Bookcase Station | BookInteractionManager, BookVolume, PerfumeBottle, DrawerController |
| Dating Loop | DateSessionManager (3-phase), GameClock, PhoneController, DateCharacterController, ReactableTag, CoffeeTableDelivery, DayPhaseManager |
| Newspaper | NewspaperManager (button-based), NewspaperAdSlot, DayManager, NewspaperSurface |
| Mechanics | DrinkMakingManager, CleaningManager, WateringManager, MirrorMakeupManager, RecordPlayerManager |

### Creating a New Flower Level (Quick Start)
1. Import your flower model into the scene
2. Open **Window > Iris > Flower Auto Setup**
3. Select the flower root in the hierarchy
4. Verify detected parts (stem, crown, leaves, petals)
5. Click **Setup Flower** - all components are auto-wired
6. Adjust the generated IdealFlowerDefinition values for scoring
7. Wire UI events (OnGameOver, OnResult) to your HUD prefabs
8. Test with the F3 debug telemetry HUD
