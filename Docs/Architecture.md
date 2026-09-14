# Architecture plan

What has to be built to support everything in `Docs/SpellDesign.md`, organised as shared
foundations first and content last. Nothing here is implemented.

Every item names its customers: the designed spells, masteries and rules that need it. The more
customers a piece has, the earlier it should be built, because every one of them is otherwise a
place the same problem gets solved differently.

---

## Ground rules the codebase already imposes

These come from `AGENTS.md` and from bugs already found. Every phase below has to respect them.

- **Assets are the source of truth.** Guns, spells, boons, enemies, familiars and loadouts are built
  in code, then any asset under `Resources` is merged over the top by id. With assets committed,
  changing a built-in in code appears to do nothing.
- **Adding a field to a definition needs a migration.** Existing assets deserialise the new field to
  its default, so the value written in code never appears. `AGENTS.md` notes this came up three
  times and that a general tool is worth building if it came up again. It has since come up twice
  more, with perception and damage types.
- **Enums are stored as integers in assets.** Append new members at the end. Never reorder or insert,
  or every authored value silently points at a different member.
- **Renaming a serialised field loses its value** unless the old name is preserved with Unity's
  `FormerlySerializedAs` attribute.
- **Unity never leaves a plain serialisable class field null.** It builds one on deserialise, so
  "is this feature present" needs an explicit flag, as `SustainProfile.Exists` and
  `WeaponDefinition.AltFire` already do.
- **Ids are plain strings.** A renamed id does not fail to build; it silently vanishes. Tooling that
  resolves ids is the only safety net.
- **Edit mode never calls `Awake`.** Verifiers must configure components explicitly, or they test
  uninitialised objects and pass by doing nothing.
- **Prove every verifier bites.** Break the code once, confirm the check fails, restore it.
- **Forward rendering allows four pixel lights per surface,** and everything is generated from code,
  with no prefabs and almost no authored shaders.

---

## Phase 0: data model and migrations

Everything later reads these types, and every change here can silently corrupt assets. Do it first,
all at once, behind one migration tool.

**Status: done.** Implemented, and every authored asset migrated. Where the work differed from the
plan below:

- **The offset is 7, not 5.** Every shipped loadout opened at 3 in each old stat, not at the default
  of 5. The formulas were rebased so a stat of 10 gives what 3 used to, and every authored loadout
  moved up by 7, which leaves each class playing as it did.
- **Loadout fields were renamed by the migration, not with `FormerlySerializedAs`.** With the
  attribute, an unmigrated asset would carry an old 3 into a field that now expects 10. Without it,
  an unmigrated asset falls back to the baseline of 10, which is the safer way to fail.
- **Per-stat bonuses were folded into flat values.** Bash keeps the damage its Strength bonus gave a
  loadout at 3, and Blink keeps the reach its Intellect bonus gave. Both effects can still scale off a
  stat, now measured from the baseline.
- **Attributes no stat reaches now sit at their formula's own base.** Gun damage and attack speed are
  1, crit damage is 1.75, dash speed is 22, dash charges are 1 and health regeneration is 0. For a
  current loadout that means:

| Change | Size |
|---|---|
| Gun damage | 7% lower, 9% for the Paladin |
| Fire rate | about 3.5% slower |
| Crit damage | about 3% lower |
| Dash speed | about 4% slower |
| Health regeneration | 0.12 to 0.16 per second, now none |
| Enemy and familiar attack rate | about 6% slower, since they read the same attack speed |

- **Three attributes changed which stat feeds them,** so a class that leaned on the old stat shifts by
  one point's worth. Mana regeneration moved from Intellect to Endurance, cooldown rate from Intellect
  to Athletics, and reload speed from Agility to Dexterity. The Wizard, for example, regenerates 7.4
  mana a second instead of 8.2, while the Juggernaut goes the other way.
- **Spread and recoil scale with Dexterity** at 3% per point either side of 10, as placeholder
  numbers. Gravity scale is wired into falling and jump height.
- **Statuses were appended with names but no behaviour,** as `PlannedStatus`, so every id resolves.
  Verify Debuffs lists them as planned.
- **The 14 older spells are retired,** built-ins and assets both, by a migration. Paladin and
  Juggernaut opened on Sprint and Aegis Stance, and now open on Dash. The effects those spells were
  built from are kept for the designed spells. `RetiredSpells` lists the ids, and Verify Spell Slots
  fails if any comes back.
