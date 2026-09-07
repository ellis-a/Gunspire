using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// An authored enemy. Create these via <c>Wizard with a Gun -> Create Enemy Assets</c>,
    /// which writes one per built-in into <c>Assets/Resources/Enemies</c> where
    /// <see cref="EnemyLibrary"/> can find them.
    ///
    /// An enemy whose <see cref="EnemyDefinition.Id"/> matches a built-in replaces it; a new id
    /// joins the roster, and starts appearing in combat rooms if InStandardRoster is set. With
    /// no assets at all the game runs on the code roster.
    ///
    /// Each attack's Sequence takes any <see cref="AbilityEffect"/>, the same list player
    /// spells are built from, so a whole new enemy can be assembled without writing C# as long
    /// as the existing effects cover what it should do.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Wizard with a Gun/Enemy")]
    public class EnemyAsset : ScriptableObject
    {
        public EnemyDefinition Enemy = new EnemyDefinition();

        private void OnValidate()
        {
            if (Enemy == null) return;

            // Values that only misbehave later if they are zero. A CharacterController with no
            // height or radius fails at spawn, far from whoever typed the zero.
            if (Enemy.Health < 1f) Enemy.Health = 1f;
            if (Enemy.Radius < 0.05f) Enemy.Radius = 0.05f;
            if (Enemy.BodyHeight < 0.1f) Enemy.BodyHeight = 0.1f;
            if (Enemy.BodyWidth < 0.1f) Enemy.BodyWidth = 0.1f;

            // A flier below head height is not meaningfully flying; above the 11m walls it is
            // unreachable.
            if (Enemy.Flying) Enemy.HoverHeight = Mathf.Clamp(Enemy.HoverHeight, 1.5f, 9f);
        }
    }
}
