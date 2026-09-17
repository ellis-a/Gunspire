# Boon implementation analysis

How to build the roster in `BoonDesign.md` (229 boons) and the shillings currency on top of the code as it
stands. The shop itself is out of scope; only what the shop boons need to store is covered. Built so far:
retiring the old roster, the framework and the supporting systems (steps 1 and 2 of the build order; each
built section says so).

---

## Summary

The boon framework is a good base and should be kept: boons are data (`Boon`, a `[SerializeReference]` list of
`BoonEffect`s, an optional `BoonRequirement`), rosters are hybrid (code built-ins, assets merged over by id), and
`RunState` carries the run. What it cannot do yet is what most of the new roster needs:

1. **Standing behaviour.** Today an effect runs once when picked and either adds a modifier or flips a flag on
   `RunState`. Around 150 of the new boons react to something (a hit, a kill, a reload, a floor starting). A
   flag per boon on `RunState` does not scale to that; boons need their own runtime objects.
2. **Hit-time damage rules.** Conditional damage (below 30% health, beyond 20 m, while airborne, last quarter of
   the magazine) needs the target and the circumstances. Outgoing damage is currently fixed where the round is
   built, before any target is known.
3. **Gates and families.** Only a movement-ability gate and a has-boon gate exist.
4. **New channels on the character sheet.** Per-school spell cost and cooldown, per-weapon-class gun stats, and a
   handful of new attributes.
5. **Shillings.** Nothing exists yet.

The plan is framework first, then shillings and weapon classes, then the boons in order of how much new code
each needs.

---

## What exists and gets reused

| Piece | Where | Used by |
|---|---|---|
| `Boon`, `BoonEffect`, `BoonRequirement`, `BoonRunner` | `Boons/` | Everything. Kept, extended. |
| Hybrid roster, `Create Boon Assets`, `Log Boon Table` | `BoonLibrary`, `Editor/BoonTools.cs` | Authoring. |
| Rarity roll weighted by Luck | `Rarities.Roll`, `PickOfRarity` | Offers. |
| Five stats, derived `Attr`s, removable modifiers with a source | `CharacterSheet` | Every number boon. |
| Typed channels (per damage type, per spell type, resistances) | `TypedModifierSet<T>` | Template for per-school and per-class channels. |
| Global events: `Health.AnyDamaged`, `Health.AnyDied`, `LevelEvents.FloorEntered/FloorLeaving` | `Core/`, `Level/` | Kill, damage and floor boons. |
| `Weapon.Fired`, `Weapon.Hit` (`WeaponHit.DealBonus`), `BulletInfusion` | `Weapons/` | On-hit, class and handling boons. |
| `PhantomWeapon` following the other hand (Divine Assistance) | `Weapons/PhantomWeapon.cs` | Mirror Barrel, nearly as is. |
| `SpellBook.SpellCast`, `SpellBook.CastEcho`, `ReduceCooldowns` | `Spells/SpellBook.cs` | Twincast, Spell Magazine, Rhythm, Blooded. |
| `Health.Shield`, `InvulnerabilityTimer`, `Execute`, `Foretold` dodge | `Core/Health.cs` | Padding, Panic Barrier, Prophecy, Glass Soul. |
| `StatusController`, `PlannedStatus` (Fear, Blind and others named, no behaviour) | `Effects/` | Enchantments and new statuses. |
| `EnemyController.SetSide` | `Enemies/` | Charmed. |
| `EtherealByNature`, Deathmark | `Health`, statuses | Ghostrealm, Doomed. |
| `FamiliarSummoner`, `GrantFamiliarEffect`, `FamiliarAura` | `Familiars/` | All familiar boons. |
| `MinionController`, `MinionSummoner` | `Minions/` | Beetle Swarm, Master Summoner. |
| Masteries on `MasteryHost` (Conflux, Blood Debt, Souls, Psi Blades, Arcane Warp, Beast, Divine Knowledge) | `Masteries/` | Every school boon. |
| `OrbPickup` | `Level/Props.cs` | Recovery boons, and the model for shilling pickups. |
| Projectile homing | `Projectile.HomingEnabled` | Magnetised Ammo. |

Two facts that change the design slightly:

