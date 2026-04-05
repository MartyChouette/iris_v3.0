# Iris v3.0 - Long-Term Development Plan

**Project:** Iris v3.0 - Contemplative Flower Trimming Game (Thesis)
**Engine:** Unity 6.0.3 with URP
**Last Updated:** April 5, 2026
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

### Phase 7: Enhanced Bookcase Station (Done — Refactored)
- [x] **BookInteractionManager** — FSM with multi-layer raycast (Books, Drawers, Perfumes, CoffeeTableBooks)
- [x] **BookVolume / BookDefinition** — book interaction with pull/read/put-back states; hides mesh/spine during reading
- [x] **PerfumeBottle / PerfumeDefinition** — one-click spray, re-sprayable (switch between perfumes freely)
- [x] **DrawerController** — open/close slide with item storage
- [x] ~~**TrinketVolume / TrinketDefinition**~~ — REMOVED (trinket system deleted)
- [x] **CoffeeTableBook / CoffeeTableBookDefinition** — 5 upright books on shelf, click to toggle flat on coffee table
- [x] **ItemInspector** — double-click close-up view for items
- [x] **EnvironmentMoodController** — light color lerp per book/perfume
- [x] **ItemStateRegistry** — static dictionary tracking item states
- [x] **BookcaseSceneBuilder** — generates standalone bookcase scene; 15 deterministic books (row 1), 5 coffee table books (row 2), 3 perfumes (row 3)
- [x] **Spine title rendering** — PPM technique (2000px/m) for crisp text on narrow spine faces

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

### Phase 12: Accessibility & Settings Suite (Done)
- [x] **AccessibilitySettings rewrite** — Expanded from colorblind-only to full 15-setting hub across 5 categories (Visual, Motion, Audio, Timing, Performance). All PlayerPrefs-backed. `OnSettingsChanged` event. `BeginChanges()`/`EndChanges()` batching.
- [x] **AudioManager volume integration** — Master + per-channel multipliers (`_masterVol`, `_sfxVol`, `_musicVol`, `_ambVol`, `_uiVol`). Applied in all Play methods. Optional `string caption` param.
- [x] **ReduceMotion consumers** — PSXRenderController (snap→4096, affine→0), ApartmentManager (skip parallax), TMP_FocusBlur (skip morphing), MotionJitter (hold rest pose).
- [x] **Timer multiplier** — DayPhaseManager `_prepDuration * multiplier`, DateSessionManager `phase3Duration * multiplier`. 0 = unlimited.
- [x] **CaptionDisplay** — DDoL singleton, screen-space overlay, queue up to 3 captions, auto-fade. `CaptionDisplay.Show(caption, duration)` static API.
- [x] **AccessibleText** — Component on TMP_Text, caches base fontSize, applies TextScale multiplier + theme font/color/spacing.
- [x] **SettingsPanel** — Tabbed UI (Visual/Audio/Motion/Timing/Controls/Performance). `Open()`/`Close()`. Reads/writes AccessibilitySettings live.
- [x] **SettingsPanelBuilder** — `Window > Iris > Build Settings Panel` procedural prefab builder.
- [x] **SimplePauseMenu integration** — ESC → settings back → pause back → game. Settings button wired by ApartmentSceneBuilder.

### Phase 13: Performance Optimization Pass (Done)
- [x] **PlaceableObject static registry** — `s_all` List with OnEnable/OnDisable. Eliminates `FindObjectsByType` in TidyScorer, DishDropZone, DropZone, ItemLabelOverlay, AutoSaveController.
- [x] **PlacementSurface static registry** — Same pattern. `FindNearest()` uses `s_all` instead of scene search.
- [x] **ObjectGrabber.HeldObject** — Static property. DishDropZone and DropZone use it instead of scanning all placeables every frame.
- [x] **Camera.main caching** — ApartmentManager, ItemLabelOverlay now cache Camera.main with null-check fallback.
- [x] **CoffeeTableDelivery material leak** — `_drinkMat` tracked and destroyed in `ClearDrink()`.

### Phase 14: Centralized Text Theme System (Done)
- [x] **IrisTextTheme SO** — `[CreateAssetMenu(menuName = "Iris/Text Theme")]`. Loaded from `Resources/IrisTextTheme`. Controls: primaryFont, headerFont, body/header/subtitle/accent colors, globalSizeMultiplier, headerSizeMultiplier, characterSpacing, lineSpacing. `OnThemeChanged` event.
- [x] **AccessibleText expanded** — TextRole enum (Body/Header/Subtitle/Accent), applies font + color + size + spacing from theme. Subscribes to both `OnSettingsChanged` and `OnThemeChanged`.
- [x] **IrisTextThemeApplier** — Scene startup component, auto-adds AccessibleText to all TMP_Text on Awake. Built by ApartmentSceneBuilder.
- [x] **CaptionDisplay + ItemLabelOverlay** — Dynamically created text now includes AccessibleText component.

