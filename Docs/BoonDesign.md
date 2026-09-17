# Boon design notes

Every boon here is built (see `BoonImplementation.md`), and every number is a placeholder. The spells in
`SpellDesign.md` are the reference for what each spell does; this doc only says how a boon changes it.

---

## Decided

- **A full redesign.** The boons are designed from scratch; the current roster carries nothing over by default.
- **School boons need the school.** A boon that belongs to a school is offered only while you hold at least one
  spell of that school.
- **Spell boons need the spell.** A boon that changes one spell is offered only while you hold that spell.
- **There will be a lot of boons.** No fixed count per school. Each school gets what its spells and mastery need.
- **Design first.** This doc is settled before any code.
- **"Floor" means a room.** Each room is a floor of the tower. The word keeps it apart from spell and boon levels.
- **A shop is coming.** The currency is shillings. Boons may earn or spend them.
- **Boons are kept.** Unequipping a spell or dropping a gun never removes a boon. It keeps working exactly as it
  says; it just has nothing to act on until you hold something it affects.

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
area can take three. Default caps: Common 5, Uncommon 3, Rare 2, Mythic 1, Legendary 1. Stat boons are the
exception: all of them cap at 5.

### Proposed defaults, still open

- **Held means equipped**, not merely known, matching how masteries count.
- **An offer draws a family first, then a rarity, then a boon.** Weights before normalising over families that
  have anything to offer: Core 30, Arsenal 20, Slots 10, School 15, Spell 25. A shrine's spell and a boon offer
  can never be the same thing, so this does not touch spell offers.

---

## Boons

### Core

**Stat boons.** Every stat boon caps at five levels, whatever its rarity. With 26 of them there is always
another to take.

- **Single-stat boons** (Common) raise one core stat by two.
- **Paired boons** (Common) raise two stats by one each. Every pair of stats has its own boon.
- **Triple boons** (Uncommon) raise three stats by one each. Every set of three has its own boon.
- **Major paired boons** (Rare) raise two stats by three each. Every pair of stats has its own boon.
- **Ascendant** (Mythic) raises every stat by five.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Nimble | Common | 5 | +2 Dexterity: tighter bullet spread, less recoil, faster reloads |
| Arcane | Common | 5 | +2 Power: more spell and status power, more maximum mana |
| Swift | Common | 5 | +2 Athletics: faster cooldowns, movement, jumps and air control |
| Tough | Common | 5 | +2 Endurance: more maximum health and mana regeneration |
| Lucky | Common | 5 | +2 Luck: more critical hits, and rarer things on offer |
| Agile | Common | 5 | +1 Dexterity, +1 Athletics |
| Hardy | Common | 5 | +1 Athletics, +1 Endurance |
| Steadfast | Common | 5 | +1 Endurance, +1 Power |
| Blessed | Common | 5 | +1 Power, +1 Luck |
| Gambler | Common | 5 | +1 Luck, +1 Dexterity |
| Veteran | Common | 5 | +1 Dexterity, +1 Endurance |
| Spellslinger | Common | 5 | +1 Dexterity, +1 Power |
| Dynamo | Common | 5 | +1 Athletics, +1 Power |
| Daring | Common | 5 | +1 Athletics, +1 Luck |
| Survivor | Common | 5 | +1 Endurance, +1 Luck |
| Commando | Uncommon | 5 | +1 Dexterity, +1 Athletics, +1 Endurance |
| Spellblade | Uncommon | 5 | +1 Dexterity, +1 Athletics, +1 Power |
| Rogue | Uncommon | 5 | +1 Dexterity, +1 Athletics, +1 Luck |
| Warden | Uncommon | 5 | +1 Dexterity, +1 Endurance, +1 Power |
| Outlaw | Uncommon | 5 | +1 Dexterity, +1 Endurance, +1 Luck |
| Trickster | Uncommon | 5 | +1 Dexterity, +1 Power, +1 Luck |
| Champion | Uncommon | 5 | +1 Athletics, +1 Endurance, +1 Power |
| Adventurer | Uncommon | 5 | +1 Athletics, +1 Endurance, +1 Luck |
| Wanderer | Uncommon | 5 | +1 Athletics, +1 Power, +1 Luck |
| Chosen | Uncommon | 5 | +1 Endurance, +1 Power, +1 Luck |
| Acrobat | Rare | 5 | +3 Dexterity, +3 Athletics |
| Mercenary | Rare | 5 | +3 Dexterity, +3 Endurance |
| Hexslinger | Rare | 5 | +3 Dexterity, +3 Power |
| Sharpshooter | Rare | 5 | +3 Dexterity, +3 Luck |
| Titan | Rare | 5 | +3 Athletics, +3 Endurance |
| Tempest | Rare | 5 | +3 Athletics, +3 Power |
| Daredevil | Rare | 5 | +3 Athletics, +3 Luck |
| Archon | Rare | 5 | +3 Endurance, +3 Power |
| Diehard | Rare | 5 | +3 Endurance, +3 Luck |
| Prophet | Rare | 5 | +3 Power, +3 Luck |
| Ascendant | Mythic | 5 | +5 to every stat |

