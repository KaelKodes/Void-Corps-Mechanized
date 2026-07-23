# Void Corps: Mechanize

**Version 0.2.2.3** — Surface warfare in the Mechanize setting.

**Factions:** Cat-folk and dog-folk from different homeworlds in the same solar system (names TBD). Campaign start picks **Cat** or **Dog**.

**Corps** are organizations — guild-scale to large businesses — fighting for territory. You hire into a frontier company, rise with it, and share in its wins.

**Manufacturers** license MAP kit. They are not Corps and not factions.
- **Ashwhisk** / **Velhound** = Cat / Dog primary houses (faction-locked).
- **Big Four** (Brimforge, OuroTech, Trinova, Lumina Vaultworks) = independent; sell to both / highest bidder.
Rumors that the Big Four quietly favor a side or back some orgs are not public knowledge at campaign start.

When orbital claims stall, companies and corps deploy **MAP** detachments — Mechanized Armor Pilots in licensed chassis. Unmanned **MAD** chassis (Mechanized Armor Drones) fill cheaper kill-slots.

North star: territory control facilitated by mechanized battles (Foxhole-like). Campaign is linear with branching paths; a later persistent seasonal MMO layer for player-run corps is deferred.

Lore lock: `.cursor/rules/void-corps-lore.mdc` · campaign notes: `CAMPAIGN_FOUNDATION.md`.

## What's new in 0.2.2.3

Pilot awareness and cockpit comfort — targeting you can use, seat adjust, and less default X-ray noise.

### Sensors & targeting
- **TAB** locks hostile **fodder** as well as MAPs; threat screen shows a single HULL pool for fodder.
- **Red lock chevron** above the current target (through walls when in front of camera).
- **Off-view lock arrow** on Screen_Threat points toward a locked contact behind / off-screen.
- **Green ally chevrons** for same-team fodder (and allied MAPs) inside close vision range.
- Live occlusion X-ray silhouettes stay **off** unless Contact Sweep / sensor reveal is active.
- Passive contact scan + world blips (ability-gated) continue as last-known awareness.

### Cockpit
- **Alt+F** then hold **F** for seat adjust (scroll / LMB / RMB; **F+Q** resets); no lever gaze required.
- **O** minimizes / extends the window objective panel (preference saved).
- Arena perimeter walls raised so booster hops stay inside the claim.

### Meta & assets
- Stock legs carry jump + dash mobility modules.
- `BUY_LIST.md` for wishlisted marketplace packs not purchased yet.
- Audio folder layout cleanup (combat / mech / voice / weapons / ambient).

## What's new in 0.2.2.2

Cleanup, better MAP visuals, and replacing legacy floating UI with cockpit / mode-aware HUD.

### Skirmish & multiplayer
- Skirmish no longer opens the campaign hangar — pick map/mode, then one of **four premade loaner kits** (same designs OK in co-op).
- Loaners spawn **pristine** (garage wear does not leak onto skirmish / academy / convention kits).
- Multiplayer lobby polish (roster, chat, skirmish premade pick) for co-op detachments.

### First-person cockpit HUD
- **HUD bars** setting: **Auto** (default) / First Person (panels) / Overlay — panels in FP, floating HUD in third person when Auto.
- Diegetic dashboard screens: sensors, integrity (+ PWR/SPD), weapons/modules, tactical placeholder.
- Cockpit glass: left = chassis heat, right = operational power; **overall chassis heat** warning bar under the crosshair (appears at 60%+, orange → deep red, fades out when cooling).
- Sensor binds (**TAB** / **C** / **X**) live beside `// SENSORS` on Screen_Threat; floating `SYSTEM LOCK: no system lock` removed in FP.
- Damage Sustained VO only when a **component is destroyed**, not on every hit.

### Mech designs & audio
- Authored torso scenes for manufacturer kits (Ashwhisk, Brimforge, Lumina, OuroTech, Velhound, Fleet polish).
- Soft-tail / cockpit hull registry / part-visual tscn pipeline notes.
- Footstep packs, dry-fire / overheat / steam SFX, and audio bus layout updates.

## What's new in 0.2.2.1