- **No cast spells exist until the designed ones are built.** The first floor's starter-spell reward
  falls back to a boon, spell shrines offer nothing, and the class cards say the slots are empty.
- **Power strengthens a spell's statuses** by the same spell power multiplier it applies to damage.
  Each status decides which of its numbers is the amount through `StatusDefinition.Empower`: stacks
  for frost; magnitude for burn, bleed, poison, shock, weaken, haste, fortify and mark; nothing for
  deathmark, ethereal and the planned statuses. Every mapping is one-to-one as a placeholder until play
  shows each status's real worth. Duration never scales, and only spells scale, never enemy or familiar
  abilities. Spell power is used rather than the full outgoing multiplier, because burn and bleed
  ticks already apply damage-dealt and per-school bonuses when they land.
- **Reinforced crates are gone.** Every crate is an ordinary 25-health crate, including the five a
  treasure room places.

Tools added:

- **`Gunspire/Migrations`** - Report, Apply, and Verify Nothing Pending. The general migration runner,
  with the six stat-rework migrations registered. A second Apply finds nothing to do.
- **`Gunspire/Verify Stats`** - every new formula against the old one at the rebased stat, and every
  removed influence held fixed. Proven to fail when a formula is off by a single point.
- **`Gunspire/Verify All`** - runs every verifier in one pass, including from batch mode.

### 0.1 A general asset migration tool

**Customers: every item in this phase.**

The existing one-offs, `Sync Alt Fires To Assets`, `Sync Perception To Assets` and the two-step
`Migrate Damage Types`, all follow the same shape. Generalise it: a report step and an apply step,
copying only the fields that changed onto assets that need them, and idempotent, so a second run
reports nothing to do.

### 0.2 Spell schools

- **The pending bug: no spell asset stores a school.** All 18 spell assets are missing the key, so
  every authored spell resolves to Elemental, the first member. Nothing school-based can work until
  this is migrated.
- **Rename `Artifice` to `Aetherics` in place.** Same position, so the stored integer keeps meaning
  the same school.
- **Append `Petty` at the end** for spells that belong to no school.
- **Reassign the four existing spells the designs keep:**

| Asset | Slot | Becomes |
|---|---|---|
| `dash` | Movement | Petty, common, charges by spell level |
| `bash` | Melee | Petty, common, knockback and smash power removed |
| `blink` | Movement | Aetherics, common, passes through walls |
| `spider_legs` | Movement | Bestial, rare, as Spider Gravity |

- **Decided: the other 14 are retired.** `arcane_ward`, `blightbloom`, `chain_lightning`, `cleave`,
  `cone_of_cold`, `eventide`, `fel_empowerment`, `firebolt`, `glacial_prison`, `kinetic_slam`,
  `lava_splash`, `leech`, `shield` and `sprint` appeared in no designed list. Retiring deletes the
  asset as well as the built-in, since the asset would otherwise keep the spell alive.

### 0.3 Damage types

**Append `Execute`.** Every execute reports its damage under this type. Customers: Prismatic Chains,
which must ignore executes, lifesteal, Wither, the Unspeakable One's maw, deathmark and frost.

### 0.4 Statuses

**Append the new statuses.** Customers are listed under Phase 2.

- Snare and stun, as one status with a magnitude
- Silence
- Disarm
- Fear
- Blind
- Confusion
- Sleep
- Plague
- A general damage over time status that takes a damage type, for Intrusive Thoughts

Markers such as "withered", "split" and "banished" are flags on the enemy, not statuses.

### 0.5 Attributes

- **Append `Spread` and `Recoil`.** Today both are read straight off each gun's definition. Customers:
  Dexterity, Elemental Form's ice form.
- **Append `GravityScale`.** Customers: Ride the Gale's slow fall, Rapture of the Deep's buoyancy.
  As an attribute, both apply it through ordinary status modifiers.
- **Keep `SmashPower` and `HealthRegen` as members, but zero their formulas.** Removing members would
  renumber every attribute after them.

### 0.6 Player stats

**Replace Strength, Intellect, Agility, Vitality and Luck with Power, Dexterity, Athletics,
Endurance and Luck.**

