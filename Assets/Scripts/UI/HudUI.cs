using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The in-play HUD: vitals, ammo, spell slots, active effects, and the prompts that tell
    /// the player what the world wants from them.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        private GameDirector _director;

        private void Awake() => _director = GetComponent<GameDirector>();

        private void OnGUI()
        {
            if (_director == null) return;

            PlayerRig player = _director.Player;
            if (player == null) return;

            // The opening screen owns the whole display, and after a death the player object
            // still exists, so the HUD has to stand down explicitly rather than rely on that.
            if (_director.State == GameStateKind.ChoosingLoadout) return;

            bool playing = _director.State == GameStateKind.Playing;

            if (playing)
            {
                DrawDivineKnowledge(player);
                DrawBloodScent(player);
                DrawCrosshair(player);
                DrawInteractPrompt(player);
                DrawPossession(player);
            }

            DrawVitals(player);
            DrawWeapon(player);
            DrawAllies(player);
            DrawSpellSlots(player);
            DrawStatuses(player);
            DrawRoomInfo();
            DrawNotification();
            DrawLowHealthVignette(player);
        }

        // ---------------------------------------------------------------- centre screen

        private void DrawCrosshair(PlayerRig player)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            float spread = 5f + Mathf.Clamp(player.Motor.HorizontalSpeed * 0.6f, 0f, 12f);
            var color = new Color(1f, 1f, 1f, 0.75f);

            UIStyles.Fill(new Rect(cx - 1f, cy - spread - 7f, 2f, 7f), color);
            UIStyles.Fill(new Rect(cx - 1f, cy + spread, 2f, 7f), color);
            UIStyles.Fill(new Rect(cx - spread - 7f, cy - 1f, 7f, 2f), color);
            UIStyles.Fill(new Rect(cx + spread, cy - 1f, 7f, 2f), color);
            UIStyles.Fill(new Rect(cx - 1f, cy - 1f, 2f, 2f), new Color(1f, 1f, 1f, 0.9f));
        }

        /// <summary>Health bars, attack timers and the like over enemies, as far as the Divination count reaches.</summary>
        private static void DrawDivineKnowledge(PlayerRig player)
        {
            if (player.Masteries == null) return;

            DivineKnowledgeMastery knowledge = player.Masteries.Get<DivineKnowledgeMastery>();
            if (knowledge != null) knowledge.DrawGUI(player.Camera);
        }

        private float _scentRefresh;
        private EnemyController[] _scented = new EnemyController[0];

        /// <summary>
        /// Blood Scent: a marker over every enemy, drawn in screen space so walls cannot hide it. The cheap,
        /// reliable version the design notes chose over a silhouette shader.
        /// </summary>
        private void DrawBloodScent(PlayerRig player)
        {
            if (player.Status == null || !player.Status.Has(StatusId.Scenting) || player.Camera == null) return;

            if ((_scentRefresh -= Time.unscaledDeltaTime) <= 0f)
            {
                _scentRefresh = 0.5f;
                _scented = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            }

            var color = new Color(0.9f, 0.2f, 0.2f, 0.85f);
            for (int i = 0; i < _scented.Length; i++)
            {
                EnemyController enemy = _scented[i];
                if (enemy == null || enemy.IsHidden || enemy.Health == null || !enemy.Health.IsAlive) continue;

                Vector3 screen = player.Camera.WorldToScreenPoint(enemy.transform.position + Vector3.up);
                if (screen.z <= 0f) continue;

                float size = Mathf.Clamp(260f / screen.z, 6f, 18f);
                UIStyles.Fill(new Rect(screen.x - size * 0.5f, Screen.height - screen.y - size * 0.5f, size, size), color);
            }
        }

        /// <summary>Who you are controlling and for how long, since the body on screen is not your own.</summary>
        private static void DrawPossession(PlayerRig player)
        {
            PossessionController possession = player.Possession;
            if (possession == null || !possession.IsPossessing) return;

            string name = possession.Current is EnemyController enemy ? enemy.DisplayName : "another body";
            var rect = new Rect(Screen.width * 0.5f - 220f, 108f, 440f, 24f);
            UIStyles.Fill(rect, UIStyles.PanelSoft);
            UIStyles.Text(rect, "CONTROLLING " + name.ToUpperInvariant() + "   " + possession.TimeLeft.ToString("0.0") + "s",
                UIStyles.Center, UIStyles.Accent);
        }

        private void DrawInteractPrompt(PlayerRig player)
        {
            string prompt = player.CombatInput != null ? player.CombatInput.InteractPrompt : null;
            if (string.IsNullOrEmpty(prompt)) return;

            var rect = new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.5f + 60f, 560f, 28f);
            UIStyles.Fill(rect, UIStyles.PanelSoft);
            UIStyles.Text(rect, "[" + PlayerCombat.InteractKey + "]  " + prompt, UIStyles.Center, UIStyles.Ink);
        }

        // ---------------------------------------------------------------- bottom left

        private void DrawVitals(PlayerRig player)
        {
            const float x = 26f;
            float y = Screen.height - 116f;
            const float width = 300f;

            DrawMasteryResources(player, x, y - 20f);

            Health health = player.Health;
            var healthRect = new Rect(x, y, width, 20f);
            UIStyles.Bar(healthRect, health.Fraction, UIStyles.HealthColor, new Color(0.2f, 0.06f, 0.09f, 0.85f));

            // The Blood Debt's overheal, as a strip along the top of the bar.
            if (health.Shield > 0f)
                UIStyles.Fill(new Rect(x, y, width * Mathf.Clamp01(health.Shield / Mathf.Max(1f, health.Max)), 4f),
                    new Color(0.6f, 0.85f, 1f, 0.9f));
            UIStyles.Text(new Rect(x + 8f, y, width, 20f),
                Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.Max), UIStyles.Small, Color.white);

            Mana mana = player.Mana;
            var manaRect = new Rect(x, y + 26f, width, 14f);
            UIStyles.Bar(manaRect, mana.Fraction, UIStyles.ManaColor, new Color(0.07f, 0.09f, 0.2f, 0.85f));
            UIStyles.Text(new Rect(x + 8f, y + 25f, width, 14f),
                Mathf.CeilToInt(mana.Current) + " mana", UIStyles.Small, Color.white);

            DrawMovementSlot(player, x, y + 44f);
            DrawMeleeSlot(player, x, y + 74f);
        }

        /// <summary>
        /// The melee slot. It has no key of its own to advertise, since V swings whatever is bound, so this is about
        /// knowing what you are swinging and whether it is ready.
        /// </summary>
        private static void DrawMeleeSlot(PlayerRig player, float x, float y)
        {
            PlayerCombat combat = player.CombatInput;
            Spell melee = combat != null ? combat.MeleeSpell : null;

            if (melee == null)
            {
                UIStyles.Text(new Rect(x, y, 240f, 16f), "V  empty", UIStyles.Small, UIStyles.Muted);
                return;
            }

            UIStyles.Icon(new Rect(x, y - 5f, 26f, 26f), melee.Icon, melee.Tint, melee.ShortName);
            x += 32f;

            float cooldown = combat.MeleeCooldownFraction;
            UIStyles.Bar(new Rect(x, y + 3f, 120f, 8f), 1f - cooldown, melee.Tint, new Color(1f, 1f, 1f, 0.10f));

            string label = "V " + melee.DisplayName + (melee.MaxLevel > 1 ? "  L" + combat.MeleeLevel : "");
            UIStyles.Text(new Rect(x + 128f, y, 260f, 16f),
                label + (cooldown > 0f ? "  " + combat.MeleeCooldown.ToString("0.0") + "s" : ""),
                UIStyles.Small, cooldown > 0f ? UIStyles.Muted : melee.Tint);
        }

        /// <summary>The masteries that are currencies: souls, psi charge, debt, and Arcane Warp's bonus. Only those held.</summary>
        private static void DrawMasteryResources(PlayerRig player, float x, float y)
        {
            MasteryHost host = player.Masteries;
            if (host == null) return;

            string line = "";

            SoulsMastery souls = host.Get<SoulsMastery>();
            if (souls != null && souls.Cap > 0) line += "SOULS " + souls.Souls + "/" + souls.Cap + "   ";

            PsiBladesMastery psi = host.Get<PsiBladesMastery>();
            if (psi != null && psi.Max > 0) line += "PSI " + psi.Charge.ToString("0.##") + "/" + psi.Max + "   ";

            BloodDebtMastery debt = host.Get<BloodDebtMastery>();
            if (debt != null && debt.Debt > 0.5f) line += "DEBT " + Mathf.CeilToInt(debt.Debt) + "   ";

            ArcaneWarpMastery warp = host.Get<ArcaneWarpMastery>();
            if (warp != null && warp.Rank > 0) line += "WARP +" + Mathf.RoundToInt(warp.Bonus * 100f) + "%   ";

            if (line.Length > 0) UIStyles.Text(new Rect(x, y, 460f, 18f), line, UIStyles.Small, UIStyles.Accent);
        }

        private static string ShiftLabel(Spell ability, MovementController movement)
            => "SHIFT " + ability.DisplayName + (ability.MaxLevel > 1 ? " L" + movement.Level : "");

        /// <summary>
        /// The Shift slot. The three ability shapes need three readouts: charge pips for Dash,
        /// a cooldown bar for Blink, and a live on/off state for the sustained ones.
        /// </summary>
        private static void DrawMovementSlot(PlayerRig player, float x, float y)
        {
            MovementController movement = player.Movement;
            Spell ability = movement != null ? movement.Current : null;

            if (ability == null)
            {
                UIStyles.Text(new Rect(x, y, 240f, 16f), "SHIFT  empty", UIStyles.Small, UIStyles.Muted);
                return;
            }

            // The icon sits left of the readout, so the two always-bound slots are recognised the same way as the
            // cast slots along the bottom of the screen.
            UIStyles.Icon(new Rect(x, y - 5f, 26f, 26f), ability.Icon, ability.Tint, ability.ShortName);
            x += 32f;

            if (ability.UsesDashCharges)
            {
                int charges = player.Motor.DashCharges;
                int max = player.Motor.MaxDashCharges;

                for (int i = 0; i < max; i++)
                {
                    var pip = new Rect(x + i * 26f, y + 4f, 20f, 6f);
                    float partial = i == charges ? player.Motor.DashRechargeFraction : 0f;

                    UIStyles.Fill(pip, new Color(1f, 1f, 1f, 0.12f));
                    if (i < charges) UIStyles.Fill(pip, ability.Tint);
                    else if (partial > 0f)
                        UIStyles.Fill(new Rect(pip.x, pip.y, pip.width * partial, pip.height),
                            new Color(ability.Tint.r, ability.Tint.g, ability.Tint.b, 0.4f));
                }

                UIStyles.Text(new Rect(x + max * 26f + 8f, y, 200f, 16f),
                    ShiftLabel(ability, movement), UIStyles.Small, UIStyles.Muted);
                return;
            }

            if (ability.IsSustained)
            {
                // Sustained abilities show what they are costing, and whether they are running.
                bool on = movement.IsActive;
                var bar = new Rect(x, y + 3f, 120f, 8f);

                float cap = ability.Sustain.MaxDuration;
                float fraction = cap > 0f && on
                    ? 1f - Mathf.Clamp01(movement.ActiveTime / cap)
                    : (on ? 1f : 0f);

                UIStyles.Bar(bar, fraction, ability.Tint, new Color(1f, 1f, 1f, 0.10f));
                UIStyles.Text(new Rect(x + 128f, y, 260f, 16f),
                    ShiftLabel(ability, movement) + (on ? "  ON" : "  " + ability.CostLine()),
                    UIStyles.Small, on ? ability.Tint : UIStyles.Muted);
                return;
            }

            float cooldown = movement.CooldownFraction;
            var cooldownBar = new Rect(x, y + 3f, 120f, 8f);
            UIStyles.Bar(cooldownBar, 1f - cooldown, ability.Tint, new Color(1f, 1f, 1f, 0.10f));

            UIStyles.Text(new Rect(x + 128f, y, 260f, 16f),
                ShiftLabel(ability, movement) +
                (cooldown > 0f ? "  " + movement.Cooldown.ToString("0.0") + "s" : ""),
                UIStyles.Small, cooldown > 0f ? UIStyles.Muted : ability.Tint);
        }

        // ---------------------------------------------------------------- bottom right

        private void DrawWeapon(PlayerRig player)
        {
            Weapon weapon = player.Weapon;
            if (weapon == null || weapon.Definition == null) return;

            const float width = 260f;
            float x = Screen.width - width - 26f;
            float y = Screen.height - 110f;

            UIStyles.Text(new Rect(x, y, width, 22f), weapon.Definition.DisplayName, UIStyles.Right, UIStyles.Ink);

            // Left of the ammo count, which is the block the eye already goes to.
            UIStyles.Icon(new Rect(x + width - 200f, y + 24f, 44f, 44f),
                weapon.Definition.Icon, weapon.Definition.Tint, weapon.Definition.DisplayName);

            string ammo = weapon.AmmoInMagazine + " / " + weapon.Definition.MagazineSize;
            UIStyles.Text(new Rect(x, y + 24f, width, 30f), ammo, UIStyles.Right, UIStyles.AmmoColor);

            if (weapon.IsReloading)
            {
                var bar = new Rect(x + width - 150f, y + 60f, 150f, 8f);
                UIStyles.Bar(bar, weapon.ReloadProgress, UIStyles.AmmoColor, new Color(0.2f, 0.16f, 0.05f, 0.8f));
                UIStyles.Text(new Rect(x, y + 70f, width, 16f), "reloading", UIStyles.Right, UIStyles.Muted);
            }
            else if (weapon.HasSpinUp && !weapon.IsSpunUp)
            {
                // Holding a trigger and getting nothing reads as a jam unless the wind-up is
                // visible, so it borrows the reload bar rather than inventing a second one.
                var bar = new Rect(x + width - 150f, y + 60f, 150f, 8f);
                UIStyles.Bar(bar, weapon.SpinProgress, UIStyles.Accent, new Color(0.2f, 0.16f, 0.05f, 0.8f));
                UIStyles.Text(new Rect(x, y + 70f, width, 16f), "winding up", UIStyles.Right, UIStyles.Muted);
            }
            else
            {
                UIStyles.Text(new Rect(x, y + 60f, width, 16f),
                    weapon.Definition.Delivery == DeliveryKind.Hitscan ? "hitscan  [R] reload" : "projectile  [R] reload",
                    UIStyles.Right, UIStyles.Muted);
            }

            DrawAltFire(weapon, x, y + 78f, width);
            DrawHolster(player, x, y - 24f, width);
        }

        /// <summary>
        /// The other gun, above the one in hand. Without this the pair is invisible: you can
        /// feel a swap happen but have no way to know what you are about to swap to, or that
        /// there is a free hand for the gun on the plinth in front of you.
        /// </summary>
        private void DrawHolster(PlayerRig player, float x, float y, float width)
        {
            Holster holster = player.Holster;
            if (holster == null) return;

            int other = (holster.ActiveIndex + 1) % Holster.SlotCount;
            WeaponDefinition stowed = holster.GetSlot(other);

            if (stowed == null)
            {
                UIStyles.Text(new Rect(x, y, width, 18f), "second hand empty",
                    UIStyles.Right, UIStyles.Muted);
                return;
            }

            string label = "[" + Holster.SwapKey + "/wheel]  " + stowed.DisplayName
                           + "   " + holster.AmmoIn(other) + " / " + stowed.MagazineSize;

            UIStyles.Text(new Rect(x, y, width, 18f), label, UIStyles.Right, UIStyles.Muted);
        }

        /// <summary>
        /// The right click line. Always drawn, including for guns that have none - "no alt
        /// fire" is information worth having when deciding whether to swap weapons, and a
        /// blank space would just look like the HUD forgot.
        /// </summary>
        private static void DrawAltFire(Weapon weapon, float x, float y, float width)
        {
            AltFireProfile alt = weapon.Alt;

            if (alt == null || !alt.Exists)
            {
                UIStyles.Text(new Rect(x, y, width, 16f), "[RMB] no alt fire", UIStyles.Right, UIStyles.Muted);
                return;
            }

            float cooldown = weapon.AltCooldownRemaining;
            if (cooldown > 0f)
            {
                UIStyles.Text(new Rect(x, y, width, 16f),
                    "[RMB] " + alt.Name + "  " + cooldown.ToString("0.0") + "s",
                    UIStyles.Right, UIStyles.Muted);
                return;
            }

            // Held alt fires say so while active, since nothing else on screen makes it
            // obvious that the zoom is a weapon state rather than a camera quirk.
            string label = weapon.IsFocusing ? "[RMB] " + alt.Name + "  ACTIVE" : "[RMB] " + alt.Name;
            UIStyles.Text(new Rect(x, y, width, 16f), label, UIStyles.Right,
                weapon.IsFocusing ? UIStyles.AmmoColor : UIStyles.Ink);
        }

        // ---------------------------------------------------------------- allies

        private const int MaxAllyRows = 12;
        private const float AllyPulseSeconds = 0.45f;
        private readonly List<MinionController> _allies = new List<MinionController>();

        /// <summary>
        /// A health bar for every walking ally - zombies, the companion, the monstrosity and the rest, but not familiars -
        /// stacked up the right side above the guns. A bar flashes and swells for a moment when its ally is hurt, so a
        /// horde being chewed through shows where without reading numbers.
        /// </summary>
        private void DrawAllies(PlayerRig player)
        {
            IReadOnlyList<MinionController> live = MinionController.Live;
            _allies.Clear();
            for (int i = 0; i < live.Count; i++)
                if (live[i] != null && live[i].Definition != null && live[i].Health != null) _allies.Add(live[i]);
            if (_allies.Count == 0) return;

            BeastMastery beasts = player.Masteries != null ? player.Masteries.Get<BeastMastery>() : null;
            MinionController companion = beasts != null ? beasts.Companion : null;

            // The companion first, then kind by kind. An insertion sort, because it is stable: allies of one kind keep
            // the order they were summoned in, so rows do not trade places between frames.
            for (int i = 1; i < _allies.Count; i++)
            {
                MinionController moving = _allies[i];
                int j = i - 1;
                while (j >= 0 && AllyOrder(moving, _allies[j], companion) < 0)
                {
                    _allies[j + 1] = _allies[j];
                    j--;
                }
                _allies[j + 1] = moving;
            }

            const float width = 240f;
            const float rowHeight = 18f;
            float x = Screen.width - width - 26f;
            float bottom = Screen.height - 160f;

            int shown = Mathf.Min(_allies.Count, MaxAllyRows);
            for (int i = 0; i < shown; i++)
            {
                MinionController ally = _allies[i];
                float y = bottom - (i + 1) * rowHeight;

                float since = Time.unscaledTime - ally.LastHurtTime;
                float pulse = since < AllyPulseSeconds ? 1f - since / AllyPulseSeconds : 0f;

                Color nameColor = ally.IsDown ? UIStyles.Muted : ally == companion ? UIStyles.Accent : UIStyles.Ink;
                UIStyles.Text(new Rect(x, y, 90f, rowHeight), ally.Definition.DisplayName, UIStyles.Small, nameColor);

                float barHeight = 8f + 4f * pulse;
                var bar = new Rect(x + 94f, y + (rowHeight - barHeight) * 0.5f, width - 94f, barHeight);

                if (ally.IsDown)
                {
                    UIStyles.Fill(bar, new Color(1f, 1f, 1f, 0.08f));
                    UIStyles.Text(new Rect(bar.x + 4f, y, bar.width, rowHeight), "down", UIStyles.Small, UIStyles.Muted);
                    continue;
                }

                Color fill = Color.Lerp(UIStyles.HealthColor, Color.white, pulse * 0.7f);
                Color back = Color.Lerp(new Color(0.2f, 0.06f, 0.09f, 0.85f), new Color(1f, 0.85f, 0.85f, 0.9f), pulse * 0.5f);
                UIStyles.Bar(bar, ally.Health.Fraction, fill, back);
                if (pulse > 0f) UIStyles.Outline(bar, new Color(1f, 1f, 1f, pulse));
            }

            if (_allies.Count > shown)
                UIStyles.Text(new Rect(x, bottom - (shown + 1) * rowHeight, width, rowHeight),
                    "+" + (_allies.Count - shown) + " more", UIStyles.Right, UIStyles.Muted);
        }

        private static int AllyOrder(MinionController a, MinionController b, MinionController companion)
        {
            if (a == b) return 0;
            if (a == companion) return -1;
            if (b == companion) return 1;
            return string.CompareOrdinal(a.Definition.Id, b.Definition.Id);
        }

        // ---------------------------------------------------------------- spells

        private void DrawSpellSlots(PlayerRig player)
        {
            SpellBook book = player.Book;
            if (book == null) return;

            // Wider and taller than before to make room for the icon without crushing the
            // three rows of text that sit beside it.
            const float slotWidth = 154f;
            const float slotHeight = 58f;
            const float gap = 10f;

            float total = SpellBook.SlotCount * slotWidth + (SpellBook.SlotCount - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height - 78f;

            for (int i = 0; i < SpellBook.SlotCount; i++)
            {
                var rect = new Rect(startX + i * (slotWidth + gap), y, slotWidth, slotHeight);
                Spell spell = book.GetSlot(i);

                if (spell == null)
                {
                    UIStyles.Fill(rect, UIStyles.PanelSoft);
                    UIStyles.Outline(rect, new Color(1f, 1f, 1f, 0.1f));
                    UIStyles.Text(rect, SpellBook.SlotLabels[i] + "  empty", UIStyles.Center, UIStyles.Muted);
                    continue;
                }

                float cooldown = book.GetCooldownFraction(i);
                CastOutcome state = book.Evaluate(i);
                bool affordable = state == CastOutcome.Ready || state == CastOutcome.OnCooldown;
                bool ready = state == CastOutcome.Ready;

                UIStyles.Fill(rect, UIStyles.Panel);
                if (cooldown > 0f)
                    UIStyles.Fill(new Rect(rect.x, rect.yMax - rect.height * cooldown, rect.width, rect.height * cooldown),
                        new Color(0f, 0f, 0f, 0.55f));

                UIStyles.Outline(rect, ready ? spell.Tint : new Color(1f, 1f, 1f, 0.15f), ready ? 2f : 1f);

                // Icon on the left, three rows of text beside it. A slot has to read in a
                // glance mid-fight, so the art carries recognition and the text carries state.
                var iconRect = new Rect(rect.x + 7f, rect.y + 8f, 42f, 42f);

                // A stance shows the form a press would switch to, since that is what the key does next.
                StanceMode next = book.NextStanceMode(i);
                Texture2D icon = next != null && next.Icon != null ? next.Icon : spell.Icon;
                UIStyles.Icon(iconRect, icon, next != null ? next.Tint : spell.Tint, spell.ShortName);

                float textX = iconRect.xMax + 8f;
                float textWidth = rect.xMax - textX - 8f;

                UIStyles.Text(new Rect(textX, rect.y + 5f, 18f, 18f), SpellBook.SlotLabels[i],
                    UIStyles.Label, spell.Tint);

                // Level sits top-right, tinted by rarity so a legendary reads at a glance.
                UIStyles.Text(new Rect(rect.xMax - 32f, rect.y + 5f, 26f, 18f),
                    "L" + book.GetLevel(spell), UIStyles.Right, Rarities.Tint(spell.Rarity));

                UIStyles.Text(new Rect(textX, rect.y + 22f, textWidth, 16f), spell.DisplayName,
                    UIStyles.Small, UIStyles.Ink);

                // A charge fills the slot from the bottom, the opposite way to a cooldown draining.
                float charge = book.ChargeFraction(i);
                if (charge > 0f)
                    UIStyles.Fill(new Rect(rect.x, rect.yMax - 4f, rect.width * charge, 4f), spell.Tint);

                StanceMode stance = book.ActiveStanceMode(i);
                Color bottomColor = affordable ? UIStyles.Muted : UIStyles.Warning;
                string bottom;

                if (stance != null)
                {
                    // The form you are in, then the one the icon is showing.
                    bottom = stance.Name + (next != null && next != stance ? "  >  " + next.Name : "");
                    bottomColor = stance.Tint;
                }
                else if (book.IsSustainActive(i))
                {
                    bottom = "ON";
                    bottomColor = spell.Tint;
                }
                else if (book.IsCharging(i)) bottom = "charging " + Mathf.RoundToInt(charge * 100f) + "%";
                else if (cooldown > 0f) bottom = book.GetCooldown(i).ToString("0.0") + "s";
                else bottom = spell.CostLine();

                UIStyles.Text(new Rect(textX, rect.y + 38f, textWidth, 16f), bottom, UIStyles.Small, bottomColor);
            }
        }

        // ---------------------------------------------------------------- statuses

        private void DrawStatuses(PlayerRig player)
        {
            StatusController status = player.Status;
            if (status == null || status.Active.Count == 0) return;

            const float width = 170f;
            float x = 26f;
            float y = Screen.height * 0.5f - status.Active.Count * 13f;

            for (int i = 0; i < status.Active.Count; i++)
            {
                ActiveStatus s = status.Active[i];
                var rect = new Rect(x, y + i * 26f, width, 22f);

                UIStyles.Fill(rect, UIStyles.PanelSoft);
                UIStyles.Fill(new Rect(rect.x, rect.y, rect.width * s.Normalized, 2f), s.Def.Tint);

                string label = s.Def.DisplayName + (s.Stacks > 1 ? "  x" + s.Stacks : "");
                UIStyles.Text(new Rect(rect.x + 6f, rect.y, rect.width - 40f, rect.height), label,
                    UIStyles.Small, s.Def.Tint);
                UIStyles.Text(new Rect(rect.xMax - 40f, rect.y, 34f, rect.height),
                    s.Remaining.ToString("0.0"), UIStyles.Right, UIStyles.Muted);
            }
        }

        // ---------------------------------------------------------------- top of screen

        private void DrawRoomInfo()
        {
            RunState run = _director.Run;
            if (run == null) return;

            var rect = new Rect(Screen.width * 0.5f - 220f, 14f, 440f, 22f);

            if (_director.InTraining)
            {
                DrawTrainingInfo(rect);
                return;
            }

            string node = run.CurrentNode != null ? run.CurrentNode.Title : "";
            UIStyles.Text(rect, "Floor " + run.Floor + " / " + _director.FloorCount + "   -   " + node,
                UIStyles.Center, UIStyles.Ink);

            RoomRuntime room = _director.CurrentRoom;
            if (room != null)
            {
                string state = room.IsCleared
                    ? "clear - find the portal"
                    : room.EnemiesRemaining + " enemies remaining";
                UIStyles.Text(new Rect(rect.x, rect.y + 22f, rect.width, 18f), state,
                    UIStyles.Center, room.IsCleared ? new Color(0.6f, 1f, 0.7f) : UIStyles.Muted);
            }

            UIStyles.Text(new Rect(Screen.width - 210f, 14f, 190f, 18f),
                "TAB character   ESC pause", UIStyles.Right, UIStyles.Muted);
        }

        /// <summary>The training room's banner: how to change the loadout, and the damage done lately.</summary>
        private void DrawTrainingInfo(Rect rect)
        {
            UIStyles.Text(rect, "Training Room", UIStyles.Center, UIStyles.Ink);

            TrainingRoom training = _director.CurrentRoom.GetComponent<TrainingRoom>();
            string line = "ESC  choose spells and guns";
            if (training != null && training.RecentDamage > 0f)
                line += "      last " + TrainingRoom.DamageWindow.ToString("0") + "s:  " + Mathf.RoundToInt(training.RecentDamage)
                        + " damage, " + Mathf.RoundToInt(training.RecentDps) + " per second";

            UIStyles.Text(new Rect(Screen.width * 0.5f - 320f, rect.y + 22f, 640f, 18f), line, UIStyles.Center, UIStyles.Muted);
            UIStyles.Text(new Rect(Screen.width - 210f, 14f, 190f, 18f), "TAB character   ESC pause", UIStyles.Right, UIStyles.Muted);
        }

        private void DrawNotification()
        {
            if (string.IsNullOrEmpty(_director.Notification)) return;

            float alpha = Mathf.Clamp01(_director.NotificationTimer);
            var rect = new Rect(Screen.width * 0.5f - 260f, 64f, 520f, 34f);

            UIStyles.Fill(rect, new Color(UIStyles.Panel.r, UIStyles.Panel.g, UIStyles.Panel.b, 0.8f * alpha));
            UIStyles.Text(rect, _director.Notification, UIStyles.Center,
                new Color(UIStyles.Accent.r, UIStyles.Accent.g, UIStyles.Accent.b, alpha));
        }

        private void DrawLowHealthVignette(PlayerRig player)
        {
            float fraction = player.Health.Fraction;
            if (fraction > 0.35f) return;

            float strength = Mathf.InverseLerp(0.35f, 0f, fraction) * 0.35f;
            UIStyles.Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.6f, 0.05f, 0.1f, strength * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f))));
        }
    }
}
