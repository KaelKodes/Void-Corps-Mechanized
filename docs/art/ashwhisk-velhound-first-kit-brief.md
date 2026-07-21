# Design brief: Ashwhisk & Velhound — first chassis kits

**Product:** Void Corps: Mechanize  
**Audience:** Graphic / concept / kit artist  
**Priority:** First playable body kits + house marks for two new manufacturers  
**Status:** Approved direction (design intent). Stats and final part IDs will be wired by engineering after art lands.

---

## 1. Why these companies exist

Mechanize already has four manufacturers (the “Big Four”) that license weapons, frames, and systems:

| ID | Name | Role |
|---|---|---|
| `brimforge` | Brimforge | Heavy armor / kinetic |
| `ourotech` | OuroTech | Precision / targeting |
| `trinova` | Trinova | Utility / hybrid |
| `lumina` | Lumina Vaultworks | Energy / experimental |

We are adding two **body-specialist** manufacturers. Together with the Big Four they form **the Six**.

| ID (proposed) | Name | Specialty |
|---|---|---|
| `ashwhisk` | **Ashwhisk Corporation** `[AWSK]` | Feline-coded chassis (legs / torso / head) |
| `velhound` | **Velhound** | Canine-coded chassis (legs / torso / head) |

### Critical scope rule

**Ashwhisk and Velhound ship body parts only.**

- **In scope:** legs, torso, head (and house logos / hull markings)
- **Out of scope:** weapons, missiles, utility mounts, energy guns, ballistics

Weapon systems continue to come from the Big Four. A finished MAP should look like:

> Ashwhisk or Velhound **hull** + Brimforge / OuroTech / Trinova / Lumina **arms and hardpoint kit**

Design so modular arm sockets read as universal license mounts, not house-brand guns.

---

## 2. Tone (read this twice)

These are **viable combat machines**, not mascots.

- Hard sci-fi industrial MAP aesthetic: weathered plate, hydraulic honesty, claim-site dirt
- Animal read comes from **silhouette, posture, sensor geometry, and gait cues** — not cartoon ears glued on for laughs
- In-world excuse: trillionaire eccentricity at the ownership level. The hardware still has to survive sustained combat
- Ashwhisk and Velhound **hate each other**. Rivalry is corporate and personal at the founder level — visible in branding contrast, not in goofy paint jobs

**Do not:** chibi proportions, pet-food logos, paw-print stickers as primary identity, “cute” colorways as defaults  
**Do:** serious foundry brands that happen to build predator-shaped warframes

### Language hygiene (for any text on sheets)

- **Manufacturer** = kit brand (Ashwhisk, Velhound, Brimforge, etc.)
- **Corp** = player/NPC organization (not these companies)
- Chassis class in-world: **MAP** (Mechanized Armor Pilot), not “robot” in formal labels if avoidable

---

## 3. Company profiles

### Ashwhisk Corporation — `[AWSK]`

**Role:** Private chassis house. Precision posture, clutch mobility, predatory silhouette.  
**Personality:** Quiet money. Exact. Slightly contemptuous of blunt instruments.  
**Rival:** Velhound.

**House mark (required):**

- Monogram built on an **X / compass cross**
- Letters at the four points:
  - **A** — top
  - **W** — left
  - **S** — right
  - **K** — bottom
- Should stencil cleanly onto armor at small HUD sizes and large hull decals
- Prefer geometric, engraved, industrial — not ornate script

**Ticker lockup:** `AWSK` should work alone on a plate next to the mark.

**Form language (feline, industrial):**

- Digitized / digitigrade-leaning biped legs (still clearly mech, not furry costume)
- Narrower waist / suspended-core torso read; hunched ready stance
- Head/sensor pod with ear-like antenna fins or asymmetric optic “ear” plates
- Optional: short armored balance fin / vestigial stabilizer (tail-coded, plated, mechanical)
- Feet: multi-toe tread pads / claw-splay contact patches for claim rock

**Default paint direction (first kit):**

- Primary: cool ash, charcoal, bone-grey, muted olive or slate
- Accent: thin AWSK white/steel stencil; restrained secondary (not rainbow)
- Weathering: fine scoring, dust in panel lines — “serviced often,” not scrapyard

**Movement fantasy to sell in stills:** coil, leap, land, re-orient. Precision aggression.

---

### Velhound

