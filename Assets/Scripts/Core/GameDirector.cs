using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// Runs the game loop: build a room, fight through it, take a boon, choose the next door,
    /// repeat until the tower ends or the player does.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Run shape")]
        [SerializeField] private int floorCount = 8;
        [SerializeField] private int boonChoices = 3;

        /// <summary>How many spells the guaranteed first-floor reward offers.</summary>
        [SerializeField] private int starterSpellChoices = 3;

        /// <summary>
        /// How many classes the opening screen offers out of the whole roster. Fewer than the
        /// roster is the point: which two you are choosing between is itself a roll.
        /// </summary>
        [SerializeField] private int loadoutChoices = 3;

        public GameStateKind State { get; private set; } = GameStateKind.Loading;
        public RunState Run { get; private set; }
        public RoomRuntime CurrentRoom { get; private set; }
        public PlayerRig Player { get; private set; }

        public IReadOnlyList<Boon> BoonOffers => _boonOffers;
        public IReadOnlyList<Spell> SpellOffers => _spellOffers;
        public IReadOnlyList<RoomNode> RoomOffers => _roomOffers;
        public Spell PendingSpell { get; private set; }

        public string Notification { get; private set; }
        public float NotificationTimer { get; private set; }

        private readonly List<Boon> _boonOffers = new List<Boon>();
        private readonly List<Spell> _spellOffers = new List<Spell>();
        private readonly List<LoadoutDefinition> _loadoutOffers = new List<LoadoutDefinition>();
        private readonly List<RoomNode> _roomOffers = new List<RoomNode>();
        private GameObject _roomRoot;
        private GameStateKind _stateBeforePause;

        public int FloorCount => floorCount;

        private void Awake()
        {
            Instance = this;
            Layers.ConfigureCollisionMatrix();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Run?.Unbind();
        }

        private void Start()
        {
            ShowLoadoutSelect();
        }

        /// <summary>
        /// The opening screen. Nothing is built until a loadout is picked, because the choice
        /// decides the player's stats, gun and spells.
        /// </summary>
        public void ShowLoadoutSelect()
        {
            ClearRoom();

            // Rolled once here rather than read live, because OnGUI redraws every frame and a
            // roster sampled during drawing would reshuffle the cards under the cursor.
            var roster = new List<LoadoutDefinition>(LoadoutLibrary.All);

            _loadoutOffers.Clear();
            _loadoutOffers.AddRange(
                new Rng(Random.Range(0, int.MaxValue)).TakeDistinct(roster, loadoutChoices));

            SetState(GameStateKind.ChoosingLoadout);
        }

        public IReadOnlyList<LoadoutDefinition> LoadoutOffers => _loadoutOffers;

        public void ChooseLoadout(int index)
        {
            if (State != GameStateKind.ChoosingLoadout) return;
            if (index < 0 || index >= _loadoutOffers.Count) return;

            StartingLoadout.Select(_loadoutOffers[index]);
            StartRun(Random.Range(0, int.MaxValue));
        }

        private void Update()
        {
            if (NotificationTimer > 0f)
            {
                NotificationTimer -= Time.unscaledDeltaTime;
                if (NotificationTimer <= 0f) Notification = null;
            }

            if (State == GameStateKind.Playing) Run.ElapsedSeconds += Time.deltaTime;

            HandleGlobalKeys();
        }

        private void HandleGlobalKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == GameStateKind.Playing) Pause();
                else if (State == GameStateKind.Paused) Resume();
            }

            if ((State == GameStateKind.Dead || State == GameStateKind.Victory) &&
                Input.GetKeyDown(KeyCode.Return))
                Restart();
        }

        // ---------------------------------------------------------------- run lifecycle

        public void StartRun(int seed)
        {
            Run?.Unbind();
            AbilityEvents.ClearSubscribers();

            Run = new RunState(seed, floorCount);

            if (Player == null)
            {
                Player = PlayerRig.Spawn(Vector3.zero);
                Player.Health.DestroyOnDeath = false;
                Player.Health.Died += OnPlayerDied;
            }
            else
            {
                ResetPlayerForNewRun();
            }

            Run.Bind(Player);

            Run.Floor = 1;
            RoomNode first = Run.Map.ChoicesForFloor(1)[0];
            LoadRoom(first);
        }

        /// <summary>
        /// A restart reuses the existing player object, so everything the last run left behind
        /// has to come off before the opening kit goes back on.
        /// </summary>
        private void ResetPlayerForNewRun()
        {
            // Effects first, so each one pulls its own modifiers off cleanly, then wipe the
            // boon modifiers and stat gains that are left.
            Player.Status.ClearAll();
            Player.Sheet.ResetToBase();

            StartingLoadout.ApplyTo(Player);

            // Revive after the stats are back, so it fills to the correct maximum.
            Player.Health.Revive();
            Player.FullRestore();
        }

        /// <summary>Back to the opening screen, so a new run can be a different build.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            ShowLoadoutSelect();
        }

        // ---------------------------------------------------------------- rooms

        private void ClearRoom()
        {
            if (_roomRoot != null) Destroy(_roomRoot);
            _roomRoot = null;
            CurrentRoom = null;

            // Sweep anything spawned at the scene root that would otherwise follow the player
            // into the next room: shots in flight, dropped orbs, telegraphs and fading debris.
            DestroyAllOfType<Projectile>();
            DestroyAllOfType<OrbPickup>();
            DestroyAllOfType<EnemyController>();

            // Familiars are not parented to the room, so they would otherwise survive a
            // restart and pile up. LoadRoom summons a fresh set immediately after.
            FamiliarController.DespawnAll();
            DestroyAllOfType<TelegraphVisual>();
            DestroyAllOfType<FadeAndDie>();
        }

        private static void DestroyAllOfType<T>() where T : Component
        {
            T[] found = FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) Destroy(found[i].gameObject);
        }

        public void LoadRoom(RoomNode node)
        {
            ClearRoom();

            Run.CurrentNode = node;
            Run.Floor = node.Floor;

            CurrentRoom = RoomBuilder.Generate(node);
            _roomRoot = CurrentRoom.gameObject;

            // Drop the player at the room entrance, facing into the arena.
            Player.Motor.Teleport(CurrentRoom.PlayerSpawn + Vector3.up * 0.5f, preserveVelocity: false);
            Player.transform.rotation = CurrentRoom.PlayerFacing;

            if (Player.Holster != null) Player.Holster.RefillAll();
            Player.Motor.RefillDashes();
            Player.Mana.Add(Player.Mana.Max * 0.5f);

            // Familiars come back at every door. Losing one costs you the rest of the room
            // rather than the rest of the run.
            FamiliarSummoner.Resummon(Run);

            SetState(GameStateKind.Playing);
            Notify(node.Title);
        }

        public void OnRoomCleared(RoomRuntime room)
        {
            if (room != CurrentRoom) return;
            if (State != GameStateKind.Playing) return;

            Notify("Room clear - the way up is open");
        }

        /// <summary>Called by the exit portal once the player steps through.</summary>
        public void CompleteRoom()
        {
            if (State != GameStateKind.Playing || CurrentRoom == null || !CurrentRoom.IsCleared) return;

            Run.RoomsCleared++;

            // Bleeding never stops on its own, so surviving the floor is the other way out of it.
            // Without this a single unlucky bleed would follow the player the whole run.
            if (Player != null && Player.Status != null) Player.Status.Remove(StatusId.Bleed);

            if (Run.CurrentNode != null && Run.CurrentNode.Kind == RoomKind.Boss)
            {
                SetState(GameStateKind.Victory);
                return;
            }

            // Loadouts start with no spell, so the first floor's reward is a spell rather than
            // a boon. Without this a run opens with both slots empty and nothing but a gun
            // until a shrine happens to turn up, which is a long way to go on one button.
            if (Run.RoomsCleared == 1 && OfferStarterSpells()) return;

            OfferBoons();
        }

        // ---------------------------------------------------------------- choices

        /// <summary>
        /// The guaranteed first spell. Returns false if the roster somehow cannot produce an
        /// offer, so the caller falls back to the ordinary boon reward rather than swallowing
        /// the floor's reward entirely.
        /// </summary>
        private bool OfferStarterSpells()
        {
            _spellOffers.Clear();

            // Rarity is pushed down hard for this one pick. It is the spell the run is built
            // around and it arrives before any Luck has been earned, so opening on a Legendary
            // would decide the run before it started.
            _spellOffers.AddRange(SpellLibrary.OfferDistinct(
                Run.Rng, Player.Book, Run.Luck, starterSpellChoices, rarityBonus: 0.2f));

            if (_spellOffers.Count == 0) return false;

            SetState(GameStateKind.ChoosingSpell);
            return true;
        }

        public void ChooseStarterSpell(int index)
        {
            if (State != GameStateKind.ChoosingSpell) return;
            if (index < 0 || index >= _spellOffers.Count) return;

            Spell spell = _spellOffers[index];
            _spellOffers.Clear();

            // Straight into the first free slot. Every slot is empty at this point, so a slot
            // picker would be a screen asking a question with no wrong answer.
            int slot = Player.Book.FirstEmptySlot();
            Player.Book.Bind(spell, slot);

            Notify(spell.DisplayName + " bound to " + SpellBook.SlotLabels[slot], 2f);
            OfferRooms();
        }

        private void OfferBoons()
        {
            // Elite rooms push the whole rarity ladder up, which is what they are for.
            bool eliteReward = Run.CurrentNode != null && Run.CurrentNode.Kind == RoomKind.Elite;
            float rarityBonus = eliteReward ? 2.5f : 1f;

            _boonOffers.Clear();
            _boonOffers.AddRange(BoonLibrary.Offer(Run, boonChoices, rarityBonus));

            if (_boonOffers.Count == 0)
            {
                OfferRooms();
                return;
            }

            SetState(GameStateKind.ChoosingBoon);
        }

        public void ChooseBoon(int index)
        {
            if (State != GameStateKind.ChoosingBoon) return;
            if (index < 0 || index >= _boonOffers.Count) return;

            Run.AddBoon(_boonOffers[index]);
            _boonOffers.Clear();
            OfferRooms();
        }

        private void OfferRooms()
        {
            int nextFloor = Run.Floor + 1;

            if (nextFloor > floorCount)
            {
                SetState(GameStateKind.Victory);
                return;
            }

            _roomOffers.Clear();
            _roomOffers.AddRange(Run.Map.ChoicesForFloor(nextFloor));

            if (_roomOffers.Count == 1)
            {
                RoomNode only = _roomOffers[0];
                _roomOffers.Clear();
                LoadRoom(only);
                return;
            }

            SetState(GameStateKind.ChoosingRoom);
        }

        public void ChooseRoom(int index)
        {
            if (State != GameStateKind.ChoosingRoom) return;
            if (index < 0 || index >= _roomOffers.Count) return;

            RoomNode node = _roomOffers[index];
            _roomOffers.Clear();
            LoadRoom(node);
        }

        // ---------------------------------------------------------------- spell binding

        /// <summary>
        /// A spell pedestal. A spell you already know levels up on the spot; a new one opens
        /// the slot picker, because binding it costs you whatever is in that slot.
        /// </summary>
        public void OfferSpellBinding(Spell spell)
        {
            if (spell == null || State != GameStateKind.Playing) return;

            if (Player.Book.Knows(spell))
            {
                int level = Player.Book.LevelUp(spell);
                Notify(spell.DisplayName + " is now level " + level);
                return;
            }

            PendingSpell = spell;
            SetState(GameStateKind.ChoosingBoon);   // reuses the choice screen
        }

        public void BindPendingSpell(int slot)
        {
            if (PendingSpell == null) return;

            Player.Book.Bind(PendingSpell, slot);
            Notify(PendingSpell.DisplayName + " bound to " + SpellBook.SlotLabels[slot]);
            PendingSpell = null;
            SetState(GameStateKind.Playing);
        }

        public void CancelSpellBinding()
        {
            PendingSpell = null;
            SetState(GameStateKind.Playing);
        }

        // ---------------------------------------------------------------- state

        private void OnPlayerDied(DamageInfo info)
        {
            SetState(GameStateKind.Dead);
        }

        public void Pause()
        {
            if (State != GameStateKind.Playing) return;
            _stateBeforePause = State;
            SetState(GameStateKind.Paused);
        }

        public void Resume()
        {
            if (State != GameStateKind.Paused) return;
            SetState(_stateBeforePause == GameStateKind.Paused ? GameStateKind.Playing : _stateBeforePause);
        }

        private void SetState(GameStateKind next)
        {
            State = next;

            bool playing = next == GameStateKind.Playing;
            Time.timeScale = playing ? 1f : 0f;

            // The cursor still has to be freed on the opening screen, where there is no player yet.
            if (Player != null) Player.SetInputEnabled(playing);
            else PlayerLook.LockCursor(playing);
        }

        public void Notify(string message, float seconds = 2.5f)
        {
            Notification = message;
            NotificationTimer = seconds;
        }
    }
}
