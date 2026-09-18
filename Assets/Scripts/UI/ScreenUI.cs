using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Full-screen interfaces: the boon draft, the room choice, spell binding, the character
    /// sheet, and the pause / run-over screens.
    /// </summary>
    public class ScreenUI : MonoBehaviour
    {
        private GameDirector _director;

        /// <summary>Abandon run has been clicked once on this pause screen, and the next click ends the run.</summary>
        private bool _confirmAbandon;

        private void Awake() => _director = GetComponent<GameDirector>();

        private void OnGUI()
        {
            if (_director == null) return;
            UIStyles.BeginScaled();

            if (_director.State != GameStateKind.Paused) _confirmAbandon = false;

            switch (_director.State)
            {
                case GameStateKind.ChoosingLoadout:
                    DrawLoadoutChoice();
                    break;

                case GameStateKind.ChoosingSpell:
                    DrawStarterSpellChoice();
                    break;

                case GameStateKind.ChoosingBoon:
                    if (_director.PendingSpell != null) DrawSpellBinding();
                    else DrawBoonChoice();
                    break;

                case GameStateKind.ChoosingRoom:
                    DrawRoomChoice();
                    break;

                case GameStateKind.Paused:
                    DrawPause();
                    break;

                case GameStateKind.Dead:
                    DrawRunOver("The tower keeps you", new Color(1f, 0.4f, 0.45f));
                    break;

                case GameStateKind.Victory:
                    DrawRunOver("The spire is yours", new Color(0.6f, 1f, 0.75f));
                    break;

                case GameStateKind.Playing:
                    if (Input.GetKey(KeyCode.Tab)) DrawCharacterSheet(new Rect(
                        UIStyles.Width * 0.5f - 330f, UIStyles.Height * 0.5f - 290f, 660f, 580f));
                    break;
            }
        }

        /// <summary>The settings panel's size. It sits beside the character sheet on the pause screen, bottom right in training.</summary>
        public const float SettingsWidth = 300f;
        public const float SettingsHeight = 236f;

        /// <summary>The settings panel: UI size, mouse sensitivity, field of view, volume and fullscreen.</summary>
        private static void DrawSettings(Rect rect)
        {
            UIStyles.Fill(rect, UIStyles.Panel);
            UIStyles.Outline(rect, new Color(1f, 1f, 1f, 0.12f));
            UIStyles.Text(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 26f), "Settings", UIStyles.Heading, UIStyles.Ink);

            const float rowHeight = 28f;
            const float rowGap = 38f;
            var row = new Rect(rect.x + 14f, rect.y + 44f, rect.width - 28f, rowHeight);

            float scale = UIStyles.UserScale;
            int step = UIStyles.Stepper(row, "UI size", Mathf.RoundToInt(scale * 100f) + "%",
                scale > UIStyles.MinUserScale + 0.001f, scale < UIStyles.MaxUserScale - 0.001f);
            if (step != 0) UIStyles.UserScale = scale + step * UIStyles.UserScaleStep;

            row.y += rowGap;
            float sensitivity = GameSettings.Sensitivity;
            step = UIStyles.Stepper(row, "Mouse sensitivity", sensitivity.ToString("0.0"),
                sensitivity > GameSettings.MinSensitivity + 0.001f, sensitivity < GameSettings.MaxSensitivity - 0.001f);
            if (step != 0) GameSettings.Sensitivity = sensitivity + step * GameSettings.SensitivityStep;

            row.y += rowGap;
            float fov = GameSettings.FieldOfView;
            step = UIStyles.Stepper(row, "Field of view", Mathf.RoundToInt(fov).ToString(),
                fov > GameSettings.MinFieldOfView + 0.001f, fov < GameSettings.MaxFieldOfView - 0.001f);
            if (step != 0) GameSettings.FieldOfView = fov + step * GameSettings.FieldOfViewStep;

            row.y += rowGap;
            float volume = GameSettings.Volume;
            step = UIStyles.Stepper(row, "Volume", Mathf.RoundToInt(volume * 100f) + "%", volume > 0.001f, volume < 0.999f);
            if (step != 0) GameSettings.Volume = volume + step * GameSettings.VolumeStep;

            row.y += rowGap;
            bool fullscreen = GameSettings.Fullscreen;
            UIStyles.Text(new Rect(row.x, row.y, row.width - 130f, rowHeight), "Display", UIStyles.Label, UIStyles.Muted);
            if (UIStyles.Button(new Rect(row.xMax - 124f, row.y, 124f, rowHeight), fullscreen ? "Fullscreen" : "Windowed",
                    UIStyles.Muted))
                GameSettings.Fullscreen = !fullscreen;
        }

        /// <summary>The controls, built from the keys the game actually reads so the line cannot fall behind them.</summary>
        private static readonly string ControlsLine =
            "WASD move   SPACE jump   SHIFT movement   LMB fire   RMB alt fire   "
            + KeyName(Holster.SwapKey) + " swap gun   "
            + KeyName(PlayerCombat.MeleeKey) + " melee   " + KeyName(PlayerCombat.ReloadKey) + " reload   "
            + string.Join("/", SpellBook.SlotLabels) + " spells   " + KeyName(PlayerCombat.InteractKey) + " interact   TAB character";

        private static string KeyName(KeyCode key) => key.ToString().ToUpperInvariant();

        private static void Dim(float alpha = 0.72f)
            => UIStyles.Fill(new Rect(0f, 0f, UIStyles.Width, UIStyles.Height), new Color(0.02f, 0.02f, 0.04f, alpha));

        // ---------------------------------------------------------------- loadout

        /// <summary>
        /// The opening screen. Nothing exists yet when this is first drawn, so it must not
        /// touch the player or the run.
        /// </summary>
        private void DrawLoadoutChoice()
        {
            Dim(0.94f);
            IReadOnlyList<LoadoutDefinition> offers = _director.LoadoutOffers;

            UIStyles.Text(new Rect(0f, 60f, UIStyles.Width, 48f), "Gunspire",
                UIStyles.Title, UIStyles.Ink);

            if (UIStyles.Button(new Rect(UIStyles.Width - 156f, 20f, 136f, 34f), "Quit game", UIStyles.Muted))
                GameSettings.Quit();
            UIStyles.Text(new Rect(0f, 110f, UIStyles.Width, 22f),
                _director.TrainingSelected
                    ? "Training room: choose a class to practise with. Any spell or gun can be picked once inside."
                    : "Choose how you climb. Click a card, or press its number.",
                UIStyles.Center, _director.TrainingSelected ? UIStyles.Accent : UIStyles.Muted);

            const float cardWidth = 320f;
            const float cardHeight = 330f;
            const float gap = 26f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = UIStyles.Width * 0.5f - total * 0.5f;
            float y = UIStyles.Height * 0.5f - cardHeight * 0.5f + 20f;

            for (int i = 0; i < offers.Count; i++)
            {
                LoadoutDefinition loadout = offers[i];
                var rect = new Rect(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                bool hover = rect.Contains(Event.current.mousePosition);

                UIStyles.Card(rect, loadout.Tint, hover);
                DrawLoadoutCard(rect, loadout, i);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _director.ChooseLoadout(i);
                if (NumberPressed(i)) _director.ChooseLoadout(i);
            }

            var training = new Rect(UIStyles.Width * 0.5f - 130f, y + cardHeight + 28f, 260f, 38f);
            if (UIStyles.Button(training, _director.TrainingSelected ? "Training room: ON  [T]" : "Training room: OFF  [T]",
                    _director.TrainingSelected ? UIStyles.Accent : UIStyles.Muted))
                _director.TrainingSelected = !_director.TrainingSelected;

            // From the event rather than Input: OnGUI runs several times a frame, and a toggle read from Input would
            // flip on every one of them.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.T)
            {
                _director.TrainingSelected = !_director.TrainingSelected;
                Event.current.Use();
            }

            UIStyles.Text(new Rect(0f, UIStyles.Height - 56f, UIStyles.Width, 20f),
                ControlsLine,
                UIStyles.Center, UIStyles.Muted);
        }

        private static void DrawLoadoutCard(Rect rect, LoadoutDefinition loadout, int index)
        {
            float x = rect.x + 18f;
            float width = rect.width - 36f;
            float y = rect.y + 16f;

            UIStyles.Text(new Rect(x, y, width, 30f), loadout.DisplayName, UIStyles.Heading, UIStyles.Ink);
            y += 34f;

            UIStyles.DrawLabel(new Rect(x, y, width, 76f), loadout.Description, UIStyles.Wrap);
            y += 82f;

            UIStyles.Fill(new Rect(x, y, width, 1f), new Color(1f, 1f, 1f, 0.12f));
            y += 10f;

            UIStyles.Text(new Rect(x, y, width, 20f), loadout.StatLine(), UIStyles.Small, UIStyles.Ink);
            y += 26f;

            WeaponDefinition gun = WeaponLibrary.Peek(loadout.WeaponId);
            if (gun != null)
            {
                UIStyles.Icon(new Rect(x, y, 38f, 38f), gun.Icon, gun.Tint, gun.DisplayName);

                UIStyles.Text(new Rect(x + 46f, y, width - 46f, 20f), gun.DisplayName,
                    UIStyles.Small, DamageTypes.Tint(gun.DamageType));
                UIStyles.Text(new Rect(x + 46f, y + 18f, width - 46f, 18f), gun.StatLine(),
                    UIStyles.Small, UIStyles.Muted);
                y += 44f;
            }

            Spell movement = SpellLibrary.Get(loadout.MovementAbilityId);
            if (movement != null)
            {
                UIStyles.Text(new Rect(x, y, 46f, 18f), "SHIFT", UIStyles.Small, movement.Tint);
                UIStyles.Text(new Rect(x + 48f, y, width - 48f, 18f),
                    movement.DisplayName + "   " + movement.CostLine(), UIStyles.Small, UIStyles.Muted);
                y += 20f;
            }

            Spell spell = SpellLibrary.Get(loadout.SpellId);
            int slot = Mathf.Clamp(loadout.SpellSlot, 0, SpellBook.SlotCount - 1);
            if (spell != null)
            {
                UIStyles.Text(new Rect(x, y, 46f, 18f), SpellBook.SlotLabels[slot],
                    UIStyles.Small, spell.Tint);
                UIStyles.Text(new Rect(x + 48f, y, width - 48f, 18f),
                    spell.DisplayName + "   " + spell.Type + " / " + DamageTypes.Name(spell.DamageType),
                    UIStyles.Small, UIStyles.Muted);
                y += 20f;
            }

            // Say plainly what fills the empty slots, rather than leaving a silent gap. With
            // no starting spell both are empty, and the first one is filled by the guaranteed
            // reward for clearing the first floor - which the card has to promise, or a
            // spell-less opening looks like the class is missing something. With no cast spells
            // in the roster at all, neither promise holds: the first floor falls back to a boon
            // and shrines have nothing to offer.
            bool castSpellsExist = SpellLibrary.ForSlot(SpellSlot.Cast).Count > 0;
            bool first = true;
            for (int i = 0; i < SpellBook.SlotCount; i++)
            {
                if (spell != null && i == slot) continue;

                UIStyles.Text(new Rect(x, y, 46f, 18f), SpellBook.SlotLabels[i], UIStyles.Small, UIStyles.Muted);
                UIStyles.Text(new Rect(x + 48f, y, width - 48f, 18f),
                    !castSpellsExist ? "empty"
                    : first && spell == null ? "chosen after the first floor"
                    : "empty - found at a Rune Shrine",
                    UIStyles.Small, UIStyles.Muted);

                first = false;
                y += 20f;
            }

            UIStyles.Text(new Rect(x, rect.yMax - 30f, width, 20f),
                "[" + (index + 1) + "]", UIStyles.Small, UIStyles.Muted);
        }

        // ---------------------------------------------------------------- boons

        private void DrawBoonChoice()
        {
            Dim();
            IReadOnlyList<Boon> offers = _director.BoonOffers;

            int picks = _director.BoonPicksLeft;
            UIStyles.Text(new Rect(0f, 70f, UIStyles.Width, 44f),
                picks > 1 ? "Choose " + picks + " boons" : "Choose a boon", UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 116f, UIStyles.Width, 22f),
                "Click a card, or press its number", UIStyles.Center, UIStyles.Muted);

            int rerolls = _director.BoonRerollsLeft;
            if (rerolls > 0)
            {
                var rerollRect = new Rect(UIStyles.Width * 0.5f - 110f, UIStyles.Height - 110f, 220f, 36f);
                if (UIStyles.Button(rerollRect, "[R] Reroll  (" + rerolls + " left)", UIStyles.Accent))
                    _director.RerollBoons();

                // From the event, like the training toggle: a key read from Input would reroll once per OnGUI pass.
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.R)
                {
                    _director.RerollBoons();
                    Event.current.Use();
                }
            }

            const float cardWidth = 300f;
            const float cardHeight = 210f;
            const float gap = 24f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = UIStyles.Width * 0.5f - total * 0.5f;
            float y = UIStyles.Height * 0.5f - cardHeight * 0.5f;

            for (int i = 0; i < offers.Count; i++)
            {
                Boon boon = offers[i];
                var rect = new Rect(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                bool hover = rect.Contains(Event.current.mousePosition);

                UIStyles.Card(rect, boon.RarityColor, hover);

                UIStyles.Icon(new Rect(rect.x + 18f, rect.y + 14f, 52f, 52f),
                    boon.Icon, boon.RarityColor, boon.Name);

                UIStyles.Text(new Rect(rect.x + 80f, rect.y + 14f, rect.width - 92f, 22f),
                    Rarities.Name(boon.Rarity), UIStyles.Small, boon.RarityColor);
                UIStyles.Text(new Rect(rect.x + 80f, rect.y + 38f, rect.width - 92f, 28f),
                    boon.Name, UIStyles.Heading, UIStyles.Ink);

                // Whether this is a first pick or another level of something you already have.
                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 68f, rect.width - 36f, 18f),
                    boon.LevelLabel(_director.Run), UIStyles.Small, UIStyles.Accent);

                UIStyles.DrawLabel(new Rect(rect.x + 18f, rect.y + 90f, rect.width - 36f, 90f),
                    boon.Description, UIStyles.Wrap);
                UIStyles.Text(new Rect(rect.x + 18f, rect.yMax - 34f, rect.width - 36f, 22f),
                    "[" + (i + 1) + "]", UIStyles.Small, UIStyles.Muted);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _director.ChooseBoon(i);
                if (NumberPressed(i)) _director.ChooseBoon(i);
            }
        }

        /// <summary>
        /// The guaranteed first spell. Its own screen rather than the boon one because the
        /// cards need different things on them - what the spell costs and what it does matters
        /// more here than rarity, since this is the only spell the player has.
        /// </summary>
        private void DrawStarterSpellChoice()
        {
            Dim();
            IReadOnlyList<Spell> offers = _director.SpellOffers;

            SpellBook book = _director.Player != null ? _director.Player.Book : null;

            UIStyles.Text(new Rect(0f, 70f, UIStyles.Width, 44f), "Choose a spell", UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 116f, UIStyles.Width, 22f),
                "A spell you have levels up; a new one takes the first free slot. Click a card, or press its number",
                UIStyles.Center, UIStyles.Muted);

            const float cardWidth = 300f;
            const float cardHeight = 230f;
            const float gap = 24f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = UIStyles.Width * 0.5f - total * 0.5f;
            float y = UIStyles.Height * 0.5f - cardHeight * 0.5f;

            Spell hovered = null;

            for (int i = 0; i < offers.Count; i++)
            {
                Spell spell = offers[i];
                var rect = new Rect(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                bool hover = rect.Contains(Event.current.mousePosition);
                if (hover) hovered = spell;

                UIStyles.Card(rect, spell.Tint, hover);

                UIStyles.Icon(new Rect(rect.x + 18f, rect.y + 14f, 56f, 56f),
                    spell.Icon, spell.Tint, spell.ShortName);

                UIStyles.Text(new Rect(rect.x + 84f, rect.y + 14f, rect.width - 96f, 22f),
                    Rarities.Name(spell.Rarity) + "  -  " + spell.Type, UIStyles.Small, spell.Tint);
                UIStyles.Text(new Rect(rect.x + 84f, rect.y + 38f, rect.width - 96f, 28f),
                    spell.DisplayName, UIStyles.Heading, UIStyles.Ink);

                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 78f, rect.width - 116f, 18f),
                    spell.CostLine(), UIStyles.Small, UIStyles.Accent);
                SchoolTag(new Rect(rect.xMax - 98f, rect.y + 77f, 80f, 19f), spell.School);

                UIStyles.DrawLabel(new Rect(rect.x + 18f, rect.y + 100f, rect.width - 36f, 100f),
                    spell.Description, UIStyles.Wrap);
                UIStyles.Text(new Rect(rect.x + 18f, rect.yMax - 34f, rect.width - 36f, 22f),
                    "[" + (i + 1) + "]", UIStyles.Small, UIStyles.Muted);
                UIStyles.Text(new Rect(rect.x + 18f, rect.yMax - 34f, rect.width - 36f, 22f),
                    StarterOutcome(book, spell), UIStyles.Right, UIStyles.Accent);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _director.ChooseStarterSpell(i);
                if (NumberPressed(i)) _director.ChooseStarterSpell(i);
            }

            // What the school behind the spell grants, since the mastery is half of what a first pick decides.
            if (hovered != null)
                DrawMasteryBox(new Rect(UIStyles.Width * 0.5f - 280f, y + cardHeight + 26f, 560f, 108f), hovered);
        }

        /// <summary>What taking a spell from the choice screen will do, for the corner of its card.</summary>
        private static string StarterOutcome(SpellBook book, Spell spell)
        {
            if (book == null) return string.Empty;
            if (book.Knows(spell)) return "Level " + book.GetLevel(spell) + " -> " + (book.GetLevel(spell) + 1);

            int slot = book.FirstEmptySlot();
            return book.GetSlot(slot) == null ? "New, binds to " + SpellBook.SlotLabels[slot] : "New, choose a slot";
        }

        // ---------------------------------------------------------------- rooms

        private void DrawRoomChoice()
        {
            Dim();
            IReadOnlyList<RoomNode> offers = _director.RoomOffers;
            int nextFloor = _director.Run != null ? _director.Run.Floor + 1 : 1;

            UIStyles.Text(new Rect(0f, 70f, UIStyles.Width, 44f),
                "Floor " + nextFloor, UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 116f, UIStyles.Width, 22f),
                "Choose your route up", UIStyles.Center, UIStyles.Muted);

            const float cardWidth = 320f;
            const float cardHeight = 190f;
            const float gap = 24f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = UIStyles.Width * 0.5f - total * 0.5f;
            float y = UIStyles.Height * 0.5f - cardHeight * 0.5f;

            for (int i = 0; i < offers.Count; i++)
            {
                RoomNode node = offers[i];
                var rect = new Rect(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                bool hover = rect.Contains(Event.current.mousePosition);

                UIStyles.Card(rect, node.Accent, hover);

                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 14f, rect.width - 30f, 22f),
                    node.Kind.ToString().ToUpperInvariant(), UIStyles.Small, node.Accent);
                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 40f, rect.width - 30f, 28f),
                    node.Title, UIStyles.Heading, UIStyles.Ink);
                UIStyles.DrawLabel(new Rect(rect.x + 18f, rect.y + 76f, rect.width - 36f, 80f),
                    node.Description, UIStyles.Wrap);
                UIStyles.Text(new Rect(rect.x + 18f, rect.yMax - 32f, rect.width - 36f, 22f),
                    "[" + (i + 1) + "]", UIStyles.Small, UIStyles.Muted);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _director.ChooseRoom(i);
                if (NumberPressed(i)) _director.ChooseRoom(i);
            }
        }

        // ---------------------------------------------------------------- spell binding

        private void DrawSpellBinding()
        {
            Dim();
            Spell spell = _director.PendingSpell;
            PlayerRig player = _director.Player;

            // Icon above the title rather than beside it: this screen is centred, and the
            // buttons below are fixed to the middle of the screen, so the header has to stay
            // narrow rather than spreading sideways.
            UIStyles.Icon(new Rect(UIStyles.Width * 0.5f - 34f, 46f, 68f, 68f),
                spell.Icon, spell.Tint, spell.ShortName);

            UIStyles.Text(new Rect(0f, 120f, UIStyles.Width, 44f), spell.DisplayName, UIStyles.Title, spell.Tint);
            UIStyles.Text(new Rect(0f, 160f, UIStyles.Width, 20f),
                Rarities.Name(spell.Rarity) + "   -   " + spell.Type + "   -   "
                + DamageTypes.Name(spell.DamageType), UIStyles.Center, Rarities.Tint(spell.Rarity));

            UIStyles.DrawLabel(new Rect(UIStyles.Width * 0.5f - 280f, 186f, 560f, 60f), spell.Description, UIStyles.Wrap);

            UIStyles.Text(new Rect(0f, 250f, UIStyles.Width, 18f), spell.CostLine(), UIStyles.Center, UIStyles.Accent);
            SchoolTag(new Rect(UIStyles.Width * 0.5f - 45f, 272f, 90f, 20f), spell.School);

            UIStyles.Text(new Rect(0f, 300f, UIStyles.Width, 22f), "Bind it to a slot", UIStyles.Center, UIStyles.Muted);

            const float buttonWidth = 240f;
            const float buttonHeight = 90f;
            const float gap = 20f;

            float total = SpellBook.SlotCount * buttonWidth + (SpellBook.SlotCount - 1) * gap;
            float startX = UIStyles.Width * 0.5f - total * 0.5f;
            float y = UIStyles.Height * 0.5f - 20f;

            for (int i = 0; i < SpellBook.SlotCount; i++)
            {
                var rect = new Rect(startX + i * (buttonWidth + gap), y, buttonWidth, buttonHeight);
                Spell current = player != null ? player.Book.GetSlot(i) : null;

                string label = SpellBook.SlotLabels[i] + "\n" +
                               (current != null
                                   ? "replaces " + current.DisplayName + " (lv " + player.Book.GetLevel(current) + ")"
                                   : "empty");

                if (UIStyles.Button(rect, label, spell.Tint)) _director.BindPendingSpell(i);
                if (Input.GetKeyDown(SpellBook.SlotKeys[i])) _director.BindPendingSpell(i);
            }

            var cancel = new Rect(UIStyles.Width * 0.5f - 90f, y + buttonHeight + 24f, 180f, 36f);
            if (UIStyles.Button(cancel, "Leave it", UIStyles.Muted)) _director.CancelSpellBinding();

            DrawMasteryBox(new Rect(UIStyles.Width * 0.5f - 280f, cancel.yMax + 22f, 560f, 108f), spell);
        }

        // ---------------------------------------------------------------- pause and run over

        private void DrawPause()
        {
            if (_director.InTraining)
            {
                DrawTrainingPicker();
                return;
            }

            Dim(0.8f);
            UIStyles.Text(new Rect(0f, 50f, UIStyles.Width, 44f), "Paused", UIStyles.Title, UIStyles.Ink);

            var sheet = new Rect(UIStyles.Width * 0.5f - 330f, 100f, 660f, 520f);
            DrawCharacterSheet(sheet);

            // Beside the sheet, pulled in from the edge on a narrow window.
            DrawSettings(new Rect(Mathf.Min(sheet.xMax + 24f, UIStyles.Width - SettingsWidth - 16f), sheet.y,
                SettingsWidth, SettingsHeight));

            const float buttonWidth = 200f;
            const float buttonGap = 20f;
            float buttonsX = UIStyles.Width * 0.5f - (buttonWidth * 3f + buttonGap * 2f) * 0.5f;
            float buttonsY = UIStyles.Height - 120f;

            if (UIStyles.Button(new Rect(buttonsX, buttonsY, buttonWidth, 40f), "Resume  [ESC]", UIStyles.Accent))
                _director.Resume();
            // Two clicks, so a stray one cannot end a run. The first arms it; leaving the pause screen disarms it.
            if (UIStyles.Button(new Rect(buttonsX + buttonWidth + buttonGap, buttonsY, buttonWidth, 40f),
                    _confirmAbandon ? "Click again to abandon" : "Abandon run", UIStyles.Warning))
            {
                if (_confirmAbandon)
                {
                    _confirmAbandon = false;
                    _director.Restart();
                }
                else _confirmAbandon = true;
            }
            if (UIStyles.Button(new Rect(buttonsX + (buttonWidth + buttonGap) * 2f, buttonsY, buttonWidth, 40f), "Quit game",
                    UIStyles.Muted))
                GameSettings.Quit();

            UIStyles.Text(new Rect(0f, UIStyles.Height - 66f, UIStyles.Width, 20f),
                ControlsLine,
                UIStyles.Center, UIStyles.Muted);

            DrawSheetHover();
        }

        private void DrawRunOver(string headline, Color color)
        {
            Dim(0.85f);
            RunState run = _director.Run;

            const float panelWidth = 760f;
            float left = UIStyles.Width * 0.5f - panelWidth * 0.5f;
            float y = 70f;

            UIStyles.Text(new Rect(0f, y, UIStyles.Width, 46f), headline, UIStyles.Title, color);
            y += 50f;

            LoadoutDefinition loadout = StartingLoadout.Selected;
            if (loadout != null)
                UIStyles.Text(new Rect(0f, y, UIStyles.Width, 22f), loadout.DisplayName, UIStyles.Center, UIStyles.Muted);
            y += 40f;

            if (run != null)
            {
                // The numbers, as a row of tiles.
                string[] labels = { "Floor", "Rooms cleared", "Kills", "Shillings earned", "Time" };
                string[] values =
                {
                    run.Floor + " / " + _director.FloorCount, run.RoomsCleared.ToString(), run.Kills.ToString(),
                    run.Wallet.TotalEarned.ToString(), FormatTime(run.ElapsedSeconds)
                };

                const float tileGap = 12f;
                float tileWidth = (panelWidth - tileGap * (labels.Length - 1)) / labels.Length;
                for (int i = 0; i < labels.Length; i++)
                {
                    var tile = new Rect(left + i * (tileWidth + tileGap), y, tileWidth, 70f);
                    UIStyles.Fill(tile, UIStyles.Panel);
                    UIStyles.Outline(tile, new Color(1f, 1f, 1f, 0.12f));
                    UIStyles.Text(new Rect(tile.x + 14f, tile.y + 10f, tile.width - 20f, 30f), values[i], UIStyles.Heading, UIStyles.Ink);
                    UIStyles.Text(new Rect(tile.x + 14f, tile.y + 42f, tile.width - 20f, 18f), labels[i], UIStyles.Small, UIStyles.Muted);
                }
                y += 90f;

                y = DrawBuild(new Rect(left, y, panelWidth, 0f));
                y = DrawTakenBoons(run, new Rect(left, y + 14f, panelWidth, 0f));
            }

            var again = new Rect(UIStyles.Width * 0.5f - 110f, Mathf.Max(y + 26f, UIStyles.Height * 0.62f), 220f, 44f);
            if (UIStyles.Button(again, "New run  [ENTER]", color)) _director.Restart();
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return total >= 3600
                ? (total / 3600) + ":" + (total / 60 % 60).ToString("00") + ":" + (total % 60).ToString("00")
                : (total / 60) + ":" + (total % 60).ToString("00");
        }

        /// <summary>The spells and guns the run ended with. Returns the y below it.</summary>
        private float DrawBuild(Rect area)
        {
            PlayerRig player = _director.Player;
            if (player == null) return area.y;

            float y = area.y;
            UIStyles.Text(new Rect(area.x, y, area.width, 24f), "Build", UIStyles.Heading, UIStyles.Ink);
            y += 30f;

            var spells = new List<string>();
            SpellBook book = player.Book;
            if (book != null)
                for (int i = 0; i < SpellBook.SlotCount; i++)
                {
                    Spell spell = book.GetSlot(i);
                    if (spell != null)
                        spells.Add(SpellBook.SlotLabels[i] + " " + Colour(spell.DisplayName, spell.Tint) + " " + book.GetSlotLevel(i));
                }
            if (player.Movement != null && player.Movement.Current != null)
                spells.Add("SHIFT " + Colour(player.Movement.Current.DisplayName, player.Movement.Current.Tint));
            if (player.CombatInput != null && player.CombatInput.MeleeSpell != null)
                spells.Add("MELEE " + Colour(player.CombatInput.MeleeSpell.DisplayName, player.CombatInput.MeleeSpell.Tint));

            var guns = new List<string>();
            if (player.Holster != null)
                for (int i = 0; i < player.Holster.SlotCount; i++)
                {
                    WeaponDefinition gun = player.Holster.GetSlot(i);
                    if (gun != null) guns.Add(gun.DisplayName);
                }

            UIStyles.Text(new Rect(area.x, y, area.width, 20f), "Spells   " + (spells.Count > 0 ? string.Join("     ", spells) : "none"),
                UIStyles.Label, UIStyles.Muted);
            y += 24f;
            UIStyles.Text(new Rect(area.x, y, area.width, 20f), "Guns     " + (guns.Count > 0 ? string.Join(",  ", guns) : "none"),
                UIStyles.Label, UIStyles.Muted);
            return y + 24f;
        }

        /// <summary>Every boon taken, coloured by rarity, with its level where it has more than one. Returns the y below it.</summary>
        private static float DrawTakenBoons(RunState run, Rect area)
        {
            float y = area.y;
            UIStyles.Text(new Rect(area.x, y, area.width, 24f), "Boons  (" + run.TakenBoons.Count + ")", UIStyles.Heading, UIStyles.Ink);
            y += 30f;

            var names = new List<string>();
            foreach (RunState.TakenBoon taken in run.TakenBoons)
                names.Add(Colour(taken.Boon.Name + (taken.Boon.MaxLevel > 1 ? " " + taken.Level : ""), taken.Boon.RarityColor));

            string text = names.Count > 0 ? string.Join(",   ", names) : "none taken";

            // Tall enough for the list: roughly how many fit on a line at this width, rounded up generously.
            int lines = Mathf.Max(1, Mathf.CeilToInt(names.Count / 5f));
            float height = Mathf.Min(lines * 18f + 8f, 200f);
            UIStyles.DrawLabel(new Rect(area.x, y, area.width, height), text, UIStyles.Wrap);
            return y + height;
        }

        private static string Colour(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        // ---------------------------------------------------------------- character sheet

        // What the pointer is over on the character sheet, drawn as a full card once everything else is down.
        private Spell _hoveredSpell;
        private RunState.TakenBoon _hoveredBoon;

        private void TrackHover(Rect row, Spell spell)
        {
            if (row.Contains(Event.current.mousePosition)) _hoveredSpell = spell;
        }

        /// <summary>
        /// The card for whatever the sheet's pointer is over, beside the pointer and kept on screen. Called last on the
        /// pause screen, so it covers the settings panel rather than sitting under it.
        /// </summary>
        private void DrawSheetHover()
        {
            const float width = 360f;
            Vector2 mouse = Event.current.mousePosition;

            if (_hoveredSpell != null)
            {
                Vector2 at = PlaceBeside(mouse, width, SpellDetailsHeight);
                DrawSpellDetails(_hoveredSpell, at.x, at.y, width);
            }
            else if (_hoveredBoon != null)
            {
                float height = BoonDetailsHeight(_hoveredBoon.Boon, width);
                Vector2 at = PlaceBeside(mouse, width, height);
                DrawBoonDetails(_hoveredBoon, new Rect(at.x, at.y, width, height));
            }
        }

        /// <summary>Right of the pointer where it fits, otherwise left of it, and never off the top or bottom.</summary>
        private static Vector2 PlaceBeside(Vector2 pointer, float width, float height)
        {
            float x = pointer.x + 20f;
            if (x + width > UIStyles.Width - 8f) x = pointer.x - width - 20f;
            float y = Mathf.Clamp(pointer.y - 20f, 8f, Mathf.Max(8f, UIStyles.Height - height - 8f));
            return new Vector2(Mathf.Max(8f, x), y);
        }

        private const float BoonDetailsTop = 84f;

        private static float BoonDetailsHeight(Boon boon, float width)
            => BoonDetailsTop + UIStyles.Wrap.CalcHeight(new GUIContent(boon.Description), width - 24f) + 14f;

        /// <summary>A taken boon's full card: name, rarity, where it belongs, its level, and the whole description.</summary>
        private static void DrawBoonDetails(RunState.TakenBoon taken, Rect rect)
        {
            Boon boon = taken.Boon;
            UIStyles.Fill(rect, UIStyles.Panel);
            UIStyles.Outline(rect, boon.RarityColor);

            UIStyles.Icon(new Rect(rect.x + 12f, rect.y + 10f, 40f, 40f), boon.Icon, boon.RarityColor, boon.Name);
            UIStyles.Text(new Rect(rect.x + 60f, rect.y + 10f, rect.width - 72f, 22f), boon.Name, UIStyles.Heading, boon.RarityColor);
            UIStyles.Text(new Rect(rect.x + 60f, rect.y + 32f, rect.width - 72f, 18f),
                Rarities.Name(boon.Rarity) + "   " + boon.Family + (string.IsNullOrEmpty(boon.Group) ? "" : " / " + boon.Group),
                UIStyles.Small, UIStyles.Muted);

            string level = boon.MaxLevel > 1 ? "Level " + taken.Level + " of " + boon.MaxLevel : "Single level";
            if (taken.Consumed) level += "   -   used up";
            UIStyles.Text(new Rect(rect.x + 12f, rect.y + 58f, rect.width - 24f, 18f), level, UIStyles.Small, UIStyles.Accent);

            UIStyles.DrawLabel(new Rect(rect.x + 12f, rect.y + BoonDetailsTop, rect.width - 24f, rect.yMax - rect.y - BoonDetailsTop - 8f),
                boon.Description, UIStyles.Wrap);
        }

        private void DrawCharacterSheet(Rect rect)
        {
            _hoveredSpell = null;
            _hoveredBoon = null;

            PlayerRig player = _director.Player;
            if (player == null) return;

            CharacterSheet sheet = player.Sheet;
            UIStyles.Fill(rect, UIStyles.Panel);
            UIStyles.Outline(rect, new Color(1f, 1f, 1f, 0.15f));

            UIStyles.Text(new Rect(rect.x + 20f, rect.y + 14f, rect.width - 40f, 26f),
                "Character", UIStyles.Heading, UIStyles.Ink);

            float y = rect.y + 50f;
            for (int i = 0; i < EnumCache.Stats.Length; i++)
            {
                StatType stat = EnumCache.Stats[i];
                Color color = StatShrine.ColorForStat(stat);

                UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 22f), stat.ToString(), UIStyles.Label, color);
                UIStyles.Text(new Rect(rect.x + 130f, y, 40f, 22f), sheet.GetStat(stat).ToString(),
                    UIStyles.Label, UIStyles.Ink);
                UIStyles.Text(new Rect(rect.x + 176f, y, rect.width - 196f, 22f),
                    sheet.DescribeStat(stat), UIStyles.Small, UIStyles.Muted);
                y += 26f;
            }

            y += 8f;
            UIStyles.Fill(new Rect(rect.x + 20f, y, rect.width - 40f, 1f), new Color(1f, 1f, 1f, 0.12f));
            y += 10f;

            // Resistances and per-school damage, which only exist once something has granted them.
            string resistances = sheet.DescribeResistances();
            if (resistances.Length > 0)
            {
                UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f), "Resist", UIStyles.Small, UIStyles.Accent);
                UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f), resistances,
                    UIStyles.Small, UIStyles.Muted);
                y += 22f;
            }

            string damage = sheet.DescribeDamageBonuses();
            if (damage.Length > 0)
            {
                UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f), "Damage", UIStyles.Small, UIStyles.Accent);
                UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f), damage,
                    UIStyles.Small, UIStyles.Muted);
                y += 22f;
            }

            // The Shift slot.
            Spell movement = player.Movement != null ? player.Movement.Current : null;
            if (movement != null)
            {
                TrackHover(new Rect(rect.x + 20f, y, rect.width - 40f, 20f), movement);
                UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f), "SHIFT  " + movement.DisplayName,
                    UIStyles.Small, movement.Tint);
                UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f),
                    movement.CostLine(), UIStyles.Small, UIStyles.Muted);
                y += 20f;
            }

            // The melee slot.
            Spell melee = player.CombatInput != null ? player.CombatInput.MeleeSpell : null;
            if (melee != null)
            {
                TrackHover(new Rect(rect.x + 20f, y, rect.width - 40f, 20f), melee);
                UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f), KeyName(PlayerCombat.MeleeKey) + "  " + melee.DisplayName,
                    UIStyles.Small, melee.Tint);
                UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f),
                    melee.CostLine(), UIStyles.Small, UIStyles.Muted);
                y += 20f;
            }

            // Spells, with their levels.
            SpellBook book = player.Book;
            if (book != null)
            {
                for (int i = 0; i < SpellBook.SlotCount; i++)
                {
                    Spell spell = book.GetSlot(i);
                    if (spell == null) continue;

                    TrackHover(new Rect(rect.x + 20f, y, rect.width - 40f, 20f), spell);
                    UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f),
                        SpellBook.SlotLabels[i] + "  " + spell.DisplayName, UIStyles.Small, spell.Tint);
                    UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f),
                        string.Format("level {0} / {1}{2}   {3}   {4}", book.GetLevel(spell), spell.MaxLevel,
                            book.SlotLevelBonus(i) > 0 ? "  (+" + book.SlotLevelBonus(i) + " from " + SpellBook.SlotLabels[i] + ")" : "",
                            spell.Type, DamageTypes.Name(spell.DamageType)),
                        UIStyles.Small, UIStyles.Muted);
                    y += 20f;
                }
            }

            y += 6f;
            UIStyles.Fill(new Rect(rect.x + 20f, y, rect.width - 40f, 1f), new Color(1f, 1f, 1f, 0.12f));
            y += 10f;

            RunState run = _director.Run;
            if (run != null)
            {
                UIStyles.Text(new Rect(rect.x + 20f, y, rect.width - 40f, 22f),
                    "Boons", UIStyles.Label, UIStyles.Accent);
                y += 24f;

                if (run.TakenBoons.Count == 0)
                {
                    UIStyles.Text(new Rect(rect.x + 20f, y, rect.width - 40f, 20f),
                        "none yet", UIStyles.Small, UIStyles.Muted);
                    y += 22f;
                }
                else
                {
                    for (int i = 0; i < run.TakenBoons.Count; i++)
                    {
                        RunState.TakenBoon taken = run.TakenBoons[i];
                        if (new Rect(rect.x + 20f, y, rect.width - 40f, 20f).Contains(Event.current.mousePosition)) _hoveredBoon = taken;
                        string name = taken.Boon.Name + (taken.Boon.MaxLevel > 1 ? "  " + taken.Level : "");

                        // Small here, because this is a scanning list rather than a card - the
                        // icon is a bullet point that happens to be recognisable.
                        UIStyles.Icon(new Rect(rect.x + 20f, y + 1f, 18f, 18f),
                            taken.Boon.Icon, taken.Boon.RarityColor, taken.Boon.Name);

                        UIStyles.Text(new Rect(rect.x + 44f, y, 180f, 20f), name,
                            UIStyles.Small, taken.Boon.RarityColor);
                        UIStyles.Text(new Rect(rect.x + 228f, y, rect.width - 248f, 20f),
                            taken.Boon.Description, UIStyles.Small, UIStyles.Muted);
                        y += 22f;
                        if (y > rect.yMax - 30f) break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- training room

        private static readonly SpellSchool[] TrainingSchools =
        {
            SpellSchool.Petty, SpellSchool.Elemental, SpellSchool.Bestial, SpellSchool.Abyssal,
            SpellSchool.Divination, SpellSchool.Death, SpellSchool.Psionic, SpellSchool.Aetherics
        };

        private SpellSchool _trainingSchool = SpellSchool.Elemental;
        private int _trainingSlot;
        private readonly List<Spell> _trainingList = new List<Spell>();

        /// <summary>
        /// The training room's pause screen: every spell by school, taken with a click, and what is equipped on the right
        /// with level and gun controls. Cast spells go to the chosen key; movement and melee spells go to their own slots.
        /// </summary>
        private void DrawTrainingPicker()
        {
            Dim(0.85f);
            PlayerRig player = _director.Player;
            if (player == null) return;

            DrawSettings(new Rect(UIStyles.Width - SettingsWidth - 16f, UIStyles.Height - SettingsHeight - 16f,
                SettingsWidth, SettingsHeight));

            float left = Mathf.Max(16f, UIStyles.Width * 0.5f - 560f);
            float top = 36f;

            UIStyles.Text(new Rect(0f, top, UIStyles.Width, 40f), "Training Room", UIStyles.Title, UIStyles.Ink);
            top += 56f;

            for (int i = 0; i < TrainingSchools.Length; i++)
            {
                SpellSchool school = TrainingSchools[i];
                var tab = new Rect(left, top + i * 38f, 150f, 32f);
                if (UIStyles.Button(tab, school.ToString(), school == _trainingSchool ? UIStyles.Accent : UIStyles.Muted))
                    _trainingSchool = school;
            }

            float listX = left + 170f;
            UIStyles.Text(new Rect(listX, top, 120f, 26f), "Cast spells go to", UIStyles.Small, UIStyles.Muted);
            for (int i = 0; i < SpellBook.SlotCount; i++)
            {
                var key = new Rect(listX + 124f + i * 54f, top, 48f, 26f);
                if (UIStyles.Button(key, SpellBook.SlotLabels[i], i == _trainingSlot ? UIStyles.Accent : UIStyles.Muted))
                    _trainingSlot = i;
            }

            // Cast spells first, then movement, then melee, each by rarity.
            _trainingList.Clear();
            foreach (Spell spell in SpellLibrary.All)
                if (spell.School == _trainingSchool) _trainingList.Add(spell);

            _trainingList.Sort((a, b) =>
                a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot)
                : a.Rarity != b.Rarity ? a.Rarity.CompareTo(b.Rarity)
                : string.CompareOrdinal(a.DisplayName, b.DisplayName));

            float panelX = listX + 440f;
            Spell hovered = null;

            for (int i = 0; i < _trainingList.Count; i++)
            {
                Spell spell = _trainingList[i];
                var row = new Rect(listX, top + 36f + i * 30f, 420f, 26f);
                if (row.Contains(Event.current.mousePosition)) hovered = spell;

                if (SpellRow(row, spell, player.Book.IsEquipped(spell))) EquipForTraining(player, spell);
            }

            if (hovered != null)
            {
                // Beside the loadout panel where the window is wide enough for a fourth column, under the list where
                // it is not.
                float cardX = panelX + 352f;
                float cardWidth = 300f;
                float cardY = top;

                if (cardX + cardWidth > UIStyles.Width - 16f)
                {
                    cardX = listX;
                    cardWidth = 420f;
                    cardY = top + 36f + _trainingList.Count * 30f + 12f;
                }

                DrawSpellDetails(hovered, cardX, cardY, cardWidth);
            }

            float y = top;

            UIStyles.Text(new Rect(panelX, y, 340f, 26f), "Equipped", UIStyles.Heading, UIStyles.Ink);
            y += 32f;
            for (int i = 0; i < SpellBook.SlotCount; i++)
                y = DrawTrainingSlot(player, panelX, y, SpellBook.SlotLabels[i], player.Book.GetSlot(i), i);
            y = DrawTrainingSlot(player, panelX, y, "SHIFT", player.Movement != null ? player.Movement.Current : null, -1);
            y = DrawTrainingSlot(player, panelX, y, "MELEE", player.CombatInput != null ? player.CombatInput.MeleeSpell : null, -1);

            y += 10f;
            UIStyles.Text(new Rect(panelX, y, 340f, 26f), "Guns", UIStyles.Heading, UIStyles.Ink);
            y += 32f;
            if (player.Holster != null)
            {
                for (int hand = 0; hand < player.Holster.SlotCount; hand++)
                {
                    WeaponDefinition gun = player.Holster.GetSlot(hand);
                    UIStyles.Text(new Rect(panelX, y, 60f, 24f), "Hand " + (hand + 1), UIStyles.Small, UIStyles.Muted);
                    if (UIStyles.Button(new Rect(panelX + 62f, y, 26f, 24f), "<", UIStyles.Muted)) CycleGun(player, hand, -1);
                    UIStyles.Text(new Rect(panelX + 92f, y, 190f, 24f), gun != null ? gun.DisplayName : "empty",
                        UIStyles.Small, gun != null ? gun.Tint : UIStyles.Muted);
                    if (UIStyles.Button(new Rect(panelX + 286f, y, 26f, 24f), ">", UIStyles.Muted)) CycleGun(player, hand, 1);
                    y += 28f;
                }
            }

            y += 10f;
            TrainingRoom training = _director.CurrentRoom != null ? _director.CurrentRoom.GetComponent<TrainingRoom>() : null;
            if (training != null)
            {
                string refill = "Refill mana, souls and psi: " + (training.RefillResources ? "ON" : "OFF");
                if (UIStyles.Button(new Rect(panelX, y, 340f, 30f), refill, training.RefillResources ? UIStyles.Accent : UIStyles.Muted))
                    training.RefillResources = !training.RefillResources;
                y += 36f;

                if (UIStyles.Button(new Rect(panelX, y, 340f, 30f), "Reset dummies", UIStyles.Muted)) training.ResetAll();
                y += 36f;
            }

            if (UIStyles.Button(new Rect(panelX, y, 340f, 30f), "Reset cooldowns", UIStyles.Muted)) player.Book.ResetCooldowns();
            y += 48f;

            if (UIStyles.Button(new Rect(panelX, y, 164f, 38f), "Resume  [ESC]", UIStyles.Accent)) _director.Resume();
            if (UIStyles.Button(new Rect(panelX + 176f, y, 164f, 38f), "Leave", UIStyles.Warning)) _director.Restart();
        }

        /// <summary>The hovered spell: what it is, what it costs, what it does, and what its school grants.</summary>
        /// <summary>A spell's full card with its school's mastery below it. The training list and the pause screen's hover.</summary>
        public const float SpellDetailsHeight = 176f + 10f + 132f;

        private void DrawSpellDetails(Spell spell, float x, float y, float width)
        {
            var panel = new Rect(x, y, width, 176f);
            UIStyles.Fill(panel, UIStyles.Panel);
            UIStyles.Outline(panel, spell.Tint);

            UIStyles.Icon(new Rect(x + 12f, y + 10f, 40f, 40f), spell.Icon, spell.Tint, spell.ShortName);
            UIStyles.Text(new Rect(x + 60f, y + 10f, width - 72f, 22f), spell.DisplayName, UIStyles.Heading, spell.Tint);
            UIStyles.Text(new Rect(x + 60f, y + 32f, width - 72f, 18f),
                Rarities.Name(spell.Rarity) + "   " + spell.Type + " / " + DamageTypes.Name(spell.DamageType),
                UIStyles.Small, UIStyles.Muted);

            UIStyles.Text(new Rect(x + 12f, y + 58f, width - 108f, 18f), spell.CostLine(), UIStyles.Small, UIStyles.Accent);
            SchoolTag(new Rect(x + width - 92f, y + 57f, 80f, 19f), spell.School);

            UIStyles.DrawLabel(new Rect(x + 12f, y + 80f, width - 24f, 88f), spell.Description, UIStyles.Wrap);

            DrawMasteryBox(new Rect(x, panel.yMax + 10f, width, 132f), spell);
        }

        /// <summary>One equipped slot: the spell, its level with buttons to change it, and for cast slots a button to empty it.</summary>
        private static float DrawTrainingSlot(PlayerRig player, float x, float y, string label, Spell spell, int castSlot)
        {
            UIStyles.Text(new Rect(x, y, 56f, 24f), label, UIStyles.Small, spell != null ? spell.Tint : UIStyles.Muted);
            UIStyles.Text(new Rect(x + 58f, y, 150f, 24f), spell != null ? spell.DisplayName : "empty",
                UIStyles.Small, spell != null ? UIStyles.Ink : UIStyles.Muted);
            if (spell == null) return y + 28f;

            int level = Mathf.Max(1, player.Book.GetLevel(spell));
            UIStyles.Text(new Rect(x + 210f, y, 50f, 24f), "L" + level + "/" + spell.MaxLevel, UIStyles.Small, Rarities.Tint(spell.Rarity));

            if (UIStyles.Button(new Rect(x + 256f, y, 26f, 24f), "-", UIStyles.Muted, level > 1))
                SetTrainingLevel(player, spell, level - 1);
            if (UIStyles.Button(new Rect(x + 286f, y, 26f, 24f), "+", UIStyles.Muted, level < spell.MaxLevel))
                SetTrainingLevel(player, spell, level + 1);
            if (castSlot >= 0 && UIStyles.Button(new Rect(x + 316f, y, 24f, 24f), "x", UIStyles.Warning))
                player.Book.Bind(null, castSlot);

            return y + 28f;
        }

        private static void SetTrainingLevel(PlayerRig player, Spell spell, int level)
        {
            player.Book.SetLevel(spell, level);

            // Dash's charges come from its level, and the controller reads them when a spell is equipped.
            if (spell.Slot == SpellSlot.Movement && player.Movement != null && player.Movement.Current == spell)
                player.Movement.Equip(spell);
        }

        private void EquipForTraining(PlayerRig player, Spell spell)
        {
            switch (spell.Slot)
            {
                case SpellSlot.Movement:
                    if (player.Movement != null) player.Movement.Equip(spell);
                    break;
                case SpellSlot.Melee:
                    if (player.CombatInput != null) player.CombatInput.EquipMelee(spell);
                    break;
                default:
                    player.Book.Bind(spell, _trainingSlot);
                    break;
            }
        }

        /// <summary>Steps a hand through every gun. A hand not in use can also be empty; the drawn one cannot.</summary>
        private static void CycleGun(PlayerRig player, int hand, int step)
        {
            IReadOnlyList<WeaponDefinition> guns = WeaponLibrary.All;
            if (guns.Count == 0) return;

            WeaponDefinition current = player.Holster.GetSlot(hand);
            int index = -1;
            for (int i = 0; i < guns.Count; i++)
                if (current != null && guns[i].Id == current.Id) index = i;

            bool mayBeEmpty = hand != player.Holster.ActiveIndex;
            int count = mayBeEmpty ? guns.Count + 1 : guns.Count;
            int position = index >= 0 ? index : mayBeEmpty ? guns.Count : 0;
            int next = ((position + step) % count + count) % count;

            player.Holster.SetSlot(hand, next < guns.Count ? guns[next] : null);
        }

        // ---------------------------------------------------------------- shared spell pieces

        /// <summary>One spell in a list: its icon, its name, and what it is. Returns true when clicked.</summary>
        private static bool SpellRow(Rect rect, Spell spell, bool equipped)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Color tint = equipped ? UIStyles.Accent : spell.Tint;

            UIStyles.Fill(rect, hover ? new Color(tint.r, tint.g, tint.b, 0.22f) : UIStyles.PanelSoft);
            UIStyles.Outline(rect, equipped ? UIStyles.Accent : new Color(1f, 1f, 1f, 0.12f));

            var icon = new Rect(rect.x + 3f, rect.y + 3f, rect.height - 6f, rect.height - 6f);
            UIStyles.Icon(icon, spell.Icon, spell.Tint, spell.ShortName);

            string slot = spell.Slot == SpellSlot.Movement ? "   SHIFT" : spell.Slot == SpellSlot.Melee ? "   MELEE" : "";
            string label = spell.DisplayName + "   " + Rarities.Name(spell.Rarity) + slot + (equipped ? "   equipped" : "");

            UIStyles.Text(new Rect(icon.xMax + 8f, rect.y, rect.width - icon.width - 16f, rect.height),
                label, UIStyles.Small, equipped ? UIStyles.Accent : UIStyles.Ink);

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        /// <summary>
        /// A small pill naming the school, in the school's own colour rather than the spell's. A spell's tint says what
        /// it does; this says what it belongs to, and the two are often different colours.
        /// </summary>
        private static void SchoolTag(Rect rect, SpellSchool school)
        {
            Color tint = Schools.Tint(school);

            UIStyles.Fill(rect, new Color(tint.r, tint.g, tint.b, 0.16f));
            UIStyles.Outline(rect, new Color(tint.r, tint.g, tint.b, 0.6f));
            UIStyles.Text(new Rect(rect.x + 7f, rect.y, rect.width - 14f, rect.height),
                school.ToString().ToUpperInvariant(), UIStyles.Small, tint);
        }

        /// <summary>
        /// What the spell's school grants and how far along it you are. A pick is half the spell and half the mastery
        /// behind it, and the mastery is the half that is invisible until it is written down somewhere.
        /// </summary>
        private void DrawMasteryBox(Rect rect, Spell spell)
        {
            if (spell == null) return;

            Color tint = Schools.Tint(spell.School);
            UIStyles.Fill(rect, UIStyles.Panel);
            UIStyles.Outline(rect, new Color(tint.r, tint.g, tint.b, 0.55f));

            string name = Schools.MasteryName(spell.School);
            float textTop = rect.y + 32f;

            if (name == null)
            {
                UIStyles.Text(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, 20f), "No mastery",
                    UIStyles.Label, UIStyles.Muted);
            }
            else
            {
                int rank = Schools.RankFor(_director.Player, spell.School);

                UIStyles.Text(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 20f), name, UIStyles.Label, tint);
                UIStyles.Text(new Rect(rect.x + 14f, rect.y + 28f, rect.width - 28f, 18f),
                    spell.School + " mastery   -   holding " + rank + " of " + Masteries.Cap,
                    UIStyles.Small, UIStyles.Muted);
                textTop = rect.y + 50f;
            }

            UIStyles.DrawLabel(new Rect(rect.x + 14f, textTop, rect.width - 28f, rect.yMax - textTop - 10f),
                Schools.MasterySummary(spell.School), UIStyles.Wrap);
        }

        private static bool NumberPressed(int index)
        {
            switch (index)
            {
                case 0: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case 1: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case 2: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                case 3: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
                default: return false;
            }
        }
    }
}
