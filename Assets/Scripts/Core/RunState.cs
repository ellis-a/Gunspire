using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Everything that persists across the rooms of a single run: the seed, the route, the
    /// boons taken, and the on-hit statuses boons add to your bullets and spells.
    /// </summary>
    public class RunState
    {
        public int Seed;
        public Rng Rng;
        public TowerMap Map;
        public RoomNode CurrentNode;

        public int Floor = 1;
        public int RoomsCleared;
        public int Kills;
        public float ElapsedSeconds;

        public PlayerRig Player;

        /// <summary>A boon the player has taken, and how many times.</summary>
        public class TakenBoon
        {
            public Boon Boon;
            public int Level;
        }

        public readonly List<TakenBoon> TakenBoons = new List<TakenBoon>();

        /// <summary>On-hit effects added to every bullet. The weapon holds this exact list.</summary>
        public readonly List<StatusApplication> BulletStatuses = new List<StatusApplication>();

        /// <summary>On-hit effects added to damaging spells.</summary>
        public readonly List<StatusApplication> SpellStatuses = new List<StatusApplication>();

        /// <summary>A familiar the player has been granted, and how many times.</summary>
        public class OwnedFamiliar
        {
            public string Id;
            public int Level;
        }

        /// <summary>
        /// Familiars granted this run. They are re-summoned on entering each room, so this is
        /// the ownership record rather than the live creatures - see FamiliarSummoner.
        /// </summary>
        public readonly List<OwnedFamiliar> Familiars = new List<OwnedFamiliar>();

        /// <summary>Walking minions carried between floors, as a count of each kind that survived.</summary>
        public readonly MinionRoster Minions = new MinionRoster();

        /// <summary>Spell ids struck from this run's offers. <see cref="SpellLibrary.Offerable"/> leaves them out.</summary>
        public readonly HashSet<string> EliminatedSpells = new HashSet<string>();

        public void EliminateSpell(string id)
        {
            if (!string.IsNullOrEmpty(id)) EliminatedSpells.Add(id);
        }

        /// <summary>Grants a familiar, or raises the level of one already owned.</summary>
        public void GrantFamiliar(string id, int level)
        {
            if (string.IsNullOrEmpty(id)) return;

            for (int i = 0; i < Familiars.Count; i++)
            {
                if (Familiars[i].Id != id) continue;
                Familiars[i].Level = Mathf.Max(Familiars[i].Level, level);
                return;
            }

            Familiars.Add(new OwnedFamiliar { Id = id, Level = Mathf.Max(1, level) });
        }

        /// <summary>
        /// The run in progress, or null outside one. Saves every caller reaching through the
        /// director and null-checking it, and keeps edit-mode tooling from exploding.
        /// </summary>
        public static RunState Current =>
            GameDirector.Instance != null ? GameDirector.Instance.Run : null;

        private bool _bound;

        public RunState(int seed, int floorCount)
        {
            Seed = seed;
            Rng = new Rng(seed);
            Map = new TowerMap(floorCount, Rng);
        }

        // ---------------------------------------------------------------- wiring

        /// <summary>Hooks the run into the global combat events. Call once the player exists.</summary>
        public void Bind(PlayerRig player)
        {
            Player = player;
            if (_bound) return;
            _bound = true;

            if (player.Weapon != null) player.Weapon.ExtraStatuses = BulletStatuses;
            if (player.SpellContext != null) player.SpellContext.ExtraStatuses = SpellStatuses;

            Health.AnyDied += OnAnyDied;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            Health.AnyDied -= OnAnyDied;
        }

        /// <summary>
        /// Every enemy death is the player's kill, whoever or whatever landed the blow: a minion, a
        /// burn, a wall, another enemy's stray shot. Souls, the Blood Debt and the on-kill boons
        /// all count from here.
        /// </summary>
        public static bool CountsAsKill(Health victim) => victim != null && victim.Team == Team.Enemy;

        /// <summary>
        /// Lifesteal is for the player's own hits: gun, spells, melee, and the statuses those leave
        /// behind. Not minions, not collisions dealt on the player's behalf, and never an execute,
        /// whose damage is only whatever health happened to be left.
        /// </summary>
        public static bool GrantsLifesteal(in DamageInfo info, GameObject player)
        {
            if (player == null || info.Source != player || info.Type == DamageType.Execute) return false;

            switch (info.Origin)
            {
                case DamageOrigin.Gun:
                case DamageOrigin.Spell:
                case DamageOrigin.Melee:
                case DamageOrigin.StatusTick:
                    return true;
                default:
                    return false;
            }
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (Player == null || !CountsAsKill(victim)) return;

            Kills++;
        }

        // ---------------------------------------------------------------- convenience

        public CharacterSheet Sheet => Player != null ? Player.Sheet : null;

        /// <summary>Takes a boon, or levels it up if it has been taken before.</summary>
        public void AddBoon(Boon boon)
        {
            if (boon == null) return;

            TakenBoon entry = FindBoon(boon.Id);
            if (entry == null)
            {
                entry = new TakenBoon { Boon = boon, Level = 0 };
                TakenBoons.Add(entry);
            }

            if (entry.Level >= boon.MaxLevel) return;

            entry.Level++;
            boon.Apply(this, entry.Level);
        }

        public TakenBoon FindBoon(string id)
        {
            for (int i = 0; i < TakenBoons.Count; i++)
                if (TakenBoons[i].Boon.Id == id) return TakenBoons[i];
            return null;
        }

        public int BoonLevel(string id)
        {
            TakenBoon entry = FindBoon(id);
            return entry == null ? 0 : entry.Level;
        }

        public bool HasBoon(string id) => BoonLevel(id) > 0;

        /// <summary>The Luck stat, used for every rarity roll in the run.</summary>
        public float Luck => Sheet != null ? Sheet.GetStat(StatType.Luck) : 0f;

        public string Summary()
        {
            return string.Format("Floor {0}  -  {1} rooms cleared  -  {2} kills  -  {3:0}s",
                Floor, RoomsCleared, Kills, ElapsedSeconds);
        }
    }
}
