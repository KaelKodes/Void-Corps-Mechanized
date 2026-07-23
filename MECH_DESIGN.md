# Mechanize — Mech Design

This document is the source of truth for MAP and component mechanics. It records intended player-facing behavior, formulas, stat ranges, and decisions before implementation.

## Design workflow

Systems are defined one at a time:

1. Establish the intended combat experience.
2. Give every stat one clear purpose.
3. Remove or rename misleading and redundant stats.
4. Define formulas and useful component ranges.
5. Check manufacturer and tier identities.
6. Implement and validate the design in play.

Status labels:

- **Current** — implemented behavior.
- **Proposed** — candidate design awaiting approval.
- **Decided** — approved design to implement and preserve.
- **Open** — unresolved design question.

## Chassis terminology

- **Mechanize** is the product title.
- **MAP** is a manned pilot chassis.
- **MAD** is an unmanned drone chassis.
- Manufacturers license frames and components; they are not corps and not factions.
- **Universe framing (Cats vs Dogs):** see `.cursor/rules/void-corps-lore.mdc` and `CAMPAIGN_FOUNDATION.md` — Cat/Dog faction at start; Ashwhisk/Velhound faction-locked; Big Four cross-faction. Mech systems in this doc are unchanged by that lock.

## MAP assembly

A MAP currently has ten loadout slots:

1. Legs
2. Torso
3. Head
4. Power core
5. Left arm weapon
6. Right arm weapon
7. Left shoulder
8. Right shoulder
9. Backpack
10. Systems

The torso determines shoulder mounts, backpack mounts, and supported power-core class. Runtime MAP performance is derived from equipped, functioning components.

## System review order

1. Durability and armor — **implemented; awaiting playtest validation**
2. Mobility — **Weight / LoadRating soft overload + FP body-cam controls + thruster/booster wiring**
3. Heat
4. Power — **hybrid preserved (build gate + combat pool)**
5. Sensors and targeting
6. Weapons — **melee + held shield + missile guidance + ballistic magazines shipping**; ballistic/energy damage+heat identity and energy shields deferred
7. Abilities and utility (incl. shield generators / energy shields)
8. Assembly constraints and overall balance

---

# 1. Durability and armor

## Design goals

**Decided direction**

Keep the current component structure. Do not regroup into fewer zones. Each component keeps its own health pool and gains its own explicit armor value so players can compare parts directly ("this arm has more armor than that one") and make their own trade-offs.

Redefine durability around the torso:

- The torso is the MAP's real health. There is no separate abstract hull pool.
- Every other component can be damaged or destroyed while the MAP keeps fighting in a degraded state.
- When the torso is destroyed, the MAP is defeated.

## Previous model (replaced)

Before the durability revamp, a MAP had two overlapping durability layers:

### Chassis hull

The global health pool that ultimately kills the MAP.

```text
Hull HP = max(40, 40 + sum of all functioning parts' Armor and HullBonus)
```

Damage to a component also dealt 12% of the raw hit damage directly to hull. A hit that could not route to a component dealt full raw damage to hull.

### Component integrity

Every non-empty component had its own HP and armor.

Default non-leg component HP:

```text
Component HP = 35 + Part Armor × 1.25
```

Default HP per leg:

```text
Limb HP = 28 + Part Armor × 0.9
```

Biped and tracked leg packages contain two limbs. Hexapod packages contain six.

Default component armor:

```text
Component Armor = Part Armor × 0.35
```

Component damage after mitigation:

```text
Damage taken = Raw damage × 50 / (50 + Component Armor)
```

Part-specific component HP and component armor overrides existed in the data model, but the catalog did not use them.

### Limb-loss shock

Destroyed limbs deal additional direct hull damage based on the MAP's maximum hull:

- Biped or tracks: 22% per limb.
- Hexapod: 16% per limb.

This means complete limb loss deals 44% of maximum hull to a biped/tracked MAP and up to 96% to a hexapod.

### Collapse conditions

A MAP previously collapsed when any of these were true:

