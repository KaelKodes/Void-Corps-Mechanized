# Void Corps: Mechanize

**Version 0.2.1** — Surface warfare in the Void Corps universe.

**Corps** are organizations — guild-scale to large businesses — fighting for territory. You join an upstart corp, rise with it, and share in its wins.

**Manufacturers** (Brimforge, OuroTech, Trinova, Lumina Vaultworks) license MAP kit. They are not Corps. Rumors say the Big Four quietly back some orgs; that is not public knowledge at campaign start.

When orbital claims stall, corps deploy **MAP** detachments — Mechanized Armor Pilots in licensed chassis. Unmanned **MAD** chassis (Mechanized Armor Drones) fill cheaper kill-slots.

> *When the claim isn't settled in orbit, MAPs settle it on the surface.*

North star: territory control facilitated by mechanized battles (Foxhole-like). Campaign is linear with branching paths; a later persistent seasonal MMO layer for player-run corps is deferred.

## What's new in 0.2.1

- **Sabotage Run** — new bullet-hell mission type: push a long corridor from Point A to Point B, plant a package, extract at an Exfil Uplink.
- **Music-synced hellfire** — WAV analysis builds a beat map (onsets, BPM, intensity); volleys fire from playback position.
- **Hellfire Turrets** — physical emplacements that fire B→A patterns; taking fire makes them return precise direct shots; destroyable.
- **Echelon Approach** — dedicated long corridor map with dispersed cover and off-map scenery for orientation.
- Mining escort refinements, manufacturer UI marks, voice/oscilloscope work, chassis/catalog durability-power-weight passes, and related campaign/hangar polish.

## Run

1. Open this folder in **Godot 4.6** (C# / .NET).
2. Let it restore NuGet / build the `Mechanize` solution.
3. Press Play — main scene is a **claim site** (`scenes/arena.tscn`).
4. Skirmish → **Sabotage Run** to try the corridor (track: `audio/BullethellTracks/Echelon 5.wav`).

## Controls

| Input | Action |
|-------|--------|
| W / S | Drive forward / reverse along facing |
| A / D | Turn chassis (locked legs) / strafe (gimbaled legs) |
| Mouse | Aim point |
| LMB | Primary weapon |
| RMB | Secondary weapon |
| 1-6 | MAP abilities |
| Hold Backspace | Self-destruct (deny the asset) |
| E | Interact / extract / plant |
| T | Field garage (after fight starts) |
| READY | Prep screen deploy → 5..1 FIGHT |

## Manufacturers on the roster

- **Brimforge** — heavy kinetic / deny-the-asset
- **OuroTech** — precision gimbals / seekers / sharpshooters
- **Trinova** — logistics utility / mend / balanced frames
- **Lumina Vaultworks** — experimental energy / shroud / crawlers

## Layout

```
scenes/     Claim sites + Mech
scripts/    mech, data, combat, arena, ai, ui, missions
audio/      soundtrack + BullethellTracks (Sabotage)
```