- **Swapping guns is instant.** `Holster.SetActive` has no draw time, so Quick Hands has nothing to speed up and
  Quickdraw's "instant swap" is already true. Decided: guns get a draw time (see Weapons below).
- **Your own explosions neither hurt nor push you.** Splash uses `Layers.HitMaskFor(OwnerTeam)`. Rocket Jump
  therefore has to add a self-impulse; it has no damage to remove.

---

## Framework changes

### 1. Boon data

Built. Added to `Boon`:

| Field | Purpose |
|---|---|
| `BoonFamily Family` | Core, Arsenal, Slots, School, Spell, Pact. Drives the offer's first roll. |
| `string Group` | Display grouping within a family (Stat, Damage, Recovery, Shop, Familiar...). Tooling and UI only. |
| `List<string> Tags` | `familiar`, `health`, `summon`... Used by exclusions and limits. |
| `[SerializeReference] List<BoonRequirement> Requirements` | Replaces the single requirement; all must pass. No assets existed after the retirement, so nothing needed migrating. |

`RunState.TakenBoon` gains `bool Consumed`. Every boon keeps a real `MaxLevel`; stat boons use 5. A taken boon is
never removed, so a one-off (Charge Up, Phylactery) is simply `MaxLevel` 1: using it up marks it consumed, and
it still counts as taken. A separate once-per-run flag turned out to be unnecessary.

### 2. Gates

Built, in `Boons/BoonRequirements.cs`, with the tags they read in `BoonTags`:

| Requirement | Boons |
|---|---|
| `SchoolEquipped(school)` | Every school boon (63). |
| `SpellEquipped(id)` | Spell family (none yet). |
| `WeaponClassCarried(class)` | 21 class boons. Checks both holster slots. |
| `SlotFilled(Movement / Melee)` | Movement boons, Afterimage, Tailwind. |
| `CanSummon` | Master Summoner, Blood Pact: a summon spell, the Bestial mastery, or Beetle Swarm. |
| `CarriesProjectileGun` | Magnetised Ammo. |
| `HasBoon(id)`, `NotWithBoon(id)`, `NotWithTag(tag)` | Cocky excludes `health` boons, and Cocky and Glass Soul if they are made exclusive. |
| `TagLimit(tag, limit, raisedBy, ownBoon)` | One familiar, raised by Familiarity. A held familiar can still level up. |
| `HasTag(tag)` | Familiarity only once a familiar is held. |

The gate list also feeds the doc's "Gate" column, so a verifier can check every School boon has a school gate
and every class boon has a class gate.

### 3. Boons are permanent

Decided: a taken boon is never switched off. Unequipping its spell or dropping its gun leaves it working exactly
as written, with nothing to act on. So there is no dormancy watcher and no removal path: effects apply once per
level, as they do today, and behaviours stay bound for the whole run. Gates only decide what is offered.

### 4. Runtime boons

Built as `BoonBehaviour` and `BoonBehaviourEffect` (`Boons/BoonBehaviour.cs`).

- The effect holds a `[SerializeReference] BoonBehaviour` template, so the Inspector's type picker lists every
  behaviour and its parameters stay editable on the asset.
- On the first pick, `RunState.LevelBehaviour` makes a live copy of the template (keyed by the template) and
  binds it; later picks raise its level. The template itself is never changed, and each run gets its own copy.
- Overridable hooks: `OnBind`, `OnUnbind`, `OnLevelChanged(previous)`, `OnFloorEntered`, `OnFloorLeaving`,
  `OnFloorCleared`, `OnFloorCompleted` and `Tick` (game time, while playing, driven by the director).
- A behaviour that implements a rule interface is registered for it while bound; nothing else is needed.
- The live copy is shallow, so a behaviour holding a list or set must create it in `OnBind`.
- `RunState.Unbind` releases every behaviour, which also unregisters its rules.

### 5. Hit-time damage passes

Built as `CombatRules` (`Core/CombatRules.cs`), a static registry cleared when a run starts, so edit-mode
tooling can use it without a run. Each list is walked in place, so no hit allocates.

**Outgoing.** `IOutgoingDamageRule.OutgoingMultiplier(in hit, target)`, a multiplier.

