using System.Collections.Generic;
using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// A room for trying things: dummies that stand still and never fight back, come back when killed and heal once left
    /// alone, a readout of recent damage, and resources kept topped up. Spells and guns are chosen from the pause screen.
    /// Reached from the opening screen, and never part of a run's route.
    /// </summary>
    public class TrainingRoom : MonoBehaviour
    {
        public const float Width = 40f;
        public const float Depth = 48f;
        public const float RespawnSeconds = 2f;
        public const float ResetAfterSeconds = 3f;
        public const float DamageWindow = 5f;

        /// <summary>Keeps mana, souls and psi full, so spells can be cast back to back. Off to test what spending does.</summary>
        public bool RefillResources = true;

        private sealed class Post
        {
            public Vector3 Home;
            public bool Elite;
            public EnemyController Dummy;
            public float RespawnIn;
            public float Quiet;
        }

        private struct Hit
        {
            public float Time;
            public float Amount;
        }

        private readonly List<Post> _posts = new List<Post>();
        private readonly List<Hit> _hits = new List<Hit>();
        private float _clock;
        private bool _subscribed;

        private static EnemyDefinition _dummy;

        /// <summary>
        /// Built here rather than in the enemy roster, so it can never turn up in a run. No attacks, no movement and no
        /// resistances, so the numbers you see are the spell's own.
        /// </summary>
        public static EnemyDefinition DummyDefinition
        {
            get
            {
                if (_dummy != null) return _dummy;

                _dummy = new EnemyDefinition
                {
                    Id = "training_dummy", DisplayName = "Training Dummy", InStandardRoster = false,
                    Health = 300f, MoveSpeed = 0f, Radius = 0.42f, BodyHeight = 1.8f, BodyWidth = 0.8f,
                    Idle = IdleActivity.Stand, Resistance = 0f, Vulnerability = 0f,
                    BodyColor = new Color(0.62f, 0.5f, 0.32f), EyeColor = new Color(1f, 0.9f, 0.55f)
                };
                return _dummy;
            }
        }

        public static RoomNode Node() => new RoomNode
        {
            Kind = RoomKind.Training,
            Floor = 1,
            Seed = 1,
            Title = "Training Room",
            Description = "Dummies that never fight back. Choose any spell or gun from the pause screen."
        };

        public static RoomRuntime Generate(RoomNode node)
        {
            var root = new GameObject("Room_Training");
            var runtime = root.AddComponent<RoomRuntime>();
            runtime.Kind = RoomKind.Training;

            RoomBuilder.BuildShell(root.transform, Width, Depth, node);
            RoomBuilder.BuildLights(root.transform, Width, Depth, node);

            // Something to hide behind, something to shoot over, and something to blink past.
            Material prop = MaterialLibrary.Lit(Palette.Prop, 0.1f);
            Build.Cube(root.transform, "TrainingWall", new Vector3(-12f, 3f, 5f), new Vector3(8f, 6f, 1f), prop,
                collider: true, layer: Layers.Level);
            Build.Cube(root.transform, "TrainingLowWall", new Vector3(12f, 0.5f, 5f), new Vector3(8f, 1f, 1f), prop,
                collider: true, layer: Layers.Level);
            Build.Cube(root.transform, "TrainingPillar", new Vector3(0f, 3f, 16f), new Vector3(2f, 6f, 2f), prop,
                collider: true, layer: Layers.Level);

            var field = root.AddComponent<NavField>();
            field.Build(Width, Depth);

            runtime.PlayerSpawn = new Vector3(0f, 0.2f, -Depth * 0.5f + 5f);
            runtime.PlayerFacing = Quaternion.identity;

            var training = root.AddComponent<TrainingRoom>();

            // A line at middle distance, a tight group for anything that spreads or chains, one behind each wall, one
            // behind the pillar, and an elite.
            for (int i = 0; i < 5; i++) training.AddPost(new Vector3(-8f + i * 4f, 0f, 10f));
            training.AddPost(new Vector3(-10f, 0f, 19f));
            training.AddPost(new Vector3(-8.6f, 0f, 20f));
            training.AddPost(new Vector3(-8.6f, 0f, 18f));
            training.AddPost(new Vector3(-12f, 0f, 8f));
            training.AddPost(new Vector3(12f, 0f, 8f));
            training.AddPost(new Vector3(0f, 0f, 21f));
            training.AddPost(new Vector3(9f, 0f, 20f), elite: true);

            training.SpawnMissing();
            return runtime;
        }

        public void AddPost(Vector3 home, bool elite = false) => _posts.Add(new Post { Home = home, Elite = elite });

        public int DummyCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _posts.Count; i++)
                    if (Alive(_posts[i].Dummy)) count++;
                return count;
            }
        }

        public float RecentDamage
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _hits.Count; i++) total += _hits[i].Amount;
                return total;
            }
        }

        /// <summary>Damage per second over the window, measured from its first hit so a short burst is not diluted.</summary>
        public float RecentDps => _hits.Count == 0 ? 0f : RecentDamage / Mathf.Max(1f, _clock - _hits[0].Time);

        private void Update() => Step(Time.deltaTime);

        private void OnDestroy() => Unbind();

        /// <summary>One frame. Update calls it; public so tooling can run the room without play mode.</summary>
        public void Step(float dt)
        {
            Subscribe();

            _clock += dt;
            while (_hits.Count > 0 && _clock - _hits[0].Time > DamageWindow) _hits.RemoveAt(0);

            for (int i = 0; i < _posts.Count; i++) StepPost(_posts[i], dt);
            if (RefillResources) Refill();
        }

        public void SpawnMissing()
        {
            for (int i = 0; i < _posts.Count; i++)
                if (!Alive(_posts[i].Dummy)) Spawn(_posts[i]);
        }

        /// <summary>Every dummy back on its spot at full health with nothing on it, and any that are down stood straight back up.</summary>
        public void ResetAll()
        {
            for (int i = 0; i < _posts.Count; i++)
            {
                Post post = _posts[i];
                if (!Alive(post.Dummy))
                {
                    Spawn(post);
                    continue;
                }

                if (post.Dummy.Status != null) post.Dummy.Status.ClearAll();
                post.Quiet = ResetAfterSeconds;
                StepPost(post, 0f);
            }

            _hits.Clear();
        }

        /// <summary>Edit mode never calls OnDestroy, so tooling calls this.</summary>
        public void Unbind()
        {
            if (!_subscribed) return;
            _subscribed = false;
            Health.AnyDamaged -= OnAnyDamaged;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            Health.AnyDamaged += OnAnyDamaged;
        }

        private void OnAnyDamaged(Health victim, DamageInfo info, float amount)
        {
            if (this == null || victim == null || amount <= 0f || victim.Team == Team.Player) return;
            if (victim.GetComponent<EnemyController>() == null) return;

            _hits.Add(new Hit { Time = _clock, Amount = amount });
        }

        private void StepPost(Post post, float dt)
        {
            if (!Alive(post.Dummy))
            {
                post.Dummy = null;
                if ((post.RespawnIn -= dt) <= 0f) Spawn(post);
                return;
            }

            EnemyController dummy = post.Dummy;
            if (dummy.IsHidden || dummy.IsPossessed)
            {
                post.Quiet = 0f;
                return;
            }

            post.Quiet += dt;
            if (post.Quiet < ResetAfterSeconds) return;

            Health health = dummy.Health;
            if (health.Current < health.Max) health.SetCurrent(health.Max);

            Vector3 moved = dummy.transform.position - post.Home;
            moved.y = 0f;
            if (moved.sqrMagnitude > 0.25f)
            {
                LandingCheck.Place(dummy.transform, post.Home + Vector3.up * 0.1f);
                dummy.transform.rotation = Facing(post.Home);
            }
        }

        private void Spawn(Post post)
        {
            EnemyController dummy = EnemyFactory.Spawn(DummyDefinition, post.Home, 1, post.Elite);
            if (dummy == null) return;

            dummy.transform.SetParent(transform, true);
            dummy.PaysNoReward = true;
            dummy.transform.rotation = Facing(post.Home);

            post.Dummy = dummy;
            post.Quiet = 0f;
            post.RespawnIn = 0f;

            dummy.Health.Damaged += (info, amount) => post.Quiet = 0f;
            dummy.Health.Died += info => post.RespawnIn = RespawnSeconds;
        }

        /// <summary>Toward where the player arrives, so every dummy opens facing you.</summary>
        private static Quaternion Facing(Vector3 home)
        {
            Vector3 toSpawn = new Vector3(0f, 0f, -Depth * 0.5f + 5f) - home;
            toSpawn.y = 0f;
            return toSpawn.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toSpawn.normalized, Vector3.up) : Quaternion.identity;
        }

        private static bool Alive(EnemyController dummy) => dummy != null && dummy.Health != null && dummy.Health.IsAlive;

        private static void Refill()
        {
            PlayerRig rig = PlayerRig.Instance;
            if (rig == null) return;

            if (rig.Mana != null && rig.Mana.Current < rig.Mana.Max) rig.Mana.Add(rig.Mana.Max - rig.Mana.Current);
            if (rig.Masteries == null) return;

            SoulsMastery souls = rig.Masteries.Get<SoulsMastery>();
            if (souls != null && souls.Souls < souls.Cap) souls.AddSouls(souls.Cap - souls.Souls);

            PsiBladesMastery psi = rig.Masteries.Get<PsiBladesMastery>();
            if (psi != null && psi.Charge < psi.Max) psi.AddBonus(psi.Max - psi.Charge);
        }
    }
}