### Fleet cockpit & diegetic HUD
- Trinova **Fleet Intermediate** torso (`torso_fleet`) — authored hollow cockpit scene with dashboard CRTs.
- **First person:** dashboard screens show sensor lock, integrity schematic, and weapons/modules; flat HUD columns hide while PWR/HEAT stay on flank meters.
- **Crosshair** styles (cross / chevron / X) from weapon type; wide while moving, precise when planted; sprint blocks fire.
- Cockpit camera anchors to `CockpitAnchor`; encased power core stays out of the viewport.
- **Mech 2.0 planning:** pelvis buffer between torso and legs noted in `MECH_DESIGN.md` (legs clipping into FP cockpit — design pass deferred).

## What's new in 0.2.2

### Campaign — frontier Job Convention
- Solar campaign onboarding keeps academy → Job Convention, but signs you to a **frontier company** (not a Big Four manufacturer).
- Four companies drawn from a larger pool each run; mining escorts are company-owned.
- Solar system map + settlement-arc scaffolding for the long campaign path.

### Combat camera & piloting
- **P** toggles first-person (head-anchored) ↔ third-person combat camera.
- **First person:** WASD steers like third person; **Q/E** strafe; mouse stays visible and aims weapons (including pitch for gimbals / elevating guns); arrow keys or **Alt+mouse** temporary head look (~35°, release snaps center).
- **Ctrl+scroll** speed governor (both camera modes); **Ctrl+middle-click** resets to full speed. **SPD** meter appears only while limited.
- Third person keeps scroll-while-firing barrel elevation for Titan band work.

### Sensors & targeting
- **TAB** cycles sensor lock, **X** clears, **C** cycles focus band (head / torso / legs / arms).
- Enemy integrity-style schematic on the HUD; soft aim assist + preferred-part hits toward the focused band.

### Hangar & parts meta
- Hangar **Selection Display** — selected part stats with direct compare vs equipped.
- Part condition / ownership / repair / materials / tech-tree scaffolding for lasting loadouts.

### Mission pressure
- Capture (single + multi) and Mining Escort no longer go quiet after the opener: a mid-progress fodder wave telegraphs in at a randomized threshold, and secured objectives sometimes drop a counter MAP.

### Deployment & field systems
- Deployment director / telegraph for inbound drops; field part crates; damage assessment UI; leg animator and related mech polish.

## Run

**Preferred:** download a build from [Releases](https://github.com/KaelKodes/Void-Corps-Mechanized/releases), unzip, and run the executable.

**From source (development):**
1. Open this folder in **Godot 4.6** (C# / .NET).
2. Let it restore NuGet / build the `Mechanize` solution.
3. Press Play — main scene is the **main menu** (`scenes/main_menu.tscn`); claims run in `scenes/arena.tscn`.
4. Campaign → academy / Job Convention for the solar path, or Skirmish for mission picks (including Sabotage Run).

## Controls

| Input | Action |
|-------|--------|
| W / S | Drive forward / reverse |
| A / D | Turn (locked legs) / strafe (gimbaled legs) |
| Q / E | Strafe (first person) |
| Mouse | Aim (gimbals / elevating guns track cursor) |
| Arrows / Alt+mouse | Temporary head look in first person |
| P | Toggle first / third person camera |
| Ctrl + scroll | Speed governor |
| Ctrl + middle-click | Reset speed to full |
| LMB / RMB | Primary / secondary weapons |
| Scroll (3rd person, while firing) | Barrel elevation |
| TAB / X / C | Sensor lock next / clear / focus band |
| 1-6 | MAP abilities |
| Hold Backspace | Self-destruct (deny the asset) |
| F | Interact / extract / plant / escort mount |
| T | Field garage (after fight starts) |
| READY | Prep screen deploy → 5..1 FIGHT |

## Manufacturers on the roster

- **Brimforge** — heavy kinetic / deny-the-asset
- **OuroTech** — precision gimbals / seekers / sharpshooters
- **Trinova** — logistics utility / mend / balanced frames
- **Lumina Vaultworks** — experimental energy / shroud / crawlers

## Layout

```
scenes/     Claim sites, main menu, solar map, mech
scripts/    mech, data, combat, arena, campaign, ai, ui, missions
audio/      soundtrack + BullethellTracks (Sabotage)
shaders/    combat VFX helpers
```

`Promo Videos/` and `resources/` are local-only (gitignored) — promo footage and scratch UI concepts, not game content.
