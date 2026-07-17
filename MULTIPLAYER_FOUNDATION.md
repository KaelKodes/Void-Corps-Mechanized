# Multiplayer Foundation

Design lock for introducing multiplayer. This is not an implementation pass yet — it records agreed scope, caps, and architecture so co-op and skirmish can ship without painting us into a corner for later mass battles.

Persistent territory / seasonal MMO remains a **later** design pass. Do not stub world-persistence systems casually; use co-op campaign + skirmishes to gather feedback first.

## Goals

1. **Co-op campaign** — up to **4** pilots on one claim detachment.
2. **Skirmishes** — up to **10v10** now; architecture aimed at eventual large-scale battles (toward **150v150**).
3. **No ops overhead for now** — players host on their own machines; friends connect directly.
4. **Later option** — hosted lobbies / relay / dedicated sim when join friction or scale demands it.

## Session model (near-term)

**Listen server (peer host)**

- One player hosts; their game is the match authority.
- Guests connect directly (IP / join address).
- Host simulates combat, mission resolve, and (for campaign) map progression.
- Clients send input and prep choices; host replicates state.

**Join friction (accepted for v1)**

- Pure direct connect may require port forwarding or fail on strict NAT.
- That is still “no servers we operate.”
- Steam/EOS peer networking or our own lobbies/relay are optional later improvements — still not the mass-battle sim itself.

**Scale reality**

| Mode | Cap (now) | Host model (now) | Later if needed |
|------|-----------|------------------|-----------------|
| Co-op campaign | 2–4 | Listen server | Stays small / peer-hosted |
| Skirmish | 10v10 (20) | Listen server for testing | Dedicated / relay when needed |
| Mass battle (north star) | toward 150v150 | Not peer-hosted | Dedicated sim + interest management |

A player’s machine will not host 150v150. The north star shapes **how we write the combat pipe**, not what we build in the first spike.

## Product order

### Phase A — Co-op campaign (primary)

- Max **4** wing MAPs, one host.
- **Host owns** `CampaignRun` (sector map, node clears, sponsor payouts).
- Guests bring their own loadout / profile identity.
- All participants fight on the same team in existing claim missions.
- Victory / defeat resolves once on the host → host save updates.
- Guests get debrief + optional shared scrap / part rewards into **their** profiles; map progress stays on the host.
- READY gate: everyone readies in garage; host starts countdown when all ready (timeout policy TBD in implementation).
- Disconnect: host continues (empty slot or AI fill TBD); if host leaves, session ends.

**Copy / fantasy**

- Party = host detachment + wing MAPs.
- **Corps** = organizations; **manufacturers** = kit brands only.
- Do not present manufacturer→corp backing as established public fact.

### Phase B — Skirmishes (feedback lab)

- Same net stack and arena loop; different lobby rules.
- Near-term modes: co-op vs AI, then player vs player / corp vs corp framing.
- Cap **10v10** while listen-server is the host model.
- No campaign save coupling — isolate sync, balance, and “does this feel like Void Corps” feedback.
- Use telemetry and playtests here to inform any later mass-battle design.

### Phase C — Later (explicit non-goals for now)

- Hosted lobbies / matchmaking / relay.
- Dedicated simulation servers.
- Persistent territory map / seasonal MMO wars.

## Architecture principles (avoid small-party traps)

These apply even while the first spike is 2–4 players:

1. **Slots and teams, not “the one local hero”** — arena is N pilots on teams. Expand beyond binary `Player` / `Enemy` when skirmish needs corp A vs corp B (or Alpha / Bravo).
2. **Authority behind a clear boundary** — host-authoritative today; the same match loop should be movable onto a dedicated process later.
3. **Replication with room for interest management** — 10v10 can replicate the whole map; design so nearby-only streaming can be added for hundreds of combatants.
4. **Entity budgets** — caps / LOD paths for MADs, props, and projectiles so 20-player fights stay viable and large-scale remains conceivable.
5. **Session ≠ world** — lobby → match → teardown. No persistent shared world in early multiplayer.

## Deliberate non-decisions

Left flexible until implementation / playtest:

1. Exact scrap / part split for co-op guests.
2. AI fill vs empty slot when a wing disconnects mid-claim.
3. READY timeout vs host-only force-start.
4. Direct-connect UX (raw IP vs short join code generated locally).
5. When to adopt Steam/EOS vs first-party lobbies.
6. Exact team enum / corp-slot schema for PvP skirmish.
7. Mass-battle map size, tick rate, and AOI radii.

## Recommended implementation order

1. Net bootstrap: host / join UI, peer IDs, disconnect handling.
2. Two MAPs in arena, same team, host-auth movement + weapons.
3. Shared match phases (prep → countdown → fight → results).
4. Stretch to **4** co-op + wire into campaign deploy (host map → shared arena).
5. Skirmish lobby: team slots, **10v10** cap, PvE then PvP.
6. Measure CPU / bandwidth / feel → decide when dedicated is required for bigger fights.
7. Only then: lobby/relay ops and any mass-battle design pass.

## Relationship to other docs

- `CAMPAIGN_FOUNDATION.md` — solo / co-op story spine and profile scaffolding.
- `README.md` / lore rules — Mechanize product title; MAP/MAD; Corps vs manufacturers; MMO deferred.