- Applied in `Health.TakeDamage` before resistances, whenever the hit comes from the player's team and the
  target is not on it. That includes confused (neutral) enemies and minion hits; each rule decides what it
  counts, using `hit.Origin`. Negative multipliers are held at zero.
- `DamageInfo` now carries `Weapon` (the firing gun), `Spell` (the casting spell) and `SourcePosition` with
  `HasSourcePosition` (muzzle, launch point or caster). Gun hits, gun and spell projectiles, splash, spell hits
  and `WeaponHit.DealBonus` all fill them in.

**Crits.** `Combat.RollCrit(sheet, target, out multiplier)`. Crits were already rolled at the moment of the hit,
in `Weapon.BuildShotDamage`, `Projectile.BuildHitDamage` and `AbilityContext.BuildDamage`; each now passes the
target, and `ICritRule.AdjustCrit(attacker, target, ref chance, ref forced)` can raise the chance or force a crit.
Timing and feel are unchanged. Splash and status ticks still cannot crit.

**Incoming.** `IIncomingDamageRule.ModifyIncoming(in hit, player, amount)`, run on the player only (not minions,
which share the team) after resistances and before the shield. Zero or less cancels the hit. The player is
`CombatRules.PlayerHealth`, set when the run binds.

**Lethal.** `ILethalHitRule.TrySurvive(in hit, player, amount)` with a `LethalPriority`, lower first. Run when a
hit, after the shield, would take the player to zero. The first rule that returns true cancels the hit and sets
whatever health the player keeps. Soul Shield gets a lower priority than Phylactery.

### 6. Character sheet

**New attributes** (`Attr`, appended at the end so asset integers stay put):

| Attr | Boons |
|---|---|
| `MagazineSize` (percent) | Mag Dimension, Extended Drum |
| `Pierce` (flat) | Overpenetration, Flechette via class channel |
| `Knockback` | Concussive, Pinball Wizard |
| `ProjectileSpeed` | Hot Load |
| `SplashRadius` | Bigger Boom |
| `SpinUpRate` | Warm Barrel |
| `ScopeZoom` | Steady Breath |
| `DrawSpeed` | Quick Hands |
| `JumpCount` | Acrophobia |
| `OrbPotency`, `OrbDropChance`, `PickupRadius` | Treasure Hunter, Scavenger, Magnetism |
| `DebuffPotency` (incoming) | Resilience |
| `ShillingGain` | Pocket Change |

**Typed channels.** Two new sets beside the existing ones:

- `TypedModifierSet<SpellSchool>` three times: damage, mana cost, cooldown rate. Covers the three template
  Commons of every school (21 boons). `SpellCosts` and `SpellBook.Tick` read them.
- A per-weapon-class channel, `GunClassModifiers`, keyed by `(WeaponClass, GunStat)`: reload, spread,
  magazine, pierce, fire rate, spin-up, splash. `Weapon` reads its class's values on top of the global
  attribute. Covers most class Commons with one effect type, `ModifyGunClassEffect`.

**Sourced stat points.** `CharacterSheet.AddStat` has no source, so a stat bonus cannot be taken back. Hoarder's
Luck rises and falls with your shillings, so it needs `SetStatBonus(source, stat, amount)`.

### 7. Weapons

**Classes.**

- `WeaponClass` enum (Unassigned, Handgun, SMG, Shotgun, Rifle, Sniper, Heavy, Launcher), stored as an
  integer, so append-only. Unassigned is what the new field deserialises to, so a missed gun is caught.
- `WeaponDefinition.Class`. One table, `WeaponLibrary.Classes`, gives every id its class; the built-ins read it,
  and the `WeaponClassesFromTable` migration applied it to the 17 existing assets.
- Verify Boon Framework checks every gun has a class and every class has a gun.
- Done with the framework, since the class gate needed it. Draw time is still to do.

**Draw time.** Decided: every gun has one.

- `WeaponDefinition.DrawTime`, in seconds. The same migration fills existing assets with a placeholder per class,
  light guns quick and heavy ones slow.
- Swapping (`Holster.SetActive`) puts the weapon in a drawing state: no firing, alt fire or reloading until it
  ends. Duration is `DrawTime / DrawSpeed`, with the class channel applied on top.
