using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// One kind of walking minion, as data: a body, how it follows and fights, whether it carries
    /// between floors, and its attacks, which are the same <see cref="AttackDefinition"/>s enemies use.
    /// </summary>
    [System.Serializable]
    public class MinionDefinition
    {
        public string Id = "minion";
        public string DisplayName = "Minion";

        [Header("Body")]
        public float Health = 40f;
        public float MoveSpeed = 4f;
        public float Radius = 0.4f;
        public float BodyHeight = 1.6f;
        public float BodyWidth = 0.7f;
        public Color BodyColor = new Color(0.45f, 0.55f, 0.4f);
        public Color EyeColor = new Color(0.8f, 1f, 0.5f);

        [Header("Behaviour")]
        /// <summary>How close to the player's body it tries to stay when it has nothing to fight.</summary>
        public float FollowDistance = 3f;

        /// <summary>How far it looks for an enemy to fight.</summary>
        public float EngageRange = 12f;

        /// <summary>How close it gets to what it is fighting.</summary>
        public float PreferredRange = 1.4f;

        /// <summary>Never moves: turns to face what it fights, and nothing more. Eye of E'pheraxx, the Phantasmal Mimic.</summary>
        public bool Immobile;

        [Header("Life")]
        /// <summary>Above zero, it crumbles after this many world seconds. Plague zombies, summoned eyes.</summary>
        public float LifetimeSeconds;

        /// <summary>Follows the player to the next floor. Zombies do; plague zombies do not.</summary>
        public bool Persistent = true;

        /// <summary>
        /// Above zero, dying knocks it down for this long instead, and it gets back up. Whether the
        /// Bestial companion works this way is still open, so the framework takes either.
        /// </summary>
        public float ReviveSeconds;

        [Range(0.05f, 1f)] public float ReviveHealthFraction = 0.5f;

        [Header("Attacks")]
        public List<AttackDefinition> Attacks = new List<AttackDefinition>();

        public MinionDefinition Clone()
        {
            var copy = (MinionDefinition)MemberwiseClone();
            copy.Attacks = new List<AttackDefinition>();
            for (int i = 0; i < Attacks.Count; i++)
                if (Attacks[i] != null) copy.Attacks.Add(Attacks[i].Clone());
            return copy;
        }
    }
}
