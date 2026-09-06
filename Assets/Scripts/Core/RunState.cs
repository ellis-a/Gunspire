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
        public readonly List<Boon> TakenBoons = new List<Boon>();

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
            SpellEvents.Cast += OnSpellCast;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
            SpellEvents.Cast -= OnSpellCast;
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

        private void OnSpellCast(Spell spell, SpellContext ctx, Vector3 position)
        {
            if (!BlinkDetonates || spell == null || spell.Id != "blink") return;

            DamageInfo template = DamageInfo.Create(BlinkDetonationDamage * ctx.SpellPower,
                DamageType.Arcane, Team.Player, ctx.Caster);
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

        public void AddBoon(Boon boon)
        {
            if (boon == null) return;
            TakenBoons.Add(boon);
            boon.Apply(this);
        }

        public bool HasBoon(string id)
        {
            for (int i = 0; i < TakenBoons.Count; i++)
                if (TakenBoons[i].Id == id) return true;
            return false;
        }

        public string Summary()
        {
            return string.Format("Floor {0}  -  {1} rooms cleared  -  {2} kills  -  {3:0}s",
                Floor, RoomsCleared, Kills, ElapsedSeconds);
        }
    }
}