- Taking a gun from a plinth also draws it. Entering a room does not: the gun is already out.
- Swapping again mid-draw starts the other gun's draw; an interrupted reload still needs finishing, as now.
- The HUD shows the draw as a short bar, the same way it shows a reload.
- Quickdraw sets handgun draw time to zero and marks the first shot after a draw for its bonus.

**Alt fire is always unlocked.** Decided. Remove the lock rather than leaving it at zero:

- `AltFireProfile.UnlockTier` and its values in `WeaponLibrary`.
- `Weapon.AltUnlocked`, `AltOutcome.Locked` and the checks that use them.
- `RunState.AltFireTier`, `AltFireTierEffect` and the Gunsmith's Kit boon.
- The "(locked)" text in `WeaponDefinition.AltLine` and the locked display in `HudUI`.

Unity drops the serialised `UnlockTier` from the assets on their next save, so no migration is needed.

### 8. Offers

Built. `BoonLibrary.Offer` rolls each card as family, then rarity, then boon.

- `BoonLibrary.FamilyWeight`: Core 30, Arsenal 20, Slots 10, School 15, Spell 25, normalised over the families
  that still have a candidate. Pacts weigh nothing: they are rewards, never rolled.
- A second `Offer` overload takes the pool, so tooling can test the roll against a known roster.
- `RunState.ExtraOfferChoices`, `ExtraBoonPicks` and `OfferRerolls` are what Rewarded and Fickle Fate will set.
  The director keeps the offer up while picks remain, dropping any card the last pick made invalid (a second
  familiar), and `RerollBoons` replaces the cards with a fresh roll of the same size.
- The offer screen says "Choose N boons" while more than one pick remains, and shows a reroll button (and R)
  while rerolls remain.
- Not covered by a verifier: the director's pick-N and reroll flow, which needs a live director. It is small
  and was checked by reading; the first boon that sets these values is the time to play-test it.

### 9. Per-floor state

Built. `LevelEvents.FloorCleared` is raised by `RoomRuntime` when a room clears, and `LevelEvents.FloorCompleted`
by `GameDirector.CompleteRoom` when the player takes the exit, before any reward. Behaviours hear both, along
with `FloorEntered` and `FloorLeaving`, through `RunState`.

---

## Shillings

### Model

A `Wallet` object on `RunState`, created with the run so a new run starts at zero:

```csharp
public class Wallet
{
    public int Balance { get; private set; }
    public event Action<int, ShillingSource> Earned;   // amount, why
    public event Action<int> Spent;
    public event Action Changed;
    public void Earn(int amount, ShillingSource source);  // applies ShillingGain once, here
    public bool TrySpend(int amount);                      // for the shop later
}
public enum ShillingSource { Kill, Elite, Boss, Prop, Pickpocket, Gilded, Interest, Other }
```

- Integer shillings. Fractional multipliers round at `Earn`, carrying the remainder so a +15% bonus on 1-shilling
  drops still pays out over time.
- `ShillingGain` (Pocket Change) applies in `Earn` for kill-type sources only, so it cannot compound with Gilded
  or interest.

### Earning

- **Drops.** `EnemyDefinition` gains `ShillingsMin` / `ShillingsMax`, defaulted by tier so no enemy asset needs
  editing on day one: common, elite (several times more), boss (a large pile). A migration or a verifier warns
  about any enemy left at zero.
- **Props.** Breakables get a small chance to drop a coin alongside their orb.
- **Pickups.** `ShillingPickup`, built like `OrbPickup`: a coin that pops out, settles, then flies to the player
  after a second. It must never be lost: `ClearRoom` destroys pickups, so on `FloorCompleted` every coin still
  on the floor is banked first.
- **Randomness.** Drop amounts use `Run.Rng`, not `Random.value`, so a seed replays the same economy.
- **Kills counted.** The same rule as the rest of the run (`RunState.CountsAsKill`): any enemy death pays,
  whoever made the kill. Decided: summoned and split enemies drop nothing, matching how Doomed treats them, so
  they cannot be farmed.

### Showing it

- HUD counter beside health and mana, with a brief "+N" on earn.
- Run summary line on death and victory.
- Training room: hidden, since nothing there costs anything yet.

### Boons that touch shillings

