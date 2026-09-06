using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// An authored loadout. Create these via
    /// <c>Wizard with a Gun -> Create Starting Loadout Assets</c>, which writes one per
    /// built-in into <c>Assets/Resources</c> where <see cref="LoadoutLibrary"/> can find them.
    ///
    /// A loadout whose <see cref="LoadoutDefinition.Id"/> matches a built-in replaces it; a new
    /// id is added to the selection screen. With no assets at all the game plays on the code
    /// roster, so a fresh clone still runs with nothing authored.
    ///
    /// Ids are plain strings and are not checked by the compiler. A wrong one is reported at
    /// startup rather than failing silently - see <see cref="StartingLoadout.ApplySpells"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Loadout", menuName = "Wizard with a Gun/Starting Loadout")]
    public class StartingLoadoutAsset : ScriptableObject
    {
        public LoadoutDefinition Loadout = new LoadoutDefinition();
    }
}