- Torso destroyed.
- Hull depleted.
- Five or more hardpoints destroyed.
- Systems destroyed while hull is at or below 5%.
- Head destroyed while hull is at or below 5%.

Complete leg loss immobilizes the MAP but does not directly trigger collapse.

### Repair

Repair can restore hull and damaged living components. It cannot restore destroyed components or lost limbs.

The current mend distribution attempts to reserve 35% for chassis hull, then applies remaining repair to the most damaged living components.

## Risks that motivated the revamp (resolved)

### Armor performs three jobs

One `Armor` value previously:

1. Adds to global hull HP.
2. Increases component HP.
3. Reduces component damage.

This makes armor disproportionately valuable and makes those three durability behaviors difficult to tune independently.

### Hull is partly abstract and partly structural

Every part contributes to global hull, yet each part also has independent integrity. Destroying a component recalculates derived stats but does not resize the existing hull pool during combat.

The player-facing meaning of “hull” is therefore unclear: it behaves partly like core structure and partly like the sum of all installed armor.

### Limb shock dominates structural damage

Limb loss uses a percentage of maximum hull, so heavier MAPs suffer larger absolute shock. A complete hexapod loss nearly depletes hull regardless of its remaining condition.

### Component targeting is spatially approximate

Normal hits route to the nearest living hardpoint from the impact point. Precision attacks can request a preferred slot. Component meshes and hitboxes are not independently authoritative.

## Decided model (Path 1, torso-as-hull)

**Implemented July 18, 2026; playtest validation pending.**

1. **Structure** — Keep all current components as independently damageable parts, each with its own HP pool. No zone consolidation.

2. **Defeat rule** — The torso is the MAP's health. Torso destroyed = MAP defeated. No separate global hull pool, no universal per-hit hull bleed, and no multi-condition collapse checklist.

3. **Non-torso components** — Destroying any non-torso component removes its function but does not directly end the MAP:
   - Head: sensors and precision targeting fail; weak fallback sensors remain.
   - Arm weapon: that weapon is disabled.
   - Legs/mobility: movement degrades per lost limb, down to immobilized.
   - Power core: shutdown or critical state (see Power system review).
   - Shoulder / backpack / systems: their ability or passive effect is lost.

4. **Armor** — Each component carries its own explicit armor value that reduces incoming damage to that component. Armor no longer feeds a global hull total.

## Resolved decisions

**A. Armor is a flat stat (decided).**
Each component's armor is a fixed mitigation value that does not deplete. "This part has more armor" is a permanent, readable property the player weighs when choosing parts. A depleting armor shell is explicitly deferred and may be reconsidered later if fights need more depth.

**B. Pure torso-death (decided).**
Only torso destruction defeats the MAP. Destroying non-torso components removes function but never directly kills. There is no overflow of damage into the torso from other components.

Lore framing: the torso is the pilot's armored shell. Once every external system is destroyed the MAP is combat-neutralized, but the pilot is still sealed inside — it may still have torso-housed missiles or utility, and can still trigger self-destruct to deny the asset.

**Consequence — pass-through fire (decided).**
Once a component is destroyed, hits to that location are no longer absorbed by the dead part; they pass through to whatever sits behind it (normally the torso). A player cannot indefinitely shield the torso behind an already-wrecked arm.

**Consequence — removals.**
The following current mechanics are removed under this model:
- The abstract global hull pool (`40 + sum of all part armor`).
- The universal per-hit hull bleed (12% of every hit).
- The multi-condition collapse checklist and limb-loss hull shock. Losing legs immobilizes; it does not deal torso damage.

## Combat pace (decided)

**Preserve the current feel.** Current combat is fast and 1v1 fights feel great; the existing time-to-kill is the target, not something to change. Being outnumbered (1v2) is lethal and is intended to stay that way.

Anchor: a medium MAP's torso takes roughly **140–160 raw incoming damage after accounting for armor (about 2 seconds at ~70 DPS)** to destroy. The torso-as-hull numbers reproduce this range so the core fight stays fast.

