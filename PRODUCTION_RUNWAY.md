# Production Runway Plan — Raspberry Rum

**Project:** Iris (working title)
**Studio:** Raspberry Rum
**Team Size:** 2
**Date:** February 17, 2026
**Document Version:** 1.0

---

## Team Roster

| Role | Person | Responsibilities |
|------|--------|-----------------|
| Programmer | Marty | All code, systems, tools, Unity pipeline, scene builders, integration |
| 2D/3D Artist | (Artist) | All 2D art, 3D modeling, texturing, UI art, visual polish |
| Shared | Both | Creative direction, art direction, narrative design, music selection, playtesting, business |

---

## Project Snapshot (Where We Are)

**Engine:** Unity 6.0.3, URP, New Input System, Cinemachine 3
**Codebase:** ~14,800 lines across 98+ scripts, 8 major subsystems
**Quality Score:** 8/10 (production-ready for prototype/thesis)

### What's Built and Working

| System | Status |
|--------|--------|
| Apartment hub (7 areas, spline-dolly camera) | Working |
| Newspaper ad selection (button-based) | Working |
| Date session (3-phase: Arrival, DrinkJudging, ApartmentJudging) | Working |
| Date character (NavMesh NPC, 7-state FSM, reactions) | Working |
| Bookcase station (11-state FSM, books, perfumes, trinkets, drawers) | Working |
| Record player station | Working |
| Drink making station | Working |
| Cleaning (sponge + spray, stubbornness, evaporation) | Working |
| Object grab/place (spring-damper, grid snap, wall mount) | Working |
| Perfume spray (one-click, MoodMachine integration) | Working |
| Ambient watering | Working |
| Mirror makeup (texture painting, stickers, smear) | Working |
| MoodMachine (global mood system: light, ambient, fog, rain) | Working |
| Camera preset system (A/B/C comparison, VolumeProfile, light overrides) | Working |
| Game clock (7-day calendar, real-time ticking) | Working |
| Day phase management (Morning/Exploration/DateInProgress/Evening) | Working |
| Flower trimming (scoring brain, virtual cut, grading) | Working |
| Date end screen (letter grades S/A/B/C/D) | Working |
| Hover highlight (rim light on mouse-over) | Working |
| Save/load (session history, placeable layout persistence) | Partial |

### What's Not Built Yet

| System | Priority |
|--------|----------|
| Main menu scene | VS-Critical |
| Tutorial card overlay | VS-Critical |
| Half-folded newspaper visual | VS-Critical |
| Outfit selection system | VS-Critical |
| Perfect pour mechanic (shared: watering + drinks) | VS-Critical |
| Date Phase 1 rework (entrance judgments) | VS-Critical |
| Date Phase 2 rework (kitchen counter, recipe HUD) | VS-Critical |
| Pre-spawned mess (Day 1 heavy preset) | VS-Critical |
| Couch win scene + flower trimming transition | VS-Critical |
| Nema player character (visible in apartment, contextual idles) | VS-High |
| Calendar system (physical object, clickable) | VS-High |
| Save game system (full state persistence) | VS-High |
| Name entry / mirror scene | Deferred |
| Photo intro sequence | Deferred |
| Convention demo mode (7-min timer) | Demo-Critical |
| Feedback overlay | Demo-Critical |

---

## Week 0: Immediate Actions (Feb 17 — Feb 21)

**Theme:** Lock down appointments, validate prototype, sprint kickoff

### Appointments to Schedule

| Who | Purpose | Deadline |
|-----|---------|----------|
| Instructors (thesis committee) | Progress check-in, milestone review, GDC prep feedback | Book by Feb 19 |
| Naomi (internal advisor) | Creative review, narrative direction sign-off, GDC strategy | Book by Feb 19 |
| Sean Perry | External review, industry feedback, networking intros | Book by Feb 21 |

### Sprint 1: Playtest Systems (2 Days — Feb 17-18)

**Owner:** Marty (Programmer)
**Goal:** Demonstrate all interactive systems to confirm the prototype functions as intended.

Playtest checklist (each system must be walked through and verified):

