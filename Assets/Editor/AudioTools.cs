#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WizardGun.EditorTools
{
    /// <summary>
    /// Gun and impact sounds are synthesised from weapon stats rather than authored, so there
    /// are no assets to create here. These are the two things you actually need: a way to see
    /// why a gun sounds how it does, and a way to pick up a dropped-in clip without a restart.
    /// </summary>
    public static class AudioTools
    {
        [MenuItem("Wizard with a Gun/Log Sound Table")]
        public static void LogSoundTable()
        {
            var text = new System.Text.StringBuilder();
            text.AppendLine("Gun sounds are derived from damage, rate of fire and school. To replace one, "
                            + "drop an AudioClip named gun_<id> into Assets/Resources/"
                            + SoundLibrary.ResourceFolder + "/.");
            text.AppendLine();
            text.AppendLine("id                 school   recipe");

            System.Collections.Generic.IReadOnlyList<WeaponDefinition> all = WeaponLibrary.All;
            for (int i = 0; i < all.Count; i++)
            {
                WeaponDefinition w = all[i];
                text.AppendLine(string.Format("{0,-18} {1,-8} {2}",
                    w.Id, DamageTypes.Name(w.DamageType), SoundLibrary.DescribeWeaponSound(w)));
            }

            text.AppendLine();
            text.AppendLine("Other ids: " + SoundLibrary.ImpactId + " (plus one per school, e.g. "
                            + SoundLibrary.ImpactId + "_fire), " + SoundLibrary.HitConfirmId + ", "
                            + SoundLibrary.DryFireId + ".");

            Debug.Log(text.ToString());
        }

        [MenuItem("Wizard with a Gun/Reload Sounds")]
        public static void ReloadSounds()
        {
            SoundLibrary.Reload();
            Sfx.Reset();
            Debug.Log("Cleared the sound cache and the voice pool. Clips in Assets/Resources/"
                      + SoundLibrary.ResourceFolder + "/ will be picked up on the next sound played.");
        }
    }
}
#endif
