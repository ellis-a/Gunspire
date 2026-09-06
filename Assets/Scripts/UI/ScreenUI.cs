using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
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

                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 14f, rect.width - 30f, 22f),
                    Rarities.Name(boon.Rarity), UIStyles.Small, boon.RarityColor);
                UIStyles.Text(new Rect(rect.x + 18f, rect.y + 40f, rect.width - 30f, 28f),
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

            UIStyles.Text(new Rect(0f, 90f, Screen.width, 44f), spell.DisplayName, UIStyles.Title, spell.Tint);
            UIStyles.Text(new Rect(0f, 130f, Screen.width, 20f),
                Rarities.Name(spell.Rarity) + "   -   " + spell.Type + "   -   "
                + DamageTypes.Name(spell.DamageType), UIStyles.Center, Rarities.Tint(spell.Rarity));
            GUI.Label(new Rect(Screen.width * 0.5f - 280f, 152f, 560f, 60f), spell.Description, UIStyles.Wrap);

            UIStyles.Text(new Rect(0f, 210f, Screen.width, 22f), "Bind it to a slot", UIStyles.Center, UIStyles.Muted);

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
            Dim(0.8f);
            UIStyles.Text(new Rect(0f, 50f, Screen.width, 44f), "Paused", UIStyles.Title, UIStyles.Ink);

            DrawCharacterSheet(new Rect(Screen.width * 0.5f - 330f, 100f, 660f, 520f));

            var resume = new Rect(Screen.width * 0.5f - 210f, Screen.height - 120f, 200f, 40f);
            var restart = new Rect(Screen.width * 0.5f + 10f, Screen.height - 120f, 200f, 40f);

            if (UIStyles.Button(resume, "Resume  [ESC]", UIStyles.Accent)) _director.Resume();
            if (UIStyles.Button(restart, "Abandon run", UIStyles.Warning)) _director.Restart();

            UIStyles.Text(new Rect(0f, Screen.height - 66f, Screen.width, 20f),
                "WASD move   SPACE jump   SHIFT dash   LMB fire   RMB bash   R reload   Q/E spells   F interact",
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

                        UIStyles.Text(new Rect(rect.x + 20f, y, 200f, 20f), name,
                            UIStyles.Small, taken.Boon.RarityColor);
                        UIStyles.Text(new Rect(rect.x + 224f, y, rect.width - 244f, 20f),
                            taken.Boon.Description, UIStyles.Small, UIStyles.Muted);
                        y += 20f;
                        if (y > rect.yMax - 30f) break;
                    }
                }
            }
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