The only intentional feel change is fixing components that are currently far too fragile (notably arm weapons at 35 HP / 0 armor). Non-torso components become slightly more deliberate to destroy, while the torso kill stays at today's pace.

### Implemented starting numbers (playtest validation pending)

Mitigation formula (author `Armor` directly per component; diminishing returns, never 100%):

```text
Damage taken = Raw damage × 50 / (50 + Armor)
```

| Armor | Reduction |
|-------|-----------|
| 0 | 0% |
| 25 | 33% |
| 50 | 50% |
| 100 | 67% |
| 150 | 75% (practical cap) |

Torso starting bands:

| Band | Torso structure | Torso armor | Approx. raw damage to destroy |
|------|-----------------|-------------|--------------------------------|
| Light / precision | 55–70 | 30–42 | 88–129 |
| Balanced | 65–75 | 48–55 | 127–158 |
| Heavy | 85–110 | 80–110 | 221–352 |

Non-torso component starting ranges (fixing the glass-arm problem):

| Component | HP | Armor |
|-----------|-----|-------|
| Head | ~54 | 10–40 |
| Arm (weapon) | ~72 | 15–40 |
| Leg package (shared) | ~67 biped / ~202 hex | per legs |
| Core / systems / shoulder / backpack | ~54 | 10–35 |

Catalogue durability is normalized centrally after tiers are assigned. Manufacturer baselines keep Brimforge toughest, Trinova balanced, and OuroTech/Lumina comparatively glassy. Individual authored armor values remain visible and authoritative.

**Playtest revision (July 19, 2026):** per-limb HP under nearest-hardpoint routing (shared chassis collider, no real limb hitboxes) permanently deleted a biped leg after ~half the package took damage — felt unfair. Legs are now **one shared structure pool** (`StructureHp × limb count`) with soft mobility from remaining HP. Revisit true per-limb hitboxes if we want discrete limb loss again.

**Playtest pad (July 19, 2026):** all catalogue `StructureHp` values × **1.2** after calibration (`CatalogDurability`), including authored torsos.

## Follow-up to investigate (not durability)

- Enemy AI appears able to use the mend beacon more frequently than intended. Review beacon cooldown / AI activation gating when the Abilities system is reviewed.

---

# Shields (decided direction — held shield implemented; generator deferred)

Two complementary defenses sit above passive component armor.

## Held shield (arm slot) — **implemented (slice)**

Occupies a left or right arm weapon slot. Trade: one less gun, active directional protection.

**Controls:** hold the fire bind for that arm to raise; release to lower. The other arm may still fire.

**While raised:**
- Intercepts hits whose impact falls inside a forward defensive arc (relative to upper-body facing).
- Sustained **power draw** while raised (`ShieldPowerPerSec`).
- Absorbed damage converts into heat (`Heat = absorbed × ShieldHeatPerDamage`).
- Absorption is limited by remaining heat headroom. Unconvertible remainder passes through to the struck component (then armor → structure).
- Zero power or overheat forces the shield down.

**On overheat while soaking:**
- The shield **breaks for the remainder of the battle** (until respawn / full rebuild).
- The MAP also enters normal overheat state.

**Slice part:** Trinova Bulwark Plate (`wep_tri_bulwark`) — arc 120°, raise drain 14/s, heat/damage 0.50.

## Shield generator (utility — Systems or Backpack) — deferred

A separate archetype: a temporary whole-MAP shield pool consumed **before** component armor/structure.

Hit resolution order when both are present:

```text
Incoming attack
→ generator shield pool (if any)
→ held shield soak (if raised and in arc)
→ component armor mitigation
→ component structure HP
```

**Proposed traits (open until Abilities review):**
- Small global pool; may regenerate after a quiet period.
- Power cost while active and/or recharging.
- Energy weapons deal bonus damage to generator shields; ballistics remain stronger against structure; energy also applies target heat — **deferred** until energy-shield pass (see Weapons — Ballistic vs Energy).
- Physical vs energy shield split — **deferred** (held plates soak both families today).
- Destroying the generator removes the pool.

