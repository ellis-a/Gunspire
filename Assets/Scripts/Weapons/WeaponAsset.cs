using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// An authored gun. Create these via <c>Gunspire -> Create Weapon Assets</c>,
    /// which writes one per built-in into <c>Assets/Resources/Weapons</c> where
    /// <see cref="WeaponLibrary"/> can find them.
    ///
    /// A weapon whose <see cref="WeaponDefinition.Id"/> matches a built-in replaces it; a new
    /// id is added to the roster and enters the drop pool at whatever rarity it declares. With
    /// no assets at all the game runs on the code roster, so a fresh clone needs nothing
    /// authored, and deleting the assets is the way back to defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Gunspire/Weapon")]
    public class WeaponAsset : ScriptableObject
    {
        public WeaponDefinition Definition = new WeaponDefinition();

        /// <summary>
        /// Guards the handful of fields where a zero typed into the Inspector would misbehave
        /// at runtime rather than just look wrong.
        /// </summary>
        private void OnValidate()
        {
            if (Definition == null) return;

            Definition.RoundsPerMinute = Mathf.Max(1f, Definition.RoundsPerMinute);
            Definition.MagazineSize = Mathf.Max(1, Definition.MagazineSize);
            Definition.PelletsPerShot = Mathf.Max(1, Definition.PelletsPerShot);
            Definition.BurstCount = Mathf.Max(1, Definition.BurstCount);
            Definition.ReloadTime = Mathf.Max(0.05f, Definition.ReloadTime);
            Definition.Range = Mathf.Max(1f, Definition.Range);
            Definition.ProjectileSpeed = Mathf.Max(1f, Definition.ProjectileSpeed);
            Definition.ProjectileRadius = Mathf.Max(0.01f, Definition.ProjectileRadius);
            Definition.ProjectileLifetime = Mathf.Max(0.1f, Definition.ProjectileLifetime);
        }
    }
}
