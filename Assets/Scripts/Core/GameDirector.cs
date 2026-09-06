using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
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

        public GameStateKind State { get; private set; } = GameStateKind.Loading;
        public RunState Run { get; private set; }
        public RoomRuntime CurrentRoom { get; private set; }
        public PlayerRig Player { get; private set; }

        public IReadOnlyList<Boon> BoonOffers => _boonOffers;
        public IReadOnlyList<RoomNode> RoomOffers => _roomOffers;
        public Spell PendingSpell { get; private set; }

        public string Notification { get; private set; }
        public float NotificationTimer { get; private set; }

        private readonly List<Boon> _boonOffers = new List<Boon>();
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
            SpellEvents.ClearSubscribers();

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

        private void ResetPlayerForNewRun()
        {
            CharacterSheet sheet = Player.Sheet;
            sheet.ResetToBase();
            for (int i = 0; i < EnumCache.Stats.Length; i++)
                sheet.SetBaseStat(EnumCache.Stats[i], 5);

            Player.Status.ClearAll();
            Player.Health.Revive();

            Player.Book.ResetBook();
            Player.Book.Bind(SpellLibrary.Get("blink"), 0);
            Player.Book.Bind(SpellLibrary.Get("cone_of_cold"), 1);
            Player.CombatInput.EquipWeapon(WeaponLibrary.Starter());
            Player.FullRestore();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            ClearRoom();
            StartRun(Random.Range(0, int.MaxValue));
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
            DestroyAllOfType<TelegraphVisual>();
            DestroyAllOfType<FadeAndDie>();
        }

        private static void DestroyAllOfType<T>() where T : Component
        {
            T[] found = FindObjectsOfType<T>();
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
            Player.transform.rotation = Quaternion.identity;

            Player.Weapon.RefillMagazine();
            Player.Motor.RefillDashes();
            Player.Mana.Add(Player.Mana.Max * 0.5f);

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

            if (Run.CurrentNode != null && Run.CurrentNode.Kind == RoomKind.Boss)
            {
                SetState(GameStateKind.Victory);
                return;
            }

            OfferBoons();
        }

        // ---------------------------------------------------------------- choices

        private void OfferBoons()
        {
            bool eliteReward = Run.CurrentNode != null && Run.CurrentNode.Kind == RoomKind.Elite;

            _boonOffers.Clear();
            _boonOffers.AddRange(BoonLibrary.Offer(Run, boonChoices, eliteReward));

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

        public void OfferSpellBinding(Spell spell)
        {
            if (spell == null || State != GameStateKind.Playing) return;
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

            if (Player != null) Player.SetInputEnabled(playing);
        }

        public void Notify(string message, float seconds = 2.5f)
        {
            Notification = message;
            NotificationTimer = seconds;
        }
    }
}