## Why this complements durability

- Glass / high-performance parts stay fragile on paper, but players can *choose* to protect them.
- Armor stays a readable passive part stat; shields are the active layer.
- Pure torso-death remains intact — shields delay damage; they do not create a second kill condition.

---

# Melee (contact slice — implemented)

Melee occupies an arm weapon slot. Its gimbaled hardpoint follows the cursor continuously; that movement is the swing. There is no click-to-attack and no authored attack animation.

**Rules:**
- Moving the cursor sweeps the blade segment between its previous and current facing.
- Air movement is free: no heat or active power spend.
- Entering contact with cover, a friendly, or a target engages the arm and adds `HeatPerShot`.
- Damage is applied only to hostile/damageable contacts. Solid cover still creates heat but has no damage API unless it is destructible.
- Remaining inside one collider does not repeatedly deal damage. The blade must clear and cross it again; `FireRate` is the maximum contact cadence.
- The first physical collider blocks whatever is behind it.
- Preferred aimed component applies when combat ID is valid (same as guns).
- Baseline blades use no active combat power; more powerful future melee may author a contact power cost.
- Melee does not use ghost-heat family tax with ballistics/energy.

**Slice part:** Brimforge Forge Cleaver (`wep_brin_cleaver`) — 32 damage, 0.85 max contacts/s, 1.5 m blade reach, 3 heat/contact, 0 active power.

Melee vs held shield uses the normal hit order (soak → armor → structure).

---

# Weapons — Ballistic vs Energy (July 21, 2026)

## Combat identity (intent — damage pass deferred)

Do **not** change hit damage / soak math until the energy-shield pass. Locked fantasy:

| | **Ballistic** | **Energy** |
|--|---------------|------------|
| Structure / kit | Stronger damage | Weaker / baseline |
| Target heat | Little / none | Applies heat to the target (pressure dissipation / overheat) |
| Your sustain | Magazine + reload | Fire while power + heat allow (current model) |
| vs physical plate | Contested later | Punches through / weak soak |
| vs energy barrier | Punches through / weak soak | Contested later |

Ghost-heat family tax (dual same-family arms) remains as today.

## Magazines (ballistic — implementing)

Energy weapons do **not** use magazines.

Ballistic weapons:
- Independent magazine per arm.
- Auto-reload when empty.
- Manual: hold **Reload** (`R`) + that arm’s fire button → reload that weapon (does not fire).
- No movement / sprint modifier on reload speed.
- Authored per part: `MagazineSize`, `ReloadTime`.
- Utility components may later add mag size / reload speed (`MagazineBonus`, `ReloadSpeedBonus` hooks).

## Shields — physical vs energy (deferred pass)

Current held shields soak **both** families — too universal. Split direction:

- **Physical (metal plate)** — blocks ballistic; energy punches through or barely soaks. Bulwark-style stays here.
- **Energy (jackal-style barrier)** — blocks energy; ballistic punches through or barely soaks. New archetype (arm projector and/or generator pool).

Generator shield pool + family damage/heat transfer ship in that focused pass. Stubs only until then.

---

# Missiles (guidance split — first pass)

Missile abilities share `AbilityId.MissileSalvo` but aim via per-part `MissileGuidanceMode`:

| Mode | Input | Fire gate |
|------|-------|-----------|
| **Paint** | Hold → paint world point → release | Combat vision cone on paint point |
| **SensorVision** | Tap with TAB lock | Lock + **in vision** + in weapon range |
| **SensorContact** | Tap with TAB lock | Lock in acquire range (vision optional) + in weapon range |

**Catalogue split (July 19, 2026):** Brimforge / Trinova / Lumina stay **Paint**. OuroTech seekers use sensor lock — Seeker / Needle / Whisper = **SensorVision**; Caliper = **SensorContact** (scanner track). Seekers home on the locked mech / focus band; mid-flight lock loss keeps last velocity. Vision vs contact is always a **weapon** property, not a global sensor rule.

---

# Sensors and targeting (contact scan — first pass)

## Hard rule

