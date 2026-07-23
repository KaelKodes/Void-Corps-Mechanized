# The Fleas — Titans, Incursions, and Escalation

**Status:** Design lock (systems not built).  
**Related:** Cats vs Dogs galaxy spine, Phase 1 foundation bags, hangar/barracks (TBD).

This doc is the working bible for the third-party threat. Cat and Dog are peer factions. **The Fleas** are not.

---

## 1. Who the Fleas are

| Lock | Detail |
|------|--------|
| Nature | A **people / species** that operates as a **hive organization** |
| Motive | **Hosts** — Matrix-style harvest/harness of populations and infrastructure, not extermination |
| Method | Willing to use a very strong arm; pilots are expendable war cost on the way to the greater population |
| Scale of folk | **Very few flea-folk.** A small command cell runs operations |
| Forces | Command deploys **MADs** in volume. A **Titan** is piloted by at least one flea-folk (possibly several) |
| Name | **The Fleas** for now (other names may be explored later) |

### Not Cat / Dog kit

- Neither Cats nor Dogs have Titans **in the field** (maybe in production rumor; not released).
- Players **never pilot a Titan**.
- When a Titan appears, it is a **universal third-party threat**, not “the other faction’s bigger MAP.”

### Look

- Same used-future military universe as Cat/Dog mechs, **bug-coded**.
- Flea MADs can **lack a clear cockpit** (unmanned read).
- Build the **Flea Fleet / army roster** before fine-tuning every escalation beat. Titans sit at the top of the **ground** food chain.

---

## 2. First contact (horror)

- Hits **mid / late campaign** with almost **no warning**.
- Completely unknown as *this* threat until it arrives.
- Both peoples know ordinary fleas on their worlds; **these are unlike anything either has seen**.
- Mystery largely stays: factions figure it out **in real time**. Full contested endgame is when the war becomes the endless territory tug-of-war.
- Authored story: **two starting points** (Cat path / Dog path) that **meet in the middle** and carry to endgame. Incursion RTS can run **systemic** alongside; story beats are a separate pass.

---

## 3. Pressure model (universal RTS)

Inspired by Helldivers-style bug fronts, adapted to this galaxy.

### Bars

- Track **per-sector** pressure **and** a **global** hive bar.
- Fleas hit in **waves**, dumping what they have stockpiled.
- Their tech is **superior**; **mass production** is the bottleneck (quantity/quality they need).
- Clear a wave globally → **cooldown** while they rebuild → next event.

### Cooldown presence

- **Flea-folk** leave the board during cooldown.
- **MADs** may still linger on the map so space does not go sterile.

### Timers

- Rough window: **~6 hours to a few days** (exact TBD).
- **One active world event** at a time; geography can be spread; **severity varies**.

### Visibility

- **Everyone** sees incursion pings — including when the **other** faction gets hit (satisfaction / dread both welcome).
- Failed / festering sites are also **visible to both factions**.

---

## 4. Severity tiers

Start with **5 tiers**. **Titans only at T5.**

| Tier | Role (working) |
|------|----------------|
| T1–T4 | Escalating MAD / site pressure (roster TBD with Flea Fleet) |
| T5 | Titan-class threat |

Exact rung names wait on the Fleet roster.

---

## 5. Fail and success

### Fail (timer expires, not cleared)

- Incursion **site remains**.
- Severity jumps about **+2 tiers** (cap T5).
- Can spawn **nearby satellite** incursions at **random tiers below** the parent.
- Satellites have **their own timers**.
- **Interior / parent timer is longer** than surrounding satellites.
- Satellites must be dealt with **before** the main site can be challenged again.

Failure **festers and spreads**; it does not simply roll the calendar forward cleanly.

### Success

- Severity in that area **resets to zero**.

### Faction stakes

- Outcome applies to the **entire faction**, whether or not someone participated.
- Participants get **additional** rewards for deploying / surviving.
- Exact reward / punish payloads TBD.
- Later: faction milestones (incursions cleared, Flea units destroyed, etc.) — **visible to both factions**.

### Participant prestige

- **Titan Exp** — tracks time spent engaging a Titan only.
- Meaningless mechanically for now; high-reputation “service hours.”

---

## 6. Own-sector vs Contested

### Own-sector (faction space)

- **Only that faction** can participate.
- Party-focused; larger party sizes; solo is near-suicide.
- **Matchmaking / queue first.**
- Fail-safe after long failed grouping: host your own lobby with **crew** (see Hangar), not anonymous filler bots.
- Crew assists are a mercy so players can *try* — not a fair Titan answer.

### Contested Incursion (deep space / shared hazards)

- **Full PvP.**
- Factions may **invite other-faction members into a party** → temporary **personal alliance** for that map.
- Joining is a **contract until you or they leave the map** — no mid-mission quit-to-betray.
- Loot follows **party leader mode** (Round-Robin / FFA / Master Looter / Roll Rarities) — UI TBD.
- **No bots** in contested; real players only (details TBD).

---

## 7. T5 Titan mission shape (raid split)

Always treat Titan sites like a **raid with two parts** (FF14-style):

1. **Lead-up mission** — smaller party or solo+crew OK; prepare the approach.
2. **Boss fight** — stage up, repair/assemble, then enter the arena.

Campaign Titan encounters: often an entire beat before the Titan arrives; split supports garage between parts.  
Endgame war Titans: flea-folk aboard usually **die with the hulk**.  
Campaign Titan clears: flea-folk more often **taken captive** (use TBD by story).

---

## 8. Hangar, barracks, and crew (related pillar)

Not Flea-only, but required for own-sector fail-safes and PvE fantasy:

- Player maintains a **hangar of multiple mechs**.
- Player maintains a **barracks of pilots** (crew met via story / assembly).
- When other players are unavailable (or preferred), enter **PvE with crew piloting extra mechs**.
- Progression style is player-driven: invest in one main chassis, or kit out the whole wing.
- Full crew/mech systems need their own design pass.

---

## 9. Copy / legacy debt

Current Warning bosses and rival-corp Titan briefs (`BossEncounterCatalog`, etc.) assume **corps field Titans**. Re-aim:

- Standard-scale rival corps / mini-bosses can stay Cat/Dog politics.
- Titan / Warning end-nodes should become **Flea incursion** language over time.
- Do not present Ashwhisk/Velhound or Big Four as Titan suppliers to either faction in early copy.

---

## 10. Open design (iron-out list)

Tracked as follow-up work; do not invent answers in code until locked:

1. **Flea Fleet roster** — unit list, roles, and T1–T5 mapping (Titans = T5 only).
2. **Timer numbers** — exact windows for T1–T5, parent vs satellite lengths, cooldown between global waves.
3. **Reward / punish tables** — faction-wide outcomes + participant bonuses; milestone thresholds (visible to both sides).
4. **Contested matchmaking** — queue rules, party invite UX, loot-mode UI (RR / FFA / Master / Roll).
5. **Dual-path story outline** — Cat start / Dog start / merge / endgame; when horror first sting fires.
6. **Captive flea-folk** — campaign use after Titan capture (story-owned).
7. **Naming pass** — keep “The Fleas” or adopt in-world euphemism + slang.
8. **Hangar + barracks spec** — multi-mech, crew pilots, assignment, progression, own-sector fail-safe deploy.
9. **Legacy Warning/boss copy** — re-aim `BossEncounterCatalog` / rival-corp Titan briefs to Flea incursions.
10. **Titan Exp** — when (if ever) it gates systems beyond prestige tracking.