---

## Vertical Slice Remaining Work

Full game flow: Menu → Tutorial → Name Entry → Photo Intro → Newspaper → Prep (timed) → Date (3 phases) → Couch Scene → Flower Trimming → End of Day 1

### VS-1: Core Flow (Not Yet Built)

- [x] **Main menu scene** — Start button, title screen. MainMenuManager (3-panel FSM), MainMenuSceneBuilder, Nema parallax head, TMP_FocusBlur title, TextDissolveButton hover, 3 GameModeConfig SOs. Scene: `Assets/Scenes/mainmenu.unity`
- [x] **Tutorial card** — Overlay shown once between menu and gameplay, direct control instructions. TutorialCard.cs integrated with MainMenuManager
- [x] **Name entry overlay** — Earthbound-style letter grid name entry (NameEntryScreen). In-apartment overlay, arrow/WASD navigation, mouse click support. Calls DayManager.BeginDay1() on confirm. Save-aware: skips if save exists for active slot.
- [ ] **Name entry (mirror scene)** — Separate scene (hard cut). 3D Nema model with loop animation in front of bathroom mirror. Deferred to later.
- [ ] **Profanity filter** — Block slurs/bad words in name input. Word list + substring check.
- [ ] **Photo intro sequence** — Nema poses, camera takes photo, B&W filter, photo placed next to personals ad in newspaper. Cinematic transition.
- [ ] **Half-folded newspaper visual** — Rework newspaper mesh/canvas to cliche folded-in-half look. 3 personal ads + 2 commercial slots per day.
- [ ] **Couch win scene** — Date succeeds → couch cuddling scene, Nema holding scissors behind her back. Separate camera angle.
- [x] **Flower trimming transition** — FlowerTrimmingBridge loads scene additively, DayPhaseManager orchestrates fade/title/scene. Done via VS-1b.

### VS-1b: Flower ↔ Apartment Integration (Done)

Each date character brings a specific flower. The flower trimming score determines how long the plant lives in the apartment and affects next-day mess.

- [x] **Flower scene transition** — `FlowerTrimmingBridge` loads flower trimming scene additively, instantiates flower prefab, captures `FlowerSessionController.OnResult`, spawns living plant, unloads scene. `DayPhaseManager` routes to flower trimming coroutine when `DateSessionManager.PendingFlowerPrefab != null`.
- [x] **DayPhaseManager flower phase** — `DayPhase.FlowerTrimming` between DateInProgress and Evening. Triggers scene load, waits for result, records score.
- [x] **Flower results → save data** — `DateOutcomeCapture` captures flower fields (hadFlowerTrim, flowerScore, flowerDaysAlive, flowerGrade, flowerWasGameOver). `DateHistory.UpdateFlowerResult()` retroactively updates most recent entry. `IrisSaveData.LivingPlantRecord` persists plant state.
- [x] **Living plant in apartment** — `LivingFlowerPlant` (health decreases by 1/totalDaysAlive each day, color lerps green→yellow→brown, scale shrinks, ReactableTag with plant/flower/gift tags). `LivingFlowerPlantManager` (4 slots, `AdvanceAllPlants()` wired to `GameClock.OnDayStarted`, feeds MoodMachine "LivingPlants" source).
- [x] **Flower score → mess intensity** — `MessBlueprint` flower conditions: `requireBadFlowerTrim` (score < 40), `requireGoodFlowerTrim` (score >= 80). SOs: Petal_Debris (bad trim), Stem_Clippings (any trim), Wilted_Leaves (day 3+).
- [x] **Flower score on calendar** — `ApartmentCalendar` shows flower grade with ✂ icon alongside date grade in grid cells. Detail panel shows "Flower: B (62pts, 5 days)".
- [x] **Per-character flower types** — `flowerSceneName` wired on all 4 date characters (Livii, Sterling, Sage, Clover) to `Daisy_Flower_Scene`.

### VS-2: Preparation Phase (Partially Built)

