# Wizard with a Gun

A single-player FPS roguelike in Unity. You are a wizard with a sidearm, fighting up through
an enemy wizard's tower. Every enemy attack is something you can move out of the way of, so
the game is about speed, spacing and reading wind-ups rather than trading damage.

Everything is generated from code — the level, the enemies, the weapons, the UI. There are no
prefabs, materials, meshes or scene assets to keep in sync, so the whole game is the C# in
`Assets/Scripts`.

## Running it

Built and verified on **Unity 6000.5.7f1** — it imports and compiles with no errors or
warnings. It should also open on 2022.3 LTS and newer (the `FindObjectsByType` APIs it uses
landed in 2022.2), but only Unity 6 has been tested. Works on the Built-in render pipeline or
URP: shaders are looked up by name with fallbacks.

The game uses the **legacy Input Manager**. Unity 6 flags that as deprecated but still fully
supports it; if you ever see `InvalidOperationException` from `Input.GetKey`, check
*Project Settings → Player → Active Input Handling* is set to "Input Manager (Old)" or "Both".

1. Open this folder in Unity Hub. (If Hub will not open it directly, create a new 3D project
   and copy `Assets/` into it — the project has no other dependencies.)
2. Press **Play** in any scene, including an empty one, and pick a loadout. `GameBootstrap` spawns itself via
   `[RuntimeInitializeOnLoadMethod]` and builds the game.
3. Optional: **Wizard with a Gun → Create Play Scene** makes a saved scene for build settings.

## Controls

| Input | Action |
|---|---|
| `WASD` | Move (Quake-style air control — strafing keeps momentum) |
| `Space` | Jump (coyote time + input buffering) |
| `Shift` | Movement ability — Dash by default; Blink, Sprint, Aegis Stance or Spider Legs once found |
| `LMB` | Fire |
| `RMB` / `V` | Melee bash (Strength-scaled, breaks reinforced objects) |
| `R` | Reload |
| `Q` / `E` | Spell slots. A run opens with one spell on E and Q empty |
| `F` | Interact (pickups, shrines, exit portal) |
| `Tab` | Character sheet (hold) |
| `Esc` | Pause |
| `1`–`3` | Pick a boon / route card |

## Loadouts

A run opens on a selection screen. Three builds, each on **25 stat points** and a gun tuned to
roughly the same damage, so none of them starts ahead:

| | Stats | Gun | Spell (E) |
|---|---|---|---|
| **Pyromancer** | INT 8, AGI 6 | **Emberspit** — full-auto fire SMG | **Lava Splash** — grenade that leaves the floor burning |
| **Ice Wizard** | INT 7, VIT 7 | **Hailmaker** — 7-pellet frost shotgun | **Cone of Cold** |
| **Warlock** | STR 6, INT 6 | **Knell** — lobbed shadow launcher, splash | **Blightbloom** |

Every build opens with **Dash** on Shift and **one spell on E**. Q starts empty and is filled at
a Rune Shrine. Dying returns you here, so the next run can be a different build.

## Movement (`Player/MovementAbilityLibrary.cs`)

Shift is a slot, not a fixed dash. Movement gets its own system rather than being a third
spell, because a toggle that drains mana for as long as it is held is a different shape from
cast-once-and-wait.

| Ability | Shape | What it costs |
|---|---|---|
| **Dash** | Instant burst with brief invulnerability | Charges, recovering on their own; Agility grants more |
| **Blink** | Instant teleport, stops at the first wall | 16 mana, 4s cooldown |
| **Sprint** | Toggle, +55% speed | 9 mana/s |
| **Aegis Stance** | Toggle, ignores all damage — **breaks the moment you move** | 14 mana/s, max 2.5s |
| **Spider Legs** | Toggle, +22% speed and you can cling to and run along walls | 11 mana/s |

Instant abilities run an `AbilityEffect` chain, so Blink is the same chain it was as a spell and
Dash is a one-effect wrapper over the motor. Sustained ones are four knobs — speed bonus,
invulnerability, break-on-movement, wall-cling — each mapping to one capability on the motor or
the character sheet, added on activation and taken back on release.

Rune Shrines offer either a spell or a movement ability, so the Shift slot is something a run
can genuinely change.

The three live in code (`Player/LoadoutLibrary.cs`) so a fresh clone has something to pick with
nothing authored. **Wizard with a Gun → Create Starting Loadout Assets** writes them out as
ScriptableObjects in `Assets/Resources` for Inspector editing; a matching id replaces the
built-in, a new id adds a fourth card. Delete the assets to go back to the code roster.

## The run

