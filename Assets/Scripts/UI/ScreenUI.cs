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

        private void Awake() => _director = GetComponent<GameDirector>();

        private void OnGUI()
        {
            if (_director == null) return;

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
                        Screen.width * 0.5f - 330f, Screen.height * 0.5f - 290f, 660f, 580f));
                    break;
            }
        }

        private static void Dim(float alpha = 0.72f)
            => UIStyles.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.02f, 0.04f, alpha));

        // ---------------------------------------------------------------- loadout

        /// <summary>
        /// The opening screen. Nothing exists yet when this is first drawn, so it must not
        /// touch the player or the run.
        /// </summary>
        private void DrawLoadoutChoice()
        {
            Dim(0.94f);
            IReadOnlyList<LoadoutDefinition> offers = _director.LoadoutOffers;

            UIStyles.Text(new Rect(0f, 60f, Screen.width, 48f), "Gunspire",
                UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 110f, Screen.width, 22f),
                _director.TrainingSelected
                    ? "Training room: choose a class to practise with. Any spell or gun can be picked once inside."
                    : "Choose how you climb. Click a card, or press its number.",
                UIStyles.Center, _director.TrainingSelected ? UIStyles.Accent : UIStyles.Muted);

            const float cardWidth = 320f;
            const float cardHeight = 330f;
            const float gap = 26f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height * 0.5f - cardHeight * 0.5f + 20f;

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

            var training = new Rect(Screen.width * 0.5f - 130f, y + cardHeight + 28f, 260f, 38f);
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

            UIStyles.Text(new Rect(0f, Screen.height - 56f, Screen.width, 20f),
                "WASD move   SPACE jump   SHIFT dash   LMB fire   RMB alt fire   V bash   R reload   Q/E spells   F interact",
                UIStyles.Center, UIStyles.Muted);
        }

        private static void DrawLoadoutCard(Rect rect, LoadoutDefinition loadout, int index)
        {
            float x = rect.x + 18f;
            float width = rect.width - 36f;
            float y = rect.y + 16f;

            UIStyles.Text(new Rect(x, y, width, 30f), loadout.DisplayName, UIStyles.Heading, UIStyles.Ink);
            y += 34f;

            GUI.Label(new Rect(x, y, width, 76f), loadout.Description, UIStyles.Wrap);
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

            UIStyles.Text(new Rect(0f, 70f, Screen.width, 44f), "Choose a boon", UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 116f, Screen.width, 22f),
                "Click a card, or press its number", UIStyles.Center, UIStyles.Muted);

            const float cardWidth = 300f;
            const float cardHeight = 210f;
            const float gap = 24f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height * 0.5f - cardHeight * 0.5f;

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

                GUI.Label(new Rect(rect.x + 18f, rect.y + 90f, rect.width - 36f, 90f),
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

            UIStyles.Text(new Rect(0f, 70f, Screen.width, 44f), "Choose your first spell",
                UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 116f, Screen.width, 22f),
                "It binds to " + SpellBook.SlotLabels[0] + ". Click a card, or press its number",
                UIStyles.Center, UIStyles.Muted);

            const float cardWidth = 300f;
            const float cardHeight = 230f;
            const float gap = 24f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height * 0.5f - cardHeight * 0.5f;

            for (int i = 0; i < offers.Count; i++)
            {
                Spell spell = offers[i];
                var rect = new Rect(startX + i * (cardWidth + gap), y, cardWidth, cardHeight);
                bool hover = rect.Contains(Event.current.mousePosition);

                UIStyles.Card(rect, spell.Tint, hover);

                UIStyles.Icon(new Rect(rect.x + 18f, rect.y + 14f, 56f, 56f),
                    spell.Icon, spell.Tint, spell.ShortName);

                UIStyles.Text(new Rect(rect.x + 84f, rect.y + 14f, rect.width - 96f, 22f),
                    Rarities.Name(spell.Rarity) + "  -  " + spell.Type, UIStyles.Small, spell.Tint);
                UIStyles.Text(new Rect(rect.x + 84f, rect.y + 38f, rect.width - 96f, 28f),
                    spell.DisplayName, UIStyles.Heading, UIStyles.Ink);

                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 78f, rect.width - 36f, 18f),
                    spell.ManaCost.ToString("0") + " mana   "
                    + spell.Cooldown.ToString("0.#") + "s cooldown",
                    UIStyles.Small, UIStyles.Accent);

                GUI.Label(new Rect(rect.x + 18f, rect.y + 100f, rect.width - 36f, 100f),
                    spell.Description, UIStyles.Wrap);
                UIStyles.Text(new Rect(rect.x + 18f, rect.yMax - 34f, rect.width - 36f, 22f),
                    "[" + (i + 1) + "]", UIStyles.Small, UIStyles.Muted);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _director.ChooseStarterSpell(i);
                if (NumberPressed(i)) _director.ChooseStarterSpell(i);
            }
        }

        // ---------------------------------------------------------------- rooms

        private void DrawRoomChoice()
        {
            Dim();
            IReadOnlyList<RoomNode> offers = _director.RoomOffers;
            int nextFloor = _director.Run != null ? _director.Run.Floor + 1 : 1;

            UIStyles.Text(new Rect(0f, 70f, Screen.width, 44f),
                "Floor " + nextFloor, UIStyles.Title, UIStyles.Ink);
            UIStyles.Text(new Rect(0f, 116f, Screen.width, 22f),
                "Choose your route up", UIStyles.Center, UIStyles.Muted);

            const float cardWidth = 320f;
            const float cardHeight = 190f;
            const float gap = 24f;

            float total = offers.Count * cardWidth + (offers.Count - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height * 0.5f - cardHeight * 0.5f;

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
                GUI.Label(new Rect(rect.x + 18f, rect.y + 76f, rect.width - 36f, 80f),
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
            UIStyles.Icon(new Rect(Screen.width * 0.5f - 34f, 46f, 68f, 68f),
                spell.Icon, spell.Tint, spell.ShortName);

            UIStyles.Text(new Rect(0f, 120f, Screen.width, 44f), spell.DisplayName, UIStyles.Title, spell.Tint);
            UIStyles.Text(new Rect(0f, 160f, Screen.width, 20f),
                Rarities.Name(spell.Rarity) + "   -   " + spell.Type + "   -   "
                + DamageTypes.Name(spell.DamageType), UIStyles.Center, Rarities.Tint(spell.Rarity));

            GUI.Label(new Rect(Screen.width * 0.5f - 280f, 186f, 560f, 60f), spell.Description, UIStyles.Wrap);

            UIStyles.Text(new Rect(0f, 244f, Screen.width, 22f), "Bind it to a slot", UIStyles.Center, UIStyles.Muted);

            const float buttonWidth = 240f;
            const float buttonHeight = 90f;
            const float gap = 20f;

            float total = SpellBook.SlotCount * buttonWidth + (SpellBook.SlotCount - 1) * gap;
            float startX = Screen.width * 0.5f - total * 0.5f;
            float y = Screen.height * 0.5f - 20f;

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

            var cancel = new Rect(Screen.width * 0.5f - 90f, y + buttonHeight + 24f, 180f, 36f);
            if (UIStyles.Button(cancel, "Leave it", UIStyles.Muted)) _director.CancelSpellBinding();
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
            UIStyles.Text(new Rect(0f, 50f, Screen.width, 44f), "Paused", UIStyles.Title, UIStyles.Ink);

            DrawCharacterSheet(new Rect(Screen.width * 0.5f - 330f, 100f, 660f, 520f));

            var resume = new Rect(Screen.width * 0.5f - 210f, Screen.height - 120f, 200f, 40f);
            var restart = new Rect(Screen.width * 0.5f + 10f, Screen.height - 120f, 200f, 40f);

            if (UIStyles.Button(resume, "Resume  [ESC]", UIStyles.Accent)) _director.Resume();
            if (UIStyles.Button(restart, "Abandon run", UIStyles.Warning)) _director.Restart();

            UIStyles.Text(new Rect(0f, Screen.height - 66f, Screen.width, 20f),
                "WASD move   SPACE jump   SHIFT dash   LMB fire   RMB alt fire   V bash   R reload   Q/E spells   F interact",
                UIStyles.Center, UIStyles.Muted);
        }

        private void DrawRunOver(string headline, Color color)
        {
            Dim(0.85f);
            RunState run = _director.Run;

            UIStyles.Text(new Rect(0f, Screen.height * 0.32f, Screen.width, 46f), headline, UIStyles.Title, color);

            if (run != null)
            {
                UIStyles.Text(new Rect(0f, Screen.height * 0.32f + 56f, Screen.width, 24f),
                    run.Summary(), UIStyles.Center, UIStyles.Ink);

                string boons = run.TakenBoons.Count == 0 ? "no boons taken" : "";
                for (int i = 0; i < run.TakenBoons.Count; i++)
                {
                    RunState.TakenBoon taken = run.TakenBoons[i];
                    boons += (i > 0 ? ",  " : "") + taken.Boon.Name
                           + (taken.Level > 1 ? " " + taken.Level : "");
                }

                GUI.Label(new Rect(Screen.width * 0.5f - 320f, Screen.height * 0.32f + 88f, 640f, 80f),
                    boons, UIStyles.Wrap);
            }

            var again = new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.62f, 220f, 44f);
            if (UIStyles.Button(again, "New run  [ENTER]", color)) _director.Restart();
        }

        // ---------------------------------------------------------------- character sheet

        private void DrawCharacterSheet(Rect rect)
        {
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
                UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f), "SHIFT  " + movement.DisplayName,
                    UIStyles.Small, movement.Tint);
                UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f),
                    movement.CostLine(), UIStyles.Small, UIStyles.Muted);
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

                    UIStyles.Text(new Rect(rect.x + 20f, y, 110f, 20f),
                        SpellBook.SlotLabels[i] + "  " + spell.DisplayName, UIStyles.Small, spell.Tint);
                    UIStyles.Text(new Rect(rect.x + 130f, y, rect.width - 150f, 20f),
                        string.Format("level {0} / {1}   {2}   {3}", book.GetLevel(spell), spell.MaxLevel,
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

            float left = Mathf.Max(16f, Screen.width * 0.5f - 560f);
            float top = 36f;

            UIStyles.Text(new Rect(0f, top, Screen.width, 40f), "Training Room", UIStyles.Title, UIStyles.Ink);
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

            for (int i = 0; i < _trainingList.Count; i++)
            {
                Spell spell = _trainingList[i];
                var row = new Rect(listX, top + 36f + i * 30f, 420f, 26f);
                bool equipped = player.Book.IsEquipped(spell);

                string slot = spell.Slot == SpellSlot.Movement ? "   SHIFT" : spell.Slot == SpellSlot.Melee ? "   MELEE" : "";
                string label = spell.DisplayName + "   " + Rarities.Name(spell.Rarity) + slot + (equipped ? "   (equipped)" : "");
                if (UIStyles.Button(row, label, equipped ? UIStyles.Accent : spell.Tint)) EquipForTraining(player, spell);
            }

            float panelX = listX + 440f;
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
                for (int hand = 0; hand < Holster.SlotCount; hand++)
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