- [ ] Apartment browsing (A/D navigation, area transitions, hover highlights)
- [ ] Object grab and place (pick up, move between rooms, wall mount, grid snap)
- [ ] Cleaning (sponge wipe, spray-then-wipe, stubbornness, evaporation)
- [ ] Bookcase (pull book, read, put back, drawers, perfume spray, trinket inspect)
- [ ] Record player (browse records, play/stop, MoodMachine integration)
- [ ] Drink making (recipe selection, pouring, stirring, scoring)
- [ ] Watering (click plant, hold pour, release to score)
- [ ] Newspaper (read spread, select personal ad, phone call flow)
- [ ] Date session (arrival, drink judging, apartment judging, reactions, grading)
- [ ] Day phase flow (Morning → Exploration → DateInProgress → Evening)
- [ ] Camera presets (1/2/3 switch, VolumeProfile, light overrides)
- [ ] MoodMachine (sources: perfume, record, time-of-day)

**Deliverable:** Bug list + confirmation status for each system. Written sign-off that prototype is functioning as intended.

### GDC Early Registration

**URGENT:** GDC early rate ends **February 20, 2026**. Decide on pass type and register before the deadline.

| Pass Type | What You Get |
|-----------|-------------|
| All-Access | Full conference + expo + summits + parties |
| Core + Summits | Main conference + Independent Games Summit |
| Expo+ | Expo floor + select sessions (cheapest option) |

---

## Week 1: Content + Art Sprint (Feb 24 — Feb 28)

**Theme:** Audio integration, art assets in, narrative content written

### Sprint 2: Audio + Art Integration (5 Days)

#### Audio Integration

| Task | Owner | Notes |
|------|-------|-------|
| Import audio assets into Unity project | Marty | Organize under `Assets/Audio/` |
| Wire SFX to existing systems via AudioManager | Marty | Scissor cuts, UI clicks, phone ring, doorbell, reactions |
| Wire ambient audio to MoodMachine | Marty | Rain, wind, room tone per mood level |
| Wire music to RecordPlayerManager | Marty | Each RecordDefinition gets an audio clip |
| Test audio mix across all stations | Both | Volume balance pass |

#### Art Asset Integration

| Task | Owner | Notes |
|------|-------|-------|
| Import 3D apartment model updates | Artist | Replace procedural boxes where ready |
| Import character placeholder for Nema | Artist | Simple humanoid, idle animation |
| Import date character models (or improved placeholders) | Artist | 4 characters: Livii, Sterling, Sage, Clover |
| UI art pass (newspaper, HUD elements, buttons) | Artist | Replace programmer art |
| Book cover textures | Artist | 15 unique BookDefinition covers |
| Create placeholder flower assets (if new) | Artist | For trimming scene transition |

### Nema Content Writing (Both — Shared Task)

#### Nema Backstory

Write the character bible for Nema. This drives narrative tone, UI text, dialogue, and environmental storytelling.

| Element | Notes |
|---------|-------|
| Full name and age | Who is Nema? Surface-level identity |
| Occupation / cover story | What does the world think she does? |
| Apartment history | How long has she lived here? Why this apartment? |
| Relationship with flowers | Why flower trimming? Therapeutic? Obsessive? |
| Serial killer implication | How aware is she? Is it compulsive? Deliberate? Dissociative? |
| Tone of internal monologue | Chipper? Deadpan? Anxious? Detached? |
| Relationship with dates | Does she genuinely like them? Feel remorse? |
| What the player knows vs. what Nema knows | Dramatic irony framework |

**Deliverable:** `NEMA_BIBLE.md` — 1-2 pages of character context that all content decisions reference.

#### Mess Narratives

Each morning the apartment has a mess from the previous night. Day 1 is pre-authored. Later days are generated from date outcomes.

| Day | Mess Source | Example Items |
|-----|-----------|---------------|
| Day 1 (Pre-authored) | "Last night was rough" — implied previous date gone wrong | Wine stains on carpet, overturned bottle on counter, broken glass near couch, damp towel in bathroom, smudged mirror, half-eaten food on table |
| Day 2+ (Date went poorly) | Date left in a hurry | Knocked-over drink, untouched appetizer, crumpled napkin |
| Day 2+ (Date went well) | Date "disappeared" | Two wine glasses (one with lipstick), scattered clothes, an unfamiliar belonging, a faint perfume not yours |
| Day 2+ (Date was neutral) | Uneventful evening | General clutter, dishes in sink, empty bottles |

For each mess narrative, define:

```
MessNarrative:
  - dayCondition (Day1 / DateWentPoorly / DateWentWell / DateWentNeutral)
  - stainSlots[] (which of the 8 stain quads activate, what texture)
  - scatteredObjects[] (PlaceableObject refs with spawn positions)
  - narrativeHint (string — Nema's morning thought, e.g. "Where did they go?")
```

