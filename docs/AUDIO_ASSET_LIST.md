# Iris v3.0 — Audio Asset Shopping List

Every serialized AudioClip field in the codebase, grouped by category.
Fields marked with the same reuse tag can share a single clip.

---

## 1. Ambient / Environment Loops

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 1 | MoodMachine | `_ambienceClip` | Soft indoor room tone — gentle hum of an apartment (fridge buzz, distant traffic, clock tick). Looping, ~60s. Volume driven by mood curve (louder when calm, quieter in storms). |
| 2 | MoodMachine | `_weatherClip` | Rain on windows / distant thunder. Looping, ~60s. Fades in as mood darkens. Should layer cleanly over room tone. |
| 3 | DayPhaseManager | `_morningAmbienceClip` | Bright morning atmosphere — birdsong outside window, soft sunlight warmth feel. Looping, ~30-60s. Plays during newspaper reading. |
| 4 | DayPhaseManager | `_explorationAmbienceClip` | Daytime apartment bustle — slightly more energetic than room tone. Looping, ~30-60s. Plays during preparation phase. |

---

## 2. Music — Vinyl Records (5 tracks)

Each is a `RecordDefinition` SO with a `musicClip` field. These play on the in-game record player, so they should sound like actual vinyl records the character owns.

| # | Record SO | Sound Needed |
|---|-----------|-------------|
| 5 | Record 1 | Warm lo-fi / bossa nova instrumental. Cozy, date-appropriate. 2-3 min loop. |
| 6 | Record 2 | Dreamy shoegaze / ambient. Atmospheric, slightly melancholy. 2-3 min loop. |
| 7 | Record 3 | Upbeat indie pop instrumental. Cheerful energy. 2-3 min loop. |
| 8 | Record 4 | Smooth jazz / piano. Sophisticated, evening mood. 2-3 min loop. |
| 9 | Record 5 | Classical guitar / folk. Intimate, gentle. 2-3 min loop. |

---

## 3. Apartment Interaction SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 10 | ApartmentManager | `_areaTransitionSFX` | Soft UI blip / gentle whoosh — camera moving between apartment areas. Short, non-intrusive. |
| 11 | ObjectGrabber | `_pickupSFX` | Light object pickup — small thud/lift sound, like picking up a mug or book off a surface. |
| 12 | ObjectGrabber | `_placeSFX` | Gentle set-down — object placed on wooden/cloth surface. Soft thunk. |
| 13 | FridgeController | `_openSFX` | Fridge door opening — magnetic seal pop + slight creak. |
| 14 | FridgeController | `_closeSFX` | Fridge door closing — soft thud with seal suction. |
| 15 | DropZone | `_depositSFX` | Item deposited into container — soft clatter of something dropped into a bin or basket. |
| 16 | DropZone | `_trashSFX` | Trash disposal — crinkle/crumple into wastebasket. |
| 17 | DoorGreetingController | `_knockSFX` | Doorbell or knocking — 2-3 knocks on a wooden apartment door. |
| 18 | DoorGreetingController | `_doorOpenSFX` | Door opening — wooden door creak/swing open. |
| 19 | OutfitSelector | `_openSFX` | Wardrobe/closet opening — sliding fabric or wooden drawer. |
| 20 | OutfitSelector | `_selectSFX` | Outfit chosen — fabric rustle / clothes hanger clink. |

---

## 4. Bookcase Station SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 21 | BookInteractionManager | `pullOutSFX` | Book sliding out of shelf — paper/cardboard scrape against wood. |
| 22 | BookInteractionManager | `putBackSFX` | Book pushed back into shelf — reverse of above, soft thud at end. |
| 23 | BookInteractionManager | `hoverSFX` | Subtle book nudge — very quiet shelf creak, finger brushing spine. |
| 24 | BookInteractionManager | `selectSFX` | Book opened — pages fanning open, spine crack. |
| 25 | PerfumeDefinition (SO) | `spraySFX` | Perfume spritz — short aerosol puff, slightly glassy. |

---

## 5. Dating System SFX

