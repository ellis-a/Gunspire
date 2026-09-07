using UnityEngine;

namespace WizardGun
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
                DrawCrosshair(player);
                DrawInteractPrompt(player);
            }

            DrawVitals(player);
            DrawWeapon(player);
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

        private void DrawInteractPrompt(PlayerRig player)
        {
            string prompt = player.CombatInput != null ? player.CombatInput.InteractPrompt : null;
            if (string.IsNullOrEmpty(prompt)) return;

            var rect = new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.5f + 60f, 560f, 28f);
            UIStyles.Fill(rect, UIStyles.PanelSoft);
            UIStyles.Text(rect, "[F]  " + prompt, UIStyles.Center, UIStyles.Ink);
        }

        // ---------------------------------------------------------------- bottom left

        private void DrawVitals(PlayerRig player)
        {
            const float x = 26f;
            float y = Screen.height - 116f;
            const float width = 300f;

            Health health = player.Health;
            var healthRect = new Rect(x, y, width, 20f);
            UIStyles.Bar(healthRect, health.Fraction, UIStyles.HealthColor, new Color(0.2f, 0.06f, 0.09f, 0.85f));
            UIStyles.Text(new Rect(x + 8f, y, width, 20f),
                Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.Max), UIStyles.Small, Color.white);

            Mana mana = player.Mana;
            var manaRect = new Rect(x, y + 26f, width, 14f);
            UIStyles.Bar(manaRect, mana.Fraction, UIStyles.ManaColor, new Color(0.07f, 0.09f, 0.2f, 0.85f));
            UIStyles.Text(new Rect(x + 8f, y + 25f, width, 14f),
                Mathf.CeilToInt(mana.Current) + " mana", UIStyles.Small, Color.white);

            DrawMovementSlot(player, x, y + 44f);
        }

        /// <summary>
        /// The Shift slot. The three ability shapes need three readouts: charge pips for Dash,
        /// a cooldown bar for Blink, and a live on/off state for the sustained ones.
        /// </summary>
        private static void DrawMovementSlot(PlayerRig player, float x, float y)
        {
            MovementController movement = player.Movement;
            MovementAbility ability = movement != null ? movement.Current : null;

            if (ability == null)
            {
                UIStyles.Text(new Rect(x, y, 240f, 16f), "SHIFT  empty", UIStyles.Small, UIStyles.Muted);
                return;
            }

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
                    "SHIFT " + ability.DisplayName, UIStyles.Small, UIStyles.Muted);
                return;
            }

            if (ability.IsSustained)
            {
                // Sustained abilities show what they are costing, and whether they are running.
                bool on = movement.IsActive;
                var bar = new Rect(x, y + 3f, 120f, 8f);

                float fraction = ability.MaxDuration > 0f && on
                    ? 1f - Mathf.Clamp01(movement.ActiveTime / ability.MaxDuration)
                    : (on ? 1f : 0f);

                UIStyles.Bar(bar, fraction, ability.Tint, new Color(1f, 1f, 1f, 0.10f));
                UIStyles.Text(new Rect(x + 128f, y, 260f, 16f),
                    "SHIFT " + ability.DisplayName + (on ? "  ON" : "  " + ability.CostLine()),
                    UIStyles.Small, on ? ability.Tint : UIStyles.Muted);
                return;
            }

            float cooldown = movement.CooldownFraction;
            var cooldownBar = new Rect(x, y + 3f, 120f, 8f);
            UIStyles.Bar(cooldownBar, 1f - cooldown, ability.Tint, new Color(1f, 1f, 1f, 0.10f));

            UIStyles.Text(new Rect(x + 128f, y, 260f, 16f),
                "SHIFT " + ability.DisplayName +
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

            if (!weapon.AltUnlocked)
            {
                UIStyles.Text(new Rect(x, y, width, 16f), "[RMB] " + alt.Name + "  - LOCKED",
                    UIStyles.Right, UIStyles.Muted);
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
                bool affordable = player.Mana.Has(spell.ManaCost);
                bool ready = cooldown <= 0f && affordable;

                UIStyles.Fill(rect, UIStyles.Panel);
                if (cooldown > 0f)
                    UIStyles.Fill(new Rect(rect.x, rect.yMax - rect.height * cooldown, rect.width, rect.height * cooldown),
                        new Color(0f, 0f, 0f, 0.55f));

                UIStyles.Outline(rect, ready ? spell.Tint : new Color(1f, 1f, 1f, 0.15f), ready ? 2f : 1f);

                // Icon on the left, three rows of text beside it. A slot has to read in a
                // glance mid-fight, so the art carries recognition and the text carries state.
                var iconRect = new Rect(rect.x + 7f, rect.y + 8f, 42f, 42f);
                UIStyles.Icon(iconRect, spell.Icon, spell.Tint, spell.ShortName);

                float textX = iconRect.xMax + 8f;
                float textWidth = rect.xMax - textX - 8f;

                UIStyles.Text(new Rect(textX, rect.y + 5f, 18f, 18f), SpellBook.SlotLabels[i],
                    UIStyles.Label, spell.Tint);

                // Level sits top-right, tinted by rarity so a legendary reads at a glance.
                UIStyles.Text(new Rect(rect.xMax - 32f, rect.y + 5f, 26f, 18f),
                    "L" + book.GetLevel(spell), UIStyles.Right, Rarities.Tint(spell.Rarity));

                UIStyles.Text(new Rect(textX, rect.y + 22f, textWidth, 16f), spell.DisplayName,
                    UIStyles.Small, UIStyles.Ink);

                string bottom = cooldown > 0f
                    ? book.GetCooldown(i).ToString("0.0") + "s"
                    : Mathf.RoundToInt(spell.ManaCost) + " mana";
                UIStyles.Text(new Rect(textX, rect.y + 38f, textWidth, 16f), bottom,
                    UIStyles.Small, affordable ? UIStyles.Muted : UIStyles.Warning);
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