- **Order the new enum to inherit the closest old meaning.** Boons store which stat they raise as an
  integer, so each new stat should take the position of the old stat nearest to it:

| Position | Old | New |
|---|---|---|
| 0 | Strength | Dexterity |
| 1 | Intellect | Power |
| 2 | Agility | Athletics |
| 3 | Vitality | Endurance |
| 4 | Luck | Luck |

  This mirrors how Frost took Chill's slot when statuses were reworked.
- **Rename the loadout fields with `FormerlySerializedAs`.** `LoadoutDefinition` stores each stat in a
  field named after it, so a plain rename loses every authored value.
- **Rebase every formula around 10.** `CharacterSheet` builds each attribute as a base plus an amount
  per point, written around loadouts that default to 5. Each formula should give today's normal
  value at 10. The loadout default moves from 5 to 10, and authored loadout assets need their values
  updated.
- **Rebase Luck's rarity odds around 10** as well, since reward rarity reads the raw Luck value.
- **Rewire the attribute sources** as recorded in the design doc: spell power and maximum mana from
  Power; spread, recoil and reload speed from Dexterity; cooldown rate, movement speed, jump height
  and air control from Athletics; maximum health and mana regeneration from Endurance; crit chance
  from Luck.
- **Stop stats reaching** gun damage, fire rate, attack speed, crit damage, health regeneration and
  smash power.
- **Rewire code that names a stat directly.** `DealDamageEffect` scales with a named stat, defaulting
  to Strength, which Bash uses. `SweepForwardEffect` scales Blink's distance with Intellect. Stat
  shrine colours in `Props` switch on stat names.

### 0.7 Remove reinforced barriers

Smash power only exists to gate them. Remove the barriers, the hardness check against
`Attr.SmashPower`, and Bash's use of it, keeping ordinary props breakable by any hit.

---

## Phase 1: combat core

**Status: done.** Verify Combat Core checks every item below against real components. Where the work
differed from the plan:

- **Origins have one extra member, `Attack`,** for an enemy's own attacks, which the plan did not name.
  Owners declare theirs through `IAbilityOwner.AttackOrigin`, so familiars report as `Minion`. Nothing
  deals `Collision` or `Environment` damage yet; the members exist for Phase 3.
- **Executes go through `Health.Execute`,** taking the fraction of an elite's health to remove, or
  `Health.FinishesElites`. Wither's own marker waits for Wither.
- **Kill credit changes what counts.** Every enemy death is now a kill, including one enemy killing
  another. Lifesteal is narrower than before: the player's gun, spells, melee and their status ticks
  only. Familiar hits used to heal the player and no longer do.
- **Setting health fires the HUD refresh,** since the bar would otherwise go stale, and holds at 1 health
  rather than killing.
- **1.5 was built with the Petty spells.**
- **A body moved to the player's side goes on the familiar layer,** so enemy attacks hit it and the
  player's shots pass through. Nothing is hit by its own attack any more, which matters only for the
  neutral team. `DamageInfo.OnBehalfOf` builds damage issued on another's account.
- **A gun's hit hook fires for splash victims too,** not only direct hits. `WeaponHit.DealBonus` deals a
  bonus as its own instance.
- **Infusions live on the weapon component,** which the holster re-equips rather than replaces, so they
  survive a swap without being pushed again. A restart clears them. Phantom weapons do not exist yet.
- **`Sfx` no longer builds its voice pool outside play mode,** where it threw, so tooling can fire guns.

### 1.1 A damage origin on every hit

**Customers: Psi Blades, Arcane Warp, Foretell, confusion, sleep, Assume Identity, Phantasmal Mimic,
lifesteal.**

`DamageInfo` records a team and a source object, but not how the damage was delivered. Everything on
the player's side shares a team, so a companion bite, a burn tick and a bullet look identical.

- **Add an origin field:** gun, spell, melee, minion, status tick, collision, and later environment.
- **Name it carefully.** A `DeliveryKind` enum already exists for weapons, meaning hitscan or
  projectile. The new one needs a different name, such as `DamageOrigin`.
- **Set it at every source.** `Weapon` for guns, `StatusController.DealTickDamage` for ticks, the
  ability context for spells and melee, familiars and minions for theirs, and the knockback system
  for collisions.

### 1.2 Executes

- Report every execute under the new `Execute` damage type. `Health.TryExecute` currently reports the
  victim's whole remaining health under the finishing hit's type.