No MAP has permanent live X-ray / occlusion silhouette. Cover hides units until a scan pulse stamps a **last-known** blip.

## Head = baseline passive scan

Every living head runs a passive contact pulse within `ScannerRange`:

- Marks **allies and enemies** (team-colored).
- Blips freeze at pulse-time position for ~1.2–2.0s (scales with `ScannerResolution`), then fade.
- Pulses recur on an interval (~2–4s; better resolution = more frequent).
- Not a live mesh ghost — radar / last-known only.
- Destroyed head → blind fallback (short range, LOS-only).
- **World 3D blips stay off by default.** The pulse still writes `LastKnownContacts` for mini-map / cockpit radar.
- Abilities call `SensorContactScan.RevealWorldBlips(duration)` to turn the passive 3D display on for a window (extends if re-triggered). Dedicated part: `AbilityId.ContactReveal` / Contact Sweep backpack. Debug: `ShowWorldBlips = true`.

## Per-part mode and presentation

| Field | Role |
|-------|------|
| `ScanPenetration` | `LineOfSight` (optical) vs `Contact` (through walls). **Depends on the part.** |
| `ScanBlipStyle` | `WorldPip` vs `GroundRing` (and later cockpit-only). **Depends on the part.** |

Baseline catalogue lean: Brimforge heads default **LOS**; most others default **Contact**. Systems/backpack enhancers may override mode/style and add range/resolution.

## Component enhancers

Systems (and future backpack links) with `ScannerRange` / `ScannerResolution` add to the head total while the head is alive. Examples: Needle Array (Ouro), Convoy Spotter Link (Trinova), Oracle Lattice (Lumina).

Active force-pulse utilities can call the same stamp path later; they are not required for baseline awareness.

## Vision vs scanner (unchanged split)

- **Vision** (`VisionRange` / angle) — combat ID cone for guns / paint / SensorVision missiles.
- **Scanner** — passive contact blips + SensorContact missile acquire range (with vision).

---

# 4. Power

## Design goals

Power is a hybrid gate with two linked jobs:

1. **Loadout gate** — an entry-level core cannot legally support every high-end component.
2. **Combat throughput** — active systems consume a rechargeable operational pool.

The torso still limits how strong a core the MAP can house.

## Power vs Heat (locked distinction)

- **Power is short-horizon throughput.** It falls quickly when several systems are pushed together and recovers quickly when demand stops. Running dry creates a brief loss of capability, not lasting punishment.
- **Heat is long-horizon pressure.** It accumulates across aggression and movement, dissipates more deliberately, throttles fire, and risks overheat / shield break.

Tuning guardrail: normal single-system use (gun alone, sprint alone, shield alone) should usually be power-sustainable. Power should cap out primarily under **combined** demand (dual fire, sprint + fire, shield + gun, active channels). Heat remains why sustained aggression eventually becomes dangerous.

## Decided model

### Core stats

A power core provides:

- **Capacity** — total power budget the MAP can support.
- **Generation** — power restored per second into the operational pool.
- **Core class** — must fit the torso's housing class.

### Component reservation

Every equipped non-empty component has a **Power Requirement** permanently reserved from capacity:

```text
Operational max = Core capacity − Σ living component Power Requirements
```

Garage legality:

```text
Σ Power Requirements ≤ Core capacity
```

Illegal / overbudget loadouts are rejected or clearly flagged. Garage installs hard-block without silently stripping other parts — strip optional modules only when migrating authored/saved loadouts (`SanitizeLoadout`). Deploy/launch paths also refuse illegal kits. A legal build may reserve the entire capacity (operational max = 0); the MAP can assemble but cannot spend power until something is unequipped or the core is upgraded.

### Passive vs active

Always-on body systems reserve power but do not repeatedly drain the pool while idle:

- Torso, head, legs, power core, passive systems, weapon/module standby.

Active usage spends from the operational pool:

- Weapons: cost per shot (or sustained draw while firing, TBD by weapon type).
- Sprint: cost per second.
- Held shield raised: cost per second.
- Abilities: activation burst and/or channelled cost per second.
- Shield generator: upkeep / recharge cost.

