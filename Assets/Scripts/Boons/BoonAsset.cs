using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// An authored boon. Create these via <c>Wizard with a Gun -> Create Boon Assets</c>, which
    /// writes one per built-in into <c>Assets/Resources/Boons</c> where
    /// <see cref="BoonLibrary"/> can find them.
    ///
    /// A boon whose <see cref="Boon.Id"/> matches a built-in replaces it; a new id joins the
    /// pool. With no assets at all the game runs on the code roster, so a fresh clone plays
    /// with nothing authored.
    ///
    /// The Effects list takes any <see cref="BoonEffect"/>, so the Inspector's type picker is
    /// the full menu of what a boon can do - which means a new boon usually needs no C# at all.
    /// </summary>
    [CreateAssetMenu(fileName = "Boon", menuName = "Wizard with a Gun/Boon")]
    public class BoonAsset : ScriptableObject
    {
        public Boon Boon = new Boon();

        private void OnValidate()
        {
            // An Inspector is a new way to type a zero into a field that only misbehaves later.
            // A boon capped at zero levels can never be taken and would sit in the pool forever.
            if (Boon == null) return;
            if (Boon.MaxLevel < 1) Boon.MaxLevel = 1;
        }
    }
}