Eight floors. Each room is cleared by killing everything in it, which unseals the exit portal.
Stepping through offers **one of three boons**, then **a choice of route** for the next floor:

- **Barracks Hall** — a straight fight.
- **Warded Sanctum** — fewer, empowered enemies; the boon roll afterwards favours rares.
- **Vault** — no guards, a gun on a plinth, reinforced crates that need Strength.
- **Rune Shrine** — a spell to learn and a standing stone that grants a permanent stat.
- **Arms Forge** — two guns, one choice, and guards who object.
- **The Warden of the Spire** — the floor-eight boss.

The route is generated from the run seed, so a seed reproduces the whole tower.

## Systems

### Rarity (`Core/Rarity.cs`)

Guns, spells and boons all share one five-tier scale. Each tier has a **flat chance per roll**;
Common takes whatever probability is left over.

| Tier | Flat chance at zero Luck |
|---|---|
| Legendary | 0.01% (1 in 10,000) |
| Mythic | 0.4% |
| Rare | 5% |
| Uncommon | 22% |
| Common | the remainder |

Luck multiplies every tier above Common by `1 + Luck × 0.08`, so a Luck 5 wizard is 1.4× as
likely to see the good stuff and a Luck 30 one is 3.4×. Elite rooms pass an extra 2.5×
multiplier on top. If the rolled tier has nothing in it, the pick steps down a tier, then up,
so a thin pool degrades instead of failing.

Note that a *run* rolls many times — three boons per floor plus shrine and vault drops — so the
per-run chance of seeing a legendary is far higher than the per-roll chance. Tune
`Rarities.BaseChance` and `LuckScaling` to taste.

### Damage schools and resistance (`Core/DamageTypes.cs`)

Six schools: **Normal, Fire, Frost, Nature, Shadow, Astral**. (A seventh, `True`, bypasses
resistance entirely and is bookkeeping rather than an element.)

Adding a school is two edits — a member on the `DamageType` enum and a case in `DamageTypes` —
because everything else walks the registry. In particular `BoonLibrary` **generates** a mastery
boon and a resistance boon per school at startup, so a new school arrives with its boons
already in the pool.

Everything that deals damage carries a school, and every entity can resist it. Negative
resistance is vulnerability, which is how each enemy archetype gets an affinity: a Frostcaller
resists Frost and takes 30% extra from Fire, a Warden resists Astral and is soft to Shadow.
Carrying a second school is how you answer a room that walls off your first.

### Levels

Spells and boons can both be taken repeatedly.

- **Spells** level up on the shrine pedestal. A spell you do not know opens the slot picker; one
  you already know levels on the spot. Levels raise the spell's own numbers via
  `LevelMultiplier`, shorten its cooldown to a floor of 60%, and often add something specific —
  Blink gains range, Chain Lightning gains targets, Cone of Cold gains chill stacks. Levels are
  keyed by spell id, so moving a spell between slots keeps its level.
- **Boons** are offered again while below `MaxLevel`. Each pick calls the boon's effect again
  with the new level, so effects apply their own per-level increment rather than recomputing a
  total. The card says whether it is a new pick or a level-up.

### Character sheet (`Assets/Scripts/Stats`)

Five core stats drive every derived number. Boons and status effects never touch the core
stats; they add flat/percent modifiers to the derived ones, so buffs stack predictably.

`final = (formulaBase + Σflat) × (1 + Σpercent)`, then clamped.

| Stat | Drives |
|---|---|
| **Strength** | Gun damage, melee bash damage, **smash power** (what you can break open) |
| **Intellect** | Spell power, cooldown rate, max mana and regeneration |
| **Agility** | Move speed, **jump height**, air control, dash charges, reload speed |
| **Vitality** | Max health and health regeneration |
| **Luck** | Crit chance and damage, and the rarity roll on boon offers |

Smashable objects have a `Hardness`. Bullets carry zero smash power, so a reinforced crate is
immune to gunfire and only opens to a Strength-backed bash or Kinetic Slam. Platform heights
straddle the 1.55 m base jump so investing in Agility genuinely opens routes.

### Status effects (`Assets/Scripts/Effects`)

Definitions are stateless singletons in `StatusLibrary`; per-entity state lives in
`ActiveStatus`. Each effect owns its stat modifiers and they are removed automatically when it
expires or restacks. Adding an effect is one class plus one line in the registry.