- [x] **Cleaning** — CleaningManager with sponge only (spray removed for now, may return for harder stains)
- [x] **Object grab/place** — ObjectGrabber (working, two-sided wall detection for pictures)
- [x] **Record player** — RecordPlayerManager (working)
- [x] **Perfume spray** — PerfumeBottle + MoodMachine (working, re-sprayable — can switch between perfumes)
- [x] **Coffee table books** — 5 upright books on shelf, click to toggle flat on coffee table (working)
- [x] ~~**Trinkets on shelf**~~ — Trinket system removed; drawers still available for item storage
- [x] **Preparation timer UI** — Countdown panel (top-right) wired to DayPhaseManager. Auto-shows/hides on prep start/end.
- [x] **Outfit selection** — OutfitSelector system. Player chooses outfit during prep. Date judges in Phase 1 via EntranceJudgmentSequence.
- [ ] **Perfect pour mechanic** — Shared one-shot click-timing game used by both plant watering and drink making. Single click at right moment for perfect pour.
- [x] **Pre-spawned mess** — DailyMessSpawner spawns trash subset + misplaces entrance items each morning. MessBlueprint SOs for flower-condition mess.
- [ ] **Plant watering rework** — Convert WateringManager to use perfect-pour mechanic

### VS-3: Date Phase Rework

- [x] **DateSessionManager 3 phases** — Arrival, DrinkJudging, ApartmentJudging (framework done)
- [x] **DateCharacterController excursion gating** — Only wanders in Phase 3 (done)
- [x] **ReactableTag system** — Static registry, date NPC discovery (done)
- [x] **DateReactionUI** — Thought bubble with emotes (done)
- [x] **Phase 1 rework (Entrance)** — EntranceJudgmentSequence: 4 sequential judgments (outfit, perfume, welcome, cleanliness) with thought bubble + emote + SFX. Cleanliness via TidyScorer.
- [ ] **Phase 2 rework (Kitchen Drinks)** — Date stands by kitchen counter. Player sees drink recipe HUD, selects correct alcohol bottle from shelf, does perfect-pour. Score → date reacts.
- [ ] **Phase 3 living room flow** — Date walks to living room with drink. Investigates: coffee table book, vinyl playing, perfume scent, shelf items, apartment cleanliness.
- [ ] **Phase pass/fail gating** — If Phase 1 or 2 fails badly, date can leave early.

### VS-4: Nema's Life Systems (Design Doc: DESIGN_NEMA_LIFE.md)

- [x] **Calendar system** — ApartmentCalendar: clickable 7-day grid with date history, flower grades, learned preferences per day. Built by ApartmentSceneBuilder.
- [ ] **Time gates** — Mail arrives at 4pm daily, dates don't start until 8pm. GameClock expansion.
- [ ] **Mail system** — Daily 4pm delivery via GameClock event. Contains: newspaper, letters from dates, bills/flyers, missing person notices (escalating horror). SO-driven content pool.
- [ ] **Nema placeholder** — Visible player character in kitchen and living room. Contextual idle animations (leaning on counter, sitting on couch, swaying to music, reading mail). `NemaController` with room-aware state machine. All meshes/animations swappable via SO/prefab references.
- [ ] **Disco ball** — Clickable ceiling object, triggers Nema dance animation at current position. Particle effects (mirror reflections). Swappable mesh + particles.
- [ ] **Date disappearance mechanic** — Dates that go "very well" (high affection) → person vanishes. Stops appearing in newspaper, doesn't respond to calls. No explicit violence — horror through implication.
- [ ] **Souvenir system** — After date disappears, one of their items appears in apartment (necklace, ring, hat, sweater, watch, scarf). Persistent across days. Accumulates. New dates react to them via ReactableTag ("whose ring is that?"). `SouvenirDefinition` SO per item.
- [ ] **Repeat dates** — Same person returns, remembers previous visits. Relationship deepens. Eventually they might disappear too.
- [ ] **Convention demo mode** — 7-minute timer, curated slice of the full loop.
- [x] **Feedback system** — PlaytestFeedbackForm (F8) + BugReportForm (F9) with Discord webhooks + local JSON/screenshot backup. DiscordWebhookConfig SO in Resources.
- [x] **Save game system** — IrisSaveData with AutoSaveController. Auto-saves on quit + end of date. Persists calendar day, date history, plant records, apartment layout. SaveManager with slot system.
- [ ] **Player knowledge system (dating journal)** — Per-date, per-phase insight unlocks. Even if rejected at Phase 2, player keeps Phase 1+2 knowledge. Reveals DatePreferences progressively. Journal UI accessible from apartment (notebook/phone). Tracks: preferences learned, times encountered, highest phase reached, disappeared status. Integrates with ReactionEvaluator + DateEndScreen.