- **Make the elite rule a per-execute setting.** Deathmark and full frost take a chunk off elites;
  Wither finishes them; the maw follows the chunk rule.
- **Wither keys off its own marker,** not the weaken status, or every weaken source inherits it.

### 1.3 Kill credit

- **Every enemy death counts as a player kill.** `RunState.OnAnyDied` currently skips deaths not dealt
  by the player's team; that check goes. Customers: Souls, the Blood Debt, on-kill boons, and
  environmental kills later.
- **Lifesteal keeps its own filter,** narrowed by origin to your own hits.

### 1.4 Setting health directly

A `Health` method that sets current health with no healing scaling, no bleed cure, no damage hooks and
no events. Customer: Rewind. It can never kill, since the value always comes from a moment you were
alive.

### 1.5 Statuses that end on damage

**Customers: confusion, sleep, Assume Identity.**

`Health.TakeDamage` applies a hit's statuses before it subtracts the damage. A status that clears on
damage would clear on the very hit that applied it.

- **Handle it once, centrally:** a status can declare that direct damage ends it. Health checks that
  after damage lands, skipping the hit that applied the status and any hit whose origin is a status
  tick.

### 1.6 Teams, masks and allegiance

**Customers: confusion, Assume Identity, Force of Will's reflect, knockback collision damage.**

- **Neutral-team attacks.** A confused enemy attacks as the neutral team with hit and target masks that
  include everyone. The friendly fire check in `Health.TakeDamage` only skips a victim's own team.
- **Switching an entity's side.** Assume Identity moves an enemy to the player's team and layer while
  controlled, then back.
- **Switching a projectile's side.** A reflected projectile changes team, recomputes its hit mask and
  layer, and clears its record of what it already pierced.
- **Damage issued on the player's behalf.** Collision damage between two enemies is issued from the
  player's side, carrying whoever caused the knockback, or friendly fire refuses it.

### 1.7 Weapon events and hooks

**Customers: Smite, Desecrate, Echo, Psi Blades, Arcane Warp.**

`Weapon` fires no events and has no on-hit hook. `Projectile.AttachOnHit` exists for spells but
weapons never use it, and hitscan has nothing.

- **A fired event** carrying origin and direction, for Echo to record and replay.
- **An on-hit hook** on both hitscan and projectile delivery, for Smite's lightning and Desecrate's
  bonus necrotic hit.
- **Bonus damage as a separate hit.** Ethereal enemies discard a kinetic hit before anything else is
  considered, so Desecrate's necrotic has to be its own damage instance.

### 1.8 Bullet infusion with expiry

**Customers: Viper's Sting, Elemental Form, Smite.**

`Weapon.ExtraStatuses` is fed by `RunState.BulletStatuses`, a run-long list with no expiry, pushed onto
the live weapon once.

- **Entries expire by time or by count.** Viper's Sting lasts a duration; Smite lasts one bullet.
- **Re-push on a holster swap.** `Holster.Draw` changes what the live weapon points at, so the infusion
  must follow the swap.
- **Reach phantom weapons,** where a design says so.

---

## Phase 2: statuses and enemy AI

### 2.1 Who enemies consider the player

**Customers: Assume Identity, minions, invisibility.**

`EnemyController.AcquireTarget` and the sight check both reach for `PlayerRig.Instance`, and the flow
field is built outward from the player's position.

- **A single "hostile to enemies" registry:** the body the player currently occupies, plus every
  minion. Hidden entities drop out of it.
- **Target the nearest hostile,** with stickiness, so an enemy between two equally close targets does
  not flip every second.
- **Elites favour the player's body,** turning on minions only when it is out of reach.
- **Build the flow field from the player's current body,** so it follows possession.

### 2.2 Perceived position

**Customers: Blind from Ink Spray, Nether Smoke and Dazzle; Invisibility; Flicker; Rewind; the body left
behind by Assume Identity.**

Enemies aim and check line of sight at the target's live transform. A perceived position that follows
the target while perception holds, and freezes when it breaks, lets enemies shoot where they last saw
you.

### 2.3 Perception changes

- **Seeing a minion alerts an enemy,** checked at the once-a-second retarget interval rather than every
  frame.
- **Hidden entities leave sight checks.** Invisibility keeps hearing; a body hidden by Assume Identity
  leaves hearing too.