**Offer boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Rewarded | Uncommon | 3 | You can pick one more boon from each offer, per level |
| Fickle Fate | Rare | 2 | You can reroll each offer once per level |

**Damage boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Super Soldier | Common | 5 | More gun damage |
| Archmage | Common | 5 | More spell damage |
| Charge Up | Rare | 1 | Double damage on the next floor. The boon is lost afterwards, and can only ever be taken once |
| Kinetic Enhancer | Common | 3 | More kinetic damage |
| Energy Enhancer | Common | 3 | More energy damage |
| Necrotic Enhancer | Common | 3 | More necrotic damage |
| Psychic Enhancer | Common | 3 | More psychic damage |
| Executioner | Common | 5 | More damage to enemies below 30% health |
| Big Game | Common | 5 | More damage to elites and bosses |
| Brawler | Common | 5 | More damage to enemies within 6 m |
| Longshot | Common | 5 | More damage to enemies beyond 20 m |
| Momentum | Common | 5 | More damage while moving |
| High Ground | Common | 5 | More damage while in the air |
| Blasphemous Act | Rare | 2 | You deal X% more damage for every enemy that has died on the current floor |
| Kill Streak | Common | 3 | A kill within 3 seconds of another grants a short, stacking damage bonus |

- **Brawler and Longshot** leave the ground between 6 m and 20 m untouched, so neither rewards every fight.

**Mobility boons.** Any build, with no movement spell needed.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Acrophobia | Rare | 1 | You can jump again in mid-air |

**Recovery boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Treasure Hunter | Common | 3 | Health and mana orbs restore more |
| Rest a Moment | Common | 3 | Heal a small amount when you finish a floor |
| Vampire Sight | Uncommon | 1 | Killed enemies drop a minor health orb |
| Demon Sight | Uncommon | 1 | Killed enemies drop a minor mana orb |
| Scavenger | Common | 3 | Enemies drop health and mana orbs more often |
| Bottled Orb | Rare | 2 | Health orbs collected at full health are stored, one per level, and used automatically when you drop low |
| Magnetism | Mythic | 1 | Orbs fly to you from anywhere on the floor, and each one also restores a little of the other resource |

- **Minor orbs** restore half what a regular orb does, and vanish after 10 seconds if not picked up.
- **Vampire Sight and Demon Sight** drop an orb on every kill, whoever or whatever made it: minions and burns
  count.
- **Rest a Moment** heals when you step through the exit.

**Shop boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Haggler | Common | 3 | Shop prices are lower |
| Check the Storage | Common | 3 | Shops have one more item to choose from, per level |
| Pocket Change | Common | 3 | Kills drop more shillings |
| Pickpocket | Common | 3 | Melee hits knock shillings loose |
| Hoarder | Rare | 1 | Every 100 unspent shillings gives +1 Luck |

**Familiars.** Each grants one familiar, which returns at the start of every floor.

- **One familiar at a time.** Once you have one, no more familiar boons are offered.
- **Familiarity** lifts the limit by one.
- **Familiars are strong.** With only one allowed, taking one should feel like a real choice, not a small extra.
- **A new roster.** The four familiars in the game today (Arcane Wisp, Mender, Fel Imp and Rime Watcher) are all
  removed. The Wisp replaces the Mender as the healer, and the Rime Watcher below is built fresh as a new
  familiar rather than kept.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Wisp | Uncommon | 1 | A wisp that heals you |