Generation continues while consuming:

```text
Net change/sec = Generation − active drains
```

If the pool hits zero, gated actions fail until generation recovers enough headroom.

### Destroyed components

When a component is destroyed, its Power Requirement is released (operational max increases). This rewards stripping enemies and makes a gutted MAP slightly more able to run remaining systems — fitting the torso-death fantasy.

### HUD / garage presentation

- Garage: Capacity, Generation, Reserved, Operational max, overbudget warning.
- Combat HUD: operational pool fill (current / max), not the full capacity including reserved.

## Open questions

**A. Overbudget equip — decided: hard block.**
If installing a part would push Σ Power Requirements above core capacity, the part does not install (including core/torso swaps that would force dropping other modules). Deploy is also blocked for any illegal loadout. Garage must state why clearly. Authored/migrated kits may auto-strip optional mounts via `SanitizeLoadout` so catalogue drift cannot soft-lock deploy.

**B. Weapon power cost — decided: per shot.**
Weapons spend operational power once per projectile fired. Sustained-while-firing draws are deferred (may return later for beam/energy weapons if needed).

**C. Numbers pass — decided (starting bands).**

### Core capacity / generation

| Class | Capacity band | Generation /sec | Role |
|-------|---------------|-----------------|------|
| 1 | 95–115 | 14–22 | Entry / light frames |
| 2 | 125–150 | 20–28 | Claim kits |
| 3 | 155–200 | 26–36 | Threat / heavy |

Manufacturer lean:
- Brimforge: highest capacity, lower generation.
- Lumina: highest generation, moderate capacity.
- OuroTech: generation-forward, lean capacity.
- Trinova: balanced.

### Power Requirement (standby reservation)

| Slot | Typical range |
|------|----------------|
| Legs | 10–22 |
| Torso | 12–24 |
| Head | 5–10 |
| Power core | 0 (source, not a load) |
| Arm weapon | 8–18 |
| Shoulder | 5–14 |
| Backpack | 6–16 |
| Systems | 5–14 |
| Empty | 0 |

Tier and manufacturer shift these within the band. Target starter MAP (Class-1 cell + balanced kit) reserves ~65–75 of 100, leaving ~25–35 operational power.

### Active spend

| Action | Cost shape | Typical |
|--------|------------|---------|
| Weapon shot | Per shot | 3–14 |
| Sprint | Per second | 12–28 |
| Ability activate | Burst | 6–18 |
| Pulse repair | Per second while held | 8–16 |

Dual-arm alpha on a starter core should drain the operational pool in a few seconds; a Class-3 high-gen core can sustain more.

## Implementation status

**Implemented (hybrid preserved).** Runtime operational pool, catalogue PowerRequirement / PowerPerShot calibration, garage hard-block, deploy-time `IsPowerLegal`, and per-shot / sprint / ability / shield spend. Playtest validates that combined demand pressures power while single-system use usually stays sustainable.

---

# First-person controls and mobility modules (July 20, 2026)

## Camera / control contract

**First person (body window)**
- Camera sits in the torso/upper-body window. The **head** remains a sensor/targeting component (not the camera mount).
- Mouse is **captured**. Mouselook yaws/pitches the body view and drives torso aim.
- Aim point = view-center ray (no on-screen cursor).
- **WASD = legs** relative to body look. **LegMode does not change FP aim** and does not force tank-turn in FP.
- Alt / arrows = small head-peek offset on top of body look.

**Third person**
- Cursor visible; aim from cursor ray.
- **LegMode matters:** Locked = chassis turn + throttle; Gimbaled = strafe + independent torso aim.

## Sprint / dash / jump

| Input | Behavior |
|-------|----------|
| Tap `sprint` (under ~0.18s) | **Dash** if a living **Thruster** module is equipped |
| Hold `sprint` | Sustained **sprint** from legs (`CanSprint` + power draw) |
| `jump` (Space) | **Jump** if a living **Booster** module is equipped |