| Effect | Behaviour |
|---|---|
| **Chill** | Slows movement and attack speed, up to 5 stacks |
| **Freeze** | At max Chill: immobilised, and the next hit shatters for +50% |
| **Burn** | Fire damage over time; **melts Chill and Freeze on contact** |
| **Blight** | Poison damage over time and **absorbs healing** (−15% per stack) |
| **Shock** | Takes more damage from every source |
| **Weaken** | Deals less damage |
| **Haste** / **Fortify** | Player-side buffs from boons and Arcane Ward |
| **Mark** | The next hit lands for bonus damage, then it is consumed |

Chill and Burn cleanse each other, which gives the ice and fire builds a real interaction
rather than two identical damage-over-time effects.

### Weapons (`Assets/Scripts/Weapons`)

`WeaponDefinition` is plain data; `Weapon` runs rate of fire, magazines, reloads, spread and
recoil. Both delivery kinds share one code path.

- **Arcanum .38** — the starting hitscan semi-auto sidearm.
- **Ember Repeater** — full-auto fire projectiles that apply Burn.
- **Frost Lance** — heavy ice projectile, 2 stacks of Chill.
- **Voltaic Rail** — piercing hitscan rail, applies Shock.
- **Hexshot** — 9-pellet hitscan shotgun, applies Blight.
- **Sunder Cannon** — arcing projectile with splash.

Projectiles move by spherecast rather than physics, so fast rounds cannot tunnel through walls
and player and enemy shots behave identically.

### Abilities (`Assets/Scripts/Abilities`)

Player spells and enemy attacks are the same thing: an ordered chain of `AbilityEffect`s run
against a shared `AbilityContext`. Selectors write `Targets` and `Point`; the effects after
them act on whatever was selected.

```
Cone of Cold   = SelectCone -> StatusPayload(Chill) -> DealDamage -> VfxCone -> VfxShards
Frost Breath   = AimAtTarget -> TelegraphCone -> Wait -> StatusPayload(Chill)
                 -> SelectCone -> DealDamage -> VfxCone -> Wait
```

Three rules make it work:

- **One mutable context**, reused per caster, so effects hold no state of their own.
- **Effects can abort.** Returning false stops the chain and refunds the cast — that is how
  Blink declines to fire when there is a wall in front of you.
- **An escape hatch.** Chain Lightning loops with per-jump retargeting, and the sweeping beam
  is sustained state over time. Loops and time are control flow, which does not belong in a
  data chain, so those stay single bespoke effects. That is the system working, not failing.

Enemy attacks add a time dimension through `ITimedEffect`: `Wait` is the wind-up that makes an
attack readable, `Repeat` drives volleys and multi-stage slams. A wait aborts if the caster is
killed or frozen, so a well-timed Cone of Cold genuinely cancels a wind-up.

`SpawnProjectileEffect` carries an **`OnHit`** chain the projectile runs where it lands.
Firebolt uses it: the bolt itself deals no damage, and the blast is
`SelectSphere -> DealDamage(falloff) -> Vfx`.

### Spells (`Assets/Scripts/Spells`)

Two slots bound to `Q` and `E`. `SpellBook.SlotCount` and `SlotKeys` are the only things to
change when you want a third slot.

- **Blink** — short forward teleport. Capsule-sweeps the route so you never land inside a
  wall, gives 0.18 s of invulnerability, and refunds the cast if there is nothing but wall in
  front of you.
- **Cone of Cold** — a 34° freezing fan with line-of-sight checks, 2 stacks of Chill per hit.
  Saturating Chill freezes the target solid.
- Plus Firebolt, Chain Lightning, Arcane Ward and Kinetic Slam, found on shrine pedestals.

Spells raise a `SpellEvents.Cast` hook, which is how a boon like *Blink Detonation* attaches
an explosion to Blink without Blink knowing that upgrade exists.

### Enemies (`Assets/Scripts/Enemies`)

`EnemyController` holds spacing and steering; each attack is a separate component, so an
archetype is a body plus a list of attacks. No navmesh — rooms are open arenas and steering
with wall whiskers plus flock separation is enough.

| Archetype | Threat |
|---|---|
| **Cultist** | Arcing volley of slow, visible orbs |
| **Hound** | Fast melee with a 0.55 s wind-up that only tracks slowly — kite it |
| **Warden** | 1.1 s telegraphed firing line, then a sweeping beam |
| **Sentinel** | Telegraphed ground circles that land where you *were* |
| **Frostcaller** | Ice shards plus a telegraphed freezing cone |
| **Tower Warden** | The boss: all four patterns, wider and faster |

Every attack is dodgeable by construction: a projectile you can sidestep, a shape drawn on the
world before it fires, or a swing you can outrun. `Telegraph` draws the three warning shapes.