- **Fear alerts,** from any source.
- **Sleep suspends perception.** Enemies cannot be un-alerted today, so sleep suspends rather than
  resets.

### 2.4 Control hooks on enemies

**Customers: Maul, Ego Fracture, Depth Grasp, Drown, Banshee Wail, Superego Death, sleep, fear,
confusion.**

`EnemyController` needs explicit questions it asks every frame, each answered by statuses:

- **Can it move?** No while stunned, asleep or banished; slowed while snared.
- **Can it attack?** No while feared, asleep, silenced or disarmed.
- **Cancel the current attack** when fear lands, since `AbilityAttack` already tracks whether it is
  executing.
- **Attack as neutral** while confused.
- **Open design decision:** what silence and disarm mean for enemies, which have attacks but no guns or
  spells.
- **Elite scaling as a rule, not per status.** Elites get half sleep, shorter stuns and fear, and 50%
  banish resistance. One per-status elite multiplier covers all of it.

### 2.5 Movement behaviours

- **Fleeing.** From the player, climb the flow field. From any other point, step to the walkable
  neighbouring cell on `NavField` furthest from that point. Customers: fear from Reckoning, tentacles,
  Apocalypse.
- **The soft hazard push.** A registry of live hazards, and a push away from nearby ones summed
  alongside the existing separation push. Customers: fire trails, Fathomless Gate's centre, possibly
  Nether Smoke.
- **Pulls toward a point,** as knockback pointed inward. Customers: Collapse Space, Fathomless Gate.

### 2.6 Hiding an enemy without deleting it

**Customers: Banish, and the recommended version of Superego Death.**

- Switch off its body, collisions and behaviour, drop it from every enemy and minion check, and
  restore it exactly as it was.
- **It must stay registered with its room.** `RoomRuntime` clears a room when its enemy list empties,
  and removes enemies that die or are destroyed. A deleted or nulled enemy would let the room clear
  and open the exit while it is still meant to come back.
- **Its statuses and attack timers pause** while hidden.
- **Returning to an occupied spot** uses the shared landing check in Phase 4.

### 2.7 Spawning a copy of an enemy

**Customer: Superego Death.**

`EnemyFactory.Spawn` builds an enemy at full definition health. A copy then needs its maximum and current
health set through its stat sheet, the original's statuses, its alertness, target and elite flag, and
**registration with the current `RoomRuntime`**. Without that last step the room clears while the copy
is alive.

---

## Phase 3: world systems

### 3.1 A world clock

**Customers: Stop Time, Banish's paused timers, and any future slow-motion effect.**

Unity's time scale is global, so freezing it freezes the player too. A separate world time rate that
everything except the player reads:

- **Already has hooks:** status durations through `StatusDefinition.DecayScale`, and enemy attack
  cooldowns through the rate multiplier in `AbilityAttack`.
- **Needs converting:** projectile movement, enemy movement and behaviour, status ticks, lingering
  zones, delayed blasts and telegraphs.
- **A held damage queue** for while the clock is stopped, so hits are delivered in one burst on resume.
  Instant-hit guns and melee otherwise resolve immediately.

**Build the clock early, even though Stop Time is a mythic.** It touches almost every update loop, and
each system built before it is one more to convert.

### 3.2 A minion framework

**Customers: the Bestial companion ladder, Raise Dead, Stitched Monstrosity, Apocalypse's plague zombies,
Eye of E'pheraxx, Phantasmal Mimic.**

Familiars write their position directly and drift through walls, which suits flying things but not
walking ones.

- **A walking minion controller:** the player's team, a character controller, the flow field to follow
  the player, and attacks through the same `AbilityAttack` chains enemies use.
- **A new minion layer** that collides with enemies and other minions, ignores the player, and is added
  to both `Layers.EnemyHitMask` and `Layers.EnemyTargetMask`. Immobile minions use it too.
- **Update the comment on `Layers.EnemyTargetMask`.** It explains that familiars must never pull aggro,
  which is now the opposite of the rule.
- **Persistence on `RunState`:** a zombie count, a single monstrosity slot and the beast tier, respawned
  on floor arrival and placed with `NavField.TryFindSpot`. Plague zombies do not persist.