| Imp | Uncommon | 1 | An imp that shoots fireballs at enemies, dealing energy damage |
| Crow | Uncommon | 1 | A crow that swoops at enemies, dealing kinetic damage |
| Bone Moth | Uncommon | 1 | A moth that bites enemies for necrotic damage and weakens them |
| Seer | Uncommon | 1 | An eye that zaps the enemy nearest your crosshair for psychic damage and marks it, so your next hit deals bonus damage |
| Storm Sprite | Uncommon | 1 | A sprite that arcs lightning between nearby enemies, shocking them |
| Rime Watcher | Uncommon | 1 | A watcher that freezes the ground under enemies, slowing them |
| Aegis Mote | Uncommon | 1 | A mote that orbits you and blocks an enemy projectile every few seconds |
| Magpie | Uncommon | 1 | A magpie that fetches health and mana orbs for you |
| Mana Sprite | Uncommon | 1 | A sprite that restores your mana while enemies are near |
| Coin Imp | Uncommon | 1 | An imp that raises your Luck while it lives |
| Homunculus | Uncommon | 1 | A homunculus that enemies attack instead of you. The only familiar enemies target. It has 100 health, and if killed it stays dead until the next floor |
| Gremlin | Uncommon | 1 | A gremlin that reloads your holstered gun |
| Familiarity | Rare | 1 | You can have one more familiar |

**Minion boons.**

| Boon | Rarity | Levels | Gate | Effect |
|---|---|---|---|---|
| Master Summoner | Uncommon | 3 | a summon spell, or the Bestial mastery | Your summoned minions have more health and deal more damage |
| Beetle Swarm | Rare | 1 | none | A tiny beetle spawns every 5 seconds and attacks enemies in melee. Each dies after 15 seconds |
| Blood Pact | Rare | 2 | a summon spell, or the Bestial mastery | Damage your minions deal heals you for a small share of it |

- **Summon spells** for the gate: Raise Dead, Stitched Monstrosity, Eye of E'pheraxx, Phantasmal Mimic and
  Apocalypse.
- **What it strengthens:** everything fighting for you. Walking minions, including Apocalypse's plague zombies,
  the Bestial companion, familiars, charmed enemies, Soul Slave's ghosts, and Beetle Swarm's beetles.
- **Beetle Swarm's beetles** count as minions, so Master Summoner and Blood Pact apply to them, and Blood Pact's
  gate is met by holding Beetle Swarm.

**Combat boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| First Blood | Rare | 1 | Damage you deal to an enemy at full health is doubled |
| Panic Barrier | Rare | 2 | Taking damage makes you immune to damage for 0.5 seconds per level |
| Resilience | Common | 5 | Debuffs on you are 10% less effective per level |
| Hard Skin | Common | 5 | You take a little less damage from every hit |
| Padding | Common | 3 | You start each floor with a small shield |
| Rhythm | Common | 3 | Kills shorten your spell cooldowns a little |
| Cool Head | Common | 3 | Spell cooldowns run faster while you are at full health |
| Cornered | Common | 3 | You take less damage while three or more enemies are near you |
| Every Reaction | Rare | 1 | Damage you take is dealt back to whoever dealt it, as psychic damage |
| Cold Blood | Rare | 1 | At full health, your critical hit chance is doubled |
| Monarch | Rare | 1 | Faster cooldowns and mana regeneration. Taking damage loses the effect until the enemy that dealt it dies |
| Mana Shield | Rare | 1 | Half of the damage you take drains mana instead. Your maximum health is halved |
| Cocky | Rare | 1 | Your maximum health becomes 1 and cannot be raised. You deal triple damage |
| Phylactery | Rare | 1 | Survive one killing blow per run, returning to half health. Can only ever be taken once |
| Pinball Wizard | Uncommon | 3 | Your knockback throws enemies further and its impacts deal more damage, and knocked enemies knock back what they hit |
| Glass Soul | Mythic | 1 | Your health is set to 1, but you gain a shield that fully refills after 3 seconds without taking damage |
| Last Stand | Mythic | 1 | Below 25% health, cooldowns run three times as fast and you deal 50% more damage |
| Juggernaut | Mythic | 1 | You cannot be knocked back, slowed or stunned, but you move 15% slower |
| Vendetta | Mythic | 1 | The enemy that last hurt you is marked, and killing it heals you for a quarter of your health |

- **First Blood** counts only your own hits: minions and status ticks such as burns do not. Elites and bosses
  count. When several pellets land at once, only the first is doubled, since the enemy is no longer at full
  health after it.