### VS-5: Deferred

- [ ] **Bathroom mirror scene** — Separate scene with hard cut. 3D Nema, mirror, name entry. Will be added later.
- [ ] **Scissors cutting mechanic** — Code preserved (`ScissorsCutController`, `CutPathEvaluator`, `NewspaperSurface`). Not active in vertical slice.
- [ ] **Additional apartment areas** — Cozy Corner, Watering Nook, Flower Room, Bathroom. Entrance is now built (3rd area with shoe rack, coat rack, DropZones).
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
- [x] **Accessibility** — Full settings suite: 15 settings across 5 categories (Visual, Motion, Audio, Timing, Performance). Tabbed SettingsPanel UI. CaptionDisplay, AccessibleText + IrisTextTheme for centralized font control
- [x] **Input rebinding** — `InputRebindManager` static utility with JSON override persistence, `InputOverrideLoader` MonoBehaviour for wiring, `InputRebindUI` with interactive rebinding rows
- [x] **Performance quality presets** — `IrisQualityPreset` ScriptableObject (sap, decals, physics, UI tuning), `IrisQualityManager` scene-scoped singleton, `QualityDropdownUI` for TMP_Dropdown binding
- [x] **Save/Load system** — `SessionSaveData` serializable container, `SaveManager` static utility persisting rolling 50-session history to `iris_sessions.json`
- [x] **Analytics hooks** — `IrisAnalytics` static utility logging timestamped JSON-lines telemetry (cuts, detachments, sessions) to `iris_analytics.jsonl`
- [x] **Localization** — `LocalizationTable` ScriptableObject for per-language string tables, `LocalizationManager` static utility with key-based lookup, language switching, English fallback, PlayerPrefs persistence
- [x] **Crash reporting** — `CrashReporter` static utility hooking `Application.logMessageReceived`, captures errors/exceptions/asserts to rolling `iris_crashlog.txt` with dedup suppression
- [x] **Highlight style system** — 3 switchable styles (Outline, RimGlow, SolidOverlay) via `InteractableHighlight.CurrentStyle`. F5 cycles, F3 panel sliders for width/alpha/pulse/rim. Stencil anti-overdraw on complex geometry. Proximity stage removed (single clean hover).
- [x] **Atmosphere system** — `AtmosphereController` with global URP Volume (LiftGammaGain teal shadows/warm highlights, bloom, vignette, film grain, dust motes). FF8/PE/SotC color grading. F3 panel sliders for all parameters.
- [x] **Volumetric light shafts** — `VolumetricLightShaft` + `Iris/VolumetricShaft` shader. PS2-style fake god rays: place quad at window, auto-tints with directional light, fades at night. Animated noise for dusty feel.
- [x] **Per-object PSX settings** — `PSXObjectSettings` component. MaterialPropertyBlock overrides for vertex snap resolution, affine intensity, shadow dither per object. -1 = use global, 0 = disabled, >0 = custom. No material instances created.
- [x] **Grab feel presets** — 5 physics presets for ObjectGrabber (Default, Plucky, Floaty, Snappy, Heavy). F6 cycles, F3 panel sliders for spring/damper/accel/speed. Plucky preset gives resistant leaf-pulling Nintendo-like feel.
- [x] **Item pairing system** — `PairableItem` component. Shoes: SpecificPartner mode, SideBySide snap. Dishes/Bowls: AnyOfCategory mode, Stacked snap. ObjectGrabber intercepts click-while-holding to pair. Per-item snap offset, snap sound. Coat removed from ItemCategory, Bowl added.
- [x] **Discord playtest reporting** — `BugReportForm` (F9) + `PlaytestFeedbackForm` (F8) Discord webhook integration. JPEG screenshots, game state telemetry, crash log tail. `DiscordWebhookConfig` SO in Resources. Uses `System.Net.Http.HttpClient` (Unity 6 curl HTTP/2 workaround).