**Deliverable:** `MESS_NARRATIVES.md` — all mess scenarios written out. Minimum 4 (one per outcome type).

---

## Weeks 2-3: GDC Preparation (Mar 1 — Mar 8)

**Theme:** Press kit, pitch materials, meeting targets, social presence launch

### GDC 2026 — March 9-13, Moscone Center, San Francisco

#### Meetings to Book (Do This NOW — Slots Fill Fast)

Use **MeetToMatch** and **Game Connection** platforms to schedule meetings. Start booking immediately.

| Category | Who to Talk To | Why |
|----------|---------------|-----|
| **Publishers (Narrative/Indie)** | Annapurna Interactive | Narrative-driven indie games, horror adjacent |
| | Devolver Digital | Weird, bold, indie-first publisher |
| | Fellow Traveller | Narrative games specialist |
| | Finji | Small team narrative games |
| | Raw Fury | Indie-friendly, creative freedom |
| | Armor Games Studios | Small-scope narrative titles |
| | Serenity Forge | Story-driven emotional games |
| | Akupara Games | Narrative indie publisher |
| **Platform Holders** | Xbox ID@Xbox | Indie program, potential showcase |
| | PlayStation Indies | Indie spotlight program |
| | Nintendo Indie World | If targeting Switch |
| | Steam (Valve) | Storefront featuring, visibility rounds |
| **Funding / Accelerators** | Indie Fund | No-strings investment collective |
| | Superhot Presents | Indie incubator from SUPERHOT team |
| **Networking** | Independent Games Summit attendees | Peer connections, shared struggles |
| | GDC Parties / Mixers | Informal publisher and press connections |
| | Day of the Devs | If accepting demo submissions |

#### Pitch Preparation

| Material | Description | Owner | Deadline |
|----------|-------------|-------|----------|
| Elevator pitch (30 sec) | "Iris is a [genre] game about [core hook] where [unique twist]" | Both | Mar 1 |
| 2-minute pitch | Expanded: gameplay loop, horror layer, audience, timeline | Both | Mar 1 |
| Pitch deck (10-15 slides) | Title, hook, gameplay, screenshots, team, timeline, ask | Both | Mar 5 |
| Playable demo build | Stable vertical slice or curated 7-min convention demo | Marty | Mar 7 |
| Business cards | Studio name, contact info, game title, QR to press kit | Both | Order by Feb 24 |

#### Press Kit

A professional press kit hosted online (presskit.html or dedicated page). Must include:

| Element | Status |
|---------|--------|
| Studio logo (Raspberry Rum) | Needed |
| Game logo (Iris or final title) | Needed |
| Studio description (1 paragraph) | Write |
| Game description (2-3 paragraphs) | Write |
| Factsheet (platform, engine, team size, release window, genre) | Write |
| Screenshots (minimum 5, 1920x1080, no UI debug overlays) | Capture |
| GIF or short gameplay clip (15-30 sec) | Capture |
| Trailer (if available, even rough) | Stretch goal |
| Team bios + photos | Write |
| Contact email | Set up |
| Social media links | Set up |