Walk (biped/hex) uses lower acceleration; sprint/dash/tracks are snappier. Immobilized legs (`MobilityFactor` 0) block dash and jump.

## Thruster / booster wiring (catalogue by other agent)

Mount on **Backpack** and/or **Systems**. Set on `PartData`:

- `MobilityModule = Thruster` → `DashSpeed`, `DashDuration`, `DashCooldown`, `DashPowerCost`, `DashHeat`
- `MobilityModule = Booster` → `JumpImpulse`, `JumpPowerCost`, `JumpHeat`

Assembler folds living modules into `MechStats` (best thruster / best booster wins). Destroyed modules drop capability.

Example bands (guidance only): DashSpeed 28–40, DashDuration 0.15–0.25, DashCooldown 1.0–1.8; JumpImpulse 10–16.

---

# 5. Weight (soft mobility constraint)

## Design goals

Weight makes leg choice matter. Heavy kits need legs rated to carry them; racing legs on fortress armor limp.

## Locked rules

- Every installed non-empty part (including legs) has **Weight**.
- Legs provide **LoadRating** for total assembled weight.
- At or below rating: no mobility penalty and no underweight bonus.
- Above rating: still legal and deployable; movement and turning fall continuously.
- At **200%** of leg rating: movement and turning reach **zero**.
- No hard weight ceiling.
- Destroyed parts **still count** toward total weight (losing an arm does not lighten the chassis).
- LoadRating comes from the living legs package only.
- Weight multipliers stack with limb-damage mobility loss. Sprint uses already-reduced walk speed.

## Overload curve

```text
LoadRatio = TotalWeight / LoadRating
Overload = clamp(LoadRatio - 1, 0, 1)
WeightMoveMultiplier = 1 - Overload
WeightTurnMultiplier = (1 - Overload)²
```

| Load | Move | Turn |
|------|------|------|
| 100% | 100% | 100% |
| 125% | 75% | 56% |
| 150% | 50% | 25% |
| 175% | 25% | 6% |
| 200% | 0% | 0% |

## Catalogue bands

Integer mass units. Manufacturer lean: Brimforge ×1.20, Trinova ×1.00, Lumina ×0.90, OuroTech ×0.85.

| Slot | T1 | T2 | T3 |
|------|----|----|-----|
| Legs | 18 | 20 | 22 |
| Torso | 22 | 25 | 28 |
| Head | 4 | 5 | 6 |
| Core | 9 | 11 | 13 |
| Arm | 8 | 10 | 12 |
| Shoulder | 4 | 5 | 6 |
| Backpack | 6 | 8 | 10 |
| Systems | 3 | 4 | 5 |

Leg LoadRating targets keep stock kits near ~85–95% utilization (Stride 95, Fortress 150, race legs ~90–105, hauler/tracks 130–150).

## Presentation

- Garage: Weight / LoadRating / load %, OVER RATING warning (equip still allowed).
- Shop: Wt and Load on parts.
- Combat HUD: no dedicated weight bar required for this pass.

## Implementation status

**Implemented.** `CatalogWeight`, assembler derivation, controller move/turn multipliers, garage/shop exposure.

---

# Mech 2.0 — visual assembly (planning)

**Status: Open — design session before implementation.**

This section captures visual/rig issues discovered during the Fleet cockpit pass. Goal: define how 2.0 mechs should be assembled, rigged, and authored so FP cockpits, leg animation, and part visuals stay coherent.

## Known issue — legs clip into FP cockpit (July 20, 2026)

**Observed:** On the Trinova Fleet Intermediate torso (`torso_fleet`), first-person walk + turn causes biped leg meshes to swing into the cockpit volume. Breaks immersion and reads as broken geometry.

