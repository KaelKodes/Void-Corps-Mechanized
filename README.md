# Void Corps: Mechanize

**Version 0.2.2** — Surface warfare in the Void Corps universe.

**Corps** are organizations — guild-scale to large businesses — fighting for territory. You join an upstart corp, rise with it, and share in its wins.

**Manufacturers** (Brimforge, OuroTech, Trinova, Lumina Vaultworks) license MAP kit. They are not Corps. Rumors say the Big Four quietly back some orgs; that is not public knowledge at campaign start.

When orbital claims stall, corps deploy **MAP** detachments — Mechanized Armor Pilots in licensed chassis. Unmanned **MAD** chassis (Mechanized Armor Drones) fill cheaper kill-slots.

> *When the claim isn't settled in orbit, MAPs settle it on the surface.*

North star: territory control facilitated by mechanized battles (Foxhole-like). Campaign is linear with branching paths; a later persistent seasonal MMO layer for player-run corps is deferred.

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
