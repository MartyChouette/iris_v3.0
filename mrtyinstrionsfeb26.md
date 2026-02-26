# MARTY IS SLEEPY INSTRUCTIONS

All the manual Unity Editor steps you need to do but were too sleepy to do right now. This doc gets updated as more things come up.

---

## 1. GameModeConfig -- Prep Duration

The code default for `prepDuration` was changed to 900s, but the existing `.asset` files still have the old value (120s) baked in. Serialized asset values override code defaults.

**What to do:**
1. Open each `GameModeConfig` SO in the Inspector (`Assets/ScriptableObjects/GameModes/`)
2. Change `prepDuration` from 120 to 900 (or whatever you want per mode)

---

## 2. GameModeConfig -- Date Phase Duration

A `datePhaseDuration` field was added to `GameModeConfig`. The "7 Minutes" mode is set to 180s (3-minute date). Other modes default to 0, which falls through to `DateSessionManager`'s built-in 40s.

**What to do:**
- If you want longer dates in other modes, open those `GameModeConfig` SOs and set `datePhaseDuration` to your desired value

---

## 3. Perfume Bottles -- Scene Placement

Perfume bottles are not auto-placed in the apartment scene. They need manual placement or scene builder integration.

**What to do:**
1. Place `PerfumeBottle` prefabs/GOs in the apartment scene manually (living room shelf, bathroom, etc.)
2. Each needs: `PerfumeBottle` component + `PerfumeDefinition` SO reference + `PlaceableObject` + `ReactableTag`
3. OR ask Claude to add them to `ApartmentSceneBuilder` for auto-generation

---

## 4. Dirty Dishes -- MessBlueprint SOs

The mess system supports spawning dirty dishes as authored messes. You just need to create the SO assets.

**What to do:**
1. Right-click in `Assets/ScriptableObjects/Messes/`
2. Create new `MessBlueprint` SO
3. Set `messType` = **Object**
4. Assign `objectPrefab` to a dish mesh, OR use procedural box (set `objectScale` + `objectColor`)
5. Set conditions (e.g. `requireDateSuccess = true` for dishes left after a good date)
6. `AuthoredMessSpawner` picks these up automatically each morning

---

## 5. Disco Ball -- Scene Setup

The disco ball system is fully coded but needs manual scene wiring.

### 5a. Create the Disco Ball GameObject

1. Create a new empty GO named `DiscoBall` in the apartment scene (living room area)
2. Add a **sphere mesh** child (the mirrored ball visual) -- scale it small, like 0.15
3. Add components to the root GO:
   - `DiscoBallController`
   - `PlacementSurface`
   - `ReactableTag` -- tags: `light`, `disco`, `party` -- set `isActive` to **false**
   - `InteractableHighlight`
   - `Collider` (BoxCollider or SphereCollider)
4. Add a child **Spotlight** (Light > Spot Light):
   - Point it roughly 45 degrees upward
   - Assign it to `DiscoBallController._spotlight` in the Inspector
5. Add a child empty GO named `BulbSnapPoint`:
   - Position it where you want the bulb to visually sit (e.g. just below the ball)
   - Assign it to `DiscoBallController._bulbSnapPoint`
6. Assign the sphere mesh transform to `DiscoBallController._ballVisual`

### 5b. Create 4 Bulb GameObjects

For each bulb:
1. Create a small colored sphere GO (scale ~0.05)
2. Add components:
   - `PlaceableObject`
   - `DiscoBallBulb`
   - `ReactableTag`
   - `Rigidbody` (use gravity, not kinematic)
   - `Collider` (SphereCollider)
3. Place them on a shelf or table where the player can grab them

### 5c. Create 4 Disco Bulb Definition SOs

In `Assets/ScriptableObjects/DiscoBall/`, right-click > **Create > Iris > Disco Bulb Definition** for each:

| SO Name | Pattern | Color | Cycle Colors | Gradient | Speed | Mood |
|---------|---------|-------|--------------|----------|-------|------|
| `Bulb_ColorCircles` | ColorCircles | White | Yes | Rainbow | 2.0 | 0.7 |
| `Bulb_MirrorClassic` | MirrorGrid | Warm white (0.95, 0.9, 0.8) | No | -- | 4.0 | 0.5 |
| `Bulb_Pinpoints` | Pinpoints | Amber (1.0, 0.7, 0.3) | No | -- | 1.5 | 0.4 |
| `Bulb_Prism` | Prism | White | Yes | Prism rainbow | 1.0 | 0.6 |

### 5d. Wire It Up

1. Assign each SO to its corresponding bulb GO's `DiscoBallBulb._definition` field
2. Optionally assign `_toggleSFX` and `_insertSFX` audio clips on the `DiscoBallController`
3. Make sure the disco ball GO and bulb GOs are on the **Placeables** layer (so ObjectGrabber can raycast them)

### 5e. Verify

1. Grab a bulb, drop it on the disco ball -- should auto-turn on, spotlight projects pattern
2. Click the disco ball -- toggles off/on
3. Swap bulbs -- old one ejects to home position, new pattern loads
4. Check MoodMachine debug panel (F3) shows "DiscoBall" source while active

---

*This doc will be updated with more instructions as the session continues.*