- **Performance.** Raise Dead has no cap. Measure a horde of fifty to a hundred.
- **Open design decision:** whether the Bestial companion revives on a timer or can only be knocked down.

### 3.3 Knockback impacts

**Customers: Telekinesis, Gush, Storm Blast, Star Comet, Tentacle, the Stitched Monstrosity's slam,
Fathomless Gate, Collapse Space, Repulse's reversal of forces.**

- **Detect impacts during knockback** from the character controller's collision reports while external
  velocity is being applied.
- **Damage from closing speed,** not speed, so a crowd pushed together does not grind itself down.
- **A speed threshold,** so walking into things never hurts.
- **Transfer with falloff:** a struck enemy moves less than the one that hit it, so cascades end.
- **Walls damage** the knocked enemy. **Minions** are damaged and pushed.
- **Credit** through the damage-on-behalf mechanism in Phase 1.

### 3.4 Zones, trails and volumes

**Customers: Murder, Consecrate, Desecrate, Soul Storm, Hailstorm, Flaming Skull, Burning Feet, Nether
Smoke, Nether Wall.**

- **Friendly zones** that buff allies inside, not only damage enemies. `LingeringZone` only queries the
  enemy team today.
- **Zones that follow the caster.**
- **A distance-gated trail emitter,** one object owning the whole trail, not one zone per patch.
- **Two new hit-mask layers.** Nether Wall is added to both teams' hit masks, collides with no bodies, and
  stays out of `BlockingMask` and `SightBlockMask`. Nether Smoke is added to the player's hit mask only
  and hands each hit to a random enemy inside.
- **Layer budget.** Layers 8 to 14 are in use. Minion, Nether Wall and smoke bring it to 17.

### 3.5 Level services

- **Recent death positions,** recorded from `Health.AnyDied` and expired after a few seconds. Customer:
  Corpse Explosion.
- **A floor-change hook.** Customers: Rewind's history, Reality Shards, plague zombies, Blink's outer
  boundary.
- **Distance to the exit,** a breadth-first map over the maze's ground graph, treating ledges as blocked
  and embrasures as impassable. Customer: Path of Light. Drawn with emissive markers, not lights.

---

## Phase 4: player systems

### 4.1 Slots and spell levels

- **Three cast slots. Done:** `SpellBook.SlotCount` is 3, with F as the third key; interact moved to X and
  weapon swap to C, and Verify Spell Slots fails if two actions share a key. The HUD already draws
  however many slots the spell book defines. Loadouts name a starting slot index and need checking.
- **Movement and melee spells gain levels.** `PlayerCombat` always casts the melee spell at level 1, and
  `SpellLibrary.Offerable` treats movement and melee spells as straight swaps. Customer: Dash's charges.
- **Offers.** At most one Petty spell per offering, enforced in `SpellLibrary.OfferDistinct`. An
  eliminated-spells set on `RunState`, filtered in `SpellLibrary.Offerable`.

### 4.2 A mastery framework

**Customers: all seven masteries.**

- **Count equipped spells per school,** capped at four, excluding Petty.
- **One mastery component per school** behind a shared interface, recomputed when the equipped set
  changes. Swapping is confined to special floors later, so the recompute can hang off floor arrival.

| Mastery | Needs |
|---|---|
| Beast ladder | Walking minion framework, tier definitions |
| Conflux | Reaction detection on multi-element targets, a per-target cooldown |
| Blood Debt | A debt ledger, a floor at one hit point, refused costs, repayment on any kill |
| Divine Knowledge | World-anchored HUD overlays for health, sight cones and attack timers |
| Souls | A counter, orbiting spheres, capped and carried between floors |
| Psi Blades | A charge meter, per-hit gain rate-capped at a quarter second, bonus sources |
| Arcane Warp | A modifier from missing mana, updated on `Mana.Changed`, mana per hit from base fire rate |

### 4.3 Spell costs and cast rules

- **Cost types:** mana, souls, health through the Blood Debt, psi charge. Every cost is refused when
  unaffordable, never clamped.
- **Echoed casts** cost nothing extra of any kind.
- **Self-silence and self-disarm.** Customers: Shapeshift, Assume Identity, Flicker.

### 4.4 Activation modes

- **Toggles in any slot.** `SustainProfile.Exists` reads mana drain only, and `SpellTools.VerifySlots`
  rejects sustained spells outside the movement slot. Customers: Invisibility, Elemental Form, and
  Bloodwake's health drain.
