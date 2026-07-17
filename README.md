# Void Corps: Mechanize

Surface warfare in the Void Corps universe.

When orbital claims stall, corporations deploy **MAP** detachments — Mechanized Armor Pilots in licensed chassis, proprietary parts, and pilots paid to hold unclaimed ground. Unmanned **MAD** chassis (Mechanized Armor Drones) fill the cheaper kill-slots.

> *When the claim isn't settled in orbit, MAPs settle it on the surface.*

## Run

1. Open this folder in **Godot 4.6** (C# / .NET).
2. Let it restore NuGet / build the `Mechanize` solution.
3. Press Play — main scene is a Void Corps **claim site** (`scenes/arena.tscn`).

## Controls

| Input | Action |
|-------|--------|
| W / S | Drive forward / reverse along facing |
| A / D | Turn chassis (locked legs) / strafe (gimbaled legs) |
| Mouse | Aim point |
| LMB | Primary weapon |
| RMB | Secondary weapon |
| 1-6 | Corps abilities |
| Hold Backspace | Self-destruct (deny the asset) |
| T | Field garage (after fight starts) |
| READY | Prep screen deploy → 5..1 FIGHT |

## Corps on the roster

- **Brimforge** — heavy kinetic / deny-the-asset
- **OuroTech** — precision gimbals / seekers / sharpshooters
- **Trinova** — logistics utility / mend / balanced frames
- **Lumina Vaultworks** — experimental energy / shroud / crawlers

## Layout

```
scenes/     Claim sites + Mech
scripts/    mech, data, combat, arena, ai, ui
```