- **Resilience** weakens only debuffs, never buffs. Five levels halve them.
  - A debuff that deals damage deals 10% less per level. Poison's damage is reduced but not its sway, and bleed
    only takes the damage reduction, since it never wears off.
  - Frost and shock apply 10% fewer stacks per level.
  - Any other debuff lasts 10% less per level.
- **Pinball Wizard** builds on how knockback already works: a knocked body that hits another damages both and
  hands on half its speed, so impacts already cascade. The hand-off stays at half: the boon makes the first
  knockback stronger, and the chain carries that further because each hand-off starts from more. It only
  affects knockback you cause, including the knockback your knocked enemies pass on; enemies' own knockback is
  unchanged. The description's "knocked enemies knock back what they hit" tells the player about that existing
  chain rather than adding a new one.
- **Monarch** is lost to any damage that has an attacker, and returns once that enemy dies. If several enemies
  hurt you, all of them must die. Damage with no attacker does not break it.
- **Mana Shield** sends damage to mana only while you have mana; once it is empty, all damage goes to health.
- **Cocky** leaves health boons with nothing to raise; they should not be offered while you hold it.
- **Every Reaction** only reflects direct hits from an enemy. Damage over time (burn, bleed, poison and the
  like), hazards, and anything without an attacker, such as the Blood Debt, are not reflected.
- **Panic Barrier** is set off by any damage, ticks included. The immunity is its own timer: it cannot set itself
  off again until the immunity ends and damage lands again.

**World boons.** They change the enemies rather than you.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Ghostrealm | Mythic | 1 | Every enemy is Ethereal at all times: immune to kinetic damage, and everything else hits it twice as hard |
| Doomed | Mythic | 1 | At the start of each floor, five random non-elite enemies are death-marked. The mark never wears off |
| Otherworldly Beauty | Rare | 1 | The first enemy to see you on each floor is charmed |
| Courageous | Rare | 1 | Enemies are stronger, and your Luck is significantly higher |

- **Ghostrealm's price is intended.** Kinetic guns and spells do nothing under it. Taking it means building
  around it.
- **Otherworldly Beauty** charms the first enemy on each floor whose vision cone you enter. Noticing you some other
  way does not count. Elites and bosses cannot be charmed, and the boon waits for the first enemy it can charm
  rather than being used up on one. If the charmed enemy was the last left, the floor clears at once.
- **Doomed** marks every non-elite when a floor has fewer than five. Only enemies present when the floor starts
  are eligible: split copies and summoned enemies are never marked.

### Arsenal

**Weapon classes.** Every gun belongs to one class. A class boon is offered only while you carry a gun of that
class, the same way a school boon needs the school.

| Class | What marks it | Guns today |
|---|---|---|
| Handgun | Semi-auto, one round per shot, small magazine, quick to handle | Arcanum .38, Revolver |
| SMG | Full auto, light rounds, large magazine, loose spread | SMG, Emberspit, Ember Repeater |
| Shotgun | Several pellets per shot, short range | Shotgun, Hailmaker, Hexshot |
| Rifle | Auto or burst, accurate at mid range | Trigram, Nightfall |
| Sniper | Slow, heavy single shots, no spread, long range, usually piercing | Frost Lance, Voltaic Rail, Requiem |
| Heavy | Huge magazine, long reload, spin-up or weight | Minigun |
| Launcher | Explosive or lobbed rounds with splash | Rocket Launcher, Knell, Sunder Cannon |

- **Heavy** covers LMGs and miniguns; the Minigun is its only gun for now.
- **Rifle** is thin too. Nightfall is an automatic rifle that fires projectiles, which is why it sits here rather
  than with the SMGs.

**Class boons.**