| Boon | Implementation |
|---|---|
| Pocket Change | `ShillingGain` attribute, applied in `Wallet.Earn` for kill sources. |
| Pickpocket | Behaviour on `PlayerCombat.MeleeFinished` hits: spawns a coin, with a per-enemy cap so one enemy cannot be milked. |
| Gilded (status via Gilded Enchantment) | Status stores an accumulated amount on the enemy, capped; on death the enemy drops it as `ShillingSource.Gilded`. |
| Hoarder | Behaviour on `Wallet.Changed`: sets a sourced Luck bonus to `Balance / 100`. Needs `SetStatBonus`. |
| Haggler | Stores `ShopDiscount` on `RunState`. Read by the shop later. |
| Check the Storage | Stores `ShopExtraItems` on `RunState`. Read by the shop later. |

### Verifying

Part of Verify Boon Systems: a kill spawns coins, picking one up credits the wallet, Pocket Change
multiplies kill income and nothing else, Gilded caps, coins left on the floor are banked on exit, and a new run
starts at zero.

---

## Boon by boon

Grouped by how much code each needs. "Data" means a built-in with existing or framework effects and no new C#
beyond the framework. "Effect" means a small new `BoonEffect`. "Behaviour" means a `BoonBehaviour` class.
"System" means new game code outside boons.

### Data only (after the framework)

| Boons | How |
|---|---|
| All 26 stat boons (Nimble to Ascendant) | `ModifyStatEffect`, five levels each. |
| Super Soldier, Archmage | `GunDamage`, `SpellPower` percent. |
| Four Enhancers | `ModifyDamageSchoolEffect`. |
| Hard Skin | `DamageTaken` percent, negative. |
| Bigger Bullets | `CritDamage`. |
| Thick Blood, Deep Reservoir | `MaxHealth`, `MaxMana` flat. |
| Mag Dimension, Treasure Hunter, Scavenger, Resilience, Concussive, Pocket Change, Haggler, Check the Storage | New attributes, `ModifyAttributeEffect`. |
| The 21 school template Commons (damage, cost, recovery) | `ModifySchoolEffect` on the new typed channels. The cost channel covers health costs too, since Shallow Wounds lowers Abyssal's. |
| Class Commons: Sidearm, Extended Drum, Tight Choke, Marksman, Overpenetration, Warm Barrel, Belt Fed, Bigger Boom, Hot Load, Spray and Pray | `ModifyGunClassEffect`. |
| Master Summoner, Bone Density, Well Fed, Sharp Teeth, Fleet Paws | Minion and companion multipliers read at summon; one effect type, `ModifyAlliesEffect`. |
| Soul Jar, Farsight, Wide Void, Keen Mind, Sharpened Will, Low Interest, Aether Tap | One mastery parameter each. Each mastery exposes a small `MasteryModifiers` bag read where the number is used. |

### Small effects

| Boons | How |
|---|---|
| Rewarded, Fickle Fate | Offer size and rerolls on `RunState`, read by the director and the offer screen. |
| Charge Up | Sets a next-floor multiplier; a behaviour clears it and marks the boon consumed on the following floor. |
| Rest a Moment | Heal on `FloorCompleted`. |
| Quiss, Esarl, Fex | A per-slot level bonus on `SpellBook`, added to `GetSlotLevel` and used wherever a cast reads its level. |
| Padding | Shield on `FloorEntered`. `Health.AddShield` exists; it needs a "lasts until broken" duration. |
| Acrophobia | `JumpCount` attribute; `PlayerMotor` counts air jumps, reset on landing. System change in the motor. |
| Deep Freeze, Stoked, Overcharge, Lingering Doubt, Lingering Light, Clear Sight | Scale the status or duration where the school applies it; a per-school `StatusPotency` channel. |
| Stat-adjacent spell boons: Pauper's Grave (mana only, not souls) | Falls out of the school cost channel if soul costs stay outside it. |

### Behaviours

**Damage rules** (outgoing pass): Executioner, Big Game, Brawler, Longshot, Momentum, High Ground, Kill Streak,
Blasphemous Act, Cold Blood, First Blood, Point Blank, Quickdraw's first shot, Closing Round, Fresh Mag, Tail End,
Quickscope, Dead Man's Hand, Pinpoint, Planted's spread half, Tri-Attuned, Pack Tactics, Guided Shots, Fractured
Mind, Vengeful Rage, Last Stand's damage half, Glass Soul, Cocky, Charge Up.

