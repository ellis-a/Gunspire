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

            // Dash charges
            int charges = player.Motor.DashCharges;
            int max = player.Motor.MaxDashCharges;
            for (int i = 0; i < max; i++)
            {
                var pip = new Rect(x + i * 26f, y + 48f, 20f, 6f);
                bool filled = i < charges;
                float partial = (i == charges) ? player.Motor.DashRechargeFraction : 0f;

                UIStyles.Fill(pip, new Color(1f, 1f, 1f, 0.12f));
                if (filled) UIStyles.Fill(pip, new Color(0.6f, 0.95f, 1f, 0.9f));
                else if (partial > 0f)
                    UIStyles.Fill(new Rect(pip.x, pip.y, pip.width * partial, pip.height),
                        new Color(0.6f, 0.95f, 1f, 0.4f));
            }
            UIStyles.Text(new Rect(x + max * 26f + 8f, y + 44f, 120f, 14f), "SHIFT dash", UIStyles.Small);
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

            string ammo = weapon.AmmoInMagazine + " / " + weapon.Definition.MagazineSize;
            UIStyles.Text(new Rect(x, y + 24f, width, 30f), ammo, UIStyles.Right, UIStyles.AmmoColor);

            if (weapon.IsReloading)
            {
                var bar = new Rect(x + width - 150f, y + 60f, 150f, 8f);
                UIStyles.Bar(bar, weapon.ReloadProgress, UIStyles.AmmoColor, new Color(0.2f, 0.16f, 0.05f, 0.8f));
                UIStyles.Text(new Rect(x, y + 70f, width, 16f), "reloading", UIStyles.Right, UIStyles.Muted);
            }
            else
            {
                UIStyles.Text(new Rect(x, y + 60f, width, 16f),
                    weapon.Definition.Delivery == DeliveryKind.Hitscan ? "hitscan  [R] reload" : "projectile  [R] reload",
                    UIStyles.Right, UIStyles.Muted);
            }
        }

        // ---------------------------------------------------------------- spells

        private void DrawSpellSlots(PlayerRig player)
        {
            SpellBook book = player.Book;
            if (book == null) return;

            const float slotWidth = 132f;
            const float slotHeight = 54f;
            const float gap = 10f;

            float total = SpellBook.SlotCount * slotWidth + (SpellBook.SlotCount - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height - 74f;

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

                UIStyles.Text(new Rect(rect.x + 8f, rect.y + 4f, 24f, 18f), SpellBook.SlotLabels[i],
                    UIStyles.Label, spell.Tint);
                UIStyles.Text(new Rect(rect.x + 30f, rect.y + 4f, rect.width - 62f, 18f), spell.DisplayName,
                    UIStyles.Small, UIStyles.Ink);

                // Level sits top-right, tinted by rarity so a legendary reads at a glance.
                UIStyles.Text(new Rect(rect.xMax - 34f, rect.y + 4f, 28f, 18f),
                    "L" + book.GetLevel(spell), UIStyles.Right, Rarities.Tint(spell.Rarity));

                string bottom = cooldown > 0f
                    ? book.GetCooldown(i).ToString("0.0") + "s"
                    : Mathf.RoundToInt(spell.ManaCost) + " mana";
                UIStyles.Text(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 12f, 18f), bottom,
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
