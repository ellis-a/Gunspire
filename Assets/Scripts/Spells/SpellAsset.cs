using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// An authored spell. Create these via <c>Gunspire -> Create Spell Assets</c>,
    /// which writes one per built-in into <c>Assets/Resources/Spells</c> where
    /// <see cref="SpellLibrary"/> can find them.
    ///
    /// A spell whose <see cref="Spell.Id"/> matches a built-in replaces it; a new id joins the
    /// roster and starts appearing on shrine pedestals at whatever rarity it declares. With no
    /// assets the game runs on the code roster, so a fresh clone needs nothing authored.
    ///
    /// The effect chain is a <c>[SerializeReference]</c> list, so the Inspector gives you a
    /// type picker and every effect in the game is available from it.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell", menuName = "Gunspire/Spell")]
    public class SpellAsset : ScriptableObject
    {
        public Spell Definition = new Spell();

        /// <summary>
        /// Guards the fields where a zero typed into the Inspector misbehaves at runtime
        /// rather than merely looking wrong.
        /// </summary>
        private void OnValidate()
        {
            if (Definition == null) return;

            Definition.MaxLevel = Mathf.Max(1, Definition.MaxLevel);
            Definition.ManaCost = Mathf.Max(0f, Definition.ManaCost);
            Definition.Cooldown = Mathf.Max(0f, Definition.Cooldown);
            Definition.GrowthPerLevel = Mathf.Max(0f, Definition.GrowthPerLevel);
        }
    }
}
