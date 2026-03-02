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

## ~~5. Disco Ball -- Scene Setup~~ ✅ DONE

~~Automated by **Window > Iris > Setup Disco Ball** (`Assets/Editor/DiscoBallSetup.cs`). Creates 4 DiscoBulbDefinition SOs + spawns DiscoBall and 4 bulb GameObjects with all references wired.~~

After running the tool, manually adjust:
1. Position the DiscoBall GO where you want it (default near ceiling)
2. Move bulb GOs to a shelf/table
3. Optionally assign `_toggleSFX` and `_insertSFX` audio clips on `DiscoBallController`
4. Set layer to **Placeables** on disco ball + bulbs (so ObjectGrabber can raycast them)

---

## ~~6. Mirror -- Scene Setup~~ ✅ DONE

~~1. Select the mirror mesh/quad in the scene~~
~~2. Assign `Assets/Materials/Mirror.mat` to its Renderer~~
~~3. Add component: **PlanarMirror**~~
~~4. Make sure the quad's **blue arrow (forward)** points toward the camera/player~~
~~5. Optional: tweak `Texture Width`/`Texture Height` (default 256x192), `Skip Frames` for performance~~

---

## 7. Cursor World Shadow

1. Select a Managers GO (or create an empty)
2. Add component: **CursorWorldShadow**
3. Set **Surface Layers** to the layers you want the shadow to project on (Default + Surfaces)
4. Adjust **Diameter** (default 0.3m) to taste
5. Auto-hides when ObjectGrabber is holding something
6. **Cursor texture**: If a `CursorContext` component exists in the scene, the shadow auto-matches the active cursor image. Otherwise, drag a cursor texture into the **Cursor Texture** field on the component

---

## ~~8. PSXLitGlitch Shader -- Messy/Trash Items~~ ✅ DONE

~~For items that should look glitchy when out of place (trash, askew items):~~

~~1. Assign `Assets/Materials/PSXLit_Glitch.mat` to the item's Renderer, OR change existing material shader to `Iris/PSXLitGlitch`~~
~~2. Set `Glitch Intensity` slider (0 = normal PSXLit, 0.5 = moderate, 1 = heavy corruption)~~
~~3. To drive from code: `renderer.material.SetFloat("_GlitchIntensity", value);`~~

**Auto-applied:** `AuthoredMessSpawner` now applies the PSXLitGlitch shader to all procedural trash objects automatically. Optionally drag `PSXLit_Glitch.mat` into the **Trash Material** field on the spawner for explicit control. For prefab-based trash, assign the material on the prefab itself.

---

## 9. Dishevel System -- Messy Item Tilting

On any `PlaceableObject` that should start tilted/crooked when messy:

1. Check **Can Be Dishelved** in the Inspector
2. `Dishevel Angle` defaults to 25 degrees (the threshold before it counts as messy)
3. Call `placeableObject.Dishevel()` from spawners to apply random tilt
4. Player picks up → auto-straightens → places down straight
5. Player scroll-rotating after placement does NOT count as disheveled

---

## 10. PrepChecklistPanel -- Date Prep Guide

1. Add `PrepChecklistPanel` component to a Managers GO
2. Toggle with **Numpad 1** — shows what the selected date likes with live completion checkmarks
3. Auto-hides when date phase starts

---

## 11. DateItemHighlighter -- Item Glow Guide

1. Add `DateItemHighlighter` component to a Managers GO
2. Toggle with **Numpad 3** — green rim on liked items, red rim on disliked items
3. Re-scans every 0.5s, auto-clears on phase change

---

## ~~12. Wall Materials -- See-Through for Date NPC~~ ✅ DONE

~~1. Assign `Assets/Materials/PSXLit_Dissovable.mat` (shader `Iris/PSXLitDissolvable`) to apartment wall materials~~
~~2. `WallOcclusionFader` drives `_DissolveAmount` when the date NPC is behind walls~~

---

## 13. Glass & Light Switch Audio — Wire in Inspector

`PlaceableObject` now has **Audio Overrides** fields (`_pickupSFXOverride`, `_placeSFXOverride`). ObjectGrabber uses these instead of the global default when set.

**Glass items** (perfume bottles, disco ball bulbs):
1. Select each glass PlaceableObject in the scene
2. Drag `Assets/Audio/glass_pickup_ES_Button Press Click, Tap, Video Game, Main Menu, Select 01 - Epidemic Sound.wav` into both **Pickup SFX Override** and **Place SFX Override**

**Disco ball toggle**:
1. Select the `DiscoBall` GO
2. Drag `Assets/Audio/light_switch_ES_Clothes Peg, Click - Epidemic Sound.wav` into `DiscoBallController._toggleSFX`

---

*This doc will be updated with more instructions as the session continues.*