- **Fixed-duration conditional buffs.** Customer: Path of Light, whose bonus depends on direction.
- **Hold to charge, release to cast.** Customer: Bone Shards.
- **Stances with several modes.** Customer: Elemental Form. Open design decision.
- **Variants chosen at pick time.** Customer: Shapeshift, most cheaply as three separate spells.

### 4.5 The player motor

- **Gravity scale** from the new attribute.
- **Timed impulse sequences.** Customers: Ride the Gale, Bound.
- **Suppressing friction** for a window, as the motor already does on a jump frame.
- **Velocity inversion.** Customer: Repulse, possible with the existing `AddImpulse`.
- **Travel with collision off.** Customers: Gravewalk passing through enemies, Rewind's playback.
- **Teleport validation.** Blink's landing is the furthest walkable point on `NavField` within range,
  clamped to the maze boundary.
- **A shared landing check** that tests the player's capsule at a spot and shoves occupants clear by
  position. Customers: Rewind, Lich Guise, Banish returning an enemy, Gravewalk.
- **A rewind recorder:** snapshots ten times a second for three seconds, cleared on teleport and floor
  change.

### 4.6 Hiding the player

**Customers: Invisibility, Flicker, Rewind's playback, the body left by Assume Identity.**

A player state that removes the player from enemy sight, and optionally hearing and targeting, feeding
the registry in Phase 2.

### 4.7 Possession

**Customers: Assume Identity, Shapeshift.** The largest single item.

Replacing the player's controls, camera and body with another body's, whether an enemy or an animal
form. It depends on Phase 2.1, so enemies treat the occupied body as the player. Build it once for both.

### 4.8 Weapon extensions

- **Phantom weapons** that fire without pushing recoil into the look controller and without ammo or
  reloads. Customers: Divine Assistance, Phantasmal Mimic, Superid's reloads.
- **Auto-aim and auto-fire.** Customer: Superid. Target by angle from the reticle among visible enemies;
  projectiles steer onto the target.
- **An action log and replay.** Customer: Echo. Replays from where you stand, along the original
  direction, excluding Rewind and duration spells.
- **A live projectile registry.** Projectiles have no collider and nothing lists them. Customer: Force
  of Will.
- **Projectile features:** sway, a trail hook, a homing target override, passing through walls.
  Customers: Flaming Skull, Divine Star.

---

## Phase 5: the effect library and content

With the foundations in place, most spells become data: effect chains authored in spell assets. These
new effects are still needed.

| Effect | Customers |
|---|---|
| Instant ray | Mind Spike, Dazzle |
| Set damage type mid-chain | Star Comet |
| Origin offset backwards | Storm Blast |
| Origin from a minion | Fang and Claw |
| Pull toward a point | Collapse Space, Fathomless Gate |
| Narrow vertical selector | Slice |
| Cast a random spell | Elemental Chaos |
| Split a status between targets | Judgement |
| Tether two enemies | Prismatic Chains |
| Chain between death positions | Corpse Explosion |
| Summon a minion | Raise Dead, Stitched Monstrosity, Eye of E'pheraxx, Phantasmal Mimic |
| Banish | Banish |
| Split an enemy | Superego Death |
| Reflect projectiles | Force of Will |
| Rewind playback | Rewind |
| Timed impulse sequence | Ride the Gale, Bound |
| Remove a random debuff | Flicker, Embiggen |
| Trail emitter | Flaming Skull, Burning Feet |
| Stop the world clock | Stop Time |

**Suggested content order:** Petty spells first, since they need only the instant ray and sleep, and
they are the starter kits every run uses. Then the school whose foundations are furthest along, and
the mythics and Aetherics' time spells last.

**Petty spells: built.** Dart, Orb, Sleep, Dazzle, Shock and Rot are code built-ins with placeholder
numbers and a level cap of 3. They pulled small slices of earlier phases forward:

- **Sleep and blind have behaviour.** A sleeping enemy cannot move, attack or notice anything, and is
  half as long asleep when elite. A blind enemy cannot spot you, and one already fighting keeps
  fighting a fixed point where it last saw you, standing in for the perceived position of 2.2.