**Role:** Pursuit / pressure chassis house. Broader stance, commitment to the charge.  
**Personality:** Blunt, durable, pack-proud. Treats Ashwhisk’s elegance as weakness.  
**Rival:** Ashwhisk Corporation.

**House mark (required):**

- **Canine silhouette** emblem (profile or shield-mounted head/hound mark)
- Must read at thumbnail size next to Big Four marks
- Industrial stencil / stamped metal, not soft illustration

**Form language (canine, industrial):**

- Heavier biped legs; wider track; paw-like tread feet with thick contact pads
- Deeper chest / jowl-adjacent armor volumes; snout-like sensor housing OK if still “machine”
- Optional: docked rear stabilizer / short thick counterweight fin (docked-tail read)
- Bulkier actuator covers; more “ram and hold” than “spring and slice”

**Default paint direction (first kit):**

- Primary: gunmetal, rust-iron, charcoal, muted ochre or slate-blue
- Accent: high-contrast stencil (off-white / hazard-adjacent sparingly)
- Weathering: impact scuffs, grit, thicker edge wear — “run hard”

**Movement fantasy to sell in stills:** brace, surge, bite the line, don’t yield.

---

## 4. First kit deliverables (what “done” means)

For **each** company, deliver a **Starter Chassis Line** — one coherent visual kit that can be shown assembled and as separate parts.

### Per company — 3D / concept package

| Slot | Count | Notes |
|---|---|---|
| Legs | 1 starter biped set | Primary locomotion identity for the house |
| Torso | 1 starter hull | Cockpit/core volume + left/right arm hardpoints |
| Head | 1 starter sensor pod | House “face”; readable silhouette |

Optional stretch (nice-to-have, not blocking):

- 1 alternate paint / weathering pass
- 1 rear 3/4 and 1 orthographic turnaround (front / side / rear)
- Decal sheet: house mark, `AWSK` or Velhound wordmark, numeral `01`, hazard strips

### Assembled hero shots (per company)

1. Clean garage / showroom 3/4 (kit identity)
2. Field / canyon dust 3/4 (combat-ready)
3. Scale sheet: assembled MAP beside a human or cat/dog-scale pilot figure if you have one — otherwise a standard 2m gauge bar

### Logo package (per company)

| Asset | Spec |
|---|---|
| Primary mark | Square-friendly, works on dark UI |
| Mono / stencil | 1-color version for hull decals |
| Wordmark | Ashwhisk Corporation / AWSK; Velhound |
| Clear space + minimum size notes | Short PDF or margin on the sheet |

Suggested filenames (engineering will mirror `art/ui/` patterns):

- `ashwhisk_mark.png` (and mono variant)
- `velhound_mark.png` (and mono variant)

Existing Big Four marks live under `art/ui/` for reference (`brimforge_mark.png`, `ourotech_ouroboros.png`, `trinova_mark.png`, `lumina_mark.png`).

---

## 5. Hardpoint & compatibility rules

Arms are **not** designed by Ashwhisk/Velhound in v1.

When drawing torsos:

- Include clear **left and right weapon hardpoints** (shoulder / forearm mount planes)
- Hardpoints should look like licensed universal receivers — bolts, rails, coupling rings
- In presentation art, you may show **placeholder Big Four weapons** for drama (e.g. one ballistic, one energy) — label them as licensed third-party kit, not house guns
- Do not invent Ashwhisk-branded rifles or Velhound-branded shotguns for this pass

Suggested presentation combo (optional):

- Ashwhisk hull + contrasting licensed arms (keeps modular fantasy obvious)
- Velhound hull + heavier licensed kinetic arm for silhouette weight

---

## 6. Suggested first-line part names (working titles)

Engineering can rename; these are for callouts on concept sheets.

### Ashwhisk Corporation — Line “Whisperframe” (working)

| Slot | Working name | Intent |
|---|---|---|
| Legs | **Coilstriders** | Digitigrade spring, clutch leap |
| Torso | **Ashrib Cage** | Suspended-core, lean predatory hull |
| Head | **Whisker Array** | Ear-fin sensors + primary optic |

### Velhound — Line “Yardbreaker” (working)

| Slot | Working name | Intent |
|---|---|---|
| Legs | **Bracehounds** | Wide stance, charge plant |
| Torso | **Ruff Plate** | Deep chest, impact-first volume |
| Head | **Muzzle Lidar** | Snout sensor housing, pack-mark ready |

If those names feel too on-nose, swap to colder industrial titles — the silhouette should still carry the animal read.