| Boon | Rarity | Levels | Class | Effect |
|---|---|---|---|---|
| Quickdraw | Common | 3 | Handgun | Swapping to a handgun is instant, and its first shot deals more damage |
| Sidearm | Common | 3 | Handgun | Handguns reload faster |
| Spray and Pray | Common | 3 | SMG | SMGs lose less accuracy while moving |
| Extended Drum | Common | 3 | SMG | SMG magazines hold more rounds |
| Tight Choke | Common | 3 | Shotgun | Shotguns have less spread |
| Point Blank | Common | 3 | Shotgun | Shotgun pellets deal more damage within 5 m |
| Marksman | Common | 3 | Rifle | Rifles have less recoil |
| Closing Round | Common | 3 | Rifle | The last round of each burst deals more damage |
| Steady Breath | Common | 3 | Sniper | Aiming down the scope zooms further and steadies faster |
| Overpenetration | Common | 3 | Sniper | Sniper rounds pierce one more enemy |
| Warm Barrel | Common | 3 | Heavy | Heavy guns spin up faster |
| Belt Fed | Common | 3 | Heavy | Heavy guns reload faster |
| Bigger Boom | Common | 3 | Launcher | Launcher explosions have a larger radius |
| Hot Load | Common | 3 | Launcher | Launcher projectiles fly faster |
| Dead Man's Hand | Rare | 1 | Handgun | Each handgun hit in a row deals more damage; a miss resets it |
| Tail End | Rare | 1 | SMG | The last quarter of an SMG magazine deals double damage |
| Flechette | Rare | 1 | Shotgun | Shotgun pellets pierce one enemy |
| Pinpoint | Rare | 1 | Rifle | Every third rifle hit on the same enemy is a critical hit |
| Quickscope | Rare | 1 | Sniper | A sniper shot within 0.3 seconds of aiming down the scope deals double damage |
| Planted | Rare | 1 | Heavy | While you stand still, heavy guns have no spread and never spin down |
| Rocket Jump | Rare | 1 | Launcher | Your own launcher explosions launch you, without hurting you |

**Enchantments.** Your guns apply a status on hit. One boon per status; any number of them can be held together.

- **Amount applied = round damage × the boon's multiplier × its level.** Each boon has its own multiplier, tuned
  by hand. With Burn's multiplier at 3, level 3 turns 1 damage into 9 burn.
- **Frost** applies that amount as frost buildup.
- **Shock** works differently: only the first round after a reload applies it, to whatever that round hits. A
  miss wastes it.
- **Weaken** applies that amount as weaken strength, up to a cap.
- **Shotguns:** each pellet applies its own share independently.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Burn Enchantment | Uncommon | 3 | Rounds apply burn |
| Bleed Enchantment | Uncommon | 3 | Rounds apply bleed |
| Poison Enchantment | Uncommon | 3 | Rounds apply poison |
| Torment Enchantment | Uncommon | 3 | Rounds apply torment |
| Shock Enchantment | Uncommon | 3 | Rounds apply shock |
| Frost Enchantment | Uncommon | 3 | Rounds apply frost |
| Weaken Enchantment | Uncommon | 3 | Rounds apply weaken, so enemies deal less damage |
| Hex Enchantment | Uncommon | 3 | Rounds apply hex |
| Volatile Enchantment | Uncommon | 3 | Rounds apply volatile |
| Gilded Enchantment | Uncommon | 3 | Rounds apply gilded |
| Dread Enchantment | Uncommon | 3 | Rounds apply dread |

**Handling.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Bigger Bullets | Common | 3 | Critical hits deal more damage |
| Mag Dimension | Uncommon | 2 | Magazines hold 50% more rounds, rounded down |
| Lock and Load | Uncommon | 1 | Kills put a round back in the magazine |
| Quick Hands | Common | 3 | Swapping guns is faster |
| Fresh Mag | Common | 3 | The first round after a reload deals more damage |
| Gunmage | Mythic | 1 | While you have mana, rounds cost mana instead of ammo, so you never need to reload |
| Third Hand | Mythic | 1 | You can carry a third gun |
| Bifurcator | Rare | 1 | Each shot also fires a second round with much worse accuracy |
| Magnetised Ammo | Rare | 1 | Projectile rounds curve towards enemies |
| Mirror Barrel | Mythic | 1 | Your holstered gun fires alongside the one in your hands, at half damage |

**Headshots.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Birthday Party | Mythic | 1 | Headshots explode, dealing heavy damage in an area. The target does not have to die |

**On-hit boons.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Chain Static | Uncommon | 3 | A gun hit sends a bolt of lightning to an enemy near the one you hit, dealing energy damage |
| Concussive | Common | 3 | Gun hits add a little knockback |

### Slots