### Phase 14: UX Polish Pass (Done — April 2026)
- [x] **Drag-to-pour mechanic** — Click to start, pull mouse down to control pour rate. Quadratic ease-in. Cursor rotates to show tilt. `PourDragHelper` shared by WateringManager + SimpleDrinkManager. `PourCursorOverlay` for visual feedback.
- [x] **Smooth context cursor fading** — Pre-baked alpha bank (16 steps per cursor). Fade in/out on hover enter/exit. Hover-fade to 45% after 2s. `CursorFadeSettings` SO with Inspector AnimationCurves. RaycastAll sorted by distance.
- [x] **Visibility eye indicators** — World-space open/closed eye icons per item. Flash all at exploration start. Fires on placement + drawer open/close. AnimationCurve-driven fade. `VisibilityEyeIndicator` auto-spawns.
- [x] **Cubby capacity enforcement** — `DrawerController.HasInteriorCapacity` checks item count on interior surface. ObjectGrabber rejects placement when full ("No room in here.").
- [x] **Fridge rejection text** — "I don't want anything right now." with 5s cooldown when clicking fridge outside drink phase.
- [x] **Quit-to-menu transition** — Fade screen + audio over 0.8s, start menu song while black, then load.
- [x] **Cursor hidden while holding** — No grab cursor; held object IS the feedback.
- [x] **Cursor shadow scaling** — Shadow grows with camera distance to maintain screen-space size.
- [x] **Album art for records** — `RecordDefinition.albumArt` Texture2D field, auto-applied by RecordItem.
- [x] **Audio loss fix** — SFX cutoff volume ratchet-down bug + music unduck after flower trimming.
- [x] **Event leak fix** — DateSessionManager unsubscribes OnReaction before destroying date character.
- [x] **Debug log cleanup** — 13 info-level logs wrapped in `#if UNITY_EDITOR`.

---

## Architecture Notes

### Key Design Patterns
- **Event-driven:** UnityEvents for loose coupling (OnGameOver, OnResult, OnEquipScissors)
- **Data-driven:** ScriptableObjects for flower definitions (IdealFlowerDefinition)
- **Singleton:** AudioManager (persistent across scenes)
- **Object pooling:** SapDecalPool, SapParticleController
- **Static registries:** PlaceableObject.All, PlacementSurface.All, StemPieceMarker.All, ReactableTag.All, DateHistory.Entries, TimeScaleManager priorities

### Subsystems
| Subsystem | Key Scripts |
|-----------|------------|
| Input/Control | CuttingPlaneController, PlaneAngleTiltController, ScissorStation |
| Mesh Cutting | PlaneBehaviour, AngleStagePlaneBehaviour, VirtualStemCutter |
| Game Logic | FlowerGameBrain, FlowerSessionController, FlowerStemRuntime |
| Physics/Joints | XYTetherJoint, FlowerJointRebinder, SquishMove |
| Fluids/VFX | FlowerSapController, SapParticleController, SapDecalPool |
| UI | FlowerGradingUI, FlowerHUD_GameplayFeedback, FlowerHUD_DebugTelemetry |
| Audio | AudioManager (6-channel, SFX auto-cutoff), MusicDirector, JointBreakAudioResponder |
| Data | IdealFlowerDefinition, FlowerTypeDefinition, DatePersonalDefinition, DatePreferences |
| Apartment Hub | ApartmentManager, StationRoot, ObjectGrabber, MoodMachine, FridgeController, ApartmentAreaDefinition, InteractableHighlight |
| Camera Presets | CameraPresetDefinition, CameraTestController, CameraPresetDefinitionEditor (gizmos + capture) |
| Context Cursors | GlobalCursorManager (9 types, alpha bank fade), CursorFadeSettings SO, CursorWorldShadow, PourCursorOverlay |
| Visibility | VisibilityEyeIndicator, ReactableTag.OnPrivacyChanged, DrawerController.OnDrawerPrivacyChanged |
| Bookcase Station | BookVolume, PerfumeBottle, DrawerController (cubby storage + privacy + capacity) |
| Dating Loop | DateSessionManager (3-phase), GameClock, PhoneController, DateCharacterController, ReactableTag, CoffeeTableDelivery, DayPhaseManager |
| Newspaper | NewspaperManager (button-based), NewspaperAdSlot, DayManager, NewspaperSurface |
| Mechanics | SimpleDrinkManager, CleaningManager, WateringManager, PourDragHelper (shared drag-to-pour), MirrorMakeupManager, RecordSlot/RecordItem |

### Creating a New Flower Level (Quick Start)
1. Import your flower model into the scene
2. Open **Window > Iris > Flower Auto Setup**
3. Select the flower root in the hierarchy
4. Verify detected parts (stem, crown, leaves, petals)
5. Click **Setup Flower** - all components are auto-wired
6. Adjust the generated IdealFlowerDefinition values for scoring
7. Wire UI events (OnGameOver, OnResult) to your HUD prefabs
8. Test with the F3 debug telemetry HUD
