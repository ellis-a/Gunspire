# Wizard with a Gun

A single-player FPS roguelike in Unity. You are a wizard with a sidearm, fighting up through
an enemy wizard's tower. Every enemy attack is something you can move out of the way of, so
the game is about speed, spacing and reading wind-ups rather than trading damage.

Everything is generated from code — the level, the enemies, the weapons, the UI. There are no
prefabs, materials, meshes or scene assets to keep in sync, so the whole game is the C# in
`Assets/Scripts`.

## Running it

Requires **Unity 2022.3 LTS or newer**. Works on the Built-in render pipeline or URP: shaders
are looked up by name with fallbacks.

1. Open this folder in Unity Hub. (If Hub will not open it directly, create a new 3D project
   and copy `Assets/` into it — the project has no other dependencies.)
2. Press **Play** in any scene, including an empty one. `GameBootstrap` spawns itself via
   `[RuntimeInitializeOnLoadMethod]` and builds the game.
3. Optional: **Wizard with a Gun → Create Play Scene** makes a saved scene for build settings.

## Controls

| Input | Action |
|---|---|
| `WASD` | Move (Quake-style air control — strafing keeps momentum) |
| `Space` | Jump (coyote time + input buffering) |
| `Shift` | Dash (charges scale with Agility, brief invulnerability) |
| `LMB` | Fire |
| `RMB` / `V` | Melee bash (Strength-scaled, breaks reinforced objects) |
| `R` | Reload |
| `Q` / `E` | Spell slots |
| `F` | Interact (pickups, shrines, exit portal) |
| `Tab` | Character sheet (hold) |
| `Esc` | Pause |
| `1`–`3` | Pick a boon / route card |

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
