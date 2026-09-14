# Spell design notes

Planning only. Nothing here is implemented. One section per school as the designs land.

Schools: Elemental, Bestial, Abyssal, Divination, Death, Psionic, Aetherics.
Per-school spread: 3 common, 3 uncommon, 2 rare, 1 mythic cast, 1 common movement,
1 rare movement, 1 uncommon melee.

---

## School masteries

Every school grants a standing effect that grows with the number of spells you hold from that
school, with diminishing returns. Bestial's is a summoned beast companion that climbs a ladder
as the count rises: jackalope (a rabbit with antlers), then fox, then wolf, then bear. The chimaera
would only arrive if the mastery cap of four were ever raised.

The point of masteries is that they give spells something to interact with. Bestial gets
spells that buff the companion; Death can hang a resource like souls off its mastery and spend
it. Without the mastery those spells have nothing to attach to.

Elemental gets Conflux and Abyssal gets the Blood Debt, both written out below. Divination gets Divine
Knowledge, written out under Divination. Death gets Souls, written out under Death. Psionic gets
Psi Blades, written out under Psionic. Aetherics gets Arcane Warp, written out under Aetherics.
Every school now has a mastery.

The bar Bestial sets is worth naming, because it is what makes the mechanic work: the mastery
is a **thing**, not a number. A companion can be buffed, cast from, and killed, so spells have
somewhere to attach. A flat damage bonus has no surface. Each mastery below grants something
with a surface, and each school's has a different shape so they do not blur together: Bestial
has an entity, Elemental a reaction, Abyssal a liability, Divination information, Death a
currency, Psionic a charge meter that turns gunfire into melee, and Aetherics an empty mana pool that turns
into speed.

### Elemental mastery: Conflux

Burn, frost and shock can now sit on the same target, which they could not until this week.
The mastery is what happens when they do: applying an element to an enemy that already carries
a different one sets off a reaction.

- **1 spell** - reactions happen at all. Two elements on one target detonate for a modest
  burst of the incoming element's damage type.
- **2** - the reaction is worth real damage.
- **3** - a third element on one target triggers a larger, area-wide detonation.
- **4** - the detonation reapplies one of the consumed elements to everything it catches, so a
  crowd chains.

The Elemental spell list already feeds it without being rewritten. Frost Burn applies two
elements in one cone, Hailstorm applies two over time, Elemental Order applies all three to
everything nearby and is the school's payoff spell, and Elemental Form decides which element
your bullets contribute.

**The risk is noise.** Hailstorm ticking twice a second on a dozen enemies, each tick eligible
to react, would be a wall of explosions and a lot of `OverlapSphere` calls. It needs a
per-target internal cooldown on reacting, and reactions should only fire from elements the
player applied, not from an enemy frostcaller's.

Considered and dropped: elemental spells leaving terrain behind, with rungs lengthening it.
Too close to "your spells last longer", which is a number wearing a coat.

### Abyssal mastery: the Blood Debt

Life spent on Abyssal costs is not gone, it is staked. It becomes debt, shown under your
health bar, and a kill repays it with interest. Fail to kill and you are simply hurt.

- **1 spell** - health costs become debt, and kills repay it at face value.
- **2** - repayment comes back with interest, so a good fight leaves you above where you began.
- **3** - carrying debt raises your spell power in proportion to it.
- **4** - repaid debt overheals into a temporary shield rather than capping at full.

`Health.Drain` already exists and is already correctly walled off from lifesteal, resistances,
invulnerability and the damage hooks - its own comment says it is for "a price the player
agreed to pay". Spells attach to the debt easily: costs paid in life, a spell that raises the
interest rate, a mythic that clears the whole debt at once for a burst.

It is a currency like Death's souls, but inverted - a liability you take on deliberately and
must clear, against a currency you harvest and spend. That contrast is worth keeping; if it
starts to feel the same in play, one of the two should change shape.

**The one hard rule.** Blood Debt must never be able to kill you. `Health.Drain` can kill by design,
and the debuff work deliberately established that the player is never executed by a status,
because an instant death from something the HUD showed for a second reads as the game
breaking. Blood Debt needs a floor that leaves you alive at one hit point, and the cost has to be
refused rather than clamped when you cannot afford it.

Considered and dropped: spell power simply rising as your health falls. A multiplier, so
nothing can hook into it.

### How masteries are counted

**Counted from spells equipped, not spells known.**

**Decided: five slots, with the mastery count capped at four.** Three cast slots, one movement
and one melee. The third cast slot is new. Capping the count at four means one slot is always free
for a second school: four Bestial spells plus one Death spell still gives a bear, and a small soul
pool besides.

- **Decided: the third cast slot is bound to F.** Interact moved from F to X, and weapon swapping
  from X to C, to make room. The mouse wheel still swaps guns.
- **Building it.** `SpellBook.SlotCount` and its key list define the cast slots, so the third slot
  is mostly a number and a key. The HUD draws each slot and loadouts name a starting slot index,
  so both need checking.

Three things follow from counting equipped spells.

- **The ladder is the capped count.** One spell is a jackalope, two a fox, three a wolf, four a
  bear. No threshold tuning needed; the tiers are the numbers. The chimaera is off the ladder
  unless the cap rises.
- **Diminishing returns has to live in the size of each step, not the spacing of them.** With
  only four rungs there is no room to widen the gaps. Jackalope to fox should be a large jump
  and wolf to bear a small one, so that the first spell of a school is worth far more than the
  fourth. That also keeps a two-school split viable instead of making full commitment the only
  sane build.
- **Swapping is not a mid-level action.** Changing equipped spells should only be possible on
  special floors that allow it, which means a mastery tier only ever changes at a floor
  transition rather than mid-fight. Not built yet; deferred along with the loadout rework.

### Starting loadouts, later

The free-mastery problem this raised - every run opening with `dash` and `bash` bound, and so
with mastery in whatever schools those belong to - goes away with a planned rework of the
starting loadouts into one class per school. Deferred until then.

**Both halves are now solved another way.** Dash has moved to the Petty spells, which belong to no
school, so a default dash no longer hands out a free Bestial pip. The default melee, `bash`, becomes
the Petty melee spell, so it stops handing out a free Elemental pip too. Every starting kit can be
mastery-neutral.

Most of the machinery is already there. `LoadoutDefinition` carries `MovementAbilityId`,
`MeleeSpellId`, `SpellId` and `SpellSlot`, and there are five loadouts today (Fortune Hunter,
Juggernaut, Paladin, Warlock, Wizard) against seven schools. The global
`SpellLibrary.DefaultMovementId` and `DefaultMeleeId` stay as the fallback for a loadout that
does not name its own.

One thing to settle when that happens: **how many of a class's starting slots come from its
own school.** All three fillable ones puts a class at three of four rungs on the first floor,
leaving the entire ladder as a single rung of progression. Starting with only the movement
spell in-school leaves three rungs to earn. The answer decides whether mastery is something
you climb during a run or something you mostly start with.

### Other open questions

- **Two schools at once is built in.** With five slots and a cap of four, one slot is always
  spare for a second school. An even split works too: two Bestial and two Death spells give a
  fox and a small soul pool, which is why the early rungs of each ladder should be the valuable
  ones.

### What already exists

The familiar system covers most of Bestial's mastery. `FamiliarDefinition` carries health,
speed, attacks, auras and a damage multiplier; `FamiliarController` handles following,
engaging and empowerment; `FamiliarSummoner.Resummon` rebuilds the live creature from an
ownership record on `RunState` each time a room is entered. The beast ladder is five
definitions and a function mapping a spell count to one of them.

**Correction: familiars cannot walk.** `FamiliarController` writes its position straight to the
transform and deliberately ignores the level, so a familiar drifts through walls. That suits a
wisp. A fox, wolf or bear gliding through a maze wall does not. The beasts need the walking body
enemies already have, a character controller and the flow field, on the player's team. Death's
Raise Dead zombies need exactly the same thing; see Death's implementation notes.

Two gaps.

- **Recompute on change.** Ownership is currently only rebuilt on entering a room. Once spell
  swapping is confined to special floors this may need nothing at all: a swap floor is a floor
  transition, and the beast would be rebuilt on arrival anyway. Worth rechecking once those
  floors exist rather than hooking `SpellBook.Changed` pre-emptively.
- **Death.** Familiars have `Health` and can die, and nothing resummons them until the next
  room. That is survivable for a familiar you bought; it is harsh for a companion that is
  supposed to be a standing property of your build, and worse for the spells below that are
  cast from the companion's position. Either the mastery beast revives on a timer or it is
  made unkillable and only ever knocked down. **Now pressing:** enemies attack minions, decided
  under Death, so the companion will be focused rather than only catching stray blasts.

---

## Elemental

### Common

**Flaming Skull** - sends a fiery skull (an orange ball for now) forward in a semi-random
forward direction: it sways left and right as it travels. Deals damage, applies burn, and
leaves a trail of fire behind it. The fire on the ground damages anything standing in it and
lasts a few seconds. Enemies try not to stand in the fire. The skull passes through enemies
and keeps going; terrain stops it.

**Ice Lance** - launches an icy bolt in a straight line. Damages the first enemy it hits and
applies frost.

**Storm Blast** - a blast of wind knocks enemies forward. The blast starts from behind the
caster, so enemies behind and beside the caster are knocked forward too.

### Uncommon

**Frost Burn** - a cone of flame that applies frost and burn.

**Hailstorm** - a zone of cold wind raining hail and lightning. Deals damage, applies frost
and shock. Lasts 15 seconds, about one room across.

**Star Comet** - drops a fire and lightning comet from the sky. Energy and kinetic damage,
knocks enemies away, applies shock.

### Rare

**Elemental Chaos** - casts 2 random uncommon Elemental spells.

**Elemental Order** - applies burn, shock and frost to all nearby enemies.

### Mythic

**Elemental Form** - a stance. Swap between fire, ice and storm forms, each of which changes
your bullets.

| Form | Bullets apply | Also |
|---|---|---|
| Fire | Burn | Passive damage to nearby enemies |
| Ice | Frost | Reduced recoil and weapon spread |
| Storm | Shock | Increased movement speed |

### Movement

**Ride the Gale** (common) - a short, constant horizontal force forward, then after a short delay
an instant upward force, followed by one second of slowed fall. It traces an S curve, most of all when cast in mid-air.

**Burning Feet** (rare) - increases movement speed, and leaves a trail of fire behind the
caster the same way Flaming Skull does.

### Melee

**Rimeblade** (uncommon) - wide horizontal arc, kinetic damage.

---

## Elemental: implementation notes

### Resolved: frost and burn now coexist

Frost and burn used to cancel each other. `FrostStatus.Cleanses` listed Burn and
`BurnStatus.Cleanses` listed Frost, so `StatusController.Apply` stripped one whenever the
other landed. Both overrides are gone. It is magic; they are allowed to burn and freeze at
once.

That unblocks Frost Burn and Elemental Order, which each apply both in a single cast and
would otherwise have delivered only whichever status happened to land last. It also means a
fire build can still walk a target toward frost's hundred-stack execute instead of thawing
its own stacks on every shot, which matters most for Elemental Form: fire form can now be
worn without giving up frost entirely.

`VerifyDebuffs` covers it, applying both in either order and checking the frost stack count
survives. The `Cleanses` hook itself stays - it has no users now, and a cleanse or dispel
spell will want it.

### Works today, no new code

- **Ice Lance** - `SpawnProjectileEffect` with a frost payload.
- **Storm Blast** - `DealDamageEffect` with `FalloffFromPoint` off already pushes along
  `ctx.Forward` rather than radially outward, which is exactly "everyone gets knocked the way
  the caster is facing". Selecting the targets behind the caster needs a back-offset origin;
  `OriginFromCasterEffect` has a height offset but no backward one, so that is one field.
- **Hailstorm** - `LingeringZoneEffect` takes duration, radius, tick interval and a status
  payload. A 15-second room-sized zone is a data entry.
- **Star Comet** - `DelayedBlastEffect` has the telegraph, delay, radius, damage and
  knockback. Only the falling visual is missing.
- **Rimeblade** - `SweepForwardEffect` plus `SelectConeEffect`, which is how the existing
  melee spells are built.
- **Burning Feet** - the speed half is a sustained movement spell with `MoveSpeedBonus`, same
  shape as Sprint. The fire trail is not; see below.
- **Ride the Gale** was a single `ImpulseSelfEffect`. Its S curve rework needs new code, noted in
  its own section below.
- **Flaming Skull passing through enemies but stopping at terrain** - `Projectile.HandleHit`
  already decrements `Pierce` only on damageables and always dies on world geometry. A large
  `Pierce` gives the behaviour for free.

### Needs new code, small