| # | Script | Field | Sound Needed | Reuse |
|---|--------|-------|-------------|-------|
| 26 | DateSessionManager | `dateArrivedSFX` | Doorbell chime or gentle notification — "your date is here." | |
| 27 | DateSessionManager | `likeSFX` | Positive reaction — warm chime, sparkle, Sims-style happy sting. | = #31 |
| 28 | DateSessionManager | `dislikeSFX` | Negative reaction — dull thud, descending tone, deflating. | = #32 |
| 29 | DateSessionManager | `phaseTransitionSFX` | Phase change — soft transition whoosh / scene-shift sound. | |
| 30 | DateCharacterController | `investigateSFX` | NPC examining item — curious "hmm" musical cue, inquisitive. | |
| 31 | DateReactionUI | `likeSFX` | (Same as #27 — positive thought bubble pop.) | = #27 |
| 32 | DateReactionUI | `dislikeSFX` | (Same as #28 — negative thought bubble pop.) | = #28 |
| 33 | DateCharacterController | `reactionSFX` | NPC emoting — soft vocal-ish cue, mumble/hum. | |
| 34 | DateEndScreen | `goodDateSFX` | Date success — warm ascending chime, satisfying resolution. |  |
| 35 | DateEndScreen | `badDateSFX` | Date failure — sad descending tone, "womp womp" feeling. | |
| 36 | CoffeeTableDelivery | `deliverSFX` | Drink placed on table — ceramic cup on wood, gentle clink. | |
| 37 | PhoneController | `ringingSFX` | Phone ringing — old rotary phone ring or cute ringtone. 2-3 rings. | = #48 |
| 38 | PhoneController | `pickupSFX` | Phone picked up — receiver lift click. | |
| 39 | PhoneController | `doorbellSFX` | Doorbell — apartment buzzer or chime. | |
| 40 | PhoneController | `callOutgoingSFX` | Outgoing call — dial tone / ringing on player's end. | |
| 41 | EntranceJudgmentSequence | `judgingSFX` | Date forming opinion — thoughtful "hmm" musical cue, suspenseful. | |
| 42 | EntranceJudgmentSequence | `sneezeSFX` | Sneeze — cute cartoon sneeze (reaction to strong perfume). | |
| 43 | FlowerGiftPresenter | `_presentSFX` | Gift given — warm sparkle + gentle fanfare. | |
| 44 | MidDateActionWatcher | `_caughtSFX` | Caught in the act — record scratch or sharp comedic sting. | |

---

## 6. Newspaper / Scissors SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 45 | ScissorsCutController | `cutLoopSFX` | Scissors cutting paper — looping snip/scrape sound while dragging. ~2s loop. |
| 46 | ScissorsCutController | `cutCompleteSFX` | Cut finished — satisfying paper tear/separation + small chime. |
| 47 | NewspaperManager | `dateSelectedSFX` | Ad circled/chosen — pen circle sound or paper tap. |
| 48 | NewspaperManager | `phoneRingSFX` | (Same as #37 — phone ringing during newspaper.) | = #37 |

---

## 7. Cleaning SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 49 | CleaningManager | `wipeSFX` | Sponge wiping surface — wet squeaky scrub on countertop/floor. Short, plays repeatedly. |
| 50 | CleaningManager | `_stainCompleteSFX` | Single stain cleaned — satisfying sparkle/ping. |
| 51 | CleaningManager | `allCleanSFX` | All stains cleaned — triumphant little jingle, "all done!" feeling. |

---

## 8. Watering SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 52 | WateringManager | `plantClickSFX` | Click on plant pot — ceramic tap / hollow pot sound. |
| 53 | WateringManager | `pourSFX` | Water pouring from can — gentle trickling water into soil. Loops while held. |
| 54 | WateringManager | `overflowSFX` | Too much water — sloshing overflow, water spilling over pot rim. |
| 55 | WateringManager | `scoreSFX` | Watering scored — neutral result chime. |
| 56 | WateringManager | `perfectSFX` | Perfect watering — sparkling success sting, plant happy. |
| 57 | WateringManager | `failSFX` | Bad watering — sad droop sound, descending tone. |

---

## 9. Drink Making SFX

| # | Script | Field | Sound Needed | Reuse |
|---|--------|-------|-------------|-------|
| 58 | BottleController | `pourSFX` | Liquid pouring from bottle — glugging into glass. | = #60 |
| 59 | DrinkMakingManager | `stirCompleteSFX` | Spoon stirring done — last clink of spoon on glass rim. | |
| 60 | DrinkMakingManager | `pourCompleteSFX` | Pour finished — bottle set down on counter. | |
| 61 | DrinkMakingManager | `scoreSFX` | Drink scored — neutral evaluation chime. | |
| 62 | SimpleDrinkManager | `pourSFX` | (Same as #58 — liquid pouring.) | = #58 |
| 63 | SimpleDrinkManager | `overflowSFX` | Glass overflowing — liquid spilling over rim onto counter. | |
| 64 | SimpleDrinkManager | `recipeSelectSFX` | Recipe card chosen — paper flip / menu select. | |
| 65 | SimpleDrinkManager | `scoreSFX` | (Same as #61.) | = #61 |
| 66 | SimpleDrinkManager | `perfectSFX` | Perfect drink — celebratory sparkle sting. | |
| 67 | SimpleDrinkManager | `failSFX` | Bad drink — comical failure sound, glass crack or fizz-out. | |

---

## 10. Record Player SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 68 | RecordPlayerManager | `browseSFX` | Flipping through records — vinyl sleeve sliding, cardboard shuffle. |
| 69 | RecordPlayerManager | `playSFX` | Needle drop — vinyl crackle start, tonearm placing down. Iconic. |

---

## 11. Flower Trimming / Cutting SFX

These use a dual-clip system (primary + secondary with slight delay) for natural variation.

| # | Script | Field | Sound Needed | Reuse |
|---|--------|-------|-------------|-------|
| 70 | CuttingPlaneController | `stemCutPrimary` | Scissors cutting through stem — woody snip, firm resistance. | |
| 71 | CuttingPlaneController | `stemCutSecondary` | Stem cut follow-up — fiber snap / settling sound. | |
| 72 | CuttingPlaneController | `leafCutPrimary` | Scissors cutting leaf — softer, wetter snip than stem. | |
| 73 | CuttingPlaneController | `leafCutSecondary` | Leaf cut follow-up — gentle flutter/settle. | |
| 74 | CuttingPlaneController | `petalCutPrimary` | Scissors cutting petal — delicate, almost silent snip. | |
| 75 | CuttingPlaneController | `petalCutSecondary` | Petal cut follow-up — soft falling sound. | |
| 76 | MouseBehaviour (DMC) | `dragStartClip` | Scissor drag begins — metal scrape / scissors opening. | |
| 77 | MouseBehaviour (DMC) | `genericSliceClip` | Generic cutting fallback — all-purpose snip. | |
| 78 | MouseBehaviour (DMC) | `stemSliceClip` | Stem slice — similar to #70 but for drag-cut. | = #70 |
| 79 | MouseBehaviour (DMC) | `leafSliceClip` | Leaf slice — similar to #72. | = #72 |
| 80 | MouseBehaviour (DMC) | `petalSliceClip` | Petal slice — similar to #74. | = #74 |
| 81 | MouseBehaviour (DMC) | `crownSliceClip` | Crown (flower head) cut — dramatic version of stem cut, heavier. | |
| 82 | GrabPull | `leafGrabPrimary` | Grabbing a leaf — rustling, slight tearing start. | |
| 83 | GrabPull | `leafGrabSecondary` | Leaf grab follow-up — stretch/tear. | |
| 84 | GrabPull | `petalGrabPrimary` | Grabbing a petal — soft pinch, delicate. | |
| 85 | GrabPull | `petalGrabSecondary` | Petal grab follow-up — gentle pull. | |
| 86 | GrabPull | `genericGrabPrimary` | Generic grab — general plant handling sound. | |
| 87 | GrabPull | `genericGrabSecondary` | Generic grab follow-up. | |
| 88 | JointBreakAudioResponder | `primaryBreakSound` | Stem/leaf joint snapping — woody crack, branch break. | |
| 89 | JointBreakAudioResponder | `secondaryBreakSound` | Break follow-up — fibers separating, settling. | |

---

## 12. Mirror Makeup SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 90 | MirrorMakeupManager | `paintSFX` | Brush stroke on skin — soft wet brush dragging. |
| 91 | MirrorMakeupManager | `stickerSFX` | Sticker placed on face — satisfying stamp/press. |
| 92 | MirrorMakeupManager | `smearSFX` | Makeup smeared/blended — finger rubbing on skin, soft. |
| 93 | MirrorMakeupManager | `peelSFX` | Sticker peeled from sheet — adhesive peel sound. |

---

## 13. Other Mechanics SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 94 | SharpeningMinigame | `sharpenLoopSFX` | Scissors on whetstone — rhythmic grinding loop, ~2s. |
| 95 | BossFlowerController | `snapSFX` | Thick thorn/branch snapping — heavy woody crack. |
| 96 | GraftingController | `graftSFX` | Successful graft — wet stick + tape wrap, satisfying join. |
| 97 | GraftingController | `failSFX` | Failed graft — wet slide apart, rejection squelch. |
| 98 | PestController | `spreadSFX` | Pest spreading — tiny skittering / buzzing swarm. |
| 99 | PestController | `removeSFX` | Pest removed — squish or flick away. |

---

## 14. UI SFX

| # | Script | Field | Sound Needed |
|---|--------|-------|-------------|
| 100 | TutorialCard | `_dismissSFX` | Card dismissed — page turn / card slide away. |
| 101 | NameEntryScreen | `confirmSFX` | Name confirmed — warm acceptance chime. |
| 102 | NameEntryScreen | `selectLetterSFX` | Key pressed — soft typewriter key click. |
| 103 | FlowerGradingUI | `happyClip` | Good grade — ascending happy sting, triumphant. |
| 104 | FlowerGradingUI | `sadClip` | Bad grade — descending sad sting, deflating. |
| 105 | DayPhaseManager | `nextDaySFX` | New day begins — sunrise chime, fresh start feeling. |
| 106 | DayPhaseManager | `timerWarningSFX` | Timer running out — gentle urgency tick or soft alarm. |

---

## Summary

| Category | Fields | Unique Clips Needed |
|----------|--------|-------------------|
| Ambient loops | 4 | 4 |
| Music (vinyl) | 5 | 5 |
| Apartment interaction | 11 | 11 |
| Bookcase | 5 | 5 |
| Dating | 19 | ~14 (reuse like/dislike/ring) |
| Newspaper/scissors | 4 | ~3 (reuse ring) |
| Cleaning | 3 | 3 |
| Watering | 6 | 6 |
| Drink making | 10 | ~7 (reuse pour/score) |
| Record player | 2 | 2 |
| Flower trimming | 20 | ~14 (reuse slice variants) |
| Mirror makeup | 4 | 4 |
| Other mechanics | 6 | 6 |
| UI | 7 | 7 |
| **Total** | **106** | **~91 unique clips** |

With aggressive reuse (sharing like/dislike, pour variants, cut variants, score chimes), a practical minimum is around **50-60 unique audio files**.
