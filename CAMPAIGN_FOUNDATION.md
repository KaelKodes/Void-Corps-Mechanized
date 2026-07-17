# Campaign Foundation

This is a first-pass implementation pass around the new story spine. It is not the final campaign spec.

## Story framing now in-project

- Player-facing framing now supports:
  - MAP Cadet Program tutorial
  - graduation into active service
  - Big Four manufacturer convention / trial premise
  - covert merc-corp operations as a manufacturer's shadow arm
- The currently playable slice is explicitly framed as **active operations**, not the full cadet-to-convention pipeline yet.

## Implemented foundation

### Persistent profile state

- `MercCorpName`
- `AffiliatedManufacturerId`
- manufacturer reputation table for all four manufacturers
- default placeholder corp name still lives on `VoidCorpsIdentity.PlayerCorpCodename`

### Campaign state

- `CampaignPhase`
- `ClaimsSecured`
- `ManufacturerPayoutEarned`

### Sponsor progression

- Winning a campaign node gives a small reputation bump with the affiliated manufacturer
- Clearing a warning / sector-clear node gives:
  - extra reputation with the sponsor
  - a first-pass sponsor stipend
  - tracked manufacturer payout total on the campaign run

This is intentionally light scaffolding, not the final territory economy.

### Mission telemetry

First-pass telemetry now records:

- mission time
- shots fired / hit
- missiles fired / hit
- utility uses
- heal applied
- damage sustained / hits sustained
- escort damage taken
- buildings destroyed
- fodder destroyed
- MAPs hit / destroyed
- MADs hit / destroyed

Telemetry is shown in post-mission debrief and is intended to later power:

- manufacturer trial scoring
- mission grading
- rep adjustments
- leaderboard / territory-performance layers

## UI / copy updates

- Main menu campaign entry now frames the cadet -> convention -> shadow-ops structure
- campaign map shows corp name and sponsor-aware status
- arena claim brief shows corp + sponsor context
- post-mission results now include telemetry summary and merc-corp identity

## Deliberate non-decisions

These were left flexible on purpose:

1. Which manufacturer is selected first in a fresh profile
   - current code supports affiliation but does not force the convention choice flow yet

2. How manufacturer trials score the player
   - telemetry exists, but no final score formula is locked in

3. Exact sponsor reward math
   - current stipend / rep gain is a placeholder progression scaffold

4. Other-manufacturer side contracts
   - not implemented yet; only sponsor-side progression exists

5. Cadet tutorial scene flow
   - story premise is in place, but no dedicated cadet scene / loaner MAP flow yet

6. Alien endgame threat
   - acknowledged in lore direction, not represented in systems yet

## Recommended next steps

1. Build the **campaign entry flow**:
   - cadet tutorial
   - graduation screen
   - convention / manufacturer choice
   - trial mission select

2. Replace placeholder sponsor progression with:
   - contract outcomes
   - rep gains / losses across multiple manufacturers
   - manufacturer-specific territory rewards

3. Turn telemetry into actual scoring:
   - trial grade
   - mission grade
   - sponsor evaluation

4. Add named rival merc corps to the sector map and mission copy.

5. Let the player name their merc corps in-game instead of relying on placeholder profile text.
