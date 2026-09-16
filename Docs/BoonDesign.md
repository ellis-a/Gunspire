# Boon design notes

Planning only: nothing here is built, and every number is a placeholder. The spells in `SpellDesign.md` are the
reference for what each spell does; this doc only says how a boon changes it.

---

## Decided

- **A full redesign.** The boons are designed from scratch; the current roster carries nothing over by default.
- **School boons need the school.** A boon that belongs to a school is offered only while you hold at least one
  spell of that school.
- **Spell boons need the spell.** A boon that changes one spell is offered only while you hold that spell.
- **There will be a lot of boons.** No fixed count per school. Each school gets what its spells and mastery need.
- **Design first.** This doc is settled before any code.
- **"Floor" means a room.** Each room is a floor of the tower. The word keeps it apart from spell and boon levels.

---

## What a boon is now

A boon changes how something you hold behaves, or gives your build a rule it did not have. A number going up is
the floor of what a boon can be, not the ceiling. The spell pass gave builds a spine (schools, masteries, a
movement slot, a melee slot, two guns) and boons should hang off that spine.

### Families

| Family | Gate | What it is for |
|---|---|---|
| **Core** | none | Any build: health, speed, mana, kills, luck. The reliable picks. |
| **Arsenal** | a matching gun, where it names one | Guns: headshots, delivery, fire mode, magazine, alt fire, rounds. |
| **Slots** | the slot's spell, where it names one | The movement slot and the melee slot in general. |
| **School** | at least one spell of the school | Extends that school's mastery, or its shared mechanics. |
| **Spell** | that spell | Changes how one spell works. |
| **Pact** | none | Legendary, run-warping, usually with a price. |

### What rarity means

| Rarity | Shape |
|---|---|
| Common | A plain number. |
| Uncommon | A number with a condition, or a status on something. |
| Rare | Changes how a thing works. |
| Mythic | A new rule for a school or a slot. |
| Legendary | Warps the run. Pacts live here. |

### Levels

Numbers level; changes mostly do not. A boon that adds a second projectile is one pick. A boon that widens an
area can take three. Default caps: Common 5, Uncommon 3, Rare 2, Mythic 1, Legendary 1.

A boon can also be **uncapped**, offered for as long as the run lasts. The stat boons are.

### Proposed defaults, still open

- **Held means equipped**, not merely known, matching how masteries count.
- **A boon whose school or spell you drop goes dormant.** It stays in your list and works again if you equip
  the spell again. It is never offered again while dormant, and it does not count against anything.
- **An offer draws a family first, then a rarity, then a boon.** Weights before normalising over families that
  have anything to offer: Core 30, Arsenal 20, Slots 10, School 15, Spell 25. A shrine's spell and a boon offer
  can never be the same thing, so this does not touch spell offers.

---

## Boons

To be written.

### Core

**Stat boons.** One per core stat, each raising that stat by one. Common, and uncapped: they can be taken any
number of times.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Nimble | Common | Uncapped | +1 Dexterity: tighter bullet spread, less recoil, faster reloads |
| Arcane | Common | Uncapped | +1 Power: more spell and status power, more maximum mana |
| Swift | Common | Uncapped | +1 Athletics: faster cooldowns, movement, jumps and air control |
| Tough | Common | Uncapped | +1 Endurance: more maximum health and mana regeneration |
| Lucky | Common | Uncapped | +1 Luck: more critical hits, and rarer things on offer |

**Offer boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Rewarded | Uncommon | 3 | You can pick one more boon from each offer, per level |

**World boons.** They change the enemies rather than you.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Ghostrealm | Mythic | 1 | Every enemy is Ethereal at all times: immune to kinetic damage, and everything else hits it twice as hard |
| Doomed | Mythic | 1 | At the start of each floor, five random non-elite enemies are death-marked. The mark never wears off |

- **Ghostrealm's price is intended.** Kinetic guns and spells do nothing under it. Taking it means building
  around it.
- **Doomed** marks every non-elite when a floor has fewer than five. Only enemies present when the floor starts
  are eligible: split copies and summoned enemies are never marked.

### Arsenal

**Enchantments.** Your guns apply a status on hit. One boon per status; any number of them can be held together.

- **Amount applied = round damage × the boon's multiplier × its level.** Each boon has its own multiplier, tuned
  by hand. With Burn's multiplier at 3, level 3 turns 1 damage into 9 burn.
- **Frost** applies that amount as frost buildup.
- **Shock** works differently: only the first round after a reload applies it, to whatever that round hits. A
  miss wastes it.
- **Shotguns:** each pellet applies its own share independently.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Burn Enchantment | Uncommon | 3 | Rounds apply burn |
| Bleed Enchantment | Uncommon | 3 | Rounds apply bleed |
| Poison Enchantment | Uncommon | 3 | Rounds apply poison |
| Torment Enchantment | Uncommon | 3 | Rounds apply torment |
| Shock Enchantment | Uncommon | 3 | Rounds apply shock |
| Frost Enchantment | Uncommon | 3 | Rounds apply frost |

**On-hit boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Chain Static | Uncommon | 3 | A gun hit sends a bolt of lightning to an enemy near the one you hit, dealing energy damage |

### Slots

### Elemental

### Bestial

### Abyssal

### Divination

### Death

### Psionic

### Aetherics

### Petty

### Pacts

---

## Open questions

- **Offer weights.** Are Core 30, Arsenal 20, Slots 10, School 15, Spell 25 roughly right, or should a
  build's own spells come up more often?
- **Held or known.** Should school and spell boons follow what is equipped, or everything you have learned?
- **Dormant or lost.** When you unequip a spell, does its boon wait for you, or is it gone?
- **Levels.** Are the default caps per rarity right?

**How shock works, for reference.** Shock stacks without limit, and each stack makes the target take 1% more
damage from everything. A spell's shock applies 12 stacks by default, editable per spell on its asset, and
spell power scales the stack count. Rapid sources apply fewer: Hailstorm and Elemental Form's storm bullets
apply 6. Each application's stacks keep their own timer and fall off when it runs out, so steady fire holds a
level rather than climbing forever. Hearing falls by a quarter for every 12 stacks.
- **Shock Enchantment's amount.** Now that shock is counted in stacks, does the first round after a reload
  apply round damage × multiplier × level stacks, the same formula as the others?
- **Chain Static's numbers.** How much the bolt deals (a flat amount, or a share of the hit), how near "near"
  is, what each level raises, and whether three levels is the cap.
- **Chain Static on fast guns.** Every hit firing a bolt means a minigun or a shotgun's pellets throw a lot of
  them. Should it have a chance per hit or a short cooldown?
- **Chain Static's bolt.** Does it only jump to an enemy you did not just hit, and can a bolt set off
  enchantments or other on-hit boons, or only the gun's own rounds?
- **Rewarded and offer size.** At three levels you can pick four boons, and an offer shows three today. Does
  the offer grow with it, say to always show two more than you can pick?