- Each is a few lines once `DamageInfo` carries the weapon, the spell and the origin.
- Fresh Mag, Tail End, Closing Round and Dead Man's Hand read per-weapon state (rounds since reload, burst index,
  hit streak). `Weapon` gains `RoundsSinceReload` and `BurstIndex`; the streak lives on the behaviour, reset by a
  miss, which needs a new `Weapon.Missed` event for rounds that hit nothing damageable.
- Quickscope needs `Weapon.FocusStartedAt`.

**Incoming and lethal rules**: Cornered, Mana Shield, Every Reaction, Monarch, Panic Barrier, Soul Shield,
Phylactery, Glass Soul, Prophecy, Last Stand's cooldown half, Juggernaut (status immunity plus a speed
modifier).

- Every Reaction only reflects hits with a source enemy and an origin other than `StatusTick` or `Environment`.
- Monarch keeps a set of enemies that hurt you and restores itself when the set empties.
- Prophecy remembers which enemies have attacked this floor and turns each one's first direct hit into a dodge,
  through the same path `Foretold` uses (`Health.Dodged`), so Foretell's refund logic sees it too.

**Kill and floor listeners**: Vampire Sight, Demon Sight, Rhythm, Blooded, Lock and Load, Doomed, Otherworldly
Beauty, Grave Hunger, Deep Pockets, Excess Force, Overflowing Souls, Foreclosure, Tidal Surge, Tailwind, Bottled
Orb, Cool Head, Vendetta (marks the last attacker; its death heals), Courageous (enemy stat multiplier at spawn
plus Luck).

**On-hit listeners** (`Weapon.Hit`): Chain Static, Shared Instinct (companion attacks carry the run's bullet
statuses), Imbued Rounds (applies the last Elemental element), Pickpocket, Birthday Party (headshot flag already on
`DamageInfo`), Gorelust (melee), Void Rounds (pierce while below half mana, set at fire time instead).

**Gun behaviour**: Bifurcator (a second round in `FireRound` with widened spread), Magnetised Ammo (sets
`HomingEnabled` on player projectiles), Rocket Jump (self impulse from own splash via `PlayerMotor.AddImpulse`),
Planted (no spread, no spin-down while still), Flechette (pierce on pellets, a class channel), Gunmage (a mana
cost path in `FireRound`; `FreeRounds` is the hook), Spell Magazine (last round calls `CastEcho` on Q), Lock and
Load.

**Enchantments**: one `EnchantmentBehaviour` for all eleven. On `Weapon.Hit` it applies
`round damage × multiplier × level` of its status. Shock's variant arms on reload and fires on the first round.
The existing `AddOnHitStatusEffect` is too fixed for this formula.

**School mechanics** that change a mastery's rules rather than a number: Fusion, Foreclosure, Perfect Timing,
Soul Shield, Psychic Wave, Thrown Blade, Void Pocket, Riding the Current, Empty Vessel, Overflow, Soul Slave,
Displacement, Enfeeble, Hemorrhage, Blinding Light.

- Each needs a hook in its mastery: an event where the rule would fire (reaction, repayment, soul spent, psi
  spent, warp bonus ended) and a flag or parameter the boon sets. Adding those events is the bulk of this group.

### Systems

| Boon | Work |
|---|---|
| Mirror Barrel | `PhantomWeapon.Create(..., followOtherHand: true)` at half damage. Divine Assistance already does the hard part. |
| Third Hand | `Holster.SlotCount` becomes a runtime value; swapping cycles; the HUD shows three. |
| Twincast | `SpellCast` listener with a chance roll, recasting through `CastEcho`, which must not trigger itself. |
| Afterimage | A decoy target registered in `TargetRegistry` so enemies attack it; explodes on death. |
| Beetle Swarm | A new `MinionDefinition` and a timed spawner behaviour; beetles count as minions. |
| 13 familiars | New `FamiliarDefinition`s (the four current ones are retired), one-familiar limit via tags. Wisp heals, Homunculus is targeted and stays dead for the floor, Gremlin reloads the holstered gun through `Holster.SetAmmo`, Magpie needs orb seeking, Coin Imp is a Luck aura. |
| Ghostrealm | Sets `EtherealByNature` on every enemy at spawn. |
| Otherworldly Beauty | Needs **Charmed**. |
| Hex, Volatile, Gilded, Dread, Charmed | New statuses (below). |

