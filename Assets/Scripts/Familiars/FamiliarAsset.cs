using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// An authored familiar. Create these via
    /// <c>Wizard with a Gun -> Create Familiar Assets</c>, which writes one per built-in into
    /// <c>Assets/Resources/Familiars</c> where <see cref="FamiliarLibrary"/> can find them.
    ///
    /// A new familiar needs a boon to grant it - see GrantFamiliarEffect - since nothing hands
    /// them out on its own.
    /// </summary>
    [CreateAssetMenu(fileName = "Familiar", menuName = "Wizard with a Gun/Familiar")]
    public class FamiliarAsset : ScriptableObject
    {
        public FamiliarDefinition Familiar = new FamiliarDefinition();

        private void OnValidate()
        {
            if (Familiar == null) return;

            if (Familiar.Health < 1f) Familiar.Health = 1f;
            if (Familiar.Radius < 0.05f) Familiar.Radius = 0.05f;
            if (Familiar.BodyDiameter < 0.05f) Familiar.BodyDiameter = 0.05f;

            // A familiar that engages from across the map stops reading as yours, and one
            // that engages from nowhere never fires at all.
            Familiar.EngageRange = Mathf.Clamp(Familiar.EngageRange, 2f, 40f);
        }
    }
}
