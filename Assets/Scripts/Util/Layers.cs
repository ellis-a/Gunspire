using UnityEngine;

namespace WizardGun
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
        public const int Familiar = 14;      // the player's summons

        public static readonly int WorldMask = (1 << Default) | (1 << Level) | (1 << Prop);
        public static readonly int BlockingMask = (1 << Default) | (1 << Level);

        public static readonly int PlayerHitMask = (1 << Default) | (1 << Level) | (1 << Prop) | (1 << Enemy);

        // Familiars are in the enemy's hit mask, which is how they can be killed: nothing
        // deliberately targets them, but they eat blasts and stand in beams like anything else.
        public static readonly int EnemyHitMask =
            (1 << Default) | (1 << Level) | (1 << Prop) | (1 << Player) | (1 << Familiar);

        public static readonly int EnemyMask = 1 << Enemy;
        public static readonly int PlayerMask = 1 << Player;
        public static readonly int FamiliarMask = 1 << Familiar;

        /// <summary>
        /// What an enemy ability is allowed to damage. Familiars are included here but not in
        /// enemy target selection, so they are collateral rather than a distraction - a pet
        /// cannot be used to pull aggro off yourself.
        /// </summary>
        public static readonly int EnemyTargetMask = PlayerMask | FamiliarMask;

        /// <summary>Layers a shot fired by the given team should be able to hit.</summary>
        public static int HitMaskFor(Team team)
            => team == Team.Player ? PlayerHitMask : EnemyHitMask;

        /// <summary>Only the hostile characters for the given team - no world geometry.</summary>
        public static int TargetMaskFor(Team team)
            => team == Team.Player ? EnemyMask : EnemyTargetMask;

        /// <summary>Layers that block line of sight for either side.</summary>
        public static int SightBlockMask => BlockingMask | (1 << Prop);

        public static void ConfigureCollisionMatrix()
        {
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
        }

        public static void SetRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                SetRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