**Cast slots.** Each levels up a slot rather than a spell: whatever spell is bound to that key is cast one level
higher, and a spell moved out of the slot loses the bonus.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Quiss | Uncommon | 1 | The spell on Q is one level higher |
| Esarl | Uncommon | 1 | The spell on E is one level higher |
| Fex | Uncommon | 1 | The spell on F is one level higher |
| Spell Magazine | Mythic | 1 | Firing the last round in a magazine casts the spell on Q for free |
| Twincast | Mythic | 1 | Each spell cast has a 25% chance to cast twice |

**Movement.** Offered once a spell is in the movement slot.

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Afterimage | Mythic | 1 | Your movement spell leaves a decoy behind that enemies attack, and it explodes when destroyed |
| Tailwind | Rare | 1 | Kills reset your movement spell's cooldown |

**Melee.**

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Gorelust | Uncommon | 3 | Melee damage heals you for a share of the damage dealt |

### Schools

Every school has the same three Commons under its own names (damage, cost and cooldown for that school's
spells), plus Commons on its own mechanics, then Uncommons. All are offered only while you hold a spell of the
school. Commons cap at five levels, Uncommons at three and Rares at two. X and Y mark numbers still to set.

### Elemental

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Kindling | Common | 5 | Elemental spells deal X% more damage |
| Conduit | Common | 5 | Elemental spells cost X% less mana |
| Squall | Common | 5 | Elemental spells recover X% faster |
| Stoked | Common | 5 | Elemental spells apply X% more burn |
| Deep Freeze | Common | 5 | Elemental spells apply X% more frost stacks |
| Overcharge | Common | 5 | Elemental spells apply X% more shock stacks |
| Tri-Attuned | Uncommon | 3 | Elemental spells deal X% more damage for each different element already on the target |
| Excess Force | Uncommon | 3 | When an enemy dies with burn, frost or shock still on it, all of it jumps to a nearby enemy. Each level extends the jump's range |
| Fusion | Rare | 2 | A Conflux reaction no longer removes the elements that caused it, so the same enemy can react again |
| Imbued Rounds | Rare | 2 | Your gun hits apply the element of the last Elemental spell you cast, so guns can set off reactions |

### Bestial

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Savagery | Common | 5 | Bestial spells deal X% more damage |
| Lean Hunt | Common | 5 | Bestial spells cost X% less mana |
| Wild Pace | Common | 5 | Bestial spells recover X% faster |
| Well Fed | Common | 5 | Your companion has X% more health |
| Sharp Teeth | Common | 5 | Your companion deals X% more damage |
| Fleet Paws | Common | 5 | Your companion moves X% faster |
| Pack Tactics | Uncommon | 3 | You deal X% more damage to the enemy your companion is attacking |
| Vengeful Rage | Uncommon | 3 | When your companion dies, you deal 100% more damage per level for 10 seconds |
| Shared Instinct | Rare | 2 | Your companion's attacks carry your gun enchantments |
| Blooded | Rare | 2 | Kills by your companion shorten your Bestial cooldowns |

### Abyssal

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Crushing Depths | Common | 5 | Abyssal spells deal X% more damage |
| Shallow Wounds | Common | 5 | Abyssal spells cost X% less health |
| Riptide | Common | 5 | Abyssal spells recover X% faster |
| Thick Blood | Common | 5 | +X maximum health |
| Low Interest | Common | 5 | The Blood Debt charges X% less interest |
| Deep Pockets | Common | 5 | Kills repay X more debt |
| Riding the Current | Uncommon | 3 | Move faster the deeper you are in debt: +X% speed for every Y debt |
| Hemorrhage | Uncommon | 3 | Abyssal spells apply bleed |
| Foreclosure | Rare | 2 | Paying your debt off to zero makes the killing blow explode, dealing damage equal to what you repaid |
| Tidal Surge | Rare | 2 | Repaying debt sends out a wave around you that knocks enemies back |

### Divination

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Zealotry | Common | 5 | Divination spells deal X% more damage |
| Grace | Common | 5 | Divination spells cost X% less mana |
| Devotion | Common | 5 | Divination spells recover X% faster |
| Farsight | Common | 5 | Divine Knowledge reaches X% further |
| Lingering Light | Common | 5 | Divination zones and buffs last X% longer, such as Consecrate, Foretell and Path of Light |
| Clear Sight | Common | 5 | Enemies' attack timers show X seconds earlier |
| Guided Shots | Uncommon | 3 | Headshots on an enemy you hit with a Divination spell in the last Y seconds deal X% more damage |
| Blinding Light | Uncommon | 3 | Divination spells blind what they hit |
| Perfect Timing | Rare | 2 | Using your movement spell just before an enemy attack lands refunds it and blinds the attacker |
| Prophecy | Rare | 2 | Each enemy's first attack on you each floor misses |