### Boons (`Assets/Scripts/Boons`)

29 boons across three rarities. They write into `RunState` or straight onto the character
sheet — nothing else needs to know they exist. `RunState` owns the run-wide combat hooks
(on-hit payloads, lifesteal, on-kill effects), subscribing once to the global `Health.AnyDamaged`
and `Health.AnyDied` events instead of patching every weapon.

Adding a boon is normally one entry in `BoonLibrary.BuildPool()`.

## Where the content lives

There are no ScriptableObjects or prefabs, so nothing is edited in the Inspector. All content
sits in a few library files, each holding one list.

| To change | Edit |
|---|---|
| Loadouts (stats, gun, spells) | `Player/LoadoutLibrary.cs` for the code roster, or **Create Starting Loadout Assets** to edit them in the Inspector |
| Guns | `Weapons/WeaponLibrary.cs` → `BuildRoster()`; field meanings in `WeaponDefinition.cs` |
| Spells | `Spells/SpellLibrary.cs` — add to the `All` list, then write the class |
| Boons | `Boons/BoonLibrary.cs` → `BuildPool()` |
| Rarity odds and Luck scaling | `Core/Rarity.cs` → `BaseChance()` and `LuckScaling` |
| Damage schools | `Core/DamageTypes.cs` — add an enum member and a case |
| Enemy resistances and weaknesses | `Enemies/EnemyFactory.cs` → the `SetAffinity` calls |
| How fast spells level | `Spells/Spell.cs` → `GrowthPerLevel`, `MaxLevel`, `CooldownAtLevel` |
| Status effects | `Effects/StatusLibrary.cs` — defaults at the top, behaviour in the classes |
| Enemy health and damage | `Enemies/EnemyFactory.cs` — one `Build*` method per archetype |
| Difficulty per floor | `Enemies/EnemyFactory.cs` → `HealthScale` / `DamageScale` |
| Enemy attacks | `Enemies/EnemyFactory.cs` — each is an effect chain, same effects as spells |
| Ability effects | `Abilities/` — add an effect class, it serves spells and enemies at once |
| What spawns in each room | `Level/RoomBuilder.cs` → `PopulateRoom()`; sizes in `SizeFor()` |
| Crate drops | `Level/Props.cs` → `Smashable.DropReward()` |
| Room types, names and odds | `Level/TowerMap.cs` → `RollKind()` and `MakeNode()` |
| Floor count, boons offered | `Core/GameDirector.cs` — the two serialized fields at the top |
| What each stat buys | `Stats/CharacterSheet.cs` → `BaseValue()` |
| Movement feel | `Player/PlayerMotor.cs` — gravity, friction, dash fields |
| Colours | `Util/MaterialLibrary.cs` → `Palette` |

Adding a gun or a boon is a single entry in the relevant list; a new gun joins the world drop
pool automatically. Adding a spell takes two steps, the class and the registry line.

**Tuning without recompiling:** the `[SerializeField]` values on `PlayerMotor` and the enemy
attacks belong to objects built at runtime, so they only exist in Play mode. Enter Play, find
the object in the Hierarchy, adjust it live, then write the value you liked back into the
code — Play mode changes are discarded on stop.

## Layout

```
Assets/Scripts/
  Core/      Health, damage resolution, run state, the game loop, bootstrap
  Stats/     Character sheet and stat modifiers
  Effects/   Status effect framework and the effect roster
  Player/    Movement, look, combat input, rig assembly
  Weapons/   Weapon data, firing, projectiles
  Spells/    Spell framework, spell roster, spell book
  Enemies/   AI, attack modules, telegraphs, enemy construction
  Level/     Room generation, tower map, props and pickups
  Boons/     Boon definitions and the offer roll
  UI/        IMGUI HUD and screens
  Util/      Primitives, materials, layers, seeded RNG, procedural meshes
```

## Known simplifications

These are deliberate scaffolding, not oversights — each is a clean seam to replace.

- **UI is IMGUI (`OnGUI`).** Zero asset dependencies and fast to iterate, but not what you
  want to ship. `UIStyles` is the single place the look is defined; swap for uGUI or UI Toolkit.
- **No audio.** There is no sound at all yet.
- **Art is primitives.** `MaterialLibrary` and `Build` are the seam where real models go in.
- **Physics layers are set by index** (8–13) rather than named in ProjectSettings, so the
  project needs no editor setup. `Layers.cs` documents them.
- **No save/meta-progression.** A run is self-contained.
- **Enemy AI does not path around corners** — it steers and slides along walls, which is fine
  for open arenas and would not be for corridors.
