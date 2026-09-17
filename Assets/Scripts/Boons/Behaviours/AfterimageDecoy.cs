using UnityEngine;

namespace Gunspire
{
    /// <summary>
    /// The decoy Afterimage leaves where a movement spell started. Enemies may target it like a minion, it fades
    /// after a few seconds, and it explodes when destroyed. Fading out is not being destroyed, so it does not explode.
    /// </summary>
    public class AfterimageDecoy : MonoBehaviour
    {
        public const float Lifetime = 6f;
        public const float MaxHealth = 60f;
        public const float BlastRadius = 4f;
        public const float BlastDamage = 35f;

        private static readonly Color Tint = new Color(0.55f, 0.8f, 1f, 0.45f);

        public Health Health { get; private set; }
        public float TimeLeft { get; private set; }
        public int BlastHits { get; private set; } = -1;

        private GameObject _owner;
        private TargetRegistry.Entry _entry;

        public static AfterimageDecoy Spawn(Vector3 position, GameObject owner)
        {
            var root = Build.Primitive(PrimitiveType.Capsule, null, "Afterimage", position + Vector3.up,
                new Vector3(0.8f, 0.9f, 0.8f), MaterialLibrary.Transparent(Tint), true, Layers.Minion);
            Layers.SetRecursively(root, Layers.Minion);

            root.AddComponent<StatusController>();
            var health = root.AddComponent<Health>();
            health.Team = Team.Player;
            health.DestroyOnDeath = Application.isPlaying;
            health.ConfigureMaxHealth(MaxHealth);

            var decoy = root.AddComponent<AfterimageDecoy>();
            decoy.Health = health;
            decoy.TimeLeft = Lifetime;
            decoy._owner = owner;
            decoy._entry = TargetRegistry.RegisterMinion(root.transform, health);
            health.Died += decoy.OnDied;
            return decoy;
        }

        private void Update() => Step(Time.deltaTime);

        /// <summary>One frame of fading. Public so tooling can step time.</summary>
        public void Step(float dt)
        {
            if (Health == null || !Health.IsAlive) return;
            TimeLeft -= dt;
            if (TimeLeft > 0f) return;

            Release();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDied(DamageInfo killer)
        {
            Release();

            DamageInfo blast = DamageInfo.Create(BlastDamage, DamageType.Energy, Team.Player, _owner);
            blast.CanCrit = false;
            blast.Origin = DamageOrigin.Mastery;
            BlastHits = Combat.Explode(transform.position, BlastRadius, blast.At(transform.position, Vector3.up),
                Layers.PlayerHitMask, 0.5f, 6f);
        }

        private void Release()
        {
            if (_entry == null) return;
            TargetRegistry.Unregister(_entry);
            _entry = null;
        }

        private void OnDestroy() => Release();
    }
}