### Death

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Grave Rites | Common | 5 | Death spells deal X% more damage |
| Pauper's Grave | Common | 5 | Death spells cost X% less mana. Soul costs are unchanged |
| Restless Dead | Common | 5 | Death spells recover X% faster |
| Soul Jar | Common | 5 | +1 soul cap per level |
| Bone Density | Common | 5 | Your undead have X% more health: zombies, the monstrosity and plague zombies |
| Grave Hunger | Common | 5 | Kills have an X% chance to leave an extra soul |
| Enfeeble | Uncommon | 3 | Death spells weaken what they hit |
| Soul Slave | Uncommon | 3 | Spending a soul summons a short-lived ghost that flies at an enemy and explodes, dealing necrotic damage around it. Master Summoner strengthens the ghost |
| Soul Shield | Rare | 2 | Once per floor, a killing blow consumes a soul instead, if you have one. The blow is cancelled entirely, so you keep the health you had. Triggers before Phylactery |
| Overflowing Souls | Rare | 2 | A soul gained while you are at the soul cap explodes where the enemy died |

### Psionic

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Psychic Pressure | Common | 5 | Psionic spells deal X% more damage |
| Lucid Mind | Common | 5 | Psionic spells cost X% less mana |
| Racing Thoughts | Common | 5 | Psionic spells recover X% faster |
| Keen Mind | Common | 5 | Gun hits build X% more psi |
| Sharpened Will | Common | 5 | Empowered melee deals X more damage |
| Lingering Doubt | Common | 5 | Your confusion, fear and torment last X% longer |
| Overflow | Uncommon | 3 | Spending psi makes your guns deal X% more damage for Y seconds |
| Fractured Mind | Uncommon | 3 | Confused enemies take X% more damage from you |
| Psychic Wave | Rare | 2 | Empowered melee also sends a blade wave forward that damages everything in its path |
| Thrown Blade | Rare | 2 | Melee with full charges throws a psi blade instead, spending every charge on one ranged hit |

### Aetherics

| Boon | Rarity | Levels | Effect |
|---|---|---|---|
| Void Edge | Common | 5 | Aetherics spells deal X% more damage |
| Folded Mana | Common | 5 | Aetherics spells cost X% less mana |
| Borrowed Seconds | Common | 5 | Aetherics spells recover X% faster |
| Wide Void | Common | 5 | Arcane Warp grants X% more power from missing mana |
| Aether Tap | Common | 5 | Gun hits restore X more mana |
| Deep Reservoir | Common | 5 | +X maximum mana |
| Empty Vessel | Uncommon | 3 | Move faster the emptier your mana: up to +X% speed at zero mana |
| Displacement | Uncommon | 3 | Aetherics spells expose what they hit, so it takes more damage |
| Void Pocket | Rare | 2 | Arcane Warp's bonus lingers for five seconds after your mana refills |
| Void Rounds | Rare | 2 | Below half mana, your gun rounds pierce |

### Petty

### Pacts

---

## New statuses

Statuses the boons need that the game does not have yet.

**Charmed** - the enemy fights for you until the end of the floor.

- It follows you and fights like a minion, and enemies attack it as they would a minion.
- It counts as defeated for the enemies-remaining counter.
- Elites and bosses cannot be charmed. Charm has no effect on the player or their minions.
- Applied by Otherworldly Beauty.

**Hex** - stored damage, paid out when it ends.

- While hexed, the enemy stores a share of all damage it takes. When the hex wears off, it takes the stored
  amount as psychic damage.
- Each application raises the share and refreshes the timer.
- If the enemy dies first, the stored damage is lost.
- Applied by Hex Enchantment.

**Volatile** - builds up, then explodes.

- The amount applied fills a buildup. At full, the enemy explodes, dealing energy damage to enemies around it,
  and the buildup resets.
- The explosion does not hurt the player or their minions, and does not add volatile to what it hits.
- Applied by Volatile Enchantment.

**Gilded** - drops shillings on death.

- The amount applied adds to the shillings the enemy drops when it dies, up to a cap per enemy.
- It never wears off.
- Applied by Gilded Enchantment.