---

## 7. Visual references (project)

Use the project’s existing industrial MAP direction:

- Heavy hollow chassis / piloted core energy
- Weathered olive / gunmetal / utilitarian plate
- Orthographic honesty over anime sleekness
- Animal DNA = posture and kit language, not soft toy proportions

**Contrast reminder**

| Ashwhisk | Velhound |
|---|---|
| Coil / leap / re-orient | Brace / surge / hold |
| Narrower, taller ready pose | Wider, lower pressure pose |
| Geometric AWSK monogram | Canine silhouette mark |
| Fine weathering | Heavier impact wear |

They should still feel like they belong in the **same universe** as Brimforge and Lumina — same material physics, same dirt, same lighting language — so a player can mix Big Four guns onto either hull without a style break.

---

## 8. Checklist for handoff back to the team

Please return:

- [ ] Ashwhisk logo set (primary + mono + wordmark/`AWSK`)
- [ ] Velhound logo set (silhouette mark + mono + wordmark)
- [ ] Ashwhisk starter legs / torso / head (concept or model + turnaround)
- [ ] Velhound starter legs / torso / head (concept or model + turnaround)
- [ ] One assembled hero per house
- [ ] Short notes: intended scale, hardpoint locations, any decal placement map
- [ ] Explicit confirmation: **no house weapons** in this package

Out of scope for this brief (do not block on):

- Mice / squirrel / rabbit DLC lines
- Titans
- Full colorway catalogs
- MAD (drone) variants
- Boss / setpiece characters

---

## 9. One-sentence creative north star

**Two rival body houses — Ashwhisk and Velhound — build serious MAP chassis whose animal silhouettes come from eccentric ownership, not comedy, and whose guns still come from the Big Four.**

---

## Contact / decisions locked

| Decision | Status |
|---|---|
| Company names | **Ashwhisk Corporation `[AWSK]`** and **Velhound** |
| Scope | Body parts only (legs, torso, head) |
| Tone | Viable machines; eccentricity, not joke brands |
| Rivalry | Public manufacturer feud between the two houses |
| Logo directions | AWSK compass-X monogram; Velhound canine silhouette |

Questions on stats, garage UI integration, or catalog IDs → engineering / design lead.  
Questions on silhouette, marking, and kit readability → this brief owns those calls until revised.

---

## 10. Engineering (locked IDs — first pass)

**Status:** Catalog + skirmish wired. Authored meshes / new `VisualKind` **deferred**.

### Manufacturers

| ID | Display | Campaign access |
|---|---|---|
| `ashwhisk` | Ashwhisk Corporation | Skirmish loaners only (`GameCatalog.CampaignManufacturerIds` excludes) |
| `velhound` | Velhound | Same |

### Body parts (T1 Field)

| House | Slot | Part ID | Display |
|---|---|---|---|
| Ashwhisk | Legs | `legs_ash_coilstriders` | Coilstriders |
| Ashwhisk | Torso | `torso_ash_ashrib` | Ashrib Cage (Mech 2.0 hollow / Fleet cockpit scene) |
| Ashwhisk | Head | `head_ash_whisker` | Whisker Array |
| Ashwhisk | Back | `backpack_ash_stabilizer` | Balance Fin Stabilizer (consumes back slot) |
| Velhound | Legs | `legs_vel_bracehounds` | Bracehounds |
| Velhound | Torso | `torso_vel_ruff` | Ruff Plate |
| Velhound | Head | `head_vel_muzzle` | Muzzle Lidar |

### Skirmish premades

| Variant | Name | Body | Licensed kit |
|---|---|---|---|
| 4 | Ashwhisk Whisperframe | ash trio + stabilizer back | OuroTech Stitch + Trinova Bulwark plate + needle systems |
| 5 | Velhound Yardbreaker | vel trio | Brimforge maul/chain + citadel systems |

Mark paths (when art lands): `res://art/ui/ashwhisk_mark.png`, `res://art/ui/velhound_mark.png`. Procedural fallbacks draw AWSK compass-X and hound silhouette until then.

### Checklist addenda

- [x] Manufacturer registry + campaign gate
- [x] Starter body trio each (procedural visuals)
- [x] Skirmish starting choices (variants 4–5)
- [ ] Models / `VisualKind` silhouette pass — **deferred**
- [ ] Authored logo PNGs
- [ ] Campaign / convention / research unlock
