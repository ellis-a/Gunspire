using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The familiar roster. Same hybrid as every other library: built-ins in code, any
    /// <see cref="FamiliarAsset"/> under a Resources folder merged over the top by id.
    /// </summary>
    public static class FamiliarLibrary
    {
        private static List<FamiliarDefinition> _all;

        public static IReadOnlyList<FamiliarDefinition> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static void Reload() => _all = null;

        /// <summary>Fresh copy, safe to mutate per summon.</summary>
        public static FamiliarDefinition Get(string id)
        {
            FamiliarDefinition found = Peek(id);
            return found != null ? found.Clone() : null;
        }

        public static FamiliarDefinition Peek(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        private static void Build()
        {
            _all = BuiltIn();

            FamiliarAsset[] authored = Resources.LoadAll<FamiliarAsset>("");
            if (authored == null) return;

            for (int i = 0; i < authored.Length; i++)
            {
                FamiliarDefinition def = authored[i] != null ? authored[i].Familiar : null;
                if (def == null) continue;

                if (string.IsNullOrEmpty(def.Id))
                {
                    Debug.LogWarning("Familiar asset \"" + authored[i].name + "\" has no Id and was ignored.");
                    continue;
                }

                int existing = _all.FindIndex(f => f.Id == def.Id);
                if (existing >= 0) _all[existing] = def;
                else _all.Add(def);
            }
        }

        /// <summary>The code roster. Also what the editor tool seeds new assets from.</summary>
        public static List<FamiliarDefinition> BuiltIn()
        {
            // Empty while the redesigned familiars in Docs/BoonDesign.md are built.
            return new List<FamiliarDefinition>();
        }
    }
}