**Current stack (why it happens):**
- Loadout still has **Legs → Torso** as adjacent slots; there is no pelvis part or mount between them.
- Runtime sockets: legs hardpoint on `Sockets/Hips`, torso on `Sockets/UpperBody/Torso` (`MechAssembler.CacheSockets`).
- Biped visuals are procedural: hip pivots at **y ≈ 0.85**, pelvis block at **y ≈ 0.9**, thighs swing from there (`PartVisualFactory.BuildBipedLegs`).
- `MechLegAnimator` drives Hip/Knee rotation from leg-package root; gait is shared with FP cockpit bob (`TopDownCamera`).
- Fleet torso is a hollow authored scene with dashboard + `CockpitAnchor`; leg socket transform does not account for the interior cavity.

**Design intent for 2.0:** Add a **pelvis** structural segment between torso and legs so:
1. Leg articulation pivots **below and aft** of the cockpit floor / pilot cell.
2. Torso (especially cockpit torsos) owns the upper volume only; pelvis owns hip width and leg attachment.
3. Walk/turn animation stays outside the viewport frustum in FP.

## Proposed pelvis role (candidate — not decided)

| Layer | Owns |
|-------|------|
| **Torso** | Pilot cell or drone core, shoulders, backpack mounts, power-core housing, upper collision |
| **Pelvis** | Hip span, leg sockets, waist articulation (yaw for turn?), lower armor belt |
| **Legs** | Thigh/shin/foot packages, gait rig, load rating |

**Open questions for design session:**
- **Loadout slot?** New `PartSlot.Pelvis` vs pelvis mesh baked into every torso/legs pair vs torso variant flag (`cockpit` torsos always spawn a pelvis child).
- **Gameplay stats?** Structure HP / armor on pelvis, or purely visual spacer with legs+torso keeping current durability slots.
- **Turn feel:** Does pelvis yaw independently in TP (gimbaled) while torso stays fixed in FP? How does this interact with `LegMode`?
- **Manufacturer identity:** Pelvis as visible “waist” kit (Brimforge slab, Trinova ring, etc.) or hidden structural mount.
- **Hex / tracks:** Same pelvis concept or leg-specific waist blocks only for bipeds?
- **Titan tier:** Scale pelvis with 4× chassis; cockpit MAPs only on Standard unless Titan gets its own interior pass.

## Related 2.0 visual targets (same session)

- **Scene-authored parts over procedural** where the player sees them (Fleet torso already on `torso_tri_fleet.tscn`; legs/pelvis/head likely follow).
- **Diegetic HUD** on cockpit screens — **shipped** for Fleet FP (threat/self/wing panels); tactical map slot reserved on `Screen_WingL`.
- **Cockpit anchor contract:** `CockpitAnchor` + dashboard screen names stable for `CockpitDiegeticHud` binding.
- **Power core placement:** encased aft cavity on fleet torso; pelvis must not reposition core glow into viewport.
- **Hitboxes vs visuals:** pelvis may need collider separation so legs animating don’t read as hits on torso.

## Implementation guardrail

**Do not patch procedurally** (e.g. hiding legs in FP or nudging hip Y only on fleet) until pelvis + rig layout is decided — risks fighting 2.0 authoring and TP/FP parity.

---

# Deferred system notes

These are known issues to revisit only when their system enters review:

- Scanner range / resolution drive the head's passive contact-scan pulse (last-known blips). Live occlusion X-ray is retired.
- Shroud changes appearance but does not currently change detection or targeting.
- Garage preview duplicates runtime stat derivation.
- No tonnage hard-block — Weight is soft by design.
- Melee weapons: Forge Cleaver slice shipped; expand roster in Weapons review.
- Functional held shields: Bulwark Plate slice shipped; shield generators deferred to Abilities review.
- **Multiplayer component state** — host replicates torso health / heat / operational power + max, but per-component HP, destroyed masks, and limb counts are not yet synced to clients. Needs a fixed-order durability snapshot on `MechIntegrity`.
- Mid-fight garage rebuild currently fully repairs / revives components; decide whether that is intentional field-refit behavior.
- Destroyed power core yields zero capacity/generation (no phantom baseline). Full core-death gameplay (limp vs shutdown) is still open.
- Pass-through beyond "nearest living / aimed-dead → torso" still lacks true ray-behind-wreck routing (single chassis collider).
