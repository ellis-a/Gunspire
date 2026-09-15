using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Physics layers. These are assigned by index at runtime so the project needs no
    /// layer setup in ProjectSettings; only the built-in 0-7 range is named in the editor.
    /// <see cref="ConfigureCollisionMatrix"/> wires up what ignores what.
    /// </summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;

        public const int Player = 8;
        public const int Enemy = 9;
        public const int PlayerProjectile = 10;
        public const int EnemyProjectile = 11;
        public const int Prop = 12;          // smashables, pickups, decoration
        public const int Level = 13;         // floors and walls
        public const int Familiar = 14;      // the player's summons that fly
        public const int Minion = 15;        // the player's summons that walk: they block enemies, never the player
        public const int NetherWall = 16;    // stops shots from both sides, and nothing else
        public const int Smoke = 17;         // stops the player's shots, to hand them to someone inside
        public const int Hitbox = 18;        // enemy heads: struck by shots, solid to nothing

        public static readonly int WorldMask = (1 << Default) | (1 << Level) | (1 << Prop);
        public static readonly int BlockingMask = (1 << Default) | (1 << Level);

        public static readonly int PlayerHitMask =
            (1 << Default) | (1 << Level) | (1 << Prop) | (1 << Enemy) | (1 << NetherWall) | (1 << Smoke);

        // Familiars and minions are in the enemy's hit mask, which is how they can be killed: they
        // eat blasts and stand in beams like anything else.
        public static readonly int EnemyHitMask =
            (1 << Default) | (1 << Level) | (1 << Prop) | (1 << Player) | (1 << Familiar) | (1 << Minion)
            | (1 << NetherWall);

        public static readonly int EnemyMask = 1 << Enemy;
        public static readonly int PlayerMask = 1 << Player;
        public static readonly int FamiliarMask = 1 << Familiar;
        public static readonly int MinionMask = 1 << Minion;

        /// <summary>
        /// What an enemy ability is allowed to damage. Minions are here because enemies fight them:
        /// pulling aggro with a horde is intended, and a horde is meant to be a wall enemies chew
        /// through. Which target an enemy chooses is <see cref="TargetRegistry"/>'s business, and
        /// familiars do not register there, so they stay collateral rather than a distraction.
        /// </summary>
        public static readonly int EnemyTargetMask = PlayerMask | FamiliarMask | MinionMask;

        /// <summary>
        /// Attacks that belong to no side hit everyone: a confused enemy's shots land on its own
        /// kind, the player, familiars and minions alike.
        /// </summary>
        public static readonly int NeutralHitMask = WorldMask | PlayerMask | EnemyMask | FamiliarMask | MinionMask
                                                    | (1 << NetherWall);
        public static readonly int NeutralTargetMask = PlayerMask | EnemyMask | FamiliarMask | MinionMask;

        /// <summary>Layers a shot fired by the given team should be able to hit.</summary>
        public static int HitMaskFor(Team team)
            => team == Team.Player ? PlayerHitMask : team == Team.Enemy ? EnemyHitMask : NeutralHitMask;

        /// <summary>
        /// What a gun round or projectile can strike: the team's hit mask, plus head hitboxes for anyone who shoots
        /// enemies. Kept apart from <see cref="HitMaskFor"/>, which blasts and overlaps use, where a head would count
        /// as a second body.
        /// </summary>
        public static int ShotMaskFor(Team team)
            => team == Team.Enemy ? EnemyHitMask : HitMaskFor(team) | (1 << Hitbox);

        /// <summary>Only the hostile characters for the given team - no world geometry.</summary>
        public static int TargetMaskFor(Team team)
            => team == Team.Player ? EnemyMask : team == Team.Enemy ? EnemyTargetMask : NeutralTargetMask;

        /// <summary>The characters on the given team's own side, for zones that help allies.</summary>
        public static int AllyMaskFor(Team team)
            => team == Team.Player ? PlayerMask | FamiliarMask | MinionMask : team == Team.Enemy ? EnemyMask : 0;

        /// <summary>
        /// The layer a character body belongs on for a side. A body moved to the player's side goes
        /// on the minion layer rather than the player's own: enemy attacks hit it, it blocks enemies,
        /// the player's shots pass through it, and nothing looking for the player mistakes it for them.
        /// </summary>
        public static int BodyLayerFor(Team team) => team == Team.Player ? Minion : Enemy;

        /// <summary>Layers that block line of sight for either side. Nether Wall and smoke stay out of it.</summary>
        public static int SightBlockMask => BlockingMask | (1 << Prop);

        public static void ConfigureCollisionMatrix()
        {
            // Head hitboxes exist only to be shot. They collide with nothing, so a head never snags a doorway or
            // pushes against the body it sits on.
            for (int layer = 0; layer < 32; layer++) Physics.IgnoreLayerCollision(Hitbox, layer, true);

            // Projectiles never collide with each other or with their own side.
            Physics.IgnoreLayerCollision(PlayerProjectile, PlayerProjectile, true);
            Physics.IgnoreLayerCollision(EnemyProjectile, EnemyProjectile, true);
            Physics.IgnoreLayerCollision(PlayerProjectile, EnemyProjectile, true);
            Physics.IgnoreLayerCollision(PlayerProjectile, Player, true);
            Physics.IgnoreLayerCollision(EnemyProjectile, Enemy, true);

            // Enemies push through each other with steering, not rigidbody shoving.
            Physics.IgnoreLayerCollision(Enemy, Enemy, false);

            // A pet that shoves you off a ledge is worse than no pet, so familiars are solid
            // to the world and to enemies but pass straight through the player and each other.
            Physics.IgnoreLayerCollision(Familiar, Player, true);
            Physics.IgnoreLayerCollision(Familiar, Familiar, true);
            Physics.IgnoreLayerCollision(Familiar, PlayerProjectile, true);
            Physics.IgnoreLayerCollision(Familiar, Enemy, true);

            // Walking minions are a wall of bodies: solid to enemies and to each other, so a horde
            // blocks a doorway and does not stack, but never to the player. In a corridor you are
            // backing out of, a horde you could not pass would trap you.
            Physics.IgnoreLayerCollision(Minion, Player, true);
            Physics.IgnoreLayerCollision(Minion, PlayerProjectile, true);
            Physics.IgnoreLayerCollision(Minion, Familiar, true);
            Physics.IgnoreLayerCollision(Minion, Enemy, false);
            Physics.IgnoreLayerCollision(Minion, Minion, false);

            // Nether Wall and smoke stop shots through the hit masks. Nothing physical collides with
            // them, so bodies walk through and projectiles only stop where a hit mask says so.
            int[] volumes = { NetherWall, Smoke };
            int[] everythingElse =
            {
                Default, Player, Enemy, PlayerProjectile, EnemyProjectile, Prop, Level, Familiar, Minion,
                NetherWall, Smoke
            };

            for (int v = 0; v < volumes.Length; v++)
                for (int o = 0; o < everythingElse.Length; o++)
                    Physics.IgnoreLayerCollision(volumes[v], everythingElse[o], true);
        }

        public static void SetRecursively(GameObject go, int layer)
        {
            // A head hitbox keeps its own layer whichever side its body is moved to.
            go.layer = go.GetComponent<HeadHitbox>() != null ? Hitbox : layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
