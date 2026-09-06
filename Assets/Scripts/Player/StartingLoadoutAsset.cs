using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// The opening kit, as an editable asset. Create one via
    /// <c>Wizard with a Gun -> Create Starting Loadout Asset</c>, which puts it in
    /// <c>Assets/Resources</c> where <see cref="StartingLoadout"/> can find it.
    ///
    /// If no asset exists the game falls back to the defaults in code, so a fresh clone still
    /// runs with nothing authored.
    ///
    /// Ids are plain strings for now and are not checked by the compiler. A wrong one is
    /// reported at startup rather than failing silently - see <see cref="StartingLoadout"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "StartingLoadout", menuName = "Wizard with a Gun/Starting Loadout")]
    public class StartingLoadoutAsset : ScriptableObject
    {
        [Header("Core stats")]
        public int Strength = 5;
        public int Intellect = 5;
        public int Agility = 5;
        public int Vitality = 5;
        public int Luck = 5;

        [Header("Equipment")]
        [Tooltip("Id from WeaponLibrary. This gun is also excluded from world drops.")]
        public string WeaponId = "arcanum";

        [Tooltip("Spell id per slot. Index 0 is Q, index 1 is E. Extra entries are ignored.")]
        public string[] SpellIdsBySlot = { "blink", "cone_of_cold" };

        public int StatFor(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return Strength;
                case StatType.Intellect: return Intellect;
                case StatType.Agility: return Agility;
                case StatType.Vitality: return Vitality;
                default: return Luck;
            }
        }
    }
}