### New statuses

`StatusId` is integer-stored, so new members are appended.

| Status | Work |
|---|---|
| Charmed | `SetSide(Team.Player)`, minion-style targeting, counted as defeated by `RoomRuntime` (unregister), refused by elites. |
| Hex | Stores a share of damage taken in `Health.AnyDamaged`; pays out as psychic on expiry; lost on death. |
| Volatile | Buildup; at full, `Combat.Explode` on the enemy's team with no volatile in the blast; resets. |
| Gilded | Accumulates shillings on the enemy up to a cap; paid on death. Never wears off. |
| Dread | Buildup that drains; at full applies Fear, slower on elites. |

All five are built. Charm lasts the floor, registers the enemy as something enemies fight, points it at its own
kind and releases it from the room. Hex, Volatile and Dread add up their applications; Gilded caps at 10
shillings and drops nothing from an enemy that pays no reward.

---

## Supporting systems (phase 3)

Built, and checked by Verify Boon Systems (which replaces the planned Verify Shillings).

**Character sheet.** The fourteen new attributes are appended to `Attr` and read where they apply:

- Guns read theirs through `Weapon.Stat`, which adds the class channel. That covers magazine size (rounded
  down, holstered guns too), pierce, projectile speed, splash radius, spin-up and draw speed. Reload,
  spread, recoil and attack speed now go through the class channel as well.
- Scope zoom pushes the focus field of view past the gun's own and makes it settle faster.
- Knockback scales every hit the player's own body deals, in `Health`, using the gun's class value when a gun
  dealt it. Minions keep their own knockback.
- Jump count gives air jumps on a fresh press of Space, reset on landing.
- Orb potency, pickup radius and orb drop chance all work. Enemies had no orb drops at all before this;
  the drop chance starts at zero, so only boons give them.
- Debuff potency weakens debuffs landing on anything, in the way each status declares
  (`StatusDefinition.Resisted`): burn, bleed, torment, hex, volatile and dread lose amount; frost and shock
  lose stacks; poison, charm and gilded are untouched; everything else loses duration. Poison is untouched
  because it deals no damage, and the design keeps its sway.
- Shilling gain scales kill income only.

**Channels.** The class channel (`AddClassModifier`, `GetFor`) adds its flat and percent values into the same
sums as the global ones, so a class bonus stacks additively with a global bonus. The school channels scale
spell power, mana and health costs, and cooldowns. The cooldowns include the movement and melee slots, not
just the cast slots. `SetStatBonus` gives stat points that can be replaced or taken back.

**Draw time.** Built as planned in Weapons above. Unset draw times read as the class default, and the
`WeaponDrawTimes` migration wrote the default into all 17 assets. Guns also count `RoundsSinceReload` and
`RoundsSinceDraw` for the boons that need them.

**Shillings.** Built as planned in Shillings below, with these details:

- A drop is split into at most five coins.
- Coins use world time, so they stop when time does.
- Training dummies pay nothing.
- `RunState` also holds `ShopPriceScale` and `ShopExtraItems` for Haggler and Check the Storage.

**Ally boosts.** `AllyBoosts` on the run holds health, damage and speed bonuses for everything summoned, plus
companion-only ones. They apply at summon time, to minions and the Bestial companion (through their sheets)
and to familiars (through their definitions).

**Mastery hooks.** Each mastery holds the settings its boons change, and every one is cleared in
`ResetForRun`. Each also raises the events those boons listen for:

| Mastery | Settings | Events |
|---|---|---|
| Souls | `CapBonus` | `Spent`, `WastedAtCap` |
| Blood Debt | `InterestMultiplier`, `RepayBonus` | `Repaid(amount, cleared)` |
| Psi Blades | `ChargeMultiplier`, `MeleeBonusExtra` | `Spent`, `EmpoweredMeleeLanded` |
| Arcane Warp | `RateMultiplier`, `ManaPerHitBonus`, `LingerSeconds` | `BonusEnded` |
| Conflux | `KeepsElements` | `Reacted(target, element, detonated)` |
| Divine Knowledge | `RangeMultiplier` | |
| Beast | | `CompanionDied`, `IsCompanionSource` |

