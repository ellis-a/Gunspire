using System;
using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>
    /// Everything that persists across the rooms of a single run: the seed, the route, the
    /// boons taken and the run-wide combat hooks those boons switch on.
    ///
    /// Boons mutate this object rather than patching weapons and spells directly, so a new
    /// upgrade usually means one flag here and one line in <see cref="BoonLibrary"/>.
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

        // ---- run-wide hooks driven by boons ----
        public float LifestealFraction;
        public float ManaOnKill;
        public float HealOnKill;
        public float CooldownReductionOnKill;
        public bool HasteOnKill;
        public bool BlinkDetonates;
        public float BlinkDetonationDamage = 45f;

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
        /// Highest alt fire unlock tier available this run. A gun whose alt fire declares a
        /// higher tier keeps it locked, which is what lets a strong gun arrive before its
        /// strong right click does. Raised by the Gunsmith boon.
        /// </summary>
        public int AltFireTier;

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

            Health.AnyDamaged += OnAnyDamaged;
            Health.AnyDied += OnAnyDied;
            AbilityEvents.Used += OnAbilityUsed;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
            AbilityEvents.Used -= OnAbilityUsed;
        }

        private bool CameFromPlayer(in DamageInfo info)
            => info.SourceTeam == Team.Player && Player != null;

        private void OnAnyDamaged(Health victim, DamageInfo info, float amount)
        {
            if (victim == null || victim.Team == Team.Player) return;
            if (!CameFromPlayer(info) || LifestealFraction <= 0f) return;

            Player.Health.Heal(amount * LifestealFraction, silent: true);
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (victim == null || victim.Team != Team.Enemy) return;
            if (!CameFromPlayer(info)) return;

            Kills++;

            if (ManaOnKill > 0f) Player.Mana.Add(ManaOnKill);
            if (HealOnKill > 0f) Player.Health.Heal(HealOnKill);
            if (CooldownReductionOnKill > 0f) Player.Book.ReduceCooldowns(CooldownReductionOnKill);
            if (HasteOnKill && Player.Status != null)
                Player.Status.Apply(StatusLibrary.Haste(3f, 1, 0.10f), Player.gameObject, Team.Player);
        }

        /// <summary>Blink lives on the movement slot now, so this keys off the ability id.</summary>
        private void OnAbilityUsed(string abilityId, AbilityContext ctx, Vector3 position)
        {
            if (!BlinkDetonates || abilityId != "blink") return;

            DamageInfo template = DamageInfo.Create(BlinkDetonationDamage * ctx.Power,
                DamageType.Astral, Team.Player, ctx.Caster);
            template.CanCrit = false;
            template = template.WithStatuses(SpellStatuses);

            Combat.Explode(position + Vector3.up * 0.9f, 5.5f, template, Layers.PlayerHitMask, 0.4f, 6f);

            var color = new Color(Palette.Arcane.r, Palette.Arcane.g, Palette.Arcane.b, 0.45f);
            GameObject pop = Build.Sphere(null, "BlinkBoom", position + Vector3.up * 0.9f, 2.5f,
                MaterialLibrary.Transparent(color), collider: false);
            FadeAndDie.Attach(pop, 0.3f, color, Vector3.one * 8f);
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