- **Two damage types in one cast** (Star Comet's energy and kinetic). `DamageType` lives on
  the context for the whole cast, not per effect. A ten-line `SetDamageTypeEffect` that
  assigns `ctx.DamageType` mid-chain covers it and serves every mixed spell after.
- **Projectile sway** (Flaming Skull). `Projectile` supports gravity and homing, nothing
  oscillating. Two fields and three lines in `Update`.
- **Fire trails** (Flaming Skull, and Burning Feet). See the section below - shared between
  the two, and the larger of the two pieces.
- **Recoil and spread as stats** (ice form). `SpreadDegrees`, `MovingSpreadDegrees`,
  `RecoilPitch` and `RecoilYaw` are read straight off the weapon definition; there is no
  `Attr` for either. Two new attributes plus multiplying them in at `Weapon.ApplySpread` and
  the `Look.AddRecoil` call.

### Needs new code, larger - fire trails

Flaming Skull and Burning Feet want the same thing: something that lays burning ground behind
a moving object. Build it once, as an emitter that anything can carry, rather than twice.
`Projectile` currently runs an effect chain on impact only, and nothing exists for the player.

Two rules it has to follow.

- **Gate on distance travelled, not on a timer.** For the skull it makes no difference because
  it never stops. For a player it makes all the difference: a time gate piles a stack of
  overlapping patches under anyone who stands still, which both looks wrong and multiplies the
  damage a stationary player deals for free.
- **One object owning the whole trail, not one per patch.** For the skull a list of small
  zones would have been tolerable - a 40m room at one patch per 2m is twenty of them. Burning
  Feet is unbounded: held for ten seconds at running speed that is fifty or more alive at
  once, each doing its own `OverlapSphere` twice a second. A single object walking one list of
  segments costs one query per tick regardless of trail length.

The player will not burn themselves. `LingeringZone.Tick` queries `Layers.TargetMaskFor`
against the owning team, so a player-owned patch only ever finds enemies. Worth stating
because Burning Feet has the caster running through their own fire constantly.

### Balance: Burning Feet, resolved by making it rare

This was a worry while Burning Feet was common. A movement spell that also lays sustained area
damage, on a slot every player has filled, is a lot for the cheapest tier, and paired with
enemies avoiding fire it made a common spell into crowd control that could wall off a corridor.

Moving it to rare and promoting Ride the Gale to common settles it. A rare movement spell is
allowed to be a second damage source, and the cheap tier goes back to buying plain mobility.

The tuning note still stands in the other direction: a short patch lifetime and low tick damage
keep it a mobility spell that leaves a mark rather than a damage spell that also runs.

### Ride the Gale: the S curve

**Decided: forward, then up, then a slow fall.** A short constant forward force, then after a
short delay an instant upward force, then a slowed fall. It should trace an S curve, most of all
when cast in mid-air.

- **Three phases make it a timed effect.** The old version was one impulse. Now the forward push
  applies a little force every frame for a short window, then waits, then gives one upward
  impulse. `RepeatEffect` and `WaitEffect` already express that kind of chain.
- **Friction eats the push on the ground.** `PlayerMotor` applies Quake-style ground friction on
  every grounded frame, so a steady forward force started on the floor is mostly scrubbed away
  until the upward kick lifts you. The air has no friction, which is why a mid-air cast gives the
  cleanest S. Either accept a shorter, flatter curve from the ground, or skip friction while the
  push lasts. The motor already skips it on the frame of a jump, so the switch exists.
- **Impulses ignore the air speed cap.** Air movement caps how fast your input can push you, but
  `AddImpulse` writes velocity directly, so the push is not capped. Good for the feel, but it
  stacks with momentum from Dash or Repulse.
- **Slowed fall needs a gravity scale.** Gravity is one fixed value on the motor, applied on every
  airborne frame, with nothing to multiply it by. Rapture of the Deep needs the same thing to turn
  gravity into buoyancy, so build one gravity multiplier that both use.
- **Decided: the slow fall lasts one second, for now.** It should also end early if you land, or
  a second jump taken inside that second floats as well.
- **Decided: horizontal, then vertical.** The forward push uses your aim direction with its pitch
  removed, so looking up or down while casting changes nothing. All the height comes from the
  upward force.
- **Up means the gravity up.** While wall-walking with Spider Gravity, up is away from the wall,
  which the motor already tracks for jumping.

### Needs new code, larger - enemies avoiding fire

**Decided: a soft push, not a refusal to enter.**

There is no hazard awareness anywhere today. `NavField` stores one walkable bit per cell and
the flow field is a single BFS from the player shared by every enemy, so there is nowhere to
put a cost for "this cell is on fire". Weighting the field would be the honest version - the
comment on `NavField.Rebuild` already anticipates weighted costs - but it turns the BFS into a
Dijkstra and has to rebuild whenever a zone appears or expires.

The soft push does not need any of that. `EnemyController` already sums a `Separation()` push
away from its neighbours in `ApplyMotion`; a push away from nearby hazards is the same shape
and costs one list of live zones. Roughly twenty lines, no field rebuild.

Being imperfect is the point rather than a compromise. An enemy that shies out of a fire it
clipped reads better than one that pathed around it perfectly, and a determined charge still
comes through a burning doorway having taken the damage for it. That keeps Burning Feet from
turning into a wall, and it means a zone spanning a full corridor slows a push instead of
stalling a room that gates its exit on being cleared.

One thing it will not do: flying enemies null out their vertical velocity every frame in
`Hover`, so a hazard push only moves them horizontally. Same reason Star Comet's knockback
will not launch them, though it still shoves them sideways.

### Elemental Form: the stance itself

Three problems beyond the bullet infusion.

- **Sustained spells are Movement-only right now.** `SustainProfile.Exists` is read off
  `ManaPerSecond`, and `VerifySlots` explicitly reports "sustained but sits in the Cast slot"
  as a problem. A Cast-slot stance means relaxing that rule, and deciding whether a stance
  drains mana per second at all or is a free state you toggle.
- **Three modes on one key.** Every spell is one key press. Cycling fire to ice to storm and
  off is four states on one button, and there is no second input on a spell slot to hang a
  cycle on. Options: the key cycles forward and the stance never turns off while mana lasts;
  or hold to cycle, tap to cast; or the form is chosen once when the spell is learned. Worth
  deciding before any of it is built.
- **Bullet infusion surviving a gun swap.** `Weapon.ExtraStatuses` is the right hook and is
  already driven by `RunState.BulletStatuses`, but `RunState.Bind` pushes it onto the live
  weapon once. `Holster.Draw` replaces what the live weapon points at, so the infusion needs
  re-pushing on swap or the second gun comes out uninfused.

### Elemental Chaos

`Spell.Cast(context, level)` is public and cooldowns live per slot on `SpellBook`, not on the
spell, so one spell casting another is mostly free. Three things to get right:

- Filter the pool by school **and** rarity, which also keeps Chaos from rolling itself, since
  it is Rare and only draws Uncommons. Add the self-exclusion guard anyway; a future uncommon
  that casts a random spell would otherwise recurse.
- Chaos pays its own mana, not the two children's.
- Its power is whatever the uncommon pool happens to hold. Two Hailstorms is a very different
  spell from two Ice Lances, and every uncommon Elemental spell added later silently retunes
  it. Either accept that or weight the pick.

---

## Bestial

Mastery: a beast companion that climbs jackalope, fox, wolf and bear as the number of Bestial
spells you have equipped rises, up to the cap of four. See School masteries above.

With three cast slots there are several routes to a bear: three Bestial casts plus a Bestial
movement or melee spell, or two casts plus both.

### Common

**Murder** - summons a murder of crows to attack enemies in a zone. Area denial and area
damage. You and your companion move faster inside the zone.

**Howl** - improves your companion's attack speed, and lets you shoot and reload faster.

**Blood Scent** - detect enemies through walls.

### Uncommon

**Fang and Claw** - horizontal melee arc that applies bleeding. Also cast from your
companion's position.

**Embiggen** - you and your companion grow larger, dealing more melee damage and fully
shrugging off a random debuff. Short duration.

**Lunge** - jump forward and attack.

### Rare

**Viper's Sting** - your attacks and your companion's, melee and ranged both, apply poison.

**Hunter's Mark** - shoot a dart forward that applies deathmark. Long cooldown.

### Mythic

**Shapeshift** - three options presented when you pick the spell. Casting shifts you into the
animal until you cast it again, and you cannot cast other spells while shifted.

| Form | Left click | Right click |
|---|---|---|
| Death Cobra | Spit venom | Leap forward and bite |
| Alpha Stag | Charge forward unstoppably (hold) | Bash with hooves |
| Tyrant Lizard | Bite | Swipe |

The Tyrant Lizard is a tyrannosaur and raptor hybrid.

### Movement

**Bound** (common) - two quick hops in the direction you are moving, like a hare, the second
steerable in the air.

**Spider Gravity** (rare) - the existing spider legs ability.

### Melee

**Maul** (uncommon) - melee attack that stuns.

---

## Bestial: movement candidates

Dash moved to the Petty spells, so Bestial needs a new common movement spell.

**Pack Rush** - a short burst of speed for you and your companion together, with
the companion bounding to your side. Bestial spells are meant to interact with the companion, and
this is the movement slot doing so, the way Bloodwake feeds the Blood Debt. It never depends on the
companion to work. Equipping it is itself a Bestial spell, so you always have at least a jackalope,
and the speed is still yours if the beast is dead.

**Bound** *(chosen)* - two quick hops in the direction you are moving, like a hare, the second
steerable in the air. A distinct feel among the commons: Ride the Gale arcs, Ascend lifts,
Gravewalk lunges and Repulse reverses. It suits a ladder that starts with a jackalope.

- **Two impulses with a gap make it a timed effect,** the same shape as Ride the Gale: one hop, a
  short wait, then a second hop. `WaitEffect` and the motor's `AddImpulse` already cover it.
- **The second hop reads your input when it fires,** not when you cast, which is what makes it
  steerable.
- **Hops ignore the air speed cap,** like every impulse, so the second hop keeps its full strength
  in the air.
- **Only the first hop meets friction.** A hop from the ground loses a little to ground friction as
  it starts, while the second is always airborne. The motor already skips friction on the frame of
  a jump, and a hop can do the same.
- **Up means the gravity up** while wall-walking with Spider Gravity, as for Ride the Gale.
- **Decided: with no movement input, both hops go straight up.** That makes a standing Bound a
  double jump. Ascend also goes straight up, but with a hang in the air and brief invulnerability,
  so the two stay distinct.

Rejected: **Pounce**, a leap onto an enemy, since Lunge already does that as an uncommon cast.
**Burrow**, digging under the ground and surfacing ahead, since Blink now passes through walls.

---

## Bestial: implementation notes

### Works today, no new code

- **Hunter's Mark** - `SpawnProjectileEffect` carrying a deathmark payload. Deathmark is built
  and verified, including the rule that it takes a chunk off elites rather than killing them.
- **Lunge** - `ImpulseSelfEffect` then a melee arc, which is how the existing lunging attacks
  are built.
- **Spider Gravity** - already shipped, already an asset. It moves school and keeps its
  behaviour. Dash, which was also listed here, is now a Petty spell.

### Needs new code, small

- **Howl** - `Attr.AttackSpeed` and `Attr.ReloadSpeed` both exist, so the player half is a
  self-buff. Familiars carry a `CharacterSheet`, so the companion half is the same status
  applied to a different target. `FamiliarController.Empower` is close but takes a damage
  multiplier specifically; a status is the more general answer.
- **Murder** - a `LingeringZone` for the damage and denial. The friendly speed aura is the new
  part: zones only ever query `Layers.TargetMaskFor` the owning team, so buffing allies inside
  one means a second query against the player mask applying haste. Ten lines, and it makes
  every future "good zone" possible.
- **Fang and Claw from the companion's position** - a sibling to `OriginFromCasterEffect` that
  repoints `ctx.Origin` at the live familiar. Watch `SweepForwardEffect`: it clamps its reach
  against geometry using `ctx.Controller`, which is the player's `CharacterController` and
  will be in the wrong place. It needs the origin it is actually sweeping from.

### Needs new code, medium

- **Stun** (Maul). No such status exists. It overlaps almost entirely with the planned snare,
  so build one status with a magnitude rather than two: snare slows, a full-strength
  application stops. The caution from the snare discussion applies here too - a stun that
  stops an ordinary enemy outright makes frost's hundred-stack ceiling pointless by
  comparison, so keep it short and let it apply to elites at reduced duration rather than
  exempting them.
- **Blood Scent** - nothing renders through walls, and the project has no authored shaders;
  every material comes from `Shader.Find` on a built-in. A silhouette pass means either a
  custom shader or overriding `_ZTest` on a built-in material, which is unreliable across
  them. The cheap and reliable version is screen-space markers drawn by `HudUI`, which already
  draws in screen space, optionally with a ground ring under each enemy. Worth agreeing the
  look before building, because the two options produce very different spells.
- **Embiggen's debuff cleanse.** The `Cleanses` hook is still in `StatusDefinition` and has no
  users since burn and frost stopped cancelling each other. This is its first real one, though
  "a random debuff" wants a pick-one-at-random helper on `StatusController` rather than a
  static array.
- **Embiggen's actual size change.** Scaling the player is the risky half. `CharacterController`
  does not follow the transform the way you expect - that is what broke spider legs - and
  growing a player standing in a doorway is a good way to push them into geometry. Scale the
  visual only, or scale the capsule and immediately depenetrate. The companion has the same
  problem with less consequence.

### Needs new code: timed bullet infusion

**Viper's Sting** puts poison on your attacks for a duration. `Weapon.ExtraStatuses` is the
right hook and is fed by `RunState.BulletStatuses`, but that list is run-long and has no
concept of expiry - it was built for permanent boons. A timed entry is needed.

Elemental Form needs exactly the same thing for its bullet infusion, so this is one piece of
work serving two schools. The same fix should also handle the gun-swap problem noted under
Elemental Form: `RunState.Bind` pushes the list onto the live weapon once, and `Holster.Draw`
then repoints what the live weapon is, so the infusion has to be re-pushed on a swap.

Viper's Sting also covers the companion's attacks. Familiar attacks come from
`AttackDefinition` rather than from a `Weapon`, so they need their own extra-status channel.

### Needs new code, large - Shapeshift

The biggest single item in either school so far, and worth treating as its own project.

- **Spells have no variants.** `Spell` has `MaxLevel` but nothing that lets one asset present
  three choices at pick time. Either Shapeshift is three separate spells that share a name and
  a rarity, or spells gain a variant concept. Three assets is much cheaper and loses only the
  "choose when you take it" moment, which the offer screen could fake by presenting all three.
- **It replaces the whole control scheme.** Left and right click stop being the gun, spells
  stop being castable, and the player has bespoke abilities instead. That is a player state
  machine, not an effect chain.
- **"Cannot cast other spells" is silence.** The planned silence effect is exactly this gate,
  applied to yourself. Build silence first and Shapeshift uses it rather than inventing a
  second way to block casting.
- **Alpha Stag's held charge** is the only one that is not a discrete attack. A held,
  unstoppable forward move is closest to the sustained movement machinery, not to an attack.
- **Each form needs a body.** Everything in the game is code-generated primitives, so three
  animal shapes is three more builders, plus whatever collider each needs.

Worth asking whether Shapeshift should be built once as one form, shipped, and extended to
three later. All three at once is a lot of surface area for a single spell.

---

## Abyssal

Mastery: the Blood Debt. Life spent on Abyssal costs is staked rather than lost, and kills repay it
with interest. See School masteries above.

### Common

**Gush** - a blast of water dealing kinetic damage, knocking enemies up and away.

**Ink Spray** - blinds enemies in a cone for a few seconds. Blind is a new debuff: the enemy
cannot see the player, and shoots at the last place it saw them.

**Depth Grasp** - tentacles burst from the ground and slow movement.

### Uncommon

**Soul Bargain** - increases damage dealt and damage taken for a short duration.

**Drown** - silences and damages an enemy.

**Eye of E'pheraxx** - summons an immobile eyeball that stares at the nearest enemy, dealing
constant necrotic damage. It switches targets as enemies move closer.

### Rare

**Leviathan** - rapidly applies frost across an area, then after a delay a leviathan breaches
the zone, striking everything still inside with a burst of kinetic damage from below.

**Fathomless Gate** - opens a whirlpool that drags enemies toward the centre while tentacles
lash them. Enemies know to move away from the centre.

### Mythic

**Unspeakable One** - summons the Unspeakable One, a kraken. Its many tentacles burst from the
ground at random nearby locations to strike enemies, and its maw rises to eat any enemy that
gets close. As each tentacle spawns it applies fear to the one enemy nearest it. The maw does
not cause fear, so an enemy caught between the maw and a tentacle flees straight into the mouth.

**Fear** is a new debuff. A feared enemy moves faster, and away from whatever feared it.

*This replaces an earlier version, a slithering beast that trampled enemies and fed on corpses
to stay longer.*

### Movement

**Bloodwake** (common) - a sustained speed boost paid for in health rather than mana, leaving a
wake of blood behind you.

**Rapture of the Deep** (rare) - gravity becomes buoyancy. You stop falling and swim through
the air as though underwater: slow, floating, able to rise and sink at will.

### Melee

**Tentacle** (uncommon) - strikes an enemy with a tentacle, knocking it away.

---

## Abyssal: movement candidates

The movement pair is common plus **rare**, as the spread at the top of this file says.
Elemental and Bestial were retiered to match: Ride the Gale and Dash are the commons, Burning
Feet and Spider Gravity the rares.

### Common

**Bloodwake** *(chosen)* - a sustained speed boost paid for in health rather than mana,
leaving a wake of blood behind you. It is the only movement spell in the game that feeds its
own school's mastery: every point of health it burns becomes Blood Debt, so running fast puts you
deeper in the hole and a kill clears it. That makes the school's common movement spell a
statement of what Abyssal is.

Two things to get right when it is built.

- **A health-cost sustain reads as not sustained.** `SustainProfile.Exists` is defined as
  `ManaPerSecond > 0`, deliberately, because Unity never leaves that profile null. A spell that
  drains only health would report itself as an ordinary one-shot cast. The flag needs to read
  either drain, not just mana.
- **It must stop at the Blood Debt floor, not drain you to death.** The Blood Debt's hard rule is that it
  never kills you and costs are refused rather than clamped. A continuous drain has no single
  moment to refuse, so Bloodwake should switch itself off when the next second's cost would
  cross the floor.

**Undertow** - a forward surge on a current that drags any enemy you pass through along in
your wake, leaving them behind you and out of position.

### Rare

**Rapture of the Deep** *(chosen)* - gravity becomes buoyancy. You stop falling and swim
through the air as though underwater: slow, floating, able to rise and sink at will. It is the
natural counterpart to Spider Legs, which reorients gravity where this removes it, and
`PlayerMotor` already carries the gravity handling that Spider Legs needed. The roof helps
here: `MazeBuilder.BuildRoof` puts one solid collider over the entire maze at wall height, so
a swimming player is contained without any new bounds check.

**It crosses embrasure walls.** The embrasure room is built around a wall you can shoot through
but not pass. That wall is 3.4m tall under an 11m roof, deliberately low so flying Gazers can
cross it, and a normal jump reaches roughly 1.5m. A floating player can rise over it just as a
Gazer does. It cannot soft-lock anything, since the maze layout never relies on crossing an
embrasure to connect the level, so at worst it is a shortcut. Probably a fair reward for a rare,
but a decision rather than an accident. Ascend raises the same question if its lift clears the
wall.

**Sound the Depths** - you sink into the floor and swim through solid terrain, untouchable but
unable to shoot, surfacing where you stop. Strong and very Abyssal, but it overlaps Aetherics,
which already owns stepping through a pocket dimension to blink.

Rejected: a tentacle grapple that hauls you to a surface. That is Spider Legs' zip with a
different skin.

---

## Abyssal: implementation notes

### Works today, or nearly

- **Soul Bargain** - `Attr.DamageDealt` and `Attr.DamageTaken` both exist, and Weaken and
  Fortify are already the inverse pair. A self-status with two modifiers.
- **Gush** and **Tentacle** - knockback is a full `Vector3` on `DamageInfo`, so knocking
  upward as well as away is just the vector. Flying enemies will not rise, for the reason
  already noted: `Hover` overwrites vertical velocity every frame.
- **Leviathan** - `LingeringZoneEffect` applying frost, then `DelayedBlastEffect`, which
  already carries the telegraph and the delay. The breach is the visual.
- **Depth Grasp** - a slow is the planned snare at low magnitude. Folds into the one
  snare-and-stun status noted under Maul rather than being its own thing.
- **Eye of E'pheraxx** - the familiar system covers most of it: a `FamiliarDefinition` with
  zero move speed, an engage range, and a `BeamEffect` attack. The one mismatch is that
  `FamiliarController` follows its owner, so an immobile summon needs that suppressed, plus
  nearest-target retargeting instead of the current behaviour.

### Blind is a new debuff, and it is shared work

Enemies currently track `Target`, which is the player's live `Transform`, and `HasLineOfSight`
raycasts straight at it. Nothing anywhere holds a stale position, so "shoots at where it last
saw you" has nowhere to read from.

The fix is a perceived position on the enemy that normally follows the target and freezes when
perception is broken. Aiming and line of sight read that instead of the transform.

Worth doing well, because Blind is not the only customer. Aetherics' invisibility is the same
mechanism - blank the sight check, leave hearing intact - and so is any future stealth. Build
it once for Blind and invisibility is nearly free.

### Silence now has three customers

Drown needs it, Shapeshift needs it to stop you casting while transformed, and it was already
on the list of new effects alongside snare and disarm. It should be built before any of the
three rather than as part of whichever comes first.

### Fathomless Gate reuses two things

The pull is knockback with the sign flipped - `DealDamageEffect` already computes an `away`
vector and scales it, so pulling is the same code pointed inward. And "enemies know to move
away from the centre" is exactly the soft hazard push decided for fire. Second customer for
that system, which is a good sign it was worth building generally rather than as fire-specific
code.

### Fear is a new debuff, and a maze makes "away" hard

Fear shows up twice so far, in Unspeakable One and in Divination's Reckoning, so it is shared
work like Blind and silence.

- **Decided: fleeing uses pathfinding, the opposite of hunting.** A hunting enemy follows the
  flow field down toward the player; a feared one climbs it away. That keeps feared enemies out
  of walls, which a straight line away would not.
- **The reversed field only works when the player is the source.** The flow field is one search
  outward from the player, so reversing it means "away from the player" and nothing else.
  Reckoning's fear comes from the player and gets it for free. A tentacle's does not: the
  reversed field would send a tentacle-feared enemy away from you rather than away from the
  tentacle. Fear from any other source needs the local version, stepping to whichever walkable
  neighbouring cell on `NavField` is furthest from the source point. Same wall avoidance,
  pointed at the right thing.
- **Remember the source as a position, not an object.** Statuses keep their source as a
  `GameObject`. A tentacle that despawns mid-fear leaves that null, and the enemy has nothing to
  flee from.
- **Dead ends are guaranteed.** The maze is a spanning tree with loops added, so dead ends
  exist by construction, and feared enemies will regularly run into one and be cornered. That
  reads well, but it means fear often ends with an enemy stuck at the end of a corridor rather
  than escaped.
- **Faster and away means out of your range.** Against a short-range build, fear can end a
  fight the player was winning, and a room that gates its exit on being cleared waits for the
  enemy to come back. Keep durations short.
- **Decided: feared enemies cannot attack.** An attack already underway when fear lands should
  be cancelled rather than finished, or a slam that started a frame earlier still lands.
  `AbilityAttack` already tracks whether it is executing, so the cancel has something to check.
- **Elites.** The pattern so far is that control bends elites rather than exempting them.
  Fear at reduced duration fits it.
- **Decided: fear alerts an enemy that had not noticed you.** Applying it calls the same alert
  a sighting or a heard gunshot does, so the enemy flees knowing you are there and returns
  hunting. This applies to fear from any source, tentacles included.

### Unspeakable One

- **Tentacle placement is already solved.** `NavField.TryFindSpot` finds a walkable point near
  a position, so tentacles never burst from inside a wall or a rock block.
- **The maw is an execute.** Deathmark and full frost established that elites are never killed
  outright; they lose a chunk. The maw should follow the same rule, or it becomes the answer to
  every elite. As an execute, it deals the new execute damage type described under Prismatic
  Chains.
- **Decided: tentacles fear, the maw does not.** Fear drives an enemy away from the tentacle
  that scared it, so one standing between a tentacle and the maw runs into the mouth. Fear
  herds rather than scatters. Placement decides how often that happens: tentacles at random
  nearby spots herd by luck, while spawning them on the far side of enemies from the maw would
  herd on purpose. Try random first, since that is the spec.
- **This fear cannot use the hunting flow field.** A tentacle is not the player. See the fear
  section above: these enemies need the walkable step away from a point, not the reversed
  gradient, or they flee away from you and straight past the maw.
- **One enemy, several fears.** Several tentacles spawning at once can each find the same
  enemy nearest. Each should pick the nearest enemy that is not already feared.

The corpse tracking the old version needed is no longer required, and Death's souls are bound on
kill rather than collected from where enemies fell, so nothing needs it.

---

## Divination

Mastery: **Divine Knowledge.** Additional information about enemies, more per spell equipped:
health bars, vision cones before combat starts, a timer on the next attack, and so on.

### Common

**Smite** - your next bullet also calls a bolt of lightning down on the target, dealing energy
damage and applying shock.

**Prismatic Chains** - tethers an enemy to another nearby enemy. They cannot move far from each
other, and kinetic or energy damage dealt to one deals psychic damage to the other.

**Underworld Vial** - thrown at a location like a grenade. It smashes open, splashing necrotic
damage and poisoning enemies.

### Uncommon

**Divine Assistance** - summons your other weapon beside you, and it fires whenever you fire,
with infinite ammo for the duration. You cannot change weapons while it lasts.

**Foretell** - dodges the next attack that would otherwise hit you, within a short window. The
cooldown is shorter if it actually dodges something.

**Divine Star** - spawns a slow-moving star that hunts the highest-health enemy in combat with
you and explodes on it.

### Rare

**Reckoning** - fears nearby enemies.

**Judgement** - a grenade carrying a set amount of burn, split between every enemy caught in a
medium area. The idea is to catch just one.

### Mythic

**Consecrate** - creates a holy zone that boosts movement speed, damage and damage resistance.

### Movement

**Ascend** (common) - a column of light lifts you straight up, making you briefly invulnerable,
and you hang there before drifting back down.

**Path of Light** (rare) - lights the route to the floor's exit for about 10 seconds, and doubles
your speed while you move along it. It does not force you toward the exit. 20 second
cooldown.

### Melee

**Punish** (uncommon) - applies burn and deals no other damage.

---

## Divination: the Divine Knowledge ladder

Proposed order, following the list above, with the fourth rung open:

- **1 spell** - health bars.
- **2** - vision cone and hearing range, shown before an enemy has noticed you.
- **3** - a timer on each enemy's next attack.
- **4** - deferred. Candidates so far: the attack it is about to use, or its current statuses
  and resistances.

Since diminishing returns lives in the size of each step, the first rung should be the most
valuable one. Worth checking that health bars really are, over attack timers.

**How it fits the bar the other masteries set.** Divine Knowledge is information, which is
neither a thing nor a number, so spells cannot attach to it the way they attach to a companion
or a debt. They attach through the player's decisions instead - which is what the school
already says it is for, sharpening the wizard rather than the spell. The spell list leans into
that already. Foretell is far stronger when you can see the attack coming, Divine Star and
Judgement both care which enemy is which, and Prismatic Chains wants you to know which two to
link. The risk is that the value is invisible until a player learns to read it, so it can feel
like it does nothing.

It also makes Divination the school for approaching a room unseen. The vision rung reads
straight off the perception system, and pairs naturally with Aetherics' invisibility.

**What exists.**

- **Vision cones** - `EnemyController` carries `SightRange`, `SightHalfAngle` and
  `HearingRange`, and `IsAlerted` is exactly the line between before combat and during it.
- **Attack timers** - `AbilityAttack.CooldownRemaining` is already public.
- **Health bars** - nothing. No enemy has a health bar anywhere today. `HudUI` draws through
  `OnGUI`, so bars projected from enemy positions need no new UI technology, but this is the
  most new code of the three.

---

## Divination: movement candidates

### Common

**Ascend** *(chosen)* - a column of light lifts you straight up, making you briefly
invulnerable, and you hang there before drifting back down. It is the only common that lifts
you straight up and holds you there: Ride the Gale arcs forward and then up, Dash goes forward, and
Bound can hop straight up but does not hang. The level gives it real use too. Embrasure walls are
3.4m tall, so a moment of hang time lets you fire over the wall instead of through the slot.

The invulnerability matches Dash, whose description already promises a sliver of it, so a
common movement spell granting it is not an outlier, and `GrantInvulnerabilityEffect` already
exists. One tuning line: cover the lift, not the hang. Invulnerable for the whole hang is a
free window to shoot over a wall at enemies who cannot answer, on a cheap common.

**Hallowed Leap** - a forward arc, whose landing sends out a pulse that grants one extra rung
of Divine Knowledge on the enemies it touches, for a few seconds. Ties the movement spell to
the mastery, the way Bloodwake does for Abyssal.

### Rare

**Dropped: Premonition.** It was chosen for this slot, then dropped. Aetherics' rewind does the
same trick without placing a mark, and rewinding your position is time manipulation rather than
sight. Its landing notes moved to the rewind under Aetherics. The slot is open again.

**Path of Light** *(chosen)* - activate it and a path of light marks the route to the floor's
exit for about 10 seconds, with a 20 second cooldown. It does not force you toward the exit.
While you move along the path, in its direction, your speed doubles; go anywhere else and you
move normally. Divination as being shown the way, and the only movement spell that uses
the maze itself.

- **"Along the path" is cheap to test.** Build a distance-to-exit map once per floor, a
  breadth-first search outward from the exit over the maze's cells, which is how `MazeLayout`
  already measures distances. You are going the path's way whenever your distance to the exit is
  falling. The lit route is just the downhill direction from your current cell, so it follows you
  as you move for free.
- **Route over the ground graph.** Ledges cannot be climbed on foot and embrasure slots cannot be
  passed at all. The maze layout already has a ground connectivity check that treats ledges as
  blocked, and the path has to use the same rules, or it leads you straight at a wall.
- **Draw it with glowing markers, not real lights.** Forward rendering caps each surface at four
  pixel lights, which is why floors are built one cell at a time. A string of point lights down a
  corridor would fight the level's own lighting; emissive floor markers cost nothing.
- **A timed buff, not a toggle.** Held movement spells are toggles that drain mana every second.
  Path of Light runs a fixed duration from one cast, which is closer to a self-status. The boost
  switches on and off with your direction every frame, so it is a status whose bonus depends on
  where you are heading, not a flat haste.
- **The cooldown starts at the cast.** Spell cooldowns begin when cast, so 20 seconds from casting
  leaves 10 seconds off between uses, roughly half uptime.
- **Decided: double speed.** It combines with other speed sources such as Haste, and with slows
  such as frost, through the stat sheet like any other move speed modifier.

**Blessed Stride** - a sustained speed boost that is far stronger while no enemy can see you. It
turns Divine Knowledge's vision cones into movement, rewarding you for reading where enemies are
looking. Speed spells are already crowded, with Burning Feet and Bloodwake, which makes it the
weaker pick.

**Astral Projection** - leave your body and scout as an untouchable spirit, snapping back when
it ends. Very Divination, but your body is a sitting target meanwhile, it overlaps Blood
Scent's sight through walls and Aetherics' invisibility, and it needs a camera and control
handoff nothing in the player rig supports.

Rejected: angelic flight. Rapture of the Deep already owns floating.

---

## Divination: implementation notes

### Works today, or nearly

- **Underworld Vial** - `SpawnProjectileEffect` with gravity, splash radius and splash damage,
  carrying a poison payload. That is already what a grenade is.
- **Consecrate** - the friendly buffing zone Murder needs. Its second customer. Movement speed,
  damage dealt and damage taken are all existing attributes.
- **Judgement** - burn is one pool per target, so the split is arithmetic before applying it.
  One wrinkle: burn landing on a target already burning keeps the larger pool rather than
  adding the two. A thin split landing on something already burning hotter does nothing. That
  suits the spell's aim of catching one target, but the split is not simply damage divided.
- **Punish** - a zero-damage hit still applies its statuses, because `Health.TakeDamage` applies
  them before it returns on a zero amount. Two things follow. Type it as energy, not kinetic: an
  ethereal enemy bails out of a kinetic hit before statuses land, so a kinetic Punish could
  never burn one. And it still counts as a hit for executes, since `TryExecute` runs before the
  zero check, so Punish consumes a death mark and kills. Probably a feature.

### Smite needs guns to have an on-hit hook

Weapons have no on-hit effect chain at all. `Projectile.AttachOnHit` exists for spells, but
`Weapon` never uses it and hitscan has nothing. Smite needs one on both delivery kinds.

It is also a third shape of bullet infusion. Viper's Sting and Elemental Form infuse for a
duration, and Smite infuses a count of one bullet. The timed infusion noted under Bestial
should expire by time or by count, not by time alone.

### Prismatic Chains

- **The leash is new.** Nothing constrains two enemies relative to each other. It is a pull
  back together past a set length, pushed through the same external velocity knockback uses.
- **The damage transfer cannot loop, and must stay that way.** Kinetic or energy in, psychic
  out, and psychic does not trigger it. Any later change letting psychic trigger a chain makes
  two chained enemies bounce damage back and forth forever. Worth a comment where it is built.
- **Burn ticks are energy damage**, so a burning chained enemy transfers psychic damage every
  half second. Probably intended, worth knowing.
- **Decided: only kinetic and energy transfer, and every execute deals its own damage type.**
  An executed enemy reports its entire remaining health as damage through `Health.AnyDamaged`,
  under whatever type the finishing hit was - kinetic, for a frost shatter. A chain would have
  passed the whole bar to its partner. With executes on a type that is neither kinetic nor
  energy, the chain ignores them by construction.
- **Append a new member rather than reusing True.** `DamageType.True` already means something:
  it ignores resistance and invulnerability, and `Health.Drain` and `Health.Kill` use it for
  deaths with no attacker. An execute is none of those. Add the new member at the end of the
  enum, because damage types are stored as integers in assets and inserting one mid-list would
  renumber every authored value.
- **The trigger and the report are separate.** A frost shatter is still triggered by a kinetic
  hit; only the damage the execute reports changes type. The incoming-type check in `TryExecute`
  that decides whether to fire stays exactly as it is.
- **Lifesteal has the same shape.** `RunState.OnAnyDamaged` heals a fraction of every amount the
  player deals, with no type filter, so today a frost shatter from a one-damage bullet heals as
  though it dealt the enemy's whole remaining health. That may be fine, since the health really
  was removed, but with executes on their own type it becomes a one-line choice either way.
- **One enemy in range** gives it nothing to tether. It should refuse the cast rather than
  spend the mana.

### Divine Assistance

- **`Weapon` is written as the one gun.** It pushes recoil into the look controller on every
  shot, so a mirrored copy firing alongside doubles your recoil unless it skips that.
- **Decided: infinite ammo for the duration.** The phantom never draws on the holster, so your
  second gun comes back exactly as you left it. Infinite should also mean it never reloads, or a
  phantom with a small magazine still pauses mid-spell to reload nothing.
- **An empty second hand** means nothing to summon. Refuse the cast.
- **Bullet infusion has to reach the phantom too**, or Viper's Sting and Smite work on half
  your shots.
- `Holster.CanSwap` is the natural gate for not changing weapons.

### Foretell

- **Status ticks look like attacks.** `StatusController.DealTickDamage` calls the same
  `Health.TakeDamage` a real hit does, so unguarded, a burn tick consumes Foretell. Ticks are
  not currently distinguishable on `DamageInfo`, so that needs a flag.
- **A per-slot cooldown reduction.** `SpellBook.ReduceCooldowns` shortens every slot at once.
- It pairs with the attack timer rung of Divine Knowledge, which is a good sign for both.

### Divine Star

- **Homing picks its own target.** `Projectile.FindHomingTarget` scores whatever an overlap
  sphere finds, not the highest-health enemy. It needs a target handed to it.
- **A slow star in a maze flies into a wall.** Projectiles die on world geometry, and homing
  steers straight at the target. Chasing an enemy around a corner, it hits the corner. The flow
  field only leads toward the player, so there is no ready path to an arbitrary enemy. Letting
  the star pass through walls is the cheapest fix and reads fine for something divine; the
  alternatives are only picking targets in line of sight, or pathing of its own.
- "In combat with you" is `IsAlerted`, which the perception system already tracks.

### Reckoning

**Decided: nearby enemies**, within a radius. Its fear comes from the player, so it gets the
reversed flow field for free.

**Decided: fear alerts an enemy that had not noticed you.** The radius reaches unaware enemies,
and each one it touches wakes up, bolts, and comes back hunting once the fear wears off. So
Reckoning buys time rather than a clean escape, and casting it before a room has seen you gives
the room away. Since fear is faster and away, it is still a very strong disengage, which is fine
at rare.

---

## Death

Mastery: **Souls.** As you kill enemies, their souls are bound to you. They float around you,
and a counter on the screen shows how many you hold. The maximum depends on how many Death
spells you have equipped: two with one spell, rising by one for each spell after.

| Death spells equipped | Maximum souls |
|---|---|
| 1 | 2 |
| 2 | 3 |
| 3 | 4 |
| 4 | 5 |

Souls carry between floors. Souls over the cap are lost.

### Common

**Raise Dead** - pulls a long-dead body from the ground, not the corpse of an enemy, and infuses
it with a bound soul: a summoned zombie with a melee attack. There is no maximum number, and
they persist between floors. They are slow and easily killed. If you move into one it steps out
of your way and follows you, so they never block you. Costs 1 soul.

**Wither** - a grenade that weakens the enemies it hits, and makes them die if they fall below
10% health, elites included. Each level of the spell raises the threshold by 1%. Costs 1 soul.

**Bone Shards** - a charged spell: hold to empower it. Fires a flurry of bone forward, more
bones the longer it charges. Consumes all remaining souls for even more bones, but does not
require any.

### Uncommon

**Banshee Wail** - a cone that silences, applies frost and deals necrotic damage. Costs 1 soul.

**Corpse Explosion** - sends a soul bouncing between corpses, exploding each one for area
kinetic damage. Costs 1 soul.

**Desecrate** - defiles the ground. Your bullets deal extra damage, as necrotic damage, both
while you stand in it and against enemies standing in it. With you and your target both inside,
both bonuses apply, so it is strongest in close quarters.

### Rare

**Stitched Monstrosity** - summons a zombie goliath. It moves fast, and has a slam that knocks
enemies away in an area. It persists between floors, and you can have one at a time. Costs 2
souls.

**Soul Storm** - rains souls from the sky, striking random locations around you, with the area
moving as you do. Every kill while it lasts extends its duration and grants a soul. The
extension has no cap: the goal is to keep it going for the whole floor. Costs 2 souls.

### Mythic

**Apocalypse** - a cone that applies poison, fear and a plague. A plagued enemy that dies rises
as a zombie under your control and passes the plague to enemies near it. Plague zombies last a
short time and do not carry between floors.

### Movement

**Gravewalk** (common) - a quick lunge forward as a shade. You pass through enemies and your own
dead without colliding, and every enemy you pass through is weakened.

**Lich Guise** (rare) - swap places with one of your raised dead within a long range. The zombie
is left standing where you were.

### Melee

**Gravebite** (uncommon) - a jaw bursts from your hand and bites the enemy in front of you,
applying weaken.

**Weaken**, the debuff Death leans on, reduces damage dealt by a percentage and stacks.

---

## Death: Souls notes

- **It already has diminishing returns.** A flat one soul per spell on a base of two shrinks in
  relative terms: the first spell is worth two souls, the fourth adds a quarter more. That
  satisfies the rule that the first rung is worth the most, without any special tuning.
- **Decided: every enemy death counts as a player kill.** Not only deaths dealt by your side:
  kills by a confused enemy, and the environmental hazards planned for later, such as lava, flame
  jets and poison clouds, all count for souls, the Blood Debt and on-kill effects. That includes your
  zombies, whose kills bind the souls that raise more zombies. See the first implementation note
  below.
  - **Lifesteal is the exception.** It heals from damage you deal, not from kills, and
    environmental or confused damage never counts toward it.
  - **The change is small.** `RunState.OnAnyDied` currently skips any death whose killing blow
    did not come from the player's team. That check goes. The lifesteal handler keeps its own.
  - **Lifesteal counts minion damage today.** `RunState.OnAnyDamaged` also decides by team, so a
    companion's bites and zombies' claws heal you. The delivery field planned under Psionic can
    narrow it to your own hits, if that is wanted.
  - **Kills anywhere count.** A confused enemy or a lava pit killing something in a room you have
    not reached yet still binds a soul to you.
- **Kills at the cap are wasted.** Once you are full, further kills bind nothing. That makes the
  cap a pressure to spend rather than hoard, and the spell list has cheap one-soul spenders for
  exactly that.
- **Decided: souls carry between floors.** A full counter at the end of a floor is banked, not
  wasted.
- **Decided: souls over the cap are lost.** When a swap floor lowers the cap below what you
  hold, the excess disappears. It deserves a visible cue as they go, or the counter just drops.
- **Soul costs are refused, not clamped.** Same rule as the Blood Debt: a spell that costs a soul
  cannot be cast with none. Bone Shards is the exception by design, spending whatever you have.
- **It contrasts with the Blood Debt as intended.** Souls are a currency harvested from kills and
  spent; Blood Debt is a liability taken on and cleared by kills. Both reward killing, from opposite
  directions.
- **Cheap to build.** Orbiting souls are code-generated spheres following the player, the
  counter is a line in `HudUI`, and `Health.AnyDied` is already the hook `RunState` uses to
  count kills.

---

## Death: choices made

Brainstormed as three names, then fleshed out into two versions each.

- **Gravewalk** is the lunge. Dropped: a dash leaving a skeletal hand to snare pursuers.
- **Lich Guise** is the zombie swap, first pitched as Shed the Flesh. Dropped: a skeleton form,
  fast and immune to slows but taking double damage.
- **Gravebite** bites from your hand. It was first pitched as jaws bursting from the ground
  under the target, binding two souls on a kill; the soul bonus is not part of the chosen
  version. Dropped: a bite that heals you.
- **Apocalypse** spreads its plague, with fear added to the cone. Dropped: every zombie you
  control growing faster and stronger while it lasts.

---

## Death: implementation notes

### Enemies attack minions

**Decided: enemies attack minions,** not only the player. This applies to everything fighting
for you: Raise Dead's zombies, the Stitched Monstrosity, plague zombies and Bestial's companion.

It settles the runaway horde. Zombies persist and their kills bind the souls that raise more of
them, so with nothing attacking them the army would have funded itself and grown all run. Now a
slow, fragile horde is a wall enemies chew through, and the loop keeps a real cost.

**Decided: minions physically block enemies, but never the player.** A horde is a wall of bodies
as well as of targets. Today it would not be: the collision matrix makes familiars ignore enemies,
and each other, so enemies would walk straight through zombies and zombies would stack on top of
one another.

- **Walking minions need their own layer.** It collides with enemies and with other minions, and
  still ignores the player, which keeps Raise Dead's promise that they never block you. Flying
  familiars can stay on the Familiar layer.
- **The new layer has to join the enemy masks.** Enemy bullets, blasts and area attacks reach
  familiars because `Layers.EnemyHitMask` and `Layers.EnemyTargetMask` both name the Familiar
  layer. A new minion layer must be added to both, or enemies will choose zombies as targets and
  then be unable to hit them.
- **A blocked doorway stays blocked.** A horde filling a narrow doorway leaves enemies pressing
  against it and attacking the nearest zombie, which is the intended wall. Elites favour you, but
  with the doorway blocked you are out of their reach, so they chew through the zombies too.

**This reverses a deliberate rule.** `Layers.EnemyTargetMask` carries a comment saying familiars
are collateral rather than a distraction, so that "a pet cannot be used to pull aggro off
yourself". Pulling aggro with minions is now intended. That comment has to change when this is
built, or the next person to read it will put the old rule back.

**Half of it already works.** Enemy bullets, blasts and beams already hit the Familiar layer, and
enemy area abilities already count familiars as valid targets. Damage delivery needs nothing.
Only target selection changes.

- **Target selection.** `EnemyController.AcquireTarget` picks the player and nothing else. It
  needs to choose among the player and nearby minions. It already reruns on a one-second timer,
  so an overlap query there is cheap.
- **Chasing a minion has no path.** The flow field is one search outward from the player, so it
  can lead an enemy to you but not to a zombie somewhere else. Minions usually stay near you,
  since they follow, so the workable rule is: fight a minion that is close or in sight, and
  otherwise hunt the player through the flow field as now.
- **Decided: ordinary enemies attack the nearest hostile.** A big enough horde soaks their
  attacks, and a soul per zombie is the price of that.
  - **Keep a little stickiness.** Retargeting every second on pure distance makes an enemy
    standing between two equally close targets flip back and forth. Switch only when the new
    target is meaningfully closer than the current one.
- **Decided: elites favour the player.** An elite goes for you whenever it can see you or reach
  you through the flow field, and turns on a minion only when you are out of reach. So a horde
  shields you from the rank and file but not from the dangerous ones. The flag is already there:
  `Health.IsElite` is set from the enemy definition at spawn.
- **Decided: seeing a minion alerts an unaware enemy.** Perception currently only looks for the
  player, so the sight check needs to consider minions too.
  - **Check at the retarget interval, not every frame.** An unaware enemy testing its sight cone
    against a large horde every frame adds up. The same once-a-second overlap query used for
    targeting can find minions within sight range, then run the cone and line of sight on those.
  - **A horde gives away your approach.** Zombies following you into a sleeping room wake it
    before you arrive. That puts a Death horde naturally at odds with approaching unseen, which
    is Divination's vision rung and Aetherics' invisibility. Probably right, since the two
    builds should feel different, but worth knowing.
- **`Target` becomes any hostile, not the player.** The perceived-position work planned for
  Blind and invisibility should be written against whatever the enemy is targeting, not against
  the player specifically.

### Walking minions cannot use the familiar system

Familiars move by writing their position directly and deliberately ignore the level, so they
drift through walls, as flying things should. A zombie walks. Following you through a maze needs
a physical body and pathfinding, which is exactly what enemies have: a character controller and
the flow field. The flow field already leads to the player, which is what a follower wants. So
zombies and the Stitched Monstrosity are enemy movement on the player's team, not familiars. The
Bestial beasts have the same problem, noted under Bestial.

- **"Never blocks you" is best built as no collision at all** between you and your own dead,
  rather than as a dodge. A narrow doorway leaves nowhere to step aside, and a horde following you
  into a corridor you are backing out of would otherwise trap you. The existing Familiar layer
  already ignores the player, but it also ignores enemies, and minions are meant to block those.
  So walking minions need a layer of their own; see the note on blocking above.
- **Count grows all run.** Each zombie is an AI with a character controller and flow field reads.
  Separation is a local overlap query, so cost grows roughly in line with the count, but with no
  cap it is worth a performance check at fifty or a hundred zombies.
- **Persistence already has a pattern.** Familiars respawn on room entry from an ownership record
  on `RunState`. Zombies can store a surviving count and respawn that many around you on arrival,
  placed with `NavField.TryFindSpot`. The Stitched Monstrosity is one more slot in the same
  record.

### Corpses, again

Corpse Explosion needs corpses, and enemies are destroyed when they die. The fix dropped earlier
comes back: record where enemies fell, from `Health.AnyDied`, and expire each record after a few
seconds. The bounce is `ChainEffect`, the chain lightning retargeting loop, pointed at recorded
positions instead of live enemies. It costs a soul, decided.

**Option: your own dead count as corpses.** Exploding your zombies is the classic necromancer
move and means the spell always has something to bounce between. With Raise Dead costing a soul,
a detonated zombie has cost two, which is a fair price for a guaranteed target and gives a
self-funding horde somewhere to go.

### Wither

- **Decided: it finishes elites.** Wither is the first execute that ignores the elite rule
  deathmark and full frost follow. That rule is written down in `Health` and checked by the debuff
  verifier, so the exception deserves a comment where Wither is built. It still never fires on
  the player.
- **Decided: each level raises the threshold by one percentage point.** Spell levels currently
  scale effects through a single level multiplier applied to power, radius and duration. A
  threshold rising by a flat point per level is additive, so it needs its own per-level field
  rather than riding the shared multiplier.
- **The kill must key off Wither, not off Weaken.** Wither applies weaken, but so do Gravewalk
  and Gravebite. If the threshold kill checked for the weaken status, every lunge and bite would
  quietly inherit Wither's execute. Wither needs its own marker for the kill, alongside the weaken
  it applies.
- It deals the execute damage type, so Prismatic Chains and lifesteal treat it like every other
  execute.

### Bone Shards is the first charged spell

Spells cast on key press, and held spells are toggles. Hold to charge and release to fire is a
new input mode for the spell slots. `SpawnProjectileEffect` already has a count and a spread, so
more bones is just a larger number.

Consuming every soul on each cast means a quick tap to finish one enemy empties a full counter.
Consider spending souls only at full charge.

### Desecrate

- **Decided: both.** Your bullets gain necrotic damage while you stand in it, and against enemies
  standing in it, and both apply when you and your target are inside. Each hit checks two things:
  where the shooter is and where the target is.
- It still differs from Consecrate, which only buffs you where you stand. Desecrate also punishes
  what stands on it.
- **It needs guns to have an on-hit hook,** same as Smite.
- **The bonus must be a separate damage instance,** not a change to the bullet's type. Ethereal
  enemies throw away a kinetic hit entirely before anything else is considered, so a combined hit
  would lose its necrotic part too. Done as a separate instance, Desecrate is a gun user's answer
  to ethereal enemies, which take double from necrotic.

### Stitched Monstrosity

- **Decided: it carries between floors, one at a time.**
- **Open: recasting with one already out.** Casting again could refuse and keep your two souls,
  or dismiss the old one and summon a fresh goliath at full health. The second makes recasting a
  paid heal.
- The targeting and walking body notes above apply.

### Soul Storm

- **Decided: no cap on the extension.** The spell is a challenge: keep killing and it lasts the
  floor. That suits the maze. The corridors between fights are where it runs dry, so it rewards
  pushing from room to room rather than clearing a room and resting.
- **Assumed: it ends when you change floors,** since the goal is the whole floor rather than the
  whole run.
- The moving area is a lingering zone that follows the player.
- Its kills grant souls, and at the cap those are wasted. A full counter mid-storm is a reason to
  spend, which feeds Raise Dead.

### Banshee Wail

Silence's fourth customer, after Drown, Shapeshift and the original planned list.

### Gravewalk

Passing through enemies means switching off collision between you and enemies for the lunge.
Ending the lunge inside an enemy is the same landing problem Aetherics' rewind has, so the lunge should
end clear of bodies, or push them aside on arrival.

### Lich Guise

- **Pick the zombie nearest your crosshair** within range. With none in range, refuse the cast.
- **Landing is safe by construction,** since a zombie was just standing on the spot.
- `PlayerMotor.Teleport` already exists and already ends a wall zip.
- **Open: do the Stitched Monstrosity and plague zombies count as swap targets?**

### Gravebite

A short-range strike from the player is how the existing melee spells are already built, so the
jaw from your hand is simpler than the ground-burst version was.

### Apocalypse

- **Decided: the plague spreads, and plague zombies are short-lived and stay on their floor.**
  The worry about a room becoming permanent zombies is settled.
- **The cone's fear comes from the player,** so it gets the reversed flow field for free. Feared
  enemies cannot attack, so Apocalypse is also a disengage.
- **Fear carries the plague away from you.** Feared enemies flee, so they tend to die, rise and
  spread plague out of sight, sometimes into enemies that never noticed you.
- **Only the cone should fear.** Fear alerts unaware enemies. If plague passed on by a death also
  applied fear, one cone could chain alerts across the floor. As specified only the cone fears,
  and it is worth keeping that way deliberately.

### Weaken

Already built as `WeakenStatus`: 15% less damage dealt per stack, up to three stacks, so 45% at
most. If Death wants deeper stacking, the cap is one number.

It already works on enemies. Enemy attacks scale their damage through `Combat.OutgoingMultiplier`,
which reads damage dealt from the attacker's stat sheet, so a weakened enemy really does hit
softer with no further work.

---

## Psionic

Mastery: **Psi Blades.** Dealing bullet damage charges your psi bar, and melee attacks spend the
charge to deal bonus psychic damage.

- **Charging.** Each bullet hit gives a quarter of a charge, at most once every quarter second, so
  a rapid-fire gun builds one full charge a second.
- **Spending.** A melee attack spends one charge.
- **Maximum.** Two charges with one Psionic spell equipped, plus one for each spell after. Only
  the maximum grows with spell count; the charge rate stays fixed for now and may need adjusting
  later.

| Psionic spells equipped | Maximum charge |
|---|---|
| 1 | 2 |
| 2 | 3 |
| 3 | 4 |
| 4 | 5 |

**Confusion** is a new debuff. A confused enemy cannot tell friend from foe and attacks the
nearest enemy. Taking damage removes it.

### Common

**Mind Spike** - a hitscan ranged attack. It spends psi charge the way melee attacks do for bonus
damage, but does not need any charge to fire.

**Phantasmal Mimic** - creates an immobile illusion of you that shoots enemies with the gun you
are currently holding. It uses none of your resources and does not benefit from your buffs. Its
hits give bonus psi charge, outside your quarter-second limit.

**Telekinesis** - pushes an enemy. If it hits a wall or another enemy it takes damage. An enemy
it hits takes damage too and is pushed on, less far, so the damage can cascade through a crowd.

### Uncommon

**Ego Fracture** - a grenade that stuns everything in an area.

**Brain Fog** - a projectile that applies confusion.

**Intrusive Thoughts** - psychic damage over time.

### Rare

**Superego Death** - throws a projectile straight forward. The first enemy it hits becomes two
copies of itself, as though the original stopped existing. Each copy has half the original's
maximum health and is silenced and disarmed for a moment. An enemy can only be split once, and
elites can be split. Striking a split enemy generates additional psi charge.

**Superid** - become id incarnate. For a while your weapon fires on its own at the enemy nearest
your reticle, and your shots do not miss. You never need to reload, you move faster, you take
reduced damage, and you can still melee.

### Mythic

**Assume Identity** - mind control an enemy directly. Your body leaves the corporeal realm: it is
immune to damage and hidden from enemies, who attack the body you now control instead. Lasts 10
seconds, or until the controlled body takes damage. When it ends you return to your real body,
with the camera sweeping across the map back to it. Elites can be controlled, but the spell does
not work when only one enemy is left.

### Movement

**Repulse** (common) - a dash in the reverse direction that also inverts any forces on you. It
should feel like snapping onto a new heading and carrying on as if nothing happened.

**Force of Will** (rare) - a dash like the default dash. If you have a psi charge, it spends one
to reflect every nearby enemy projectile.

### Melee

**Slice** (uncommon) - a vertical strike with longer reach than a normal melee attack. Small
damage, 2 mana, and a very short cooldown.

---

## Knockback impacts, game-wide

**Decided: anything knocked into something hard takes damage,** from any source of knockback, not
only Telekinesis.

- **Into another enemy:** both take damage, and the struck enemy is moved too, less far, so
  impacts can cascade through a crowd.
- **Into a wall:** the knocked enemy takes damage.
- **Into your minions:** the minions take damage and are pushed as well. Minions physically block
  enemies, so a knocked enemy really does hit them.

This makes every knockback in the design stronger. Gush, Storm Blast, Star Comet, Tentacle, the
Stitched Monstrosity's slam and Fathomless Gate's pull all gain collision damage.

- **Enemies really do collide with each other.** The collision matrix leaves Enemy against Enemy
  on, so a knocked enemy physically strikes the next one.
- **Detecting the impact is the new part.** Knockback plays out through the enemy's movement, and
  the character controller reports what it bumps on every move, so the collision is available
  while the push is underway.
- **Measure closing speed, not speed.** Storm Blast shoves a whole crowd forward together. Against
  absolute speed, every enemy in that crowd is fast and touching its neighbours, and the crowd
  grinds itself to death. What should hurt is how fast the two bodies close on each other. A wall
  does not move, so for walls the two are the same.
- **Cascades end on their own,** as long as each transfer moves the next body less and impacts
  below a speed threshold do nothing. The same threshold stops enemies hurting each other, or
  themselves on walls, just by walking into things.
- **Friendly fire.** Collision damage between two enemies has to be issued from the player's side,
  carrying whoever caused the knockback, or `Health.TakeDamage` refuses it as friendly fire.
- **Wall damage makes the maze a weapon.** Corridors are narrow and walls are everywhere, so almost
  every knockback ends against something, and knockback spells will feel much stronger than their
  numbers suggest. **Planned for later:** rooms get somewhat bigger, which softens this and also
  gives more space to move. Not urgent.
- **Pushing minions works like pushing enemies.** Walking minions have character controllers, so a
  push applies the same way.
- **Open: the player.** Enemy attacks knock the player around too. Presumably the player takes no
  collision damage, in line with never being executed by a status, but it is worth stating.
- Flying enemies still only move sideways, since hovering overwrites vertical velocity every frame.

---

## Psionic: movement choices

Both movement spells are decided, and both replaced the first pitches.

- **Repulse** was first pitched as pushing off the enemy you aim at. It is now a reversed dash that
  inverts your momentum.
- **Force of Will** was first pitched as a sprint that shoves enemies aside. It is now the default
  dash, plus a psi-powered projectile reflect.
- **Dropped:** Displace, swapping places with an enemy, since Lich Guise owns swapping. Forget Your
  Place, which was invisibility by another name. Telekinetic levitation, since Rapture of the Deep
  owns floating.

---

## Psionic: implementation notes

### Confusion cannot hurt allies under the current rules

Three separate things stop a confused enemy damaging its own side:

- **Friendly fire is refused in `Health.TakeDamage`,** which discards any hit whose source team
  matches the victim's.
- **Enemy shots cannot find enemies.** Projectiles and hitscan use `Layers.HitMaskFor`, and the
  enemy version leaves the Enemy layer out.
- **Enemy area attacks cannot find enemies either.** `Layers.TargetMaskFor` gives enemy abilities
  only the player and familiars.

The fix is for a confused enemy's attacks to go out as the neutral team, with masks that include
everyone. The friendly fire check only skips hits from the victim's own team, so a neutral source
hurts anyone. That also means it can hit the player, which suits not telling friend from foe.

**Decided: kills by a confused enemy count as yours,** as every enemy death now does. Lifesteal
does not trigger from them.

### Taking damage removes confusion, including the hit that applies it

`Health.TakeDamage` applies a hit's statuses before it subtracts the damage. If confusion clears
whenever damage lands, Brain Fog's projectile confuses its target and cures it in the same hit.
Either Brain Fog deals no damage, or the removal ignores the hit that applied the confusion.

It also clears on things that may not be intended:

- **Damage over time.** Status ticks go through the same damage path, so a burn, bleed or
  Intrusive Thoughts tick cures confusion on the first tick. As written, Brain Fog and Intrusive
  Thoughts, two uncommons in the same school, cancel each other. Clearing only on direct hits
  would fix that.
- **Your own minions.** A zombie or companion hitting a confused enemy cures it.
- **Other confused enemies.** Probably fine: the brawl ends itself.

### Psi Blades

**Decided: the numbers.** A quarter charge per bullet hit, at most once every quarter second, so
one charge a second at most. One charge per melee attack. Two maximum at one spell, plus one per
spell after.

**Decided: spell count raises only the maximum,** not the charge rate, for now. It may need
adjusting later.

- **It needs to know a hit came from your gun.** `DamageInfo` records a team and a source object,
  but not how the damage was delivered. Everything on the player's team shares that team, so
  companion bites, zombie claws, zones and burn ticks would all look like your bullets. It needs a
  delivery field on the damage record: gun, spell, melee, minion, or status tick.
- **Build that field once, early.** Foretell needs it to ignore status ticks, confusion and Assume
  Identity need it for the same reason, and lifesteal can use it to count only your own hits.
- **The rate cap makes it gun-agnostic.** Anything firing four or more hits a second charges at
  the cap, and a shotgun's pellets count as one hit per window. Only slow guns charge more slowly.
- **Melee drains faster than guns fill.** A full two-charge bar takes two seconds of steady fire,
  and a melee attack spends one charge. Slice, with its very short cooldown, empties the bar in
  two swings and is then an ordinary strike until the bar refills. That keeps charge a burst
  rather than a constant bonus, which seems right.
- **Decided: the mimic's charge is a bonus.** Its hits charge outside your quarter-second limit, so
  it is a real second source of psi while you shoot.
- **Psi is spent by three things now:** melee attacks, Mind Spike, and Force of Will's reflect.
- **Melee attacks means the melee slot.** Melee attacks have been spells in the melee slot since
  the rework, so the bonus applies to whatever melee spell is equipped, from any school.
- **It shares the Souls curve.** Two at one spell, plus one per spell after. Consistent across the
  two resource masteries, which makes both easy to read.

### Mind Spike

- **Decided: it does not need charge.** It spends one charge for bonus psychic damage when there
  is some, the same as a melee attack.
- **Spells have no hitscan.** Guns do; spells fire projectiles, beams and cones. A single instant
  ray is a small new effect, close to one tick of `BeamEffect`.

### Phantasmal Mimic

- **Decided: it fires the gun you are holding, uses none of your resources, and gets none of your
  buffs.** No ammo, no mana. It uses the gun's own stats, not your gun damage or damage dealt, and
  none of your bullet infusions such as Smite or Viper's Sting.
- **Decided: its charge is bonus charging,** separate from your quarter-second limit.
- **It follows your swaps.** "The gun you are currently holding" means switching weapons switches
  the illusion's too.
- **It never reloads,** since it has no magazine to empty.
- **It is a minion,** so the decisions under Death apply. Enemies attack the nearest hostile, so
  it is a decoy, and enemies that see it are alerted. It also blocks enemies physically, so it
  needs the walking-minion layer even though it never moves.
- **Its hits need the delivery field** to be recognised as the mimic's, both to count for charge
  and to skip your limit.

### Telekinesis

Covered under Knockback impacts, since every knockback now damages on hitting an enemy, a wall or
a minion.

### Superego Death

**Decided: it should feel like two copies of an original that stopped existing. How it works
underneath is whatever is most manageable.**

**Recommended: keep the original and dress it as one of the copies.** Halve the original's maximum
health, spawn one copy beside it with matching maximum and current health, then play the same
split effect on both and push them apart sideways. Neither reads as the survivor, so to the player
it is two copies of something that is gone. Underneath, one of them is the original.

Why this is the more manageable version:

- **Nothing is left pointing at a deleted enemy.** Plenty of things hold a reference to one specific
  enemy: a homing projectile's target, whatever an enemy or minion is currently targeting, the
  source recorded on statuses it applied, a Prismatic Chains tether. Deleting the original means
  every one of those has to cope with it vanishing mid-use. Keeping it means none of them do.
- **No second way for enemies to leave the game.** The only way an enemy leaves today is dying,
  which fires `Health.AnyDied` and counts as a kill. Deleting the original cleanly needs a new
  removal that skips all of that, which is one more place for kill counting and cleanup to go
  wrong.
- **Half the construction cost.** Enemies are built from code when they spawn, so one new enemy is
  cheaper than two plus a removal. A small saving, but a free one.
- **The original's state carries over for nothing:** its elite flag, alertness, target and statuses.

What the copy needs:

- **Its health set straight after it spawns.** `EnemyFactory.Spawn` builds an enemy at full
  definition health, so its maximum and current health are set to match the halved original.
  Enemies get their maximum from their stat sheet, and lowering the original's makes `Health`
  clamp its current health down for free.
- **The original's statuses copied across,** so both halves look and behave like the same enemy.
- **Open: copy the death mark too?** Copying every status is the most faithful to "two copies", but
  a death mark on both halves turns one mark into two executes. Copying everything except the death
  mark is the safer default.
- **The same alertness and target,** so the copy does not stand idle while its twin fights.
- **The elite flag,** which `EnemyFactory.Spawn` already accepts.
- **Registration as a live enemy** for anything that tracks whether a room or floor is clear.

Still as before:

- **Decided: only once.** Both halves are marked as split, so neither can be split again.
- **Decided: works on elites.** A split elite becomes two half-health elites.
- **Splitting never heals,** because the copy takes the original's current health after halving.
- **One cast adds exactly one enemy,** so with every death counting as yours it is worth one extra
  kill, not a farm.
- **Both halves are silenced and disarmed** for a moment, and both count as split enemies for the
  bonus charge.
- **Silence and disarm on enemies need a definition.** Enemies have no guns and no spells, only
  attacks. Disarm could mean ranged attacks and silence ability attacks, or both could simply mean
  unable to attack briefly. Worth defining once for every enemy use of silence and disarm.
- **Open: does striking a split enemy give bonus charge outside the limit,** like the mimic? If not,
  its extra charge adds nothing when you are already charging at the cap.

### Superid

**Decided: auto-aim that fires on its own.** Your weapon fires by itself at the enemy nearest your
reticle and never misses. It keeps the first version's faster movement, reduced damage taken and
no reloading, and you can still melee. The first version's firing at every enemy at once, and its
worse spread, are gone.

- **You steer it with the camera.** Nearest the reticle means nearest by angle from where you aim,
  among enemies in line of sight, not nearest by distance, which could be behind you. Looking
  toward a threat switches to it.
- **Open: what happens with no target in sight?** Firing on its own at nothing wastes ammo it does
  not need but, more importantly, makes noise. Holding fire until an enemy is in sight is the
  sensible default.
- **It is loud.** Continuous automatic fire at full weapon noise travels around walls to every
  enemy in hearing range, so Superid wakes much of the floor.
- **Fire rate comes from the gun.** A semi-automatic gun fires at its fastest possible rate, which
  is a much bigger boost for slow-trigger guns than for automatics.
- **"Does not miss" depends on the gun.** Hitscan guns simply hit the target. Projectile guns such
  as the rocket launcher need their shots to steer onto the target. Spread guns such as the
  shotgun land every pellet, which makes Superid worth far more on a shotgun than on a rifle.
- **No reloading is Divine Assistance's infinite magazine again.** Build it once.
- **No swapping while it lasts,** presumably, through the same holster gate Divine Assistance uses.
- **The charge cap still applies,** so guaranteed hits cannot fill the psi bar faster than a
  quarter charge every quarter second.
- Faster movement and reduced damage taken are existing attributes.

### Assume Identity

The largest item in Psionic. Its hardest part is shared with Bestial's Shapeshift: both replace the
player's controls with a different body's. Build that player state once.

**Decided:**

- Your real body is immune to damage and hidden from enemies, who attack the body you control.
- Control ends after 10 seconds, or when the controlled body takes damage.
- You return to your real body, with the camera sweeping across the map to it.
- Elites can be controlled.
- The spell does not work when only one enemy is left.

Notes:

- **"The player" has to mean whichever body you are in.** Enemy targeting, sight checks and the
  flow field all look for the player. The flow field is built outward from the player's position,
  so while you control an enemy it has to be built from the controlled body, or enemies path toward
  your hidden real body. Elites favouring the player should favour the controlled body.
- **Hidden means gone from every enemy check,** sight and hearing included, not only targeting.
  Otherwise an enemy that wanders into your real body notices it.
- **Damage over time ends it immediately.** Status ticks go through the same damage path as hits.
  Take control of an enemy you have been burning, bleeding or hitting with Intrusive Thoughts, and
  the first tick ends control. Same fix as confusion: only direct hits count.
- **It lasts longest out of combat.** Enemies attack the body you control, so walking it into a
  group ends control within moments. The full 10 seconds mostly happens while scouting or setting
  up.
- **The camera sweep and walls.** A straight flight from the controlled enemy to your body passes
  through walls, and the roof is one solid slab, so there is no clear view from above. A fast
  straight flight through geometry, with a blur or fade, reads as astral travel and needs no
  pathing. Lock your controls during it, and keep your body immune until the camera arrives.
- **Open: "only one enemy left" where?** On the whole floor, or within some range of you? The whole
  floor is simplest to count. Superego Death's copies count as enemies; your minions do not.
- **Open: what happens to the enemy afterwards?** It returns to its side, dies, or stays confused
  for a moment.
- **Controls.** Enemies attack through a list of ability attacks with priorities and cooldowns, not
  a trigger. Playing one means mapping its attacks to buttons, and some enemies have more than two.
- **Movement varies wildly.** Hounds run, Gazers hover, and an immobile enemy cannot move at all.
  Controlling a Gazer means flight controls.
- **The camera sits at the enemy's eyes,** which range from a hound's height to a hovering Gazer's.
- **Teams.** While controlled, the enemy is on your side, so it has to change team and layer for
  friendly fire, masks and enemy targeting to treat it as yours. Confusion already needs attacks
  that ignore team, so the two share that work.
- Casting your own spells while in control is presumably blocked, which is silence again.

### Repulse

**Decided: a dash in the reverse direction that also inverts any forces on you,** so it feels like
snapping onto a new heading and carrying on as if nothing happened.

- **Inverting needs no new motor code.** `PlayerMotor` keeps its velocity private behind a read-only
  `Velocity`, and `AddImpulse` adds to it. Adding minus twice the current velocity flips it exactly,
  and the dash then adds on top.
- **Knockback counts as a force.** An enemy slam that throws you backwards sends you back toward the
  enemy when you invert it. A strong counter, and very much the snap-and-carry-on feel.
- **Open: reverse of what, when standing still?** Moving, it reverses your movement. Standing still
  there is nothing to reverse, so it needs a fallback, most naturally backwards from where you face.
- **Open: vertical too?** Inverting all velocity turns a fall into a rise and a jump into a dive.
  Horizontal only is calmer; full inversion is more of a trick.
- **Open: does the camera turn?** "Snapping in a different direction" could mean movement only,
  with your view unchanged. Turning the camera around would be disorienting.
- **Open: does it keep Dash's sliver of invulnerability?**
- **Spider Legs.** Velocity is stored in world space even while wall-walking, so inverting it still
  works, but the standing-still fallback should use the wall-walking frame.

### Force of Will

**Decided: the default dash, plus a psi-powered reflect.** With at least one psi charge, it spends
one to reflect every nearby enemy projectile.

- **Projectiles cannot be found by an area search today.** They move by spherecast, are built with
  no collider, and nothing keeps a list of the live ones. The reflect needs a small registry of live
  projectiles, added on launch and removed on destroy.
- **Reflecting flips a projectile's side, not just its direction.** Its team, hit mask and layer are
  all set once, at launch, from the owning team. Reflection has to set the team to the player's,
  recompute the mask and layer, and clear its record of what it has already pierced. Otherwise it
  passes through enemies and hits the player who reflected it.
- **It keeps the enemy's damage.** A projectile's damage is fixed when it is fired, from the enemy's
  stats, so a reflected shot hits for what the enemy would have dealt you. Its statuses come along
  too, so a frostcaller's bolt applies frost to enemies.
- **Homing shots retarget for free.** Homing picks its targets by the owning team, so a reflected
  homing projectile turns to chase enemies once its team flips.
- **Only projectiles.** Hitscan enemy attacks, beams and telegraphed blasts have nothing in flight to
  reflect.
- **Open: where do reflected shots go?** Straight back along their path, back toward whoever fired
  them, or toward where you aim.
- **Open: spend the charge with nothing to reflect?** Spending a charge on an empty reflect punishes
  dashing just to move. Spending it only when at least one projectile is in range is kinder.

### Slice

- **Decided: 2 mana.** That also clears the spell checker, which reports any spell costing no mana
  as a mistake.
- **A vertical strike needs a new shape.** The melee selector, `Combat.ConeTargets`, is a round cone
  measured by angle from where you aim, so it is as wide as it is tall. A vertical slash wants a
  narrow, tall box.

### Ego Fracture

A stun, so it uses the planned combined snare and stun status, alongside Maul. By the pattern so
far, elites get a shorter stun rather than none.

### Intrusive Thoughts

- **No psychic damage over time exists.** Burn deals energy and bleed is its own thing. It needs a
  new status, or a general damage-over-time status that takes a damage type.
- It cures Brain Fog's confusion on its first tick unless ticks are excluded; see above.
- Psychic damage does not pass through Prismatic Chains, so there is no loop.

---

## Aetherics

**Theme: reality and time warping, with no summons.** Summons are already heavy in Bestial, Death,
Psionic and Abyssal, so the golems and turrets from the first Aetherics list are gone. The theme
mixes storage, phasing, space and time.

One sentence keeps it together: **Aetherics takes things out of the normal flow of reality and puts
them back somewhere else, or somewhen else.**

Mastery: **Arcane Warp.** Your attack speed and reload speed rise for every point of mana missing
from your pool. One Aetherics spell gives 0.5% per missing point, and each extra spell adds 0.25%,
up to 1.25% per point at four spells. Your own gun's hits restore mana, paced by the gun's base
fire rate, so that hitting with every attack restores roughly 10 mana a second whatever the gun.
Spending mana makes you faster, and attacking fills the pool back up.

Aetherics spells cost more mana than other schools' spells. That is the whole of their relationship
with the pool; they do not otherwise interact with it.

### Common

**Banish** - a grenade that removes the enemies caught in its splash from reality. The total
banished time is 12 seconds, split evenly: one enemy is gone for 12 seconds, two for 6 each, three
for 4 each, and so on. Elites have 50% resistance, so they are banished for half their share.

**Reality Shards** - casting spends the mana and begins generating warped spheres of reality, which
appear slowly, follow you, and fire at nearby enemies at semi-random intervals. The number of
spheres depends on the spell's level. The damage arrives as a delayed reward for the mana.

**Nether Wall** - creates a wall of nether across your path, just in front of you. It stops
hitscan shots and projectiles from both sides, but enemies and players walk through it.

### Uncommon

**Invisibility** - a toggle that makes you invisible and drains mana while it lasts. Shooting or
casting a spell breaks it.

**Collapse Space** - a grenade that deals energy damage and pulls enemies into its centre.

**Flicker** - vanish from reality for less than a second. You can move slowly and cast spells, but
not shoot. Removes one random debuff.

### Rare

**Nether Smoke** - a grenade that creates a cloud of smoke. The smoke deals damage over time to
enemies inside it and blinds them. You cannot see into it, but shooting into it is guaranteed to hit
a random enemy inside.

**Echo** - for a short time, every action you take happens twice, the copy following after a small
delay. That covers shooting and casting, including movement and melee spells. The copies use your
original aim, spend no ammo and cost no extra mana.

### Mythic

**Stop Time** - freeze everything but you for 6 seconds. Enemies stop mid-stride and mid-attack,
their projectiles hang in the air, and their statuses stop ticking. Your own shots leave the gun and
hang there too, then all land at once when time resumes.

### Movement

**Blink** (common) - as it is today, except it can pass through walls, other than the walls on the
outer edge of the map.

**Rewind** (rare) - snap back along your path to where you were three seconds ago, with your health
reset to exactly what it was then.

### Melee

**Space Hammer** (uncommon) - pulls a hammer from the void for one big swing that deals large
kinetic damage, with a very long cooldown.

---

## Aetherics: implementation notes

### Arcane Warp

- **Decided: a bonus per missing mana point, scaled by spell count.** It is always based on what is
  missing from the pool right now, not on mana spent over time.

| Aetherics spells equipped | Bonus per missing mana point | Empty 100-mana pool |
|---|---|---|
| 1 | 0.5% | +50% |
| 2 | 0.75% | +75% |
| 3 | 1% | +100% |
| 4 | 1.25% | +125% |

- **It follows the rule that the first rung is worth the most.** The first spell grants 0.5%; each
  one after adds half that.
- **Decided: per missing point, not per share of the pool.** A bigger pool is meant to make Arcane
  Warp better. Anything that raises maximum mana raises the ceiling: a 200-mana pool emptied at four
  spells gives +250%.
- **It is stateless and cheap.** `Mana` already exposes its current and maximum values and fires a
  `Changed` event. The bonus is the missing points times the per-spell rate, applied to the existing
  attack speed and reload speed attributes, and updated whenever the event fires.
- **Decided: mana per hit follows fire rate, about 10 a second.** Weapons set their pace per trigger
  pull, through `SecondsBetweenShots` on the definition, and a single pull can fire several rounds
  through pellets or bursts. A pull is worth 10 mana times the seconds between pulls, shared across
  its rounds, so each round that hits restores its share. A shotgun that lands half its pellets
  restores half. Fast and slow guns come out the same, so no separate rate cap is needed.
- **Semi-automatic guns fired below their maximum rate restore less,** since the pace is the gun's
  fastest, not how fast you actually pull the trigger. That seems fair.
- **Decided: paced by the base fire rate.** Each hit restores the share worked out from the gun's
  unboosted rate. The attack speed bonus makes you fire faster, so while the bonus is up you restore
  more than 10 a second, and the emptier the pool the faster it refills. The mastery pulls you back
  toward a full pool, and toward slow, on its own.
- **Hits need the delivery field on damage,** the same one Psi Blades needs, to know a hit came from
  your gun.
- **Decided: only your own gun's hits restore mana.** Echo's copied shots, Reality Shards and melee
  attacks do not. Psionic's Phantasmal Mimic, in a mixed build, falls outside that too, since its
  shots are not yours.
- **Decided: Aetherics spells cost more mana than other schools'.** They do not otherwise interact
  with the pool. The high costs feed the mastery directly: the school's own expense is what makes
  you fast.
- **It balances itself.** Spending empties the pool and speeds you up; attacking refills it and slows
  you down again. Mana regeneration also works against the bonus, drifting you back toward full and
  slow, which pushes the player to keep spending.

### Banish

- **Decided: 12 seconds shared evenly** between everyone caught in the splash.
- **Decided: elites have 50% resistance,** so an elite is banished for half its share. The half it
  resists is simply lost rather than handed to the other enemies caught.
- **Hide, never delete.** For the same reasons given under Superego Death, removing an enemy outright
  would leave homing shots, targets, status sources and Prismatic Chains tethers pointing at nothing.
  Switch off its body, collisions and behaviour, drop it from every enemy and minion check, and
  restore it exactly as it was.
- **Out of reality means out of time.** Its statuses should pause, neither ticking nor wearing off,
  and so should its attack cooldowns. A burning enemy comes back still burning.
- **It returns where it left.** If something is standing on that spot, it is the rewind's landing
  problem with the same fix: check the space and shove the occupant aside.
- **Banished enemies are still alive.** Anything checking whether a room or floor is clear must count
  them, or a room could clear while enemies are away. Assume Identity's "only one enemy left" check
  should count them too.
- **Resistance could become a stat.** "Elites have 50% resistance" is naturally a banish resistance
  value, which some enemy types could carry later without special cases.

### Reality Shards

- **Decided: it is cast.** Casting spends all the mana up front and starts the spheres generating, so
  the damage arrives later as a reward already paid for.
- **It pairs with Arcane Warp.** The up-front spend empties the pool at the start, so your attack speed
  is high while the spheres are still arriving and firing.
- **Keep them out of the minion rules.** The school has no summons, so the shards should not be
  minions: not on the minion layer, not targetable by enemies, not blocking anyone, and not alerting
  enemies that see them. They are orbiting projectiles, not creatures.
- **The orbit already has a precedent.** Death's floating souls are code-generated spheres following
  the player. The shards can share that motion.
- **Decided: unfired spheres last until you leave the floor.** They wait, following you, until there
  is an enemy to fire at.
- **Decided: recasting replaces them.** Any spheres from the previous cast are lost, along with the
  mana spent on them, so recasting early is a real cost.
- **Decided: shard hits do not restore mana** for Arcane Warp.
- **They clear on the floor change,** which is the same moment Rewind's history clears, so both can
  hang off one floor-change hook.
- Each shot is an ordinary spell projectile fired from the sphere toward the nearest enemy in line of
  sight.

### Nether Wall

- **Decided: it blocks shots from both sides.**
- **It is a layer on the hit lists, not a physical wall.** Hitscan shots and projectiles both find
  their targets through `Layers.HitMaskFor`, so a new layer added to both teams' hit masks stops both
  kinds of shot. Left out of the collision matrix against players, enemies and minions, it lets
  bodies walk through. Left out of `Layers.BlockingMask`, it does not affect movement or pathfinding.
- **Enemies can still see through it.** Sight uses `Layers.SightBlockMask`, which only holds level
  geometry and props. So enemies see you behind the wall but cannot shoot you, and you cannot shoot
  them. Both sides watch each other and wait, which suits a wall that blocks both ways.
- **Area damage ignores it.** Explosions find their victims by overlap, not by a shot, so a blast on
  the far side still hurts. Telegraphed slams do too.
- **Open: size and duration.**
- Nether Smoke uses the same trick.

### Invisibility

- **Decided: a mana-draining toggle, broken by shooting or casting.**
- **Held spells are Movement-only today.** `SpellTools.VerifySlots` reports a sustained spell in any
  other slot as a problem. Elemental Form already needs that rule relaxed; Invisibility is the second
  cast-slot toggle.
- **Sight goes, hearing stays.** It reuses the planned last-seen position work from Blind, so enemies
  that were tracking you aim where you vanished. Footsteps still make noise, so walking close to an
  enemy can still give you away.
- **It pairs with Arcane Warp.** The drain empties your pool while you are hidden, so your opening
  shot comes out at a high attack speed. A strong ambush, and a natural fit.
- **Open: does melee break it?**

### Collapse Space

- **The pull is knockback pointed inward,** the same as Fathomless Gate's.
- **Knockback impacts make it much stronger than its damage.** Enemies pulled in from every side
  close on each other fast at the centre, and every knockback into another enemy or a wall deals
  damage. One grenade in a crowd sets off a cascade. Tune the pull with that in mind.
- It overlaps Fathomless Gate, but one is an instant grenade and the other a lasting whirlpool.

### Flicker

- **Out of reality means untouchable.** Immune and untargetable for its brief duration.
- **"Cannot shoot" is disarm, applied to yourself.** Disarm is already planned for enemies.
- **A random debuff cleanse has two customers now,** Flicker and Bestial's Embiggen, so the helper
  that removes one debuff at random is worth building once.

### Nether Smoke

- **Guaranteed hits use Nether Wall's layer trick, one-sided.** The smoke is a volume on a layer in
  the player's hit mask only. Your hitscan shot or projectile strikes it, and the hit is handed to a
  random enemy inside. Enemy shots pass straight through.
- **Blind has a second customer,** after Abyssal's Ink Spray.
- **Making smoke genuinely opaque is the art-heavy part.** Everything is built from code, and a few
  transparent spheres will not hide what is inside. Layered camera-facing sprites or a dense particle
  system is closer.
- **Open: does it block enemy sight too?** If so, you can hide in it.
- **Open: is it a hazard for the soft push?** Enemies shy away from fire. If they shy away from smoke,
  they leave it, and the guaranteed hits have fewer targets. Probably not a hazard.
- **Shotguns and splash.** Each pellet that enters picks its own random enemy, and a rocket explodes on
  whichever enemy it is handed.

### Echo

- **Decided: original aim, no ammo, no extra mana.** The copies replay each shot along its original
  direction, cost no ammo, and echoed spells cost nothing extra. Echo itself is the price.
- **Three cast slots make it workable.** With the third cast slot decided, Echo no longer leaves you
  a single other cast spell to double.
- **Decided: copies fire from where you stand now,** along the original shot's direction. If you
  moved during the delay, the copy travels a parallel line and can miss what the original hit, or
  hit something it missed. It stays tied to you rather than being a ghostly replay.
- **Decided: copied shots do not restore mana** for Arcane Warp.
- **Other costs should follow the mana decision.** Echoed spells that cost souls, health or psi charge
  are simplest free as well, matching the choice to drop the extra mana cost.
- **Nothing records actions yet.** `SpellBook` has a `SpellCast` event, but `Weapon` fires no event at
  all. Echo needs a record of each shot's direction to replay.
- **Rewind should not echo.** No history is recorded during a rewind's playback, so a second rewind
  straight after has nothing sensible to return to. Spells with a duration, such as Path of Light,
  gain nothing from a second copy either.
- **Echoes should not start extra cooldowns.**

### Stop Time

**Decided: the mythic.** Freeze everything but you for a few seconds. Your shots hang in the air and
land when time resumes. It completes a set with the school's other time spells: Echo repeats, Rewind
returns, Stop Time halts.

- **The global time scale cannot do it.** Unity's time scale freezes the player along with everything
  else. Stopping the world but not you needs a separate world time rate that everything except the
  player reads.
- **What has to read the world rate.** Projectiles, enemy movement and behaviour, enemy attack timers
  and telegraphs, status ticks and durations, lingering zones and delayed blasts. Status durations
  already decay through `StatusDefinition.DecayScale`, and enemy attack cooldowns already tick
  through a rate multiplier. Projectiles and enemy movement advance by raw frame time today, and are
  the main work.
- **Build the world rate as a general tool.** Once it exists, any slow-motion or time effect for any
  school reads the same value.
- **Hold all damage until time resumes.** Hitscan shots land instantly, so "hangs in the air" needs
  their hits queued and delivered on resume, and melee against a frozen enemy is the same question.
  Holding every hit dealt during the stop, then applying them all at once, keeps enemies from dying
  while frozen and makes the resume a single burst. It is also the school's storage idea at its
  largest.
- **Everything that isn't you freezes,** your own lingering zones and shots included.
- **Banish timers stop too.** A banished enemy's countdown runs on world time, so Stop Time extends
  every banish by its own length.
- **The resume is a knockback storm.** Held knockback applied all at once sets off impact damage
  across the room simultaneously.
- **Arcane Warp refills in one go.** If mana is restored when shots land, a long stop full of shooting
  restores its mana in one lump on resume.
- **Decided, as placeholders: 6 seconds, 50 mana, 30 second cooldown, fixed duration.** The numbers
  are expected to change with tuning.

### Blink

- **Decided: it passes through walls, except the outer edge of the map.**
- **Today it stops at the first wall.** Its targeting sweeps a capsule forward against level geometry
  and aborts on contact, refunding the cost.
- **The landing must be valid.** Landing inside a wall's thickness or a rock block is not. `NavField`
  already knows which cells are walkable from the static geometry, so the landing is the furthest
  walkable point along the line within range.
- **The outer edge** is the maze footprint's boundary, which the level builder already knows.
- **It changes how the maze plays.** A common spell that crosses walls turns every wall into a
  shortcut. It also crosses embrasure walls, as Rapture of the Deep does. Nothing can soft-lock, since
  the maze never relies on crossing either.
- **It moves school and rarity.** In code it is still Psionic and uncommon.

### Space Hammer

- **A very long cooldown on the melee slot** means most of the time you have no melee attack at all.
  That is a real trade against the damage, and worth it being deliberate.
- **Kinetic, so it shatters frost.** A full frost stack plus one hammer swing is an execute.
- **Psi Blades adds to it,** like any melee spell.
- **Open: does it knock back?** If so, knockback impacts add wall and collision damage on top.

### Rewind

**Decided:** snap back along your path to where you were three seconds ago, like Tracer's Recall in
Overwatch, on an eight second cooldown. Playback takes 0.75 seconds. Health resets to exactly its
recorded value, up or down, with no special treatment for the Blood Debt.

How to build it:

1. **Record.** A small component on the player saves a snapshot about ten times a second, keeping the
   last three seconds. Each snapshot holds position, health, the ammo in both holster slots, which gun
   is out, and the wall-walking up direction. A fixed rate keeps the history the same length at any
   frame rate; it is about thirty small records.
2. **Play it back.** Casting starts a timed effect, the kind `WaitEffect` already is, that moves the
   player backwards through the snapshots over 0.75 seconds, blending between them. The character
   controller is switched off and the body moved directly, so nothing blocks the path, and since level
   geometry never moves, the old path is always clear of walls. You are invulnerable and hidden from
   enemies throughout. The camera keeps your aim; only the body travels.
3. **Land.** At the oldest snapshot, collision comes back on, velocity is zeroed, and health and ammo
   are restored.

Things to get right:

- **The landing spot may be occupied now.** Test the player's whole capsule at the spot and shove
  anything it touches clear by position before arriving. Knockback alone plays out over later frames,
  so you would land inside it, and two character controllers overlapping tend to stick.
- **Set health directly, not through healing or damage.** Healing is scaled by healing received and
  cures bleed, and damage runs through resistances, statuses and the damage hooks. `Health` has no
  direct way to set current health today, so the rewind needs one that fires no damaged or healed
  events. Otherwise a rewind that lowers health could consume Foretell, end Assume Identity or
  trigger lifesteal.
- **It can never kill you.** Snapshots are only taken while you are alive.
- **The Blood Debt stays owed.** Rewinding health does not touch the debt, so Abyssal costs paid in
  those three seconds come back as health while kills still repay the debt with interest. A
  deliberate payoff for mixing the two schools.
- **Only position, health and ammo go back.** Souls, psi charge, mana and cooldowns stay as they are.
- **Playback runs four times faster than the moment it replays.** A sprint with Path of Light or
  Burning Feet flashes past, so the camera should follow a smoothed path rather than every recorded
  point.
- **Clear the history on teleports and floor changes,** or a rewind carries you back onto the previous
  floor. `PlayerMotor.Teleport` is the single place to do it.
- **Wall walking.** A snapshot on a wall either reattaches you or drops you. Dropping you is simpler,
  and teleporting already ends a wall zip.
- **Held movement spells end when it starts,** such as Burning Feet, Bloodwake and Rapture of the Deep.
- **Enemies lose you on landing,** through the same last-seen position Blind needs.

---

## Aetherics: choices made

- **Mastery.** Held Moments, a pocket holding specific things, was proposed and set aside for Arcane
  Warp.
- **Mythic.** Stop Time was chosen over Loop, which snapped enemies back to where they stood while
  keeping their wounds, and Unmake, which erased one enemy and the damage it had dealt.
- **Echo** first cost 50% extra mana for spells; that was dropped to keep it simple.
- **From the first list:** Blink stays. Unseen became Invisibility, and Halfstep became Flicker.
  Essence Tap, Mote Sentry, Rift Shard, Animate Clay, Prism Post, Colossus Core, Open the Vault,
  Aetherwalk and Phaseblade are dropped, the constructs because the school has no summons.
- **Rewind** took the rare movement slot from Aetherwalk, and replaced Divination's Premonition.

---

## Petty spells

**Decided: filler and starter spells that belong to no school.** They count toward no mastery, so a
Petty spell in a slot is a slot with no mastery attached. With five slots and the mastery count
capped at four, that makes them natural early-run filler, and the loadout rework can build starting
kits from them without handing anyone a free mastery pip.

### Common

**Dart** - a straight-line projectile dealing kinetic damage.

**Orb** - a grenade dealing energy damage.

**Sleep** - a projectile that puts an enemy to sleep for 10 seconds. Damage wakes it sooner.

**Dazzle** - a flash that blinds a single target for a short duration.

### Uncommon

**Shock** - a cone that applies shock.

**Rot** - a cone that applies poison.

### Movement

**Dash** (common) - the existing dash. Its charges are set by the spell's level.

### Melee

**Bash** (common) - the existing default melee, moved from Elemental: a close swing that deals
damage and nothing else.

---

## Petty spells: implementation notes

### Belonging to no school

- **It needs a school value that means none.** `SpellSchool` has one member per school today. Add
  the new member at the end of the enum, never mid-list, since schools are stored as integers in
  assets and inserting one would renumber every authored spell.
- **Mastery counting skips it** by construction, since no mastery asks for it.
- **Decided: Petty spells appear in shrine and pedestal offers, at most one per offering.** The
  player will also have ways to eliminate Petty spells as options.
  - **The one-per-offering rule is a small change.** `SpellLibrary.OfferDistinct` builds an offer
    one pick at a time, removing each pick from the pool. Once a Petty spell is picked, removing
    every other Petty spell from the pool enforces the rule.
  - **Eliminating spells needs a list on the run.** A set of eliminated spell ids on `RunState`,
    filtered out in `SpellLibrary.Offerable`, would do it, and would work for eliminating any
    spell, not only Petty ones.
  - **Offers never come up short because of it.** When nothing is left at the rolled rarity, the
    pick falls back to another tier, so removing Petty spells from the pool only leaves a slot
    empty once no spells remain at all.
- **Decided: a Petty melee spell that just deals damage.** The existing default melee, `bash`, is
  the natural candidate: a close cone swing. Under the new stats its damage scales with Power, like
  every spell. It is Elemental in code, so
  moving it to Petty also removes the free Elemental pip.
  - **Its knockback goes.** Bash currently knocks enemies back, and knockback impacts now deal
    damage, so it would do more than damage. "Nothing else" means removing the knockback.
  - **Its smash power goes too.** Reinforced barriers are being removed from the game, so smash power
    has nothing left to open.
  - **Crits count as damage,** so it can keep them.
- **Dropped: Mana Shield.** It was designed as an absorb that took half of each hit and burned mana
  for it, and was cut.
- **Decided: the older spells are retired.** Shield, Arcane Ward and the other twelve spells from
  before these designs appear in no designed list and are gone from the game.

### Works today

- **Dart** - `SpawnProjectileEffect` dealing kinetic damage.
- **Orb** - `SpawnProjectileEffect` with gravity and splash, dealing energy damage.
- **Shock** and **Rot** - `SelectConeEffect` with a shock or poison payload. Both feed other schools'
  systems for free: shock is one of Conflux's three elements, and poison ruins enemy aim.
- **Dash** - already built. It moves from Divination, where the code still has it, to Petty. Its
  charges move from Agility to the spell's level; see Player stats.

### Sleep

- **The projectile's own damage would wake the target.** A hit applies its statuses before its
  damage, so a damaging Sleep puts the enemy to sleep and wakes it in the same hit. It is the same
  trap as Brain Fog's confusion, so Sleep should deal no damage.
- **Status ticks wake it too,** unless ticks are excluded using the delivery field on damage. A
  burning or bleeding enemy could otherwise never stay asleep.
- **Knockback impacts and minion attacks wake it,** since both deal damage.
- **Decided: sleep lasts 10 seconds,** or until the enemy takes damage, whichever comes first.
- **Decided: elites sleep for half as long,** 5 seconds, in line with the pattern of control effects
  bending elites rather than exempting them.
- **Enemies cannot be un-alerted today.** Perception only ever switches alert on. Being asleep needs
  perception and attacks suspended while it lasts, and waking can then alert the enemy as normal.

### Dazzle

- **Decided: a single-target flash.** That settles its overlap with Abyssal's Ink Spray, which blinds
  everything in a cone. Ink Spray is the crowd tool, and Dazzle picks out one enemy.
- **Target the enemy nearest your reticle.** An instant flash suits a quick pick: the enemy closest to
  where you aim, by angle, among those in line of sight, which is the same rule Superid uses.
  Spells have no hitscan yet, so it shares the small instant-ray effect Mind Spike needs.
- **Blind does not break on damage,** unlike sleep and confusion, so Dazzle cannot undo itself the
  way a damaging Sleep would.
- **Decided: common.** One enemy blinded is weaker than a whole cone, so it sits a tier below the
  cone spells.
- **Blind has a third customer,** after Ink Spray and Nether Smoke.

---

## Player stats

**Decided: five stats built around a wizard with a gun.** They replace Strength, Intellect, Agility,
Vitality and Luck.

| Stat | Affects |
|---|---|
| Power | Spell damage, strength of the statuses spells apply, maximum mana |
| Dexterity | Bullet spread, recoil, reload speed |
| Athletics | Spell cooldowns, movement speed, jump and air control |
| Endurance | Maximum health, mana regeneration |
| Luck | Reward rarity, crit chance |

Also decided:

- **Stats do not affect gun damage or fire rate.** A gun's damage and rate come from the gun alone.
- **There is no base health regeneration.** The health code treats any healing as closing a bleed,
  including a slow regeneration trickle, so leaving it out keeps bleed meaningful.
- **Stats do not affect crit damage.** The crit multiplier is a fixed base value.
- **Power strengthens statuses as it does damage.** A spell that applies 6 burn gets the same boost
  from Power that 6 damage would. Each status will need its own scaling, since a point of burn is not
  worth a point of damage or a stack of frost. For now every status maps one to one, and testing will
  set the real curves.
- **Reinforced barriers are removed from the game,** so nothing needs smash power any more.
- **Every stat has a baseline of 10.**

### Where today's attributes go

| Attribute | Today, from | Under the new stats |
|---|---|---|
| Max health | Vitality | Endurance |
| Health regeneration | Vitality | None |
| Max mana | Intellect | Power |
| Mana regeneration | Intellect | Endurance |
| Spell power | Intellect | Power |
| Cooldown rate | Intellect | Athletics |
| Move speed, jump height, air control | Agility | Athletics |
| Dash charges, dash speed | Agility | The Dash spell: charges by spell level, speed fixed |
| Reload speed | Agility | Dexterity |
| Attack speed | Agility | None, though Arcane Warp still raises it |
| Gun damage | Strength | None |
| Smash power | Strength | Removed with reinforced barriers |
| Crit chance | Luck | Luck |
| Crit damage | Luck | None, a fixed base value |
| Spread, recoil | No attribute exists | Dexterity, once the attributes exist |

### Notes

- **Decided: stats do not affect crit damage.** The crit multiplier is a fixed base value.
- **Crits are the one way stats still reach gun damage.** Gun hits and melee can crit, while spell
  projectiles cannot, so Luck's crit chance mostly scales guns. Probably the intended exception, but
  it is one.
- **Decided: dash charges come from the Dash spell's level.** Dash is a spell, so its charges belong
  to it rather than to a stat.
- **Movement spells do not level today.** Only cast-slot spells stack levels: `SpellLibrary.Offerable`
  treats a movement or melee spell as a straight swap, and Dash has a maximum level of 1. Charges by
  level needs movement spells, or at least Dash, to level up.
- **Charges already have an attribute.** `Attr.DashCharges` exists and is fed by Agility today, so the
  spell's level can feed it instead.
- **Decided: dash speed is fixed.** Agility raises it today; under the new stats it is a constant, and
  only the charges grow with the spell's level.
- **Jump height** is read as part of Athletics' jump and air control.
- **Spread and recoil need attributes.** Today they are read straight off each gun's definition.
  Elemental Form's ice form needs the same two attributes.
- **Melee scales with Power.** Melee attacks are spells now, so Power's spell damage covers them.
  Bash scales its damage with Strength by name today and needs rewiring.
- **Blink's distance scales with Intellect by name.** It needs a new stat or a fixed distance.
  Athletics fits a movement spell.
- **Power and Endurance pull Arcane Warp in opposite directions.** Power's larger mana pool raises
  its ceiling, while Endurance's mana regeneration refills the pool and lowers the bonus.
- **Removing barriers removes smash power.** `Attr.SmashPower` is compared against the hardness of
  smashable props. With barriers gone the check can go, as long as ordinary props still break to
  any hit.
- **Considered and dropped: Resilience,** which would have shortened debuffs on the player and raised
  mana regeneration. Mana regeneration moved to Endurance, and nothing shortens debuffs on the
  player.
- **Stats are stored in assets.** Loadouts store each stat as a field named after it, so renaming a
  field silently loses its authored value. Boons store which stat they raise as a number, the same
  trap damage types had. Both belong in the architecture analysis.
- **Decided: 10 is the baseline for every stat.** A stat at 10 is normal, and loadouts, shrines and
  boons move stats up or down from there.
- **The formulas need rebasing around 10.** Every attribute today is a base value plus so much per
  stat point, written around loadouts that default to 5 in each stat. Maximum health, for example, is
  80 plus 12 per point. Moving the baseline to 10 without touching the formulas would quietly raise
  everything, so each formula should give today's normal value at 10.
- **Luck's rarity odds need the same.** Reward rarity scales with the raw Luck value, so a baseline of
  10 would improve every run's odds unless the rarity formula is rebased around 10 too.
- **Each point is worth relatively less.** One point from a shrine was a fifth of a baseline stat; at
  10 it is a tenth. Shrine and boon amounts may want revisiting.
- **Loadouts default to 5 in every stat** in `LoadoutDefinition`, and the authored loadout assets
  store their own values, so both change.
