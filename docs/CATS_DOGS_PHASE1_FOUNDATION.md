# Cats vs Dogs — Phase 1 Foundation

**Status:** Implemented (checkpoint before galaxy hub).  
**Plan:** Cats vs Dogs Master Plan — foundation first, then Phase 2 galaxy.

## What shipped

### Identity
- Forced create wizard: pilot callsign + Cat/Dog + portrait (`faction_pick.tscn` / `FactionPickUi`).
- No auto-created slot 0 on fresh install.
- Profile hub shows portrait, faction, campaign scrap, and skirmish scrap/record.

### Three bags (per slot)
| File | Role |
|------|------|
| `campaign_profile.json` | Solar campaign economy (was `profile.json`) |
| `roguelike_profile.json` | Rogue-Like kit / run economy |
| `skirmish_profile.json` | Sandbox scrap, W/L, mirrored unlocks |

Legacy `profile.json` migrates to `campaign_profile.json` on reconcile.

### Economy firewall
- Skirmish matches commit only to the skirmish bag.
- Campaign / RL earns apply to their bag, then **copy scrap** into skirmish and **mirror unlocks** (blueprints + missing part types as copies).
- Skirmish spend never reduces campaign/RL scrap.

### Menu flow
`Intro → Create/Select Profile → Profile Hub → Mode Hub → Skirmish / Campaign / Rogue-Like / Multiplayer`

### Multiplayer (Phase 1 honesty)
- Co-op map modes are labeled **Co-op Rogue-Like** (solar co-op deferred to Phase 2).
- Skirmish / PvP modes use the skirmish bag.
- Shared loot pool, weakest-player intersection, ProgressEvents → Phase 2.

## Checkpoint checklist

- [x] Fresh install forces name / faction / portrait
- [x] Three bags persist independently
- [x] Skirmish scrap spend cannot reduce campaign/RL scrap
- [x] Campaign/RL scrap earn increases skirmish scrap
- [x] Skirmish loot lands on skirmish bag only
- [x] RL death wipe remains RL-scoped
- [x] Wipe / new slot does not auto-spawn a default pilot
- [x] This doc marks foundation done; galaxy is next

## Phase 2 (do not start until checkpoint review)

Galaxy hub: Cat/Dog homeworlds, PvE globes, hazard PvEvP battlefronts, RL destination unlock, skirmish as endgame ladder. See master plan.