**Dread** - builds up to fear.

- The amount applied fills a buildup that drains over time. At full, the enemy is feared, fleeing and unable to
  attack, and the buildup resets.
- Elites and bosses take longer to fill.
- Applied by Dread Enchantment. Fear itself is a planned status the game already names.

---

## Open questions

Numbers are placeholders that play will settle, so this list only tracks design decisions. The roster is built
with a placeholder answer to each; `BoonImplementation.md` lists them under "Defaults for the open questions".

- **Offer weights.** Are Core 30, Arsenal 20, Slots 10, School 15, Spell 25 roughly right, or should a
  build's own spells come up more often?
- **Held or known.** Should school and spell boons follow what is equipped, or everything you have learned?
- **Levels.** Are the default caps per rarity right?
- **Shock Enchantment's amount.** Now that shock is counted in stacks, does the first round after a reload
  apply round damage × multiplier × level stacks, the same formula as the others?
- **Chain Static's scaling.** Does the bolt deal a flat amount or a share of the hit, and what does each level
  raise?
- **Chain Static on fast guns.** Every hit firing a bolt means a minigun or a shotgun's pellets throw a lot of
  them. Should it have a chance per hit or a short cooldown?
- **Chain Static's bolt.** Does it only jump to an enemy you did not just hit, and can a bolt set off
  enchantments or other on-hit boons, or only the gun's own rounds?
- **Birthday Party's blast.** Can it hurt you? Does each pellet of a shotgun headshot explode?
- **Quiss, Esarl and Fex past the cap.** Can a slot's bonus lift a spell above its maximum level?
- **Lock and Load.** Any kill, or only kills by your gun? And does the round go to the gun in hand?
- **Bigger Bullets.** Guns only, or spell critical hits as well, despite the name?
- **Mag Dimension's second level.** +100% of the base magazine, or 50% more again (2.25 times)?
- **Familiarity's gate.** Only offered once you already have a familiar, since it does nothing otherwise?
- **Low Interest.** The Blood Debt's interest is extra healing on repayment, so charging less of it would be a
  downside. Should Low Interest raise the interest instead, or mean something else?
- **Riding the Current's ceiling.** Does the speed keep climbing with debt, or stop at a cap?
- **Overflow's timer.** Does spending psi again while it runs refresh the timer, or add to the bonus?
- **Enfeeble, Hemorrhage, Blinding Light and Displacement.** What does each level raise? And blind on every
  Divination hit is strong control: does Blinding Light want a cooldown per enemy?
- **What a Rare school boon's second level does.** Fusion, Shared Instinct, Foreclosure, Perfect Timing, Soul
  Shield, Psychic Wave and Void Pocket change how something works rather than raising a number. Should each be a single level, or what does
  the second level raise?
- **Perfect Timing's refund.** For Dash, is that a charge back; for a spell with a cooldown, the cooldown reset?
- **Spell Magazine's cast.** Does the free cast ignore the spell's cooldown as well as its cost? And is it always
  Q, or the last slot you cast from?
- **Glass Soul and maximum health.** With health fixed at 1, what happens to Tough and other health boons: do
  they grow the shield instead? And do health orbs refill the shield?
- **Juggernaut's reach.** Does it stop frost's slow and freeze too, or only snares and stuns?
- **Gunmage and mana guns.** Guns that already spend mana per shot: do they pay twice, or just their own cost?
- **Third Hand's swapping.** Does the swap key cycle through all three guns, and where does the third show on
  the HUD?
- **Excess Force's source.** Does it move burn, frost and shock from any source, or only what you applied?
- **Master Summoner and familiars.** It strengthens familiars, but owning one does not open its gate. Should it,
  or does Otherworldly Beauty count, since a charmed enemy is one more thing it strengthens?
- **Rewarded and offer size.** At three levels you can pick four boons, and an offer shows three today. Does
  the offer grow with it, say to always show two more than you can pick?

**How shock works, for reference.** Shock stacks without limit, and each stack makes the target take 1% more
damage from everything. A spell's shock applies 12 stacks by default, editable per spell on its asset, and
spell power scales the stack count. Rapid sources apply fewer: Hailstorm and Elemental Form's storm bullets
apply 6. Each application's stacks keep their own timer and fall off when it runs out, so steady fire holds a
level rather than climbing forever. Hearing falls by a quarter for every 12 stacks.