Two of these need a design decision:

- **Low Interest.** The Blood Debt's interest is a bonus (repayment heals extra), so "charges less interest"
  would make the boon a downside. `InterestMultiplier` is there either way; the doc should say what Low
  Interest means.
- **Void Pocket.** The linger holds the strongest bonus since the pool was last full, not the small bonus
  left just before it refilled.

**Statuses.** Charmed, Hex, Volatile, Gilded and Dread are built (see New statuses below). Fear and Blind
already had their behaviour. Statuses gained hooks for a top-up (`OnTopUp`), damage taken (`OnOwnerDamaged`),
refusal (`CanApplyTo`) and a running total (`ActiveStatus.Stored`).

---

## Retiring the old roster

Done, along with removing the alt fire lock.

- The code rosters in `BoonLibrary` and `FamiliarLibrary` are empty until the new ones are written. An offer
  with nothing in the pool falls straight through to the room choice.
- The 44 old boon assets and the 4 old familiar assets were deleted outright rather than by migration. Several
  old ids (`archmage`, `cold_blood`, `executioner`, `overcharge`, `sharpshooter`, `wisp`, `watcher`) will be
  reused by the new roster, so a migration keyed to those ids would later delete the new assets too. The files
  were tracked in git, so every clone receives the deletion.
- Removed with them: `OnKillEffect`, `LifestealEffect`, `BlinkDetonationEffect`, `AltFireTierEffect`, and the
  `RunState` flags and handlers they drove. `RunState.GrantsLifesteal` stays, since it is tested and Gorelust
  and Blood Pact will want the same filter.
- The generic effects (`ModifyAttribute`, `ModifyStat`, `ModifyResistance`, `ModifyDamageSchool`,
  `ModifySpellCategory`, `AddOnHitStatus`, `FullHeal`, `LevelUpKnownSpells`) and both requirements stay for the
  new roster to use.

---

## Build order

1. **Framework.** Done: boon fields, gates, behaviours, the damage passes with target-aware crits, floor
   events, family-first offers with pick-N and rerolls, and weapon classes. Checked by Verify Boon Framework.
   Retiring the old roster and the alt fire lock is also done.
2. **Supporting systems.** Done: shillings, draw time, the new attributes and channels, ally boosts, the
   mastery hooks and the new statuses. Checked by Verify Boon Systems.
3. **Data boons.** Stat, template, class and multiplier boons: about 110 boons with almost no per-boon code.
4. **Behaviours.** Damage rules first (one pattern, many boons), then incoming and lethal, then listeners, then
   gun behaviours, then enchantments.
5. **School Rares.** One school at a time, on the mastery hooks already built.
6. **Systems.** Familiars, Third Hand, Afterimage, Beetle Swarm.

Authoring follows the spells: built-ins in code, then `Create Boon Assets` generates the assets, and icons are
assigned on the assets afterwards.

### Verification

`Verify Boons`, added to Verify All:

- Every boon has a name, description, family, rarity and at least one effect; ids are unique.
- School and class boons carry the matching gate; every stat boon caps at 5.
- Every level of every boon applies without an exception on a test rig.
- A swap waits out the draw time before the gun can fire, and alt fire works on every gun from the start.
- Damage rules: a scripted hit under each condition gets the expected multiplier, and none apply to enemies' hits.
- Lethal order: Soul Shield fires before Phylactery.
- Offers never contain a boon whose gate fails, a capped boon at its cap, or a second familiar.

Plus a count check that the roster matches the design doc's rarity targets
(101, 60, 53, 15).

---

## Decisions

Settled:

- **Boons are permanent.** No dormancy; a boon without its spell or gun simply has nothing to act on.
- **Crits.** Rolled where they are now, with the target passed in so rules can change the chance.
- **Draw time.** Every gun has one, and `DrawSpeed` scales it.
- **Stat boons.** All cap at 5 levels; no special offer weighting.
- **Alt fire.** Always unlocked; the lock code is removed.
- **Summoned and split enemies** drop no shillings.

Still open: the questions in `BoonDesign.md`, especially Rewarded's offer size and held versus known.