- **A damage origin, for ticks only** (1.1). Status ticks are marked so they do not wake a sleeper.
- **Statuses that end on damage** (1.5), skipping the applying hit and ticks.
- **A per-status elite duration multiplier** (2.4).
- **A nearest-to-aim selector** for Dazzle, in place of the instant ray.
- **An effect that applies statuses with no hit,** for Shock, Rot and Dazzle.
- **A hit with no damage no longer executes.** It cannot spend a death mark or a mark, or shatter full
  frost, so a sleep bolt is not a finishing blow.

Not yet: the one-Petty-per-offer rule. With only Petty cast spells in the roster, it would shrink every
offer to a single card.

---

## Highest risk

- **The world clock.** It touches nearly every update loop. Retrofitting it after the other systems
  exist is much more work than building it first.
- **Possession.** Enemy AI reaches for the player directly in several places, and both mythics that need
  possession replace the entire control scheme.
- **Knockback impacts.** Closing speed, cascades and wall damage interact with every knockback spell and
  are hard to tune, and crowds of colliding enemies may be expensive.
- **Walking hordes.** An uncapped zombie count that persists between floors, with every zombie pathing
  and colliding.
- **Echo.** Recording and replaying actions needs event plumbing across guns and all three slot types.
- **Nether Smoke's look.** Genuinely opaque smoke is hard to build from code-generated primitives.
- **Silent asset corruption.** Schools, stats, statuses and attributes all change stored integers or
  field names. Each needs its migration verified, not assumed.

---

## Decisions still open

These are unresolved in the design doc, grouped by the phase they block.

**Phase 2, enemy AI**

- What silence and disarm mean for enemies.
- Superego Death: whether the death mark copies onto both halves.
- Assume Identity: whether "only one enemy left" means the room or the floor. `RoomRuntime` already
  counts per room. Also what happens to the controlled enemy when control ends.

**Phase 3, world systems**

- Whether the Bestial companion revives on a timer or is only knocked down.
- Whether the player takes knockback collision damage.
- Stitched Monstrosity: whether recasting with one out refuses or replaces it.
- Lich Guise: whether the Stitched Monstrosity and plague zombies are swap targets.
- Nether Wall's size and duration.
- Nether Smoke: whether it blocks enemy sight, and whether it counts as a hazard.

**Phase 4, player systems**

- Elemental Form: how three forms share one key, and whether it drains mana.
- Repulse: which way it goes when standing still, whether vertical momentum inverts, whether the camera
  turns, and whether it keeps Dash's invulnerability.
- Force of Will: where reflected shots go, and whether a charge is spent with nothing to reflect.
- Superid: what it does with no target in sight.
- Superego Death: whether hitting a split enemy gives bonus charge outside the rate limit.
- Invisibility: whether melee breaks it.
- Space Hammer: whether it knocks back.

**Phase 5, content**

- Divine Star: whether it passes through walls.
- Elemental Chaos: whether its random pick is weighted.
- Numbers for Conflux, the Blood Debt and Divine Knowledge's rungs, with the fourth rung deferred.

**Deferred on purpose:** spell swapping confined to special floors, the one-class-per-school loadout
rework, bigger rooms, and environmental hazards.

---

## Verification

Follow the existing pattern: editor verifiers under the Gunspire menu, run in batch mode, each proven to
fail when the code is deliberately broken. The project already has verifiers for debuffs, the enemy
roster, the holster, the maze generator and its navigation, perception, shot spread, spell slots and
wall-zip maths.

New verifiers worth adding, one per foundation:

- **Migrations:** each reports zero changes on a second run.
- **Stats:** every attribute at a stat of 10 equals today's value at 5.
- **Damage origins:** each source tags its hits correctly, and a burn tick is never mistaken for a
  bullet.
- **Statuses that end on damage:** the applying hit does not end them, a tick does not end them, and a
  direct hit does.
- **Rooms:** a banished enemy keeps its room uncleared, and a split copy is registered.
- **Minion targeting:** nearest hostile with stickiness, elites favouring the player, minions alerting
  enemies that see them.
- **Knockback impacts:** a crowd pushed together takes no damage, a wall impact does, and cascades end.
- **World clock:** a frozen projectile does not move and statuses do not tick, while the player does.
- **Rewind:** health returns exactly, up or down, and history clears on a floor change.
- **Offers:** never more than one Petty spell, and eliminated spells never appear.
