# Spell design notes

Planning only. Nothing here is implemented. One section per school as the designs land.

Schools: Elemental, Bestial, Abyssal, Divination, Death, Psionic, Aetherics.
Per-school spread: 3 common, 3 uncommon, 2 rare, 1 mythic cast, 1 common movement,
1 rare movement, 1 uncommon melee.

---

## School masteries

Every school grants a standing effect that grows with the number of spells you hold from that
school, with diminishing returns. Bestial's is a summoned beast companion that climbs a ladder
as the count rises: jackalope (a rabbit with antlers), then fox, then wolf, then bear, and a
chimaera if the spell slot count grows.

The point of masteries is that they give spells something to interact with. Bestial gets
spells that buff the companion; Death can hang a resource like souls off its mastery and spend
it. Without the mastery those spells have nothing to attach to.

Masteries are not yet designed for Elemental, Abyssal, Divination, Death, Psionic or Aetherics.

**Counted from spells equipped, not spells known.** The bound slots are two cast (Q and E),
one movement and one melee, so the count runs 0 to 4 today and rises only when slots do.

Three things follow from that.

- **The ladder is the slot count.** One spell is a jackalope, two a fox, three a wolf, four a
  bear, and a fifth slot is where the chimaera lives. No threshold tuning needed; the tiers
  are the numbers.
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
starting loadouts into one class per school. A beast master starting with Dash and therefore
starting with a jackalope is the intended reading, not an accident. Deferred until then.

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

- **Nothing stops two weak masteries at once.** Two Bestial and two Death spells gives a fox
  and a small soul pool. With four slots that is a genuine choice rather than a mistake, and
  it is the main reason the early rungs of each ladder should be the valuable ones.

### What already exists

The familiar system covers most of Bestial's mastery. `FamiliarDefinition` carries health,
speed, attacks, auras and a damage multiplier; `FamiliarController` handles following,
engaging and empowerment; `FamiliarSummoner.Resummon` rebuilds the live creature from an
ownership record on `RunState` each time a room is entered. The beast ladder is five
definitions and a function mapping a spell count to one of them.

Two gaps.

- **Recompute on change.** Ownership is currently only rebuilt on entering a room. Once spell
  swapping is confined to special floors this may need nothing at all: a swap floor is a floor
  transition, and the beast would be rebuilt on arrival anyway. Worth rechecking once those
  floors exist rather than hooking `SpellBook.Changed` pre-emptively.
- **Death.** Familiars have `Health` and can die, and nothing resummons them until the next
  room. That is survivable for a familiar you bought; it is harsh for a companion that is
  supposed to be a standing property of your build, and worse for the spells below that are
  cast from the companion's position. Either the mastery beast revives on a timer or it is
  made unkillable and only ever knocked down.

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

**Burning Feet** (common) - increases movement speed, and leaves a trail of fire behind the
caster the same way Flaming Skull does.

**Ride the Gale** (uncommon) - launches the caster forward.

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
- **Ride the Gale** - `ImpulseSelfEffect`.
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

### Balance: Burning Feet is a common that does damage

Worth deciding deliberately rather than discovering. The other common movement spells buy
mobility; this one also lays down sustained area damage, and it does it on a slot the player
always has filled. Paired with enemies avoiding fire, a common spell also becomes crowd
control that can wall off a corridor on demand.

Nothing about that is unfixable - a short patch lifetime and low tick damage keep it a
mobility spell that leaves a mark rather than a damage spell that also runs. But it is a
reason to make the fire avoidance a soft push rather than a hard refusal to enter, so that a
player painting a line across a doorway slows a charge instead of stopping it.

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

Mastery: a beast companion that climbs jackalope, fox, wolf, bear, chimaera as the number of
Bestial spells you have equipped rises. See School masteries above.

Note that Dash and Spider Gravity taking the two movement rungs means a Bestial build reaches
the top of the ladder through the movement and melee slots as much as the cast ones. Running
Dash, Maul and two Bestial casts is the only way to a bear at four slots.

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

**Dash** (common) - dash in the direction of movement. The existing dash.

**Spider Gravity** (uncommon) - the existing spider legs ability.

### Melee

**Maul** (uncommon) - melee attack that stuns.

---

## Bestial: implementation notes

### Works today, no new code

- **Hunter's Mark** - `SpawnProjectileEffect` carrying a deathmark payload. Deathmark is built
  and verified, including the rule that it takes a chunk off elites rather than killing them.
- **Lunge** - `ImpulseSelfEffect` then a melee arc, which is how the existing lunging attacks
  are built.
- **Dash** and **Spider Gravity** - already shipped, already assets. They move schools and
  keep their behaviour.

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