**Tool:** Use [presskit()](https://dopresskit.com/) or host a simple page on itch.io or a custom site.

**Deliverable:** Live press kit URL by March 5.

---

## Funding & Grants — Apply Now

Applications take time. Start these immediately and work on them in parallel with sprints.

| Opportunity | Amount | Deadline / Status | Notes |
|-------------|--------|-------------------|-------|
| **Epic MegaGrants** | Varies ($5K-$500K) | Submissions Jun 29 — Sep 4, 2026 | Prepare materials now, submit in summer window |
| **Indie Fund** | Investment (not grant) | Rolling applications | Need playable build, submit after GDC |
| **DANGEN Entertainment Grant** | $50K total pool | Check deadline | Need playable PC build with clear art direction |
| **Canada Council — Explore and Create** | Varies | Rolling deadlines | If either team member is Canadian |
| **Canada Media Fund** | Varies | Check provincial programs | CMF + Creative BC for BC-based studios |
| **Ontario Creates** | Varies | Program-specific | If Ontario-based |
| **Weird Ghosts** | Mentorship + funding | Check current cohort | Women and marginalized founders in Canada |
| **Unity for Humanity** | $25K per grant | Annual cycle | Social impact angle if applicable |

**Action Items:**
- [ ] Research eligibility for each grant (geographic, entity type, project stage)
- [ ] Identify 3-5 best-fit grants and calendar their deadlines
- [ ] Begin writing grant application narratives (reuse pitch deck content)
- [ ] Prepare a budget breakdown (what the money would fund)

---

## Social Media & Public Presence

### Launch Checklist (Start This Week)

| Platform | Handle | Purpose | Frequency |
|----------|--------|---------|-----------|
| Twitter/X | @RaspberryRumDev (or similar) | Dev updates, GIFs, screenshots, community | 2-3x/week |
| Instagram | @raspberryrum.studio | Visual posts, behind-the-scenes, art process | 1-2x/week |
| TikTok | @raspberryrum | Short dev clips, satisfying mechanics, "indie dev life" | 1-2x/week |
| Bluesky | @raspberryrum | Mirror Twitter content, growing indie dev community | 1-2x/week |
| YouTube | Raspberry Rum | Devlogs (monthly), trailer (when ready) | Monthly |

### Content Calendar — First 4 Weeks

| Week | Post Ideas |
|------|-----------|
| Week 1 (Feb 24) | Studio announcement post ("We're Raspberry Rum, making Iris"), first screenshot or GIF of apartment browsing |
| Week 2 (Mar 3) | Mechanic spotlight: flower trimming GIF, "the scissors feel", short clip of cutting |
| Week 3 (Mar 10) | GDC week: "We're at GDC!", hallway photos, people met, games played |
| Week 4 (Mar 17) | Post-GDC recap: what we learned, who we met, what's next |

### Content Pillars (Recurring Themes)

1. **Mechanic Spotlights** — short GIF/video of a single system (trimming, cleaning, dating reactions)
2. **Art Process** — before/after of programmer art → real art, 3D modeling progress
3. **Narrative Teasers** — cryptic hints about Nema, the horror layer, "what happened last night?"
4. **Indie Dev Life** — honest updates, struggles, wins, studio cat photos
5. **Community Questions** — polls ("what would you do on a date?"), engagement bait

### Public Dev Journal

| Option | Pros | Cons |
|--------|------|------|
| **itch.io devlog** | Built-in audience, free, game page + devlog in one place | Limited formatting |
| **Substack / newsletter** | Email list building, long-form, professional | Separate from game page |
| **Studio website blog** | Full control, SEO, press kit colocated | Requires setup |
| **TIGSource / IndieDB** | Established indie communities | Older platforms, less traffic |

**Recommendation:** Start with **itch.io devlog** (lowest friction, game page doubles as demo host) and cross-post highlights to social media.

**Posting cadence:** Biweekly (every 2 weeks). Each entry covers: what was built, what's next, one interesting problem solved, one screenshot/GIF.

---

## Steam Store Page — Goal & Timeline

### Why Early

Steam wishlists are the primary driver of indie game visibility. The earlier the page goes live, the more wishlists accumulate before launch. A "Coming Soon" page with no release date is standard.

### Minimum Requirements for Store Page

| Element | Status | Notes |
|---------|--------|-------|
| Steamworks developer account | Needed | $100 app credit fee |
| Store page assets (capsule images, screenshots) | Needed | 5+ screenshots, header capsule (460x215), library hero (3840x1240) |
| Game description (short + long) | Write | Reuse press kit copy |
| Genre tags | Define | Simulation, Indie, Horror, Dating Sim, Casual |
| System requirements | Define | Estimate from Unity 6 URP baseline |
| Trailer | Stretch goal | Even a 30-sec teaser helps conversion |
| "Coming Soon" — no release date | Target | Don't commit to a date yet |

### Steam Page Goal

**Target:** Store page live by **May 2026** (post-GDC, after art pass, when screenshots are representative).

**Stretch:** Store page live by **April 2026** if GDC generates publisher interest that benefits from a live page.

### Milestone Checklist

- [ ] Register Steamworks developer account ($100)
- [ ] Create app, fill out store page fields
- [ ] Commission or create capsule art (header, hero, logo)
- [ ] Capture 5-8 polished screenshots (post-art-integration)
- [ ] Write store description (short blurb + detailed)
- [ ] Set genre tags and categories
- [ ] Submit for review (takes ~3-5 business days)
- [ ] Page goes live as "Coming Soon"
- [ ] Share wishlist link on all social channels

---

## Feedback Mechanism

### In-Game Feedback (Convention Demo / Playtests)

When any timed session ends (convention demo 7-min, playtest session, vertical slice endpoint), show a feedback overlay:

| Field | Type | Notes |
|-------|------|-------|
| Overall rating | 1-5 stars or emoji scale | Quick gut reaction |
| "What did you enjoy most?" | Free text (optional) | Open-ended positive |
| "What confused you?" | Free text (optional) | Friction points |
| "Would you play more?" | Yes / Maybe / No | Purchase intent signal |
| Contact (optional) | Email field | For follow-up, beta invites |

Data saved to local JSON (`feedback_<timestamp>.json`). Non-intrusive — player can skip with one click.

### External Feedback Collection

| Method | When | Tool |
|--------|------|------|
| Google Form link on itch.io page | Always | Google Forms |
| QR code on business card → feedback form | GDC / events | Google Forms or Typeform |
| Discord server | Post-GDC | Discord (free) |
| Playtest signup form | Ongoing | Google Forms → mailing list |

---

## Prototype Confirmation Checklist

Before GDC or any external showing, the prototype must be confirmed working end-to-end.

### Full Loop Verification

- [ ] Game launches to main menu (or placeholder start)
- [ ] Newspaper displays 3 personal ads correctly
- [ ] Player can select an ad and trigger phone call
- [ ] Phone rings, player answers, date arrives
- [ ] Date walks in, sits on couch (NavMesh stable)
- [ ] Drink making → coffee table delivery → date reacts
- [ ] Date excursions to ReactableTags (books, records, perfume, plants)
- [ ] Date reactions display correctly (thought bubble, emote, SFX)
- [ ] Date session ends, grade screen shows
- [ ] Day transitions work (Morning → Evening → next day)
- [ ] No crashes over a full day loop
- [ ] Camera presets apply correctly (1/2/3 keys)
- [ ] MoodMachine responds to player actions
- [ ] Audio plays without errors (or gracefully silent if no clips assigned)

### Known Issues to Resolve

(Fill in from Sprint 1 playtest bug list)

---

## Master Timeline — Feb through May 2026

```
FEB 17-21  Week 0    Appointments, playtest sprint, GDC registration
FEB 24-28  Week 1    Audio + art sprint, Nema content, mess narratives
MAR 1-7    Week 2    GDC prep: press kit, pitch deck, demo build polish
MAR 9-13   GDC       Meetings, networking, publisher pitches, feedback
MAR 17-28  Weeks 4-5 Post-GDC follow-ups, grant applications started
APR        Month 3   Art integration, vertical slice systems, Steam page prep
MAY        Month 4   Steam "Coming Soon" page live, continued development
JUN-SEP    Months 5-8 Epic MegaGrants window, continued production
```

### Weekly Rhythm (Ongoing)

| Day | Focus |
|-----|-------|
| Monday | Sprint planning, priority check, social post |
| Tuesday-Thursday | Heads-down production (code / art) |
| Friday | Playtest, bug fixes, social post, devlog notes |
| Weekend | Content writing (narrative, grant apps), rest |

---

## Appendix A: GDC 2026 Quick Reference

| Detail | Info |
|--------|------|
| Dates | March 9-13, 2026 |
| Location | Moscone Center, San Francisco |
| Early rate deadline | **February 20, 2026** |
| Key summit | Independent Games Summit |
| Meeting platforms | MeetToMatch, Game Connection |
| Party list | gdcparties.com |
| Format | Rebranded as "GDC Festival of Gaming" |

## Appendix B: Key Contacts & Outreach Tracker

| Contact | Role | Status | Next Step |
|---------|------|--------|-----------|
| Instructors | Thesis committee | Not scheduled | Book this week |
| Naomi | Internal advisor | Not scheduled | Book this week |
| Sean Perry | External advisor | Not scheduled | Book this week |
| (Publisher 1) | — | — | Research + email after pitch deck done |
| (Publisher 2) | — | — | Research + email after pitch deck done |

## Appendix C: Document References

| Document | Location | Purpose |
|----------|----------|---------|
| Long-term dev plan | `LONGTERM_PLAN.md` | Technical roadmap, phase tracking |
| Nema life design | `DESIGN_NEMA_LIFE.md` | Game design: narrative, mechanics, save system |
| Codebase assessment | `CODEBASE_QUALITY_ASSESSMENT.md` | Technical audit, quality score |
| Dev journal | `DEV_JOURNAL.md` | Session-by-session development notes |
| This document | `PRODUCTION_RUNWAY.md` | Production plan, business, GDC, grants, marketing |
